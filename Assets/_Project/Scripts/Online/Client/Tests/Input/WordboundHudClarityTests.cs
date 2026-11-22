using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundHudClarityTests
    {
        [Test]
        public void ActionNoticeExpiresWithoutExpiringConnectionFailure()
        {
            var root = new GameObject("HUD notice policy test");
            try
            {
                var controller = root.AddComponent<WordboundGameplayController>();
                var view = root.AddComponent<WordboundPlayerView>();
                view.Initialize(controller, root.AddComponent<Camera>());
                var hud = root.AddComponent<WordboundMatchHud>();
                Field("controller").SetValue(hud, controller);
                Field("view").SetValue(hud, view);
                Field("returnLabel").SetValue(hud, "Return to workshop");
                double before = Time.unscaledTimeAsDouble;
                Invoke(hud, "SetNotice", "Move closer and keep the object in sight.");
                double after = Time.unscaledTimeAsDouble;
                double expiry = (double)Field("noticeUntil").GetValue(hud);
                Assert.That(expiry, Is.InRange(before + 4, after + 4), "A notice lasts four seconds from when it is set.");
                Assert.AreEqual("Move closer and keep the object in sight.", Invoke(hud, "TransientNotice", expiry - .001));
                Assert.AreEqual("", Invoke(hud, "TransientNotice", expiry));
                Assert.AreEqual("", Invoke(hud, "TransientNotice", expiry + 100));
                hud.ShowFailure("Reconnecting…");
                Assert.AreEqual("Reconnecting…", Invoke(hud, "ContextPrompt"));
                Assert.AreEqual("", Invoke(hud, "TransientNotice", expiry + 100));
                Assert.AreEqual("Reconnecting…", Invoke(hud, "ContextPrompt"));
                hud.ShowFailure("");
                Assert.AreEqual("Preparing the world…", Invoke(hud, "ContextPrompt"));
                typeof(WordboundGameplayController).GetProperty("Fault").SetValue(controller, "test connection unavailable");
                Assert.AreEqual("Connection interrupted · Return to workshop", Invoke(hud, "ContextPrompt"));
                Invoke(hud, "SetNotice", "");
                Assert.AreEqual("Connection interrupted · Return to workshop", Invoke(hud, "ContextPrompt"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void HelpBackReturnsToEquipmentBeforeResuming()
        {
            var root = new GameObject("HUD help back test");
            try
            {
                var controller = root.AddComponent<WordboundGameplayController>();
                var view = root.AddComponent<WordboundPlayerView>();
                view.Initialize(controller, root.AddComponent<Camera>()); view.SetMenuOpen(true);
                var hud = root.AddComponent<WordboundMatchHud>();
                Field("controller").SetValue(hud, controller); Field("view").SetValue(hud, view);
                Field("initialized").SetValue(hud, true); Field("helpOpen").SetValue(hud, true);
                Invoke(hud, "ReadMenuCommands", new WordboundMenuCommands(0, false, true));
                Assert.IsFalse((bool)Field("helpOpen").GetValue(hud));
                Assert.IsTrue(view.MenuOpen);
                Assert.AreEqual("help", ((WordboundMenuFocus)Field("menuFocus").GetValue(hud)).Selected);
                Invoke(hud, "ReadMenuCommands", new WordboundMenuCommands(0, false, true));
                Assert.IsFalse(view.MenuOpen);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void OptionalHelpReadsActualDesktopAndControllerBindings()
        {
            var root = new GameObject("HUD help binding test");
            Gamepad prior = Gamepad.current, pad = null;
            try
            {
                var view = root.AddComponent<WordboundPlayerView>();
                view.Initialize(root.AddComponent<WordboundGameplayController>(), root.AddComponent<Camera>());
                var hud = root.AddComponent<WordboundMatchHud>(); Field("view").SetValue(hud, view);
                var binding = (WordboundInputBinding)typeof(WordboundPlayerView).GetField("binding", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                foreach (bool usePad in new[] { false, true })
                {
                    if (usePad)
                    {
                        pad = InputSystem.AddDevice<Gamepad>();
                        typeof(WordboundInputBinding).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(binding, pad);
                    }
                    var rows = (string[])Invoke(hud, "HelpControls");
                    Assert.AreEqual(9, rows.Length);
                    Assert.That(rows[0], Does.StartWith("[" + view.ControlHint(WordboundControl.Interact) + "]"));
                    Assert.That(rows[1], Does.StartWith("[" + view.ControlHint(WordboundControl.Attack) + "]"));
                    Assert.That(rows[2], Does.StartWith("[" + view.ControlHint(WordboundControl.Spell) + "] Spell"));
                    Assert.That(rows[3], Does.Contain("[" + view.ControlHint(WordboundControl.Aim) + "]"));
                    Assert.That(rows[4], Does.Contain("[" + view.ControlHint(WordboundControl.Drop) + "]"));
                    string first = (string)Invoke(hud, "HandKey", 0), second = (string)Invoke(hud, "HandKey", 1);
                    Assert.AreEqual(usePad ? "D-PAD " + ControlHints.PadHand(0) : ControlHints.KeyOf(DesktopBinding.Shared.Hand1), first);
                    Assert.AreEqual(first.Length == 0 ? "Hand 1 / Hand 2" : "[" + first + "] / [" + second + "] Hand 1 / Hand 2", rows[5]);
                    Assert.That(rows[6], Does.StartWith("[" + view.ControlHint(WordboundControl.CycleGear) + "] Swap hands"));
                    Assert.That(rows[7], Does.Contain("[" + view.ControlHint(WordboundControl.Block) + "]"));
                    Assert.That(rows[8], Does.StartWith("[" + view.ControlHint(WordboundControl.Menu) + "]"));
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                if (prior != null && prior.added) prior.MakeCurrent();
            }
        }

        [Test]
        public void UnknownBindingShowsConciseActionsWithoutInventedKeysOrDuplicatedLabels()
        {
            var root = new GameObject("Unknown HUD hint test");
            try
            {
                var view = root.AddComponent<WordboundPlayerView>(); view.enabled = false;
                view.Initialize(root.AddComponent<WordboundGameplayController>(), root.AddComponent<Camera>(), new ScriptedBinding());
                var hud = root.AddComponent<WordboundMatchHud>(); Field("view").SetValue(hud, view);
                Assert.AreEqual("Spell", Invoke(hud, "Hint", WordboundControl.Spell, "Spell"));
                Assert.AreEqual("Menu", Invoke(hud, "Hint", WordboundControl.Menu, "Menu"));
                Assert.AreEqual("Smash furniture", Invoke(hud, "Hint", WordboundControl.Attack, "Smash furniture"));
                CollectionAssert.AreEqual(new[] { "Collect a letter, pick up or use", "Smash", "Spell an item", "Throw", "Hold Drop",
                    "Hand 1 / Hand 2", "Swap hands", "Hold Block with a shield", "Resume / menu" }, (string[])Invoke(hud, "HelpControls"));
                Assert.AreEqual("Collect each letter by hand.", ((string[])Invoke(hud, "IntroLines"))[0]);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void LetterTilesShowEveryLetterUpToTheLimitThenCountTheRest()
        {
            Assert.AreEqual((0, 0), ((int, int))Static("TileSplit", 0, 20));
            Assert.AreEqual((3, 0), ((int, int))Static("TileSplit", 3, 20));
            Assert.AreEqual((20, 0), ((int, int))Static("TileSplit", 20, 20));
            Assert.AreEqual((19, 7), ((int, int))Static("TileSplit", 26, 20));
            Assert.AreEqual((0, 5), ((int, int))Static("TileSplit", 5, 0));
        }

        [Test]
        public void ContextCopyDistinguishesUsePickupLetterAndKeepsake()
        {
            Assert.AreEqual("Collect letter", Static("InteractionCaption", WorldBodyKind.Tile, null, false));
            Assert.AreEqual("Take keepsake", Static("InteractionCaption", WorldBodyKind.Keepsake, null, false));
            Assert.AreEqual("Use FAN", Static("InteractionCaption", WorldBodyKind.Item, "FAN", true));
            Assert.AreEqual("Pick up FAN", Static("InteractionCaption", WorldBodyKind.Item, "FAN", false));
            Assert.AreEqual("", Static("InteractionCaption", WorldBodyKind.Item, null, false));
        }

        [Test]
        public void RaceStatusShowsTheGoalUnderTheLapLineWithoutRepeatingVitals()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun", flagship: true);
            for (int step = 0; step < 2; step++)
            {
                f.Grid.Call("PreparePanels");
                var rows = PanelRows(f.Grid.Get("statusPanel"));
                var team = ((MatchSnapshot)f.Grid.Get("frame")).Progress.Teams.Single();
                Assert.AreEqual(3, rows.Length, string.Join(" | ", rows));
                StringAssert.StartsWith("Time Trial", rows[0]);
                StringAssert.StartsWith("Lap 1/3 · Checkpoint " + (step + 1) + "/", rows[1]);
                Assert.AreEqual(f.Grid.Call("ObjectiveText", team), rows[2]);
                Assert.IsFalse(rows.Any(row => row.Contains("Health") || row.Contains("Stamina")));
                f.AdvanceCheckpoint();
            }
            StringAssert.Contains("TABLE", PanelRows(f.Grid.Get("statusPanel"))[2]);
        }

        [Test]
        public void MissingLettersFollowTheRecipeSpellingInsteadOfTheAlphabet()
        {
            Assert.AreEqual("BAT", Static("MissingInWordOrder", "BAT", LetterBag.FromWord("ABT")));
            Assert.AreEqual("TE", Static("MissingInWordOrder", "TABLE", LetterBag.FromWord("ET")));
            Assert.AreEqual("HMR", Static("MissingInWordOrder", "HAMMER", LetterBag.FromWord("HMR")));
            Assert.AreEqual("MM", Static("MissingInWordOrder", "HAMMER", LetterBag.FromWord("MM")));
            using var f = new WordboundRecipeGridFixture();
            var card = new RecipeCard(new ItemDefinition { Id = "BAT", Letters = LetterBag.FromWord("BAT") }, LetterBag.FromWord("ABT"));
            Assert.AreEqual("BAT · Need BAT", f.Call("RecipeDetail", card));
        }

        static string[] PanelRows(object panel)
        {
            var type = panel.GetType();
            var rows = (System.Collections.Generic.IList<GUIContent>)type.GetField("rows", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            int count = (int)type.GetField("count", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(panel);
            return rows.Take(count).Select(row => row.text).ToArray();
        }

        [TestCase(1280, 720), TestCase(1920, 1080), TestCase(854, 480), TestCase(1248, 576)]
        public void ActualOverlayPlacesTheHousePiecesWithoutOverlap(int pixelsWide, int pixelsHigh)
        {
            using var f = new WordboundRecipeContextFixture("TimeRun");
            OverlayWindow window = null;
            try
            {
                f.Grid.Set("initialized", true);
                f.Grid.View.SetMenuOpen(false);
                f.Grid.Call("PreparePanels");
                float scale = Mathf.Clamp(Mathf.Min(pixelsWide / 1280f, pixelsHigh / 720f), .6f, 1.8f);
                window = ScriptableObject.CreateInstance<OverlayWindow>(); window.Hud = f.Grid.Hud;
                window.Size = new Vector2(pixelsWide / scale, pixelsHigh / scale);
                window.position = new Rect(100, 100, 640, 480); window.ShowUtility();
                window.SendEvent(new Event { type = EventType.Layout }); window.SendEvent(new Event { type = EventType.Repaint });
                Assert.IsNull(window.Failure); Assert.Greater(window.Repaints, 0);
            }
            finally
            {
                if (window) window.Close();
                f.Grid.View.SetMenuOpen(true);
            }
        }

        static FieldInfo Field(string name) => typeof(WordboundMatchHud).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        static object Invoke(WordboundMatchHud hud, string method, params object[] args) => typeof(WordboundMatchHud)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, args);
        static object Static(string method, params object[] args) => typeof(WordboundMatchHud)
            .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);

        sealed class OverlayWindow : EditorWindow
        {
            public WordboundMatchHud Hud;
            public Vector2 Size;
            public Exception Failure;
            public int Repaints;
            void OnGUI()
            {
                if (!Hud) return;
                try
                {
                    var before = GUI.backgroundColor;
                    Invoke(Hud, "DrawOverlay", Size.x, Size.y);
                    Assert.AreEqual(before, GUI.backgroundColor);
                    if (Event.current.type != EventType.Repaint) return;
                    Repaints++;
                    var plate = (Rect)Field("plateRect").GetValue(Hud);
                    var vitals = (Rect)Field("vitalsRect").GetValue(Hud);
                    var tray = (Rect)Field("trayRect").GetValue(Hud);
                    var action = (Rect)Field("actionRect").GetValue(Hud);
                    var pieces = new[] { ("title", plate), ("health", vitals), ("bag", tray), ("prompt", action) };
                    foreach (var (name, rect) in pieces)
                    {
                        Assert.Greater(rect.width, 0, name + " is drawn.");
                        Assert.GreaterOrEqual(rect.xMin, 18 - .5f, name); Assert.LessOrEqual(rect.xMax, Size.x - 18 + .5f, name);
                        Assert.GreaterOrEqual(rect.yMin, 18 - .5f, name); Assert.LessOrEqual(rect.yMax, Size.y - 18 + .5f, name);
                    }
                    for (int i = 0; i < pieces.Length; i++)
                        for (int j = i + 1; j < pieces.Length; j++)
                            Assert.IsFalse(pieces[i].Item2.Overlaps(pieces[j].Item2), pieces[i].Item1 + " overlaps " + pieces[j].Item1);
                    Assert.LessOrEqual(action.yMax, tray.yMin, "The prompt sits above the bag.");
                    Assert.AreEqual(Size.y - 18, tray.yMax, .5f, "The bag rests on the bottom margin.");
                    Assert.AreEqual(Size.x * .5f, tray.center.x, 1f, "The bag is centred like the house HUD.");
                }
                catch (Exception failure) { Failure = failure; }
            }
        }
    }
}
