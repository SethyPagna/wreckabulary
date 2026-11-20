using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public abstract class LobbyPage
    {
        public const float SideWidth = 860f;
        public const string Tag = "Name tag", TagLine = "Tag line", CardName = "Card name";

        public abstract string Id { get; }
        public virtual LobbyStage.Focus Focus => LobbyStage.Focus.Centre;
        public virtual bool ShowFeed => false;
        public virtual bool Modal => false;
        public virtual float PanelWidth => SideWidth;
        public RectTransform Root { get; private set; }
        public Selectable First { get; protected set; }
        protected LobbyMenu Menu { get; private set; }

        public void Create(LobbyMenu menu, RectTransform host)
        {
            Menu = menu;
            Root = LobbyKit.Rect(host, Id + " page").Fill();
            Root.gameObject.SetActive(false);
        }

        public void Pop()
        {
            if (Id == LobbyMenu.Home) return;
            LobbyPop.On(Root).Play(Focus == LobbyStage.Focus.Left ? new Vector2(24, 0) : new Vector2(0, -16));
        }

        public virtual void Opened() { }
        public virtual void Closed() { }

        public void Refresh()
        {
            Closed();
            LobbyKit.Clear(Root);
            First = null;
            if (Modal)
            {
                var shade = LobbyKit.Rect(Root, "Shade").Fill();
                shade.Paint(LobbyKit.Shade);
                shade.gameObject.AddComponent<LobbyShade>().Clicked = Menu.Close;
            }
            Build();
            LobbyParcel.DressPage(Root);
        }

        protected abstract void Build();

        protected void Reselect(string name)
        {
            if (!EventSystem.current) return;
            var target = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == name);
            if (target) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        protected RectTransform Panel(Vector2 min, Vector2 max, string title, string subtitle = null)
        {
            var panel = LobbyKit.Rect(Root, "Panel").Place(min, max);
            LobbyKit.PanelFace(panel, 28, 5, 8).raycastTarget = true;
            var head = LobbyKit.Display(panel, LobbyKit.Upper(title), 43, LobbyKit.Sun, TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Drop);
            head.name = "Title";
            head.characterSpacing = 2;
            head.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(34, -84), new Vector2(-88, -20));
            var close = LobbyKit.IconButton(panel, LobbyIcons.Close, "Close page", Menu.Close);
            ((RectTransform)close.transform).Pin(Vector2.one, new Vector2(-20, -20), new Vector2(48, 48));
            float top = 100;
            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = LobbyKit.Text(panel, subtitle, 17, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
                sub.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(34, -120), new Vector2(-88, -90));
                top = 130;
            }
            return LobbyKit.Rect(panel, "Body").Place(Vector2.zero, Vector2.one, new Vector2(32, 28), new Vector2(-32, -top));
        }

        protected RectTransform Side(string title, string subtitle = null)
        {
            var body = Panel(new Vector2(1, 0), Vector2.one, title, subtitle);
            ((RectTransform)body.parent).offsetMin = new Vector2(-PanelWidth, 0);
            return body;
        }

        protected static TextMeshProUGUI Wrapped(Transform parent, string text, float size, Color colour, float height = -1)
        {
            var label = LobbyKit.Text(parent, text, size, colour, TextAlignmentOptions.TopLeft);
            label.textWrappingMode = TextWrappingModes.Normal;
            if (height > 0) label.Size(-1, height);
            return label;
        }

        protected static RectTransform Grow(RectTransform rect)
        {
            if (!rect.TryGetComponent(out LayoutElement element)) element = rect.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1;
            return rect;
        }

        protected static Button Poster(Transform parent, string name, Color colour, bool on, Action click, Vector2 size)
        {
            var button = LobbyKit.Button(parent, name, LobbyKit.Card, click, 12, LobbyKit.Line, 1);
            var press = button.GetComponent<LobbyPress>();
            press.Lift = 0f; press.Tilt = 0f;
            var body = button.Body();
            if (on) Chosen(body, 12);
            return button;
        }

        protected static void Chosen(RectTransform body, int radius)
        {
            LobbyKit.Ring(body, LobbyKit.Selection, radius, 2);
            var check = LobbyKit.Rect(body, "Check").Pin(Vector2.one, new Vector2(12, 12), new Vector2(38, 38));
            LobbyKit.Face(check, LobbyKit.Selection, 19, LobbyKit.Line, 1);
            LobbyKit.Icon(check, LobbyIcons.Check, LobbyKit.OnSelection).rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
        }

        protected static string SkinOf(Outfit outfit)
        {
            var styles = GameConfig.Current.Items.Enabled.Where(item => item.Skins.Count > 1).Select(item => outfit.SkinFor(item.Id) ?? Skin.Standard)
                .Distinct().Take(2).ToArray();
            return styles.Length > 1 ? null : styles.FirstOrDefault() ?? Skin.Standard;
        }

        public static Outfit WithFinish(Outfit outfit, string skin)
        {
            var next = outfit.Clone();
            foreach (var item in GameConfig.Current.Items.Enabled)
                if (item.HasSkin(skin)) next.ItemSkins[item.Id] = skin;
            return next;
        }

        public static IEnumerable<Finish> CustomizeFinishes => Rules.Finishes.All.Where(finish => finish.InCustomize);

        public static string FinishName(string skin) => Rules.Finishes.Find(skin)?.Name ?? skin;

        public static (Color from, Color to) FinishPaint(string skin) =>
            Rules.Finishes.Find(skin) is Finish finish ? (LobbyKit.Hex(finish.Swatch), LobbyKit.Hex(finish.Accent)) : (LobbyKit.WoodHi, LobbyKit.WoodLo);

        public static string ShowcaseItem(string skin)
        {
            var items = GameConfig.Current.Items;
            bool Shows(ItemDefinition item) => skin == Skin.Standard || item.HasSkin(skin);
            if (items.TryGet("BAT", out var bat) && bat.Enabled && Shows(bat)) return bat.Id;
            return items.Enabled.FirstOrDefault(Shows)?.Id ?? "BAT";
        }

        protected static RawImage FinishedItem(Transform parent, string id, string skin, float size, float degrees = 0)
        {
            if (!Wreckabulary.UI.ItemArt.TryGet(id, skin, out var texture, out var uv)) return null;
            var rect = LobbyKit.Rect(parent, "Art " + id).Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
            rect.localRotation = Quaternion.Euler(0, 0, degrees);
            var image = rect.gameObject.AddComponent<RawImage>();
            image.texture = texture; image.uvRect = uv; image.raycastTarget = false;
            return image;
        }

        protected static RawImage Emblem(Transform parent, string path, Vector2 anchor, Vector2 position, float size)
        {
            var picture = string.IsNullOrEmpty(path) ? null : Resources.Load<Texture2D>(path);
            if (!picture) return null;
            var image = LobbyKit.Rect(parent, "Emblem").Pin(anchor, position, new Vector2(size, size)).gameObject.AddComponent<RawImage>();
            image.texture = picture; image.raycastTarget = false;
            return image;
        }

        protected static RectTransform NameTag(RectTransform art, string line, float height = 28)
        {
            var tag = LobbyKit.Rect(art, Tag).Place(Vector2.zero, new Vector2(1, 0), new Vector2(6, 6), new Vector2(-6, 6 + height));
            var fill = LobbyKit.Panel; fill.a = .88f;
            tag.Paint(fill, 9).raycastTarget = false;
            var text = LobbyKit.Text(tag, line, 15, LobbyKit.Cream, TextAlignmentOptions.Center);
            text.name = TagLine;
            text.textWrappingMode = height > 30 ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            text.enableAutoSizing = true; text.fontSizeMin = 11; text.fontSizeMax = 15;
            text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 2), new Vector2(-8, -2));
            return tag;
        }

        protected static RectTransform FinishDisc(Transform parent, string skin, float size)
        {
            var (from, to) = FinishPaint(skin);
            var disc = LobbyKit.Rect(parent, "Finish").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
            LobbyKit.Face(disc, from, Mathf.RoundToInt(size / 2f), LobbyKit.Navy, 3, 0, to);
            FinishedItem(disc, ShowcaseItem(skin), skin, size * .95f, 20f);
            Emblem(disc, Rules.Finishes.Find(skin)?.Emblem, new Vector2(1, 0), new Vector2(size * .12f, -size * .12f), size * .42f);
            return disc;
        }

        protected static RectTransform Blob(Transform parent, Color colour, float size)
        {
            var blob = LobbyKit.Rect(parent, "Swatch").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
            LobbyKit.Face(blob, Color.Lerp(colour, Color.white, .2f), Mathf.RoundToInt(size / 2f), LobbyKit.Navy, 3, 0,
                Color.Lerp(colour, Color.black, .18f));
            var shine = LobbyKit.Rect(blob, "Shine").Pin(new Vector2(.32f, .72f), Vector2.zero, new Vector2(size * .22f, size * .16f));
            shine.Paint(new Color(1f, 1f, 1f, .7f), Mathf.Max(1, Mathf.RoundToInt(size * .08f))).raycastTarget = false;
            return blob;
        }

        protected static void Burst(RectTransform rect, int width, int height, int radius)
        {
            rect.Paint(LobbyKit.Card, radius).raycastTarget = false;
        }

        protected static string Capital(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        protected static string MapName(string id) =>
            id == Session.LobbyRandomMap ? "Random house"
            : id != null && GameConfig.Current.Houses.TryGetValue(id, out var house) ? house.Name : id ?? "";

        protected static string Number(int value) => value.ToString("N0", CultureInfo.InvariantCulture);

        protected static string When(long endedAt) => endedAt <= 0 || endedAt > Career.LatestTime ? "-" :
            DateTimeOffset.FromUnixTimeSeconds(endedAt).ToLocalTime().ToString("d MMM HH:mm", CultureInfo.InvariantCulture);
    }

    public sealed class HomePage : LobbyPage
    {
        public override string Id => LobbyMenu.Home;
        public override bool ShowFeed => true;
        public const float DockWidth = 300f;

        protected override void Build()
        {
            var layout = Resources.Load<LobbyHomeLayout>(LobbyHomeLayout.ResourcePath);
            LobbyHomePageView view;
            if (layout)
            {
                layout.Validate();
                view = LobbyRuntimeTypography.Instantiate(layout.Page, Root, false);
            }
            else view = LobbyHomeDefaultLayout.Create(Root);
            First = view.Bind(!PlayerPrefs.HasKey(LobbyMenu.TurnHintKey), Menu.Starting, () => Menu.Open(LobbyMenu.Play));
        }
    }

    public sealed class LoadoutPage : LobbyPage
    {
        public const float Width = 700f;
        public override string Id => LobbyMenu.Loadout;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;
        public override float PanelWidth => authoredPage && authoredPage.Panel.rect.width > 0 ? authoredPage.Panel.rect.width : Width;
        public bool ShowingRecipes { get; private set; }
        public string Part { get; private set; } = "Top";
        public string Pinned { get; private set; }

        RectTransform lockerList, detail;
        TMP_Text recipeFinish;
        float scrolled;
        string trying, previewing, shownRecipe;
        LobbyLoadoutLayout layout;
        LobbyLoadoutPageView authoredPage;

        public override void Opened()
        {
            scrolled = 0f;
            lockerList = null;
        }

        public override void Closed()
        {
            trying = previewing = null;
            if (Menu && Menu.Stage) Menu.Stage.Dress(Menu.Outfit);
        }

        protected override void Build()
        {
            if (lockerList) scrolled = lockerList.anchoredPosition.y;
            lockerList = detail = null;
            recipeFinish = null;
            trying = previewing = shownRecipe = null;
            authoredPage = null;
            layout = Resources.Load<LobbyLoadoutLayout>(LobbyLoadoutLayout.ResourcePath);
            if (layout)
            {
                layout.Validate();
                authoredPage = LobbyRuntimeTypography.Instantiate(layout.Page, Root, false);
                authoredPage.Bind(ShowingRecipes, Menu.Close, ShowRecipes, CustomizeRecipe);
                if (ShowingRecipes) Recipes(authoredPage.RecipesBody);
                else Locker(authoredPage.LockerBody);
                return;
            }
            var body = Side("Loadout");
            var tabs = LobbyKit.Segmented(body.parent, "Tabs", new[] { ("LOCKER", "Locker"), ("RECIPES", "Recipes") },
                ShowingRecipes ? "RECIPES" : "LOCKER", id => ShowRecipes(id == "RECIPES"));
            tabs.Place(Vector2.one, Vector2.one, new Vector2(-84 - 240, -74), new Vector2(-84, -26));
            if (ShowingRecipes) Recipes(body);
            else Locker(body);
        }

        public void ShowRecipes(bool on)
        {
            ShowingRecipes = on;
            Refresh();
            Reselect(on ? "RECIPES" : "LOCKER");
        }

        void Locker(RectTransform body)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var outfit = Menu.Outfit;
            var done = LobbyKit.Confirm(body, "Done", "Done", Menu.Close);
            ((RectTransform)done.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(0, 6), new Vector2(0, 66));
            var view = LobbyKit.Rect(body, "Locker").Place(Vector2.zero, Vector2.one, new Vector2(0, 84), Vector2.zero);
            lockerList = LobbyKit.Scroll(view, "Scroll", 12);
            lockerList.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 6, 10);
            lockerList.anchoredPosition = new Vector2(0, scrolled);
            AddLockerScrollbar(view, lockerList.GetComponentInParent<ScrollRect>());

            var slots = wardrobe.Slots.Where(s => wardrobe.PiecesFor(s).Count() > 1).ToList();
            var shown = new HashSet<string>();
            foreach (var (group, members) in LockerGroups)
            {
                var inGroup = members.Where(slots.Contains).ToList();
                if (inGroup.Count == 0) continue;
                GroupHeading(lockerList, group);
                foreach (string slot in inGroup) { PiecePicker(lockerList, slot); shown.Add(slot); }
            }
            var rest = slots.Where(s => !shown.Contains(s)).ToList();
            if (rest.Count > 0) GroupHeading(lockerList, "More");
            foreach (string slot in rest) PiecePicker(lockerList, slot);
            if (outfit.Pieces.Values.Any(id => wardrobe.Piece(id)?.Letter == true)) LetterPicker(lockerList);

            GroupHeading(lockerList, "Colours");
            var colour = LobbyKit.Column(lockerList, "Colour", 10, 14);
            LobbyKit.Face(colour, LobbyKit.Card, 14, LobbyKit.Line, 2);
            LobbyKit.Display(colour, "Pick a part, then a colour", 18, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft).Size(-1, 26);
            var parts = Parts(outfit);
            if (!parts.Contains(Part)) Part = parts.Contains("Top") ? "Top" : parts.FirstOrDefault();
            var chips = LobbyKit.Grid(colour, "Parts", new Vector2(140, 40), 8);
            foreach (string slot in parts) PartChip(chips, slot, wardrobe.ColourFor(outfit, slot));
            if (Part != null) Colours(colour, Part);

            var extras = wardrobe.Slots.Where(s => !wardrobe.RequiredSlots.Contains(s) && wardrobe.PiecesFor(s).Count() == 1).ToList();
            if (extras.Count > 0)
            {
                var section = LobbyKit.Column(lockerList, "Extras", 8);
                LobbyKit.SectionLabel(section, "Accessories");
                var cards = LobbyKit.Row(section, "Cards", 12);
                cards.Size(-1, 84);
                cards.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
                foreach (string slot in extras)
                {
                    var piece = wardrobe.PiecesFor(slot).First();
                    bool on = outfit.PieceIn(slot) == piece.Id;
                    LobbyKit.ToggleCard(cards, "Extra " + slot, ExtraIcon(slot), ExtraName(piece.Id), on,
                        () => PutOn(slot, on ? "No " + slot : piece.Id, "Extra " + slot)).Size(-1, -1, 1);
                }
            }

            GroupHeading(lockerList, "Equipment");
            var gear = LobbyKit.Column(lockerList, "Gear", 8, 14);
            LobbyKit.Face(gear, LobbyKit.Card, 14, LobbyKit.Line, 2);
            var gearHeading = LobbyKit.Row(gear, "Gear heading", 8);
            gearHeading.Size(-1, 40);
            gearHeading.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            LobbyKit.SectionLabel(gearHeading, "Equipment finish").Size(-1, 28, 1);
            LobbyKit.TextAction(gearHeading, "Per-recipe finishes", "By recipe", 16,
                () => ShowRecipes(true), icon: LobbyIcons.Chevron).Size(132, 40);
            string wornSkin = SkinOf(outfit);
            var finishState = LobbyKit.Text(gear, wornSkin == null ? "Mixed recipe finishes · choose below to match all." : "All recipes: " + FinishName(wornSkin),
                16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            finishState.name = "Gear finish state";
            finishState.enableAutoSizing = true; finishState.fontSizeMin = 12; finishState.fontSizeMax = 16;
            finishState.Size(-1, 28);
            var styles = LobbyKit.Row(gear, "Styles", 12);
            styles.Size(-1, 80);
            styles.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            foreach (var finish in CustomizeFinishes) FinishCard(styles, finish, wornSkin == finish.Id);
        }

        static void AddLockerScrollbar(RectTransform host, ScrollRect scroll)
        {
            if (!scroll) return;
            ((RectTransform)scroll.transform).offsetMax = new Vector2(-18, 0);
            var track = LobbyKit.Rect(host, "Scroll position").Place(new Vector2(1, 0), Vector2.one, new Vector2(-8, 0), Vector2.zero);
            track.Paint(LobbyKit.Line, 4).raycastTarget = true;
            var handle = LobbyKit.Rect(track, "Handle").Fill();
            var ink = handle.Paint(LobbyKit.Cyan, 4);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle; bar.targetGraphic = ink;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        static List<string> Parts(Outfit outfit)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            return wardrobe.Slots.Where(slot =>
            {
                var piece = wardrobe.Piece(outfit.PieceIn(slot) ?? "");
                return piece != null && piece.TintMaterial != null && piece.ColourFrom == null && wardrobe.Palettes.ContainsKey(slot);
            }).ToList();
        }

        string PartName(string slot) => slot == "Hair" || slot == "Eyes" ? slot :
            Menu.Outfit.PieceIn(slot) is string piece ? GameConfig.Current.Wardrobe.NameOf(piece) : slot;

        static List<(string id, string label, bool locked)> Choices(string slot, Career career = null)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var choices = wardrobe.PiecesFor(slot).Select(p => (p.Id, p.DisplayName, career != null && !career.Owns("piece", p.Id))).ToList();
            if (!wardrobe.RequiredSlots.Contains(slot)) choices.Add(("No " + slot, "None", false));
            return choices;
        }

        static readonly (string group, string[] slots)[] LockerGroups =
        {
            ("Your look", new[] { "Hair", "Eyes" }),
            ("Accessories", new[] { "Headwear", "Face" }),
            ("Clothes", new[] { "Top", "Bottoms", "Gloves", "Footwear", "Back", "Badge" }),
        };

        static string SlotTitle(string slot) => slot switch
        {
            "Headwear" => "Hats & ears",
            "Face" => "Glasses",
            "Back" => "Bag",
            _ => slot,
        };

        static void GroupHeading(Transform parent, string text)
        {
            var label = LobbyKit.Caps(parent, text, 14);
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.margin = new Vector4(6, 0, 0, 0);
            label.Size(-1, 30);
        }

        void PiecePicker(Transform parent, string slot)
        {
            const int PerRow = 3;
            var card = LobbyKit.Column(parent, slot, 10, 14);
            LobbyKit.Face(card, LobbyKit.Card, 14, LobbyKit.Line, 2);
            var choices = Choices(slot, Menu.Career);
            string worn = Menu.Outfit.PieceIn(slot) ?? "No " + slot;
            var heading = LobbyKit.Row(card, "Heading", 8);
            heading.Size(-1, 28);
            heading.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            LobbyKit.Display(heading, SlotTitle(slot), 20, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft).Size(-1, 28, 1);
            string wornLabel = choices.Where(c => c.id == worn).Select(c => c.label).FirstOrDefault() ?? "None";
            var now = LobbyKit.Text(heading, wornLabel, 16, LobbyKit.Muted, TextAlignmentOptions.MidlineRight);
            now.name = "Worn";
            now.Size(-1, 28, 1);
            for (int i = 0; i < choices.Count; i += PerRow)
            {
                var row = LobbyKit.Row(card, "Pick " + slot, 8);
                row.Size(-1, 44);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
                var chunk = choices.Skip(i).Take(PerRow).ToList();
                foreach (var (id, label, locked) in chunk)
                {
                    var button = ChoiceChip(row, id, label, id == worn, locked, () => Choose(slot, id));
                    button.Size(-1, -1, 1);
                    if (locked && CosmeticBundles.GrantedBy(id) is CosmeticBundle look)
                        button.gameObject.AddComponent<LobbyHint>().Text = label + " comes with " + look.Name;
                    if (!First && id == worn) First = button;
                }
                for (int pad = chunk.Count; pad < PerRow; pad++) LobbyKit.Rect(row, "Gap").Size(-1, -1, 1);
            }
        }

        static Button ChoiceChip(Transform parent, string id, string label, bool on, bool locked, Action click)
        {
            var fill = on ? LobbyKit.Selection : LobbyKit.Panel;
            var button = LobbyKit.Button(parent, id, fill, click, 12, on ? LobbyKit.Selection : LobbyKit.Line, 2);
            var press = button.GetComponent<LobbyPress>();
            var content = LobbyKit.Row(button.Body(), "Content", 6);
            content.Fill();
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(8, 8, 0, 0);
            var ink = on ? LobbyKit.OnSelection : locked ? LobbyKit.Faded : LobbyKit.Navy;
            if (on) LobbyKit.Icon(content, LobbyIcons.Check, ink).Size(16, 16);
            else if (locked) LobbyKit.Icon(content, LobbyIcons.Lock, ink).Size(16, 16);
            var text = LobbyKit.Text(content, label, 17, ink, TextAlignmentOptions.Center, FontStyles.Bold);
            text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 17;
            text.Size(-1, 26);
            text.GetComponent<LayoutElement>().minWidth = 0;
            if (!on)
            {
                var face = button.FaceOf();
                press.Hot = hot => face.color = hot ? LobbyKit.Raised : fill;
            }
            return button;
        }

        void Choose(string slot, string id)
        {
            if (id != "No " + slot && !Menu.Career.Owns("piece", id))
            {
                var look = CosmeticBundles.GrantedBy(id);
                Menu.Post(GameConfig.Current.Wardrobe.NameOf(id) + (look != null ? " comes with " + look.Name + "." : " is in the shop."));
                if (look != null) Menu.OpenShop("look:" + look.Id);
                return;
            }
            PutOn(slot, id, id);
        }

        void LetterPicker(Transform parent)
        {
            var row = LobbyKit.Row(parent, "Letter", 10);
            row.Size(-1, 68);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(14, 14, 6, 6);
            LobbyKit.Face(row, LobbyKit.Card, 14, LobbyKit.Line, 2);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childForceExpandHeight = false; layout.childAlignment = TextAnchor.MiddleLeft;
            LobbyKit.SectionLabel(row, "Letter").Size(86, 28);
            LobbyKit.TextAction(row, "Letter previous", "‹", 30, () => StepLetter(-1)).Size(52, 48);
            LobbyKit.LetterTile(row, LetterBadge.For(Menu.Outfit, 0), 46).name = "Letter tile";
            LobbyKit.TextAction(row, "Letter next", "›", 30, () => StepLetter(1)).Size(52, 48);
            LobbyKit.TextAction(row, "Letter seat", "Seat letter", 17, () => WearLetter(null, "Letter seat"), Menu.Outfit.Letter == null).Size(130, 44);
            LobbyKit.Rect(row, "Gap").Size(0, 10, 1);
        }

        void StepLetter(int step)
        {
            var letters = LetterBadge.Letters.ToList();
            int index = Mathf.Max(0, letters.IndexOf(LetterBadge.For(Menu.Outfit, 0)));
            WearLetter(letters[(index + step + letters.Count) % letters.Count], step < 0 ? "Letter previous" : "Letter next");
        }

        void WearLetter(string letter, string reselect)
        {
            var next = LetterBadge.With(Menu.Outfit, letter);
            if (next == null) return;
            Menu.Wear(next);
            Refresh();
            Reselect(reselect);
        }

        static string ExtraIcon(string slot) => slot switch
        {
            "Face" => LobbyIcons.Glasses,
            "Back" => LobbyIcons.Satchel,
            "Badge" => LobbyIcons.Badge,
            _ => LobbyIcons.Plus,
        };

        static string ExtraName(string piece) => GameConfig.Current.Wardrobe.NameOf(piece);

        void PartChip(Transform parent, string slot, Colourway colour)
        {
            bool on = Part == slot;
            var button = on
                ? LobbyKit.Button(parent, "Part " + slot, LobbyKit.Raised, () => PickPart(slot), 10, LobbyKit.Selection, 2)
                : LobbyKit.Button(parent, "Part " + slot, LobbyKit.Card, () => PickPart(slot), 10, LobbyKit.ChipEdge, 2);
            var press = button.GetComponent<LobbyPress>();
            if (on) press.Lift = 0f;
            var row = LobbyKit.Row(button.Body(), "Content", 8);
            row.Fill();
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(9, 8, 0, 0);
            layout.childForceExpandHeight = false;
            var dot = LobbyKit.Rect(row, "Dot");
            dot.Size(20, 20);
            if (colour != null) LobbyKit.Face(dot, new Color(colour.R, colour.G, colour.B), 10, LobbyKit.Navy, 2);
            var name = LobbyKit.Display(row, PartName(slot), 17, on ? LobbyKit.Navy : LobbyKit.Cream, TextAlignmentOptions.MidlineLeft);
            name.enableAutoSizing = true; name.fontSizeMin = 12; name.fontSizeMax = 17;
            name.Size(-1, 28, 1);
            if (colour != null) button.gameObject.AddComponent<LobbyHint>().Text = PartName(slot) + " · " + colour.Name;
        }

        public void PickPart(string slot)
        {
            Part = slot;
            Refresh();
            Reselect("Part " + slot);
        }

        void Colours(RectTransform section, string slot)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var outfit = Menu.Outfit;
            var palette = wardrobe.Palettes[slot];
            var current = wardrobe.ColourFor(outfit, slot);
            bool sold = slot == "Top";
            var swatches = LobbyKit.Grid(section, "Colours", new Vector2(80, 64), 8);
            swatches.GetComponent<GridLayoutGroup>().padding = new RectOffset(4, 4, 8, 4);
            foreach (var way in palette.Where(c => !sold || Menu.Career.Owns("colour", c.Id)))
                Swatch(swatches, slot, way, current?.Id == way.Id);
            var locked = sold ? palette.Where(c => !Menu.Career.Owns("colour", c.Id)).ToList() : new List<Colourway>();
            if (locked.Count > 0)
            {
                var shop = LobbyKit.Row(section, "In the shop", 8);
                shop.Size(-1, 40);
                var layout = shop.GetComponent<HorizontalLayoutGroup>();
                layout.childForceExpandHeight = false;
                layout.padding = new RectOffset(4, 0, 0, 0);
                LobbyKit.Caps(shop, "In the shop", 13).Size(-1, 20);
                foreach (var way in locked) ShopSwatch(shop, way);
                LobbyKit.Rect(shop, "Gap").Size(0, 10, 1);
                LobbyKit.Pill(shop, "Open shop", "Shop", 16, () => Menu.OpenShop("colour:" + locked[0].Id)).Size(84, 34);
            }
            var matching = outfit.Pieces.Values.Select(wardrobe.Piece).Where(p => p != null && p.ColourFrom == slot)
                .Select(p => p.DisplayName.ToLowerInvariant() + " matches");
            string caption = string.Join("  ·  ", new[] { PartName(slot), current?.Name }.Where(s => s != null).Concat(matching));
            LobbyKit.Text(section, caption, 16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft).Size(-1, 22);
        }

        void Swatch(Transform parent, string slot, Colourway colour, bool on)
        {
            var cell = LobbyKit.Rect(parent, "Swatch " + colour.Id);
            var label = LobbyKit.Text(cell, colour.Name, 13, on ? LobbyKit.Cream : LobbyKit.Muted, TextAlignmentOptions.Center);
            label.name = CardName;
            label.enableAutoSizing = true; label.fontSizeMin = 11; label.fontSizeMax = 14;
            label.rectTransform.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 20));
            var button = LobbyKit.Button(cell, "Colour " + colour.Id, LobbyKit.Navy, () => PickColour(slot, colour), 20, null, 0, 3);
            ((RectTransform)button.transform).Pin(new Vector2(.5f, 1), Vector2.zero, new Vector2(40, 40));
            SwatchFill(button.Body(), colour, 17);
            var press = button.GetComponent<LobbyPress>();
            press.HoverScale = 1.06f; press.Tilt = 0f;
            if (on) { press.Scale = 1.06f; LobbyKit.Ring(button.Body(), LobbyKit.Cyan, 20, 2); }
            press.Hot = hot => TryOn(slot, colour, hot);
            button.gameObject.AddComponent<LobbyHint>().Text = colour.Name;
        }

        void ShopSwatch(Transform parent, Colourway colour)
        {
            var offer = Career.Shop.FirstOrDefault(o => o.Kind == "colour" && o.Value == colour.Id);
            var button = LobbyKit.Button(parent, "Colour " + colour.Id, LobbyKit.Navy, () => ToShop(colour), 16, null, 0, 2);
            button.Size(32, 32);
            SwatchFill(button.Body(), colour, 13);
            LobbyKit.Coin(button.Body(), 16).Pin(new Vector2(1, 0), new Vector2(5, -5), new Vector2(16, 16));
            var press = button.GetComponent<LobbyPress>();
            press.HoverScale = 1.06f; press.Tilt = 0f;
            press.Hot = hot => TryOn("Top", colour, hot);
            button.gameObject.AddComponent<LobbyHint>().Text = colour.Name + (offer != null ? $" · {offer.Price} coins" : " · in the shop");
        }

        static void SwatchFill(RectTransform body, Colourway colour, int radius)
        {
            var fill = LobbyKit.Rect(body, "Fill").Place(Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
            fill.Paint(new Color(colour.R, colour.G, colour.B), radius).raycastTarget = false;
            var shine = LobbyKit.Rect(fill, "Shine").Pin(new Vector2(.32f, .72f), Vector2.zero, new Vector2(radius * .55f, radius * .42f));
            shine.Paint(new Color(1f, 1f, 1f, .67f), Mathf.Max(1, Mathf.RoundToInt(radius * .25f))).raycastTarget = false;
        }

        void TryOn(string slot, Colourway colour, bool hot)
        {
            string key = slot + ":" + colour.Id;
            if (hot)
            {
                trying = key;
                var preview = Menu.Outfit.Clone();
                preview.Colours[slot] = colour.Id;
                Menu.Stage.Dress(preview);
            }
            else if (trying == key)
            {
                trying = null;
                Menu.Stage.Dress(Menu.Outfit);
            }
        }

        void ToShop(Colourway colour)
        {
            Menu.Post(colour.Name + " is in the shop.");
            Menu.OpenShop("colour:" + colour.Id);
        }

        void PickColour(string slot, Colourway colour)
        {
            var next = Menu.Outfit.Clone();
            next.Colours[slot] = colour.Id;
            Menu.Wear(next);
            Refresh();
            Reselect("Colour " + colour.Id);
        }

        public void PutOn(string slot, string choice, string reselect)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            string id = choice == "No " + slot ? null : choice;
            var next = Menu.Outfit;
            foreach (string need in wardrobe.Piece(id ?? "")?.Requires ?? new List<string>())
            {
                var needed = wardrobe.Piece(need);
                if (needed != null && next.PieceIn(needed.Slot) != need) next = wardrobe.Wear(next, needed.Slot, need) ?? next;
            }
            next = wardrobe.Wear(next, slot, id);
            if (next == null)
            {
                Menu.Post("That doesn't go with what you're wearing.");
                return;
            }
            if (Menu.Career.Missing(next).Any(missing => missing.StartsWith("piece:", StringComparison.Ordinal)))
            {
                Menu.Post("That piece isn't yours yet.");
                return;
            }
            Menu.Wear(next);
            Refresh();
            Reselect(reselect);
        }

        void FinishCard(Transform parent, Finish finish, bool worn)
        {
            string skin = finish.Id;
            bool owned = Menu.Career.Owns("skin", skin);
            var offer = Career.Shop.FirstOrDefault(o => o.Kind == "skin" && o.Value == skin);
            var card = LobbyKit.Button(parent, "Finish " + skin, LobbyKit.Card, () => PickFinish(finish, owned), 14, LobbyKit.Line, 1, 0);
            card.Size(-1, -1, 1);
            if (!string.IsNullOrEmpty(finish.Tagline)) card.gameObject.AddComponent<LobbyHint>().Text = finish.Name + " · " + finish.Tagline;
            var press = card.GetComponent<LobbyPress>();
            press.Lift = 3f; press.Tilt = 1f;
            var body = card.Body();
            if (worn) LobbyKit.Ring(body, LobbyKit.Selection, 14, 2);
            FinishDisc(body, skin, 52).Pin(new Vector2(0, .5f), new Vector2(12, 0), new Vector2(52, 52));
            var name = LobbyKit.Text(body, finish.Name, 20, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            name.name = CardName;
            name.enableAutoSizing = true; name.fontSizeMin = 13; name.fontSizeMax = 20;
            name.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(74, 38), new Vector2(-8, -10));
            var state = LobbyKit.Row(body, "State", 4);
            state.Place(Vector2.zero, new Vector2(1, 0), new Vector2(74, 12), new Vector2(-8, 36));
            state.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            if (worn || owned || offer == null)
            {
                var label = LobbyKit.Text(state, worn ? "WEARING" : "OWNED", 13, worn ? LobbyKit.Owned : LobbyKit.CardSub,
                    TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                label.characterSpacing = 1;
                label.enableAutoSizing = true; label.fontSizeMin = 10; label.fontSizeMax = 13;
                label.Size(-1, 20);
            }
            else
            {
                LobbyKit.Coin(state, 18);
                LobbyKit.Display(state, offer.Price.ToString("N0", CultureInfo.InvariantCulture), 17, LobbyKit.Navy,
                    TextAlignmentOptions.MidlineLeft).Size(-1, 22);
            }
        }

        void PickFinish(Finish finish, bool owned)
        {
            if (!owned)
            {
                Menu.Post(finish.Name + " gear is in the shop.");
                Menu.OpenShop("skin:" + finish.Id);
                return;
            }
            Menu.Wear(WithFinish(Menu.Outfit, finish.Id));
            Refresh();
            Reselect("Finish " + finish.Id);
        }

        ItemDefinition Shown()
        {
            var items = GameConfig.Current.Items.Enabled.ToList();
            return items.FirstOrDefault(i => i.Id == Pinned) ?? items.FirstOrDefault();
        }

        void Recipes(RectTransform body)
        {
            var shown = Shown();
            if (authoredPage)
            {
                recipeFinish = authoredPage.Finish.Label;
                detail = authoredPage.RecipeDetail;
                foreach (var item in GameConfig.Current.Items.Enabled) RecipeCard(authoredPage.RecipeGrid.transform, item, item == shown);
                Detail(shown);
                return;
            }
            var finish = LobbyKit.TextAction(body, "Recipe finish", "Finish", 18,
                CustomizeRecipe, icon: LobbyIcons.Chevron);
            ((RectTransform)finish.transform).Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 44));
            recipeFinish = finish.GetComponentInChildren<TMP_Text>();
            detail = LobbyKit.Rect(body, "Recipe detail").Place(Vector2.zero, new Vector2(1, 0), new Vector2(0, 56), new Vector2(0, 252));
            var view = LobbyKit.Rect(body, "Recipes").Place(Vector2.zero, Vector2.one, new Vector2(0, 268), Vector2.zero);
            var list = LobbyKit.Scroll(view, "Scroll", 0);
            list.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 8, 12);
            var grid = LobbyKit.Grid(list, "Grid", new Vector2(122, 118), 12);
            foreach (var item in GameConfig.Current.Items.Enabled) RecipeCard(grid, item, item == shown);
            Detail(shown);
        }

        void RecipeCard(Transform parent, ItemDefinition item, bool on)
        {
            if (authoredPage)
            {
                var savedTile = LobbyRuntimeTypography.Instantiate(layout.Recipe, parent, false);
                savedTile.Bind(item.Id, on, () => PinRecipe(item.Id), hot =>
                {
                    if (hot) { previewing = item.Id; Detail(item); }
                    else if (previewing == item.Id) { previewing = null; Detail(Shown()); }
                });
                if (on) First = savedTile.Button;
                return;
            }
            var card = LobbyKit.Button(parent, "Recipe " + item.Id, LobbyKit.Card, () => PinRecipe(item.Id), 14, LobbyKit.Line, 1, 0);
            var press = card.GetComponent<LobbyPress>();
            press.Lift = 4f; press.Tilt = 2f;
            var body = card.Body();
            if (on)
            {
                LobbyKit.Ring(body, LobbyKit.Selection, 14, 2);
                First = card;
            }
            var icon = LobbyKit.ItemImage(body, item.Id, 64);
            if (icon) icon.rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -10), new Vector2(64, 64));
            string word = item.Id;
            float tile = Mathf.Min(24f, (112f - 2f * (word.Length - 1)) / word.Length);
            var letters = LobbyKit.Row(body, "Letters", 2);
            letters.Place(Vector2.zero, new Vector2(1, 0), new Vector2(4, 12), new Vector2(-4, 12 + tile + 4));
            var row = letters.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandHeight = false;
            foreach (char letter in word) LobbyKit.LetterTile(letters, letter, tile);
            press.Hot = hot =>
            {
                if (hot) { previewing = item.Id; Detail(item); }
                else if (previewing == item.Id) { previewing = null; Detail(Shown()); }
            };
        }

        public void PinRecipe(string id)
        {
            Pinned = id;
            Refresh();
            Reselect("Recipe " + id);
        }

        void CustomizeRecipe()
        {
            string id = shownRecipe;
            if (id == null) return;
            Menu.Open(LobbyMenu.Shop);
            Menu.Page<ShopPage>().SelectRecipe(id);
        }

        void Detail(ItemDefinition item)
        {
            if (!detail || item == null) return;
            shownRecipe = item.Id;
            string skin = Appearance.Pick(item, Menu.Outfit.SkinFor(item.Id));
            if (recipeFinish) recipeFinish.text = "Finish · " + FinishName(skin);
            LobbyKit.Clear(detail);
            var strip = LobbyKit.Rect(detail, "Strip").Fill();
            LobbyKit.Face(strip, LobbyKit.Track, 18);
            var art = LobbyKit.Rect(strip, "Art").Pin(new Vector2(0, .5f), new Vector2(16, 0), new Vector2(150, 150));
            Burst(art, 150, 150, 16);
            FinishedItem(art, item.Id, skin, 118);
            var words = LobbyKit.Column(strip, "Words", 10);
            words.Place(Vector2.zero, Vector2.one, new Vector2(184, 16), new Vector2(-16, -16));
            words.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            var tiles = LobbyKit.Row(words, "Word", 5);
            tiles.Size(-1, 44);
            tiles.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            string word = item.Id;
            float size = Mathf.Min(38f, (320f - 5f * (word.Length - 1)) / word.Length);
            for (int i = 0; i < word.Length; i++) LobbyKit.LetterTile(tiles, word[i], size, (i - (word.Length - 1) / 2f) * 2f);
            var blurb = LobbyKit.Display(words, Blurb(item), 22, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft);
            blurb.name = "Blurb";
            blurb.enableAutoSizing = true; blurb.fontSizeMin = 15; blurb.fontSizeMax = 22;
            blurb.Size(-1, 30);
            LobbyKit.Text(words, "Spell this word in a match", 16, LobbyKit.Muted,
                TextAlignmentOptions.MidlineLeft).Size(-1, 30);
        }

        public static string Blurb(ItemDefinition item)
        {
            if (item.Use?.Effect == UseEffect.Heal)
                return $"Restore {item.Use.Amount:0} HP · {item.Use.ChannelSeconds:0.##}s to use";
            if (item.Use?.Effect == UseEffect.Speed)
                return $"{item.Use.Amount:0.##}× speed · {item.Use.Seconds:0.#} seconds";
            if (item.Shield?.FrontArcDegrees >= 360f) return "All-around block · hold to raise";
            if (item.Deploy?.Effect == DeployEffect.WindField) return "Forward gust · pushes everyone";
            if (item.Deploy?.Effect == DeployEffect.SlowField) return "Slowing field · affects everyone";
            if (item.Thrown?.FuseSeconds > 0f) return "Delayed blast · clear the room";
            if (item.Consumable && item.Thrown != null) return "One throw · one splat";
            return Blurb(item.Family);
        }

        public static string Blurb(HandlingFamily family) => family switch
        {
            HandlingFamily.MeleeSwing => "A satisfying swing",
            HandlingFamily.MeleeThrust => "A little extra reach",
            HandlingFamily.Thrown => "Catch. Throw. Repeat.",
            HandlingFamily.Buff => "A protective bubble",
            HandlingFamily.Shield => "Frontal block · hold to raise",
            HandlingFamily.DeployPad => "A bouncy jump pad",
            HandlingFamily.DeploySpeed => "A speedy little shortcut",
            HandlingFamily.DeployZone => "A slippery surprise",
            HandlingFamily.DeployCover => "Make your own cover",
            HandlingFamily.Heal => "Patch yourself up",
            HandlingFamily.Ranged => "Hit them from afar",
            _ => "Handy around the house",
        };
    }

    public sealed class CareerScreen : LobbyPage
    {
        public override string Id => LobbyMenu.CareerPage;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;

        protected override void Build()
        {
            var career = Menu.Career;
            var body = Side("Career", "Practice matches count. Online play will add ranked results.");
            var column = LobbyKit.Column(body, "Column", 16).Fill();

            var level = LobbyKit.Row(column, "Level", 22);
            level.Size(-1, 112);
            level.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            LobbyKit.LetterTile(level, career.Level.ToString(CultureInfo.InvariantCulture), 104).name = "Level tile";
            var info = LobbyKit.Rect(level, "Info");
            info.Size(-1, 112, 1);
            var name = LobbyKit.Display(info, career.Name, 36, LobbyKit.Cream, TextAlignmentOptions.BottomLeft);
            name.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -46), Vector2.zero);
            int next = Career.XpToNext(career.Level);
            var bar = LobbyKit.Rect(info, "XP bar").Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -84), new Vector2(0, -54));
            bar.Paint(LobbyKit.Track, 10).raycastTarget = false;
            var fill = LobbyKit.Rect(bar, "Fill").Place(Vector2.zero, new Vector2(Mathf.Clamp01(career.XpIntoLevel / (float)next), 1)).Paint(Color.white, 10);
            fill.raycastTarget = false;
            LobbyKit.Gradient(fill, LobbyKit.Selection, LobbyKit.Selection);
            LobbyKit.Frame(bar, LobbyKit.Line, 10, 1);
            var xpBacking = LobbyKit.Rect(bar, "XP label").Place(new Vector2(1, 0), Vector2.one, new Vector2(-180, 2), new Vector2(-2, -2));
            xpBacking.Paint(LobbyKit.Card, 8).raycastTarget = false;
            var xp = LobbyKit.Display(xpBacking, Number(career.XpIntoLevel) + " / " + Number(next) + " XP", 18, LobbyKit.Cream,
                TextAlignmentOptions.MidlineRight, LobbyKit.Ink.Stroke);
            xp.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-14, 0));
            var line = LobbyKit.Text(info, $"Level {career.Level}  ·  {Number(Mathf.Max(0, next - career.XpIntoLevel))} XP to level {career.Level + 1}",
                18, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            line.name = "XP line";
            line.overflowMode = TextOverflowModes.Overflow;
            line.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -114), new Vector2(0, -90));

            var stats = LobbyKit.Row(column, "Stats", 14);
            stats.Size(-1, 110);
            Stat(stats, "Matches", Number(career.Matches), LobbyIcons.Play);
            Stat(stats, "Wins", Number(career.Wins), LobbyIcons.Trophy);
            Stat(stats, "Win rate", career.Matches == 0 ? "-" : Mathf.RoundToInt(100f * career.Wins / career.Matches) + "%", LobbyIcons.Badge);
            Stat(stats, "Coins", Number(career.Coins), null);

            LobbyKit.SectionLabel(column, "Recent matches");
            if (career.History.Count == 0)
            {
                Empty(column);
                return;
            }
            var holder = Grow(LobbyKit.Rect(column, "History"));
            var list = LobbyKit.Scroll(holder, "Scroll", 10);
            foreach (var match in career.History) Match(list, match);
        }

        static void Stat(Transform parent, string title, string value, string icon)
        {
            var tile = LobbyKit.Rect(parent, title);
            LobbyKit.Face(tile, LobbyKit.Card, 16, LobbyKit.Line, 1, 0);
            tile.Size(-1, -1, 1);
            var mark = LobbyKit.Rect(tile, "Mark").Pin(new Vector2(0, .5f), new Vector2(16, 0), new Vector2(52, 52));
            if (icon == null) LobbyKit.Coin(mark, 52).Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(52, 52));
            else
            {
                LobbyKit.Face(mark, LobbyKit.SunHi, 26, LobbyKit.Navy, 2, 0, LobbyKit.Sun2);
                LobbyKit.Icon(mark, icon, LobbyKit.ArtInk).rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(13, 13), new Vector2(-13, -13));
            }
            var number = LobbyKit.Display(tile, value, 34, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            number.enableAutoSizing = true; number.fontSizeMin = 22; number.fontSizeMax = 34;
            number.rectTransform.Place(new Vector2(0, .5f), Vector2.one, new Vector2(80, -6), new Vector2(-10, -8));
            var caption = LobbyKit.Caps(tile, title, 13, TextAlignmentOptions.TopLeft);
            caption.color = LobbyKit.CardSub;
            caption.rectTransform.Place(Vector2.zero, new Vector2(1, .5f), new Vector2(80, 8), new Vector2(-8, -4));
        }

        static void Match(Transform list, MatchRecord match)
        {
            var card = LobbyKit.Row(list, "Match", 14);
            card.Size(-1, 72);
            var layout = card.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 18, 0, 0);
            layout.childForceExpandHeight = false;
            LobbyKit.Face(card, LobbyKit.Card, 14, LobbyKit.Line, 2);
            var colour = LobbyKit.ModeColour(match.Mode);
            var badge = LobbyKit.Rect(card, "Mode");
            badge.Size(52, 52);
            LobbyKit.Face(badge, Color.Lerp(colour, Color.white, .25f), 12, LobbyKit.Navy, 2, 0, Color.Lerp(colour, Color.black, .2f));
            LobbyKit.ItemImage(badge, LobbyKit.ModeArt(match.Mode), 44, 6f);
            var words = LobbyKit.Rect(card, "Words");
            words.Size(-1, 56, 1);
            var title = LobbyKit.Display(words, LobbyMenu.ModeName(match.Mode), 24, LobbyKit.Cream, TextAlignmentOptions.BottomLeft);
            title.rectTransform.Place(new Vector2(0, .45f), Vector2.one, Vector2.zero, Vector2.zero);
            var detail = LobbyKit.Text(words, MapName(match.Map) + "  ·  " + When(match.EndedAt), 15, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            detail.rectTransform.Place(Vector2.zero, new Vector2(1, .45f), Vector2.zero, new Vector2(0, -2));
            var result = LobbyKit.Rect(card, "Result");
            result.Size(84, 32);
            if (match.Won) LobbyKit.Face(result, LobbyKit.LimeHi, 16, LobbyKit.Navy, 2, 0, LobbyKit.LimeLo);
            else LobbyKit.Face(result, LobbyKit.Card, 16, LobbyKit.Line, 2);
            LobbyKit.Display(result, match.Won ? "WON" : "LOST", 18, match.Won ? LobbyKit.ArtInk : LobbyKit.Muted).rectTransform.Fill();
            LobbyKit.Display(card, Number(match.Score), 28, LobbyKit.Sun, TextAlignmentOptions.MidlineRight).Size(110, 40);
            LobbyKit.Text(card, $"+{Number(match.Coins)} coins  ·  +{Number(match.Xp)} XP", 15, LobbyKit.Muted,
                TextAlignmentOptions.MidlineRight).Size(200, 30);
        }

        void Empty(Transform column)
        {
            var card = LobbyKit.Rect(column, "No matches");
            card.Size(-1, 220);
            LobbyKit.Face(card, LobbyKit.Card, 18, LobbyKit.Line, 2);
            var art = LobbyKit.ItemImage(card, "BOX", 112, 8f);
            if (art) art.rectTransform.Pin(new Vector2(0, .5f), new Vector2(30, 0), new Vector2(112, 112));
            var title = LobbyKit.Display(card, "No matches yet", 34, LobbyKit.Sun, TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Stroke);
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(172, -78), new Vector2(-24, -26));
            var line = Wrapped(card, "Finish a local match to see your results here.", 19, LobbyKit.Muted);
            line.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(172, -136), new Vector2(-24, -84));
            var go = LobbyKit.Button(card, "Play practice", LobbyKit.SunHi, () =>
            {
                Menu.Choose(queue: LobbyMenu.Practice);
                Menu.Open(LobbyMenu.Play);
            }, 12, LobbyKit.Navy, 3, 4, LobbyKit.Sun2);
            var label = LobbyKit.Display(go.Body(), "PLAY PRACTICE", 22, LobbyKit.ArtInk);
            label.characterSpacing = 2;
            label.rectTransform.Fill();
            ((RectTransform)go.transform).Pin(Vector2.zero, new Vector2(172, 26), new Vector2(250, 54));
            First = go;
        }
    }

    public sealed class TrophyPage : LobbyPage
    {
        static readonly (Color from, Color to, Color hi, Color lo)[] Podium =
        {
            (LobbyKit.Hex(0xffe14d), LobbyKit.Hex(0xfff7c2), LobbyKit.Hex(0xfff4a8), LobbyKit.Hex(0xe09a00)),
            (LobbyKit.Hex(0xd9e2f2), LobbyKit.Hex(0xf7f9fd), LobbyKit.Hex(0xffffff), LobbyKit.Hex(0x8f9cb0)),
            (LobbyKit.Hex(0xffbf8a), LobbyKit.Hex(0xffeedf), LobbyKit.Hex(0xffd5ad), LobbyKit.Hex(0xb4601f)),
        };
        const int Places = 5, ChipsPerRow = 5;
        string board;

        public override string Id => LobbyMenu.Trophy;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;
        public override float PanelWidth => 700f;

        public override void Opened() => board = null;

        protected override void Build()
        {
            var career = Menu.Career;
            if (!LobbyMenu.Modes.Contains(board)) board = LobbyMenu.Modes.Contains(Menu.Mode) ? Menu.Mode : LobbyMenu.Modes[0];
            var body = Side("Leaderboards", "Best single-match score in each mode.");
            var column = LobbyKit.Column(body, "Column", 10, 6).Fill();
            for (int first = 0; first < LobbyMenu.Modes.Length; first += ChipsPerRow)
            {
                var chips = LobbyKit.Row(column, "Boards", 10);
                chips.Size(-1, 92);
                chips.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
                var modes = LobbyMenu.Modes.Skip(first).Take(ChipsPerRow).ToArray();
                foreach (string mode in modes) Chip(chips, mode);
                for (int empty = modes.Length; empty < ChipsPerRow; empty++) LobbyKit.Rect(chips, "Empty").Size(-1, -1, 1);
            }
            LobbyKit.Rect(column, "Gap").Size(-1, 4);

            var rows = Rows(career, board);
            Note(column, "Records on this PC.");
            if (rows.Count == 0) Note(column, "No scores yet. Be the first!");
            for (int i = 0; i < rows.Count; i++) Entry(column, i + 1, rows[i].score, career.Name, rows[i].detail, i == 0);
            Grow(LobbyKit.Rect(column, "Space"));

            var bar = LobbyKit.Row(column, "Your best", 12);
            bar.Size(-1, 68);
            var layout = bar.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(20, 22, 0, 0);
            layout.childForceExpandHeight = false;
            LobbyKit.Face(bar, LobbyKit.Raised, 14, LobbyKit.Line, 2);
            LobbyKit.Icon(bar, LobbyIcons.Trophy, LobbyKit.SunHi).Size(30, 30);
            LobbyKit.Text(bar, "Your best", 20, Color.white, TextAlignmentOptions.MidlineLeft).Size(-1, 30, 1);
            LobbyKit.Display(bar, career.Bests.TryGetValue(board, out int best) ? Number(best) : "-", 32, LobbyKit.SunHi,
                TextAlignmentOptions.MidlineRight).Size(200, 40);
        }

        static List<(int score, string detail)> Rows(Career career, string mode)
        {
            var rows = career.History.Where(m => m.Mode == mode).OrderByDescending(m => m.Score)
                .Select(m => (score: m.Score, detail: MapName(m.Map) + "  ·  " + When(m.EndedAt))).ToList();
            if (career.Bests.TryGetValue(mode, out int best) && (rows.Count == 0 || best > rows[0].score)) rows.Insert(0, (best, "All-time best"));
            return rows.Take(Places).ToList();
        }

        void Chip(Transform row, string mode)
        {
            bool on = mode == board;
            string name = "Board " + mode;
            var button = on ? LobbyKit.Button(row, name, LobbyKit.Raised, () => Pick(mode), 12, LobbyKit.Selection, 2, 0)
                : LobbyKit.Button(row, name, LobbyKit.Card, () => Pick(mode), 12, LobbyKit.Line, 2);
            button.Size(-1, -1, 1);
            var press = button.GetComponent<LobbyPress>();
            press.Lift = 2f;
            if (on) press.Lean = 2f;
            var body = button.Body();
            var art = LobbyKit.ItemImage(body, LobbyKit.ModeArt(mode), 50);
            if (art) art.rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -6), new Vector2(50, 50));
            var label = LobbyKit.Display(body, LobbyMenu.ModeName(mode), 17, LobbyKit.Cream, TextAlignmentOptions.Center,
                on ? LobbyKit.Ink.Stroke : LobbyKit.Ink.Plain);
            label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 17;
            label.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(6, 8), new Vector2(-6, 34));
            if (on) First = button;
        }

        void Pick(string mode)
        {
            board = mode;
            Refresh();
            Reselect("Board " + mode);
        }

        static void Entry(Transform column, int rank, int score, string name, string detail, bool you)
        {
            var row = LobbyKit.Row(column, "Rank " + rank, 14);
            row.Size(-1, 64);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 18, 0, 0);
            layout.childForceExpandHeight = false;
            var tile = LobbyKit.Rect(row, "Rank");
            tile.Size(48, 48);
            bool podium = rank <= Podium.Length;
            if (podium)
            {
                var (from, to, hi, lo) = Podium[rank - 1];
                LobbyKit.Face(row, from, 14, LobbyKit.Navy, 3, 3, to).GetComponent<LobbyGradient>().Set(from, to, true);
                LobbyKit.Face(tile, hi, 12, LobbyKit.Navy, 2, 0, lo);
            }
            else
            {
                LobbyKit.Face(row, LobbyKit.Card, 14, LobbyKit.Line, 1);
                LobbyKit.Face(tile, LobbyKit.WoodHi, 12, LobbyKit.Cocoa, 2, 3, LobbyKit.WoodLo, dropColour: LobbyKit.Cocoa);
            }
            if (you) LobbyKit.Frame(row, LobbyKit.Hot, 14, 3, "You", 4);
            LobbyKit.Display(tile, rank.ToString(CultureInfo.InvariantCulture), 26, podium ? LobbyKit.ArtInk : LobbyKit.WoodInk).rectTransform.Fill();
            var who = LobbyKit.Rect(row, "Who");
            who.Size(-1, 56, 1);
            var title = LobbyKit.Display(who, name, 24, podium ? LobbyKit.ArtInk : LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            title.rectTransform.Place(new Vector2(0, .45f), Vector2.one, Vector2.zero, Vector2.zero);
            var sub = LobbyKit.Text(who, detail, 15, podium ? LobbyKit.ArtInk : LobbyKit.CardSub, TextAlignmentOptions.TopLeft);
            sub.rectTransform.Place(Vector2.zero, new Vector2(1, .45f), Vector2.zero, new Vector2(0, -2));
            LobbyKit.Display(row, Number(score), 30, podium ? LobbyKit.ArtInk : LobbyKit.Navy, TextAlignmentOptions.MidlineRight).Size(160, 40);
        }

        static void Note(Transform column, string text)
        {
            var note = LobbyKit.Rect(column, "Note");
            note.Size(-1, 48);
            note.Paint(LobbyKit.Card, 10).raycastTarget = false;
            LobbyKit.Text(note, text, 17, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft).rectTransform
                .Place(Vector2.zero, Vector2.one, new Vector2(16, 0), new Vector2(-12, 0));
        }
    }

    public sealed class LatinNameValidator : TMP_InputValidator
    {
        public static LatinNameValidator Create() => CreateInstance<LatinNameValidator>();

        public override char Validate(ref string text, ref int position, char character)
        {
            if (Career.CleanName(character.ToString()).Length == 0) return '\0';
            text = text.Insert(position, character.ToString());
            position++;
            return character;
        }
    }
}

