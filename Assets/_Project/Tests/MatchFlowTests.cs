using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class MatchFlowTests
    {
        const float Slow = 30f, LongClock = 600f, ShortCountdown = .05f, CarriedHoldSeconds = 1.6f, LeftOnTheClock = 2.5f, PausedSeconds = .5f;
        const float Slack = 1f, SmallCardWidth = 575.5f;
        const int ReleaseFrames = 5;

        static readonly string[] LineUps = { HouseModes.Dibs, HouseModes.Duos, HouseModes.FreeForAll, HouseModes.War };
        static readonly string[] BothFlows = { HouseModes.Dibs, HouseModes.FreeForAll };

        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RoundManager.WarmupRequested = false;
            Time.timeScale = 1f;
            yield return TestScenes.Reset();
        }

        static PlayerJoinManager Joins => Object.FindAnyObjectByType<PlayerJoinManager>();
        static GameHud Hud => Object.FindAnyObjectByType<GameHud>();
        static RoundManager Rounds => RoundManager.Instance;
        static HouseRoundDirector Director => Object.FindAnyObjectByType<HouseRoundDirector>();
        static PlayerController Human(int nth = 0) => Joins.Players.Where(p => p.Binding is ScriptedBinding).ElementAt(nth);
        static PlayerController Bot => Joins.Players.First(p => p.Binding is BotBinding);
        static Transform ResultCard => Hud.UiCanvas.transform.Find("Safe HUD/Result/Result card");
        static string Text(string path) => ResultCard.Find(path).GetComponent<TMP_Text>().text;
        static bool Active(string path) => ResultCard.Find(path).gameObject.activeSelf;

        static IEnumerator Load(string mode, bool warmup, int humans = 1)
        {
            Session.SelectMap("pinwheel");
            Match.ModeOverride = mode;
            for (int i = 0; i < humans; i++) Session.Remember(new ScriptedBinding());
            RoundManager.WarmupRequested = warmup;
            yield return TestScenes.Load(Session.DibsScene);
            yield return TestScenes.ExploreHouse();
        }

        static IEnumerator LoadWarmup(string mode, int humans = 1)
        {
            yield return Load(mode, true, humans);
            yield return TestScenes.WaitUntil(() => Rounds && Rounds.Phase == Phase.Warmup && Rounds.Warmup != null, Slow, "the warm-up to start in " + mode);
            Rounds.Warmup.State.Length = LongClock;
            StopBots();
            Hud.Follow(Human());
        }

        static IEnumerator LoadShowcase(string mode, int humans = 1)
        {
            yield return LoadWarmup(mode, humans);
            EndWarmup();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Showcase && Rounds.Showcase != null, Slow, "the team showcase in " + mode);
            Rounds.Showcase.State.Length = LongClock;
        }

        static void EndWarmup() => Rounds.Warmup.State.Length = Rounds.Warmup.State.Elapsed;

        static void EndShowcase() => Rounds.Showcase.State.Length = Rounds.Showcase.State.Elapsed;

        static void StopBots()
        {
            foreach (var p in Joins.Players)
            {
                if (p.TryGetComponent<BotController>(out var bot)) bot.enabled = false;
                if (p.Binding is BotBinding binding) binding.Commands = default;
                var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0f;
                p.Health.UseRules(rules);
            }
        }

        static void QuickCountdown()
        {
            Rounds.CountdownTime = ShortCountdown;
            if (Director) Director.CountdownSeconds = ShortCountdown;
        }

        static void Knock(PlayerController victim, PlayerController attacker) =>
            victim.Health.ApplyDamage(Hits.Of(attacker, Vector3.forward, HitSource.Melee, 1000f, 1f));

        static void HoldStart(PlayerController player) => ((ScriptedBinding)player.Binding).Next.start = true;

        static IEnumerator UntilFree(PlayerController player) =>
            TestScenes.WaitUntil(() => player.CanAct && !player.IsDodging, Slow, "the player to be free to act");

        static Rect OnScreen(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static Rect SafeArea() => OnScreen((RectTransform)Hud.UiCanvas.transform.Find("Safe HUD"));

        static int Count()
        {
            string text = Hud.ShowcaseCountText;
            int space = text.LastIndexOf(' ');
            return space >= 0 && int.TryParse(text.Substring(space + 1), out int left) ? left : 0;
        }

        static void AssertReadable(Transform root, float smallest)
        {
            foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Assert.IsTrue(text.enableAutoSizing, text.name);
                Assert.GreaterOrEqual(text.fontSizeMin, smallest, text.name);
                Assert.GreaterOrEqual(text.fontSizeMax, text.fontSizeMin, text.name);
                Assert.AreNotEqual(TextOverflowModes.Ellipsis, text.overflowMode, text.name + " is never cut short");
                Assert.AreNotEqual(TextOverflowModes.Truncate, text.overflowMode, text.name);
            }
        }

        [UnityTest]
        public IEnumerator ADirectStartHasNoShowcase([ValueSource(nameof(BothFlows))] string mode)
        {
            yield return Load(mode, false);
            yield return TestScenes.WaitUntil(() => Rounds && Rounds.Phase != Phase.Lobby, Slow, mode + " to start");
            Assert.That(Rounds.Phase, Is.EqualTo(Phase.Countdown).Or.EqualTo(Phase.Playing), "a direct start goes straight to round one");
            Assert.IsNull(Rounds.Showcase);
            Assert.IsFalse(Hud.ShowcaseShown);
            Assert.IsNull(Hud.UiCanvas.transform.Find("Safe HUD/" + GameHud.ShowcaseRootName), "the overlay is not even built");
            Assert.AreEqual(1, Rounds.Round);
        }

        [UnityTest]
        public IEnumerator AShowcaseTimeOfZeroGoesStraightToRoundOne([ValueSource(nameof(BothFlows))] string mode)
        {
            yield return LoadWarmup(mode);
            Rounds.ShowcaseTime = 0f;
            EndWarmup();
            yield return TestScenes.WaitUntil(() => Rounds.Phase != Phase.Warmup, Slow, "the warm-up to end");
            Assert.AreEqual(Phase.Countdown, Rounds.Phase, "no showcase, the usual countdown follows at once");
            Assert.IsNull(Rounds.Showcase);
            Assert.IsFalse(Hud.ShowcaseShown);
            Assert.AreEqual(1, Rounds.Round);
        }

        [UnityTest]
        public IEnumerator TheShowcaseFollowsTheWarmupAndLinesUpEverySeat([ValueSource(nameof(LineUps))] string mode)
        {
            yield return LoadShowcase(mode);
            var players = Joins.Players;
            Assert.IsNull(Rounds.Warmup, "the warm-up is closed");
            Assert.IsFalse(LetterInventory.FreeSpelling);
            Assert.AreEqual(0, Rounds.Round, "round one waits for the showcase");
            Assert.IsTrue(players.All(p => p.Frozen), "everyone stands still for the line-up");
            Assert.IsTrue(players.All(p => !p.IsKnockedOut), "everyone is on their feet");
            Assert.IsFalse(Joins.AllowJoining);
            Assert.IsFalse(Hud.ResultShown);
            Assert.IsFalse(Hud.CountdownShown);
            Assert.IsFalse(BackToHub.StartPauses(Hud), "Start is the skip button here, not pause");
            if (Rounds.Directed) Assert.AreEqual(HouseRoundDirector.Stage.Showcase, Director.Current);

            Assert.IsTrue(Hud.ShowcaseShown);
            Assert.AreEqual(players.Count, Hud.ShowcaseSeatCount, "one card per seat, bots included");
            bool sided = MatchShowcase.Sided(players.Select(p => p.Team).ToArray());
            Assert.AreEqual(mode == HouseModes.Duos || mode == HouseModes.War, sided);
            Assert.AreEqual(sided ? 2 : 1, Hud.ShowcaseRowCount, sided ? "two sides, side by side" : "one line of everyone");
            StringAssert.StartsWith(HouseModes.Name(mode).ToUpperInvariant(), Hud.ShowcaseModeText);
            StringAssert.IsMatch(@"^MATCH STARTS IN \d+$", Hud.ShowcaseCountText);
            Assert.That(Count(), Is.InRange(1, (int)LongClock));
            Assert.AreEqual(MatchShowcase.SkipPrompt(ControlHints.For(Human().Binding, PlayerControl.Start)), Hud.ShowcaseSkipText);

            var shown = Enumerable.Range(0, Hud.ShowcaseSeatCount).Select(Hud.ShowcaseSeatPlayer).ToArray();
            CollectionAssert.AreEquivalent(players.ToArray(), shown, "every seat is shown once");
            for (int i = 0; i < shown.Length; i++)
            {
                bool bot = shown[i].Binding is BotBinding;
                Assert.AreEqual(bot ? shown[i].Name : "You", Hud.ShowcaseSeatName(i));
                Assert.AreEqual(bot ? MatchShowcase.BotTag : "", Hud.ShowcaseSeatTag(i), "bots are marked: " + shown[i].Name);
                Assert.AreEqual(GameHud.ShowcaseSideColour(shown[i], players, sided), Hud.ShowcaseSeatColour(i), shown[i].Name);
                for (int j = 0; j < i; j++)
                {
                    bool together = shown[i].Team == shown[j].Team;
                    if (!sided) continue;
                    Assert.AreEqual(together, Hud.ShowcaseSeatRow(i) == Hud.ShowcaseSeatRow(j), "team mates stand together");
                    Assert.AreEqual(together, Hud.ShowcaseSeatColour(i) == Hud.ShowcaseSeatColour(j), "a side shares one colour");
                }
            }
            if (sided)
            {
                Assert.AreEqual(MatchShowcase.YourTeam, Hud.ShowcaseRowTitle(Hud.ShowcaseSeatRow(System.Array.IndexOf(shown, Human()))));
                Assert.IsNotEmpty(Hud.ShowcaseRowTitle(0));
                Assert.IsNotEmpty(Hud.ShowcaseRowTitle(1));
            }

            Assert.IsTrue(Hud.ShowcaseRoot.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "nothing on the showcase takes a click");
            Assert.IsEmpty(Hud.ShowcaseRoot.GetComponentsInChildren<Selectable>(true), "nothing on the showcase takes focus");
            AssertReadable(Hud.ShowcaseRoot, GameHud.ShowcaseMinFont);
            Canvas.ForceUpdateCanvases();
            var safe = SafeArea();
            for (int i = 0; i < shown.Length; i++)
            {
                var box = OnScreen(Hud.ShowcaseSeatRect(i));
                Assert.That(box.xMin, Is.GreaterThanOrEqualTo(safe.xMin - Slack), "seat " + i);
                Assert.That(box.xMax, Is.LessThanOrEqualTo(safe.xMax + Slack), "seat " + i);
                Assert.That(box.yMin, Is.GreaterThanOrEqualTo(safe.yMin - Slack), "seat " + i);
                Assert.That(box.yMax, Is.LessThanOrEqualTo(safe.yMax + Slack), "seat " + i);
                if (i > 0) Assert.That(box.xMin, Is.GreaterThanOrEqualTo(OnScreen(Hud.ShowcaseSeatRect(i - 1)).xMax - Slack), "seats stand side by side");
            }

            EndShowcase();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Countdown, Slow, "the usual countdown after the showcase in " + mode);
            Assert.AreEqual(1, Rounds.Round);
            Assert.IsNull(Rounds.Showcase);
            Assert.IsFalse(Hud.ShowcaseShown);
            Assert.AreEqual(0, Hud.ShowcaseSeatCount);
            Assert.IsTrue(players.All(p => p.Frozen), "everyone waits out the countdown as usual");
            Assert.IsTrue(players.All(p => p.Health.IsAlive && Mathf.Approximately(p.Health.Current, p.Health.Max)));
            if (Rounds.Directed) Assert.AreEqual(HouseRoundDirector.Stage.Countdown, Director.Current);

            QuickCountdown();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Playing, Slow, mode + " to reach Playing");
            Assert.AreEqual(1, Rounds.Round);
            Assert.IsTrue(players.All(p => !p.Frozen), "round one plays as it always did");

            Rounds.StartMatch();
            yield return null;
            Assert.AreNotEqual(Phase.Warmup, Rounds.Phase, "a rematch has no warm-up");
            Assert.AreNotEqual(Phase.Showcase, Rounds.Phase, "and no showcase");
            Assert.IsNull(Rounds.Showcase);
            Assert.IsFalse(Hud.ShowcaseShown);
        }

        [UnityTest]
        public IEnumerator TheShowcaseCountsDownFromTenByItself()
        {
            yield return LoadWarmup(HouseModes.Dibs);
            Assert.AreEqual(MatchShowcase.DefaultSeconds, Rounds.ShowcaseTime);
            EndWarmup();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Showcase && Rounds.Showcase != null, Slow, "the team showcase");
            var state = Rounds.Showcase.State;
            Assert.AreEqual(MatchShowcase.DefaultSeconds, state.Length, "ten seconds unless somebody skips");
            Assert.That(Count(), Is.InRange(1, 10));
            state.Tick(state.TimeLeft - LeftOnTheClock);
            yield return TestScenes.WaitUntil(() => Rounds.Phase != Phase.Showcase || Count() <= 3, Slow, "the big number to follow the clock");
            if (Rounds.Phase == Phase.Showcase) Assert.That(Count(), Is.InRange(1, 3), "the count never shows zero");
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Countdown, Slow, "round one once the count runs out");
            Assert.AreEqual(1, Rounds.Round);
            Assert.IsFalse(state.AllReady, "nobody skipped it");
            Assert.IsFalse(Hud.ShowcaseShown);
            Assert.IsNull(Rounds.Showcase);
        }

        [UnityTest]
        public IEnumerator EveryHumanHoldingStartSkipsTheShowcaseButACarriedHoldDoesNot()
        {
            yield return LoadWarmup(HouseModes.Dibs, humans: 2);
            var first = Human(0);
            var second = Human(1);
            float deadline = Time.realtimeSinceStartup + Slow;
            while (Rounds.Phase == Phase.Warmup && Time.realtimeSinceStartup < deadline)
            {
                HoldStart(first); HoldStart(second);
                yield return null;
            }
            Assert.AreEqual(Phase.Showcase, Rounds.Phase, "everyone ready ends the warm-up and opens the showcase");
            var state = Rounds.Showcase.State;
            state.Length = LongClock;
            Assert.AreEqual(2, state.Voters, "both humans vote, the bots do not");

            deadline = Time.realtimeSinceStartup + Slow;
            while (state.Elapsed < CarriedHoldSeconds && Rounds.Phase == Phase.Showcase && Time.realtimeSinceStartup < deadline)
            {
                HoldStart(first); HoldStart(second);
                yield return null;
            }
            Assert.AreEqual(Phase.Showcase, Rounds.Phase, "a button still held from the warm-up skips nothing");
            Assert.GreaterOrEqual(state.Elapsed, CarriedHoldSeconds);
            Assert.AreEqual(0, state.ReadyCount);
            Assert.AreEqual(MatchShowcase.SkipPrompt(ControlHints.For(first.Binding, PlayerControl.Start)), Hud.ShowcaseSkipText);

            for (int i = 0; i < ReleaseFrames; i++) yield return null;
            deadline = Time.realtimeSinceStartup + Slow;
            while (!state.IsReady(first.Index) && Time.realtimeSinceStartup < deadline)
            {
                HoldStart(first);
                yield return null;
            }
            Assert.IsTrue(state.IsReady(first.Index), "a fresh hold counts");
            Assert.AreEqual(Phase.Showcase, Rounds.Phase, "one human is not everyone");
            yield return TestScenes.WaitUntil(() => Hud.ShowcaseSkipText == "1 of 2 ready", Slow, "the ready count under the countdown");

            deadline = Time.realtimeSinceStartup + Slow;
            while (Rounds.Phase == Phase.Showcase && Time.realtimeSinceStartup < deadline)
            {
                HoldStart(second);
                yield return null;
            }
            Assert.AreEqual(Phase.Countdown, Rounds.Phase, "every human holding Start skips the showcase");
            Assert.IsTrue(state.AllReady);
            Assert.Greater(state.TimeLeft, 0f, "it ended on the votes, not the timer");
            Assert.AreEqual(1, Rounds.Round);
            Assert.IsFalse(Hud.ShowcaseShown);
            Assert.IsFalse(Hud.Paused, "Start never opened the pause menu");
        }

        [UnityTest]
        public IEnumerator PausingHoldsTheShowcaseClock()
        {
            yield return LoadShowcase(HouseModes.FreeForAll);
            var state = Rounds.Showcase.State;
            Hud.TogglePause();
            yield return TestScenes.WaitUntil(() => Hud.Paused && Time.timeScale == 0f, Slow, "the pause menu");
            float held = state.Elapsed;
            yield return new WaitForSecondsRealtime(PausedSeconds);
            Assert.AreEqual(held, state.Elapsed, "the showcase clock waits while paused");
            Assert.AreEqual(Phase.Showcase, Rounds.Phase);
            Hud.TogglePause();
            yield return TestScenes.WaitUntil(() => Hud.AcceptsGameplayInput, Slow, "the game to resume");
            yield return TestScenes.WaitUntil(() => state.Elapsed > held, Slow, "the showcase clock to run again");
            Assert.AreEqual(Phase.Showcase, Rounds.Phase);
            Assert.IsTrue(Hud.ShowcaseShown);
            Assert.IsTrue(Joins.Players.All(p => p.Frozen), "the line-up still stands still after the pause");
        }

        [UnityTest]
        public IEnumerator NothingIsRecordedBeforeRoundOneThenTheMatchIsTallied()
        {
            yield return LoadWarmup(HouseModes.Dibs);
            var human = Human();
            var victim = Bot;
            var room = Object.FindAnyObjectByType<RoomBuilder>();
            Assert.IsFalse(MatchTally.StatsLive, "nothing is live in the warm-up");
            Assert.IsEmpty(MatchStats.Current.Lines, "the table is empty until the match begins");
            Knock(victim, human);
            yield return TestScenes.WaitUntil(() => !victim.IsKnockedOut, Slow, "the warm-up to bring the seat back");
            human.Inventory.Collects = false;
            yield return UntilFree(human);
            Assert.IsTrue(human.Summoner.Summon("BAT"));
            room.Originals.First(s => s && !s.IsBroken && !s.Invulnerable).Break();
            yield return null;
            Assert.IsEmpty(MatchStats.Current.Lines, "a warm-up knockout, spell and smash are not recorded");

            EndWarmup();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Showcase && Rounds.Showcase != null, Slow, "the team showcase");
            Assert.IsFalse(MatchTally.StatsLive);
            Assert.IsEmpty(MatchStats.Current.Lines, "the showcase records nothing either");
            EndShowcase();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Countdown, Slow, "round one");

            var stats = MatchStats.Current;
            Assert.IsTrue(MatchTally.StatsLive, "the table opens with the match");
            Assert.IsFalse(MatchTally.StatsRecording, "the countdown records nothing");
            Assert.AreEqual(Joins.Players.Count, stats.Lines.Count, "every seat is entered");
            foreach (var p in Joins.Players)
            {
                var line = stats.Of(p.Index);
                Assert.IsNotNull(line, p.Name);
                Assert.AreEqual(p.Name, line.Name);
                Assert.AreEqual(p.Team, line.Team);
                Assert.AreEqual(p.Binding is BotBinding, line.Bot);
                Assert.AreEqual(0, line.Knockouts + line.Falls + line.Crafted + line.Broken + line.Score, p.Name + " starts from nothing");
                Assert.AreEqual(0f, line.Damage, p.Name);
            }

            QuickCountdown();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Playing, Slow, "round one to start");
            StopBots();
            Assert.IsTrue(MatchTally.StatsRecording);
            victim = Bot;
            yield return TestScenes.WaitUntil(() => !victim.Health.IsInvulnerable, Slow, "spawn protection to wear off");
            victim.Health.ApplyDamage(Hits.Of(human, Vector3.forward, HitSource.Melee, 10f, 0f));
            yield return TestScenes.WaitUntil(() => stats.Of(human.Index).Damage > 0f, Slow, "the damage to be credited");
            Assert.AreEqual(0f, stats.Of(victim.Index).Damage, "taking damage is not dealing it");
            Knock(victim, human);
            yield return TestScenes.WaitUntil(() => stats.Of(human.Index).Knockouts == 1, Slow, "the knockout to be credited");
            Assert.AreEqual(1, stats.Of(victim.Index).Falls);
            Assert.AreEqual(0, stats.Of(human.Index).Falls);
            Assert.AreEqual(0, stats.Of(victim.Index).Knockouts);

            yield return UntilFree(human);
            human.Inventory.Set("BAT");
            Assert.IsTrue(human.Summoner.Summon("BAT"));
            yield return TestScenes.WaitUntil(() => stats.Of(human.Index).Crafted == 1, Slow, "the spelled item to be counted");
            room.Originals.First(s => s && !s.IsBroken && !s.Invulnerable).Break();
            yield return null;
            yield return null;
            Assert.AreEqual(0, stats.Lines.Sum(l => l.Broken), "a piece nobody hit is nobody's");
            Assert.AreSame(stats, MatchStats.Current, "one table for the whole match");
        }

        [UnityTest]
        public IEnumerator RoundResultsStayAsTheyAreAndTheFinalOneShowsTheMatchEndScreen()
        {
            yield return Load(HouseModes.Dibs, false);
            yield return TestScenes.WaitUntil(() => Rounds && Rounds.Phase != Phase.Lobby, Slow, "the match to start");
            QuickCountdown();
            var human = Human();
            Hud.Follow(human);
            int target = Match.Rules.RoundsToWin;
            Assert.Greater(target, 1, "Last One Standing is played over rounds");
            for (int round = 1; round <= target; round++)
            {
                int now = round;
                yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Playing && Rounds.Round == now, Slow, "round " + now);
                StopBots();
                foreach (var other in Joins.Players.Where(p => p != human)) other.Health.Eliminate();
                yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.RoundOver || Rounds.Phase == Phase.MatchOver, Slow, "round " + now + " to end");
                yield return TestScenes.WaitUntil(() => Hud.ResultShown, Slow, "the result card of round " + now);
                if (round == target) break;
                Assert.AreEqual(Phase.RoundOver, Rounds.Phase);
                Assert.IsFalse(Hud.MatchEndShown, "a round result stays the small card");
                Assert.IsNull(ResultCard.Find(GameHud.MatchEndHeadName), "the match end parts are not even built yet");
                Assert.IsTrue(Active("Spark"));
                Assert.IsTrue(Active("Players"));
                Assert.IsTrue(Active("Stats"));
                StringAssert.Contains("NEXT ROUND", Text("Next/Body/Label"));
                Hud.ConfirmResult();
            }

            Assert.AreEqual(Phase.MatchOver, Rounds.Phase);
            Assert.IsTrue(Hud.MatchEndShown, "the final result is the match end screen");
            Assert.AreEqual(MatchEnd.YouWin, Hud.MatchEndBanner, "more than two sides: the banner names the winner");
            StringAssert.StartsWith("LAST ONE STANDING", Hud.MatchEndModeLine);
            StringAssert.DoesNotContain("DIBS", Hud.MatchEndModeLine);
            Assert.AreEqual(Joins.Players.Count, Hud.MatchEndCardCount, "a card per seat");
            Assert.AreEqual("You", Hud.MatchEndCardName(0), "the winner comes first");
            Assert.AreEqual(MatchAwards.MvpTitle, Hud.MatchEndCardTitle(0));
            Assert.AreEqual(MatchEnd.WinnerTag, Hud.MatchEndCardTag(0));
            StringAssert.StartsWith(MatchAwards.WinsLabel + " " + target, Hud.MatchEndCardNumbers(0));
            for (int i = 1; i < Hud.MatchEndCardCount; i++)
            {
                Assert.AreEqual(MatchEnd.BotTag, Hud.MatchEndCardTag(i), "bots are marked and did not win");
                Assert.IsNotEmpty(Hud.MatchEndCardTitle(i), "every seat gets a title");
                StringAssert.StartsWith(MatchAwards.WinsLabel + " 0", Hud.MatchEndCardNumbers(i));
            }
            Assert.IsTrue(MatchStats.Current.Lines.Where(l => l.Bot).All(l => l.Falls == target), "every bot fell once a round");
            Assert.AreEqual(0, MatchStats.Current.Of(human.Index).Falls);
            Assert.IsFalse(MatchTally.StatsLive, "the table closes with the match");

            StringAssert.StartsWith("MATCH OVER", Text("Eyebrow"));
            Assert.IsNotEmpty(Text("Heading"));
            Assert.IsNotEmpty(Text("Players"), "the seat lines stay filled for anything that reads them");
            Assert.IsFalse(Active("Players"), "the player cards take their place");
            Assert.IsFalse(Active("Stats"));
            Assert.IsFalse(Active("Spark"));
            Assert.IsTrue(Active(GameHud.MatchEndHeadName));
            Assert.IsTrue(Active(GameHud.MatchEndCardsName));
            StringAssert.Contains("PLAY AGAIN", Text("Next/Body/Label"));
            Assert.AreEqual("Leave match", Text("Result home/Body/Label"));
            AssertReadable(ResultCard.Find(GameHud.MatchEndHeadName), GameHud.MatchEndMinFont);
            AssertReadable(ResultCard.Find(GameHud.MatchEndCardsName), GameHud.MatchEndMinFont);

            ResultCard.Find("Next").GetComponent<Button>().onClick.Invoke();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Playing && Rounds.Round == 1, Slow, "the rematch to play");
            Assert.IsFalse(Hud.ResultShown);
            Assert.IsFalse(Hud.MatchEndShown);
            Assert.IsNull(Rounds.Showcase, "a rematch has no showcase");
            Assert.IsTrue(MatchStats.Current.Lines.All(l => l.Falls == 0 && l.Score == 0), "a rematch starts a fresh table");
            StopBots();
            foreach (var other in Joins.Players.Where(p => p != human)) other.Health.Eliminate();
            yield return TestScenes.WaitUntil(() => Hud.ResultShown, Slow, "the first result of the rematch");
            Assert.AreEqual(Phase.RoundOver, Rounds.Phase);
            Assert.IsFalse(Hud.MatchEndShown, "the small card is back for round results");
            Assert.IsFalse(Active(GameHud.MatchEndHeadName));
            Assert.IsFalse(Active(GameHud.MatchEndCardsName));
            Assert.IsTrue(Active("Spark"));
            Assert.IsTrue(Active("Players"));
            Assert.IsTrue(Active("Stats"));
            Assert.LessOrEqual(((RectTransform)ResultCard).sizeDelta.x, SmallCardWidth, "and it is its old width again");
        }

        [UnityTest]
        public IEnumerator AFreeForAllWinnerIsNamedOnTheBannerWithTheModeCountOnTheCards()
        {
            yield return Load(HouseModes.FreeForAll, false);
            var director = Object.FindAnyObjectByType<FreeForAllDirector>();
            Assert.IsNotNull(director);
            director.Target = 1;
            director.CountdownSeconds = ShortCountdown;
            director.StartMatch();
            yield return TestScenes.WaitUntil(() => director.Current == HouseRoundDirector.Stage.Playing, Slow, "the match to start");
            StopBots();
            var human = Human();
            Hud.Follow(human);
            var winner = Bot;
            var loser = Joins.Players.Last(p => p.Binding is BotBinding);
            yield return TestScenes.WaitUntil(() => !loser.Health.IsInvulnerable, Slow, "spawn protection to wear off");
            Knock(loser, winner);
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.MatchOver, Slow, "the race to end");
            yield return TestScenes.WaitUntil(() => Hud.ResultShown, Slow, "the result card");

            Assert.IsTrue(Hud.MatchEndShown);
            Assert.AreEqual(winner.Name.ToUpperInvariant() + " WINS", Hud.MatchEndBanner);
            StringAssert.StartsWith("FREE-FOR-ALL", Hud.MatchEndModeLine);
            var board = Hud.ShownMatchEnd;
            Assert.IsFalse(board.TwoSides);
            Assert.IsFalse(board.Won);
            Assert.IsTrue(board.Lost, "the only human did not win");
            Assert.AreEqual(Joins.Players.Count, Hud.MatchEndCardCount);
            Assert.AreEqual(winner.Name, Hud.MatchEndCardName(0));
            Assert.AreEqual(MatchAwards.MvpTitle, Hud.MatchEndCardTitle(0));
            Assert.AreEqual(MatchEnd.WinnerTag + MatchEnd.Dot + MatchEnd.BotTag, Hud.MatchEndCardTag(0));
            StringAssert.StartsWith(MatchAwards.KnockoutsLabel + " 1", Hud.MatchEndCardNumbers(0), "the card shows the mode's own knockout count");
            int you = Enumerable.Range(0, Hud.MatchEndCardCount).Single(i => Hud.MatchEndCardName(i) == "You");
            Assert.AreEqual("", Hud.MatchEndCardTag(you), "a human who did not win carries no tag");
            Assert.AreEqual(1, MatchStats.Current.Of(winner.Index).Knockouts);
            Assert.AreEqual(1, MatchStats.Current.Of(loser.Index).Falls);
            StringAssert.StartsWith("MATCH OVER", Text("Eyebrow"));
            StringAssert.Contains(winner.Name, Text("Heading"), "the old heading is still filled");
            StringAssert.Contains(winner.Name, Text("Players"));
        }

        [UnityTest]
        public IEnumerator TwoSidesShowVictoryWithTheScoreLineAndWinnersFirst()
        {
            yield return Load(HouseModes.War, false);
            var director = Object.FindAnyObjectByType<WarDirector>();
            Assert.IsNotNull(director);
            director.StartingLives = 1;
            director.RespawnSeconds = .2f;
            director.HoldSeconds = 0f;
            director.CountdownSeconds = ShortCountdown;
            director.StartMatch();
            yield return TestScenes.WaitUntil(() => director.Current == HouseRoundDirector.Stage.Playing, Slow, "the match to start");
            StopBots();
            var you = Human();
            Hud.Follow(you);
            var them = Joins.Players.Where(p => p.Team != you.Team).ToArray();
            int mates = Joins.Players.Count(p => p.Team == you.Team);
            yield return TestScenes.WaitUntil(() => them.All(p => !p.Health.IsInvulnerable), Slow, "spawn protection to wear off");
            Knock(them[0], you);
            yield return TestScenes.WaitUntil(() => !them[0].IsKnockedOut, Slow, "the spent life to bring them back");
            yield return TestScenes.WaitUntil(() => !them[0].Health.IsInvulnerable, Slow, "spawn protection to wear off again");
            foreach (var p in them) Knock(p, you);
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.MatchOver, Slow, "the empty team to lose");
            yield return TestScenes.WaitUntil(() => Hud.ResultShown, Slow, "the result card");

            Assert.IsTrue(Hud.MatchEndShown);
            Assert.AreEqual(MatchEnd.Victory + " " + director.Score.Lives(you.Team) + MatchEnd.ScoreGap + "0", Hud.MatchEndBanner, "two sides: the score line is the lives left");
            StringAssert.StartsWith("TEAM BATTLE", Hud.MatchEndModeLine);
            var board = Hud.ShownMatchEnd;
            Assert.IsTrue(board.TwoSides);
            Assert.IsTrue(board.Won);
            Assert.AreEqual(Joins.Players.Count, Hud.MatchEndCardCount, "a card per seat, all eight");
            for (int i = 0; i < Hud.MatchEndCardCount; i++)
                Assert.AreEqual(i < mates, Hud.MatchEndCardTag(i).StartsWith(MatchEnd.WinnerTag), "the winning side comes first: card " + i);
            Assert.AreEqual("You", Hud.MatchEndCardName(0));
            Assert.AreEqual(MatchAwards.MvpTitle, Hud.MatchEndCardTitle(0));
            StringAssert.StartsWith(MatchAwards.KnockoutsLabel + " " + (them.Length + 1), Hud.MatchEndCardNumbers(0));
            Assert.AreEqual(them.Length + 1, MatchStats.Current.Of(you.Index).Knockouts, "the table agrees with the mode's count");
            Assert.AreEqual(2, MatchStats.Current.Of(them[0].Index).Falls);
            Assert.AreEqual("Your team wins!", Text("Heading"), "the old heading is untouched");

            AssertReadable(ResultCard.Find(GameHud.MatchEndHeadName), GameHud.MatchEndMinFont);
            AssertReadable(ResultCard.Find(GameHud.MatchEndCardsName), GameHud.MatchEndMinFont);
            Canvas.ForceUpdateCanvases();
            var safe = SafeArea();
            var card = OnScreen((RectTransform)ResultCard);
            Assert.That(card.xMin, Is.GreaterThanOrEqualTo(safe.xMin - Slack));
            Assert.That(card.xMax, Is.LessThanOrEqualTo(safe.xMax + Slack));
            if (safe.width >= safe.height)
            {
                Assert.That(card.yMin, Is.GreaterThanOrEqualTo(safe.yMin - Slack), "eight cards still fit a wide screen");
                Assert.That(card.yMax, Is.LessThanOrEqualTo(safe.yMax + Slack));
            }
            var boxes = Enumerable.Range(0, Hud.MatchEndCardCount).Select(i => OnScreen(Hud.MatchEndCardRect(i))).ToArray();
            for (int i = 0; i < boxes.Length; i++)
            {
                Assert.That(boxes[i].xMin, Is.GreaterThanOrEqualTo(card.xMin - Slack), "card " + i + " stays on the result card");
                Assert.That(boxes[i].xMax, Is.LessThanOrEqualTo(card.xMax + Slack), "card " + i);
                Assert.That(boxes[i].yMin, Is.GreaterThanOrEqualTo(card.yMin - Slack), "card " + i);
                for (int j = 0; j < i; j++)
                {
                    bool apart = boxes[i].xMin >= boxes[j].xMax - Slack || boxes[j].xMin >= boxes[i].xMax - Slack
                        || boxes[i].yMin >= boxes[j].yMax - Slack || boxes[j].yMin >= boxes[i].yMax - Slack;
                    Assert.IsTrue(apart, $"cards {j} and {i} do not overlap");
                }
            }
            var next = ResultCard.Find("Next").GetComponent<Button>();
            var home = ResultCard.Find("Result home").GetComponent<Button>();
            Assert.IsTrue(next && next.isActiveAndEnabled, "Next is still there by name");
            Assert.IsTrue(home && home.isActiveAndEnabled, "so is the way home");
            Assert.That(OnScreen((RectTransform)next.transform).yMax, Is.LessThanOrEqualTo(boxes.Min(b => b.yMin) + Slack), "the buttons sit under the cards");
        }
    }
}
