using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Wreckabulary.Tests
{
    public class HudParityTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            TouchBinding.Shared.ReleaseAll();
            TouchBinding.Shared.Enabled = TouchBinding.Shared.OverlayDesktop = false;
            TouchBinding.Shared.OverlayBindingId = null;
            SummonedThing.ClearAll();
            yield return TestScenes.Reset();
        }

        static PlayerController Spawn(InputBinding binding)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, binding);
            player.Respawn(Vector3.zero);
            player.Inventory.Collects = false;
            return player;
        }

        [UnityTest]
        public IEnumerator HandsKeepTheirSlotsAndOneOrTwoPicksAHand()
        {
            var binding = new ScriptedBinding();
            var player = Spawn(binding);
            yield return null;
            player.Inventory.Set("BATBLADE");
            Assert.IsTrue(player.Summoner.Summon("BAT"));
            var bat = player.Combat.Weapon;
            Assert.AreEqual(0, player.Combat.ActiveSlot);
            Assert.IsTrue(player.Summoner.Summon("BLADE"));
            var blade = player.Combat.Weapon;
            Assert.AreEqual(1, player.Combat.ActiveSlot, "New gear goes in the other hand; the old gear keeps its hand.");
            Assert.AreSame(bat, player.Combat.GearIn(0));
            Assert.AreSame(blade, player.Combat.GearIn(1));

            Assert.IsTrue(player.Combat.SelectSlot(0));
            Assert.AreSame(bat, player.Combat.Weapon);
            Assert.AreSame(blade, player.Combat.GearIn(1), "Switching hands doesn't reorder the slots.");
            Assert.IsFalse(player.Combat.SelectSlot(0), "Picking the hand already in use does nothing.");

            binding.Next.slot = 2;
            yield return null;
            yield return null;
            Assert.AreEqual(1, player.Combat.ActiveSlot);
            Assert.AreSame(blade, player.Combat.Weapon);
            Assert.AreSame(bat, player.Combat.GearIn(0));
        }

        [UnityTest]
        public IEnumerator HudFollowsTheBrowserLayout()
        {
            var touch = TouchBinding.Shared;
            touch.Enabled = true;
            ControlTips.Reset();
            var player = Spawn(touch);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(false);
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreSame(player, hud.LocalPlayer);
            var safe = hud.transform.Find("Safe HUD");

            var tray = safe.Find("Letter bag");
            Assert.IsTrue(tray.gameObject.activeSelf);
            for (int i = 1; i <= 10; i++) Assert.IsNotNull(tray.Find($"Letter {i}"), $"tray cell {i}");
            Assert.IsNull(tray.Find("Letter 11"), "Two rows of five, like the browser bag.");
            player.Inventory.Set("");
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.IsTrue(tray.Find("Letter 10/Face/Outline").GetComponent<Image>().enabled, "An empty cell keeps a dark edge over light floors.");
            var spellGlyph = tray.Find("Spell key/Glyph");
            Assert.IsNull(spellGlyph.GetComponent<RawImage>(), "Q no longer uses the bag glyph that reads as a padlock.");
            Assert.AreEqual("A", spellGlyph.Find("Letter").GetComponent<TMPro.TMP_Text>().text, "Q shows a letter tile.");
            foreach (var shortcut in new[] { "Spell key", "Bag link" })
            {
                var cap = tray.Find(shortcut + "/Key cap").GetComponent<Image>();
                Assert.Less(cap.color.r, .2f, shortcut + " key sits on a dark pill");
                Assert.Greater(cap.color.a, .7f, shortcut);
            }
            Assert.IsNotNull(safe.Find("Side column/Minimap"));
            Assert.IsNotNull(safe.Find("Side column/Timer"));
            Assert.IsNotNull(safe.Find("Side column/Alive/Count"), "A head count sits before the timer, as on the web.");
            Assert.IsNotNull(safe.Find("Side column/Objective"));
            Assert.IsNull(safe.Find("Side column/Pause"), "The brand tile is the pause button.");
            Assert.IsNull(safe.Find("Return home"), "The way home is in the pause card.");
            Assert.IsNotNull(safe.Find("Letter bag/Gear slot 1"));
            Assert.IsNotNull(safe.Find("Letter bag/Gear slot 2"));
            Assert.IsNull(safe.Find("Vitals").GetComponent<Image>(), "Vitals sit straight on the game, with no card.");
            Assert.IsNull(safe.Find("Vitals/Health track"));
            StringAssert.Contains("HP", safe.Find("Vitals/HP").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsTrue(safe.Find("Desktop controls").gameObject.activeSelf, "A new desktop player gets a control tip.");
            Assert.IsNull(safe.Find("Desktop controls").GetComponent<Image>(), "Tips sit straight on the game, with no box.");
            var tip = safe.Find("Desktop controls/Keys").GetComponent<TMPro.TMP_Text>();
            StringAssert.Contains("move", tip.text, "Moving comes first.");
            ControlTips.Learn(ControlTips.Tip.Move);
            yield return new WaitForSecondsRealtime(0.5f);
            StringAssert.Contains("smash, throw, place, block", tip.text, "Once you've moved, the next tip: left click does it all.");
            StringAssert.DoesNotContain("RMB", tip.text, "Right click aims only in the over-the-shoulder view, so its tip waits.");
            Assert.IsFalse(safe.Find("Touch controls").gameObject.activeSelf, "On-screen buttons are for touch only.");

            safe.Find("Letter bag/Gear slot 2").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, player.Combat.ActiveSlot, "Tapping a hand slot picks that hand.");

            Assert.IsFalse(hud.BagOpen);
            tray.Find("Bag link").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.BagOpen, "The bag link pins the bag and map panel.");
            Assert.IsNotNull(safe.Find("Bag panel/House/Map area/Big map"));
            tray.Find("Bag link").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.BagOpen);
            yield return TheBagIsTheWebsDarkGlass(hud, player);

            safe.Find("Brand").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.Paused);
            Assert.AreEqual(0f, Time.timeScale);
            hud.transform.Find("Pause/Pause card/Resume").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.Paused);
            Assert.AreEqual(1f, Time.timeScale);

            hud.ShowTouchControls(true);
            Assert.IsFalse(safe.Find("Desktop controls").gameObject.activeSelf);
            Assert.IsNull(safe.Find("Touch controls/SWAP"), "Touch players tap a hand slot instead of a swap button.");
            Assert.IsNotNull(safe.Find("Touch controls/SPELL"));
            Assert.IsNotNull(safe.Find("Touch controls/HOLD DROP"));
            foreach (Transform child in safe.Find("Touch controls"))
                Assert.IsFalse(child.name.StartsWith("PLACE"), "No separate place button: " + child.name);
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
            ControlTips.Reset();
        }

        [UnityTest]
        public IEnumerator ControllerTrayShowsPadButtonsAndHoldingRbOpensTheBag()
        {
            var route = InputSystem.settings.editorInputBehaviorInPlayMode;
            var focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var pad = InputSystem.AddDevice<Gamepad>();
            ControlTips.Reset();
            try
            {
                var binding = new GamepadBinding(pad);
                var player = Spawn(binding);
                var hud = new GameObject("Pad HUD", typeof(Canvas)).AddComponent<GameHud>();
                yield return null; yield return null;
                Assert.AreSame(player, hud.LocalPlayer);
                yield return new WaitForSecondsRealtime(0.9f);
                var tip = hud.transform.Find("Safe HUD/Desktop controls/Keys").GetComponent<TMPro.TMP_Text>();
                Assert.IsTrue(tip.gameObject.activeInHierarchy, "A pad player gets control tips too.");
                Assert.AreEqual(ControlTips.Text(ControlTips.Tip.Move, binding), tip.text);
                StringAssert.DoesNotContain("WASD", tip.text, "One key per device: the pad's own.");
                var tray = hud.transform.Find("Safe HUD/Letter bag");
                Assert.AreEqual("Y", tray.Find("Spell key").GetComponentInChildren<TMPro.TMP_Text>().text);
                Assert.AreEqual("RB", tray.Find("Bag link").GetComponentInChildren<TMPro.TMP_Text>().text);
                Assert.AreEqual("‹", tray.Find("Gear slot 1/Key").GetComponent<TMPro.TMP_Text>().text);
                Assert.AreEqual("›", tray.Find("Gear slot 2/Key").GetComponent<TMPro.TMP_Text>().text);
                Assert.IsFalse(hud.BagOpen);
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.RightShoulder));
                yield return null; yield return null;
                Assert.IsTrue(hud.BagOpen, "Holding RB shows the bag and map.");
                Assert.AreEqual("‹", hud.transform.Find("Safe HUD/Bag panel").GetComponentsInChildren<TMPro.TMP_Text>(true)
                    .First(text => text.name == "Key").text, "The bag's hand cards name the pad buttons too.");
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null; yield return null;
                Assert.IsFalse(hud.BagOpen);
                Object.Destroy(hud.gameObject);
                Object.Destroy(player.gameObject);
            }
            finally
            {
                if (pad.added) InputSystem.RemoveDevice(pad);
                InputSystem.settings.editorInputBehaviorInPlayMode = route;
                InputSystem.settings.backgroundBehavior = focus;
                ControlTips.Reset();
            }
        }

        [UnityTest]
        public IEnumerator StartPausesForASeatedPadButNeverOverTheResultCard()
        {
            var route = InputSystem.settings.editorInputBehaviorInPlayMode;
            var focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var seated = InputSystem.AddDevice<Gamepad>();
            var stranger = InputSystem.AddDevice<Gamepad>();
            try
            {
                var player = Spawn(new GamepadBinding(seated));
                var hud = new GameObject("Pad pause HUD", typeof(Canvas)).AddComponent<GameHud>();
                new GameObject("Back to hub").AddComponent<BackToHub>();
                hud.StateController.FadeDuration = 0f;
                yield return null; yield return null;

                IEnumerator Press(Gamepad pad)
                {
                    InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.Start));
                    yield return null; yield return null;
                    InputSystem.QueueStateEvent(pad, new GamepadState());
                    yield return null; yield return null;
                }

                yield return Press(stranger);
                Assert.IsFalse(hud.Paused, "Start on a pad without a seat joins; it never pauses.");
                yield return Press(seated);
                Assert.IsTrue(hud.Paused, "Start pauses for a seated pad.");
                yield return Press(seated);
                Assert.IsFalse(hud.Paused, "Start again resumes.");
                hud.ShowResult(new HudResult { Round = 1, Heading = "AI 2 called dibs!" }, () => { });
                yield return Press(seated);
                Assert.IsFalse(hud.Paused, "Start over a result card is left to the card.");
                hud.HideResult();
                Object.Destroy(hud.gameObject);
                Object.Destroy(player.gameObject);
            }
            finally
            {
                if (seated.added) InputSystem.RemoveDevice(seated);
                if (stranger.added) InputSystem.RemoveDevice(stranger);
                InputSystem.settings.editorInputBehaviorInPlayMode = route;
                InputSystem.settings.backgroundBehavior = focus;
            }
        }

        static IEnumerator TheBagIsTheWebsDarkGlass(GameHud hud, PlayerController player)
        {
            var safe = hud.transform.Find("Safe HUD");
            var link = safe.Find("Letter bag/Bag link").GetComponent<Button>();
            player.Inventory.Set("BALL");
            link.onClick.Invoke();
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(hud.BagOpen);

            var backdrop = safe.Find("Bag panel/Backdrop").GetComponent<Image>();
            var corners = new Vector3[4];
            backdrop.rectTransform.GetWorldCorners(corners);
            Assert.LessOrEqual(corners[0].x, 0.5f); Assert.LessOrEqual(corners[0].y, 0.5f);
            Assert.GreaterOrEqual(corners[2].x, Screen.width - 0.5f); Assert.GreaterOrEqual(corners[2].y, Screen.height - 0.5f);
            Assert.IsTrue(backdrop.raycastTarget, "Clicks on the scrim don't reach the game.");
            var scrim = backdrop.sprite.texture;
            Assert.AreEqual(.55f, scrim.GetPixel(64, 64).a, .02f, "The web's gradient: light in the middle...");
            Assert.AreEqual(.85f, scrim.GetPixel(0, 0).a, .02f, "...and darker at the edges.");
            Assert.AreEqual(1f, safe.Find("Bag panel").GetComponent<CanvasGroup>().alpha, 1e-3f, "Faded in.");

            foreach (var part in new[] { "Side column", "Letter bag", "Vitals", "Desktop controls" })
            {
                var group = safe.Find(part).GetComponent<CanvasGroup>();
                Assert.AreEqual(0f, group.alpha, part + " steps aside under the bag");
                Assert.IsFalse(group.blocksRaycasts, part);
            }
            Assert.IsTrue(safe.Find("Brand").gameObject.activeInHierarchy, "The brand stays, as on the web.");

            for (int i = 1; i <= 10; i++) Assert.IsNotNull(safe.Find($"Bag panel/Bag/Letters/Big letter {i}"), $"bag cell {i}");
            Assert.IsNull(safe.Find("Bag panel/Bag/Wearing"), "The in-match bag shows no cosmetic slots.");
            var ballTile = safe.Find("Bag panel/Recipe book/View/Cards/BALL/Tile 1");
            Assert.Greater(ballTile.GetComponent<Image>().color.a, .9f, "An owned letter is a filled tile.");
            Assert.IsFalse(ballTile.Find("Rim").GetComponent<Image>().enabled);
            Assert.IsNotNull(safe.Find("Bag panel/Bag/Hands/Bag hand 1/Open hand"), "An empty hand shows an open hand.");
            var map = (RectTransform)safe.Find("Bag panel/House/Map area/Big map");
            Assert.GreaterOrEqual(map.rect.width, 325f, "The house map is the panel's centrepiece.");
            Assert.AreEqual(map.rect.width, map.rect.height, .5f, "and square");

            var cards = safe.Find("Bag panel/Recipe book/View/Cards");
            Assert.AreEqual(GameConfig.Current.Items.All.Count(i => i.Enabled), cards.childCount, "A card per recipe.");
            var ball = cards.Find("BALL").GetComponent<Button>();
            Assert.IsTrue(ball.interactable, "Holding B, A, L, L lights BALL up.");
            Assert.IsTrue(cards.Find("BALL/Glow").GetComponent<Image>().enabled);
            Assert.IsFalse(cards.Find("SOFA").GetComponent<Button>().interactable, "A word you can't spell isn't a button.");
            ball.onClick.Invoke();
            Assert.IsFalse(hud.BagOpen, "Spelling from the book closes the bag, so you see it made.");
            Assert.IsTrue(player.Summoner.IsCrafting);
            Assert.AreEqual("BALL", player.Summoner.CraftWord);
            yield return null;
            foreach (var part in new[] { "Side column", "Letter bag", "Vitals" })
            {
                var group = safe.Find(part).GetComponent<CanvasGroup>();
                Assert.AreEqual(1f, group.alpha, part + " is back");
                Assert.IsTrue(group.blocksRaycasts, part);
            }
            player.Summoner.CancelCraft();

            link.onClick.Invoke();
            Assert.IsTrue(hud.BagOpen);
            safe.Find("Bag panel/Close bag").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.BagOpen, "The x closes it.");
        }

        [UnityTest]
        public IEnumerator RecipeBookSaysWhatTheFocusedItemDoesInOneLine()
        {
            Spawn(TouchBinding.Shared);
            var hud = new GameObject("Recipe line HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            safe.Find("Letter bag/Bag link").GetComponent<Button>().onClick.Invoke();
            var book = safe.Find("Bag panel/Recipe book");
            var line = book.Find("Book line").GetComponent<TMPro.TMP_Text>();
            var cards = book.Find("View/Cards");
            Assert.AreEqual("", line.text, "quiet until a card is focused");
            var legend = book.Find("Legend");
            Assert.IsTrue(legend.gameObject.activeSelf, "An idle book explains its stars.");
            Assert.AreEqual(Wreckabulary.Rules.RecipeStars.Legend, legend.Find("Text").GetComponent<TMPro.TMP_Text>().text);
            int Stars(string word) => cards.Find(word).Cast<Transform>().Count(child => child.name == "Star");
            if (GameConfig.Current.Tuning != null)
            {
                Assert.AreEqual(Wreckabulary.Rules.RecipeStars.Count(Wreckabulary.Rules.RecipeDifficulty.TierOf("HAMMER")), Stars("HAMMER"));
                Assert.Greater(Stars("HAMMER"), Stars("BAT"), "A harder word shows more stars.");
                Assert.AreEqual(0, Stars("BAT"));
            }
            var batTile = cards.Find("BAT/Tile 1");
            Assert.AreEqual(0f, batTile.GetComponent<Image>().color.a, .01f, "A missing letter is hollow...");
            Assert.IsTrue(batTile.Find("Rim").GetComponent<Image>().enabled, "...with an outline.");
            UnityEngine.EventSystems.ExecuteEvents.Execute(cards.Find("HAMMER").gameObject,
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
            StringAssert.StartsWith("HAMMER · ", line.text);
            StringAssert.Contains("every 3rd hit crushes", line.text);
            Assert.GreaterOrEqual(line.fontSize, 23f);
            Assert.IsFalse(legend.gameObject.activeSelf, "The focused item's line takes the legend's place.");
            UnityEngine.EventSystems.ExecuteEvents.Execute(cards.Find("HAMMER").gameObject,
                new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
            Assert.AreEqual("", line.text);
            UnityEngine.EventSystems.ExecuteEvents.Execute(cards.Find("BAT").gameObject,
                new UnityEngine.EventSystems.BaseEventData(UnityEngine.EventSystems.EventSystem.current), UnityEngine.EventSystems.ExecuteEvents.selectHandler);
            Assert.AreEqual("BAT · no extra effect", line.text, "a pad selection says it too");
            Object.Destroy(hud.gameObject);
        }

        [UnityTest]
        public IEnumerator RecipeBookReflowsWithoutShrinkingLettersOrOverlappingTheMap()
        {
            var player = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Responsive recipe HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            yield return new WaitForSecondsRealtime(.12f);
            player.Inventory.Set("BALL");
            var safe = (RectTransform)hud.transform.Find("Safe HUD");
            safe.Find("Letter bag/Bag link").GetComponent<Button>().onClick.Invoke();
            var book = (RectTransform)safe.Find("Bag panel/Recipe book");
            var view = (RectTransform)book.Find("View");
            var cards = (RectTransform)view.Find("Cards");
            var grid = cards.GetComponent<GridLayoutGroup>();
            var scroll = view.GetComponent<ScrollRect>();
            var mapArea = (RectTransform)safe.Find("Bag panel/House/Map area");
            var map = (RectTransform)mapArea.Find("Big map");

            foreach (var size in new[] { new Vector2(1920f, 1080f), new Vector2(2520f, 1080f), new Vector2(1440f, 1080f) })
            {
                safe.anchorMin = safe.anchorMax = safe.pivot = Vector2.zero;
                safe.anchoredPosition = Vector2.zero;
                safe.sizeDelta = size;
                yield return new WaitForSecondsRealtime(.12f);
                Canvas.ForceUpdateCanvases();
                Assert.AreEqual(size.x >= 1920f ? 4 : 3, grid.constraintCount, size.ToString());
                Assert.GreaterOrEqual(grid.cellSize.x, 188f, "Fewer columns keep the recipe cells readable.");
                Assert.GreaterOrEqual(grid.cellSize.y, 200f);
                Assert.IsTrue(scroll.vertical);
                Assert.IsFalse(scroll.horizontal);
                Assert.Greater(cards.rect.height, view.rect.height, "All recipes remain reachable by scrolling.");
                Assert.GreaterOrEqual(map.rect.width, 325f);
                Assert.AreEqual(map.rect.width, map.rect.height, .5f);
                AssertRectInside(safe, book);
                AssertRectInside(safe, mapArea);
                AssertRectInside(mapArea, map);
                var mapCorners = new Vector3[4]; var bookCorners = new Vector3[4];
                mapArea.GetWorldCorners(mapCorners); book.GetWorldCorners(bookCorners);
                Assert.Less(mapCorners[2].x, bookCorners[0].x, "The map and recipe column have separate space.");

                foreach (Transform card in cards)
                {
                    Assert.GreaterOrEqual(((RectTransform)card.Find("Picture")).rect.width, 112f);
                    foreach (var text in card.GetComponentsInChildren<TMPro.TMP_Text>())
                    {
                        Assert.GreaterOrEqual(text.fontSize, 23f, card.name);
                        AssertRectInside((RectTransform)card, (RectTransform)text.transform.parent);
                    }
                }
                scroll.verticalNormalizedPosition = 0f;
                Canvas.ForceUpdateCanvases();
                AssertRectInside(view, (RectTransform)cards.GetChild(cards.childCount - 1));
                scroll.verticalNormalizedPosition = 1f;
            }
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        static void AssertRectInside(RectTransform outer, RectTransform inner)
        {
            var corners = new Vector3[4];
            inner.GetWorldCorners(corners);
            foreach (var corner in corners)
            {
                var local = outer.InverseTransformPoint(corner);
                Assert.That(local.x, Is.InRange(outer.rect.xMin - .5f, outer.rect.xMax + .5f), inner.name + " horizontal fit");
                Assert.That(local.y, Is.InRange(outer.rect.yMin - .5f, outer.rect.yMax + .5f), inner.name + " vertical fit");
            }
        }

        [UnityTest]
        public IEnumerator AnnouncementsAreToastsAndTheRoundChipCountsDown()
        {
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            Assert.IsNull(hud.transform.Find("Safe HUD/Title plaque"), "No big title plaque: the web announces with a toast.");
            hud.SetTitle("ROUND 1", "Dibs on the living room");
            Assert.AreEqual("ROUND 1 · Dibs on the living room", hud.ToastText);
            hud.SetTitle("", "");
            Assert.AreEqual("ROUND 1 · Dibs on the living room", hud.ToastText, "No words don't replace a toast.");
            yield return new WaitForSecondsRealtime(3.1f);
            Assert.AreEqual("", hud.ToastText, "A toast goes by itself.");

            hud.ShowCountdown(3, "ROUND 1 · LIVING ROOM");
            Assert.IsTrue(hud.CountdownShown);
            Assert.AreEqual("3", hud.CountdownText);
            hud.ShowCountdown(2, "ROUND 1 · LIVING ROOM");
            Assert.AreEqual("2", hud.CountdownText);
            hud.ShowGo();
            Assert.AreEqual("GO", hud.CountdownText);
            yield return new WaitForSecondsRealtime(1f);
            Assert.IsFalse(hud.CountdownShown, "GO goes a moment later.");

            hud.ShowBanner("Round 1 · Dibs!", "Last one standing wins the round · First to 3");
            Assert.IsTrue(hud.BannerShown);
            Assert.AreEqual("Round 1 · Dibs! · Last one standing wins the round · First to 3", hud.BannerText);
            var banner = (RectTransform)hud.transform.Find("Safe HUD/Round banner");
            Assert.GreaterOrEqual(banner.Find("Title").GetComponent<TMPro.TMP_Text>().fontSize, 40f, "The round banner is large.");
            Assert.IsFalse(banner.GetComponent<CanvasGroup>().blocksRaycasts, "It never blocks a click.");
            yield return new WaitForSecondsRealtime(2.7f);
            Assert.IsFalse(hud.BannerShown, "The banner goes by itself after 2.5 s.");
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }

        [UnityTest]
        public IEnumerator PauseHelpAndResultCardsFollowTheWeb()
        {
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            var safe = hud.transform.Find("Safe HUD");
            safe.Find("Brand").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.Paused);
            var card = hud.transform.Find("Pause/Pause card");
            Assert.AreEqual("The mess can wait.", card.Find("Heading").GetComponent<TMPro.TMP_Text>().text);
            StringAssert.Contains("First to", card.Find("Blurb").GetComponent<TMPro.TMP_Text>().text, "The pause card says what the mode is.");
            var leave = card.Find("Pause home");
            Assert.AreEqual("Leave match", leave.Find("Body/Label").GetComponent<TMPro.TMP_Text>().text);
            leave.GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.LeaveAsked, "Leaving mid-match asks first.");
            Assert.IsTrue(hud.Paused);
            hud.transform.Find("Leave match/Leave card/Keep playing").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.LeaveAsked);
            Assert.IsTrue(hud.Paused, "Keep playing goes back to the pause card.");
            leave.GetComponent<Button>().onClick.Invoke();
            hud.TogglePause();
            Assert.IsFalse(hud.LeaveAsked, "Back or Start closes the question.");
            Assert.IsTrue(hud.Paused);
            card.Find("Pause how to play").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.HelpShown);
            Assert.IsFalse(card.gameObject.activeSelf, "Help takes the pause card's place.");
            StringAssert.Contains(" HP.", hud.transform.Find("Pause/Pause help/Fine print").GetComponent<TMPro.TMP_Text>().text);
            var spellStep = hud.transform.Find("Pause/Pause help/Steps/Step 02/Text").GetComponent<TMPro.TMP_Text>().text;
            StringAssert.Contains("type a word, press Enter", spellStep, "Keyboard players are told to type, not to hold.");
            StringAssert.Contains("click a lit card", spellStep, "...or to use the book.");
            StringAssert.DoesNotContain("craft", spellStep);
            StringAssert.Contains("Harder words carry effects.", hud.transform.Find("Pause/Pause help/Steps/Step 03/Text").GetComponent<TMPro.TMP_Text>().text);
            hud.transform.Find("Pause/Pause help/Help done").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.Paused, "GOT IT. LET'S PLAY. goes straight back to the game.");
            Assert.AreEqual(1f, Time.timeScale);

            int nexts = 0;
            hud.ShowResult(new HudResult { Round = 2, Won = true, Heading = "You called dibs!", Broken = 4, Crafted = 2, Damage = 37 }, () => nexts++);
            Assert.IsTrue(hud.ResultShown);
            Assert.IsTrue(hud.NeedsPointer, "The result card wants the mouse.");
            var result = safe.Find("Result/Result card");
            Assert.AreEqual("ROUND 2 COMPLETE", result.Find("Eyebrow").GetComponent<TMPro.TMP_Text>().text);
            Assert.AreEqual("You called dibs!", result.Find("Heading").GetComponent<TMPro.TMP_Text>().text);
            Assert.AreEqual("4", result.Find("Stats/OBJECTS WRECKED/Number").GetComponent<TMPro.TMP_Text>().text);
            Assert.AreEqual("37", result.Find("Stats/DAMAGE DEALT/Number").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsFalse(result.Find("Reward").gameObject.activeSelf, "A round in the middle of a match pays nothing yet.");
            StringAssert.Contains("NEXT ROUND", result.Find("Next/Body/Label").GetComponent<TMPro.TMP_Text>().text);
            safe.Find("Brand").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.Paused, "No pause over the result card.");
            var next = result.Find("Next").GetComponent<Button>();
            next.onClick.Invoke();
            next.onClick.Invoke();
            Assert.AreEqual(1, nexts, "NEXT ROUND acts once.");
            Assert.IsFalse(hud.ResultShown);

            hud.ShowResult(new HudResult { Final = true, Heading = "One more word. One more chance.",
                Reward = new Wreckabulary.Rules.MatchRecord { Coins = 12, Score = 70 } }, () => nexts++);
            Assert.AreEqual("HOUSE PARTY COMPLETE", result.Find("Eyebrow").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsTrue(result.Find("Reward").gameObject.activeSelf, "The match's pay shows at the end.");
            Assert.AreEqual("+12", result.Find("Reward/Coins").GetComponent<TMPro.TMP_Text>().text);
            StringAssert.Contains("PLAY AGAIN", result.Find("Next/Body/Label").GetComponent<TMPro.TMP_Text>().text);
            hud.HideResult();
            Assert.AreEqual(1, nexts, "Hiding the card isn't a choice.");
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }

        [UnityTest]
        public IEnumerator TheResultNamesTheWinnerAndOnlyAMidMatchLeaveAsks()
        {
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Result HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            var result = hud.transform.Find("Safe HUD/Result/Result card");
            string Text(string path) => result.Find(path).GetComponent<TMPro.TMP_Text>().text;
            int nexts = 0, homes = 0;
            hud.ShowResult(new HudResult { Round = 2, Heading = "AI 3 called dibs!", Eyebrow = "ROUND 2 · AI 3 WINS", Flavour = "2 more round wins to take the house.",
                Winner = "AI 3", Lines = new[] { "You · 1/3 wins · wrecked", "<b>AI 3 · 1/3 wins · 40 HP</b>" } }, () => nexts++, () => homes++);
            Assert.AreEqual("ROUND 2 · AI 3 WINS", Text("Eyebrow"));
            Assert.AreEqual("2 more round wins to take the house.", Text("Flavour"));
            StringAssert.Contains("AI 3 · 1/3 wins · 40 HP", Text("Players"), "One line per seat, bots included.");
            StringAssert.Contains("You · 1/3 wins · wrecked", Text("Players"));
            Assert.AreEqual("Leave match", Text("Result home/Body/Label"));
            Assert.IsTrue(result.Find("Stats").gameObject.activeSelf);
            result.Find("Result home").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.LeaveAsked, "Leaving between rounds asks first.");
            hud.ConfirmResult();
            Assert.AreEqual(0, nexts, "NEXT ROUND waits while the question is open.");
            hud.transform.Find("Leave match/Leave card/Leave").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(1, homes);
            Assert.IsFalse(hud.ResultShown);

            hud.ShowResult(new HudResult { Final = true, Heading = "You're ready!", Eyebrow = "TUTORIAL COMPLETE", NextLabel = "PLAY DIBS! NOW →",
                HomeLabel = "Back to the lobby", Stats = false }, () => nexts++, () => homes++);
            Assert.AreEqual("TUTORIAL COMPLETE", Text("Eyebrow"));
            Assert.AreEqual("PLAY DIBS! NOW →", Text("Next/Body/Label"));
            Assert.AreEqual("Back to the lobby", Text("Result home/Body/Label"));
            Assert.IsFalse(result.Find("Stats").gameObject.activeSelf, "Stats = false hides the stats row.");
            Assert.IsFalse(result.Find("Players").gameObject.activeSelf, "No seat lines without a roster.");
            result.Find("Result home").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.LeaveAsked, "The final card leaves at once.");
            Assert.AreEqual(2, homes);
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }

        [UnityTest]
        public IEnumerator TabOrRightClickClosesTheComposer()
        {
            var route = InputSystem.settings.editorInputBehaviorInPlayMode;
            var focus = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keys = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            keys.MakeCurrent();
            mouse.MakeCurrent();
            try
            {
                var player = Spawn(DesktopBinding.Shared);
                var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
                yield return null;
                yield return new WaitForSecondsRealtime(0.12f);
                player.Inventory.Set("BAT");
                player.Summoner.Open();
                yield return null;
                Assert.IsTrue(hud.ComposerOpen);

                InputSystem.QueueStateEvent(keys, new KeyboardState(Key.Tab));
                yield return null;
                yield return null;
                Assert.IsFalse(player.Summoner.IsSpelling, "Tab closes the composer");
                Assert.IsFalse(hud.ComposerOpen);
                Assert.IsFalse(hud.BagOpen, "the Tab that closed it doesn't open the bag");
                Assert.IsTrue(DesktopBinding.Typing, "held keys stay swallowed");
                InputSystem.QueueStateEvent(keys, new KeyboardState());
                yield return null;
                yield return null;
                Assert.IsFalse(DesktopBinding.Typing);

                player.Summoner.Open();
                yield return null;
                Assert.IsTrue(hud.ComposerOpen);
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return null;
                yield return null;
                Assert.IsFalse(hud.ComposerOpen, "right-click closes it too");
                Assert.IsTrue(DesktopBinding.Typing, "the right-click doesn't reach the player as a block");
                InputSystem.QueueStateEvent(mouse, new MouseState());
                yield return null;
                yield return null;
                Assert.IsFalse(DesktopBinding.Typing);
                Object.Destroy(hud.gameObject);
                Object.Destroy(player.gameObject);
            }
            finally
            {
                InputSystem.RemoveDevice(keys);
                InputSystem.RemoveDevice(mouse);
                InputSystem.settings.editorInputBehaviorInPlayMode = route;
                InputSystem.settings.backgroundBehavior = focus;
                DesktopBinding.Typing = false;
            }
        }

        [UnityTest]
        public IEnumerator KeyboardPlayersTypeTheirWordLikeTheWeb()
        {
            var player = Spawn(DesktopBinding.Shared);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreSame(player, hud.LocalPlayer);
            player.Inventory.Set("BA");
            player.Summoner.Open();
            yield return null;
            Assert.IsTrue(hud.ComposerOpen, "Spell opens the composer.");
            Assert.IsTrue(DesktopBinding.Typing, "Letters go to the word, not to the player.");
            Assert.IsTrue(hud.ComposerLettersOnly, "Typing shows only the typed letters.");
            Assert.AreEqual("Type a word you can make from your letters. Tab or right-click closes.", hud.ComposerStatus);
            hud.TypeWord("ba");
            yield return null;
            Assert.AreEqual("Keep going…", hud.ComposerStatus);
            hud.TypeWord("bat");
            yield return null;
            Assert.AreEqual("BAT", hud.ComposerText);
            Assert.AreEqual("BAT needs T. Smash more furniture.", hud.ComposerStatus);
            hud.SubmitComposer();
            yield return null;
            Assert.AreEqual("You still need some letters.", hud.ComposerStatus);
            Assert.IsFalse(player.Summoner.IsCrafting);
            hud.TypeWord("zzz");
            yield return null;
            StringAssert.StartsWith("That isn't a recipe.", hud.ComposerStatus);
            hud.SubmitComposer();
            yield return null;
            Assert.AreEqual("That recipe is not available.", hud.ComposerStatus);

            player.Inventory.Set("BAT");
            hud.TypeWord("bat");
            yield return null;
            Assert.AreEqual("BAT is ready. Press Enter!", hud.ComposerStatus);
            Assert.IsTrue(player.Summoner.IsSpelling, "Typing never spells by itself.");
            hud.SubmitComposer();
            Assert.IsTrue(player.Summoner.IsCrafting, "Enter spells it.");
            Assert.AreEqual("Spelling BAT…", hud.ToastText);
            yield return null;
            Assert.IsFalse(hud.ComposerOpen);
            Assert.IsFalse(DesktopBinding.Typing);
            player.Summoner.CancelCraft();
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
            yield return null;
            Assert.IsFalse(DesktopBinding.Typing);
        }

        [UnityTest]
        public IEnumerator EverySeatGetsACardWithWinsHealthAndReviveMe()
        {
            var touch = TouchBinding.Shared;
            touch.Enabled = true;
            var you = Spawn(touch);
            var mate = Spawn(new ScriptedBinding());
            mate.Name = "P2";
            mate.transform.position = new Vector3(3f, 0f, 0f);
            var bot = Spawn(new BotBinding());
            bot.Name = "AI 3";
            bot.transform.position = new Vector3(-3f, 0f, 0f);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.SetScoreboard(new[] { you, mate, bot }, p => p == bot ? 2 : 0, 3, true);
            mate.Health.Eliminate();
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreSame(you, hud.LocalPlayer);
            var safe = hud.transform.Find("Safe HUD");
            string Text(string path) => safe.Find(path).GetComponent<TMPro.TMP_Text>().text;
            StringAssert.StartsWith("P1", Text("Roommate 1/Name"), "You're on the roster too, named by seat on a couch.");
            StringAssert.StartsWith("P2", Text("Roommate 2/Name"));
            StringAssert.Contains("WRECKED", Text("Roommate 2/Status"));
            Assert.IsTrue(safe.Find("Roommate 3").gameObject.activeSelf, "Bots get a card.");
            StringAssert.StartsWith("AI 3", Text("Roommate 3/Name"));
            StringAssert.Contains("2/3", Text("Roommate 3/Name"), "Every card shows its wins.");
            Assert.IsFalse(safe.Find("Roommate 4").gameObject.activeSelf);
            Assert.IsNull(safe.Find("Roommate 1/Seat colour").GetComponentInChildren<TMPro.TMP_Text>(), "No seat initial next to the P1 name.");
            var label = (RectTransform)safe.Find("Match label");
            Assert.LessOrEqual(((RectTransform)safe.Find("Roommate 1")).anchoredPosition.y, label.anchoredPosition.y - label.sizeDelta.y, "The roster starts below the match label.");
            Assert.AreEqual("2/3", Text("Side column/Alive/Count"));
            Assert.IsFalse(safe.Find("Side column/Objective").gameObject.activeSelf, "No filler objective when there's nothing to do.");

            Object.Destroy(mate.gameObject);
            hud.SetScoreboard(new[] { you, bot }, _ => 0, 3, true);
            yield return new WaitForSecondsRealtime(0.12f);
            StringAssert.StartsWith("You", Text("Roommate 1/Name"), "The only human is You.");
            Object.Destroy(hud.gameObject);
            foreach (var p in new[] { you, bot }) Object.Destroy(p.gameObject);
        }

        [UnityTest]
        public IEnumerator TouchButtonsWaitForATouchScreen()
        {
            ControlTips.Reset();
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            var safe = hud.transform.Find("Safe HUD");
            Assert.AreEqual(Application.isMobilePlatform, safe.Find("Touch controls").gameObject.activeSelf,
                "A desktop starts with a control tip, even with a touchscreen plugged in.");
            Assert.AreEqual(!Application.isMobilePlatform, safe.Find("Desktop controls").gameObject.activeSelf);
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }

        [Test]
        public void SpellHintNamesTheBestReadyWordThenTheNearestMiss()
        {
            static WordEntry W(string word, bool hidden = false) => new WordEntry { word = word, hidden = hidden };
            var words = new[] { W("BALL"), W("BAT"), W("AXE"), W("SECRET", true) };
            Assert.Greater(LetterScores.ScoreOf("AXE"), LetterScores.ScoreOf("BAT"));
            Assert.AreEqual("BAT ready · Q", GameHud.SpellHintText(words, "TAB".ToCharArray(), 10, "Q"));
            Assert.AreEqual("AXE ready · Q", GameHud.SpellHintText(words, "BATAXE".ToCharArray(), 10, "Q"), "The highest-scoring ready word wins.");
            Assert.AreEqual("BAT ready", GameHud.SpellHintText(words, "BAT".ToCharArray(), 10, ""));
            Assert.AreEqual("BAT needs T", GameHud.SpellHintText(words, "BA".ToCharArray(), 10, "Q"), "BALL is two letters away, BAT only one.");
            Assert.AreEqual("", GameHud.SpellHintText(words, "BA".ToCharArray(), 2, "Q"), "A word that cannot fit the bag is no hint.");
            Assert.AreEqual("", GameHud.SpellHintText(words, "SECRE".ToCharArray(), 10, "Q"), "Hidden words are never hinted.");
            Assert.AreEqual("SECRET ready · Q", GameHud.SpellHintText(words, "SECRET".ToCharArray(), 10, "Q"));
            Assert.AreEqual("", GameHud.SpellHintText(words, "QZ".ToCharArray(), 10, "Q"));
            Assert.AreEqual("", GameHud.SpellHintText(words, new char[0], 10, "Q"));
            Assert.AreEqual("Y", GameHud.SpellKeyLabel(new GamepadBinding(null)));
            Assert.AreEqual("SPELL", GameHud.SpellKeyLabel(TouchBinding.Shared));
            Assert.AreEqual("K", GameHud.SpellKeyLabel(new KeyboardBinding(KeyboardBinding.Side.Left)));
        }

        [UnityTest]
        public IEnumerator SpellHintSitsQuietlyAboveTheTrayAndHidesWhenNothingHelps()
        {
            var touch = TouchBinding.Shared;
            touch.Enabled = true;
            var player = Spawn(touch);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.ShowTouchControls(false);
            player.Inventory.Set("");
            yield return new WaitForSecondsRealtime(0.2f);
            var tray = hud.transform.Find("Safe HUD/Letter bag");
            var hint = tray.Find("Spell hint").GetComponent<TMPro.TMP_Text>();
            Assert.IsFalse(hint.gameObject.activeSelf, "An empty bag has nothing useful to say.");
            Assert.IsNull(hint.GetComponent<Image>(), "The hint is a single line of text, not a card.");
            Assert.IsNotNull(hint.GetComponent<Shadow>());
            Assert.GreaterOrEqual(hint.fontSize, 23f);
            var hintRect = (RectTransform)hint.transform;
            Assert.GreaterOrEqual(hintRect.anchoredPosition.y, 0f, "The line sits above the letter tiles.");

            player.Inventory.Set("BAT");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(hint.gameObject.activeSelf);
            Assert.AreEqual(GameHud.SpellHintText(GameAssets.I.words.Words, player.Inventory.Letters, player.Inventory.Capacity, "SPELL"), hint.text);
            StringAssert.EndsWith(" ready · SPELL", hint.text);

            player.Inventory.Set("BA");
            yield return new WaitForSecondsRealtime(0.2f);
            string expected = GameHud.SpellHintText(GameAssets.I.words.Words, player.Inventory.Letters, player.Inventory.Capacity, "SPELL");
            Assert.AreEqual(expected, hint.text);
            Assert.AreEqual(expected.Length > 0, hint.gameObject.activeSelf);

            player.Inventory.Set("BATBAT");
            Assert.IsTrue(player.Summoner.BeginCraft(new WordEntry { word = "BAT" }));
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(player.Summoner.IsCrafting);
            Assert.IsFalse(hint.gameObject.activeSelf, "The hint steps aside while a word is being spelled.");
            player.Summoner.CancelCraft();
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        [Test]
        public void HouseHeadingsUseTheSameModeNamesAsTheLobby()
        {
            foreach (string mode in new[] { "Dibs", "Duos", "MovingOut", "MovingDay", "Escape" })
                Assert.AreEqual(LobbyPlayModeLabels.Name(mode), GameHud.MatchHeading(mode).name, mode);
            Assert.AreEqual("Duos", GameHud.MatchHeading("Duos").name);
            Assert.AreEqual("Moving Out", GameHud.MatchHeading("MovingOut").name);
            Assert.AreEqual("CO-OP", GameHud.MatchHeading("Escape").tag);
            Assert.AreEqual("The house", GameHud.MatchHeading("Hub").name);
        }
    }
}
