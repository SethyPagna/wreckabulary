using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class RiddlesTests
    {
        static RiddleBook Book() => RiddleBook.FromJson(TestData.Read(RiddleBook.FileName));

        static HashSet<string> HouseRecipes()
        {
            var catalogue = TestData.Catalogue();
            catalogue.Expand(ItemCatalogue.FromJson(TestData.Read("items_house.json"), "items_house.json"));
            return new HashSet<string>(catalogue.Enabled.Select(i => i.Id), StringComparer.Ordinal);
        }

        static IEnumerable<HouseLayout> Houses() =>
            Directory.GetFiles(Path.Combine(TestData.Root, "Assets/_Project/Data/Config"), "house_*.json").OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => HouseLayout.FromJson(File.ReadAllText(f), Path.GetFileName(f)));

        static RiddleBook Small() => new RiddleBook(new[]
        {
            new Riddle("TABLE", "Four legs and a top (5)"),
            new Riddle("BED", "You sleep in me (3)"),
            new Riddle("LAMP", "Click me on (4)"),
            new Riddle("CABINET", "A cupboard with doors (7)"),
        });

        static RiddleRace Race(RiddleBook book = null, int seats = 4, ulong seed = 7, int target = RiddleRace.PointsToWin) =>
            new RiddleRace(new RiddleDeck((book ?? Small()).All, seed), seats, seed, target);

        [Test]
        public void EveryShippedRiddleIsAHouseRecipeWithAMatchingLengthMarker()
        {
            var book = Book();
            CollectionAssert.IsEmpty(book.Validate(HouseRecipes()));
            Assert.That(book.All.Count, Is.InRange(36, 45));
            Assert.IsTrue(book.All.All(r => r.Hint.Length > 0), "every shipped riddle carries a hint");
        }

        [Test]
        public void TheHouseRulesTuneRiddleRushAsBriefed()
        {
            var book = TestData.LoadRuleBook();
            book.Layer(TestData.Read("modes_house.json"));
            var rules = book.For(HouseModes.RiddleRush);
            Assert.AreEqual(RiddleRace.PointsToWin, rules.Setting("pointsToWin", 0));
            Assert.AreEqual(RiddleRace.HintSeconds, rules.Setting("hintSeconds", 0f));
            Assert.AreEqual(RiddleRace.BeatSeconds, rules.Setting("beatSeconds", 0f));
            Assert.AreEqual(RiddleRace.BotKnowsMin, rules.Setting("botKnowsMin", 0f));
            Assert.AreEqual(RiddleRace.BotKnowsMax, rules.Setting("botKnowsMax", 0f));
            Assert.AreEqual(3f, rules.RespawnSeconds);
            Assert.AreEqual(1, rules.TeamSize);
            Assert.IsFalse(rules.ClearOutEnabled);
            Assert.Greater(rules.RoundTimeLimitSeconds, RiddleRace.HintSeconds * RiddleRace.PointsToWin);
            Assert.IsTrue(HouseModes.Find(HouseModes.RiddleRush).BotsPlay);
        }

        [Test]
        public void EveryHouseSuppliesTheRiddlesItKeeps()
        {
            var book = Book();
            int maxLetters = TestData.HouseRulesFor("Dibs").MaxLetters;
            foreach (var house in Houses())
            {
                var supply = RiddleBook.Supply(house);
                var kept = book.For(house, maxLetters, HouseRecipes());
                Assert.GreaterOrEqual(kept.Count, 30, house.Name);
                foreach (var riddle in book.All)
                    Assert.AreEqual(supply.Contains(riddle.Letters) && riddle.Answer.Length <= maxLetters, kept.Contains(riddle), house.Name + " " + riddle.Answer);
            }
        }

        [Test]
        public void TheHintAlwaysPointsAtFurnitureThatHoldsTheAnswer()
        {
            var book = Book();
            foreach (var house in Houses())
            {
                var words = house.Furniture.Select(f => f.Word).ToList();
                foreach (var riddle in book.For(house, 99))
                {
                    var sources = RiddleHints.Sources(riddle.Answer, words);
                    Assert.IsNotEmpty(sources, house.Name + " " + riddle.Answer);
                    var held = new LetterBag();
                    foreach (int i in sources) held.Add(LetterBag.FromWord(words[i]));
                    Assert.IsTrue(sources.All(i => words[i] == riddle.Answer) || held.Contains(riddle.Letters), house.Name + " " + riddle.Answer);
                }
            }
        }

        [Test]
        public void ValidationCatchesBrokenRiddles()
        {
            var recipes = new[] { "TABLE", "BED", "LAMP", "PAN" };
            var book = new RiddleBook(new[]
            {
                new Riddle("TABLE", "Four legs and a top (4)"),
                new Riddle("SWORD", "Pointy and sharp (5)"),
                new Riddle("BED", "A bed is where you sleep (3)"),
                new Riddle("LAMP", "It glows beside your beds (4)"),
                new Riddle("PAN", "", "hint"),
                new Riddle("PAN", "Flat and hot (3)", "Flip pancakes in me."),
                new Riddle("B3D", "Not a word (3)"),
                new Riddle("LAMP", "It glows beside your beds (4)"),
                new Riddle("TABLE", new string('x', RiddleBook.MaxClueLength) + " (5)"),
            });
            var problems = book.Validate(recipes);
            void Has(string text) => Assert.IsTrue(problems.Any(p => p.Contains(text)), $"expected '{text}' in:\n{string.Join("\n", problems)}");
            Has("TABLE: the clue must end with (5)");
            Has("SWORD is not a House recipe word");
            Has("BED: the clue gives the answer away");
            Has("LAMP: the clue names BED, another answer");
            Has("PAN has no clue");
            Has("PAN has more than one riddle");
            Has("PAN: the hint gives the answer away");
            Has("B3D: the answer must be A-Z letters");
            Has("LAMP shares its clue with another riddle");
            Has($"keep it to {RiddleBook.MaxClueLength}");
            CollectionAssert.IsEmpty(Small().Validate(new[] { "TABLE", "BED", "LAMP", "CABINET" }));
            CollectionAssert.IsNotEmpty(new RiddleBook(Array.Empty<Riddle>()).Validate(recipes));
        }

        [Test]
        public void TheLengthMarkerIsTheTrailingNumber()
        {
            Assert.AreEqual(5, RiddleBook.LengthMarker("I have four legs and a top, but never walk (5)"));
            Assert.AreEqual(12, RiddleBook.LengthMarker("Long (12)"));
            Assert.AreEqual(-1, RiddleBook.LengthMarker("No marker"));
            Assert.AreEqual(-1, RiddleBook.LengthMarker("Odd (x)"));
            Assert.AreEqual(-1, RiddleBook.LengthMarker("Signed (-3)"));
            Assert.AreEqual(-1, RiddleBook.LengthMarker(""));
        }

        [Test]
        public void ParsingNeedsARiddleListAndTheHintIsOptional()
        {
            Assert.Throws<FormatException>(() => RiddleBook.FromJson("{\"schema\": 1}"));
            Assert.Throws<FormatException>(() => RiddleBook.FromJson("{\"riddles\": [{\"answer\": \"BED\"}]}"));
            var book = RiddleBook.FromJson("{\"riddles\": [{\"answer\": \" bed \", \"clue\": \"You sleep in me (3)\"}]}");
            Assert.AreEqual("BED", book.All[0].Answer);
            Assert.AreEqual("", book.All[0].Hint);
        }

        [Test]
        public void TheSupplyFilterKeepsOnlyAnswersTheLettersCanSpell()
        {
            var book = Small();
            var supply = LetterBag.FromWord("TABLEBEDCAINT");
            CollectionAssert.AreEqual(new[] { "TABLE", "BED", "CABINET" }, book.For(supply, 10).Select(r => r.Answer));
            CollectionAssert.AreEqual(new[] { "TABLE", "BED" }, book.For(supply, 5).Select(r => r.Answer));
            CollectionAssert.AreEqual(new[] { "BED" }, book.For(supply, 10, new[] { "BED", "LAMP" }).Select(r => r.Answer));
            CollectionAssert.IsEmpty(book.For((LetterBag)null, 10));
            var house = new HouseLayout();
            house.Furniture.Add(new FurniturePlacement { Word = "LAMP", Room = "Den" });
            house.Furniture.Add(new FurniturePlacement { Word = "BED", Room = "Den" });
            CollectionAssert.AreEqual(new[] { "BED", "LAMP" }, book.For(house, 10).Select(r => r.Answer));
        }

        [Test]
        public void TheDeckDrawsEveryRiddleOnceBeforeAnyRepeats()
        {
            var book = Book();
            var deck = new RiddleDeck(book.All, 42);
            var drawn = Enumerable.Range(0, book.All.Count).Select(_ => deck.Draw()).ToList();
            CollectionAssert.AllItemsAreUnique(drawn);
            CollectionAssert.AreEquivalent(book.All, drawn);
            Assert.AreEqual(0, deck.Left);
            var next = deck.Draw();
            Assert.IsNotNull(next);
            Assert.AreNotSame(drawn[drawn.Count - 1], next);
            Assert.AreEqual(book.All.Count - 1, deck.Left);
            Assert.IsNull(new RiddleDeck(Array.Empty<Riddle>(), 1).Draw());
        }

        [Test]
        public void TheDeckOrderFollowsTheMatchSeed()
        {
            var all = Book().All;
            string Order(ulong seed)
            {
                var deck = new RiddleDeck(all, seed);
                return string.Join(",", Enumerable.Range(0, all.Count).Select(_ => deck.Draw().Answer));
            }
            Assert.AreEqual(Order(3), Order(3));
            Assert.AreNotEqual(Order(3), Order(4));
        }

        [Test]
        public void TheDeckSkipsRiddlesTheHouseCannotSupplyAndKeepsThemForLater()
        {
            var deck = new RiddleDeck(Small().All, 5);
            bool lampMissing = true;
            var seen = new List<string>();
            for (int i = 0; i < 3; i++) seen.Add(deck.Draw(r => !lampMissing || r.Answer != "LAMP").Answer);
            CollectionAssert.DoesNotContain(seen, "LAMP");
            Assert.AreEqual(1, deck.Left);
            Assert.IsNull(new RiddleDeck(Small().All, 5).Draw(_ => false));
            lampMissing = false;
            Assert.AreEqual("LAMP", deck.Draw(r => !lampMissing || r.Answer != "LAMP").Answer);
        }

        [Test]
        public void OnlyTheFirstCorrectSpellingScoresAndTheNextRiddleFollowsTheBeat()
        {
            var race = Race();
            Assert.AreEqual(RiddleStage.Waiting, race.Stage);
            Assert.AreEqual(RiddleEvent.None, race.Tick(1f));
            Assert.AreEqual(RiddleEvent.Asked, race.Start());
            var first = race.Current;
            Assert.AreEqual(RiddleStage.Asking, race.Stage);
            Assert.IsFalse(race.Solve(1, first.Answer == "BED" ? "LAMP" : "BED"));
            Assert.IsFalse(race.Solve(9, first.Answer));
            Assert.IsTrue(race.Solve(1, first.Answer.ToLowerInvariant()));
            Assert.IsFalse(race.Solve(2, first.Answer));
            Assert.AreEqual(1, race.Points(1));
            Assert.AreEqual(0, race.Points(2));
            Assert.AreEqual(1, race.Solver);
            Assert.AreEqual(RiddleStage.Solved, race.Stage);
            for (int i = 0; i < first.Answer.Length; i++) Assert.AreEqual(first.Answer[i], race.Tile(i));
            Assert.AreEqual(RiddleEvent.None, race.Tick(RiddleRace.BeatSeconds - .1f));
            Assert.AreSame(first, race.Current);
            Assert.AreEqual(RiddleEvent.Asked, race.Tick(.2f));
            Assert.AreNotSame(first, race.Current);
            Assert.AreEqual(RiddleStage.Asking, race.Stage);
            Assert.AreEqual(2, race.Asked);
            Assert.AreEqual(-1, race.Solver);
            Assert.AreEqual(0f, race.Elapsed);
        }

        [Test]
        public void TheHintShowsOnceAfterFortySecondsAndRevealsTheFirstLetter()
        {
            var race = Race();
            race.Start();
            Assert.AreEqual(RiddleRace.HintSeconds, race.UntilHint);
            Assert.AreEqual(RiddleEvent.None, race.Tick(RiddleRace.HintSeconds - .5f));
            Assert.IsFalse(race.HintShown);
            Assert.AreEqual(.5f, race.UntilHint, 1e-3f);
            Assert.AreEqual('\0', race.Tile(0));
            Assert.AreEqual(RiddleEvent.Hint, race.Tick(1f));
            Assert.IsTrue(race.HintShown);
            Assert.AreEqual(0f, race.UntilHint);
            Assert.AreEqual(race.Current.Answer[0], race.Tile(0));
            Assert.AreEqual('\0', race.Tile(1));
            Assert.AreEqual('\0', race.Tile(-1));
            Assert.AreEqual('\0', race.Tile(race.Current.Answer.Length));
            Assert.AreEqual(RiddleEvent.None, race.Tick(10f));
        }

        [Test]
        public void TheFirstToFivePointsWinsTheMatch()
        {
            var race = Race(Book());
            race.Start();
            for (int point = 1; point <= RiddleRace.PointsToWin; point++)
            {
                Assert.AreEqual(-1, race.Winner);
                Assert.IsTrue(race.Solve(2, race.Current.Answer));
                Assert.AreEqual(point, race.Points(2));
                if (point < RiddleRace.PointsToWin) Assert.AreEqual(RiddleEvent.Asked, race.Tick(RiddleRace.BeatSeconds));
                if (point != 2) continue;
                Assert.IsTrue(race.Solve(0, race.Current.Answer));
                Assert.AreEqual(RiddleEvent.Asked, race.Tick(RiddleRace.BeatSeconds));
            }
            Assert.AreEqual(RiddleStage.Won, race.Stage);
            Assert.AreEqual(2, race.Winner);
            Assert.AreEqual(1, race.Points(0));
            Assert.AreEqual(RiddleEvent.None, race.Tick(RiddleRace.BeatSeconds * 2f));
            Assert.IsFalse(race.Solve(0, race.Current.Answer));
            CollectionAssert.AreEqual(new[] { 2, 0, 1, 3 }, race.Standings().ToArray());
            Assert.AreEqual(RiddleRace.PointsToWin + 1, race.Asked);
            Assert.AreEqual(RiddleEvent.Asked, race.Start());
            Assert.AreEqual(0, race.Points(2));
            Assert.AreEqual(1, race.Asked);
        }

        [Test]
        public void TheLeaderIsTheOnlySeatWithTheMostPoints()
        {
            var race = Race(Book());
            race.Start();
            Assert.AreEqual(-1, race.Leader());
            race.Solve(1, race.Current.Answer);
            race.Tick(RiddleRace.BeatSeconds);
            Assert.AreEqual(1, race.Leader());
            race.Solve(3, race.Current.Answer);
            race.Tick(RiddleRace.BeatSeconds);
            Assert.AreEqual(-1, race.Leader());
            race.Solve(3, race.Current.Answer);
            Assert.AreEqual(3, race.Leader());
            Assert.AreEqual(3, race.Solved);
        }

        [Test]
        public void BotsKnowTheAnswerBetweenEightAndFifteenSeconds()
        {
            var race = Race(Book());
            race.Start();
            var delays = new List<float>();
            for (int riddle = 0; riddle < 30; riddle++)
            {
                for (int seat = 0; seat < race.Seats; seat++)
                {
                    float at = race.KnowsAt(seat);
                    Assert.That(at, Is.InRange(RiddleRace.BotKnowsMin, RiddleRace.BotKnowsMax));
                    delays.Add(at);
                }
                race.Tick(RiddleRace.BotKnowsMin - .01f);
                Assert.IsFalse(Enumerable.Range(0, race.Seats).Any(race.Knows));
                race.Tick(RiddleRace.BotKnowsMax - RiddleRace.BotKnowsMin + .02f);
                Assert.IsTrue(Enumerable.Range(0, race.Seats).All(race.Knows));
                race.Solve(riddle % race.Seats, race.Current.Answer);
                Assert.IsFalse(race.Knows(0));
                if (race.Stage == RiddleStage.Won) race.Start(); else race.Tick(RiddleRace.BeatSeconds);
            }
            Assert.Greater(delays.Distinct().Count(), 20);
            Assert.AreEqual(float.PositiveInfinity, race.KnowsAt(-1));
        }

        [Test]
        public void WhenNoRiddleFitsTheLettersLeftTheHouseIsRefurnished()
        {
            var race = Race();
            Assert.AreEqual(RiddleEvent.Refurnish, race.Start(_ => false));
            Assert.AreEqual(RiddleStage.Asking, race.Stage);
            Assert.IsNotNull(race.Current);
            var empty = new RiddleRace(new RiddleDeck(Array.Empty<Riddle>(), 1), 2, 1);
            Assert.AreEqual(RiddleEvent.None, empty.Start());
            Assert.AreEqual(RiddleStage.Waiting, empty.Stage);
            Assert.Throws<ArgumentOutOfRangeException>(() => new RiddleRace(new RiddleDeck(Small().All, 1), 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RiddleRace(new RiddleDeck(Small().All, 1), 2, 1, 0));
            Assert.Throws<ArgumentNullException>(() => new RiddleRace(null, 2, 1));
        }

        [Test]
        public void HintsPulseFurnitureNamedLikeTheAnswerFirst()
        {
            var furniture = new[] { "CHAIR", "TABLE", "LAMP", "TABLE", "TABLE", "TABLE" };
            CollectionAssert.AreEqual(new[] { 1, 3, 4 }, RiddleHints.Sources("TABLE", furniture));
            CollectionAssert.AreEqual(new[] { 1 }, RiddleHints.Sources("TABLE", furniture, 1));
        }

        [Test]
        public void OtherwiseHintsPulseFurnitureThatHoldsTheAnswerLetters()
        {
            CollectionAssert.AreEqual(new[] { 1, 2 }, RiddleHints.Sources("BEAR", new[] { "LAMP", "BED", "CHAIR", "BOX" }));
            CollectionAssert.AreEqual(new[] { 0 }, RiddleHints.Sources("ZAP", new[] { "LAMP", "BED" }));
            CollectionAssert.IsEmpty(RiddleHints.Sources("ZZZ", new[] { "LAMP", "BED" }));
            CollectionAssert.IsEmpty(RiddleHints.Sources("bad word", new[] { "LAMP" }));
            CollectionAssert.IsEmpty(RiddleHints.Sources("BED", null));
        }

        [Test]
        public void RiddleWordsReadPlainly()
        {
            Assert.AreEqual("Hint in 0:24", RiddleText.UntilHint(23.2f));
            Assert.AreEqual("Hint in 0:40", RiddleText.UntilHint(40f));
            Assert.AreEqual("Hint in 0:00", RiddleText.UntilHint(-3f));
            Assert.AreEqual("Starts with T. Dinner is served on me.", RiddleText.StartsWith(new Riddle("TABLE", "x (5)", "Dinner is served on me.")));
            Assert.AreEqual("Starts with B", RiddleText.StartsWith(new Riddle("BED", "x (3)")));
            Assert.AreEqual("", RiddleText.StartsWith(null));
            Assert.AreEqual("P2 spelled TABLE!", RiddleText.Spelled("P2", "TABLE"));
            Assert.AreEqual("1 point", RiddleText.Points(1));
            Assert.AreEqual("0 points", RiddleText.Points(0));
            Assert.AreEqual("P3 wins Riddles!", RiddleText.Heading("P3", false));
            Assert.AreEqual("You win Riddles!", RiddleText.Heading("You", true));
            Assert.AreEqual(RiddleText.Tie, RiddleText.Heading("", false));
            Assert.AreEqual("MATCH OVER · P3 WINS", RiddleText.Eyebrow("P3", false));
            Assert.AreEqual("MATCH OVER · YOU WIN", RiddleText.Eyebrow("You", true));
            Assert.AreEqual("MATCH OVER · DRAW", RiddleText.Eyebrow(null, false));
            Assert.AreEqual("1 riddle solved.", RiddleText.Flavour(1));
            Assert.AreEqual("7 riddles solved.", RiddleText.Flavour(7));
            Assert.AreEqual("Read the riddle and spell the answer. First to 5 points wins.", RiddleText.Opening(5));
            Assert.AreEqual("AI 3: one more point to win!", RiddleText.OneMore("AI 3"));
            Assert.AreEqual("<b>P1 · 5 points</b>", RiddleText.Line("P1", 5, true));
            Assert.AreEqual("AI 2 · 3 points", RiddleText.Line("AI 2", 3, false));
        }

        [Test]
        public void OnlineAndCourseCodeNeverReadTheRiddles()
        {
            string scripts = Path.Combine(TestData.Root, "Assets", "_Project", "Scripts");
            var forbidden = new[] { "riddles_house", "RiddleBook", "RiddleRace", "RiddleDeck" };
            var hits = new List<string>();
            foreach (string tree in new[] { "Online", "Rules/Wordbound", "Workshop" })
                foreach (string file in Directory.GetFiles(Path.Combine(scripts, tree.Replace('/', Path.DirectorySeparatorChar)), "*.cs", SearchOption.AllDirectories))
                {
                    string text = File.ReadAllText(file);
                    hits.AddRange(forbidden.Where(text.Contains).Select(token => Path.GetRelativePath(scripts, file) + ": " + token));
                }
            Assert.IsEmpty(hits, string.Join("\n", hits));
        }
    }
}
