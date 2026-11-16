using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class IdleWatchTests
    {
        [Test]
        public void APlayerWhoStandsStillWithNoInputForThirtySecondsIsIdle()
        {
            var watch = new IdleWatch();
            Assert.IsFalse(watch.IsIdle(1, 0), "nobody is idle before they are seen");
            watch.Observe(1, 0f, 0f, 0f, false, true, 10);
            watch.Observe(1, .3f, 0f, 0f, false, true, 39.9);
            Assert.IsFalse(watch.IsIdle(1, 39.9), "a small nudge is not a move, but the clock has not run out");
            Assert.IsTrue(watch.IsIdle(1, 40), "thirty seconds without input or a real move");
        }

        [Test]
        public void InputOrARealMoveRestartsTheClock()
        {
            var watch = new IdleWatch();
            watch.Observe(0, 0f, 0f, 0f, false, true, 0);
            watch.Observe(0, 0f, 0f, 0f, true, true, 25);
            Assert.IsFalse(watch.IsIdle(0, 54));
            Assert.IsTrue(watch.IsIdle(0, 55));
            watch.Observe(0, 0f, 0f, .6f, false, true, 56);
            Assert.IsFalse(watch.IsIdle(0, 85), "walking on counts as playing");
            watch.Observe(0, 0f, 3f, .6f, false, true, 86);
            Assert.IsFalse(watch.IsIdle(0, 115), "so does climbing the stairs");
        }

        [Test]
        public void ALostControllerIsIdleAtOnceAndPluggingItBackInRejoins()
        {
            var watch = new IdleWatch();
            watch.Observe(2, 0f, 0f, 0f, true, true, 0);
            watch.Observe(2, 0f, 0f, 0f, false, false, 1);
            Assert.IsTrue(watch.IsIdle(2, 1));
            watch.Observe(2, 0f, 0f, 0f, false, true, 5);
            Assert.IsFalse(watch.IsIdle(2, 34));
            watch.Clear();
            Assert.IsFalse(watch.IsIdle(2, 1000), "a new round forgets everyone");
        }

        [Test]
        public void AShorterWatchSuitsTests()
        {
            var watch = new IdleWatch(2);
            watch.Observe(0, 0f, 0f, 0f, false, true, 0);
            Assert.IsTrue(watch.IsIdle(0, 2));
        }

        [Test]
        public void TheNoticeNamesEveryIdlePlayer()
        {
            Assert.AreEqual("", IdleWatch.Notice(0));
            Assert.AreEqual("P3 is idle · the crew can leave without them", IdleWatch.Notice(1 << 2));
            Assert.AreEqual("P2 and P4 are idle · the crew can leave without them", IdleWatch.Notice(1 << 1 | 1 << 3));
            Assert.AreEqual("P1, P2 and P3 are idle · the crew can leave without them", IdleWatch.Notice(0b111));
        }
    }
}
