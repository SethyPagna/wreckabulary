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
    /// <summary>
    /// One lobby page. Only one shows at a time, and each redraws itself from the menu's state.
    /// Pages wear the web lobby's look (LobbyKit): navy panels with sun titles, posters for modes and
    /// maps, wood tiles, white cards with navy edges and hard drops. Rotations are in uGUI degrees,
    /// which turn the other way from CSS: the web's rotate(-8deg) is +8 here.
    /// </summary>
    public abstract class LobbyPage
    {
        protected static readonly string[] Finishes = { "Classic", "Candy", "Arcade" };
        /// <summary>The hard drop under cards on a solid navy page, where a navy one wouldn't show.</summary>
        protected static readonly Color Deep = LobbyKit.Hex(0x04052a);
        /// <summary>The web's see-through card on the navy page, made solid: a uGUI shadow draws under
        /// its card, so a see-through card would show its own drop through itself.</summary>
        protected static readonly Color BoardIdle = LobbyKit.Hex(0x292b5c), BoardHover = LobbyKit.Hex(0x3c3e6a);

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

        /// <summary>A web panel: navy with a light edge and a hard drop, its title in sun capitals. Returns the body to fill.</summary>
        protected RectTransform Panel(Vector2 min, Vector2 max, string title, string subtitle = null)
        {
            var panel = LobbyKit.Rect(Root, "Panel").Place(min, max);
            // Pages that cover you are solid; the side panels are glass over the map.
            var face = LobbyKit.Face(panel, Focus == LobbyStage.Focus.Centre ? LobbyKit.Page : LobbyKit.Panel, 24, LobbyKit.Line, 3, 7);
            // A click on the panel stays there instead of turning you.
            face.raycastTarget = true;
            var head = LobbyKit.Display(panel, LobbyKit.Upper(title), 44, LobbyKit.Sun, TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Drop);
            head.characterSpacing = 2;
            head.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(32, -86), new Vector2(-32, -18));
            float top = 100;
            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = LobbyKit.Text(panel, subtitle, 19, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
                sub.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(32, -124), new Vector2(-32, -92));
                top = 134;
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

        /// <summary>
        /// A web poster for a mode or a map: its colour running light to dark, a navy edge and a hard
        /// drop, lifting and leaning a little under the pointer. Put its content on its Body.
        /// </summary>
        protected static Button Poster(Transform parent, string name, Color colour, bool on, Action click)
        {
            var button = LobbyKit.Button(parent, name, Color.Lerp(colour, Color.white, .3f), click, 20, LobbyKit.Navy, 4, 6,
                Color.Lerp(colour, Color.black, .25f));
            var press = button.GetComponent<LobbyPress>();
            press.Lift = 5f; press.Tilt = 1f;
            if (on) Chosen(button.Body(), 20);
            return button;
        }

        /// <summary>The web's chosen mark: a sun ring just outside the card and a check on its corner.</summary>
        protected static void Chosen(RectTransform body, int radius)
        {
            LobbyKit.Ring(body, LobbyKit.Sun, radius, 5);
            var check = LobbyKit.Rect(body, "Check").Pin(Vector2.one, new Vector2(12, 12), new Vector2(38, 38));
            LobbyKit.Face(check, LobbyKit.Sun, 19, LobbyKit.Navy, 4);
            LobbyKit.Icon(check, LobbyIcons.Check, LobbyKit.Navy).rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
        }

        /// <summary>The small navy tag at a poster's foot ("2 v 2", "Solo").</summary>
        protected static void Tag(RectTransform body, string text)
        {
            var tag = LobbyKit.Row(body, "Foot", 0);
            tag.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(12, 12, 3, 3);
            tag.Pin(Vector2.zero, new Vector2(16, 14), new Vector2(0, 30));
            tag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tag.Paint(LobbyKit.Navy, 8).raycastTarget = false;
            LobbyKit.Text(tag, LobbyKit.Upper(text), 15, LobbyKit.Cream, TextAlignmentOptions.Center, FontStyles.Bold).characterSpacing = 2;
        }

        /// <summary>A queue choice: a glass card with a light edge, or a sun slab with navy type when chosen.</summary>
        protected static Button Board(Transform parent, string name, string title, string body, bool on, Action click)
        {
            var button = on
                ? LobbyKit.Button(parent, name, LobbyKit.SunHi, click, 16, LobbyKit.Navy, 3, 5, LobbyKit.Sun2)
                : LobbyKit.Button(parent, name, BoardIdle, click, 16, LobbyKit.ChipEdge, 2, 5);
            var t = button.Body();
            t.GetComponent<Shadow>().effectColor = Deep;
            var press = button.GetComponent<LobbyPress>();
            if (on) press.Lift = 0f;
            else
            {
                var face = t.GetComponent<Image>();
                press.Hot = hot => face.color = hot ? BoardHover : BoardIdle;
            }
            var head = LobbyKit.Display(t, title, 28, on ? LobbyKit.Navy : LobbyKit.Cream, TextAlignmentOptions.TopLeft);
            head.characterSpacing = 1;
            head.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-16, -16));
            var text = Wrapped(t, body, 17, on ? LobbyKit.CardSub : LobbyKit.Muted);
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(20, 12), new Vector2(-16, -54));
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
            var hint = LobbyKit.Row(Root, "Turn hint", 8, 0);
            hint.Pin(new Vector2(.5f, 0), new Vector2(0, 4), new Vector2(200, 40));
            var layout = hint.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            // Says what the open view does without getting in its way: Face leaves it click-through.
            LobbyKit.Face(hint, LobbyKit.TabIdle, 10, LobbyKit.Line, 2);
            LobbyKit.Icon(hint, LobbyIcons.Turn, LobbyKit.Cyan).Size(24, 24);
            LobbyKit.Text(hint, "Drag to turn", 19, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft).Size(126, 30);
            // While a match starts, the countdown under the tabs takes over.
            if (Menu.Starting) return;

            // The web's match dock: the mode's colour, its art, what GO starts and the GO button.
            bool blocked = Menu.Blocked != null;
            var colour = LobbyKit.ModeColour(Menu.Mode);
            var card = LobbyKit.Rect(Root, "Next match").Pin(Vector2.zero, Vector2.zero, new Vector2(560, 212));
            LobbyKit.Face(card, LobbyKit.Panel, 18, LobbyKit.Line, 3, 5).raycastTarget = true;
            LobbyKit.Rect(card, "Accent").Place(Vector2.zero, new Vector2(0, 1), new Vector2(14, 18), new Vector2(24, -18))
                .Paint(colour, 5).raycastTarget = false;
            var art = LobbyKit.Rect(card, "Art").Pin(new Vector2(0, 1), new Vector2(40, -20), new Vector2(76, 76));
            LobbyKit.Face(art, colour, 16, LobbyKit.Navy, 3);
            LobbyKit.ItemImage(art, LobbyKit.ModeArt(Menu.Mode), 66, 10f);
            var caps = LobbyKit.Caps(card, "Next up  ·  " + Menu.Queue, 15);
            caps.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(134, -44), new Vector2(-152, -20));
            var title = LobbyKit.Display(card, LobbyMenu.ModeName(Menu.Mode), 36, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft, LobbyKit.Ink.Stroke);
            // Clear of CHANGE in the corner; a long name shrinks instead.
            title.enableAutoSizing = true; title.fontSizeMin = 24; title.fontSizeMax = 36;
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(134, -88), new Vector2(-150, -42));
            var detail = LobbyKit.Text(card, (blocked ? "Not built yet  ·  " : "") + Menu.Describe(), 19, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            detail.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(134, -112), new Vector2(-20, -86));
            var change = LobbyKit.Pill(card, "CHANGE", "Change", 20, () => Menu.Open(LobbyMenu.Play));
            ((RectTransform)change.transform).Pin(Vector2.one, new Vector2(-18, -18), new Vector2(120, 40));
            var go = LobbyKit.Primary(card, "GO", "GO", Menu.Go, 58, 36);
            ((RectTransform)go.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(18, 20), new Vector2(-18, 98));
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
            var body = Panel(Vector2.zero, Vector2.one, "Play", "Pick a queue, a mode and a map, then press GO.");

            var queues = LobbyKit.Column(body, "Queues", 14);
            queues.Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(280, 0));
            foreach (var (id, title, text) in Queues)
            {
                bool on = Menu.Queue == id;
                var card = Board(queues, "Queue " + id, title, text, on, () => { Menu.Choose(queue: id); Refresh(); Reselect("Queue " + id); });
                card.Size(-1, 128);
                if (on) First = card;
            }

            var main = LobbyKit.Rect(body, "Choices").Place(Vector2.zero, Vector2.one, new Vector2(312, 112), Vector2.zero);
            var column = LobbyKit.Column(main, "Column", 10).Fill();
            LobbyKit.Heading(column, "Mode");
            var modes = LobbyKit.Row(column, "Modes", 16);
            modes.Size(-1, 196);
            foreach (var (mode, title, text, foot) in ModesFor(Menu.Queue, Menu.PartySize))
                ModePoster(modes, mode, title, text, foot, Menu.Mode == mode);
            if (Menu.Mode != LobbyMenu.TutorialMode)
            {
                LobbyKit.Rect(column, "Gap").Size(-1, 8);
                LobbyKit.Heading(column, "Map");
                var maps = LobbyKit.Grid(column, "Maps", new Vector2(260, 204), 18);
                int index = 0;
                foreach (var house in GameConfig.Current.Houses)
                    MapPoster(maps, house.Key, house.Value, Menu.Map == house.Key, index++);
            }

            var bar = LobbyKit.Rect(body, "Summary").Place(Vector2.zero, new Vector2(1, 0), new Vector2(312, 0), new Vector2(0, 92));
            LobbyKit.Face(bar, LobbyKit.ChipFill, 18, LobbyKit.Line, 2);
            string blocked = Menu.Blocked;
            var summary = Wrapped(bar, blocked ?? Menu.Describe(), 22, blocked == null ? LobbyKit.Cream : LobbyKit.Muted);
            summary.alignment = TextAlignmentOptions.MidlineLeft;
            summary.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(24, 6), new Vector2(-290, -6));
            var go = LobbyKit.Primary(bar, "GO", "GO", Menu.Go, 48, 30);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-268, -36), new Vector2(-12, 36));
            go.interactable = blocked == null;
        }

        static IEnumerable<(string mode, string title, string body, string foot)> ModesFor(string queue, int humans)
        {
            if (queue == LobbyMenu.Workshop)
            {
                yield return (LobbyMenu.TutorialMode, "Play & learn", "No pressure, just wordplay. Smash, spell and craft step by step.", Capital(LobbyMenu.Seats(LobbyMenu.TutorialMode, humans)));
                yield return (LobbyMenu.WorkshopMode, "Creative Workshop", "Build your cozy home from the furniture you spell.", "Solo");
                yield break;
            }
            bool online = queue == LobbyMenu.Matchmaking;
            yield return ("Dibs", "Dibs", "A friendly scrap. Last roommate standing wins.", online ? "Free for all" : Capital(LobbyMenu.Seats("Dibs", humans)));
            yield return ("Duos", "Duos", "Two teams, shared trouble. Revive your buddy.", online ? "2 v 2" : Capital(LobbyMenu.Seats("Duos", humans)));
            yield return ("MovingDay", "Moving Day", "Spell the furniture and put everything in its room.", online ? "Co-op" : Capital(LobbyMenu.Seats("MovingDay", humans)));
            yield return ("MovingOut", "Moving Out", "Rescue the keepsakes before the house clears out.", online ? "Co-op" : Capital(LobbyMenu.Seats("MovingOut", humans)));
            if (online) yield return (LobbyMenu.RoomMode, "Private room", "Invite friends with a room code and pick the rules.", "Friends only");
        }

        void ModePoster(Transform parent, string mode, string title, string text, string foot, bool on)
        {
            var card = Poster(parent, "Mode " + mode, LobbyKit.ModeColour(mode), on, () => { Menu.Choose(mode: mode); Refresh(); Reselect("Mode " + mode); });
            card.Size(-1, -1, 1);
            var body = card.Body();
            var art = LobbyKit.ItemImage(body, LobbyKit.ModeArt(mode), 84, 8f);
            if (art) art.rectTransform.Pin(new Vector2(1, 0), new Vector2(-6, 6), new Vector2(84, 84));
            var head = LobbyKit.Display(body, title, 30, LobbyKit.Cream, TextAlignmentOptions.TopLeft, LobbyKit.Ink.Stroke);
            head.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-14, -14));
            var line = Wrapped(body, text, 17, LobbyKit.Cream);
            line.overflowMode = TextOverflowModes.Ellipsis;
            line.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 70), new Vector2(-14, -54));
            if (foot != null) Tag(body, foot);
        }

        void MapPoster(Transform parent, string id, HouseLayout layout, bool on, int index)
        {
            // The web alternates its house cards between hot and lime.
            var card = Poster(parent, "Map " + id, index % 2 == 0 ? LobbyKit.Hot : LobbyKit.Lime, on,
                () => { Menu.Choose(map: id); Refresh(); Reselect("Map " + id); });
            var body = card.Body();
            var plan = LobbyKit.Rect(body, "Plan").Place(Vector2.zero, Vector2.one, new Vector2(22, 54), new Vector2(-22, -18));
            plan.localRotation = Quaternion.Euler(0, 0, 4f);
            FloorPlan(plan, layout, Color.white, new Vector2(216, 132));
            var name = LobbyKit.Display(body, layout.Name, 26, LobbyKit.Cream, TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Stroke);
            name.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 14), new Vector2(-14, 0));
        }

        /// <summary>Draws a map's rooms from above to fit a box, like the web's plans: rooms with navy
        /// walls, the garden green. Upper floors are fainter.</summary>
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
                var fill = room.Name.Contains("Garden") ? LobbyKit.Lime : room.Name.Contains("Hall") ? LobbyKit.Sun : colour;
                rect.Paint(new Color(fill.r, fill.g, fill.b, room.FloorY > .5f ? .5f : 1f), 3).raycastTarget = false;
                LobbyKit.Frame(rect, LobbyKit.Navy, 3, 3);
            }
        }
    }

    /// <summary>The locker (what you wear and the finish on your gear) and every recipe you can spell.</summary>
    public sealed class LoadoutPage : LobbyPage
    {
        public const string FinishSlot = "Finish";
        static readonly Color CocoaSoft = new Color(LobbyKit.Cocoa.r, LobbyKit.Cocoa.g, LobbyKit.Cocoa.b, .75f);
        public override string Id => LobbyMenu.Loadout;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;
        public bool ShowingRecipes { get; private set; }
        public string Slot { get; private set; } = "Top";

        protected override void Build()
        {
            var body = Side("Loadout");
            var tabs = LobbyKit.Row((RectTransform)body.parent, "Tabs", 10);
            tabs.Place(Vector2.one, Vector2.one, new Vector2(-380, -82), new Vector2(-30, -26));
            var row = tabs.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleRight;
            row.childForceExpandHeight = false;
            var locker = LobbyKit.Tab(tabs, "LOCKER", null, () => ShowRecipes(false), 170, 48, 22);
            locker.On = !ShowingRecipes;
            LobbyKit.Tab(tabs, "RECIPES", null, () => ShowRecipes(true), 170, 48, 22).On = ShowingRecipes;
            if (ShowingRecipes) { First = locker.Button; Recipes(body); }
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
            var slots = LobbyKit.Column(body, "Slots", 8);
            slots.Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(236, 0));
            foreach (string slot in wardrobe.Slots.Append(FinishSlot))
            {
                string worn = slot == FinishSlot ? SkinOf(outfit) : outfit.PieceIn(slot) ?? "None";
                var colour = slot == FinishSlot || outfit.PieceIn(slot) == null ? null : wardrobe.ColourFor(outfit, slot);
                string title = slot == FinishSlot ? "Item finish" : slot;
                var tile = SlotTile(slots, "Slot " + slot, title, colour != null ? worn + " · " + colour.Name : worn, Slot == slot, () => Pick(slot));
                tile.Size(-1, 64);
                if (Slot == slot) First = tile;
            }
            var detail = LobbyKit.Column(body, "Detail", 10);
            detail.Place(Vector2.zero, Vector2.one, new Vector2(266, 0), Vector2.zero);
            if (Slot == FinishSlot) Finish(detail);
            else Styles(detail, Slot);
        }

        /// <summary>The web's wood select: a honey slab with a cocoa edge and a chevron, ringed in sun when open.</summary>
        static Button SlotTile(Transform parent, string name, string title, string sub, bool on, Action click)
        {
            var button = LobbyKit.Button(parent, name, LobbyKit.WoodHi, click, 12, LobbyKit.Cocoa, 3, 3, LobbyKit.WoodLo);
            var t = button.Body();
            if (on) LobbyKit.Ring(t, LobbyKit.Sun, 12, 4);
            var head = LobbyKit.Display(t, title, 22, LobbyKit.Cocoa, TextAlignmentOptions.BottomLeft);
            head.rectTransform.Place(new Vector2(0, .5f), Vector2.one, new Vector2(16, -2), new Vector2(-36, -4));
            var line = LobbyKit.Text(t, sub, 16, CocoaSoft, TextAlignmentOptions.TopLeft);
            line.rectTransform.Place(Vector2.zero, new Vector2(1, .5f), new Vector2(16, 6), new Vector2(-36, -2));
            LobbyKit.Icon(t, LobbyIcons.Chevron, LobbyKit.Cocoa).rectTransform.Pin(new Vector2(1, .5f), new Vector2(-12, 0), new Vector2(18, 18));
            return button;
        }

        void Styles(RectTransform detail, string slot)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var outfit = Menu.Outfit;
            LobbyKit.Heading(detail, slot + " · style");
            var choices = wardrobe.Choices(outfit, slot);
            var chips = LobbyKit.Grid(detail, "Styles", new Vector2(170, 52), 12);
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
            // Room for a chosen swatch's ring and bigger size without touching its neighbours.
            var swatches = LobbyKit.Grid(detail, "Colours", new Vector2(60, 60), 14);
            foreach (var colour in palette)
                Swatch(swatches, slot, colour, current?.Id == colour.Id, slot == "Top" && !Menu.Career.Owns("colour", colour.Id));
            if (current != null)
                Wrapped(detail, "Wearing " + current.Name + (slot == "Top" ? ". More top colours are in the shop." : "."), 18, LobbyKit.Muted, 30);
        }

        /// <summary>The web's colour swatch: a navy-rimmed blob with a shine, greyed with a lock when it's in the shop.</summary>
        void Swatch(Transform parent, string slot, Colourway colour, bool on, bool locked)
        {
            var button = LobbyKit.Button(parent, "Colour " + colour.Id, LobbyKit.Navy, () => PickColour(slot, colour, locked), 24, null, 0, 3);
            var body = button.Body();
            var shade = new Color(colour.R, colour.G, colour.B);
            if (locked)
            {
                float grey = shade.grayscale;
                shade = Color.Lerp(new Color(grey, grey, grey), shade, .4f) * .85f;
                shade.a = 1f;
            }
            var fill = LobbyKit.Rect(body, "Fill").Place(Vector2.zero, Vector2.one, new Vector2(3, 3), new Vector2(-3, -3));
            fill.Paint(shade, 21).raycastTarget = false;
            LobbyKit.Rect(fill, "Shine").Pin(new Vector2(.32f, .72f), Vector2.zero, new Vector2(12, 10)).Paint(new Color(1f, 1f, 1f, .67f), 5).raycastTarget = false;
            if (locked)
            {
                var glyph = LobbyKit.Icon(fill, LobbyIcons.Lock, LobbyKit.Cream);
                glyph.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(14, 14), new Vector2(-14, -14));
                LobbyKit.Drop(glyph.rectTransform, 2, LobbyKit.Navy);
            }
            var press = button.GetComponent<LobbyPress>();
            press.HoverScale = 1.12f; press.Tilt = 8f;
            if (on) { press.Scale = 1.14f; LobbyKit.Ring(body, LobbyKit.Sun, 24, 4); }
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
            var chips = LobbyKit.Grid(detail, "Finishes", new Vector2(170, 52), 12);
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
            // Room inside the mask for a card that lifts and its drop.
            list.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 8, 12);
            var grid = LobbyKit.Grid(list, "Grid", new Vector2(196, 196), 14);
            foreach (var item in GameConfig.Current.Items.Enabled) Recipe(grid, item);
        }

        /// <summary>The web's recipe card: white with a navy edge, the item, its word in wood tiles and a line about it.</summary>
        static void Recipe(Transform parent, ItemDefinition item)
        {
            var card = LobbyKit.Rect(parent, "Recipe " + item.Id);
            // A still, invisible hit box, so the card can lift under the pointer like the web's.
            card.Paint(Color.clear);
            var body = LobbyKit.Rect(card, "Body").Fill();
            LobbyKit.Face(body, Color.white, 16, LobbyKit.Navy, 3, 4);
            var press = card.gameObject.AddComponent<LobbyPress>();
            press.Body = body; press.Drop = body.GetComponent<Shadow>(); press.DropRest = 4f;
            press.Lift = 4f; press.Tilt = 2f; press.Sink = 0f;
            var icon = LobbyKit.ItemImage(body, item.Id, 80);
            if (icon) icon.rectTransform.Pin(new Vector2(.5f, 1), new Vector2(0, -14), new Vector2(80, 80));
            string word = item.Id;
            float tile = Mathf.Min(26f, (180f - 3f * (word.Length - 1)) / word.Length);
            var letters = LobbyKit.Row(body, "Letters", 3);
            letters.Place(Vector2.zero, new Vector2(1, 0), new Vector2(8, 52), new Vector2(-8, 52 + tile + 4));
            var row = letters.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandHeight = false;
            foreach (char letter in word) LobbyKit.LetterTile(letters, letter, tile);
            var blurb = Wrapped(body, Blurb(item.Family), 15, LobbyKit.CardSub);
            blurb.alignment = TextAlignmentOptions.Top;
            blurb.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(10, 8), new Vector2(-10, 46));
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
            var column = LobbyKit.Column(body, "Column", 14).Fill();

            var level = LobbyKit.Row(column, "Level", 22);
            level.Size(-1, 96);
            // Your level on a wood tile, like a letter from the game.
            LobbyKit.LetterTile(level, career.Level.ToString(CultureInfo.InvariantCulture), 96).name = "Badge";
            var info = LobbyKit.Rect(level, "Info");
            info.Size(-1, -1, 1);
            var name = LobbyKit.Display(info, career.Name, 32, LobbyKit.Cream, TextAlignmentOptions.BottomLeft);
            name.rectTransform.Place(new Vector2(0, .5f), Vector2.one, new Vector2(0, 4), Vector2.zero);
            int next = Career.XpToNext(career.Level);
            var bar = LobbyKit.Rect(info, "XP bar").Place(new Vector2(0, .5f), new Vector2(1, .5f), new Vector2(0, -16), new Vector2(0, 0));
            bar.Paint(LobbyKit.Line, 8).raycastTarget = false;
            var fill = LobbyKit.Rect(bar, "Fill").Place(Vector2.zero, new Vector2(Mathf.Clamp01(career.XpIntoLevel / (float)next), 1)).Paint(Color.white, 8);
            fill.raycastTarget = false;
            LobbyKit.Gradient(fill, LobbyKit.SunHi, LobbyKit.Sun2);
            LobbyKit.Frame(bar, LobbyKit.Navy, 8, 2);
            var xp = LobbyKit.Text(info, $"Level {career.Level}  ·  {career.XpIntoLevel} / {next} XP to level {career.Level + 1}", 18, LobbyKit.Muted, TextAlignmentOptions.BottomLeft);
            xp.rectTransform.Place(Vector2.zero, new Vector2(1, .5f), new Vector2(0, 4), new Vector2(0, -22));

            var stats = LobbyKit.Row(column, "Stats", 14);
            stats.Size(-1, 96);
            Stat(stats, "Matches", career.Matches.ToString("N0", CultureInfo.InvariantCulture));
            Stat(stats, "Wins", career.Wins.ToString("N0", CultureInfo.InvariantCulture));
            Stat(stats, "Win rate", career.Matches == 0 ? "-" : Mathf.RoundToInt(100f * career.Wins / career.Matches) + "%");
            Stat(stats, "Coins", career.Coins.ToString("N0", CultureInfo.InvariantCulture));

            LobbyKit.Rect(column, "Gap").Size(-1, 4);
            LobbyKit.Heading(column, "Recent matches");
            Row(column, new[] { "MODE", "MAP", "RESULT", "SCORE", "COINS", "XP", "WHEN" }, LobbyKit.Cyan, true, 0);
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

        /// <summary>A white stat card: the number in navy display type over a small caption.</summary>
        static void Stat(Transform parent, string title, string value)
        {
            var tile = LobbyKit.Rect(parent, title);
            LobbyKit.Face(tile, Color.white, 16, LobbyKit.Navy, 3, 4);
            tile.Size(-1, -1, 1);
            var v = LobbyKit.Display(tile, value, 36, LobbyKit.Navy, TextAlignmentOptions.TopLeft);
            v.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 0), new Vector2(-12, -12));
            var caption = LobbyKit.Caps(tile, title, 14, TextAlignmentOptions.BottomLeft);
            caption.color = LobbyKit.CardSub;
            caption.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 14), new Vector2(-12, 0));
        }

        static void Row(Transform parent, string[] cells, Color colour, bool header, int index)
        {
            var row = LobbyKit.Rect(parent, header ? "Header" : "Match");
            row.Size(-1, header ? 30 : 42);
            if (!header && index % 2 == 0) row.Paint(LobbyKit.Mist(.06f), 10).raycastTarget = false;
            for (int i = 0; i < cells.Length; i++)
            {
                var align = i >= 3 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                var cell = header ? LobbyKit.Caps(row, cells[i], 14, align) : LobbyKit.Text(row, cells[i], 19, colour, align);
                cell.rectTransform.Place(new Vector2(Columns[i], 0), new Vector2(Columns[i + 1], 1), new Vector2(12, 0), new Vector2(-12, 0));
            }
        }
    }

    /// <summary>The cart: cosmetic finishes and top colours, bought with coins from matches.</summary>
    public sealed class ShopPage : LobbyPage
    {
        string note;

        public override string Id => LobbyMenu.Shop;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;

        /// <summary>Each finish's colours, as the web's skin art paints them.</summary>
        static (Color from, Color to) FinishPaint(string skin) => skin switch
        {
            "Candy" => (LobbyKit.Hex(0xff7ac8), LobbyKit.Hex(0x7fe3ff)),
            "Arcade" => (LobbyKit.Hex(0x3a1fd1), LobbyKit.Hex(0x00e0c6)),
            _ => (LobbyKit.WoodHi, LobbyKit.WoodLo),
        };

        protected override void Build()
        {
            var body = Side("Shop", "Looks only. Nothing here changes health, damage or speed.");
            var column = LobbyKit.Column(body, "Column", 12).Fill();
            var wallet = LobbyKit.Row(column, "Wallet", 14);
            wallet.Size(-1, 50);
            wallet.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            var chip = LobbyKit.Row(wallet, "Coin chip", 10);
            var chipLayout = chip.GetComponent<HorizontalLayoutGroup>();
            chipLayout.padding = new RectOffset(10, 18, 5, 5);
            chipLayout.childForceExpandHeight = false;
            LobbyKit.Face(chip, LobbyKit.ChipFill, 10, LobbyKit.ChipEdge, 2);
            chip.Size(-1, 48);
            LobbyKit.Coin(chip, 32);
            LobbyKit.Display(chip, Menu.Career.Coins.ToString("N0", CultureInfo.InvariantCulture), 26, LobbyKit.Sun, TextAlignmentOptions.MidlineLeft).Size(-1, 36);
            LobbyKit.Text(wallet, note ?? "Finish matches to earn more.", 19, note != null ? LobbyKit.Sun : LobbyKit.Muted,
                TextAlignmentOptions.MidlineLeft).Size(-1, 36, 1);

            LobbyKit.Heading(column, "Item finishes");
            var finishes = LobbyKit.Grid(column, "Finishes", new Vector2(270, 172), 12);
            foreach (var offer in Career.Shop.Where(o => o.Kind == "skin")) Offer(finishes, offer);
            LobbyKit.Heading(column, "Top colours");
            var colours = LobbyKit.Grid(column, "Colours", new Vector2(270, 172), 12);
            foreach (var offer in Career.Shop.Where(o => o.Kind == "colour")) Offer(colours, offer);
            Wrapped(column, "Click a colour to try it on.", 18, LobbyKit.Muted, 30);
        }

        /// <summary>The web's shop card: white with a navy edge, the art in a wood well, the name and a price tag.</summary>
        void Offer(Transform parent, ShopOffer offer)
        {
            bool owned = Menu.Career.Owns(offer.Kind, offer.Value);
            bool worn = offer.Kind == "skin" ? SkinOf(Menu.Outfit) == offer.Value : Menu.Outfit.ColourOf("Top") == offer.Value;
            var colourway = offer.Kind == "colour" ? GameConfig.Current.Wardrobe.Colour("Top", offer.Value) : null;
            var card = LobbyKit.Button(parent, "Offer " + offer.Id, Color.white, () => TryOn(colourway), 18, LobbyKit.Navy, 3, 5);
            var press = card.GetComponent<LobbyPress>();
            press.Lift = 4f; press.Tilt = 1f;
            var t = card.Body();
            if (worn) LobbyKit.Ring(t, LobbyKit.Sun, 18, 5);
            var well = LobbyKit.Rect(t, "Art").Pin(new Vector2(0, 1), new Vector2(14, -14), new Vector2(76, 76));
            LobbyKit.Face(well, LobbyKit.WoodHi, 12, null, 0, 0, LobbyKit.WoodLo);
            if (colourway != null) Blob(well, new Color(colourway.R, colourway.G, colourway.B));
            else FinishDisc(well, offer.Value);
            var name = LobbyKit.Display(t, LobbyKit.Upper(offer.Name), 22, LobbyKit.Navy, TextAlignmentOptions.TopLeft);
            name.enableAutoSizing = true; name.fontSizeMin = 14; name.fontSizeMax = 22;
            name.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(104, -46), new Vector2(-12, -16));
            var kind = LobbyKit.Text(t, offer.Kind == "skin" ? "Item finish" : "Top colour", 15, LobbyKit.CardSub, TextAlignmentOptions.TopLeft);
            kind.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(104, -72), new Vector2(-12, -48));
            var action = worn ? Stamp(t, offer) : owned ? WearChip(t, offer) : PriceTag(t, offer);
            action.interactable = !worn;
            ((RectTransform)action.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(14, 14), new Vector2(-14, 60));
            if (!First && !worn) First = action;
        }

        /// <summary>A colour offer's paint blob: the colour in a navy rim, with a shine.</summary>
        static void Blob(RectTransform well, Color colour)
        {
            var blob = LobbyKit.Rect(well, "Swatch").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(46, 46));
            LobbyKit.Face(blob, colour, 23, LobbyKit.Navy, 3);
            LobbyKit.Rect(blob, "Shine").Pin(new Vector2(.32f, .72f), Vector2.zero, new Vector2(10, 8)).Paint(new Color(1f, 1f, 1f, .67f), 4).raycastTarget = false;
        }

        /// <summary>A finish offer's art: a disc in the finish's colours with a BAT on it.</summary>
        static void FinishDisc(RectTransform well, string skin)
        {
            var (from, to) = FinishPaint(skin);
            var disc = LobbyKit.Rect(well, "Finish").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(56, 56));
            LobbyKit.Face(disc, from, 28, LobbyKit.Navy, 3, 0, to);
            LobbyKit.ItemImage(disc, "BAT", 42, 20f);
        }

        /// <summary>The web's price tag: a sun slab with a coin and the price. Short of coins it greys
        /// but still answers, so the shop can say why.</summary>
        Button PriceTag(RectTransform card, ShopOffer offer)
        {
            bool poor = Menu.Career.Coins < offer.Price;
            var button = LobbyKit.Button(card, "Buy " + offer.Id, poor ? LobbyKit.Short : LobbyKit.Sun, () => Buy(offer), 10, LobbyKit.Navy, 3, 3);
            button.GetComponent<LobbyPress>().Tilt = 2f;
            var content = LobbyKit.Row(button.Body(), "Content", 8);
            content.Fill();
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandHeight = false;
            var ink = poor ? new Color(LobbyKit.Navy.r, LobbyKit.Navy.g, LobbyKit.Navy.b, .5f) : LobbyKit.Navy;
            LobbyKit.Display(content, "BUY", 22, ink).Size(-1, 30);
            LobbyKit.Coin(content, 26);
            LobbyKit.Display(content, offer.Price.ToString("N0", CultureInfo.InvariantCulture), 22, ink).Size(-1, 30);
            return button;
        }

        Button WearChip(RectTransform card, ShopOffer offer)
        {
            var button = LobbyKit.Button(card, "Wear " + offer.Id, Color.white, () => Wear(offer), 10, LobbyKit.Navy, 3, 3);
            LobbyKit.Display(button.Body(), "WEAR", 22, LobbyKit.Navy).rectTransform.Fill();
            var face = button.FaceOf();
            button.GetComponent<LobbyPress>().Hot = hot => face.color = hot ? LobbyKit.Sun : Color.white;
            return button;
        }

        /// <summary>The web's owned stamp: green outline and type, turned a little, and never faded.</summary>
        static Button Stamp(RectTransform card, ShopOffer offer)
        {
            var button = LobbyKit.Button(card, "Wearing " + offer.Id, Color.clear, null, -1);
            button.GetComponent<LobbyPress>().FadeOff = false;
            var stamp = LobbyKit.Rect(button.Body(), "Stamp").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(150, 38));
            stamp.localRotation = Quaternion.Euler(0, 0, 8f);
            LobbyKit.Frame(stamp, LobbyKit.Owned, 8, 3);
            var label = LobbyKit.Display(stamp, "WEARING", 20, LobbyKit.Owned);
            label.characterSpacing = 4;
            label.rectTransform.Fill();
            return button;
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
            // WEAR turns into a WEARING stamp, so a controller lands on the card instead.
            Reselect("Offer " + offer.Id);
        }
    }

    /// <summary>The leaderboard: your best score in each mode on this PC.</summary>
    public sealed class TrophyPage : LobbyPage
    {
        static readonly Color GoldFrom = LobbyKit.Hex(0xffe14d), GoldTo = LobbyKit.Hex(0xfff7c2);
        static readonly Color RankHi = LobbyKit.Hex(0xfff4a8), RankLo = LobbyKit.Hex(0xe09a00);

        public override string Id => LobbyMenu.Trophy;

        protected override void Build()
        {
            var career = Menu.Career;
            var body = Panel(new Vector2(.14f, 0), new Vector2(.86f, 1), "Leaderboard",
                "Your best score in each mode. Online leaderboards arrive with online play.");
            // Padding leaves room for the outline round your rows.
            var column = LobbyKit.Column(body, "Column", 14, 6).Fill();
            foreach (string mode in LobbyMenu.Modes)
            {
                bool played = career.Bests.TryGetValue(mode, out int best);
                var row = LobbyKit.Row(column, "Best " + mode, 18, 14);
                row.Size(-1, 88);
                if (played)
                {
                    // The web's first place: a gold row, ringed in hot orange because it's yours.
                    LobbyKit.Face(row, GoldFrom, 14, LobbyKit.Navy, 3, 3, GoldTo).GetComponent<LobbyGradient>().Set(GoldFrom, GoldTo, true);
                    LobbyKit.Frame(row, LobbyKit.Hot, 14, 3, "You", 4);
                }
                else
                {
                    row.Paint(LobbyKit.Card, 14).raycastTarget = false;
                    LobbyKit.Frame(row, LobbyKit.Line, 14, 2);
                }
                var rank = LobbyKit.Rect(row, "Rank");
                rank.Size(60, 60);
                if (played) LobbyKit.Face(rank, RankHi, 12, LobbyKit.Navy, 3, 0, RankLo);
                else rank.Paint(LobbyKit.Card, 12).raycastTarget = false;
                LobbyKit.Display(rank, played ? "1" : "-", 34, played ? LobbyKit.Navy : LobbyKit.Faded).rectTransform.Fill();
                var who = LobbyKit.Rect(row, "Who");
                who.Size(-1, -1, 1);
                var title = LobbyKit.Display(who, LobbyMenu.ModeName(mode), 28, played ? LobbyKit.Navy : LobbyKit.Cream, TextAlignmentOptions.BottomLeft);
                // Ellipsis drops a line that is taller than its box, so the title box reaches above the row's middle.
                title.rectTransform.Place(new Vector2(0, .5f), Vector2.one, new Vector2(0, -2), new Vector2(0, 14));
                var by = LobbyKit.Text(who, played ? "#1  ·  " + career.Name + "  ·  level " + career.Level : "Not played yet", 17,
                    played ? LobbyKit.CardSub : LobbyKit.Faded, TextAlignmentOptions.TopLeft);
                by.rectTransform.Place(Vector2.zero, new Vector2(1, .5f), Vector2.zero, new Vector2(0, -2));
                LobbyKit.Display(row, played ? best.ToString("N0", CultureInfo.InvariantCulture) : "-", 40,
                    played ? LobbyKit.Navy : LobbyKit.Faded, TextAlignmentOptions.MidlineRight).Size(220, -1);
            }

            // The web's board-best bar.
            var bar = LobbyKit.Row(column, "Board best", 14, 14);
            bar.Size(-1, 64);
            bar.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            LobbyKit.Face(bar, LobbyKit.Navy, 14, LobbyKit.Line, 2);
            LobbyKit.Icon(bar, LobbyIcons.Trophy, LobbyKit.Sun).Size(32, 32);
            LobbyKit.Text(bar, $"{career.Matches} matches  ·  {career.Wins} wins  ·  level {career.Level}", 19, LobbyKit.Muted,
                TextAlignmentOptions.MidlineLeft).Size(-1, 30, 1);
            LobbyKit.Caps(bar, "Board best", 14, TextAlignmentOptions.MidlineRight).Size(150, 30);
            string top = career.Bests.Count > 0 ? career.Bests.Values.Max().ToString("N0", CultureInfo.InvariantCulture) : "-";
            LobbyKit.Display(bar, top, 30, LobbyKit.Sun, TextAlignmentOptions.MidlineRight).Size(140, 40);
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
            row.Paint(LobbyKit.Card, 14).raycastTarget = false;
            var text = LobbyKit.Display(row, label, 24, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft);
            text.characterSpacing = 1;
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
            Round(controls, name + " less", true, () => { step(-1); Refresh(); Reselect(name + " less"); });
            LobbyKit.Display(controls, value, 24, LobbyKit.Sun, TextAlignmentOptions.Center).Size(200, -1);
            Round(controls, name + " more", false, () => { step(1); Refresh(); Reselect(name + " more"); });
        }

        /// <summary>A round white step button with a navy chevron that turns sun under the pointer.</summary>
        static void Round(RectTransform controls, string name, bool back, Action click)
        {
            var button = LobbyKit.Button(controls, name, Color.white, click, 22, LobbyKit.Navy, 3, 3);
            button.Size(44, 44);
            var glyph = LobbyKit.Icon(button.Body(), LobbyIcons.Chevron, LobbyKit.Navy);
            glyph.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(11, 11), new Vector2(-11, -11));
            if (back) glyph.rectTransform.localRotation = Quaternion.Euler(0, 0, 180f);
            var face = button.FaceOf();
            button.GetComponent<LobbyPress>().Hot = hot => face.color = hot ? LobbyKit.Sun : Color.white;
        }

        void NameField(RectTransform controls)
        {
            var holder = LobbyKit.Rect(controls, "Name field");
            // Built inactive so the field finds its text when it first wakes up.
            holder.gameObject.SetActive(false);
            holder.Size(340, -1);
            holder.Paint(Color.white, 10);
            LobbyKit.Frame(holder, LobbyKit.Navy, 10, 3);
            var area = LobbyKit.Rect(holder, "Text area").Place(Vector2.zero, Vector2.one, new Vector2(14, 2), new Vector2(-14, -2));
            area.gameObject.AddComponent<RectMask2D>();
            var text = LobbyKit.Text(area, "", 22, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            text.overflowMode = TextOverflowModes.Overflow;
            text.rectTransform.Fill();
            var field = holder.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = area;
            field.textComponent = text;
            field.characterLimit = Career.NameLength;
            // The caret colour is ignored unless it is marked custom.
            field.customCaretColor = true;
            field.caretColor = LobbyKit.Navy;
            field.caretWidth = 2;
            field.selectionColor = new Color(LobbyKit.Sun.r, LobbyKit.Sun.g, LobbyKit.Sun.b, .5f);
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
