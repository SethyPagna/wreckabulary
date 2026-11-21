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
    public sealed partial class LoadoutPage : LobbyPage
    {
        public const float Width = 700f;
        public const string LockerTab = "LOCKER", RecipesTab = "RECIPES";
        public const string PiecesView = "Wardrobe pieces", LooksView = "Wardrobe looks", OwnedToggle = "Owned only";
        public const string OwnedHint = "Show only what you own", CategoryTitle = "Category title";
        public const string DetailLine = "Wardrobe detail", GetBar = "Get bar", GetBarLine = "Get bar line";
        public const string BuyPrefix = "Buy ", TakeOff = "Take off", SeeInShop = "See in shop";
        public const string SaveLookButton = "Save look", SaveLookHint = "Save what you are wearing", DoneButton = "Done";
        public const string Clash = "That doesn't go with what you're wearing.", NotYours = "That piece isn't yours yet.";
        public const string NotSold = "This isn't in the shop yet.";
        public const string TitleArea = "Title area";
        const string TitleName = "Title";
        const string PieceKind = "piece", ColourKind = "colour", SkinKind = "skin", LookKind = "look";
        const string Separator = " · ";
        const float HeaderHeight = 48f, HeaderGap = 10f, SwitchWidth = 236f, ToggleSize = 48f, HeaderInset = 12f;
        const float FooterBottom = 6f, FooterHeight = 60f, SaveWidth = 220f, FooterGap = 16f;
        const float BarHeight = 56f, BarGap = 8f, DetailHeight = 30f, DetailGap = 6f, StripHeight = 50f, GridGap = 6f;
        const float BarBuyWidth = 112f, BarTakeOffWidth = 104f, BarShopWidth = 48f, BarControlHeight = 40f, BarIcon = 22f;
        const float TitleGap = 14f, TitleCeiling = 2f, TitleFloor = 98f, TitleMinRoom = 160f;
        public const float CaptionText = 20f;
        const float MinText = 15f, DetailText = 17f, TitleText = 16f;
        const int TileRadius = 14;

        public override string Id => LobbyMenu.Loadout;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;
        public override float PanelWidth => PanelSide + RailWidth - RailTuck;
        public bool ShowingRecipes { get; private set; }
        public bool ShowingLooks { get; private set; }
        public bool OwnedOnly { get; private set; }
        public string Category { get; private set; } = RailOrder[0];
        public string Pinned { get; private set; }
        public Outfit TryOn => tryOn;
        public Outfit Shown => tryOn ?? Menu.Outfit;
        public bool Replacing => replacing;
        public SavedLooks Looks => looks ??= store.Load();
        public LobbyLooksStore Store => store;

        readonly LobbyLooksStore store = new LobbyLooksStore();
        readonly List<Selectable> tiles = new List<Selectable>();
        readonly List<List<Selectable>> tileRows = new List<List<Selectable>>();
        readonly List<Selectable> railButtons = new List<Selectable>();
        HashSet<string> fresh = new HashSet<string>(StringComparer.Ordinal);
        RectTransform lockerList, detail;
        TMP_Text recipeFinish, detailLine;
        Selectable viewSwitch, stripFirst, saveLook, selectedRail;
        float scrolled, tileCell;
        int tileBreak;
        string hovering, previewing, shownRecipe;
        bool rebuilding, replacing;
        Outfit tryOn;
        SavedLooks looks;
        LobbyLoadoutLayout layout;
        LobbyLoadoutPageView authoredPage;

        static WardrobeCatalogue Wardrobe => GameConfig.Current.Wardrobe;
        LobbyStage Stage => Menu ? Menu.Stage : null;
        float PanelSide => authoredPage && authoredPage.Panel.rect.width > 0 ? authoredPage.Panel.rect.width : Width;

        public override void Opened()
        {
            ResetScroll();
            tryOn = null;
            replacing = false;
            focusedLook = armedDelete = naming = NoLook;
            looks = store.Load();
            NoteSeen();
        }

        public override void Closed()
        {
            hovering = previewing = null;
            if (rebuilding || !Stage) return;
            Stage.Dress(Menu.Outfit);
            Stage.ClearHeldItem();
        }

        protected override void Build()
        {
            if (lockerList) scrolled = lockerList.anchoredPosition.y;
            lockerList = detail = null;
            recipeFinish = detailLine = null;
            viewSwitch = stripFirst = saveLook = selectedRail = null;
            deleteButton = null;
            nameField = null;
            naming = NoLook;
            hovering = previewing = shownRecipe = null;
            tiles.Clear();
            tileRows.Clear();
            tileBreak = 0;
            railButtons.Clear();
            looks ??= store.Load();
            if (tryOn != null && Menu.Career.Missing(tryOn).Count == 0) tryOn = null;
            authoredPage = null;
            layout = Resources.Load<LobbyLoadoutLayout>(LobbyLoadoutLayout.ResourcePath);
            RectTransform lockerBody, recipesBody;
            if (layout)
            {
                layout.Validate();
                authoredPage = LobbyRuntimeTypography.Instantiate(layout.Page, Root, false);
                authoredPage.Bind(ShowingRecipes, Menu.Close, ShowRecipes, CustomizeRecipe);
                lockerBody = authoredPage.LockerBody;
                recipesBody = authoredPage.RecipesBody;
                ClearTitle(authoredPage.Panel, authoredPage.LockerTab.transform.parent as RectTransform);
            }
            else
            {
                lockerBody = recipesBody = Side("Customize");
                var panel = (RectTransform)lockerBody.parent;
                panel.offsetMin = new Vector2(-Width, 0);
                var tabs = LobbyKit.Segmented(panel, "Tabs", new[] { (LockerTab, "Character"), (RecipesTab, "Equipment") },
                    ShowingRecipes ? RecipesTab : LockerTab, id => ShowRecipes(id == RecipesTab));
                tabs.Place(Vector2.one, Vector2.one, new Vector2(-84 - 240, -74), new Vector2(-84, -26));
                ClearTitle(panel, tabs);
            }
            BuildRail();
            if (ShowingRecipes) Recipes(recipesBody);
            else Character(lockerBody);
            WireNavigation();
            ListenForKeys();
            if (!Stage) return;
            Stage.Dress(Shown);
            if (ShowingRecipes) HoldShown();
            else Stage.ClearHeldItem();
        }

        void ClearTitle(RectTransform panel, RectTransform tabs)
        {
            var title = panel.GetComponentsInChildren<TMP_Text>().FirstOrDefault(label => label.name == TitleName && label.transform.parent == panel);
            if (!title || !tabs) return;
            var frame = title.rectTransform;
            if (frame.anchorMin.x != 0f || frame.anchorMax.x != 1f) return;
            var corners = new Vector3[4];
            tabs.GetWorldCorners(corners);
            float left = frame.offsetMin.x, right = panel.InverseTransformPoint(corners[0]).x - panel.rect.xMin - TitleGap;
            if (right - left < TitleMinRoom) return;
            frame.offsetMax = new Vector2(right - panel.rect.width, frame.offsetMax.y);
            var area = LobbyKit.Rect(panel, TitleArea).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(left, -TitleFloor), new Vector2(right, -TitleCeiling));
            area.SetSiblingIndex(frame.GetSiblingIndex());
            var word = LobbyTileWord.Replace(title, LobbyParcel.TitleSize);
            if (word) word.Within = area;
        }

        void Rebuild()
        {
            rebuilding = true;
            try { Refresh(); }
            finally { rebuilding = false; }
        }

        void ResetScroll()
        {
            scrolled = 0f;
            lockerList = null;
        }

        void Disarm()
        {
            replacing = false;
            armedDelete = NoLook;
        }

        void Select(Selectable target)
        {
            if (EventSystem.current && target) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        public void ShowRecipes(bool on)
        {
            ShowingRecipes = on;
            Disarm();
            Rebuild();
            Reselect(on ? RecipesTab : LockerTab);
        }

        public void ShowLooks(bool on)
        {
            ShowingLooks = on;
            ShowingRecipes = false;
            ResetScroll();
            Disarm();
            Rebuild();
            Reselect(on ? LooksView : PiecesView);
        }

        public void ShowOwnedOnly(bool on)
        {
            OwnedOnly = on;
            ResetScroll();
            Disarm();
            Rebuild();
            Reselect(OwnedToggle);
        }

        void Character(RectTransform body)
        {
            bool bar = tryOn != null;
            bool strip = ShowingLooks ? LooksStripShown() : PieceStripShown();
            float floor = FooterBottom + FooterHeight + (bar ? BarGap + BarHeight : 0f);
            float detailBottom = floor + DetailGap;
            float stripBottom = detailBottom + DetailHeight;
            float gridBottom = stripBottom + (strip ? StripHeight : 0f) + GridGap;

            Header(body);
            var view = LobbyKit.Rect(body, "Wardrobe").Place(Vector2.zero, Vector2.one, new Vector2(0, gridBottom),
                new Vector2(0, -(HeaderHeight + HeaderGap)));
            if (ShowingLooks) LooksGrid(view, body);
            else PieceGrid(view, body);
            if (strip)
            {
                var row = LobbyKit.Rect(body, "Strip").Place(Vector2.zero, new Vector2(1, 0), new Vector2(0, stripBottom),
                    new Vector2(0, stripBottom + StripHeight));
                if (ShowingLooks) LooksStrip(row);
                else PieceStrip(row);
            }
            detailLine = LobbyKit.Text(body, RestingDetail(), CaptionText, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            detailLine.name = DetailLine;
            detailLine.enableAutoSizing = true; detailLine.fontSizeMin = MinText; detailLine.fontSizeMax = CaptionText;
            detailLine.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(4, detailBottom), new Vector2(-4, detailBottom + DetailHeight));
            if (bar) BuildGetBar(body, FooterBottom + FooterHeight + BarGap);
            Footer(body);
        }

        void Header(RectTransform body)
        {
            var header = LobbyKit.Rect(body, "Wardrobe header").Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -HeaderHeight), Vector2.zero);
            string picked = ShowingLooks ? LooksView : PiecesView;
            var views = LobbyKit.Segmented(header, "Wardrobe view", new[] { (PiecesView, "Pieces"), (LooksView, "Looks") }, picked,
                id => ShowLooks(id == LooksView), HeaderHeight);
            views.Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(SwitchWidth, 0));
            foreach (var choice in views.GetComponentsInChildren<Button>())
            {
                RaiseText(choice);
                if (choice.name == picked) viewSwitch = choice;
            }
            if (!ShowingLooks) HeaderTitle(header);
            var owned = IconTile(header, OwnedToggle, LobbyIcons.Filter, OwnedHint, OwnedOnly, () => ShowOwnedOnly(!OwnedOnly), 28f);
            ((RectTransform)owned.transform).Pin(new Vector2(1, .5f), Vector2.zero, new Vector2(ToggleSize, ToggleSize));
        }

        void HeaderTitle(RectTransform header)
        {
            var title = LobbyKit.Caps(header, SlotTitle(Category), TitleText, TextAlignmentOptions.Midline);
            title.name = CategoryTitle;
            title.enableAutoSizing = true; title.fontSizeMin = MinText; title.fontSizeMax = TitleText;
            title.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(SwitchWidth + HeaderInset, 0), new Vector2(-(ToggleSize + HeaderInset), 0));
        }

        void Footer(RectTransform body)
        {
            var save = LobbyKit.TextAction(body, SaveLookButton, "Save look", 20, SaveLook, icon: LobbyIcons.Save);
            ((RectTransform)save.transform).Place(Vector2.zero, Vector2.zero, new Vector2(0, FooterBottom), new Vector2(SaveWidth, FooterBottom + FooterHeight));
            save.gameObject.AddComponent<LobbyHint>().Text = SaveLookHint;
            save.interactable = store.Readable;
            RaiseText(save);
            saveLook = save;
            var done = LobbyKit.Confirm(body, DoneButton, "Done", Menu.Close);
            ((RectTransform)done.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(SaveWidth + FooterGap, FooterBottom),
                new Vector2(0, FooterBottom + FooterHeight));
        }

        static void RaiseText(Selectable control)
        {
            foreach (var label in control.GetComponentsInChildren<TMP_Text>(true))
                if (label.enableAutoSizing && label.fontSizeMin < MinText) label.fontSizeMin = Mathf.Min(MinText, label.fontSizeMax);
        }

        static Button IconTile(Transform parent, string name, string icon, string hint, bool on, Action click, float glyphSize)
        {
            var button = LobbyKit.Button(parent, name, on ? LobbyKit.Selection : LobbyKit.Card, click, TileRadius,
                on ? LobbyKit.Selection : LobbyKit.Line, 2);
            var body = button.Body();
            var glyph = LobbyKit.Icon(body, icon, on ? LobbyKit.OnSelection : LobbyKit.Navy);
            glyph.rectTransform.Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(glyphSize, glyphSize));
            if (on) LobbyKit.Ring(body, LobbyKit.Selection, TileRadius, 2);
            else
            {
                var face = button.FaceOf();
                button.GetComponent<LobbyPress>().Hot = hot => face.color = hot ? LobbyKit.Raised : LobbyKit.Card;
            }
            button.gameObject.AddComponent<LobbyHint>().Text = hint;
            return button;
        }

        void SetDetail(string line)
        {
            if (detailLine) detailLine.text = line ?? "";
        }

        string RestingDetail()
        {
            if (replacing) return ReplacePrompt;
            if (ShowingLooks) return LooksDetail();
            return PieceLine(Category, Dressing.Worn(Wardrobe, Shown, Category), Shown);
        }

        void Hover(string key, Func<Outfit> preview, string line, bool hot)
        {
            if (hot)
            {
                hovering = key;
                var outfit = preview?.Invoke();
                if (outfit != null && Stage) Stage.Dress(outfit);
                SetDetail(line);
            }
            else if (hovering == key)
            {
                hovering = null;
                if (Stage) Stage.Dress(Shown);
                SetDetail(RestingDetail());
            }
        }

        void StartTryOn(Outfit outfit, string reselect)
        {
            tryOn = outfit;
            Rebuild();
            Reselect(reselect);
        }

        void ClearTryOn()
        {
            Disarm();
            tryOn = null;
            Rebuild();
            Select(First);
        }

        void BuildGetBar(RectTransform body, float bottom)
        {
            var offer = Dressing.Needed(Menu.Career, tryOn).FirstOrDefault();
            var bar = LobbyKit.Row(body, GetBar, 8, 8);
            bar.Place(Vector2.zero, new Vector2(1, 0), new Vector2(0, bottom), new Vector2(0, bottom + BarHeight));
            LobbyKit.Face(bar, LobbyKit.Card, TileRadius, LobbyKit.Line, 2);
            var row = bar.GetComponent<HorizontalLayoutGroup>();
            row.childForceExpandHeight = false;
            row.childAlignment = TextAnchor.MiddleLeft;
            LobbyKit.Icon(bar, LobbyIcons.Lock, LobbyKit.Navy).Size(BarIcon, BarIcon);
            var line = LobbyKit.Text(bar, OfferLine(offer), DetailText, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            line.name = GetBarLine;
            line.textWrappingMode = TextWrappingModes.Normal;
            line.enableAutoSizing = true; line.fontSizeMin = MinText; line.fontSizeMax = DetailText;
            line.Size(-1, BarControlHeight, 1);
            line.GetComponent<LayoutElement>().minWidth = 0;
            if (offer != null)
            {
                int price = Menu.Career.PriceOf(offer);
                var buy = LobbyKit.PriceTag(bar, BuyPrefix + offer.Id, price, Menu.Career.Coins < price, () => BuyOffer(offer));
                buy.Size(BarBuyWidth, BarControlHeight);
                buy.gameObject.AddComponent<LobbyHint>().Text = "Buy " + offer.Name;
            }
            var off = LobbyKit.TextAction(bar, TakeOff, "Take off", DetailText, ClearTryOn);
            off.Size(BarTakeOffWidth, BarControlHeight);
            RaiseText(off);
            if (offer == null) return;
            LobbyKit.IconButton(bar, LobbyIcons.Cart, SeeInShop, () => Menu.OpenShop(offer.Id)).Size(BarShopWidth, BarControlHeight);
        }

        static string OfferLine(ShopOffer offer)
        {
            if (offer == null) return NotSold;
            if (offer.Kind == LookKind) return "Comes with " + offer.Name;
            return offer.Kind == ColourKind ? offer.Name + " colour" : offer.Name;
        }

        void BuyOffer(ShopOffer offer)
        {
            Disarm();
            string error = Menu.Career.Buy(offer.Id);
            if (error != null)
            {
                Menu.Post(error);
                return;
            }
            Menu.SaveCareer();
            Menu.Post("Bought " + offer.Name + ".");
            bool complete = tryOn != null && Menu.Career.Missing(tryOn).Count == 0;
            if (complete)
            {
                Menu.Wear(tryOn);
                tryOn = null;
            }
            Rebuild();
            var next = complete ? null : Root.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name.StartsWith(BuyPrefix, StringComparison.Ordinal));
            Select(next ? next : First);
        }

        void NoteSeen()
        {
            var owned = Sold().Where(Owns).ToList();
            var known = store.LoadSeen();
            fresh = known == null ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(owned.Where(id => !known.Contains(id)), StringComparer.Ordinal);
            store.SaveSeen(owned);
        }

        static IEnumerable<string> Sold()
        {
            foreach (var piece in Wardrobe.Pieces)
                if (Dressing.OfferFor(PieceKind + ":" + piece.Id) != null) yield return PieceKind + ":" + piece.Id;
            if (Wardrobe.Palettes.TryGetValue(SoldPalette, out var colours))
                foreach (var colour in colours)
                    if (Dressing.OfferFor(ColourKind + ":" + colour.Id) != null) yield return ColourKind + ":" + colour.Id;
            foreach (var finish in CustomizeFinishes)
                if (Dressing.OfferFor(SkinKind + ":" + finish.Id) != null) yield return SkinKind + ":" + finish.Id;
        }

        bool Owns(string id)
        {
            int split = id.IndexOf(':');
            return split > 0 && Menu.Career.Owns(id.Substring(0, split), id.Substring(split + 1));
        }

        bool IsNew(string kind, string value) => fresh.Contains(kind + ":" + value);
    }
}
