using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class ShopPage : LobbyPage
    {
        public const string Looks = "looks", Gear = "gear", Colours = "colours", Rooms = "rooms", Extras = "extras";
        static readonly (string id, string label)[] Categories =
            { (Looks, "Looks"), (Gear, "Gear"), (Colours, "Colours"), (Rooms, "Rooms"), (Extras, "Extras") };
        static readonly Vector2 Card = new Vector2(248, 270);
        string note, spotlight, recipe = "BAT";
        bool recipePicker;
        TMP_Text footnote, selectedName, selectedState;
        RectTransform selection;
        Button selectedAction;
        readonly List<(ShopOffer offer, string title, bool worn)> visibleOffers = new();
        readonly Dictionary<string, (Image outline, bool worn)> outlines = new Dictionary<string, (Image, bool)>(StringComparer.Ordinal);
        readonly Dictionary<string, LobbyShopOfferView> authoredOffers = new Dictionary<string, LobbyShopOfferView>(StringComparer.Ordinal);
        LobbyShopLayout layout;
        LobbyShopPageView authoredPage;

        public override string Id => LobbyMenu.Shop;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;
        public override float PanelWidth => authoredPage && authoredPage.Panel ? authoredPage.Panel.rect.width : base.PanelWidth;
        public string Spotlit => spotlight;
        public string SelectedRecipe => recipe;
        public string Category { get; private set; } = Looks;

        public override void Opened() { note = null; spotlight = null; }

        public override void Closed()
        {
            LobbyThemes.Restore();
            if (Menu && Menu.Stage)
            {
                Menu.Stage.ClearItemPreview();
                Menu.Stage.Dress(Menu.Outfit);
            }
        }

        public void Spotlight(string offerId)
        {
            spotlight = offerId;
            note = null;
            Category = CategoryFor(offerId);
            Refresh();
            if (First && EventSystem.current) EventSystem.current.SetSelectedGameObject(First.gameObject);
        }

        protected override void Build()
        {
            outlines.Clear();
            visibleOffers.Clear();
            selection = null; selectedAction = null; selectedName = selectedState = null;
            authoredOffers.Clear();
            authoredPage = null;
            layout = Resources.Load<LobbyShopLayout>(LobbyShopLayout.ResourcePath);
            RectTransform list;
            if (layout)
            {
                layout.Validate();
                authoredPage = LobbyRuntimeTypography.Instantiate(layout.Page, Root, false);
                First = authoredPage.Bind(Category, Menu.Close, id => ShowCategory(id), note);
                footnote = authoredPage.Footnote;
                list = authoredPage.Content;
                authoredPage.Offers.gameObject.SetActive(Category != Extras);
            }
            else list = BuildLegacyShell();

            BuildOffers(list);
            BuildSelection(list);
            var selected = visibleOffers.FirstOrDefault(entry => entry.offer.Id == spotlight);
            if (selected.offer == null) selected = visibleOffers.FirstOrDefault(entry => entry.worn);
            if (selected.offer == null) selected = visibleOffers.FirstOrDefault(entry => Menu.Career.Owns(entry.offer.Kind, entry.offer.Value));
            if (selected.offer == null) selected = visibleOffers.FirstOrDefault();
            if (selected.offer != null) Preview(selected.offer);
            else if (footnote) footnote.text = note ?? "Free accessories";
        }

        RectTransform BuildLegacyShell()
        {
            var body = Side("Shop");
            var tabs = LobbyKit.Row(body, "Shop categories", 8);
            tabs.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -48), Vector2.zero);
            foreach (var choice in Categories)
            {
                string id = choice.id;
                var tab = LobbyKit.TextAction(tabs, "Shop category " + id, choice.label, 18,
                    () => ShowCategory(id), Category == id).Size(-1, 48, 1);
                if (Category == id) First = tab;
            }
            footnote = LobbyKit.Text(body, note ?? "Select to preview",
                16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            footnote.enableAutoSizing = true; footnote.fontSizeMin = 13; footnote.fontSizeMax = 16;
            footnote.rectTransform.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 36));
            var view = LobbyKit.Rect(body, "Offers").Place(Vector2.zero, Vector2.one, new Vector2(0, 46), new Vector2(0, -62));
            var list = LobbyKit.Scroll(view, "Scroll", 12);
            list.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 8, 12);
            return list;
        }

        void BuildSelection(RectTransform list)
        {
            if (visibleOffers.Count == 0 || !footnote) return;
            float height = authoredPage ? authoredPage.SelectionHeight : 96f;
            float actionWidth = authoredPage ? authoredPage.SelectionActionWidth : 200f;
            var footer = footnote.rectTransform;
            var scroll = list.GetComponentInParent<ScrollRect>();
            if (!scroll || scroll.transform.parent is not RectTransform viewport)
                throw new InvalidOperationException("Shop offers need a ScrollRect inside their layout container.");
            float bottom = footer.offsetMax.y + 8f;
            selection = LobbyKit.Rect(footer.parent, "Selected shop offer").Place(Vector2.zero, new Vector2(1, 0),
                new Vector2(footer.offsetMin.x, bottom), new Vector2(footer.offsetMax.x, bottom + height));
            viewport.offsetMin = new Vector2(viewport.offsetMin.x, Mathf.Max(viewport.offsetMin.y, bottom + height + 10f));
            selectedName = LobbyKit.Text(selection, "", 25, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            selectedName.name = "Selected shop name";
            selectedName.enableAutoSizing = true; selectedName.fontSizeMin = 18; selectedName.fontSizeMax = 25;
            selectedName.rectTransform.Place(new Vector2(0, .5f), Vector2.one,
                new Vector2(0, 0), new Vector2(-actionWidth - 16f, 0));
            selectedState = LobbyKit.Text(selection, "", 17, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            selectedState.name = "Selected shop state";
            selectedState.enableAutoSizing = true; selectedState.fontSizeMin = 14; selectedState.fontSizeMax = 17;
            selectedState.rectTransform.Place(Vector2.zero, new Vector2(1, .5f),
                Vector2.zero, new Vector2(-actionWidth - 16f, -4));
            selectedAction = LobbyKit.TextAction(selection, "Selected shop action", "", 22, ActivateSelection);
            ((RectTransform)selectedAction.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f),
                new Vector2(-actionWidth, -28), new Vector2(0, 28));
        }

        void PresentSelection(ShopOffer offer)
        {
            if (!selection) return;
            var entry = visibleOffers.FirstOrDefault(value => value.offer.Id == offer.Id);
            if (entry.offer == null) return;
            bool owned = Menu.Career.Owns(offer.Kind, offer.Value);
            string category = Categories.First(value => value.id == Category).label;
            selectedName.text = entry.title;
            selectedState.text = (Category == Gear ? recipe + " finish" : category) + " · " +
                (entry.worn ? "Equipped" : owned ? "Owned · Preview" : "Not owned · Preview");
            var label = selectedAction.GetComponentInChildren<TMP_Text>();
            label.text = entry.worn ? "EQUIPPED" : owned ? "EQUIP" : "BUY " + Menu.Career.PriceOf(offer);
            selectedAction.interactable = !entry.worn;
        }

        void ActivateSelection()
        {
            var entry = visibleOffers.FirstOrDefault(value => value.offer.Id == spotlight);
            if (entry.offer == null || entry.worn) return;
            if (Menu.Career.Owns(entry.offer.Kind, entry.offer.Value)) Wear(entry.offer);
            else Buy(entry.offer);
        }

        void BuildOffers(RectTransform list)
        {
            if (Category == Looks)
            {
                var looks = OfferGrid(list, "Looks");
                foreach (var bundle in CosmeticBundles.All) LookCard(looks, bundle);
            }
            else if (Category == Gear)
            {
                RecipeChooser(list);
                var finishes = OfferGrid(list, "Finishes");
                foreach (var finish in Finishes.ForItem(recipe, GameConfig.Current.Items.Get(recipe).Skins)) FinishCard(finishes, finish);
            }
            else if (Category == Colours)
            {
                var colours = OfferGrid(list, "Colours");
                foreach (var offer in Career.Shop.Where(o => o.Kind == "colour")) ColourCard(colours, offer);
            }
            else if (Category == Rooms)
            {
                var themes = OfferGrid(list, "Themes");
                foreach (var theme in LobbyThemes.All) ThemeCard(themes, theme);
            }
            else
            {
                var accessories = LobbyKit.Grid(list, "Accessories", new Vector2(248, 170), 16);
                var wardrobe = GameConfig.Current.Wardrobe;
                foreach (var piece in wardrobe.Pieces.Where(p => !wardrobe.RequiredSlots.Contains(p.Slot))) Accessory(accessories, piece);
                WinterEmote(list);
            }
        }

        RectTransform OfferGrid(Transform parent, string name)
        {
            if (!authoredPage) return LobbyKit.Grid(parent, name, Card, 16);
            var grid = (RectTransform)authoredPage.Offers.transform;
            grid.name = name;
            grid.SetAsLastSibling();
            return grid;
        }

        static string CategoryFor(string offerId)
        {
            if (offerId == null) return Looks;
            if (offerId.StartsWith("skin:", StringComparison.Ordinal)) return Gear;
            if (offerId.StartsWith("colour:", StringComparison.Ordinal)) return Colours;
            if (offerId.StartsWith("theme:", StringComparison.Ordinal)) return Rooms;
            return Looks;
        }

        public bool ShowCategory(string id)
        {
            if (!Categories.Any(choice => choice.id == id)) return false;
            Category = id;
            spotlight = note = null;
            recipePicker = false;
            Refresh();
            Reselect("Shop category " + id);
            return true;
        }

        void WinterEmote(Transform parent)
        {
            var collection = Art.SeasonalCollection.Winter;
            if (!collection || !collection.emote) return;
            LobbyKit.SectionLabel(parent, "Winter shuffle");
            var row = LobbyKit.Row(parent, "Winter emote", 8);
            row.Size(-1, 58);
            LobbyKit.TextAction(row, "Play winter shuffle", "SHUFFLE", 24, PlayWinterEmote,
                icon: LobbyIcons.Chevron).Size(-1, 52, 1);
            LobbyKit.TextAction(row, "Stop winter shuffle", "STOP", 19, () => Menu.Stage.StopEmote()).Size(100, 52);
        }

        public void PlayWinterEmote()
        {
            var collection = Art.SeasonalCollection.Winter;
            if (!collection || !collection.emote) return;
            Menu.Stage.ClearItemPreview();
            Menu.Stage.PlayEmote(collection.emote);
        }

        RectTransform ArtCard(Transform parent, ShopOffer offer, string title, string description, bool worn, out RectTransform art)
        {
            visibleOffers.Add((offer, title, worn));
            if (authoredPage)
            {
                bool owned = Menu.Career.Owns(offer.Kind, offer.Value);
                var view = LobbyRuntimeTypography.Instantiate(layout.Offer, parent, false);
                view.Bind(offer.Id, title, description, owned, worn, Menu.Career.PriceOf(offer), offer.Id == spotlight,
                    () => Preview(offer), owned ? () => Wear(offer) : () => Buy(offer));
                authoredOffers[offer.Id] = view;
                outlines[offer.Id] = (view.Outline, worn);
                if (!worn && (offer.Id == spotlight || !First)) First = view.Action.Button;
                art = view.Art;
                return view.Face;
            }
            var card = LobbyKit.Button(parent, "Offer " + offer.Id, LobbyKit.Card, () => Preview(offer), 16, LobbyKit.Line, 1, 0);
            var face = card.Body();
            var outline = LobbyKit.Ring(face, offer.Id == spotlight ? LobbyKit.Sun : LobbyKit.Cyan, 16, 3);
            outline.enabled = worn || offer.Id == spotlight;
            outlines[offer.Id] = (outline, worn);
            art = LobbyKit.Rect(face, "Art").Place(new Vector2(0, 1), Vector2.one, new Vector2(10, -166), new Vector2(-10, -10));
            art.Paint(LobbyKit.Raised, 12).raycastTarget = false;
            var name = LobbyKit.Display(face, title, 23, LobbyKit.Navy);
            name.enableAutoSizing = true; name.fontSizeMin = 18; name.fontSizeMax = 23;
            name.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(10, -204), new Vector2(-10, -172));
            card.gameObject.AddComponent<LobbyHint>().Text = description.Replace("\n", " · ");
            ActionFor(face, offer, worn);
            return face;
        }

        void ActionFor(RectTransform face, ShopOffer offer, bool worn)
        {
            bool owned = Menu.Career.Owns(offer.Kind, offer.Value);
            int price = Menu.Career.PriceOf(offer);
            var action = LobbyKit.TextAction(face, (worn ? "Wearing " : owned ? "Wear " : "Buy ") + offer.Id,
                worn ? "EQUIPPED" : owned ? "EQUIP" : price == 0 ? "FREE" : price.ToString(), 21,
                worn ? null : owned ? () => Wear(offer) : () => Buy(offer), worn, worn ? LobbyIcons.Check : owned ? null : LobbyIcons.Coin);
            action.interactable = !worn;
            ((RectTransform)action.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(16, 10), new Vector2(-16, 54));
            if (!worn && (offer.Id == spotlight || !First)) First = action;
        }

        void ThemeCard(Transform parent, LobbyTheme theme)
        {
            var offer = Career.Shop.FirstOrDefault(o => o.Id == "theme:" + theme.Id) ?? new ShopOffer("theme", theme.Id, theme.Name, 0);
            bool worn = PlayerPrefs.GetString("wv.theme", "sunroom") == theme.Id;
            ArtCard(parent, offer, theme.Name, "Re-dresses the lobby room", worn, out var art);
            LobbyRoom.Swatch(art, theme.Id).Fill();
        }

        void LookCard(Transform parent, CosmeticBundle bundle)
        {
            var offer = Career.Shop.FirstOrDefault(o => o.Id == "look:" + bundle.Id) ?? new ShopOffer("look", bundle.Id, bundle.Name, 0);
            var proposed = bundle.Apply(Menu.Outfit, GameConfig.Current.Wardrobe);
            bool worn = proposed != null && proposed.Serialize() == Menu.Outfit.Serialize();
            ArtCard(parent, offer, bundle.Name, (string.IsNullOrEmpty(bundle.Tagline) ? "" : bundle.Tagline + "\n") + bundle.Description, worn, out var art);
            var portrait = Resources.Load<Texture2D>("UI/Shop/Looks/" + bundle.Id);
            if (portrait)
            {
                var image = LobbyKit.Rect(art, "Outfit portrait").Place(Vector2.zero, Vector2.one, new Vector2(0, TagRoom), Vector2.zero)
                    .gameObject.AddComponent<RawImage>();
                image.texture = portrait;
                image.raycastTarget = false;
                var fit = image.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = (float)portrait.width / portrait.height;
            }
            else
            {
                var colour = GameConfig.Current.Wardrobe.Colour("Top", bundle.TopColour);
                Blob(art, new Color(colour.R, colour.G, colour.B), 94);
                var icon = LobbyKit.Icon(art, LobbyIcons.Satchel, LobbyKit.Navy);
                icon.rectTransform.Pin(new Vector2(.75f, .35f), Vector2.zero, new Vector2(48, 48));
            }
            Emblem(art, bundle.Emblem, new Vector2(0, 1), new Vector2(8, -8), 40);
            NameTag(art, bundle.Collection);
        }

        const float TagRoom = 34f, TaglineRoom = 46f;

        void FinishCard(Transform parent, Finish finish)
        {
            var offer = Career.Shop.FirstOrDefault(o => o.Kind == "skin" && o.Value == finish.Id) ?? new ShopOffer("skin", finish.Id, finish.Name, 0);
            bool worn = (Menu.Outfit.SkinFor(recipe) ?? Skin.Standard) == finish.Id;
            string line = string.IsNullOrEmpty(finish.Tagline) ? finish.Name : finish.Tagline;
            ArtCard(parent, offer, finish.Name, line + "\nOriginal " + recipe + " shape · Select to preview " + finish.Name + " finish", worn, out var art);
            var frame = LobbyKit.Rect(art, "Item frame").Place(Vector2.zero, Vector2.one, new Vector2(6, TaglineRoom), new Vector2(-6, -6));
            var item = FinishedItem(frame, recipe, finish.Id, 144);
            if (item)
            {
                var fit = item.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = item.texture.width * item.uvRect.width / (item.texture.height * item.uvRect.height);
            }
            var (from, to) = FinishPaint(finish.Id);
            var swatch = LobbyKit.Rect(art, "Finish swatch").Pin(Vector2.one, new Vector2(-10, -10), new Vector2(22, 22));
            LobbyKit.Face(swatch, from, 11, LobbyKit.Line, 1, 0, to);
            Emblem(art, finish.Emblem, new Vector2(0, 1), new Vector2(8, -8), 38);
            NameTag(art, line, TaglineRoom - 10);
        }

        void ColourCard(Transform parent, ShopOffer offer)
        {
            ArtCard(parent, offer, offer.Name, (Menu.Outfit.PieceIn("Top") ?? "Top") + " colour",
                Menu.Outfit.ColourOf("Top") == offer.Value, out var art);
            var colour = GameConfig.Current.Wardrobe.Colour("Top", offer.Value);
            Blob(art, new Color(colour.R, colour.G, colour.B), 100);
        }

        static string AccessoryIcon(WardrobePiece piece) => piece.Slot switch
        {
            "Face" => LobbyIcons.Glasses,
            "Back" => LobbyIcons.Satchel,
            "Badge" => LobbyIcons.Badge,
            _ => LobbyIcons.Locker,
        };

        void Accessory(Transform parent, WardrobePiece piece)
        {
            string id = piece.Id;
            bool worn = Menu.Outfit.PieceIn(piece.Slot) == id;
            var look = CosmeticBundles.GrantedBy(id);
            bool owned = Menu.Career.Owns("piece", id);
            var face = LobbyKit.Rect(parent, "Accessory " + id);
            face.Paint(LobbyKit.Card, 14).raycastTarget = false;
            var emblem = look != null && look.Signature == id ? Emblem(face, look.Emblem, new Vector2(.5f, 1), new Vector2(0, -12), 58) : null;
            if (!emblem)
                LobbyKit.Icon(face, AccessoryIcon(piece), LobbyKit.Cyan).rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(54, 54));
            var name = LobbyKit.Text(face, piece.DisplayName, 21, LobbyKit.Navy, TextAlignmentOptions.Center);
            name.name = CardName;
            name.enableAutoSizing = true; name.fontSizeMin = 15; name.fontSizeMax = 21;
            name.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(8, -106), new Vector2(-8, -74));
            Button action;
            if (!owned && look != null)
            {
                action = LobbyKit.TextAction(face, "Accessory look " + id, look.Name, 18, () => Spotlight("look:" + look.Id), icon: LobbyIcons.Lock);
                action.gameObject.AddComponent<LobbyHint>().Text = piece.DisplayName + " comes with " + look.Name;
            }
            else action = LobbyKit.TextAction(face, "Accessory toggle " + id, worn ? "REMOVE" : "EQUIP", 19, () => EquipAccessory(id), worn);
            ((RectTransform)action.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(12, 10), new Vector2(-12, 54));
        }

        void RecipeChooser(Transform parent)
        {
            var row = LobbyKit.Row(parent, "Recipe selector", 8);
            row.Size(-1, 54);
            LobbyKit.TextAction(row, "Previous recipe", "‹", 30, () => StepRecipe(-1)).Size(60, 48);
            var choose = LobbyKit.TextAction(row, "Choose recipe", recipe, 24, () => { recipePicker = !recipePicker; Refresh(); Reselect("Choose recipe"); }, icon: LobbyIcons.Chevron).Size(-1, 48, 1);
            choose.Body().Find("Content/Icon " + LobbyIcons.Chevron).localRotation = Quaternion.Euler(0, 0, -90);
            LobbyKit.TextAction(row, "Next recipe", "›", 30, () => StepRecipe(1)).Size(60, 48);
            if (!recipePicker) return;
            var grid = LobbyKit.Grid(parent, "Recipes", new Vector2(180, 44), 8);
            foreach (var item in GameConfig.Current.Items.Enabled)
            {
                string word = item.Id;
                LobbyKit.TextAction(grid, "Recipe " + word, word, 18, () => SelectRecipe(word), word == recipe);
            }
        }

        public bool SelectRecipe(string word)
        {
            if (!GameConfig.Current.Items.TryGet(word, out var item) || !item.Enabled) return false;
            Category = Gear;
            spotlight = note = null;
            recipe = item.Id; recipePicker = false;
            Refresh();
            Reselect("Choose recipe");
            return true;
        }

        void StepRecipe(int direction)
        {
            var items = GameConfig.Current.Items.Enabled.ToArray();
            int index = Array.FindIndex(items, item => item.Id == recipe);
            SelectRecipe(items[(index + direction + items.Length) % items.Length].Id);
        }

        public void Preview(ShopOffer offer)
        {
            if (offer == null) return;
            if (spotlight != offer.Id) note = null;
            if (offer.Kind == "theme") LobbyThemes.Preview(offer.Value);
            else if (offer.Kind == "skin") Menu.Stage.PreviewItem(recipe, offer.Value);
            else
            {
                var next = offer.Kind == "look" ? CosmeticBundles.Find(offer.Value)?.Apply(Menu.Outfit, GameConfig.Current.Wardrobe) : Menu.Outfit.Clone();
                if (offer.Kind == "colour") next.Colours["Top"] = offer.Value;
                if (next == null || !GameConfig.Current.Wardrobe.Fits(next)) return;
                Menu.Stage.ClearItemPreview(); Menu.Stage.Dress(next);
            }
            foreach (var entry in outlines)
            {
                if (!entry.Value.outline) continue;
                if (authoredOffers.TryGetValue(entry.Key, out var authored) && authored)
                { authored.SetPreview(entry.Key == offer.Id); continue; }
                entry.Value.outline.enabled = entry.Value.worn || entry.Key == offer.Id;
                entry.Value.outline.color = LobbyKit.Selection;
            }
            spotlight = offer.Id;
            PresentSelection(offer);
            if (footnote) footnote.text = note ?? "";
        }

        public void Buy(ShopOffer offer)
        {
            if (offer == null) return;
            spotlight = offer.Id;
            string error = Menu.Career.Buy(offer.Id);
            note = error ?? "Bought " + offer.Name + ".";
            if (error == null) { Menu.SaveCareer(); Menu.Post(note); }
            Refresh();
            Reselect((error == null ? "Wear " : "Buy ") + offer.Id);
        }

        public void Wear(ShopOffer offer)
        {
            if (offer == null || !Menu.Career.Owns(offer.Kind, offer.Value)) return;
            if (offer.Kind == "theme")
            {
                if (!LobbyThemes.Equip(offer.Value, Menu.Career)) return;
            }
            else
            {
                Outfit next;
                if (offer.Kind == "skin") next = CosmeticBundles.WithItemSkin(Menu.Outfit, recipe, offer.Value, Menu.Career, GameConfig.Current.Items);
                else if (offer.Kind == "look") next = CosmeticBundles.Find(offer.Value)?.Apply(Menu.Outfit, GameConfig.Current.Wardrobe);
                else if (offer.Kind == "colour") { next = Menu.Outfit.Clone(); next.Colours["Top"] = offer.Value; }
                else return;
                if (next == null || !GameConfig.Current.Wardrobe.Fits(next)) return;
                Menu.Wear(next);
            }
            spotlight = offer.Id;
            note = "Equipped " + offer.Name + ".";
            Refresh();
            Reselect("Offer " + offer.Id);
        }

        public void EquipAccessory(string id)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var piece = wardrobe.Piece(id);
            if (piece == null || !Menu.Career.Owns("piece", id) || wardrobe.RequiredSlots.Contains(piece.Slot)) return;
            bool remove = Menu.Outfit.PieceIn(piece.Slot) == id;
            var next = Menu.Outfit.Clone();
            if (!remove)
                foreach (string requirement in piece.Requires)
                {
                    var required = wardrobe.Piece(requirement);
                    next = wardrobe.Wear(next, required.Slot, requirement) ?? next;
                }
            next = wardrobe.Wear(next, piece.Slot, remove ? null : id);
            if (next == null) return;
            Menu.Wear(next); Refresh(); Reselect("Accessory toggle " + id);
        }
    }
}
