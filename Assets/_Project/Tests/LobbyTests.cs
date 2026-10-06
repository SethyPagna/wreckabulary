using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    /// <summary>The PC lobby on the hub: the map behind you, one page at a time, and PLAY → GO into a match.</summary>
    public class LobbyTests
    {
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey };
        string[] saved;
        bool hadVolume;
        float savedVolume, savedDelay;
        Gamepad pad;
        Keyboard keys;
        InputSettings.EditorInputBehaviorInPlayMode? keysRoute;
        InputSettings.BackgroundBehavior keysFocus;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // The lobby saves into the real prefs; keep the player's own and start each test fresh.
            saved = Keys.Select(k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetString(k) : null).ToArray();
            hadVolume = PlayerPrefs.HasKey(LobbyMenu.VolumeKey);
            savedVolume = PlayerPrefs.GetFloat(LobbyMenu.VolumeKey, 1f);
            foreach (var key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(LobbyMenu.VolumeKey);
            savedDelay = LobbyMenu.StartDelay;
            LobbyMenu.StartDelay = .3f;
            MatchTally.LastResult = null;
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (pad != null && pad.added) InputSystem.RemoveDevice(pad);
            pad = null;
            if (keys != null && keys.added) InputSystem.RemoveDevice(keys);
            keys = null;
            if (keysRoute.HasValue)
            {
                InputSystem.settings.editorInputBehaviorInPlayMode = keysRoute.Value;
                InputSystem.settings.backgroundBehavior = keysFocus;
            }
            keysRoute = null;
            yield return TestScenes.Reset();
            for (int i = 0; i < Keys.Length; i++)
            {
                if (saved[i] != null) PlayerPrefs.SetString(Keys[i], saved[i]);
                else PlayerPrefs.DeleteKey(Keys[i]);
            }
            if (hadVolume) PlayerPrefs.SetFloat(LobbyMenu.VolumeKey, savedVolume);
            else PlayerPrefs.DeleteKey(LobbyMenu.VolumeKey);
            PlayerPrefs.Save();
            AudioListener.volume = savedVolume;
            LobbyMenu.StartDelay = savedDelay;
            MatchTally.LastResult = null;
        }

        static IEnumerator OpenLobby()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            Assert.IsNotNull(LobbyMenu.Instance, "the lobby opens on the hub");
            Canvas.ForceUpdateCanvases();
        }

        /// <summary>A button on screen now; hidden pages and cleared controls don't count.</summary>
        static Button Find(string name)
        {
            var button = LobbyMenu.Instance.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == name);
            Assert.IsNotNull(button, $"a '{name}' button is showing");
            return button;
        }

        static void Click(string name)
        {
            var button = Find(name);
            Assert.IsTrue(button.interactable, $"'{name}' can be pressed");
            button.onClick.Invoke();
        }

        static float ScreenX(Component c)
        {
            var rect = (RectTransform)c.transform;
            return rect.TransformPoint(rect.rect.center).x;
        }

        static Transform Backdrop(LobbyStage stage) =>
            stage.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.StartsWith("Lobby backdrop"));

        /// <summary>Across the floor, ignoring height.</summary>
        static float DistanceToSegment(Vector3 point, Vector3 from, Vector3 to)
        {
            Vector2 p = new Vector2(point.x, point.z), a = new Vector2(from.x, from.z), b = new Vector2(to.x, to.z);
            float t = Mathf.Clamp01(Vector2.Dot(p - a, b - a) / Mathf.Max(1e-6f, (b - a).sqrMagnitude));
            return (p - (a + (b - a) * t)).magnitude;
        }

        static float Height(string name, System.Type layout)
        {
            Canvas.ForceUpdateCanvases();
            var rect = LobbyMenu.Instance.GetComponentsInChildren<RectTransform>().Single(r => r.name == name && r.GetComponent(layout));
            return rect.rect.height;
        }

        static GameObject Selected => EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;

        /// <summary>A pad press that the lobby reads on the next frame, as from a real controller.</summary>
        IEnumerator Press(GamepadButton button)
        {
            if (pad == null) pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        /// <summary>A key press that the lobby reads on the next frame.</summary>
        IEnumerator Tap(Key key)
        {
            if (keys == null)
            {
                // In the editor, keys reach play mode only while the Game view has focus, and a batch run has
                // none: the Input System lets them through regardless only with both of these set.
                keysRoute = InputSystem.settings.editorInputBehaviorInPlayMode;
                keysFocus = InputSystem.settings.backgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                keys = InputSystem.AddDevice<Keyboard>();
            }
            keys.MakeCurrent();
            InputSystem.QueueStateEvent(keys, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keys, new KeyboardState());
            yield return null;
        }

        /// <summary>A left click that starts and ends on this object, as the event system sends it.</summary>
        static void ClickOn(GameObject target)
        {
            var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            e.pointerPressRaycast = new RaycastResult { gameObject = target };
            ExecuteEvents.ExecuteHierarchy(target, e, ExecuteEvents.pointerClickHandler);
        }

        static void AssertFramed(LobbyStage stage, string why)
        {
            var cam = stage.Camera;
            var toYou = (stage.Spot + Vector3.up * .5f - cam.transform.position).normalized;
            Assert.Greater(Vector3.Dot(cam.transform.forward, toYou), .95f, why);
        }

        [UnityTest]
        public IEnumerator LobbyStandsYouInTheSelectedMap()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            var stage = menu.Stage;
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            Assert.AreEqual(Session.MapId, stage.Map, "the backdrop is the selected map");
            Assert.IsNotNull(Backdrop(stage), "the map is built behind you");
            Assert.IsNotNull(stage.Avatar, "your avatar stands in it");
            Assert.Greater(stage.Spot.magnitude, 100f, "built well away from the hub room");
            Assert.AreSame(Camera.main, stage.Camera);
            Assert.IsFalse(stage.Camera.orthographic);
            AssertFramed(stage, "the camera looks at you");
            // No stage: no rug lies on the floor between the camera and you.
            var eye = stage.Camera.transform.position;
            foreach (var rug in Backdrop(stage).GetComponentsInChildren<Transform>().Where(t => t.name is "RUG" or "Rug" || t.name.EndsWith("_Rug")))
                Assert.Greater(DistanceToSegment(rug.position, eye, stage.Spot), 2f, $"{rug.name} at {rug.position} is clear of the view");
            Assert.IsFalse(Object.FindAnyObjectByType<PlayerJoinManager>().AllowJoining, "clicking the hub no longer joins players");
            Assert.IsEmpty(Object.FindAnyObjectByType<PlayerJoinManager>().Players, "bots never stand in the lobby");
        }

        [UnityTest]
        public IEnumerator TopBarIsLaidOutLikeAPcShooter()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            var bar = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Top bar");
            var names = bar.GetComponentsInChildren<Button>().Select(b => b.name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "Home", "Settings", "Quit", "Leaderboard", "Shop", "LOADOUT", "PLAY", "CAREER" }, names,
                "no people, sound or help buttons: those live in the rail and Settings");

            float middle = ScreenX(menu.transform);
            float unit = Screen.width / 1920f * 10f;
            Assert.AreEqual(middle, ScreenX(Find("PLAY")), unit, "PLAY sits in the middle");
            Assert.Less(ScreenX(Find("LOADOUT")), ScreenX(Find("PLAY")));
            Assert.Greater(ScreenX(Find("CAREER")), ScreenX(Find("PLAY")));
            foreach (var left in new[] { "Home", "Settings", "Quit", "Leaderboard", "Shop" })
                Assert.Less(ScreenX(Find(left)), ScreenX(Find("LOADOUT")), $"{left} is on the left");
            var rail = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Party and friends");
            Assert.Greater(ScreenX(rail), Screen.width * .75f, "the party and friends rail is on the right");
        }

        [UnityTest]
        public IEnumerator TabsAndIconsShowOnePageAtATime()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            // Not the tab row, which is also called Pages.
            var host = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Pages" && r.parent.name == "Safe area");
            var feed = menu.GetComponentsInChildren<RectTransform>(true).Single(r => r.name == "Messages");
            foreach (var (button, page) in new[]
            {
                ("LOADOUT", LobbyMenu.Loadout), ("PLAY", LobbyMenu.Play), ("CAREER", LobbyMenu.CareerPage),
                ("Shop", LobbyMenu.Shop), ("Leaderboard", LobbyMenu.Trophy), ("Settings", LobbyMenu.Settings), ("Home", LobbyMenu.Home),
            })
            {
                Click(button);
                yield return null;
                Assert.AreEqual(page, menu.Current, button);
                var showing = host.Cast<Transform>().Where(t => t.gameObject.activeSelf).Select(t => t.name).ToArray();
                CollectionAssert.AreEqual(new[] { page + " page" }, showing, $"{button} shows only its own page");
                Assert.AreEqual(page == LobbyMenu.Home, feed.gameObject.activeSelf, "lobby messages show beside Home only");
            }

            // The loadout slides you to the left so the panel can fill the right.
            Click("LOADOUT");
            yield return new WaitForSeconds(1f);
            Assert.Less(menu.Stage.Camera.WorldToViewportPoint(menu.Stage.Spot).x, .42f, "you stand left of the loadout panel");
            Click("Home");
            yield return new WaitForSeconds(1f);
            Assert.AreEqual(.5f, menu.Stage.Camera.WorldToViewportPoint(menu.Stage.Spot).x, .03f, "back in the middle at home");
        }

        [UnityTest]
        public IEnumerator BarButtonsToggleTheirPage()
        {
            // "clicking to open and clicking to close" (user, 6 Oct 2026).
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            foreach (var (button, page) in new[]
            {
                ("LOADOUT", LobbyMenu.Loadout), ("PLAY", LobbyMenu.Play), ("CAREER", LobbyMenu.CareerPage),
                ("Shop", LobbyMenu.Shop), ("Leaderboard", LobbyMenu.Trophy), ("Settings", LobbyMenu.Settings),
            })
            {
                Click(button);
                yield return null;
                Assert.AreEqual(page, menu.Current, $"{button} opens its page");
                Assert.IsTrue(Find(button).TryGetComponent(out LobbyTab tab) ? tab.On : true, $"{button} shows it is open");
                Click(button);
                yield return null;
                Assert.AreEqual(LobbyMenu.Home, menu.Current, $"{button} again closes it");
                Assert.AreSame(Find(button).gameObject, Selected, $"a keyboard or controller is back on {button}, not on GO");
            }
            Click("Home");
            yield return null;
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "Home stays home");
        }

        [UnityTest]
        public IEnumerator PagesCloseFromTheirCornerEscAndTheSpaceRoundThem()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            foreach (var (button, page) in new[] { ("CAREER", LobbyMenu.CareerPage), ("PLAY", LobbyMenu.Play), ("Settings", LobbyMenu.Settings) })
            {
                Click(button);
                yield return null;
                Click("Close page");
                yield return null;
                Assert.AreEqual(LobbyMenu.Home, menu.Current, $"the X closes {page}");
                Assert.AreSame(Find(button).gameObject, Selected);
            }

            Click("LOADOUT");
            yield return null;
            yield return Tap(Key.Escape);
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "Esc closes the page");
            Assert.AreSame(Find("LOADOUT").gameObject, Selected);

            // Centred pages close on a click beside their panel; a click on the panel doesn't.
            Click("Leaderboard");
            yield return null;
            var shade = menu.GetComponentsInChildren<LobbyShade>().Single(s => s.name == "Shade");
            var panel = menu.GetComponentsInChildren<RectTransform>().First(r => r.name == "Panel");
            ClickOn(panel.gameObject);
            yield return null;
            Assert.AreEqual(LobbyMenu.Trophy, menu.Current, "a click on the panel stays");
            ClickOn(shade.gameObject);
            yield return null;
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "a click beside it closes it");
            Assert.AreSame(Find("Leaderboard").gameObject, Selected);
        }

        [UnityTest]
        public IEnumerator EscInTheNameFieldKeepsSettingsOpen()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("Settings");
            yield return null;
            var field = menu.GetComponentsInChildren<TMPro.TMP_InputField>().Single();
            EventSystem.current.SetSelectedGameObject(field.gameObject);
            field.ActivateInputField();
            yield return null;
            yield return null;
            Assert.IsTrue(field.isFocused, "typing a name");
            yield return Tap(Key.Escape);
            Assert.AreEqual(LobbyMenu.Settings, menu.Current, "Esc leaves the field, not the page");
            field.DeactivateInputField();
            EventSystem.current.SetSelectedGameObject(null);
            yield return null;
            yield return null;
            yield return Tap(Key.Escape);
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "out of the field, Esc closes the page");
        }

        [UnityTest]
        public IEnumerator AClickBesideTheQuitBoxMeansStay()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("Quit");
            yield return null;
            var dialog = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Quit dialog");
            ClickOn(dialog.Find("Box").gameObject);
            yield return null;
            Assert.IsTrue(dialog, "a click on the box itself stays open");
            ClickOn(dialog.gameObject);
            yield return null;
            Assert.IsFalse(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Quit dialog"), "a click beside it closes it");
            Assert.AreSame(Find("Quit").gameObject, Selected);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "and the game keeps running");
        }

        [UnityTest]
        public IEnumerator TheLobbyAndMatchesRenderSmoothedAndToneMapped()
        {
            yield return OpenLobby();
            var asset = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            Assert.AreEqual(4, asset.msaaSampleCount, "MSAA 4x on the PC pipeline");
            var cam = LobbyMenu.Instance.Stage.Camera;
            Assert.IsTrue(cam.GetUniversalAdditionalCameraData().renderPostProcessing, "the lobby camera post-processes");
            Assert.IsTrue(GraphicsOptions.Look, "the shared look volume exists");
            Assert.IsTrue(GraphicsOptions.Look.sharedProfile.TryGet(out Tonemapping tone) && tone.mode.value == TonemappingMode.ACES, "ACES, as on the web");
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.DibsScene);
            Assert.IsTrue(Camera.main.GetUniversalAdditionalCameraData().renderPostProcessing, "match cameras post-process too");
        }

        [UnityTest]
        public IEnumerator FixedRowsKeepTheirHeight()
        {
            // Rows report themselves flexible, so a sized row must not swallow its column's spare room.
            yield return OpenLobby();
            Assert.AreEqual(74f, Height("You", typeof(HorizontalLayoutGroup)), 1f, "your party card");
            Click("PLAY");
            yield return null;
            Assert.AreEqual(196f, Height("Modes", typeof(HorizontalLayoutGroup)), 1f, "the mode cards");
            Click("CAREER");
            yield return null;
            Assert.AreEqual(96f, Height("Level", typeof(HorizontalLayoutGroup)), 1f, "the level header");
            Assert.AreEqual(96f, Height("Stats", typeof(HorizontalLayoutGroup)), 1f, "the stat tiles");
        }

        [UnityTest]
        public IEnumerator DraggingTheOpenViewTurnsYou()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            var stage = menu.Stage;

            // The middle of the home screen is open: a press there lands on the turn area, not a panel.
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = new Vector2(Screen.width * .5f, Screen.height * .55f) }, hits);
            Assert.IsNotEmpty(hits);
            Assert.AreEqual("Turn area", hits[0].gameObject.name);

            float before = stage.Yaw;
            var facing = stage.Avatar.rotation;
            ExecuteEvents.Execute(hits[0].gameObject, new PointerEventData(EventSystem.current) { delta = new Vector2(-100f, 0f) }, ExecuteEvents.dragHandler);
            float turn = 100f * LobbyDrag.DegreesPerPixel * 1080f / Mathf.Max(1, Screen.height);
            Assert.AreEqual(Mathf.Repeat(before + turn, 360f), stage.Yaw, .01f);
            yield return new WaitForSeconds(.6f);
            Assert.AreEqual(turn, Quaternion.Angle(facing, stage.Avatar.rotation), 2f, "the avatar turns on the spot");
            Assert.AreEqual(menu.Stage.Spot, stage.Avatar.position, "without moving");
        }

        [UnityTest]
        public IEnumerator PlayPicksAModeAndMapThenGoStartsTheMatch()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            string other = GameConfig.Current.Houses.Keys.First(k => k != menu.Map);
            var oldBackdrop = Backdrop(menu.Stage);

            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Practice);
            yield return null;
            Click("Mode Duos");
            yield return null;
            Assert.AreEqual("Duos", menu.Mode);
            Click("Map " + other);
            yield return null;
            Assert.AreEqual(other, menu.Map);
            Assert.AreEqual(other, Session.MapId);
            Assert.AreEqual(other, menu.Stage.Map, "the backdrop follows the map");
            Assert.IsTrue(!oldBackdrop, "the old map is taken down");
            StringAssert.Contains(GameConfig.Current.HouseFor(other).Name, Backdrop(menu.Stage).name);
            StringAssert.Contains(GameConfig.Current.HouseFor(other).Name, menu.Messages[0]);
            AssertFramed(menu.Stage, "you stand in the new map");

            Click("GO");
            Assert.IsTrue(menu.Starting, "GO starts a countdown");
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "back to the lobby while it starts");
            Assert.IsTrue(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Starting"), "the countdown shows under the tabs");
            yield return TestScenes.WaitForActive(Session.DibsScene);
            Assert.AreEqual("Duos", Match.Mode);
            Assert.AreEqual(other, Session.MapId);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(1, joins.HumanCount, "you came along");
            Assert.AreEqual(4, joins.Players.Count, "bots fill the practice seats");

            // Home again, GO starts the same thing.
            Assert.IsTrue(Session.GoHome());
            yield return TestScenes.WaitForActive(Session.HubScene);
            yield return null;
            Assert.AreEqual(LobbyMenu.Practice, LobbyMenu.Instance.Queue);
            Assert.AreEqual("Duos", LobbyMenu.Instance.Mode, "the lobby remembers the mode");
            Assert.AreEqual(other, LobbyMenu.Instance.Map);
        }

        [UnityTest]
        public IEnumerator TheControllerThatPressesGoPlaysTheMatch()
        {
            yield return OpenLobby();
            Assert.AreSame(Find("GO").gameObject, Selected, "a controller starts on GO");
            yield return Press(GamepadButton.South);
            Assert.IsTrue(LobbyMenu.Instance.Starting, "A presses GO");
            yield return TestScenes.WaitForActive(Session.DibsScene);
            yield return null;
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(1, joins.HumanCount);
            Assert.IsTrue(joins.Players.Any(p => p.Binding is GamepadBinding g && g.Pad == pad), "the pad that pressed GO plays");
            Assert.IsFalse(joins.Players.Any(p => p.Binding is DesktopBinding), "the keyboard and mouse don't take a seat as well");
        }

        [UnityTest]
        public IEnumerator ACouchPlayerJoinsThePartyAndComesAlong()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Assert.IsTrue(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Join"), "the rail says how to join");
            yield return Press(GamepadButton.Start);
            Assert.AreEqual(2, menu.PartySize, "Start on a controller takes a seat");
            Assert.IsTrue(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Seat 2"), "the rail shows player 2");
            StringAssert.Contains("2 players + 2 bots", menu.Describe());
            yield return Press(GamepadButton.Start);
            Assert.AreEqual(2, menu.PartySize, "pressing Start again doesn't take a second seat");

            Click("GO");
            yield return TestScenes.WaitForActive(Session.DibsScene);
            yield return null;
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(2, joins.HumanCount, "the couch player came along");
            Assert.IsTrue(joins.Players.Any(p => p.Binding is DesktopBinding), "you play on the keyboard and mouse");
            Assert.IsTrue(joins.Players.Any(p => p.Binding is GamepadBinding g && g.Pad == pad), "player 2 plays on their controller");
            Assert.AreEqual(4, joins.Players.Count, "bots fill the other seats");

            Assert.IsTrue(Session.GoHome());
            yield return TestScenes.WaitForActive(Session.HubScene);
            yield return null;
            Assert.AreEqual(2, LobbyMenu.Instance.PartySize, "the party is still together back home");
            yield return Press(GamepadButton.Select);
            Assert.AreEqual(1, LobbyMenu.Instance.PartySize, "Select leaves the party");
        }

        [UnityTest]
        public IEnumerator APartyHasFourSeatsAndAnyoneCanLeave()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            menu.Join(new KeyboardBinding(KeyboardBinding.Side.Right));
            menu.Join(new ScriptedBinding());
            menu.Join(new ScriptedBinding());
            menu.Join(new ScriptedBinding());
            Assert.AreEqual(LobbyMenu.PartyMax, menu.PartySize, "four seats at most");
            StringAssert.Contains("4 players", menu.Describe());
            Assert.IsFalse(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Join"), "no join hint with every seat taken");
            menu.Choose(mode: "Duos");
            StringAssert.Contains("2 v 2", menu.Describe());

            Click("Leave 2");
            yield return null;
            Assert.AreEqual(3, menu.PartySize);
            Assert.IsFalse(menu.Party.Any(b => b is KeyboardBinding), "the keyboard's right half left");
            StringAssert.Contains("3 players and a bot", menu.Describe());
            Assert.IsTrue(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Join"), "a free seat shows how to join again");
        }

        [UnityTest]
        public IEnumerator AControllerCanCancelAndStepBack()
        {
            // Long enough that slow frames can't start the match before B.
            LobbyMenu.StartDelay = 5f;
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            yield return Press(GamepadButton.South);
            Assert.IsTrue(menu.Starting);
            Assert.AreSame(Find("CANCEL").gameObject, Selected, "GO hides, so the controller lands on CANCEL");
            yield return Press(GamepadButton.East);
            Assert.IsFalse(menu.Starting, "B cancels");
            Assert.AreSame(Find("GO").gameObject, Selected, "and the controller is back on GO");

            Click("Settings");
            yield return null;
            Click("Quit");
            yield return null;
            Assert.AreSame(Find("STAY").gameObject, Selected);
            Assert.AreEqual(Navigation.Mode.Explicit, Find("STAY").navigation.mode, "a controller can't wander out of the dialog");
            yield return Press(GamepadButton.East);
            Assert.IsFalse(menu.GetComponentsInChildren<RectTransform>().Any(r => r.name == "Quit dialog"), "B closes the dialog");
            Assert.AreEqual(LobbyMenu.Settings, menu.Current, "and only the dialog");
            Assert.AreSame(Find("Quit").gameObject, Selected, "focus goes back to the power button");
            yield return Press(GamepadButton.East);
            Assert.AreEqual(LobbyMenu.Home, menu.Current, "B again goes home");
        }

        [UnityTest]
        public IEnumerator RenamingChangesTheBadgeAndOldLooksAreSaved()
        {
            var outfit = GameConfig.Current.Wardrobe.Default.Clone();
            outfit.Colours["Top"] = "grape";
            PlayerPrefs.SetString(LobbyMenu.OutfitKey, outfit.Serialize());
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Assert.AreEqual("grape", menu.Outfit.ColourOf("Top"));
            Assert.IsTrue(MatchTally.LoadCareer().Owns("colour", "grape"), "a colour worn before the shop is saved as yours");

            menu.Career.Name = "Zed";
            menu.SaveCareer();
            var badge = menu.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Badge");
            Assert.AreEqual("Z", badge.GetComponentInChildren<TMPro.TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator CancelKeepsYouInTheLobby()
        {
            LobbyMenu.StartDelay = .5f;
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("GO");
            Assert.IsTrue(menu.Starting);
            Assert.AreSame(Find("CANCEL").gameObject, Selected, "a keyboard lands on CANCEL");
            Click("CANCEL");
            Assert.IsFalse(menu.Starting);
            Assert.AreSame(Find("GO").gameObject, Selected, "and back on GO after");
            yield return new WaitForSeconds(.8f);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
            Assert.AreSame(menu, LobbyMenu.Instance);
            Find("GO");
        }

        [UnityTest]
        public IEnumerator MatchmakingSaysWhyItCannotStartYet()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Matchmaking);
            yield return null;
            Find("Mode " + LobbyMenu.RoomMode);
            Assert.IsFalse(Find("GO").interactable, "GO is off until online play exists");
            menu.Go();
            Assert.IsFalse(menu.Starting);
            StringAssert.Contains("online or LAN", menu.Messages[0]);
            Click("Home");
            yield return null;
            Assert.IsFalse(Find("GO").interactable);
            Assert.IsTrue(Find("CHANGE").interactable, "you can still change the queue");
        }

        [UnityTest]
        public IEnumerator LoadoutAndShopChangeWhatYouWear()
        {
            PlayerPrefs.SetString(MatchTally.CareerKey, new Career { Coins = 500 }.Serialize());
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("LOADOUT");
            yield return null;
            Click("Slot Top");
            yield return null;
            Click("Colour mint");
            yield return null;
            Assert.AreEqual("mint", menu.Outfit.ColourOf("Top"));
            Assert.AreEqual("mint", Outfit.Deserialize(PlayerPrefs.GetString(LobbyMenu.OutfitKey)).ColourOf("Top"), "saved");

            Click("Colour sky");
            yield return null;
            Assert.AreEqual(LobbyMenu.Shop, menu.Current, "a colour you don't own opens the shop");
            Assert.AreEqual("mint", menu.Outfit.ColourOf("Top"));

            Click("Buy colour:sky");
            yield return null;
            Assert.AreEqual(380, menu.Career.Coins);
            Assert.IsTrue(menu.Career.Owns("colour", "sky"));
            Assert.AreEqual(380, MatchTally.LoadCareer().Coins, "the purchase is saved");
            Click("Wear colour:sky");
            yield return null;
            Assert.AreEqual("sky", menu.Outfit.ColourOf("Top"));

            Click("Buy skin:Arcade");
            yield return null;
            Assert.AreEqual(380, menu.Career.Coins, "Arcade costs 400");
            Assert.IsFalse(menu.Career.Owns("skin", "Arcade"));
        }

        [UnityTest]
        public IEnumerator WorkshopOpensOverTheLobbyAndComesBack()
        {
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Click("PLAY");
            yield return null;
            Click("Queue " + LobbyMenu.Workshop);
            yield return null;
            Click("Mode " + LobbyMenu.WorkshopMode);
            yield return null;
            var lobbyLook = menu.Stage.Camera.backgroundColor;
            Click("GO");
            yield return TestScenes.WaitUntil(() => CreativeWorkshop.Instance, 3f, "the workshop to open");
            yield return null;
            Assert.IsFalse(menu.isActiveAndEnabled && menu.GetComponent<Canvas>().enabled, "the lobby steps aside");
            var cam = Camera.main;
            Assert.IsFalse(cam.clearFlags == CameraClearFlags.SolidColor && cam.backgroundColor == lobbyLook,
                "the workshop shows the hub's own sky, not the lobby's warm wash");

            CreativeWorkshop.Instance.Close();
            yield return null;
            yield return null;
            Assert.IsTrue(menu.isActiveAndEnabled && menu.GetComponent<Canvas>().enabled, "the lobby comes back");
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            AssertFramed(menu.Stage, "the camera is yours again");
            Assert.AreEqual(lobbyLook, menu.Stage.Camera.backgroundColor, "with the lobby's wash");
            Find("CHANGE");
            Assert.AreSame(Find("GO").gameObject, Selected, "GO is back, with a controller on it");
        }
    }
}
