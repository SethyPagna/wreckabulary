using NUnit.Framework;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class VoiceLinkTests
    {
        static VoiceLinkStats Link(float loss, float late = 0, float jitter = 0) => VoiceLinkStats.Report(loss, late, jitter, 60);

        [Test]
        public void GradesFollowLossLateAndJitter()
        {
            Assert.AreEqual(VoiceLinkQuality.Unknown, VoiceLinkGrades.Grade(default));
            Assert.AreEqual(VoiceLinkQuality.Good, VoiceLinkGrades.Grade(Link(0)));
            Assert.AreEqual(VoiceLinkQuality.Good, VoiceLinkGrades.Grade(Link(2.9f)));
            Assert.AreEqual(VoiceLinkQuality.Fair, VoiceLinkGrades.Grade(Link(2, 1)));
            Assert.AreEqual(VoiceLinkQuality.Fair, VoiceLinkGrades.Grade(Link(0, 0, 25)));
            Assert.AreEqual(VoiceLinkQuality.Poor, VoiceLinkGrades.Grade(Link(10)));
            Assert.AreEqual(VoiceLinkQuality.Poor, VoiceLinkGrades.Grade(Link(0, 0, 60)));
            Assert.AreEqual(VoiceLinkQuality.Good, VoiceLinkGrades.Grade(Link(float.NaN, -3, float.NaN)));
            Assert.AreEqual(3, VoiceLinkGrades.Bars(VoiceLinkQuality.Good));
            Assert.AreEqual(2, VoiceLinkGrades.Bars(VoiceLinkQuality.Fair));
            Assert.AreEqual(1, VoiceLinkGrades.Bars(VoiceLinkQuality.Poor));
            Assert.AreEqual(0, VoiceLinkGrades.Bars(VoiceLinkQuality.Unknown));
            Assert.AreEqual("Weak connection", VoiceLinkGrades.Tip(VoiceLinkQuality.Poor));
            Assert.AreEqual("", VoiceLinkGrades.Tip(VoiceLinkQuality.Fair));
        }

        [Test]
        public void TheMeterWorsensAfterOneSecondAndRecoversAfterThree()
        {
            var meter = new VoiceLinkMeter();
            Assert.IsFalse(meter.Update(default, 0), "no data, no grade");
            Assert.AreEqual(VoiceLinkQuality.Unknown, meter.Quality);
            Assert.IsTrue(meter.Update(Link(0), 0), "the first report sets the grade at once");
            Assert.AreEqual(VoiceLinkQuality.Good, meter.Quality);
            Assert.IsFalse(meter.Update(Link(20), 1));
            Assert.IsFalse(meter.Update(Link(20), 1.9));
            Assert.IsTrue(meter.Update(Link(20), 2));
            Assert.AreEqual(VoiceLinkQuality.Poor, meter.Quality);
            Assert.IsTrue(meter.TipShown(2), "Weak connection shows when the link turns poor");
            Assert.IsTrue(meter.TipShown(5.9));
            Assert.IsFalse(meter.TipShown(6), "and is short");
            Assert.IsFalse(meter.Update(Link(0), 7));
            Assert.IsFalse(meter.Update(Link(20), 8), "a blip resets the wait");
            Assert.IsFalse(meter.Update(Link(0), 9));
            Assert.IsFalse(meter.Update(Link(0), 11.9));
            Assert.IsTrue(meter.Update(Link(0), 12));
            Assert.AreEqual(VoiceLinkQuality.Good, meter.Quality);
            Assert.IsFalse(meter.TipShown(12));
        }

        [Test]
        public void TheBoardTracksEachTalkerAndForgetsLeavers()
        {
            var board = new VoiceLinkBoard();
            Assert.IsTrue(board.Update("a", Link(0), 0));
            Assert.IsTrue(board.Update("b", Link(30), 0));
            Assert.IsFalse(board.Update(null, Link(0), 0));
            Assert.AreEqual(VoiceLinkQuality.Good, board.Quality("a"));
            Assert.AreEqual(VoiceLinkQuality.Poor, board.Quality("b"));
            Assert.IsTrue(board.TipShown("b", 1));
            Assert.IsFalse(board.TipShown("a", 1));
            Assert.AreEqual(VoiceLinkQuality.Unknown, board.Quality("c"));
            Assert.IsTrue(board.Keep(id => id == "a"));
            Assert.AreEqual(1, board.Count);
            Assert.AreEqual(VoiceLinkQuality.Unknown, board.Quality("b"));
        }

        [Test]
        public void TheSendQualityStepsDownUnderSustainedLossAndBackUpWhenClean()
        {
            var policy = new VoiceQualityPolicy();
            Assert.AreEqual(32000, policy.Current.Bitrate);
            Assert.IsFalse(policy.Current.Fec);
            Assert.IsFalse(policy.Update(Link(8), 0));
            Assert.IsFalse(policy.Update(Link(8), 1.9));
            Assert.IsTrue(policy.Update(Link(8), 2), "two seconds of loss steps down");
            Assert.AreEqual(1, policy.Step);
            Assert.IsTrue(policy.Current.Fec, "the first step turns on forward error correction");
            Assert.IsFalse(policy.Update(Link(8), 3), "one step at a time");
            Assert.IsTrue(policy.Update(Link(8), 4));
            Assert.IsTrue(policy.Update(Link(8), 6));
            Assert.AreEqual(3, policy.Step);
            Assert.IsFalse(policy.Update(Link(8), 20), "the lowest step is the floor");
            Assert.AreEqual(12000, policy.Current.Bitrate);
            Assert.AreEqual(60, policy.Current.PacketMs);
            Assert.IsFalse(policy.Update(Link(2), 21), "a fair link holds the step");
            Assert.IsFalse(policy.Update(Link(0), 22));
            Assert.IsFalse(policy.Update(Link(0), 29.9));
            Assert.IsTrue(policy.Update(Link(0), 30), "eight clean seconds step back up");
            Assert.AreEqual(2, policy.Step);
            Assert.IsFalse(policy.Update(default, 100), "no report changes nothing");
            policy.Reset();
            Assert.AreEqual(0, policy.Step);
            Assert.AreEqual("32 kbps, 20 ms", policy.Current.ToString());
        }
    }
}
