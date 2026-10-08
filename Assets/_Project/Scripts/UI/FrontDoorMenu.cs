using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Touch-friendly front door to the house; all choices use the same session as the typewriter.</summary>
    public sealed class FrontDoorMenu : MonoBehaviour
    {
        static readonly Color Cream = new Color(1f, 0.96f, 0.85f);
        static readonly Color Ink = new Color(30f / 255f, 30f / 255f, 36f / 255f);
        static readonly Color Gold = new Color(209f / 255f, 161f / 255f, 83f / 255f);
        static readonly Color Teal = new Color(0.24f, 0.84f, 0.70f);
        Canvas canvas;
        RectTransform safe;
        TextMeshProUGUI mapLabel, lookLabel;
        string[] mapIds;
        int mapIndex, preset, colourIndex, skinIndex;
        Outfit outfit;
        UIStateController states;
        CanvasGroup sheetGroup, toggleGroup;
        GameObject sheet;
        Sprite roundedPanel;
        Texture2D panelTexture;
        static readonly string[] SkinNames = { "Classic", "Candy", "Arcade" };
        static readonly string[] LookNames = { "Neighbour", "Garden explorer", "Cozy scholar" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
        }

        static void OnScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == Session.HubScene && !FindAnyObjectByType<FrontDoorMenu>())
                new GameObject("Front Door Menu").AddComponent<FrontDoorMenu>();
        }

        void Start()
        {
            mapIds = GameConfig.Current.Houses.Keys.ToArray();
            mapIndex = Math.Max(0, Array.IndexOf(mapIds, Session.MapId));
            outfit = PlayerAppearance.DefaultPresentationOutfit();
            string saved = PlayerPrefs.GetString("wv.outfit.0", "");
            if (!string.IsNullOrEmpty(saved))
            {
                var candidate = Outfit.Deserialize(saved);
                if (GameConfig.Current.Wardrobe.Problems(candidate).Count == 0) outfit = candidate;
            }
            preset = outfit.PieceIn("Headwear") == "Hood" ? 2 : outfit.PieceIn("Headwear") == "Cap" ? 1 : 0;
            var topPalette = GameConfig.Current.Wardrobe.Palettes["Top"];
            colourIndex = Math.Max(0, topPalette.FindIndex(c => c.Id == outfit.ColourOf("Top")));
            skinIndex = Math.Max(0, Array.IndexOf(SkinNames, outfit.ItemSkins.Values.FirstOrDefault()));
            roundedPanel = MakePanelSprite();
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (!FindAnyObjectByType<EventSystem>())
            {
                var system = new GameObject("Menu Event System");
                system.AddComponent<EventSystem>();
                system.AddComponent<InputSystemUIInputModule>();
            }
            safe = Rect(transform, "Safe area", Vector2.zero, Vector2.zero);
            safe.anchorMin = Vector2.zero; safe.anchorMax = Vector2.one;
            safe.offsetMin = safe.offsetMax = Vector2.zero;
            sheet = Rect(safe, "Welcome postcard", new Vector2(0, 0), new Vector2(1700, 920)).gameObject;
            sheet.AddComponent<Image>().color = new Color(Ink.r, Ink.g, Ink.b, 0.98f);
            sheetGroup = sheet.AddComponent<CanvasGroup>();
            var border = Rect(sheet.transform, "Gold rule", new Vector2(0, 430), new Vector2(1580, 4));
            border.gameObject.AddComponent<Image>().color = Gold;
            Text(sheet.transform, "WRECKABULARY", new Vector2(-190, 350), new Vector2(1190, 110), 82, Gold);
            Text(sheet.transform, "MAKE WORDS. MAKE A MESS.", new Vector2(0, 266), new Vector2(1560, 54), 28, Teal);
            Text(sheet.transform, "PICK YOUR CHAOS", new Vector2(-385, 178), new Vector2(720, 55), 30, Cream);
            Mode("DIBS", "Last roommate standing", "Dibs", -560, 70);
            Mode("DUOS", "Team up • revive your buddy", "Duos", -190, 70);
            Mode("MOVING DAY", "Spell it • place it", "MovingDay", -560, -108);
            Mode("MOVING OUT", "Grab keepsakes • get out", "MovingOut", -190, -108);
            Text(sheet.transform, "YOUR ROOMMATE", new Vector2(445, 178), new Vector2(590, 55), 30, Cream);
            BuildKeyArt();
            lookLabel = Text(sheet.transform, "", new Vector2(445, -215), new Vector2(590, 40), 23, Teal);
            Button(sheet.transform, "CHANGE OUTFIT", new Vector2(595, 75), new Vector2(280, 66), () => { preset = (preset + 1) % 3; SetLook(true); });
            Button(sheet.transform, "CHANGE COLOUR", new Vector2(595, -10), new Vector2(280, 66), () => { colourIndex++; SetLook(); });
            Button(sheet.transform, "FINISH  /  " + SkinNames[skinIndex], new Vector2(595, -95), new Vector2(280, 66), () =>
            {
                skinIndex = (skinIndex + 1) % SkinNames.Length;
                foreach (var item in GameConfig.Current.Items.Enabled) outfit.ItemSkins[item.Id] = SkinNames[skinIndex];
                SaveLook();
                var selected = EventSystem.current?.currentSelectedGameObject;
                if (selected) selected.GetComponentInChildren<TextMeshProUGUI>().text = "FINISH  /  " + SkinNames[skinIndex];
            });
            Button(sheet.transform, "PLAY & LEARN", new Vector2(-380, -216), new Vector2(720, 56), () =>
            {
                EnsureRoommate(); SaveLook(); Session.LoadMode("Tutorial");
            });
            mapLabel = Text(sheet.transform, "", new Vector2(-370, -306), new Vector2(720, 68), 29, Cream);
            Button(sheet.transform, "CHANGE HOUSE", new Vector2(260, -306), new Vector2(330, 65), () =>
            {
                mapIndex = (mapIndex + 1) % mapIds.Length;
                Session.SelectMap(mapIds[mapIndex]); RefreshLabels();
            });
            Button(sheet.transform, "EXPLORE", new Vector2(600, -306), new Vector2(310, 65), () =>
            {
                EnsureRoommate(); states.TransitionToState(UIState.GameplayHUD);
            });
            var toggle = Button(safe, "PLAY / LOOK", Vector2.zero, new Vector2(180, 42), () =>
            { states.TransitionToState(UIState.MainMenu); });
            toggleGroup = toggle.gameObject.AddComponent<CanvasGroup>();
            var toggleRect = (RectTransform)toggle.transform;
            toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = Vector2.one;
            toggleRect.anchoredPosition = new Vector2(-22, -132);
            toggle.GetComponentInChildren<TextMeshProUGUI>().fontSize = 20f;
            Button soundButton = null;
            soundButton = Button(sheet.transform, GameFeedback.Muted ? "Sound off" : "Sound on", new Vector2(645, 355), new Vector2(230, 52), () =>
            {
                GameFeedback.Muted = !GameFeedback.Muted;
                soundButton.GetComponentInChildren<TextMeshProUGUI>().text = GameFeedback.Muted ? "Sound off" : "Sound on";
            });
            Text(sheet.transform, "SMASH  →  COLLECT  →  SPELL  →  PLAY", new Vector2(0, -397), new Vector2(1560, 44), 24, Gold);
            states = FindFirstObjectByType<UIStateController>();
            if (!states) states = new GameObject("UI State Controller").AddComponent<UIStateController>();
            states.Register(UIState.MainMenu, sheetGroup, sheet.GetComponentInChildren<Button>().gameObject);
            states.StateChanged += UpdateToggle;
            states.TransitionToState(UIState.MainMenu);
            UpdateToggle(states.CurrentState);
            RefreshLabels();
        }

        void BuildKeyArt()
        {
            var texture = Resources.Load<Texture2D>("UI/Generated/house-key-art");
            if (texture)
            {
                var frame = Rect(sheet.transform, "House illustration", Vector2.zero, new Vector2(1700, 920));
                frame.SetAsFirstSibling();
                AddArt(frame, "Generated house art", texture);
                var scrim = Rect(frame, "Charcoal scrim", Vector2.zero, new Vector2(1700, 920));
                var shade = scrim.gameObject.AddComponent<Image>(); shade.color = new Color(Ink.r, Ink.g, Ink.b, 0.84f); shade.raycastTarget = false;
            }
            var portrait = Resources.Load<Texture2D>("UI/Generated/roommate-portrait");
            var wardrobe = Rect(sheet.transform, "Wardrobe card", new Vector2(445, -15), new Vector2(650, 480));
            wardrobe.SetSiblingIndex(1);
            var backing = wardrobe.gameObject.AddComponent<Image>();
            backing.sprite = roundedPanel; backing.type = Image.Type.Sliced; backing.color = new Color(Ink.r, Ink.g, Ink.b, 0.97f); backing.raycastTarget = false;
            if (portrait) AddArt(Rect(sheet.transform, "Roommate portrait", new Vector2(290, -18), new Vector2(280, 350)), "Generated roommate portrait", portrait);
        }

        static void AddArt(RectTransform frame, string name, Texture2D texture)
        {
            var art = Rect(frame, name, Vector2.zero, Vector2.zero);
            var image = art.gameObject.AddComponent<RawImage>(); image.texture = texture; image.raycastTarget = false;
            var fit = art.gameObject.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = (float)texture.width / texture.height;
        }

        void UpdateToggle(UIState state)
        {
            bool visible = state == UIState.GameplayHUD;
            toggleGroup.alpha = visible ? 1f : 0f;
            toggleGroup.interactable = toggleGroup.blocksRaycasts = visible;
        }

        void OnDestroy()
        {
            if (states) states.StateChanged -= UpdateToggle;
            if (roundedPanel) Destroy(roundedPanel);
            if (panelTexture) Destroy(panelTexture);
        }

        void Update()
        {
            if (!safe) return;
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            // Fit the postcard inside both axes, including portrait phones and short ultrawide windows.
            if (sheet)
            {
                float fit = Mathf.Min(1f, Mathf.Max(0.1f, (safe.rect.width - 32f) / 1700f),
                    Mathf.Max(0.1f, (safe.rect.height - 32f) / 920f));
                sheet.transform.localScale = Vector3.one * fit;
            }
        }

        void Mode(string title, string description, string id, float x, float y)
        {
            var button = Button(sheet.transform, title + "\n<size=52%>" + description + "</size>",
                new Vector2(x, y), new Vector2(350, 155), () =>
                {
                    EnsureRoommate(); SaveLook(); Session.LoadMode(id, mapIds[mapIndex]);
                });
            button.GetComponent<Image>().color = id switch
            {
                "Dibs" => new Color(1f, 0.42f, 0.34f),
                "Duos" => Teal,
                "MovingDay" => Gold,
                _ => new Color(0.56f, 0.64f, 0.96f)
            };
            button.GetComponentInChildren<TextMeshProUGUI>().color = Ink;
        }

        static PlayerController EnsureRoommate()
        {
            var joins = FindAnyObjectByType<PlayerJoinManager>();
            if (!joins) throw new InvalidOperationException("The house has no join manager.");
            var player = joins.Players.FirstOrDefault(p => p.Binding is not BotBinding);
            if (player) return player;
            bool touch = Application.isMobilePlatform || Touchscreen.current != null;
            if (touch) TouchBinding.Shared.Enabled = true;
            return joins.Join(touch ? TouchBinding.Shared : DesktopBinding.Shared);
        }

        void SetLook(bool changePreset = false)
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            if (changePreset)
            {
                outfit = preset == 0 ? wardrobe.Default.Clone() : PlayerAppearance.DefaultPresentationOutfit();
                if (preset == 1) { outfit.Pieces["Headwear"] = "Cap"; outfit.Pieces["Back"] = "Satchel"; }
                if (preset == 2) { outfit.Pieces["Headwear"] = "Hood"; outfit.Pieces["Face"] = "Glasses"; }
            }
            var palette = wardrobe.Palettes["Top"];
            outfit.Colours["Top"] = palette[colourIndex % palette.Count].Id;
            foreach (var item in GameConfig.Current.Items.Enabled) outfit.ItemSkins[item.Id] = SkinNames[skinIndex];
            SaveLook(); RefreshLabels();
        }

        void SaveLook()
        {
            var problems = GameConfig.Current.Wardrobe.Problems(outfit);
            if (problems.Count > 0) throw new InvalidOperationException(string.Join("\n", problems));
            PlayerPrefs.SetString("wv.outfit.0", outfit.Serialize());
            PlayerPrefs.Save();
            var joins = FindAnyObjectByType<PlayerJoinManager>();
            var player = joins ? joins.Players.FirstOrDefault(p => p.Binding is not BotBinding) : null;
            if (player) player.GetComponent<PlayerAppearance>()?.ApplyOutfit(outfit);
        }

        void RefreshLabels()
        {
            if (mapLabel) mapLabel.text = "HOUSE  /  " + GameConfig.Current.HouseFor(mapIds[mapIndex]).Name;
            if (lookLabel) lookLabel.text = LookNames[preset] + "  ·  " +
                (GameConfig.Current.Wardrobe.Colour("Top", outfit.ColourOf("Top"))?.Name ?? "Pool Teal");
        }

        static RectTransform Rect(Transform parent, string name, Vector2 at, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = at; rect.sizeDelta = size; return rect;
        }

        static TextMeshProUGUI Text(Transform parent, string text, Vector2 at, Vector2 size, float fontSize, Color colour)
        {
            var rect = Rect(parent, text, at, size);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = GameAssets.I.font; label.text = text; label.fontSize = fontSize;
            label.alignment = TextAlignmentOptions.Center; label.color = colour; label.raycastTarget = false;
            return label;
        }

        Sprite MakePanelSprite()
        {
            const int size = 64;
            panelTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Menu rounded panel", filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var edge = new Vector2(Mathf.Max(Mathf.Abs(x - 31.5f) - 20f, 0f), Mathf.Max(Mathf.Abs(y - 31.5f) - 20f, 0f));
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(11.5f - edge.magnitude));
            }
            panelTexture.SetPixels(pixels); panelTexture.Apply(false, true);
            return Sprite.Create(panelTexture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(14f, 14f, 14f, 14f));
        }

        Button Button(Transform parent, string title, Vector2 at, Vector2 size, Action action)
        {
            var rect = Rect(parent, title, at, size);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.24f, 0.25f, 0.30f);
            image.sprite = roundedPanel; image.type = Image.Type.Sliced;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            Text(rect, title, Vector2.zero, size - new Vector2(24, 18), 27, Cream);
            return button;
        }
    }
}
