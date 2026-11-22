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
        public void TheSixVersusAndTheThreeCoopHouseModesWarmUp()
        {
            var versus = new[] { HouseModes.Dibs, HouseModes.Duos, HouseModes.FreeForAll, HouseModes.War, HouseModes.ShoppingList, HouseModes.RiddleRush };
            var crew = new[] { HouseModes.MovingOut, HouseModes.Escape, HouseModes.MovingDay };
            CollectionAssert.AreEquivalent(versus, Warmup.VersusModes.ToArray());
            CollectionAssert.AreEquivalent(crew, Warmup.CrewModes.ToArray());
            CollectionAssert.AreEquivalent(versus.Concat(crew).ToArray(), Warmup.Modes.ToArray());
            foreach (string id in Warmup.Modes)
            {
                Assert.IsTrue(Warmup.Applies(id), id);
                Assert.IsTrue(ModeGuideBook.Has(id), id + " has cards for the strip");
                Assert.IsTrue(HouseModes.Has(id), id + " is a house mode");
            }
            foreach (string id in new[] { HouseModes.Tutorial, ModeGuideBook.TimeTrial, ModeGuideBook.Freeplay, "Hub", "Default", "", null })
            {
                Assert.IsFalse(Warmup.Applies(id), id ?? "null");
                Assert.IsFalse(Warmup.Crew(id), id ?? "null");
            }
        }

        [Test]
        public void ACrewModeIsExactlyACoopHouseMode()
        {
            foreach (var mode in HouseModes.All)
                Assert.AreEqual(mode.Coop, Warmup.Crew(mode.Id), mode.Id);
            foreach (string id in Warmup.VersusModes) Assert.IsFalse(Warmup.Crew(id), id);
            foreach (string id in Warmup.CrewModes) Assert.AreEqual(0, HouseModes.BotSeats(id), id + " has no bots, so every seat can ready up");
            CollectionAssert.DoesNotContain(Warmup.Modes.ToArray(), HouseModes.Tutorial, "the Tutorial never warms up");
        }

        [Test]
        public void ACrewLinesUpAsOneTeamUnderYourCrew()
        {
            Assert.AreEqual("YOUR CREW", MatchShowcase.YourCrew);
            foreach (string mode in Warmup.CrewModes)
                foreach (var teams in new[] { new[] { 0 }, new[] { 0, 0 }, new[] { 0, 0, 0, 0 }, new[] { 0, 1, 0, 1 }, new[] { Teams.NoTeam, Teams.NoTeam } })
                {
                    var rows = MatchShowcase.Rows(teams, mode);
                    Assert.AreEqual(1, rows.Count, mode + " shows one line");
                    CollectionAssert.AreEqual(Enumerable.Range(0, teams.Length).ToArray(), rows[0].ToArray());
                    Assert.AreEqual("YOUR CREW", MatchShowcase.RowTitle(teams, rows[0], Teams.NoTeam, mode), "a shared couch is still one crew");
                    Assert.AreEqual("YOUR CREW", MatchShowcase.RowTitle(teams, rows[0], 0, mode));
                }
            Assert.IsEmpty(MatchShowcase.Rows(new int[0], HouseModes.Escape));
            Assert.IsEmpty(MatchShowcase.Rows(null, HouseModes.Escape));
            Assert.AreEqual("", MatchShowcase.RowTitle(new[] { 0 }, new int[0], 0, HouseModes.Escape));
            Assert.AreEqual("", MatchShowcase.RowTitle(new[] { 0 }, null, 0, HouseModes.Escape));
        }

        [Test]
        public void VersusLineUpsAreTheSameWithOrWithoutTheModeName()
        {
            foreach (string mode in Warmup.VersusModes.Concat(new[] { null, "Other" }))
                foreach (var teams in new[] { new[] { 0, 1, 2, 3 }, new[] { 0, 0, 1, 1 }, new[] { 1, 0, 1, 0, 1, 0, 1, 0 }, new[] { 0 } })
                {
                    var plain = MatchShowcase.Rows(teams);
                    var named = MatchShowcase.Rows(teams, mode);
                    Assert.AreEqual(plain.Count, named.Count, mode);
                    for (int row = 0; row < plain.Count; row++)
                    {
                        CollectionAssert.AreEqual(plain[row].ToArray(), named[row].ToArray(), mode);
                        Assert.AreEqual(MatchShowcase.RowTitle(teams, plain[row], 0), MatchShowcase.RowTitle(teams, named[row], 0, mode), mode);
                        Assert.AreEqual(MatchShowcase.RowTitle(teams, plain[row]), MatchShowcase.RowTitle(teams, named[row], Teams.NoTeam, mode), mode);
                    }
                }
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

        [Test]
        public void OnlyTheLastThreeSecondsOfTheLineUpTick()
        {
            Assert.IsTrue(MatchShowcase.Ticks(3));
            Assert.IsTrue(MatchShowcase.Ticks(1));
            Assert.IsFalse(MatchShowcase.Ticks(4));
            Assert.IsFalse(MatchShowcase.Ticks(10));
            Assert.IsFalse(MatchShowcase.Ticks(0));
            Assert.AreEqual(3, MatchShowcase.Count(2.4f));
        }

        [Test]
        public void LineUpCardsSlideInFromTheirOwnSide()
        {
            Assert.AreEqual(-1, MatchShowcase.SlideSide(0, 4, 0, 2), "the left team comes from the left");
            Assert.AreEqual(-1, MatchShowcase.SlideSide(1, 4, 0, 2));
            Assert.AreEqual(1, MatchShowcase.SlideSide(2, 4, 1, 2), "the right team from the right");
            Assert.AreEqual(1, MatchShowcase.SlideSide(3, 4, 1, 2));
            Assert.AreEqual(-1, MatchShowcase.SlideSide(0, 4, 0, 1), "one line splits down the middle");
            Assert.AreEqual(-1, MatchShowcase.SlideSide(1, 4, 0, 1));
            Assert.AreEqual(1, MatchShowcase.SlideSide(2, 4, 0, 1));
            Assert.AreEqual(1, MatchShowcase.SlideSide(3, 4, 0, 1));
            Assert.AreEqual(0, MatchShowcase.SlideSide(1, 3, 0, 1), "the middle card rises in place");
            Assert.AreEqual(0, MatchShowcase.SlideSide(0, 1, 0, 1));

            Assert.AreEqual(0f, MatchShowcase.Slide(0f, 0f));
            Assert.AreEqual(1f, MatchShowcase.Slide(MatchShowcase.SlideSeconds, 0f));
            Assert.AreEqual(1f, MatchShowcase.Slide(5f, 0f), "it stays in place once it lands");
            Assert.AreEqual(0f, MatchShowcase.Slide(.1f, .2f), "a later card waits for its turn");
            float previous = 0f;
            for (int step = 1; step <= 10; step++)
            {
                float now = MatchShowcase.Slide(step * MatchShowcase.SlideSeconds / 10f, 0f);
                Assert.GreaterOrEqual(now, previous);
                previous = now;
            }
            Assert.Greater(MatchShowcase.Slide(MatchShowcase.SlideSeconds * .5f, 0f), .5f, "it eases out");
            Assert.AreEqual(0f, MatchShowcase.SlideDelay(0, 4, 0, 1));
            Assert.AreEqual(0f, MatchShowcase.SlideDelay(3, 4, 0, 1), "the outer cards lead");
            Assert.Greater(MatchShowcase.SlideDelay(1, 4, 0, 1), 0f);
            Assert.Less(MatchShowcase.SlideDelay(7, 8, 1, 2) + MatchShowcase.SlideSeconds, MatchShowcase.DefaultSeconds);
        }
    }
}
