using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class CameraPushTests
    {
        const float Beat = .5f, Step = .01f, Tolerance = 1e-5f;

        [Test]
        public void NoBeatMeansNoPush()
        {
            Assert.AreEqual(0f, CameraPush.Weight(.2f, 0f));
            Assert.AreEqual(0f, CameraPush.Weight(.2f, -1f));
            Assert.AreEqual(0f, CameraPush.Weight(-.1f, Beat));
            Assert.AreEqual(0f, CameraPush.Weight(float.NaN, Beat));
            Assert.AreEqual(0f, CameraPush.Weight(.2f, float.NaN));
            Assert.AreEqual(0f, CameraPush.Length(0f));
        }

        [Test]
        public void ThePushLeansInHoldsThroughTheBeatAndLetsGo()
        {
            Assert.AreEqual(0f, CameraPush.Weight(0f, Beat), "it starts from the normal shot");
            float rise = Beat * CameraPush.RiseShare;
            Assert.AreEqual(.5f, CameraPush.Weight(rise * .5f, Beat), Tolerance);
            Assert.AreEqual(1f, CameraPush.Weight(rise, Beat));
            Assert.AreEqual(1f, CameraPush.Weight(Beat, Beat), "fully in while the beat plays");
            Assert.AreEqual(.5f, CameraPush.Weight(Beat + CameraPush.ReleaseSeconds * .5f, Beat), Tolerance);
            Assert.AreEqual(Beat + CameraPush.ReleaseSeconds, CameraPush.Length(Beat), Tolerance);
            Assert.AreEqual(0f, CameraPush.Weight(CameraPush.Length(Beat), Beat), "back to the normal shot");
            Assert.AreEqual(0f, CameraPush.Weight(10f, Beat));
        }

        [Test]
        public void ThePushNeverJumps()
        {
            float before = 0f, most = 0f;
            for (float t = 0f; t <= CameraPush.Length(Beat) + Step; t += Step)
            {
                float now = CameraPush.Weight(t, Beat);
                Assert.That(now, Is.InRange(0f, 1f), t.ToString());
                most = System.Math.Max(most, System.Math.Abs(now - before));
                before = now;
            }
            Assert.Less(most, .06f, "eased in and out, never a cut");
            Assert.AreEqual(0f, CameraPush.Weight(CameraPush.Length(Beat) + Step, Beat));
        }

        [Test]
        public void AtRestTheFramingIsExactlyTheNormalShot()
        {
            Assert.AreEqual(12.5f, CameraPush.Narrow(12.5f, 0f));
            Assert.AreEqual(40f, CameraPush.Narrow(40f, float.NaN));
            Assert.AreEqual(0f, CameraPush.Reach(6f, 0f));
            Assert.AreEqual(12.5f * (1f - CameraPush.Zoom), CameraPush.Narrow(12.5f, 1f), Tolerance);
            Assert.AreEqual(CameraPush.Narrow(12.5f, 1f), CameraPush.Narrow(12.5f, 3f), "never closer than the full push");
            Assert.Less(CameraPush.Narrow(12.5f, .5f), 12.5f, "a push only ever closes in");
        }

        [Test]
        public void ThePanMovesPartWayAndNeverFar()
        {
            Assert.AreEqual(0f, CameraPush.Reach(0f, 1f));
            Assert.AreEqual(0f, CameraPush.Reach(-2f, 1f));
            Assert.AreEqual(2f * CameraPush.Pan, CameraPush.Reach(2f, 1f), Tolerance);
            Assert.AreEqual(CameraPush.Pan, CameraPush.Reach(2f, .5f), Tolerance);
            Assert.AreEqual(CameraPush.MaxPan, CameraPush.Reach(100f, 1f), Tolerance, "a winner across the house only tips the shot");
            Assert.Less(CameraPush.Reach(2f, 1f), 2f, "the winner moves toward the middle, never past it");
        }
    }
}
