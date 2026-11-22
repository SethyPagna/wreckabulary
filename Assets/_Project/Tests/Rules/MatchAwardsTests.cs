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

        static void Points(MatchStats stats, int seat, int times)
        {
            for (int i = 0; i < times; i++) stats.Point(seat);
        }

        static void Revives(MatchStats stats, int seat, int times)
        {
            for (int i = 0; i < times; i++) stats.Revive(seat);
        }

        static MatchStats Crew(int seats)
        {
            var stats = new MatchStats();
            for (int seat = 0; seat < seats; seat++) stats.Enter(seat, "P" + (seat + 1), 0, false);
            return stats;
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
            foreach (string mode in Warmup.Modes.Concat(new[] { null, "Other" }))
            {
                var numbers = MatchAwards.Columns(mode);
                Assert.AreEqual(MatchAwards.NumbersShown, numbers.Count, mode);
                Assert.AreEqual(numbers.Count, numbers.Distinct().Count(), mode);
                var labels = numbers.Select(n => MatchAwards.Label(n, mode)).ToArray();
                Assert.AreEqual(labels.Length, labels.Distinct().Count(), "no label twice: " + string.Join(",", labels));
            }
            CollectionAssert.AreEqual(new[] { MatchStatKind.Score, MatchStatKind.Knockouts, MatchStatKind.Crafted },
                MatchAwards.Columns(HouseModes.Dibs).ToArray());
            CollectionAssert.AreEqual(new[] { MatchStatKind.Score, MatchStatKind.Crafted, MatchStatKind.Broken },
                MatchAwards.Columns(HouseModes.FreeForAll).ToArray(), "the mode score already is the knockout count");
            CollectionAssert.AreEqual(new[] { MatchStatKind.Knockouts, MatchStatKind.Crafted, MatchStatKind.Broken },
                MatchAwards.Columns().ToArray());
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
            Assert.AreEqual("PACK THE VAN", MatchEnd.ModeLine(HouseModes.MovingOut));
            Assert.AreEqual("ESCAPE THE HOUSE", MatchEnd.ModeLine(HouseModes.Escape));
            Assert.AreEqual("FURNISH THE HOUSE", MatchEnd.ModeLine(HouseModes.MovingDay));
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
            Assert.AreEqual("P1", you.Name, "the seat name, with a YOU tag");
            Assert.AreEqual(MatchEnd.WinnerTag + MatchEnd.Dot + SeatTags.YouTag, MatchEnd.Tag(you, false));
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
            Assert.AreEqual(MatchAwards.SmasherTitle, wrecker.Title);
            Assert.AreEqual(MatchEnd.BotTag, MatchEnd.Tag(wrecker, false));
            CollectionAssert.AreEqual(new[] { "WINS", "KO", "WORDS" }, wrecker.Numbers.Select(n => n.Label).ToArray(), "every card has the same columns");
            CollectionAssert.AreEqual(new[] { 0, 0, 0 }, wrecker.Numbers.Select(n => n.Value).ToArray());

            var couch = Table(2, 0, 1, 2);
            Assert.AreEqual("P1", MatchEnd.Build(couch, 0, HouseModes.Dibs).Cards.Single(c => c.Seat == 0).Name, "a shared couch keeps seat names");
            Assert.IsTrue(MatchEnd.Build(couch, 0, HouseModes.Dibs).Cards.All(c => !c.You));
            Assert.AreEqual(MatchEnd.WinnerTag, MatchEnd.Tag(MatchEnd.Build(couch, 0, HouseModes.Dibs).Cards[0], false), "nobody is YOU on a shared couch");
            Assert.AreEqual("", MatchEnd.Tag(null, false));
        }

        [Test]
        public void EveryCardInAMatchHasTheSameColumnsInTheSameOrder()
        {
            foreach (string mode in Warmup.Modes)
            {
                var stats = Table(1, 0, 1, 2, 3);
                stats.SetScore(0, 2);
                Knockouts(stats, 1, 0, 3);
                Crafts(stats, 2, 4);
                Breaks(stats, 3, 5);
                stats.Hurt(3, 30f);
                var board = MatchEnd.Build(stats, 0, mode);
                var first = board.Cards[0].Numbers.Select(n => n.Label).ToArray();
                foreach (var card in board.Cards)
                    CollectionAssert.AreEqual(first, card.Numbers.Select(n => n.Label).ToArray(), card.Name + " in " + mode);
                CollectionAssert.AreEqual(MatchAwards.Columns(mode).Select(k => MatchAwards.Label(k, mode)).ToArray(), first, mode);
            }
        }

        [Test]
        public void ATitleShowsOnlyWhenItsNumberIsAboveZero()
        {
            var stats = Table(1, 0, 1, 2, 3);
            stats.SetScore(0, 2);
            Knockouts(stats, 0, 1, 2);
            var board = MatchEnd.Build(stats, 0, HouseModes.Dibs);
            var winner = board.Cards.Single(c => c.Seat == 0);
            Assert.AreEqual(MatchAwards.MvpTitle, winner.Title);
            Assert.IsTrue(winner.Titled);
            var fell = board.Cards.Single(c => c.Seat == 1);
            Assert.AreEqual(MatchAwardKind.GoodSport, fell.Award);
            Assert.AreEqual("", fell.Title, "no Good Sport over three zeros");
            Assert.IsFalse(fell.Titled);
            var idle = board.Cards.Single(c => c.Seat == 2);
            Assert.AreEqual(MatchAwardKind.Survivor, idle.Award, "still picked, never fell");
            Assert.AreEqual("", idle.Title, "but a seat that did nothing gets no title");

            Assert.IsFalse(MatchAwards.Earned(MatchAwardKind.GoodSport, stats.Of(0)));
            Assert.IsFalse(MatchAwards.Earned(MatchAwardKind.Mvp, stats.Of(2)), "a top title needs a score");
            Assert.IsTrue(MatchAwards.Earned(MatchAwardKind.Knockouts, stats.Of(0)));
            Assert.IsFalse(MatchAwards.Earned(MatchAwardKind.Speller, stats.Of(0)));
            Crafts(stats, 2, 1);
            Assert.IsTrue(MatchAwards.Earned(MatchAwardKind.Survivor, stats.Of(2)), "busy and never fell");
            Assert.IsFalse(MatchAwards.Earned(MatchAwardKind.Mvp, null));
        }

        [Test]
        public void VersusTitlesNeverUseTheCrewTitleEvenWhenRevivesAreCounted()
        {
            var stats = Table(1, 0, 0, 1, 1);
            Revives(stats, 1, 3);
            Revives(stats, 3, 1);
            foreach (int winner in new[] { 0, 1, Teams.NoTeam })
                CollectionAssert.DoesNotContain(MatchAwards.Pick(stats, winner).Values.ToArray(), MatchAwardKind.Medic);
            Assert.IsTrue(MatchAwards.Pick(stats).Values.All(kind => kind == MatchAwardKind.GoodSport), "a revive alone earns no versus title");
            foreach (string mode in Warmup.VersusModes)
                foreach (MatchAwardKind kind in System.Enum.GetValues(typeof(MatchAwardKind)))
                    CollectionAssert.DoesNotContain(MatchAwards.Columns(mode).ToArray(), MatchStatKind.Revives, kind + " in " + mode);
        }

        [Test]
        public void ACrewGetsTitlesThatFitWorkingTogether()
        {
            var stats = Crew(4);
            Points(stats, 2, 2); Points(stats, 0, 1);
            Revives(stats, 1, 2);
            Crafts(stats, 0, 5); Crafts(stats, 3, 1);
            Breaks(stats, 3, 6);
            stats.Knockout(0, -1);
            var picked = MatchAwards.PickCrew(stats);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 2, 3 }, picked.Keys.ToArray());
            Assert.AreEqual(MatchAwardKind.Mvp, picked[2], "the seat that did the most for the goal");
            Assert.AreEqual(MatchAwardKind.Medic, picked[1]);
            Assert.AreEqual(MatchAwardKind.Speller, picked[0]);
            Assert.AreEqual(MatchAwardKind.Smasher, picked[3]);
            Assert.AreEqual("Lifesaver", MatchAwards.Title(MatchAwardKind.Medic));
            foreach (var kind in picked.Values)
            {
                Assert.AreNotEqual(MatchAwardKind.Knockouts, kind, "nobody fights in a crew");
                Assert.AreNotEqual(MatchAwardKind.Hitter, kind);
            }
            for (int run = 0; run < 3; run++)
                CollectionAssert.AreEqual(picked.OrderBy(p => p.Key).Select(p => p.Value).ToArray(),
                    MatchAwards.PickCrew(stats).OrderBy(p => p.Key).Select(p => p.Value).ToArray(), "the same table always gives the same titles");
        }

        [Test]
        public void AQuietCrewAndASoloRunStayModest()
        {
            Assert.IsTrue(MatchAwards.PickCrew(Crew(3)).Values.All(kind => kind == MatchAwardKind.GoodSport), "nothing happened, everyone is a good sport");
            Assert.IsEmpty(MatchAwards.PickCrew(null));
            Assert.IsEmpty(MatchAwards.PickCrew(new MatchStats()));

            var alone = Crew(1);
            Points(alone, 0, 3);
            Assert.AreEqual(MatchAwardKind.GoodSport, MatchAwards.PickCrew(alone)[0], "one seat has nobody to be more valuable than");
            Crafts(alone, 0, 2);
            Assert.AreEqual(MatchAwardKind.Speller, MatchAwards.PickCrew(alone)[0]);

            var helpers = Crew(2);
            Crafts(helpers, 0, 1);
            Assert.IsFalse(MatchAwards.PickCrew(helpers).Values.Contains(MatchAwardKind.Mvp), "the top title needs a point toward the goal");
            Assert.AreEqual(MatchAwardKind.Speller, MatchAwards.PickCrew(helpers)[0]);
            helpers.Knockout(0, -1);
            Assert.AreEqual(MatchAwardKind.Survivor, MatchAwards.PickCrew(helpers)[1], "stayed up while a crewmate fell");
        }

        [Test]
        public void CrewCardsShowTheGoalCountAndNeverKnockoutsOrDamage()
        {
            Assert.AreEqual("PACKED", MatchAwards.Label(MatchStatKind.Score, HouseModes.MovingOut));
            Assert.AreEqual("UNLOCKED", MatchAwards.Label(MatchStatKind.Score, HouseModes.Escape));
            Assert.AreEqual("PLACED", MatchAwards.Label(MatchStatKind.Score, HouseModes.MovingDay));
            Assert.AreEqual("REVIVES", MatchAwards.Label(MatchStatKind.Revives));
            foreach (string mode in Warmup.CrewModes)
            {
                Assert.IsFalse(MatchAwards.Shows(MatchStatKind.Knockouts, mode), mode);
                Assert.IsFalse(MatchAwards.Shows(MatchStatKind.Damage, mode), mode);
                var numbers = MatchAwards.Columns(mode);
                Assert.AreEqual(MatchAwards.NumbersShown, numbers.Count, mode);
                Assert.AreEqual(MatchStatKind.Score, numbers[0], mode);
                Assert.AreEqual(numbers.Count, numbers.Distinct().Count(), mode);
                CollectionAssert.DoesNotContain(numbers.ToArray(), MatchStatKind.Knockouts, mode);
                CollectionAssert.DoesNotContain(numbers.ToArray(), MatchStatKind.Damage, mode);
                var labels = numbers.Select(n => MatchAwards.Label(n, mode)).ToArray();
                Assert.AreEqual(labels.Length, labels.Distinct().Count(), "no label twice: " + string.Join(",", labels));
                CollectionAssert.AreEqual(new[] { MatchStatKind.Score, MatchStatKind.Crafted, MatchStatKind.Broken }, numbers.ToArray(), mode);
            }
            foreach (string mode in Warmup.VersusModes)
            {
                Assert.IsTrue(MatchAwards.Shows(MatchStatKind.Damage, mode), mode);
                Assert.AreEqual(!MatchAwards.ScoreCountsKnockouts(mode), MatchAwards.Shows(MatchStatKind.Knockouts, mode), mode);
            }
        }

        [Test]
        public void TheCrewBannerUsesEachModesOwnWords()
        {
            Assert.AreEqual("ALL PACKED", CrewEnd.Banner(HouseModes.MovingOut, CrewOutcome.Won));
            Assert.AreEqual("EVERYBODY OUT", CrewEnd.Banner(HouseModes.Escape, CrewOutcome.Won));
            Assert.AreEqual("FURNISHED", CrewEnd.Banner(HouseModes.MovingDay, CrewOutcome.Won));
            Assert.AreEqual("WELL DONE", CrewEnd.Banner("Other", CrewOutcome.Won));
            foreach (string mode in Warmup.CrewModes)
            {
                Assert.AreEqual("OUT OF TIME", CrewEnd.Banner(mode, CrewOutcome.TimeUp));
                Assert.AreEqual("CREW DOWN", CrewEnd.Banner(mode, CrewOutcome.Wiped));
                foreach (CrewOutcome outcome in System.Enum.GetValues(typeof(CrewOutcome)))
                {
                    string banner = CrewEnd.Banner(mode, outcome);
                    Assert.AreEqual(banner.ToUpperInvariant(), banner);
                    StringAssert.DoesNotContain("\u2014", banner);
                    StringAssert.DoesNotContain("\u2013", banner);
                    StringAssert.DoesNotContain("VICTORY", banner, "a crew has nobody to beat");
                    StringAssert.DoesNotContain("DEFEAT", banner);
                }
            }
            Assert.AreEqual(CrewOutcome.Won, CrewEnd.Outcome(true, false));
            Assert.AreEqual(CrewOutcome.Won, CrewEnd.Outcome(true, true));
            Assert.AreEqual(CrewOutcome.Wiped, CrewEnd.Outcome(false, true));
            Assert.AreEqual(CrewOutcome.TimeUp, CrewEnd.Outcome(false, false));
        }

        [Test]
        public void ACrewBoardHasOneCardPerSeatBestFirst()
        {
            var stats = Crew(3);
            Points(stats, 1, 2); Points(stats, 2, 1);
            Crafts(stats, 0, 4); Crafts(stats, 1, 1);
            Breaks(stats, 2, 3);
            Revives(stats, 0, 1);
            stats.Knockout(2, -1);
            stats.Hurt(0, 50f);
            var won = CrewEnd.Build(stats, CrewOutcome.Won, HouseModes.MovingOut, "Pinwheel House");
            Assert.AreEqual("ALL PACKED", won.Banner);
            Assert.AreEqual("PACK THE VAN · PINWHEEL HOUSE", won.ModeLine);
            Assert.IsTrue(won.Crew);
            Assert.IsTrue(won.Won);
            Assert.IsFalse(won.Lost);
            Assert.IsFalse(won.Draw);
            Assert.IsFalse(won.TwoSides);
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, won.Cards.Select(c => c.Seat).ToArray());
            Assert.IsTrue(won.Cards.All(c => c.Winner), "the whole crew made it");
            Assert.IsTrue(won.Cards.All(c => !c.Bot && !c.You), "a shared couch keeps seat names");
            CollectionAssert.AreEqual(new[] { "P2", "P3", "P1" }, won.Cards.Select(c => c.Name).ToArray());
            Assert.AreEqual("Most Valuable Player", won.Cards[0].Title);
            Assert.AreEqual("Wrecking Ball", won.Cards[1].Title);
            Assert.AreEqual("Lifesaver", won.Cards[2].Title);
            foreach (var card in won.Cards)
                CollectionAssert.AreEqual(new[] { "PACKED", "WORDS", "SMASHED" }, card.Numbers.Select(n => n.Label).ToArray(), card.Name);
            CollectionAssert.AreEqual(new[] { 2, 1, 0 }, won.Cards[0].Numbers.Select(n => n.Value).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 0, 3 }, won.Cards[1].Numbers.Select(n => n.Value).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 4, 0 }, won.Cards[2].Numbers.Select(n => n.Value).ToArray());
            Assert.IsTrue(won.Cards.All(c => MatchEnd.Tag(c, true) == ""), "a crew has no winner tag");

            var late = CrewEnd.Build(stats, CrewOutcome.TimeUp, HouseModes.Escape);
            Assert.AreEqual("OUT OF TIME", late.Banner);
            Assert.AreEqual("ESCAPE THE HOUSE", late.ModeLine);
            Assert.IsFalse(late.Won);
            Assert.IsTrue(late.Lost, "a crew that fails has lost even with no side to lose to");
            Assert.IsFalse(late.Cards.Any(c => c.Winner));
            Assert.AreEqual("UNLOCKED", late.Cards[0].Numbers[0].Label);
            Assert.AreEqual("CREW DOWN", CrewEnd.Build(stats, CrewOutcome.Wiped, HouseModes.MovingOut).Banner);
        }

        [Test]
        public void ASoloCrewReadsYouAndAnEmptyTableHasNoCards()
        {
            var alone = Crew(1);
            Points(alone, 0, 4);
            var board = CrewEnd.Build(alone, CrewOutcome.Won, HouseModes.MovingDay, "City Flat");
            Assert.AreEqual("FURNISHED", board.Banner);
            Assert.AreEqual("FURNISH THE HOUSE · CITY FLAT", board.ModeLine);
            Assert.AreEqual(1, board.Cards.Count);
            Assert.AreEqual("P1", board.Cards[0].Name);
            Assert.IsTrue(board.Cards[0].You);
            Assert.AreEqual(SeatTags.YouTag, MatchEnd.Tag(board.Cards[0], true));
            Assert.AreEqual(MatchAwardKind.GoodSport, board.Cards[0].Award);
            Assert.AreEqual("", board.Cards[0].Title, "no title without a number behind it");
            CollectionAssert.AreEqual(new[] { "PLACED", "WORDS", "SMASHED" }, board.Cards[0].Numbers.Select(n => n.Label).ToArray());
            Assert.AreEqual(4, board.Cards[0].Numbers[0].Value);
            Assert.IsEmpty(CrewEnd.Build(null, CrewOutcome.Won, HouseModes.Escape).Cards, "no table, no cards");
            Assert.IsEmpty(CrewEnd.Build(new MatchStats(), CrewOutcome.TimeUp, HouseModes.Escape).Cards);
        }

        [Test]
        public void VersusBoardsAreNotCrewBoards()
        {
            var stats = Table(1, 0, 1, 0, 1);
            stats.SetSideScore(0, 3); stats.SetSideScore(1, 1);
            var won = MatchEnd.Build(stats, 0, HouseModes.Duos);
            Assert.IsFalse(won.Crew);
            Assert.IsFalse(won.Lost);
            Assert.IsTrue(MatchEnd.Build(stats, 1, HouseModes.Duos).Lost);
            var couch = Table(2, 0, 1, 2, 3);
            Assert.IsFalse(MatchEnd.Build(couch, 2, HouseModes.Dibs).Lost, "a shared couch on different sides has no home side");
            Assert.IsFalse(MatchEnd.Build(couch, Teams.NoTeam, HouseModes.Dibs).Lost);
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
