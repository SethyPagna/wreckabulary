using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public sealed class SkinPresentationTests
    {
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, LobbyThemes.PreferenceKey };
        string[] saved;
        string homes, folder;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            folder = HomeStorage.DefaultFolder;
            homes = Path.Combine(Path.GetTempPath(), "wreckabulary-skin-tests-" + System.Guid.NewGuid().ToString("N"));
            HomeStorage.DefaultFolder = homes;
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetString(MatchTally.CareerKey, new Career { Coins = 1000 }.Serialize());
            LobbyThemes.Restore();
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && !GameHud.Active.StateController.IsTransitioning, 2f, "lobby ready");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (CreativeWorkshop.Instance) CreativeWorkshop.Instance.Close();
            yield return TestScenes.Reset();
            HomeStorage.DefaultFolder = folder;
            if (Directory.Exists(homes)) Directory.Delete(homes, true);
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            PlayerPrefs.Save();
            LobbyThemes.Restore();
        }

        static Button Find(Component root, string name)
        {
            var button = root.GetComponentsInChildren<Button>().FirstOrDefault(candidate => candidate.name == name);
            Assert.IsNotNull(button, name);
            return button;
        }

        static TMP_Text Line(Component card, string name) =>
            card.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.name == name);

        static void Visible(TMP_Text text, string what)
        {
            Assert.IsNotNull(text, what);
            Assert.IsTrue(text.isActiveAndEnabled && text.text.Length > 0, what + " shows on the card");
            text.ForceMeshUpdate();
            Assert.IsFalse(text.isTextTruncated, what + " fits: " + text.text);
        }

        [UnityTest]
        public IEnumerator EveryShopCardCarriesItsNameOnTheCard()
        {
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Shop);
            var shop = menu.Page<ShopPage>();
            foreach (string category in new[] { ShopPage.Looks, ShopPage.Gear, ShopPage.Colours, ShopPage.Rooms })
            {
                Assert.IsTrue(shop.ShowCategory(category));
                yield return null;
                Canvas.ForceUpdateCanvases();
                var cards = shop.Root.GetComponentsInChildren<LobbyShopOfferView>();
                Assert.Greater(cards.Length, 0, category);
                foreach (var card in cards)
                {
                    string id = card.name.Substring("Offer ".Length);
                    Visible(card.Title, card.name + " name");
                    if (category == ShopPage.Looks)
                    {
                        var look = CosmeticBundles.Find(id.Substring("look:".Length));
                        Assert.AreEqual(look.Name, card.Title.text);
                        Visible(Line(card, LobbyPage.TagLine), card.name + " collection");
                        Assert.AreEqual(look.Collection, Line(card, LobbyPage.TagLine).text);
                        Assert.AreEqual(Resources.Load<Texture2D>(look.Emblem) != null, card.Art.Find("Emblem") != null, card.name + " emblem");
                    }
                    else if (category == ShopPage.Gear)
                    {
                        var finish = Finishes.Find(id.Substring("skin:".Length));
                        Assert.AreEqual(finish.Name, card.Title.text);
                        Visible(Line(card, LobbyPage.TagLine), card.name + " tagline");
                        Assert.AreEqual(finish.Tagline, Line(card, LobbyPage.TagLine).text);
                        Assert.AreEqual(Resources.Load<Texture2D>(finish.Emblem) != null, card.Art.Find("Emblem") != null, card.name + " emblem");
                    }
                    else if (category == ShopPage.Rooms)
                        Assert.AreEqual(LobbyThemes.All.Single(theme => "theme:" + theme.Id == id).Name, card.Title.text);
                    else Assert.AreEqual(Career.Shop.Single(offer => offer.Id == id).Name, card.Title.text);
                }
            }
            Assert.IsTrue(shop.ShowCategory(ShopPage.Extras));
            yield return null;
            Canvas.ForceUpdateCanvases();
            var wardrobe = GameConfig.Current.Wardrobe;
            var pieces = wardrobe.Pieces.Where(piece => !wardrobe.RequiredSlots.Contains(piece.Slot)).ToArray();
            foreach (var piece in pieces)
            {
                var card = shop.Root.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Accessory " + piece.Id);
                Visible(Line(card, LobbyPage.CardName), card.name + " name");
                Assert.AreEqual(piece.DisplayName, Line(card, LobbyPage.CardName).text);
            }
        }

        [UnityTest]
        public IEnumerator CustomizeFinishCardsUseRegistryNamesAndColours()
        {
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Loadout);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var page = menu.Page<LoadoutPage>();
            var cards = page.Root.GetComponentsInChildren<Button>().Where(button => button.name.StartsWith("Finish ")).ToArray();
            CollectionAssert.AreEqual(LobbyPage.CustomizeFinishes.Select(finish => "Finish " + finish.Id).ToArray(), cards.Select(card => card.name).ToArray());
            foreach (var finish in LobbyPage.CustomizeFinishes)
            {
                var card = cards.Single(button => button.name == "Finish " + finish.Id);
                Visible(Line(card, LobbyPage.CardName), card.name + " name");
                Assert.AreEqual(finish.Name, Line(card, LobbyPage.CardName).text);
                var disc = card.GetComponentsInChildren<RectTransform>().Single(rect => rect.name == "Finish");
                var gradient = disc.GetComponent<LobbyGradient>();
                Assert.AreEqual((Color)LobbyKit.Hex(finish.Swatch), gradient.From, card.name + " disc top");
                Assert.AreEqual((Color)LobbyKit.Hex(finish.Accent), gradient.To, card.name + " disc bottom");
                var art = disc.GetComponentsInChildren<RawImage>().First(image => image.name.StartsWith("Art "));
                string word = art.name.Substring("Art ".Length);
                Assert.IsTrue(finish.Id == Skin.Standard || GameConfig.Current.Items.Get(word).HasSkin(finish.Id), card.name + " shows an item that has the finish");
                var icon = Wreckabulary.UI.ItemArt.FinishIcon(word, finish.Id);
                if (icon) Assert.AreSame(icon, art.texture, card.name + " shows the item in its finish");
            }
            var colours = page.Root.GetComponentsInChildren<RectTransform>().Where(rect => rect.name.StartsWith("Swatch ")).ToArray();
            Assert.Greater(colours.Length, 0);
            foreach (var swatch in colours) Visible(Line(swatch, LobbyPage.CardName), swatch.name + " colour name");
        }

        [UnityTest]
        public IEnumerator GearCardsShowTheItemInEachFinish()
        {
            var menu = LobbyMenu.Instance;
            menu.Open(LobbyMenu.Shop);
            var shop = menu.Page<ShopPage>();
            Assert.IsTrue(shop.SelectRecipe("SHIELD"));
            yield return null;
            var cards = shop.Root.GetComponentsInChildren<LobbyShopOfferView>();
            CollectionAssert.AreEqual(Finishes.ForItem("SHIELD", GameConfig.Current.Items.Get("SHIELD").Skins).Select(finish => "Offer skin:" + finish.Id).ToArray(),
                cards.Select(card => card.name).ToArray());
            foreach (var card in cards)
            {
                string skin = card.name.Substring("Offer skin:".Length);
                var art = card.Art.GetComponentsInChildren<RawImage>().Single(image => image.name == "Art SHIELD");
                var expected = Wreckabulary.UI.ItemArt.FinishIcon("SHIELD", skin) ?? Wreckabulary.UI.OriginalItemArt.Find("SHIELD");
                Assert.AreSame(expected, art.texture, card.name);
                var (from, to) = LobbyPage.FinishPaint(skin);
                var swatch = card.Art.Find("Finish swatch").GetComponent<LobbyGradient>();
                Assert.AreEqual(from, swatch.From);
                Assert.AreEqual(to, swatch.To);
            }
        }

        [Test]
        public void NextFinishOffersOnlyOwnedFinishesTheObjectLists()
        {
            var table = GameConfig.Current.Items.Get("TABLE");
            var career = new Career();
            Assert.AreEqual(Skin.Standard, CreativeWorkshop.NextFinish(Skin.Standard, table, career), "only Classic is free for a table");
            career.Owned.Add("skin:Arcade");
            Assert.AreEqual("Arcade", CreativeWorkshop.NextFinish(Skin.Standard, table, career));
            Assert.AreEqual(Skin.Standard, CreativeWorkshop.NextFinish("Arcade", table, career));
            Assert.AreEqual("Arcade", CreativeWorkshop.NextFinish("Candy", table, career), "an unowned finish moves on to the next owned one");
            Assert.IsFalse(CreativeWorkshop.Fits(table, Finishes.Find("Winter")), "Winter is only for SHIELD and SODA");
        }

        [UnityTest]
        public IEnumerator TheHomeBuilderLocksPaidFinishesYouDoNotOwn()
        {
            var menu = LobbyMenu.Instance;
            Find(menu, "PLAY").onClick.Invoke();
            yield return null;
            Find(menu, "Queue " + LobbyMenu.Workshop).onClick.Invoke();
            yield return null;
            Find(menu, "Create house").onClick.Invoke();
            yield return null;
            var config = GameConfig.Current;
            var designer = new HomeDesigner(config.Houses, config.Items);
            var furnished = designer.AddWords(designer.CreateLayout(menu.Map, "Skin test"), "TABLE PLANT", config.HouseFor(menu.Map).Rooms[0].Name);
            Assert.IsTrue(furnished.Ok, string.Join("; ", furnished.Errors));
            var layout = furnished.Layout;
            layout.Props.Single(prop => prop.Word == "TABLE").Skin = "Arcade";
            Assert.IsTrue(HomeStorage.ForGame().TrySave(layout, out string error), error);
            Find(menu, "GO").onClick.Invoke();
            yield return TestScenes.WaitUntil(() => CreativeWorkshop.Instance, 3f, "the Home builder to open");
            yield return null;
            var workshop = CreativeWorkshop.Instance;
            TMP_Text Selection() => workshop.GetComponentsInChildren<TMP_Text>(true).First(text => text.text.StartsWith("TABLE  /  ") || text.text == "Tap a prop to select it" || text.text.StartsWith("PLANT  /  "));
            for (int i = 0; i < 4 && !Selection().text.StartsWith("TABLE  /  "); i++) Find(workshop, "Select next").onClick.Invoke();
            StringAssert.StartsWith("TABLE  /  Arcade", Selection().text, "a saved home keeps the finish it already uses");

            var arcade = Find(workshop, "Finish Arcade");
            Assert.IsTrue(arcade.interactable, "a table can wear Arcade");
            Assert.IsTrue(arcade.GetComponentsInChildren<Image>(true).Single(image => image.name == "Locked").enabled, "Arcade shows locked");
            Assert.IsTrue(workshop.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == CreativeWorkshop.LockedNote), "with a line pointing to the Shop");
            Assert.IsFalse(Find(workshop, "Finish Classic").GetComponentsInChildren<Image>(true).Single(image => image.name == "Locked").enabled);
            Assert.IsFalse(Find(workshop, "Finish Winter").interactable, "Winter is not offered for a table");
            arcade.onClick.Invoke();
            StringAssert.StartsWith("TABLE  /  Arcade", Selection().text, "picking a locked finish changes nothing");
            Assert.IsTrue(workshop.GetComponentsInChildren<TMP_Text>(true).Any(text => text.text == "Arcade is locked. Get it in the Shop."));

            Find(workshop, "Change finish").onClick.Invoke();
            StringAssert.StartsWith("TABLE  /  Classic", Selection().text, "Change finish moves to a finish you own");
            Find(workshop, "Change finish").onClick.Invoke();
            StringAssert.StartsWith("TABLE  /  Classic", Selection().text, "and never on to an unowned paid finish");
            Find(workshop, "Finish Candy").onClick.Invoke();
            StringAssert.StartsWith("TABLE  /  Classic", Selection().text);

            menu.Career.Owned.Add("skin:Candy");
            Find(workshop, "Change finish").onClick.Invoke();
            StringAssert.StartsWith("TABLE  /  Candy", Selection().text, "an owned paid finish can be used");
            Assert.AreEqual(1000, menu.Career.Coins);
        }
    }
}
