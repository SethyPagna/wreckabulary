using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public sealed class LoadoutWardrobeTests
    {
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, LobbyThemes.PreferenceKey,
            LobbyLooksStore.LooksKey, LobbyLooksStore.SeenKey, LastPlayChoice.StorageKey };
        static readonly Vector2[] Windows = { new Vector2(1920, 1080), new Vector2(1280, 720), new Vector2(1248, 576) };
        const float Wait = 5f, Slack = .5f;
        const int StartCoins = 1000;
        const string NewerLooks = "99/Later,later";
        string[] saved;
        bool remember;
        LobbyMenu menu;
        LoadoutPage page;
        Gamepad pad;
        Keyboard keys;
        InputSettings.EditorInputBehaviorInPlayMode? keysRoute;
        InputSettings.BackgroundBehavior keysFocus;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            remember = LobbyMenu.RememberChoices;
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            LobbyMenu.RememberChoices = false;
            PlayerPrefs.SetString(MatchTally.CareerKey, new Career { Coins = StartCoins }.Serialize());
            LobbyThemes.Restore();
            MatchTally.LastResult = null;
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning, Wait, "lobby ready");
            menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Loadout);
            yield return null;
            Canvas.ForceUpdateCanvases();
            page = menu.Page<LoadoutPage>();
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
            if (saved != null)
                for (int i = 0; i < Keys.Length; i++)
                    if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            PlayerPrefs.Save();
            LobbyMenu.RememberChoices = remember;
            LobbyThemes.Restore();
            MatchTally.LastResult = null;
        }

        Button Find(string name)
        {
            var button = page.Root.GetComponentsInChildren<Button>().FirstOrDefault(candidate => candidate.name == name);
            Assert.IsNotNull(button, "a '" + name + "' control is showing");
            return button;
        }

        bool Has(string name) => page.Root.GetComponentsInChildren<Button>().Any(candidate => candidate.name == name);

        void Click(string name)
        {
            var button = Find(name);
            Assert.IsTrue(button.interactable, "'" + name + "' can be pressed");
            button.onClick.Invoke();
        }

        static Bounds OwnBounds(RectTransform root, RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var bounds = new Bounds(root.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(root.InverseTransformPoint(corners[i]));
            return bounds;
        }

        bool Marked(string tile, string mark) => Find(tile).GetComponentsInChildren<RectTransform>().Any(rect => rect.name == mark);

        string Hint(string name) => Find(name).GetComponent<LobbyHint>().Text;

        string Detail => page.Root.GetComponentsInChildren<TMP_Text>().Single(text => text.name == LoadoutPage.DetailLine).text;

        static GameObject Selected => EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;

        string Stored => PlayerPrefs.GetString(LobbyMenu.OutfitKey, "");

        bool Wears(string node) => menu.Stage.Avatar.GetComponentsInChildren<Transform>(true)
            .Any(part => part.name == node && part.gameObject.activeInHierarchy);

        bool Shows(string mesh) => menu.Stage.Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(skin => skin.name == mesh && skin.enabled);

        static void Point(Component control, bool over)
        {
            var press = control.GetComponent<LobbyPress>();
            var pointer = new PointerEventData(EventSystem.current);
            if (over) press.OnPointerEnter(pointer);
            else press.OnPointerExit(pointer);
        }

        IEnumerator Reopen()
        {
            menu.Open(LobbyMenu.Home);
            yield return null;
            menu.Open(LobbyMenu.Loadout);
            yield return null;
            Canvas.ForceUpdateCanvases();
        }

        IEnumerator Pull(bool right)
        {
            if (pad == null) pad = InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(pad, right ? new GamepadState { rightTrigger = 1f } : new GamepadState { leftTrigger = 1f });
            yield return null;
            InputSystem.QueueStateEvent(pad, new GamepadState());
            yield return null;
        }

        IEnumerator Tap(Key key)
        {
            if (keys == null)
            {
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

        void WearBottoms(int colour)
        {
            var next = menu.Outfit.Clone();
            next.Colours["Bottoms"] = GameConfig.Current.Wardrobe.Palettes["Bottoms"][colour].Id;
            menu.Wear(next);
        }

        sealed class Window : System.IDisposable
        {
            readonly Canvas canvas;
            readonly CanvasScaler scaler;
            readonly Vector2 size;
            readonly float scale;
            readonly bool scaled;
            public readonly RectTransform Safe;

            public Window(LobbyMenu menu)
            {
                canvas = menu.GetComponent<Canvas>(); scaler = menu.GetComponent<CanvasScaler>();
                Safe = (RectTransform)menu.transform.Find("Safe area");
                size = Safe.sizeDelta; scale = canvas.scaleFactor; scaled = scaler.enabled;
                scaler.enabled = false; canvas.scaleFactor = Screen.height / 1080f;
                Canvas.ForceUpdateCanvases();
            }

            public void Set(float width, float height)
            {
                Safe.sizeDelta += new Vector2(width / height * 1080f, 1080f) - Safe.rect.size;
                Canvas.ForceUpdateCanvases();
            }

            public void Dispose()
            {
                if (Safe) Safe.sizeDelta = size;
                if (canvas) canvas.scaleFactor = scale;
                if (scaler) scaler.enabled = scaled;
                Canvas.ForceUpdateCanvases();
            }
        }

        [UnityTest]
        public IEnumerator HoverTriesAPieceOnWithoutSavingOrRebuilding()
        {
            page.ShowCategory("Headwear");
            yield return null;
            string worn = menu.Outfit.Serialize(), stored = Stored;
            StringAssert.StartsWith("Hood", Detail, "the detail line rests on what you wear");
            var tile = Find("CatEars");
            Assert.IsFalse(Wears("Cute_CatEars"));
            Point(tile, true);
            yield return TestScenes.WaitUntil(() => Wears("Cute_CatEars"), Wait, "the avatar tries the cat ears on");
            StringAssert.StartsWith("Cat ears", Detail, "the detail line names the focused piece");
            Assert.IsTrue(tile, "hover does not rebuild the page");
            Assert.AreSame(tile, Find("CatEars"));
            Assert.AreEqual(worn, menu.Outfit.Serialize(), "hover wears nothing");
            Assert.AreEqual(stored, Stored, "and saves nothing");
            Assert.IsNull(page.TryOn, "and is not a try-on");
            Point(tile, false);
            yield return TestScenes.WaitUntil(() => !Wears("Cute_CatEars"), Wait, "the avatar goes back to what is worn");
            StringAssert.StartsWith("Hood", Detail);

            var none = Find("No Headwear");
            Point(none, true);
            yield return TestScenes.WaitUntil(() => !Shows("SK_Hood"), Wait, "hovering None shows the bare head");
            Assert.AreEqual("Hood", menu.Outfit.PieceIn("Headwear"));
            Point(none, false);
            yield return TestScenes.WaitUntil(() => Shows("SK_Hood"), Wait, "the hood comes back");
        }

        [UnityTest]
        public IEnumerator ALockedPieceIsTriedOnThenTakenOffOrBought()
        {
            page.ShowCategory("Badge");
            yield return null;
            Assert.IsTrue(Marked("SweetBadge", LoadoutPage.LockMark), "a piece you do not own shows a lock");
            var price = Find("SweetBadge").GetComponentsInChildren<RectTransform>().Single(rect => rect.name == LoadoutPage.PricePill);
            Assert.AreEqual("400", price.GetComponentInChildren<TMP_Text>().text, "and what the look that grants it costs");
            StringAssert.Contains("Sugar Rush", Hint("SweetBadge"));
            Assert.IsFalse(Marked("TBadge", LoadoutPage.LockMark));
            Assert.IsTrue(Marked("TBadge", LoadoutPage.CheckMark));
            string worn = menu.Outfit.Serialize(), stored = Stored;

            Click("SweetBadge");
            yield return null;
            Assert.AreEqual(LobbyMenu.Loadout, menu.Current, "a locked piece no longer sends you to the shop");
            Assert.AreEqual("SweetBadge", page.TryOn.PieceIn("Badge"));
            Assert.AreEqual(worn, menu.Outfit.Serialize(), "a try-on wears nothing");
            Assert.AreEqual(stored, Stored, "and saves nothing");
            Assert.AreSame(Find("SweetBadge").gameObject, Selected, "focus stays on the tile");
            Assert.IsTrue(Marked("SweetBadge", LoadoutPage.TriedRing), "the tried piece is ringed");
            Assert.IsFalse(Marked("SweetBadge", LoadoutPage.CheckMark), "but not ticked");
            Assert.IsTrue(Marked("TBadge", LoadoutPage.CheckMark), "what you really wear keeps its tick");
            yield return TestScenes.WaitUntil(() => Wears("Sig_SweetBadge"), Wait, "the avatar keeps the try-on on");
            var line = page.Root.GetComponentsInChildren<TMP_Text>().Single(text => text.name == LoadoutPage.GetBarLine);
            Assert.AreEqual("Comes with Sugar Rush", line.text);
            Assert.IsTrue(Has(LoadoutPage.BuyPrefix + "look:candy") && Has(LoadoutPage.TakeOff) && Has(LoadoutPage.SeeInShop));
            var scroll = page.Root.GetComponentsInChildren<ScrollRect>().Single();
            var bar = page.Root.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == LoadoutPage.GetBar);
            var corners = new Vector3[4];
            bar.GetWorldCorners(corners);
            float barTop = corners[1].y;
            scroll.viewport.GetWorldCorners(corners);
            Assert.GreaterOrEqual(corners[0].y, barTop - Slack, "the grid makes room for the Get bar");

            Click(LoadoutPage.TakeOff);
            yield return null;
            Assert.IsNull(page.TryOn);
            Assert.IsFalse(Has(LoadoutPage.TakeOff), "the Get bar leaves with the try-on");
            yield return TestScenes.WaitUntil(() => !Wears("Sig_SweetBadge"), Wait, "the avatar wears the saved outfit again");
            Assert.AreEqual(worn, menu.Outfit.Serialize());

            Click("SweetBadge");
            yield return null;
            menu.Career.Coins = 100;
            Click(LoadoutPage.BuyPrefix + "look:candy");
            yield return null;
            Assert.AreEqual(100, menu.Career.Coins, "too few coins buys nothing");
            Assert.IsTrue(menu.Messages.Any(message => message.Contains("300 more coins")), "and says how many are missing");
            Assert.AreEqual("SweetBadge", page.TryOn.PieceIn("Badge"), "the try-on stays");

            menu.Career.Coins = StartCoins;
            Click(LoadoutPage.BuyPrefix + "look:candy");
            yield return null;
            Assert.AreEqual(600, menu.Career.Coins, "Sugar Rush costs 400");
            Assert.IsTrue(menu.Career.Owns("look", "candy"));
            Assert.AreEqual(600, MatchTally.LoadCareer().Coins, "the purchase is saved");
            Assert.IsNull(page.TryOn);
            Assert.AreEqual("SweetBadge", menu.Outfit.PieceIn("Badge"), "what you tried on is worn once it is yours");
            Assert.AreEqual("SweetBadge", Outfit.Deserialize(Stored).PieceIn("Badge"), "and saved");
            Assert.IsFalse(Has(LoadoutPage.TakeOff));
            Assert.IsTrue(Marked("SweetBadge", LoadoutPage.CheckMark));
            Assert.IsFalse(Marked("SweetBadge", LoadoutPage.LockMark));
            Assert.AreEqual(LobbyMenu.Loadout, menu.Current);
        }

        [UnityTest]
        public IEnumerator TheHoodAndHoodieSayWhatTheyBringAndTakeOff()
        {
            page.ShowCategory("Top");
            yield return null;
            StringAssert.Contains("takes off Hood", Hint("Crewneck"), "the crewneck warns before the click");
            var crewneck = Find("Crewneck");
            Point(crewneck, true);
            yield return TestScenes.WaitUntil(() => Shows("SK_Crewneck") && !Shows("SK_Hood"), Wait, "hover shows the whole result");
            Point(crewneck, false);
            yield return TestScenes.WaitUntil(() => Shows("SK_Hood"), Wait, "and puts it back");
            Click("Crewneck");
            yield return null;
            Assert.IsNull(menu.Outfit.PieceIn("Headwear"));
            Assert.IsTrue(menu.Messages.Contains("Hood taken off."));

            page.ShowCategory("Headwear");
            yield return null;
            Assert.IsTrue(Marked("Hood", LoadoutPage.BringsMark), "the hood shows it brings a top");
            Assert.IsFalse(Marked("Cap", LoadoutPage.BringsMark));
            Assert.AreEqual("Hood · puts on Hoodie", Hint("Hood"));
            Assert.IsTrue(Marked("No Headwear", LoadoutPage.CheckMark), "nothing on the head is the chosen tile");
            Click("Hood");
            yield return null;
            Assert.AreEqual("Hoodie", menu.Outfit.PieceIn("Top"));
            Assert.IsTrue(menu.Messages.Contains("Hoodie put on to go with the Hood."));
            Assert.IsFalse(Marked("Hood", LoadoutPage.BringsMark), "nothing more to bring once the hoodie is on");
            Assert.AreSame(Find("Hood").gameObject, Selected);
            StringAssert.Contains("matches your top", Detail, "the hood takes the colour of the top");
            Assert.AreEqual(GameConfig.Current.Wardrobe.Palettes["Top"].Count,
                page.Root.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith(LoadoutPage.ColourPrefix)), "and offers the top's colours");
        }

        [UnityTest]
        public IEnumerator DefaultHairIsChosenAndPaintingItStoresTheStyle()
        {
            Assert.AreEqual("Hair", page.Category, "the wardrobe opens on the first category");
            Assert.IsNull(menu.Outfit.PieceIn("Hair"));
            Assert.IsTrue(Marked("HairSwept", LoadoutPage.CheckMark));
            Assert.IsFalse(Has("No Hair"));
            Assert.AreEqual(GameConfig.Current.Wardrobe.Palettes["Hair"].Count,
                page.Root.GetComponentsInChildren<Button>().Count(button => button.name.StartsWith(LoadoutPage.ColourPrefix)), "hair has colours before a style is stored");
            string stored = Stored;
            Click("Colour cocoa");
            yield return null;
            Assert.AreEqual(stored, Stored, "the colour you already have writes nothing");
            Click("Colour rose");
            yield return null;
            Assert.AreEqual("HairSwept", menu.Outfit.PieceIn("Hair"), "the default style is stored with its new colour");
            Assert.AreEqual("rose", menu.Outfit.ColourOf("Hair"));
            Assert.AreEqual("rose", Outfit.Deserialize(Stored).ColourOf("Hair"), "saved");
            Assert.AreSame(Find("Colour rose").gameObject, Selected);
            StringAssert.Contains("Rose", Detail);

            page.ShowCategory("Eyes");
            yield return null;
            Assert.IsTrue(Marked("EyesSparkle", LoadoutPage.CheckMark));
            Assert.IsFalse(page.Root.GetComponentsInChildren<Button>().Any(button => button.name.StartsWith(LoadoutPage.ColourPrefix)), "eyes have no colours");
            var grid = page.Root.GetComponentsInChildren<ScrollRect>().Single();
            var strip = page.Root.GetComponentsInChildren<RectTransform>().FirstOrDefault(rect => rect.name == "Strip");
            Assert.IsNull(strip, "an empty strip is hidden and the grid takes the room");
            Assert.Greater(grid.viewport.rect.height, 3 * LoadoutPage.Tile, "three whole rows fit");
        }

        [UnityTest]
        public IEnumerator TriggersAndPageKeysStepThroughTheCategories()
        {
            Assert.AreEqual(LoadoutPage.RailOrder[0], page.Category);
            for (int i = 1; i <= LoadoutPage.RailOrder.Length; i++)
            {
                string next = LoadoutPage.RailOrder[i % LoadoutPage.RailOrder.Length];
                yield return Pull(true);
                yield return TestScenes.WaitUntil(() => page.Category == next, Wait, "RT reaches " + next);
                Assert.AreSame(page.First.gameObject, Selected, "focus lands on the chosen tile of " + next);
            }
            string last = LoadoutPage.RailOrder[LoadoutPage.RailOrder.Length - 1];
            yield return Pull(false);
            yield return TestScenes.WaitUntil(() => page.Category == last, Wait, "LT wraps back to " + last);

            EventSystem.current.SetSelectedGameObject(Find(LoadoutPage.CategoryPrefix + last).gameObject);
            yield return Pull(true);
            yield return TestScenes.WaitUntil(() => page.Category == LoadoutPage.RailOrder[0], Wait, "RT wraps to the first category");
            Assert.AreSame(Find(LoadoutPage.CategoryPrefix + LoadoutPage.RailOrder[0]).gameObject, Selected, "a rail focus stays on the rail");

            yield return Tap(Key.PageDown);
            yield return TestScenes.WaitUntil(() => page.Category == LoadoutPage.RailOrder[1], Wait, "PageDown steps forward");
            yield return Tap(Key.PageUp);
            yield return TestScenes.WaitUntil(() => page.Category == LoadoutPage.RailOrder[0], Wait, "PageUp steps back");

            page.ShowRecipes(true);
            yield return null;
            var rail = page.Root.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == LoadoutPage.RailName);
            Assert.AreEqual(LoadoutPage.RailFade, rail.GetComponent<CanvasGroup>().alpha, .001f, "the rail stays, faded, on the Equipment tab");
            yield return Pull(true);
            yield return TestScenes.WaitUntil(() => !page.ShowingRecipes && page.Category == LoadoutPage.RailOrder[1], Wait, "a trigger returns to the Character tab");

            page.ShowLooks(true);
            yield return null;
            Click(LoadoutPage.CategoryPrefix + "Back");
            yield return null;
            Assert.IsFalse(page.ShowingLooks, "a category returns to Pieces");
            Assert.AreEqual("Back", page.Category);
            Assert.AreEqual(LobbyMenu.Loadout, menu.Current, "the triggers never leave the page");
        }

        [UnityTest]
        public IEnumerator FocusStaysOnWhatYouPressedAndTheEdgesAreWired()
        {
            Click(LoadoutPage.CategoryPrefix + "Face");
            yield return null;
            Assert.AreSame(Find(LoadoutPage.CategoryPrefix + "Face").gameObject, Selected);
            Click("Glasses");
            yield return null;
            Assert.AreSame(Find("Glasses").gameObject, Selected);
            Click("Colour gold");
            yield return null;
            Assert.AreSame(Find("Colour gold").gameObject, Selected);
            Assert.AreEqual("gold", menu.Outfit.ColourOf("Face"));

            var first = Find("No Face");
            var rail = Find(LoadoutPage.CategoryPrefix + "Face");
            Assert.AreSame(rail, first.navigation.selectOnLeft, "left of the first column is the chosen category");
            Assert.AreSame(Find(LoadoutPage.PiecesView), first.navigation.selectOnUp, "above the first row is the Pieces switch");
            Assert.AreSame(Find("Glasses"), first.navigation.selectOnRight);
            Assert.AreSame(Find("Colour classic"), Find("StarShades").navigation.selectOnDown, "under the last row is the first swatch");
            Assert.AreSame(Find(LoadoutPage.CategoryPrefix + "Headwear"), rail.navigation.selectOnUp, "the rail steps inside its column");
            Assert.AreSame(Find(LoadoutPage.CategoryPrefix + "Badge"), rail.navigation.selectOnDown);
            Assert.AreSame(Find(LoadoutPage.CategoryPrefix + "Footwear"), rail.navigation.selectOnRight, "and across to the other column");
            Assert.AreSame(page.First, Find(LoadoutPage.CategoryPrefix + "Footwear").navigation.selectOnRight, "the right column leads to the chosen tile");
            Assert.AreSame(Find("Glasses").gameObject, page.First.gameObject);

            Click(LoadoutPage.LooksView);
            yield return null;
            Assert.IsTrue(page.ShowingLooks);
            Assert.AreSame(Find(LoadoutPage.LooksView).gameObject, Selected);
            Click(LoadoutPage.PiecesView);
            yield return null;
            Assert.AreSame(Find(LoadoutPage.PiecesView).gameObject, Selected);
            Click(LoadoutPage.OwnedToggle);
            yield return null;
            Assert.IsTrue(page.OwnedOnly);
            Assert.AreSame(Find(LoadoutPage.OwnedToggle).gameObject, Selected);
            Assert.AreEqual(LoadoutPage.OwnedHint, Hint(LoadoutPage.OwnedToggle));

            Click(LoadoutPage.RecipesTab);
            yield return null;
            Assert.AreSame(Find(LoadoutPage.RecipesTab).gameObject, Selected);
            Click("Finish Classic");
            yield return null;
            Assert.AreSame(Find("Finish Classic").gameObject, Selected);
            Assert.IsTrue(Marked("Finish Classic", LoadoutPage.CheckMark));
        }

        [UnityTest]
        public IEnumerator OwnedOnlyHidesWhatIsLocked()
        {
            page.ShowCategory("Badge");
            yield return null;
            Assert.IsTrue(Has("SweetBadge") && Has("PixelBadge"));
            page.ShowOwnedOnly(true);
            yield return null;
            Assert.IsFalse(Has("SweetBadge") || Has("PixelBadge"), "locked badges are filtered out");
            Assert.IsTrue(Has("TBadge") && Has("SunBadge") && Has("No Badge"));

            page.ShowCategory("Top");
            yield return null;
            var shown = page.Root.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(LoadoutPage.ColourPrefix))
                .Select(button => button.name.Substring(LoadoutPage.ColourPrefix.Length)).ToArray();
            CollectionAssert.AreEqual(GameConfig.Current.Wardrobe.Palettes["Top"].Select(colour => colour.Id).Where(id => menu.Career.Owns("colour", id)).ToArray(), shown);

            page.ShowLooks(true);
            yield return null;
            Assert.IsFalse(Has(LoadoutPage.LookPrefix + "candy") || Has(LoadoutPage.LookPrefix + "arcade"), "looks you do not own are filtered out");
            Assert.IsTrue(Has(LoadoutPage.LookPrefix + "sunroom") && Has(LoadoutPage.LookPrefix + "winter"));
            page.ShowOwnedOnly(false);
            yield return null;
            Assert.IsTrue(Has(LoadoutPage.LookPrefix + "candy"));
            Assert.IsTrue(Marked(LoadoutPage.LookPrefix + "candy", LoadoutPage.LockMark));
            var owned = page.Root.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(LoadoutPage.LookPrefix)).Select(button => button.name).ToArray();
            CollectionAssert.AreEqual(CosmeticBundles.All.Where(look => look.OwnedBy(menu.Career)).Concat(CosmeticBundles.All.Where(look => !look.OwnedBy(menu.Career)))
                .Select(look => LoadoutPage.LookPrefix + look.Id).ToArray(), owned, "owned looks first, then the rest");
        }

        [UnityTest]
        public IEnumerator ShopLooksAreWornOrTriedOnFromTheLooksView()
        {
            page.ShowLooks(true);
            yield return null;
            string stored = Stored;
            Click(LoadoutPage.LookPrefix + "sunroom");
            yield return null;
            Assert.AreEqual("SunBadge", menu.Outfit.PieceIn("Badge"), "a look you own is worn at once");
            Assert.AreNotEqual(stored, Stored, "and saved");
            Assert.IsTrue(Marked(LoadoutPage.LookPrefix + "sunroom", LoadoutPage.CheckMark));
            Assert.AreSame(Find(LoadoutPage.LookPrefix + "sunroom").gameObject, Selected);
            stored = Stored;

            Click(LoadoutPage.LookPrefix + "arcade");
            yield return null;
            Assert.AreEqual("PixelBadge", page.TryOn.PieceIn("Badge"), "a look you do not own is tried on");
            Assert.AreEqual(stored, Stored);
            Assert.IsTrue(Has(LoadoutPage.BuyPrefix + "look:arcade"));
            Assert.IsTrue(Marked(LoadoutPage.LookPrefix + "arcade", LoadoutPage.TriedRing));
            Click(LoadoutPage.BuyPrefix + "look:arcade");
            yield return null;
            Assert.AreEqual(StartCoins - 550, menu.Career.Coins);
            Assert.AreEqual("PixelBadge", menu.Outfit.PieceIn("Badge"));
            Assert.AreEqual("Arcade", menu.Outfit.SkinFor("SHIELD"));
            Assert.IsNull(page.TryOn);
        }

        [UnityTest]
        public IEnumerator SaveLookAddsWearsReplacesAndDeletesWithoutTouchingStoredData()
        {
            Assert.IsFalse(LobbyMenu.RememberChoices, "tests keep saved looks in memory");
            Assert.AreEqual(0, page.Looks.Count, "every lobby load starts with none");
            Assert.AreEqual(LoadoutPage.SaveLookHint, Hint(LoadoutPage.SaveLookButton));
            string first = menu.Outfit.Serialize();
            Click(LoadoutPage.SaveLookButton);
            yield return null;
            Assert.AreEqual(1, page.Looks.Count);
            Assert.AreEqual("Look 1", page.Looks[0].Name);
            Assert.AreEqual(first, page.Looks[0].OutfitText);
            Assert.IsTrue(page.ShowingLooks, "saving shows the Looks view");
            Assert.AreSame(Find(LoadoutPage.SavedPrefix + "1").gameObject, Selected, "on the new look");
            Assert.IsTrue(menu.Messages.Contains("Saved as Look 1."));
            Assert.IsTrue(Marked(LoadoutPage.SavedPrefix + "1", LoadoutPage.CheckMark), "the look you wear is ticked");
            Assert.IsTrue(Find(LoadoutPage.SavedPrefix + "1").GetComponentsInChildren<TMP_Text>().Any(text => text.text == "Look 1"), "the tile names the look");

            Click(LoadoutPage.SaveLookButton);
            yield return null;
            Assert.AreEqual(1, page.Looks.Count, "the same outfit is not saved twice");
            Assert.IsTrue(menu.Messages.Contains("Already saved as Look 1."));

            for (int i = 2; i <= SavedLooks.Slots; i++)
            {
                WearBottoms(i + 1);
                page.SaveLook();
                yield return null;
                Assert.AreEqual(i, page.Looks.Count);
                Assert.AreSame(Find(LoadoutPage.SavedPrefix + i).gameObject, Selected);
            }
            Click(LoadoutPage.SavedPrefix + "1");
            yield return null;
            Assert.AreEqual(first, menu.Outfit.Serialize(), "a saved look is worn on click");
            Assert.AreEqual(first, Stored, "and saved as the outfit");
            Assert.IsNull(page.TryOn);

            WearBottoms(SavedLooks.Slots + 2);
            string seventh = menu.Outfit.Serialize();
            page.SaveLook();
            yield return null;
            Assert.AreEqual(SavedLooks.Slots, page.Looks.Count, "a full set does not grow");
            Assert.IsTrue(menu.Messages.Contains(SavedLooks.Full));
            Assert.IsTrue(page.Replacing);
            Assert.AreEqual(LoadoutPage.ReplacePrompt, Detail);
            Click(LoadoutPage.PiecesView);
            yield return null;
            Assert.IsFalse(page.Replacing, "any other action cancels the replace");
            page.SaveLook();
            yield return null;
            Assert.IsTrue(page.Replacing);
            Click(LoadoutPage.SavedPrefix + "2");
            yield return null;
            Assert.IsFalse(page.Replacing);
            Assert.AreEqual(seventh, page.Looks[1].OutfitText, "the picked look now holds what you wear");
            Assert.AreEqual("Look 2", page.Looks[1].Name, "under its own name");
            Assert.AreEqual(seventh, menu.Outfit.Serialize(), "replacing wears nothing new");

            Point(Find(LoadoutPage.SavedPrefix + "3"), true);
            yield return TestScenes.WaitUntil(() => Detail == "Look 3", Wait, "the detail line names the focused look");
            Assert.IsTrue(Find(LoadoutPage.DeleteLook).interactable, "the focused look can be deleted");
            string third = page.Looks[2].OutfitText;
            Click(LoadoutPage.DeleteLook);
            yield return null;
            Assert.AreEqual(SavedLooks.Slots, page.Looks.Count, "one press only asks");
            Assert.AreEqual("Press again to delete Look 3", Detail);
            Click(LoadoutPage.DeleteLook);
            yield return null;
            Assert.AreEqual(SavedLooks.Slots - 1, page.Looks.Count, "the second press deletes");
            Assert.IsTrue(page.Looks.IndexOf(third) < 0);
            Assert.IsTrue(menu.Messages.Contains("Deleted Look 3."));
            Assert.IsFalse(PlayerPrefs.HasKey(LobbyLooksStore.LooksKey), "nothing reached the stored looks");
            Assert.IsFalse(PlayerPrefs.HasKey(LobbyLooksStore.SeenKey), "nor the seen marks");

            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && LobbyMenu.Instance != menu && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                Wait, "a fresh lobby");
            LobbyMenu.Instance.Open(LobbyMenu.Loadout);
            yield return null;
            Assert.AreEqual(0, LobbyMenu.Instance.Page<LoadoutPage>().Looks.Count, "a new lobby load starts with none again");
        }

        [UnityTest]
        public IEnumerator StoredLooksAreUsedOnlyWhenRememberingAndANewerFormatIsLeftAlone()
        {
            LobbyMenu.RememberChoices = true;
            var beach = new SavedLooks();
            Assert.IsNull(beach.Add("Beach", menu.Outfit));
            PlayerPrefs.SetString(LobbyLooksStore.LooksKey, beach.Serialize());
            yield return Reopen();
            Assert.AreEqual(1, page.Looks.Count, "the stored looks are read on entering");
            Assert.AreEqual("Beach", page.Looks[0].Name);
            Assert.IsTrue(PlayerPrefs.HasKey(LobbyLooksStore.SeenKey), "the seen marks are stored when choices are remembered");
            WearBottoms(3);
            page.SaveLook();
            yield return null;
            Assert.IsTrue(SavedLooks.TryDeserialize(PlayerPrefs.GetString(LobbyLooksStore.LooksKey), out var stored));
            Assert.AreEqual(2, stored.Count, "a new look is written to the stored set");
            Assert.AreEqual(menu.Outfit.Serialize(), stored[1].OutfitText);

            PlayerPrefs.SetString(LobbyLooksStore.LooksKey, NewerLooks);
            yield return Reopen();
            Assert.AreEqual(0, page.Looks.Count, "looks from a newer build are not shown");
            Assert.IsFalse(Find(LoadoutPage.SaveLookButton).interactable, "and cannot be added to");
            page.SaveLook();
            yield return null;
            Assert.AreEqual(NewerLooks, PlayerPrefs.GetString(LobbyLooksStore.LooksKey), "and are never overwritten");
            Assert.IsTrue(menu.Messages.Contains(LoadoutPage.NewerLooks));
        }

        [UnityTest]
        public IEnumerator NewMarksShowWhatWasBoughtSinceTheLastVisit()
        {
            page.ShowCategory("Top");
            yield return null;
            Assert.IsFalse(Marked("Colour sky", LoadoutPage.NewMark));
            Assert.IsNull(menu.Career.Buy("colour:sky"));
            yield return Reopen();
            page.ShowCategory("Top");
            yield return null;
            Assert.IsTrue(Marked("Colour sky", LoadoutPage.NewMark), "a colour bought since the last visit is marked new");
            Assert.IsFalse(Marked("Colour mint", LoadoutPage.NewMark), "what was always yours is not");
            yield return Reopen();
            page.ShowCategory("Top");
            yield return null;
            Assert.IsFalse(Marked("Colour sky", LoadoutPage.NewMark), "the mark is gone on the next visit");
            Assert.IsFalse(PlayerPrefs.HasKey(LobbyLooksStore.SeenKey));
        }

        [UnityTest]
        public IEnumerator EquipmentHoldsTheGearAndLeavingPutsItDown()
        {
            var held = GameConfig.Current.Items.Enabled.Where(item => !string.IsNullOrEmpty(item.Model)).Take(2).ToArray();
            Assert.AreEqual(2, held.Length, "two recipes with models");
            Assert.IsFalse(menu.Stage.IsHoldingItem, "the Character tab holds nothing");
            page.PinRecipe(held[0].Id);
            page.ShowRecipes(true);
            yield return null;
            Assert.IsTrue(menu.Stage.IsHoldingItem, "the Equipment tab puts the pinned recipe in the avatar's hand");
            var pinned = menu.Stage.HeldItem;
            var other = page.Root.GetComponentsInChildren<LobbyRecipeTileView>().Single(view => view.name == LoadoutPage.RecipePrefix + held[1].Id);
            other.Press.OnPointerEnter(new PointerEventData(EventSystem.current));
            yield return TestScenes.WaitUntil(() => menu.Stage.IsHoldingItem && menu.Stage.HeldItem != pinned, Wait, "hovering a recipe holds that recipe");
            var hovered = menu.Stage.HeldItem;
            other.Press.OnPointerExit(new PointerEventData(EventSystem.current));
            yield return TestScenes.WaitUntil(() => menu.Stage.IsHoldingItem && menu.Stage.HeldItem != hovered, Wait, "leaving it holds the pinned recipe again");

            string worn = menu.Outfit.Serialize(), stored = Stored;
            var finish = Find(LoadoutPage.FinishPrefix + "Arcade");
            Assert.IsTrue(Marked(finish.name, LoadoutPage.LockMark));
            Point(finish, true);
            yield return null;
            yield return null;
            Assert.IsTrue(menu.Stage.IsHoldingItem, "hovering a finish keeps something in the hand");
            Assert.AreEqual(worn, menu.Outfit.Serialize());
            Point(finish, false);
            yield return null;
            Click(finish.name);
            yield return null;
            Assert.AreEqual(worn, menu.Outfit.Serialize(), "a finish you do not own is only tried on");
            Assert.AreEqual(stored, Stored);
            Assert.IsTrue(Has(LoadoutPage.BuyPrefix + "skin:Arcade"));
            Assert.IsFalse(Has("Recipe finish"), "the Get bar takes the place of the per-recipe finish action");
            Assert.IsTrue(menu.Stage.IsHoldingItem);
            Click(LoadoutPage.TakeOff);
            yield return null;
            Assert.IsTrue(Has("Recipe finish"));

            page.ShowRecipes(false);
            yield return null;
            Assert.IsFalse(menu.Stage.IsHoldingItem, "back on the Character tab the hand is empty");
            page.ShowRecipes(true);
            yield return null;
            Assert.IsTrue(menu.Stage.IsHoldingItem);
            menu.Open(LobbyMenu.Home);
            yield return null;
            Assert.IsFalse(menu.Stage.IsHoldingItem, "leaving the page puts the gear down");
        }

        [UnityTest]
        public IEnumerator LeavingDropsATryOnAndKeepsWhatWasSaved()
        {
            page.ShowCategory("Top");
            yield return null;
            string stored = Stored, worn = menu.Outfit.Serialize();
            Click("Colour grape");
            yield return null;
            Assert.AreEqual("grape", page.TryOn.ColourOf("Top"));
            menu.Open(LobbyMenu.Home);
            yield return null;
            Assert.AreEqual(worn, menu.Outfit.Serialize());
            Assert.AreEqual(stored, Stored);
            menu.Open(LobbyMenu.Loadout);
            yield return null;
            Assert.IsNull(page.TryOn, "coming back starts clean");
            Assert.IsFalse(Has(LoadoutPage.TakeOff));
        }

        [UnityTest]
        public IEnumerator TheWardrobeFitsThreeWindowSizes()
        {
            using var window = new Window(menu);
            foreach (var size in Windows)
            {
                window.Set(size.x, size.y);
                yield return Reopen();
                float canvasWidth = size.x / size.y * 1080f;
                string where = " at " + size.x + "x" + size.y;
                foreach (string slot in new[] { "Headwear", "Hair", "Top" })
                {
                    page.ShowCategory(slot);
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    var safe = window.Safe;
                    var panel = page.Root.GetComponentsInChildren<RectTransform>().First(rect => rect.name == "Panel");
                    var panelBounds = OwnBounds(safe, panel);
                    Assert.AreEqual(LoadoutPage.Width, panelBounds.size.x, 1f, "the panel keeps its width" + where);
                    var rail = page.Root.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == LoadoutPage.RailName);
                    var railBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(safe, rail);
                    Assert.AreEqual(LoadoutPage.RailWidth, railBounds.size.x, Slack, "the rail keeps its width" + where);
                    Assert.AreEqual(panelBounds.min.x + LoadoutPage.RailTuck, railBounds.max.x, Slack, "the rail tucks under the panel edge" + where);
                    foreach (var control in page.Root.GetComponentsInChildren<Button>())
                    {
                        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(safe, control.transform);
                        Assert.GreaterOrEqual(bounds.min.x, safe.rect.xMin - Slack, control.name + " is inside the window" + where);
                        Assert.LessOrEqual(bounds.max.x, safe.rect.xMax + Slack, control.name + " is inside the window" + where);
                        if (control.GetComponentInParent<ScrollRect>()) continue;
                        Assert.GreaterOrEqual(bounds.min.y, safe.rect.yMin - Slack, control.name + " is inside the window" + where);
                        Assert.LessOrEqual(bounds.max.y, safe.rect.yMax + Slack, control.name + " is inside the window" + where);
                    }
                    var scroll = page.Root.GetComponentsInChildren<ScrollRect>().Single();
                    var tiles = scroll.content.GetComponentsInChildren<Button>();
                    for (int i = 0; i < tiles.Length; i++)
                    {
                        var bounds = OwnBounds(scroll.viewport, (RectTransform)tiles[i].transform);
                        Assert.AreEqual(LoadoutPage.Tile, bounds.size.x, Slack, tiles[i].name + " keeps the tile size" + where);
                        Assert.AreEqual(bounds.size.x, bounds.size.y, Slack, tiles[i].name + " is square" + where);
                        Assert.GreaterOrEqual(bounds.min.x, scroll.viewport.rect.xMin - Slack, tiles[i].name + " fits the grid" + where);
                        Assert.LessOrEqual(bounds.max.x, scroll.viewport.rect.xMax + Slack, tiles[i].name + " fits the grid" + where);
                        if (i >= LoadoutPage.Columns) continue;
                        Assert.GreaterOrEqual(bounds.min.y, scroll.viewport.rect.yMin - Slack, "the first row shows whole" + where);
                        Assert.LessOrEqual(bounds.max.y, scroll.viewport.rect.yMax + Slack, "the first row shows whole" + where);
                    }
                    var body = RectTransformUtility.CalculateRelativeRectTransformBounds(safe, scroll.viewport.parent);
                    foreach (var swatch in page.Root.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith(LoadoutPage.ColourPrefix)))
                    {
                        var bounds = OwnBounds(safe, (RectTransform)swatch.transform);
                        Assert.AreEqual(LoadoutPage.SwatchSize, bounds.size.x, Slack, swatch.name + " keeps the swatch size" + where);
                        Assert.LessOrEqual(bounds.max.x, panelBounds.max.x - Slack, swatch.name + " stays inside the panel" + where);
                        Assert.GreaterOrEqual(bounds.min.x, body.min.x - Slack, swatch.name + " stays inside the panel" + where);
                    }
                    foreach (var text in page.Root.GetComponentsInChildren<TMP_Text>())
                    {
                        text.ForceMeshUpdate();
                        Assert.IsFalse(text.isTextTruncated, "\"" + text.text + "\" fits" + where);
                        Assert.GreaterOrEqual(text.fontSize, 15f - .01f, "\"" + text.text + "\" is readable" + where);
                    }
                    float avatar = LobbyStage.StandAt((26f + page.PanelWidth) / 1080f, size.x / size.y) * canvasWidth;
                    Assert.Less(avatar, railBounds.min.x - safe.rect.xMin, "the avatar stands left of the rail" + where);
                }
            }
        }
    }
}
