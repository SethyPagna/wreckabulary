using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class CourseLookTests
    {
        const float Frame = 1f / 60f, Low = -25.8f, High = 54.4f, Steady = 9.2f;

        [Test]
        public void OrbitTurnsTheShortWayAround()
        {
            var look = new CourseLook();
            look.Orbit(200f, 0f, Steady, Low, High);
            Assert.AreEqual(-160f, look.Yaw, 1e-4f);
            look.Orbit(-40f, 0f, Steady, Low, High);
            Assert.AreEqual(160f, look.Yaw, 1e-4f);
            Assert.IsTrue(look.Orbiting);
        }

        [Test]
        public void PitchStaysInsideTheViewLimitsAroundTheSteadyPitch()
        {
            var look = new CourseLook();
            look.Orbit(0f, 90f, Steady, Low, High);
            Assert.AreEqual(High - Steady, look.Pitch, 1e-4f);
            look.Orbit(0f, -500f, Steady, Low, High);
            Assert.AreEqual(Low - Steady, look.Pitch, 1e-4f);
            look.Update(Frame, Low, Low, High);
            Assert.AreEqual(0f, look.Pitch, 1e-4f, "A lower steady pitch pulls the offset back inside the limits.");
        }

        [Test]
        public void HoldingKeepsTheOrbitAndLettingGoEasesBackToExactlyZero()
        {
            var look = new CourseLook();
            look.Hold(true);
            look.Orbit(180f, 20f, Steady, Low, High);
            for (int i = 0; i < 120; i++) look.Update(Frame, Steady, Low, High);
            Assert.AreEqual(180f, look.Yaw, 1e-4f);
            Assert.AreEqual(20f, look.Pitch, 1e-4f);
            look.Hold(false);
            float before = look.Yaw;
            int frames = 0;
            while (look.Orbiting && frames < 600)
            {
                look.Update(Frame, Steady, Low, High);
                Assert.LessOrEqual(look.Yaw, before);
                before = look.Yaw;
                frames++;
            }
            Assert.AreEqual(0f, look.Yaw);
            Assert.AreEqual(0f, look.Pitch);
            Assert.LessOrEqual(frames, 60, "The view is back behind the player within a second.");
        }

        [Test]
        public void EyeLeasesPairUpAndKeepTheOrbitUntilTheLastOneEnds()
        {
            var look = new CourseLook();
            look.BeginLook(); look.BeginLook();
            Assert.IsTrue(look.Dragging && look.Looking);
            look.Orbit(45f, 0f, Steady, Low, High);
            look.EndLook();
            look.Update(1f, Steady, Low, High);
            Assert.AreEqual(45f, look.Yaw, 1e-4f, "Another eye lease is still open.");
            look.EndLook(); look.EndLook();
            Assert.IsFalse(look.Dragging || look.Looking);
            look.Update(1f, Steady, Low, High);
            Assert.IsFalse(look.Orbiting);
            look.BeginLook();
            Assert.IsTrue(look.Dragging, "An extra EndLook never leaves a negative count behind.");
        }

        [Test]
        public void NonFiniteTurnsAreIgnored()
        {
            var look = new CourseLook();
            look.Orbit(30f, 5f, Steady, Low, High);
            look.Orbit(float.NaN, 0f, Steady, Low, High);
            look.Orbit(0f, float.PositiveInfinity, Steady, Low, High);
            Assert.AreEqual(30f, look.Yaw, 1e-4f);
            Assert.AreEqual(5f, look.Pitch, 1e-4f);
        }

        [Test]
        public void RecenterKeepsEyeLeasesAndClearDropsThem()
        {
            var look = new CourseLook();
            look.BeginLook(); look.Hold(true);
            look.Orbit(30f, 5f, Steady, Low, High);
            look.Recenter();
            Assert.IsFalse(look.Orbiting || look.Held);
            Assert.IsTrue(look.Dragging);
            look.Clear();
            Assert.IsFalse(look.Dragging || look.Looking);
        }
    }
}
