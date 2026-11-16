using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Voice
{
    public static class VoiceChat
    {
        public const string SpeakersNote = "Plays through your system's default speakers",
            EchoHint = "Use headphones to avoid echo.", SimulateRow = "Simulate a bad connection", SimulateHelp = "For testing: adds loss and jitter.";
#if UNITY_EDITOR || GSK_QA
        public static bool QaTools => true;
#else
        public static bool QaTools => false;
#endif

        static IVoiceTransport transport;
        static VoiceSettings settings;
        static VoiceRoom room = VoiceRoom.Empty;
        static string self = "";
        static readonly SpeakingTracker speaking = new();
        static readonly VoiceLinkBoard links = new();
        static readonly VoiceQualityPolicy policy = new();
        static bool pushHeld, touchHeld, sent, hooked;
        static double now, linksAt = double.NegativeInfinity;

        public static event Action Changed;
        public static int Version { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Unhook();
            if (transport != null) Detach(transport);
            transport = null;
            settings = null;
            room = VoiceRoom.Empty;
            self = "";
            speaking.Clear();
            links.Clear();
            policy.Reset();
            pushHeld = touchHeld = sent = false;
            now = 0;
            linksAt = double.NegativeInfinity;
            Version = 0;
            Changed = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (hooked) return;
            InputSystem.onAfterUpdate += Poll;
            hooked = true;
        }

        static void Unhook()
        {
            if (!hooked) return;
            InputSystem.onAfterUpdate -= Poll;
            hooked = false;
        }

        static VoiceSettings Current => settings ??= Load();

        public static IVoiceTransport Transport => transport ??= Attach(new NullVoiceTransport());

        public static void Use(IVoiceTransport next)
        {
            next ??= new NullVoiceTransport();
            if (ReferenceEquals(next, transport)) return;
            if (transport != null) Detach(transport);
            transport = null;
            sent = false;
            speaking.Clear();
            links.Clear();
            policy.Reset();
            linksAt = double.NegativeInfinity;
            transport = Attach(next);
            Notify();
        }

        static IVoiceTransport Attach(IVoiceTransport next)
        {
            next.SpeakingChanged += OnSpeaking;
            next.Changed += Notify;
            var s = Current;
            next.Device = s.Device;
            next.PlaybackVolume = VoiceRules.Gain(s.Volume);
            next.SetReceiving(s.HearTeammates);
            foreach (string id in s.Muted) next.SetMuted(id, true);
            next.SetRoom(room, self);
            if (next is IVoiceLinkTransport link) link.SetSendQuality(policy.Current);
            sent = WantsToSend;
            next.SetSending(sent);
            return next;
        }

        static void Detach(IVoiceTransport old)
        {
            old.SpeakingChanged -= OnSpeaking;
            old.Changed -= Notify;
            old.SetSending(false);
        }

        public static bool Available => Transport.Available;
        public static string Status => Transport.Status;
        public static bool Sending => Transport.Sending;
        public static float InputLevel => Transport.InputLevel;
        public static IReadOnlyList<string> Devices => Transport.Devices;
        public static VoiceRoom Room => room;
        public static string Self => self;

        public static MicMode Mic
        {
            get => Current.Mic;
            set
            {
                if (Current.Mic == value) return;
                Current.Mic = value;
                if (value != MicMode.PushToTalk) pushHeld = touchHeld = false;
                Saved();
            }
        }

        public static bool SelfMuted
        {
            get => Current.SelfMuted;
            set
            {
                if (Current.SelfMuted == value) return;
                Current.SelfMuted = value;
                Saved();
            }
        }

        public static void ToggleSelfMute() => SelfMuted = !SelfMuted;

        public static bool HearTeammates
        {
            get => Current.HearTeammates;
            set
            {
                if (Current.HearTeammates == value) return;
                Current.HearTeammates = value;
                Transport.SetReceiving(value);
                Saved();
            }
        }

        public static void ToggleHearing() => HearTeammates = !HearTeammates;

        public static int Volume
        {
            get => Current.Volume;
            set
            {
                int before = Current.Volume;
                Current.Volume = value;
                if (Current.Volume == before) return;
                Transport.PlaybackVolume = VoiceRules.Gain(Current.Volume);
                Saved();
            }
        }

        public static void StepVolume(int steps) => Volume = VoiceRules.Step(Volume, steps);

        public static string Device
        {
            get => Current.Device;
            set
            {
                string before = Current.Device;
                Current.Device = value;
                if (Current.Device == before) return;
                Transport.Device = Current.Device;
                Saved();
            }
        }

        public static bool IsMuted(string playerId) => Current.IsMuted(playerId);

        public static void SetMuted(string playerId, bool muted)
        {
            if (!Current.SetMuted(playerId, muted)) return;
            Transport.SetMuted(playerId, muted);
            Saved();
        }

        public static void ToggleMuted(string playerId) => SetMuted(playerId, !IsMuted(playerId));

        public static bool TalkHeld => Current.Mic == MicMode.PushToTalk && (pushHeld || touchHeld);
        public static bool WantsToSend => VoiceRules.Transmits(Current.Mic, Current.SelfMuted, TalkHeld);
        public static MicState MicState => VoiceRules.Mic(Current.Mic, Current.SelfMuted, TalkHeld);

        public static void SetTalkButton(bool held)
        {
            held &= Current.Mic == MicMode.PushToTalk;
            if (touchHeld == held) return;
            touchHeld = held;
            SyncSending();
            Notify();
        }

        public static string PushToTalkKey => ControlHints.PushToTalkKey;

        public static void Join(VoiceRoom next, string selfId)
        {
            room = next ?? VoiceRoom.Empty;
            self = selfId ?? "";
            speaking.Clear();
            links.Clear();
            linksAt = double.NegativeInfinity;
            Transport.SetRoom(room, self);
            Notify();
        }

        public static void Leave() => Join(VoiceRoom.Empty, "");

        public static bool Hears(string playerId) => VoiceRules.Plays(Current, room, self, playerId);

        public static bool IsSpeaking(string playerId) =>
            speaking.IsSpeaking(playerId, now) && (playerId == self && self.Length > 0 ? Sending : Hears(playerId));

        public static IReadOnlyList<string> Speaking => speaking.Speaking(now).Where(IsSpeaking).ToArray();

        public const double LinkSeconds = .25;

        public static VoiceLinkStats LinkStats(string playerId) =>
            playerId != null && Transport is IVoiceLinkTransport link ? link.Link(playerId) : default;

        public static VoiceLinkQuality Link(string playerId) => Hears(playerId) ? links.Quality(playerId) : VoiceLinkQuality.Unknown;

        public static bool WeakTip(string playerId) => Link(playerId) == VoiceLinkQuality.Poor && links.TipShown(playerId, now);

        public static VoiceSendQuality SendQuality => Transport is IVoiceLinkTransport link ? link.SendQuality : policy.Current;

        public static void Tick(bool held, double time)
        {
            now = time;
            held &= Current.Mic == MicMode.PushToTalk;
            bool changed = held != pushHeld;
            pushHeld = held;
            changed |= SyncSending();
            changed |= speaking.Tick(now);
            changed |= TickLinks();
            if (changed) Notify();
        }

        static bool TickLinks()
        {
            if (now - linksAt < LinkSeconds && now >= linksAt) return false;
            linksAt = now;
            if (!(Transport is IVoiceLinkTransport link)) return false;
            if (policy.Update(link.Outgoing, now)) link.SetSendQuality(policy.Current);
            bool changed = false;
            var members = room.Members;
            for (int i = 0; i < members.Count; i++)
            {
                string id = members[i].PlayerId;
                if (id == self || !room.CanHear(self, id)) continue;
                bool was = links.TipShown(id, now - LinkSeconds);
                changed |= links.Update(id, link.Link(id), now);
                changed |= was != links.TipShown(id, now);
            }
            return changed;
        }

        public static void Reload()
        {
            var unmuted = settings?.Muted.ToArray() ?? Array.Empty<string>();
            settings = Load();
            pushHeld = touchHeld = false;
            if (transport != null)
            {
                var keep = transport;
                Detach(keep);
                transport = null;
                foreach (string id in unmuted)
                    if (!settings.IsMuted(id)) keep.SetMuted(id, false);
                transport = Attach(keep);
            }
            Notify();
        }

        public static void ResetToDefaults()
        {
            foreach (string key in VoiceSettings.Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Reload();
        }

        static void OnSpeaking(string playerId, bool on)
        {
            if (speaking.Report(playerId, on, now)) Notify();
        }

        static bool SyncSending()
        {
            bool wants = WantsToSend;
            if (wants == sent) return false;
            sent = wants;
            Transport.SetSending(wants);
            return true;
        }

        static void Saved()
        {
            foreach (var pair in Current.Write()) PlayerPrefs.SetString(pair.Key, pair.Value);
            PlayerPrefs.Save();
            SyncSending();
            Notify();
        }

        static VoiceSettings Load() => VoiceSettings.Read(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key, null) : null);

        static void Notify()
        {
            Version++;
            Changed?.Invoke();
        }

        static void Poll()
        {
            if (!Application.isPlaying || InputState.currentUpdateType == InputUpdateType.Editor) return;
            Tick(ReadHeld(), Time.unscaledTimeAsDouble);
        }

        static bool ReadHeld()
        {
            if (Current.Mic != MicMode.PushToTalk || DesktopBinding.Typing || KeyBindings.Listening) return false;
            var keys = DesktopBinding.Existing;
            if (keys != null && keys.PushToTalk.IsPressed()) return true;
            foreach (var pad in Gamepad.all)
                if (pad.added && pad.dpad.up.isPressed) return true;
            return false;
        }
    }
}
