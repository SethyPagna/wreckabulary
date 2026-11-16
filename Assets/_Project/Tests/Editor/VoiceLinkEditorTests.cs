using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Voice;

namespace Wreckabulary.EditorTests
{
    public class VoiceLinkEditorTests
    {
        sealed class LinkTransport : IVoiceLinkTransport
        {
            public readonly Dictionary<string, VoiceLinkStats> Links = new();
            public readonly List<VoiceSendQuality> Qualities = new();
            public VoiceLinkStats Report;
            public bool Available => true;
            public string Status => "Connected";
            public bool Sending { get; private set; }
            public void SetSending(bool on) => Sending = on;
            public void SetReceiving(bool on) { }
            public float PlaybackVolume { get; set; }
            public void SetMuted(string playerId, bool muted) { }
            public void SetRoom(VoiceRoom room, string self) { }
            public float InputLevel => 0f;
            public IReadOnlyList<string> Devices => Array.Empty<string>();
            public string Device { get; set; }
            public event Action<string, bool> SpeakingChanged { add { } remove { } }
            public event Action Changed { add { } remove { } }
            public VoiceLinkStats Link(string playerId) => Links.TryGetValue(playerId, out var stats) ? stats : default;
            public VoiceLinkStats Outgoing => Report;
            public VoiceSendQuality SendQuality { get; private set; }
            public void SetSendQuality(VoiceSendQuality quality) { SendQuality = quality; Qualities.Add(quality); }
        }

        string[] saved;
        bool[] had;

        [SetUp]
        public void SetUp()
        {
            had = VoiceSettings.Keys.Select(PlayerPrefs.HasKey).ToArray();
            saved = VoiceSettings.Keys.Select(key => PlayerPrefs.GetString(key, "")).ToArray();
            VoiceChat.Use(null);
            VoiceChat.Leave();
            VoiceChat.ResetToDefaults();
        }

        [TearDown]
        public void TearDown()
        {
            VoiceChat.Use(null);
            VoiceChat.Leave();
            for (int i = 0; i < VoiceSettings.Keys.Length; i++)
                if (had[i]) PlayerPrefs.SetString(VoiceSettings.Keys[i], saved[i]); else PlayerPrefs.DeleteKey(VoiceSettings.Keys[i]);
            PlayerPrefs.Save();
            VoiceChat.Reload();
        }

        [Test]
        public void EachHeardTeammateGetsALinkGradeAndAShortWeakConnectionTip()
        {
            var link = new LinkTransport();
            VoiceChat.Use(link);
            VoiceChat.Join(VoiceRoom.Party(new[] { "me", "good", "weak" }), "me");
            link.Links["good"] = VoiceLinkStats.Report(0, 0, 5, 60);
            link.Links["weak"] = VoiceLinkStats.Report(18, 2, 40, 120);
            VoiceChat.Tick(false, 10);
            Assert.AreEqual(VoiceLinkQuality.Good, VoiceChat.Link("good"));
            Assert.AreEqual(VoiceLinkQuality.Poor, VoiceChat.Link("weak"));
            Assert.AreEqual(VoiceLinkQuality.Unknown, VoiceChat.Link("me"), "no mark for yourself");
            Assert.AreEqual(VoiceLinkQuality.Unknown, VoiceChat.Link("stranger"));
            Assert.IsTrue(VoiceChat.WeakTip("weak"));
            Assert.IsFalse(VoiceChat.WeakTip("good"));
            Assert.AreEqual(18f, VoiceChat.LinkStats("weak").LossPercent, 1e-5f);
            VoiceChat.Tick(false, 10 + VoiceLinkMeter.TipSeconds + 1);
            Assert.IsFalse(VoiceChat.WeakTip("weak"), "the tip is short");
            Assert.AreEqual(VoiceLinkQuality.Poor, VoiceChat.Link("weak"), "the red bar stays");
            VoiceChat.SetMuted("weak", true);
            Assert.AreEqual(VoiceLinkQuality.Unknown, VoiceChat.Link("weak"), "a muted teammate shows no link mark");
        }

        [Test]
        public void TheSendQualityStepsDownWhenTeammatesReportLoss()
        {
            var link = new LinkTransport();
            VoiceChat.Use(link);
            Assert.AreEqual(VoiceQualityPolicy.Steps[0], link.SendQuality, "a new transport starts at full quality");
            VoiceChat.Join(VoiceRoom.Party(new[] { "me", "pal" }), "me");
            link.Report = VoiceLinkStats.Report(9, 0, 30, 90);
            for (double t = 0; t <= 2.6; t += .3) VoiceChat.Tick(false, 20 + t);
            Assert.AreEqual(1, link.SendQuality.Step);
            Assert.IsTrue(link.SendQuality.Fec);
            Assert.AreEqual(link.SendQuality, VoiceChat.SendQuality);
        }

        [Test]
        public void TransportsWithoutLinkReportsShowNoMarks()
        {
            VoiceChat.Join(VoiceRoom.Party(new[] { "me", "pal" }), "me");
            VoiceChat.Tick(false, 1);
            Assert.AreEqual(VoiceLinkQuality.Unknown, VoiceChat.Link("pal"));
            Assert.IsFalse(VoiceChat.WeakTip("pal"));
            Assert.AreEqual(default(VoiceLinkStats).Known, VoiceChat.LinkStats("pal").Known);
            Assert.AreEqual(VoiceQualityPolicy.Steps[0], VoiceChat.SendQuality);
        }

        [Test]
        public void VoicePlaybackForAReceiverIsSeparateFromGameSound()
        {
            var receiver = new VoiceReceiver(16000);
            var host = new GameObject("Voice receiver host");
            try
            {
                var playback = new VoiceReceiverPlayback(receiver, host);
                Assert.IsTrue(VoicePlayback.IsSeparate(playback.Source));
                Assert.AreSame(playback.Clip, playback.Source.clip);
                Assert.AreEqual(16000, playback.Clip.frequency);
                Assert.AreEqual(1, playback.Clip.channels);
                playback.Volume = 2f;
                Assert.AreEqual(1f, playback.Volume);
                playback.Dispose();
                Assert.IsFalse(playback.Playing);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
        }

        [Test]
        public void TheEchoHintAndTheQaSwitchAreWordedPlainly()
        {
            Assert.AreEqual("Use headphones to avoid echo.", VoiceChat.EchoHint);
            Assert.AreEqual("Simulate a bad connection", VoiceChat.SimulateRow);
            Assert.IsTrue(VoiceChat.QaTools, "the editor is a QA build");
            CollectionAssert.Contains(SettingsPage.VoiceRows, "Voice echo hint");
            CollectionAssert.Contains(SettingsPage.VoiceRows, VoiceChat.SimulateRow);
        }
    }
}
