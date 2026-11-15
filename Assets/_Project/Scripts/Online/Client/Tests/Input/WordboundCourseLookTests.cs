using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wreckabulary.Rules.Wordbound;
using Object = UnityEngine.Object;

namespace Wreckabulary.Online.Tests
{
    public sealed class WordboundCourseLookTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<InputDevice> created = new List<InputDevice>();
        Keyboard previousKeyboard, keyboard;
        Mouse previousMouse, mouse;
        Gamepad pad;
        bool typing, hadOverrides;
        string overrides;
        float scale;
        WordboundNativeInputScope input;

        [SetUp]
        public void SetUp()
        {
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current;
            typing = DesktopBinding.Typing; scale = Time.timeScale;
            hadOverrides = PlayerPrefs.HasKey(KeyBindings.OverridesKey);
            overrides = PlayerPrefs.GetString(KeyBindings.OverridesKey, "");
            PlayerPrefs.DeleteKey(KeyBindings.OverridesKey);
            input = new WordboundNativeInputScope();
            DesktopBinding.Typing = false; Time.timeScale = 1;
            keyboard = Add<Keyboard>(); keyboard.MakeCurrent();
            mouse = Add<Mouse>(); mouse.MakeCurrent();
            pad = Add<Gamepad>();
            Feed(new KeyboardState(), Vector2.zero);
            WordboundNativeInputScope.Sample(pad, new GamepadState());
        }

