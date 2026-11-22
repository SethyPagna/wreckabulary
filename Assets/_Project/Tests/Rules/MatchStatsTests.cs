using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class MatchStatsTests
    {
        static MatchStats Table(params int[] teams)
        {
            var stats = new MatchStats();
            for (int seat = 0; seat < teams.Length; seat++) stats.Enter(seat, seat == 0 ? "P1" : "AI " + (seat + 1), teams[seat], seat > 0);
            return stats;
        }

        [Test]
        public void BeginStartsAnEmptyTableAndBecomesCurrent()
        {
            var first = MatchStats.Begin();
            first.Enter(0, "P1", 0, false);
            Assert.AreSame(first, MatchStats.Current);
            var second = MatchStats.Begin();
            Assert.AreSame(second, MatchStats.Current);
            Assert.AreNotSame(first, second);
            Assert.IsEmpty(second.Lines);
            Assert.AreEqual(1, first.Lines.Count, "the old table keeps its lines");
        }

        [Test]
        public void EnteringASeatTwiceKeepsTheFirstLine()
        {
            var stats = new MatchStats();
            var line = stats.Enter(2, "P3", 1, false);
            Assert.AreSame(line, stats.Enter(2, "Someone else", 0, true));
            Assert.AreEqual(1, stats.Lines.Count);
            Assert.AreEqual("P3", line.Name);
            Assert.AreEqual(1, line.Team);
            Assert.IsFalse(line.Bot);
            Assert.AreEqual("", stats.Enter(3, null, 0, true).Name);
            Assert.IsNull(stats.Of(9));
        }

        [Test]
        public void AKnockoutCountsAFallAndCreditsTheAttacker()
        {
            var stats = Table(0, 1, 2);
            stats.Knockout(1, 0);
            stats.Knockout(2, 0);
            stats.Knockout(0, 2);
            Assert.AreEqual(2, stats.Of(0).Knockouts);
            Assert.AreEqual(1, stats.Of(0).Falls);
            Assert.AreEqual(1, stats.Of(1).Falls);
            Assert.AreEqual(1, stats.Of(2).Knockouts);
            Assert.AreEqual(1, stats.Of(2).Falls);
        }

        [Test]
        public void AFallWithNoAttackerOrByYourOwnHandCreditsNobody()
        {
            var stats = Table(0, 1);
            stats.Knockout(1, -1);
            stats.Knockout(0, 0);
            stats.Knockout(1, 7);
            Assert.AreEqual(2, stats.Of(1).Falls);
            Assert.AreEqual(1, stats.Of(0).Falls);
            Assert.AreEqual(0, stats.Of(0).Knockouts);
            Assert.AreEqual(0, stats.Of(1).Knockouts);
        }

        [Test]
        public void SeatsThatNeverEnteredAreIgnored()
        {
            var stats = Table(0);
            stats.Knockout(5, 6);
            stats.Craft(5);
            stats.Break(5);
            stats.Hurt(5, 10f);
            stats.SetScore(5, 3);
            Assert.AreEqual(1, stats.Lines.Count);
            Assert.AreEqual(0, stats.Of(0).Falls + stats.Of(0).Knockouts + stats.Of(0).Crafted + stats.Of(0).Broken + stats.Of(0).Score);
        }

        [Test]
        public void CraftBreakDamageAndScoreAddUpPerSeat()
        {
            var stats = Table(0, 1);
            stats.Craft(0); stats.Craft(0); stats.Break(1);
            stats.Hurt(0, 12.5f); stats.Hurt(0, 7.5f); stats.Hurt(0, -4f); stats.Hurt(1, 0f);
            stats.SetScore(1, 4); stats.SetScore(1, 2);
            Assert.AreEqual(2, stats.Of(0).Crafted);
            Assert.AreEqual(1, stats.Of(1).Broken);
            Assert.AreEqual(20f, stats.Of(0).Damage, 1e-4f);
            Assert.AreEqual(0f, stats.Of(1).Damage);
            Assert.AreEqual(2, stats.Of(1).Score, "a score is set, not added");
        }

        [Test]
        public void SidesAreListedInOrderWithTheirSeats()
        {
            var stats = Table(1, 0, 1, 0);
            CollectionAssert.AreEqual(new[] { 0, 1 }, stats.Sides().ToArray());
            CollectionAssert.AreEqual(new[] { 1, 3 }, stats.Side(0).Select(l => l.Seat).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 2 }, stats.Side(1).Select(l => l.Seat).ToArray());
            Assert.IsEmpty(stats.Side(5));
        }

        [Test]
        public void ASideScoreIsTheSumOfItsSeatsUntilOneIsSet()
        {
            var stats = Table(0, 1, 0, 1);
            stats.SetScore(0, 2); stats.SetScore(2, 3); stats.SetScore(1, 1);
            Assert.AreEqual(5, stats.SideScore(0));
            Assert.AreEqual(1, stats.SideScore(1));
            Assert.AreEqual(0, stats.SideScore(4), "an unknown side has nothing");
            stats.SetSideScore(0, 3);
            Assert.AreEqual(3, stats.SideScore(0), "a set score wins over the sum");
            Assert.AreEqual(1, stats.SideScore(1));
            stats.SetSideScore(0, 0);
            Assert.AreEqual(0, stats.SideScore(0), "zero is a score too");
        }

        [Test]
        public void RankingGoesByScoreThenKnockoutsThenFewerFallsThenSeat()
        {
            var stats = Table(0, 1, 2, 3, 4);
            stats.SetScore(3, 5);
            stats.SetScore(1, 2); stats.SetScore(2, 2); stats.Knockout(0, 2);
            stats.Knockout(4, -1);
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 0, 4 }, stats.Ranked().Select(l => l.Seat).ToArray());
            stats.Knockout(0, -1);
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 4, 0 }, stats.Ranked().Select(l => l.Seat).ToArray(), "fewer falls ranks higher");
            stats.Knockout(4, -1);
            CollectionAssert.AreEqual(new[] { 3, 2, 1, 0, 4 }, stats.Ranked().Select(l => l.Seat).ToArray(), "level seats keep seat order");
        }

        [Test]
        public void PointsAndRevivesAreCountedPerSeat()
        {
            var stats = Table(0, 0, 0);
            stats.Point(1); stats.Point(1); stats.Point(2);
            stats.Revive(0); stats.Revive(0);
            Assert.AreEqual(2, stats.Of(1).Score);
            Assert.AreEqual(1, stats.Of(2).Score);
            Assert.AreEqual(0, stats.Of(0).Score);
            Assert.AreEqual(2, stats.Of(0).Revives);
            Assert.AreEqual(0, stats.Of(1).Revives);
            Assert.AreEqual(3, stats.SideScore(0), "a crew's points add up");
            stats.Point(-1); stats.Point(9);
            stats.Revive(-1); stats.Revive(9);
            Assert.AreEqual(3, stats.Lines.Count, "an unknown seat is ignored");
            Assert.AreEqual(3, stats.Lines.Sum(l => l.Score));
            Assert.AreEqual(2, stats.Lines.Sum(l => l.Revives));
            stats.SetScore(1, 7);
            stats.Point(1);
            Assert.AreEqual(8, stats.Of(1).Score, "a point adds to a set score");
            CollectionAssert.AreEqual(new[] { 1, 2, 0 }, stats.Ranked().Select(l => l.Seat).ToArray(), "points rank a crew");
        }

        [Test]
        public void TheShowcaseCountsDownFromTenAndNeverShowsZero()
        {
            Assert.AreEqual(10f, MatchShowcase.DefaultSeconds);
            Assert.AreEqual(10, MatchShowcase.Count(10f));
            Assert.AreEqual(10, MatchShowcase.Count(9.01f));
            Assert.AreEqual(9, MatchShowcase.Count(9f));
            Assert.AreEqual(1, MatchShowcase.Count(.2f));
            Assert.AreEqual(1, MatchShowcase.Count(0f));
            Assert.AreEqual(1, MatchShowcase.Count(-3f));
            Assert.AreEqual("MATCH STARTS IN 10", MatchShowcase.CountLine(10f));
            Assert.AreEqual("MATCH STARTS IN 3", MatchShowcase.CountLine(2.4f));
        }

        [Test]
        public void TheShowcaseSkipLineFollowsTheVotes()
        {
            var votes = new Warmup(MatchShowcase.DefaultSeconds);
            Assert.AreEqual("", MatchShowcase.SkipLine(votes), "nobody can vote, nothing to say");
            Assert.AreEqual("", MatchShowcase.SkipLine(null));
            votes.Join(0, true); votes.Join(1, true); votes.Join(2, false);
            Assert.AreEqual("Hold Start to skip", MatchShowcase.SkipLine(votes));
            Assert.AreEqual("Hold Enter to skip", MatchShowcase.SkipLine(votes, "Enter"));
            Assert.AreEqual("Hold Start to skip", MatchShowcase.SkipLine(votes, ""), "mixed devices fall back to the word");
            for (float held = 0f; held < Warmup.ReadyHoldSeconds + .1f; held += .05f) votes.Hold(0, true, .05f);
            Assert.AreEqual("1 of 2 ready", MatchShowcase.SkipLine(votes, "Enter"));
            Assert.IsFalse(votes.Ends);
            for (float held = 0f; held < Warmup.ReadyHoldSeconds + .1f; held += .05f) votes.Hold(1, true, .05f);
            Assert.IsTrue(votes.Ends, "every human holding Start skips the showcase");
        }

        [Test]
        public void TheShowcaseLinesUpEachSideOrEveryoneInOneRow()
        {
            var everyone = MatchShowcase.Rows(new[] { 0, 1, 2, 3 });
            Assert.AreEqual(1, everyone.Count, "free-for-all is one line of everyone");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, everyone[0].ToArray());
            Assert.IsFalse(MatchShowcase.Sided(new[] { 0, 1, 2, 3 }));
            Assert.AreEqual("", MatchShowcase.RowTitle(new[] { 0, 1, 2, 3 }, everyone[0], 0));

            var duos = new[] { 0, 0, 1, 1 };
            var pairs = MatchShowcase.Rows(duos);
            Assert.IsTrue(MatchShowcase.Sided(duos));
            Assert.AreEqual(2, pairs.Count);
            CollectionAssert.AreEqual(new[] { 0, 1 }, pairs[0].ToArray());
            CollectionAssert.AreEqual(new[] { 2, 3 }, pairs[1].ToArray());
            Assert.AreEqual("YOUR TEAM", MatchShowcase.RowTitle(duos, pairs[0], 0));
            Assert.AreEqual("TEAM 2", MatchShowcase.RowTitle(duos, pairs[1], 0));
            Assert.AreEqual("TEAM 1", MatchShowcase.RowTitle(duos, pairs[0]), "a shared couch reads the plain side names");

            var battle = MatchShowcase.Rows(new[] { 1, 0, 1, 0, 1, 0, 1, 0 });
            Assert.AreEqual(2, battle.Count);
            CollectionAssert.AreEqual(new[] { 1, 3, 5, 7 }, battle[0].ToArray(), "side one first");
            CollectionAssert.AreEqual(new[] { 0, 2, 4, 6 }, battle[1].ToArray());

            Assert.IsEmpty(MatchShowcase.Rows(new int[0]));
            Assert.IsEmpty(MatchShowcase.Rows(null));
            Assert.AreEqual(1, MatchShowcase.Rows(new[] { Teams.NoTeam, Teams.NoTeam }).Count, "seats without a team share one line");
        }
    }
}
