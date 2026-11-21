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
        public const string ModeBoard = "Modes", ModeSections = "Sections";
        public const string KeepOneMap = "Keep at least one map ticked.";
        public const string GoalLine = "Mode goal", AccessLine = "Mode access", PlayOnline = "Play online";
        public const string NameScrim = "Map name scrim";
        public const string GuideIcon = "Guide icon", GuideHint = "How to play";
        public const string GuideStrip = "How to play strip", GuideStripTitle = "How to play title", GuideStep = "How to play step ";
        public const string GuideStepTitle = "Step title", GuideStepLine = "Step line", LastMatchLine = "Last match";
        public const string MapEditorTitle = "Map editor", MapEditorNote = "One-player playtests.";
        public const string ModeEmblem = "Mode emblem", SeatTag = "Seat tag", StartTag = "Start here tag";
        public const string MapsRow = "Maps row", AnyHouse = "Any house", Houses = "Houses", AnyHouseHint = "Each match picks one of the houses";
        public const string TutorialRoom = "Tutorial room", TutorialRow = "Tutorial maps";
        public const string CreateRow = "Create tools", CreateHouse = "Create house", CreateEditor = "Create Wordbound";
        public const string ToolTitle = "Tool title", ToolLine = "Tool line";
        public const string MapEditorLine = "Build course maps and test them on your own";
        public const string MapEditorMissing = "The map editor is unavailable in this build.";
        const string MapEditorArt = "TABLE", TutorialLine = "Learn the game in two minutes";
        const float ModeHeight = 52f;
        const float ModeLabelInset = 8f, ModeArt = 34f, TagSize = 15f, TagHeight = 22f, TagSlack = 4f, ClosedAlpha = .5f;
        const float ChipHeight = 44f, ChipIcon = 20f, ChipGap = 8f, ChipText = 18f, MapsRowGap = 16f;
        const int ChipRadius = 22, ChipPadLeft = 14, ChipPadRight = 18;
        const float ToolTitleSize = 28f, ToolLineSize = 18f, ToolInset = 16f, ToolArt = 240f;
        const int PictureRadius = 10, GuideRadius = 13, StepRadius = 14;
        const float GuideInset = 9f;
        const float StripHead = 28f, StripGap = 8f, StepSpacing = 12f, StepPadding = 14f, StepArt = 120f, StepTitle = 30f, StepLineGap = 4f, StepMinimumWidth = 360f;
        const float TitleInset = 12f;
        const float AccessTarget = 44f, AccessLabelRoom = 28f, SmallestLabel = 15f;
        const int WidestMapRow = 5, HouseRow = 6;

        static readonly (string id, string label)[] Queues =
        {
            ("Queue " + LobbyMenu.Practice, "Same screen"),
            ("Queue " + LobbyMenu.Matchmaking, "Online"),
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
            if (DisplayQueue == LobbyMenu.Workshop)
            {
                CreateTools(column, body);
                return;
            }
            PracticeModes(column);
            if (WordboundSolo)
            {
                SoloChoices(column, body);
                return;
            }
            if (Menu.Mode == LobbyMenu.TutorialMode)
            {
                LobbyKit.SectionLabel(column, "Room");
                TutorialCard(column);
                HowToPlay(column, Menu.Mode);
            }
            else
            {
                MapsHeader(column);
                var houses = MapCards(column, Houses);
                houses.GetComponent<LobbyPlayCardGrid>().MaximumColumns = HouseRow;
                foreach (var house in GameConfig.Current.Houses)
                    HouseCard(houses, house.Key, house.Value);
                if (HomeDesigner.Playable(Menu.Mode))
                    foreach (var home in HomeStorage.ForGame().Saved()) HomeCard(houses, home);
                HowToPlay(column, Menu.Mode);
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
            maps.GetComponent<LobbyPlayCardGrid>().MaximumColumns = HouseRow;
            SoloMapCard(maps, WordboundSoloEntry.ParcelServiceMap, "Parcel Depot", "Two-level depot");
            SoloMapCard(maps, WordboundSoloEntry.LostAddressMap, "The Lost Address", "Four-floor story");
            SoloMapCard(maps, WordboundSoloEntry.ParcelHallMap, "Parcel Hall", "Furnished hall");
            HowToPlay(column, WordboundSoloEntry.SelectedMode);
            var bar = SummaryFrame(body, "Solo summary");
            SummaryLine(bar);
            string blocked = Menu.PartySize != 1 ? "One player only. Remove the extra seats to start." :
                !Session.CanLoad(Session.WordboundScene) ? "Courses are unavailable in this build." : null;
            var heading = LobbyKit.Text(bar, CourseTitle, 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
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

        void CreateTools(RectTransform column, RectTransform body)
        {
            var row = LobbyKit.Rect(column, CreateRow);
            var fit = row.gameObject.AddComponent<LobbyPlayToolRow>();
            bool editor = Session.CanLoad(Session.WordboundScene);
            var house = ToolCard(row, CreateHouse, LobbyMenu.ModeName(LobbyMenu.WorkshopMode), LobbyPlayModeLabels.Goal(LobbyMenu.WorkshopMode),
                LobbyMapSnapshots.For(Menu.Map), LobbyKit.ModeArt(LobbyMenu.WorkshopMode), OpenHomeBuilder, true);
            var maps = ToolCard(row, CreateEditor, MapEditorTitle, editor ? MapEditorLine : MapEditorMissing, null, MapEditorArt, OpenMapEditor, editor);
            fit.Fit();
            if (WordboundMaps) WorkshopFooter(body, editor);
            else Footer(body);
            First = WordboundMaps && editor ? maps : house;
        }

        Button ToolCard(RectTransform row, string name, string title, string line, Sprite picture, string art, Action open, bool available)
        {
            var card = MapCard(row, name, false, open);
            card.interactable = available;
            var area = LobbyKit.Rect(card.Body(), "Map picture").Place(Vector2.zero, Vector2.one,
                new Vector2(LobbyPlayToolRow.Inset, LobbyPlayToolRow.Caption), new Vector2(-LobbyPlayToolRow.Inset, -LobbyPlayToolRow.Inset));
            area.SetAsFirstSibling();
            var shape = LobbyKit.Rect(area, picture ? "Picture frame" : "Map fallback").Fill();
            var aspect = shape.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = LobbyPlayCardGrid.PictureAspect;
            shape.Paint(picture ? Color.white : Color.Lerp(LobbyKit.Raised, LobbyKit.Selection, .14f), PictureRadius).raycastTarget = false;
            if (picture)
            {
                shape.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var image = LobbyKit.Rect(shape, "Map preview").Fill().gameObject.AddComponent<Image>();
                image.sprite = picture; image.preserveAspect = true; image.color = Color.white; image.raycastTarget = false;
            }
            else
            {
                var item = LobbyKit.ItemImage(shape, art, ToolArt);
                if (item)
                {
                    item.rectTransform.Place(new Vector2(0, .1f), new Vector2(1, .9f));
                    var square = item.gameObject.AddComponent<AspectRatioFitter>();
                    square.aspectMode = AspectRatioFitter.AspectMode.FitInParent; square.aspectRatio = 1;
                }
            }
            var heading = LobbyKit.Text(card.Body(), title, ToolTitleSize, LobbyKit.Navy, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            heading.name = ToolTitle;
            heading.enableAutoSizing = true; heading.fontSizeMin = SmallestLabel; heading.fontSizeMax = ToolTitleSize;
            heading.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(ToolInset, LobbyPlayToolRow.Caption * .5f), new Vector2(-ToolInset, LobbyPlayToolRow.Caption - LobbyPlayToolRow.Inset));
            var words = LobbyKit.Text(card.Body(), line, ToolLineSize, available ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            words.name = ToolLine;
            words.enableAutoSizing = true; words.fontSizeMin = SmallestLabel; words.fontSizeMax = ToolLineSize;
            words.rectTransform.Place(Vector2.zero, new Vector2(1, 0), new Vector2(ToolInset, LobbyPlayToolRow.Inset), new Vector2(-ToolInset, LobbyPlayToolRow.Caption * .5f - 2f));
            return card;
        }

        void OpenHomeBuilder()
        {
            if (Menu.Starting) return;
            WordboundMaps = false;
            Menu.Choose(queue: LobbyMenu.Workshop, mode: LobbyMenu.WorkshopMode);
            Menu.NoteChoice();
            Menu.Go();
        }

        void OpenMapEditor()
        {
            if (Menu.Starting || !Session.CanLoad(Session.WordboundScene)) return;
            WordboundMaps = true;
            Menu.NoteChoice();
            Menu.OpenWordboundWorkshop();
        }

        void WorkshopFooter(RectTransform body, bool available)
        {
            var bar = SummaryFrame(body, "Workshop summary");
            SummaryLine(bar);
            var heading = LobbyKit.Text(bar, MapEditorTitle, 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            heading.name = "Workshop title";
            heading.enableAutoSizing = true; heading.fontSizeMin = 20; heading.fontSizeMax = 26;
            heading.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-250, -8));
            var detail = LobbyKit.Text(bar, available ? MapEditorNote : MapEditorMissing,
                18, available ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Workshop detail";
            detail.enableAutoSizing = true; detail.fontSizeMin = SmallestLabel; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-250, -48));
            var open = LobbyKit.Primary(bar, "Workshop OPEN", PlayPageText.OpenWord, OpenMapEditor, 32, 24);
            ((RectTransform)open.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-220, -30), new Vector2(-8, 30));
            open.interactable = available;
        }

        public static string CourseTitle =>
            LobbyPlayModeLabels.Name(WordboundSoloEntry.SelectedMode) + MapCardText.Separator + SoloMapName(WordboundSoloEntry.SelectedMap);

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
            Menu.NoteChoice();
            Refresh();
            Reselect(id);
        }

        static string QueueLine(string queue) => queue == LobbyMenu.Matchmaking
            ? "Play with people online."
            : queue == LobbyMenu.Workshop ? "Build homes and maps." : "1 to 4 players on this screen. Bots fill empty seats.";

        string ShownMode => WordboundSolo ? WordboundSoloEntry.SelectedMode : Menu.Mode;

        public static string ModeButton(string mode) => (HouseModes.Has(mode) ? "Mode " : "Solo ") + mode;

        public static string SoloOnly(string mode) => LobbyPlayModeLabels.Name(mode) + " is solo only. Remove extra seats to play.";

        public static IReadOnlyList<HouseModeFamily> FamilyOrder => PlayPageText.Families(TutorialDirector.Done);

        public static bool SeatOpen(string mode, int partySize) => HouseModes.Has(mode) || partySize == 1;

        void PracticeModes(RectTransform column)
        {
            string shown = ShownMode;
            var board = LobbyKit.Rect(column, ModeBoard);
            var families = LobbyKit.Rect(board, ModeSections).gameObject.AddComponent<LobbyPlayModeFamilies>();
            families.Stacked = true;
            families.ButtonWidth = playLayout ? playLayout.ModeMinimumWidth : 104;
            families.ButtonHeight = playLayout ? playLayout.ModeHeight : ModeHeight;
            var closed = new List<(Button button, string mode)>();
            foreach (var family in FamilyOrder)
            {
                var section = Family(families.transform, HouseModes.FamilyName(family));
                foreach (string mode in HouseModes.ModesIn(family))
                {
                    var button = PracticeChoice(section, mode);
                    if (!SeatOpen(mode, Menu.PartySize)) closed.Add((button, mode));
                }
            }
            var line = Goal(board, LobbyPlayModeLabels.Line(shown));
            GuideButton(line, shown);
            foreach (var (button, mode) in closed) ExplainClosed(button, line, mode);
            var fit = board.gameObject.AddComponent<LobbyPlayModeBoard>();
            fit.Sections = families; fit.Goal = line; fit.Access = Access(board, shown);
            fit.Fit();
        }

        TextMeshProUGUI Access(Transform parent, string mode)
        {
            var access = ModeAccess.For(mode);
            if (access.Line.Length == 0) return null;
            var line = LobbyKit.Text(parent, access.Line, 16, LobbyKit.Muted, TextAlignmentOptions.Center);
            line.name = AccessLine;
            line.enableAutoSizing = true; line.fontSizeMin = SmallestLabel; line.fontSizeMax = 16;
            if (access.SameScreen && access.OnlineCard != null) PlayOnlineButton(line, PlaylistCard + access.OnlineCard);
            return line;
        }

        void PlayOnlineButton(TextMeshProUGUI line, string card)
        {
            var button = LobbyKit.TextAction(line.transform, PlayOnline, PlayOnline, 16, () => OpenOnline(card));
            var label = button.GetComponentInChildren<TMP_Text>();
            label.fontSizeMin = SmallestLabel;
            float width = label.GetPreferredValues(PlayOnline).x + AccessLabelRoom;
            var rect = ((RectTransform)button.transform).Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(width, AccessTarget));
            var info = line.gameObject.AddComponent<LobbyPlayGoalInfo>();
            info.Line = line; info.Icon = rect; info.IconWidth = width;
        }

        void OpenOnline(string card)
        {
            Online = true; Friends = false;
            Aim = card;
            Menu.NoteChoice();
            Refresh();
            Reselect(card);
        }

        static RectTransform Family(Transform sections, string name)
        {
            var family = LobbyKit.Rect(sections, name);
            var caption = LobbyKit.Text(family, LobbyKit.Upper(name), 14, LobbyKit.Muted, TextAlignmentOptions.BottomLeft);
            caption.name = LobbyPlayModeFamilies.Caption;
            caption.characterSpacing = 1;
            caption.margin = new Vector4(ModeLabelInset, 0, 0, 0);
            return family;
        }

        Button PracticeChoice(Transform section, string mode)
        {
            string id = ModeButton(mode);
            bool open = SeatOpen(mode, Menu.PartySize);
            bool selected = open && ShownMode == mode;
            Action pick = open ? () => { PickMode(mode); Refresh(); Reselect(id); } : () => Menu.Post(SoloOnly(mode));
            var button = CompactMode(section, id, LobbyPlayModeLabels.Name(mode), selected, pick);
            var content = button.Body().Find("Content");
            content.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;
            ModePicture(content, mode);
            if (!HouseModes.Has(mode)) Tag(content, SeatTag, ModeAccess.OnePlayer, LobbyKit.Muted, !open);
            if (mode == LobbyMenu.TutorialMode && !TutorialDirector.Done) Tag(content, StartTag, PlayPageText.StartHere, LobbyKit.Cyan, true);
            if (!open) button.gameObject.AddComponent<CanvasGroup>().alpha = ClosedAlpha;
            if (selected) First = button;
            return button;
        }

        static void ModePicture(Transform content, string mode)
        {
            var art = LobbyKit.ItemImage(content, LobbyPlayModeLabels.Emblem(mode), ModeArt);
            if (!art) return;
            art.name = ModeEmblem;
            art.Size(ModeArt, ModeArt);
            art.transform.SetAsFirstSibling();
        }

        static void Tag(Transform content, string name, string text, Color ink, bool shown)
        {
            var room = LobbyKit.Rect(content, name);
            var tag = LobbyKit.Caps(room, text, TagSize, TextAlignmentOptions.MidlineLeft);
            tag.color = ink;
            tag.rectTransform.Fill();
            room.Size(Mathf.Ceil(tag.GetPreferredValues(tag.text).x) + TagSlack, TagHeight);
            tag.gameObject.SetActive(shown);
        }

        static void ExplainClosed(Button button, TextMeshProUGUI goal, string mode)
        {
            button.gameObject.AddComponent<LobbyHint>().Text = SoloOnly(mode);
            if (!goal) return;
            var swap = button.gameObject.AddComponent<LobbyPlayGoalSwap>();
            swap.Goal = goal; swap.Original = goal.text; swap.Reason = SoloOnly(mode);
        }

        void PickMode(string mode)
        {
            WordboundSolo = !HouseModes.Has(mode);
            if (WordboundSolo) WordboundSoloEntry.Select(mode);
            else Menu.Choose(queue: LobbyMenu.Practice, mode: mode);
            Menu.NoteChoice(true);
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

        static Button CompactMode(Transform parent, string id, string label, bool selected, Action action) =>
            LobbyKit.TextAction(parent, id, label, 20, action, selected);

        RectTransform MapCards(Transform parent, string name)
        {
            float width = LobbyPlayCardGrid.PreferredWidth, height = LobbyPlayCardGrid.HeightFor(width);
            var rect = LobbyKit.Grid(parent, name, new Vector2(width, height), 12).Size(-1, height);
            var grid = rect.gameObject.AddComponent<LobbyPlayCardGrid>();
            grid.MinimumWidth = playLayout ? playLayout.MapMinimumWidth : 264;
            grid.MaximumColumns = playLayout ? playLayout.MapColumns : WidestMapRow;
            return rect;
        }

        void MapsHeader(RectTransform column)
        {
            var row = LobbyKit.Row(column, MapsRow, MapsRowGap);
            row.Size(-1, ChipHeight);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            var label = LobbyKit.SectionLabel(row, "Maps");
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.Size(Mathf.Ceil(label.GetPreferredValues(label.text).x) + TagSlack, ChipHeight);
            AnyHouseChip(row, Menu.SavedHome == null && Menu.Maps.All);
        }

        void AnyHouseChip(Transform row, bool lit)
        {
            var chip = LobbyKit.Button(row, AnyHouse, lit ? LobbyKit.Picked : LobbyKit.Card, () => PickAnyHouse(AnyHouse), ChipRadius,
                lit ? LobbyKit.Selection : LobbyKit.Line, lit ? 2 : 1);
            var content = LobbyKit.Row(chip.Body(), "Content", ChipGap);
            content.Fill();
            var layout = content.GetComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter; layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(ChipPadLeft, ChipPadRight, 0, 0);
            LobbyKit.Icon(content, LobbyIcons.Check, lit ? LobbyKit.Selection : LobbyKit.Line).Size(ChipIcon, ChipIcon);
            var label = LobbyKit.Text(content, PracticeMaps.Every, ChipText, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft);
            label.Size(-1, ChipText + 10);
            float width = ChipPadLeft + ChipIcon + ChipGap + Mathf.Ceil(label.GetPreferredValues(label.text).x) + ChipPadRight;
            chip.Size(width, ChipHeight);
            chip.gameObject.AddComponent<LobbyHint>().Text = AnyHouseHint;
        }

        void PickAnyHouse(string aim)
        {
            Menu.Maps.TickAll();
            Session.LobbyMaps = Menu.Maps.Save();
            Menu.Choose(map: Menu.Map);
            Menu.NoteChoice(true);
            Refresh(); Reselect(aim);
        }

        void PickHouse(string id, string name)
        {
            if (Menu.SavedHome == null && Menu.Maps.All) Menu.PlayOnly(id);
            else if (!Menu.ToggleMap(id)) { PickAnyHouse(name); return; }
            Menu.NoteChoice(true);
            Refresh(); Reselect(name);
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
                WordboundSoloEntry.SelectMap(id); Menu.NoteChoice(true); Refresh(); Reselect(name);
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

        static TextMeshProUGUI RowWords(Selectable card, RectTransform picture, string title, string detail)
        {
            var name = LobbyKit.Text(card.Body(), title, 24, LobbyKit.Navy, TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            name.name = "Map title";
            name.textWrappingMode = TextWrappingModes.Normal;
            name.enableAutoSizing = true; name.fontSizeMin = SmallestLabel; name.fontSizeMax = 24;
            var text = LobbyKit.Text(card.Body(), detail, 17, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            text.name = "Map detail";
            text.textWrappingMode = TextWrappingModes.Normal;
            text.enableAutoSizing = true; text.fontSizeMin = SmallestLabel; text.fontSizeMax = 17;
            var row = card.gameObject.AddComponent<LobbyPlayRowCard>();
            row.Picture = (RectTransform)picture.parent; row.Title = name.rectTransform; row.Detail = text.rectTransform;
            row.Fit();
            return text;
        }

        void HouseCard(Transform parent, string id, HouseLayout layout)
        {
            string name = "Map " + id;
            bool ticked = Menu.SavedHome == null && !Menu.Maps.All && Menu.Maps.IsTicked(id);
            var card = MapCard(parent, name, ticked, () => PickHouse(id, name));
            MapWords(card, CardPicture(card, LobbyMapSnapshots.For(id), LobbyIcons.Home), layout.Name, MapCardText.HouseLine(layout));
        }

        void HomeCard(Transform parent, HomeLayout home)
        {
            string name = SavedHomeCard + home.Map;
            var house = GameConfig.Current.HouseFor(home.Map);
            var card = MapCard(parent, name, Menu.SavedHome == home.Map,
                () => { if (Menu.ChooseHome(home.Map)) { Menu.NoteChoice(true); Refresh(); Reselect(name); } });
            MapWords(card, CardPicture(card, LobbyMapSnapshots.For(home.Map), LobbyIcons.Home), home.Name, "Your home" + MapCardText.Separator + house.Name);
        }

        void TutorialCard(RectTransform column)
        {
            var row = MapCards(column, TutorialRow);
            row.GetComponent<LobbyPlayCardGrid>().MaximumColumns = HouseRow;
            var card = MapCard(row, TutorialRoom, true, null);
            card.interactable = false;
            MapWords(card, CardPicture(card, null, LobbyIcons.Home, LobbyKit.ModeArt(LobbyMenu.TutorialMode)), TutorialRoom, TutorialLine);
        }

        void HowToPlay(RectTransform column, string mode, bool sameScreen = true)
        {
            var guide = ModeGuideBook.For(mode);
            if (guide == null || guide.Steps.Count == 0) return;
            int count = guide.Steps.Count;
            var strip = LobbyKit.Rect(column, GuideStrip);
            var content = LobbyKit.Rect(strip, "Content").Fill();
            var head = LobbyKit.SectionLabel(content, LobbyPlayDetail.GuideWord);
            head.name = GuideStripTitle;
            head.rectTransform.Place(new Vector2(0, 1), new Vector2(.5f, 1), new Vector2(0, -StripHead), Vector2.zero);
            string last = sameScreen ? ModeAccess.LastMatch(Menu.Career.History, mode) : "";
            if (last.Length > 0)
            {
                var result = LobbyKit.Text(content, last, 17, LobbyKit.Muted, TextAlignmentOptions.BottomRight);
                result.name = LastMatchLine;
                result.rectTransform.Place(new Vector2(.5f, 1), Vector2.one, new Vector2(0, -StripHead), Vector2.zero);
            }
            var device = ModeGuidePanel.Pressed(null) ?? ModeGuides.Fallback();
            for (int i = 0; i < count; i++) GuideStepCard(content, guide.Steps[i], i, count, device);
            strip.gameObject.AddComponent<LobbyPlaySpare>().Watch(content, count * StepMinimumWidth + (count - 1) * StepSpacing);
        }

        static void GuideStepCard(Transform content, ModeGuideStep step, int index, int count, ControlDevice device)
        {
            float share = 1f / count;
            var card = LobbyKit.Rect(content, GuideStep + (index + 1)).Place(new Vector2(index * share, 0), new Vector2((index + 1) * share, 1),
                new Vector2(index * StepSpacing * share, 0), new Vector2(-(count - 1 - index) * StepSpacing * share, -StripHead - StripGap));
            LobbyKit.Face(card, LobbyKit.Card, StepRadius, LobbyKit.Line, 1, 0).raycastTarget = false;
            var art = LobbyKit.Rect(card, "Step art").Place(Vector2.zero, new Vector2(0, 1),
                new Vector2(StepPadding, StepPadding), new Vector2(StepPadding + StepArt, -StepPadding));
            LobbyKit.ItemImage(art, step.Art, StepArt);
            float words = StepPadding * 2 + StepArt;
            var title = LobbyKit.Text(card, (index + 1) + LobbyPlayDetail.StepSeparator + step.Title, 20, LobbyKit.Navy,
                TextAlignmentOptions.BottomLeft, FontStyles.Bold);
            title.name = GuideStepTitle;
            title.enableAutoSizing = true; title.fontSizeMin = SmallestLabel; title.fontSizeMax = 20;
            title.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(words, -StepPadding - StepTitle), new Vector2(-StepPadding, -StepPadding));
            var line = LobbyKit.Text(card, ModeGuides.Line(step, device), 17, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            line.name = GuideStepLine;
            line.textWrappingMode = TextWrappingModes.Normal;
            line.enableAutoSizing = true; line.fontSizeMin = SmallestLabel; line.fontSizeMax = 17;
            line.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(words, StepPadding),
                new Vector2(-StepPadding, -StepPadding - StepTitle - StepLineGap));
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
            var title = LobbyKit.Text(bar, Menu.MatchTitle, 26, LobbyKit.Navy, TextAlignmentOptions.BottomLeft);
            title.enableAutoSizing = true; title.fontSizeMin = 20; title.fontSizeMax = 26;
            title.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 44), new Vector2(-250, -8));
            string detailText = blocked ?? Participants(Menu.Mode, Menu.PartySize);
            var detail = LobbyKit.Text(bar, detailText, 18, blocked == null ? LobbyKit.Muted : LobbyKit.Hot, TextAlignmentOptions.TopLeft);
            detail.name = "Mode detail";
            detail.gameObject.SetActive(!string.IsNullOrEmpty(detailText));
            detail.enableAutoSizing = true; detail.fontSizeMin = 14; detail.fontSizeMax = 18;
            detail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-250, -48));
            string word = Menu.Queue == LobbyMenu.Workshop ? PlayPageText.OpenWord : "Play";
            var go = LobbyKit.Primary(bar, "GO", word, Menu.Go, 42, 26);
            ((RectTransform)go.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-220, -30), new Vector2(-8, 30));
            go.interactable = blocked == null;
        }
    }

}
