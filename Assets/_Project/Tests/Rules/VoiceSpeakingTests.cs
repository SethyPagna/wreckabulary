using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class VoiceSpeakingTests
    {
        [Test]
        public void SpeakingShowsAtOnceAndHoldsBrieflyAfterStopping()
        {
            var tracker = new SpeakingTracker();
            Assert.IsTrue(tracker.Report("pal", true, 10));
            Assert.IsTrue(tracker.IsSpeaking("pal", 10));
            Assert.IsFalse(tracker.Report("pal", false, 11), "the hold keeps the indicator on");
            Assert.IsTrue(tracker.IsSpeaking("pal", 11 + SpeakingTracker.HoldSeconds - .01));
            Assert.IsFalse(tracker.Tick(11 + SpeakingTracker.HoldSeconds - .01));
            Assert.IsTrue(tracker.Tick(11 + SpeakingTracker.HoldSeconds));
            Assert.IsFalse(tracker.IsSpeaking("pal", 11 + SpeakingTracker.HoldSeconds));
            Assert.IsFalse(tracker.Tick(20), "an expired hold reports once");
        }

        [Test]
        public void ShortGapsBetweenWordsNeverFlicker()
        {
            var tracker = new SpeakingTracker();
            int changes = 0;
            double now = 0;
            if (tracker.Report("pal", true, now)) changes++;
            for (int i = 0; i < 20; i++)
            {
                now += .1;
                if (tracker.Report("pal", i % 2 == 0 ? false : true, now)) changes++;
                if (tracker.Tick(now + .05)) changes++;
                Assert.IsTrue(tracker.IsSpeaking("pal", now + .05), "still speaking at " + now);
            }
            Assert.AreEqual(1, changes, "one change for a whole sentence with gaps under the hold");
        }

        [Test]
        public void SeveralSpeakersAreListedInIdOrder()
        {
            var tracker = new SpeakingTracker();
            tracker.Report("b", true, 0);
            tracker.Report("a", true, 0);
            tracker.Report("c", true, 0);
            tracker.Report("c", false, 1);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, tracker.Speaking(1.1));
            CollectionAssert.AreEqual(new[] { "a", "b" }, tracker.Speaking(2));
            tracker.Forget("a");
            CollectionAssert.AreEqual(new[] { "b" }, tracker.Speaking(2));
            tracker.Clear();
            CollectionAssert.IsEmpty(tracker.Speaking(2));
            Assert.IsFalse(tracker.Report(null, true, 3));
            Assert.IsFalse(tracker.Report("quiet", false, 3), "stopping without starting changes nothing");
        }

        [Test]
        public void TheMeterReadsSilenceAsZeroAndLoudSpeechNearFull()
        {
            Assert.AreEqual(0f, VoiceMeter.Level(null, 10));
            Assert.AreEqual(0f, VoiceMeter.Level(new float[64], 64));
            Assert.AreEqual(0f, VoiceMeter.Level(new float[] { 1f }, 0));
            var loud = Enumerable.Range(0, 480).Select(i => (float)Math.Sin(i * .2)).ToArray();
            Assert.Greater(VoiceMeter.Level(loud, loud.Length), .9f);
            var quiet = loud.Select(s => s * .001f).ToArray();
            Assert.Less(VoiceMeter.Level(quiet, quiet.Length), .1f, "-63 dB sits on the floor");
            var broken = new[] { float.NaN, float.PositiveInfinity, .5f, -.5f };
            float level = VoiceMeter.Level(broken, 99);
            Assert.That(level, Is.InRange(0f, 1f));
        }

        [Test]
        public void TheMeterRisesAtOnceAndFallsSmoothly()
        {
            Assert.AreEqual(.9f, VoiceMeter.Smooth(.1f, .9f, .016f), 1e-6f);
            float falling = VoiceMeter.Smooth(.9f, 0f, .1f);
            Assert.AreEqual(.9f - VoiceMeter.ReleasePerSecond * .1f, falling, 1e-5f);
            Assert.AreEqual(0f, VoiceMeter.Smooth(.1f, 0f, 1f));
            Assert.AreEqual(1f, VoiceMeter.Smooth(0f, 5f, 0f));
            Assert.AreEqual(0f, VoiceMeter.Smooth(float.NaN, 0f, 1f));
        }
    }
}
