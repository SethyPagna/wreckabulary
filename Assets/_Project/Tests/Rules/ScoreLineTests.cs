using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class ScoreLineTests
    {
        const float Reach = ScoreLine.CentreWidth * .5f + ScoreLine.CentreGap;

        static (int[] order, float[] centres, float width, int split) Arrange(bool centred, params int[] teams)
        {
            var order = new int[PlayerBar.MostSeats];
            var centres = new float[PlayerBar.MostSeats];
            float width = ScoreLine.Arrange(teams, order, centres, centred, out int split);
            return (order, centres, width, split);
        }

        static ScoreSeat Seat(int seat, int team, int score = 0, int sideScore = 0, bool mine = false, bool scored = true) =>
            new ScoreSeat(seat, "P" + (seat + 1), team, score, sideScore, scored, mine);

        [Test]
        public void SidesAreTeamsAndSeatsWithoutATeamStandAlone()
        {
            Assert.AreEqual(0, ScoreLine.Sides(null));
            Assert.AreEqual(1, ScoreLine.Sides(new[] { 0, 0 }));
            Assert.AreEqual(2, ScoreLine.Sides(new[] { 0, 1, 0, 1 }));
            Assert.AreEqual(2, ScoreLine.Sides(new[] { 0, 1, 0, 1, 0, 1, 0, 1 }));
            Assert.AreEqual(4, ScoreLine.Sides(new[] { 0, 1, 2, 3 }));
            Assert.AreEqual(2, ScoreLine.Sides(new[] { Teams.NoTeam, Teams.NoTeam }), "Two seats without a team are a duel.");
            Assert.AreEqual(3, ScoreLine.Sides(new[] { Teams.NoTeam, Teams.NoTeam, 0 }));
            Assert.IsTrue(ScoreLine.TwoSides(new[] { 3, 5 }));
            Assert.IsFalse(ScoreLine.TwoSides(new[] { 0, 1, 2 }));
        }

        [Test]
        public void OnlyTwoScoredSidesGetACentreScore()
        {
            Assert.AreEqual(ScoreCentre.Score, ScoreLine.Centre(new[] { 0, 1, 0, 1 }, true, false));
            Assert.AreEqual(ScoreCentre.Score, ScoreLine.Centre(new[] { 0, 1 }, true, false), "A duel is two sides.");
            Assert.AreEqual(ScoreCentre.None, ScoreLine.Centre(new[] { 0, 1, 2, 3 }, true, false), "Everyone for themselves keeps the card scores only.");
            Assert.AreEqual(ScoreCentre.None, ScoreLine.Centre(new[] { 0, 1, 2 }, true, false));
            Assert.AreEqual(ScoreCentre.None, ScoreLine.Centre(new[] { 0, 1, 0, 1 }, false, false), "No scores, no score line.");
            Assert.AreEqual(ScoreCentre.None, ScoreLine.Centre(new[] { 0, 0 }, true, false), "One side has nobody to count against.");
            Assert.AreEqual(ScoreCentre.None, ScoreLine.Centre(null, true, true));
        }

        [Test]
        public void ATargetTakesTheCentreForAnyNumberOfSides()
        {
            Assert.AreEqual(ScoreCentre.Target, ScoreLine.Centre(new[] { 0, 1 }, true, true));
            Assert.AreEqual(ScoreCentre.Target, ScoreLine.Centre(new[] { 0, 1, 2, 3 }, true, true));
            Assert.AreEqual(ScoreCentre.Target, ScoreLine.Centre(new[] { 0, 1, 2 }, false, true));
            Assert.AreEqual(ScoreCentre.None, ScoreLine.Centre(new[] { 0 }, true, true), "Alone there is no bar to hold it.");
        }

        [Test]
        public void AClockTakesTheCentreOnlyWhenNothingElseDoes()
        {
            Assert.AreEqual(ScoreCentre.Clock, ScoreLine.Centre(new[] { 0, 1, 2, 3 }, true, false, true), "Everyone for themselves keeps the clock in the bar.");
            Assert.AreEqual(ScoreCentre.Clock, ScoreLine.Centre(new[] { 0, 0, 0 }, false, false, true), "A crew too.");
            Assert.AreEqual(ScoreCentre.Clock, ScoreLine.Centre(new[] { 0 }, true, false, true), "Alone, the clock still sits top centre.");
            Assert.AreEqual(ScoreCentre.Clock, ScoreLine.Centre(new int[0], false, false, true));
            Assert.AreEqual(ScoreCentre.Score, ScoreLine.Centre(new[] { 0, 1, 0, 1 }, true, false, true), "Two scored sides keep the score line, clock above it.");
            Assert.AreEqual(ScoreCentre.Target, ScoreLine.Centre(new[] { 0, 1, 2, 3 }, true, true, true));
            Assert.AreEqual(ScoreCentre.None, ScoreLine.Centre(new[] { 0, 1, 2, 3 }, true, false, false), "No clock, no clock centre.");
            Assert.AreEqual(ScoreCentre.None, ScoreLine.Centre(null, true, false, true));
            Assert.AreEqual(0f, ScoreLine.Width(ScoreCentre.None));
            Assert.AreEqual(ScoreLine.CentreWidth, ScoreLine.Width(ScoreCentre.Score));
            Assert.AreEqual(ScoreLine.CentreWidth, ScoreLine.Width(ScoreCentre.Target));
            Assert.AreEqual(ScoreLine.ClockWidth, ScoreLine.Width(ScoreCentre.Clock));
            Assert.Less(ScoreLine.ClockWidth, ScoreLine.CentreWidth, "A clock alone needs less room than a score line.");
        }

        [Test]
        public void AClockCentreSplitsTheCardsAroundItsOwnWidth()
        {
            var order = new int[PlayerBar.MostSeats];
            var centres = new float[PlayerBar.MostSeats];
            float width = ScoreLine.Arrange(new[] { 0, 1, 2, 3 }, order, centres, ScoreLine.ClockWidth, out int split);
            float half = PlayerBar.RoomyCard * .5f, reach = ScoreLine.ClockWidth * .5f + ScoreLine.CentreGap;
            Assert.AreEqual(2, split);
            Assert.AreEqual(-reach, centres[1] + half, .001f);
            Assert.AreEqual(reach, centres[2] - half, .001f);
            Assert.AreEqual(2f * (reach + 2f * PlayerBar.RoomyCard + PlayerBar.RoomyGap), width, .001f);
            Assert.AreEqual(ScoreLine.ClockWidth, ScoreLine.Arrange(new int[0], order, centres, ScoreLine.ClockWidth, out split), "With no cards the bar is the clock.");
            Assert.AreEqual(0, split);
            var plain = Arrange(true, 0, 1, 0, 1);
            Assert.AreEqual(plain.width, ScoreLine.Arrange(new[] { 0, 1, 0, 1 }, order, centres, ScoreLine.CentreWidth, out split), .001f, "The width overload matches the old centred layout.");
            Assert.AreEqual(PlayerBar.Arrange(new[] { 0, 1, 2 }, new int[8], new float[8]), ScoreLine.Arrange(new[] { 0, 1, 2 }, order, centres, 0f, out split), .001f);
        }

        [Test]
        public void TheBarSplitsBetweenTheTwoSides()
        {
            var two = Arrange(true, 0, 1, 0, 1);
            CollectionAssert.AreEqual(new[] { 0, 2, 1, 3 }, two.order.Take(4));
            Assert.AreEqual(2, two.split);
            Assert.AreEqual(1, Arrange(true, 0, 1).split);
            Assert.AreEqual(1, Arrange(true, 0, 1, 1).split, "One against two splits after the single seat.");
            var flipped = Arrange(true, 1, 1, 0);
            Assert.AreEqual(2, flipped.order[0], "The lower team number sits left.");
            Assert.AreEqual(1, flipped.split);
            Assert.AreEqual(4, Arrange(true, 0, 1, 0, 1, 0, 1, 0, 1).split);
        }

        [Test]
        public void ManySidesSplitInTheMiddleAroundATarget()
        {
            Assert.AreEqual(2, Arrange(true, 0, 1, 2, 3).split);
            Assert.AreEqual(2, Arrange(true, 0, 1, 2).split, "An odd seat sits left.");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, Arrange(true, 0, 1, 2, 3).order.Take(4), "Seat order is kept.");
        }

        [Test]
        public void TheCentreStaysInTheMiddleOfTheBar()
        {
            float half = PlayerBar.RoomyCard * .5f, pair = 2f * PlayerBar.RoomyCard + PlayerBar.RoomyGap;
            var even = Arrange(true, 0, 1, 0, 1);
            Assert.AreEqual(-Reach, even.centres[1] + half, .001f, "The left side ends one gap before the centre.");
            Assert.AreEqual(Reach, even.centres[2] - half, .001f, "The right side starts one gap after it.");
            Assert.AreEqual(2f * (Reach + pair), even.width, .001f);
            Assert.AreEqual(PlayerBar.RoomyCard + PlayerBar.RoomyGap, even.centres[1] - even.centres[0], .001f, "Team mates keep the normal gap.");
            var uneven = Arrange(true, 0, 1, 1);
            Assert.AreEqual(-Reach, uneven.centres[0] + half, .001f);
            Assert.AreEqual(Reach, uneven.centres[1] - half, .001f, "With uneven sides the centre is still at zero.");
            Assert.AreEqual(2f * (Reach + pair), uneven.width, .001f, "The bar stays symmetric around the larger side.");
            for (int i = 1; i < 3; i++) Assert.Greater(uneven.centres[i], uneven.centres[i - 1]);
        }

        [Test]
        public void EightSnugSeatsAndTheCentreStayWithinNineHundred()
        {
            var war = Arrange(true, 0, 1, 0, 1, 0, 1, 0, 1);
            Assert.LessOrEqual(war.width, 900f);
            float half = PlayerBar.SnugCard * .5f;
            Assert.AreEqual(-war.width * .5f, war.centres[0] - half, .001f);
            Assert.AreEqual(war.width * .5f, war.centres[7] + half, .001f);
            for (int i = 1; i < 8; i++) Assert.Greater(war.centres[i], war.centres[i - 1]);
        }

        [Test]
        public void WithoutACentreTheBarIsArrangedAsBefore()
        {
            foreach (var teams in new[] { new[] { 0, 1, 0, 1 }, new[] { 0, 1, 2, 3 }, new[] { 0, 0 }, new[] { 0, 1, 0, 1, 0, 1, 0, 1 } })
            {
                var order = new int[PlayerBar.MostSeats];
                var centres = new float[PlayerBar.MostSeats];
                float width = PlayerBar.Arrange(teams, order, centres);
                var plain = Arrange(false, teams);
                Assert.AreEqual(width, plain.width);
                CollectionAssert.AreEqual(order, plain.order);
                CollectionAssert.AreEqual(centres, plain.centres);
                Assert.AreEqual(0, plain.split);
            }
        }

        [Test]
        public void ArrangeToleratesMissingListsAndASingleSeat()
        {
            Assert.AreEqual(0f, ScoreLine.Arrange(null, new int[4], new float[4], true, out int none));
            Assert.AreEqual(0, none);
            Assert.AreEqual(PlayerBar.RoomyCard, Arrange(true, 0).width);
            Assert.AreEqual(0, ScoreLine.Split(new[] { 0 }, new[] { 0 }, 1));
        }

        [Test]
        public void ASideScoresItsBestMember()
        {
            var teams = new[] { 0, 1, 0, 1 };
            Assert.AreEqual(2, ScoreLine.Best(teams, new[] { 2, 1, 2, 1 }, 0), "Round wins are shared by the side.");
            Assert.AreEqual(1, ScoreLine.Best(teams, new[] { 2, 1, 2, 1 }, 3));
            Assert.AreEqual(7, ScoreLine.Best(teams, new[] { 3, 0, 7, 4 }, 0));
            Assert.AreEqual(4, ScoreLine.Best(teams, new[] { 3, 0, 7, 4 }, 1));
            Assert.AreEqual(5, ScoreLine.Best(new[] { Teams.NoTeam, Teams.NoTeam }, new[] { 5, 9 }, 0), "Seats without a team never pool.");
            Assert.AreEqual(0, ScoreLine.Best(teams, null, 0));
            Assert.AreEqual(0, ScoreLine.Best(teams, new[] { 1, 2, 3, 4 }, 9));
        }

        [Test]
        public void ScoresReadLeftColonRight()
        {
            Assert.AreEqual("1 : 2", ScoreLine.Text(1, 2));
            Assert.AreEqual("12 : 9", ScoreLine.Text(12, 9));
            Assert.AreEqual("0 : 0", ScoreLine.Text(0, 0));
        }

        [Test]
        public void ProgressReadsDoneOverTotal()
        {
            Assert.AreEqual("3/18", ScoreLine.Progress(3, 18));
            Assert.AreEqual("4/18", ScoreLine.Progress(4, 18));
            Assert.AreEqual("5", ScoreLine.Progress(5, 0), "Without a total only the count shows.");
            Assert.AreEqual("3/18", ScoreLine.Value(3, 18, true));
            Assert.AreEqual("3", ScoreLine.Value(3, 18, false));
        }

        [Test]
        public void TheLeadGoesToTheHigherSideAndNobodyOnATie()
        {
            Assert.AreEqual(ScoreLine.Left, ScoreLine.Lead(4, 3));
            Assert.AreEqual(ScoreLine.Right, ScoreLine.Lead(3, 4));
            Assert.AreEqual(ScoreLine.NoLead, ScoreLine.Lead(3, 3));
            Assert.AreEqual(ScoreLine.NoLead, ScoreLine.Lead(0, 0));
        }

        [Test]
        public void TheLeaderIsTheSingleBestSeat()
        {
            Assert.AreEqual(2, ScoreLine.Leader(new[] { 1, 3, 4, 0 }));
            Assert.AreEqual(0, ScoreLine.Leader(new[] { 1, 0, 0 }));
            Assert.AreEqual(ScoreLine.NoLead, ScoreLine.Leader(new[] { 0, 0, 0 }), "Nobody leads before the first point.");
            Assert.AreEqual(ScoreLine.NoLead, ScoreLine.Leader(new[] { 3, 3, 1 }), "A tie at the top has no leader.");
            Assert.AreEqual(2, ScoreLine.Leader(new[] { 3, 3, 5 }));
            Assert.AreEqual(ScoreLine.NoLead, ScoreLine.Leader(new[] { 1, 3, 3 }));
            Assert.AreEqual(ScoreLine.NoLead, ScoreLine.Leader(null));
            Assert.AreEqual(ScoreLine.NoLead, ScoreLine.Leader(new int[0]));
        }

        [Test]
        public void TwoSidesGetABlockEachWithTheirTotal()
        {
            var stats = new MatchStats();
            for (int seat = 0; seat < 4; seat++) stats.Enter(seat, "P" + (seat + 1), seat % 2, false);
            stats.Knockout(1, 2);
            stats.Knockout(3, 2);
            stats.Knockout(0, 3);
            stats.Craft(0);
            stats.Break(2);
            stats.Break(2);
            var seats = new[] { Seat(0, 0, 0, 12, true), Seat(1, 1, 0, 9), Seat(2, 0, 2, 12, true), Seat(3, 1, 1, 9) };
            var board = ScoreLine.Board(seats, stats);
            Assert.AreEqual(2, board.Count);
            Assert.AreEqual(0, board[0].Team, "The left block is the side on the left of the bar.");
            Assert.AreEqual(1, board[1].Team);
            Assert.AreEqual(12, board[0].Total);
            Assert.AreEqual(9, board[1].Total);
            Assert.IsTrue(board[0].Mine);
            Assert.IsFalse(board[1].Mine);
            Assert.IsFalse(board[0].Ranked);
            CollectionAssert.AreEqual(new[] { 2, 0 }, board[0].Rows.Select(r => r.Seat), "The best score leads each block.");
            CollectionAssert.AreEqual(new[] { 3, 1 }, board[1].Rows.Select(r => r.Seat));
            var top = board[0].Rows[0];
            Assert.AreEqual("P3", top.Name);
            Assert.AreEqual(2, top.Knockouts);
            Assert.AreEqual(0, top.Falls);
            Assert.AreEqual(0, top.Spelled);
            Assert.AreEqual(2, top.Smashed);
            Assert.AreEqual(2, top.Score);
            var first = board[0].Rows[1];
            Assert.AreEqual(1, first.Falls);
            Assert.AreEqual(1, first.Spelled);
            for (int column = 0; column < ScoreLine.Columns.Count; column++)
                Assert.AreEqual(new[] { top.Knockouts, top.Falls, top.Spelled, top.Smashed, top.Score }[column], top.Column(column), ScoreLine.Columns[column]);
        }

        [Test]
        public void ADuelIsTwoBlocksOfOne()
        {
            var board = ScoreLine.Board(new[] { Seat(4, 4, 1, 1), Seat(2, 2, 3, 3, true) }, null);
            Assert.AreEqual(2, board.Count);
            Assert.AreEqual(4, board[0].Team, "Without teams to sort, the bar keeps seat order.");
            Assert.AreEqual(3, board[1].Total);
            Assert.IsTrue(board[1].Mine);
        }

        [Test]
        public void ManySidesGetOneRankedListWithYourSideMarked()
        {
            var stats = new MatchStats();
            for (int seat = 0; seat < 4; seat++) stats.Enter(seat, "P" + (seat + 1), seat, seat > 0);
            stats.Knockout(0, 3);
            stats.Knockout(0, 3);
            stats.Knockout(1, 2);
            var seats = new[] { Seat(0, 0, 0, 0, true), Seat(1, 1, 0), Seat(2, 2, 1), Seat(3, 3, 2) };
            var board = ScoreLine.Board(seats, stats);
            Assert.AreEqual(1, board.Count);
            Assert.IsTrue(board[0].Ranked);
            Assert.AreEqual(Teams.NoTeam, board[0].Team);
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 0 }, board[0].Rows.Select(r => r.Seat), "Score, then knockouts, then fewer falls, then seat.");
            CollectionAssert.AreEqual(new[] { false, false, false, true }, board[0].Rows.Select(r => r.Mine));
            Assert.AreEqual(2, board[0].Rows[3].Falls);
        }

        [Test]
        public void TiesRankByKnockoutsThenFewerFallsThenSeat()
        {
            var stats = new MatchStats();
            for (int seat = 0; seat < 4; seat++) stats.Enter(seat, "P" + (seat + 1), seat, false);
            stats.Knockout(0, 2);
            stats.Knockout(3, 1);
            stats.Knockout(1, 2);
            var seats = Enumerable.Range(0, 4).Select(seat => Seat(seat, seat, 0, 0, false, false)).ToArray();
            var rows = ScoreLine.Board(seats, stats)[0].Rows;
            CollectionAssert.AreEqual(new[] { 2, 1, 0, 3 }, rows.Select(r => r.Seat));
        }

        [Test]
        public void NothingRecordedShowsZeros()
        {
            var seats = new[] { Seat(0, 0, 0, 0, true, false), Seat(1, 1, 0, 0, false, false), Seat(2, 2, 0, 0, false, false) };
            foreach (var stats in new[] { null, new MatchStats() })
            {
                var board = ScoreLine.Board(seats, stats);
                Assert.AreEqual(1, board.Count);
                Assert.AreEqual(3, board[0].Rows.Count);
                foreach (var row in board[0].Rows)
                    for (int column = 0; column < ScoreLine.Columns.Count; column++) Assert.AreEqual(0, row.Column(column));
                CollectionAssert.AreEqual(new[] { "P1", "P2", "P3" }, board[0].Rows.Select(r => r.Name));
            }
            Assert.AreEqual(0, ScoreLine.Board(null, null).Count);
            Assert.AreEqual(0, ScoreLine.Board(new ScoreSeat[0], new MatchStats()).Count);
        }

        [Test]
        public void TheScoreColumnPrefersARecordedScoreOverTheBarScore()
        {
            var stats = new MatchStats();
            stats.Enter(0, "P1", 0, false);
            stats.Enter(1, "P2", 1, false);
            stats.SetScore(0, 40);
            var board = ScoreLine.Board(new[] { Seat(0, 0, 2, 2), Seat(1, 1, 3, 3), Seat(2, 2, 1, 1) }, stats);
            var bySeat = board[0].Rows.ToDictionary(r => r.Seat, r => r.Score);
            Assert.AreEqual(40, bySeat[0], "A recorded score wins.");
            Assert.AreEqual(3, bySeat[1], "Nothing recorded falls back to the score on the bar.");
            Assert.AreEqual(1, bySeat[2], "A seat the stats never saw still shows its bar score.");
            var hidden = ScoreLine.Board(new[] { Seat(0, 0, 2, 2, false, false), Seat(1, 1, 3, 3, false, false), Seat(2, 2, 1, 1, false, false) }, null);
            Assert.IsTrue(hidden[0].Rows.All(r => r.Score == 0), "A mode without scores shows none.");
        }

        [Test]
        public void WithoutABarTheBoardReadsTheRecordedSeats()
        {
            var stats = new MatchStats();
            stats.Enter(5, "Ada", 0, false);
            stats.Enter(6, "Bo", 1, true);
            stats.Enter(7, "Cy", 2, true);
            stats.SetScore(6, 3);
            var board = ScoreLine.Board(null, stats);
            Assert.AreEqual(1, board.Count);
            CollectionAssert.AreEqual(new[] { "Bo", "Ada", "Cy" }, board[0].Rows.Select(r => r.Name));
        }

        [Test]
        public void ColumnsAreNamedInOrder()
        {
            CollectionAssert.AreEqual(new[] { "KO", "FALLS", "SPELLED", "SMASHED", "SCORE" }, ScoreLine.Columns);
            var row = ScoreLine.Board(new[] { Seat(0, 0), Seat(1, 1), Seat(2, 2) }, null)[0].Rows[0];
            Assert.Throws<System.ArgumentOutOfRangeException>(() => row.Column(5));
        }
    }
}
