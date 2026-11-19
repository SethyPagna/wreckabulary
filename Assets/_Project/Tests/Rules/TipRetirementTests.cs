using System;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class TipRetirementTests
    {
        [Test]
        public void ATipRetiresAfterTwentySecondsOnScreen()
        {
            var clock = new TipRetirement(8);
            Assert.IsFalse(clock.Show(0, 10f));
            Assert.IsFalse(clock.Show(0, 9.5f));
            Assert.IsFalse(clock.Spent(0));
            Assert.IsTrue(clock.Show(0, .5f), "20 s on screen is enough");
            Assert.IsTrue(clock.Spent(0));
            Assert.IsFalse(clock.Spent(1), "each tip keeps its own time");
            Assert.AreEqual(0f, clock.SecondsShown(1));
        }

        [Test]
        public void ATipShowsInThreeMatchesAndRetiresOnTheFourth()
        {
            var clock = new TipRetirement(8);
            for (int match = 1; match <= TipRetirement.Matches; match++)
            {
                Assert.IsFalse(clock.Spent(2));
                Assert.IsFalse(clock.Show(2, 1f));
                Assert.IsFalse(clock.Show(2, 1f));
                Assert.AreEqual(match, clock.MatchesShown(2), "a match counts once, however long the tip shows");
                clock.BeginMatch();
            }
            Assert.IsTrue(clock.Spent(2), "the fourth match retires it before it shows");
            Assert.IsTrue(clock.Show(2, 0f));
            Assert.AreEqual(0, clock.MatchesShown(3));
        }

        [Test]
        public void MatchesWithoutTheTipDoNotCount()
        {
            var clock = new TipRetirement(8);
            for (int i = 0; i < 6; i++) clock.BeginMatch();
            Assert.IsFalse(clock.Spent(4));
            Assert.IsFalse(clock.Show(4, 1f));
            Assert.AreEqual(1, clock.MatchesShown(4));
        }

        [Test]
        public void MatchCountsCarryOverToTheNextLaunch()
        {
            var first = new TipRetirement(8);
            first.Show(3, 1f);
            first.BeginMatch();
            first.Show(3, 1f);
            first.Show(7, 1f);
            var again = new TipRetirement(8, first.Seen);
            Assert.AreEqual(2, again.MatchesShown(3));
            Assert.AreEqual(1, again.MatchesShown(7));
            Assert.AreEqual(0, again.MatchesShown(0));
            Assert.AreEqual(0f, again.SecondsShown(3), "time on screen starts over");
            Assert.IsFalse(again.Show(3, 1f));
            again.BeginMatch();
            Assert.IsTrue(again.Spent(3));
        }

        [Test]
        public void ClearingForgetsEverything()
        {
            var clock = new TipRetirement(8);
            clock.Show(1, 25f);
            clock.Show(2, 1f);
            clock.Clear();
            Assert.AreEqual(0, clock.Seen);
            Assert.IsFalse(clock.Spent(1));
            Assert.IsFalse(clock.Show(2, 1f));
            Assert.AreEqual(1, clock.MatchesShown(2));
        }

        [Test]
        public void TheCountsFitInOneSavedNumber()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TipRetirement(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TipRetirement(16));
            var clock = new TipRetirement(15);
            for (int match = 0; match < 5; match++, clock.BeginMatch())
                for (int tip = 0; tip < 15; tip++) clock.Show(tip, 0f);
            for (int tip = 0; tip < 15; tip++) Assert.AreEqual(TipRetirement.Matches, clock.MatchesShown(tip));
            Assert.GreaterOrEqual(clock.Seen, 0);
        }
    }
}
