using System;
using System.Collections.Generic;
using System.Threading;
using Photon.Realtime;
using Photon.Voice;
using Photon.Voice.Unity;
using UnityEngine;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Voice;
using IVoiceTransport = Wreckabulary.Voice.IVoiceTransport;
using UnityMicrophone = Wreckabulary.Voice.UnityMicrophone;

namespace Wreckabulary.Online
{
    public sealed class PhotonVoiceTransport : IVoiceMicTransport, IDisposable, IMatchmakingCallbacks
    {
        public const string Idle = "Voice chat connects when you play online with others.",
            Connecting = "Connecting voice chat…", Connected = "Voice chat is connected.",
            Retrying = "Voice chat couldn't connect. Trying again…", NoChannel = "Voice chat isn't available here.",
            WrongApp = "Voice chat isn't set up: the Photon app must be a Voice app.";
        public const float SpeakingLevel = .01f;
        public const int SpeakingHoldMs = 300;
        public const double MicHoldSeconds = 8;
        public const int PlayDelayLowMs = 120, PlayDelayHighMs = 200, PlayDelayMaxMs = 600, FecInterval = 4, FrameMs = 20;
        static readonly double[] RetrySeconds = { 2, 5, 10, 20, 30 };

        sealed class Remote
        {
            public readonly string PlayerId;
            public readonly Speaker Speaker;
            public readonly AudioSource Source;
            public int Frames;
            public int LoudAt = Environment.TickCount - 60000;

            public Remote(string playerId, Speaker speaker, AudioSource source)
            {
                PlayerId = playerId;
                Speaker = speaker;
                Source = source;
            }

            public void Decoded(FrameOut<float> frame)
            {
                var buf = frame?.Buf;
                if (buf == null || buf.Length == 0) return;
                Interlocked.Increment(ref Frames);
                int now = Environment.TickCount;
                if (now - Volatile.Read(ref LoudAt) < SpeakingHoldMs / 3) return;
                double sum = 0;
                for (int i = 0; i < buf.Length; i++) sum += buf[i] * buf[i];
                if (Math.Sqrt(sum / buf.Length) >= SpeakingLevel) Volatile.Write(ref LoudAt, now);
            }
        }

        readonly PhotonVoiceOptions options;
        readonly IMicrophoneSource microphone;
        readonly HashSet<string> muted = new(StringComparer.Ordinal);
        readonly List<Remote> remotes = new();
        readonly Dictionary<string, bool> reported = new(StringComparer.Ordinal);
        readonly HashSet<string> speakingNow = new(StringComparer.Ordinal);
        readonly List<string> stopped = new();
        GameObject host;
        WordboundVoiceConnection connection;
        Recorder recorder;
        WordboundVoiceTuning tuning;
        VoiceRoom room = VoiceRoom.Empty;
        string self = "", wantedRoom, wantedRegion, connectedRegion, joinedRoom, status = Idle, device = "";
        bool wantSend, receiving = true, available, connecting, joinRequested, selfSpeaking, disposed, micKnown;
        MicMode micMode = MicMode.OpenMic;
        MicState micState = MicState.Off;
        float volume = 1f;
        int capacity, failures, liveReports;
        double retryAt, micUntil;

        public PhotonVoiceTransport(PhotonVoiceOptions options, IMicrophoneSource microphone = null)
        {
            this.options = options ?? throw new ArgumentNullException(nameof(options));
            if (!PhotonVoiceSetup.IsAppId(options.AppId)) throw new ArgumentException("A Photon Voice App ID is required.", nameof(options));
            this.microphone = microphone ?? UnityMicrophone.Shared;
        }

        public bool Available => available;
        public string Status => status;
        public bool Sending => joinedRoom != null && recorder && recorder.TransmitEnabled;
        public string RoomName => joinedRoom;
        public string Region => joinedRoom != null ? connectedRegion : null;

        public float PlaybackVolume
        {
            get => volume;
            set
            {
                volume = float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
                ApplyAll();
            }
        }

        public float InputLevel =>
            recorder && recorder.RecordingEnabled && recorder.LevelMeter != null ? Mathf.Clamp01(recorder.LevelMeter.CurrentPeakAmp) : 0f;

        public IReadOnlyList<string> Devices => microphone.Devices;

