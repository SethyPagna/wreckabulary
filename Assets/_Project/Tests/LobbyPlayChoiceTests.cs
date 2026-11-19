using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules.Wordbound;
using HouseModeFamily = Wreckabulary.Rules.HouseModeFamily;
using HouseModes = Wreckabulary.Rules.HouseModes;

namespace Wreckabulary.Tests
{
    public sealed class LobbyPlayChoiceTests
    {
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, QuickPlayChoices.StorageKey, FriendRooms.StorageKey };
        string[] saved;
        float volume, delay;
        int vsync, tutorial;
        bool hadTutorial;
        string homes, priorHomes;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            volume = AudioListener.volume; vsync = QualitySettings.vSyncCount; delay = LobbyMenu.StartDelay;
            hadTutorial = PlayerPrefs.HasKey(TutorialDirector.DoneKey); tutorial = PlayerPrefs.GetInt(TutorialDirector.DoneKey, 0);
            priorHomes = HomeStorage.DefaultFolder;
            homes = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "wreckabulary-lobby-homes-" + System.Guid.NewGuid().ToString("N"));
            HomeStorage.DefaultFolder = homes;
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetInt(TutorialDirector.DoneKey, 1);
            yield return OpenLobby();
            LobbyMenu.Instance.Open(LobbyMenu.Play);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        static IEnumerator OpenLobby()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                2f, "play choices ready");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            if (hadTutorial) PlayerPrefs.SetInt(TutorialDirector.DoneKey, tutorial); else PlayerPrefs.DeleteKey(TutorialDirector.DoneKey);
            PlayerPrefs.Save();
            AudioListener.volume = volume; QualitySettings.vSyncCount = vsync; LobbyMenu.StartDelay = delay;
            HomeStorage.DefaultFolder = priorHomes;
            if (System.IO.Directory.Exists(homes)) System.IO.Directory.Delete(homes, true);
        }

        static Wreckabulary.Rules.HomeLayout SaveHome(string map, string name)
        {
            var designer = new Wreckabulary.Rules.HomeDesigner(GameConfig.Current.Houses, GameConfig.Current.Items);
            var house = GameConfig.Current.HouseFor(map);
            var batch = designer.AddWords(designer.CreateLayout(map, name), "TABLE PLANT", house.Rooms[0].Name);
            Assert.IsTrue(batch.Ok, string.Join("\n", batch.Errors));
            Assert.IsTrue(new HomeStorage(designer).TrySave(batch.Layout, out var error), error);
            return batch.Layout;
        }

        static RectTransform Page => LobbyMenu.Instance.Page<PlayPage>().Root;
        static Button Find(string name) => Page.GetComponentsInChildren<Button>().Single(button => button.name == name);
        static string Detail => Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Mode detail").text;
        static string[] Modes => Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Mode "))
            .Select(button => button.name.Substring(5)).ToArray();

        static string Summary => Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Matchmaking playlists").text;
        const string BrokenRoom = "wb-room:1234567890abcdef:NoSuchMode:Solo:asia:p1:0123456789abcdef:m1";
        static string FriendsRoom => Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Friends room").text;
        static string FriendsDetail => Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Friends detail").text;
        static TMP_InputField RoomField => Page.GetComponentsInChildren<TMP_InputField>().Single(field => field.name == PlayPage.RoomCodeField);
        static TMP_InputField PartyField => Page.GetComponentsInChildren<TMP_InputField>().Single(field => field.name == PlayPage.PartyCodeField);
        static string Named(TMP_Text[] texts, string name) => texts.Single(text => text.name == name).text;
        static string[] Named(string prefix) => Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(prefix))
            .Select(button => button.name.Substring(prefix.Length)).ToArray();
        static bool Ticked(string id) => Find(PlayPage.PlaylistCard + id).GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "Selected");

        static void Submit(string name)
        {
            var button = Find(name);
            Assert.IsTrue(button.IsInteractable(), name);
            EventSystem.current.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
        }

        static void Pick(string mode)
        {
            string id = PlayPage.ModeButton(mode);
            if (!Page.GetComponentsInChildren<Button>().Any(button => button.name == id)) Submit(PlayPage.GroupTab + HouseModes.FamilyOf(mode));
            Submit(id);
        }

        static string[] HouseModesBeside(string mode) => HouseModes.ModesIn(HouseModes.FamilyOf(mode)).Where(HouseModes.Has).ToArray();

        static string[] GroupTabs => Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(PlayPage.GroupTab))
            .Select(button => button.name).ToArray();

        static bool Marked(string name) => Find(name).GetComponentsInChildren<RectTransform>(true).Any(rect => rect.name == "Selected");

        [UnityTest]
        public IEnumerator CenteredDirectModesStayVisibleWhenTheSharedMapGalleryScrolls()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat"); menu.Open(LobbyMenu.Play);
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(900, 580) })
            {
                Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = size;
                Pick("TimeRun"); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var row = frame.Body.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Modes");
                var queueBounds = InPage(frame.Queues); var modeBounds = InPage(row);
                var scroll = frame.Choices.GetComponentInChildren<ScrollRect>();
                Assert.That(queueBounds.center.x, Is.EqualTo(InPage(frame.Panel).center.x).Within(.5f));
                Assert.That(modeBounds.center.x, Is.EqualTo(InPage(frame.Panel).center.x).Within(.5f));
                Assert.LessOrEqual(modeBounds.yMax, queueBounds.yMin);
                Assert.GreaterOrEqual(modeBounds.yMin, InPage(scroll.viewport).yMax);
                Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(b => b.name.StartsWith("Family ")));
                var targets = HouseModes.Families.Select(family => PlayPage.GroupTab + family)
                    .Concat(HouseModes.ModesIn(HouseModeFamily.Arcade).Select(PlayPage.ModeButton)).ToArray();
                CollectionAssert.AreEqual(targets, row.GetComponentsInChildren<Button>().Select(b => b.name).ToArray(), "four group tabs, then the Arcade modes");
                foreach (string id in targets)
                {
                    var button = Find(id); var bounds = InPage((RectTransform)button.transform);
                    Assert.IsTrue(button.IsInteractable()); Assert.IsTrue(button.transform.IsChildOf(row));
                    Assert.GreaterOrEqual(bounds.yMin, modeBounds.yMin - .5f); Assert.LessOrEqual(bounds.yMax, modeBounds.yMax + .5f);
                }
                Assert.Less(InPage((RectTransform)Find("Solo TimeRun").transform).yMax, InPage((RectTransform)Find(PlayPage.GroupTab + HouseModeFamily.Arcade).transform).yMin,
                    "the group's modes sit on the row under the group tabs");
                Submit("Solo map ParcelService"); yield return null; yield return null;
                frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                row = frame.Body.GetComponentsInChildren<RectTransform>().Single(r => r.name == "Modes");
                AssertSameBounds(modeBounds, InPage(row), "Selecting/revealing a map cannot scroll the mode selector.");
                AssertSameBounds(queueBounds, InPage(frame.Queues), "The category selector remains fixed.");
                Pick("Freestyle"); yield return null;
                Assert.AreEqual(WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.SelectedMap);
                Pick("Duos"); yield return null;
                Assert.AreEqual("flat", menu.Map); Assert.AreEqual("Duos", menu.Mode, "the Classic tab returns to the last Classic mode");
            }
        }

        [UnityTest]
        public IEnumerator SingleChoiceCardsKeepTheHouseCardFootprintAndFixedActions()
        {
            var menu = LobbyMenu.Instance;
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(1388, 968), new Vector2(900, 580) })
            {
                menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel");
                menu.Page<PlayPage>().WordboundSolo = false;
                menu.Open(LobbyMenu.Play);
                Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = size;
                yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var reference = InPage((RectTransform)Find("Map pinwheel").transform);
                float practiceHeader = InPage(Strip).height;
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var header = InPage(frame.Queues);
                var footer = InPage((RectTransform)Find("GO").transform);

                Pick("Escape"); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var escape = Find("Map pinwheel");
                Assert.AreEqual(GameConfig.Current.Houses.Count, Page.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Map ")),
                    "Escape the House offers the same tickable houses as every other mode.");
                Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name == "Map random"), "there is no separate Random card");
                AssertCardFootprint(reference, (RectTransform)escape.transform, Mathf.Abs(InPage(Strip).height - practiceHeader) < .5f);
                EventSystem.current.SetSelectedGameObject(escape.gameObject);
                yield return null; yield return null;
                AssertSameBounds(header, InPage(Page.GetComponentInChildren<LobbyPlayPageView>().Queues), "Header stays fixed.");
                AssertSameBounds(footer, InPage((RectTransform)Find("GO").transform), "Play stays fixed.");
                Assert.AreSame(escape.gameObject, EventSystem.current.currentSelectedGameObject);
                Assert.AreEqual("pinwheel", menu.Map);

                Submit("Queue " + LobbyMenu.Workshop); yield return null;
                Submit("Create Wordbound"); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var editor = Find("Wordbound maps");
                var createRow = Page.GetComponentInChildren<LobbyPlayPageView>().Body.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Create modes");
                AssertCardFootprint(reference, (RectTransform)editor.transform, Mathf.Abs(InPage(createRow).height - practiceHeader) < .5f);
                Assert.AreEqual(1, editor.transform.parent.GetComponentsInChildren<Button>().Length,
                    "The editor card does not add fake choices to fill the row.");
                AssertSameBounds(header, InPage(Page.GetComponentInChildren<LobbyPlayPageView>().Queues), "Creator header stays fixed.");
                AssertSameBounds(footer, InPage((RectTransform)Find("Workshop OPEN").transform), "Open uses the fixed action slot.");
                Assert.AreEqual(Session.CanLoad(Session.WordboundScene), Find("Workshop OPEN").interactable);
                foreach (var text in editor.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.text);
                }
                Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
                Assert.IsFalse(menu.Starting, "Choosing a creator family must not launch it.");
            }
        }

        static void AssertCardFootprint(Rect reference, RectTransform card, bool sameHeader)
        {
            var actual = InPage(card);
            Assert.That(actual.width, Is.EqualTo(reference.width).Within(.1f), "One card keeps the multi-map card width.");
            if (sameHeader) Assert.That(actual.height, Is.EqualTo(reference.height).Within(.1f), "One card keeps the multi-map card height.");
            else Assert.LessOrEqual(actual.height, LobbyPlayCardGrid.HeightFor(actual.width) + .1f, "A card never grows past its 16:9 picture and caption.");
            Assert.That(actual.xMin, Is.EqualTo(reference.xMin).Within(.1f), "One card aligns with the first map column.");
        }

        [UnityTest]
        public IEnumerator ShortHouseChooserScrollsFocusedChoicesWithoutMovingHeaderOrPlay()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat");
            menu.Open(LobbyMenu.Play);
            Page.anchorMin = Page.anchorMax = Vector2.zero;
            Page.sizeDelta = new Vector2(900, 580);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
            var scroll = frame.Choices.GetComponentInChildren<ScrollRect>();
            Assert.IsNotNull(scroll, "Short house choices need a reachable scroll region.");
            Assert.Greater(scroll.content.rect.height, scroll.viewport.rect.height);
            Assert.IsTrue(scroll.verticalScrollbar.gameObject.activeSelf);
            var header = InPage(frame.Queues);
            var footer = InPage((RectTransform)Find("GO").transform);
            var viewport = InPage(scroll.viewport);
            Assert.LessOrEqual(viewport.yMax, header.yMin);
            Assert.GreaterOrEqual(viewport.yMin, footer.yMax);
            Assert.GreaterOrEqual(((RectTransform)Find("Mode Dibs").transform).rect.height, 44,
                "The compact fixed mode strip retains accessible targets.");
            var mapNames = Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Map "))
                .Select(button => button.name).ToArray();
            Assert.AreEqual(GameConfig.Current.Houses.Count, mapNames.Length, "Every installed house remains focusable.");
            foreach (string name in mapNames)
            {
                var target = Find(name);
                EventSystem.current.SetSelectedGameObject(target.gameObject);
                yield return null;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertSameBounds(viewport, InPage(scroll.viewport), "Revealing a card cannot change the fixed map viewport.");
                var visible = InPage((RectTransform)target.transform);
                Assert.GreaterOrEqual(visible.yMin, viewport.yMin - .1f, name + " enters the viewport");
                Assert.LessOrEqual(visible.yMax, viewport.yMax + .1f, name + " enters the viewport");
                AssertSameBounds(header, InPage(frame.Queues), "Scrolling must not move the queue strip.");
                AssertSameBounds(footer, InPage((RectTransform)Find("GO").transform), "Play stays fixed.");
                Assert.AreSame(target.gameObject, EventSystem.current.currentSelectedGameObject);
            }
            Page.sizeDelta = new Vector2(1868, 968);
            EventSystem.current.SetSelectedGameObject(Find("Mode Dibs").gameObject);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(scroll.content.rect.height > scroll.viewport.rect.height + .1f,
                scroll.verticalScrollbar.gameObject.activeSelf, "Only overflowing map galleries expose a scrollbar.");
            Assert.AreEqual("Mode Dibs", EventSystem.current.currentSelectedGameObject.name);
            var map = Find("Map flat");
            EventSystem.current.SetSelectedGameObject(map.gameObject);
            yield return null;
            yield return null;
            Page.sizeDelta = new Vector2(900, 580);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            viewport = InPage(scroll.viewport);
            var focused = InPage((RectTransform)map.transform);
            Assert.AreSame(map.gameObject, EventSystem.current.currentSelectedGameObject, "Resize keeps the selected map.");
            Assert.GreaterOrEqual(focused.yMin, viewport.yMin - .1f, "Resize reveals the same selected map.");
            Assert.LessOrEqual(focused.yMax, viewport.yMax + .1f, "Resize reveals the same selected map.");
            scroll.verticalNormalizedPosition = 1f;
            yield return null;
            yield return null;
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(.01f),
                "Manual scrolling must not jump back to an unchanged selection.");
            Assert.AreEqual("Duos", menu.Mode); Assert.AreEqual("flat", menu.Map);
        }

        [UnityTest]
        public IEnumerator WordboundFamiliesFillThePageWithoutChangingChoicesOrFooter()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "ShoppingList", "flat");
            menu.Open(LobbyMenu.Play);
            yield return null;
            Submit("Solo TimeRun");
            yield return null;
            Assert.AreEqual(Session.CanLoad(Session.WordboundScene), Find("Solo GO").interactable);
            var focus = EventSystem.current.currentSelectedGameObject;
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(1388, 968), new Vector2(900, 920), new Vector2(900, 580), new Vector2(1868, 968) })
            {
                Page.anchorMin = Page.anchorMax = Vector2.zero;
                Page.sizeDelta = size;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertFullFamily(size, "Modes", "Solo summary");
                Assert.AreSame(focus, EventSystem.current.currentSelectedGameObject, "resizing must not recreate focus");
            }
            Assert.AreEqual("ShoppingList", menu.Mode, "a course beside a house mode keeps the house choice"); Assert.AreEqual("flat", menu.Map);
            var second = new ScriptedBinding();
            menu.Join(second);
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.IsFalse(menu.Page<PlayPage>().WordboundSolo, "a second seat leaves the solo-only courses");
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(900, 580) })
            {
                Page.sizeDelta = size;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertFullHousePanel();
                AssertStripFits();
            }
            CollectionAssert.AreEqual(HouseModesBeside("ShoppingList"), Modes);
            Assert.AreEqual(PlayPage.SoloOnly("TimeRun"), Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.SoloNote).text);
            Assert.AreEqual("ShoppingList", menu.Mode); Assert.AreEqual("flat", menu.Map);
            Assert.AreSame(second, menu.Party.Single());
            Submit("Queue " + LobbyMenu.Workshop);
            yield return null;
            Submit("Create Wordbound");
            yield return null;
            foreach (var size in new[] { new Vector2(1868, 968), new Vector2(900, 580) })
            {
                Page.sizeDelta = size;
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertFullFamily(size, "Wordbound workshop", "Workshop summary");
                Assert.AreEqual(Session.CanLoad(Session.WordboundScene), Find("Workshop OPEN").interactable);
            }
            Submit("Create house");
            yield return null;
            Canvas.ForceUpdateCanvases();
            AssertFullHousePanel();
            CollectionAssert.IsEmpty(Modes, "Create offers its two tools without play modes");
            menu.Leave(second);
        }

        [UnityTest]
        public IEnumerator EveryPlayFamilyKeepsTheFullSurfaceAndSavedProfileAcrossResize()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat"); menu.Open(LobbyMenu.Play);
            var profile = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            string savedProfile = JsonUtility.ToJson(profile);
            using var viewport = new StageViewport(menu);
            foreach (string family in new[] { "Solo TimeRun", "Mode Duos", "Create Wordbound", "Create house" })
            {
                if (family == "Create Wordbound") { Submit("Queue " + LobbyMenu.Workshop); yield return null; }
                if (family.StartsWith("Create ")) Submit(family); else Pick(family.Substring(5));
                yield return null;
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var focus = EventSystem.current.currentSelectedGameObject;
                foreach (var size in new[] { new Vector2(1920, 1080), new Vector2(854, 480), new Vector2(2560, 1080), new Vector2(1920, 1080) })
                {
                    viewport.Set(size.x, size.y); yield return null; yield return null;
                    Canvas.ForceUpdateCanvases();
                    Assert.AreSame(frame, Page.GetComponentInChildren<LobbyPlayPageView>(), "Resize must not recreate the frame.");
                    Assert.AreSame(focus, EventSystem.current.currentSelectedGameObject, "Resize retains keyboard/controller focus.");
                    AssertFullHousePanel();
                    Assert.AreEqual(LobbyStage.Focus.Centre, menu.Page<PlayPage>().Focus, "Play does not reserve space for an avatar.");
                }
                if (family.StartsWith("Create "))
                {
                    Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode);
                    Assert.IsTrue(Wreckabulary.Rules.HomeDesigner.Supports(menu.Map), "The home builder must normalize an unsupported house choice.");
                }
                else Assert.AreEqual("flat", menu.Map, "Practice choices preserve the original house selection.");
            }
            Assert.IsTrue(Wreckabulary.Rules.HomeDesigner.Supports(menu.Map));
            Submit("Queue " + LobbyMenu.Practice); yield return null;
            Assert.AreEqual("flat", menu.Map, "Leaving Create gives the practice house back.");
            Assert.AreEqual(savedProfile, JsonUtility.ToJson(profile), "Presentation must not mutate the saved profile.");
        }

        sealed class StageViewport : System.IDisposable
        {
            readonly Canvas canvas;
            readonly CanvasScaler scaler;
            readonly Camera camera;
            readonly Vector2 size;
            readonly float scale, aspect;
            readonly bool scaled;
            public readonly RectTransform Safe;

            public StageViewport(LobbyMenu menu)
            {
                canvas = menu.GetComponent<Canvas>(); scaler = menu.GetComponent<CanvasScaler>(); camera = menu.Stage.Camera;
                Safe = (RectTransform)menu.transform.Find("Safe area");
                size = Safe.sizeDelta; scale = canvas.scaleFactor; scaled = scaler.enabled; aspect = camera.aspect;
                scaler.enabled = false; canvas.scaleFactor = Screen.height / 1080f;
                Canvas.ForceUpdateCanvases();
            }

            public void Set(float width, float height)
            {
                var requested = new Vector2(width / height * 1080f, 1080f);
                Safe.sizeDelta += requested - Safe.rect.size;
                camera.aspect = width / height;
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                if (Safe) Safe.sizeDelta = size;
                if (camera) camera.aspect = aspect;
                if (canvas) canvas.scaleFactor = scale;
                if (scaler) scaler.enabled = scaled;
                Canvas.ForceUpdateCanvases();
            }
        }

        [UnityTest]
        public IEnumerator SavedPlayFrameSuppliesRuntimeRegionsWithoutReplacingActions()
        {
            var saved = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            Assert.IsNotNull(saved); saved.Validate();
            var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
            Assert.IsNotNull(frame); Assert.AreNotSame(saved.Page, frame);
            Assert.AreEqual(saved.Page.ContentInset, frame.ContentInset);
            Assert.AreEqual(saved.Page.HeaderHeight, frame.HeaderHeight);
            Assert.AreEqual(saved.Page.SummaryHeight, frame.SummaryHeight);
            AssertFullHousePanel();
            Assert.IsTrue(Find("Queue " + LobbyMenu.Practice).transform.IsChildOf(frame.Queues));
            Assert.IsTrue(Find("GO").transform.IsChildOf(frame.Summary));
            CollectionAssert.AreEqual(HouseModes.Families.Select(family => PlayPage.GroupTab + family), GroupTabs);
            CollectionAssert.AreEqual(HouseModesBeside(LobbyMenu.Instance.Mode), Modes);
            yield return null;
        }

        static RectTransform Strip => Page.GetComponentInChildren<LobbyPlayPageView>().Body.GetComponentsInChildren<RectTransform>()
            .Single(rect => rect.name == "Modes");

        static void AssertStripFits()
        {
            var strip = Strip;
            var bounds = InPage(strip);
            Assert.That(bounds.center.x, Is.EqualTo(InPage(Page.GetComponentInChildren<LobbyPlayPageView>().Panel).center.x).Within(.5f));
            foreach (var button in strip.GetComponentsInChildren<Button>())
            {
                var at = InPage((RectTransform)button.transform);
                Assert.GreaterOrEqual(at.xMin, bounds.xMin - .5f, button.name);
                Assert.LessOrEqual(at.xMax, bounds.xMax + .5f, button.name);
                Assert.GreaterOrEqual(at.yMin, bounds.yMin - .5f, button.name);
                Assert.LessOrEqual(at.yMax, bounds.yMax + .5f, button.name);
                Assert.GreaterOrEqual(at.height, 44f, button.name);
            }
            foreach (var text in strip.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.name + ": " + text.text);
            }
        }

        static void AssertFullHousePanel()
        {
            var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
            Assert.IsNotNull(frame, "The real Play page must instantiate the saved frame.");
            var panel = frame.Panel;
            Assert.IsNull(panel.GetComponent<LobbyCompactPanel>());
            Assert.That(panel.rect.width, Is.EqualTo(Page.rect.width).Within(.1f));
            Assert.That(panel.rect.height, Is.EqualTo(Page.rect.height).Within(.1f));
        }

        static void AssertFullFamily(Vector2 available, string rowName, string summaryName)
        {
            AssertFullHousePanel();
            var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
            var panelBounds = InPage(frame.Panel);
            var scroll = frame.Choices.GetComponentInChildren<ScrollRect>();
            Assert.IsNotNull(scroll, "Every family keeps a reachable choices viewport.");
            var viewport = InPage(scroll.viewport);
            var summary = InPage(frame.Summary);
            Assert.LessOrEqual(summary.yMax, viewport.yMin - 1f, "The footer stays below choices.");
            Assert.LessOrEqual(viewport.yMax, InPage(frame.Queues).yMin, "Queue choices stay outside the scroll region.");
            Assert.IsTrue(frame.Body.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == rowName));
            Assert.IsTrue(frame.Summary.GetComponentsInChildren<RectTransform>().Any(rect => rect.name == summaryName));
            foreach (var button in frame.Queues.GetComponentsInChildren<Button>().Concat(frame.Summary.GetComponentsInChildren<Button>()))
            {
                var bounds = InPage((RectTransform)button.transform);
                Assert.GreaterOrEqual(bounds.xMin, panelBounds.xMin - .1f, button.name);
                Assert.LessOrEqual(bounds.xMax, panelBounds.xMax + .1f, button.name);
                Assert.GreaterOrEqual(bounds.yMin, panelBounds.yMin - .1f, button.name);
                Assert.LessOrEqual(bounds.yMax, panelBounds.yMax + .1f, button.name);
            }
            foreach (var text in frame.Panel.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.name + ": " + text.text);
            }
        }

        static Rect InPage(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = Page.InverseTransformPoint(corners[0]);
            var max = Page.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        static void AssertSameBounds(Rect expected, Rect actual, string message)
        {
            Assert.That(actual.xMin, Is.EqualTo(expected.xMin).Within(.1f), message);
            Assert.That(actual.xMax, Is.EqualTo(expected.xMax).Within(.1f), message);
            Assert.That(actual.yMin, Is.EqualTo(expected.yMin).Within(.1f), message);
            Assert.That(actual.yMax, Is.EqualTo(expected.yMax).Within(.1f), message);
        }

        [Test]
        public void LocalCreatorIntentIsOneShotExclusiveAndResetWithSession()
        {
            WordboundSoloEntry.Request("Freestyle");
            WordboundWorkshopEntry.Request();
            Assert.IsNull(WordboundSoloEntry.TakeRequest());
            Assert.IsTrue(WordboundWorkshopEntry.TakeRequest());
            Assert.IsFalse(WordboundWorkshopEntry.TakeRequest());
            WordboundWorkshopEntry.MarkReturned();
            Assert.IsTrue(WordboundWorkshopEntry.TakeReturn());
            Assert.IsFalse(WordboundWorkshopEntry.TakeReturn());
            WordboundWorkshopEntry.Request();
            WordboundSoloEntry.Request("TimeRun");
            Assert.IsFalse(WordboundWorkshopEntry.TakeRequest());
            Assert.AreEqual("TimeRun", WordboundSoloEntry.TakeRequest());
            WordboundWorkshopEntry.Request(); WordboundWorkshopEntry.MarkReturned();
            Session.Clear();
            Assert.IsFalse(WordboundWorkshopEntry.TakeRequest());
            Assert.IsFalse(WordboundWorkshopEntry.TakeReturn());
        }

        [UnityTest]
        public IEnumerator CreateFamilyKeepsHomeModeMapAndEveryLocalSeat()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Workshop, LobbyMenu.WorkshopMode, "courtyard"); menu.Open(LobbyMenu.Play);
            Assert.AreEqual("courtyard", menu.Map, "Start with a supported, non-default home-builder map.");
            var second = new KeyboardBinding(KeyboardBinding.Side.Right);
            menu.Join(second);
            var bindings = Session.Bindings.ToArray();
            Submit("Create Wordbound"); yield return null;
            Assert.IsTrue(menu.Page<PlayPage>().WordboundMaps);
            CollectionAssert.IsEmpty(Modes);
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")));
            Assert.IsTrue(Find("Workshop OPEN").interactable);
            Assert.AreEqual("Local editor. One-player playtests.", Page.GetComponentsInChildren<TMP_Text>()
                .Single(text => text.name == "Workshop detail").text);
            Assert.AreEqual(LobbyMenu.Workshop, menu.Queue); Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode);
            Assert.AreEqual("courtyard", menu.Map); Assert.AreEqual(2, menu.PartySize);
            Assert.AreSame(second, menu.Party.Single()); CollectionAssert.AreEqual(bindings, Session.Bindings);
            Submit("Create house"); yield return null;
            CollectionAssert.IsEmpty(Modes);
            Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode); Assert.AreEqual("courtyard", menu.Map);
            Assert.AreSame(second, menu.Party.Single()); CollectionAssert.AreEqual(bindings, Session.Bindings);
            menu.Leave(second);
        }

        [UnityTest]
        public IEnumerator CreateToolsKeepOneSelectedModeAfterEditorSwitches()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Workshop, LobbyMenu.WorkshopMode, "courtyard"); menu.Open(LobbyMenu.Play);
            var bindings = Session.Bindings.ToArray();
            foreach (string id in new[] { "Create Wordbound", "Create house", "Create Wordbound", "Create house" })
            {
                Submit(id); yield return null;
                Canvas.ForceUpdateCanvases();
                var row = Page.GetComponentInChildren<LobbyPlayPageView>().Body.GetComponentsInChildren<RectTransform>()
                    .Single(rect => rect.name == "Create modes");
                foreach (var button in row.GetComponentsInChildren<Button>())
                {
                    button.GetComponent<LobbyPress>().Hot(false);
                    var underline = button.GetComponentsInChildren<Image>().Single(image => image.name == "Underline");
                    Assert.AreEqual(button.name == id, underline.enabled, "Only the active Create tool retains its selection marker.");
                    foreach (var text in button.GetComponentsInChildren<TMP_Text>())
                    {
                        text.ForceMeshUpdate();
                        Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, "Create mode label fits immediately: " + text.text);
                    }
                }
                Assert.AreEqual(LobbyMenu.Workshop, menu.Queue);
                Assert.AreEqual("courtyard", menu.Map);
                Assert.IsTrue(Find(id == "Create Wordbound" ? "Workshop OPEN" : "GO").interactable);
                Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
                Assert.IsFalse(menu.Starting, "Changing a Create tool cannot launch it.");
                CollectionAssert.AreEqual(bindings, Session.Bindings);
            }
        }

        [UnityTest]
        public IEnumerator LocalAndCreateOfferActualModesWithConciseCards()
        {
            Assert.AreEqual("Practice (Offline)", Find("Queue " + LobbyMenu.Practice).GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Matchmaking", Find("Queue " + LobbyMenu.Matchmaking).GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Create", Find("Queue " + LobbyMenu.Workshop).GetComponentInChildren<TMP_Text>().text);
            CollectionAssert.AreEqual(HouseModesBeside("Dibs"), Modes);
            foreach (var family in HouseModes.Families)
            {
                Submit(PlayPage.GroupTab + family);
                yield return null;
                Assert.AreEqual(LobbyKit.Upper(HouseModes.FamilyName(family)), Find(PlayPage.GroupTab + family).GetComponentInChildren<TMP_Text>().text);
                foreach (string mode in HouseModes.ModesIn(family))
                {
                    var button = Find(PlayPage.ModeButton(mode));
                    Assert.AreEqual(1, button.GetComponentsInChildren<TMP_Text>().Length, "one concise mode label: " + button.name);
                    Assert.AreEqual(LobbyPlayModeLabels.Name(mode), button.GetComponentInChildren<TMP_Text>().text);
                    Assert.IsFalse(button.GetComponentsInChildren<RawImage>().Any(), "Mode choices leave imagery to the map gallery.");
                }
            }
            Pick("Dibs");
            yield return null;
            Submit("Queue " + LobbyMenu.Workshop);
            yield return null;
            CollectionAssert.IsEmpty(Modes);
            Assert.AreEqual(LobbyMenu.Workshop, LobbyMenu.Instance.Queue);
            Assert.AreEqual(LobbyMenu.WorkshopMode, LobbyMenu.Instance.Mode);
            CollectionAssert.AreEqual(new[] { "Create house", "Create Wordbound" }, Page.GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith("Create ")).Select(button => button.name).ToArray());
            Assert.IsFalse(Page.GetComponentsInChildren<Transform>().Any(child => child.name == "Tutorial room"), "the tutorial lives in Practice");
            Submit("Queue " + LobbyMenu.Practice);
            yield return null;
            CollectionAssert.AreEqual(HouseModesBeside("Dibs"), Modes);
            Assert.AreEqual("Dibs", LobbyMenu.Instance.Mode);
            Assert.AreSame(Find("Queue " + LobbyMenu.Practice).gameObject, EventSystem.current.currentSelectedGameObject);
        }

        [UnityTest]
        public IEnumerator SummaryUsesActualBotPolicyAndUpdatesWhenAPlayerJoins()
        {
            var menu = LobbyMenu.Instance;
            foreach (string mode in LobbyMenu.Modes)
            {
                Pick(mode);
                yield return null;
                Assert.AreEqual(Wreckabulary.Rules.HouseModes.Find(mode).BotSeats > 0, Detail.ToLowerInvariant().Contains("bot"), mode);
                Assert.AreEqual(1, Page.GetComponentsInChildren<TMP_Text>().Count(text => text.name == "Mode detail"));
                Canvas.ForceUpdateCanvases();
                var detail = Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Mode detail");
                detail.ForceMeshUpdate();
                Assert.IsFalse(detail.isTextOverflowing, "selected objective/participants fit: " + mode);
                Assert.IsTrue(Find("GO").interactable);
            }
            Pick("Duos");
            yield return null;
            menu.Join(new ScriptedBinding());
            yield return null;
            StringAssert.Contains("2 players, each with a bot", Detail);
            Assert.AreSame(Find("Mode Duos").gameObject, EventSystem.current.currentSelectedGameObject,
                "party refresh preserves selected mode focus");
            Pick("Escape");
            yield return null;
            StringAssert.Contains("2 players", Detail);
            Assert.IsFalse(Detail.ToLowerInvariant().Contains("bot"));
            Assert.AreEqual(GameConfig.Current.Houses.Count, Page.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Map ")),
                "Escape the House picks from the ticked houses like every mode");
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name == "Map random"));
            Canvas.ForceUpdateCanvases();
            string footer = LobbyMenu.ModeName("Escape") + Wreckabulary.Rules.MapCardText.Separator + Wreckabulary.Rules.PracticeMaps.Every;
            Assert.IsTrue(Page.GetComponentsInChildren<TMP_Text>().Any(text => text.text == footer), "every house is ticked on a first visit: " + footer);
            Assert.IsFalse(menu.Messages.Any(message => message.StartsWith("Map: ")),
                "map state is shown in the ticked cards and footer, without stale map notifications");
        }

        [UnityTest]
        public IEnumerator TutorialIsTheFirstPracticeChoiceWithItsOwnRoom()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, LobbyMenu.TutorialMode);
            menu.Open(LobbyMenu.Play);
            yield return null;
            CollectionAssert.AreEqual(new[] { LobbyMenu.TutorialMode }, Modes, "the tutorial leads the Training group");
            Assert.IsTrue(Find(PlayPage.GroupTab + HouseModeFamily.Training).GetComponentsInChildren<Image>().Single(image => image.name == "Underline").enabled);
            CollectionAssert.AreEqual(new[] { "Solo Freestyle" }, Page.GetComponentsInChildren<Button>()
                .Where(b => b.name.StartsWith("Solo ") && !b.name.StartsWith("Solo map ") && b.name != "Solo GO").Select(b => b.name).ToArray());
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue);
            Assert.AreEqual(LobbyMenu.TutorialMode, menu.Mode);
            Assert.AreSame(Find("Mode " + LobbyMenu.TutorialMode).gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.IsTrue(Page.GetComponentsInChildren<Transform>().Any(child => child.name == "Tutorial room"));
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")), "the tutorial has its own room");
            Assert.IsTrue(Find("GO").interactable);
            Submit("Queue " + LobbyMenu.Workshop);
            yield return null;
            Assert.AreEqual(LobbyMenu.WorkshopMode, menu.Mode);
            CollectionAssert.IsEmpty(Modes);
            Submit("Queue " + LobbyMenu.Practice);
            yield return null;
            Assert.AreEqual("Dibs", menu.Mode, "a finished tutorial hands Practice to Dibs");
        }

        [UnityTest]
        public IEnumerator TutorialIsSelectedUntilItIsFinished()
        {
            yield return TestScenes.Reset();
            PlayerPrefs.DeleteKey(TutorialDirector.DoneKey);
            yield return OpenLobby();
            var menu = LobbyMenu.Instance;
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue);
            Assert.AreEqual(LobbyMenu.TutorialMode, menu.Mode, "a first visit starts on the tutorial");
            menu.Open(LobbyMenu.Play);
            yield return null;
            Assert.AreSame(Find("Mode " + LobbyMenu.TutorialMode).gameObject, EventSystem.current.currentSelectedGameObject);
            Submit("Queue " + LobbyMenu.Workshop); yield return null;
            Submit("Queue " + LobbyMenu.Practice); yield return null;
            Assert.AreEqual(LobbyMenu.TutorialMode, menu.Mode, "Practice keeps offering the tutorial first");
            Assert.AreEqual(LobbyMenu.TutorialMode, Session.LobbyMode);
            TutorialDirector.MarkDone();
            yield return OpenLobby();
            Assert.AreEqual("Dibs", LobbyMenu.Instance.Mode, "finishing the tutorial moves the default to Dibs");
        }

        [UnityTest]
        public IEnumerator GroupTabsShowOneGroupAndSoloCoursesHideWhileExtraSeatsAreTaken()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel"); menu.Open(LobbyMenu.Play);
            yield return null;
            var strip = Strip;
            CollectionAssert.AreEqual(new[] { PlayPage.GroupTabs, HouseModes.FamilyName(HouseModeFamily.Classic) },
                strip.Cast<Transform>().Select(row => row.name).ToArray(), "group tabs, then the chosen group's modes");
            CollectionAssert.AreEqual(new[] { "CLASSIC", "CO-OP", "ARCADE", "TRAINING" },
                strip.Find(PlayPage.GroupTabs).GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray());
            Assert.IsFalse(strip.GetComponentsInChildren<TMP_Text>().Any(text => text.name == LobbyPlayModeFamilies.Caption), "no captions above the rows");
            foreach (var family in HouseModes.Families)
            {
                Submit(PlayPage.GroupTab + family); yield return null;
                Canvas.ForceUpdateCanvases();
                strip = Strip;
                var row = strip.Find(HouseModes.FamilyName(family));
                Assert.IsNotNull(row, family.ToString());
                CollectionAssert.AreEqual(HouseModes.ModesIn(family).Select(PlayPage.ModeButton).ToArray(),
                    row.GetComponentsInChildren<Button>().Select(button => button.name).ToArray(), family.ToString());
                Assert.AreEqual(family, HouseModes.FamilyOf(menu.Page<PlayPage>().WordboundSolo ? WordboundSoloEntry.SelectedMode : menu.Mode),
                    "a group tab selects one of its own modes");
                foreach (var tab in HouseModes.Families)
                    Assert.AreEqual(tab == family, Find(PlayPage.GroupTab + tab).GetComponentsInChildren<Image>().Single(image => image.name == "Underline").enabled, tab.ToString());
                Assert.AreSame(Find(PlayPage.GroupTab + family).gameObject, EventSystem.current.currentSelectedGameObject, "focus stays on the tab");
                Assert.IsFalse(strip.GetComponentsInChildren<TMP_Text>().Any(text => text.name == PlayPage.SoloNote));
                AssertStripFits();
            }
            Submit(PlayPage.GroupTab + HouseModeFamily.Classic); yield return null;
            Assert.AreEqual("Free-for-All", Find("Mode " + HouseModes.FreeForAll).GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Dibs", menu.Mode, "the Classic tab returns to its last mode");
            var second = new ScriptedBinding();
            menu.Join(second);
            yield return null;
            foreach (var (family, course) in new[] { (HouseModeFamily.Arcade, "TimeRun"), (HouseModeFamily.Training, "Freestyle") })
            {
                Submit(PlayPage.GroupTab + family); yield return null;
                Canvas.ForceUpdateCanvases();
                strip = Strip;
                Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Solo ")),
                    "courses are hidden, not just disabled");
                var note = strip.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.SoloNote);
                Assert.AreEqual(PlayPage.SoloOnly(course), note.text);
                Assert.IsTrue(note.transform.IsChildOf(strip.Find(HouseModes.FamilyName(family))));
                CollectionAssert.AreEqual(HouseModes.ModesIn(family).Where(HouseModes.Has).ToArray(), Modes, "every house party mode stays");
                Assert.IsTrue(HouseModes.Has(menu.Mode) && !menu.Page<PlayPage>().WordboundSolo);
                AssertStripFits();
            }
            menu.Leave(second);
            yield return null;
            Assert.IsTrue(Find("Solo Freestyle").IsInteractable());
            Assert.IsFalse(Page.GetComponentsInChildren<TMP_Text>().Any(text => text.name == PlayPage.SoloNote));
        }

        [UnityTest]
        public IEnumerator ASavedHomeIsOfferedOnlyForLastTeamStanding()
        {
            var saved = SaveHome("courtyard", "Test Den");
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "flat"); menu.Open(LobbyMenu.Play);
            yield return null;
            string card = PlayPage.SavedHomeCard + "courtyard";
            Assert.IsTrue(Find(card).GetComponentsInChildren<TMP_Text>().Any(text => text.text == "Test Den"));
            Assert.AreEqual(GameConfig.Current.Houses.Count, Page.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith("Map ")));
            Submit(card); yield return null;
            Assert.AreEqual("courtyard", menu.Map); Assert.AreEqual("courtyard", menu.SavedHome);
            Assert.AreEqual("Test Den", menu.SavedHomeName);
            StringAssert.Contains("Test Den", menu.Describe());
            Assert.AreSame(Find(card).gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.IsFalse(GameConfig.Current.Houses.Keys.Any(id => Marked("Map " + id)), "a chosen saved home replaces the house ticks on screen");
            Assert.AreEqual("courtyard", menu.NextMap(), "a saved home always plays its own house");
            Pick("Duos"); yield return null;
            Assert.AreEqual("courtyard", menu.SavedHome, "Duos plays the saved home too");
            Assert.IsTrue(Find(card).GetComponentsInChildren<Transform>().Any(child => child.name == "Selected"));
            foreach (string mode in new[] { "MovingOut", "MovingDay", "Escape" })
            {
                Pick(mode); yield return null;
                Assert.IsNull(menu.SavedHome, mode + " needs objectives that a home save does not have");
                Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith(PlayPage.SavedHomeCard)), mode);
            }
            Pick("Dibs"); yield return null;
            Submit(card); yield return null;
            Submit("Map courtyard"); yield return null;
            Assert.IsNull(menu.SavedHome, "the original house card plays the original furniture");
            Assert.AreEqual(saved.Map, menu.Map);
            Assert.IsTrue(menu.Maps.All, "leaving a saved home brings the earlier ticks back");
            Assert.IsTrue(GameConfig.Current.Houses.Keys.All(id => Marked("Map " + id)));
        }

        [UnityTest]
        public IEnumerator GoPlaysTheSavedHomeInsteadOfTheOriginalFurniture()
        {
            var saved = SaveHome("pinwheel", "Party Place");
            LobbyMenu.StartDelay = .2f;
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos");
            Assert.IsTrue(menu.ChooseHome("pinwheel"));
            menu.Go();
            Assert.IsTrue(menu.Starting);
            yield return TestScenes.WaitForActive(Session.DibsScene, 8f);
            Assert.AreEqual("Duos", Match.Mode);
            var room = Object.FindAnyObjectByType<RoomBuilder>();
            Assert.AreEqual("Party Place", room.HomeName);
            CollectionAssert.AreEquivalent(saved.Props.Select(prop => prop.Word), room.Originals.Select(prop => prop.Word));
            Assert.IsTrue(Session.GoHome());
            yield return TestScenes.WaitForActive(Session.HubScene);
            yield return null;
            Assert.AreEqual("pinwheel", LobbyMenu.Instance.SavedHome, "the lobby remembers the saved home");
            Assert.AreEqual("Duos", LobbyMenu.Instance.Mode);
        }

        [UnityTest]
        public IEnumerator StaleMatchmakingCannotLaunchHouseModesOrOfferFakeOnlineCards()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Matchmaking, LobbyMenu.RoomMode);
            menu.Open(LobbyMenu.Play);
            yield return null;
            Assert.IsEmpty(Modes);
            Assert.IsFalse(Find("GO").interactable);
            menu.Go();
            Assert.IsFalse(menu.Starting);
            StringAssert.Contains("local", menu.Messages[0]);
            Assert.IsTrue(Find("Queue " + LobbyMenu.Matchmaking).interactable, "the working online front door remains available");
            Submit("Queue " + LobbyMenu.Practice);
            yield return null;
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue);
            Assert.AreEqual("Dibs", menu.Mode);
            Assert.IsTrue(Find("GO").interactable);
        }

        [Test]
        public void SoloIntentIsExplicitOneShotAndRejectsMultiplayerModes()
        {
            try
            {
                WordboundSoloEntry.Clear();
                Assert.AreEqual(WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.SelectedMap, "Parcel Depot is the default course");
                foreach (string map in new[] { "flat", "Parcelhall", "", null })
                    Assert.Throws<System.ArgumentException>(() => WordboundSoloEntry.SelectMap(map));
                WordboundSoloEntry.SelectMap(WordboundSoloEntry.ParcelHallMap);
                foreach (string mode in new[] { "TeamRace", "FreeForAll", "Brawl", "Dibs", "", null })
                    Assert.Throws<System.ArgumentException>(() => WordboundSoloEntry.Request(mode));
                foreach (string mode in new[] { "TimeRun", "Freestyle" })
                {
                    WordboundSoloEntry.Request(mode);
                    Assert.AreEqual(mode, WordboundSoloEntry.TakeRequest());
                    Assert.IsNull(WordboundSoloEntry.TakeRequest());
                    Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
                }
                WordboundSoloEntry.Request("TimeRun");
                WordboundSoloEntry.CancelRequest();
                Assert.IsNull(WordboundSoloEntry.TakeRequest());
                WordboundSoloEntry.MarkReturned();
                Assert.IsTrue(WordboundSoloEntry.TakeReturn());
                Assert.IsFalse(WordboundSoloEntry.TakeReturn());
                WordboundSoloEntry.Request("Freestyle");
                Session.Clear();
                Assert.IsNull(WordboundSoloEntry.TakeRequest());
                Assert.AreEqual("TimeRun", WordboundSoloEntry.SelectedMode);
                Assert.AreEqual(WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.SelectedMap, "Parcel Depot is the default course");
            }
            finally { WordboundSoloEntry.Clear(); }
        }

        [UnityTest]
        public IEnumerator ParcelServiceChoiceUsesAnOptionalRealPreviewAndStaysReachableOnShortFrames()
        {
            var profile = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            var old = profile.ParcelServicePreview;
            var texture = new Texture2D(128, 64);
            var preview = Sprite.Create(texture, new Rect(0, 0, 128, 64), new Vector2(.5f, .5f));
            try
            {
                profile.ParcelServicePreview = null;
                var menu = LobbyMenu.Instance;
                menu.Choose(LobbyMenu.Practice, "ShoppingList", "flat"); menu.Open(LobbyMenu.Play);
                var bindings = Session.Bindings.ToArray();
                Submit("Solo TimeRun"); yield return null;
                Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = new Vector2(900, 580);
                yield return null; yield return null; Canvas.ForceUpdateCanvases();
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var footer = InPage((RectTransform)Find("Solo GO").transform);
                var card = Find("Solo map ParcelService");
                Assert.IsFalse(card.GetComponentsInChildren<RawImage>().Any(), "A missing map preview must not pretend an item is the map.");
                AssertSoloFallback("ParcelService");
                Submit("Solo map ParcelService"); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                card = Find("Solo map ParcelService");
                var scroll = Page.GetComponentInChildren<LobbyPlayPageView>().Choices.GetComponentInChildren<ScrollRect>();
                var shown = InPage((RectTransform)card.transform); var viewport = InPage(scroll.viewport);
                Assert.GreaterOrEqual(shown.yMin, viewport.yMin - .5f);
                Assert.LessOrEqual(shown.yMax, viewport.yMax + .5f);
                AssertSameBounds(footer, InPage((RectTransform)Find("Solo GO").transform), "Third map focus cannot move Play.");
                Assert.AreEqual(WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.SelectedMap);
                Assert.AreEqual("Solo map ParcelService", EventSystem.current.currentSelectedGameObject.name);
                profile.ParcelServicePreview = preview; menu.Open(LobbyMenu.Play); yield return null;
                Canvas.ForceUpdateCanvases(); AssertSoloPreview("ParcelService", preview);
                Assert.AreEqual("ShoppingList", menu.Mode); Assert.AreEqual("flat", menu.Map);
                CollectionAssert.AreEqual(bindings, Session.Bindings);
                WordboundSoloEntry.Clear(); Assert.AreEqual(WordboundSoloEntry.ParcelServiceMap, WordboundSoloEntry.SelectedMap, "Parcel Depot is the default course");
            }
            finally
            {
                profile.ParcelServicePreview = old;
                UnityEngine.Object.DestroyImmediate(preview); UnityEngine.Object.DestroyImmediate(texture);
                WordboundSoloEntry.Clear();
            }
        }

        [UnityTest]
        public IEnumerator SoloMapPreviewsKeepSelectionAndFallBackIndependently()
        {
            var profile = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            Assert.IsNotNull(profile);
            var savedLost = profile.LostAddressPreview;
            var savedParcel = profile.ParcelHallPreview;
            var texture = new Texture2D(128, 64);
            var lost = Sprite.Create(texture, new Rect(0, 0, 128, 64), new Vector2(.5f, .5f));
            var parcel = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(.5f, .5f));
            try
            {
                profile.LostAddressPreview = lost; profile.ParcelHallPreview = parcel;
                Assert.DoesNotThrow(profile.Validate);
                WordboundSoloEntry.SelectMap(WordboundSoloEntry.LostAddressMap);
                var menu = LobbyMenu.Instance;
                menu.Choose(LobbyMenu.Practice, "ShoppingList", "flat"); menu.Open(LobbyMenu.Play);
                var bindings = Session.Bindings.ToArray();
                Submit("Solo TimeRun"); yield return null;
                foreach (var size in new[] { new Vector2(1868, 968), new Vector2(900, 580) })
                {
                    Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = size;
                    yield return null; yield return null;
                    Canvas.ForceUpdateCanvases();
                    AssertSoloPreview("LostAddress", lost);
                    AssertSoloPreview("ParcelHall", parcel);
                }
                Submit("Solo map ParcelHall"); yield return null;
                Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
                Assert.AreEqual("Solo map ParcelHall", EventSystem.current.currentSelectedGameObject.name);
                AssertSoloPreview("ParcelHall", parcel);

                profile.LostAddressPreview = null;
                menu.Open(LobbyMenu.Play); yield return null;
                AssertSoloFallback("LostAddress");
                AssertSoloPreview("ParcelHall", parcel);
                profile.ParcelHallPreview = null;
                Assert.DoesNotThrow(profile.Validate);
                menu.Open(LobbyMenu.Play); yield return null;
                AssertSoloFallback("LostAddress");
                AssertSoloFallback("ParcelHall");
                Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
                Assert.AreEqual("ShoppingList", menu.Mode); Assert.AreEqual("flat", menu.Map);
                CollectionAssert.AreEqual(bindings, Session.Bindings);
            }
            finally
            {
                profile.LostAddressPreview = savedLost; profile.ParcelHallPreview = savedParcel;
                Object.Destroy(lost); Object.Destroy(parcel); Object.Destroy(texture);
            }
        }

        [UnityTest]
        public IEnumerator SoloPreviewUsesCardWidthWithReadableLabelsAndFixedFooterOnShortFrames()
        {
            var profile = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            var oldPreview = profile.ParcelServicePreview;
            float oldHeight = profile.MapMinimumHeight;
            var texture = new Texture2D(160, 90);
            var preview = Sprite.Create(texture, new Rect(0, 0, 160, 90), new Vector2(.5f, .5f));
            try
            {
                profile.ParcelServicePreview = preview; profile.MapMinimumHeight = 480;
                var menu = LobbyMenu.Instance;
                foreach (var size in new[] { new Vector2(1868, 968), new Vector2(900, 580) })
                {
                    menu.Choose(LobbyMenu.Practice, "ShoppingList", "flat");
                    menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play);
                    Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = size;
                    yield return null; yield return null; Canvas.ForceUpdateCanvases();
                    var house = InPage((RectTransform)Find("Map flat").transform);
                    var footer = InPage((RectTransform)Find("GO").transform);
                    Submit("Solo TimeRun"); yield return null; yield return null;
                    Submit("Solo map ParcelService"); yield return null; yield return null;
                    Canvas.ForceUpdateCanvases();
                    AssertSoloPreview("ParcelService", preview);
                    var card = Find("Solo map ParcelService");
                    var art = card.GetComponentsInChildren<Image>().Single(image => image.name == "Map preview");
                    var imageRect = art.rectTransform.rect;
                    float displayedWidth = Mathf.Min(imageRect.width, imageRect.height * 16f / 9f);
                    Assert.That(displayedWidth, Is.EqualTo(imageRect.width).Within(.5f),
                        "A 16:9 map should fill the padded card width without cropping or distortion.");
                    var scroll = Page.GetComponentInChildren<LobbyPlayPageView>().Choices.GetComponentInChildren<ScrollRect>();
                    var shown = InPage((RectTransform)card.transform); var viewport = InPage(scroll.viewport);
                    Assert.GreaterOrEqual(shown.yMin, viewport.yMin - .5f);
                    Assert.LessOrEqual(shown.yMax, viewport.yMax + .5f);
                    AssertSameBounds(footer, InPage((RectTransform)Find("Solo GO").transform), "Larger map previews cannot move Play.");
                    Assert.AreSame(card.gameObject, EventSystem.current.currentSelectedGameObject);
                    foreach (var text in card.GetComponentsInChildren<TMP_Text>())
                    { text.ForceMeshUpdate(); Assert.IsFalse(text.isTextOverflowing, text.text); }
                    Submit("Mode ShoppingList"); yield return null; yield return null; Canvas.ForceUpdateCanvases();
                    AssertSameBounds(house, InPage((RectTransform)Find("Map flat").transform), "House card geometry stays unchanged.");
                }
            }
            finally
            {
                profile.ParcelServicePreview = oldPreview; profile.MapMinimumHeight = oldHeight;
                Object.Destroy(preview); Object.Destroy(texture);
            }
        }

        static void AssertSoloPreview(string id, Sprite authored)
        {
            var card = Find("Solo map " + id);
            var image = card.GetComponentsInChildren<Image>().Single(item => item.name == "Map preview");
            Assert.AreSame(LobbyMapSnapshots.For(id) ?? authored, image.sprite, "A captured snapshot comes first, then the play layout preview.");
            Assert.IsTrue(image.preserveAspect); Assert.IsFalse(image.raycastTarget);
            Assert.AreEqual(Color.white, image.color);
            Assert.IsFalse(card.GetComponentsInChildren<RawImage>().Any(item => item.name.StartsWith("Art ")));
            AssertNameOnPicture(card, InPage((RectTransform)image.transform.parent), id);
            var selected = card.GetComponentsInChildren<Transform>().SingleOrDefault(item => item.name == "Selected");
            if (selected) Assert.Greater(selected.GetSiblingIndex(), image.transform.GetSiblingIndex(),
                "The image cannot cover the selected mark.");
        }

        static void AssertSoloFallback(string id)
        {
            var card = Find("Solo map " + id);
            var snapshot = LobbyMapSnapshots.For(id);
            if (snapshot) { AssertSoloPreview(id, snapshot); return; }
            Assert.IsFalse(card.GetComponentsInChildren<Image>().Any(item => item.name == "Map preview"));
            Assert.IsFalse(card.GetComponentsInChildren<RawImage>().Any(), "The fallback is a tinted card with a symbol, not item art.");
            var fallback = card.GetComponentsInChildren<Image>().Single(item => item.name == "Map fallback");
            Assert.IsTrue(fallback.GetComponentsInChildren<Image>().Any(item => item.name.StartsWith("Icon ")));
        }

        [UnityTest]
        public IEnumerator SoloFamilyPreservesHouseSelectionPartyAndOriginalArtwork()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "ShoppingList", "flat");
            menu.Open(LobbyMenu.Play);
            var bindings = Session.Bindings.ToArray();
            yield return null;
            Submit("Solo TimeRun");
            yield return null;
            CollectionAssert.AreEqual(HouseModesBeside("TimeRun"), Modes, "House choices beside the course stay directly reachable.");
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Map ")));
            CollectionAssert.AreEqual(new[] { "Solo TimeRun" },
                Page.GetComponentsInChildren<Button>().Where(button => button.name == "Solo TimeRun" || button.name == "Solo Freestyle")
                    .Select(button => button.name).ToArray(), "Free Roam waits in the Training group");
            CollectionAssert.AreEqual(new[] { "Solo map ParcelService", "Solo map LostAddress", "Solo map ParcelHall" },
                Page.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Solo map "))
                    .Select(button => button.name).ToArray());
            Assert.AreEqual(LobbyPlayModeLabels.Name("TimeRun"), Find("Solo TimeRun").GetComponentInChildren<TMP_Text>().text);
            Assert.IsFalse(Find("Solo TimeRun").GetComponentsInChildren<RawImage>().Any());
            Submit("Solo map ParcelHall");
            yield return null;
            Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
            Assert.AreEqual("Solo map ParcelHall", EventSystem.current.currentSelectedGameObject.name);
            Assert.AreEqual("ShoppingList", menu.Mode); Assert.AreEqual("flat", menu.Map);
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue);
            CollectionAssert.AreEqual(bindings, Session.Bindings);
            Pick("Freestyle");
            yield return null;
            Assert.AreEqual("Freestyle", WordboundSoloEntry.SelectedMode);
            Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap);
            Assert.AreEqual(LobbyPlayModeLabels.Name("Freestyle"), Find("Solo Freestyle").GetComponentInChildren<TMP_Text>().text);
            Assert.IsFalse(Find("Solo Freestyle").GetComponentsInChildren<RawImage>().Any());
            Assert.AreEqual(LobbyMenu.TutorialMode, menu.Mode, "opening Training picked its first house choice before Free Roam");
            var second = new ScriptedBinding();
            menu.Join(second);
            yield return null;
            Assert.IsFalse(menu.Page<PlayPage>().WordboundSolo, "a second seat returns to the house party");
            Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith("Solo ")));
            menu.OpenWordboundSolo("TimeRun");
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
            Assert.IsNull(WordboundSoloEntry.TakeRequest());
            CollectionAssert.AreEqual(HouseModesBeside("Freestyle"), Modes);
            Assert.AreEqual(LobbyMenu.TutorialMode, menu.Mode); Assert.AreEqual("flat", menu.Map);
            Assert.AreSame(second, menu.Party.Single());
            menu.Leave(second);
            yield return null;
            Pick("TimeRun");
            yield return null;
            Assert.AreEqual("TimeRun", WordboundSoloEntry.SelectedMode, "the Arcade tab returns to its last course");
            Assert.AreEqual(WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.SelectedMap, "the course choice survives the extra seat");
            Assert.AreEqual(Session.CanLoad(Session.WordboundScene), Find("Solo GO").interactable);
            Canvas.ForceUpdateCanvases();
            foreach (var text in Page.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.name + ": " + text.text);
            }
        }

        [UnityTest]
        public IEnumerator HouseCardsNameOnePictureAtASteadyLeftAlignedWidth()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel");
            menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play);
            Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = new Vector2(1868, 968);
            yield return null; yield return null; Canvas.ForceUpdateCanvases();
            var grid = Page.GetComponentsInChildren<GridLayoutGroup>().Single(row => row.name == "Houses");
            var row = InPage((RectTransform)grid.transform);
            var cards = grid.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Map ")).ToArray();
            Assert.AreEqual(GameConfig.Current.Houses.Count, cards.Length);
            Assert.That(grid.cellSize.x, Is.EqualTo(LobbyPlayCardGrid.PreferredWidth).Within(.5f), "A wide page keeps the steady card width.");
            Assert.LessOrEqual(grid.constraintCount, 3);
            var first = InPage((RectTransform)cards[0].transform);
            Assert.That(first.xMin, Is.EqualTo(row.xMin).Within(.5f), "Cards start at the left edge.");
            Assert.Less(cards.Max(card => InPage((RectTransform)card.transform).xMax), row.xMax - 100f, "Cards never stretch to fill the row.");
            foreach (var card in cards)
            {
                string id = card.name.Substring(4);
                var layout = GameConfig.Current.Houses[id];
                var bounds = InPage((RectTransform)card.transform);
                Assert.That(bounds.width, Is.EqualTo(first.width).Within(.1f)); Assert.That(bounds.height, Is.EqualTo(first.height).Within(.1f));
                Assert.IsFalse(card.GetComponentsInChildren<RectTransform>().Any(part => part.name == "Fitted plan" || part.name == "Storey" || part.name == "Start"),
                    "The floor-plan skeleton is gone: " + id);
                var picture = card.GetComponentsInChildren<RectTransform>().Single(part => part.name == "Map preview" || part.name == "Map fallback");
                var shape = InPage(picture.name == "Map preview" ? (RectTransform)picture.parent : picture);
                Assert.That(shape.width / shape.height, Is.EqualTo(16f / 9f).Within(.02f), id + " shows one 16:9 picture");
                var title = card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map title");
                var detail = card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map detail");
                Assert.AreEqual(layout.Name, title.text);
                Assert.AreEqual(Wreckabulary.Rules.MapCardText.HouseLine(layout), detail.text);
                AssertNameOnPicture(card, shape, id);
                Assert.AreEqual(LobbyMapSnapshots.For(id) != null, picture.name == "Map preview", id);
                foreach (var text in new[] { title, detail }) { text.ForceMeshUpdate(); Assert.IsFalse(text.isTextTruncated || text.isTextOverflowing, text.text); }
            }
        }

        [UnityTest]
        public IEnumerator EveryHouseModeOffersTickedHousesAndEachMatchPicksOneOfThem()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel");
            menu.Page<PlayPage>().WordboundSolo = false; menu.Page<PlayPage>().WordboundMaps = false; menu.Open(LobbyMenu.Play);
            Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = new Vector2(1868, 968);
            yield return null;
            var houses = GameConfig.Current.Houses.Keys.ToArray();
            var cards = houses.Select(id => "Map " + id).ToArray();
            Assert.IsTrue(menu.Maps.All, "a first visit ticks every house");
            foreach (string mode in LobbyMenu.Modes)
            {
                Pick(mode); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var grid = Page.GetComponentsInChildren<GridLayoutGroup>().Single(row => row.name == "Houses");
                CollectionAssert.AreEqual(cards, grid.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Map ")).Select(button => button.name).ToArray(), mode);
                Assert.IsFalse(Page.GetComponentsInChildren<Button>().Any(button => button.name == "Map random"), mode + ": no separate Random card");
                Assert.IsTrue(cards.All(Marked), mode + ": every ticked house shows its check");
            }
            Pick("Dibs"); yield return null;
            Submit(cards[0]); yield return null;
            Assert.IsFalse(menu.Maps.IsTicked(houses[0]), "a second tap unticks a house");
            Assert.IsFalse(Marked(cards[0])); Assert.AreSame(Find(cards[0]).gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreEqual(menu.Maps.Save(), Session.LobbyMaps, "ticks persist like the map choice");
            StringAssert.Contains("Any of " + (houses.Length - 1) + " houses", menu.Describe());
            foreach (string card in cards.Skip(1).Take(houses.Length - 2)) { Submit(card); yield return null; }
            CollectionAssert.AreEqual(new[] { houses.Last() }, menu.Maps.Ticked);
            Submit(cards.Last()); yield return null;
            Assert.IsTrue(menu.Maps.IsTicked(houses.Last()), "the last ticked house stays ticked");
            CollectionAssert.Contains(menu.Messages, PlayPage.KeepOneMap);
            Assert.AreEqual(houses.Last(), menu.Map, "one ticked house is the house you stand in");
            StringAssert.Contains(GameConfig.Current.HouseFor(houses.Last()).Name, menu.Describe());
            menu.Roll = _ => throw new System.InvalidOperationException("one house never rolls");
            Assert.AreEqual(houses.Last(), menu.NextMap());
            Submit(cards[1]); yield return null;
            CollectionAssert.AreEqual(new[] { houses[1], houses.Last() }, menu.Maps.Ticked);
            menu.Roll = count => 0; Assert.AreEqual(houses[1], menu.NextMap());
            menu.Roll = count => count - 1; Assert.AreEqual(houses.Last(), menu.NextMap());
            Pick("Duos"); yield return null;
            Assert.IsTrue(Marked(cards[1]) && Marked(cards.Last()) && !Marked(cards[0]), "ticks stay the same in the next mode");
            Submit("Queue " + LobbyMenu.Workshop); yield return null;
            Assert.IsTrue(GameConfig.Current.Houses.ContainsKey(menu.Map) && Wreckabulary.Rules.HomeDesigner.Supports(menu.Map), "The Home builder edits one real house.");
            Assert.AreEqual(1, cards.Count(Marked), "the Home builder still picks a single house");
            Submit("Queue " + LobbyMenu.Practice); yield return null;
            CollectionAssert.AreEqual(new[] { houses[1], houses.Last() }, menu.Maps.Ticked, "leaving the Home builder keeps the ticks");
            Assert.IsTrue(Marked(cards[1]) && Marked(cards.Last()));
        }

        [UnityTest]
        public IEnumerator GoPlaysTheRolledHouseAndTheTicksSurviveTheMatch()
        {
            LobbyMenu.StartDelay = .2f;
            var menu = LobbyMenu.Instance;
            var houses = GameConfig.Current.Houses.Keys.ToArray();
            menu.Choose(LobbyMenu.Practice, "Dibs", houses[0]); menu.Open(LobbyMenu.Play);
            yield return null;
            foreach (string id in houses.Skip(3)) { Submit("Map " + id); yield return null; }
            CollectionAssert.AreEqual(houses.Take(3), menu.Maps.Ticked);
            menu.Roll = count => { Assert.AreEqual(3, count); return 2; };
            menu.Go();
            Assert.IsTrue(menu.Starting);
            yield return TestScenes.WaitForActive(Session.DibsScene, 8f);
            Assert.AreEqual(houses[2], Session.MapId, "the injected roll picked the third ticked house");
            Assert.IsTrue(Session.GoHome());
            yield return TestScenes.WaitForActive(Session.HubScene);
            yield return null;
            CollectionAssert.AreEqual(houses.Take(3), LobbyMenu.Instance.Maps.Ticked, "the ticks come back after the match");
            Assert.AreEqual(houses[2], LobbyMenu.Instance.Map, "the lobby stands in the house just played");
        }

        [UnityTest]
        public IEnumerator TheModeGuideIconShowsOnlyWhenAGuideExists()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel");
            menu.Page<PlayPage>().WordboundSolo = false; menu.Open(LobbyMenu.Play);
            Page.anchorMin = Page.anchorMax = Vector2.zero; Page.sizeDelta = new Vector2(1868, 968);
            yield return null;
            foreach (string mode in LobbyMenu.PracticeModes.Concat(new[] { "TimeRun", "Freestyle" }))
            {
                Pick(mode); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var icons = Page.GetComponentsInChildren<Button>().Where(item => item.name == PlayPage.GuideIcon).ToArray();
                Assert.AreEqual(ModeGuides.Has(mode) ? 1 : 0, icons.Length, mode);
                if (icons.Length == 0) continue;
                var goal = Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.GoalLine);
                goal.ForceMeshUpdate();
                Assert.IsFalse(goal.isTextTruncated || goal.isTextOverflowing, goal.text);
                var icon = InPage((RectTransform)icons[0].transform); var line = InPage(goal.rectTransform);
                Assert.That(icon.center.y, Is.EqualTo(line.center.y).Within(1f), mode + ": the icon sits on the goal line");
                Assert.Greater(icon.xMin, line.center.x, mode + ": after the goal text");
                Assert.LessOrEqual(icon.xMax, line.xMax + .5f, mode + ": inside the line");
                Assert.IsTrue(icons[0].IsInteractable());
                Assert.AreEqual(PlayPage.GuideHint, icons[0].GetComponent<LobbyHint>().Text);
            }
        }

        static void AssertNameOnPicture(Selectable card, Rect shape, string id)
        {
            var title = card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map title");
            var name = InPage(title.rectTransform);
            var detail = InPage(card.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Map detail").rectTransform);
            Assert.GreaterOrEqual(name.xMin, shape.xMin, id + ": the name starts inside the picture");
            Assert.GreaterOrEqual(name.yMin, shape.yMin - .5f, id + ": the name sits on the picture");
            Assert.LessOrEqual(name.yMax, shape.center.y + .5f, id + ": the name keeps to the lower half");
            Assert.LessOrEqual(detail.yMax, shape.yMin + .5f, id + ": the detail line sits under the picture");
            Assert.AreEqual(Color.white, title.color, id);
            var scrim = card.GetComponentsInChildren<Image>().Single(image => image.name == PlayPage.NameScrim);
            Assert.AreSame(scrim.transform.parent, title.transform.parent, id);
            Assert.Less(scrim.transform.GetSiblingIndex(), title.transform.GetSiblingIndex(), id + ": the gradient sits under the name");
            var gradient = scrim.GetComponent<LobbyGradient>();
            Assert.IsNotNull(gradient, id + ": a soft gradient keeps the name legible over any picture");
            Assert.Less(gradient.To.grayscale, .25f, id + ": the gradient darkens under the white name");
            Assert.Greater(gradient.To.a, .5f, id);
            Assert.IsFalse(scrim.raycastTarget);
        }

        [UnityTest]
        public IEnumerator EverySelectedModeShowsItsGoalUnderTheModes()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "pinwheel"); menu.Open(LobbyMenu.Play);
            yield return null;
            foreach (string mode in LobbyMenu.PracticeModes)
            {
                Pick(mode); yield return null; yield return null;
                Canvas.ForceUpdateCanvases();
                var goal = Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.GoalLine);
                Assert.AreEqual(LobbyPlayModeLabels.Line(mode), goal.text, mode);
                StringAssert.Contains(Wreckabulary.Rules.HouseModes.Goal(mode), goal.text);
                var frame = Page.GetComponentInChildren<LobbyPlayPageView>();
                var bounds = InPage(goal.rectTransform);
                Assert.LessOrEqual(bounds.yMax, InPage(Strip).yMin + .5f, "The goal sits under the modes.");
                Assert.GreaterOrEqual(bounds.yMin, InPage(frame.Choices.GetComponentInChildren<ScrollRect>().viewport).yMax - .5f, "The goal stays above the maps.");
                goal.ForceMeshUpdate();
                Assert.IsFalse(goal.isTextTruncated || goal.isTextOverflowing, goal.text);
                Assert.AreSame(Find("Mode " + mode).gameObject, EventSystem.current.currentSelectedGameObject);
            }
            Pick("TimeRun"); yield return null;
            Assert.AreEqual(LobbyPlayModeLabels.Line("TimeRun"), Page.GetComponentsInChildren<TMP_Text>().Single(text => text.name == PlayPage.GoalLine).text);
        }

        [Test]
        public void CardGridKeepsItsWidthShrinksOnlyToFitAndKeepsThePictureAt16By9()
        {
            var wide = LobbyPlayCardGrid.Layout(1820, 12, 264, 3, 640);
            Assert.AreEqual(3, wide.columns);
            Assert.AreEqual(LobbyPlayCardGrid.PreferredWidth, wide.cell.x);
            Assert.That((wide.cell.x - LobbyPlayCardGrid.Inset * 2) / (wide.cell.y - LobbyPlayCardGrid.CaptionHeight - LobbyPlayCardGrid.Inset), Is.EqualTo(16f / 9f).Within(.001f));
            var narrow = LobbyPlayCardGrid.Layout(840, 12, 264, 3, 0);
            Assert.AreEqual(3, narrow.columns);
            Assert.That(narrow.cell.x, Is.EqualTo((840 - 24) / 3f).Within(.01f));
            var low = LobbyPlayCardGrid.Layout(1820, 12, 264, 3, 200);
            Assert.That(low.cell.y, Is.EqualTo(200 - LobbyPlayCardGrid.ViewportMargin).Within(.01f), "A short viewport still shows a whole card.");
            Assert.AreEqual(1, LobbyPlayCardGrid.Layout(200, 12, 264, 3, 0).columns);
        }

        [Test]
        public void ModeFamiliesBalanceIntoAsFewLinesAsFit()
        {
            CollectionAssert.AreEqual(new[] { 5 }, LobbyPlayModeFamilies.Lines(new[] { 108f, 478f, 372f, 302f, 242f }, 28, 1800, .8f));
            CollectionAssert.AreEqual(new[] { 2, 3 }, LobbyPlayModeFamilies.Lines(new[] { 108f, 478f, 372f, 302f, 242f }, 28, 840, .8f));
            CollectionAssert.AreEqual(new[] { 1, 1 }, LobbyPlayModeFamilies.Lines(new[] { 900f, 900f }, 28, 600, .8f));
            CollectionAssert.IsEmpty(LobbyPlayModeFamilies.Lines(new float[0], 28, 600, .8f));
        }

        [UnityTest]
        public IEnumerator MatchmakingTabStaysInTheLobbyWithPlaylistCardsAndFindMatch()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat");
            menu.Open(LobbyMenu.Play);
            yield return null;
            Submit("Queue " + LobbyMenu.Matchmaking);
            yield return null;
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
            Assert.AreEqual(LobbyMenu.Practice, menu.Queue, "the local selection is not reclassified");
            CollectionAssert.AreEqual(QuickPlaylists.Ids, Page.GetComponentsInChildren<Button>()
                .Where(button => button.name.StartsWith(PlayPage.PlaylistCard)).Select(button => button.name.Substring(PlayPage.PlaylistCard.Length)));
            Assert.IsEmpty(Modes, "no house modes on the Matchmaking tab");
            Assert.IsTrue(Find(PlayPage.FindMatch).interactable); Assert.IsTrue(Find(PlayPage.CustomMatch).interactable);
            Assert.AreEqual("Custom match".ToUpperInvariant(), Find(PlayPage.CustomMatch).GetComponentInChildren<TMP_Text>().text);
            Assert.AreEqual("Party · You", Named(Page.GetComponentsInChildren<TMP_Text>(), PlayPage.PartyLabel), "the party strip is always there");
            Assert.IsTrue(Find(PlayPage.InviteFriendsButton).interactable); Assert.IsTrue(Find(PlayPage.JoinParty).interactable);
            Assert.AreEqual("", PartyField.text);
            Assert.AreEqual("Team Race 2v2, Team Race 4v4 + 4 more", Summary);
            CollectionAssert.AreEqual(QuickPlaylists.Groups, Page.GetComponentsInChildren<TMP_Text>()
                .Where(text => QuickPlaylists.Groups.Contains(text.text)).Select(text => text.text), "Classic, then Arcade");
            Assert.IsFalse(Ticked("freeplay")); Assert.IsTrue(Ticked("ffa"));
            Submit(PlayPage.PlaylistCard + "freeplay");
            yield return null;
            Assert.IsTrue(Ticked("freeplay")); Assert.AreEqual("All playlists", Summary);
            StringAssert.Contains("freeplay", PlayerPrefs.GetString(QuickPlayChoices.StorageKey));
            foreach (string id in QuickPlaylists.Ids.Skip(1))
            {
                Submit(PlayPage.PlaylistCard + id);
                yield return null;
            }
            Assert.AreEqual("Team Race 2v2", Summary);
            Submit(PlayPage.PlaylistCard + "relay-2v2");
            yield return null;
            Assert.IsTrue(Ticked("relay-2v2"), "the last playlist stays ticked");
            CollectionAssert.Contains(menu.Messages, PlayPage.KeepOnePlaylist);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "ticking never leaves the lobby");
            PartyField.text = "not a code";
            Submit(PlayPage.JoinParty);
            yield return null;
            CollectionAssert.Contains(menu.Messages, PlayPage.NotAPartyCode);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "a wrong party code never leaves the lobby");
            Assert.IsNull(WordboundRoomEntry.TakeRequest(), "a wrong party code asks for nothing");
            Submit("Queue " + LobbyMenu.Practice);
            yield return null;
            Assert.IsNotEmpty(Modes); Assert.AreEqual("Duos", menu.Mode);
        }

        [UnityTest]
        public IEnumerator CustomMatchHostsOrJoinsFromTheLobby()
        {
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Play);
            yield return null;
            Submit("Queue " + LobbyMenu.Matchmaking);
            yield return null;
            Submit(PlayPage.CustomMatch);
            yield return null;
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "Custom match stays in the lobby");
            Assert.AreEqual(PlayPage.AmongYourselves, Named(Page.GetComponentsInChildren<TMP_Text>(), PlayPage.CustomMatchNote));
            CollectionAssert.AreEqual(FriendRooms.All.Select(room => room.Id), Named(PlayPage.RoomModeCard));
            Assert.IsEmpty(Named(PlayPage.PlaylistCard), "the playlists step aside");
            foreach (string name in new[] { PlayPage.CreateRoom, PlayPage.JoinRoom, PlayPage.CustomMap, PlayPage.BackToPlaylists })
                Assert.IsTrue(Find(name).interactable, name);
            Assert.AreEqual("Team Race 2v2 · The Lost Address", FriendsRoom);
            StringAssert.StartsWith("Needs 4 players", FriendsDetail);
            CollectionAssert.AreEqual(new[] { " Solo", " Duo", " Squad" }, Named(PlayPage.TeamSizeChoice));
            Assert.AreEqual(FriendRooms.Layouts, Named(PlayPage.LayoutChoice).Length);
            CollectionAssert.AreEqual(new[] { "The Lost Address", "The Lost Address 2", "The Lost Address 3", "The Lost Address 4" },
                Enumerable.Range(1, FriendRooms.Layouts).Select(slot => Find(PlayPage.LayoutChoice + " " + slot).GetComponentInChildren<TMP_Text>().text),
                "the map chips name the course");
            Submit(PlayPage.TeamSizeChoice + " Solo");
            yield return null;
            Assert.AreEqual("Team Race 1v1 · The Lost Address", FriendsRoom);
            Submit(PlayPage.LayoutChoice + " 3");
            yield return null;
            Assert.AreEqual("Team Race 1v1 · The Lost Address 3", FriendsRoom);
            Submit(PlayPage.RoomModeCard + "ffa");
            yield return null;
            Assert.AreEqual("Free-for-All · Toybox Arena 3", FriendsRoom);
            Assert.IsEmpty(Named(PlayPage.TeamSizeChoice), "team size only shows for team modes");
            StringAssert.StartsWith("2 to 8 players", FriendsDetail);
            Assert.AreEqual("v1;mode=ffa;team=Solo;layout=2", PlayerPrefs.GetString(FriendRooms.StorageKey));
            RoomField.text = "not a code";
            Submit(PlayPage.JoinRoom);
            yield return null;
            CollectionAssert.Contains(menu.Messages, PlayPage.NotARoomCode);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "a wrong code never leaves the lobby");
            Assert.AreEqual("not a code", RoomField.text);
            Submit(PlayPage.BackToPlaylists);
            yield return null;
            CollectionAssert.AreEqual(QuickPlaylists.Ids, Named(PlayPage.PlaylistCard));
            Assert.IsTrue(Find(PlayPage.FindMatch).interactable);
        }

        [UnityTest]
        public IEnumerator OnlineSubmitLoadsWordboundWithoutReclassifyingTheLocalSelection()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Duos", "flat");
            menu.Open(LobbyMenu.Play);
            yield return null;
            WordboundSoloEntry.Request("Freestyle");
            Submit("Queue " + LobbyMenu.Matchmaking);
            yield return null;
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "the Matchmaking tab stays in the lobby");
            Submit(PlayPage.CustomMatch);
            yield return null;
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name, "Custom match stays in the lobby");
            RoomField.text = BrokenRoom;
            Submit(PlayPage.JoinRoom);
            yield return TestScenes.WaitForActive(Session.WordboundScene);
            yield return null; yield return null;
            var party = Object.FindAnyObjectByType<Wreckabulary.Online.WordboundPartyMenu>();
            Assert.IsTrue(party && party.InRoom, "the room view opens in the online scene");
            Assert.IsFalse(party.Coordinator.Authenticated, "a code that cannot work never reaches the service");
            Assert.IsNull(WordboundRoomEntry.TakeRequest());
            Assert.AreEqual(Session.WordboundScene, SceneManager.GetActiveScene().name);
            Assert.IsNull(WordboundSoloEntry.TakeRequest());
            Assert.AreEqual(LobbyMenu.Practice, Session.LobbyQueue);
            Assert.AreEqual("Duos", Session.LobbyMode);
            Assert.AreEqual("flat", Session.MapId);
            Assert.IsNull(Object.FindAnyObjectByType<RoundManager>(), "no legacy battle starts on the online route");
            Assert.IsEmpty(Object.FindObjectsByType<BotController>(FindObjectsSortMode.None));
        }
    }
}
