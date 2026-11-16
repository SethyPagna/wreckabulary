using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class TouchControlsTests
    {
        TouchBinding touch;
        string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Application.temporaryCachePath, "touch-controls-" + System.Guid.NewGuid().ToString("N"));
            TouchLayouts.Folder = folder;
            TouchLayouts.Reload();
            touch = TouchBinding.Shared;
            touch.ReleaseAll();
            touch.Enabled = true;
            touch.OverlayDesktop = false;
            touch.OverlayBindingId = null;
        }

        [TearDown]
        public void TearDown()
        {
            touch.ReleaseAll();
            touch.Enabled = touch.OverlayDesktop = false;
            touch.OverlayBindingId = null;
            TouchLayouts.Folder = null;
            TouchLayouts.Reload();
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        static PointerEventData Pointer(Vector2 position, int id = 3) => new PointerEventData(EventSystem.current) { pointerId = id, position = position };

        static Vector2 ScreenOf(Transform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.position);

        static void Tap(TouchActionButton button)
        {
            var at = ScreenOf(button.transform);
            button.OnPointerDown(Pointer(at));
            button.OnPointerUp(Pointer(at));
        }

        [Test]
        public void HandButtonsPickAHandOnceThroughTheBinding()
        {
            touch.PulseHand(2);
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.AreEqual(2, command.slot);
            command = default;
            touch.Read(ref command);
            Assert.AreEqual(0, command.slot, "A tap picks the hand once.");
            touch.PulseHand(1);
            var merged = new PlayerCommands { attack = true };
            touch.Merge(ref merged);
            Assert.AreEqual(1, merged.slot);
            Assert.IsTrue(merged.attack);
            touch.PulseHand(3);
            command = default;
            touch.Read(ref command);
            Assert.AreEqual(0, command.slot, "Only hands 1 and 2 exist.");
            touch.PulseHand(1);
            touch.ReleaseAll();
            command = default;
            touch.Read(ref command);
            Assert.AreEqual(0, command.slot);
        }

        [Test]
        public void AButtonKeepsItsLayoutSizeAndOpacityThroughPressAndFade()
        {
            var host = new GameObject("Layout button", typeof(RectTransform), typeof(Image));
            var button = host.AddComponent<TouchActionButton>();
            button.action = TouchAction.Jump;
            button.Scale = 1.3f;
            button.Opacity = .5f;
            var group = host.GetComponent<CanvasGroup>();
            Assert.AreEqual(1.3f, host.transform.localScale.x, 1e-5f);
            Assert.AreEqual(.5f, group.alpha, 1e-5f);
            button.OnPointerDown(Pointer(Vector2.zero));
            Assert.AreEqual(1.3f * TouchActionButton.PressedScale, host.transform.localScale.x, 1e-5f, "The press squash is relative to the layout size.");
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.IsTrue(command.jump);
            button.OnPointerUp(Pointer(Vector2.zero));
            Assert.AreEqual(1.3f, host.transform.localScale.x, 1e-5f);
            button.SetAvailable(false);
            Assert.AreEqual(.5f * TouchActionButton.UnavailableAlpha, group.alpha, 1e-5f);
            button.Opacity = 1f;
            Assert.AreEqual(TouchActionButton.UnavailableAlpha, group.alpha, 1e-5f);
            button.SetAvailable(true);
            Assert.AreEqual(1f, group.alpha, 1e-5f);
            Object.DestroyImmediate(host);
        }

        [UnityTest]
        public IEnumerator EveryLayoutPartIsAHudControlPlacedByTheLayout()
        {
            yield return TestScenes.Reset();
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, touch);
            var hud = new GameObject("Touch layout HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(true);
            yield return new WaitForSecondsRealtime(.12f);
            var root = (RectTransform)hud.transform.Find("Safe HUD/Touch controls");
            var view = root.GetComponent<TouchLayoutView>();
            Assert.IsNotNull(view, "GameHud attaches the layout view to its touch controls.");
            var size = root.rect.size;
            float side = Mathf.Min(size.x, size.y);
            var classic = TouchLayout.Classic(size.x, size.y);
            foreach (var part in TouchLayout.Parts)
            {
                var rect = view.Part(part);
                if (part == TouchPart.Mic || part == TouchPart.Speaker)
                {
                    Assert.IsFalse(TouchLayoutView.InHouse(part));
                    Assert.IsNull(rect, part + " is a voice control and never exists in House play.");
                    Assert.IsNull(root.Find(TouchLayout.Label(part)));
                    continue;
                }
                Assert.IsNotNull(rect, part + " has a HUD control");
                Assert.AreSame(root, rect.parent);
                Assert.AreEqual(TouchLayoutView.HudName(part), rect.name);
                Assert.AreEqual(classic[part].X, rect.anchorMin.x, 1e-4f, part.ToString());
                Assert.AreEqual(classic[part].Y, rect.anchorMin.y, 1e-4f, part.ToString());
                Assert.AreEqual(classic[part].Size * side, rect.sizeDelta.x * rect.localScale.x, .05f, part + " diameter");
            }
            foreach (var part in new[] { TouchPart.Book, TouchPart.Hand1, TouchPart.Hand2, TouchPart.Pause })
                Assert.IsNotNull(view.Part(part).GetComponent<TouchActionButton>(), part.ToString());
            Assert.IsNotNull(view.Part(TouchPart.Eye).GetComponent<TouchEyeButton>());
            Assert.AreEqual(1f, view.Part(TouchPart.Eye).GetComponent<CanvasGroup>().alpha, 1e-5f);
            Assert.IsNull(root.Find("SWAP"), "Hands are picked with HAND 1 and HAND 2, not a swap button.");

            Tap(view.Part(TouchPart.Hand2).GetComponent<TouchActionButton>());
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.AreEqual(2, command.slot);

            var book = view.Part(TouchPart.Book).GetComponent<TouchActionButton>();
            Tap(book);
            Assert.IsTrue(hud.BagOpen, "BOOK opens the bag and recipe book.");
            Tap(book);
            Assert.IsFalse(hud.BagOpen);

            Tap(view.Part(TouchPart.Pause).GetComponent<TouchActionButton>());
            yield return null;
            Assert.IsTrue(hud.Paused, "PAUSE opens the pause card.");
            hud.TogglePause();
            yield return new WaitForSecondsRealtime(.3f);
            Assert.IsFalse(hud.Paused);

            var faded = TouchLayouts.Book.Clone();
            faded.Store(TouchTemplate.Custom1, classic.With(TouchPart.Move, classic[TouchPart.Move].Faded(.5f)).WithOpacity(.8f));
            faded.Active = TouchTemplate.Custom1;
            Assert.IsTrue(TouchLayouts.Commit(faded, out string error), error);
            yield return null;
            Assert.AreEqual(.4f, view.Part(TouchPart.Move).GetComponent<CanvasGroup>().alpha, 1e-4f, "Button opacity times layout opacity.");
            Assert.AreEqual(.8f, view.Part(TouchPart.Aim).GetComponent<CanvasGroup>().alpha, 1e-4f);
            Assert.AreEqual(.8f, view.Part(TouchPart.Pause).GetComponent<CanvasGroup>().alpha, 1e-4f);
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
            yield return TestScenes.Reset();
        }

        [UnityTest]
        public IEnumerator TheEyeOrbitsTheCameraWhileTheMoveStickKeepsItsFrame()
        {
            yield return TestScenes.Reset();
            var lens = new GameObject("Main Camera").AddComponent<Camera>();
            lens.tag = "MainCamera";
            lens.transform.SetPositionAndRotation(new Vector3(0f, 12f, -9f), Quaternion.Euler(50f, 0f, 0f));
            var rig = lens.gameObject.AddComponent<CameraRig>();
            rig.FrameLayout(GameConfig.Current.HouseFor("pinwheel"));
            var hud = new GameObject("Touch eye HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(true);
            yield return null;
            yield return null;
            var view = hud.transform.Find("Safe HUD/Touch controls").GetComponent<TouchLayoutView>();
            var steady = rig.ScreenDirectionToWorld(Vector2.up);
            var eye = view.Part(TouchPart.Eye).GetComponent<TouchEyeButton>();
            var at = ScreenOf(eye.transform);
            eye.OnPointerDown(Pointer(at, 5));
            Assert.IsTrue(rig.Dragging && rig.Looking);
            Assert.Less(eye.transform.localScale.x, eye.Scale, "The eye squashes while held.");
            float quarter = Screen.height * .25f;
            eye.OnDrag(new PointerEventData(EventSystem.current) { pointerId = 5, position = at + new Vector2(quarter, 0f), delta = new Vector2(quarter, 0f) });
            Assert.AreEqual(CameraRig.DragDegrees(quarter), rig.OrbitYaw, 1f, "Dragging the eye turns the view.");
            Assert.Greater(Mathf.Abs(rig.OrbitYaw), 45f);
            yield return null;

            var stick = view.Part(TouchPart.Move);
            var centre = ScreenOf(stick);
            float reach = stick.rect.height * stick.lossyScale.y * .3f;
            var stickInput = stick.GetComponent<TouchStick>();
            stickInput.OnPointerDown(Pointer(centre + Vector2.up * reach, 6));
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.Greater(command.move.magnitude, .5f);
            Assert.Less(Vector2.Angle(steady, command.move), 2f, "Pushing up still walks the steady forward while the view is orbited.");
            stickInput.OnPointerUp(Pointer(centre, 6));

            eye.OnPointerUp(Pointer(at, 5));
            Assert.IsFalse(rig.Dragging);
            Assert.AreEqual(eye.Scale, eye.transform.localScale.x, 1e-5f);
            Object.Destroy(hud.gameObject);
            Object.Destroy(lens.gameObject);
            yield return TestScenes.Reset();
        }

        [Test]
        public void MovementAimAndHeldSkillCoexistWithoutRepeatedPresses()
        {
            touch.SetMove(new Vector2(5f, 5f));
            touch.SetLook(Vector2.left);
            touch.SetHeld(TouchAction.Block, true);
            touch.SetHeld(TouchAction.Attack, true);
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.That(command.move.magnitude, Is.EqualTo(1f).Within(0.0001f));
            Assert.AreEqual(Vector2.left, command.look);
            Assert.IsTrue(command.attack);
            Assert.IsTrue(command.blockHeld);
            command = default;
            touch.Read(ref command);
            Assert.IsFalse(command.attack, "A held press must not fire every frame.");
            Assert.IsTrue(command.blockHeld, "Another button must not release the shield.");
            Assert.AreEqual(Vector2.left, command.look);
            touch.SetHeld(TouchAction.Block, false);
            command = default;
            touch.Read(ref command);
            Assert.IsFalse(command.blockHeld);
        }

        [Test]
        public void ReleaseAllClearsBothSticksHeldReviveAndCraftMenu()
        {
            touch.SetMove(Vector2.up);
            touch.SetLook(Vector2.right);
            touch.SetHeld(TouchAction.Grab, true);
            touch.SetHeld(TouchAction.Block, true);
            touch.SetCraftOpen(true);
            touch.ReleaseAll();
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.AreEqual(Vector2.zero, command.move);
            Assert.AreEqual(Vector2.zero, command.look);
            Assert.IsFalse(command.grab || command.grabHeld || command.blockHeld);
            Assert.IsFalse(command.spellDown || command.spellHeld || command.spellUp);
        }

        [Test]
        public void CraftMenuStaysOpenAfterTapAndCancellingDoesNotConfirm()
        {
            touch.SetCraftOpen(true);
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.IsTrue(command.spellDown && command.spellHeld);
            command = default;
            touch.Read(ref command);
            Assert.IsFalse(command.spellDown);
            Assert.IsTrue(command.spellHeld);
            touch.SetCraftOpen(false);
            command = default;
            touch.Read(ref command);
            Assert.IsFalse(command.spellHeld || command.spellUp, "Touch BUILD is explicit; cancelling cannot spend letters.");
        }

        [Test]
        public void TouchOverlayPreservesKeyboardCommandsAndOverridesOnlyActiveSticks()
        {
            var command = new PlayerCommands { move = Vector2.up, attack = true, aimAtPointer = true };
            touch.SetHeld(TouchAction.Jump, true);
            touch.Merge(ref command);
            Assert.AreEqual(Vector2.up, command.move);
            Assert.IsTrue(command.attack && command.jump && command.aimAtPointer);
            touch.SetLook(Vector2.right);
            touch.Merge(ref command);
            Assert.AreEqual(Vector2.right, command.look);
            Assert.IsFalse(command.aimAtPointer);
        }

        [Test]
        public void StartSignalSurvivesThePlayersReadForTheRoundDirector()
        {
            touch.Pulse(TouchAction.Start);
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.IsTrue(command.start);
            Assert.IsTrue(touch.StartPressed());
        }

        [UnityTest]
        public IEnumerator DropRequiresAQuarterSecondHoldAndFiresOnlyOnce()
        {
            touch.SetHeld(TouchAction.Drop, true);
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.IsFalse(command.drop);
            yield return new WaitForSecondsRealtime(InputBinding.DropHoldSeconds + 0.05f);
            command = default;
            touch.Read(ref command);
            Assert.IsTrue(command.drop);
            command = default;
            touch.Read(ref command);
            Assert.IsFalse(command.drop);
            touch.SetHeld(TouchAction.Drop, false);
            touch.Read(ref command);
            touch.SetHeld(TouchAction.Drop, true);
            command = default;
            touch.Read(ref command);
            Assert.IsFalse(command.drop, "A new press starts a new hold.");
        }

        [Test]
        public void SwapHasMatchingDesktopAndScriptedEdges()
        {
            Assert.IsNull(DesktopBinding.Shared.Map.FindAction("Place"));
            Assert.AreEqual("<Keyboard>/1", DesktopBinding.Shared.Hand1.bindings[0].path);
            Assert.AreEqual("<Keyboard>/2", DesktopBinding.Shared.Hand2.bindings[0].path);
            Assert.AreEqual("<Keyboard>/tab", DesktopBinding.Shared.Bag.bindings[0].path);
            Assert.AreEqual("<Keyboard>/escape", DesktopBinding.Shared.Pause.bindings[0].path);
            var scripted = new ScriptedBinding { Next = new PlayerCommands { attack = true, swap = true, slot = 2 } };
            var command = default(PlayerCommands);
            scripted.Read(ref command);
            Assert.IsTrue(command.attack && command.swap && command.slot == 2);
            scripted.Read(ref command);
            Assert.IsFalse(command.attack || command.swap || command.slot != 0);
        }

        [Test]
        public void DisabledTouchCannotIssueCommands()
        {
            touch.Enabled = false;
            touch.SetMove(Vector2.right);
            touch.SetHeld(TouchAction.Attack, true);
            var command = default(PlayerCommands);
            touch.Read(ref command);
            Assert.AreEqual(Vector2.zero, command.move);
            Assert.IsFalse(command.attack);
        }

        [UnityTest]
        public IEnumerator FrozenTypewriterPlayerKeepsDedicatedTouchNavigationAndExit()
        {
            yield return TestScenes.Reset();
            var typewriter = new GameObject("Touch typewriter").AddComponent<Typewriter>();
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, touch);
            var hud = new GameObject("Touch HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(true);
            typewriter.Open(player);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.IsTrue(player.Frozen);
            var menu = hud.transform.Find("Safe HUD/Typewriter touch menu");
            Assert.IsNotNull(menu);
            Assert.IsTrue(menu.gameObject.activeSelf, "A frozen mode-select player still needs touch controls.");
            Assert.IsNotNull(menu.Find("Previous mode"));
            Assert.IsNotNull(menu.Find("Next mode"));
            Assert.IsNotNull(menu.Find("Choose mode"));
            Assert.IsNotNull(menu.Find("Leave typewriter"));
            Assert.IsNotNull(hud.transform.Find("Safe HUD/Brand").GetComponent<UnityEngine.UI.Button>(), "The brand tile opens the pause card...");
            Assert.IsNotNull(hud.transform.Find("Pause/Pause card/Pause home"), "...which has the way home.");
            int selected = typewriter.Selected;
            touch.Pulse(TouchAction.Down);
            yield return null;
            yield return null;
            Assert.AreNotEqual(selected, typewriter.Selected, "Menu navigation must read input while the player is frozen.");
            typewriter.Close();
            Object.Destroy(hud.gameObject);
            Object.Destroy(typewriter.gameObject);
            Object.Destroy(player.gameObject);
            yield return TestScenes.Reset();
        }
    }
}