        public string Device
        {
            get => device;
            set
            {
                device = value ?? "";
                if (recorder) recorder.MicrophoneDevice = DeviceFor(device);
            }
        }

        public event Action<string, bool> SpeakingChanged;
        public event Action Changed;

        public void SetSending(bool on) => wantSend = on;

        public void SetMic(MicMode mode, MicState state)
        {
            micKnown = true;
            micMode = mode;
            micState = state;
            if (recorder) recorder.VoiceDetection = VoiceRules.DetectsVoice(mode);
        }

        public int FramesSent => connection ? connection.VoiceClient.FramesSent : 0;
        public int FramesReceived => connection ? connection.VoiceClient.FramesReceived : 0;
        public int FramesLost => connection ? connection.VoiceClient.FramesLost : 0;
        public int FramesRecovered => connection ? connection.VoiceClient.FramesRecovered : 0;
        public int FecAttempts => connection ? connection.VoiceClient.FramesTryFEC : 0;
        public int AppliedFec => tuning ? tuning.AppliedFec : 0;

        public int LagMs(string playerId)
        {
            int lag = -1;
            foreach (var remote in remotes)
                if (remote.PlayerId == playerId && remote.Speaker && remote.Speaker.IsPlaying) lag = Math.Max(lag, remote.Speaker.Lag);
            return lag;
        }

        public void SetReceiving(bool on)
        {
            receiving = on;
            ApplyAll();
        }

        public void SetMuted(string playerId, bool mute)
        {
            if (playerId == null) return;
            if (mute) muted.Add(playerId); else muted.Remove(playerId);
            ApplyAll();
        }

        public void SetRoom(VoiceRoom next, string me)
        {
            if (disposed) return;
            room = next ?? VoiceRoom.Empty;
            self = me ?? "";
            wantedRoom = VoiceChannel.RoomName(room, self);
            wantedRegion = wantedRoom == null ? null : room.Channel.Region;
            capacity = VoiceChannel.Capacity(room, self);
            failures = 0;
            retryAt = 0;
            ApplyAll();
            if (wantedRoom == null)
            {
                SetState(false, room.Scope != VoiceScope.Nobody && !room.Channel.IsSet ? NoChannel : Idle);
                if (connection) Drive(Now);
                return;
            }
            Ensure();
            if (recorder && !Equals(recorder.UserData, self)) recorder.UserData = self;
            if (!available) SetState(false, Connecting);
        }

        public int FramesFrom(string playerId)
        {
            int total = 0;
            foreach (var remote in remotes)
                if (remote.PlayerId == playerId) total += Volatile.Read(ref remote.Frames);
            return total;
        }

        public bool Audible(string playerId)
        {
            foreach (var remote in remotes)
                if (remote.PlayerId == playerId && remote.Source && !remote.Source.mute) return true;
            return false;
        }

        public void Pump()
        {
            if (disposed || !connection) return;
            double now = Now;
            Drive(now);
            ApplyMic(now);
            TrackSpeaking();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (connection)
            {
                connection.Owner = null;
                connection.Client.RemoveCallbackTarget(this);
                connection.SpeakerLinked -= OnLinked;
            }
            remotes.Clear();
            if (host)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(host);
                else UnityEngine.Object.DestroyImmediate(host);
            }
            host = null;
            connection = null;
            recorder = null;
            tuning = null;
            available = false;
        }

        static double Now => Time.realtimeSinceStartupAsDouble;

        static DeviceInfo DeviceFor(string name) => string.IsNullOrEmpty(name) ? DeviceInfo.Default : new DeviceInfo(name);

