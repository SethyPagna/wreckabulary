using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// The PC lobby, laid out like a shooter's front end (user, 6 Oct 2026): your avatar standing
    /// in the selected map, home / settings / power and the trophy and cart top left, the
    /// LOADOUT · PLAY · CAREER pages in the middle, coins top right, the party and friends
    /// down the right, and lobby messages between them. One page shows at a time.
    /// </summary>
    public sealed class LobbyMenu : MonoBehaviour
    {
        public const string OutfitKey = "wv.outfit.0";
        public const string Home = "home", Loadout = "loadout", Play = "play", CareerPage = "career",
            Shop = "shop", Trophy = "trophy", Settings = "settings";
        public const string Practice = "practice", Matchmaking = "matchmaking", Workshop = "workshop";
        public const string TutorialMode = "Tutorial", WorkshopMode = "Workshop", RoomMode = "Room";
        public const string VolumeKey = "wv.volume", VsyncKey = "wv.vsync";
        /// <summary>Practice modes, in the order the PLAY page shows them.</summary>
        public static readonly string[] Modes = { "Dibs", "Duos", "MovingDay", "MovingOut" };
        /// <summary>Seconds between GO and the match loading; tests shorten it.</summary>
        public static float StartDelay = 3f;
        const float BarHeight = 76f, RailWidth = 300f, FeedWidth = 360f, Gutter = 20f;

        public static LobbyMenu Instance { get; private set; }

        public Outfit Outfit { get; private set; }
        public Career Career { get; private set; }
        public string Map { get; private set; }
        public string Queue { get; private set; } = Practice;
        public string Mode { get; private set; } = "Dibs";
        public LobbyStage Stage { get; private set; }
        public string Current { get; private set; }
        public bool Starting => startAt > 0f;
        public IReadOnlyList<string> Messages => messages;

        readonly List<string> messages = new List<string>();
        readonly Dictionary<string, LobbyPage> pages = new Dictionary<string, LobbyPage>();
        readonly Dictionary<string, (TextMeshProUGUI label, Image underline)> tabs = new Dictionary<string, (TextMeshProUGUI, Image)>();
        readonly List<PlayerController> frozen = new List<PlayerController>();
        Canvas canvas;
        RectTransform safe, pageHost, feed, feedList, rail, status, quitDialog;
        TextMeshProUGUI coins, initial, statusTitle, statusDetail, statusCount;
        Button power, cancelButton;
        GameHud hud;
        /// <summary>The pad that pressed GO, which then plays the match.</summary>
        Gamepad goPad;
        float startAt;
        bool hidden;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
        }

        /// <summary>Play pressed in the editor with Hub open: this project's Enter Play Mode Options skip
        /// the scene reload, so the open scene never raises sceneLoaded and the lobby wouldn't appear.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OpenOnPlay() => OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        static void OnScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == Session.HubScene && !FindAnyObjectByType<LobbyMenu>())
                new GameObject("Lobby").AddComponent<LobbyMenu>();
        }

        void Awake() => Instance = this;

        void Start()
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            Outfit = wardrobe.Default.Clone();
            string saved = PlayerPrefs.GetString(OutfitKey, "");
            if (!string.IsNullOrEmpty(saved))
            {
                var candidate = Outfit.Deserialize(saved);
                if (wardrobe.Problems(candidate).Count == 0) Outfit = candidate;
            }
            Career = MatchTally.LoadCareer();
            // Looks picked before the shop existed stay yours.
            if (Career.Keep(Outfit.ItemSkins.Values.FirstOrDefault() ?? "Classic", Outfit.ColourOf("Top") ?? ""))
                MatchTally.SaveCareer(Career);
            Map = GameConfig.Current.Houses.ContainsKey(Session.MapId) ? Session.MapId : GameConfig.Current.Houses.Keys.First();
            // Back from a match, GO starts the same thing again.
            Choose(Session.LobbyQueue, Session.LobbyMode);
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
            if (PlayerPrefs.HasKey(VsyncKey)) QualitySettings.vSyncCount = PlayerPrefs.GetInt(VsyncKey) > 0 ? 1 : 0;
            SuspendHub();
            Stage = new GameObject("Lobby stage").AddComponent<LobbyStage>();
            Stage.Show(Map, Outfit);
            BuildCanvas();
            BuildBar();
            BuildRail();
            BuildFeed();
            BuildStatus();
            foreach (var page in new LobbyPage[] { new HomePage(), new LoadoutPage(), new PlayPage(), new CareerScreen(),
                new ShopPage(), new TrophyPage(), new SettingsPage() })
            {
                page.Create(this, pageHost);
                pages[page.Id] = page;
            }
            Open(Home);
            Post($"Welcome back, {Career.Name}.");
            if (MatchTally.LastResult != null)
            {
                var last = MatchTally.LastResult;
                Post($"{ModeName(last.Mode)} {(last.Won ? "won" : "finished")}: {last.Score} points, +{last.Coins} coins, +{last.Xp} XP" +
                    (MatchTally.LastWasBest ? ". New best!" : "."));
                MatchTally.LastResult = null;
            }
            RefreshBar();
        }

        void OnEnable()
        {
            // Back from the Creative Workshop, which hides the lobby while it is open.
            if (!Stage) return;
            Stage.TakeCamera();
            if (Current != null && pages.TryGetValue(Current, out var page) && page.First && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(page.First.gameObject);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // The stage gives the camera back to the hub as it goes.
            if (Stage) Destroy(Stage.gameObject);
            ResumeHub();
        }

        /// <summary>Stops the hub's own controls while the lobby is up: no joining by clicking,
        /// no walking about on keyboard players, and the hub HUD out of the way.</summary>
        void SuspendHub()
        {
            var joins = FindAnyObjectByType<PlayerJoinManager>();
            if (joins)
            {
                joins.AllowJoining = false;
                // Only real controls: test players keep moving so the hub can still be tested.
                foreach (var player in joins.Players)
                    if (player && (player.Binding is DesktopBinding || player.Binding is TouchBinding ||
                        player.Binding is KeyboardBinding || player.Binding is GamepadBinding) && !player.Frozen)
                    { player.Frozen = true; frozen.Add(player); }
            }
            hud = FindAnyObjectByType<GameHud>();
            if (hud) foreach (var c in hud.GetComponentsInChildren<Canvas>(true)) c.enabled = false;
        }

        void ResumeHub()
        {
            foreach (var player in frozen) if (player) player.Frozen = false;
            frozen.Clear();
            if (hud) foreach (var c in hud.GetComponentsInChildren<Canvas>(true)) c.enabled = true;
        }

        void BuildCanvas()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            // PC first: keep the bar the same height on wide screens and let width grow.
            scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (!FindAnyObjectByType<EventSystem>())
            {
                var system = new GameObject("Menu Event System");
                system.AddComponent<EventSystem>();
                // Like the HUD's own: without actions a controller can't move or press anything.
                system.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            safe = LobbyKit.Rect(transform, "Safe area").Fill();
            // Behind everything: dragging on the open view turns your avatar.
            var drag = LobbyKit.Rect(safe, "Turn area").Fill();
            drag.Paint(new Color(0, 0, 0, 0));
            drag.gameObject.AddComponent<LobbyDrag>().Turned = degrees => Stage.Turn(degrees);
            pageHost = LobbyKit.Rect(safe, "Pages").Place(Vector2.zero, Vector2.one,
                new Vector2(Gutter, Gutter), new Vector2(-(RailWidth + Gutter * 2), -(BarHeight + Gutter)));
        }

        void BuildBar()
        {
            var bar = LobbyKit.Rect(safe, "Top bar").Place(new Vector2(0, 1), Vector2.one, new Vector2(0, -BarHeight), Vector2.zero);
            bar.Paint(LobbyKit.Bar);
            var left = LobbyKit.Row(bar, "System", 4, 0);
            left.Place(new Vector2(0, 0), new Vector2(0, 1), new Vector2(14, 10), new Vector2(560, -10));
            LobbyKit.IconButton(left, LobbyIcons.Home, "Home", () => Open(Home)).Size(56, 56);
            LobbyKit.IconButton(left, LobbyIcons.Settings, "Settings", () => Open(Settings)).Size(56, 56);
            power = LobbyKit.IconButton(left, LobbyIcons.Power, "Quit", AskQuit).Size(56, 56);
            LobbyKit.Rect(left, "Divider").Paint(LobbyKit.Line).Size(2, 36);
            LobbyKit.IconButton(left, LobbyIcons.Trophy, "Leaderboard", () => Open(Trophy)).Size(56, 56);
            LobbyKit.IconButton(left, LobbyIcons.Cart, "Shop", () => Open(Shop)).Size(56, 56);
            left.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            left.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleLeft;

            // PLAY sits exactly in the middle of the screen.
            var centre = LobbyKit.Row(bar, "Pages", 0, 0);
            centre.Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(660, BarHeight));
            foreach (var (id, title) in new[] { (Loadout, "LOADOUT"), (Play, "PLAY"), (CareerPage, "CAREER") })
            {
                var tab = LobbyKit.Ghost(centre, title, () => Open(id));
                tab.Size(220, BarHeight);
                ((Image)tab.targetGraphic).sprite = null;
                var label = LobbyKit.Text(tab.transform, title, id == Play ? 34 : 28, LobbyKit.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
                label.characterSpacing = 4;
                label.rectTransform.Fill();
                var underline = LobbyKit.Rect(tab.transform, "Underline").Place(new Vector2(.2f, 0), new Vector2(.8f, 0), Vector2.zero, new Vector2(0, 5)).Paint(LobbyKit.Honey);
                underline.raycastTarget = false;
                tabs[id] = (label, underline);
            }

            // Coins only: the party lives in the rail, and sound and help live in Settings.
            var wallet = LobbyKit.Row(bar, "Wallet", 10, 0);
            wallet.Place(new Vector2(1, 0), new Vector2(1, 1), new Vector2(-(RailWidth + Gutter), 14), new Vector2(-Gutter, -14));
            wallet.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleRight;
            wallet.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = false;
            LobbyKit.Icon(wallet, LobbyIcons.Coin, LobbyKit.Honey).Size(34, 34);
            coins = LobbyKit.Text(wallet, "0", 30, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Bold).Size(150, 48);
        }

        void BuildRail()
        {
            rail = LobbyKit.Rect(safe, "Party and friends").Place(new Vector2(1, 0), Vector2.one,
                new Vector2(-RailWidth - Gutter, Gutter), new Vector2(-Gutter, -(BarHeight + Gutter)));
            rail.Paint(LobbyKit.Panel, true);
            var column = LobbyKit.Column(rail, "Rail", 10, 18).Fill();
            column.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            LobbyKit.Heading(column, "Party  1 / 4");
            var you = LobbyKit.Row(column, "You", 12, 10).Size(-1, 74);
            you.Paint(LobbyKit.Card, true);
            var badge = LobbyKit.Rect(you, "Badge");
            badge.Paint(LobbyKit.Tomato, true);
            badge.Size(54, 54);
            initial = LobbyKit.Text(badge, LobbyKit.Upper(Career.Name.Substring(0, 1)), 30, LobbyKit.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            initial.rectTransform.Fill();
            var who = LobbyKit.Column(you, "Who", 0, 0);
            who.Size(170, -1, 1);
            LobbyKit.Text(who, Career.Name, 24, LobbyKit.Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Bold).Size(-1, 30).name = "Name";
            LobbyKit.Text(who, "Level " + Career.Level + "  ·  in the lobby", 18, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft).Size(-1, 24).name = "Level";
            LobbyKit.Rect(column, "Gap").Size(-1, 14);
            LobbyKit.Heading(column, "Friends");
            var empty = LobbyKit.Text(column, "Friends and other players show up here once online or LAN play is built. Practice matches fill the seats with bots.",
                19, LobbyKit.Muted, TextAlignmentOptions.TopLeft);
            empty.textWrappingMode = TextWrappingModes.Normal;
            empty.Size(-1, 120);
        }

        void BuildFeed()
        {
            // Between the page area and the rail, like a shooter's news and chat column.
            feed = LobbyKit.Rect(safe, "Messages").Place(new Vector2(1, .38f), Vector2.one,
                new Vector2(-(RailWidth + Gutter * 2 + FeedWidth), 0), new Vector2(-(RailWidth + Gutter * 2), -(BarHeight + Gutter)));
            var column = LobbyKit.Column(feed, "Feed", 8, 0).Fill();
            column.GetComponent<VerticalLayoutGroup>().childAlignment = TextAnchor.UpperLeft;
            LobbyKit.Heading(column, "Lobby");
            feedList = LobbyKit.Column(column, "Entries", 6, 0);
        }

        void BuildStatus()
        {
            // Just under the PLAY tab, on every page, the way a shooter shows a match being found.
            status = LobbyKit.Rect(safe, "Starting").Pin(new Vector2(.5f, 1), new Vector2(0, -(BarHeight + 12)), new Vector2(660, 100));
            status.Paint(LobbyKit.Panel, true);
            var accent = LobbyKit.Rect(status, "Accent").Place(Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(8, 0));
            accent.Paint(LobbyKit.Honey);
            statusTitle = LobbyKit.Text(status, "", 20, LobbyKit.Honey, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            statusTitle.characterSpacing = 4;
            statusTitle.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(30, 0), new Vector2(-250, -16));
            statusDetail = LobbyKit.Text(status, "", 23, LobbyKit.Cream, TextAlignmentOptions.BottomLeft);
            statusDetail.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(30, 18), new Vector2(-250, 0));
            statusCount = LobbyKit.Text(status, "", 50, LobbyKit.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            statusCount.rectTransform.Place(new Vector2(1, 0), Vector2.one, new Vector2(-240, 0), new Vector2(-145, 0));
            cancelButton = LobbyKit.LabelButton(status, "CANCEL", LobbyKit.Card, LobbyKit.Cream, 22, CancelStart);
            ((RectTransform)cancelButton.transform).Place(new Vector2(1, .5f), new Vector2(1, .5f), new Vector2(-140, -28), new Vector2(-20, 28));
            status.gameObject.SetActive(false);
        }

        public void Open(string id)
        {
            if (!pages.TryGetValue(id, out var page)) throw new ArgumentException("No lobby page " + id, nameof(id));
            foreach (var other in pages.Values) other.Root.gameObject.SetActive(other == page);
            Current = id;
            // Drops any colour being tried on in the shop.
            Stage.Dress(Outfit);
            page.Refresh();
            Stage.Frame(page.Focus);
            feed.gameObject.SetActive(page.ShowFeed);
            foreach (var kv in tabs)
            {
                bool on = kv.Key == id;
                kv.Value.label.color = on ? LobbyKit.Honey : LobbyKit.Cream;
                kv.Value.underline.enabled = on;
            }
            var first = page.First;
            if (EventSystem.current && first) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        public void Post(string message)
        {
            messages.Insert(0, message);
            if (messages.Count > 6) messages.RemoveAt(messages.Count - 1);
            if (!feedList) return;
            LobbyKit.Clear(feedList);
            for (int i = 0; i < messages.Count; i++)
            {
                var card = LobbyKit.Rect(feedList, "Message");
                card.Paint(new Color(LobbyKit.Bar.r, LobbyKit.Bar.g, LobbyKit.Bar.b, .72f - i * .08f), true);
                var text = LobbyKit.Text(card, messages[i], 19, i == 0 ? LobbyKit.Cream : LobbyKit.Muted, TextAlignmentOptions.MidlineLeft);
                text.textWrappingMode = TextWrappingModes.Normal;
                text.rectTransform.Place(Vector2.zero, Vector2.one, new Vector2(14, 6), new Vector2(-14, -6));
                card.Size(-1, messages[i].Length > 40 ? 66 : 44);
            }
        }

        public void RefreshBar()
        {
            if (coins) coins.text = Career.Coins.ToString("N0", CultureInfo.InvariantCulture);
            if (initial) initial.text = LobbyKit.Upper(Career.Name.Substring(0, 1));
            var level = rail ? rail.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "Level") : null;
            if (level) level.text = "Level " + Career.Level + "  ·  " + (Starting ? "starting a match" : "in the lobby");
            var name = rail ? rail.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.name == "Name") : null;
            if (name) name.text = Career.Name;
        }

        /// <summary>Wears an outfit, saves it and dresses the avatar in the lobby.</summary>
        public void Wear(Outfit next)
        {
            var problems = GameConfig.Current.Wardrobe.Problems(next);
            if (problems.Count > 0) throw new InvalidOperationException(string.Join("\n", problems));
            Outfit = next;
            PlayerPrefs.SetString(OutfitKey, Outfit.Serialize());
            PlayerPrefs.Save();
            Stage.Dress(Outfit);
            var joins = FindAnyObjectByType<PlayerJoinManager>();
            var player = joins ? joins.Players.FirstOrDefault(p => p.Binding is not BotBinding) : null;
            if (player) player.GetComponent<PlayerAppearance>()?.ApplyOutfit(Outfit);
        }

        public void SaveCareer()
        {
            MatchTally.SaveCareer(Career);
            RefreshBar();
        }

        /// <summary>Chooses what GO starts. A null argument keeps the current choice.</summary>
        public void Choose(string queue = null, string mode = null, string map = null)
        {
            if (Starting) CancelStart();
            if (queue != null) Queue = queue;
            if (mode != null) Mode = mode;
            if (Queue == Workshop && Mode != TutorialMode && Mode != WorkshopMode) Mode = TutorialMode;
            if (Queue != Workshop && !Modes.Contains(Mode) && !(Queue == Matchmaking && Mode == RoomMode)) Mode = Modes[0];
            Session.LobbyQueue = Queue;
            Session.LobbyMode = Mode;
            if (map != null && map != Map)
            {
                Session.SelectMap(map);
                Map = map;
                Stage.SetMap(map);
                Post("Map: " + GameConfig.Current.HouseFor(map).Name + ".");
            }
        }

        /// <summary>Why GO can't start the current choice, or null when it can.</summary>
        public string Blocked => Queue == Matchmaking
            ? "Matchmaking needs online or LAN play, which isn't built yet. Practice plays every mode with bots."
            : null;

        /// <summary>GO: back to the lobby with a short countdown, like a match being found.</summary>
        public void Go()
        {
            if (Starting) return;
            if (Blocked != null) { Post(Blocked); return; }
            // Whoever pressed GO plays: a pad's A or Start seats that pad, anything else the keyboard and mouse.
            goPad = Gamepad.all.FirstOrDefault(p => p.buttonSouth.wasPressedThisFrame || p.startButton.wasPressedThisFrame);
            startAt = Time.unscaledTime + StartDelay;
            status.gameObject.SetActive(true);
            statusTitle.text = Queue == Workshop ? "OPENING" : "STARTING PRACTICE";
            statusDetail.text = Describe();
            Open(Home);
            // Home hides GO while a match starts, so a keyboard or controller lands on CANCEL.
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(cancelButton.gameObject);
            Post("Starting " + Describe() + ".");
            RefreshBar();
        }

        public void CancelStart()
        {
            if (!Starting) return;
            startAt = 0f;
            goPad = null;
            status.gameObject.SetActive(false);
            Post("Cancelled.");
            // Redraws the page with GO back on it and puts a keyboard or controller there.
            if (Current != null) Open(Current);
            RefreshBar();
        }

        /// <summary>"Dibs · Pinwheel House · you + 3 bots" and so on.</summary>
        public string Describe()
        {
            if (Mode == TutorialMode) return "Play & learn · the tutorial room";
            if (Mode == WorkshopMode) return "Creative Workshop · " + GameConfig.Current.HouseFor(Map).Name;
            if (Queue == Matchmaking) return ModeName(Mode) + " · " + GameConfig.Current.HouseFor(Map).Name + " · online";
            return ModeName(Mode) + " · " + GameConfig.Current.HouseFor(Map).Name + " · " + Seats(Mode);
        }

        public static string ModeName(string mode) => mode switch
        {
            "MovingDay" => "Moving Day",
            "MovingOut" => "Moving Out",
            "Tutorial" => "Play & learn",
            "Workshop" => "Creative Workshop",
            "Room" => "Private room",
            _ => mode,
        };

        /// <summary>Who plays in practice, as the match fills its seats today.</summary>
        public static string Seats(string mode) => mode switch
        {
            "Dibs" => "you + 3 bots",
            "Duos" => "you and a bot vs 2 bots",
            _ => "solo",
        };

        void Update()
        {
            var area = Screen.safeArea;
            if (Screen.width > 0 && Screen.height > 0)
            {
                safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
                safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            }
            bool workshopOpen = CreativeWorkshop.Instance;
            if (workshopOpen != hidden)
            {
                hidden = workshopOpen;
                canvas.enabled = !hidden;
            }
            if (hidden) return;
            if (Starting)
            {
                float left = startAt - Time.unscaledTime;
                statusCount.text = Mathf.CeilToInt(Mathf.Max(0f, left)).ToString();
                if (left <= 0f) Launch();
            }
            // Esc or a pad's B steps back: out of the quit dialog, out of a start, then home.
            var keyboard = Keyboard.current;
            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || AnyPad(p => p.buttonEast))
            {
                if (quitDialog) CloseQuit();
                else if (Starting) CancelStart();
                else if (Current != Home) Open(Home);
            }
        }

        static bool AnyPad(Func<Gamepad, ButtonControl> button)
        {
            foreach (var pad in Gamepad.all)
                if (button(pad).wasPressedThisFrame) return true;
            return false;
        }

        void Launch()
        {
            startAt = 0f;
            status.gameObject.SetActive(false);
            // GO hid Home's card and buttons; bring them back whatever happens next.
            Open(Current ?? Home);
            var pad = goPad;
            goPad = null;
            if (Mode == WorkshopMode)
            {
                // The workshop shows the hub's own camera look; OnEnable takes the camera back when it closes.
                Stage.Release();
                if (!Session.OpenWorkshop(Map, out string error)) { Stage.TakeCamera(); Post(error); }
                RefreshBar();
                return;
            }
            Seat(pad);
            if (Mode == TutorialMode) Session.LoadMode(TutorialMode);
            else Session.LoadMode(Mode, Map);
        }

        /// <summary>Seats the controls that pressed GO as you. Anyone else already playing along stays;
        /// a different device for you replaces the one you used last time.</summary>
        static void Seat(Gamepad pad)
        {
            InputBinding you;
            if (pad != null && pad.added) you = new GamepadBinding(pad);
            else
            {
                bool touch = Application.isMobilePlatform || Touchscreen.current != null;
                if (touch) TouchBinding.Shared.Enabled = true;
                you = touch ? TouchBinding.Shared : DesktopBinding.Shared;
            }
            if (Session.Bindings.Exists(b => b.Id == you.Id)) return;
            if (Session.Bindings.Count > 0) Session.Bindings[0] = you;
            else Session.Remember(you);
        }

        void AskQuit()
        {
            if (quitDialog) return;
            var shade = quitDialog = LobbyKit.Rect(safe, "Quit dialog").Fill();
            shade.Paint(LobbyKit.Shade);
            var box = LobbyKit.Rect(shade, "Box").Pin(new Vector2(.5f, .5f), Vector2.zero, new Vector2(560, 240));
            box.Paint(LobbyKit.Panel, true);
            var title = LobbyKit.Text(box, "Leave Wreckabulary?", 34, LobbyKit.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
            title.rectTransform.Place(new Vector2(0, .55f), Vector2.one);
            var quit = LobbyKit.LabelButton(box, "QUIT GAME", LobbyKit.Tomato, LobbyKit.Cream, 24, Application.Quit);
            ((RectTransform)quit.transform).Place(new Vector2(.06f, .12f), new Vector2(.48f, .42f));
            var stay = LobbyKit.LabelButton(box, "STAY", LobbyKit.Card, LobbyKit.Cream, 24, CloseQuit);
            ((RectTransform)stay.transform).Place(new Vector2(.52f, .12f), new Vector2(.94f, .42f));
            // A keyboard or controller stays inside the dialog; the shade stops clicks behind it.
            quit.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = stay };
            stay.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = quit };
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(stay.gameObject);
        }

        void CloseQuit()
        {
            if (quitDialog) { quitDialog.gameObject.SetActive(false); Destroy(quitDialog.gameObject); }
            quitDialog = null;
            if (EventSystem.current && power) EventSystem.current.SetSelectedGameObject(power.gameObject);
        }
    }
}
