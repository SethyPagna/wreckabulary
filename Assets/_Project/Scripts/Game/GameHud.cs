using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// The match HUD, laid out like the browser edition: brand and match label top left; minimap with timer,
    /// room and objective in one column top right; vitals with both hands bottom left; a 5 x 2 letter tray
    /// bottom centre; a desktop key bar; touch buttons only on touch; a hold-Tab bag and map panel; an Esc pause card.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI title, subtitle, timer, scoreboard, instruction, checklist;
        [SerializeField] GameObject checklistPanel;
        public Canvas UiCanvas { get; private set; }
        public bool TouchControlsShown => touchRoot && touchRoot.activeSelf;
        public PlayerController LocalPlayer { get; private set; }
        /// <summary>The Tab bag and map panel is showing.</summary>
        public bool BagOpen => bagPanel && bagPanel.activeSelf;
        public bool Paused => pauseRoot && pauseRoot.activeSelf;
        /// <summary>The HUD of the scene being played (one per match scene).</summary>
        public static GameHud Active { get; private set; }
        /// <summary>Something on screen wants the mouse pointer, so the third-person view lets the cursor go.</summary>
        public bool NeedsPointer => pointerFreed || Paused || BagOpen || (craftRoot && craftRoot.activeSelf) || (typewriter && typewriter.User) || ModeActions.AnyShown;
        /// <summary>Losing the window pauses a match, as on the web. Batch runs have no window; tests set this.</summary>
        public static bool? PauseOnFocusLossOverride;
        static bool PauseOnFocusLoss => PauseOnFocusLossOverride ?? !Application.isBatchMode;

        // The browser edition's palette (Web/src/style.css).
        static readonly Color Ink = Hex(0x173b3c), Cream = Hex(0xfff0d9), Paper = Hex(0xfff0dc, .92f);
        static readonly Color Teal = Hex(0x193f3d, .93f), Tile = Hex(0xf4dca7), TileEdge = Hex(0xbba06b);
        static readonly Color Slot = Hex(0xf7e5c8), SlotActive = Hex(0xdeedcf), Track = Hex(0x153c3c, .13f);
        static readonly Color Health = Hex(0x419681), Low = Hex(0xd16849), Faded = Hex(0x62736a);
        static readonly Color EmptyCell = Hex(0xead9b7, .35f), Reserved = Hex(0xc8e6d8), ReservedInk = Hex(0x2f6b5c);
        static readonly Color MapBg = Hex(0x193c3b, .85f), MapEdge = Hex(0xf1dfbd, .85f), RoomFill = Hex(0xaaad82, .38f);
        static readonly Color RoomEdge = Hex(0xe4d6b2, .53f), RoomEdgeHere = Hex(0xc9ffe9);
        static readonly Color RoomHere = Hex(0x9ff8d3, .35f), RoomWarn = Hex(0xe79553), RoomClosed = Hex(0xd85c45, .65f);
        static readonly Color Mint = Hex(0xb7dfc8), Coral = Hex(0xe7785e), Dark = Hex(0x1a2a30, .94f);
        // Straight on the game, as on the web: light text with a dark edge, and glass hand slots.
        static readonly Color OnGame = Hex(0xfff7e8), LowHp = Hex(0xff8a6b), Shade = Hex(0x0b1f1f, .8f);
        static readonly Color Glass = Hex(0x1d2b2b, .28f), GlassActive = Hex(0x1d2b2b, .40f), GlassEdge = Hex(0xfff7e8, .85f), GlassIdle = Hex(0xffffff, .17f);
        // The Tab bag is the web's dark glass (style.css .bag-panel): cream on a scrim, with glass cells, cards and chips.
        static readonly Color BagInk = Hex(0xfff4e2), BagCell = Hex(0xffffff, .08f), BagCellEdge = Hex(0xffffff, .25f);
        static readonly Color BagCard = Hex(0xffffff, .09f), BagCardActive = Hex(0xffffff, .15f), BagCardEdge = Hex(0xffffff, .24f);
        static readonly Color Gold = Hex(0xffe7b8), GoldGlow = Hex(0xffd46a, .4f), TileGot = Hex(0xe8c48b), TileGotInk = Hex(0x3b2614);
        static readonly Color TileMissing = Hex(0xffffff, .12f), TileMissingInk = Hex(0xffffff, .65f);
        static readonly string[] WearSlots = { "Headwear", "Face", "Top", "Gloves", "Bottoms", "Footwear", "Back", "Badge" };
        // Bag layout in canvas units (the web's CSS pixels x 1.25 on a 1920 x 1080 canvas).
        const float BagTop = 105f, BagSide = 35f, BagBottom = 35f, BagColumn = 375f, BookColumn = 413f, BagGap = 17.5f;
        const float Chip = 52f, ChipGap = 10f;
        const int ChipsPerRow = 6;

        readonly List<PlayerCard> cards = new();
        readonly List<RecipeView> recipes = new();
        readonly List<Sprite> ownedSprites = new();
        readonly List<Texture2D> ownedTextures = new();
        readonly List<TouchStick> sticks = new();
        readonly Dictionary<TouchAction, (TouchActionButton button, TextMeshProUGUI label)> skillButtons = new();
        readonly Dictionary<string, Texture2D> itemIcons = new(StringComparer.Ordinal);
        readonly StringBuilder sb = new();
        // The tray is two rows of five (the 10-letter bag); the capacity from rules.json decides how many are visible.
        const int TrayTiles = 10, TilesPerRow = 5;
        readonly LetterCell[] trayCells = new LetterCell[TrayTiles], bagCells = new LetterCell[TrayTiles];
        readonly HandView[] hands = new HandView[2], bagHands = new HandView[2];
        IReadOnlyList<PlayerController> players;
        Func<PlayerController, int> wins;
        int roundsToWin;
        bool showWins;
        PlayerJoinManager joins;
        Typewriter typewriter;
        RoomBuilder rooms;
        ClearOutController clearOut;
        RoundManager rounds;
        RectTransform safe, side, status, vitals, tray, hint;
        GameObject touchRoot, craftRoot, desktopHints, bagPanel, pauseRoot, crosshair;
        GameObject typewriterControls;
        TextMeshProUGUI typewriterChoice;
        TextMeshProUGUI matchTag, matchTitle, matchDetail, roomPill, objective, statusText;
        TextMeshProUGUI aliveText, hintText, hpValue, shieldText, bagCount, bagPanelCount, touchToggle;
        TextMeshProUGUI craftStatus, buildLabel, playLabel;
        Image craftProgress;
        Button buildButton;
        RectTransform playRect;
        MapView miniMap, bigMap;
        Texture2D iconAtlas;
        Sprite roundSprite, panelSprite, ringSprite;
        Rect lastSafe;
        int lastWidth, lastHeight;
        float nextRefresh, resumeScale = 1f;
        bool bagPinned;
        // The Tab bag's pieces: what it hides, its fade, its chips and the recipe book.
        readonly List<CanvasGroup> bagHides = new();
        readonly List<(GameObject root, RawImage icon)> wearChips = new();
        readonly List<(GameObject root, RawImage icon, GameObject badge, TextMeshProUGUI seconds)> effectChips = new();
        readonly List<BookCard> bookCards = new();
        readonly List<char> spare = new();
        readonly Dictionary<string, Texture2D> glyphs = new(StringComparer.Ordinal);
        RectTransform bagColumn, bagLetters, bagHandsRow, bagWearRow, bagEffectsRow, bagMapFrame, bagMapShadow, bagBackdrop, pauseShade, titlePlaque;
        CanvasGroup bagFade;
        Sprite hairRing, circleRing, softSprite, lineBox;
        float bagOpenedAt;
        bool bagHidden;
        /// <summary>The on-screen buttons come up by themselves on the first real touch, until someone picks in the pause card.</summary>
        bool touchChosen;
        /// <summary>The key bar says "mouse look" in the third-person view and "mouse aim" under the overhead camera.</summary>
        bool? hintLooks;
        /// <summary>Esc in the Hub (which never pauses) lets the cursor go until a click back on the game.</summary>
        bool pointerFreed;
        string checklistText = "";

        sealed class PlayerCard
        {
            public GameObject root;
            public Image badge, fill;
            public TextMeshProUGUI initial, name, status;
        }
        sealed class RecipeView
        {
            public Button button;
            public Image face;
            public TextMeshProUGUI text;
            public int index;
            public RawImage icon;
        }
        sealed class LetterCell
        {
            public Image face, edge, ring;
            public TextMeshProUGUI letter;
        }
        sealed class HandView
        {
            public Image face, wear;
            public Image edge;
            public RawImage icon, empty;
            public TextMeshProUGUI label;
            public GameObject wearTrack;
        }
        sealed class MapView
        {
            public RectTransform content;
            public readonly List<(RoomBox box, Image fill, Image edge)> rooms = new();
            public readonly List<Image> dots = new();
            public readonly List<GameObject> stairs = new();
            public TextMeshProUGUI badge;
            public bool full;
            public HouseLayout layout;
            public int storey = -1;
        }
        /// <summary>A word in the bag's recipe book, with its letters marked as you hold them.</summary>
        sealed class BookCard
        {
            public string word;
            public Button button;
            public Image face, edge, glow;
            public Image[] tiles;
            public TextMeshProUGUI[] letters;
        }

        static Color Hex(int rgb, float a = 1f) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        void Start()
        {
            UiCanvas = GetComponentInParent<Canvas>();
            if (!UiCanvas) UiCanvas = gameObject.AddComponent<Canvas>();
            UiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            if (!UiCanvas.TryGetComponent<GraphicRaycaster>(out _)) UiCanvas.gameObject.AddComponent<GraphicRaycaster>();
            var scaler = UiCanvas.GetComponent<CanvasScaler>() ?? UiCanvas.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            if (!EventSystem.current)
            {
                var events = new GameObject("UI Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            joins = FindFirstObjectByType<PlayerJoinManager>();
            typewriter = FindFirstObjectByType<Typewriter>();
            rooms = FindFirstObjectByType<RoomBuilder>();
            clearOut = FindFirstObjectByType<ClearOutController>();
            rounds = FindFirstObjectByType<RoundManager>();
            iconAtlas = Resources.Load<Texture2D>("UI/ActionIcons");
            foreach (var item in GameConfig.Current.Items.All)
            {
                var icon = Resources.Load<Texture2D>("UI/Items/" + item.Id);
                if (icon) itemIcons[item.Id] = icon;
            }
            roundSprite = MakeShape(true);
            panelSprite = MakeShape(false);
            ringSprite = MakeShape(false, 3f);
            hairRing = MakeShape(false, 1.6f);
            circleRing = MakeShape(true, 2f);
            softSprite = MakeSoft();
            lineBox = MakeLineBox();
            safe = Rect("Safe HUD", UiCanvas.transform, Vector2.zero, Vector2.zero, Vector2.zero);
            safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            StyleLegacyText();
            BuildTop(); BuildSide(); BuildStatus(); BuildVitals(); BuildTray(); BuildHint();
            BuildCraftDrawer(); BuildTouchControls(); BuildNavigation(); BuildBagPanel(); BuildPause(); BuildCrosshair();
            // The web hides everything but the brand and match label behind its bag (style.css .hud.bag-open).
            foreach (var part in new[] { side, tray, vitals, hint, status, (RectTransform)craftRoot.transform, (RectTransform)touchRoot.transform, (RectTransform)typewriterControls.transform }
                .Concat(cards.Select(c => (RectTransform)c.root.transform)))
                bagHides.Add(part.gameObject.AddComponent<CanvasGroup>());
            // A countdown would show through the see-through map.
            if (titlePlaque) bagHides.Add(titlePlaque.gameObject.AddComponent<CanvasGroup>());
            // A laptop's touchscreen alone doesn't bring the buttons up; a real touch does (R41).
            ShowTouchControls(Application.isMobilePlatform);
            ApplySafeArea();
        }

        // ---- Layout ----

        void StyleLegacyText()
        {
            if (scoreboard) scoreboard.gameObject.SetActive(false);
            foreach (var line in new[] { title, subtitle })
            {
                if (!line) continue;
                line.raycastTarget = false; line.outlineWidth = 0f; line.alignment = TextAlignmentOptions.Center;
                line.textWrappingMode = TextWrappingModes.NoWrap; line.overflowMode = TextOverflowModes.Overflow;
            }
            if (title) { title.fontSize = 72f; title.color = Cream; }
            if (subtitle) { subtitle.fontSize = 26f; subtitle.color = Hex(0xfff0d9, .82f); }
            if (title) BuildTitlePlaque();
            // The objective lives in the side column now; the scene's checklist objects stay hidden.
            if (checklist) checklist.gameObject.SetActive(false);
            if (checklistPanel) checklistPanel.SetActive(false);
        }

        /// <summary>
        /// Round titles sit on an ink plaque, so a countdown or a winner reads over a cream floor; the bare white title
        /// with an outline didn't. The plaque fits its words and goes away with them.
        /// </summary>
        void BuildTitlePlaque()
        {
            var at = title.rectTransform;
            titlePlaque = Panel("Title plaque", at.parent, at.anchorMin, at.anchoredPosition, new Vector2(720f, 120f), new Vector2(.5f, .5f), Hex(0x173b3c, .85f));
            titlePlaque.SetSiblingIndex(at.GetSiblingIndex());
            titlePlaque.GetComponent<Image>().pixelsPerUnitMultiplier = 11f / 24f;
            void Line(TMP_Text line, float y, float height)
            {
                var rt = line.rectTransform;
                rt.SetParent(titlePlaque, false);
                rt.anchorMin = new Vector2(0f, y); rt.anchorMax = new Vector2(1f, y); rt.pivot = new Vector2(.5f, y);
                rt.anchoredPosition = new Vector2(0f, y > .5f ? -8f : 14f); rt.sizeDelta = new Vector2(-60f, height);
            }
            Line(title, 1f, 100f);
            if (subtitle) Line(subtitle, 0f, 40f);
            FitTitlePlaque();
        }

        void FitTitlePlaque()
        {
            if (!titlePlaque) return;
            string main = title.text ?? "", sub = subtitle ? subtitle.text ?? "" : "";
            bool shown = main.Length > 0 || sub.Length > 0;
            titlePlaque.gameObject.SetActive(shown);
            if (!shown) return;
            float width = Mathf.Max(main.Length > 0 ? title.GetPreferredValues(main).x : 0f, sub.Length > 0 ? subtitle.GetPreferredValues(sub).x : 0f) + 96f;
            titlePlaque.sizeDelta = new Vector2(Mathf.Clamp(width, 720f, 1500f), 10f + (main.Length > 0 ? 110f : 0f) + (sub.Length > 0 ? 56f : 0f));
        }

        void BuildTop()
        {
            var brand = Panel("Brand", safe, new Vector2(0f, 1f), new Vector2(38f, -30f), new Vector2(81f, 81f), new Vector2(0f, 1f), Hex(0xf2d295));
            brand.localRotation = Quaternion.Euler(0f, 0f, 5f);
            // The browser's brand tile is its pause button; there is no separate one.
            brand.GetComponent<Image>().raycastTarget = true;
            brand.gameObject.AddComponent<Button>().onClick.AddListener(TogglePause);
            Text("W", brand, new Vector2(.5f, .5f), new Vector2(-4f, 0f), new Vector2(70f, 70f), 48f, Ink).text = "W<size=55%><color=#B86647>!</color></size>";
            var label = Panel("Match label", safe, new Vector2(0f, 1f), new Vector2(134f, -30f), new Vector2(250f, 84f), new Vector2(0f, 1f), Paper);
            matchTag = Text("Tag", label, new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(220f, 18f), 12f, Ink, TextAlignmentOptions.Left);
            matchTag.characterSpacing = 8f; matchTag.rectTransform.pivot = new Vector2(0f, 1f);
            matchTitle = Text("Title", label, new Vector2(0f, 1f), new Vector2(18f, -29f), new Vector2(220f, 32f), 27f, Ink, TextAlignmentOptions.Left);
            matchTitle.rectTransform.pivot = new Vector2(0f, 1f);
            matchDetail = Text("Detail", label, new Vector2(0f, 1f), new Vector2(18f, -61f), new Vector2(220f, 16f), 12f, Ink, TextAlignmentOptions.Left);
            matchDetail.characterSpacing = 4f; matchDetail.rectTransform.pivot = new Vector2(0f, 1f);

            // Couch roommates (and AI seats) under the label; the local player has the vitals card instead.
            for (int i = 0; i < 4; i++)
            {
                var rt = Panel($"Roommate {i + 1}", safe, new Vector2(0f, 1f), new Vector2(38f, -134f - i * 62f), new Vector2(300f, 54f), new Vector2(0f, 1f), Paper);
                var badge = Panel("Initial badge", rt, new Vector2(0f, .5f), new Vector2(9f, 0f), new Vector2(38f, 38f), new Vector2(0f, .5f), Mint, true).GetComponent<Image>();
                var card = new PlayerCard { root = rt.gameObject, badge = badge };
                card.initial = Text("Initial", badge.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(36f, 36f), 22f, Ink);
                card.name = Text("Name", rt, new Vector2(0f, 1f), new Vector2(56f, -6f), new Vector2(150f, 22f), 16f, Ink, TextAlignmentOptions.Left);
                card.name.rectTransform.pivot = new Vector2(0f, 1f);
                card.status = Text("Status", rt, new Vector2(1f, 1f), new Vector2(-12f, -8f), new Vector2(110f, 18f), 12f, Faded, TextAlignmentOptions.Right);
                card.status.rectTransform.pivot = new Vector2(1f, 1f);
                card.fill = Fill("Health", Panel("Health track", rt, new Vector2(0f, 0f), new Vector2(56f, 10f), new Vector2(230f, 8f), Vector2.zero, Track), Health);
                cards.Add(card); rt.gameObject.SetActive(false);
            }
        }

        void BuildSide()
        {
            side = Rect("Side column", safe, Vector2.one, new Vector2(-38f, -30f), new Vector2(252f, 560f));
            side.pivot = Vector2.one;
            var mapFrame = Panel("Minimap", side, new Vector2(0f, 1f), Vector2.zero, new Vector2(252f, 252f), new Vector2(0f, 1f), MapEdge);
            miniMap = MakeMap(mapFrame, false, 7f);
            // Who's still up, before the timer.
            var alive = Panel("Alive", side, new Vector2(0f, 1f), new Vector2(0f, -262f), new Vector2(96f, 54f), new Vector2(0f, 1f), Teal);
            Panel("Head", alive, new Vector2(0f, .5f), new Vector2(21f, 8f), new Vector2(12f, 12f), new Vector2(.5f, .5f), Cream, true);
            Panel("Shoulders", alive, new Vector2(0f, .5f), new Vector2(21f, -8f), new Vector2(22f, 14f), new Vector2(.5f, .5f), Cream, true);
            Panel("Head behind", alive, new Vector2(0f, .5f), new Vector2(35f, 10f), new Vector2(10f, 10f), new Vector2(.5f, .5f), Hex(0xfff0d9, .6f), true);
            aliveText = Text("Count", alive, new Vector2(1f, .5f), new Vector2(-12f, 0f), new Vector2(50f, 40f), 21f, Cream, TextAlignmentOptions.Right);
            aliveText.rectTransform.pivot = new Vector2(1f, .5f);
            var timerPill = Panel("Timer", side, new Vector2(0f, 1f), new Vector2(102f, -262f), new Vector2(150f, 54f), new Vector2(0f, 1f), Teal);
            if (timer)
            {
                // The scene's timer text moves into the pill, so every director keeps calling SetTimer.
                timer.rectTransform.SetParent(timerPill, false);
                timer.rectTransform.anchorMin = Vector2.zero; timer.rectTransform.anchorMax = Vector2.one;
                timer.rectTransform.pivot = new Vector2(.5f, .5f);
                timer.rectTransform.offsetMin = timer.rectTransform.offsetMax = Vector2.zero;
                timer.fontSize = 27f; timer.color = Cream; timer.alignment = TextAlignmentOptions.Center;
                timer.characterSpacing = 4f; timer.raycastTarget = false; timer.textWrappingMode = TextWrappingModes.NoWrap;
                // A director may have cleared the timer before the pill existed; an empty pill stays hidden.
                timerPill.gameObject.SetActive(!string.IsNullOrEmpty(timer.text));
            }
            var room = Panel("Room", side, new Vector2(0f, 1f), new Vector2(0f, -326f), new Vector2(252f, 40f), new Vector2(0f, 1f), Paper);
            roomPill = Text("Room name", room, new Vector2(.5f, .5f), Vector2.zero, new Vector2(240f, 36f), 15f, Ink);
            var goal = Panel("Objective", side, new Vector2(0f, 1f), new Vector2(0f, -376f), new Vector2(252f, 120f), new Vector2(0f, 1f), Paper);
            objective = Text("Objective text", goal, new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(226f, 100f), 15f, Ink, TextAlignmentOptions.TopLeft);
            objective.rectTransform.pivot = new Vector2(0f, 1f); objective.textWrappingMode = TextWrappingModes.Normal;
        }

        void BuildStatus()
        {
            status = Panel("Status", safe, new Vector2(.5f, 1f), new Vector2(0f, -168f), new Vector2(630f, 70f), new Vector2(.5f, 1f), Paper);
            statusText = Text("Status text", status, new Vector2(.5f, .5f), Vector2.zero, new Vector2(600f, 60f), 17f, Ink);
            statusText.textWrappingMode = TextWrappingModes.Normal;
            if (instruction) instruction.gameObject.SetActive(false);
            status.gameObject.SetActive(false);
        }

        void BuildVitals()
        {
            // No card: a cross, the number and two glass hand slots, straight on the game like the web.
            vitals = Rect("Vitals", safe, Vector2.zero, new Vector2(39f, 110f), new Vector2(360f, 168f));
            vitals.pivot = Vector2.zero;
            var cross = Rect("HP cross", vitals, new Vector2(0f, 1f), new Vector2(17f, -40f), new Vector2(33f, 33f));
            foreach (var (bar, size) in new[] { ("Across", new Vector2(33f, 11f)), ("Down", new Vector2(11f, 33f)) })
            {
                var piece = CreateImage(bar, cross, new Vector2(.5f, .5f), Vector2.zero, size, OnGame);
                var shadow = piece.gameObject.AddComponent<Shadow>(); shadow.effectColor = Shade; shadow.effectDistance = new Vector2(0f, -2f);
            }
            hpValue = Text("HP", vitals, new Vector2(0f, 1f), new Vector2(46f, -8f), new Vector2(220f, 64f), 60f, OnGame, TextAlignmentOptions.Left);
            hpValue.rectTransform.pivot = new Vector2(0f, 1f);
            OnGameText(hpValue);
            shieldText = Text("Shield", vitals, new Vector2(0f, 1f), new Vector2(206f, -30f), new Vector2(150f, 22f), 15f, Hex(0x9ff8d3), TextAlignmentOptions.Left);
            shieldText.rectTransform.pivot = new Vector2(0f, 1f);
            OnGameText(shieldText);
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                hands[i] = MakeHand($"Hand {i + 1}", vitals, new Vector2(i * 160f, 0f), new Vector2(152f, 86f), i, () => SelectHand(slot));
            }
        }

        void BuildTray()
        {
            tray = Panel("Letter bag", safe, new Vector2(.5f, 0f), new Vector2(0f, 52f), new Vector2(400f, 196f), new Vector2(.5f, 0f), Paper);
            var head = Text("Heading", tray, new Vector2(0f, 1f), new Vector2(22f, -16f), new Vector2(160f, 20f), 14f, Ink, TextAlignmentOptions.Left);
            head.text = "YOUR LETTERS"; head.characterSpacing = 6f; head.rectTransform.pivot = new Vector2(0f, 1f);
            bagCount = Text("Bag count", tray, new Vector2(0f, 1f), new Vector2(178f, -17f), new Vector2(70f, 20f), 12f, Ink, TextAlignmentOptions.Left);
            bagCount.rectTransform.pivot = new Vector2(0f, 1f);
            var spell = Text("Spell key", tray, new Vector2(1f, 1f), new Vector2(-104f, -17f), new Vector2(70f, 20f), 12f, Ink, TextAlignmentOptions.Right);
            spell.text = "<u>Spell</u> Q"; spell.rectTransform.pivot = Vector2.one;
            var link = MakeButton("Bag link", tray, Vector2.one, new Vector2(-14f, -10f), new Vector2(84f, 30f), "<u>Bag</u> Tab", () => SetBagPinned(!bagPinned), Color.clear);
            link.GetComponentInChildren<TextMeshProUGUI>().fontSize = 12f;
            float x0 = (400f - (TilesPerRow * 45f + (TilesPerRow - 1) * 7f)) * .5f;
            for (int i = 0; i < TrayTiles; i++)
                trayCells[i] = MakeCell($"Letter {i + 1}", tray, new Vector2(x0 + (i % TilesPerRow) * 52f, -50f - (i / TilesPerRow) * 58f), new Vector2(45f, 50f), 27f);
            craftProgress = Fill("Building", Panel("Progress track", tray, new Vector2(.5f, 0f), new Vector2(0f, 14f), new Vector2(356f, 8f), new Vector2(.5f, 0f), Track), Health);
        }

        void BuildHint()
        {
            hint = Panel("Desktop controls", safe, Vector2.zero, new Vector2(39f, 38f), new Vector2(470f, 60f), Vector2.zero, Hex(0x173b3c, .78f));
            hintText = Text("Keys", hint, new Vector2(0f, .5f), new Vector2(18f, 0f), new Vector2(434f, 56f), 14f, Hex(0xfff0d9, .82f), TextAlignmentOptions.Left);
            hintText.rectTransform.pivot = new Vector2(0f, .5f);
            hintText.fontStyle = FontStyles.Normal;
            hintText.textWrappingMode = TextWrappingModes.Normal;
            hintText.lineSpacing = 18f;
            SetHint(false);
            desktopHints = hint.gameObject;
        }

        /// <summary>
        /// Folds onto two lines beside the letter tray, as the browser's does. Right click, Shift, Space,
        /// R and 1/2 still work; the bar lists only what the web's lists.
        /// </summary>
        void SetHint(bool looks)
        {
            if (hintLooks == looks) return;
            hintLooks = looks;
            static string Key(string k) => $"<b><color=#FFF7E8>{k}</color></b>";
            const string dot = "  <alpha=#55>·<alpha=#FF>  ";
            hintText.text = Key("WASD") + " move" + dot + (looks ? "mouse look" : "mouse aim") + dot + Key("LMB") + " smash, throw, place" + dot
                + Key("Q") + " spell" + dot + Key("E") + " interact" + dot + Key("Tab") + " bag & map" + dot + Key("Esc") + " pause";
            hint.sizeDelta = new Vector2(470f, Mathf.Max(44f, hintText.GetPreferredValues(hintText.text, 434f, 0f).y + 20f));
        }

        /// <summary>Text straight on the game keeps a dark edge so it reads over a light floor.</summary>
        static void OnGameText(TMP_Text t)
        {
            t.fontStyle |= FontStyles.Bold;
            t.outlineWidth = .14f;
            t.outlineColor = new Color32(0x0b, 0x1f, 0x1f, 0xb0);
        }

        /// <summary>The web's crosshair: four light ticks round the screen's centre, shown in the third-person view.</summary>
        void BuildCrosshair()
        {
            var root = Rect("Crosshair", UiCanvas.transform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(22f, 22f));
            // Under every HUD panel, and never in the way of a click.
            root.SetAsFirstSibling();
            void Tick(string label, Vector2 at, Vector2 size)
            {
                var tick = CreateImage(label, root, Vector2.zero, at, size, Hex(0xfff8e8));
                tick.rectTransform.pivot = Vector2.zero;
                var outline = tick.gameObject.AddComponent<Outline>();
                outline.effectColor = Hex(0x10292b, .8f);
                outline.effectDistance = new Vector2(1f, -1f);
                var shadow = tick.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, .56f);
                shadow.effectDistance = new Vector2(0f, -1.5f);
            }
            Tick("Top", new Vector2(10f, 16f), new Vector2(2f, 6f));
            Tick("Bottom", new Vector2(10f, 0f), new Vector2(2f, 6f));
            Tick("Left", new Vector2(0f, 10f), new Vector2(6f, 2f));
            Tick("Right", new Vector2(16f, 10f), new Vector2(6f, 2f));
            crosshair = root.gameObject;
            crosshair.SetActive(false);
        }

        void BuildCraftDrawer()
        {
            var rt = Panel("Craft drawer", safe, new Vector2(.5f, 0f), new Vector2(0f, 262f), new Vector2(566f, 400f), new Vector2(.5f, 0f), Paper);
            craftRoot = rt.gameObject;
            var heading = Text("Heading", rt, new Vector2(.5f, 1f), new Vector2(0f, -16f), new Vector2(510f, 36f), 27f, Ink);
            heading.text = "SPELL SOMETHING";
            craftStatus = Text("Craft status", rt, new Vector2(.5f, 1f), new Vector2(0f, -58f), new Vector2(520f, 40f), 16f, Faded);
            craftStatus.textWrappingMode = TextWrappingModes.Normal;
            for (int i = 0; i < 4; i++)
            {
                var row = Panel($"Recipe {i + 1}", rt, new Vector2(.5f, 1f), new Vector2(0f, -105f - i * 53f), new Vector2(500f, 46f), new Vector2(.5f, 1f), Slot);
                var view = new RecipeView { face = row.GetComponent<Image>(), button = row.gameObject.AddComponent<Button>() };
                view.face.raycastTarget = true;
                view.icon = Rect("Object preview", row, new Vector2(0f, .5f), new Vector2(28f, 0f), new Vector2(42f, 42f)).gameObject.AddComponent<RawImage>();
                view.icon.raycastTarget = false;
                view.text = Text("Recipe", row, new Vector2(.5f, .5f), new Vector2(25f, 0f), new Vector2(412f, 40f), 20f, Ink, TextAlignmentOptions.Left);
                view.button.onClick.AddListener(() => { if (LocalPlayer && view.index >= 0) LocalPlayer.Summoner.Select(view.index); });
                recipes.Add(view);
            }
            MakeButton("Previous", rt, Vector2.zero, new Vector2(30f, 16f), new Vector2(66f, 48f), "<", () => { if (LocalPlayer) LocalPlayer.Summoner.Step(-1); });
            MakeButton("Next", rt, new Vector2(1f, 0f), new Vector2(-30f, 16f), new Vector2(66f, 48f), ">", () => { if (LocalPlayer) LocalPlayer.Summoner.Step(1); });
            buildButton = MakeButton("Build", rt, new Vector2(.5f, 0f), new Vector2(0f, 16f), new Vector2(248f, 48f), "SPELL IT", ConfirmCraft);
            buildLabel = buildButton.GetComponentInChildren<TextMeshProUGUI>();
            MakeButton("Cancel", rt, Vector2.one, new Vector2(-8f, -8f), new Vector2(40f, 40f), "X", CancelCraft, Slot);
            craftRoot.SetActive(false);
        }

        void BuildTouchControls()
        {
            var rt = Rect("Touch controls", safe, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; touchRoot = rt.gameObject;
            AddStick(rt, false, new Vector2(135f, 128f), Vector2.zero, 190f);
            AddStick(rt, true, new Vector2(-128f, 142f), new Vector2(1f, 0f), 152f);
            AddSkill(rt, TouchAction.Attack, "SMASH", new Vector2(-280f, 167f), 112f, Coral);
            AddSkill(rt, TouchAction.Dodge, "DODGE", new Vector2(-250f, 297f), 89f, Mint);
            AddSkill(rt, TouchAction.Jump, "JUMP", new Vector2(-123f, 294f), 89f, Cream);
            AddSkill(rt, TouchAction.Block, "BLOCK", new Vector2(-384f, 270f), 89f, Cream);
            AddSkill(rt, TouchAction.Grab, "INTERACT", new Vector2(-384f, 161f), 89f, Cream);
            AddSkill(rt, TouchAction.Craft, "SPELL", new Vector2(-503f, 270f), 89f, Mint);
            AddSkill(rt, TouchAction.Drop, "HOLD DROP", new Vector2(-384f, 51f), 76f, Cream);
            playLabel = MakeButton("Play or Ready", rt, new Vector2(.5f, 1f), new Vector2(0f, -185f), new Vector2(200f, 50f), "PLAY", JoinOrReady).GetComponentInChildren<TextMeshProUGUI>();
            playRect = (RectTransform)playLabel.transform.parent;
        }

        void AddStick(Transform parent, bool aim, Vector2 position, Vector2 anchor, float diameter)
        {
            var rt = Panel(aim ? "Aim stick" : "Move stick", parent, anchor, position, new Vector2(diameter, diameter), new Vector2(.5f, .5f), Hex(0x153f3c, .45f), true);
            rt.GetComponent<Image>().raycastTarget = true;
            var ring = Panel("Ring", rt, new Vector2(.5f, .5f), Vector2.zero, new Vector2(diameter - 12f, diameter - 12f), new Vector2(.5f, .5f), Hex(0xffe7bc, .2f), true);
            var knob = Panel("Thumb", rt, new Vector2(.5f, .5f), Vector2.zero, new Vector2(diameter * .36f, diameter * .36f), new Vector2(.5f, .5f), Hex(0xffe6b5, .8f), true);
            Text("Hint", ring, new Vector2(.5f, .5f), Vector2.zero, new Vector2(diameter, 30f), 19f, Cream).text = aim ? "AIM" : "MOVE";
            var stick = rt.gameObject.AddComponent<TouchStick>(); stick.knob = knob; stick.aims = aim; stick.radius = diameter * .28f;
            stick.Initialise(Camera.main); sticks.Add(stick);
        }

        void BuildNavigation()
        {
            var panel = Panel("Typewriter touch menu", safe, new Vector2(.5f, 0f), new Vector2(0f, 262f), new Vector2(580f, 166f), new Vector2(.5f, 0f), Paper);
            typewriterControls = panel.gameObject;
            typewriterChoice = Text("Mode choice", panel, new Vector2(.5f, 1f), new Vector2(0f, -35f), new Vector2(540f, 54f), 25f, Ink);
            typewriterChoice.textWrappingMode = TextWrappingModes.Normal;
            MakeButton("Previous mode", panel, Vector2.zero, new Vector2(18f, 22f), new Vector2(132f, 52f), "PREV", () => TouchBinding.Shared.Pulse(TouchAction.Up));
            MakeButton("Next mode", panel, Vector2.zero, new Vector2(162f, 22f), new Vector2(132f, 52f), "NEXT", () => TouchBinding.Shared.Pulse(TouchAction.Down));
            MakeButton("Choose mode", panel, Vector2.zero, new Vector2(306f, 22f), new Vector2(132f, 52f), "SELECT", () => TouchBinding.Shared.Pulse(TouchAction.Grab));
            MakeButton("Leave typewriter", panel, Vector2.zero, new Vector2(450f, 22f), new Vector2(112f, 52f), "BACK", () => { if (typewriter) typewriter.Close(); });
            typewriterControls.SetActive(false);
        }

        /// <summary>
        /// The web's Tab panel, pictures not paragraphs: a dark glass scrim over the whole screen, your bag down the
        /// left (letters, hands, what you wear, what's on you), the house map large in the middle and the recipe
        /// book down the right. The match keeps running underneath.
        /// </summary>
        void BuildBagPanel()
        {
            var rt = Rect("Bag panel", safe, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            bagPanel = rt.gameObject;
            bagFade = bagPanel.AddComponent<CanvasGroup>();
            // The scrim reaches past the safe area (ApplySafeArea) and catches clicks, so none lands in the game.
            var scrim = CreateImage("Backdrop", rt, Vector2.zero, Vector2.zero, Vector2.zero, Color.white);
            bagBackdrop = scrim.rectTransform; bagBackdrop.anchorMax = Vector2.one; bagBackdrop.offsetMin = bagBackdrop.offsetMax = Vector2.zero;
            scrim.sprite = MakeScrim(); scrim.raycastTarget = true;

            // Left: the bag, centred in the space under the close button.
            bagColumn = Rect("Bag", rt, new Vector2(0f, .5f), new Vector2(BagSide, (BagBottom - BagTop) * .5f), new Vector2(BagColumn, 400f));
            bagColumn.pivot = new Vector2(0f, .5f);
            RectTransform Row(string label) { var row = Rect(label, bagColumn, new Vector2(0f, 1f), Vector2.zero, new Vector2(BagColumn, 0f)); row.pivot = new Vector2(0f, 1f); return row; }
            bagLetters = Row("Letters");
            GlyphImage("Bag glyph", bagLetters, new Vector2(0f, 1f), new Vector2(0f, -4f), 28f, "bag", BagInk);
            for (int i = 0; i < TrayTiles; i++)
            {
                var cell = bagCells[i] = MakeCell($"Big letter {i + 1}", bagLetters, new Vector2(40f + (i % TilesPerRow) * 62.5f, -(i / TilesPerRow) * 67.5f), new Vector2(55f, 60f), 33f);
                cell.face.pixelsPerUnitMultiplier = cell.edge.pixelsPerUnitMultiplier = 11f / 6.25f;
                cell.ring = CreateImage("Ring", cell.face.transform, Vector2.zero, Vector2.zero, Vector2.zero, BagCellEdge);
                Stretch(cell.ring.rectTransform); cell.ring.sprite = hairRing; cell.ring.type = Image.Type.Sliced;
                cell.ring.pixelsPerUnitMultiplier = cell.face.pixelsPerUnitMultiplier;
            }
            bagPanelCount = Text("Count", bagLetters, Vector2.one, new Vector2(0f, -133.5f), new Vector2(120f, 22f), 16f, Hex(0xfff4e2, .8f), TextAlignmentOptions.Right);
            bagPanelCount.rectTransform.pivot = Vector2.one;
            bagHandsRow = Row("Hands");
            for (int i = 0; i < 2; i++)
            {
                int slot = i;
                bagHands[i] = MakeBagHand($"Bag hand {i + 1}", bagHandsRow, new Vector2(i * 193.5f, 0f), i, () => SelectHand(slot));
            }
            bagWearRow = Row("Wearing");
            foreach (var slot in WearSlots)
            {
                var chip = MakeChip(slot, bagWearRow);
                wearChips.Add((chip.gameObject, GlyphImage("Glyph", chip, new Vector2(.5f, .5f), Vector2.zero, 28f, slot, BagInk)));
            }
            bagEffectsRow = Row("Effects");
            foreach (var effect in new[] { "Effect 1", "Effect 2", "Effect 3" })
            {
                var chip = MakeChip(effect, bagEffectsRow);
                var icon = GlyphImage("Glyph", chip, new Vector2(.5f, .5f), Vector2.zero, 28f, "bubble", BagInk);
                var badge = Panel("Seconds", chip, new Vector2(1f, 0f), new Vector2(6f, -6f), new Vector2(26f, 20f), new Vector2(1f, 0f), Gold);
                badge.GetComponent<Image>().pixelsPerUnitMultiplier = 11f / 10f;
                var seconds = Text("Count", badge, new Vector2(.5f, .5f), Vector2.zero, new Vector2(26f, 20f), 12f, Hex(0x17393a));
                effectChips.Add((chip.gameObject, icon, badge.gameObject, seconds));
            }

            // Middle: the house map, as large as fits between the columns (sized in LayoutBag).
            var house = Rect("House", rt, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(house);
            var area = Rect("Map area", house, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(area);
            var shadow = CreateImage("Map shadow", area, new Vector2(.5f, .5f), new Vector2(0f, -65f), Vector2.one * 100f, Hex(0x000000, .53f));
            shadow.sprite = softSprite; shadow.type = Image.Type.Sliced; shadow.pixelsPerUnitMultiplier = 28f / 75f;
            bagMapShadow = shadow.rectTransform;
            bagMapFrame = Panel("Big map", area, new Vector2(.5f, .5f), new Vector2(0f, -35f), Vector2.one * 100f, new Vector2(.5f, .5f), MapEdge);
            bagMapFrame.GetComponent<Image>().pixelsPerUnitMultiplier = 11f / 22f;
            bigMap = MakeMap(bagMapFrame, true, 9f);

            // Right: the recipe book, every word with the letters you hold marked; one you can spell lights up gold.
            var book = Rect("Recipe book", rt, Vector2.one, Vector2.zero, Vector2.zero);
            book.anchorMin = new Vector2(1f, 0f); book.pivot = Vector2.one;
            book.offsetMin = new Vector2(-BagSide - BookColumn, BagBottom); book.offsetMax = new Vector2(-BagSide, -BagTop);
            GlyphImage("Book glyph", book, new Vector2(0f, 1f), new Vector2(0f, -2f), 28f, "book", BagInk);
            var view = Rect("View", book, Vector2.zero, Vector2.zero, Vector2.zero);
            Stretch(view); view.offsetMax = new Vector2(0f, -40f);
            view.gameObject.AddComponent<RectMask2D>();
            view.gameObject.AddComponent<Image>().color = Color.clear;
            var cardsRoot = Rect("Cards", view, new Vector2(.5f, 1f), Vector2.zero, Vector2.zero);
            cardsRoot.anchorMin = new Vector2(0f, 1f); cardsRoot.anchorMax = Vector2.one; cardsRoot.pivot = new Vector2(.5f, 1f); cardsRoot.offsetMin = cardsRoot.offsetMax = Vector2.zero;
            var grid = cardsRoot.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(127f, 130f); grid.spacing = new Vector2(10f, 10f); grid.padding = new RectOffset(4, 4, 4, 4);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 3;
            cardsRoot.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = view; scroll.content = cardsRoot; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
            foreach (var item in GameConfig.Current.Items.All)
                if (item.Enabled) bookCards.Add(MakeBookCard(item.Id, cardsRoot));

            // Close, at the top in the middle, over everything.
            var close = Panel("Close bag", rt, new Vector2(.5f, 1f), new Vector2(0f, -28f), new Vector2(40f, 40f), new Vector2(.5f, 1f), Hex(0xffffff, .12f), true);
            close.GetComponent<Image>().raycastTarget = true;
            close.gameObject.AddComponent<Button>().onClick.AddListener(() => SetBagPinned(false));
            var edge = CreateImage("Edge", close, Vector2.zero, Vector2.zero, Vector2.zero, Hex(0xffffff, .25f));
            Stretch(edge.rectTransform); edge.sprite = circleRing;
            Text("Label", close, new Vector2(.5f, .5f), new Vector2(0f, 2f), new Vector2(40f, 40f), 30f, BagInk).text = "×";
            bagPanel.SetActive(false);
        }

        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        /// <summary>A glass chip for something you wear or something on you.</summary>
        RectTransform MakeChip(string label, RectTransform row)
        {
            var chip = Panel(label, row, new Vector2(0f, 1f), Vector2.zero, Vector2.one * Chip, new Vector2(0f, 1f), BagCard);
            Edge(chip, BagCardEdge, 15f);
            return chip;
        }

        /// <summary>A light edge round a glass shape, the web's 1px border.</summary>
        Image Edge(RectTransform shape, Color colour, float radius)
        {
            var face = shape.GetComponent<Image>(); face.pixelsPerUnitMultiplier = 11f / radius;
            var edge = CreateImage("Edge", shape, Vector2.zero, Vector2.zero, Vector2.zero, colour);
            Stretch(edge.rectTransform); edge.sprite = hairRing; edge.type = Image.Type.Sliced; edge.pixelsPerUnitMultiplier = face.pixelsPerUnitMultiplier;
            return edge;
        }

        /// <summary>A hand in the bag: a glass card with its key, the thing held (or an open hand) and its wear.</summary>
        HandView MakeBagHand(string label, RectTransform parent, Vector2 position, int slot, Action onTap)
        {
            var size = new Vector2(181f, 145f);
            var rt = Panel(label, parent, new Vector2(0f, 1f), position, size, new Vector2(0f, 1f), BagCard);
            var view = new HandView { face = rt.GetComponent<Image>() };
            view.face.raycastTarget = true;
            rt.gameObject.AddComponent<Button>().onClick.AddListener(() => onTap());
            view.edge = Edge(rt, BagCardEdge, 17.5f);
            view.edge.sprite = MakeShape(false, 2.6f);
            var key = Text("Key", rt, new Vector2(0f, 1f), new Vector2(10f, -8f), new Vector2(24f, 20f), 15f, BagInk, TextAlignmentOptions.Left);
            key.text = (slot + 1).ToString(); key.rectTransform.pivot = new Vector2(0f, 1f);
            view.icon = Rect("Icon", rt, new Vector2(.5f, .5f), new Vector2(0f, 10f), new Vector2(78f, 78f)).gameObject.AddComponent<RawImage>();
            view.icon.raycastTarget = false;
            view.empty = GlyphImage("Open hand", rt, new Vector2(.5f, .5f), Vector2.zero, 42f, "hand", Hex(0xfff4e2, .45f));
            view.label = Text("Word", rt, new Vector2(.5f, 0f), new Vector2(0f, 20f), new Vector2(size.x - 16f, 20f), 14f, BagInk);
            view.label.rectTransform.pivot = new Vector2(.5f, 0f);
            var track = Panel("Wear", rt, new Vector2(.5f, 0f), new Vector2(0f, 10f), new Vector2(size.x - 25f, 6f), new Vector2(.5f, 0f), Hex(0xffffff, .16f));
            view.wear = Fill("Left", track, Health); view.wearTrack = track.gameObject;
            return view;
        }

        BookCard MakeBookCard(string word, RectTransform parent)
        {
            var root = new GameObject(word, typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(parent, false);
            var card = new BookCard { word = word, tiles = new Image[word.Length], letters = new TextMeshProUGUI[word.Length] };
            card.glow = CreateImage("Glow", root, Vector2.zero, Vector2.zero, Vector2.zero, GoldGlow);
            Stretch(card.glow.rectTransform); card.glow.rectTransform.offsetMin = -Vector2.one * 19f; card.glow.rectTransform.offsetMax = Vector2.one * 19f;
            card.glow.sprite = softSprite; card.glow.type = Image.Type.Sliced; card.glow.pixelsPerUnitMultiplier = 28f / 17.5f;
            var face = Panel("Face", root, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f), BagCard);
            Stretch(face);
            card.face = face.GetComponent<Image>(); card.face.raycastTarget = true;
            card.edge = Edge(face, BagCardEdge, 15f);
            card.button = root.gameObject.AddComponent<Button>();
            card.button.targetGraphic = card.face;
            var colours = card.button.colors; colours.disabledColor = Color.white; card.button.colors = colours;
            card.button.onClick.AddListener(() => SpellFromBook(word));
            var icon = Rect("Picture", root, new Vector2(.5f, 1f), new Vector2(0f, -12f), new Vector2(58f, 58f)).gameObject.AddComponent<RawImage>();
            ((RectTransform)icon.transform).pivot = new Vector2(.5f, 1f); icon.raycastTarget = false;
            icon.texture = itemIcons.TryGetValue(word, out var picture) ? picture : null; icon.enabled = icon.texture;
            float tile = word.Length > 6 ? 13f : 16f, step = tile + 2.5f, x0 = -(word.Length * step - 2.5f) * .5f;
            for (int i = 0; i < word.Length; i++)
            {
                var t = Panel($"Tile {i + 1}", root, new Vector2(.5f, 0f), new Vector2(x0 + i * step, 16f), new Vector2(tile, 20f), Vector2.zero, TileMissing);
                card.tiles[i] = t.GetComponent<Image>(); card.tiles[i].pixelsPerUnitMultiplier = 11f / 3.75f;
                card.letters[i] = Text("Letter", t, new Vector2(.5f, .5f), Vector2.zero, new Vector2(tile, 20f), 11f, TileMissingInk);
                card.letters[i].text = word[i].ToString();
            }
            return card;
        }

        /// <summary>A recipe you hold the letters for spells straight from the book; the bag closes so you see it made.</summary>
        void SpellFromBook(string word)
        {
            if (!LocalPlayer || Paused) return;
            SetBagPinned(false);
            LocalPlayer.Summoner.BeginCraft(new WordEntry { word = word });
        }

        RawImage GlyphImage(string label, Transform parent, Vector2 anchor, Vector2 position, float size, string glyph, Color colour)
        {
            var image = Rect(label, parent, anchor, position, Vector2.one * size).gameObject.AddComponent<RawImage>();
            if (anchor == new Vector2(0f, 1f)) image.rectTransform.pivot = anchor;
            image.texture = Glyph(glyph); image.color = colour; image.raycastTarget = false;
            return image;
        }

        /// <summary>The web's line icons (main.js GLYPHS), drawn once to white PNGs and tinted here.</summary>
        Texture2D Glyph(string name)
        {
            if (!glyphs.TryGetValue(name, out var texture)) glyphs[name] = texture = Resources.Load<Texture2D>("UI/Glyphs/" + name);
            return texture;
        }

        void BuildPause()
        {
            var shade = Panel("Pause", safe, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f), Hex(0x0f2827, .45f));
            shade.anchorMin = Vector2.zero; shade.anchorMax = Vector2.one; shade.offsetMin = shade.offsetMax = Vector2.zero;
            shade.GetComponent<Image>().raycastTarget = true;
            pauseRoot = shade.gameObject; pauseShade = shade;
            var card = Panel("Pause card", shade, new Vector2(.5f, .5f), Vector2.zero, new Vector2(520f, 380f), new Vector2(.5f, .5f), Hex(0xfff0dc, .98f));
            Text("Heading", card, new Vector2(.5f, 1f), new Vector2(0f, -40f), new Vector2(460f, 60f), 46f, Ink).text = "Paused";
            Text("Note", card, new Vector2(.5f, 1f), new Vector2(0f, -92f), new Vector2(460f, 30f), 17f, Faded).text = "The house is holding its breath.";
            MakeButton("Resume", card, new Vector2(.5f, 1f), new Vector2(0f, -150f), new Vector2(380f, 56f), "RESUME", TogglePause);
            MakeButton("Pause home", card, new Vector2(.5f, 1f), new Vector2(0f, -216f), new Vector2(380f, 56f), "BACK TO THE HOUSE", GoHome, Slot);
            touchToggle = MakeButton("Touch switch", card, new Vector2(.5f, 1f), new Vector2(0f, -282f), new Vector2(380f, 56f), "TOUCH CONTROLS: OFF",
                () => { touchChosen = true; ShowTouchControls(!TouchControlsShown); }, Slot).GetComponentInChildren<TextMeshProUGUI>();
            pauseRoot.SetActive(false);
        }

        void AddSkill(Transform parent, TouchAction action, string label, Vector2 position, float diameter, Color accent)
        {
            var rt = Panel(label, parent, new Vector2(1f, 0f), position, new Vector2(diameter, diameter), new Vector2(.5f, .5f), accent, true);
            rt.GetComponent<Image>().raycastTarget = true;
            var inset = Panel("Face", rt, new Vector2(.5f, .5f), Vector2.zero, new Vector2(diameter - 7f, diameter - 7f), new Vector2(.5f, .5f), Hex(0xfff0dc, .95f), true);
            var icon = CreateImage("Icon", inset, new Vector2(.5f, .5f), new Vector2(0f, 5f), new Vector2(diameter * .77f, diameter * .77f), Color.white);
            icon.sprite = Icon(action); icon.preserveAspect = true; if (!icon.sprite) icon.enabled = false;
            var text = Text("Label", rt, new Vector2(.5f, 0f), new Vector2(0f, -4f), new Vector2(diameter + 34f, 26f), 14f, Cream);
            text.rectTransform.pivot = new Vector2(.5f, 1f); text.text = label; text.outlineWidth = .2f; text.outlineColor = Ink;
            var button = rt.gameObject.AddComponent<TouchActionButton>(); button.action = action;
            skillButtons[action] = (button, text);
            if (action == TouchAction.Craft) { button.sendsInput = false; button.pressed = ToggleCraft; }
        }

        // ---- Actions ----

        public void ShowTouchControls(bool on)
        {
            if (!touchRoot) return;
            touchRoot.SetActive(on); TouchBinding.Shared.ReleaseAll(); TouchBinding.Shared.Enabled = on;
            TouchBinding.Shared.OverlayDesktop = on && LocalPlayer && LocalPlayer.Binding is DesktopBinding;
            TouchBinding.Shared.OverlayBindingId = on && LocalPlayer && LocalPlayer.Binding is not TouchBinding ? LocalPlayer.Binding?.Id : null;
            if (desktopHints) desktopHints.SetActive(!on);
            if (touchToggle) touchToggle.text = on ? "TOUCH CONTROLS: ON" : "TOUCH CONTROLS: OFF";
            if (!on && LocalPlayer) LocalPlayer.Summoner.Close();
            ApplySafeArea();
        }
        void JoinOrReady()
        {
            if (!joins) return;
            if (!LocalPlayer) { TouchBinding.Shared.Enabled = true; LocalPlayer = joins.Join(TouchBinding.Shared); TouchBinding.Shared.OverlayDesktop = false; TouchBinding.Shared.OverlayBindingId = null; }
            else TouchBinding.Shared.Pulse(TouchAction.Start);
        }
        void ToggleCraft()
        {
            if (!LocalPlayer || !LocalPlayer.CanAct) return;
            if (LocalPlayer.Summoner.IsCrafting || LocalPlayer.Summoner.IsSpelling) CancelCraft();
            else TouchBinding.Shared.SetCraftOpen(true);
        }
        void ConfirmCraft() { if (LocalPlayer) LocalPlayer.Summoner.CraftSelected(); TouchBinding.Shared.SetCraftOpen(false); }
        void CancelCraft()
        {
            if (LocalPlayer) { LocalPlayer.Summoner.Close(); LocalPlayer.Summoner.CancelCraft(); }
            TouchBinding.Shared.SetCraftOpen(false);
        }
        void SelectHand(int slot) { if (LocalPlayer && !Paused) LocalPlayer.Combat.SelectSlot(slot); }
        void SetBagPinned(bool on) { bagPinned = on; RefreshBagPanel(); }
        void GoHome()
        {
            if (Paused) SetPaused(false);
            TouchBinding.Shared.ReleaseAll();
            Session.GoHome();
        }
        void TogglePause() => SetPaused(!Paused);
        void SetPaused(bool on)
        {
            if (!pauseRoot || on == Paused) return;
            if (on)
            {
                resumeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                TouchBinding.Shared.ReleaseAll();
                bagPinned = false;
            }
            else Time.timeScale = resumeScale;
            pauseRoot.SetActive(on);
            pauseRoot.transform.SetAsLastSibling();
            RefreshBagPanel();
        }

        // ---- Refresh ----

        void Update()
        {
            if (!safe) return;
            // Esc closes the open thing first, as in the browser edition, then pauses.
            bool hub = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Session.HubScene;
            if (DesktopBinding.Shared.Pause.WasPressedThisFrame())
            {
                if (Paused) SetPaused(false);
                else if (bagPinned) SetBagPinned(false);
                else if (craftRoot.activeSelf) CancelCraft();
                else if (hub) pointerFreed = CursorPolicy.Locked;
                else if (!(typewriter && typewriter.User)) SetPaused(true);
            }
            else if (pointerFreed && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame
                && !(EventSystem.current && EventSystem.current.IsPointerOverGameObject()))
                pointerFreed = false;
            // The round-end slow motion restores time on its own realtime clock; a pause outlasts it.
            if (Paused && Time.timeScale != 0f) { resumeScale = Time.timeScale; Time.timeScale = 0f; }
            if (BagOpen != WantsBag()) RefreshBagPanel();
            var screen = Touchscreen.current;
            if (!touchChosen && !TouchControlsShown && screen != null && screen.primaryTouch.press.wasPressedThisFrame) ShowTouchControls(true);
        }

        bool WantsBag() => LocalPlayer && !Paused && (bagPinned || (LocalPlayer.Binding is DesktopBinding && DesktopBinding.Shared.Bag.IsPressed()));

        void OnEnable() => Active = this;

        void LateUpdate()
        {
            if (!safe) return;
            if (Screen.width != lastWidth || Screen.height != lastHeight || Screen.safeArea != lastSafe) ApplySafeArea();
            var rig = CameraRig.Instance;
            bool aiming = rig && rig.isActiveAndEnabled && rig.IsThirdPerson && !NeedsPointer;
            if (crosshair && crosshair.activeSelf != aiming) crosshair.SetActive(aiming);
            if (hintText) SetHint(rig && rig.isActiveAndEnabled && rig.IsThirdPerson);
            if (BagOpen && bagFade.alpha < 1f) bagFade.alpha = Mathf.Clamp01((Time.unscaledTime - bagOpenedAt) / .12f);
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .08f;
            FindLocalPlayer(); RefreshMatch(); RefreshCards(); RefreshSide(); RefreshVitals(); RefreshTray(); RefreshCraft(); RefreshSkills(); RefreshNavigation();
            if (BagOpen) RefreshBagContents();
            // Round start/retry lives in ModeActions. Keep this join control separate so their
            // canvases cannot overlap after a touch player has joined.
            if (playRect) playRect.gameObject.SetActive(!LocalPlayer);
            if (playLabel) playLabel.text = "PLAY";
        }
        void FindLocalPlayer()
        {
            if (LocalPlayer && LocalPlayer.isActiveAndEnabled) return;
            LocalPlayer = null;
            foreach (var p in World.Players) if (p && (p.Binding is TouchBinding || p.Binding is DesktopBinding)) { LocalPlayer = p; break; }
            // Never an AI seat: its HP and hands aren't anyone's at this screen.
            if (!LocalPlayer) foreach (var p in World.Players) if (p && p.Binding is not ScriptedBinding and not BotBinding) { LocalPlayer = p; break; }
            TouchBinding.Shared.OverlayDesktop = TouchControlsShown && LocalPlayer && LocalPlayer.Binding is DesktopBinding;
            TouchBinding.Shared.OverlayBindingId = TouchControlsShown && LocalPlayer && LocalPlayer.Binding is not TouchBinding ? LocalPlayer.Binding?.Id : null;
        }
        IReadOnlyList<PlayerController> Roster => players ?? joins?.Players;
        void RefreshMatch()
        {
            bool hub = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == Session.HubScene;
            string mode = hub ? "Hub" : Match.Mode;
            (string tag, string name) = mode switch
            {
                "Dibs" => ("HOUSE BRAWL", "Dibs!"),
                "Duos" => ("2V2 · TEAMS", "Double trouble"),
                "MovingOut" => ("CO-OP", "The great escape"),
                "MovingDay" => ("CO-OP", "Moving day"),
                "Tutorial" => ("PRACTICE", "Play & learn"),
                "Hub" => ("WELCOME HOME", "The house"),
                _ => ("HOUSE PARTY", "Wreckabulary"),
            };
            matchTag.text = tag; matchTitle.text = name;
            var mine = LocalPlayer && wins != null ? wins(LocalPlayer) : 0;
            matchDetail.text = mode is "Dibs" or "Duos" && rounds && rounds.Round > 0 ? $"ROUND {rounds.Round} · {mine} / {roundsToWin} WINS"
                : mode == "Tutorial" ? "NO PRESSURE. JUST PLAY." : (Layout?.Name ?? "").ToUpperInvariant();
        }
        void RefreshCards()
        {
            var current = Roster;
            int shown = 0;
            for (int i = 0; current != null && i < current.Count && shown < cards.Count; i++)
            {
                var p = current[i];
                // Only couch roommates get a card; the web shows none for AI housemates.
                if (!p || p == LocalPlayer || p.Binding is BotBinding) continue;
                var card = cards[shown++];
                card.root.SetActive(true);
                card.badge.color = p.Color; card.initial.text = p.Initial.ToString();
                card.name.text = p.Name + (showWins && wins != null ? $"  <size=75%>{wins(p)}/{roundsToWin}</size>" : "");
                card.fill.fillAmount = p.Health.Fraction; card.fill.color = p.Health.Fraction > .35f ? Health : Low;
                card.status.text = p.IsEliminated ? "WRECKED" : p.IsDowned ? $"REVIVE ME {Mathf.CeilToInt(p.Health.BleedOutLeft)}s" : $"{Mathf.CeilToInt(p.Health.Current)} HP · {p.Inventory.TotalCount}/{p.Inventory.Capacity}";
            }
            for (int i = shown; i < cards.Count; i++) cards[i].root.SetActive(false);
        }
        HouseLayout Layout => rooms && rooms.Layout != null ? rooms.Layout : clearOut ? clearOut.Layout : null;
        void RefreshSide()
        {
            UpdateMap(miniMap);
            var layout = Layout;
            string room = LocalPlayer && layout != null ? RoomOf(layout, LocalPlayer) : null;
            roomPill.text = room != null ? Spaced(room) : layout != null ? "House" : "";
            roomPill.transform.parent.gameObject.SetActive(roomPill.text.Length > 0);
            // The head count has its own pill now; the objective card shows only a real objective.
            objective.text = checklistText;
            var current = Roster;
            int up = 0, all = 0;
            if (current != null) foreach (var p in current) { if (!p) continue; all++; if (!p.IsDowned && !p.IsEliminated) up++; }
            aliveText.text = all > 0 ? $"{up}/{all}" : "-";
            var goal = (RectTransform)objective.transform.parent;
            goal.gameObject.SetActive(objective.text.Length > 0);
            goal.sizeDelta = new Vector2(252f, Mathf.Clamp(objective.GetPreferredValues(objective.text, 226f, 0f).y + 22f, 48f, 330f));
        }
        void RefreshVitals()
        {
            vitals.gameObject.SetActive(LocalPlayer);
            if (!LocalPlayer) return;
            var health = LocalPlayer.Health;
            hpValue.text = health.IsDowned ? $"DOWN <size=40%>{Mathf.CeilToInt(health.BleedOutLeft)}s</size>" : $"{Mathf.CeilToInt(health.Current)}<size=30%> HP</size>";
            // Low health shows in the number's colour and a pulse, not in colour alone.
            bool low = health.IsDowned || health.Current < 30f;
            hpValue.color = low ? LowHp : OnGame;
            hpValue.rectTransform.localScale = Vector3.one * (low ? 1f + .06f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f)) : 1f);
            shieldText.text = health.Bubble > 0f ? $"+{Mathf.CeilToInt(health.Bubble)} SHIELD" : "";
            for (int i = 0; i < 2; i++) ShowHand(hands[i], i, false);
        }
        void ShowHand(HandView view, int slot, bool detail)
        {
            var combat = LocalPlayer.Combat;
            var gear = combat.GearIn(slot);
            bool active = slot == combat.ActiveSlot;
            bool carrying = active && combat.IsHolding && !combat.Weapon;
            // The bag panel's slots sit on its paper; the HUD's are glass on the game with a light edge on the hand in use.
            string word = gear ? gear.word : carrying ? "Carrying" : null;
            if (detail)
            {
                // The bag's cards are glass with a gold edge on the hand in use, and an open hand when it's empty.
                view.face.color = active ? BagCardActive : BagCard; view.edge.color = active ? Gold : BagCardEdge;
                view.label.text = gear ? Spaced(gear.word) : ""; view.label.color = BagInk;
                view.empty.enabled = !gear;
            }
            else
            {
                view.face.color = active ? GlassActive : Glass; view.edge.color = active ? GlassEdge : GlassIdle;
                view.label.text = word ?? "Empty hand"; view.label.color = word != null ? OnGame : Hex(0xfff7e8, .7f);
            }
            view.icon.texture = gear && itemIcons.TryGetValue(gear.word, out var texture) ? texture : null;
            view.icon.enabled = view.icon.texture;
            if (view.wearTrack)
            {
                int most = gear && gear.Definition != null ? Mathf.Max(1, gear.Definition.Durability) : 0;
                view.wearTrack.SetActive(detail && gear && most > 0);
                if (most > 0) view.wear.fillAmount = Mathf.Clamp01(gear.DurabilityLeft / most);
            }
        }
        void RefreshTray()
        {
            tray.gameObject.SetActive(LocalPlayer);
            if (!LocalPlayer) return;
            var inv = LocalPlayer.Inventory;
            bagCount.text = $"{inv.TotalCount} / {inv.Capacity}";
            FillCells(trayCells, inv);
            var summon = LocalPlayer.Summoner;
            craftProgress.transform.parent.gameObject.SetActive(summon.IsCrafting);
            craftProgress.fillAmount = summon.CraftProgress;
        }
        void FillCells(LetterCell[] cells, LetterInventory inv)
        {
            var letters = inv.Letters.OrderBy(c => c).ToArray();
            var crafting = LocalPlayer.Summoner.IsCrafting ? LocalPlayer.Summoner.CraftWord ?? "" : "";
            for (int i = 0; i < cells.Length; i++)
            {
                var cell = cells[i];
                cell.face.transform.parent.gameObject.SetActive(i < inv.Capacity);
                bool held = i < letters.Length, reserved = !held && i < letters.Length + inv.ReservedCount;
                int r = i - letters.Length;
                cell.face.color = held ? Tile : reserved ? Reserved : cell.ring ? BagCell : EmptyCell;
                cell.edge.enabled = held;
                if (cell.ring) cell.ring.enabled = !held && !reserved;
                cell.letter.color = reserved ? ReservedInk : Ink;
                cell.letter.text = held ? letters[i].ToString() : reserved && r < crafting.Length ? crafting[r].ToString() : "";
            }
        }
        void RefreshBagPanel()
        {
            if (!bagPanel) return;
            bool open = WantsBag();
            if (open != bagPanel.activeSelf)
            {
                bagPanel.SetActive(open);
                if (open)
                {
                    // The web's bag-fade: 0.12 s in. Thumbs on the hidden sticks let go.
                    bagOpenedAt = Time.unscaledTime; bagFade.alpha = 0f;
                    TouchBinding.Shared.ReleaseAll();
                    foreach (var stick in sticks) if (stick) stick.Release();
                }
            }
            HideForBag(open);
            if (!open) return;
            bagPanel.transform.SetAsLastSibling();
            RefreshBagContents();
        }

        /// <summary>While the bag is open the rest of the HUD steps aside, as the web's does; the brand and match label stay.</summary>
        void HideForBag(bool open)
        {
            if (bagHidden == open) return;
            bagHidden = open;
            foreach (var group in bagHides) { if (!group) continue; group.alpha = open ? 0f : 1f; group.blocksRaycasts = group.interactable = !open; }
            ModeActions.Hidden = open;
        }
        void RefreshBagContents()
        {
            if (!LocalPlayer) return;
            var inv = LocalPlayer.Inventory;
            bagPanelCount.text = $"{inv.TotalCount}/{inv.Capacity}";
            FillCells(bagCells, inv);
            for (int i = 0; i < 2; i++) ShowHand(bagHands[i], i, true);
            // What you wear, a glyph per piece.
            var outfit = LocalPlayer.GetComponent<PlayerAppearance>()?.CurrentOutfit;
            int worn = 0;
            if (outfit != null)
                foreach (var slot in WearSlots)
                {
                    if (string.IsNullOrEmpty(outfit.PieceIn(slot))) continue;
                    var (root, icon) = wearChips[worn++];
                    root.SetActive(true); icon.texture = Glyph(slot);
                }
            for (int i = worn; i < wearChips.Count; i++) wearChips[i].root.SetActive(false);
            // What's on you: a bubble and a speed boost count down; a carried thing shows its picture.
            int effects = 0;
            void Effect(Texture picture, Color tint, float seconds)
            {
                var (root, icon, badge, count) = effectChips[effects++];
                root.SetActive(true); icon.texture = picture; icon.color = tint;
                badge.SetActive(seconds > 0f); count.text = Mathf.CeilToInt(seconds).ToString();
            }
            var health = LocalPlayer.Health;
            if (health.Bubble > 0f) Effect(Glyph("bubble"), Hex(0x9fe3ff), health.BubbleLeft);
            if (LocalPlayer.BoostLeft > 0f) Effect(Glyph("speed"), Hex(0xb9ffb0), LocalPlayer.BoostLeft);
            var combat = LocalPlayer.Combat;
            if (combat.IsHolding && !combat.Weapon)
            {
                var held = combat.Held.name.Replace("(Clone)", "").Trim().ToUpperInvariant();
                bool pictured = itemIcons.TryGetValue(held, out var picture);
                Effect(pictured ? picture : Glyph("carry"), pictured ? Color.white : BagInk, 0f);
            }
            for (int i = effects; i < effectChips.Count; i++) effectChips[i].root.SetActive(false);
            RefreshBook(inv);
            LayoutBag(worn, effects);
            UpdateMap(bigMap);
        }

        /// <summary>The web's letterCover: each letter of a word takes one matching letter from your bag.</summary>
        void RefreshBook(LetterInventory inv)
        {
            bool free = LocalPlayer.CanAct && !LocalPlayer.Summoner.IsCrafting;
            foreach (var card in bookCards)
            {
                spare.Clear(); spare.AddRange(inv.Letters);
                bool all = true;
                for (int i = 0; i < card.word.Length; i++)
                {
                    int at = spare.IndexOf(card.word[i]);
                    bool got = at >= 0;
                    if (got) spare.RemoveAt(at); else all = false;
                    card.tiles[i].color = got ? TileGot : TileMissing;
                    card.letters[i].color = got ? TileGotInk : TileMissingInk;
                }
                card.face.color = all ? Hex(0xffe7b8, .17f) : BagCard;
                card.edge.color = all ? Gold : BagCardEdge;
                card.glow.enabled = all;
                card.button.interactable = all && free;
            }
        }

        /// <summary>Stacks the bag's rows (chips wrap six to a row) and sizes the map to the room between the columns.</summary>
        void LayoutBag(int worn, int effects)
        {
            float y = 0f;
            static float Rows(int n) => n == 0 ? 0f : Mathf.Ceil(n / (float)ChipsPerRow) * (Chip + ChipGap) - ChipGap;
            void Place(RectTransform row, float height)
            {
                row.gameObject.SetActive(height > 0f);
                if (height <= 0f) return;
                row.anchoredPosition = new Vector2(0f, -y); row.sizeDelta = new Vector2(BagColumn, height);
                y += height + BagGap;
            }
            void Flow(int count, Func<int, GameObject> chip)
            {
                for (int i = 0; i < count; i++)
                    ((RectTransform)chip(i).transform).anchoredPosition = new Vector2(i % ChipsPerRow * (Chip + ChipGap), -(i / ChipsPerRow) * (Chip + ChipGap));
            }
            Place(bagLetters, 155f); Place(bagHandsRow, 145f); Place(bagWearRow, Rows(worn)); Place(bagEffectsRow, Rows(effects));
            Flow(worn, i => wearChips[i].root); Flow(effects, i => effectChips[i].root);
            bagColumn.sizeDelta = new Vector2(BagColumn, Mathf.Max(0f, y - BagGap));
            // The web's zoom: max(260px, min(62vh, 100vw - 720px)), a little below the middle.
            var area = safe.rect;
            float side = Mathf.Max(325f, Mathf.Min(area.height * .62f, area.width - 900f));
            bagMapFrame.sizeDelta = Vector2.one * side;
            bagMapShadow.sizeDelta = Vector2.one * (side + 2f * 31f * 75f / 28f);
        }
        void RefreshCraft()
        {
            if (!LocalPlayer) { craftRoot.SetActive(false); return; }
            var summon = LocalPlayer.Summoner; bool visible = summon.IsSpelling || summon.IsCrafting; craftRoot.SetActive(visible);
            if (!visible) { if (TouchBinding.Shared.CraftOpen && !TouchBinding.Shared.CraftPressPending) TouchBinding.Shared.SetCraftOpen(false); return; }
            bool building = summon.IsCrafting;
            craftStatus.text = building ? $"Spelling {summon.CraftWord}…  {Mathf.RoundToInt(summon.CraftProgress * 100f)}%" : summon.Ready.Count > 0 ? "Pick a word. Its letters become a real object." : "Smash furniture and find the missing letters.";
            buildButton.interactable = !building && summon.SelectedWord != null; buildLabel.text = building ? "SPELLING…" : "SPELL IT";
            int start = Mathf.Max(0, summon.Selected - 1);
            for (int i = 0; i < recipes.Count; i++)
            {
                var view = recipes[i]; view.index = start + i; bool ready = view.index < summon.Ready.Count;
                view.button.interactable = ready && !building;
                if (ready)
                {
                    var word = summon.Ready[view.index]; bool selected = view.index == summon.Selected;
                    view.face.color = selected ? Mint : Slot; view.text.color = Ink;
                    view.text.text = $"{word.word}     <size=70%>{word.word.Length} letters</size>";
                    view.icon.texture = itemIcons.TryGetValue(word.word, out var texture) ? texture : null;
                    view.icon.color = Color.white; view.icon.enabled = view.icon.texture;
                }
                else
                {
                    int hintIndex = view.index - summon.Ready.Count; view.face.color = Hex(0xf7e5c8, .5f); view.text.color = Faded;
                    view.text.text = hintIndex < summon.Hints.Count ? $"{summon.Hints[hintIndex].entry.word}     <size=70%>find {summon.Hints[hintIndex].missing}</size>" : "";
                    view.icon.texture = hintIndex < summon.Hints.Count && itemIcons.TryGetValue(summon.Hints[hintIndex].entry.word, out var texture) ? texture : null;
                    view.icon.color = new Color(1f, 1f, 1f, .45f); view.icon.enabled = view.icon.texture;
                }
            }
        }
        void RefreshSkills()
        {
            if (!LocalPlayer) return;
            var combat = LocalPlayer.Combat;
            var weapon = combat.Weapon;
            bool canAct = LocalPlayer.CanAct && !LocalPlayer.IsDodging;
            bool free = canAct && !LocalPlayer.Summoner.IsSpelling && !LocalPlayer.Summoner.IsCrafting && !combat.IsChanneling;
            foreach (var pair in skillButtons)
            {
                bool available = pair.Key switch
                {
                    TouchAction.Block => free && weapon && weapon.Shield != null,
                    TouchAction.Drop => free && combat.IsHolding,
                    // Carrying a prop, grab does nothing: SMASH throws it and DROP sets it down.
                    TouchAction.Grab => free && !(combat.IsHolding && !weapon),
                    TouchAction.Dodge => canAct && LocalPlayer.Health.CanDodge,
                    TouchAction.Jump => canAct,
                    TouchAction.Craft => canAct && (LocalPlayer.Summoner.IsCrafting || LocalPlayer.Summoner.IsSpelling || !combat.IsChanneling),
                    _ => free,
                };
                pair.Value.button.SetAvailable(available);
            }
            skillButtons[TouchAction.Grab].label.text = combat.IsReviving ? "REVIVING" : combat.DownedTeammateNearby() ? "HOLD REVIVE" : "INTERACT";
            skillButtons[TouchAction.Craft].label.text = LocalPlayer.Summoner.IsCrafting || LocalPlayer.Summoner.IsSpelling ? "CANCEL" : "SPELL";
            // The attack button does the held thing's job, in HeldWeapon.Use's order.
            var job = weapon && weapon.Shield == null ? weapon.Definition : null;
            skillButtons[TouchAction.Attack].label.text = combat.IsHolding && !weapon ? "THROW"
                : job == null ? "SMASH"
                : job.Use != null ? "USE" : job.Thrown != null ? "THROW" : job.Deploy != null ? "PLACE"
                : job.Family == HandlingFamily.Ranged ? "FIRE" : "SMASH";
        }
        void RefreshNavigation()
        {
            bool usingMenu = typewriter && LocalPlayer && typewriter.User == LocalPlayer;
            typewriterControls.SetActive(usingMenu);
            if (!usingMenu) return;
            var mode = typewriter.Modes[typewriter.Selected];
            typewriterChoice.text = mode.label + "\n<size=65%>" + mode.blurb + "</size>";
        }

        // ---- Map ----

        MapView MakeMap(RectTransform frame, bool full, float border)
        {
            // The frame is a ring, so the floor inside is the only thing over the game (the web's border, not a card).
            var edge = frame.GetComponent<Image>();
            float scale = edge.pixelsPerUnitMultiplier;
            edge.sprite = MakeShape(false, border * scale);
            var bg = Panel("Floor", frame, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f), MapBg);
            bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = Vector2.one * border; bg.offsetMax = -Vector2.one * border;
            bg.GetComponent<Image>().pixelsPerUnitMultiplier = 11f / Mathf.Max(1f, 11f / scale - border);
            bg.gameObject.AddComponent<RectMask2D>();
            return new MapView { content = bg, full = full };
        }
        void UpdateMap(MapView map)
        {
            var layout = Layout;
            map.content.parent.gameObject.SetActive(layout != null && layout.Rooms.Count > 0);
            if (layout == null || layout.Rooms.Count == 0) return;
            float minX = layout.Rooms.Min(r => r.MinX), maxX = layout.Rooms.Max(r => r.MaxX);
            float minZ = layout.Rooms.Min(r => r.MinZ), maxZ = layout.Rooms.Max(r => r.MaxZ);
            float extent = Mathf.Max(maxX - minX, maxZ - minZ) * 1.04f;
            float ox = minX - (extent - (maxX - minX)) * .5f, oz = minZ - (extent - (maxZ - minZ)) * .5f;
            Vector2 At(float x, float z) => new((x - ox) / extent, (z - oz) / extent);
            // A house with an upstairs shows the floor you're on; the frame stays the whole house's, so it doesn't jump.
            int storeyCount = layout.StoreyFloors().Count;
            // On a flight, the same storey the camera is holding, so the map and the view never disagree.
            var cutaway = StoreyCutaway.Instance;
            int StoreyOfPlayer(PlayerController who) => cutaway ? cutaway.StoreyOfPlayer(who) : layout.StoreyAt(who.transform.position.y);
            int storey = LocalPlayer && storeyCount > 1 ? StoreyOfPlayer(LocalPlayer) : 0;
            if (map.layout != layout || map.storey != storey)
            {
                foreach (var (_, fill, _) in map.rooms) Destroy(fill.gameObject);
                map.rooms.Clear();
                foreach (var flight in map.stairs) Destroy(flight);
                map.stairs.Clear();
                foreach (var box in layout.Rooms)
                {
                    if (storeyCount > 1 && layout.StoreyOf(box) != storey) continue;
                    var rt = Panel(box.Name, map.content, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f), RoomFill);
                    rt.anchorMin = At(box.MinX, box.MinZ); rt.anchorMax = At(box.MaxX, box.MaxZ); rt.offsetMin = rt.offsetMax = Vector2.zero;
                    var image = rt.GetComponent<Image>(); image.sprite = null;
                    var line = CreateImage("Edge", rt, Vector2.zero, Vector2.zero, Vector2.zero, RoomEdge);
                    Stretch(line.rectTransform); line.sprite = lineBox; line.type = Image.Type.Sliced; line.pixelsPerUnitMultiplier = .8f;
                    var label = Text("Name", rt, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero, map.full ? 19f : 12f, Hex(0xead7b3));
                    label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one; label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
                    label.text = Initial(layout, box.Name); label.textWrappingMode = TextWrappingModes.Normal;
                    map.rooms.Add((box, image, line));
                }
                // Each flight of stairs shows on both floors it joins.
                foreach (var s in layout.Stairs)
                {
                    if (layout.StoreyOf(layout.Room(s.Lower)) != storey && layout.StoreyOf(layout.Room(s.Upper)) != storey) continue;
                    var rt = Panel("Stairs", map.content, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(.5f, .5f), Hex(0xe4d6b2, .55f));
                    rt.anchorMin = At(s.MinX, s.MinZ); rt.anchorMax = At(s.MaxX, s.MaxZ); rt.offsetMin = rt.offsetMax = Vector2.zero;
                    rt.GetComponent<Image>().sprite = null;
                    map.stairs.Add(rt.gameObject);
                }
                if (!map.badge)
                {
                    map.badge = Text("Storey", map.content, new Vector2(0f, 1f), new Vector2(6f, -4f), new Vector2(220f, 22f), map.full ? 16f : 11f, Cream, TextAlignmentOptions.TopLeft);
                    map.badge.rectTransform.pivot = new Vector2(0f, 1f); map.badge.characterSpacing = 6f;
                }
                map.badge.text = storeyCount > 1 ? layout.StoreyLabel(storey) : "";
                // Rebuilt floors go under the player dots, which already exist after the first floor you saw.
                int order = 0;
                foreach (var (_, fill, _) in map.rooms) fill.transform.SetSiblingIndex(order++);
                foreach (var flight in map.stairs) flight.transform.SetSiblingIndex(order++);
                map.badge.transform.SetAsLastSibling();
                map.layout = layout;
                map.storey = storey;
            }
            string here = LocalPlayer ? RoomOf(layout, LocalPlayer) : null;
            var schedule = clearOut && clearOut.Running ? clearOut.Schedule : null;
            foreach (var (box, fill, line) in map.rooms)
            {
                line.color = box.Name == here ? RoomEdgeHere : RoomEdge;
                var phase = schedule?.PhaseOf(box.Name, clearOut.Elapsed) ?? RoomPhase.Safe;
                fill.color = phase == RoomPhase.Closed || phase == RoomPhase.Filling ? RoomClosed
                    : phase == RoomPhase.Warning ? Color.Lerp(RoomWarn, RoomFill, Mathf.PingPong(Time.unscaledTime * 1.4f, 1f))
                    : box.Name == here ? RoomHere : RoomFill;
            }
            int n = 0;
            foreach (var p in World.Players)
            {
                if (!p || p.IsEliminated) continue;
                if (n == map.dots.Count)
                    map.dots.Add(Panel("Player", map.content, Vector2.zero, Vector2.zero, Vector2.one * 10f, new Vector2(.5f, .5f), Cream, true).GetComponent<Image>());
                var image = map.dots[n++];
                image.gameObject.SetActive(true);
                bool you = p == LocalPlayer;
                var rt = image.rectTransform;
                rt.anchorMin = rt.anchorMax = At(p.transform.position.x, p.transform.position.z);
                rt.sizeDelta = Vector2.one * (map.full ? (you ? 20f : 14f) : (you ? 12f : 8f));
                image.color = p.IsDowned ? Coral : you ? Hex(0x9ff8d3) : p.Color;
                // Someone on another floor shows faintly where they are, above or below you.
                if (storeyCount > 1 && StoreyOfPlayer(p) != storey) image.color = new Color(image.color.r, image.color.g, image.color.b, .4f);
            }
            for (int i = n; i < map.dots.Count; i++) map.dots[i].gameObject.SetActive(false);
        }

        // ---- Safe area and layout modes ----

        void ApplySafeArea()
        {
            if (!safe) return;
            lastWidth = Screen.width; lastHeight = Screen.height; lastSafe = Screen.safeArea;
            if (lastWidth <= 0 || lastHeight <= 0) return;
            safe.anchorMin = new Vector2(lastSafe.xMin / lastWidth, lastSafe.yMin / lastHeight); safe.anchorMax = new Vector2(lastSafe.xMax / lastWidth, lastSafe.yMax / lastHeight);
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            // The bag's scrim and the pause shade reach past the safe area to the screen's edges.
            float safeWidth = Mathf.Max(1f, lastSafe.width), safeHeight = Mathf.Max(1f, lastSafe.height);
            var coverMin = new Vector2(-lastSafe.xMin / safeWidth, -lastSafe.yMin / safeHeight);
            var coverMax = new Vector2(1f + (lastWidth - lastSafe.xMax) / safeWidth, 1f + (lastHeight - lastSafe.yMax) / safeHeight);
            foreach (var cover in new[] { bagBackdrop, pauseShade }) if (cover) { cover.anchorMin = coverMin; cover.anchorMax = coverMax; }
            bool portrait = lastWidth < lastHeight, touch = TouchControlsShown;
            // Touch screens keep the vitals above the move stick, as the browser's coarse-pointer layout does.
            if (vitals) vitals.anchoredPosition = new Vector2(39f, touch ? (portrait ? 420f : 250f) : 110f);
            // Portrait browser windows keep the bag above the thumb controls; mobile players
            // normally use landscape, but resizing must never hide the craft economy.
            if (tray)
            {
                tray.anchorMin = tray.anchorMax = tray.pivot = portrait ? new Vector2(0f, 1f) : new Vector2(.5f, 0f);
                tray.anchoredPosition = portrait ? new Vector2(38f, -134f) : new Vector2(0f, touch ? 24f : 52f);
            }
            if (playRect)
            {
                playRect.anchorMin = playRect.anchorMax = portrait ? Vector2.zero : new Vector2(.5f, 1f);
                playRect.pivot = playRect.anchorMin;
                playRect.anchoredPosition = portrait ? new Vector2(30f, 340f) : new Vector2(0f, -185f);
            }
            if (status) status.anchoredPosition = new Vector2(0f, portrait ? -360f : -168f);
            ResizeStatus();
        }
        void ResizeStatus()
        {
            if (!status || !UiCanvas) return;
            float logicalWidth = lastSafe.width / Mathf.Max(.01f, UiCanvas.scaleFactor);
            float width = Mathf.Min(630f, logicalWidth - 600f);
            if (width < 360f) width = Mathf.Min(630f, logicalWidth - 64f);
            var size = statusText.GetPreferredValues(statusText.text, width - 40f, 0f);
            status.sizeDelta = new Vector2(Mathf.Min(width, size.x + 44f), size.y + 24f);
            statusText.rectTransform.sizeDelta = new Vector2(Mathf.Min(width, size.x + 44f) - 40f, size.y + 4f);
        }
        void OnApplicationFocus(bool focused)
        {
            if (focused) return; TouchBinding.Shared.ReleaseAll();
            foreach (var stick in sticks) if (stick) stick.Release(); if (LocalPlayer) LocalPlayer.Summoner.Close();
            // Alt-tabbing out of a match pauses it and lets the mouse go; a click on Resume takes it back.
            if (PauseOnFocusLoss && safe && LocalPlayer && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != Session.HubScene)
                SetPaused(true);
        }
        void OnDisable()
        {
            TouchBinding.Shared.ReleaseAll();
            if (bagHidden) HideForBag(false);
            if (Active == this) Active = null;
        }
        void OnDestroy()
        {
            if (Paused) Time.timeScale = resumeScale;
            foreach (var sprite in ownedSprites) if (sprite) Destroy(sprite);
            foreach (var texture in ownedTextures) if (texture) Destroy(texture);
        }

        // ---- What the directors call ----

        public void SetTitle(string text, string sub = "")
        {
            // Countdowns set the same words every frame; only a change re-measures the plaque.
            if (title && title.text == text && (!subtitle || subtitle.text == sub)) return;
            if (title) title.text = text;
            if (subtitle) subtitle.text = sub;
            FitTitlePlaque();
        }
        public void SetInstruction(string main, string hint = "")
        {
            string text = string.IsNullOrEmpty(hint) ? main : $"<b>{main}</b>\n<size=82%><color=#173B3CB3>{hint}</color></size>";
            if (instruction) instruction.text = text;
            if (!statusText || statusText.text == OnCream(text)) return;
            statusText.text = OnCream(text);
            status.gameObject.SetActive(!string.IsNullOrEmpty(main) || !string.IsNullOrEmpty(hint));
            ResizeStatus();
        }
        public void SetChecklist(string text)
        {
            if (checklist) checklist.text = text;
            checklistText = OnCream(text ?? "").Replace("<b>", "<size=72%><cspace=6>").Replace("</b>", "</cspace></size>");
        }
        public void SetTimer(string text)
        {
            if (timer) timer.text = text;
            if (timer) timer.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }
        public void SetScoreboard(IReadOnlyList<PlayerController> currentPlayers, Func<PlayerController, int> currentWins, int targetWins, bool winsVisible)
        { players = currentPlayers; wins = currentWins; roundsToWin = targetWins; showWins = winsVisible; }

        /// <summary>The directors' colours were picked for dark panels; on the cream panels they need darker twins.</summary>
        static string OnCream(string text) => text
            .Replace("#FFF4E0AA", "#173B3C99").Replace("#FFF4E0CC", "#173B3CB3").Replace("#FFF4E0", "#173B3C")
            .Replace("#8FD18B", "#2F7A5C").Replace("#FFD24A", "#B86647");

        static readonly Regex Words = new("(?<=[a-z])(?=[A-Z])");
        static string Spaced(string name) => Words.Replace(name, " ");

        /// <summary>A room's letter on the small map, or two letters when another room starts the same way (Bedroom, Bathroom).</summary>
        static string RoomOf(HouseLayout layout, PlayerController p) => layout.RoomAt(p.transform.position.x, p.transform.position.y, p.transform.position.z);

        public static string Initial(HouseLayout layout, string name) =>
            name.Substring(0, layout.Rooms.Count(r => r.Name[0] == name[0]) > 1 ? Mathf.Min(2, name.Length) : 1);

        // ---- Builders ----

        LetterCell MakeCell(string label, RectTransform parent, Vector2 position, Vector2 size, float fontSize)
        {
            var holder = Rect(label, parent, new Vector2(0f, 1f), position, size); holder.pivot = new Vector2(0f, 1f);
            var edge = Panel("Edge", holder, new Vector2(.5f, .5f), new Vector2(0f, -3f), size, new Vector2(.5f, .5f), TileEdge).GetComponent<Image>();
            var face = Panel("Face", holder, new Vector2(.5f, .5f), Vector2.zero, size, new Vector2(.5f, .5f), Tile).GetComponent<Image>();
            var letter = Text("Letter", face.transform, new Vector2(.5f, .5f), Vector2.zero, size, fontSize, Ink);
            letter.fontStyle = FontStyles.Bold;
            return new LetterCell { face = face, edge = edge, letter = letter };
        }
        HandView MakeHand(string label, RectTransform parent, Vector2 position, Vector2 size, int slot, Action onTap)
        {
            var rt = Panel(label, parent, Vector2.zero, position, size, Vector2.zero, Glass);
            var view = new HandView { face = rt.GetComponent<Image>() };
            view.face.raycastTarget = true;
            rt.gameObject.AddComponent<Button>().onClick.AddListener(() => onTap());
            // An outline effect would show through the glass, so the edge is a ring of its own.
            view.edge = CreateImage("Edge", rt, Vector2.zero, Vector2.zero, Vector2.zero, GlassIdle);
            Stretch(view.edge.rectTransform); view.edge.sprite = ringSprite; view.edge.type = Image.Type.Sliced;
            var key = Text("Key", rt, new Vector2(0f, 1f), new Vector2(8f, -5f), new Vector2(20f, 18f), 12f, OnGame, TextAlignmentOptions.Left);
            key.text = (slot + 1).ToString(); key.rectTransform.pivot = new Vector2(0f, 1f);
            view.icon = Rect("Icon", rt, new Vector2(.5f, 1f), new Vector2(0f, -4f), new Vector2(54f, 50f)).gameObject.AddComponent<RawImage>();
            ((RectTransform)view.icon.transform).pivot = new Vector2(.5f, 1f); view.icon.raycastTarget = false;
            view.label = Text("Word", rt, new Vector2(.5f, 0f), new Vector2(0f, 6f), new Vector2(size.x - 10f, 20f), 12f, Ink);
            view.label.rectTransform.pivot = new Vector2(.5f, 0f);
            OnGameText(key); OnGameText(view.label);
            return view;
        }
        Sprite Icon(TouchAction action)
        {
            if (!iconAtlas || (int)action > 8) return null;
            // Measured silhouettes avoid bleed where the generated atlas differs from a regular grid.
            var bounds = new Rect[] { new(34,19,391,376), new(453,100,369,286), new(925,28,280,373), new(39,445,365,336), new(499,434,313,353), new(874,439,333,346), new(35,833,381,375), new(487,813,281,405), new(836,870,410,314) };
            var r = bounds[(int)action]; float sx = iconAtlas.width / 1254f, sy = iconAtlas.height / 1254f;
            var sprite = Sprite.Create(iconAtlas, new Rect(r.x * sx, iconAtlas.height - (r.y + r.height) * sy, r.width * sx, r.height * sy), new Vector2(.5f, .5f), 100f);
            ownedSprites.Add(sprite); return sprite;
        }
        /// <summary>A rounded square, a circle, or (with a ring width) only a rounded square's edge.</summary>
        Sprite MakeShape(bool circle, float ring = 0f)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = circle ? "HUD circle" : "HUD panel", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - 31.5f), dy = Mathf.Abs(y - 31.5f);
                float distance = circle ? Mathf.Sqrt(dx * dx + dy * dy) - 30f : new Vector2(Mathf.Max(dx - 20f, 0f), Mathf.Max(dy - 20f, 0f)).magnitude - 11f;
                float alpha = Mathf.Clamp01(.5f - distance);
                if (ring > 0f) alpha *= Mathf.Clamp01(.5f + distance + ring);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            if (ring > 0f) texture.name = "HUD ring";
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, circle ? Vector4.zero : new Vector4(14f, 14f, 14f, 14f));
            ownedSprites.Add(sprite); ownedTextures.Add(texture); return sprite;
        }
        /// <summary>A crisp one-texel box edge for the map's rooms, sliced so it stays a line at any size.</summary>
        Sprite MakeLineBox()
        {
            const int size = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HUD line box", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                pixels[y * size + x] = new Color(1f, 1f, 1f, x == 0 || y == 0 || x == size - 1 || y == size - 1 ? 1f : 0f);
            texture.SetPixels(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one);
            ownedSprites.Add(sprite); ownedTextures.Add(texture); return sprite;
        }

        /// <summary>A soft rounded square for drop shadows and glows, sliced so its blur keeps its width at any size.</summary>
        Sprite MakeSoft()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HUD soft", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x - 31.5f) - 3.5f, 0f), dy = Mathf.Max(Mathf.Abs(y - 31.5f) - 3.5f, 0f);
                float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 28f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, 1f - t * t * (3f - 2f * t));
            }
            texture.SetPixels(pixels); texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * 31f);
            ownedSprites.Add(sprite); ownedTextures.Add(texture); return sprite;
        }

        /// <summary>The bag's scrim, the web's radial gradient: #10292B at .55 in the middle to #0B1D1F at .85 at the corners.</summary>
        Sprite MakeScrim()
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Bag scrim", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            Color middle = Hex(0x10292b, .55f), corner = Hex(0x0b1d1f, .85f);
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = (x - 63.5f) / 63.5f, dy = (y - 63.5f) / 63.5f;
                pixels[y * size + x] = Color.Lerp(middle, corner, Mathf.Clamp01(Mathf.Sqrt((dx * dx + dy * dy) * .5f)));
            }
            texture.SetPixels(pixels); texture.Apply(false);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f);
            ownedSprites.Add(sprite); ownedTextures.Add(texture); return sprite;
        }

        RectTransform Rect(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor; rt.anchoredPosition = position; rt.sizeDelta = size; return rt;
        }
        RectTransform Panel(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Vector2 pivot, Color color, bool circle = false)
        {
            var rt = Rect(label, parent, anchor, position, size); rt.pivot = pivot;
            var image = rt.gameObject.AddComponent<Image>(); image.sprite = circle ? roundSprite : panelSprite;
            image.type = circle ? Image.Type.Simple : Image.Type.Sliced; image.color = color; image.raycastTarget = false; return rt;
        }
        Image CreateImage(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, Color color)
        {
            var image = Rect(label, parent, anchor, position, size).gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        Image Fill(string label, Transform parent, Color color)
        {
            var image = CreateImage(label, parent, Vector2.zero, Vector2.zero, Vector2.zero, color); image.rectTransform.anchorMax = Vector2.one;
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero; image.sprite = panelSprite;
            image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Horizontal; image.fillOrigin = 0; return image;
        }
        TextMeshProUGUI Text(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var text = Rect(label, parent, anchor, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = GameAssets.I ? GameAssets.I.font : TMP_Settings.defaultFontAsset; text.fontSize = fontSize; text.fontStyle = FontStyles.Bold;
            text.color = color; text.alignment = alignment; text.textWrappingMode = TextWrappingModes.NoWrap; text.raycastTarget = false; return text;
        }
        Button MakeButton(string label, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, string caption, Action action, Color? colour = null)
        {
            var rt = Panel(label, parent, anchor, position, size, anchor, colour ?? Teal); rt.GetComponent<Image>().raycastTarget = true;
            var button = rt.gameObject.AddComponent<Button>(); button.onClick.AddListener(() => action?.Invoke());
            Text("Label", rt, new Vector2(.5f, .5f), Vector2.zero, size - new Vector2(8f, 4f), 20f, colour.HasValue ? Ink : Cream).text = caption; return button;
        }
    }
}
