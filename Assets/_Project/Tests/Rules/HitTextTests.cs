using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HitTextTests
    {
        [TestCase(12f, "12")]
        [TestCase(11.2f, "12")]
        [TestCase(.4f, "1")]
        [TestCase(140f, "140")]
        [TestCase(0f, "")]
        [TestCase(-3f, "")]
        public void AHitShowsJustTheRoundedUpNumber(float amount, string shown) => Assert.AreEqual(shown, HitText.Damage(amount));

        [TestCase("12", true)]
        [TestCase("7", true)]
        [TestCase("12 <size=60%>DMG</size>", false)]
        [TestCase("+8 <size=60%>PTS</size>", false)]
        [TestCase("+12", false)]
        [TestCase("CRUSH!", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void OnlyABareNumberCountsAsADamageNumber(string text, bool number) => Assert.AreEqual(number, HitText.IsDamageNumber(text));
    }
}
