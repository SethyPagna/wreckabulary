using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class BotTacticsTests
    {
        const float Gravity = 9.81f;

        static readonly ItemCatalogue Items = House();

        static ItemCatalogue House()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            return items;
        }

        static ItemDefinition Item(string id) => Items.Get(id);

        [Test]
        public void BotsStrikeFromTheReachOfWhatTheyHold()
        {
            Assert.AreEqual(BotTactics.FistRange, BotTactics.StrikeRange(null));
            Assert.AreEqual(Item("SPEAR").Melee.Reach + BotTactics.ReachSlack, BotTactics.StrikeRange(Item("SPEAR")), 1e-4f);
            Assert.Greater(BotTactics.StrikeRange(Item("SPEAR")), BotTactics.StrikeRange(Item("BAT")));
            Assert.Greater(BotTactics.StrikeRange(Item("BAT")), BotTactics.FistRange);
            foreach (string notASwing in new[] { "ARROW", "SHIELD", "PLANT", "TABLE", "APPLE" })
                Assert.AreEqual(BotTactics.FistRange, BotTactics.StrikeRange(Item(notASwing)), notASwing);
            Assert.AreEqual(Item("BROOM").Melee.ArcDegrees * .5f, BotTactics.AimDegrees(Item("BROOM"), 90f), 1e-4f);
            Assert.AreEqual(Item("SPEAR").Melee.ArcDegrees * .5f, BotTactics.AimDegrees(Item("SPEAR"), 90f), 1e-4f, "a narrow thrust waits until it lines up");
            Assert.AreEqual(45f, BotTactics.AimDegrees(null, 90f), 1e-4f, "fists use their own arc");
            Assert.AreEqual(BotTactics.ThrowAimDegrees, BotTactics.AimDegrees(Item("ARROW"), 90f));
            Assert.AreEqual(BotTactics.MinAimDegrees, BotTactics.AimDegrees(null, 2f), "never a pinpoint aim");
        }

        [Test]
        public void OnlyThrownItemsAreThrown()
        {
            Assert.IsTrue(BotTactics.Throws(Item("ARROW")));
            Assert.IsTrue(BotTactics.Throws(Item("BOMB")));
            Assert.IsFalse(BotTactics.Throws(Item("BAT")));
            Assert.IsFalse(BotTactics.Throws(Item("APPLE")));
            Assert.IsFalse(BotTactics.Throws(null));
        }

        [Test]
        public void FlatThrowsHitFromPointBlankAndFasterOnesReachFurther()
        {
            var arrow = BotTactics.Window(Item("ARROW").Thrown, Gravity);
            var toaster = BotTactics.Window(Item("TOASTER").Thrown, Gravity);
            Assert.AreEqual(0f, arrow.Near);
            Assert.That(arrow.Far, Is.InRange(8f, 11f), arrow.ToString());
            Assert.Less(toaster.Far, arrow.Far);
            Assert.IsTrue(arrow.Contains(2f) && arrow.Contains(8f));
            Assert.IsFalse(arrow.Contains(12f));
        }

        [Test]
        public void LobsLandInARingFurtherOut()
        {
            var pot = BotTactics.Window(Item("POT").Thrown, Gravity);
            Assert.Greater(pot.Near, 5f, pot.ToString());
            Assert.Greater(pot.Far, pot.Near);
            Assert.IsFalse(pot.Contains(2f), "a lob sails over someone standing close");
            Assert.IsTrue(pot.Contains((pot.Near + pot.Far) * .5f));
            Assert.IsTrue(BotTactics.Window(null, Gravity).IsEmpty);
            Assert.IsTrue(BotTactics.Window(Item("POT").Thrown, 0f).IsEmpty);
        }

        [Test]
        public void OnlyTheHammerWindsUpLongEnoughToDodge()
        {
            var telegraphed = Items.Enabled.Where(BotTactics.Swings).Where(i => BotTactics.Telegraphed(i.Melee)).Select(i => i.Id);
            CollectionAssert.AreEqual(new[] { "HAMMER" }, telegraphed);
            var hammer = Item("HAMMER").Melee;
            Assert.IsTrue(BotTactics.Threatens(hammer, 1.5f, 0f));
            Assert.IsFalse(BotTactics.Threatens(hammer, hammer.Reach + BotTactics.DodgeMargin + .5f, 0f), "out of reach");
            Assert.IsFalse(BotTactics.Threatens(hammer, 1f, 150f), "swinging the other way");
            Assert.IsFalse(BotTactics.Threatens(Item("BAT").Melee, 1f, 0f), "too quick to read");
        }

        [Test]
        public void JunkIsTheLetterNoWantedRecipeNeeds()
        {
            Assert.AreEqual('Q', BotTactics.Junk("BATQ".ToCharArray(), new[] { "BAT" }));
            Assert.AreEqual('B', BotTactics.Junk("BBAT".ToCharArray(), new[] { "BAT" }), "a spare copy goes before a needed one");
            Assert.AreEqual('Z', BotTactics.Junk("SPEARZ".ToCharArray(), new[] { "SPEAR", "BAT" }));
            Assert.AreEqual('B', BotTactics.Junk("ABB".ToCharArray(), new string[0]), "with nothing wanted the most repeated letter goes");
            Assert.AreEqual('\0', BotTactics.Junk(new char[0], new[] { "BAT" }));
            Assert.AreEqual('Q', BotTactics.Junk("QBATZ".ToCharArray(), new[] { "BAT" }), "of two equal junk letters the first in the bag goes");
        }

        [Test]
        public void AFullBagOnlyTakesLettersAWantedRecipeStillNeeds()
        {
            Assert.IsTrue(BotTactics.Helps("BA".ToCharArray(), 'T', new[] { "BAT" }));
            Assert.IsTrue(BotTactics.Helps("BA".ToCharArray(), 't', new[] { "BAT" }));
            Assert.IsFalse(BotTactics.Helps("BAT".ToCharArray(), 'T', new[] { "BAT" }));
            Assert.IsFalse(BotTactics.Helps("BA".ToCharArray(), 'Q', new[] { "BAT" }));
            Assert.IsTrue(BotTactics.Helps("HAMER".ToCharArray(), 'M', new[] { "HAMMER" }), "a second copy can still help");
        }
    }
}
