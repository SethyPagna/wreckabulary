using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using TMPro;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class FreeLookTests
    {
        Camera lens;
        CameraRig rig;
        Keyboard keyboard;
        Mouse mouse;
        Gamepad pad;
        bool hadOverrides, touchEnabled;
        string overrides;
        InputSettings.EditorInputBehaviorInPlayMode route;
        InputSettings.BackgroundBehavior focus;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            hadOverrides = PlayerPrefs.HasKey(KeyBindings.OverridesKey);
            overrides = PlayerPrefs.GetString(KeyBindings.OverridesKey, "");
            PlayerPrefs.DeleteKey(KeyBindings.OverridesKey);
            touchEnabled = TouchBinding.Shared.Enabled;
            route = InputSystem.settings.editorInputBehaviorInPlayMode;
            focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
            pad = InputSystem.AddDevice<Gamepad>();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Test Ground";
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(60f, 1f, 60f);
            TilePool.Ensure();
            lens = new GameObject("Main Camera").AddComponent<Camera>();
            lens.tag = "MainCamera";
            lens.transform.SetPositionAndRotation(new Vector3(0f, 12f, -9f), Quaternion.Euler(50f, 0f, 0f));
            rig = lens.gameObject.AddComponent<CameraRig>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            CursorPolicy.Apply(false);
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
            foreach (InputDevice device in new InputDevice[] { keyboard, mouse, pad })
                if (device != null && device.added) InputSystem.RemoveDevice(device);
            InputSystem.settings.editorInputBehaviorInPlayMode = route;
            InputSystem.settings.backgroundBehavior = focus;
            TouchBinding.Shared.ReleaseAll(); TouchBinding.Shared.Enabled = touchEnabled;
            if (hadOverrides) PlayerPrefs.SetString(KeyBindings.OverridesKey, overrides);
            else PlayerPrefs.DeleteKey(KeyBindings.OverridesKey);
            PlayerPrefs.Save();
        }

        sealed class LookBinding : InputBinding
        {
            public PlayerCommands Next;
            public override string Id => "test-free-look";
            public override bool CanLook => true;
            public override void Read(ref PlayerCommands c)
            {
                c = Next;
                Next.lookDelta = Next.orbit = Vector2.zero;
            }
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        static PlayerController Spawn(InputBinding binding, Vector3 at, int index = 0, bool pinned = false)
        {
            var p = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            p.Setup(index, binding);
            p.Inventory.Collects = false;
            p.Respawn(at);
            if (pinned)
            {
                p.Body.useGravity = false;
                p.Body.constraints = RigidbodyConstraints.FreezeAll;
            }
            return p;
        }

        static PlayerCommands Read(InputBinding binding)
        {
            var commands = default(PlayerCommands);
            binding.Read(ref commands);
            return commands;
        }

        static float Yaw(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        static float Elevation(Camera camera) => Mathf.Asin(-camera.transform.forward.y) * Mathf.Rad2Deg;

        void InView(PlayerController player)
        {
            var at = lens.WorldToViewportPoint(player.transform.position + Vector3.up * .9f);
            Assert.That(at.x, Is.InRange(0f, 1f), player.name + " across");
            Assert.That(at.y, Is.InRange(0f, 1f), player.name + " along");
        }

        static List<(string name, Quaternion turn, Vector2 at)> Snapshot(Transform root) =>
            root.GetComponentsInChildren<RectTransform>(true).Select(t => (t.name, t.localRotation, t.anchoredPosition)).ToList();

        static void Unchanged(List<(string name, Quaternion turn, Vector2 at)> before, List<(string name, Quaternion turn, Vector2 at)> after)
        {
            Assert.AreEqual(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i].name, after[i].name);
                Assert.That(Quaternion.Angle(before[i].turn, after[i].turn), Is.LessThan(.01f), before[i].name + " turned");
                Assert.That(Vector2.Distance(before[i].at, after[i].at), Is.LessThan(.05f), before[i].name + " moved");
            }
        }

        [Test]
        public void LookAroundIsARebindableRowRightAfterAim()
        {
            using var keys = new DesktopBinding();
            var rows = KeyBindings.Rows(keys).ToList();
            int aim = rows.FindIndex(row => row.action.name == "Aim");
            Assert.AreEqual("Look around (hold)", rows[aim + 1].label);
            Assert.AreSame(keys.LookAround, rows[aim + 1].action);
            Assert.AreEqual("<Keyboard>/leftCtrl", keys.LookAround.bindings[0].effectivePath, "Right Ctrl stays the right keyboard half's dodge.");
            KeyBindings.Assign(keys.LookAround, 0, "<Keyboard>/g");
            using var fresh = new DesktopBinding();
            Assert.AreEqual("<Keyboard>/g", fresh.LookAround.bindings[0].effectivePath);
            Assert.AreEqual("<Keyboard>/space", fresh.Jump.bindings[0].effectivePath);
            Assert.AreEqual("G", KeyBindings.KeyName(fresh.LookAround, 0));
            Assert.AreEqual(ControlHints.PadLookAround, ControlHints.LookAround(new GamepadBinding(pad)));
            Assert.AreEqual("", ControlHints.LookAround(new KeyboardBinding(KeyboardBinding.Side.Right)));
            Assert.AreEqual(ControlHints.LookAroundKey, ControlHints.LookAround(DesktopBinding.Shared));
            Assert.IsNotEmpty(ControlHints.LookAroundKey);
            StringAssert.DoesNotContain("CONTROL", ControlHints.LookAroundKey, "The key pill stays short.");
        }

        [UnityTest]
        public IEnumerator CtrlAndMouseOrbitOnlyWhereTheHouseCameraTakesTheLook()
        {
            using var keys = new DesktopBinding();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(40f, 0f) });
            yield return null;
            var looking = Read(keys);
            Assert.IsTrue(looking.lookAround);
            Assert.Greater(looking.orbit.x, 0f, "Mouse right looks right.");
            Assert.AreEqual(Vector2.zero, looking.lookDelta, "The player does not turn while looking around.");
            Assert.IsFalse(looking.aimAtPointer, "The pointer stops steering the aim while looking around.");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(40f, 0f) });
            yield return null;
            var free = Read(keys);
            Assert.IsFalse(free.lookAround);
            Assert.AreEqual(Vector2.zero, free.orbit);

            rig.enabled = false;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = new Vector2(40f, 0f) });
            yield return null;
            var courses = Read(keys);
            Assert.IsFalse(courses.lookAround, "Without a party camera (Courses) Ctrl changes nothing.");
            Assert.AreEqual(Vector2.zero, courses.orbit);
            rig.enabled = true;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            rig.BeginLook();
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            var dragging = Read(keys);
            Assert.IsFalse(dragging.attack || dragging.attackHeld || dragging.aimHeld, "Dragging the eye never swings.");
            InputSystem.QueueStateEvent(mouse, new MouseState());
            yield return null;
            rig.EndLook();
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Left));
            yield return null;
            Assert.IsTrue(Read(keys).attack, "Clicks attack again once the drag ends.");
            InputSystem.QueueStateEvent(mouse, new MouseState());
        }

        [UnityTest]
        public IEnumerator HoldL3AndTheRightStickOrbitWithoutPressingAnythingElse()
        {
            var binding = new GamepadBinding(pad);
            InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(.5f, 0f), rightStick = new Vector2(1f, 0f) }.WithButton(GamepadButton.LeftStick));
            yield return null;
            var look = Read(binding);
            Assert.IsTrue(look.lookAround);
            Assert.Greater(look.orbit.x, 0f);
            Assert.AreEqual(Vector2.zero, look.look, "The right stick stops aiming while L3 is held.");
            Assert.AreEqual(Vector2.zero, look.lookDelta);
            Assert.Greater(look.move.x, .4f, "You can still walk while looking around.");
            Assert.IsFalse(look.swap || look.attack || look.jump || look.dodge || look.spellHeld || look.spellDown || look.grab || look.grabHeld ||
                look.blockHeld || look.drop || look.up || look.down || look.start, "L3 clashes with no other House or Courses button.");
            Assert.AreEqual(0, look.slot);
            Assert.IsFalse(binding.BagHeld);

            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightStick));
            yield return null;
            var swap = Read(binding);
            Assert.IsTrue(swap.swap, "R3 still swaps hands.");
            Assert.IsFalse(swap.lookAround);

            rig.enabled = false;
            InputSystem.QueueStateEvent(pad, new GamepadState { rightStick = new Vector2(1f, 0f) }.WithButton(GamepadButton.LeftStick));
            yield return null;
            var courses = Read(binding);
            Assert.IsFalse(courses.lookAround);
            Assert.AreEqual(Vector2.zero, courses.orbit);
            Assert.Greater(courses.look.x, .9f, "Without a party camera (Courses) L3 changes nothing.");
            rig.enabled = true;
            InputSystem.QueueStateEvent(pad, new GamepadState());
        }

        [UnityTest]
        public IEnumerator OverheadOrbitCirclesThePartyWidensClampsAndEasesBackExactly()
        {
            rig.FrameLayout(GameConfig.Current.HouseFor("pinwheel"));
            var world = new GameObject("Two-storey house").AddComponent<StoreyCutaway>();
            new GameObject(RoomBuilder.StoreyName(0)).transform.SetParent(world.transform);
            var upper = new GameObject(RoomBuilder.StoreyName(1)).transform;
            upper.SetParent(world.transform);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Upstairs floor";
            floor.transform.SetParent(upper, false);
            floor.transform.position = new Vector3(0f, 2.9f, 0f);
            floor.transform.localScale = new Vector3(8f, .2f, 8f);
            floor.GetComponent<Collider>().enabled = false;
            var upstairs = floor.GetComponent<Renderer>();
            world.Configure(GameConfig.Current.HouseFor("terrace"), world.transform);
            var a = Spawn(new LookBinding(), new Vector3(-14f, 0f, 0f), 0, true);
            var b = Spawn(new LookBinding(), new Vector3(14f, 0f, 0f), 1, true);
            yield return new WaitForSeconds(2.5f);
            Assert.IsTrue(lens.orthographic, "Two seats share the overhead view.");
            Assert.IsFalse(rig.IsThirdPerson);
            var steady = lens.transform.rotation;
            float size = lens.orthographicSize;
            var frame = rig.PlanarForward;
            var cutaway = lens.GetComponent<CameraCutaway>();
            Assert.IsTrue(cutaway.WouldHide(upstairs));
            int evaluations = StoreyCutaway.Evaluations;

            rig.BeginLook();
            rig.Orbit(90f, 0f);
            yield return null;
            yield return null;
            Assert.IsTrue(rig.Looking && rig.Dragging);
            Assert.AreEqual(90f, Yaw(lens.transform.forward), 1f, "A quarter turn to the right.");
            Assert.Greater(lens.orthographicSize, size + .5f, "The side-on view widens the frame.");
            InView(a);
            InView(b);
            Assert.That(Vector3.Angle(frame, rig.PlanarForward), Is.LessThan(.01f), "Movement keeps the frame from before the look.");
            Assert.IsTrue(cutaway.WouldHide(upstairs), "The storey cutaway keeps running from the orbited camera.");
            Assert.Greater(StoreyCutaway.Evaluations, evaluations);

            rig.Orbit(90f, 500f);
            yield return null;
            Assert.AreEqual(180f, Mathf.Abs(Yaw(lens.transform.forward)), 1f);
            Assert.That(Elevation(lens), Is.InRange(FreeLook.MaxElevation - 1f, FreeLook.MaxElevation + .5f));
            rig.Orbit(0f, -1000f);
            yield return null;
            Assert.That(Elevation(lens), Is.InRange(FreeLook.MinElevation - .5f, FreeLook.MinElevation + 1f), "The camera never dips under the floor.");
            Assert.Greater(lens.transform.position.y, 0f);

            rig.EndLook();
            rig.EndLook();
            Assert.IsFalse(rig.Dragging, "An extra EndLook is ignored.");
            yield return TestScenes.WaitUntil(() => rig.OrbitYaw == 0f && rig.OrbitPitch == 0f, 2f, "the view eases back");
            yield return null;
            Assert.IsFalse(rig.Looking);
            Assert.That(Quaternion.Angle(steady, lens.transform.rotation), Is.LessThan(.01f), "Back on the normal angle.");
            Assert.AreEqual(size, lens.orthographicSize, .2f);
        }

        [UnityTest]
        public IEnumerator ThirdPersonLookAroundKeepsTheFrameTheMapAndHidesTheCrosshair()
        {
            var hud = new GameObject("Test HUD", typeof(Canvas)).AddComponent<GameHud>();
            var input = new LookBinding();
            var player = Spawn(input, Vector3.zero);
            yield return new WaitForSeconds(.4f);
            Assert.IsTrue(rig.IsThirdPerson);
            var cross = hud.UiCanvas.transform.Find("Crosshair").gameObject;
            Assert.IsTrue(cross.activeSelf);
            var minimap = hud.GetComponentsInChildren<RectTransform>(true).First(t => t.name == "Minimap");
            var map = Snapshot(minimap);
            float yaw = player.LookYaw;
            var facing = player.Facing;
            var frame = rig.PlanarForward;

            input.Next.lookAround = true;
            input.Next.orbit = new Vector2(.6f, 0f);
            yield return null;
            yield return null;
            Assert.IsTrue(rig.Looking);
            Assert.AreEqual(yaw, player.LookYaw, "The player keeps facing the same way.");
            Assert.That(Vector3.Angle(facing, player.Facing), Is.LessThan(.01f));
            Assert.That(Vector3.Angle(frame, rig.PlanarForward), Is.LessThan(.01f), "Movement keeps the frame from before the look.");
            Assert.That(Vector3.Angle(World.Flat(lens.transform.forward), World.Flat(facing)), Is.InRange(25f, 45f), "The view swung around the player.");
            Assert.IsFalse(cross.activeSelf, "The crosshair hides while looking around.");
            yield return new WaitForSeconds(.25f);
            Unchanged(map, Snapshot(minimap));

            input.Next.orbit = new Vector2(0f, 10f);
            yield return null;
            yield return null;
            Assert.LessOrEqual(lens.transform.eulerAngles.x, ShoulderView.MaxPitch * Mathf.Rad2Deg + 3f, "Pitch is clamped.");
            Assert.Greater(lens.transform.position.y, player.transform.position.y);

            input.Next.lookAround = false;
            yield return TestScenes.WaitUntil(() => !rig.Looking && rig.OrbitYaw == 0f && rig.OrbitPitch == 0f, 2f, "the view eases back behind the player");
            yield return null;
            Assert.IsTrue(cross.activeSelf);
            var behind = World.Flat(lens.transform.position - player.transform.position).normalized;
            Assert.Less(Vector3.Dot(behind, World.Flat(player.Facing).normalized), -.95f);
        }

        [UnityTest]
        public IEnumerator EyeButtonDragOrbitsReleasesAndStepsAsideForTouch()
        {
            var hud = new GameObject("Test HUD", typeof(Canvas)).AddComponent<GameHud>();
            Spawn(new LookBinding(), Vector3.zero);
            yield return new WaitForSeconds(.4f);
            hud.ShowTouchControls(false);
            yield return null;
            var holder = hud.GetComponentsInChildren<Transform>(true).First(t => t.name == "Look around");
            Assert.AreEqual("Side column", holder.parent.name);
            var eye = holder.Find("Eye").gameObject;
            Assert.IsTrue(eye.activeInHierarchy);
            Assert.AreEqual(ControlHints.LookAroundKey, eye.GetComponentsInChildren<TMP_Text>(true).Single(t => t.name == "Key").text);
            var corners = new Vector3[4];
            ((RectTransform)eye.transform).GetWorldCorners(corners);
            float eyeRight = corners[2].x;
            ((RectTransform)holder.parent.Find("Minimap")).GetWorldCorners(corners);
            Assert.LessOrEqual(eyeRight, corners[0].x + .5f, "The eye sits beside the minimap, not on it.");

            var finger = new PointerEventData(EventSystem.current) { pointerId = 3 };
            ExecuteEvents.ExecuteHierarchy(eye, finger, ExecuteEvents.pointerDownHandler);
            Assert.IsTrue(rig.Dragging);
            finger.delta = new Vector2(Screen.height / 4f, 0f);
            ExecuteEvents.ExecuteHierarchy(eye, finger, ExecuteEvents.dragHandler);
            Assert.AreEqual(90f, rig.OrbitYaw, .5f, "A quarter screen-height drag turns a quarter.");
            var other = new PointerEventData(EventSystem.current) { pointerId = 4, delta = new Vector2(Screen.height, 0f) };
            ExecuteEvents.ExecuteHierarchy(eye, other, ExecuteEvents.dragHandler);
            Assert.AreEqual(90f, rig.OrbitYaw, .5f, "Another finger cannot steal the drag.");
            yield return null;
            Assert.IsTrue(rig.Looking);
            ExecuteEvents.ExecuteHierarchy(eye, finger, ExecuteEvents.pointerUpHandler);
            Assert.IsFalse(rig.Dragging);
            yield return TestScenes.WaitUntil(() => rig.OrbitYaw == 0f, 2f, "release eases back");

            ExecuteEvents.ExecuteHierarchy(eye, finger, ExecuteEvents.pointerDownHandler);
            Assert.IsTrue(rig.Dragging);
            hud.ShowTouchControls(true);
            yield return null;
            yield return null;
            Assert.IsFalse(eye.activeSelf, "Touch players use the touch EYE button instead.");
            Assert.IsFalse(rig.Dragging, "Hiding the eye ends its drag.");
            hud.ShowTouchControls(false);
        }
    }
}
