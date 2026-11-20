using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class WarmupTests
    {
        const float Frame = 1f / 60f;

        static Warmup Couch(int humans, int bots, float seconds = Warmup.DefaultSeconds)
        {
            var warmup = new Warmup(seconds);
            for (int seat = 0; seat < humans + bots; seat++) warmup.Join(seat, seat < humans);
            return warmup;
        }

        static void HoldFor(Warmup warmup, int seat, float seconds)
        {
            for (float held = 0f; held < seconds - Frame * .5f; held += Frame) warmup.Hold(seat, true, Frame);
        }

        static void MakeReady(Warmup warmup, int seat) => HoldFor(warmup, seat, Warmup.ReadyHoldSeconds + .1f);

        [Test]
        public void OnlyTheSixVersusHouseModesWarmUp()
        {
            var expected = new[] { HouseModes.Dibs, HouseModes.Duos, HouseModes.FreeForAll, HouseModes.War, HouseModes.ShoppingList, HouseModes.RiddleRush };
            CollectionAssert.AreEquivalent(expected, Warmup.Modes.ToArray());
            foreach (string id in expected) Assert.IsTrue(Warmup.Applies(id), id);
            foreach (string id in new[] { HouseModes.MovingOut, HouseModes.Escape, HouseModes.MovingDay, HouseModes.Tutorial,
                ModeGuideBook.TimeTrial, ModeGuideBook.Freeplay, "Hub", "Default", "", null })
                Assert.IsFalse(Warmup.Applies(id), id ?? "null");
            foreach (string id in expected) Assert.IsTrue(ModeGuideBook.Has(id), id + " has cards for the strip");
            foreach (var mode in HouseModes.All.Where(m => m.Coop)) Assert.IsFalse(Warmup.Applies(mode.Id), mode.Id);
        }

        [Test]
        public void TheTimerRunsThirtySecondsByDefaultAndEndsTheWarmup()
        {
            var warmup = Couch(1, 3);
            Assert.AreEqual(30f, Warmup.DefaultSeconds);
            Assert.AreEqual(30f, warmup.Length);
            Assert.AreEqual("0:30", warmup.Clock);
            Assert.IsFalse(warmup.Ends);
            warmup.Tick(.5f);
            Assert.AreEqual("0:30", warmup.Clock, "the clock rounds up like the round clock");
            warmup.Tick(.5f);
            Assert.AreEqual("0:29", warmup.Clock);
            warmup.Tick(28.5f);
            Assert.AreEqual("0:01", warmup.Clock);
            Assert.IsFalse(warmup.Ends);
            warmup.Tick(.5f);
            Assert.AreEqual(0f, warmup.TimeLeft);
            Assert.AreEqual("0:00", warmup.Clock);
            Assert.IsTrue(warmup.Ends);
            warmup.Tick(5f);
            Assert.AreEqual(0f, warmup.TimeLeft, "time left never goes negative");
        }

        [Test]
        public void TheLengthCanBeChangedAndNegativeStepsAreIgnored()
        {
            var warmup = Couch(2, 0, 90f);
            Assert.AreEqual("1:30", warmup.Clock);
            warmup.Tick(-4f);
            Assert.AreEqual(0f, warmup.Elapsed);
            warmup.Tick(10f);
            warmup.Length = 10.5f;
            Assert.AreEqual("0:01", warmup.Clock);
            Assert.IsFalse(warmup.Ends);
            warmup.Length = -3f;
            Assert.AreEqual(0f, warmup.Length);
            Assert.IsTrue(warmup.Ends);
        }

        [Test]
        public void HoldingStartForASecondMarksASeatReadyAndLettingGoKeepsIt()
        {
            var warmup = Couch(2, 2);
            HoldFor(warmup, 0, .9f);
            Assert.IsFalse(warmup.IsReady(0), "not held long enough yet");
            Assert.That(warmup.HoldProgress(0), Is.InRange(.85f, .95f));
            HoldFor(warmup, 0, .2f);
            Assert.IsTrue(warmup.IsReady(0));
            Assert.AreEqual(1f, warmup.HoldProgress(0));
            Assert.IsTrue(warmup.Hold(0, false, Frame), "releasing does not unmark the seat");
            Assert.IsTrue(warmup.IsReady(0));
            Assert.AreEqual(1, warmup.ReadyCount);
            Assert.IsFalse(warmup.AllReady);
            Assert.IsFalse(warmup.Ends);
        }

        [Test]
        public void AShortTapOrABrokenHoldDoesNotCount()
        {
            var warmup = Couch(1, 0);
            HoldFor(warmup, 0, .6f);
            Assert.IsFalse(warmup.Hold(0, false, Frame));
            Assert.AreEqual(0f, warmup.HoldProgress(0), "letting go early starts the hold again");
            HoldFor(warmup, 0, .6f);
            Assert.IsFalse(warmup.IsReady(0));
            HoldFor(warmup, 0, .5f);
            Assert.IsTrue(warmup.IsReady(0));
        }

        [Test]
        public void EveryHumanReadyEndsItEarlyAndBotsNeverBlock()
        {
            var warmup = Couch(3, 1);
            Assert.AreEqual(3, warmup.Voters);
            MakeReady(warmup, 0);
            MakeReady(warmup, 1);
            Assert.IsFalse(warmup.Ends, "one human is still warming up");
            HoldFor(warmup, 3, 5f);
            Assert.IsFalse(warmup.IsReady(3), "a bot has no vote");
            Assert.AreEqual(0f, warmup.HoldProgress(3));
            Assert.AreEqual(2, warmup.ReadyCount);
            MakeReady(warmup, 2);
            Assert.IsTrue(warmup.AllReady);
            Assert.IsTrue(warmup.Ends);
            Assert.Greater(warmup.TimeLeft, 0f, "it ended on the votes, not the timer");
        }

        [Test]
        public void WithNoHumanVoteOnlyTheTimerEndsIt()
        {
            var warmup = Couch(0, 4, 2f);
            Assert.AreEqual(0, warmup.Voters);
            Assert.IsFalse(warmup.AllReady, "nobody voting is not everybody ready");
            Assert.IsFalse(warmup.Ends);
            Assert.AreEqual("", warmup.ReadyLine("START"));
            warmup.Tick(2f);
            Assert.IsTrue(warmup.Ends);
        }

        [Test]
        public void ASeatThatLeavesStopsBlockingTheOthers()
        {
            var warmup = Couch(2, 0);
            MakeReady(warmup, 0);
            Assert.IsFalse(warmup.Ends);
            warmup.Leave(1);
            Assert.AreEqual(1, warmup.Voters);
            Assert.IsTrue(warmup.Ends);
            warmup.Leave(0);
            Assert.AreEqual(0, warmup.Voters);
            Assert.AreEqual(0, warmup.ReadyCount);
            Assert.IsFalse(warmup.AllReady);
            warmup.Join(0, true);
            warmup.Join(0, true);
            Assert.AreEqual(1, warmup.Voters, "joining twice counts one seat");
            Assert.IsFalse(warmup.IsReady(0), "a seat that joins again votes again");
            Assert.IsFalse(warmup.Hold(7, true, 5f), "an unknown seat has no vote");
        }

        [Test]
        public void AKnockedOutSeatComesBackAfterOneSecond()
        {
            var warmup = Couch(1, 1);
            Assert.AreEqual(1f, Warmup.RespawnSeconds);
            Assert.IsFalse(warmup.Down(1, true, .5f));
            Assert.IsFalse(warmup.Down(1, true, .25f));
            Assert.IsTrue(warmup.Down(1, true, .25f), "one second down brings the seat back");
            Assert.IsFalse(warmup.Down(1, true, .5f), "the count starts again after coming back");
            Assert.IsFalse(warmup.Down(1, false, .5f));
            Assert.IsFalse(warmup.Down(1, true, .75f), "standing up clears the count");
            Assert.IsFalse(warmup.Down(9, true, 5f), "an unknown seat never comes back");
        }

        [Test]
        public void TheHudWordsArePlain()
        {
            var warmup = Couch(3, 1);
            Assert.AreEqual("WARM-UP", Warmup.Title);
            Assert.AreEqual("0:30", warmup.Clock);
            Assert.AreEqual("Hold Start when ready", warmup.ReadyLine());
            Assert.AreEqual("Hold Start when ready", warmup.ReadyLine(""));
            Assert.AreEqual("Hold ENTER when ready", warmup.ReadyLine("ENTER"));
            Assert.AreEqual("Hold START when ready", Warmup.ReadyPrompt("START"));
            MakeReady(warmup, 0);
            Assert.AreEqual("1 of 3 ready", warmup.ReadyLine("START"));
            MakeReady(warmup, 2);
            Assert.AreEqual("2 of 3 ready", warmup.ReadyLine());
            Assert.AreEqual("1:05", Warmup.FormatClock(64.2f));
            Assert.AreEqual("0:00", Warmup.FormatClock(-3f));
            foreach (string words in new[] { Warmup.Title, Warmup.Tip, Warmup.TypeAnything, Warmup.ReadyWord, warmup.ReadyLine() })
            {
                StringAssert.DoesNotContain("\u2014", words);
                StringAssert.DoesNotContain("\u2013", words);
            }
        }

        [Test]
        public void TheCardTurnsEveryFiveSecondsAndLoops()
        {
            var warmup = Couch(1, 0, 60f);
            Assert.AreEqual(5f, Warmup.CardSeconds);
            Assert.AreEqual(0, warmup.Card(4));
            warmup.Tick(4.9f);
            Assert.AreEqual(0, warmup.Card(4));
            warmup.Tick(.2f);
            Assert.AreEqual(1, warmup.Card(4));
            warmup.Tick(10f);
            Assert.AreEqual(3, warmup.Card(4));
            warmup.Tick(5f);
            Assert.AreEqual(0, warmup.Card(4), "after the last card the first shows again");
            Assert.AreEqual(1, warmup.Card(3), "a three card guide loops on its own count");
            Assert.AreEqual(0, warmup.Card(0));
            Assert.AreEqual(0, warmup.Card(-2));
        }

        [Test]
        public void TheSameStepsAlwaysGiveTheSameWarmup()
        {
            string Run()
            {
                var warmup = Couch(2, 2);
                var log = new System.Text.StringBuilder();
                for (int frame = 0; frame < 600 && !warmup.Ends; frame++)
                {
                    warmup.Tick(Frame);
                    warmup.Hold(0, frame >= 100, Frame);
                    warmup.Hold(1, frame >= 300 && frame % 50 != 0, Frame);
                    if (warmup.Down(3, frame % 200 > 20, Frame)) log.Append('r').Append(frame).Append(' ');
                    if (frame % 60 == 0) log.Append(warmup.Clock).Append('|').Append(warmup.ReadyLine()).Append('|').Append(warmup.Card(4)).Append(' ');
                }
                return log.Append(warmup.Elapsed.ToString("R", System.Globalization.CultureInfo.InvariantCulture)).ToString();
            }
            string first = Run();
            Assert.AreEqual(first, Run());
            StringAssert.Contains("1 of 2 ready", first);
        }
    }
}
