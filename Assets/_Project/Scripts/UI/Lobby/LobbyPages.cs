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
    /// Pages wear the web lobby's look (LobbyKit): royal panels with sun titles, posters for modes and
    /// maps, wood tiles, cards with navy edges and hard drops. Rotations are in uGUI degrees,
    /// which turn the other way from CSS: the web's rotate(-8deg) is +8 here.
    /// </summary>
    public abstract class LobbyPage
    {
        protected static readonly string[] Finishes = { "Classic", "Candy", "Arcade" };
        /// <summary>The hard drop under cards on a solid navy page, where a navy one wouldn't show.</summary>
        protected static readonly Color Deep = LobbyKit.Hex(0x04052a);
        /// <summary>The web's see-through white card on the royal panel, made solid: a uGUI shadow draws
        /// under its card, so a see-through card would show its own drop through itself.</summary>
        protected static readonly Color BoardIdle = LobbyKit.Hex(0x445eeb), BoardHover = LobbyKit.Hex(0x556ded);
        /// <summary>The width of the panel the loadout, career and shop share.</summary>
        public const float SideWidth = 860f;

        public abstract string Id { get; }
        public virtual LobbyStage.Focus Focus => LobbyStage.Focus.Centre;
        /// <summary>True when the lobby messages show beside this page.</summary>
        public virtual bool ShowFeed => false;
        /// <summary>A centred page: a click in the empty space round its panel closes it.</summary>
        public virtual bool Modal => false;
        /// <summary>How wide a side page's panel is; the stage stands you in the middle of the room left of it.</summary>
        public virtual float PanelWidth => SideWidth;
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

        /// <summary>The page's entrance when it opens: side pages slide in from the right, the others rise a
        /// little, both fading in. Home just appears.</summary>
        public void Pop()
        {
            if (Id == LobbyMenu.Home) return;
            LobbyPop.On(Root).Play(Focus == LobbyStage.Focus.Left ? new Vector2(24, 0) : new Vector2(0, -16));
        }

        /// <summary>Called when the page opens from another one, before it draws: where it forgets what it
        /// showed last time.</summary>
        public virtual void Opened() { }

        /// <summary>Redraws the page from the menu's current state.</summary>
        public void Refresh()
        {
            LobbyKit.Clear(Root);
            First = null;
            if (Modal)
            {
                // Under the panel: clicks on the panel never reach it.
                var shade = LobbyKit.Rect(Root, "Shade").Fill();
                shade.Paint(LobbyKit.Shade);
                shade.gameObject.AddComponent<LobbyShade>().Clicked = Menu.Close;
            }
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

        /// <summary>A web panel: the royal face with a navy edge and a hard navy drop, its title in sun capitals.
        /// Returns the body to fill.</summary>
        protected RectTransform Panel(Vector2 min, Vector2 max, string title, string subtitle = null)
        {
            var panel = LobbyKit.Rect(Root, "Panel").Place(min, max);
            // Solid, so neither the map nor its own drop shows through; a click on it stays there instead of turning you.
            LobbyKit.PanelFace(panel, 28, 5, 8).raycastTarget = true;
            var head = LobbyKit.Display(panel, LobbyKit.Upper(title), 43, LobbyKit.Sun, TextAlignmentOptions.BottomLeft, LobbyKit.Ink.Drop);
            head.characterSpacing = 2;
            head.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(34, -84), new Vector2(-88, -20));
            // Every page closes from its own corner too, not only from the bar.
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

        /// <summary>The panel the loadout, career and shop use: docked right at the page's width, so you stand on the left.</summary>
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

        /// <summary>Each finish's colours, as the web's skin art paints them.</summary>
        protected static (Color from, Color to) FinishPaint(string skin) => skin switch
        {
            "Candy" => (LobbyKit.Hex(0xff7ac8), LobbyKit.Hex(0x7fe3ff)),
            "Arcade" => (LobbyKit.Hex(0x3a1fd1), LobbyKit.Hex(0x00e0c6)),
            _ => (LobbyKit.WoodHi, LobbyKit.WoodLo),
        };

        /// <summary>A gear style's art: a disc in its colours with a BAT on it, centred in its parent.</summary>
        protected static RectTransform FinishDisc(Transform parent, string skin, float size)
        {
            var (from, to) = FinishPaint(skin);
            var disc = LobbyKit.Rect(parent, "Finish").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
            LobbyKit.Face(disc, from, Mathf.RoundToInt(size / 2f), LobbyKit.Navy, 3, 0, to);
            LobbyKit.ItemImage(disc, "BAT", size * .95f, 20f);
            return disc;
        }

        /// <summary>A colour's paint blob: lit from above, in a navy rim, with a shine.</summary>
        protected static RectTransform Blob(Transform parent, Color colour, float size)
        {
            var blob = LobbyKit.Rect(parent, "Swatch").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(size, size));
            LobbyKit.Face(blob, Color.Lerp(colour, Color.white, .2f), Mathf.RoundToInt(size / 2f), LobbyKit.Navy, 3, 0,
                Color.Lerp(colour, Color.black, .18f));
            var shine = LobbyKit.Rect(blob, "Shine").Pin(new Vector2(.32f, .72f), Vector2.zero, new Vector2(size * .22f, size * .16f));
            shine.Paint(new Color(1f, 1f, 1f, .7f), Mathf.Max(1, Mathf.RoundToInt(size * .08f))).raycastTarget = false;
            return blob;
        }

        /// <summary>The web's shop art behind an item: a rounded sunburst with a navy edge. Give the rect this
        /// size, so its corners stay round.</summary>
        protected static void Burst(RectTransform rect, int width, int height, int radius)
        {
            var raw = rect.gameObject.AddComponent<RawImage>();
            raw.texture = LobbyIcons.Sunburst(width, height, radius);
            raw.raycastTarget = false;
            LobbyKit.Frame(rect, LobbyKit.Navy, radius, 2);
        }

        protected static string Capital(string text) =>
            string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);

        protected static string MapName(string id) =>
            id != null && GameConfig.Current.Houses.TryGetValue(id, out var house) ? house.Name : id ?? "";
    }

    /// <summary>Just you in the map, the lobby notices, and the match dock: what GO will start, and GO.</summary>
    public sealed class HomePage : LobbyPage
    {
        public override string Id => LobbyMenu.Home;
        public override bool ShowFeed => true;
        /// <summary>The web's match dock width.</summary>
        public const float DockWidth = 456f;

        protected override void Build()
        {
            // Says what the open view does until you have done it once; no box, so it never reads as a button.
            if (!PlayerPrefs.HasKey(LobbyMenu.TurnHintKey))
            {
                var hint = LobbyKit.Row(Root, "Turn hint", 8, 0);
                hint.Pin(new Vector2(.5f, 0), new Vector2(0, 6), new Vector2(240, 40));
                var layout = hint.GetComponent<HorizontalLayoutGroup>();
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childForceExpandHeight = false;
                LobbyKit.Icon(hint, LobbyIcons.Turn, LobbyKit.Cyan).Size(26, 26);
                LobbyKit.Display(hint, "Drag to turn", 22, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft, LobbyKit.Ink.Stroke).Size(-1, 30);
            }
            // While a match starts, the countdown under the tabs takes over.
            if (Menu.Starting) return;

            // The web's match dock, bottom right: the mode (a click changes it), the house, and a big GO.
            bool blocked = Menu.Blocked != null;
            var dock = LobbyKit.Column(Root, "Next match", 12);
            dock.Pin(new Vector2(1, 0), Vector2.zero, new Vector2(DockWidth, 0));
            dock.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var mode = LobbyKit.PickCard(dock, "CHANGE", LobbyKit.ModeColour(Menu.Mode), "Next up  ·  " + Capital(Menu.Queue),
                LobbyMenu.ModeName(Menu.Mode), blocked ? "Not built yet  ·  online play" : Who(), () => Menu.Open(LobbyMenu.Play), 74);
            mode.Size(-1, 104);
            LobbyKit.ItemImage(mode.Body().Find("Art"), LobbyKit.ModeArt(Menu.Mode), 64, 10f);
            if (Menu.Mode != LobbyMenu.TutorialMode)
            {
                var house = LobbyKit.PickCard(dock, "Change house", LobbyKit.Hot, "House", MapName(Menu.Map), null,
                    () => Menu.Open(LobbyMenu.Play), 54);
                house.Size(-1, 80);
                LobbyKit.Icon(house.Body().Find("Art"), LobbyIcons.Home, LobbyKit.Cream).rectTransform
                    .Place(Vector2.zero, Vector2.one, new Vector2(11, 11), new Vector2(-11, -11));
            }
            var go = LobbyKit.Primary(dock, "GO", "GO", Menu.Go, 74, 46);
            go.Size(-1, 116);
            go.interactable = !blocked;
            First = blocked ? mode : go;
        }

        /// <summary>Who plays, or what the workshop modes are for.</summary>
        string Who() => Menu.Mode == LobbyMenu.TutorialMode ? "The tutorial room"
            : Menu.Mode == LobbyMenu.WorkshopMode ? "Build and test a home"
            : Capital(LobbyMenu.Seats(Menu.Mode, Menu.PartySize));
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
                var maps = LobbyKit.FitRow(column, "Maps", new Vector2(260, 204), 18, GameConfig.Current.Houses.Count);
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
            // A long name ("Walk-up Apartments") shrinks a little to fit the card rather than losing its end.
            name.enableAutoSizing = true; name.fontSizeMin = 18; name.fontSizeMax = 26;
            name.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(18, 14), new Vector2(-14, 0));
        }

        /// <summary>Draws a map's rooms from above to fit a box, like the web's plans: rooms with navy
        /// walls, the garden green, halls and landings sun. A house with an upstairs shows its storeys side
        /// by side, ground floor first, each labelled, with the stairs on both floors they join.</summary>
        public static void FloorPlan(RectTransform box, HouseLayout layout, Color colour, Vector2 size)
        {
            if (layout.Rooms.Count == 0) return;
            const float Gap = 10f, LabelHeight = 16f;
            float minX = layout.Rooms.Min(r => r.MinX), maxX = layout.Rooms.Max(r => r.MaxX);
            float minZ = layout.Rooms.Min(r => r.MinZ), maxZ = layout.Rooms.Max(r => r.MaxZ);
            int storeys = layout.StoreyFloors().Count;
            float label = storeys > 1 ? LabelHeight : 0f;
            float column = (size.x - Gap * (storeys - 1)) / storeys;
            float scale = Mathf.Min(column / Mathf.Max(1f, maxX - minX), (size.y - label) / Mathf.Max(1f, maxZ - minZ));
            var middle = new Vector2((minX + maxX) * .5f, (minZ + maxZ) * .5f);
            var planSize = new Vector2(maxX - minX, maxZ - minZ) * scale;
            for (int storey = 0; storey < storeys; storey++)
            {
                var offset = new Vector2((storey - (storeys - 1) * .5f) * (planSize.x + Gap), -label * .5f);
                Vector2 At(float x, float z) => offset + (new Vector2(x, z) - middle) * scale;
                foreach (var room in layout.Rooms)
                {
                    if (layout.StoreyOf(room) != storey) continue;
                    var rect = LobbyKit.Rect(box, room.Name).Pin(new Vector2(.5f, .5f), At((room.MinX + room.MaxX) * .5f, (room.MinZ + room.MaxZ) * .5f),
                        new Vector2(room.MaxX - room.MinX, room.MaxZ - room.MinZ) * scale - new Vector2(3, 3));
                    var fill = room.Name.Contains("Garden") ? LobbyKit.Lime
                        : room.Name.Contains("Hall") || room.Name.Contains("Landing") ? LobbyKit.Sun : colour;
                    rect.Paint(fill, 3).raycastTarget = false;
                    LobbyKit.Frame(rect, LobbyKit.Navy, 3, 3);
                }
                if (storeys == 1) continue;
                foreach (var s in layout.Stairs)
                {
                    if (layout.StoreyOf(layout.Room(s.Lower)) != storey && layout.StoreyOf(layout.Room(s.Upper)) != storey) continue;
                    var flight = LobbyKit.Rect(box, "Stairs").Pin(new Vector2(.5f, .5f), At((s.MinX + s.MaxX) * .5f, (s.MinZ + s.MaxZ) * .5f),
                        new Vector2(s.MaxX - s.MinX, s.MaxZ - s.MinZ) * scale);
                    flight.Paint(new Color(LobbyKit.Navy.r, LobbyKit.Navy.g, LobbyKit.Navy.b, .55f)).raycastTarget = false;
                }
                var name = LobbyKit.Display(box, layout.StoreyLabel(storey), 12, LobbyKit.Cream, TextAlignmentOptions.Center, LobbyKit.Ink.Stroke);
                name.enableAutoSizing = true; name.fontSizeMin = 7; name.fontSizeMax = 12;
                name.rectTransform.Pin(new Vector2(.5f, .5f), offset + new Vector2(0f, (planSize.y + label) * .5f), new Vector2(planSize.x + Gap, label));
            }
        }
    }

    /// <summary>
    /// The web's locker (Web/src/main.js): a narrow panel on the right, so you watch yourself change beside it.
    /// What you wear in pickers, the colour of each worn part, the extras as cards, the crafted gear style and
    /// THAT'S MY LOOK, all on one page with nothing to open first. Its second tab is the recipe book.
    /// </summary>
    public sealed class LoadoutPage : LobbyPage
    {
        /// <summary>The web locker's 540 plus room for our panel's edge and drop.</summary>
        public const float Width = 600f;
        public override string Id => LobbyMenu.Loadout;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;
        public override float PanelWidth => Width;
        public bool ShowingRecipes { get; private set; }
        /// <summary>The worn part the swatches colour.</summary>
        public string Part { get; private set; } = "Top";
        /// <summary>The recipe a click pinned to the detail strip; null shows the first.</summary>
        public string Pinned { get; private set; }

        RectTransform lockerList, detail;
        float scrolled;
        string trying, previewing;

        public override void Opened()
        {
            scrolled = 0f;
            lockerList = null;
        }

        protected override void Build()
        {
            // A choice redraws the page; the locker stays where it was scrolled.
            if (lockerList) scrolled = lockerList.anchoredPosition.y;
            lockerList = detail = null;
            trying = previewing = null;
            int words = GameConfig.Current.Items.Enabled.Count();
            var body = Side("Loadout", ShowingRecipes ? $"{words} words to spell. In a match, press Q and type one." : "All style. Zero stats.");
            var tabs = LobbyKit.Segmented(body.parent, "Tabs", new[] { ("LOCKER", "Locker"), ("RECIPES", "Recipes") },
                ShowingRecipes ? "RECIPES" : "LOCKER", id => ShowRecipes(id == "RECIPES"));
            // Left of the page's close button (48 wide, 20 in from the corner).
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
            var done = LobbyKit.Confirm(body, "Done", "That's my look", Menu.Close);
            ((RectTransform)done.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(0, 6), new Vector2(0, 66));
            var view = LobbyKit.Rect(body, "Locker").Place(Vector2.zero, Vector2.one, new Vector2(0, 84), Vector2.zero);
            lockerList = LobbyKit.Scroll(view, "Scroll", 18);
            // Room inside the mask for rings, drops and cards that lift.
            lockerList.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 6, 10);
            lockerList.anchoredPosition = new Vector2(0, scrolled);

            // What you wear, where there is a choice to make (the web's Top and Headwear selects).
            var pickers = LobbyKit.Row(lockerList, "Outfit", 16);
            pickers.Size(-1, 84);
            pickers.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            foreach (string slot in wardrobe.Slots.Where(s => wardrobe.PiecesFor(s).Count() > 1))
            {
                var column = LobbyKit.Column(pickers, slot, 8);
                column.Size(-1, -1, 1);
                LobbyKit.SectionLabel(column, slot);
                var choices = wardrobe.PiecesFor(slot).Select(p => (p.Id, p.Id)).ToList();
                if (!wardrobe.RequiredSlots.Contains(slot)) choices.Add(("No " + slot, "None"));
                string worn = outfit.PieceIn(slot) ?? "No " + slot;
                var picker = LobbyKit.Segmented(column, "Pick " + slot, choices, worn, id => PutOn(slot, id, id));
                if (!First) First = picker.GetComponentsInChildren<Button>().FirstOrDefault(b => b.name == worn);
            }

            // Each worn part's colour: pick the part, then a swatch.
            var colour = LobbyKit.Column(lockerList, "Colour", 8);
            LobbyKit.SectionLabel(colour, "Colour");
            var parts = Parts(outfit);
            if (!parts.Contains(Part)) Part = parts.Contains("Top") ? "Top" : parts.FirstOrDefault();
            var chips = LobbyKit.Grid(colour, "Parts", new Vector2(124, 40), 8);
            foreach (string slot in parts) PartChip(chips, slot, wardrobe.ColourFor(outfit, slot));
            if (Part != null) Colours(colour, Part);

            // The extras: one piece each, on or off (the web's checkbox cards).
            var extras = wardrobe.Slots.Where(s => !wardrobe.RequiredSlots.Contains(s) && wardrobe.PiecesFor(s).Count() == 1).ToList();
            if (extras.Count > 0)
            {
                var section = LobbyKit.Column(lockerList, "Extras", 8);
                LobbyKit.SectionLabel(section, "Extras");
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

            // The finish on what you craft (the web's crafted gear style select), as three cards.
            var gear = LobbyKit.Column(lockerList, "Gear", 8);
            LobbyKit.SectionLabel(gear, "Crafted gear style");
            var styles = LobbyKit.Row(gear, "Styles", 12);
            styles.Size(-1, 80);
            styles.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            string wornSkin = SkinOf(outfit);
            foreach (string skin in Finishes) FinishCard(styles, skin, wornSkin == skin);
        }

        /// <summary>The worn parts with a colour of their own: not the hood (it matches the top) or the badge.</summary>
        static List<string> Parts(Outfit outfit)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            return wardrobe.Slots.Where(slot =>
            {
                var piece = wardrobe.Piece(outfit.PieceIn(slot) ?? "");
                return piece != null && piece.TintMaterial != null && piece.ColourFrom == null && wardrobe.Palettes.ContainsKey(slot);
            }).ToList();
        }

        string PartName(string slot) => Menu.Outfit.PieceIn(slot) ?? slot;

        static string ExtraIcon(string slot) => slot switch
        {
            "Face" => LobbyIcons.Glasses,
            "Back" => LobbyIcons.Satchel,
            "Badge" => LobbyIcons.Badge,
            _ => LobbyIcons.Plus,
        };

        static string ExtraName(string piece) => piece == "TBadge" ? "Letter badge" : piece;

        /// <summary>A part to colour: its colour in a dot and its name, a sun slab while its swatches show.</summary>
        void PartChip(Transform parent, string slot, Colourway colour)
        {
            bool on = Part == slot;
            var button = on
                ? LobbyKit.Button(parent, "Part " + slot, LobbyKit.SunHi, () => PickPart(slot), 10, LobbyKit.Navy, 3, 3, LobbyKit.Sun2)
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

        /// <summary>A part's swatches. Only top colours are sold: the rest of those wait in a row of their own,
        /// each with a coin on it, beside a way into the shop.</summary>
        void Colours(RectTransform section, string slot)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            var outfit = Menu.Outfit;
            var palette = wardrobe.Palettes[slot];
            var current = wardrobe.ColourFor(outfit, slot);
            bool sold = slot == "Top";
            var swatches = LobbyKit.Grid(section, "Colours", new Vector2(40, 40), 12);
            swatches.GetComponent<GridLayoutGroup>().padding = new RectOffset(4, 4, 8, 8);
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
                .Select(p => p.Id.ToLowerInvariant() + " matches");
            string caption = string.Join("  ·  ", new[] { PartName(slot), current?.Name }.Where(s => s != null).Concat(matching));
            LobbyKit.Text(section, caption, 16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft).Size(-1, 22);
        }

        /// <summary>The web's colour swatch: a navy-rimmed blob with a shine, bigger and sun-ringed when worn.
        /// Pointing at one tries it on.</summary>
        void Swatch(Transform parent, string slot, Colourway colour, bool on)
        {
            var button = LobbyKit.Button(parent, "Colour " + colour.Id, LobbyKit.Navy, () => PickColour(slot, colour), 20, null, 0, 3);
            SwatchFill(button.Body(), colour, 17);
            var press = button.GetComponent<LobbyPress>();
            press.HoverScale = 1.12f; press.Tilt = 8f;
            if (on) { press.Scale = 1.14f; LobbyKit.Ring(button.Body(), LobbyKit.Sun, 20, 4); }
            press.Hot = hot => TryOn(slot, colour, hot);
            button.gameObject.AddComponent<LobbyHint>().Text = colour.Name;
        }

        /// <summary>A top colour still in the shop: smaller, with a coin on it. A click opens the shop on it.</summary>
        void ShopSwatch(Transform parent, Colourway colour)
        {
            var offer = Career.Shop.FirstOrDefault(o => o.Kind == "colour" && o.Value == colour.Id);
            var button = LobbyKit.Button(parent, "Colour " + colour.Id, LobbyKit.Navy, () => ToShop(colour), 16, null, 0, 2);
            button.Size(32, 32);
            SwatchFill(button.Body(), colour, 13);
            LobbyKit.Coin(button.Body(), 16).Pin(new Vector2(1, 0), new Vector2(5, -5), new Vector2(16, 16));
            var press = button.GetComponent<LobbyPress>();
            press.HoverScale = 1.12f; press.Tilt = 8f;
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

        /// <summary>Shows a colour on you while it's pointed at, and what you wear again after.</summary>
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
            // Only the swatch being tried: the next one may already have taken over this frame.
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

        /// <summary>Wears a piece, or takes a slot off ("No " + slot), first putting on what the piece needs: the
        /// hood brings the hoodie, as on the web. Anything that no longer fits comes off.</summary>
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
            Menu.Wear(next);
            Refresh();
            Reselect(reselect);
        }

        /// <summary>A gear style: its disc, its name, and WEARING, OWNED or its price.</summary>
        void FinishCard(Transform parent, string skin, bool worn)
        {
            bool owned = Menu.Career.Owns("skin", skin);
            var offer = Career.Shop.FirstOrDefault(o => o.Kind == "skin" && o.Value == skin);
            var card = LobbyKit.Button(parent, "Finish " + skin, Color.white, () => PickFinish(skin, owned), 14, LobbyKit.Navy, 3, 4);
            card.Size(-1, -1, 1);
            var press = card.GetComponent<LobbyPress>();
            press.Lift = 3f; press.Tilt = 1f;
            var body = card.Body();
            if (worn) LobbyKit.Ring(body, LobbyKit.Sun, 14, 4);
            FinishDisc(body, skin, 52).Pin(new Vector2(0, .5f), new Vector2(12, 0), new Vector2(52, 52));
            var name = LobbyKit.Display(body, skin, 20, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            name.enableAutoSizing = true; name.fontSizeMin = 14; name.fontSizeMax = 20;
            name.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(74, 38), new Vector2(-8, -10));
            var state = LobbyKit.Row(body, "State", 4);
            state.Place(Vector2.zero, new Vector2(1, 0), new Vector2(74, 12), new Vector2(-8, 36));
            state.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            if (worn || owned || offer == null)
            {
                var label = LobbyKit.Text(state, worn ? "WEARING" : "OWNED", 12, worn ? LobbyKit.Owned : LobbyKit.CardSub,
                    TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
                label.characterSpacing = 1;
                label.Size(-1, 20);
            }
            else
            {
                LobbyKit.Coin(state, 18);
                LobbyKit.Display(state, offer.Price.ToString("N0", CultureInfo.InvariantCulture), 17, LobbyKit.Navy,
                    TextAlignmentOptions.MidlineLeft).Size(-1, 22);
            }
        }

        void PickFinish(string skin, bool owned)
        {
            if (!owned)
            {
                Menu.Post(skin + " gear is in the shop.");
                Menu.OpenShop("skin:" + skin);
                return;
            }
            Menu.Wear(WithFinish(Menu.Outfit, skin));
            Refresh();
            Reselect("Finish " + skin);
        }

        ItemDefinition Shown()
        {
            var items = GameConfig.Current.Items.Enabled.ToList();
            return items.FirstOrDefault(i => i.Id == Pinned) ?? items.FirstOrDefault();
        }

        void Recipes(RectTransform body)
        {
            var shown = Shown();
            detail = LobbyKit.Rect(body, "Recipe detail").Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 196));
            var view = LobbyKit.Rect(body, "Recipes").Place(Vector2.zero, Vector2.one, new Vector2(0, 212), Vector2.zero);
            var list = LobbyKit.Scroll(view, "Scroll", 0);
            // Room inside the mask for a card that lifts and its drop.
            list.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 8, 12);
            var grid = LobbyKit.Grid(list, "Grid", new Vector2(122, 118), 12);
            foreach (var item in GameConfig.Current.Items.Enabled) RecipeCard(grid, item, item == shown);
            Detail(shown);
        }

        /// <summary>The web's recipe card: white with a navy edge, the item over its word in wood tiles. Pointing
        /// at one shows it in the strip below; a click keeps it there.</summary>
        void RecipeCard(Transform parent, ItemDefinition item, bool on)
        {
            var card = LobbyKit.Button(parent, "Recipe " + item.Id, Color.white, () => PinRecipe(item.Id), 14, LobbyKit.Navy, 3, 4);
            var press = card.GetComponent<LobbyPress>();
            press.Lift = 4f; press.Tilt = 2f;
            var body = card.Body();
            if (on)
            {
                LobbyKit.Ring(body, LobbyKit.Sun, 14, 4);
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

        /// <summary>The strip under the recipe book: the item big on a sunburst, its word, what it does and how to
        /// spell it in a match.</summary>
        void Detail(ItemDefinition item)
        {
            if (!detail || item == null) return;
            LobbyKit.Clear(detail);
            var strip = LobbyKit.Rect(detail, "Strip").Fill();
            LobbyKit.Face(strip, LobbyKit.Track, 18);
            var art = LobbyKit.Rect(strip, "Art").Pin(new Vector2(0, .5f), new Vector2(16, 0), new Vector2(150, 150));
            Burst(art, 150, 150, 16);
            LobbyKit.ItemImage(art, item.Id, 118);
            var words = LobbyKit.Column(strip, "Words", 10);
            words.Place(Vector2.zero, Vector2.one, new Vector2(184, 16), new Vector2(-16, -16));
            words.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            var tiles = LobbyKit.Row(words, "Word", 5);
            tiles.Size(-1, 44);
            tiles.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            string word = item.Id;
            float size = Mathf.Min(38f, (320f - 5f * (word.Length - 1)) / word.Length);
            for (int i = 0; i < word.Length; i++) LobbyKit.LetterTile(tiles, word[i], size, (i - (word.Length - 1) / 2f) * 2f);
            var blurb = LobbyKit.Display(words, Blurb(item.Family), 22, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft);
            blurb.name = "Blurb";
            blurb.enableAutoSizing = true; blurb.fontSizeMin = 15; blurb.fontSizeMax = 22;
            blurb.Size(-1, 30);
            var how = LobbyKit.Row(words, "How", 6);
            how.Size(-1, 30);
            how.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            LobbyKit.Text(how, "Press", 16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft).Size(-1, 28);
            LobbyKit.Kbd(how, "Q");
            LobbyKit.Text(how, "and type it, then", 16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft).Size(-1, 28);
            LobbyKit.Kbd(how, "ENTER");
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

    /// <summary>
    /// The web's item shop: big cards on sunbursts in the wide side panel, in two shelves (gear styles, then top
    /// colours), bought with coins from matches. A click on a card tries it on; its price tag buys it, then WEAR
    /// puts it on. Opened from the locker, it rings the offer you came for.
    /// </summary>
    public sealed class ShopPage : LobbyPage
    {
        static readonly Vector2 Card = new Vector2(248, 276);
        string note, spotlight;

        public override string Id => LobbyMenu.Shop;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Left;
        /// <summary>The offer the shop was opened on, ringed in cyan; null when it was opened from the bar.</summary>
        public string Spotlit => spotlight;

        public override void Opened()
        {
            note = null;
            spotlight = null;
        }

        /// <summary>Rings one offer and puts a keyboard or controller on what buys it.</summary>
        public void Spotlight(string offerId)
        {
            spotlight = offerId;
            Refresh();
            if (First && EventSystem.current) EventSystem.current.SetSelectedGameObject(First.gameObject);
        }

        protected override void Build()
        {
            var body = Side("Item shop", "Looks only. Never stats.");
            var foot = LobbyKit.Row(body, "Footer", 10);
            foot.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 34));
            foot.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            LobbyKit.Coin(foot, 24);
            LobbyKit.Text(foot, note ?? "Click a card to try it on. Earn coins in matches.", 17, note != null ? LobbyKit.Sun : LobbyKit.Muted,
                TextAlignmentOptions.MidlineLeft).Size(-1, 30, 1);
            var view = LobbyKit.Rect(body, "Offers").Place(Vector2.zero, Vector2.one, new Vector2(0, 46), Vector2.zero);
            var list = LobbyKit.Scroll(view, "Scroll", 10);
            // Room inside the mask for rings, drops and cards that lift.
            list.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(4, 4, 8, 12);
            LobbyKit.SectionLabel(list, "Gear styles");
            var finishes = LobbyKit.Grid(list, "Finishes", Card, 16);
            foreach (var offer in Career.Shop.Where(o => o.Kind == "skin")) Offer(finishes, offer);
            LobbyKit.SectionLabel(list, "Top colours");
            var colours = LobbyKit.Grid(list, "Colours", Card, 16);
            foreach (var offer in Career.Shop.Where(o => o.Kind == "colour")) Offer(colours, offer);
        }

        /// <summary>The web's shop card: white with a navy edge, the art on a sunburst, the name and what it is,
        /// and its price tag, WEAR or WEARING.</summary>
        void Offer(Transform parent, ShopOffer offer)
        {
            bool owned = Menu.Career.Owns(offer.Kind, offer.Value);
            bool worn = offer.Kind == "skin" ? SkinOf(Menu.Outfit) == offer.Value : Menu.Outfit.ColourOf("Top") == offer.Value;
            var colourway = offer.Kind == "colour" ? GameConfig.Current.Wardrobe.Colour("Top", offer.Value) : null;
            var card = LobbyKit.Button(parent, "Offer " + offer.Id, Color.white, () => TryOn(colourway), 16, LobbyKit.Navy, 3, 5);
            var press = card.GetComponent<LobbyPress>();
            press.Lift = 4f; press.Tilt = 1f;
            var t = card.Body();
            if (worn) LobbyKit.Ring(t, LobbyKit.Sun, 16, 5);
            else if (offer.Id == spotlight) LobbyKit.Ring(t, LobbyKit.Cyan, 16, 5);
            var art = LobbyKit.Rect(t, "Art").Place(new Vector2(0, 1), Vector2.one, new Vector2(10, -138), new Vector2(-10, -10));
            Burst(art, (int)Card.x - 20, 128, 14);
            if (colourway != null) Blob(art, new Color(colourway.R, colourway.G, colourway.B), 100);
            else FinishDisc(art, offer.Value, 108);
            if (owned && !worn) OwnedMark(art);
            var name = LobbyKit.Display(t, LobbyKit.Upper(offer.Name), 24, LobbyKit.Navy);
            name.characterSpacing = 1;
            name.enableAutoSizing = true; name.fontSizeMin = 16; name.fontSizeMax = 24;
            name.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(10, -176), new Vector2(-10, -146));
            // What the colour goes on, as the web says it: "Hoodie colour" while you wear the hoodie.
            string kindText = offer.Kind == "skin" ? "Crafted gear style" : (Menu.Outfit.PieceIn("Top") ?? "Top") + " colour";
            var kind = LobbyKit.Text(t, kindText, 16, LobbyKit.CardSub, TextAlignmentOptions.Center, FontStyles.Bold);
            kind.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(10, -200), new Vector2(-10, -176));
            var action = worn ? Stamp(t, offer) : owned ? WearChip(t, offer)
                : LobbyKit.PriceTag(t, "Buy " + offer.Id, offer.Price, Menu.Career.Coins < offer.Price, () => Buy(offer), 22);
            action.interactable = !worn;
            ((RectTransform)action.transform).Place(Vector2.zero, new Vector2(1, 0), new Vector2(44, 16), new Vector2(-44, 60));
            // A controller starts on the offer the shop was opened for, or else on the first thing to buy or wear.
            if (!worn && (offer.Id == spotlight || !First)) First = action;
        }

        /// <summary>The web's owned stamp on an offer's art: green type in a green outline, turned a little.</summary>
        static void OwnedMark(RectTransform art)
        {
            var stamp = LobbyKit.Rect(art, "Owned").Pin(Vector2.one, new Vector2(-8, -8), new Vector2(92, 28));
            stamp.localRotation = Quaternion.Euler(0, 0, -6f);
            stamp.Paint(Color.white, 6).raycastTarget = false;
            LobbyKit.Frame(stamp, LobbyKit.Owned, 6, 2);
            var label = LobbyKit.Display(stamp, "OWNED", 17, LobbyKit.Owned);
            label.characterSpacing = 2;
            label.rectTransform.Fill();
        }

        /// <summary>WEAR on something you own: a lime slab, like the locker's confirm.</summary>
        Button WearChip(RectTransform card, ShopOffer offer)
        {
            var button = LobbyKit.Button(card, "Wear " + offer.Id, LobbyKit.LimeHi, () => Wear(offer), 10, LobbyKit.Navy, 2, 3, LobbyKit.LimeLo);
            var label = LobbyKit.Display(button.Body(), "WEAR", 22, LobbyKit.Navy);
            label.characterSpacing = 2;
            label.rectTransform.Fill();
            return button;
        }

        /// <summary>The web's worn stamp: green outline and type, turned a little, and never faded.</summary>
        static Button Stamp(RectTransform card, ShopOffer offer)
        {
            var button = LobbyKit.Button(card, "Wearing " + offer.Id, Color.clear, null, -1);
            button.GetComponent<LobbyPress>().FadeOff = false;
            var stamp = LobbyKit.Rect(button.Body(), "Stamp").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(140, 38));
            stamp.localRotation = Quaternion.Euler(0, 0, 6f);
            LobbyKit.Frame(stamp, LobbyKit.Owned, 8, 3);
            var label = LobbyKit.Display(stamp, "WEARING", 20, LobbyKit.Owned);
            label.characterSpacing = 3;
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
        public override bool Modal => true;

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
        public override bool Modal => true;

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
