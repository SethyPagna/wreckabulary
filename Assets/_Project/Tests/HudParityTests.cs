using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Wreckabulary.Tests
{
    /// <summary>The Unity HUD and hands follow the browser edition: 1/2 hands, a 5 x 2 tray, a map column, a bag peek and a pause card.</summary>
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
            Assert.IsNotNull(safe.Find("Side column/Minimap"));
            Assert.IsNotNull(safe.Find("Side column/Timer"));
            Assert.IsNotNull(safe.Find("Side column/Alive/Count"), "A head count sits before the timer, as on the web.");
            Assert.IsNotNull(safe.Find("Side column/Objective"));
            Assert.IsNull(safe.Find("Side column/Pause"), "The brand tile is the pause button.");
            Assert.IsNull(safe.Find("Return home"), "The way home is in the pause card.");
            Assert.IsNotNull(safe.Find("Vitals/Hand 1"));
            Assert.IsNotNull(safe.Find("Vitals/Hand 2"));
            Assert.IsNull(safe.Find("Vitals").GetComponent<Image>(), "Vitals sit straight on the game, with no card.");
            Assert.IsNull(safe.Find("Vitals/Health track"));
            StringAssert.Contains("HP", safe.Find("Vitals/HP").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsTrue(safe.Find("Desktop controls").gameObject.activeSelf, "Desktop shows the key bar.");
            Assert.IsFalse(safe.Find("Touch controls").gameObject.activeSelf, "On-screen buttons are for touch only.");

            safe.Find("Vitals/Hand 2").GetComponent<Button>().onClick.Invoke();
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
            safe.Find("Pause/Pause card/Resume").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.Paused);
            Assert.AreEqual(1f, Time.timeScale);

            hud.ShowTouchControls(true);
            Assert.IsFalse(safe.Find("Desktop controls").gameObject.activeSelf);
            Assert.IsNull(safe.Find("Touch controls/SWAP"), "Touch players tap a hand slot instead of a swap button.");
            Assert.IsNotNull(safe.Find("Touch controls/SPELL"));
            Assert.IsNotNull(safe.Find("Touch controls/HOLD DROP"));
            // SMASH throws and places too, so there is no PLACE button (its name has a "/", so Find can't look it up).
            foreach (Transform child in safe.Find("Touch controls"))
                Assert.IsFalse(child.name.StartsWith("PLACE"), "No separate place button: " + child.name);
            var keys = safe.Find("Desktop controls/Keys").GetComponent<TMPro.TMP_Text>().text;
            StringAssert.Contains("smash, throw, place", keys);
            StringAssert.Contains("mouse aim", keys, "Under the overhead camera the mouse aims.");
            StringAssert.DoesNotContain(">F<", keys);
            StringAssert.DoesNotContain("block", keys, "Seven entries, like the web's bar.");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }

        /// <summary>
        /// The web's Tab panel: a dark scrim to the screen's edges with the rest of the HUD stepped aside, the bag on
        /// the left, a large square map in the middle and a recipe book whose ready words spell from a click.
        /// </summary>
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
        public IEnumerator RoundTitlesSitOnAPlaqueThatFitsThem()
        {
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            TMPro.TextMeshProUGUI Line(string name)
            {
                var line = new GameObject(name, typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
                line.transform.SetParent(hud.transform, false);
                typeof(GameHud).GetField(name.ToLowerInvariant(), BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hud, line);
                return line;
            }
            var title = Line("Title"); var subtitle = Line("Subtitle");
            yield return null;
            var plaque = (RectTransform)hud.transform.Find("Title plaque");
            Assert.IsNotNull(plaque);
            Assert.AreSame(plaque, title.transform.parent, "The title sits on the plaque.");
            Assert.AreSame(plaque, subtitle.transform.parent);
            Assert.IsFalse(plaque.gameObject.activeSelf, "No words, no plaque.");

            hud.SetTitle("ROUND 1", "Dibs on the living room");
            Assert.IsTrue(plaque.gameObject.activeSelf);
            Assert.GreaterOrEqual(plaque.sizeDelta.x, 720f);
            Assert.GreaterOrEqual(plaque.sizeDelta.y, 120f);
            var ink = plaque.GetComponent<Image>().color;
            Assert.AreEqual("173B3C", ColorUtility.ToHtmlStringRGB(ink));
            Assert.AreEqual(.85f, ink.a, .01f);
            Assert.AreEqual(0f, title.outlineWidth, "A plaque, not an outline, makes it readable.");
            float shortWidth = plaque.sizeDelta.x;
            hud.SetTitle("SOMEBODY WITH A LONG NAME WINS THE ROUND", "3/5");
            Assert.Greater(plaque.sizeDelta.x, shortWidth, "It grows with its words.");
            hud.SetTitle("", "");
            Assert.IsFalse(plaque.gameObject.activeSelf);
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }

        [UnityTest]
        public IEnumerator OnlyCouchRoommatesGetCardsAndTheCountShowsWhoIsUp()
        {
            var touch = TouchBinding.Shared;
            touch.Enabled = true;
            var you = Spawn(touch);
            var mate = Spawn(new ScriptedBinding());
            mate.transform.position = new Vector3(3f, 0f, 0f);
            var bot = Spawn(new BotBinding());
            bot.transform.position = new Vector3(-3f, 0f, 0f);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.SetScoreboard(new[] { you, mate, bot }, _ => 0, 3, false);
            mate.Health.Eliminate();
            yield return new WaitForSecondsRealtime(0.12f);
            Assert.AreSame(you, hud.LocalPlayer);
            var safe = hud.transform.Find("Safe HUD");
            Assert.IsTrue(safe.Find("Roommate 1").gameObject.activeSelf, "A couch roommate keeps a card.");
            StringAssert.Contains("WRECKED", safe.Find("Roommate 1/Status").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsFalse(safe.Find("Roommate 2").gameObject.activeSelf, "AI housemates get no card, as on the web.");
            Assert.AreEqual("2/3", safe.Find("Side column/Alive/Count").GetComponent<TMPro.TMP_Text>().text);
            Assert.IsFalse(safe.Find("Side column/Objective").gameObject.activeSelf, "No filler objective when there's nothing to do.");
            Object.Destroy(hud.gameObject);
            foreach (var p in new[] { you, mate, bot }) Object.Destroy(p.gameObject);
        }

        [UnityTest]
        public IEnumerator TouchButtonsWaitForATouchScreen()
        {
            var you = Spawn(TouchBinding.Shared);
            var hud = new GameObject("Parity HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            var safe = hud.transform.Find("Safe HUD");
            Assert.AreEqual(Application.isMobilePlatform, safe.Find("Touch controls").gameObject.activeSelf,
                "A desktop starts with the key bar, even with a touchscreen plugged in.");
            Assert.AreEqual(!Application.isMobilePlatform, safe.Find("Desktop controls").gameObject.activeSelf);
            Object.Destroy(hud.gameObject);
            Object.Destroy(you.gameObject);
        }
    }
}