        void Ensure()
        {
            if (connection) return;
            host = new GameObject("Wreckabulary voice");
            host.SetActive(false);
            recorder = host.AddComponent<Recorder>();
            tuning = host.AddComponent<WordboundVoiceTuning>();
            tuning.Fec = FecInterval;
            WebRtcAudioDsp dsp = options.CleanMicrophone && options.Input == null ? host.AddComponent<WebRtcAudioDsp>() : null;
            connection = host.AddComponent<WordboundVoiceConnection>();
            connection.ApplyDontDestroyOnLoad = Application.isPlaying;
            connection.PrimaryRecorder = recorder;
            host.SetActive(true);
            connection.Owner = this;
            connection.VoiceLogger.LogLevel = Photon.Voice.LogLevel.Warning;
            if (dsp)
            {
                dsp.AEC = true;
                dsp.NoiseSuppression = true;
                dsp.AGC = true;
                dsp.HighPass = true;
                dsp.VAD = false;
            }
            recorder.RecordWhenJoined = false;
            recorder.RecordingEnabled = false;
            recorder.TransmitEnabled = false;
            recorder.VoiceDetection = !micKnown || VoiceRules.DetectsVoice(micMode);
            recorder.MicrophoneType = Recorder.MicType.Unity;
            recorder.FrameDuration = OpusCodec.FrameDuration.Frame20ms;
            recorder.SamplingRate = POpusCodec.Enums.SamplingRate.Sampling48000;
            if (options.Input != null)
            {
                recorder.InputFactory = options.Input;
                recorder.SourceType = Recorder.InputSourceType.Factory;
            }
            else
            {
                recorder.SourceType = Recorder.InputSourceType.Microphone;
                recorder.MicrophoneDevice = DeviceFor(device);
            }
            recorder.UserData = self;
            connection.Client.AddCallbackTarget(this);
            connection.SpeakerLinked += OnLinked;
            connection.AddRecorder(recorder);
        }

        AppSettings Settings() => new AppSettings
        {
            AppIdVoice = options.AppId,
            AppVersion = options.AppVersion,
            FixedRegion = wantedRegion,
            UseNameServer = true,
            EnableLobbyStatistics = false
        };

        void Drive(double now)
        {
            var client = connection.Client;
            var state = client.State;
            if (wantedRoom == null)
            {
                joinedRoom = null;
                joinRequested = connecting = false;
                if (state != ClientState.Disconnected && state != ClientState.PeerCreated && state != ClientState.Disconnecting) client.Disconnect();
                return;
            }
            switch (state)
            {
                case ClientState.PeerCreated:
                case ClientState.Disconnected:
                    bool wasJoined = joinedRoom != null;
                    joinedRoom = null;
                    joinRequested = false;
                    if (connecting || wasJoined)
                    {
                        connecting = false;
                        Fail(now, client.DisconnectedCause.ToString());
                        break;
                    }
                    if (now < retryAt) break;
                    connectedRegion = wantedRegion;
                    connecting = connection.ConnectUsingSettings(Settings());
                    if (connecting) SetState(false, Connecting);
                    else Fail(now, "connect refused");
                    break;
                case ClientState.ConnectedToMasterServer:
                    connecting = false;
                    joinedRoom = null;
                    if (connectedRegion != wantedRegion) { client.Disconnect(); break; }
                    if (joinRequested || now < retryAt) break;
                    joinRequested = client.OpJoinOrCreateRoom(new EnterRoomArgs
                    {
                        RoomName = wantedRoom,
                        RoomOptions = new RoomOptions { IsVisible = false, IsOpen = true, MaxPlayers = capacity, PlayerTtl = 0, EmptyRoomTtl = 0 }
                    });
                    if (!joinRequested) Fail(now, "join refused");
                    break;
                case ClientState.Joined:
                    joinRequested = false;
                    string current = client.CurrentRoom?.Name;
                    if (connectedRegion != wantedRegion) { client.Disconnect(); break; }
                    if (current != wantedRoom)
                    {
                        joinedRoom = null;
                        client.OpLeaveRoom(false);
                        SetState(false, Connecting);
                        break;
                    }
                    if (joinedRoom != current)
                    {
                        joinedRoom = current;
                        failures = 0;
                        Debug.Log("[Wreckabulary] Voice chat joined a " + capacity + "-player room in " + connectedRegion + ".");
                    }
                    SetState(true, Connected);
                    break;
            }
        }

        void Fail(double now, string cause, string text = Retrying, bool giveUpQuickRetries = false)
        {
            if (giveUpQuickRetries) failures = RetrySeconds.Length - 1;
            double wait = RetrySeconds[Math.Min(failures, RetrySeconds.Length - 1)];
            failures++;
            retryAt = now + wait;
            Debug.LogWarning("[Wreckabulary] Voice chat could not connect (" + cause + "); retrying in " + wait + " s.");
            SetState(false, text);
        }

        void JoinFailed(short code)
        {
            joinRequested = false;
            bool wrongApp = code == ErrorCode.PluginReportedError || code == ErrorCode.PluginMismatch;
            Fail(Now, "room error " + code, wrongApp ? WrongApp : Retrying, wrongApp);
        }

