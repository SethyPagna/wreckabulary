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
        static readonly Color Cream = new Color(0.97f, 0.94f, 0.86f);
        static readonly Color Ink = new Color(0.18f, 0.23f, 0.23f);
        static readonly Color Teal = new Color(0.13f, 0.48f, 0.48f);
        Canvas canvas;
        RectTransform safe;
        TextMeshProUGUI mapLabel, lookLabel;
        TextMeshProUGUI portraitMapLabel, portraitLookLabel;
        string[] mapIds;
        int mapIndex, preset, colourIndex, skinIndex;
        Outfit outfit;
        bool tucked;
        GameObject sheet;
        GameObject portraitSheet;
        RectTransform portraitContent;
        Button menuToggle, portraitFirst;
        Button classicFinish, portraitFinish, classicSound, portraitSound;
        ScrollRect portraitScroll;
        GameObject lastMenuSelection;
        bool portrait;
        readonly System.Collections.Generic.List<(TextMeshProUGUI label,float size)> portraitText = new();
        readonly System.Collections.Generic.List<(RectTransform rect,float height)> portraitRows = new();
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
            outfit = GameConfig.Current.Wardrobe.Default.Clone();
            string saved = PlayerPrefs.GetString("wv.outfit.0", "");
            if (!string.IsNullOrEmpty(saved))
            {
                var candidate = Outfit.Deserialize(saved);
                if (GameConfig.Current.Wardrobe.Problems(candidate).Count == 0) outfit = candidate;
            }
            preset = outfit.PieceIn("Face") == "Glasses" ? 2 : outfit.PieceIn("Headwear") == "Cap" ? 1 : 0;
            var topPalette = GameConfig.Current.Wardrobe.Palettes["Top"];
            colourIndex = Math.Max(0, topPalette.FindIndex(c => c.Id == outfit.ColourOf("Top")));
            skinIndex = Math.Max(0, Array.IndexOf(SkinNames, outfit.ItemSkins.Values.FirstOrDefault()));
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
            sheet.AddComponent<Image>().color = new Color(Cream.r, Cream.g, Cream.b, 0.97f);
            Text(sheet.transform, "WRECKABULARY", new Vector2(0, 350), new Vector2(1580, 110), 82, Ink);
            Text(sheet.transform, "MAKE WORDS. MAKE A MESS. MAKE YOURSELF AT HOME.", new Vector2(0, 266), new Vector2(1560, 54), 28, Teal);
            Text(sheet.transform, "Pick your kind of chaos", new Vector2(-385, 178), new Vector2(720, 55), 36, Ink);
            Mode("Dibs", "A friendly scrap. Last roommate standing wins.", "Dibs", -560, 70);
            Mode("Duos", "Two teams, shared trouble. Revive your buddy.", "Duos", -190, 70);
            Mode("Moving Day", "Spell the furniture and put everything in its room.", "MovingDay", -560, -108);
            Mode("Moving Out", "Rescue the keepsakes before the house clears out.", "MovingOut", -190, -108);
            Text(sheet.transform, "Your roommate", new Vector2(445, 178), new Vector2(590, 55), 36, Ink);
            lookLabel = Text(sheet.transform, "", new Vector2(445, 100), new Vector2(590, 60), 30, Teal);
            Button(sheet.transform, "Change outfit", new Vector2(445, 20), new Vector2(520, 64), () => { preset = (preset + 1) % 3; SetLook(); });
            Button(sheet.transform, "Change colour", new Vector2(445, -62), new Vector2(520, 64), () => { colourIndex++; SetLook(); });
            classicFinish = Button(sheet.transform, "Item finish: " + SkinNames[skinIndex], new Vector2(445, -144), new Vector2(520, 64), () =>
            {
                skinIndex = (skinIndex + 1) % SkinNames.Length;
                foreach (var item in GameConfig.Current.Items.Enabled) outfit.ItemSkins[item.Id] = SkinNames[skinIndex];
                SaveLook(); RefreshLabels();
            });
            Button(sheet.transform, "Play & learn  /  No pressure, just wordplay", new Vector2(-380, -216), new Vector2(720, 56), () =>
            {
                EnsureRoommate(); SaveLook(); Session.LoadMode("Tutorial");
            });
            Button(sheet.transform, "Creative Workshop  /  Build your cozy home", new Vector2(445, -216), new Vector2(520, 56), () =>
            {
                SaveLook();
                if (!Session.OpenWorkshop(mapIds[mapIndex], out var error)) lookLabel.text = error;
            });
            mapLabel = Text(sheet.transform, "", new Vector2(-370, -282), new Vector2(720, 68), 32, Ink);
            Button(sheet.transform, "Change house", new Vector2(260, -282), new Vector2(330, 65), () =>
            {
                mapIndex = (mapIndex + 1) % mapIds.Length;
                Session.SelectMap(mapIds[mapIndex]); RefreshLabels();
            });
            Button(sheet.transform, "Explore the house", new Vector2(600, -282), new Vector2(310, 65), () =>
            {
                EnsureRoommate(); tucked = true; ShowSheets();
            });
            var toggle = Button(safe, "Play & wardrobe", Vector2.zero, new Vector2(330, 64), () =>
            { tucked = !tucked; ShowSheets(); });
            menuToggle = toggle;
            var toggleRect = (RectTransform)toggle.transform;
            toggleRect.anchorMin = toggleRect.anchorMax = toggleRect.pivot = new Vector2(0, 1);
            toggleRect.anchoredPosition = new Vector2(18, -18);
            classicSound = Button(sheet.transform, GameFeedback.Muted ? "Sound off" : "Sound on", new Vector2(645, 355), new Vector2(230, 52), () =>
            {
                GameFeedback.Muted = !GameFeedback.Muted;
                RefreshLabels();
            });
            Text(sheet.transform, "Smash furniture • collect letters • spell gear • use it!", new Vector2(0, -390), new Vector2(1560, 54), 25, Ink);
            BuildPortrait();
            RefreshLabels();
            // A newly created overlay has no selected control until the pointer clicks it.
            // Select the first mode explicitly so controller navigation starts immediately.
            var first = sheet.GetComponentInChildren<Button>();
            if (EventSystem.current && first) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        void Update()
        {
            if (!safe) return;
            var area = Screen.safeArea;
            safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            // Fit the postcard inside both axes, including portrait phones and short ultrawide windows.
            bool nextPortrait = safe.rect.width < safe.rect.height;
            bool changed = portrait != nextPortrait; portrait = nextPortrait;
            if (sheet && !portrait)
            {
                float fit = Mathf.Min(1f, Mathf.Max(0.1f, (safe.rect.width - 32f) / 1700f),
                    Mathf.Max(0.1f, (safe.rect.height - 32f) / 920f));
                sheet.transform.localScale = Vector3.one * fit;
            }
            if (portraitSheet)
            {
                float unit = Mathf.Max(1f,Screen.dpi/160f)/Mathf.Max(.1f,canvas.scaleFactor);
                foreach (var row in portraitRows) row.rect.GetComponent<LayoutElement>().preferredHeight = row.height*unit;
                foreach (var text in portraitText) text.label.fontSize = text.size*unit;
                var layout = portraitContent.GetComponent<VerticalLayoutGroup>();
                layout.spacing = 12*unit; layout.padding = new RectOffset(16,16,16,16);
                var rect = (RectTransform)portraitSheet.transform; rect.offsetMin = Vector2.zero; rect.offsetMax = new Vector2(0,-(48*unit+32));
                if (portrait)
                {
                    var toggleRect = (RectTransform)menuToggle.transform; toggleRect.sizeDelta = new Vector2(Mathf.Min(safe.rect.width-36,240*unit),48*unit);
                    StretchLabel(menuToggle);
                }
                else ((RectTransform)menuToggle.transform).sizeDelta = new Vector2(330,64);
            }
            ShowSheets();
            if (changed && !tucked && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(portrait ? portraitFirst.gameObject : sheet.GetComponentInChildren<Button>().gameObject);
            var selected=EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != lastMenuSelection)
            {
                lastMenuSelection=selected;
                if (portrait && selected && selected.transform.IsChildOf(portraitContent))
                {
                    var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(portraitScroll.viewport,selected.transform);
                    var position=portraitContent.anchoredPosition;
                    if (bounds.min.y<portraitScroll.viewport.rect.yMin) position.y+=portraitScroll.viewport.rect.yMin-bounds.min.y;
                    else if (bounds.max.y>portraitScroll.viewport.rect.yMax) position.y-=bounds.max.y-portraitScroll.viewport.rect.yMax;
                    position.y=Mathf.Clamp(position.y,0,Mathf.Max(0,portraitContent.rect.height-portraitScroll.viewport.rect.height));
                    portraitContent.anchoredPosition=position;
                }
            }
        }

        void ShowSheets()
        { if (sheet) sheet.SetActive(!tucked && !portrait); if (portraitSheet) portraitSheet.SetActive(!tucked && portrait); }

        void BuildPortrait()
        {
            var panel = Rect(safe,"Portrait welcome",Vector2.zero,Vector2.zero); panel.anchorMin = Vector2.zero; panel.anchorMax = Vector2.one; panel.offsetMin = panel.offsetMax = Vector2.zero;
            portraitSheet = panel.gameObject; panel.gameObject.AddComponent<Image>().color = Cream;
            var scroll = panel.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            portraitScroll=scroll;
            var viewport = Rect(panel,"Viewport",Vector2.zero,Vector2.zero); viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one; viewport.offsetMin = viewport.offsetMax = Vector2.zero;
            viewport.gameObject.AddComponent<Image>().color = Cream; viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            portraitContent = Rect(viewport,"Welcome choices",Vector2.zero,Vector2.zero); portraitContent.anchorMin = new Vector2(0,1); portraitContent.anchorMax = Vector2.one; portraitContent.pivot = new Vector2(.5f,1);
            var layout = portraitContent.gameObject.AddComponent<VerticalLayoutGroup>(); layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            portraitContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport; scroll.content = portraitContent;
            PortraitLabel("WRECKABULARY",32,48);
            PortraitLabel("Make words. Make a mess. Make yourself at home.",17,48);
            portraitFirst = PortraitMode("Dibs", "Last roommate standing wins.", "Dibs");
            PortraitMode("Duos", "Two teams. Revive your buddy.", "Duos");
            PortraitMode("Moving Day", "Put spelled furniture in its room.", "MovingDay");
            PortraitMode("Moving Out", "Rescue keepsakes before clear-out.", "MovingOut");
            PortraitButton("Play & learn",() => { EnsureRoommate(); SaveLook(); Session.LoadMode("Tutorial"); });
            PortraitButton("Creative Workshop",() => { SaveLook(); if (!Session.OpenWorkshop(mapIds[mapIndex],out var error)) portraitLookLabel.text = error; });
            portraitMapLabel = PortraitLabel("",20,58);
            PortraitButton("Change house",() => { mapIndex=(mapIndex+1)%mapIds.Length; Session.SelectMap(mapIds[mapIndex]); RefreshLabels(); });
            portraitLookLabel = PortraitLabel("",20,58);
            PortraitButton("Change outfit",() => { preset=(preset+1)%3; SetLook(); });
            PortraitButton("Change colour",() => { colourIndex++; SetLook(); });
            portraitFinish = PortraitButton("Item finish: "+SkinNames[skinIndex],() =>
            {
                skinIndex=(skinIndex+1)%SkinNames.Length;
                foreach (var item in GameConfig.Current.Items.Enabled) outfit.ItemSkins[item.Id]=SkinNames[skinIndex];
                SaveLook(); RefreshLabels();
            });
            PortraitButton("Explore the house",() => { EnsureRoommate(); tucked=true; ShowSheets(); });
            portraitSound = PortraitButton(GameFeedback.Muted ? "Sound off" : "Sound on",() =>
            { GameFeedback.Muted=!GameFeedback.Muted; RefreshLabels(); });
            PortraitLabel("Smash furniture · collect letters · spell gear · use it!",17,58);
            portraitSheet.SetActive(false);
        }
        TextMeshProUGUI PortraitLabel(string text,float font,float height)
        {
            var label = Text(portraitContent,text,Vector2.zero,new Vector2(800,height),font,Ink);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            portraitRows.Add((label.rectTransform,height)); portraitText.Add((label,font)); return label;
        }
        Button PortraitButton(string title,Action action,float height=48)
        {
            var button = Button(portraitContent,title,Vector2.zero,new Vector2(800,height),action);
            button.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            portraitRows.Add(((RectTransform)button.transform,height));
            portraitText.Add((button.GetComponentInChildren<TextMeshProUGUI>(),20)); StretchLabel(button); return button;
        }
        Button PortraitMode(string title,string description,string mode)
        {
            var button = PortraitButton(title+"\n<size=65%>"+description+"</size>",() => { EnsureRoommate(); SaveLook(); Session.LoadMode(mode,mapIds[mapIndex]); },80);
            button.GetComponent<Image>().color=Teal; button.GetComponentInChildren<TextMeshProUGUI>().color=Cream; return button;
        }
        static void StretchLabel(Button button)
        {
            var rect = button.GetComponentInChildren<TextMeshProUGUI>().rectTransform; rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one;
            rect.offsetMin=new Vector2(12,6); rect.offsetMax=new Vector2(-12,-6);
        }

        void Mode(string title, string description, string id, float x, float y)
        {
            var button = Button(sheet.transform, title + "\n<size=52%>" + description + "</size>",
                new Vector2(x, y), new Vector2(350, 155), () =>
                {
                    EnsureRoommate(); SaveLook(); Session.LoadMode(id, mapIds[mapIndex]);
                });
            button.GetComponent<Image>().color = Teal;
            button.GetComponentInChildren<TextMeshProUGUI>().color = Cream;
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

        void SetLook()
        {
            var wardrobe = GameConfig.Current.Wardrobe;
            outfit = wardrobe.Default.Clone();   // Neighbour: the default hoodie, hood up and satchel
            if (preset > 0) outfit.Pieces["Top"] = "Hoodie";
            if (preset == 1) { outfit.Pieces["Headwear"] = "Cap"; outfit.Pieces["Back"] = "Satchel"; }
            if (preset == 2) { outfit.Pieces["Headwear"] = "Hood"; outfit.Pieces["Face"] = "Glasses"; outfit.Pieces.Remove("Back"); }
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
                (GameConfig.Current.Wardrobe.Colour("Top", outfit.ColourOf("Top"))?.Name ?? "Tomato");
            if (portraitMapLabel) portraitMapLabel.text = mapLabel.text;
            if (portraitLookLabel) portraitLookLabel.text = lookLabel.text;
            foreach (var button in new[] {classicFinish,portraitFinish}) if (button) button.GetComponentInChildren<TextMeshProUGUI>().text="Item finish: "+SkinNames[skinIndex];
            foreach (var button in new[] {classicSound,portraitSound}) if (button) button.GetComponentInChildren<TextMeshProUGUI>().text=GameFeedback.Muted ? "Sound off" : "Sound on";
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

        static Button Button(Transform parent, string title, Vector2 at, Vector2 size, Action action)
        {
            var rect = Rect(parent, title, at, size);
            var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0.84f, 0.89f, 0.82f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.onClick.AddListener(() => action());
            Text(rect, title, Vector2.zero, size - new Vector2(24, 18), 31, Ink);
            return button;
        }
    }
}
