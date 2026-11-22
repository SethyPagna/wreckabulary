using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundCourseHudTests
    {
        const string Player = WordboundRecipeContextFixture.Player;

        static string[] Rows(WordboundRecipeContextFixture f, string panel)
        {
            var value = f.Grid.Get(panel);
            var type = value.GetType();
            var rows = (IList<GUIContent>)type.GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
            int count = (int)type.GetField("count", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
            return rows.Take(count).Select(row => row.text).ToArray();
        }

        static MatchSnapshot Frame(WordboundRecipeContextFixture f) => (MatchSnapshot)f.Grid.Get("frame");

        static bool Flag(WordboundRecipeContextFixture f, string property) => (bool)typeof(WordboundMatchHud)
            .GetProperty(property, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(f.Grid.Hud);

        static void FinishRace(WordboundRecipeContextFixture f)
        {
            for (int lap = 0; lap < 3; lap++)
            {
                while (f.Progress.Team(0).NextCheckpoint < f.Course.Laps[lap].Checkpoints.Count) f.AdvanceCheckpoint();
                if (f.Progress.Team(0).KeepsakeCarrier == null) Assert.IsTrue(f.Progress.TakeKeepsake(Player));
                f.Progress.Advance(f.Progress.Tick + 1, (f.Progress.Tick + 1) * 17,
                    new[] { new ProgressEvent(ProgressEventKind.FinishReached, Player, f.Course.Laps[lap].FinishModuleId, lap) });
            }
            f.SetFrame();
            Assert.AreEqual("Win", Frame(f).Progress.Result.Outcome);
        }

        static void Confirm(WordboundRecipeContextFixture f) =>
            typeof(MatchResult).GetProperty(nameof(MatchResult.Confirmed)).SetValue(Frame(f).Progress.Result, true);

        [Test]
        public void LeaveMatchAsksMidMatchAndBackKeepsPlaying()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun");
            int returns = 0;
            f.Grid.Hud.ReturnRequested += () => returns++;
            f.Grid.Set("initialized", true);
            Assert.IsTrue(Flag(f, "MatchRunning"));
            f.Grid.Call("RequestLeave");
            Assert.AreEqual(0, returns, "Leaving mid-match waits for confirmation.");
            Assert.IsTrue((bool)f.Grid.Get("leaveConfirm"));
            Assert.AreEqual("Leave match", f.Grid.Call("MenuTitle"));
            Assert.AreEqual("leave-cancel", ((WordboundMenuFocus)f.Grid.Get("menuFocus")).Selected);
            f.Grid.Call("ReadMenuCommands", new WordboundMenuCommands(0, false, true));
            Assert.IsFalse((bool)f.Grid.Get("leaveConfirm"));
            Assert.IsTrue(f.Grid.View.MenuOpen, "Back from the question returns to the menu, not the course.");
            Assert.AreEqual("Items", f.Grid.Call("MenuTitle"));
            f.Grid.Call("RequestLeave");
            f.Grid.Call("ConfirmLeave");
            Assert.AreEqual(1, returns);
            Assert.IsFalse((bool)f.Grid.Get("leaveConfirm"));
        }

        [Test]
        public void LeaveIsImmediateAfterAFaultOrAResult()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun");
            int returns = 0;
            f.Grid.Hud.ReturnRequested += () => returns++;
            var controller = (WordboundGameplayController)f.Grid.Get("controller");
            var fault = typeof(WordboundGameplayController).GetProperty("Fault");
            fault.SetValue(controller, "test connection unavailable");
            f.Grid.Call("RequestLeave");
            Assert.AreEqual(1, returns);
            fault.SetValue(controller, "");
            f.Grid.Hud.ShowFailure("Reconnecting…");
            f.Grid.Call("RequestLeave");
            Assert.AreEqual(2, returns);
            f.Grid.Hud.ShowFailure("");
            FinishRace(f);
            f.Grid.Call("RequestLeave");
            Assert.AreEqual(3, returns);
            Assert.IsFalse((bool)f.Grid.Get("leaveConfirm"));
        }

        [Test]
        public void ConfirmedResultOpensResultsOnceAndPlayAgainNeedsAHandler()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun");
            int again = 0;
            FinishRace(f);
            f.Grid.Hud.PlayAgainRequested += () => again++;
            Assert.IsFalse(Flag(f, "PlayAgainAvailable"), "An unconfirmed result offers no Play again.");
            Assert.AreEqual("Confirming result…", f.Grid.Call("ResultText", Frame(f).Progress.Result));
            f.Grid.Call("ShowResultsOnce");
            Assert.IsFalse((bool)f.Grid.Get("resultsShown"));
            Confirm(f);
            Assert.AreEqual("You won!", f.Grid.Call("ResultText", Frame(f).Progress.Result));
            f.Grid.View.SetMenuOpen(false);
            f.Grid.Set("helpOpen", true);
            f.Grid.Call("ShowResultsOnce");
            Assert.IsTrue(f.Grid.View.MenuOpen);
            Assert.AreEqual("Results", f.Grid.Call("MenuTitle"));
            Assert.AreEqual("play-again", ((WordboundMenuFocus)f.Grid.Get("menuFocus")).Selected);
            f.Grid.Call("OpenPage", false, false, false);
            f.Grid.Call("ShowResultsOnce");
            Assert.AreEqual("Items", f.Grid.Call("MenuTitle"), "Results open by themselves only once.");
            Assert.IsTrue(Flag(f, "PlayAgainAvailable"));
            f.Grid.Call("PlayAgain"); f.Grid.Call("PlayAgain");
            Assert.AreEqual(1, again);
            Assert.IsFalse(Flag(f, "PlayAgainAvailable"));
        }

        [Test]
        public void PlayAgainIsHiddenWithoutAHandler()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun");
            FinishRace(f);
            Confirm(f);
            Assert.IsFalse(Flag(f, "PlayAgainAvailable"));
            f.Grid.Call("ShowResultsOnce");
            Assert.AreEqual("return", ((WordboundMenuFocus)f.Grid.Get("menuFocus")).Selected);
        }

        [Test]
        public void IntroCardWaitsForAPlayerActionThenNeverReturnsAndKeepsTheInput()
        {
            bool had = PlayerPrefs.HasKey(WordboundMatchHud.IntroSeenKey);
            int previous = PlayerPrefs.GetInt(WordboundMatchHud.IntroSeenKey, 0);
            try
            {
                PlayerPrefs.DeleteKey(WordboundMatchHud.IntroSeenKey);
                using var f = new WordboundRecipeContextFixture("TimeRun");
                f.Grid.Call("PrepareIntro");
                Assert.IsTrue((bool)f.Grid.Get("introPending"));
                f.Grid.Call("NoteIntroInput", new PlayerCommands { grab = true });
                Assert.IsTrue((bool)f.Grid.Get("introPending"), "The card stays until it has been seen.");
                f.Grid.Set("introShownAt", Time.unscaledTimeAsDouble);
                f.Grid.Call("NoteIntroInput", new PlayerCommands { grab = true });
                Assert.IsTrue((bool)f.Grid.Get("introPending"), "An action in the first seconds does not close it.");
                f.Grid.Set("introShownAt", Time.unscaledTimeAsDouble - 10);
                f.Grid.Call("NoteIntroInput", new PlayerCommands());
                Assert.IsTrue((bool)f.Grid.Get("introPending"), "Walking alone does not close it.");
                f.Grid.Set("notice", "");
                f.Grid.Call("ReadCommands", new PlayerCommands { grab = true });
                Assert.IsFalse((bool)f.Grid.Get("introPending"));
                Assert.AreEqual("Move closer to a letter, object or keepsake.", f.Grid.Get("notice"), "The closing action still reaches the game.");
                Assert.AreEqual(1, PlayerPrefs.GetInt(WordboundMatchHud.IntroSeenKey, 0));
                f.Grid.Call("PrepareIntro");
                Assert.IsFalse((bool)f.Grid.Get("introPending"));
                var lines = (string[])f.Grid.Call("IntroLines");
                Assert.AreEqual(4, lines.Length);
                Assert.AreEqual("Follow the arrow to the next checkpoint.", lines[3]);
                Assert.IsFalse(lines.Any(line => line.IndexOf("craft", StringComparison.OrdinalIgnoreCase) >= 0));
            }
            finally
            {
                if (had) PlayerPrefs.SetInt(WordboundMatchHud.IntroSeenKey, previous);
                else PlayerPrefs.DeleteKey(WordboundMatchHud.IntroSeenKey);
            }
        }

        [Test]
        public void ProgressBecomesToastsAndTheTrayShowsHealthHandsAndKeepsake()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun");
            var before = Frame(f);
            f.AdvanceCheckpoint();
            f.Grid.Call("ObserveProgress", before, Frame(f));
            var toasts = (IList<(string Text, double Until)>)f.Grid.Get("toasts");
            int count = f.Course.Laps[0].Checkpoints.Count;
            Assert.IsTrue(toasts.Any(t => t.Text == "Checkpoint 1/" + count + " reached"), string.Join(" | ", toasts.Select(t => t.Text)));
            for (int i = 0; i < 6; i++) f.Grid.Call("AddToast", "note " + i, 0d);
            Assert.AreEqual(4, toasts.Count);
            Assert.AreEqual("note 5", toasts[3].Text);
            f.Grid.Call("PreparePanels");
            Assert.AreEqual(100, (int)f.Grid.Get("vitalsReading"));
            Assert.AreEqual("100", ((GUIContent)f.Grid.Get("vitalsContent")).text, "The health number sits inside the bar, with no HP word.");
            var hands = (string[])f.Grid.Get("handWords");
            Assert.AreEqual(2, hands.Length);
            Assert.IsNull(hands[0], "Hand 1 is empty."); Assert.IsNull(hands[1], "Hand 2 is empty.");
            Assert.AreEqual("BT", f.Grid.Get("tileText"), "The bag shows the held letters as tiles.");
            Assert.IsFalse((bool)f.Grid.Get("trayKeepsake"));
            Assert.IsTrue(f.Progress.TakeKeepsake(Player));
            f.SetFrame();
            f.Grid.Call("PreparePanels");
            Assert.IsTrue((bool)f.Grid.Get("trayKeepsake"));
            Assert.AreEqual(3, Rows(f, "statusPanel").Length);
            Assert.AreEqual(1, (int)f.Grid.Get("statusTagRow"), "The lap line is the gold tag above the mode title.");
            string spell = PromptText(f, "spellKeys"), menu = PromptText(f, "menuKeys");
            StringAssert.StartsWith("[", spell); StringAssert.EndsWith("] Spell", spell);
            StringAssert.StartsWith("[", menu); StringAssert.EndsWith("] Menu", menu);
        }

        [Test]
        public void OwnHealthIsTheHouseBarWithItsNumberTurningAmberThenRed()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun");
            void Health(float health)
            {
                f.Grid.Set("actor", new MatchActorState(Player, health, 100));
                f.Grid.Call("PreparePanels");
            }
            void Shows(string number, float share, HealthBand band)
            {
                Assert.AreEqual(number, ((GUIContent)f.Grid.Get("vitalsContent")).text);
                Assert.AreEqual(share, (float)f.Grid.Get("vitalsShare"), .001f, number);
                Assert.AreEqual(band, (HealthBand)f.Grid.Get("vitalsBand"), number);
            }
            void Ticks(int count)
            {
                for (int i = 0; i < count; i++) f.Progress.Advance(f.Progress.Tick + 1, (f.Progress.Tick + 1) * 17);
                f.SetFrame();
            }
            Health(100); Shows("100", 1f, HealthBand.Healthy);
            Health(50); Shows("50", .5f, HealthBand.Hurt);
            Health(25); Shows("25", .25f, HealthBand.Critical);
            Health(.3f); Shows("1", .003f, HealthBand.Critical);
            f.Grid.Set("healthMax", 200f);
            Health(100); Shows("100", .5f, HealthBand.Hurt);
            f.Grid.Set("healthMax", 0f);
            Health(40); Shows("40", .4f, HealthBand.Hurt);
            Assert.IsTrue(f.Progress.RecoverPlayer(Player));
            f.SetFrame();
            Health(0); Shows("DOWN 2s", 0f, HealthBand.Critical);
            Ticks(ModeDefinition.TickRate);
            Health(0); Shows("DOWN 1s", 0f, HealthBand.Critical);
            Ticks(ModeDefinition.TickRate);
            Assert.IsFalse(Frame(f).Progress.Players.Single(p => p.Id == Player).Eliminated, "The respawn tick brings the player back.");
            Health(100); Shows("100", 1f, HealthBand.Healthy);
        }

        static string PromptText(WordboundRecipeContextFixture f, string field)
        {
            var prompt = f.Grid.Get(field);
            return (string)prompt.GetType().GetProperty("Text").GetValue(prompt);
        }

        [Test]
        public void PromptsSplitIntoKeyCapsAndWords()
        {
            var type = typeof(WordboundMatchHud).GetNestedType("KeyPrompt", BindingFlags.NonPublic);
            var prompt = Activator.CreateInstance(type, true);
            string[] Parts(string text)
            {
                type.GetMethod("Set").Invoke(prompt, new object[] { text });
                int count = (int)type.GetProperty("Count").GetValue(prompt);
                return Enumerable.Range(0, count).Select(i =>
                {
                    string part = ((GUIContent)type.GetMethod("Part").Invoke(prompt, new object[] { i })).text;
                    return (bool)type.GetMethod("IsCap").Invoke(prompt, new object[] { i }) ? "[" + part + "]" : part;
                }).ToArray();
            }
            CollectionAssert.AreEqual(new[] { "Press", "[E]", "on a letter to collect it." }, Parts("Press [E] on a letter to collect it."));
            CollectionAssert.AreEqual(new[] { "[1]", "and", "[2]", "pick Hand 1 and Hand 2." }, Parts("[1] and [2] pick Hand 1 and Hand 2."));
            CollectionAssert.AreEqual(new[] { "Hold", "[LEFT BUTTON]", "Block" }, Parts("Hold [LEFT BUTTON] Block"));
            CollectionAssert.AreEqual(new[] { "[]]", "Spell" }, Parts("[]] Spell"));
            CollectionAssert.AreEqual(new[] { "Checkpoint 1/32 reached" }, Parts("Checkpoint 1/32 reached"));
            CollectionAssert.AreEqual(new[] { "Open [ the box" }, Parts("Open [ the box"));
            Assert.IsFalse((bool)type.GetProperty("HasCaps").GetValue(prompt));
            CollectionAssert.IsEmpty(Parts(""));
            CollectionAssert.IsEmpty(Parts(null));
        }

        [Test]
        public void HandSlotsShowTheHeldItemFromTheCurrentSnapshot()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun", flagship: true);
            f.SetFrame(heldTool: true);
            var held = Frame(f).Economy.Players.Single(p => p.PlayerId == 0);
            f.Grid.Set("inventory", held);
            f.Grid.Call("PreparePanels");
            var hands = (string[])f.Grid.Get("handWords");
            Assert.AreEqual(1, hands.Count(word => word == "HAMMER"), string.Join(",", hands.Select(word => word ?? "-")));
            for (int slot = 0; slot < hands.Length; slot++)
                Assert.AreEqual(((ItemInstance)f.Grid.Call("HeldItem", slot))?.Word, hands[slot], "slot " + slot);
            f.SetFrame();
            f.Grid.Call("PreparePanels");
            Assert.IsTrue(((string[])f.Grid.Get("handWords")).All(word => word == null), "A new snapshot without the item empties the hand.");
        }

        static string Text(WordboundRecipeContextFixture f, string content) => ((GUIContent)f.Grid.Get(content)).text;

        [Test]
        public void TwoTeamsShowBothCountsAndTheLeaderWhileOneTeamShowsOnlyTheClock()
        {
            using var race = new WordboundRecipeContextFixture("TeamRace", flagship: true);
            int stops = race.Course.Laps.Sum(lap => lap.Checkpoints.Count + 1);
            race.Grid.Call("RefreshScore");
            Assert.IsTrue((bool)race.Grid.Get("scoreSidesShown"));
            Assert.IsTrue((bool)race.Grid.Get("scoreClockShown"));
            Assert.AreEqual("0/" + stops, Text(race, "scoreMine"));
            Assert.AreEqual("0/" + stops, Text(race, "scoreTheirs"));
            Assert.AreEqual(Wreckabulary.Rules.ScoreLine.NoLead, (int)race.Grid.Get("scoreLead"));
            race.AdvanceCheckpoint("recipe-rival");
            race.Grid.Call("RefreshScore");
            Assert.AreEqual("0/" + stops, Text(race, "scoreMine"));
            Assert.AreEqual("1/" + stops, Text(race, "scoreTheirs"));
            Assert.AreEqual(Wreckabulary.Rules.ScoreLine.Right, (int)race.Grid.Get("scoreLead"));
            race.AdvanceCheckpoint(); race.AdvanceCheckpoint();
            race.Grid.Call("RefreshScore");
            Assert.AreEqual("2/" + stops, Text(race, "scoreMine"));
            Assert.AreEqual(Wreckabulary.Rules.ScoreLine.Left, (int)race.Grid.Get("scoreLead"));
            using var solo = new WordboundRecipeContextFixture("TimeRun");
            solo.Grid.Call("RefreshScore");
            Assert.IsFalse((bool)solo.Grid.Get("scoreSidesShown"));
            Assert.IsTrue((bool)solo.Grid.Get("scoreClockShown"));
            Assert.AreEqual("25:00", Text(solo, "scoreClock"));
            solo.Grid.Call("PreparePanels");
            Assert.AreEqual("Time Trial", Rows(solo, "statusPanel")[0], "The clock lives in the top strip, not in the heading.");
            using var free = new WordboundRecipeContextFixture("Freestyle");
            free.Grid.Call("RefreshScore");
            Assert.IsFalse((bool)free.Grid.Get("scoreSidesShown"));
            Assert.IsFalse((bool)free.Grid.Get("scoreClockShown"));
        }

        [Test]
        public void ARunIsTimedOnceFromItsFirstTickAndTheEditorNeverWritesTheCareer()
        {
            bool remember = LobbyMenu.RememberChoices;
            string career = PlayerPrefs.GetString(MatchTally.CareerKey, "");
            try
            {
                LobbyMenu.RememberChoices = false;
                using var f = new WordboundRecipeContextFixture("TimeRun");
                f.Grid.Call("NoteRunStarted", Frame(f));
                f.Grid.Call("RecordRunTime");
                Assert.IsFalse((bool)f.Grid.Get("runTimed"), "A run that never started records nothing.");
                f.AdvanceCheckpoint();
                f.Grid.Call("NoteRunStarted", Frame(f));
                Assert.Greater((float)f.Grid.Get("runStartedAt"), 0f);
                f.Grid.Call("RecordRunTime");
                Assert.IsTrue((bool)f.Grid.Get("runTimed"));
                f.Grid.Call("NoteRunStarted", Frame(f));
                Assert.AreEqual(0f, (float)f.Grid.Get("runStartedAt"), "A run is timed once.");
                Assert.AreEqual(career, PlayerPrefs.GetString(MatchTally.CareerKey, ""));
            }
            finally { LobbyMenu.RememberChoices = remember; }
        }

        [Test]
        public void ArrowTargetFollowsTheCurrentFrameAndFreeplayGetsAGuidanceLine()
        {
            using var race = new WordboundRecipeContextFixture("TimeRun");
            var step = (CourseStep)race.Grid.Call("CurrentStep");
            Assert.IsTrue(step.HasTarget, step.Text);
            Assert.AreSame(Frame(race), race.Grid.Get("stepFrame"));
            race.AdvanceCheckpoint();
            race.Grid.Call("CurrentStep");
            Assert.AreSame(Frame(race), race.Grid.Get("stepFrame"));
            StringAssert.EndsWith(" m", ((string)race.Grid.Call("DistanceLabel")).Split('·')[0].Trim());
            using var free = new WordboundRecipeContextFixture("Freestyle");
            free.Grid.Call("PreparePanels");
            var status = Rows(free, "statusPanel");
            Assert.AreEqual(2, status.Length, string.Join(" | ", status));
            Assert.AreEqual("Walk to the nearest practice station.", status[1]);
            Assert.AreEqual("Follow the arrow to a practice station.", ((string[])free.Grid.Call("IntroLines"))[3]);
        }
    }
}