        void IMatchmakingCallbacks.OnCreateRoomFailed(short returnCode, string message) => JoinFailed(returnCode);
        void IMatchmakingCallbacks.OnJoinRoomFailed(short returnCode, string message) => JoinFailed(returnCode);
        void IMatchmakingCallbacks.OnJoinRandomFailed(short returnCode, string message) { }
        void IMatchmakingCallbacks.OnFriendListUpdate(List<FriendInfo> friendList) { }
        void IMatchmakingCallbacks.OnCreatedRoom() { }
        void IMatchmakingCallbacks.OnJoinedRoom() { }
        void IMatchmakingCallbacks.OnLeftRoom() { }

        void ApplyMic(double now)
        {
            if (!recorder) return;
            bool joined = joinedRoom != null, send = joined && wantSend;
            if (send) micUntil = now + MicHoldSeconds;
            bool record = micKnown ? send || VoiceRules.Records(micState, joined) : joined && (send || now < micUntil);
            if (recorder.RecordingEnabled != record) recorder.RecordingEnabled = record;
            if (recorder.TransmitEnabled != send) recorder.TransmitEnabled = send;
        }

        void TrackSpeaking()
        {
            if (remotes.Count == 0 && liveReports == 0 && !selfSpeaking && !Sending) return;
            int tick = Environment.TickCount;
            speakingNow.Clear();
            foreach (var remote in remotes)
                if (Heard(remote.PlayerId) && tick - Volatile.Read(ref remote.LoudAt) < SpeakingHoldMs) speakingNow.Add(remote.PlayerId);
            foreach (var remote in remotes) Report(remote.PlayerId, speakingNow.Contains(remote.PlayerId));
            stopped.Clear();
            foreach (var pair in reported)
                if (pair.Value && !speakingNow.Contains(pair.Key)) stopped.Add(pair.Key);
            foreach (string id in stopped) Report(id, false);
            bool me = Sending && recorder.IsCurrentlyTransmitting;
            if (me != selfSpeaking && self.Length != 0)
            {
                selfSpeaking = me;
                SpeakingChanged?.Invoke(self, me);
            }
        }

        void Report(string playerId, bool on)
        {
            bool known = reported.TryGetValue(playerId, out bool was);
            if (known && was == on) return;
            reported[playerId] = on;
            if (on) liveReports++;
            else if (known) liveReports--;
            SpeakingChanged?.Invoke(playerId, on);
        }

        bool Heard(string playerId) => receiving && !muted.Contains(playerId) && VoiceChannel.Accepts(room, self, playerId);

        internal Speaker CreateSpeaker(VoiceConnection owner, string playerId)
        {
            if (disposed || !VoiceChannel.Accepts(room, self, playerId)) return null;
            var go = new GameObject("Voice " + playerId.Substring(0, Math.Min(6, playerId.Length)));
            go.transform.SetParent(owner.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            var speaker = go.AddComponent<Speaker>();
            var delay = AudioOutDelayControl.PlayDelayConfig.Default;
            delay.Low = PlayDelayLowMs;
            delay.High = PlayDelayHighMs;
            delay.Max = PlayDelayMaxMs;
            speaker.PlayDelayConfig = delay;
            var remote = new Remote(playerId, speaker, source);
            speaker.OnRemoteVoiceRemoveAction += _ =>
            {
                remotes.Remove(remote);
                if (go) UnityEngine.Object.Destroy(go);
            };
            remotes.Add(remote);
            Apply(remote);
            return speaker;
        }

        void OnLinked(Speaker speaker)
        {
            foreach (var remote in remotes)
                if (remote.Speaker == speaker && speaker.RemoteVoice != null)
                {
                    speaker.RemoteVoice.FloatFrameDecoded += remote.Decoded;
                    return;
                }
        }

        void ApplyAll()
        {
            foreach (var remote in remotes) Apply(remote);
        }

        void Apply(Remote remote)
        {
            if (!remote.Source) return;
            remote.Source.mute = !Heard(remote.PlayerId);
            remote.Source.volume = volume;
        }

        void SetState(bool nowAvailable, string text)
        {
            if (available == nowAvailable && status == text) return;
            available = nowAvailable;
            status = text;
            Changed?.Invoke();
        }
    }
}
