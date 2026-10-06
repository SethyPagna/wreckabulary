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
    /// <summary>One lobby page. Only one shows at a time, and each redraws itself from the menu's state.</summary>
    public abstract class LobbyPage
    {
        protected static readonly Color Chosen = new Color(LobbyKit.Honey.r, LobbyKit.Honey.g, LobbyKit.Honey.b, .16f);
        protected static readonly Color Ink = new Color(.12f, .09f, .06f);
        protected static readonly string[] Finishes = { "Classic", "Candy", "Arcade" };

        public abstract string Id { get; }
        public virtual LobbyStage.Focus Focus => LobbyStage.Focus.Centre;
        /// <summary>True when the lobby messages show beside this page.</summary>
        public virtual bool ShowFeed => false;
        public RectTransform Root { get; private set; }
        /// <summary>Where a keyboard or controller starts on this page.</summary>
        public Selectable First { get; protected set; }
        protected LobbyMenu Menu { get; private set; }

        public void Create(LobbyMenu menu, RectTransform host)
        {
            Menu = menu;
            Root = LobbyKit.Rect(host, Id + " page").Fill();
            Root.gameObject.SetActive(false);
        }

        /// <summary>Redraws the page from the menu's current state.</summary>
        public void Refresh()
        {
            LobbyKit.Clear(Root);
            First = null;
            Build();
        }

        protected abstract void Build();

        /// <summary>Keeps the keyboard or controller on the same control after a redraw.</summary>
        protected void Reselect(string name)
        {
            if (!EventSystem.current) return;
            var target = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == name);
            if (target) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        /// <summary>A dark panel with a title. Returns the body to fill.</summary>
        protected RectTransform Panel(Vector2 min, Vector2 max, string title, string subtitle = null)
        {
            var panel = LobbyKit.Rect(Root, "Panel").Place(min, max);
            panel.Paint(Focus == LobbyStage.Focus.Centre ? LobbyKit.Page : LobbyKit.Panel, true);
            var head = LobbyKit.Text(panel, LobbyKit.Upper(title), 34, LobbyKit.Cream, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            head.characterSpacing = 3;
            head.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(32, -78), new Vector2(-32, -24));
            float top = 98;
            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = LobbyKit.Text(panel, subtitle, 19, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
                sub.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(32, -116), new Vector2(-32, -84));
                top = 130;
            }
            return LobbyKit.Rect(panel, "Body").Place(Vector2.zero, Vector2.one, new Vector2(32, 28), new Vector2(-32, -top));
        }

        /// <summary>The panel the loadout, career and shop share: docked right, so you stand on the left.</summary>
        protected RectTransform Side(string title, string subtitle = null) => Panel(new Vector2(.42f, 0), Vector2.one, title, subtitle);

        protected static TextMeshProUGUI Wrapped(Transform parent, string text, float size, Color colour, float height = -1)
        {
            var label = LobbyKit.Text(parent, text, size, colour, TextAlignmentOptions.TopLeft);
            label.textWrappingMode = TextWrappingModes.Normal;
            if (height > 0) label.Size(-1, height);
            return label;
        }

        /// <summary>Lets a layout child take the height left over.</summary>
        protected static RectTransform Grow(RectTransform rect)
        {
            if (!rect.TryGetComponent(out LayoutElement element)) element = rect.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1;
            return rect;
        }

        /// <summary>A tall choice card: a title, a line about it and an optional footer.</summary>
        protected static Button Card(Transform parent, string name, string title, string body, string foot, bool on, Action click)
        {
            var button = LobbyKit.Button(parent, name, on ? Chosen : LobbyKit.Card, click);
            var t = button.transform;
            if (on) LobbyKit.Rect(t, "Mark").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 5)).Paint(LobbyKit.Honey).raycastTarget = false;
            var head = LobbyKit.Text(t, title, 27, on ? LobbyKit.Honey : LobbyKit.Cream, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            head.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-16, -18));
            var text = Wrapped(t, body, 18, LobbyKit.Muted);
            text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(20, foot != null ? 44 : 14), new Vector2(-16, -56));
            text.overflowMode = TextOverflowModes.Ellipsis;
            if (foot != null)
            {
                var f = LobbyKit.Text(t, foot, 18, LobbyKit.Honey, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
                f.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(20, 16), new Vector2(-16, 0));
            }
            return button;
        }

        /// <summary>A short row button: a title over a muted line, like a loadout slot.</summary>
        protected static Button Tile(Transform parent, string name, string title, string sub, bool on, Action click)
        {
            var button = LobbyKit.Button(parent, name, on ? Chosen : LobbyKit.Card, click);
            var t = button.transform;
            if (on) LobbyKit.Rect(t, "Mark").Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(5, 0)).Paint(LobbyKit.Honey).raycastTarget = false;
            var head = LobbyKit.Text(t, title, 21, on ? LobbyKit.Honey : LobbyKit.Cream, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            head.rectTransform.Place(new Vector2(0, .5f), Vector2.one, new Vector2(18, 0), new Vector2(-12, -6));
            var line = LobbyKit.Text(t, sub, 17, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            line.rectTransform.Place(Vector2.zero, new Vector2(1, .5f), new Vector2(18, 6), new Vector2(-12, 0));
            return button;
        }

        protected static string SkinOf(Outfit outfit) => outfit.ItemSkins.Values.FirstOrDefault() ?? Skin.Standard;

        /// <summary>The same finish on every craftable item.</summary>
        public static Outfit WithFinish(Outfit outfit, string skin)
        {
            var next = outfit.Clone();
            foreach (var item in GameConfig.Current.Items.Enabled) next.ItemSkins[item.Id] = skin;
            return next;
        }

        protected static string Capital(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        protected static string MapName(string id) =>
            id != null && GameConfig.Current.Houses.TryGetValue(id, out var house) ? house.Name : id ?? "";
    }

    /// <summary>Just you in the map, the lobby messages, and what GO will start.</summary>
    public sealed class HomePage : LobbyPage
    {
        public override string Id => LobbyMenu.Home;
        public override bool ShowFeed => true;

        protected override void Build()
        {
            var hint = LobbyKit.Row(Root, "Turn hint", 8);
            hint.Pin(new Vector2(.5f, 0), new Vector2(0, 4), new Vector2(240, 36));
            var layout = hint.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            LobbyKit.Icon(hint, LobbyIcons.Turn, LobbyKit.Muted).Size(26, 26);
            LobbyKit.Text(hint, "Drag to turn", 19, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft).Size(130, 30);
            // While a match starts, the countdown under the tabs takes over.
            if (Menu.Starting) return;

            var card = LobbyKit.Rect(Root, "Next match").Pin(Vector2.zero, Vector2.zero, new Vector2(540, 176));
            card.Paint(LobbyKit.Panel, true);
            LobbyKit.Rect(card, "Accent").Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(6, 0)).Paint(LobbyKit.Honey);
            bool blocked = Menu.Blocked != null;
            var title = LobbyKit.Text(card, "NEXT UP  ·  " + LobbyKit.Upper(Menu.Queue) + (blocked ? "  ·  NOT BUILT YET" : ""),
                18, LobbyKit.Honey, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            title.characterSpacing = 3;
            title.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(28, 0), new Vector2(-24, -18));
            var detail = Wrapped(card, Menu.Describe(), 22, blocked ? LobbyKit.Muted : LobbyKit.Cream);
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(28, 84), new Vector2(-24, -48));
            var change = LobbyKit.LabelButton(card, "CHANGE", LobbyKit.Card, LobbyKit.Cream, 22, () => Menu.Open(LobbyMenu.Play));
            ((RectTransform)change.transform).Place(Vector2.zero, Vector2.zero, new Vector2(28, 20), new Vector2(228, 76));
            var go = LobbyKit.LabelButton(card, "GO", LobbyKit.Tomato, LobbyKit.Cream, 30, Menu.Go);
            ((RectTransform)go.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-264, 20), new Vector2(-24, 76));
            go.interactable = !blocked;
            First = blocked ? change : go;
        }
    }

    /// <summary>PRACTICE, MATCHMAKING or WORKSHOP, then a mode and a map, then GO back to the lobby.</summary>
    public sealed class PlayPage : LobbyPage
    {
        public override string Id => LobbyMenu.Play;

        static readonly (string id, string title, string body)[] Queues =
        {
            (LobbyMenu.Practice, "PRACTICE", "Every mode, with bots in the empty seats"),
            (LobbyMenu.Matchmaking, "MATCHMAKING", "Friends and other players, once online play is built"),
            (LobbyMenu.Workshop, "WORKSHOP", "Learn the controls or build a home"),
        };

        protected override void Build()
        {
            var panel = LobbyKit.Rect(Root, "Panel").Fill();
            panel.Paint(LobbyKit.Page, true);

            var queues = LobbyKit.Column(panel, "Queues", 12);
            queues.Place(Vector2.zero, new Vector2(0, 1), new Vector2(24, 24), new Vector2(304, -24));
            foreach (var (id, title, body) in Queues)
            {
                bool on = Menu.Queue == id;
                var card = Card(queues, "Queue " + id, title, body, null, on, () => { Menu.Choose(queue: id); Refresh(); Reselect("Queue " + id); });
                card.Size(-1, 128);
                if (on) First = card;
            }

            var main = LobbyKit.Rect(panel, "Choices").Place(Vector2.zero, Vector2.one, new Vector2(332, 136), new Vector2(-28, -20));
            var column = LobbyKit.Column(main, "Column", 10).Fill();
            LobbyKit.Heading(column, "Mode");
            var modes = LobbyKit.Row(column, "Modes", 14);
            modes.Size(-1, 196);
            foreach (var (mode, title, body, foot) in ModesFor(Menu.Queue))
            {
                bool on = Menu.Mode == mode;
                Card(modes, "Mode " + mode, title, body, foot, on, () => { Menu.Choose(mode: mode); Refresh(); Reselect("Mode " + mode); })
                    .Size(-1, -1, 1);
            }
            if (Menu.Mode != LobbyMenu.TutorialMode)
            {
                LobbyKit.Rect(column, "Gap").Size(-1, 6);
                LobbyKit.Heading(column, "Map");
                var maps = LobbyKit.Grid(column, "Maps", new Vector2(260, 204), 14);
                foreach (var house in GameConfig.Current.Houses)
                    MapCard(maps, house.Key, house.Value, Menu.Map == house.Key);
            }

            var bar = LobbyKit.Rect(panel, "Summary").Place(Vector2.zero, new Vector2(1, 0), new Vector2(332, 24), new Vector2(-28, 116));
            bar.Paint(LobbyKit.Card, true);
            string blocked = Menu.Blocked;
            var summary = Wrapped(bar, blocked ?? Menu.Describe(), 23, blocked == null ? LobbyKit.Cream : LobbyKit.Muted);
            summary.alignment = TextAlignmentOptions.MidlineLeft;
            summary.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(24, 6), new Vector2(-296, -6));
            var go = LobbyKit.LabelButton(bar, "GO", LobbyKit.Tomato, LobbyKit.Cream, 38, Menu.Go);
            ((RectTransform)go.transform).Place(new Vector2(1, 0), Vector2.one, new Vector2(-268, 10), new Vector2(-12, -10));
            go.interactable = blocked == null;
        }

        static IEnumerable<(string mode, string title, string body, string foot)> ModesFor(string queue)
        {
            if (queue == LobbyMenu.Workshop)
            {
                yield return (LobbyMenu.TutorialMode, "Play & learn", "No pressure, just wordplay. Smash, spell and craft step by step.", "Solo");
                yield return (LobbyMenu.WorkshopMode, "Creative Workshop", "Build your cozy home from the furniture you spell.", "Solo");
                yield break;
            }
            bool online = queue == LobbyMenu.Matchmaking;
            yield return ("Dibs", "Dibs", "A friendly scrap. Last roommate standing wins.", online ? "Free for all" : Capital(LobbyMenu.Seats("Dibs")));
            yield return ("Duos", "Duos", "Two teams, shared trouble. Revive your buddy.", online ? "2 v 2" : Capital(LobbyMenu.Seats("Duos")));
            yield return ("MovingDay", "Moving Day", "Spell the furniture and put everything in its room.", online ? "Co-op" : "Solo");
            yield return ("MovingOut", "Moving Out", "Rescue the keepsakes before the house clears out.", online ? "Co-op" : "Solo");
            if (online) yield return (LobbyMenu.RoomMode, "Private room", "Invite friends with a room code and pick the rules.", "Friends only");
        }

        void MapCard(Transform parent, string id, HouseLayout layout, bool on)
        {
            var card = LobbyKit.Button(parent, "Map " + id, on ? Chosen : LobbyKit.Card, () => { Menu.Choose(map: id); Refresh(); Reselect("Map " + id); });
            var t = card.transform;
            var plan = LobbyKit.Rect(t, "Plan").Place(Vector2.zero, Vector2.one, new Vector2(16, 52), new Vector2(-16, -14));
            FloorPlan(plan, layout, on ? LobbyKit.Honey : LobbyKit.Cream, new Vector2(228, 138));
            var name = LobbyKit.Text(t, layout.Name, 22, on ? LobbyKit.Honey : LobbyKit.Cream, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            name.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 14), new Vector2(-18, 0));
            if (on) LobbyKit.Rect(t, "Mark").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 5)).Paint(LobbyKit.Honey).raycastTarget = false;
        }

        /// <summary>Draws a map's rooms from above to fit a box. Upper floors are fainter.</summary>
        public static void FloorPlan(RectTransform box, HouseLayout layout, Color colour, Vector2 size)
        {
            if (layout.Rooms.Count == 0) return;
            float minX = layout.Rooms.Min(r => r.MinX), maxX = layout.Rooms.Max(r => r.MaxX);
            float minZ = layout.Rooms.Min(r => r.MinZ), maxZ = layout.Rooms.Max(r => r.MaxZ);
            float scale = Mathf.Min(size.x / Mathf.Max(1f, maxX - minX), size.y / Mathf.Max(1f, maxZ - minZ));
            var middle = new Vector2((minX + maxX) * .5f, (minZ + maxZ) * .5f);
            foreach (var room in layout.Rooms)
            {
                var centre = new Vector2((room.MinX + room.MaxX) * .5f, (room.MinZ + room.MaxZ) * .5f);
                var rect = LobbyKit.Rect(box, room.Name).Pin(new Vector2(.5f, .5f), (centre - middle) * scale,
                    new Vector2(room.MaxX - room.MinX, room.MaxZ - room.MinZ) * scale - new Vector2(3, 3));
                rect.Paint(new Color(colour.r, colour.g, colour.b, room.FloorY > .5f ? .14f : .32f)).raycastTarget = false;
            }
        }
    }

    /// <summary>The locker (what you wear and the finish on your gear) and every recipe you can spell.</summary>
    public sealed class LoadoutPage : LobbyPage
    {
        public const string FinishSlot = "Finish";
        public override string Id => LobbyMenu.Loadout;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;
        public bool ShowingRecipes { get; private set; }
        public string Slot { get; private set; } = "Top";

        protected override void Build()
        {
            var body = Side("Loadout");
            var tabs = LobbyKit.Row((RectTransform)body.parent, "Tabs", 8);
            tabs.Place(Vector2.one, Vector2.one, new Vector2(-376, -80), new Vector2(-28, -26));
            var locker = LobbyKit.Chip(tabs, "LOCKER", !ShowingRecipes, () => ShowRecipes(false));
            locker.Size(170, -1);
            LobbyKit.Chip(tabs, "RECIPES", ShowingRecipes, () => ShowRecipes(true)).Size(170, -1);
            if (ShowingRecipes) { First = locker; Recipes(body); }
            else Locker(body);
        }

        public void ShowRecipes(bool on)
        {
            ShowingRecipes = on;
            Refresh();
            Reselect(on ? "RECIPES" : "LOCKER");
        }

        public void Pick(string slot)
        {
            Slot = slot;
            Refresh();
            Reselect("Slot " + slot);
        }

        void Locker(RectTransform body)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var outfit = Menu.Outfit;
            var slots = LobbyKit.Column(body, "Slots", 6);
            slots.Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(230, 0));
            foreach (string slot in wardrobe.Slots.Append(FinishSlot))
            {
                string worn = slot == FinishSlot ? SkinOf(outfit) : outfit.PieceIn(slot) ?? "None";
                var colour = slot == FinishSlot || outfit.PieceIn(slot) == null ? null : wardrobe.ColourFor(outfit, slot);
                string title = slot == FinishSlot ? "Item finish" : slot;
                var tile = Tile(slots, "Slot " + slot, title, colour != null ? worn + " · " + colour.Name : worn, Slot == slot, () => Pick(slot));
                tile.Size(-1, 64);
                if (Slot == slot) First = tile;
            }
            var detail = LobbyKit.Column(body, "Detail", 10);
            detail.Place(Vector2.zero, Vector2.one, new Vector2(256, 0), Vector2.zero);
            if (Slot == FinishSlot) Finish(detail);
            else Styles(detail, Slot);
        }

        void Styles(RectTransform detail, string slot)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var outfit = Menu.Outfit;
            LobbyKit.Heading(detail, slot + " · style");
            var choices = wardrobe.Choices(outfit, slot);
            var chips = LobbyKit.Grid(detail, "Styles", new Vector2(170, 52), 10);
            foreach (string id in choices)
            {
                string label = id ?? "None";
                LobbyKit.Chip(chips, label, outfit.PieceIn(slot) == id, () =>
                {
                    var next = wardrobe.Wear(Menu.Outfit, slot, id);
                    if (next != null) Menu.Wear(next);
                    Refresh();
                    Reselect(label);
                });
            }
            if (choices.Count == 1 && wardrobe.RequiredSlots.Contains(slot))
                Wrapped(detail, "Every look wears these. Pick a colour below.", 18, LobbyKit.Muted, 30);
            var piece = wardrobe.Piece(outfit.PieceIn(slot) ?? "");
            if (piece == null) return;
            if (piece.ColourFrom != null)
            {
                Wrapped(detail, $"The {piece.Id.ToLowerInvariant()} matches your {piece.ColourFrom.ToLowerInvariant()} colour.", 18, LobbyKit.Muted, 30);
                return;
            }
            if (piece.TintMaterial == null || !wardrobe.Palettes.TryGetValue(slot, out var palette)) return;
            LobbyKit.Rect(detail, "Gap").Size(-1, 6);
            LobbyKit.Heading(detail, slot + " · colour");
            var current = wardrobe.ColourFor(outfit, slot);
            var swatches = LobbyKit.Grid(detail, "Colours", new Vector2(60, 60), 10);
            foreach (var colour in palette)
                Swatch(swatches, slot, colour, current?.Id == colour.Id, slot == "Top" && !Menu.Career.Owns("colour", colour.Id));
            if (current != null)
                Wrapped(detail, "Wearing " + current.Name + (slot == "Top" ? ". More top colours are in the shop." : "."), 18, LobbyKit.Muted, 30);
        }

        void Swatch(Transform parent, string slot, Colourway colour, bool on, bool locked)
        {
            var button = LobbyKit.Button(parent, "Colour " + colour.Id, on ? LobbyKit.Honey : LobbyKit.Line, () => PickColour(slot, colour, locked));
            var fill = LobbyKit.Rect(button.transform, "Fill").Place(Vector2.zero, Vector2.one, new Vector2(5, 5), new Vector2(-5, -5));
            fill.Paint(new Color(colour.R, colour.G, colour.B, locked ? .3f : 1f), true).raycastTarget = false;
            if (locked)
                LobbyKit.Icon(fill, LobbyIcons.Cart, LobbyKit.Cream).rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(12, 12), new Vector2(-12, -12));
            button.gameObject.AddComponent<LobbyHint>().Text = colour.Name + (locked ? " · in the shop" : "");
        }

        void PickColour(string slot, Colourway colour, bool locked)
        {
            if (locked)
            {
                Menu.Post(colour.Name + " is in the shop.");
                Menu.Open(LobbyMenu.Shop);
                return;
            }
            var next = Menu.Outfit.Clone();
            next.Colours[slot] = colour.Id;
            Menu.Wear(next);
            Refresh();
            Reselect("Colour " + colour.Id);
        }

        void Finish(RectTransform detail)
        {
            LobbyKit.Heading(detail, "Item finish");
            Wrapped(detail, "How the gear you craft looks in your hands. It never changes what the gear does.", 18, LobbyKit.Muted, 54);
            var chips = LobbyKit.Grid(detail, "Finishes", new Vector2(170, 52), 10);
            string worn = SkinOf(Menu.Outfit);
            foreach (string skin in Finishes)
            {
                bool owned = Menu.Career.Owns("skin", skin);
                var offer = Career.Shop.FirstOrDefault(o => o.Kind == "skin" && o.Value == skin);
                string label = owned || offer == null ? skin : skin + " · " + offer.Price;
                LobbyKit.Chip(chips, label, worn == skin, () =>
                {
                    if (!owned) { Menu.Post(skin + " gear is in the shop."); Menu.Open(LobbyMenu.Shop); return; }
                    Menu.Wear(WithFinish(Menu.Outfit, skin));
                    Refresh();
                    Reselect(label);
                }, !owned);
            }
        }

        void Recipes(RectTransform body)
        {
            var intro = Wrapped(body, "Smash furniture for letters, then spell one of these words to craft it.", 19, LobbyKit.Muted);
            intro.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -32), Vector2.zero);
            var view = LobbyKit.Rect(body, "Recipes").Place(Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0, -44));
            var list = LobbyKit.Scroll(view, "Scroll", 0);
            var grid = LobbyKit.Grid(list, "Grid", new Vector2(196, 196), 12);
            foreach (var item in GameConfig.Current.Items.Enabled) Recipe(grid, item);
        }

        static void Recipe(Transform parent, ItemDefinition item)
        {
            var card = LobbyKit.Rect(parent, "Recipe " + item.Id);
            card.Paint(LobbyKit.Card, true).raycastTarget = false;
            var texture = Resources.Load<Texture2D>("UI/Items/" + item.Id);
            if (texture)
            {
                var icon = LobbyKit.Rect(card, "Icon").Pin(new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(92, 92));
                var raw = icon.gameObject.AddComponent<RawImage>();
                raw.texture = texture; raw.raycastTarget = false;
            }
            var word = LobbyKit.Text(card, item.Id, 24, LobbyKit.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            word.characterSpacing = 6;
            word.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(8, 52), new Vector2(-8, 84));
            var blurb = Wrapped(card, Blurb(item.Family), 16, LobbyKit.Muted);
            blurb.alignment = TextAlignmentOptions.Top;
            blurb.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(10, 8), new Vector2(-10, 50));
        }

        /// <summary>One line per handling family, the same as the web edition's recipe book.</summary>
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

    /// <summary>Your level, totals and recent matches.</summary>
    public sealed class CareerScreen : LobbyPage
    {
        static readonly float[] Columns = { 0f, .19f, .41f, .54f, .66f, .76f, .85f, 1f };
        public override string Id => LobbyMenu.CareerPage;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;

        protected override void Build()
        {
            var career = Menu.Career;
            var body = Side("Career", "Practice matches count. Online play will add ranked results.");
            var column = LobbyKit.Column(body, "Column", 12).Fill();

            var level = LobbyKit.Row(column, "Level", 20);
            level.Size(-1, 96);
            var badge = LobbyKit.Rect(level, "Badge");
            badge.Paint(LobbyKit.Honey, true);
            badge.Size(96, 96);
            var number = LobbyKit.Text(badge, career.Level.ToString(CultureInfo.InvariantCulture), 46, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            number.rectTransform.Fill();
            var info = LobbyKit.Rect(level, "Info");
            info.Size(-1, -1, 1);
            var name = LobbyKit.Text(info, career.Name, 30, LobbyKit.Cream, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            name.rectTransform.Place(new Vector2(0, .5f), Vector2.one, Vector2.zero, new Vector2(0, -4));
            int next = Career.XpToNext(career.Level);
            var bar = LobbyKit.Rect(info, "XP bar").Place(new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(0, -16), new Vector2(0, -2));
            bar.Paint(LobbyKit.Line, true);
            LobbyKit.Rect(bar, "Fill").Place(Vector2.zero, new Vector2(Mathf.Clamp01(career.XpIntoLevel / (float)next), 1)).Paint(LobbyKit.Honey, true);
            var xp = LobbyKit.Text(info, $"Level {career.Level}  ·  {career.XpIntoLevel} / {next} XP to level {career.Level + 1}", 18, LobbyKit.Muted, TextAlignmentOptions.BottomLeft);
            xp.rectTransform.Place(Vector2.zero, new Vector2(1, .5f), new Vector2(0, 4), new Vector2(0, -22));

            var stats = LobbyKit.Row(column, "Stats", 12);
            stats.Size(-1, 96);
            Stat(stats, "Matches", career.Matches.ToString("N0", CultureInfo.InvariantCulture));
            Stat(stats, "Wins", career.Wins.ToString("N0", CultureInfo.InvariantCulture));
            Stat(stats, "Win rate", career.Matches == 0 ? "-" : Mathf.RoundToInt(100f * career.Wins / career.Matches) + "%");
            Stat(stats, "Coins", career.Coins.ToString("N0", CultureInfo.InvariantCulture));

            LobbyKit.Rect(column, "Gap").Size(-1, 4);
            LobbyKit.Heading(column, "Recent matches");
            Row(column, new[] { "MODE", "MAP", "RESULT", "SCORE", "COINS", "XP", "WHEN" }, LobbyKit.Muted, true, 0);
            if (career.History.Count == 0)
            {
                Wrapped(column, "No matches yet. Pick PLAY, then Practice, to start one.", 20, LobbyKit.Muted, 40);
                return;
            }
            var holder = Grow(LobbyKit.Rect(column, "History"));
            var list = LobbyKit.Scroll(holder, "Scroll", 2);
            for (int i = 0; i < career.History.Count; i++)
            {
                var m = career.History[i];
                Row(list, new[]
                {
                    LobbyMenu.ModeName(m.Mode), MapName(m.Map), m.Won ? "Won" : "Lost",
                    m.Score.ToString("N0", CultureInfo.InvariantCulture), "+" + m.Coins, "+" + m.Xp, When(m.EndedAt),
                }, m.Won ? LobbyKit.Cream : LobbyKit.Muted, false, i);
            }
        }

        static string When(long endedAt) => endedAt <= 0 || endedAt > Career.LatestTime ? "-" :
            DateTimeOffset.FromUnixTimeSeconds(endedAt).ToLocalTime().ToString("d MMM HH:mm", CultureInfo.InvariantCulture);

        static void Stat(Transform parent, string title, string value)
        {
            var tile = LobbyKit.Rect(parent, title);
            tile.Paint(LobbyKit.Card, true);
            tile.Size(-1, -1, 1);
            var v = LobbyKit.Text(tile, value, 34, LobbyKit.Cream, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            v.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-12, -12));
            var t = LobbyKit.Text(tile, LobbyKit.Upper(title), 15, LobbyKit.Muted, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            t.characterSpacing = 4;
            t.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 12), new Vector2(-12, 0));
        }

        static void Row(Transform parent, string[] cells, Color colour, bool header, int index)
        {
            var row = LobbyKit.Rect(parent, header ? "Header" : "Match");
            row.Size(-1, header ? 32 : 42);
            if (!header && index % 2 == 0) row.Paint(LobbyKit.Card, true).raycastTarget = false;
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = LobbyKit.Text(row, cells[i], header ? 15 : 19, colour,
                    i >= 3 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft, header ? FontStyles.Bold : FontStyles.Normal);
                cell.rectTransform.Place(new Vector2(Columns[i], 0), new Vector2(Columns[i + 1], 1), new Vector2(12, 0), new Vector2(-12, 0));
            }
        }
    }

    /// <summary>The cart: cosmetic finishes and top colours, bought with coins from matches.</summary>
    public sealed class ShopPage : LobbyPage
    {
        static readonly Dictionary<string, Color> FinishColours = new Dictionary<string, Color>
        {
            ["Classic"] = new Color(.85f, .74f, .58f),
            ["Candy"] = new Color(.96f, .55f, .72f),
            ["Arcade"] = new Color(.55f, .88f, .42f),
        };
        string note;

        public override string Id => LobbyMenu.Shop;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;

        protected override void Build()
        {
            var body = Side("Shop", "Looks only. Nothing here changes health, damage or speed.");
            var column = LobbyKit.Column(body, "Column", 12).Fill();
            var wallet = LobbyKit.Row(column, "Wallet", 10);
            wallet.Size(-1, 40);
            wallet.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            LobbyKit.Icon(wallet, LobbyIcons.Coin, LobbyKit.Honey).Size(30, 30);
            LobbyKit.Text(wallet, Menu.Career.Coins.ToString("N0", CultureInfo.InvariantCulture) + " coins", 24, LobbyKit.Cream,
                TextAlignmentOptions.MidlineLeft, FontStyles.Bold).Size(180, 36);
            LobbyKit.Text(wallet, note ?? "Finish matches to earn more.", 19, note != null ? LobbyKit.Honey : LobbyKit.Muted,
                TextAlignmentOptions.MidlineLeft).Size(-1, 36, 1);

            LobbyKit.Heading(column, "Item finishes");
            var finishes = LobbyKit.Grid(column, "Finishes", new Vector2(250, 150), 12);
            foreach (var offer in Career.Shop.Where(o => o.Kind == "skin")) Offer(finishes, offer);
            LobbyKit.Heading(column, "Top colours");
            var colours = LobbyKit.Grid(column, "Colours", new Vector2(250, 150), 12);
            foreach (var offer in Career.Shop.Where(o => o.Kind == "colour")) Offer(colours, offer);
            Wrapped(column, "Click a colour to try it on.", 18, LobbyKit.Muted, 30);
        }

        void Offer(Transform parent, ShopOffer offer)
        {
            bool owned = Menu.Career.Owns(offer.Kind, offer.Value);
            bool worn = offer.Kind == "skin" ? SkinOf(Menu.Outfit) == offer.Value : Menu.Outfit.ColourOf("Top") == offer.Value;
            var colourway = offer.Kind == "colour" ? GameConfig.Current.Wardrobe.Colour("Top", offer.Value) : null;
            var card = LobbyKit.Button(parent, "Offer " + offer.Id, worn ? Chosen : LobbyKit.Card, () => TryOn(colourway));
            var t = card.transform;
            var swatch = LobbyKit.Rect(t, "Swatch").Pin(new Vector2(0, 1), new Vector2(16, -16), new Vector2(52, 52));
            var shade = colourway != null ? new Color(colourway.R, colourway.G, colourway.B)
                : FinishColours.TryGetValue(offer.Value, out var c) ? c : LobbyKit.Cream;
            swatch.Paint(shade, true).raycastTarget = false;
            var name = LobbyKit.Text(t, offer.Name, 23, LobbyKit.Cream, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            name.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(82, 0), new Vector2(-12, -18));
            var kind = LobbyKit.Text(t, offer.Kind == "skin" ? "Item finish" : "Top colour", 16, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            kind.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(82, 0), new Vector2(-12, -48));
            Button action;
            if (worn) action = LobbyKit.LabelButton(t, "WEARING", LobbyKit.Line, LobbyKit.Muted, 19, null);
            else if (owned) action = LobbyKit.LabelButton(t, "WEAR", LobbyKit.Card, LobbyKit.Cream, 19, () => Wear(offer));
            else action = LobbyKit.LabelButton(t, "BUY  " + offer.Price.ToString("N0", CultureInfo.InvariantCulture), LobbyKit.Honey, Ink, 19, () => Buy(offer));
            action.name = (worn ? "Wearing " : owned ? "Wear " : "Buy ") + offer.Id;
            action.interactable = !worn;
            ((RectTransform)action.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(14, 14), new Vector2(-14, 62));
            if (!First && !worn) First = action;
        }

        void TryOn(Colourway colour)
        {
            if (colour == null) return;
            var preview = Menu.Outfit.Clone();
            preview.Colours["Top"] = colour.Id;
            Menu.Stage.Dress(preview);
        }

        public void Buy(ShopOffer offer)
        {
            string error = Menu.Career.Buy(offer.Id);
            if (error != null) note = error;
            else
            {
                note = "Bought " + offer.Name + ".";
                Menu.SaveCareer();
                Menu.Post(note);
            }
            Refresh();
            Reselect((error == null ? "Wear " : "Buy ") + offer.Id);
        }

        public void Wear(ShopOffer offer)
        {
            Outfit next;
            if (offer.Kind == "skin") next = WithFinish(Menu.Outfit, offer.Value);
            else { next = Menu.Outfit.Clone(); next.Colours["Top"] = offer.Value; }
            Menu.Wear(next);
            note = "Wearing " + offer.Name + ".";
            Refresh();
            // WEAR turns into a disabled WEARING, so a controller lands on the card instead.
            Reselect("Offer " + offer.Id);
        }
    }

    /// <summary>The leaderboard: your best score in each mode on this PC.</summary>
    public sealed class TrophyPage : LobbyPage
    {
        public override string Id => LobbyMenu.Trophy;

        protected override void Build()
        {
            var career = Menu.Career;
            var body = Panel(new Vector2(.14f, 0), new Vector2(.86f, 1), "Leaderboard",
                "Your best score in each mode. Online leaderboards arrive with online play.");
            var column = LobbyKit.Column(body, "Column", 12).Fill();
            foreach (string mode in LobbyMenu.Modes)
            {
                bool played = career.Bests.TryGetValue(mode, out int best);
                var row = LobbyKit.Row(column, "Best " + mode, 18, 16);
                row.Size(-1, 92);
                row.Paint(played ? Chosen : LobbyKit.Card, true);
                LobbyKit.Icon(row, LobbyIcons.Trophy, played ? LobbyKit.Honey : LobbyKit.Line).Size(56, -1);
                var who = LobbyKit.Rect(row, "Who");
                who.Size(-1, -1, 1);
                var title = LobbyKit.Text(who, LobbyMenu.ModeName(mode), 27, LobbyKit.Cream, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
                // Ellipsis drops a line that is taller than its box, and 27 pt is taller than half the row.
                title.rectTransform.Place(new Vector2(0, .5f), Vector2.one, Vector2.zero, new Vector2(0, 14));
                var by = LobbyKit.Text(who, played ? "#1  ·  " + career.Name + "  ·  level " + career.Level : "Not played yet", 18,
                    LobbyKit.Muted, TextAlignmentOptions.TopLeft);
                by.rectTransform.Place(Vector2.zero, new Vector2(1, .5f), new Vector2(0, 2), Vector2.zero);
                LobbyKit.Text(row, played ? best.ToString("N0", CultureInfo.InvariantCulture) : "-", 40,
                    played ? LobbyKit.Honey : LobbyKit.Muted, TextAlignmentOptions.MidlineRight, FontStyles.Bold).Size(220, -1);
            }
            Wrapped(column, $"{career.Matches} matches  ·  {career.Wins} wins  ·  level {career.Level}", 20, LobbyKit.Muted, 40);
        }
    }

    /// <summary>Profile, sound, display and the controls, in one place instead of icons in the bar.</summary>
    public sealed class SettingsPage : LobbyPage
    {
        public override string Id => LobbyMenu.Settings;

        // Unity switches full screen at the end of the frame, so the redraw straight after a click
        // would still read the old state; it shows what was asked for instead.
        bool? fullScreenAsked;

        protected override void Build()
        {
            var body = Panel(new Vector2(.12f, 0), new Vector2(.88f, 1), "Settings");
            var list = LobbyKit.Scroll(body, "Scroll", 8);
            LobbyKit.Heading(list, "Profile");
            NameField(Setting(list, "Name"));
            LobbyKit.Heading(list, "Audio");
            Toggle(Setting(list, "Sound"), "Sound", !GameFeedback.Muted, on => GameFeedback.Muted = !on);
            Stepper(Setting(list, "Volume"), "Volume", Mathf.RoundToInt(AudioListener.volume * 100) + "%", step =>
            {
                AudioListener.volume = Mathf.Clamp01(Mathf.Round(AudioListener.volume * 10f + step) / 10f);
                PlayerPrefs.SetFloat(LobbyMenu.VolumeKey, AudioListener.volume);
                PlayerPrefs.Save();
            });
            LobbyKit.Heading(list, "Display");
            bool fullScreen = fullScreenAsked ?? Screen.fullScreen;
            fullScreenAsked = null;
            Toggle(Setting(list, "Full screen"), "Full screen", fullScreen, on => { Screen.fullScreen = on; fullScreenAsked = on; });
            var names = QualitySettings.names;
            int quality = QualitySettings.GetQualityLevel();
            Stepper(Setting(list, "Quality"), "Quality", names.Length > 0 ? names[quality] : "-", step =>
            {
                if (names.Length == 0) return;
                // Each quality level carries its own v-sync; keep the player's choice instead.
                int vsync = QualitySettings.vSyncCount;
                QualitySettings.SetQualityLevel((quality + step + names.Length) % names.Length, true);
                QualitySettings.vSyncCount = vsync;
            });
            Toggle(Setting(list, "V-sync"), "V-sync", QualitySettings.vSyncCount > 0, on =>
            {
                QualitySettings.vSyncCount = on ? 1 : 0;
                PlayerPrefs.SetInt(LobbyMenu.VsyncKey, on ? 1 : 0);
                PlayerPrefs.Save();
            });
            LobbyKit.Heading(list, "Controls");
            foreach (var (what, keys) in new[]
            {
                ("Move", ControlHints.Move), ("Smash, throw, place", ControlHints.Attack), ("Pick up and revive", ControlHints.Grab),
                ("Spell and craft", ControlHints.Spell), ("Jump", ControlHints.Jump), ("Dodge", ControlHints.Dodge),
                ("Block", ControlHints.Block), ("Drop", ControlHints.Drop), ("Swap hands", ControlHints.Swap),
            })
            {
                var text = LobbyKit.Text(Setting(list, what), keys, 19, LobbyKit.Muted, TextAlignmentOptions.MidlineRight);
                text.Size(-1, -1, 1);
            }
        }

        static RectTransform Setting(Transform list, string label)
        {
            var row = LobbyKit.Rect(list, label);
            row.Size(-1, 58);
            row.Paint(LobbyKit.Card, true).raycastTarget = false;
            var text = LobbyKit.Text(row, label, 22, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft);
            text.rectTransform.Place(Vector2.zero, new Vector2(.32f, 1), new Vector2(20, 0), Vector2.zero);
            var controls = LobbyKit.Row(row, "Controls", 8);
            controls.Place(new Vector2(.32f, 0), Vector2.one, new Vector2(0, 7), new Vector2(-10, -7));
            controls.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            return controls;
        }

        void Toggle(RectTransform controls, string name, bool value, Action<bool> set)
        {
            foreach (bool option in new[] { true, false })
            {
                string id = name + (option ? " on" : " off");
                var chip = LobbyKit.Chip(controls, option ? "ON" : "OFF", value == option, () => { set(option); Refresh(); Reselect(id); });
                chip.name = id;
                chip.Size(96, -1);
                if (!First) First = chip;
            }
        }

        void Stepper(RectTransform controls, string name, string value, Action<int> step)
        {
            var less = LobbyKit.LabelButton(controls, "<", LobbyKit.Card, LobbyKit.Cream, 24, () => { step(-1); Refresh(); Reselect(name + " less"); });
            less.name = name + " less";
            less.Size(56, -1);
            LobbyKit.Text(controls, value, 21, LobbyKit.Cream, TextAlignmentOptions.Center).Size(200, -1);
            var more = LobbyKit.LabelButton(controls, ">", LobbyKit.Card, LobbyKit.Cream, 24, () => { step(1); Refresh(); Reselect(name + " more"); });
            more.name = name + " more";
            more.Size(56, -1);
        }

        void NameField(RectTransform controls)
        {
            var holder = LobbyKit.Rect(controls, "Name field");
            // Built inactive so the field finds its text when it first wakes up.
            holder.gameObject.SetActive(false);
            holder.Size(340, -1);
            holder.Paint(LobbyKit.Line, true);
            var area = LobbyKit.Rect(holder, "Text area").Place(Vector2.zero, Vector2.one, new Vector2(14, 2), new Vector2(-14, -2));
            area.gameObject.AddComponent<RectMask2D>();
            var text = LobbyKit.Text(area, "", 22, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft);
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.Fill();
            var field = holder.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.characterLimit = Career.NameLength;
            field.text = Menu.Career.Name;
            field.onEndEdit.AddListener(value =>
            {
                string trimmed = (value ?? "").Trim();
                if (trimmed.Length < 2 || trimmed == Menu.Career.Name) { field.text = Menu.Career.Name; return; }
                Menu.Career.Name = trimmed;
                Menu.SaveCareer();
                Menu.Post("You're now " + trimmed + ".");
            });
            holder.gameObject.SetActive(true);
        }
    }
}
