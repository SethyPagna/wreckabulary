using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundWorkshopInputSafetyTests
    {
        GameObject root;
        WordboundWorkshop workshop;
        Gamepad pad, previousPad;
        string folder;
        bool oldTyping;
        WordboundNativeInputScope input;

        [SetUp]
        public void SetUp()
        {
            previousPad = Gamepad.current; oldTyping = DesktopBinding.Typing;
            try
            {
                input = new WordboundNativeInputScope();
                Assert.IsFalse(KeyBindings.GameplayBlocked, "Workshop fixture must start without an inherited blocker: " + WordboundNativeInputScope.Context());
                DesktopBinding.Typing = false; pad = InputSystem.AddDevice<Gamepad>();
                folder = Path.Combine(Path.GetTempPath(), "wordbound-pad-" + Guid.NewGuid().ToString("N"));
                root = new GameObject("Workshop input safety native test");
                workshop = root.AddComponent<WordboundWorkshop>(); workshop.enabled = false;
                workshop.Open(CourseTemplates.FreestyleWorld(), CourseTemplates.ProofCatalogue(),
                    MatchStateValidation.Requirements(ModeDefinition.For("Freestyle")), folder);
                Sample(default, true); Sample(default, true);
            }
            catch (Exception setupError)
            {
                try { TearDown(); }
                catch (Exception cleanupError) { throw new AggregateException(setupError, cleanupError); }
                throw;
            }
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (root) Object.DestroyImmediate(root);
                if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
                if (previousPad != null && previousPad.added) previousPad.MakeCurrent();
                if (folder != null && Path.GetFullPath(folder).StartsWith(Path.Combine(Path.GetTempPath(), "wordbound-pad-"), StringComparison.OrdinalIgnoreCase) && Directory.Exists(folder))
                    Directory.Delete(folder, true);
            }
            finally
            {
                try { input?.Dispose(); }
                finally { input = null; DesktopBinding.Typing = oldTyping; }
            }
        }

        [Test]
        public void ActualUndoPathIgnoresUnfocusedAndHeldResumeInputUntilNeutral()
        {
            string original = workshop.History.Current.Course().Title;
            workshop.History.Execute("Rename", draft => draft.Course.Title = "Pending user edit");
            var held = new GamepadState().WithButton(GamepadButton.LeftShoulder);
            Sample(held, false); Sample(held, true);
            Assert.AreEqual("Pending user edit", workshop.History.Current.Course().Title);
            Sample(default, true); Sample(default, true); Sample(held, true);
            Assert.AreEqual(original, workshop.History.Current.Course().Title, Context());
        }

        [Test]
        public void ActualUndoPathIsSuppressedWhileEditingTextAndAfterPadReplacement()
        {
            workshop.History.Execute("Rename", draft => draft.Course.Title = "Typing edit");
            Set("workshopTyping", true);
            var held = new GamepadState().WithButton(GamepadButton.LeftShoulder);
            Sample(held, true); Set("workshopTyping", false); Sample(held, true);
            Assert.AreEqual("Typing edit", workshop.History.Current.Course().Title);
            InputSystem.RemoveDevice(pad); pad = InputSystem.AddDevice<Gamepad>();
            Sample(held, true);
            Assert.AreEqual("Typing edit", workshop.History.Current.Course().Title);
            Sample(default, true); Sample(default, true); Sample(held, true);
            Assert.AreNotEqual("Typing edit", workshop.History.Current.Course().Title, Context());
        }

        [Test]
        public void UnplugCancelsUncommittedControllerDragWithoutCancellingAnIdleMousePlacement()
        {
            string id = workshop.History.Current.Course().Modules.First().Id;
            workshop.History.Execute("Select", draft => draft.Selection.Add(id));
            string before = workshop.History.Current.CourseJsonText;
            Sample(new GamepadState { leftStick = new Vector2(.9f, 0) }, true);
            Assert.IsTrue(Get<bool>("controllerMoving"), Context());
            InputSystem.RemoveDevice(pad);
            Invoke("ControllerInput", true);
            Assert.IsFalse(Get<bool>("controllerMoving"));
            Assert.AreEqual(before, workshop.History.Current.CourseJsonText);
            Set("placing", "Mouse placement");
            Invoke("ControllerInput", true);
            Assert.AreEqual("Mouse placement", Get<string>("placing"));
        }

        [Test]
        public void ViewOpensThePanelWhereShouldersChangeStepsInsteadOfUndoing()
        {
            string original = workshop.History.Current.Course().Title;
            workshop.History.Execute("Rename", draft => draft.Course.Title = "Redo target");
            workshop.History.Undo();
            Sample(new GamepadState().WithButton(GamepadButton.Select), true);
            Assert.IsTrue(Get<bool>("padPanel"), Context());
            Sample(default, true); Sample(new GamepadState().WithButton(GamepadButton.RightShoulder), true);
            Assert.AreEqual("Objects", Get<object>("step").ToString(), Context());
            Assert.AreEqual(original, workshop.History.Current.Course().Title, "RB changes the step while the panel is open.");
            Sample(default, true); Sample(new GamepadState().WithButton(GamepadButton.LeftShoulder), true);
            Assert.AreEqual("Build", Get<object>("step").ToString());
            Assert.IsTrue(workshop.History.CanRedo);
            Sample(default, true); Sample(new GamepadState().WithButton(GamepadButton.East), true);
            Assert.IsFalse(Get<bool>("padPanel"));
            Sample(default, true); Sample(new GamepadState().WithButton(GamepadButton.RightShoulder), true);
            Assert.AreEqual("Redo target", workshop.History.Current.Course().Title, "Back on the map RB redoes again.");
        }

        [Test]
        public void ARoomChosenInThePanelIsPlacedAtTheViewCentreWithA()
        {
            string definition = workshop.Catalogue.Modules.Keys.OrderBy(id => id, StringComparer.Ordinal).First();
            int rooms = workshop.History.Current.Course().Modules.Count;
            Set("padPanel", true); Set("placing", definition);
            Sample(default, true);
            Assert.IsFalse(Get<bool>("padPanel"), Context());
            Assert.IsTrue(Get<bool>("padAiming"));
            StringAssert.StartsWith("A places", Get<string>("status"));
            Sample(new GamepadState { leftStick = new Vector2(.9f, 0) }, true);
            Assert.AreEqual(rooms, workshop.History.Current.Course().Modules.Count, "Moving the view does not place anything.");
            Sample(default, true); Sample(new GamepadState().WithButton(GamepadButton.South), true);
            var course = workshop.History.Current.Course();
            Assert.AreEqual(rooms + 1, course.Modules.Count, Context());
            var placed = course.Modules.Single(module => workshop.History.Current.Selection.Contains(module.Id));
            Assert.AreEqual(definition, placed.DefinitionId);
            var target = Get<WordboundWorkshopViewport>("view").Target;
            int grid = workshop.History.Current.GridMm;
            Assert.LessOrEqual(Math.Abs(placed.Position.X - target.x * 1000), grid);
            Assert.LessOrEqual(Math.Abs(placed.Position.Z - target.z * 1000), grid);
            Assert.IsNull(Get<string>("placing"));
            Assert.IsFalse(Get<bool>("padAiming"));
        }

        [Test]
        public void AnObjectChosenInThePanelIsPlacedInTheSelectedRoomWithA()
        {
            string room = workshop.History.Current.Course().Modules.First().Id;
            workshop.History.Execute("Select", draft => draft.Selection.Add(room));
            int zones = workshop.History.Current.Course().Zones.Count;
            Set("zoneMode", 0); Set("pool", "STOOL"); Set("zoneId", "");
            Set("padPanel", true); Set("drawingZone", true);
            Sample(default, true);
            Assert.IsTrue(Get<bool>("padAiming"), Context());
            Sample(new GamepadState().WithButton(GamepadButton.South), true);
            var course = workshop.History.Current.Course();
            Assert.AreEqual(zones + 1, course.Zones.Count, Context());
            var zone = course.Zones.Single(item => item.Id == Get<string>("zoneId"));
            Assert.AreEqual("Fixed", zone.Placement);
            Assert.AreEqual(room, zone.ModuleId);
            Assert.IsFalse(Get<bool>("drawingZone"));
        }

        [Test]
        public void ShareDialogClosesWithBOnlyAfterThePadIsNeutral()
        {
            Set("sharing", true);
            Invoke("TrackPadScreen");
            var back = new GamepadState().WithButton(GamepadButton.East);
            SampleDialog(back);
            Assert.IsTrue(Get<bool>("sharing"), "A held button from the editor cannot close the new dialog.");
            Sample(default, true);
            Assert.IsTrue(Get<bool>("sharing"), "The map controls stay ignored behind the dialog.");
            SampleDialog(default); SampleDialog(default); SampleDialog(back);
            Assert.IsFalse(Get<bool>("sharing"), Context());
        }

        [Test]
        public void BOnTheStartScreenCancelsADeleteFirstThenLeavesWithoutSaving()
        {
            int closed = 0; workshop.CloseRequested += () => closed++;
            workshop.ShowStart();
            Assert.IsTrue(workshop.StartOpen);
            Invoke("TrackPadScreen");
            Set("deleteConfirm", "pending");
            var back = new GamepadState().WithButton(GamepadButton.East);
            SampleDialog(back);
            Assert.IsTrue(workshop.StartOpen, "A held button from the editor cannot close the start screen.");
            SampleDialog(default); SampleDialog(default); SampleDialog(back);
            Assert.AreEqual("", Get<string>("deleteConfirm"), Context());
            Assert.IsTrue(workshop.StartOpen);
            SampleDialog(default); SampleDialog(back);
            Assert.IsFalse(workshop.StartOpen, Context());
            Assert.AreEqual(1, closed); Assert.IsFalse(workshop.Visible);
            Assert.IsFalse(Directory.Exists(folder) && Directory.GetFiles(folder, "*" + WorkshopStorage.AutosaveExtension).Any(),
                "Leaving an untouched map writes no autosave.");
        }

        [Test]
        public void AOnTheStartScreenPressesTheFocusedChoiceOnlyAfterTheFirstMove()
        {
            workshop.ShowStart();
            Invoke("TrackPadScreen");
            Set("padTargets", 4); Set("padFocus", 0);
            var press = new GamepadState().WithButton(GamepadButton.South);
            SampleDialog(default); SampleDialog(default); SampleDialog(press);
            Assert.AreEqual(-1, Get<int>("padPress"), "The first input only wakes the focus ring.");
            Assert.IsTrue(Get<bool>("padDialog"), Context());
            SampleDialog(default); SampleDialog(press);
            Assert.AreEqual(0, Get<int>("padPress"), Context());
            Assert.IsFalse(Get<bool>("controllerTools"));
        }

        void SampleDialog(GamepadState state)
        {
            WordboundNativeInputScope.Sample(pad, state);
            Invoke("DialogControllerInput", true);
        }

        void Sample(GamepadState state, bool focused)
        {
            WordboundNativeInputScope.Sample(pad, state);
            Assert.AreSame(pad, Gamepad.current, "Sampled gamepad must be current before ControllerInput.");
            Invoke("ControllerInput", focused);
        }
        string Context() => WordboundNativeInputScope.Context(pad) + "; visible=" + workshop.Visible +
            "; workshopTyping=" + Get<bool>("workshopTyping") + "; sharing=" + Get<bool>("sharing") +
            "; onlineStarting=" + workshop.OnlineStarting + "; canUndo=" + workshop.History.CanUndo +
            "; controllerMoving=" + Get<bool>("controllerMoving") + "; status=" + Get<string>("status");
        T Get<T>(string name) => (T)typeof(WordboundWorkshop).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(workshop);
        void Set(string name, object value) => typeof(WordboundWorkshop).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(workshop, value);
        void Invoke(string name, params object[] args)
        {
            try { typeof(WordboundWorkshop).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(workshop, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
