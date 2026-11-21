using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class MatchHudScoreTests
    {
        const float Slow = 10f, Slack = .5f, SmallestText = 15f;

        readonly List<Object> made = new();

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            MatchStats.Begin();
            LetterInventory.FreeSpelling = false;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var thing in made) if (thing) Object.Destroy(thing);
            made.Clear();
            Time.timeScale = 1f;
            LetterInventory.FreeSpelling = false;
            MatchStats.Begin();
            TouchBinding.Shared.ReleaseAll();
            TouchBinding.Shared.Enabled = TouchBinding.Shared.OverlayDesktop = false;
            TouchBinding.Shared.OverlayBindingId = null;
            SummonedThing.ClearAll();
            yield return TestScenes.Reset();
        }

        static PlayerJoinManager Joins => Object.FindAnyObjectByType<PlayerJoinManager>();
        static GameHud SceneHud => Object.FindAnyObjectByType<GameHud>();

        void Ground()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            made.Add(ground);
            TilePool.Ensure();
        }

        PlayerController Spawn(int seat, int team, InputBinding binding = null)
        {
            var at = new Vector3(seat * 3f - 6f, 0f, 0f);
            var player = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            player.Setup(seat, binding ?? new ScriptedBinding());
            player.Respawn(at);
            player.Inventory.Collects = false;
            player.Team = team;
            made.Add(player.gameObject);
            return player;
        }

        PlayerController[] Seats(params int[] teams)
        {
            Ground();
            return teams.Select((team, seat) => Spawn(seat, team)).ToArray();
        }

        IEnumerator Hud(PlayerController you, System.Action<GameHud> ready)
        {
            var hud = new GameObject("Score HUD", typeof(Canvas)).AddComponent<GameHud>();
            made.Add(hud.gameObject);
            yield return null;
            hud.ShowTouchControls(false);
            hud.Follow(you);
            yield return TestScenes.WaitUntil(() => hud.AcceptsGameplayInput, Slow, "the gameplay HUD");
            ready(hud);
        }

        static RectTransform Bar(GameHud hud) => (RectTransform)hud.transform.Find("Safe HUD/Player bar");
        static RectTransform Part(GameHud hud, string path) => (RectTransform)Bar(hud).Find(path);
        static float X(GameHud hud, int card) => Part(hud, $"Card {card}").anchoredPosition.x;
        static string Name(GameHud hud, int card) => Part(hud, $"Card {card}/Name").GetComponent<TMP_Text>().text;
        static string CardScore(GameHud hud, int card) => Part(hud, $"Card {card}/Score").GetComponent<TMP_Text>().text;
        static bool Leads(GameHud hud, int card) => Part(hud, $"Card {card}/Lead").GetComponent<Image>().enabled;
        static bool Marked(GameHud hud, string mark) => Part(hud, "Centre/" + mark).GetComponent<Image>().enabled;

        static void AssertSameColour(Color expected, Color actual, string message)
        {
            Assert.AreEqual(expected.r, actual.r, .01f, message);
            Assert.AreEqual(expected.g, actual.g, .01f, message);
            Assert.AreEqual(expected.b, actual.b, .01f, message);
        }

        static void AssertCentreBetween(GameHud hud, int leftCard, int rightCard, float cardWidth)
        {
            var centre = Part(hud, "Centre");
            Assert.IsTrue(centre.gameObject.activeSelf, "The centre block shows.");
            Assert.AreEqual(0f, centre.anchoredPosition.x, Slack, "The centre block sits in the middle of the bar.");
            Assert.AreEqual(ScoreLine.CentreWidth, centre.rect.width, Slack);
            float half = ScoreLine.CentreWidth * .5f;
            Assert.LessOrEqual(X(hud, leftCard) + cardWidth * .5f, -half + Slack, "The left cards stop before the centre.");
            Assert.GreaterOrEqual(X(hud, rightCard) - cardWidth * .5f, half - Slack, "The right cards start after it.");
            var plate = Part(hud, "Backing");
            Assert.AreEqual(PlayerBar.PlateWidth(Bar(hud).rect.width), plate.rect.width, Slack, "One plate spans both sides and the centre.");
            var caption = Part(hud, "Centre/Caption");
            Assert.LessOrEqual(caption.rect.height - caption.anchoredPosition.y, Bar(hud).rect.height + Slack, "The centre fits the bar's height.");
            Assert.AreEqual(Bar(hud).rect.height, centre.rect.height, Slack);
        }

        [UnityTest]
        public IEnumerator TwoSidesShowTheirScoresEitherSideOfTheClock()
        {
            var seats = Seats(0, 1, 0, 1);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, p => p.Team == 0 ? 2 : 1, 3, true);
            hud.SetTimer("1:23");
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Score && hud.CentreScore == "2 : 1", Slow, "the score line");
            Assert.AreEqual("1:23", hud.CentreClock, "The match clock sits between the scores.");
            Assert.AreEqual("", hud.CentreCaption);
            Assert.AreEqual(ScoreLine.Separator, Part(hud, "Centre/Colon").GetComponent<TMP_Text>().text);
            CollectionAssert.AreEqual(new[] { "P1", "P3", "P2", "P4" }, Enumerable.Range(1, 4).Select(i => Name(hud, i)), "Each side's cards are grouped on its own side.");
            for (int card = 2; card <= 4; card++) Assert.Greater(X(hud, card), X(hud, card - 1), "left to right");
            AssertCentreBetween(hud, 2, 3, PlayerBar.RoomyCard);
            Assert.AreEqual(PlayerBar.CardHeight(4), Bar(hud).rect.height, Slack, "The bar is no taller than before.");
            AssertSameColour(PlayerHud.Vivid(seats[0].Color), Part(hud, "Centre/Left score").GetComponent<TMP_Text>().color, "The left score wears the left side's colour.");
            AssertSameColour(PlayerHud.Vivid(seats[1].Color), Part(hud, "Centre/Right score").GetComponent<TMP_Text>().color, "The right score wears the right side's colour.");
            Assert.AreEqual("2/3", CardScore(hud, 1), "Cards keep their own scores.");
            Assert.IsFalse(Part(hud, "Centre/Target").GetComponent<RawImage>().enabled, "No target picture in a plain score line.");
            Assert.IsFalse(Marked(hud, "Left lead") || Marked(hud, "Right lead"), "The colours and numbers carry a plain score.");
            foreach (var text in Part(hud, "Centre").GetComponentsInChildren<TMP_Text>(true))
                Assert.GreaterOrEqual(text.enableAutoSizing ? text.fontSizeMin : text.fontSize, SmallestText, text.name);
        }

        [UnityTest]
        public IEnumerator ADuelIsTwoSidesAndThreeOrMoreAreNot()
        {
            var seats = Seats(0, 1, 2);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, p => p.Index, 3, true);
            yield return TestScenes.WaitUntil(() => Bar(hud).gameObject.activeSelf && CardScore(hud, 3) == "2/3", Slow, "three cards");
            Assert.AreEqual(ScoreCentre.None, hud.Centre, "Everyone for themselves has no centre score.");
            Assert.IsFalse(Part(hud, "Centre").gameObject.activeSelf);
            Assert.AreEqual("", hud.CentreScore);
            Assert.AreEqual(PlayerBar.RoomyCard + PlayerBar.RoomyGap, X(hud, 2) - X(hud, 1), Slack, "The cards keep their normal spacing.");
            hud.SetScoreboard(new[] { seats[0], seats[2] }, p => p.Index + 1, 0, true);
            yield return TestScenes.WaitUntil(() => hud.CentreScore == "1 : 3", Slow, "a duel's score line");
            AssertCentreBetween(hud, 1, 2, PlayerBar.RoomyCard);
            hud.SetScoreboard(new[] { seats[0], seats[2] }, _ => 0, 0, false);
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.None, Slow, "no score line without scores");
            hud.SetScoreboard(new[] { seats[0] }, _ => 0, 3, true);
            yield return TestScenes.WaitUntil(() => !Bar(hud).gameObject.activeSelf, Slow, "no bar alone");
            Assert.AreEqual(ScoreCentre.None, hud.Centre);
        }

        [UnityTest]
        public IEnumerator ADirectorCanNameTheSideScore()
        {
            var seats = Seats(0, 1, 0, 1);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, p => p.Index, 0, true, FreeForAllDirector.Unit);
            yield return TestScenes.WaitUntil(() => hud.CentreScore == "2 : 3", Slow, "the best member's score by default");
            hud.SetSideScores(team => team == 0 ? 12 : 9, WarDirector.LivesCaption);
            yield return TestScenes.WaitUntil(() => hud.CentreScore == "12 : 9", Slow, "the named side score");
            Assert.AreEqual(WarDirector.LivesCaption, hud.CentreCaption);
            Assert.AreEqual("2 KO", CardScore(hud, 2), "Seat scores still come from SetScoreboard.");
            hud.SetSideScores(null);
            yield return TestScenes.WaitUntil(() => hud.CentreScore == "2 : 3" && hud.CentreCaption == "", Slow, "the default again");
        }

        [UnityTest]
        public IEnumerator ATargetSitsInTheCentreWithProgressAndALeader()
        {
            var seats = Seats(0, 1, 2, 3);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            var done = new[] { 3, 4, 1, 0 };
            hud.SetScoreboard(seats, p => done[p.Index], 18, true, ShoppingListDirector.Unit);
            hud.SetTarget("BAT");
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Target && hud.LeadingSeat == seats[1], Slow, "the target and its leader");
            Assert.AreEqual("BAT", hud.TargetWord);
            Assert.AreEqual("BAT", hud.CentreCaption);
            Assert.AreEqual("", hud.CentreScore, "Four sides keep their progress on their cards.");
            Assert.AreEqual("3/18 items", CardScore(hud, 1));
            AssertCentreBetween(hud, 2, 3, PlayerBar.RoomyCard);
            var picture = Part(hud, "Centre/Target").GetComponent<RawImage>();
            Assert.AreEqual(Wreckabulary.UI.ItemArt.TryGet("BAT", out _, out _), picture.enabled, "The item's own picture is the target.");
            if (picture.enabled) Assert.IsNotNull(picture.texture);
            CollectionAssert.AreEqual(new[] { false, true, false, false }, Enumerable.Range(1, 4).Select(i => Leads(hud, i)), "Only the leader's card is marked.");
            done[0] = 4;
            yield return TestScenes.WaitUntil(() => !hud.LeadingSeat, Slow, "a tie has no leader");
            Assert.IsFalse(Enumerable.Range(1, 4).Any(i => Leads(hud, i)));

            hud.SetScoreboard(new[] { seats[0], seats[1] }, p => done[p.Index], 18, true, ShoppingListDirector.Unit);
            done[0] = 3;
            yield return TestScenes.WaitUntil(() => hud.CentreScore == "3/18 : 4/18", Slow, "progress per side");
            yield return TestScenes.WaitUntil(() => hud.LeadingSide == ScoreLine.Right, Slow, "the right side leads");
            Assert.IsTrue(Marked(hud, "Right lead"));
            Assert.IsFalse(Marked(hud, "Left lead"));
            Assert.AreSame(seats[1], hud.LeadingSeat);
            Assert.IsFalse(Leads(hud, 1) || Leads(hud, 2), "With two sides the centre carries the mark.");
            done[0] = 5;
            yield return TestScenes.WaitUntil(() => hud.LeadingSide == ScoreLine.Left && hud.CentreScore == "5/18 : 4/18", Slow, "the lead changes hands");
            Assert.IsTrue(Marked(hud, "Left lead"));
            Assert.IsFalse(Marked(hud, "Right lead"));

            hud.SetTarget(null);
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Score && hud.CentreScore == "5 : 4", Slow, "a plain score line again");
            Assert.AreEqual("", hud.TargetWord);
            Assert.IsFalse(Part(hud, "Centre/Target").GetComponent<RawImage>().enabled);
            Assert.IsFalse(Marked(hud, "Left lead") || Marked(hud, "Right lead"));
        }

        [UnityTest]
        public IEnumerator TheScoreboardShowsABlockPerSideAndZerosBeforeAnythingIsRecorded()
        {
            var seats = Seats(0, 1, 0, 1);
            GameHud hud = null;
            yield return Hud(seats[0], h => hud = h);
            hud.SetScoreboard(seats, p => p.Team == 0 ? 2 : 1, 3, true);
            yield return TestScenes.WaitUntil(() => hud.ScoreboardReady && hud.CentreScore == "2 : 1", Slow, "a bar to score");
            var board = (RectTransform)hud.transform.Find("Safe HUD/Scoreboard");
            Assert.IsFalse(hud.ScoreboardShown);
            Assert.IsFalse(board.gameObject.activeSelf);
            hud.ToggleScoreboard();
            Assert.IsTrue(hud.ScoreboardShown, "A tap shows the scoreboard.");
            Assert.AreEqual(2, hud.ScoreboardBlocks);
            CollectionAssert.AreEqual(new[] { "P1 0 0 0 0 2", "P3 0 0 0 0 2", "P2 0 0 0 0 1", "P4 0 0 0 0 1" }, hud.ScoreboardLines, "Nothing recorded reads as zeros.");
            Assert.AreEqual("2", board.Find("Block 1/Total").GetComponent<TMP_Text>().text);
            Assert.AreEqual("1", board.Find("Block 2/Total").GetComponent<TMP_Text>().text);
            Assert.AreEqual("TEAM 1", board.Find("Block 1/Side").GetComponent<TMP_Text>().text);
            AssertSameColour(PlayerHud.Vivid(seats[0].Color), board.Find("Block 1/Total").GetComponent<TMP_Text>().color, "The total wears the side's colour.");
            AssertSameColour(PlayerHud.Vivid(seats[1].Color), board.Find("Block 2/Total").GetComponent<TMP_Text>().color, "The total wears the side's colour.");
            CollectionAssert.AreEqual(new[] { true, true, false, false },
                Enumerable.Range(1, 4).Select(i => board.Find($"Row {i}/Band/Face").GetComponent<Image>().enabled), "Your own side is highlighted.");
            CollectionAssert.AreEqual(ScoreLine.Columns, ScoreLine.Columns.Select(c => board.Find("Column " + c).GetComponent<TMP_Text>().text));
            foreach (var text in board.GetComponentsInChildren<TMP_Text>(true))
                Assert.GreaterOrEqual(text.enableAutoSizing ? text.fontSizeMin : text.fontSize, SmallestText, text.name);
            var area = new Vector3[4];
            var panel = new Vector3[4];
            ((RectTransform)hud.transform.Find("Safe HUD")).GetWorldCorners(area);
            board.GetWorldCorners(panel);
            Assert.GreaterOrEqual(panel[0].x, area[0].x - Slack, "The scoreboard fits the screen.");
            Assert.LessOrEqual(panel[2].x, area[2].x + Slack);
            Assert.GreaterOrEqual(panel[0].y, area[0].y - Slack);
            Assert.LessOrEqual(panel[2].y, area[2].y + Slack);

            var stats = MatchStats.Begin();
            foreach (var p in seats) stats.Enter(p.Index, p.Name, p.Team, false);
            stats.Knockout(1, 2);
            stats.Craft(0);
            stats.Break(3);
            yield return TestScenes.WaitUntil(() => hud.ScoreboardLines.Count == 4 && hud.ScoreboardLines[0] == "P3 1 0 0 0 2", Slow, "recorded numbers");
            CollectionAssert.AreEqual(new[] { "P3 1 0 0 0 2", "P1 0 0 1 0 2", "P4 0 0 0 1 1", "P2 0 1 0 0 1" }, hud.ScoreboardLines);

            hud.ToggleScoreboard();
            Assert.IsFalse(hud.ScoreboardShown, "A second tap hides it.");
            var button = Part(hud, "Scores").GetComponent<Button>();
            button.onClick.Invoke();
            Assert.IsTrue(hud.ScoreboardShown, "The small button by the bar toggles it.");
            hud.ShowResult(new HudResult { Round = 1, Heading = "Round over" }, () => { });
            yield return TestScenes.WaitUntil(() => !hud.ScoreboardShown, Slow, "the result card to take over");
            hud.HideResult();
            yield return null; yield return null;
            Assert.IsFalse(hud.ScoreboardShown, "It does not come back by itself.");
        }

        [UnityTest]
        public IEnumerator ManySidesShareOneRankedList()
        {
            var seats = Seats(0, 1, 2);
            GameHud hud = null;
            yield return Hud(seats[1], h => hud = h);
            hud.SetScoreboard(seats, p => p.Index * 2, 10, true, FreeForAllDirector.Unit);
            yield return TestScenes.WaitUntil(() => hud.ScoreboardReady && CardScore(hud, 3) == "4/10 KO", Slow, "a bar to score");
            hud.ToggleScoreboard();
            Assert.IsTrue(hud.ScoreboardShown);
            Assert.AreEqual(1, hud.ScoreboardBlocks);
            CollectionAssert.AreEqual(new[] { "P3 0 0 0 0 4", "P2 0 0 0 0 2", "P1 0 0 0 0 0" }, hud.ScoreboardLines, "Ranked by score.");
            var board = hud.transform.Find("Safe HUD/Scoreboard");
            Assert.IsFalse(board.Find("Block 2").gameObject.activeSelf);
            Assert.IsFalse(board.Find("Block 1/Total").GetComponent<TMP_Text>().enabled, "No team total for everyone-for-themselves.");
            CollectionAssert.AreEqual(new[] { "1", "2", "3" }, Enumerable.Range(1, 3).Select(i => board.Find($"Row {i}/Rank").GetComponent<TMP_Text>().text));
            CollectionAssert.AreEqual(new[] { false, true, false },
                Enumerable.Range(1, 3).Select(i => board.Find($"Row {i}/Band/Face").GetComponent<Image>().enabled), "Only your own row is highlighted.");
            Assert.IsFalse(board.Find("Row 4").gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator HoldingTheKeyOrPadSelectShowsTheScoreboardAndStartStillPauses()
        {
            var route = InputSystem.settings.editorInputBehaviorInPlayMode;
            var focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keys = InputSystem.AddDevice<Keyboard>();
            var pad = InputSystem.AddDevice<Gamepad>();
            try
            {
                Ground();
                var you = Spawn(0, 0, new GamepadBinding(pad));
                var foe = Spawn(1, 1);
                GameHud hud = null;
                yield return Hud(you, h => hud = h);
                made.Add(new GameObject("Back to hub").AddComponent<BackToHub>().gameObject);
                hud.StateController.FadeDuration = 0f;
                hud.SetScoreboard(new[] { you, foe }, _ => 0, 3, true);
                yield return TestScenes.WaitUntil(() => hud.ScoreboardReady, Slow, "a bar to score");
                Assert.AreEqual(GameHud.PadScoreboardKey, GameHud.ScoreboardKeyLabel(you.Binding));
                Assert.AreEqual(GameHud.ScoreboardKey, GameHud.ScoreboardKeyLabel(foe.Binding));
                yield return TestScenes.WaitUntil(() => Part(hud, "Scores/Key cap/Key").GetComponent<TMP_Text>().text == GameHud.PadScoreboardKey, Slow, "the button's key cap");

                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Backquote));
                yield return TestScenes.WaitUntil(() => hud.ScoreboardShown, Slow, "the key to show the scoreboard");
                InputSystem.QueueStateEvent(keys, new KeyboardState());
                yield return TestScenes.WaitUntil(() => !hud.ScoreboardShown, Slow, "letting go to hide it");

                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Select));
                yield return TestScenes.WaitUntil(() => hud.ScoreboardShown, Slow, "Select to show the scoreboard");
                yield return null; yield return null;
                Assert.IsFalse(hud.Paused, "Select shows the scores in a match; it does not pause.");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return TestScenes.WaitUntil(() => !hud.ScoreboardShown, Slow, "letting go to hide it");
                Assert.IsFalse(hud.Paused);

                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start));
                yield return TestScenes.WaitUntil(() => hud.Paused, Slow, "Start to pause");
                Assert.IsFalse(hud.ScoreboardShown, "The pause menu never sits under a scoreboard.");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null; yield return null;
            }
            finally
            {
                Time.timeScale = 1f;
                if (keys.added) InputSystem.RemoveDevice(keys);
                if (pad.added) InputSystem.RemoveDevice(pad);
                InputSystem.settings.editorInputBehaviorInPlayMode = route;
                InputSystem.settings.backgroundBehavior = focus;
            }
        }

        [UnityTest]
        public IEnumerator WarmUpSaysSpellingIsFree()
        {
            Ground();
            var you = Spawn(0, 0);
            GameHud hud = null;
            yield return Hud(you, h => hud = h);
            you.Inventory.Set("");
            var tray = hud.transform.Find("Safe HUD/Letter bag");
            var hint = tray.Find("Spell hint").GetComponent<TMP_Text>();
            LetterInventory.FreeSpelling = true;
            string free = GameHud.FreeSpellHint(GameHud.SpellKeyLabel(you.Binding));
            StringAssert.StartsWith(GameHud.FreeSpelling, free);
            yield return TestScenes.WaitUntil(() => hint.gameObject.activeInHierarchy && hint.text == free, Slow, "the free spelling hint");
            hud.ToggleBag();
            yield return TestScenes.WaitUntil(() => hud.BagOpen, Slow, "the bag");
            var book = hud.transform.Find("Safe HUD/Bag panel/Recipe book");
            var line = book.Find("Book line").GetComponent<TMP_Text>();
            yield return TestScenes.WaitUntil(() => line.text == GameHud.FreeBookLine, Slow, "the recipe book to say spelling is free");
            Assert.IsFalse(book.Find("Legend").gameObject.activeSelf, "The free line takes the legend's place.");
            var tile = book.Find("View/Cards/BAT/Tile 1").GetComponent<Image>();
            yield return TestScenes.WaitUntil(() => tile.color.a > 0f, Slow, "every recipe tile to light up");
            Assert.IsFalse(book.Find("View/Cards/BAT/Tile 1/Rim").GetComponent<Image>().enabled);

            LetterInventory.FreeSpelling = false;
            yield return TestScenes.WaitUntil(() => line.text == "" && tile.color.a == 0f, Slow, "the real bag again");
            Assert.IsTrue(book.Find("Legend").gameObject.activeSelf);
            hud.ToggleBag();
            yield return TestScenes.WaitUntil(() => !hud.BagOpen, Slow, "the bag to close");
            yield return TestScenes.WaitUntil(() => !hint.gameObject.activeInHierarchy || hint.text != free, Slow, "the real hint again");
        }

        static IEnumerator Load(string mode, string map)
        {
            Session.SelectMap(map);
            Match.ModeOverride = mode;
            Session.Remember(new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
            yield return TestScenes.ExploreHouse();
            SceneHud.Follow(Joins.Players[0]);
        }

        static IEnumerator Play(HouseRoundDirector director)
        {
            director.CountdownSeconds = .05f;
            director.StartMatch();
            yield return TestScenes.WaitUntil(() => director.Current == HouseRoundDirector.Stage.Playing, Slow, "the match to start");
            foreach (var p in Joins.Players)
            {
                if (p.TryGetComponent<BotController>(out var bot)) bot.enabled = false;
                if (p.Binding is BotBinding binding) binding.Commands = default;
                var rules = Match.Rules.Clone(); rules.SpawnProtectionSeconds = 0f;
                p.Health.UseRules(rules);
            }
        }

        [UnityTest]
        public IEnumerator TeamBattleShowsLivesBetweenTheTeamsAndTheClockMovesToTheCentre()
        {
            yield return Load(HouseModes.War, "pinwheel");
            var director = Object.FindAnyObjectByType<WarDirector>();
            Assert.IsNotNull(director);
            yield return Play(director);
            var hud = SceneHud;
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Score && hud.CentreScore == ScoreLine.Text(director.StartingLives, director.StartingLives), Slow, "lives either side of the clock");
            Assert.AreEqual(WarDirector.LivesCaption, hud.CentreCaption);
            AssertCentreBetween(hud, 4, 5, PlayerBar.SnugCard);
            Assert.LessOrEqual(Bar(hud).rect.width, 900f, "Eight snug cards and the centre stay within about 900 px.");
            var sidePill = hud.transform.Find("Safe HUD/Side column/Timer");
            var sideClock = sidePill.Find("Timer").GetComponent<TMP_Text>();
            yield return TestScenes.WaitUntil(() => hud.CentreClock.Length > 0 && hud.CentreClock == sideClock.text, Slow, "the clock in the centre");
            Assert.IsFalse(sidePill.gameObject.activeSelf, "One clock only: the side pill steps aside.");
            var attacker = Joins.Players.First(p => p.Team == 0);
            var victim = Joins.Players.First(p => p.Team == 1);
            victim.Health.ApplyDamage(Hits.Of(attacker, Vector3.forward, HitSource.Melee, 1000f, 1f));
            yield return TestScenes.WaitUntil(() => director.Score.Lives(1) == director.StartingLives - 1, Slow, "a knockout to cost a life");
            yield return TestScenes.WaitUntil(() => hud.CentreScore == ScoreLine.Text(director.StartingLives, director.StartingLives - 1), Slow, "the score line to follow");
            hud.ToggleScoreboard();
            Assert.IsTrue(hud.ScoreboardShown);
            Assert.AreEqual(2, hud.ScoreboardBlocks);
            Assert.AreEqual(8, hud.ScoreboardLines.Count);
            var board = hud.transform.Find("Safe HUD/Scoreboard");
            Assert.AreEqual(director.StartingLives.ToString(), board.Find("Block 1/Total").GetComponent<TMP_Text>().text);
            Assert.AreEqual((director.StartingLives - 1).ToString(), board.Find("Block 2/Total").GetComponent<TMP_Text>().text);
            Assert.AreEqual(GameHud.YourSide, board.Find("Block 1/Side").GetComponent<TMP_Text>().text, "One human: their side reads as theirs.");
            hud.ToggleScoreboard();
        }

        [UnityTest]
        public IEnumerator SpellTheListPutsTheTargetPictureInTheCentre()
        {
            yield return Load(HouseModes.ShoppingList, "flat");
            var director = ShoppingListDirector.Active;
            Assert.IsNotNull(director);
            director.Seed = 2024;
            yield return Play(director);
            var hud = SceneHud;
            var race = director.Race;
            var you = Joins.Players[0];
            var rival = Joins.Players[1];
            yield return TestScenes.WaitUntil(() => hud.Centre == ScoreCentre.Target && hud.TargetWord == race.Items[0].Word && hud.CentreCaption == race.Items[0].Word, Slow, "your first item as the target");
            Assert.AreEqual(race.Items[0].Word, hud.CentreCaption);
            Assert.AreEqual("", hud.CentreScore, "Four seats keep progress on their cards.");
            Assert.IsNull(hud.LeadingSeat, "Nobody leads before the first item.");
            AssertCentreBetween(hud, 2, 3, PlayerBar.RoomyCard);
            var first = race.Items[0];
            Assert.IsTrue(race.Spelled(rival.Index, first.Word));
            Assert.IsTrue(race.Count(rival.Index, first.Word, first.Act, 1.0));
            yield return TestScenes.WaitUntil(() => hud.LeadingSeat == rival, Slow, "the rival to take the lead");
            Assert.AreEqual(race.Items[0].Word, hud.TargetWord, "Your target is still your own next item.");
            int rivalCard = Enumerable.Range(1, 4).First(i => Name(hud, i) == rival.Name);
            Assert.IsTrue(Leads(hud, rivalCard), "The leader's card is marked.");
            Assert.AreEqual(1, Enumerable.Range(1, 4).Count(i => Leads(hud, i)));
            Assert.IsTrue(race.Spelled(you.Index, first.Word));
            Assert.IsTrue(race.Count(you.Index, first.Word, first.Act, 2.0));
            yield return TestScenes.WaitUntil(() => hud.TargetWord == race.Items[1].Word, Slow, "your next item as the target");
            yield return TestScenes.WaitUntil(() => !hud.LeadingSeat, Slow, "a tie has no leader");
        }
    }
}