        T Add<T>() where T : InputDevice, new()
        {
            var device = InputSystem.AddDevice<T>(); created.Add(device); return device;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                foreach (var device in created) if (device != null && device.added) InputSystem.RemoveDevice(device);
                created.Clear();
                if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
                if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            }
            finally
            {
                try { input?.Dispose(); }
                finally
                {
                    input = null; DesktopBinding.Typing = typing; Time.timeScale = scale;
                    if (hadOverrides) PlayerPrefs.SetString(KeyBindings.OverridesKey, overrides);
                    else PlayerPrefs.DeleteKey(KeyBindings.OverridesKey);
                }
            }
        }

        void Feed(KeyboardState keys, Vector2 delta)
        {
            InputSystem.QueueStateEvent(keyboard, keys);
            InputSystem.QueueStateEvent(mouse, new MouseState { delta = delta });
            WordboundNativeInputScope.Pump();
        }

        static PlayerCommands Read(InputBinding binding)
        {
            var commands = default(PlayerCommands); binding.Read(ref commands); return commands;
        }

        static string Wire(PlayerCommands commands)
        {
            commands.lookAround = false;
            commands.orbit = commands.look = commands.lookDelta = Vector2.zero;
            return JsonUtility.ToJson(commands);
        }

        static readonly Key[][] Walk = { new[] { Key.W }, new[] { Key.W, Key.Space }, new[] { Key.W, Key.E }, new[] { Key.A, Key.W }, new Key[0] };

        List<PlayerCommands> Desktop(bool looking, Vector2 delta)
        {
            Feed(new KeyboardState(), Vector2.zero); Feed(new KeyboardState(), Vector2.zero);
            using var binding = new WordboundInputBinding();
            var frames = new List<PlayerCommands>();
            foreach (var keys in Walk)
            {
                var held = looking && keys.Length > 0 ? keys.Append(Key.LeftCtrl).ToArray() : keys;
                Feed(new KeyboardState(held), keys.Length > 0 ? delta : Vector2.zero);
                frames.Add(Read(binding));
            }
            return frames;
        }

        [Test]
        public void CtrlTurnsMouseMotionIntoAnOrbitAndLeavesEveryOtherCommandAsItWas()
        {
            var delta = new Vector2(30f, -12f);
            var walking = Desktop(false, Vector2.zero);
            var looking = Desktop(true, delta);
            for (int i = 0; i < Walk.Length; i++)
            {
                Assert.AreEqual(Wire(walking[i]), Wire(looking[i]), "Frame " + i + " sends the same commands while looking around.");
                Assert.IsFalse(walking[i].lookAround);
                Assert.AreEqual(Vector2.zero, walking[i].orbit);
                if (Walk[i].Length == 0) { Assert.IsFalse(looking[i].lookAround, "Letting go of Ctrl ends the look."); continue; }
                Assert.IsTrue(looking[i].lookAround, "Frame " + i);
                Assert.AreEqual(KeyBindings.Look(delta).x, looking[i].orbit.x, 1e-6f);
                Assert.AreEqual(KeyBindings.Look(delta).y, looking[i].orbit.y, 1e-6f);
                Assert.Greater(looking[i].orbit.x, 0f, "Mouse right looks right.");
                Assert.AreEqual(Vector2.zero, looking[i].lookDelta, "The player never turns while looking around.");
                Assert.AreEqual(Vector2.zero, looking[i].look);
            }
            Assert.Greater(looking[0].move.y, .5f, "You keep walking while looking around.");
            Assert.IsTrue(looking[1].jump && looking[2].grab, "Jump and pick up still go through.");
        }

        List<PlayerCommands> Pad(bool looking)
        {
            WordboundNativeInputScope.Sample(pad, new GamepadState()); WordboundNativeInputScope.Sample(pad, new GamepadState());
            using var binding = new WordboundInputBinding(new GamepadBinding(pad));
            var frames = new List<PlayerCommands>();
            var states = new[] {
                new GamepadState { leftStick = new Vector2(0f, .8f), rightStick = new Vector2(.9f, .4f) },
                new GamepadState { leftStick = new Vector2(.6f, .6f), rightStick = new Vector2(-.7f, 0f) }.WithButton(GamepadButton.South),
                new GamepadState { leftStick = new Vector2(0f, .8f), rightStick = new Vector2(.9f, 0f) }.WithButton(GamepadButton.RightStick),
                new GamepadState()
            };
            for (int i = 0; i < states.Length; i++)
            {
                var state = states[i];
                if (looking && i < states.Length - 1) state = state.WithButton(GamepadButton.LeftStick);
                WordboundNativeInputScope.Sample(pad, state);
                frames.Add(Read(binding));
            }
            return frames;
        }

        [Test]
        public void L3TurnsTheRightStickIntoAnOrbitWithoutTurningAndR3StillSwaps()
        {
            var walking = Pad(false);
            var looking = Pad(true);
            for (int i = 0; i < walking.Count; i++)
            {
                Assert.AreEqual(Wire(walking[i]), Wire(looking[i]), "Frame " + i + " sends the same commands while looking around.");
                Assert.IsFalse(walking[i].lookAround);
                if (i == walking.Count - 1) { Assert.IsFalse(looking[i].lookAround); continue; }
                Assert.IsTrue(looking[i].lookAround, "Frame " + i);
                Assert.AreEqual(walking[i].lookDelta, looking[i].orbit, "The stick turns the camera at its usual speed.");
                Assert.AreEqual(Vector2.zero, looking[i].look, "The right stick stops turning the player while L3 is held.");
                Assert.AreEqual(Vector2.zero, looking[i].lookDelta);
            }
            Assert.Greater(walking[0].look.x, .3f, "Without L3 the stick turns the player.");
            Assert.IsTrue(looking[1].jump, "A still jumps.");
            Assert.IsTrue(looking[2].swap, "R3 still swaps hands.");
            Assert.Greater(looking[0].move.y, .5f, "You keep walking while looking around.");
        }

        sealed class Scripted : InputBinding
        {
            readonly bool mouse;
            public Scripted(bool readsMouse) => mouse = readsMouse;
            public override string Id => "course-look-test";
            public override bool CanLook => true;
            public override bool ReadsMouse => mouse;
            public override void Read(ref PlayerCommands commands) { }
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        static readonly PlayerCommands[] Script = {
            new PlayerCommands { move = new Vector2(0f, 1f) },
            new PlayerCommands { move = new Vector2(.6f, .8f), jump = true },
            new PlayerCommands { move = new Vector2(0f, 1f), grab = true, grabHeld = true, attack = true, attackHeld = true },
            new PlayerCommands { move = new Vector2(-1f, 0f), aimHeld = true, slot = 2 },
            new PlayerCommands { move = new Vector2(0f, -1f), drop = true, swap = true }
        };

        sealed class Run : IDisposable
        {
            readonly GameObject root;
            public readonly WordboundPlayerView View;
            public readonly List<PlayerCommands> Heard = new List<PlayerCommands>();
            public readonly List<WordboundMovementInput> Sent = new List<WordboundMovementInput>();

            public Run(bool mouse, WordboundCameraProfile profile = null)
            {
                root = new GameObject("Course look view test");
                View = root.AddComponent<WordboundPlayerView>(); View.enabled = false;
                View.Initialize(root.AddComponent<WordboundGameplayController>(), root.AddComponent<Camera>(), new Scripted(mouse), profile);
                View.CommandsRead += Heard.Add;
            }

            public void Frame(PlayerCommands commands)
            {
                typeof(WordboundPlayerView).GetMethod("Steer", Private).Invoke(View, new object[] { commands, 1f / 60f });
                Sent.Add((WordboundMovementInput)typeof(WordboundPlayerView).GetMethod("Pending", Private).Invoke(View, null));
            }

            public float Steady(string name) => (float)typeof(WordboundPlayerView).GetField(name, Private).GetValue(View);

            public CourseLook Look => (CourseLook)typeof(WordboundPlayerView).GetField("look", Private).GetValue(View);

            public void Dispose() => Object.DestroyImmediate(root);
        }

        static PlayerCommands Looking(PlayerCommands commands)
        {
            commands.lookAround = true;
            commands.orbit = new Vector2(.05f, .02f);
            commands.look = new Vector2(1f, .5f);
            commands.lookDelta = new Vector2(.3f, .1f);
            return commands;
        }

        static PlayerCommands Turning(PlayerCommands commands)
        {
            commands.look = new Vector2(1f, .5f);
            commands.lookDelta = new Vector2(.3f, .1f);
            return commands;
        }

        [TestCase(false), TestCase(true)]
        public void LookingAroundSendsTheSameMovementFacingAndActionsAsNotLookingThenEasesBack(bool mouse)
        {
            using var still = new Run(mouse);
            using var looking = new Run(mouse);
            using var turning = new Run(mouse);
            foreach (var frame in Script) { still.Frame(frame); looking.Frame(Looking(frame)); turning.Frame(Turning(frame)); }
            for (int i = 0; i < Script.Length; i++)
            {
                var a = still.Sent[i]; var b = looking.Sent[i];
                Assert.AreEqual(a.MoveX, b.MoveX, "Frame " + i); Assert.AreEqual(a.MoveZ, b.MoveZ, "Frame " + i);
                Assert.AreEqual((bool)a.Jump, (bool)b.Jump, "Frame " + i); Assert.AreEqual((bool)a.HasFacing, (bool)b.HasFacing);
                Assert.AreEqual(a.FacingYaw, b.FacingYaw, "Frame " + i + ": looking around never turns the facing that is sent.");
                Assert.AreEqual(Wire(still.Heard[i]), Wire(looking.Heard[i]), "Frame " + i + ": the HUD gets the same actions.");
            }
            Assert.IsTrue(still.Sent.Any(s => s.Jump) && still.Sent.Any(s => Mathf.Abs(s.MoveX) > .5f), "The script walks and jumps.");
            Assert.AreNotEqual(still.Sent.Last().FacingYaw, turning.Sent.Last().FacingYaw, "The same input without Look around does turn.");
            Assert.AreEqual(0f, still.View.OrbitYaw);
            Assert.AreEqual(Script.Length * .05f * Mathf.Rad2Deg, looking.View.OrbitYaw, 1e-3f, "Only the camera turns.");
            Assert.Greater(looking.View.OrbitPitch, 0f);
            Assert.IsTrue(looking.View.Looking);

            float facing = looking.Sent.Last().FacingYaw;
            looking.Frame(Script[0]);
            Assert.IsFalse(looking.View.Looking, "Letting go ends the look.");
            for (int i = 0; i < 90 && looking.Look.Orbiting; i++) looking.Look.Update(1f / 60f, .16f * Mathf.Rad2Deg, -.45f * Mathf.Rad2Deg, .95f * Mathf.Rad2Deg);
            Assert.AreEqual(0f, looking.View.OrbitYaw, "The camera eases back behind the player.");
            Assert.AreEqual(0f, looking.View.OrbitPitch);
            Assert.AreEqual(facing, looking.Sent.Last().FacingYaw);
        }

        [Test]
        public void MouseTurnsTheCourseCameraOnceAtTheHouseRateAndMouseUpLooksUp()
        {
            bool invert = KeyBindings.InvertY, hadInvert = PlayerPrefs.HasKey(KeyBindings.InvertKey);
            var profile = ScriptableObject.CreateInstance<WordboundCameraProfile>();
            try
            {
                KeyBindings.SetInvertY(false);
                var raw = new Vector2(100f, 50f);
                var delta = KeyBindings.Look(raw);
                using var run = new Run(true);
                float yaw = run.Steady("yaw"), pitch = run.Steady("pitch");
                run.Frame(new PlayerCommands { lookDelta = delta });
                float turned = (run.Steady("yaw") - yaw) * Mathf.Rad2Deg, raised = (run.Steady("pitch") - pitch) * Mathf.Rad2Deg;
                Assert.AreEqual(100f * ShoulderView.MouseSensitivity * KeyBindings.Sensitivity * Mathf.Rad2Deg, turned, 1e-3f,
                    "100 px of mouse turns the course camera as far as the House camera.");
                Assert.AreEqual(delta.x * Mathf.Rad2Deg, turned, 1e-3f, "House adds the scaled delta to LookYaw once.");
                Assert.AreEqual(delta.y * Mathf.Rad2Deg, raised, 1e-3f, "House adds the scaled delta to LookPitch once.");
                Assert.Less(raised, 0f, "Mouse up looks up.");
                Assert.Greater(ShoulderView.Forward(run.Steady("yaw"), run.Steady("pitch")).y, ShoulderView.Forward(yaw, pitch).y);

                KeyBindings.SetInvertY(true);
                pitch = run.Steady("pitch");
                run.Frame(new PlayerCommands { lookDelta = KeyBindings.Look(raw) });
                Assert.Greater(run.Steady("pitch"), pitch, "Invert mouse Y: mouse up looks down.");

                KeyBindings.SetInvertY(false);
                profile.MouseSensitivity = ShoulderView.MouseSensitivity * 2f;
                using var faster = new Run(true, profile);
                yaw = faster.Steady("yaw");
                faster.Frame(new PlayerCommands { lookDelta = delta });
                Assert.AreEqual(2f * delta.x * Mathf.Rad2Deg, (faster.Steady("yaw") - yaw) * Mathf.Rad2Deg, 1e-3f,
                    "The course camera profile scales the House rate.");
            }
            finally
            {
                KeyBindings.SetInvertY(invert);
                if (!hadInvert) PlayerPrefs.DeleteKey(KeyBindings.InvertKey);
                Object.DestroyImmediate(profile);
            }
        }

        static Vector3 Project(Camera camera, Vector3 position, Quaternion rotation, Vector3 world) => (Vector3)typeof(WordboundPlayerView)
            .GetMethod("ViewportPoint", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { camera, position, rotation, world });

        [Test]
        public void InteractTargetsUseTheSteadyViewWhileTheCameraOrbits()
        {
            using var run = new Run(true);
            var camera = run.View.Camera;
            camera.fieldOfView = 55f; camera.aspect = 16f / 9f; camera.nearClipPlane = .1f; camera.farClipPlane = 1800f;
            var position = new Vector3(1f, 2.5f, -3f); var rotation = Quaternion.Euler(12f, 30f, 0f);
            var points = new[] { new Vector3(0f, 1f, 2f), new Vector3(3f, 0f, 5f), new Vector3(-2f, 1.5f, 8f), new Vector3(1f, 2.5f, -6f) };
            camera.transform.SetPositionAndRotation(position, rotation);
            var expected = points.Select(point => camera.WorldToViewportPoint(point)).ToArray();
            typeof(WordboundPlayerView).GetField("steadyPosition", Private).SetValue(run.View, position);
            typeof(WordboundPlayerView).GetField("steadyRotation", Private).SetValue(run.View, rotation);
            typeof(WordboundPlayerView).GetField("placed", Private).SetValue(run.View, true);
            run.View.Orbit(120f, 10f);
            camera.transform.SetPositionAndRotation(new Vector3(-4f, 3f, 2f), Quaternion.Euler(30f, -150f, 0f));
            for (int i = 0; i < points.Length; i++)
            {
                var actual = run.View.SteadyViewportPoint(points[i]);
                Assert.AreEqual(expected[i].z, actual.z, 1e-4f, "depth " + i);
                Assert.AreEqual(expected[i].z, Project(camera, position, rotation, points[i]).z, 1e-4f);
                if (expected[i].z <= 0f) { Assert.LessOrEqual(actual.z, 0f, "Behind the steady view stays out of reach."); continue; }
                Assert.AreEqual(expected[i].x, actual.x, 1e-4f, "x " + i); Assert.AreEqual(expected[i].y, actual.y, 1e-4f, "y " + i);
            }
            Assert.Greater((expected[0] - camera.WorldToViewportPoint(points[0])).magnitude, 1e-2f, "The orbiting camera itself sees a different picture.");
            run.View.LocalPlayerChanged(null);
            Assert.IsFalse(run.Look.Orbiting);
            Assert.AreEqual(camera.WorldToViewportPoint(points[1]), run.View.SteadyViewportPoint(points[1]),
                "Without an orbit the camera itself picks targets, exactly as before.");
        }

        [Test]
        public void TheCourseEyeMatchesTheHouseEyeAndDragsTheCameraUntilReleased()
        {
            using var f = new WordboundRecipeContextFixture("TimeRun");
            var view = f.Grid.View;
            f.Grid.Set("initialized", true);
            view.SetMenuOpen(false);
            float house = (float)typeof(GameHud).GetField("EyeSize", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            float HouseScale(float w, float h) => (float)typeof(WordboundMatchHud).GetMethod("HouseScale", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { w, h });
            Assert.AreEqual(1f, HouseScale(1920f, 1080f), 1e-5f);
            Assert.AreEqual(2f / 3f, HouseScale(1280f, 720f), 1e-5f);
            float unit = HouseScale(1920f, 1080f) / 1.5f;
            f.Grid.Call("PlaceEye", 1280f, unit);
            var eye = (Rect)f.Grid.Get("eyeRect"); var cap = (Rect)f.Grid.Get("keyCapRect");
            Assert.AreEqual(house, eye.width * 1.5f, 1e-3f, "At 1080p the course eye is as many pixels wide as the House eye.");
            Assert.AreEqual(eye.width, eye.height, 1e-5f);
            Assert.AreEqual(1280f - 18f, eye.xMax, 1e-3f); Assert.AreEqual(18f, eye.yMin, 1e-3f);
            Assert.AreEqual(56f, cap.width * 1.5f, 1e-3f); Assert.AreEqual(22f, cap.height * 1.5f, 1e-3f);
            Assert.AreEqual(eye.center.x, cap.center.x, 1e-3f); Assert.AreEqual(eye.yMax + 4f / 1.5f, cap.yMin, 1e-3f);
            Assert.AreEqual(ControlHints.LookAroundKey, view.LookAroundHint, "The key pill shows the same key as the House eye.");

            Assert.IsFalse((bool)f.Grid.Call("PressEye", new Vector2(40f, 40f)));
            Assert.IsFalse(view.Looking);
            Assert.IsTrue((bool)f.Grid.Call("PressEye", eye.center));
            Assert.IsTrue(view.Dragging && view.Looking);
            Assert.IsFalse((bool)f.Grid.Call("PressEye", eye.center), "A second finger is ignored.");
            var quarter = new Vector2(Mathf.Max(1f, Screen.height) / 4f, 0f);
            f.Grid.Call("DragEye", quarter);
            Assert.AreEqual(90f, view.OrbitYaw, 1e-2f, "A quarter screen height turns the view a quarter of the way round.");
            f.Grid.Call("ReleaseEye"); f.Grid.Call("ReleaseEye");
            Assert.IsFalse(view.Dragging || view.Looking);
            f.Grid.Call("DragEye", quarter);
            Assert.AreEqual(90f, view.OrbitYaw, 1e-2f, "Dragging after letting go does nothing.");

            Assert.IsTrue((bool)f.Grid.Call("PressEye", eye.center));
            view.SetMenuOpen(true);
            f.Grid.Call("Update");
            Assert.IsFalse(view.Dragging, "Opening the menu lets go of the eye.");
            Assert.IsFalse((bool)f.Grid.Call("PressEye", eye.center), "The eye is hidden behind the menu.");
        }

        [Test]
        public void TheKeyPillFollowsTheActiveDevice()
        {
            using var desktop = new WordboundInputBinding();
            Assert.AreEqual(ControlHints.LookAroundKey, desktop.LookAroundHint);
            Assert.IsNotEmpty(desktop.LookAroundHint);
            using var controller = new WordboundInputBinding(new GamepadBinding(pad));
            Assert.AreEqual(ControlHints.PadLookAround, controller.LookAroundHint);
            controller.Dispose();
            Assert.AreEqual("", controller.LookAroundHint);
        }
    }
}
