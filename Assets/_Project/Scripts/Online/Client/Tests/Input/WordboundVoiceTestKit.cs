using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Voice;

namespace Wreckabulary.Online.Tests
{
    internal sealed class FakeVoiceTransport : IVoiceTransport
    {
        public readonly HashSet<string> Muted = new HashSet<string>(StringComparer.Ordinal);
        public readonly List<bool> SendingCalls = new List<bool>(), ReceivingCalls = new List<bool>();
        public bool Available { get; set; } = true;
        public string Status { get; set; } = "Voice test transport.";
        public bool Sending { get; private set; }
        public bool Receiving { get; private set; } = true;
        public VoiceRoom Room { get; private set; } = VoiceRoom.Empty;
        public string Self { get; private set; } = "";
        public float PlaybackVolume { get; set; } = 1f;
        public float InputLevel => 0f;
        public IReadOnlyList<string> Devices => Array.Empty<string>();
        public string Device { get; set; } = "";
        public event Action<string, bool> SpeakingChanged;
        public event Action Changed;

        public void SetSending(bool on) { Sending = on; SendingCalls.Add(on); }
        public void SetReceiving(bool on) { Receiving = on; ReceivingCalls.Add(on); }
        public void SetMuted(string playerId, bool muted) { if (muted) Muted.Add(playerId); else Muted.Remove(playerId); }
        public void SetRoom(VoiceRoom room, string self) { Room = room ?? VoiceRoom.Empty; Self = self ?? ""; }
        public void Speak(string playerId, bool speaking) => SpeakingChanged?.Invoke(playerId, speaking);
        public void Touch() => Changed?.Invoke();
    }

    internal sealed class VoiceTestScope : IDisposable
    {
        readonly Dictionary<string, string> saved = new Dictionary<string, string>();
        public readonly FakeVoiceTransport Fake;

        public VoiceTestScope(bool connected = true)
        {
            foreach (string key in VoiceSettings.Keys)
                if (PlayerPrefs.HasKey(key)) saved[key] = PlayerPrefs.GetString(key, "");
            VoiceChat.Leave();
            VoiceChat.ResetToDefaults();
            Fake = connected ? new FakeVoiceTransport() : null;
            VoiceChat.Use(Fake);
            VoiceChat.Tick(false, 0);
        }

        public void Dispose()
        {
            VoiceChat.Leave();
            VoiceChat.Use(null);
            foreach (string key in VoiceSettings.Keys) PlayerPrefs.DeleteKey(key);
            foreach (var pair in saved) PlayerPrefs.SetString(pair.Key, pair.Value);
            PlayerPrefs.Save();
            VoiceChat.Reload();
        }
    }
}
