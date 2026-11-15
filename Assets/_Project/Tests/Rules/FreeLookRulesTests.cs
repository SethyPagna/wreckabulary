using System;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class FreeLookRulesTests
    {
        const float Wide = 1280f / 720f;

        [Test]
        public void YawWrapsToTheShortestTurn()
        {
            Assert.AreEqual(-170f, FreeLook.Wrap(190f), 1e-4f);
            Assert.AreEqual(180f, FreeLook.Wrap(-180f), 1e-4f);
            Assert.AreEqual(10f, FreeLook.Wrap(730f), 1e-4f);
            Assert.AreEqual(0f, FreeLook.Wrap(360f), 1e-4f);
        }

        [Test]
        public void NoOrbitKeepsThePartyCameraOffset()
        {
            var (x, y, z) = FreeLook.Orbit(0f, PartyCamera.Rise, -PartyCamera.Back, 0f, 0f);
            Assert.AreEqual(0f, x, 1e-4f);
            Assert.AreEqual(PartyCamera.Rise, y, 1e-3f);
            Assert.AreEqual(-PartyCamera.Back, z, 1e-3f);
            Assert.AreEqual(FreeLook.BaseElevation, FreeLook.Elevation(x, y, z), 1e-3f);
        }

        [Test]
        public void HalfTurnLandsOnTheFarSideAtTheSameDistance()
        {
            var (x, y, z) = FreeLook.Orbit(0f, PartyCamera.Rise, -PartyCamera.Back, 180f, 0f);
            Assert.AreEqual(0f, x, 1e-3f);
            Assert.AreEqual(PartyCamera.Rise, y, 1e-3f);
            Assert.AreEqual(PartyCamera.Back, z, 1e-3f);
            var (rx, _, rz) = FreeLook.Orbit(0f, PartyCamera.Rise, -PartyCamera.Back, 90f, 0f);
            Assert.Less(rx, -PartyCamera.Back + 1e-3f, "A positive yaw looks right, so the camera swings to the left of the pivot.");
            Assert.AreEqual(0f, rz, 1e-3f);
        }

        [Test]
        public void PitchNeverDropsTheCameraBelowTheFloorOrPastOverhead()
        {
            foreach (float pitch in new[] { -500f, -90f, -24f, 0f, 31f, 90f, 500f })
            {
                float clamped = FreeLook.OverheadPitch(pitch);
                var (x, y, z) = FreeLook.Orbit(0f, PartyCamera.Rise, -PartyCamera.Back, 45f, clamped);
                float elevation = FreeLook.Elevation(x, y, z);
                Assert.That(elevation, Is.InRange(FreeLook.MinElevation - 1e-3f, FreeLook.MaxElevation + 1e-3f), pitch.ToString());
                Assert.Greater(y, 0f, "The camera stays above the floor.");
                var (_, raw, _) = FreeLook.Orbit(0f, PartyCamera.Rise, -PartyCamera.Back, 0f, pitch);
                Assert.Greater(raw, 0f, "Orbit itself clamps an unclamped pitch too.");
            }
            Assert.AreEqual(0f, FreeLook.OverheadPitch(0f));
            Assert.AreEqual(5f, FreeLook.ClampPitch(20f, 10f, -30f, 15f), 1e-4f);
            Assert.AreEqual(-40f, FreeLook.ClampPitch(-90f, 10f, -30f, 15f), 1e-4f);
        }

        [Test]
        public void ReleasedViewEasesBackAndSettlesExactlyOnZero()
        {
            float yaw = 170f, pitch = -20f;
            float previous = Math.Abs(yaw);
            int frames = 0;
            while ((yaw != 0f || pitch != 0f) && frames < 600)
            {
                yaw = FreeLook.Settle(yaw, 1f / 60f);
                pitch = FreeLook.Settle(pitch, 1f / 60f);
                Assert.LessOrEqual(Math.Abs(yaw), previous);
                previous = Math.Abs(yaw);
                frames++;
            }
            Assert.AreEqual(0f, yaw);
            Assert.AreEqual(0f, pitch);
            Assert.Less(frames, 90, "Back to the normal angle in well under a second and a half.");
            Assert.AreEqual(40f, FreeLook.Settle(40f, 0f));
        }

        [Test]
        public void DragSpeedIsTheSameAtEveryResolution()
        {
            Assert.AreEqual(FreeLook.DragDegrees(108f, 1080f), FreeLook.DragDegrees(216f, 2160f), 1e-4f);
            Assert.AreEqual(FreeLook.DegreesPerScreen, FreeLook.DragDegrees(720f, 720f), 1e-3f);
            Assert.AreEqual(0f, FreeLook.DragDegrees(0f, 0f));
        }

        [Test]
        public void OrbitWidensTheFrameSoASideOnViewKeepsTheParty()
        {
            Assert.AreEqual(12f, FreeLook.WidenedSize(12f, Wide, 0f));
            float quarter = FreeLook.WidenedSize(12f, Wide, 90f);
            float across = 12f * Wide;
            Assert.GreaterOrEqual(quarter * Wide, 12f / PartyCamera.DepthScale - 1e-3f, "The old depth now runs across the screen and still fits.");
            Assert.GreaterOrEqual(quarter / PartyCamera.DepthScale, across - 1e-3f, "The old width now runs up the screen and still fits.");
            Assert.AreEqual(quarter, FreeLook.WidenedSize(12f, Wide, -90f), 1e-3f);
            Assert.AreEqual(12f, FreeLook.WidenedSize(12f, Wide, 180f), 1e-3f);
            Assert.GreaterOrEqual(FreeLook.WidenedSize(12f, Wide, 45f), 12f);
        }
    }
}
