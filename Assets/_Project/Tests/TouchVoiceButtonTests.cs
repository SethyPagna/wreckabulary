using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Voice;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class TouchVoiceButtonTests
    {
        readonly Dictionary<string, string> saved = new Dictionary<string, string>();
        Fake fake;
        GameObject host;

        [SetUp]
        public void SetUp()
        {
            saved.Clear();
            foreach (string key in VoiceSettings.Keys)
                if (PlayerPrefs.HasKey(key)) saved[key] = PlayerPrefs.GetString(key, "");
            VoiceChat.Leave();
            VoiceChat.ResetToDefaults();
            fake = new Fake();
            VoiceChat.Use(fake);
            host = new GameObject("Touch voice buttons", typeof(RectTransform), typeof(Canvas));
        }

        [TearDown]
        public void TearDown()
        {
            if (host) Object.DestroyImmediate(host);
            VoiceChat.Leave();
            VoiceChat.Use(null);
            foreach (string key in VoiceSettings.Keys) PlayerPrefs.DeleteKey(key);
            foreach (var pair in saved) PlayerPrefs.SetString(pair.Key, pair.Value);
            PlayerPrefs.Save();
            VoiceChat.Reload();
        }

        static PointerEventData Pointer() => new PointerEventData(null) { pointerId = 3 };

        [Test]
        public void HoldMicToTalkAndTapSpeakerOnlyWhereVoiceApplies()
        {
            var root = (RectTransform)host.transform;
            var mic = TouchVoiceButton.Create(root, TouchPart.Mic);
            var speaker = TouchVoiceButton.Create(root, TouchPart.Speaker);
            Assert.AreEqual(TouchLayout.Label(TouchPart.Mic), mic.name);
            Assert.AreEqual(TouchLayout.Label(TouchPart.Speaker), speaker.name);
            Assert.Throws<ArgumentException>(() => TouchVoiceButton.Create(root, TouchPart.Jump));
            VoiceChat.Mic = MicMode.PushToTalk;

            mic.Refresh(); speaker.Refresh();
            Assert.IsFalse(mic.Shown, "Without anyone to hear, the buttons are hidden.");
            Assert.AreEqual(0f, mic.GetComponent<CanvasGroup>().alpha);
            mic.OnPointerDown(Pointer());
            Assert.IsFalse(VoiceChat.TalkHeld, "A hidden mic never talks.");
            mic.OnPointerUp(Pointer());

            VoiceChat.Join(VoiceRoom.Party(new[] { "touch-me", "touch-friend" }), "touch-me");
            mic.Refresh(); speaker.Refresh();
            Assert.IsTrue(mic.Shown && speaker.Shown);
            Assert.AreEqual(1f, mic.GetComponent<CanvasGroup>().alpha, 1e-5f);
            mic.OnPointerDown(Pointer());
            Assert.IsTrue(mic.Held);
            Assert.IsTrue(VoiceChat.TalkHeld, "Holding MIC is push to talk.");
            Assert.AreEqual(MicState.Live, VoiceChat.MicState);
            Assert.IsTrue(fake.Sending);
            mic.OnPointerUp(Pointer());
            Assert.IsFalse(VoiceChat.TalkHeld);
            Assert.IsFalse(fake.Sending, "Letting go stops sending.");

            speaker.OnPointerDown(Pointer()); speaker.OnPointerUp(Pointer());
            Assert.IsFalse(VoiceChat.HearTeammates, "Tapping SPEAKER turns teammates off.");
            Assert.IsFalse(fake.Receiving);
            speaker.OnPointerDown(Pointer()); speaker.OnPointerUp(Pointer());
            Assert.IsTrue(VoiceChat.HearTeammates);
            Assert.IsTrue(fake.Receiving);

            mic.OnPointerDown(Pointer());
            VoiceChat.Leave();
            mic.Refresh();
            Assert.IsFalse(mic.Shown);
            Assert.IsFalse(VoiceChat.TalkHeld, "Leaving voice lets go of the mic.");
        }

        [Test]
        public void TheHouseOverlayNeverBuildsVoiceButtons()
        {
            foreach (var part in TouchLayout.Parts)
            {
                bool voice = part == TouchPart.Mic || part == TouchPart.Speaker;
                Assert.AreEqual(!voice, TouchLayoutView.InHouse(part), part.ToString());
            }
            Assert.AreEqual("MIC", TouchLayoutView.HudName(TouchPart.Mic));
            Assert.AreEqual("SPEAKER", TouchLayoutView.HudName(TouchPart.Speaker));
        }

        sealed class Fake : IVoiceTransport
        {
            public bool Available => true;
            public string Status => "Touch test transport.";
            public bool Sending { get; private set; }
            public bool Receiving { get; private set; } = true;
            public void SetSending(bool on) => Sending = on;
            public void SetReceiving(bool on) => Receiving = on;
            public float PlaybackVolume { get; set; } = 1f;
            public void SetMuted(string playerId, bool muted) { }
            public void SetRoom(VoiceRoom room, string self) { }
            public float InputLevel => 0f;
            public IReadOnlyList<string> Devices => Array.Empty<string>();
            public string Device { get; set; } = "";
            public event Action<string, bool> SpeakingChanged { add { } remove { } }
            public event Action Changed { add { } remove { } }
        }
    }
}
