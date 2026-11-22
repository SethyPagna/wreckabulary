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
        const float Slack = 1f, SmallCardWidth = 575.5f, OldSeatMax = 170f, Tolerance = .01f, BigCoinFont = 30f;
        const int ReleaseFrames = 5, PaidCoins = 25, PaidScore = 140;

        static readonly string[] LineUps = { HouseModes.Dibs, HouseModes.Duos, HouseModes.FreeForAll, HouseModes.War };
        static readonly string[] BothFlows = { HouseModes.Dibs, HouseModes.FreeForAll };
        static readonly string[] Crews = { HouseModes.MovingOut, HouseModes.Escape, HouseModes.MovingDay };
        static readonly string[] Wipeable = { HouseModes.MovingOut, HouseModes.Escape };
        const string CrewMap = "flat", FurnishMap = "pinwheel";
        const string CrewShowcase = "Showcase", CrewCountdown = "Countdown", CrewPlaying = "Playing", CrewFailed = "Failed";
        const float FurnishFirstZ = -3f, FurnishStepZ = 1.8f, FurnishDrop = .3f, VanDrop = .3f, VanSpread = .8f, AlmostNoTime = .2f;

        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RoundManager.WarmupRequested = false;
            RoundManager.FinishBeatsRequested = false;
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
        static GameObject HudPart(string path) => Hud.UiCanvas.transform.Find("Safe HUD/" + path).gameObject;
        static bool BagAndHealthShown => HudPart("Letter bag").activeSelf && (HudPart("Vitals").activeSelf || Hud.HealthOnBar);
        static bool BagAndHealthCleared => !HudPart("Letter bag").activeSelf && !HudPart("Vitals").activeSelf && SmashableLabel.Covered;

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

        static string Columns(string numbers) => System.Text.RegularExpressions.Regex.Replace(numbers, @"\d+", "#");

        static void AssertRoundRow(PlayerController winner, int wins)
        {
            Assert.IsTrue(Hud.RoundRowShown, "the round card lines up the seats");
            Assert.AreEqual(Joins.Players.Count, Hud.RoundRowCount, "one portrait per seat");
            Assert.IsFalse(Active("Players"), "no text lines under the portraits");
            Assert.IsFalse(Active("Stats"), "and no small numbers");
            for (int i = 0; i < Hud.RoundRowCount; i++)
            {
                string name = Hud.RoundRowSeatName(i);
                var seat = Joins.Players.First(p => p.Name == name);
                bool won = seat == winner;
                Assert.AreEqual(won, Hud.RoundRowRaised(i), name + " is raised only when they won the round");
                Assert.AreEqual(seat.Binding is BotBinding ? SeatTags.BotTag : SeatTags.YouTag, Hud.RoundRowSeatTag(i), name);
                Assert.AreEqual(won ? wins : 0, Hud.RoundRowWins(i), name + " has a pip per round won");
                if (won) Assert.Greater(OnScreen(Hud.RoundRowRect(i)).yMax, OnScreen(Hud.RoundRowRect((i + 1) % Hud.RoundRowCount)).yMax, "the winner stands higher");
                if (won) AssertSquare(Hud.RoundRowRect(i), "Portrait/Winner", "the winner's gold rim");
                var pips = Hud.RoundRowRect(i).Find("Pips");
                for (int pip = 0; pip < pips.childCount; pip++)
                    if (pips.GetChild(pip).GetComponent<Image>()) AssertSquare(pips, pips.GetChild(pip).name, name + "'s win pips");
            }
            AssertSquarePortraits(Hud.RoundRowRect, Hud.RoundRowCount, "Portrait", "round portrait");
            AssertReadable(ResultCard.Find(GameHud.RoundRowName), GameHud.MatchEndMinFont);
        }

        static void AssertOneColumnSet()
        {
            string first = Columns(Hud.MatchEndCardNumbers(0));
            for (int i = 1; i < Hud.MatchEndCardCount; i++)
                Assert.AreEqual(first, Columns(Hud.MatchEndCardNumbers(i)), "every card has the same columns in the same order");
            for (int i = 0; i < Hud.MatchEndCardCount; i++)
                Assert.AreNotEqual(MatchAwards.DefaultTitle, Hud.MatchEndCardTitle(i), "no Good Sport over empty numbers");
        }

        static void AssertResultSaysItOnce()
        {
            Assert.IsFalse(Active("Eyebrow"), "the mode line and the banner say who won");
            Assert.IsFalse(Active("Heading"));
            Assert.IsFalse(Active("Flavour"));
        }

        static IEnumerator AssertPaidMatchEndShowsTheCoinChip(MatchEndBoard board)
        {
            Assert.IsNotNull(board, "the match end screen is up");
            Hud.ShowResult(new HudResult { Round = Rounds.Round, Final = true, Won = board.Won, Board = board,
                Reward = new MatchRecord { Coins = PaidCoins, Score = PaidScore } }, Rounds.StartMatch);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.AreSame(board, Hud.ShownMatchEnd, "the same match end screen, now paying a person on the couch");
            AssertResultSaysItOnce();
            Assert.IsTrue(Active("Reward"), "the coins show at the end");
            Assert.AreEqual("+" + PaidCoins, Text("Reward/Coins"));
            var chip = (RectTransform)ResultCard.Find("Reward/Chip");
            Assert.IsNotNull(chip, "the coins sit on a chip");
            Assert.IsTrue(chip.gameObject.activeInHierarchy, "the chip is drawn");
            Assert.Greater(chip.sizeDelta.x, ((RectTransform)ResultCard.Find("Reward/Coins")).rect.width, "the chip holds the coin count");
            Assert.GreaterOrEqual(ResultCard.Find("Reward/Coins").GetComponent<TMP_Text>().fontSize, BigCoinFont, "a big coin number");
            var card = OnScreen((RectTransform)ResultCard);
            var onCard = OnScreen(chip);
            Assert.That(onCard.xMin, Is.GreaterThanOrEqualTo(card.xMin - Slack), "the chip sits on the card");
            Assert.That(onCard.xMax, Is.LessThanOrEqualTo(card.xMax + Slack), "the chip sits on the card");
            Assert.That(onCard.yMin, Is.GreaterThanOrEqualTo(card.yMin - Slack), "the chip sits on the card");
            Assert.That(onCard.yMax, Is.LessThanOrEqualTo(card.yMax + Slack), "the chip sits on the card");
            StringAssert.Contains("PLAY AGAIN", Text("Next/Body/Label"));
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

        static void AssertSquare(Transform root, string path, string what)
        {
            var part = root ? root.Find(path) : null;
            Assert.IsNotNull(part, what + ": " + path);
            var image = part.GetComponent<Image>();
            Assert.IsNotNull(image, what + ": " + path + " is drawn");
            Assert.IsTrue(GameHud.Squared(image), what + ": " + path + " is square like the seat strip, not rounded");
        }

        static void AssertSquarePortraits(System.Func<int, RectTransform> seat, int count, string frame, string what)
        {
            for (int i = 0; i < count; i++)
            {
                AssertSquare(seat(i), frame, what + " " + i);
                AssertSquare(seat(i), frame + "/Backing", what + " " + i);
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
            if (players.Count <= 4) Assert.Greater(Hud.ShowcaseSeatSize, OldSeatMax, "the line-up cards are big");
            for (int i = 0; i < shown.Length; i++)
            {
                bool bot = shown[i].Binding is BotBinding;
                Assert.AreEqual(shown[i].Name, Hud.ShowcaseSeatName(i), "one naming rule: the seat name");
                Assert.AreEqual(bot ? MatchShowcase.BotTag : SeatTags.YouTag, Hud.ShowcaseSeatTag(i), "bots are marked, the one person on the couch is YOU: " + shown[i].Name);
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
            yield return TestScenes.WaitUntil(() => Enumerable.Range(0, Hud.ShowcaseSeatCount).All(Hud.ShowcaseSeatLanded), Slow, "the cards to slide in from their sides");
            Assert.IsTrue(Hud.LocalPlayer, "the couch player has a HUD");
            Assert.IsTrue(Hud.FullCardShown);
            yield return TestScenes.WaitUntil(() => BagAndHealthCleared, Slow, "the line-up to clear the letter bag, hands, health and furniture names");
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
            AssertSquarePortraits(Hud.ShowcaseSeatRect, shown.Length, "Body/Frame", "line-up seat");

            EndShowcase();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Countdown, Slow, "the usual countdown after the showcase in " + mode);
            Assert.AreEqual(1, Rounds.Round);
            Assert.IsNull(Rounds.Showcase);
            Assert.IsFalse(Hud.ShowcaseShown);
            Assert.AreEqual(0, Hud.ShowcaseSeatCount);
            yield return TestScenes.WaitUntil(() => BagAndHealthShown && !SmashableLabel.Covered, Slow, "the bag, hands and health back after the line-up");
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
                yield return TestScenes.WaitUntil(() => BagAndHealthShown && !SmashableLabel.Covered, Slow, "the bag, hands and health in round " + now);
                foreach (var other in Joins.Players.Where(p => p != human)) other.Health.Eliminate();
                yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.RoundOver || Rounds.Phase == Phase.MatchOver, Slow, "round " + now + " to end");
                Assert.IsFalse(CameraRig.Instance && CameraRig.Instance.Pushing, "a direct start keeps the usual camera shot");
                yield return TestScenes.WaitUntil(() => Hud.ResultShown, Slow, "the result card of round " + now);
                yield return TestScenes.WaitUntil(() => BagAndHealthCleared, Slow, "the result card to clear the bag, hands, health and furniture names");
                if (round == target) break;
                Assert.AreEqual(Phase.RoundOver, Rounds.Phase);
                Assert.IsFalse(Hud.MatchEndShown, "a round result stays the small card");
                Assert.IsNull(ResultCard.Find(GameHud.MatchEndHeadName), "the match end parts are not even built yet");
                Assert.IsTrue(Active("Spark"));
                AssertRoundRow(human, now);
                Assert.IsNotEmpty(Text("Players"), "the seat lines stay filled for anything that reads them");
                Assert.AreEqual(1f, Time.timeScale, "a direct start never slows time");
                StringAssert.Contains("NEXT ROUND", Text("Next/Body/Label"));
                Hud.ConfirmResult();
            }

            Assert.AreEqual(Phase.MatchOver, Rounds.Phase);
            Assert.IsTrue(Hud.MatchEndShown, "the final result is the match end screen");
            Assert.AreEqual(MatchEnd.YouWin, Hud.MatchEndBanner, "more than two sides: the banner names the winner");
            StringAssert.StartsWith("LAST ONE STANDING", Hud.MatchEndModeLine);
            StringAssert.DoesNotContain("DIBS", Hud.MatchEndModeLine);
            Assert.AreEqual(Joins.Players.Count, Hud.MatchEndCardCount, "a card per seat");
            Assert.AreEqual(human.Name, Hud.MatchEndCardName(0), "the winner comes first, by seat name");
            Assert.AreEqual(MatchAwards.MvpTitle, Hud.MatchEndCardTitle(0));
            Assert.AreEqual(MatchEnd.WinnerTag + MatchEnd.Dot + SeatTags.YouTag, Hud.MatchEndCardTag(0));
            StringAssert.StartsWith(MatchAwards.WinsLabel + " " + target, Hud.MatchEndCardNumbers(0));
            for (int i = 1; i < Hud.MatchEndCardCount; i++)
            {
                Assert.AreEqual(MatchEnd.BotTag, Hud.MatchEndCardTag(i), "bots are marked and did not win");
                StringAssert.StartsWith(MatchAwards.WinsLabel + " 0", Hud.MatchEndCardNumbers(i));
            }
            AssertOneColumnSet();
            AssertResultSaysItOnce();
            Assert.IsFalse(Active("Reward"), "a scripted seat is nobody's career, so nothing is paid and no empty chip shows");
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

            yield return AssertPaidMatchEndShowsTheCoinChip(Hud.ShownMatchEnd);

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
            Assert.IsTrue(Active("Eyebrow"), "the round card keeps its words");
            Assert.IsTrue(Active("Heading"));
            AssertRoundRow(human, 1);
            Assert.LessOrEqual(((RectTransform)ResultCard).sizeDelta.x, SmallCardWidth, "and it is its old width again");
        }

        [UnityTest]
        public IEnumerator ALobbyMatchSlowsTheLastKnockoutBrieflyAndGivesTimeBack()
        {
            RoundManager.FinishBeatsRequested = true;
            yield return Load(HouseModes.Dibs, false);
            yield return TestScenes.WaitUntil(() => Rounds && Rounds.Phase != Phase.Lobby, Slow, "the match to start");
            Assert.IsTrue(Rounds.FinishBeats, "a lobby launch asks for the finishing beat");
            Assert.IsFalse(RoundManager.FinishBeatsRequested, "the request is used once");
            QuickCountdown();
            var human = Human();
            Hud.Follow(human);
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Playing, Slow, "round one");
            StopBots();
            foreach (var other in Joins.Players.Where(p => p != human)) other.Health.Eliminate();
            yield return TestScenes.WaitUntil(() => Rounds.FinishBeatPlaying, Slow, "the finishing beat");
            Assert.AreEqual(RoundManager.BeatScale, Time.timeScale, Tolerance, "a third of the speed for a moment");
            Assert.IsFalse(Hud.ResultShown, "the card waits for the beat");
            var rig = CameraRig.Instance;
            Assert.IsTrue(rig, "the house camera");
            Assert.IsTrue(rig.Pushing, "the camera leans in on the winner for the beat");
            Assert.Less(Vector3.Distance(rig.PushedToward, human.transform.position), Slack, "toward the winner");
            Hud.TogglePause();
            yield return TestScenes.WaitUntil(() => Hud.Paused && Time.timeScale == 0f, Slow, "the pause menu");
            yield return new WaitForSecondsRealtime(RoundManager.BeatSeconds + PausedSeconds);
            Assert.IsTrue(Rounds.FinishBeatPlaying, "the beat waits while paused");
            Assert.AreEqual(0f, Time.timeScale, "pause wins over the beat");
            Hud.TogglePause();
            yield return TestScenes.WaitUntil(() => !Rounds.FinishBeatPlaying, Slow, "the beat to end");
            Assert.AreEqual(1f, Time.timeScale, "time comes back exactly");
            yield return TestScenes.WaitUntil(() => !rig.Pushing, Slow, "the camera to settle back");
            Assert.AreEqual(0f, rig.PushAmount, "the usual shot again");
            yield return TestScenes.WaitUntil(() => Hud.ResultShown, Slow, "the round card after the beat");
            Assert.AreEqual(1f, Time.timeScale);
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
            int you = Enumerable.Range(0, Hud.MatchEndCardCount).Single(i => Hud.MatchEndCardName(i) == human.Name);
            Assert.AreEqual(SeatTags.YouTag, Hud.MatchEndCardTag(you), "a human who did not win carries only the YOU tag");
            AssertOneColumnSet();
            AssertResultSaysItOnce();
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
            Assert.AreEqual(you.Name, Hud.MatchEndCardName(0));
            Assert.AreEqual(MatchAwards.MvpTitle, Hud.MatchEndCardTitle(0));
            StringAssert.StartsWith(MatchAwards.KnockoutsLabel + " " + (them.Length + 1), Hud.MatchEndCardNumbers(0));
            AssertOneColumnSet();
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
            AssertSquarePortraits(Hud.MatchEndCardRect, Hud.MatchEndCardCount, "Portrait", "end card");
            var next = ResultCard.Find("Next").GetComponent<Button>();
            var home = ResultCard.Find("Result home").GetComponent<Button>();
            Assert.IsTrue(next && next.isActiveAndEnabled, "Next is still there by name");
            Assert.IsTrue(home && home.isActiveAndEnabled, "so is the way home");
            Assert.That(OnScreen((RectTransform)next.transform).yMax, Is.LessThanOrEqualTo(boxes.Min(b => b.yMin) + Slack), "the buttons sit under the cards");
        }

        static MovingOutDirector Van => Object.FindAnyObjectByType<MovingOutDirector>();
        static EscapeDirector Escape => Object.FindAnyObjectByType<EscapeDirector>();
        static MovingDayDirector Furnish => Object.FindAnyObjectByType<MovingDayDirector>();

        static string CrewState(string mode)
        {
            if (mode == HouseModes.MovingOut) return Van ? Van.Current.ToString() : "";
            if (mode == HouseModes.Escape) return Escape ? Escape.Current.ToString() : "";
            return Furnish ? Furnish.Current.ToString() : "";
        }

        static string CrewPlace(string mode) =>
            mode == HouseModes.MovingDay ? Furnish.CurrentLevel.name : Object.FindAnyObjectByType<RoomBuilder>().Layout.Name;

        static IEnumerator LoadCrew(string mode, bool warmup, int humans, string map = CrewMap)
        {
            Session.SelectMap(map);
            Match.ModeOverride = mode;
            for (int i = 0; i < humans; i++) Session.Remember(new ScriptedBinding());
            RoundManager.WarmupRequested = warmup;
            yield return TestScenes.Load(Session.SceneForMode(mode));
            yield return TestScenes.ExploreHouse();
        }

        static IEnumerator LoadCrewShowcase(string mode, int humans)
        {
            yield return LoadCrew(mode, true, humans);
            yield return TestScenes.WaitUntil(() => Rounds && Rounds.Phase == Phase.Warmup && Rounds.Warmup != null, Slow, "the warm-up to start in " + mode);
            Rounds.Warmup.State.Length = LongClock;
            Hud.Follow(Human());
            EndWarmup();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Showcase && Rounds.Showcase != null, Slow, "the crew line-up in " + mode);
            Rounds.Showcase.State.Length = LongClock;
            yield return TestScenes.WaitUntil(() => CrewState(mode) == CrewShowcase, Slow, mode + " to show its line-up state");
        }

        static IEnumerator UntilCrewPlaying(string mode) =>
            TestScenes.WaitUntil(() => CrewState(mode) == CrewPlaying, Slow, mode + " to start");

        static void AssertOldCoopCardIsStillThere(string heading)
        {
            Assert.AreEqual(heading, Text("Heading"), "the old heading is untouched");
            Assert.AreEqual("HOUSE PARTY COMPLETE", Text("Eyebrow"));
            Assert.IsNotEmpty(Text("Flavour"));
            foreach (string part in new[] { "Spark", "Detail", "Players", "Stats", "Reward", "Next", "Result home" })
                Assert.IsNotNull(ResultCard.Find(part), part + " is still on the card by name");
            StringAssert.Contains("PLAY AGAIN", Text("Next/Body/Label"));
            Assert.AreEqual("Leave match", Text("Result home/Body/Label"));
            var next = ResultCard.Find("Next").GetComponent<Button>();
            var home = ResultCard.Find("Result home").GetComponent<Button>();
            Assert.IsTrue(next && next.isActiveAndEnabled, "Next is still there by name");
            Assert.IsTrue(home && home.isActiveAndEnabled, "so is the way home");
        }

        static void AssertCrewBoardFits()
        {
            AssertResultSaysItOnce();
            Assert.IsTrue(Active(GameHud.MatchEndHeadName));
            Assert.IsTrue(Active(GameHud.MatchEndCardsName));
            Assert.IsFalse(Active("Stats"), "the player cards take the place of the shared numbers");
            AssertReadable(ResultCard.Find(GameHud.MatchEndHeadName), GameHud.MatchEndMinFont);
            AssertReadable(ResultCard.Find(GameHud.MatchEndCardsName), GameHud.MatchEndMinFont);
            Canvas.ForceUpdateCanvases();
            var safe = SafeArea();
            var card = OnScreen((RectTransform)ResultCard);
            Assert.That(card.xMin, Is.GreaterThanOrEqualTo(safe.xMin - Slack));
            Assert.That(card.xMax, Is.LessThanOrEqualTo(safe.xMax + Slack));
            if (safe.width >= safe.height)
            {
                Assert.That(card.yMin, Is.GreaterThanOrEqualTo(safe.yMin - Slack), "the card fits a wide screen");
                Assert.That(card.yMax, Is.LessThanOrEqualTo(safe.yMax + Slack));
            }
            var boxes = Enumerable.Range(0, Hud.MatchEndCardCount).Select(i => OnScreen(Hud.MatchEndCardRect(i))).ToArray();
            for (int i = 0; i < boxes.Length; i++)
            {
                Assert.That(boxes[i].xMin, Is.GreaterThanOrEqualTo(card.xMin - Slack), "card " + i + " stays on the result card");
                Assert.That(boxes[i].xMax, Is.LessThanOrEqualTo(card.xMax + Slack), "card " + i);
                for (int j = 0; j < i; j++)
                    Assert.IsTrue(boxes[i].xMin >= boxes[j].xMax - Slack || boxes[j].xMin >= boxes[i].xMax - Slack
                        || boxes[i].yMin >= boxes[j].yMax - Slack || boxes[j].yMin >= boxes[i].yMax - Slack, $"cards {j} and {i} do not overlap");
            }
            AssertSquarePortraits(Hud.MatchEndCardRect, Hud.MatchEndCardCount, "Portrait", "crew end card");
            var next = (RectTransform)ResultCard.Find("Next");
            Assert.That(OnScreen(next).yMax, Is.LessThanOrEqualTo(boxes.Min(b => b.yMin) + Slack), "the buttons sit under the cards");
        }

        [UnityTest]
        public IEnumerator ACrewLinesUpAsOneTeamBeforeACoopRun([ValueSource(nameof(Crews))] string mode)
        {
            yield return LoadCrewShowcase(mode, 2);
            var players = Joins.Players;
            Assert.AreEqual(2, players.Count, "a crew has no bots");
            Assert.IsNull(Rounds.Warmup, "the warm-up is closed");
            Assert.IsFalse(LetterInventory.FreeSpelling);
            Assert.IsTrue(Rounds.isActiveAndEnabled, "the round manager holds the phase during the line-up");
            Assert.IsTrue(players.All(p => p.Frozen), "everyone stands still for the line-up");
            Assert.IsTrue(players.All(p => !p.IsKnockedOut), "everyone is on their feet");
            Assert.IsFalse(Joins.AllowJoining);
            Assert.IsFalse(Hud.ResultShown);
            Assert.IsFalse(Hud.CountdownShown);
            Assert.IsFalse(MatchTally.StatsLive, "nothing is tallied before the run");

            Assert.IsTrue(Hud.ShowcaseShown);
            Assert.AreEqual(2, Hud.ShowcaseSeatCount, "one card per crewmate");
            Assert.AreEqual(1, Hud.ShowcaseRowCount, "one crew, one line");
            Assert.AreEqual(MatchShowcase.YourCrew, Hud.ShowcaseRowTitle(0));
            Assert.IsNull(Hud.ShowcaseRoot.Find("Line-up/Versus 1"), "a crew faces nobody");
            StringAssert.StartsWith(HouseModes.Name(mode).ToUpperInvariant(), Hud.ShowcaseModeText);
            StringAssert.IsMatch(@"^MATCH STARTS IN \d+$", Hud.ShowcaseCountText);
            Assert.That(Count(), Is.InRange(1, (int)LongClock));
            Assert.AreEqual(MatchShowcase.SkipPrompt(ControlHints.For(Human().Binding, PlayerControl.Start)), Hud.ShowcaseSkipText);

            var shown = Enumerable.Range(0, Hud.ShowcaseSeatCount).Select(Hud.ShowcaseSeatPlayer).ToArray();
            CollectionAssert.AreEquivalent(players.ToArray(), shown, "every crewmate is shown once");
            for (int i = 0; i < shown.Length; i++)
            {
                Assert.AreEqual(shown[i].Name, Hud.ShowcaseSeatName(i), "a shared couch keeps seat names");
                Assert.AreEqual("", Hud.ShowcaseSeatTag(i));
                Assert.AreEqual(0, Hud.ShowcaseSeatRow(i));
                Assert.AreEqual(GameHud.ShowcaseSideColour(shown[i], players, false), Hud.ShowcaseSeatColour(i), "each crewmate keeps their own colour");
            }

            Assert.IsTrue(Hud.ShowcaseRoot.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "nothing on the line-up takes a click");
            Assert.IsEmpty(Hud.ShowcaseRoot.GetComponentsInChildren<Selectable>(true), "nothing on the line-up takes focus");
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
                if (i > 0) Assert.That(box.xMin, Is.GreaterThanOrEqualTo(OnScreen(Hud.ShowcaseSeatRect(i - 1)).xMax - Slack), "crewmates stand side by side");
            }
            AssertSquarePortraits(Hud.ShowcaseSeatRect, shown.Length, "Body/Frame", "crew line-up seat");

            EndShowcase();
            yield return TestScenes.WaitUntil(() => CrewState(mode) == CrewCountdown || CrewState(mode) == CrewPlaying, Slow, "the usual countdown after the line-up in " + mode);
            Assert.IsNull(Rounds.Showcase);
            Assert.IsFalse(Hud.ShowcaseShown);
            Assert.AreEqual(0, Hud.ShowcaseSeatCount);
            Assert.AreEqual(Phase.Lobby, Rounds.Phase, "the round manager steps back");
            Assert.IsFalse(Rounds.enabled);
            Assert.IsTrue(players.All(p => p.Health.IsAlive && Mathf.Approximately(p.Health.Current, p.Health.Max)));
            yield return UntilCrewPlaying(mode);
            Assert.IsTrue(MatchTally.StatsLive);
            Assert.IsFalse(Hud.ShowcaseShown);
        }

        [UnityTest]
        public IEnumerator EveryCrewmateHoldingStartSkipsTheCrewLineUp()
        {
            yield return LoadCrewShowcase(HouseModes.Escape, 2);
            var votes = Rounds.Showcase.State;
            Assert.AreEqual(2, votes.Voters);
            for (int i = 0; i < ReleaseFrames; i++) yield return null;
            float deadline = Time.realtimeSinceStartup + Slow;
            while (Rounds.Phase == Phase.Showcase && Time.realtimeSinceStartup < deadline)
            {
                HoldStart(Human(0));
                HoldStart(Human(1));
                yield return null;
            }
            Assert.AreNotEqual(Phase.Showcase, Rounds.Phase, "everyone holding Start skips the line-up");
            Assert.IsTrue(votes.AllReady);
            Assert.Greater(votes.TimeLeft, 0f, "it ended on the votes, not the count");
            Assert.IsFalse(Hud.Paused, "Start never opened the pause menu");
            yield return TestScenes.WaitUntil(() => CrewState(HouseModes.Escape) == CrewCountdown || CrewState(HouseModes.Escape) == CrewPlaying, Slow, "the countdown after the skip");
            Assert.IsFalse(Hud.ShowcaseShown);
        }

        [UnityTest]
        public IEnumerator ACoopWarmupWithALineUpTimeOfZeroGoesStraightToTheRun()
        {
            yield return LoadCrew(HouseModes.MovingOut, true, 1);
            yield return TestScenes.WaitUntil(() => Rounds && Rounds.Phase == Phase.Warmup && Rounds.Warmup != null, Slow, "the warm-up to start");
            Rounds.ShowcaseTime = 0f;
            EndWarmup();
            yield return TestScenes.WaitUntil(() => CrewState(HouseModes.MovingOut) == CrewCountdown || CrewState(HouseModes.MovingOut) == CrewPlaying, Slow, "the countdown");
            Assert.IsNull(Rounds.Showcase);
            Assert.IsFalse(Hud.ShowcaseShown);
            Assert.IsNull(Hud.UiCanvas.transform.Find("Safe HUD/" + GameHud.ShowcaseRootName), "the line-up is not even built");
        }

        [UnityTest]
        public IEnumerator ACoopRunEndsWithItsBannerAndACardPerPlayer()
        {
            yield return LoadCrew(HouseModes.MovingOut, false, 2);
            var director = Van;
            yield return UntilCrewPlaying(HouseModes.MovingOut);
            PlayerController first = Human(0), second = Human(1);
            Hud.Follow(first);
            Assert.IsNull(Rounds.Showcase, "a direct start has no line-up");
            Assert.Greater(director.KeepsakeCount, 0);
            yield return TestScenes.WaitUntil(() => MatchStats.Current.Lines.Count == 2, Slow, "both crewmates on the table");
            foreach (var keepsake in Object.FindObjectsByType<Keepsake>(FindObjectsSortMode.None))
            {
                keepsake.Carrier = second.Index;
                keepsake.GetComponent<Rigidbody>().position = director.ExtractionPoint + Vector3.up * VanDrop;
            }
            yield return TestScenes.WaitUntil(() => director.PackedCount == director.KeepsakeCount, Slow, "the keepsakes to be packed");
            Assert.AreEqual(director.KeepsakeCount, MatchStats.Current.Of(second.Index).Score, "whoever carried a keepsake last is credited");
            Assert.AreEqual(0, MatchStats.Current.Of(first.Index).Score);
            first.Respawn(director.ExtractionPoint + Vector3.left * VanSpread);
            second.Respawn(director.ExtractionPoint + Vector3.right * VanSpread);
            yield return TestScenes.WaitUntil(() => director.Current == MovingOutDirector.State.Complete, Slow, "the crew to reach the van");

            Assert.IsTrue(Hud.ResultShown, "the result card comes straight away, as before");
            Assert.IsTrue(Hud.MatchEndShown, "and now carries the banner and the player cards");
            Assert.AreEqual(CrewEnd.Packed, Hud.MatchEndBanner);
            Assert.AreEqual(MatchEnd.ModeLine(HouseModes.MovingOut, CrewPlace(HouseModes.MovingOut)), Hud.MatchEndModeLine);
            StringAssert.StartsWith("PACK THE VAN", Hud.MatchEndModeLine);
            var board = Hud.ShownMatchEnd;
            Assert.IsTrue(board.Crew);
            Assert.IsTrue(board.Won);
            Assert.IsFalse(board.TwoSides);
            Assert.AreEqual(2, Hud.MatchEndCardCount, "a card per player");
            Assert.AreEqual(second.Name, Hud.MatchEndCardName(0), "the crewmate who did the most comes first");
            Assert.AreEqual(MatchAwards.MvpTitle, Hud.MatchEndCardTitle(0));
            StringAssert.StartsWith(MatchAwards.PackedLabel + " " + director.KeepsakeCount, Hud.MatchEndCardNumbers(0));
            Assert.AreEqual(first.Name, Hud.MatchEndCardName(1));
            StringAssert.StartsWith(MatchAwards.PackedLabel + " 0", Hud.MatchEndCardNumbers(1));
            for (int i = 0; i < Hud.MatchEndCardCount; i++)
            {
                Assert.AreEqual("", Hud.MatchEndCardTag(i), "a crew has no winner tag and no bots");
                Assert.AreNotEqual(MatchAwards.DefaultTitle, Hud.MatchEndCardTitle(i), "a title only with a number behind it");
                StringAssert.DoesNotContain(MatchAwards.KnockoutsLabel + " ", Hud.MatchEndCardNumbers(i));
                StringAssert.DoesNotContain(MatchAwards.DamageLabel, Hud.MatchEndCardNumbers(i));
            }
            Assert.IsFalse(MatchTally.StatsLive, "the table closes with the run");
            AssertOldCoopCardIsStillThere("All packed. Everybody aboard!");
            AssertCrewBoardFits();

            ResultCard.Find("Next").GetComponent<Button>().onClick.Invoke();
            yield return TestScenes.WaitUntil(() => director.Current == MovingOutDirector.State.Countdown, Slow, "play again");
            Assert.IsFalse(Hud.ResultShown);
            Assert.IsFalse(Hud.MatchEndShown);
            Assert.AreEqual(0, director.PackedCount);
            Assert.IsNull(Rounds.Warmup, "playing again has no warm-up");
            Assert.IsNull(Rounds.Showcase);
            Assert.AreEqual(0, MatchStats.Current.Lines.Sum(l => l.Score), "a new run starts a fresh table");
        }

        [UnityTest]
        public IEnumerator ACrewThatGoesDownIsToldSo([ValueSource(nameof(Wipeable))] string mode)
        {
            yield return LoadCrew(mode, false, 2);
            yield return UntilCrewPlaying(mode);
            Hud.Follow(Human());
            yield return TestScenes.WaitUntil(() => MatchStats.Current.Lines.Count == 2, Slow, "both crewmates on the table");
            foreach (var p in Joins.Players.ToArray()) p.Health.Eliminate();
            yield return TestScenes.WaitUntil(() => CrewState(mode) == CrewFailed, Slow, "the run to fail");
            Assert.IsTrue(Hud.ResultShown);
            Assert.IsTrue(Hud.MatchEndShown);
            Assert.AreEqual(CrewEnd.Wiped, Hud.MatchEndBanner);
            Assert.AreEqual(MatchEnd.ModeLine(mode, CrewPlace(mode)), Hud.MatchEndModeLine);
            Assert.IsTrue(Hud.ShownMatchEnd.Lost);
            Assert.IsFalse(Hud.ShownMatchEnd.Won);
            Assert.AreEqual(2, Hud.MatchEndCardCount);
            for (int i = 0; i < Hud.MatchEndCardCount; i++)
            {
                Assert.AreEqual("", Hud.MatchEndCardTag(i));
                Assert.AreEqual("", Hud.MatchEndCardTitle(i), "nobody did anything worth a title");
                StringAssert.StartsWith(MatchAwards.Label(MatchStatKind.Score, mode) + " 0", Hud.MatchEndCardNumbers(i));
            }
            AssertOldCoopCardIsStillThere(mode == HouseModes.Escape ? EscapeText.Wiped : "The Movers got your crew.");
            AssertCrewBoardFits();
        }

        [UnityTest]
        public IEnumerator FurnishTheHouseShowsItsBannerWhenTheTruckLeavesAndWhenTheHouseIsDone()
        {
            string savedStars = PlayerPrefs.HasKey(Session.StarsKey) ? PlayerPrefs.GetString(Session.StarsKey) : null;
            try
            {
                yield return LoadCrew(HouseModes.MovingDay, false, 1, FurnishMap);
                var director = Furnish;
                yield return UntilCrewPlaying(HouseModes.MovingDay);
                Hud.Follow(Human());
                Assert.IsFalse(Rounds, "a direct start of Furnish the House has no round manager");
                yield return TestScenes.WaitUntil(() => MatchStats.Current.Lines.Count == 1, Slow, "the player on the table");
                director.TimeLeft = AlmostNoTime;
                yield return TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.OutOfTime, Slow, "out of time");
                Assert.IsTrue(Hud.ResultShown, "the result card comes straight away, as before");
                Assert.IsTrue(Hud.MatchEndShown);
                Assert.AreEqual(CrewEnd.TimeUp, Hud.MatchEndBanner);
                Assert.AreEqual(MatchEnd.ModeLine(HouseModes.MovingDay, director.CurrentLevel.name), Hud.MatchEndModeLine);
                StringAssert.StartsWith("FURNISH THE HOUSE", Hud.MatchEndModeLine);
                Assert.IsTrue(Hud.ShownMatchEnd.Lost);
                Assert.AreEqual(1, Hud.MatchEndCardCount);
                Assert.AreEqual(Human().Name, Hud.MatchEndCardName(0), "one naming rule: the seat name");
                Assert.AreEqual(SeatTags.YouTag, Hud.MatchEndCardTag(0), "and the one person on the couch is tagged YOU");
                StringAssert.StartsWith(MatchAwards.PlacedLabel + " 0", Hud.MatchEndCardNumbers(0));
                AssertOldCoopCardIsStillThere("The truck is leaving. Try a faster route!");
                Assert.IsFalse(Active("Detail"), "no stars line when the truck left");
                AssertCrewBoardFits();

                ResultCard.Find("Next").GetComponent<Button>().onClick.Invoke();
                yield return TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.Countdown, Slow, "retry");
                Assert.IsFalse(Hud.MatchEndShown);
                yield return UntilCrewPlaying(HouseModes.MovingDay);
                float z = FurnishFirstZ;
                foreach (var item in director.CurrentLevel.items)
                {
                    FurnitureCatalog.Spawn(item.word, director.RoomNamed(item.room).Centre + new Vector3(0f, FurnishDrop, z), 0f, World.Transient);
                    z += FurnishStepZ;
                }
                yield return TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.Complete, Slow, "the house to be furnished");
                Assert.IsTrue(Hud.ResultShown);
                Assert.IsTrue(Hud.MatchEndShown);
                Assert.AreEqual(CrewEnd.Furnished, Hud.MatchEndBanner);
                Assert.IsTrue(Hud.ShownMatchEnd.Won);
                Assert.AreEqual(1, Hud.MatchEndCardCount);
                AssertOldCoopCardIsStillThere("Home, sweet home!");
                Assert.IsTrue(Active("Detail"), "the stars line stays");
                StringAssert.Contains("3 stars", Text("Detail"));
                AssertCrewBoardFits();
            }
            finally
            {
                if (savedStars == null) PlayerPrefs.DeleteKey(Session.StarsKey); else PlayerPrefs.SetString(Session.StarsKey, savedStars);
                PlayerPrefs.Save();
            }
        }
    }
}
