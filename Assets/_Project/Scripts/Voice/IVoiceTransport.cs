using System;
using System.Collections.Generic;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Voice
{
    public interface IVoiceTransport
    {
        bool Available { get; }
        string Status { get; }
        bool Sending { get; }
        void SetSending(bool on);
        void SetReceiving(bool on);
        float PlaybackVolume { get; set; }
        void SetMuted(string playerId, bool muted);
        void SetRoom(VoiceRoom room, string self);
        float InputLevel { get; }
        IReadOnlyList<string> Devices { get; }
        string Device { get; set; }
        event Action<string, bool> SpeakingChanged;
        event Action Changed;
    }

    public interface IVoiceLinkTransport : IVoiceTransport
    {
        VoiceLinkStats Link(string playerId);
        VoiceLinkStats Outgoing { get; }
        VoiceSendQuality SendQuality { get; }
        void SetSendQuality(VoiceSendQuality quality);
    }

    public sealed class NullVoiceTransport : IVoiceTransport
    {
        public const string NotConnected = "Voice chat isn't connected in this build.";

        readonly IMicrophoneSource microphone;
        readonly HashSet<string> muted = new(StringComparer.Ordinal);
        float volume = 1f;
        string device = "";

        public NullVoiceTransport(IMicrophoneSource microphone = null) => this.microphone = microphone ?? UnityMicrophone.Shared;

        public bool Available => false;
        public string Status => NotConnected;
        public bool Sending => false;
        public bool Receiving { get; private set; }
        public VoiceRoom Room { get; private set; } = VoiceRoom.Empty;
        public void SetSending(bool on) { }
        public void SetReceiving(bool on) => Receiving = on;
        public float PlaybackVolume { get => volume; set => volume = float.IsNaN(value) ? 0f : Math.Max(0f, Math.Min(1f, value)); }
        public bool IsMuted(string playerId) => playerId != null && muted.Contains(playerId);

        public void SetMuted(string playerId, bool mute)
        {
            if (playerId == null) return;
            if (mute) muted.Add(playerId); else muted.Remove(playerId);
        }

        public void SetRoom(VoiceRoom room, string self) => Room = room ?? VoiceRoom.Empty;
        public float InputLevel => 0f;
        public IReadOnlyList<string> Devices => microphone.Devices;
        public string Device { get => device; set => device = value ?? ""; }

        public event Action<string, bool> SpeakingChanged { add { } remove { } }
        public event Action Changed { add { } remove { } }
    }
}
