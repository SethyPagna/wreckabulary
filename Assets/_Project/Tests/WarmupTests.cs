using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class WarmupTests
    {
        const float Slow = 30f, AfterWarmup = 45f, LongWarmup = 600f, ShortCountdown = .05f, SpawnSlack = 2f, WatchSeconds = 1.5f;

        static readonly string[] VersusModes =
        {
            HouseModes.Dibs, HouseModes.Duos, HouseModes.FreeForAll, HouseModes.War, HouseModes.ShoppingList, HouseModes.RiddleRush,
        };
        static readonly string[] BothFlows = { HouseModes.Dibs, HouseModes.FreeForAll };
        static readonly string[] CoopModes = { HouseModes.MovingOut, HouseModes.Escape };
        static readonly string[] OwnScenes = { Session.MovingDayScene, Session.TutorialScene };

        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RoundManager.WarmupRequested = false;
            RiddleRushDirector.SeedOverride = null;
            Time.timeScale = 1f;
            yield return TestScenes.Reset();
        }

        static PlayerJoinManager Joins => Object.FindAnyObjectByType<PlayerJoinManager>();
        static GameHud Hud => Object.FindAnyObjectByType<GameHud>();
        static RoundManager Rounds => RoundManager.Instance;
        static HouseRoundDirector Director => Object.FindAnyObjectByType<HouseRoundDirector>();
        static WarmupGuideStrip Strip => Object.FindAnyObjectByType<WarmupGuideStrip>();
        static PlayerController Human(int nth = 0) => Joins.Players.Where(p => p.Binding is ScriptedBinding).ElementAt(nth);
        static PlayerController Bot => Joins.Players.First(p => p.Binding is BotBinding);

        static IEnumerator Load(string mode, bool warmup, int humans = 1, string map = "pinwheel", InputBinding extra = null)
        {
            Session.SelectMap(map);
            Match.ModeOverride = mode;
            for (int i = 0; i < humans; i++) Session.Remember(new ScriptedBinding());
            if (extra != null) Session.Remember(extra);
            RoundManager.WarmupRequested = warmup;
            yield return TestScenes.Load(Session.DibsScene);
            yield return TestScenes.ExploreHouse();
        }

        static IEnumerator LoadWarmup(string mode, int humans = 1, InputBinding extra = null)
        {
            yield return Load(mode, true, humans, extra: extra);
            yield return UntilWarmup(mode);
            if (humans > 0) Hud.Follow(Human());
        }

        static IEnumerator UntilWarmup(string mode)
        {
            yield return TestScenes.WaitUntil(() => Rounds && Rounds.Phase == Phase.Warmup && Rounds.Warmup != null, Slow, "the warm-up to start in " + mode);
            Rounds.Warmup.State.Length = LongWarmup;
            Rounds.ShowcaseTime = 0f;
            StopBots();
        }

        static IEnumerator UntilCountdown(string what) =>
            TestScenes.WaitUntil(() => Rounds.Phase == Phase.Countdown, AfterWarmup, what);

        static PlayerController Seated(InputBinding binding) => Joins.Players.First(p => p.Binding == binding);

        static IEnumerator UntilFree(PlayerController player) =>
            TestScenes.WaitUntil(() => player.CanAct && !player.IsDodging, Slow, "the player to be free to act");

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

        static void EndByTimer() => Rounds.Warmup.State.Length = Rounds.Warmup.State.Elapsed;

        static void Knock(PlayerController victim, PlayerController attacker) =>
            victim.Health.ApplyDamage(Hits.Of(attacker, Vector3.forward, HitSource.Melee, 1000f, 1f));

        static Rect OnScreen(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        static Vector3 Flat(Vector3 at) => new Vector3(at.x, 0f, at.z);

        static void TurnTo(Warmup state, int card, int count)
        {
            float cycle = Warmup.CardSeconds * count, at = state.Elapsed % cycle, target = Warmup.CardSeconds * (card + .5f);
            state.Tick(target > at ? target - at : cycle - at + target);
        }

        [UnityTest]
        public IEnumerator ADirectStartSkipsTheWarmup([ValueSource(nameof(BothFlows))] string mode)
        {
            yield return Load(mode, false);
            yield return TestScenes.WaitUntil(() => Rounds && Rounds.Phase != Phase.Lobby, Slow, mode + " to start");
            Assert.That(Rounds.Phase, Is.EqualTo(Phase.Countdown).Or.EqualTo(Phase.Playing), "a direct start goes straight to round one");
            Assert.IsNull(Rounds.Warmup);
            Assert.IsNull(Strip);
            Assert.IsFalse(LetterInventory.FreeSpelling);
            Assert.AreEqual(1, Rounds.Round);
            QuickCountdown();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Playing, Slow, mode + " to reach Playing");
            var human = Human();
            human.Inventory.Collects = false;
            human.Inventory.Set("");
            Assert.IsFalse(human.Inventory.SpellsFree);
            Assert.IsFalse(human.Summoner.Summon("BAT"), "spelling still needs letters");
        }

        [UnityTest]
        public IEnumerator ARequestedWarmupRunsOnceBeforeRoundOne([ValueSource(nameof(VersusModes))] string mode)
        {
            yield return LoadWarmup(mode);
            var warmup = Rounds.Warmup;
            Assert.IsFalse(RoundManager.WarmupRequested, "the request is used up by the scene that took it");
            Assert.AreEqual(0, Rounds.Round, "round one has not started");
            Assert.AreEqual(HouseModes.BotSeats(mode), Joins.Players.Count, "bots and teams exist before the warm-up");
            Assert.AreEqual(1, warmup.State.Voters, "only the human votes");
            Assert.IsTrue(Joins.Players.All(p => !p.Frozen), "nobody is frozen");
            Assert.IsFalse(Joins.AllowJoining);
            Assert.IsFalse(Joins.RespawnKnockedOut, "the warm-up brings knockouts back itself");
            Assert.IsTrue(LetterInventory.FreeSpelling);
            Assert.IsFalse(Hud.ResultShown);
            Assert.IsFalse(Hud.CountdownShown);
            if (Rounds.Directed) Assert.AreEqual(HouseRoundDirector.Stage.Warmup, Director.Current);
            yield return TestScenes.WaitUntil(() => Strip && Strip.Built, Slow, "the card strip");
            Assert.AreEqual(mode, Strip.Mode);
            Assert.AreEqual(ModeGuideBook.For(mode).Steps.Count, Strip.CardCount);

            EndByTimer();
            yield return TestScenes.WaitUntil(() => Rounds.Phase != Phase.Warmup, Slow, "the timer to end the warm-up");
            Assert.IsNull(Rounds.Warmup);
            Assert.IsFalse(LetterInventory.FreeSpelling);
            yield return null;
            Assert.IsNull(Strip, "the strip goes with the warm-up");
            Assert.IsNull(Hud.UiCanvas.transform.Find("Safe HUD/" + WarmupGuideStrip.RootName));
            yield return UntilCountdown("the usual countdown after the warm-up in " + mode);
            Assert.AreEqual(1, Rounds.Round);
            Assert.IsNull(Rounds.Warmup);
            Assert.IsFalse(LetterInventory.FreeSpelling);
            Assert.IsTrue(Joins.Players.All(p => p.Frozen), "everyone waits out the countdown as usual");
            Assert.IsTrue(Joins.Players.All(p => p.Health.IsAlive && Mathf.Approximately(p.Health.Current, p.Health.Max)));
            if (Rounds.Directed) Assert.AreEqual(HouseRoundDirector.Stage.Countdown, Director.Current);

            QuickCountdown();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Playing, Slow, mode + " to reach Playing");
            if (Rounds.Directed) Assert.AreEqual(HouseRoundDirector.Stage.Playing, Director.Current);
            Assert.AreEqual(1, Rounds.Round);

            Rounds.StartMatch();
            yield return null;
            Assert.AreNotEqual(Phase.Warmup, Rounds.Phase, "a rematch does not warm up again");
            Assert.IsNull(Rounds.Warmup);
        }

        [UnityTest]
        public IEnumerator PlayersMoveAndSpellWithoutLettersDuringWarmup()
        {
            yield return LoadWarmup(HouseModes.Dibs);
            var human = Human();
            var input = (ScriptedBinding)human.Binding;
            human.Inventory.Collects = false;
            human.Inventory.Set("");
            Assert.IsFalse(human.Frozen, "nobody is frozen in the warm-up");
            yield return UntilFree(human);

            var from = human.transform.position;
            float moved = 0f;
            foreach (var way in new[] { Vector2.right, Vector2.up, Vector2.left, Vector2.down })
            {
                float until = Time.time + .6f;
                while (Time.time < until && moved < 1f)
                {
                    input.Next.move = way;
                    yield return null;
                    moved = Mathf.Max(moved, Vector3.Distance(Flat(from), Flat(human.transform.position)));
                }
            }
            input.Next.move = Vector2.zero;
            Assert.Greater(moved, .5f, "the player can walk about");

            yield return UntilFree(human);
            Assert.IsTrue(human.Inventory.SpellsFree);
            Assert.AreEqual(human.Summoner.Words.Count, human.Summoner.Options().Count, "every recipe the mode allows is on offer");
            var recipe = human.Summoner.Recipe("BAT");
            Assert.IsNotNull(recipe);
            Assert.IsTrue(human.Summoner.BeginCraft(recipe), "a craft starts with an empty bag");
            Assert.AreEqual(0, human.Inventory.ReservedCount, "nothing is reserved because nothing was paid");
            human.Summoner.CancelCraft();
            Assert.AreEqual(0, human.Inventory.Count, "a refund during free spelling mints no letters");
            Assert.IsTrue(human.Summoner.Summon("BAT"), "a spell works with an empty bag");
            Assert.IsTrue(human.Combat.Weapon && human.Combat.Weapon.word == "BAT");
            Assert.AreEqual(0, human.Inventory.Count);

            human.Inventory.Set("BAT");
            Assert.IsTrue(human.Summoner.Summon("BAT"));
            Assert.AreEqual(3, human.Inventory.Count, "letters you do hold are not taken either");

            var bot = Bot;
            bot.Inventory.Collects = false;
            bot.Inventory.Set("");
            Assert.IsFalse(bot.Inventory.SpellsFree, "bots play as usual");
            Assert.IsFalse(bot.Summoner.Summon("BAT"));
            Assert.AreEqual(0, bot.Summoner.Options().Count);
        }

        [UnityTest]
        public IEnumerator AKnockoutComesBackAndNothingScores([ValueSource(nameof(BothFlows))] string mode)
        {
            yield return LoadWarmup(mode);
            var human = Human();
            var victim = Bot;
            var room = Object.FindAnyObjectByType<RoomBuilder>();
            room.Originals.First(s => s && !s.IsBroken && !s.Invulnerable).Break();
            yield return null;
            Assert.GreaterOrEqual(MatchTally.RoundBroken, 1, "the tally listens all the time");

            Knock(victim, human);
            Assert.IsTrue(victim.IsKnockedOut, "damage is on");
            yield return TestScenes.WaitUntil(() => !victim.IsKnockedOut, Slow, "the knocked-out seat to come back");
            Assert.IsTrue(Mathf.Approximately(victim.Health.Current, victim.Health.Max));
            Assert.AreEqual(Phase.Warmup, Rounds.Phase);
            Assert.AreEqual(0, Rounds.Round);
            Assert.AreEqual(0, Rounds.WinsOf(human));
            var battle = Object.FindAnyObjectByType<FreeForAllDirector>();
            if (battle) Assert.IsNull(battle.Race, "no knockout race exists yet");

            foreach (var other in Joins.Players.Where(p => p != human)) other.Health.Eliminate();
            yield return null;
            yield return null;
            Assert.AreEqual(Phase.Warmup, Rounds.Phase, "a wipe ends nothing");
            Assert.IsFalse(Hud.ResultShown);
            Assert.IsFalse(Hud.ResultPending);
            yield return TestScenes.WaitUntil(() => Joins.Players.All(p => !p.IsKnockedOut), Slow, "everyone to come back");

            EndByTimer();
            yield return UntilCountdown("round one");
            Assert.AreEqual(0, MatchTally.RoundBroken, "the match tally starts after the warm-up");
            Assert.AreEqual(0, MatchTally.RoundCrafted);
            Assert.AreEqual(0, MatchTally.RoundDamage);
            Assert.AreEqual(0, Rounds.WinsOf(human));
        }

        [UnityTest]
        public IEnumerator EveryHumanHoldingStartEndsItEarly()
        {
            yield return LoadWarmup(HouseModes.Dibs, humans: 2);
            var state = Rounds.Warmup.State;
            var first = Human(0);
            var second = Human(1);
            Assert.AreEqual(2, state.Voters);
            Assert.AreEqual(4, Joins.Players.Count);

            float deadline = Time.realtimeSinceStartup + Slow;
            while (!state.IsReady(first.Index) && Time.realtimeSinceStartup < deadline)
            {
                ((ScriptedBinding)first.Binding).Next.start = true;
                yield return null;
            }
            Assert.IsTrue(state.IsReady(first.Index), "holding Start for a second marks the seat ready");
            Assert.AreEqual(Phase.Warmup, Rounds.Phase, "one human is still warming up");
            yield return null;
            yield return null;
            Assert.IsTrue(state.IsReady(first.Index), "letting go does not unmark the seat");
            Assert.AreEqual(1, state.ReadyCount);
            yield return TestScenes.WaitUntil(() => Strip && Strip.Built && Strip.ReadyText == "1 of 2 ready", Slow, "the ready count on the banner");

            ((ScriptedBinding)second.Binding).Next.start = true;
            yield return null;
            yield return null;
            yield return null;
            Assert.IsFalse(state.IsReady(second.Index), "a tap is not a hold");

            deadline = Time.realtimeSinceStartup + Slow;
            while (Rounds.Phase == Phase.Warmup && Time.realtimeSinceStartup < deadline)
            {
                ((ScriptedBinding)second.Binding).Next.start = true;
                yield return null;
            }
            Assert.AreNotEqual(Phase.Warmup, Rounds.Phase, "everyone ready ends the warm-up");
            Assert.IsTrue(state.AllReady);
            Assert.Greater(state.TimeLeft, 0f, "it ended on the votes, not the timer");
            Assert.IsFalse(LetterInventory.FreeSpelling);
            Assert.IsFalse(Hud.Paused, "Start never opened the pause menu");
            yield return UntilCountdown("the countdown once everyone is ready");
            Assert.AreEqual(1, Rounds.Round);
            Assert.IsFalse(Hud.Paused, "and the match is not paused when the countdown starts");
        }

        [UnityTest]
        public IEnumerator AfterWarmupTheHouseAndPlayersAreFresh()
        {
            yield return LoadWarmup(HouseModes.Dibs);
            var human = Human();
            var room = Object.FindAnyObjectByType<RoomBuilder>();
            int pieces = room.Originals.Count;
            Assert.Greater(pieces, 0);
            human.Inventory.Collects = false;
            yield return UntilFree(human);

            Assert.IsTrue(human.Summoner.Summon("BAT"));
            human.Inventory.Set("WRECK");
            human.Health.ApplyDamage(Hits.Of(Bot, Vector3.forward, HitSource.Melee, 30f, 0f));
            Assert.Less(human.Health.Current, human.Health.Max);
            var broken = room.Originals.First(s => s && !s.IsBroken && !s.Invulnerable);
            broken.Break();
            Assert.IsTrue(broken.IsBroken);
            human.Respawn(Joins.SpawnPoint(human.Index + 1));
            yield return null;

            EndByTimer();
            yield return UntilCountdown("the countdown after the warm-up");
            Assert.AreEqual(Match.Rules.StarterLetters ?? "", new string(human.Inventory.Letters.ToArray()), "the bag is back to the starter letters");
            Assert.AreEqual(0, human.Inventory.ReservedCount);
            Assert.IsTrue(Mathf.Approximately(human.Health.Current, human.Health.Max), "health is full");
            Assert.IsFalse(human.Combat.Weapon, "the warm-up gear is gone");
            Assert.IsFalse(human.Combat.StoredGear);
            Assert.IsFalse(human.Summoner.IsCrafting);
            Assert.IsFalse(LetterInventory.FreeSpelling, "free spelling is off");
            Assert.IsFalse(human.Inventory.SpellsFree);
            Assert.Less(Vector3.Distance(Flat(human.transform.position), Flat(Joins.SpawnPoint(human.Index))), SpawnSlack, "everyone is back at their start");
            yield return null;
            Assert.AreEqual(pieces, room.Originals.Count, "the furniture is rebuilt");
            Assert.IsTrue(room.Originals.All(s => s && !s.IsBroken), "the broken piece is back");
            Assert.IsEmpty(Object.FindObjectsByType<SummonedThing>(FindObjectsSortMode.None), "nothing summoned survives");
            Assert.IsEmpty(Object.FindObjectsByType<HeldWeapon>(FindObjectsSortMode.None), "no gear is left lying about");

            QuickCountdown();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Playing, Slow, "round one to start");
            Assert.AreEqual(1, Rounds.Round);
            yield return UntilFree(human);
            human.Inventory.Set("");
            Assert.IsFalse(human.Summoner.Summon("BAT"), "round one spelling needs letters again");
            human.Inventory.Set("BAT");
            Assert.IsTrue(human.Summoner.Summon("BAT"));
            Assert.AreEqual(0, human.Inventory.Count, "and spends them");
        }

        [UnityTest]
        public IEnumerator TheGuideStripShowsTheModeCardsAndNeverTakesInput()
        {
            yield return LoadWarmup(HouseModes.ShoppingList);
            var state = Rounds.Warmup.State;
            var guide = ModeGuideBook.For(HouseModes.ShoppingList);
            yield return TestScenes.WaitUntil(() => Strip && Strip.Built && Strip.Shown, Slow, "the card strip");
            var strip = Strip;
            TurnTo(state, 0, guide.Steps.Count);
            yield return TestScenes.WaitUntil(() => strip.Card == 0, Slow, "the first card");
            Assert.AreEqual(Warmup.Title, strip.TitleText);
            StringAssert.IsMatch(@"^\d:\d\d$", strip.ClockText);
            Assert.AreEqual(Warmup.ReadyPrompt(ControlHints.For(Human().Binding, PlayerControl.Start)), strip.ReadyText);
            Assert.AreEqual(Warmup.Tip, strip.TipText);
            Assert.AreEqual(guide.Steps[0].Title, strip.CardTitleText);
            Assert.IsNotEmpty(strip.CardLineText);
            StringAssert.DoesNotContain("{", strip.CardLineText, "control tokens are filled in");
            Assert.IsTrue(strip.PictureShown, "the card shows its picture");

            Assert.IsFalse(ModeGuides.Open, "the strip is not the modal guide");
            Assert.AreEqual(1f, Time.timeScale);
            Assert.IsTrue(Hud.AcceptsGameplayInput);
            Assert.IsFalse(Human().Frozen, "play carries on under the strip");
            Assert.IsTrue(strip.Root.GetComponentsInChildren<Graphic>(true).All(g => !g.raycastTarget), "nothing on the strip takes a click");
            Assert.IsEmpty(strip.Root.GetComponentsInChildren<Selectable>(true), "nothing on the strip takes focus");
            var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            Assert.IsFalse(selected && selected.transform.IsChildOf(strip.Root));
            foreach (var text in strip.Root.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Assert.IsTrue(text.enableAutoSizing, text.name);
                Assert.GreaterOrEqual(text.fontSizeMin, WarmupGuideStrip.MinFont, text.name);
                Assert.GreaterOrEqual(text.fontSizeMax, text.fontSizeMin, text.name);
            }

            Canvas.ForceUpdateCanvases();
            var safe = OnScreen((RectTransform)Hud.UiCanvas.transform.Find("Safe HUD"));
            var box = OnScreen(strip.Root);
            Assert.That(box.xMin, Is.GreaterThanOrEqualTo(safe.xMin - 1f));
            Assert.That(box.xMax, Is.LessThanOrEqualTo(safe.xMax + 1f));
            Assert.That(box.yMin, Is.GreaterThanOrEqualTo(safe.yMin - 1f));
            Assert.That(box.yMax, Is.LessThanOrEqualTo(safe.yMax + 1f));
            var tray = (RectTransform)Hud.UiCanvas.transform.Find("Safe HUD/Letter bag");
            if (strip.InCorner) Assert.IsFalse(box.Overlaps(OnScreen(tray)), "the strip sits beside the letter bag");

            TurnTo(state, 1, guide.Steps.Count);
            yield return TestScenes.WaitUntil(() => strip.Card == 1, Slow, "the next card by itself");
            Assert.AreEqual(guide.Steps[1].Title, strip.CardTitleText);
            Assert.IsTrue(strip.PictureShown);
            TurnTo(state, guide.Steps.Count - 1, guide.Steps.Count);
            yield return TestScenes.WaitUntil(() => strip.Card == guide.Steps.Count - 1, Slow, "the last card");
            TurnTo(state, 0, guide.Steps.Count);
            yield return TestScenes.WaitUntil(() => strip.Card == 0, Slow, "the cards to loop");
            Assert.AreEqual(Phase.Warmup, Rounds.Phase);
        }

        [UnityTest]
        public IEnumerator PausingHoldsTheWarmupClockAndResumingCarriesOn()
        {
            yield return LoadWarmup(HouseModes.Dibs);
            var state = Rounds.Warmup.State;
            var human = Human();
            Assert.IsFalse(BackToHub.StartPauses(Hud), "Start is the ready button here, not pause");
            Hud.TogglePause();
            yield return TestScenes.WaitUntil(() => Hud.Paused && Time.timeScale == 0f, Slow, "the pause menu");
            float held = state.Elapsed;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.AreEqual(held, state.Elapsed, "the warm-up clock waits while paused");
            Assert.AreEqual(Phase.Warmup, Rounds.Phase);
            Hud.TogglePause();
            yield return TestScenes.WaitUntil(() => Hud.AcceptsGameplayInput, Slow, "the game to resume");
            yield return TestScenes.WaitUntil(() => state.Elapsed > held, Slow, "the warm-up clock to run again");
            Assert.AreEqual(Phase.Warmup, Rounds.Phase);
            Assert.IsFalse(human.Frozen, "players are free again after the pause");
            Assert.IsTrue(LetterInventory.FreeSpelling);
        }

        [UnityTest]
        public IEnumerator LeavingMidWarmupTurnsFreeSpellingOff()
        {
            yield return LoadWarmup(HouseModes.FreeForAll);
            Assert.IsTrue(LetterInventory.FreeSpelling);
            yield return TestScenes.Reset();
            Assert.IsFalse(LetterInventory.FreeSpelling, "the flag cannot outlive its scene");
            Assert.IsFalse(RoundManager.WarmupRequested);
            Assert.IsNull(Strip);
        }

        [UnityTest]
        public IEnumerator CoopModesIgnoreTheRequest([ValueSource(nameof(CoopModes))] string mode)
        {
            yield return Load(mode, true, map: "flat");
            Assert.IsFalse(RoundManager.WarmupRequested, "the request does not linger for the next match");
            Assert.IsTrue(Rounds);
            Assert.IsFalse(Rounds.enabled, mode + " runs its own director");
            yield return null;
            yield return null;
            Assert.AreNotEqual(Phase.Warmup, Rounds.Phase);
            Assert.IsNull(Rounds.Warmup);
            Assert.IsNull(Strip);
            Assert.IsFalse(LetterInventory.FreeSpelling);
            var human = Human();
            human.Inventory.Collects = false;
            human.Inventory.Set("");
            Assert.IsFalse(human.Inventory.SpellsFree);
            if (mode == HouseModes.MovingOut)
            {
                var director = Object.FindAnyObjectByType<MovingOutDirector>();
                Assert.IsNotNull(director);
                yield return TestScenes.WaitUntil(() => director.Current == MovingOutDirector.State.Playing, Slow, "Pack the Van to start as usual");
            }
            else
            {
                var director = Object.FindAnyObjectByType<EscapeDirector>();
                Assert.IsNotNull(director);
                yield return TestScenes.WaitUntil(() => director.Current == EscapeDirector.State.Playing, Slow, "Escape to start as usual");
            }
            Assert.IsFalse(LetterInventory.FreeSpelling);
        }

        [UnityTest]
        public IEnumerator ATouchSeatDoesNotVoteAndNeverHoldsUpTheOthers()
        {
            yield return LoadWarmup(HouseModes.Dibs, extra: TouchBinding.Shared);
            var state = Rounds.Warmup.State;
            var human = Human();
            var touch = Seated(TouchBinding.Shared);
            Assert.AreEqual(2, Joins.HumanCount);
            Assert.AreEqual(HouseModes.BotSeats(HouseModes.Dibs), Joins.Players.Count, "bots fill the seats the two players left");
            Assert.AreEqual(1, state.Voters, "a touch seat has no Start to hold, so it does not vote");
            Assert.AreEqual(0f, state.HoldProgress(touch.Index));
            Assert.IsFalse(touch.Frozen, "the touch seat still plays");
            Assert.IsTrue(touch.Inventory.SpellsFree, "and spells for free");

            float deadline = Time.realtimeSinceStartup + Slow;
            while (Rounds.Phase == Phase.Warmup && Time.realtimeSinceStartup < deadline)
            {
                ((ScriptedBinding)human.Binding).Next.start = true;
                yield return null;
            }
            Assert.AreNotEqual(Phase.Warmup, Rounds.Phase, "the one seat that can vote ends the warm-up alone");
            Assert.IsTrue(state.AllReady);
            Assert.IsFalse(state.IsReady(touch.Index));
            Assert.Greater(state.TimeLeft, 0f, "it ended on the vote, not the timer");
            Assert.IsFalse(LetterInventory.FreeSpelling);
        }

        [UnityTest]
        public IEnumerator WithOnlyATouchSeatTheTimerEndsTheWarmup()
        {
            yield return LoadWarmup(HouseModes.Dibs, humans: 0, extra: TouchBinding.Shared);
            var state = Rounds.Warmup.State;
            var touch = Seated(TouchBinding.Shared);
            Hud.Follow(touch);
            Assert.AreEqual(1, Joins.HumanCount);
            Assert.AreEqual(0, state.Voters, "nobody has a Start to hold");
            Assert.AreEqual("", state.ReadyLine(), "so the banner does not ask for one");
            Assert.IsFalse(touch.Frozen);
            float until = Time.realtimeSinceStartup + WatchSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                Assert.AreEqual(Phase.Warmup, Rounds.Phase, "no votes is not everyone ready");
                yield return null;
            }
            Assert.IsFalse(state.AllReady);

            EndByTimer();
            yield return TestScenes.WaitUntil(() => Rounds.Phase != Phase.Warmup, Slow, "the timer to end a warm-up nobody can vote in");
            Assert.IsNull(Rounds.Warmup);
            Assert.IsFalse(LetterInventory.FreeSpelling);
            yield return UntilCountdown("round one for the touch seat");
            Assert.AreEqual(1, Rounds.Round);
        }

        [UnityTest]
        public IEnumerator TheHouseIsFullSoNoSeatJoinsDuringWarmup([ValueSource(nameof(BothFlows))] string mode)
        {
            yield return LoadWarmup(mode);
            var state = Rounds.Warmup.State;
            int seats = Joins.Players.Count;
            Assert.IsFalse(Joins.AllowJoining, "joining closes when the match starts");
            Assert.GreaterOrEqual(seats, Joins.MaxPlayers, "bots already hold every seat");
            Assert.IsNull(Joins.Join(new ScriptedBinding()), "so a late seat has nowhere to sit");
            yield return null;
            Assert.AreEqual(seats, Joins.Players.Count);
            Assert.AreEqual(1, state.Voters, "and the votes needed cannot change mid warm-up");
            Assert.AreEqual(1, Session.Bindings.Count, "the refused seat is not remembered for the next match");

            EndByTimer();
            yield return UntilCountdown("round one in " + mode);
            Assert.IsFalse(Joins.AllowJoining, "joining stays closed for round one");
            Assert.AreEqual(seats, Joins.Players.Count);
        }

        [UnityTest]
        public IEnumerator NothingFromTheWarmupReachesTheCareer()
        {
            var kept = new[] { new Kept(MatchTally.CareerKey, false), new Kept(ControlTips.LearnedKey, true), new Kept(ControlTips.SeenKey, true) };
            var lastResult = MatchTally.LastResult;
            try
            {
                string career = PlayerPrefs.GetString(MatchTally.CareerKey, "");
                var seat = new CareerSeat();
                yield return Load(HouseModes.Dibs, true, humans: 0, extra: seat);
                yield return UntilWarmup(HouseModes.Dibs);
                var human = Seated(seat);
                Hud.Follow(human);
                human.Inventory.Collects = false;
                yield return UntilFree(human);
                yield return null;

                var room = Object.FindAnyObjectByType<RoomBuilder>();
                room.Originals.First(s => s && !s.IsBroken && !s.Invulnerable).Break();
                Assert.IsTrue(human.Summoner.Summon("BAT"));
                Knock(Bot, human);
                yield return null;
                Assert.GreaterOrEqual(MatchTally.RoundBroken, 1, "the tally hears the warm-up");
                Assert.GreaterOrEqual(MatchTally.RoundCrafted, 1, "this seat is the one the career follows");
                Assert.AreEqual(0, MatchTally.RoundDamage, "warm-up hits pop no points and count for nothing");
                Assert.AreEqual(career, PlayerPrefs.GetString(MatchTally.CareerKey, ""), "nothing is saved while warming up");

                EndByTimer();
                yield return UntilCountdown("round one");
                Assert.AreEqual(0, MatchTally.RoundBroken, "the match tally starts after the warm-up");
                Assert.AreEqual(0, MatchTally.RoundCrafted);
                Assert.AreEqual(0, MatchTally.RoundDamage);
                Assert.AreEqual(career, PlayerPrefs.GetString(MatchTally.CareerKey, ""), "ending the warm-up saves nothing");
                Assert.AreSame(lastResult, MatchTally.LastResult, "and records no match");

                var record = MatchTally.Finish(false);
                Assert.IsNotNull(record);
                Assert.AreEqual(0, record.Broken, "a match that ends now counts no warm-up furniture");
                Assert.AreEqual(0, record.Crafted, "no warm-up spells");
                Assert.AreEqual(0, record.Damage, "and no warm-up damage");
            }
            finally
            {
                foreach (var pref in kept) pref.Restore();
                PlayerPrefs.Save();
                MatchTally.LastResult = lastResult;
            }
        }

        [UnityTest]
        public IEnumerator RoundTwoStartsWithoutAWarmup()
        {
            yield return LoadWarmup(HouseModes.Dibs);
            var human = Human();
            EndByTimer();
            yield return UntilCountdown("round one");
            QuickCountdown();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Playing, Slow, "round one to play");
            Assert.AreEqual(1, Rounds.Round);

            foreach (var other in Joins.Players.Where(p => p != human)) other.Health.Eliminate();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.RoundOver, Slow, "round one to end with rounds still to play");
            Assert.AreEqual(1, Rounds.WinsOf(human));
            yield return TestScenes.WaitUntil(() => Hud.ResultShown, Slow, "the round result");
            Hud.ConfirmResult();

            bool warmed = false;
            float deadline = Time.realtimeSinceStartup + Slow;
            while (Rounds.Round != 2 || Rounds.Phase != Phase.Countdown)
            {
                warmed |= Rounds.Phase == Phase.Warmup || Rounds.Warmup != null || LetterInventory.FreeSpelling || Strip != null;
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: the round two countdown");
                yield return null;
            }
            Assert.IsFalse(warmed, "later rounds keep the short countdown");
            Assert.IsNull(Rounds.Warmup);
            Assert.IsNull(Strip);
            Assert.IsTrue(Joins.Players.All(p => p.Frozen), "everyone waits out the countdown as usual");

            QuickCountdown();
            yield return TestScenes.WaitUntil(() => Rounds.Phase == Phase.Playing, Slow, "round two to play");
            Assert.AreEqual(2, Rounds.Round);
            Assert.IsFalse(LetterInventory.FreeSpelling);
            human.Inventory.Collects = false;
            human.Inventory.Set("");
            Assert.IsFalse(human.Inventory.SpellsFree);
            Assert.IsFalse(human.Summoner.Summon("BAT"), "round two spelling needs letters");
        }

        [UnityTest]
        public IEnumerator ScenesWithTheirOwnFlowNeverWarmUp([ValueSource(nameof(OwnScenes))] string scene)
        {
            RoundManager.WarmupRequested = true;
            yield return TestScenes.Load(scene);
            Assert.IsFalse(RoundManager.WarmupRequested, "the request does not linger for the next match");
            Assert.IsNotNull(Joins);
            Joins.Join(new ScriptedBinding());
            float until = Time.realtimeSinceStartup + WatchSeconds;
            while (Time.realtimeSinceStartup < until)
            {
                Assert.IsFalse(Rounds && (Rounds.Phase == Phase.Warmup || Rounds.Warmup != null), scene + " has no warm-up");
                Assert.IsFalse(LetterInventory.FreeSpelling);
                Assert.IsNull(Strip);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ASceneWithoutRoundsDropsTheRequest()
        {
            RoundManager.WarmupRequested = true;
            yield return TestScenes.Reset();
            Assert.IsFalse(RoundManager.WarmupRequested, "a stale request never reaches a later match");
            yield return Load(HouseModes.Dibs, false);
            yield return TestScenes.WaitUntil(() => Rounds && Rounds.Phase != Phase.Lobby, Slow, "the match to start");
            Assert.AreNotEqual(Phase.Warmup, Rounds.Phase);
            Assert.IsNull(Rounds.Warmup);
        }

        sealed class CareerSeat : InputBinding
        {
            public override string Id => "warm-up-career-seat";
            public override void Read(ref PlayerCommands c) => c = default;
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        sealed class Kept
        {
            readonly string key, text;
            readonly int number;
            readonly bool whole, had;

            public Kept(string key, bool whole)
            {
                this.key = key; this.whole = whole;
                had = PlayerPrefs.HasKey(key);
                text = whole ? null : PlayerPrefs.GetString(key);
                number = whole ? PlayerPrefs.GetInt(key) : 0;
            }

            public void Restore()
            {
                if (!had) PlayerPrefs.DeleteKey(key);
                else if (whole) PlayerPrefs.SetInt(key, number);
                else PlayerPrefs.SetString(key, text);
            }
        }
    }
}
