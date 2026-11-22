using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class SeatVitalsTests
    {
        [Test]
        public void ShareClampsToTheBar()
        {
            Assert.AreEqual(1f, SeatVitals.Share(100f, 100f));
            Assert.AreEqual(.5f, SeatVitals.Share(50f, 100f), 1e-6f);
            Assert.AreEqual(0f, SeatVitals.Share(0f, 100f));
            Assert.AreEqual(0f, SeatVitals.Share(-5f, 100f), "Overkill never draws backwards.");
            Assert.AreEqual(1f, SeatVitals.Share(150f, 100f), "Extra health never overflows the track.");
            Assert.AreEqual(0f, SeatVitals.Share(10f, 0f));
            Assert.AreEqual(0f, SeatVitals.Share(float.NaN, 100f));
        }

        [Test]
        public void HealthTurnsAmberAtHalfAndRedAtAQuarter()
        {
            Assert.AreEqual(HealthBand.Healthy, SeatVitals.Band(1f));
            Assert.AreEqual(HealthBand.Healthy, SeatVitals.Band(.51f));
            Assert.AreEqual(HealthBand.Hurt, SeatVitals.Band(.5f), "Half or less is amber.");
            Assert.AreEqual(HealthBand.Hurt, SeatVitals.Band(.26f));
            Assert.AreEqual(HealthBand.Critical, SeatVitals.Band(.25f), "A quarter or less is red.");
            Assert.AreEqual(HealthBand.Critical, SeatVitals.Band(0f));
        }

        [Test]
        public void TheNumberRoundsUpWhileAliveAndHidesWhenDown()
        {
            Assert.AreEqual("100", SeatVitals.Label(SeatVitals.Reading(99.2f, true, false, 0f, false)));
            Assert.AreEqual("1", SeatVitals.Label(SeatVitals.Reading(.3f, true, false, 0f, false)), "A sliver of health still reads as alive.");
            Assert.AreEqual(SeatVitals.Hidden, SeatVitals.Reading(0f, false, true, 6.2f, false), "A downed seat card says REVIVE in its portrait instead.");
            Assert.AreEqual("", SeatVitals.Label(SeatVitals.Reading(0f, false, false, 0f, true)), "A wrecked player shows no number.");
            Assert.AreEqual("DOWN 7s", SeatVitals.Label(SeatVitals.Reading(0f, false, true, 6.2f, true)), "Playing alone, the bar counts down the revive window.");
            Assert.AreEqual("DOWN 0s", SeatVitals.Label(SeatVitals.Reading(0f, false, true, -1f, true)));
            Assert.AreNotEqual(SeatVitals.Reading(0f, false, true, 6.2f, true), SeatVitals.Reading(0f, false, true, 5.2f, true), "Each second redraws.");
        }

        [Test]
        public void StaminaIsWindedBelowOneDodgeAndHiddenWhenDown()
        {
            Assert.IsTrue(SeatVitals.Winded(29.9f, 30f));
            Assert.IsFalse(SeatVitals.Winded(30f, 30f), "Exactly one dodge left is not winded.");
            Assert.IsFalse(SeatVitals.ShowsStamina(false));
            Assert.AreEqual(PlayerBar.StaminaShown, SeatVitals.ShowsStamina(true));
        }
    }
}
