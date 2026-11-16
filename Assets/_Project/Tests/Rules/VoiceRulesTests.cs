using NUnit.Framework;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class VoiceRulesTests
    {
        [Test]
        public void OnlyAnOpenMicOrAHeldKeySendsAndSelfMuteAlwaysWins()
        {
            foreach (bool held in new[] { false, true })
            {
                Assert.IsFalse(VoiceRules.Transmits(MicMode.Off, false, held), "Off never sends");
                Assert.IsFalse(VoiceRules.Transmits(MicMode.Off, true, held));
                Assert.IsFalse(VoiceRules.Transmits(MicMode.PushToTalk, true, held), "self-mute overrides push to talk");
                Assert.IsFalse(VoiceRules.Transmits(MicMode.OpenMic, true, held), "self-mute overrides an open mic");
                Assert.IsTrue(VoiceRules.Transmits(MicMode.OpenMic, false, held));
            }
            Assert.IsFalse(VoiceRules.Transmits(MicMode.PushToTalk, false, false), "push to talk waits for the key");
            Assert.IsTrue(VoiceRules.Transmits(MicMode.PushToTalk, false, true));
            Assert.IsFalse(VoiceRules.Transmits((MicMode)42, false, true), "an unknown mode never sends");
        }

        [Test]
        public void MicStateNamesWhatTheButtonShows()
        {
            Assert.AreEqual(MicState.Off, VoiceRules.Mic(MicMode.Off, true, true));
            Assert.AreEqual(MicState.Muted, VoiceRules.Mic(MicMode.OpenMic, true, false));
            Assert.AreEqual(MicState.Muted, VoiceRules.Mic(MicMode.PushToTalk, true, true));
            Assert.AreEqual(MicState.Ready, VoiceRules.Mic(MicMode.PushToTalk, false, false));
            Assert.AreEqual(MicState.Live, VoiceRules.Mic(MicMode.PushToTalk, false, true));
            Assert.AreEqual(MicState.Live, VoiceRules.Mic(MicMode.OpenMic, false, false));
        }

        [Test]
        public void PlaybackNeedsTheRouteTheSpeakerAndNoMute()
        {
            var room = VoiceRoom.Party(new[] { "me", "pal", "other" });
            var settings = new VoiceSettings();
            Assert.IsTrue(VoiceRules.Plays(settings, room, "me", "pal"));
            settings.SetMuted("pal", true);
            Assert.IsFalse(VoiceRules.Plays(settings, room, "me", "pal"), "a muted teammate stays silent");
            Assert.IsTrue(VoiceRules.Plays(settings, room, "me", "other"));
            settings.HearTeammates = false;
            Assert.IsFalse(VoiceRules.Plays(settings, room, "me", "other"), "the speaker switch silences everyone");
            settings.HearTeammates = true;
            Assert.IsFalse(VoiceRules.Plays(settings, room, "me", "me"), "you never hear yourself");
            Assert.IsFalse(VoiceRules.Plays(settings, room, "me", "stranger"));
            Assert.IsFalse(VoiceRules.Plays(null, room, "me", "other"));
            Assert.IsFalse(VoiceRules.Plays(settings, null, "me", "other"));
        }

        [Test]
        public void VolumeStepsByTenAndMapsToGain()
        {
            Assert.AreEqual(.8f, VoiceRules.Gain(80), 1e-6f);
            Assert.AreEqual(0f, VoiceRules.Gain(-20), 1e-6f);
            Assert.AreEqual(1f, VoiceRules.Gain(130), 1e-6f);
            Assert.AreEqual(90, VoiceRules.Step(80, 1));
            Assert.AreEqual(100, VoiceRules.Step(100, 1));
            Assert.AreEqual(0, VoiceRules.Step(10, -3));
            Assert.AreEqual(90, VoiceRules.Step(85, 0), "odd saved values snap to the grid");
            Assert.AreEqual(60, VoiceRules.Step(55, 0));
        }
    }
}
