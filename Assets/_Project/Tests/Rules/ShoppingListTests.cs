using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class ShoppingListTests
    {
        static ItemCatalogue House()
        {
            var items = TestData.Catalogue();
            items.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            return items;
        }

        static readonly ItemCatalogue Items = House();
        static readonly string[] Maps = { "house_pinwheel.json", "house_courtyard.json", "house_flat.json", "house_terrace.json", "house_walkup.json" };

        static HouseLayout Map(string file) => HouseLayout.FromJson(TestData.Read(file), file);

        static List<ShoppingItem> Draw(string file, int seed) =>
            ShoppingList.Draw(Items.Enabled, ShoppingList.Supply(Map(file)), 10, seed);

        static ShoppingRace Race(params (string word, ShoppingKind kind)[] items)
        {
            var race = new ShoppingRace(items.Select(i => new ShoppingItem(i.word, i.kind)).ToList());
            for (int seat = 0; seat < 4; seat++) race.Join(seat);
            return race;
        }

        static readonly (string, ShoppingKind)[] Sample =
        {
            ("BAT", ShoppingKind.Weapon), ("MAT", ShoppingKind.Deployable), ("PAN", ShoppingKind.Weapon), ("CAKE", ShoppingKind.Tool),
        };

        [Test]
        public void ItemsSortIntoWeaponsDeployablesAndTools()
        {
            ShoppingKind? Kind(string id) => ShoppingList.KindOf(Items.Get(id));
            foreach (string id in new[] { "BAT", "PAN", "SPEAR", "BALL", "BOMB", "PIE", "TOASTER" }) Assert.AreEqual(ShoppingKind.Weapon, Kind(id), id);
            foreach (string id in new[] { "MAT", "CLOCK", "FAN", "PLANT", "SPEAKER", "SOAP" }) Assert.AreEqual(ShoppingKind.Deployable, Kind(id), id);
            foreach (string id in new[] { "APPLE", "CAKE", "FOAM", "SODA", "WATER" }) Assert.AreEqual(ShoppingKind.Tool, Kind(id), id);
            foreach (string id in new[] { "SHIELD", "PLATE", "MIRROR", "SOFA", "TABLE", "BED", "BARREL" }) Assert.IsNull(Kind(id), id);
            Assert.IsNull(ShoppingList.KindOf(null));
        }

        [Test]
        public void SupplyCountsEveryFurnitureLetter()
        {
            var house = Map("house_flat.json");
            var supply = ShoppingList.Supply(house);
            Assert.AreEqual(house.Furniture.Sum(f => f.Word.Length), supply.Count);
            Assert.AreEqual(0, ShoppingList.CopiesIn(new LetterBag(), "BAT"));
            Assert.AreEqual(2, ShoppingList.CopiesIn(LetterBag.FromWord("BBAATTX"), "BAT"));
        }

        [Test]
        public void EveryHouseDrawsFourSpellableItemsWithOneDeployableOrTool([ValueSource(nameof(Maps))] string file)
        {
            var supply = ShoppingList.Supply(Map(file));
            for (int seed = 0; seed < 40; seed++)
            {
                var list = Draw(file, seed);
                Assert.AreEqual(ShoppingList.Length, list.Count, $"{file} seed {seed}");
                Assert.AreEqual(list.Count, list.Select(i => i.Word).Distinct().Count(), $"{file} seed {seed}: {string.Join(",", list)}");
                Assert.AreEqual(1, list.Count(i => i.Kind != ShoppingKind.Weapon), $"{file} seed {seed}: {string.Join(",", list)}");
                foreach (var item in list)
                {
                    Assert.GreaterOrEqual(ShoppingList.CopiesIn(supply, item.Word), ShoppingList.Copies, $"{file}: {item.Word}");
                    Assert.AreEqual(ShoppingList.KindOf(Items.Get(item.Word)), item.Kind, item.Word);
                    Assert.LessOrEqual(item.Word.Length, 10, item.Word);
                }
            }
        }

        [Test]
        public void ScarceLettersKeepAnItemOffTheList()
        {
            var supply = ShoppingList.Supply(Map("house_pinwheel.json"));
            Assert.Less(ShoppingList.CopiesIn(supply, "WATER"), ShoppingList.Copies);
            var pool = ShoppingList.Candidates(Items.Enabled, supply, 10).Select(i => i.Word).ToList();
            CollectionAssert.DoesNotContain(pool, "WATER");
            CollectionAssert.Contains(pool, "BAT");
            CollectionAssert.DoesNotContain(ShoppingList.Candidates(Items.Enabled, supply, 6).Select(i => i.Word), "SPEAKER");
        }

        [Test]
        public void TheSameSeedDrawsTheSameListAndRoundsDiffer()
        {
            CollectionAssert.AreEqual(Draw("house_flat.json", 77), Draw("house_flat.json", 77));
            var lists = Enumerable.Range(1, 6).Select(r => string.Join(",", Draw("house_flat.json", ShoppingList.RoundSeed(1234, r)))).ToList();
            Assert.Greater(lists.Distinct().Count(), 3, string.Join(" | ", lists));
            Assert.AreEqual(ShoppingList.RoundSeed(1234, 2), ShoppingList.RoundSeed(1234, 2));
            Assert.AreNotEqual(ShoppingList.RoundSeed(1234, 2), ShoppingList.RoundSeed(1234, 3));
        }

        [Test]
        public void ADrawFallsBackWhenTheHouseLacksAKind()
        {
            var weaponsOnly = Items.Enabled.Where(i => ShoppingList.KindOf(i) == ShoppingKind.Weapon).ToList();
            var supply = LetterBag.FromWord(string.Concat(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZ", 8)));
            var list = ShoppingList.Draw(weaponsOnly, supply, 10, 5);
            Assert.AreEqual(4, list.Count);
            Assert.IsTrue(list.All(i => i.Kind == ShoppingKind.Weapon));
            var tiny = ShoppingList.Draw(Items.Enabled, LetterBag.FromWord("BATBATBATBAT"), 10, 5);
            CollectionAssert.AreEqual(new[] { "BAT" }, tiny.Select(i => i.Word));
        }

        [Test]
        public void YouWorkDownTheListInOrderSpellingEachItemThenMakingItCount()
        {
            var race = Race(Sample);
            Assert.AreEqual("BAT", race.Wanted(0));
            Assert.IsFalse(race.Count(0, "BAT", ShoppingAct.Hit, 1), "an item must be spelled first");
            Assert.IsFalse(race.Spelled(0, "SOFA"), "only list items are recorded");
            Assert.IsTrue(race.Spelled(0, "BAT"));
            Assert.IsTrue(race.CurrentSpelled(0));
            Assert.IsFalse(race.Count(0, "BAT", ShoppingAct.Place, 2), "a weapon counts only when it lands a hit");
            Assert.IsFalse(race.Count(0, "PAN", ShoppingAct.Hit, 2), "later items do not count yet");
            Assert.IsTrue(race.Count(0, "BAT", ShoppingAct.Hit, 3));
            Assert.AreEqual(1, race.Done(0));
            Assert.AreEqual("MAT", race.Wanted(0));
            Assert.IsFalse(race.CurrentSpelled(0));
            Assert.IsTrue(race.Spelled(0, "PAN"), "an item spelled early is remembered");
            Assert.IsFalse(race.Spelled(0, "BAT"), "a ticked item is not recorded again");
            Assert.IsTrue(race.Spelled(0, "MAT"));
            Assert.IsFalse(race.Count(0, "MAT", ShoppingAct.Use, 4));
            Assert.IsTrue(race.Count(0, "MAT", ShoppingAct.Place, 4));
            Assert.IsTrue(race.Count(0, "PAN", ShoppingAct.Hit, 5));
            Assert.AreEqual(0, race.Done(1), "every player keeps their own progress");
            Assert.AreEqual("BAT", race.Wanted(1));
        }

        [Test]
        public void TheFirstToFinishWinsAndTheRaceThenStops()
        {
            var race = Race(Sample);
            foreach (var (word, kind) in Sample)
            {
                Assert.IsNull(race.Winner);
                race.Spelled(2, word);
                Assert.IsTrue(race.Count(2, word, ShoppingList.ActFor(kind), 10));
            }
            Assert.AreEqual(2, race.Winner);
            Assert.IsTrue(race.Finished(2));
            Assert.IsNull(race.Current(2));
            Assert.IsNull(race.Wanted(2));
            Assert.AreEqual(2, race.Leader());
            race.Spelled(1, "BAT");
            Assert.IsFalse(race.Count(1, "BAT", ShoppingAct.Hit, 11));
            Assert.AreEqual(2, race.Standings()[0]);
        }

        [Test]
        public void AtTimeUpTheMostItemsWinAndTheEarliestLastTickBreaksTies()
        {
            var race = Race(Sample);
            void Tick(int seat, double at)
            {
                var item = race.Current(seat).Value;
                race.Spelled(seat, item.Word);
                Assert.IsTrue(race.Count(seat, item.Word, item.Act, at));
            }
            Tick(0, 10); Tick(0, 50);
            Tick(1, 20); Tick(1, 40);
            Tick(3, 5);
            Assert.AreEqual(1, race.Leader(), "two each: P2 ticked their second item first");
            CollectionAssert.AreEqual(new[] { 1, 0, 3, 2 }, race.Standings());
            Tick(3, 60); Tick(3, 70);
            Assert.AreEqual(3, race.Leader(), "most items ticked wins");
            Assert.IsNull(race.Winner, "nobody finished the list");
        }

        [Test]
        public void NobodyTickingAnythingOrAnExactTieIsADraw()
        {
            var race = Race(Sample);
            Assert.IsNull(race.Leader());
            race.Spelled(0, "BAT");
            race.Spelled(1, "BAT");
            race.Count(0, "BAT", ShoppingAct.Hit, 30);
            race.Count(1, "BAT", ShoppingAct.Hit, 30);
            Assert.IsNull(race.Leader());
            Assert.IsNull(new ShoppingRace(new List<ShoppingItem>()).Leader());
        }

        [Test]
        public void SeatsJoinOnceAndUnknownSeatsHaveNoProgress()
        {
            var race = new ShoppingRace(Sample.Select(i => new ShoppingItem(i.Item1, i.Item2)).ToList());
            race.Join(4);
            race.Join(4);
            CollectionAssert.AreEqual(new[] { 4 }, race.Seats);
            Assert.AreEqual(0, race.Done(9));
            Assert.IsNull(race.Current(9));
            Assert.IsFalse(race.Spelled(9, "BAT"));
            Assert.IsFalse(race.Count(9, "BAT", ShoppingAct.Hit, 1));
        }

        [Test]
        public void TheHouseRulesGiveARaceOfFourItemsFirstToThreeRounds()
        {
            var book = TestData.LoadRuleBook();
            book.Layer(TestData.Read("modes_house.json"));
            var rules = book.For("ShoppingList");
            Assert.AreEqual(3, rules.RoundsToWin);
            Assert.AreEqual(180f, rules.RoundTimeLimitSeconds);
            Assert.AreEqual(3f, rules.RespawnSeconds);
            Assert.IsFalse(rules.ClearOutEnabled, "the Movers stay out of the race");
            Assert.AreEqual(ShoppingList.Length, rules.Setting("items", 0));
            Assert.AreEqual(ShoppingList.Copies, rules.Setting("copies", 0));
        }

        [Test]
        public void PlayerFacingLinesAreShortAndPlain()
        {
            Assert.AreEqual("Spell it", ShoppingList.Prompt(new ShoppingItem("BAT", ShoppingKind.Weapon), false));
            Assert.AreEqual("Hit someone with it", ShoppingList.Prompt(new ShoppingItem("BAT", ShoppingKind.Weapon), true));
            Assert.AreEqual("Place it", ShoppingList.Prompt(new ShoppingItem("MAT", ShoppingKind.Deployable), true));
            Assert.AreEqual("Use it", ShoppingList.Prompt(new ShoppingItem("CAKE", ShoppingKind.Tool), true));
            Assert.AreEqual("2/4", ShoppingList.Progress(2, 4));
            Assert.AreEqual("P2 · 3 of 4 items · 1/3 wins", ShoppingList.ResultLine("P2", 3, 4, 1, 3, false));
            Assert.AreEqual("<b>P3 · 4 of 4 items · 2/3 wins</b>", ShoppingList.ResultLine("P3", 4, 4, 2, 3, true));
            Assert.AreEqual("ROUND 2 · P3 WINS", ShoppingList.Eyebrow(2, false, "P3"));
            Assert.AreEqual("MATCH OVER · P3 WINS", ShoppingList.Eyebrow(5, true, "P3"));
            Assert.AreEqual("ROUND 1 · DRAW", ShoppingList.Eyebrow(1, false, null));
            Assert.AreEqual("ROUND 1 · YOU WIN", ShoppingList.Eyebrow(1, false, "You"));
            Assert.AreEqual("You win Shopping List.", ShoppingList.Flavour("You", true, 3));
            Assert.AreEqual("P3 got everything on the list!", ShoppingList.Heading("P3", true));
            Assert.AreEqual("Time’s up. P3 ticked off the most.", ShoppingList.Heading("P3", false));
            StringAssert.Contains("draw", ShoppingList.Heading(null, false));
            Assert.AreEqual("P3 wins Shopping List.", ShoppingList.Flavour("P3", true, 3));
            Assert.AreEqual("First to 3 rounds wins. Next round, new list.", ShoppingList.Flavour("P3", false, 3));
        }
    }
}
