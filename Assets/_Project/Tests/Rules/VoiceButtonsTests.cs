using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public class VoiceButtonsTests
    {
        [Test]
        public void MicFacesFollowTheMicState()
        {
            Assert.IsTrue(VoiceButtons.Crossed(MicState.Off));
            Assert.IsTrue(VoiceButtons.Crossed(MicState.Muted));
            Assert.IsFalse(VoiceButtons.Crossed(MicState.Ready));
            Assert.IsFalse(VoiceButtons.Crossed(MicState.Live));
            Assert.IsTrue(VoiceButtons.Alarm(MicState.Muted), "Self-muted is the red crossed mic.");
            Assert.IsFalse(VoiceButtons.Alarm(MicState.Off), "Off is a plain crossed mic, not an alarm.");
            Assert.IsTrue(VoiceButtons.Lit(MicState.Live));
            Assert.IsFalse(VoiceButtons.Lit(MicState.Ready));
            Assert.IsTrue(VoiceButtons.ShowsKey(MicMode.PushToTalk));
            Assert.IsFalse(VoiceButtons.ShowsKey(MicMode.OpenMic));
            Assert.IsFalse(VoiceButtons.ShowsKey(MicMode.Off));
        }

        [Test]
        public void ATapMutesShowsTheStatusOrPicksAMode()
        {
            Assert.AreEqual(VoiceTap.Status, VoiceButtons.MicTap(false, MicMode.OpenMic), "Not connected: a tap explains why.");
            Assert.AreEqual(VoiceTap.Status, VoiceButtons.SpeakerTap(false));
            Assert.AreEqual(VoiceTap.Toggle, VoiceButtons.MicTap(true, MicMode.PushToTalk));
            Assert.AreEqual(VoiceTap.Toggle, VoiceButtons.MicTap(true, MicMode.OpenMic));
            Assert.AreEqual(VoiceTap.Choose, VoiceButtons.MicTap(true, MicMode.Off), "With the mic off there is nothing to mute, so a tap opens the choice.");
            Assert.AreEqual(VoiceTap.Toggle, VoiceButtons.SpeakerTap(true));
        }

        [Test]
        public void ALongPressOpensTheChoiceOnceAndAShortOneIsATap()
        {
            var press = new VoicePress();
            Assert.AreEqual(VoicePressEnd.None, press.End(1), "Letting go without a press does nothing.");
            Assert.IsTrue(press.Begin(10));
            Assert.IsFalse(press.Begin(10.1), "A second finger is ignored.");
            Assert.IsFalse(press.Long(10.2));
            Assert.AreEqual(VoicePressEnd.Tap, press.End(10.3));
            Assert.IsFalse(press.Down);

            Assert.IsTrue(press.Begin(20));
            Assert.IsTrue(press.Long(20 + VoiceButtons.LongPressSeconds));
            Assert.IsFalse(press.Long(21), "The choice opens once per press.");
            Assert.AreEqual(VoicePressEnd.None, press.End(22), "A long press is not also a tap.");

            Assert.IsTrue(press.Begin(30));
            Assert.AreEqual(VoicePressEnd.Long, press.End(31), "A long press noticed only on release still opens the choice.");
            Assert.IsTrue(press.Begin(40));
            press.Cancel();
            Assert.AreEqual(VoicePressEnd.None, press.End(40.1));
        }

        [Test]
        public void ModesReadPlainly()
        {
            CollectionAssert.AreEqual(new[] { MicMode.Off, MicMode.PushToTalk, MicMode.OpenMic }, VoiceButtons.Modes.ToArray());
            CollectionAssert.AreEqual(new[] { "Off", "Push to talk", "Open" }, VoiceButtons.Modes.Select(VoiceButtons.ModeName).ToArray());
            Assert.AreEqual("Mic off", VoiceButtons.MicLine(MicState.Off, "V"));
            Assert.AreEqual("Mic muted", VoiceButtons.MicLine(MicState.Muted, "V"));
            Assert.AreEqual("Hold V to talk", VoiceButtons.MicLine(MicState.Ready, "V"));
            Assert.AreEqual("Push to talk", VoiceButtons.MicLine(MicState.Ready, ""));
            Assert.AreEqual("Talking", VoiceButtons.MicLine(MicState.Live, "V"));
            Assert.AreEqual("Mute", VoiceButtons.MuteLabel(false));
            Assert.AreEqual("Unmute", VoiceButtons.MuteLabel(true));
        }

        [Test]
        public void VoiceShowsOnlyWhenSomeoneElseCanBeHeard()
        {
            var solo = VoiceRoom.Match(ModeDefinition.For("TimeRun"), new[] { new WordboundSeat("me", 0, 0) });
            Assert.IsFalse(VoiceButtons.Shows(solo, "me"), "A one-seat game (local, solo) has no voice controls.");
            Assert.IsFalse(VoiceButtons.Shows(VoiceRoom.Empty, "me"));
            Assert.IsFalse(VoiceButtons.Shows(null, "me"));
            var coop = VoiceRoom.Match(ModeDefinition.For("TimeRun"), new[] { new WordboundSeat("me", 0, 0), new WordboundSeat("friend", 0, 1) });
            Assert.IsTrue(VoiceButtons.Shows(coop, "me"));
            Assert.IsFalse(VoiceButtons.Shows(coop, "stranger"), "Someone outside the room hears nobody.");
            Assert.IsFalse(VoiceButtons.Shows(VoiceRoom.Party(new[] { "me" }), "me"));
            Assert.IsTrue(VoiceButtons.Shows(VoiceRoom.Party(new[] { "me", "friend" }), "me"));
        }

        [Test]
        public void RowsListTeammatesWithStableShortNames()
        {
            var mode = ModeDefinition.For("TeamRace", RaceFormat.Duo);
            var seats = new[] { new WordboundSeat("me", 0, 0), new WordboundSeat("mate", 0, 1), new WordboundSeat("rival", 1, 0), new WordboundSeat("rival2", 1, 1) };
            var room = VoiceRoom.Match(mode, seats);
            Assert.AreEqual(VoiceScope.Team, room.Scope);
            CollectionAssert.AreEqual(new[] { "mate" }, VoiceButtons.Rows(room, "me").Select(m => m.PlayerId).ToArray(), "Team voice lists only my team.");
            Assert.AreEqual("P1", VoiceButtons.Label(room, "me"));
            Assert.AreEqual("P2", VoiceButtons.Label(room, "mate"));
            Assert.AreEqual("P3", VoiceButtons.Label(room, "rival"));
            Assert.AreEqual("", VoiceButtons.Label(room, "nobody"));
            Assert.AreEqual("", VoiceButtons.Label(null, "me"));
            var crowd = VoiceRoom.Party(Enumerable.Range(0, 9).Select(i => "p" + i));
            Assert.AreEqual(VoiceButtons.MaxRows, VoiceButtons.Rows(crowd, "p0").Count, "A crowd stays a short list.");
            CollectionAssert.IsEmpty(VoiceButtons.Rows(null, "p0"));
        }
    }
}
