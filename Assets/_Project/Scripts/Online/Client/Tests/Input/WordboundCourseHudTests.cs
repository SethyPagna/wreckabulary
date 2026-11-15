using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
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
            var tray = Rows(f, "inventoryPanel");
            Assert.AreEqual("Health 100", tray[0]);
            StringAssert.EndsWith("Hand 1: empty", tray[1]); StringAssert.EndsWith("Hand 2: empty", tray[2]);
            Assert.AreEqual(3, tray.Length);
            Assert.IsTrue(f.Progress.TakeKeepsake(Player));
            f.SetFrame();
            f.Grid.Call("PreparePanels");
            Assert.AreEqual("Carrying the keepsake", Rows(f, "inventoryPanel").Last());
            Assert.AreEqual(3, Rows(f, "statusPanel").Length);
            StringAssert.StartsWith("[", Rows(f, "menuHintPanel")[0]);
            StringAssert.Contains("] Spell · [", Rows(f, "menuHintPanel")[0]);
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
