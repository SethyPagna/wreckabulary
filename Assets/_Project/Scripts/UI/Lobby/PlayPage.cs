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
    public sealed partial class PlayPage : LobbyPage
    {
        public override string Id => LobbyMenu.Play;
        public string Aim;
        public bool WordboundSolo;
        public bool WordboundMaps;
        LobbyPlayPageView authoredFrame;
        LobbyPlayLayout playLayout;
        RectTransform framePanel;
        public override LobbyStage.Focus Focus => LobbyStage.Focus.Centre;
        public override float PanelWidth => framePanel && framePanel.rect.width > 0 ? framePanel.rect.width : base.PanelWidth;

        public const string SavedHomeCard = "Saved home ";
        public const string ModeSections = "Sections", SidewaysTrack = "Sideways position", SoloNote = "Solo only note";
        public const string KeepOneMap = "Keep at least one map ticked.";
        public const string GoalLine = "Mode goal";
        public const string NameScrim = "Map name scrim";
        public const string GuideIcon = "Guide icon", GuideHint = "How to play";
        static readonly Vector2 HouseSize = new Vector2(280, 300);
        const float ModeHeight = 52f, WideStrip = 1800f, SidewaysWheel = -40f;
        const int PictureRadius = 10, GuideRadius = 13;
        const float GuideInset = 4f;
        const float TitleInset = 12f;

        static readonly (string id, string label)[] Queues =
        {
            ("Queue " + LobbyMenu.Practice, "Practice (Offline)"),
            ("Queue " + LobbyMenu.Matchmaking, "Matchmaking"),
            ("Queue " + LobbyMenu.Workshop, "Create"),
        };

        string DisplayQueue => Online ? LobbyMenu.Matchmaking : Menu.Queue;

        protected override void Build()
        {
            authoredFrame = null; framePanel = null;
            if (Menu.PartySize > 1) WordboundSolo = false;
            var saved = Resources.Load<LobbyPlayLayout>(LobbyPlayLayout.ResourcePath);
            playLayout = saved;
            RectTransform body;
            if (saved)
            {
                saved.Validate();
                authoredFrame = LobbyRuntimeTypography.Instantiate(saved.Page, Root, false);
                authoredFrame.BindOptions(Menu.Close, QueueLine(DisplayQueue));
                body = authoredFrame.Body;
            }
            else body = Panel(Vector2.zero, Vector2.one, "Play", QueueLine(DisplayQueue));
            framePanel = authoredFrame ? authoredFrame.Panel : (RectTransform)body.parent;
            var queues = LobbyKit.Segmented(authoredFrame ? authoredFrame.Queues : body.parent, "Queues", Queues, "Queue " + DisplayQueue, PickQueue, 52, 20);
            if (authoredFrame) queues.Fill();
            else queues.Place(Vector2.one, Vector2.one, new Vector2(-688, -78), new Vector2(-88, -26));

            if (Menu.Queue == LobbyMenu.Matchmaking && !Online)
            {
                Footer(body);
                First = queues.GetComponentsInChildren<Button>().FirstOrDefault(button => button.name == "Queue " + LobbyMenu.Practice);
                return;
            }

            var column = authoredFrame ? authoredFrame.Choices : LobbyKit.Column(body, "Choices", 8).Place(Vector2.zero, Vector2.one, new Vector2(0, 104), Vector2.zero);
            column = ScrollableChoices(column);
            if (Online)
            {
                if (Friends) FriendsChoices(column, body);
                else MatchmakingChoices(column, body);
                return;
            }
            if (DisplayQueue == LobbyMenu.Practice)
            {
                PracticeModes(column);
                if (WordboundSolo)
                {
                    SoloChoices(column, body);
                    return;
                }
            }
            else if (DisplayQueue == LobbyMenu.Workshop)
            {
                CreateModes(column);
                if (WordboundMaps) { WorkshopChoices(column, body); return; }
            }
            if (Menu.Mode == LobbyMenu.TutorialMode)
            {
                LobbyKit.Text(column, "Room", 18, LobbyKit.Muted).Size(-1, 32);
                TutorialRoom(column);
            }
            else
            {
                bool practice = Menu.Queue == LobbyMenu.Practice;
                LobbyKit.SectionLabel(column, practice ? "Maps" : "Map");
                var houses = MapCards(column, "Houses");
                foreach (var house in GameConfig.Current.Houses)
                    HouseCard(houses, house.Key, house.Value);
                if (practice && HomeDesigner.Playable(Menu.Mode))
                    foreach (var home in HomeStorage.ForGame().Saved()) HomeCard(houses, home);
            }
            Footer(body);

            if (Aim != null)
            {
                var aimed = Root.GetComponentsInChildren<Selectable>().FirstOrDefault(s => s.name == Aim && s.IsInteractable());
                if (aimed) First = aimed;
                Aim = null;
            }
        }

        void SoloChoices(RectTransform column, RectTransform body)
        {
            LobbyKit.SectionLabel(column, "Map");
            var maps = MapCards(column, "Solo maps");
            SoloMapCard(maps, WordboundSoloEntry.ParcelServiceMap, "Parcel Depot", "Two-level depot");
            SoloMapCard(maps, WordboundSoloEntry.LostAddressMap, "The Lost Address", "Four-floor story");
            SoloMapCard(maps, WordboundSoloEntry.ParcelHallMap, "Parcel Hall", "Furnished hall");
            var bar = SummaryFrame(body, "Solo summary");
            SummaryLine(bar);
            string blocked = Menu.PartySize != 1 ? "Solo only. Leave extra local seats to start." :
                !Session.CanLoad(Session.WordboundScene) ? "Courses are unavailable in this build." : null;
            var heading = LobbyKit.Text(bar, LobbyPlayModeLabels.Name(WordboundSoloEntry.SelectedMode) + MapCardText.Separator + SoloMapName(WordboundSoloEntry.SelectedMap),
                26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            heading.enableAutoSizing = true; heading.fontSizeMin = 20; heading.fontSizeMax = 26;
            heading.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-250, -8));
            var detail = LobbyKit.Text(bar, blocked ?? "1 player", 18,
                blocked == null ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Solo detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-250, -48));
            var go = LobbyKit.Primary(bar, "Solo GO", "Play", () => Menu.OpenWordboundSolo(WordboundSoloEntry.SelectedMode), 42, 26);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-220, -30), new Vector2(-8, 30));
            go.interactable = blocked == null;
        }

        void WorkshopChoices(RectTransform column, RectTransform body)
        {
            LobbyKit.SectionLabel(column, "Map");
            var row = MapCards(column, "Wordbound workshop");
            var card = MapCard(row, "Wordbound maps", true, Menu.OpenWordboundWorkshop);
            MapWords(card, CardPicture(card, null, LobbyIcons.Layers, "TABLE"), "Map editor", "Build and test course maps");
            bool available = Session.CanLoad(Session.WordboundScene);
            card.interactable = available;
            var bar = SummaryFrame(body, "Workshop summary");
            SummaryLine(bar);
            var detail = LobbyKit.Text(bar, available ? "Local editor. One-player playtests." : "The map editor is unavailable in this build.",
                18, available ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.MidlineLeft);
            detail.name = "Workshop detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-250, -10));
            var open = LobbyKit.Primary(bar, "Workshop OPEN", "Open", Menu.OpenWordboundWorkshop, 32, 24);
            ((RectTransform)open.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-220, -30), new Vector2(-8, 30));
            open.interactable = available;
            First = open;
        }

        static string SoloMapName(string id) => id switch
        {
            WordboundSoloEntry.LostAddressMap => "The Lost Address",
            WordboundSoloEntry.ParcelHallMap => "Parcel Hall",
            _ => "Parcel Depot",
        };

        void PickQueue(string id)
        {
            string queue = id.Substring("Queue ".Length);
            Online = queue == LobbyMenu.Matchmaking;
            Friends = false;
            if (!Online) Menu.Choose(queue: queue);
            Refresh();
            Reselect(id);
        }

        static string QueueLine(string queue) => queue == LobbyMenu.Matchmaking
            ? "Human players online."
            : queue == LobbyMenu.Workshop ? "Build homes and maps." : "Play on this PC.";

        string ShownMode => WordboundSolo ? WordboundSoloEntry.SelectedMode : Menu.Mode;

        public static string ModeButton(string mode) => (HouseModes.Has(mode) ? "Mode " : "Solo ") + mode;

        public static string SoloOnly(string mode) => LobbyPlayModeLabels.Name(mode) + " is solo only. Remove extra seats to play.";

        void PracticeModes(RectTransform column)
        {
            string shown = ShownMode;
            var strip = ModeStrip(column, "Modes", LobbyPlayModeLabels.Line(shown), shown, true);
            var families = strip.GetComponentInChildren<LobbyPlayModeFamilies>();
            foreach (var family in HouseModes.Families)
            {
                var section = Family(families.transform, HouseModes.FamilyName(family));
                foreach (string mode in HouseModes.ModesIn(family))
                {
                    if (HouseModes.Has(mode) || Menu.PartySize == 1) { PracticeChoice(section, mode); continue; }
                    var note = LobbyKit.Text(section, SoloOnly(mode), 16, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
                    note.name = SoloNote;
                    note.enableAutoSizing = true; note.fontSizeMin = 12; note.fontSizeMax = 16;
                }
            }
            families.Fit();
            strip.GetComponent<LobbyPlayModeDock>()?.Fit();
        }

        static RectTransform Family(Transform sections, string name)
        {
            var family = LobbyKit.Rect(sections, name);
            var caption = LobbyKit.Text(family, LobbyKit.Upper(name), 14, LobbyKit.Muted, TextAlignmentOptions.BottomLeft);
            caption.name = LobbyPlayModeFamilies.Caption;
            caption.characterSpacing = 1;
            return family;
        }

        static RectTransform Sideways(RectTransform strip)
        {
            strip.Paint(new Color(0, 0, 0, 0));
            strip.gameObject.AddComponent<RectMask2D>();
            var content = LobbyKit.Rect(strip, ModeSections);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            var scroll = strip.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = true; scroll.vertical = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = SidewaysWheel;
            scroll.viewport = strip; scroll.content = content;
            var track = LobbyKit.Rect(strip, SidewaysTrack).Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 4));
            track.Paint(LobbyKit.Line, 2);
            var handle = LobbyKit.Rect(track, "Handle").Fill();
            var ink = handle.Paint(LobbyKit.Cyan, 2);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.LeftToRight;
            bar.handleRect = handle; bar.targetGraphic = ink;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.horizontalScrollbar = bar;
            scroll.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            strip.gameObject.AddComponent<LobbyScrollFollow>();
            return content;
        }

        void PracticeChoice(Transform row, string mode)
        {
            string id = ModeButton(mode);
            bool selected = ShownMode == mode;
            var button = CompactMode(row, id, LobbyPlayModeLabels.Name(mode), selected, () => { PickMode(mode); Refresh(); Reselect(id); });
            if (selected) First = button;
        }

        void PickMode(string mode)
        {
            WordboundSolo = !HouseModes.Has(mode);
            if (WordboundSolo) WordboundSoloEntry.Select(mode);
            else Menu.Choose(queue: LobbyMenu.Practice, mode: mode);
        }

        RectTransform ModeStrip(Transform parent, string name, string goal, string guide = null, bool sideways = false)
        {
            var host = authoredFrame ? authoredFrame.Body : parent;
            var strip = LobbyKit.Rect(host, name);
            var element = strip.gameObject.AddComponent<LayoutElement>();
            var families = (sideways ? Sideways(strip) : strip).gameObject.AddComponent<LobbyPlayModeFamilies>();
            families.OneRow = sideways;
            families.ButtonWidth = playLayout ? playLayout.ModeMinimumWidth : 104;
            families.ButtonHeight = playLayout ? playLayout.ModeHeight : ModeHeight;
            var line = Goal(host, goal);
            GuideButton(line, guide);
            if (authoredFrame)
            {
                element.ignoreLayout = true;
                var dock = strip.gameObject.AddComponent<LobbyPlayModeDock>();
                dock.Choices = authoredFrame.Choices;
                dock.MaximumWidth = Mathf.Max(playLayout ? playLayout.ModeMaximumWidth : 1160, WideStrip);
                if (line)
                {
                    line.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                    dock.Goal = line.rectTransform;
                }
            }
            else if (line) line.Size(-1, 30);
            return strip;
        }

        static TextMeshProUGUI Goal(Transform parent, string goal)
        {
            if (string.IsNullOrEmpty(goal)) return null;
            var line = LobbyKit.Text(parent, goal, 19, LobbyKit.Muted, TextAlignmentOptions.Center);
            line.name = GoalLine;
            line.enableAutoSizing = true; line.fontSizeMin = 13; line.fontSizeMax = 19;
            return line;
        }

        static void GuideButton(TextMeshProUGUI line, string mode)
        {
            if (!line || string.IsNullOrEmpty(mode) || !ModeGuides.Has(mode)) return;
            var button = LobbyKit.Button(line.transform, GuideIcon, LobbyKit.Card, () => ModeGuides.Show(mode), GuideRadius, LobbyKit.Line, 2);
            var rect = ((RectTransform)button.transform).Pin(new Vector2(.5f, .5f), Vector2.zero, Vector2.one * LobbyPlayGoalInfo.Size);
            button.Body().Place(Vector2.zero, Vector2.one, Vector2.one * GuideInset, -Vector2.one * GuideInset);
            LobbyKit.Text(button.Body(), "i", 17, LobbyKit.Navy, TextAlignmentOptions.Center, FontStyles.Bold).rectTransform.Fill();
            button.gameObject.AddComponent<LobbyHint>().Text = GuideHint;
            var info = line.gameObject.AddComponent<LobbyPlayGoalInfo>();
            info.Line = line; info.Icon = rect;
        }

        void CreateModes(RectTransform column)
        {
            var strip = ModeStrip(column, "Create modes", WordboundMaps
                ? "Map editor" + MapCardText.Separator + "Build course maps and test them on your own"
                : LobbyPlayModeLabels.Line(LobbyMenu.WorkshopMode));
            var tools = LobbyKit.Rect(strip, "Builders");
            foreach (var choice in new[] { (id: "Create house", title: LobbyMenu.ModeName(LobbyMenu.WorkshopMode)), (id: "Create Wordbound", title: "Map editor") })
            {
                bool editor = choice.id == "Create Wordbound";
                bool selected = editor == WordboundMaps;
                var button = CompactMode(tools, choice.id, choice.title, selected, () => {
                    WordboundMaps = editor;
                    if (!editor) Menu.Choose(queue: LobbyMenu.Workshop, mode: LobbyMenu.WorkshopMode);
                    Refresh(); Reselect(choice.id);
                });
                if (selected) First = button;
            }
            strip.GetComponent<LobbyPlayModeFamilies>().Fit();
            strip.GetComponent<LobbyPlayModeDock>()?.Fit();
        }

        static Button CompactMode(Transform parent, string id, string label, bool selected, Action action) =>
            LobbyKit.TextAction(parent, id, label, 20, action, selected);

        RectTransform MapCards(Transform parent, string name)
        {
            float width = LobbyPlayCardGrid.PreferredWidth, height = LobbyPlayCardGrid.HeightFor(width);
            var rect = LobbyKit.Grid(parent, name, new Vector2(width, height), 12).Size(-1, height);
            var grid = rect.gameObject.AddComponent<LobbyPlayCardGrid>();
            grid.MinimumWidth = playLayout ? playLayout.MapMinimumWidth : 264;
            grid.MaximumColumns = playLayout ? playLayout.MapColumns : 3;
            return rect;
        }

        static string Participants(string mode, int humans)
        {
            if (mode == LobbyMenu.WorkshopMode) return "Solo";
            string seats = LobbyMenu.Seats(mode, humans);
            return string.IsNullOrEmpty(seats) ? humans == 1 ? "1 player" : humans + " players" : Capital(seats);
        }

        static RectTransform ScrollableChoices(RectTransform region)
        {
            var content = LobbyKit.Scroll(region, "House choices scroll", 8);
            var scroll = content.GetComponentInParent<ScrollRect>();
            var view = (RectTransform)scroll.transform;
            view.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            view.offsetMax = new Vector2(-18, 0);
            var track = LobbyKit.Rect(region, "Scroll position").Place(new Vector2(1, 0), Vector2.one,
                new Vector2(-8, 0), Vector2.zero);
            track.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            track.Paint(LobbyKit.Line, 4);
            var handle = LobbyKit.Rect(track, "Handle").Fill();
            var ink = handle.Paint(LobbyKit.Cyan, 4);
            var bar = track.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle; bar.targetGraphic = ink;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            return content;
        }

        static Button MapCard(Transform parent, string name, bool selected, Action click)
        {
            var card = LobbyKit.Button(parent, name, LobbyKit.Card, click, 14,
                selected ? LobbyKit.Selection : LobbyKit.Line, selected ? 3 : 1, 0);
            var press = card.GetComponent<LobbyPress>();
            press.Lift = 0; press.Tilt = 0; press.Sink = 1;
            var ink = selected ? LobbyParcel.Stamp : LobbyKit.Line;
            LobbyParcel.Dashes(card.Body(), new Color(ink.r, ink.g, ink.b, selected ? .9f : .7f), 5f);
            if (selected)
            {
                var mark = LobbyKit.Rect(card.Body(), "Selected").Pin(Vector2.one, new Vector2(-16, -16), new Vector2(34, 38));
                mark.localRotation = Quaternion.Euler(0f, 0f, -6f);
                mark.Paint(LobbyKit.Hex(0xFFF6E8), 2).raycastTarget = false;
                LobbyParcel.Dashes(mark, LobbyParcel.Stamp, 2f);
                var tick = LobbyKit.Icon(mark, LobbyIcons.Check, LobbyParcel.Stamp).rectTransform.Fill();
                tick.offsetMin = new Vector2(7, 9); tick.offsetMax = new Vector2(-7, -9);
            }
            return card;
        }

        static RectTransform PictureShape(Selectable card, string name, Color fill)
        {
            var area = LobbyKit.Rect(card.Body(), "Map picture").Place(Vector2.zero, Vector2.one,
                new Vector2(LobbyPlayCardGrid.Inset, LobbyPlayCardGrid.CaptionHeight), new Vector2(-LobbyPlayCardGrid.Inset, -LobbyPlayCardGrid.Inset));
            area.SetAsFirstSibling();
            var shape = LobbyKit.Rect(area, name).Fill();
            var fitter = shape.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = LobbyPlayCardGrid.PictureAspect;
            shape.Paint(fill, PictureRadius).raycastTarget = false;
            return shape;
        }

        static RectTransform CardPicture(Selectable card, Sprite picture, string icon, string art = null)
        {
            var shape = PictureShape(card, picture ? "Picture frame" : "Map fallback", picture ? Color.white : Color.Lerp(LobbyKit.Raised, LobbyKit.Selection, .14f));
            if (picture)
            {
                shape.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var image = LobbyKit.Rect(shape, "Map preview").Fill().gameObject.AddComponent<Image>();
                image.sprite = picture; image.preserveAspect = true; image.color = Color.white; image.raycastTarget = false;
                return shape;
            }
            var item = art == null ? null : LobbyKit.ItemImage(shape, art, 160);
            if (item)
            {
                item.rectTransform.Place(new Vector2(0, .12f), new Vector2(1, .88f));
                var square = item.gameObject.AddComponent<AspectRatioFitter>();
                square.aspectMode = AspectRatioFitter.AspectMode.FitInParent; square.aspectRatio = 1;
                return shape;
            }
            LobbyKit.Icon(shape, icon, new Color(LobbyKit.Selection.r, LobbyKit.Selection.g, LobbyKit.Selection.b, .9f)).rectTransform
                .Place(new Vector2(0, .3f), new Vector2(1, .7f));
            return shape;
        }

        Sprite CoursePreview(string id) => !playLayout ? null : id switch
        {
            WordboundSoloEntry.LostAddressMap => playLayout.LostAddressPreview,
            WordboundSoloEntry.ParcelHallMap => playLayout.ParcelHallPreview,
            WordboundSoloEntry.ParcelServiceMap => playLayout.ParcelServicePreview,
            _ => null,
        };

        void SoloMapCard(Transform parent, string id, string title, string detail)
        {
            string name = "Solo map " + id;
            var card = MapCard(parent, name, WordboundSoloEntry.SelectedMap == id, () => {
                WordboundSoloEntry.SelectMap(id); Refresh(); Reselect(name);
            });
            MapWords(card, CardPicture(card, LobbyMapSnapshots.For(id) ?? CoursePreview(id), LobbyIcons.Layers), title, detail);
        }

        static void MapWords(Selectable card, RectTransform picture, string title, string detail)
        {
            var scrim = LobbyKit.Rect(picture, NameScrim).Place(Vector2.zero, new Vector2(1, .55f));
            var shade = scrim.Paint(Color.white, PictureRadius);
            shade.raycastTarget = false;
            var dark = LobbyKit.ScrimNavy;
            LobbyKit.Gradient(shade, new Color(dark.r, dark.g, dark.b, 0f), new Color(dark.r, dark.g, dark.b, .8f));
            var name = LobbyKit.Text(picture, title, 24, Color.white, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            name.name = "Map title";
            name.enableAutoSizing = true; name.fontSizeMin = 14; name.fontSizeMax = 24;
            name.rectTransform.Place(Vector2.zero, new Vector2(1, .5f), new Vector2(TitleInset, 8), new Vector2(-TitleInset, 0));
            float inset = LobbyPlayCardGrid.Inset + 6;
            var text = LobbyKit.Text(card.Body(), detail, 17, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
            text.name = "Map detail";
            text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 17;
            text.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(inset, 4), new Vector2(-inset, LobbyPlayCardGrid.CaptionHeight - 2));
        }

        void HouseCard(Transform parent, string id, HouseLayout layout)
        {
            string name = "Map " + id;
            bool practice = Menu.Queue == LobbyMenu.Practice;
            bool open = Menu.Mode != LobbyMenu.WorkshopMode || HomeDesigner.Supports(id);
            bool ticked = Menu.SavedHome == null && (practice ? Menu.Maps.IsTicked(id) : Menu.Map == id);
            var card = MapCard(parent, name, ticked, () => {
                if (practice && !Menu.ToggleMap(id)) { Menu.Post(KeepOneMap); return; }
                if (!practice) Menu.Choose(map: id);
                Refresh(); Reselect(name);
            });
            MapWords(card, CardPicture(card, LobbyMapSnapshots.For(id), LobbyIcons.Home), layout.Name, MapCardText.HouseLine(layout));
            if (open) return;
            card.interactable = false;
            card.gameObject.AddComponent<CanvasGroup>().alpha = .55f;
            card.gameObject.AddComponent<LobbyHint>().Text = "The workshop builds homes in " + string.Join(" and ",
                GameConfig.Current.Houses.Where(h => HomeDesigner.Supports(h.Key)).Select(h => h.Value.Name));
        }

        void HomeCard(Transform parent, HomeLayout home)
        {
            string name = SavedHomeCard + home.Map;
            var house = GameConfig.Current.HouseFor(home.Map);
            var card = MapCard(parent, name, Menu.SavedHome == home.Map,
                () => { if (Menu.ChooseHome(home.Map)) { Refresh(); Reselect(name); } });
            MapWords(card, CardPicture(card, LobbyMapSnapshots.For(home.Map), LobbyIcons.Home), home.Name, "Your home" + MapCardText.Separator + house.Name);
        }

        static void TutorialRoom(Transform parent)
        {
            var note = LobbyKit.Rect(parent, "Tutorial room").Size(-1, HouseSize.y);
            LobbyKit.Face(note, LobbyKit.Card, 14, LobbyKit.Line, 1, 0);
            var art = LobbyKit.ItemImage(note, LobbyKit.ModeArt(LobbyMenu.TutorialMode), 172);
            if (art) art.rectTransform.Pin(new Vector2(0, .5f), new Vector2(24, 0), new Vector2(172, 172));
            var title = LobbyKit.Text(note, "Tutorial room", 26, LobbyKit.Navy);
            title.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(224, 150), new Vector2(-24, -70));
            var text = Wrapped(note, "Learn the game in two minutes: smash furniture, collect its letters, spell items and use them.", 19, LobbyKit.Muted);
            text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(224, 70), new Vector2(-24, -154));
        }


        RectTransform SummaryFrame(RectTransform body, string name) => authoredFrame
            ? LobbyKit.Rect(authoredFrame.Summary, name).Fill()
            : LobbyKit.Rect(body, name).Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 88));

        static void SummaryLine(RectTransform bar)
        {
            LobbyKit.Rect(bar, "Summary divider").Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -1), Vector2.zero)
                .Paint(LobbyKit.Line).raycastTarget = false;
        }

        void Footer(RectTransform body)
        {
            var bar = SummaryFrame(body, "Summary");
            SummaryLine(bar);
            string blocked = Menu.Blocked;
            string where = Menu.Mode == LobbyMenu.TutorialMode ? "The tutorial room" : Menu.MapLabel;
            var title = LobbyKit.Text(bar, LobbyMenu.ModeName(Menu.Mode) + MapCardText.Separator + where, 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            title.enableAutoSizing = true; title.fontSizeMin = 20; title.fontSizeMax = 26;
            title.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-250, -8));
            string detailText = blocked ?? Participants(Menu.Mode, Menu.PartySize);
            var detail = LobbyKit.Text(bar, detailText, 18, blocked == null ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Mode detail";
            detail.gameObject.SetActive(!string.IsNullOrEmpty(detailText));
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-250, -48));
            var go = LobbyKit.Primary(bar, "GO", "Play", Menu.Go, 42, 26);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-220, -30), new Vector2(-8, 30));
            go.interactable = blocked == null;
        }
    }

}
