using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class MatchAwardsTests
    {
        static MatchStats Table(int humans, params int[] teams)
        {
            var stats = new MatchStats();
            for (int seat = 0; seat < teams.Length; seat++)
                stats.Enter(seat, seat < humans ? "P" + (seat + 1) : "AI " + (seat + 1), teams[seat], seat >= humans);
            return stats;
        }

        static void Knockouts(MatchStats stats, int attacker, int victim, int times)
        {
            for (int i = 0; i < times; i++) stats.Knockout(victim, attacker);
        }

        static void Crafts(MatchStats stats, int seat, int times)
        {
            for (int i = 0; i < times; i++) stats.Craft(seat);
        }

        static void Breaks(MatchStats stats, int seat, int times)
        {
            for (int i = 0; i < times; i++) stats.Break(seat);
        }

        [Test]
        public void EveryTitleHasItsOwnWords()
        {
            var kinds = (MatchAwardKind[])System.Enum.GetValues(typeof(MatchAwardKind));
            var titles = kinds.Select(MatchAwards.Title).ToArray();
            Assert.AreEqual(kinds.Length, titles.Distinct().Count());
            Assert.IsTrue(titles.All(t => !string.IsNullOrWhiteSpace(t)));
            Assert.AreEqual("Most Valuable Player", MatchAwards.Title(MatchAwardKind.Mvp));
            Assert.AreEqual("Good Sport", MatchAwards.Title(MatchAwardKind.GoodSport));
        }

        [Test]
        public void EverySeatGetsExactlyOneTitle()
        {
            var stats = Table(1, 0, 1, 2, 3);
            stats.SetScore(1, 3);
            Knockouts(stats, 1, 0, 2);
            Crafts(stats, 0, 4);
            Breaks(stats, 2, 5);
            stats.Hurt(3, 40f);
            var picked = MatchAwards.Pick(stats, 1);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, picked.Keys.ToArray());
            Assert.AreEqual(MatchAwardKind.Mvp, picked[1], "the best seat of the winning side");
            Assert.AreEqual(MatchAwardKind.Speller, picked[0]);
            Assert.AreEqual(MatchAwardKind.Smasher, picked[2]);
            Assert.AreEqual(MatchAwardKind.Hitter, picked[3]);
        }

        [Test]
        public void TheMostValuablePlayerComesFromTheWinningSide()
        {
            var stats = Table(1, 0, 1, 0, 1);
            stats.SetScore(1, 9);
            stats.SetScore(0, 2); stats.SetScore(2, 4);
            var picked = MatchAwards.Pick(stats, 0);
            Assert.AreEqual(MatchAwardKind.Mvp, picked[2], "seat 2 leads the winners even though seat 1 scored more");
            Assert.AreNotEqual(MatchAwardKind.Mvp, picked[1]);
            Assert.AreEqual(1, picked.Values.Count(kind => kind == MatchAwardKind.Mvp));
        }

        [Test]
        public void ADrawStillNamesTheBusiestSeatButAQuietTableHasNone()
        {
            var quiet = Table(1, 0, 1, 2);
            Assert.IsTrue(MatchAwards.Pick(quiet).Values.All(kind => kind == MatchAwardKind.GoodSport), "nothing happened, everyone is a good sport");

            var busy = Table(1, 0, 1, 2);
            Crafts(busy, 2, 1);
            var picked = MatchAwards.Pick(busy);
            Assert.AreEqual(MatchAwardKind.Mvp, picked[2], "the only seat that did anything");
            Assert.AreEqual(MatchAwardKind.GoodSport, picked[0]);
            Assert.AreEqual(MatchAwardKind.GoodSport, picked[1]);
        }

        [Test]
        public void TiesGoToTheBetterRankedSeatThenTheLowerSeat()
        {
            var stats = Table(1, 0, 1, 2, 3);
            stats.SetScore(0, 1);
            Crafts(stats, 1, 3); Crafts(stats, 2, 3); Crafts(stats, 3, 3);
            stats.SetScore(3, 0); stats.SetScore(2, 0);
            var level = MatchAwards.Pick(stats, 0);
            Assert.AreEqual(MatchAwardKind.Mvp, level[0]);
            Assert.AreEqual(MatchAwardKind.Speller, level[1], "level on words and rank, the lower seat takes it");

            Knockouts(stats, 3, 0, 1);
            var ranked = MatchAwards.Pick(stats, 0);
            Assert.AreEqual(MatchAwardKind.Knockouts, ranked[3], "the knockout title is handed out first");
            Assert.AreEqual(MatchAwardKind.Speller, ranked[1]);

            for (int run = 0; run < 3; run++)
                CollectionAssert.AreEqual(ranked.OrderBy(p => p.Key).Select(p => p.Value).ToArray(),
                    MatchAwards.Pick(stats, 0).OrderBy(p => p.Key).Select(p => p.Value).ToArray(), "the same table always gives the same titles");
        }

        [Test]
        public void ATitleNeedsANumberBehindIt()
        {
            var stats = Table(1, 0, 1, 2, 3);
            stats.SetScore(0, 2);
            Knockouts(stats, 0, 1, 2);
            var picked = MatchAwards.Pick(stats, 0);
            Assert.AreEqual(MatchAwardKind.Mvp, picked[0]);
            Assert.AreEqual(MatchAwardKind.GoodSport, picked[1], "seat 1 fell the most and did nothing else");
            Assert.AreEqual(MatchAwardKind.Survivor, picked[2], "never fell, and somebody did");
            Assert.AreEqual(MatchAwardKind.GoodSport, picked[3], "only one seat is hard to wreck");
            Assert.IsFalse(picked.Values.Contains(MatchAwardKind.Knockouts), "the only seat with knockouts already holds a title");
            Assert.IsFalse(picked.Values.Contains(MatchAwardKind.Speller));
        }

        [Test]
        public void NobodyIsHardToWreckWhenEveryoneFellTheSame()
        {
            var stats = Table(1, 0, 1);
            Knockouts(stats, 0, 1, 1);
            Knockouts(stats, 1, 0, 1);
            Assert.IsFalse(MatchAwards.Pick(stats).Values.Contains(MatchAwardKind.Survivor));
            var alone = Table(1, 0);
            Assert.IsFalse(MatchAwards.Pick(alone).Values.Contains(MatchAwardKind.Survivor), "one seat has nobody to outlast");
            Assert.IsEmpty(MatchAwards.Pick(null));
            Assert.IsEmpty(MatchAwards.Pick(new MatchStats()));
        }

        [Test]
        public void EachCardShowsThreeNumbersLedByTheModeScore()
        {
            foreach (MatchAwardKind kind in System.Enum.GetValues(typeof(MatchAwardKind)))
                foreach (string mode in new[] { HouseModes.Dibs, HouseModes.Duos, HouseModes.FreeForAll, HouseModes.War, HouseModes.ShoppingList, HouseModes.RiddleRush, null, "Other" })
                {
                    var numbers = MatchAwards.Numbers(kind, mode);
                    Assert.AreEqual(MatchAwards.NumbersShown, numbers.Count, kind + " in " + mode);
                    Assert.AreEqual(numbers.Count, numbers.Distinct().Count(), kind + " in " + mode);
                    var labels = numbers.Select(n => MatchAwards.Label(n, mode)).ToArray();
                    Assert.AreEqual(labels.Length, labels.Distinct().Count(), "no label twice: " + string.Join(",", labels));
                }
            CollectionAssert.AreEqual(new[] { MatchStatKind.Score, MatchStatKind.Knockouts, MatchStatKind.Crafted },
                MatchAwards.Numbers(MatchAwardKind.Mvp, HouseModes.Dibs).ToArray());
            CollectionAssert.AreEqual(new[] { MatchStatKind.Score, MatchStatKind.Damage, MatchStatKind.Falls },
                MatchAwards.Numbers(MatchAwardKind.Hitter, HouseModes.FreeForAll).ToArray(), "the mode score already is the knockout count");
            CollectionAssert.AreEqual(new[] { MatchStatKind.Falls, MatchStatKind.Knockouts, MatchStatKind.Crafted },
                MatchAwards.Numbers(MatchAwardKind.Survivor).ToArray());
            Assert.AreEqual("WINS", MatchAwards.Label(MatchStatKind.Score, HouseModes.Duos));
            Assert.AreEqual("KO", MatchAwards.Label(MatchStatKind.Score, HouseModes.War));
            Assert.AreEqual("RIDDLES", MatchAwards.Label(MatchStatKind.Score, HouseModes.RiddleRush));
            Assert.AreEqual("ITEMS", MatchAwards.Label(MatchStatKind.Score, HouseModes.ShoppingList));
            Assert.AreEqual("SCORE", MatchAwards.Label(MatchStatKind.Score));
        }

        [Test]
        public void DamageIsShownAsAWholeNumber()
        {
            var stats = Table(1, 0);
            stats.Hurt(0, 12.5f);
            Assert.AreEqual(13, MatchAwards.Value(stats.Of(0), MatchStatKind.Damage));
            Assert.AreEqual(0, MatchAwards.Value(null, MatchStatKind.Damage));
        }

        [Test]
        public void TwoSidesShowVictoryOrDefeatWithTheScoreLine()
        {
            var stats = Table(1, 0, 1, 0, 1);
            stats.SetSideScore(0, 3); stats.SetSideScore(1, 1);
            var won = MatchEnd.Build(stats, 0, HouseModes.Duos, "Pinwheel");
            Assert.AreEqual("VICTORY 3 - 1", won.Banner);
            Assert.AreEqual("DUOS · PINWHEEL", won.ModeLine);
            Assert.IsTrue(won.TwoSides);
            Assert.IsTrue(won.Won);
            Assert.IsFalse(won.Draw);

            var behind = Table(1, 0, 1, 0, 1);
            behind.SetSideScore(0, 1); behind.SetSideScore(1, 3);
            var lost = MatchEnd.Build(behind, 1, HouseModes.Duos);
            Assert.AreEqual("DEFEAT 1 - 3", lost.Banner, "your side's score always comes first");
            Assert.IsFalse(lost.Won);
            Assert.AreEqual("DUOS", lost.ModeLine);

            var even = Table(1, 1, 0, 1, 0);
            even.SetSideScore(0, 2); even.SetSideScore(1, 4);
            var level = MatchEnd.Build(even, Teams.NoTeam, HouseModes.War);
            Assert.AreEqual("DRAW 4 - 2", level.Banner, "your side first in a draw too");
            Assert.IsTrue(level.Draw);
            Assert.IsFalse(level.Won);
            Assert.IsFalse(level.Cards.Any(c => c.Winner));
        }

        [Test]
        public void ASharedCouchOnBothSidesReadsTheWinningSideByName()
        {
            var stats = Table(2, 0, 1, 0, 1);
            stats.SetSideScore(0, 2); stats.SetSideScore(1, 5);
            var board = MatchEnd.Build(stats, 1, HouseModes.War);
            Assert.AreEqual("TEAM 2 WINS 5 - 2", board.Banner);
            Assert.IsFalse(board.Won, "no single home side to cheer for");
            Assert.AreEqual("TEAM BATTLE", board.ModeLine);

            var together = Table(2, 0, 0, 1, 1);
            together.SetSideScore(0, 1); together.SetSideScore(1, 3);
            Assert.AreEqual("DEFEAT 1 - 3", MatchEnd.Build(together, 1, HouseModes.Duos).Banner, "couch mates on one side share the result");
        }

        [Test]
        public void WithoutTwoSidesTheBannerNamesTheWinner()
        {
            var stats = Table(1, 0, 1, 2, 3);
            stats.SetScore(1, 10); stats.SetScore(0, 4);
            var theirs = MatchEnd.Build(stats, 1, HouseModes.FreeForAll);
            Assert.AreEqual("AI 2 WINS", theirs.Banner);
            Assert.IsFalse(theirs.TwoSides);
            Assert.AreEqual("FREE-FOR-ALL", theirs.ModeLine);
            Assert.AreEqual("YOU WIN", MatchEnd.Build(stats, 0, HouseModes.FreeForAll).Banner);
            Assert.AreEqual("DRAW", MatchEnd.Build(stats, Teams.NoTeam, HouseModes.FreeForAll).Banner);
            Assert.AreEqual("DRAW", MatchEnd.Build(stats, 7, HouseModes.FreeForAll).Banner, "a side nobody is on cannot win");

            var couch = Table(2, 0, 1, 2, 3);
            Assert.AreEqual("P2 WINS", MatchEnd.Build(couch, 1, HouseModes.Dibs).Banner);
            Assert.AreEqual("LAST ONE STANDING", MatchEnd.Build(couch, 1, HouseModes.Dibs).ModeLine, "players never see the word Dibs");
            Assert.IsEmpty(MatchEnd.Build(null, Teams.NoTeam, HouseModes.Dibs).Cards, "no table, no cards");
        }

        [Test]
        public void TheModeLineUsesThePlayerNamesOfEveryVersusMode()
        {
            Assert.AreEqual("LAST ONE STANDING", MatchEnd.ModeLine(HouseModes.Dibs));
            Assert.AreEqual("DUOS", MatchEnd.ModeLine(HouseModes.Duos));
            Assert.AreEqual("FREE-FOR-ALL", MatchEnd.ModeLine(HouseModes.FreeForAll));
            Assert.AreEqual("TEAM BATTLE", MatchEnd.ModeLine(HouseModes.War));
            Assert.AreEqual("SPELL THE LIST", MatchEnd.ModeLine(HouseModes.ShoppingList));
            Assert.AreEqual("RIDDLES", MatchEnd.ModeLine(HouseModes.RiddleRush));
            foreach (string mode in Warmup.Modes)
            {
                StringAssert.DoesNotContain("DIBS", MatchEnd.ModeLine(mode));
                StringAssert.DoesNotContain("DEATHMATCH", MatchEnd.ModeLine(mode));
                StringAssert.DoesNotContain("WAR", MatchEnd.ModeLine(mode));
            }
        }

        [Test]
        public void WinnersComeFirstThenTheRestInRankOrder()
        {
            var stats = Table(1, 0, 1, 0, 1);
            stats.SetScore(0, 1); stats.SetScore(2, 4);
            stats.SetScore(1, 7); stats.SetScore(3, 2);
            var board = MatchEnd.Build(stats, 0, HouseModes.War);
            CollectionAssert.AreEqual(new[] { 2, 0, 1, 3 }, board.Cards.Select(c => c.Seat).ToArray());
            CollectionAssert.AreEqual(new[] { true, true, false, false }, board.Cards.Select(c => c.Winner).ToArray());
            Assert.AreEqual(0, board.WinningTeam);
            Assert.AreEqual(MatchAwardKind.Mvp, board.Cards[0].Award);
            Assert.AreEqual("Most Valuable Player", board.Cards[0].Title);
        }

        [Test]
        public void ACardCarriesTheSeatNameBotFlagTitleAndNumbers()
        {
            var stats = Table(1, 0, 1, 2, 3);
            stats.SetScore(0, 3);
            Knockouts(stats, 0, 1, 2);
            Crafts(stats, 0, 5);
            Breaks(stats, 0, 4);
            Breaks(stats, 3, 6);
            var board = MatchEnd.Build(stats, 0, HouseModes.Dibs);
            var you = board.Cards[0];
            Assert.AreEqual(0, you.Seat);
            Assert.AreEqual("You", you.Name, "one person on the couch is You");
            Assert.IsTrue(you.You);
            Assert.IsFalse(you.Bot);
            Assert.IsTrue(you.Winner);
            CollectionAssert.AreEqual(new[] { "WINS", "KO", "WORDS" }, you.Numbers.Select(n => n.Label).ToArray());
            CollectionAssert.AreEqual(new[] { "3", "2", "5" }, you.Numbers.Select(n => n.Text).ToArray());

            var wrecker = board.Cards.Single(c => c.Seat == 3);
            Assert.AreEqual("AI 4", wrecker.Name);
            Assert.IsTrue(wrecker.Bot);
            Assert.IsFalse(wrecker.You);
            Assert.AreEqual(MatchAwardKind.Smasher, wrecker.Award);
            CollectionAssert.AreEqual(new[] { "WINS", "SMASHED", "WORDS" }, wrecker.Numbers.Select(n => n.Label).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 6, 0 }, wrecker.Numbers.Select(n => n.Value).ToArray());

            var couch = Table(2, 0, 1, 2);
            Assert.AreEqual("P1", MatchEnd.Build(couch, 0, HouseModes.Dibs).Cards.Single(c => c.Seat == 0).Name, "a shared couch keeps seat names");
            Assert.IsTrue(MatchEnd.Build(couch, 0, HouseModes.Dibs).Cards.All(c => !c.You));
        }

        [Test]
        public void InKnockoutModesTheCardShowsTheModeCountOnce()
        {
            var stats = Table(1, 0, 1, 2, 3);
            stats.SetScore(1, 10);
            Knockouts(stats, 1, 0, 9);
            var card = MatchEnd.Build(stats, 1, HouseModes.FreeForAll).Cards[0];
            CollectionAssert.AreEqual(new[] { "KO", "WORDS", "SMASHED" }, card.Numbers.Select(n => n.Label).ToArray());
            Assert.AreEqual(10, card.Numbers[0].Value, "the number on the card is the mode's own count");
        }
    }
}
