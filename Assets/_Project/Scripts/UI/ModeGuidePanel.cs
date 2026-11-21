using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public static class ModeGuides
    {
        public const string Folder = "ModeGuides";
        static ModeGuidePanel current;
        static readonly Dictionary<string, Sprite> pictures = new Dictionary<string, Sprite>();

        public static bool Has(string modeId) => ModeGuideBook.Has(modeId);

        public static bool Open => current && current.Showing;

        public static string Mode => Open ? current.Mode : null;

        public static ModeGuidePanel Show(string modeId) => Show(modeId, null);

        public static ModeGuidePanel Show(string modeId, InputBinding viewer, Action closed = null)
        {
            var guide = ModeGuideBook.For(modeId);
            if (guide == null) return null;
            var device = viewer != null ? ControlHints.Device(viewer) : ModeGuidePanel.Pressed(null) ?? Fallback();
            Close();
            return current = ModeGuidePanel.Create(guide, device, closed);
        }

        public static void Close()
        {
            var open = current;
            current = null;
            if (open) open.Close();
        }

        public static Sprite Picture(string image)
        {
            if (string.IsNullOrEmpty(image)) return null;
            if (pictures.TryGetValue(image, out var sprite) && sprite) return sprite;
            sprite = Resources.Load<Sprite>(Folder + "/" + image);
            if (sprite) pictures[image] = sprite; else pictures.Remove(image);
            return sprite;
        }

        internal static void Forget(ModeGuidePanel panel)
        {
            if (current == panel) current = null;
        }

        public static string Eyebrow(string modeId) => ModeGuideBook.Eyebrow(modeId, LobbyPlayModeLabels.Name(modeId));

        internal static string Line(ModeGuideStep step, ControlDevice device) =>
            ModeGuideBook.Fill(step.Line, device, control => ControlHints.For(device, control));

        internal static void Art(RectTransform slot, string mode, string art, float size)
        {
            LobbyKit.Clear(slot);
            var image = LobbyKit.ItemImage(slot, art, size) ?? LobbyKit.ItemImage(slot, LobbyKit.ModeArt(mode), size);
            if (image) return;
            var word = LobbyKit.Display(slot, art, 120, LobbyKit.Cream);
            word.rectTransform.Fill();
        }

        internal static ControlDevice Fallback()
        {
            var hud = GameHud.Active;
            if (hud && hud.LocalPlayer && SceneManager.GetActiveScene().name != Session.HubScene) return ControlHints.Device(hud.LocalPlayer.Binding);
            if (Session.Bindings.Count > 0) return ControlHints.Device(Session.Bindings[0]);
            return Application.isMobilePlatform ? ControlDevice.Touch : ControlDevice.MouseKeyboard;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget()
        {
            current = null;
            pictures.Clear();
        }
    }

    [DefaultExecutionOrder(100)]
    public sealed class ModeGuidePanel : MonoBehaviour
    {
        public const string RootName = "Mode guide", EyebrowName = "Guide eyebrow";
        const int SortingOrder = 600;
        const float EyebrowFont = 20f, EyebrowMinFont = 15f;
        const float CardWidth = 1280f, CardHeight = 1000f, Header = 84f, Chrome = 344f, Stick = .6f;

        ModeGuide guide;
        ControlDevice device;
        Action closed;
        int page, stickHeld;
        bool closing;
        GameObject returnFocus;
        RectTransform canvasRect, card, picture, fallback, dots, titleRect, lineRect;
        Image photo;
        TextMeshProUGUI title, line, nextLabel, eyebrow;
        Button back, next;
        readonly List<Image> dotImages = new List<Image>();
        Vector2 laidOut;
        string fallbackArt = "";

        public string Mode => guide?.Mode;
        public int Page => page;
        public int PageCount => guide?.Steps.Count ?? 0;
        public ControlDevice Device => device;
        public bool Showing => this && !closing && gameObject.activeInHierarchy;
        public string TitleText => title ? title.text : "";
        public string LineText => line ? line.text : "";
        public string EyebrowText => eyebrow ? eyebrow.text : "";
        public bool PictureShown => photo && photo.enabled && photo.sprite;
        public string FallbackArt => fallback && fallback.gameObject.activeSelf ? fallbackArt : "";
        public int ActiveDot => dotImages.FindIndex(d => d.TryGetComponent(out LayoutElement size) && size.preferredWidth > 20f);
        public Func<bool> KeepWhile { get; set; }

        internal static ModeGuidePanel Create(ModeGuide guide, ControlDevice device, Action closed)
        {
            var root = new GameObject(RootName, typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f;
            root.AddComponent<GraphicRaycaster>();
            var panel = root.AddComponent<ModeGuidePanel>();
            panel.Build(guide, device, closed);
            return panel;
        }

        void Build(ModeGuide shown, ControlDevice viewer, Action onClosed)
        {
            guide = shown; device = viewer; closed = onClosed;
            returnFocus = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            canvasRect = (RectTransform)transform;

            var shade = LobbyKit.Rect(transform, "Shade").Fill();
            shade.Paint(LobbyKit.Shade).raycastTarget = true;
            Quiet(shade.gameObject.AddComponent<Button>()).onClick.AddListener(Close);

            card = LobbyKit.Rect(transform, "Card");
            card.anchorMin = card.anchorMax = card.pivot = new Vector2(.5f, .5f);
            LobbyKit.PanelFace(card, 28).raycastTarget = true;

            eyebrow = LobbyKit.Caps(card, ModeGuides.Eyebrow(guide.Mode), EyebrowFont);
            eyebrow.name = EyebrowName;
            eyebrow.enableAutoSizing = true; eyebrow.fontSizeMin = EyebrowMinFont; eyebrow.fontSizeMax = EyebrowFont;
            eyebrow.rectTransform.Place(new Vector2(0, 1), Vector2.one, new Vector2(36, -64), new Vector2(-104, -22));

            var close = LobbyKit.IconButton(card, LobbyIcons.Close, "Close", Close);
            close.name = "Guide close";
            if (close.TryGetComponent(out LobbyHint hint)) Destroy(hint);
            Quiet(close);
            ((RectTransform)close.transform).Pin(Vector2.one, new Vector2(-22, -18), new Vector2(58, 58));

            picture = LobbyKit.Rect(card, "Picture");
            LobbyKit.Face(picture, LobbyKit.Raised, 18, LobbyKit.Line, 2);
            var touch = LobbyKit.Rect(picture, "Swipe").Fill();
            touch.Paint(Color.clear).raycastTarget = true;
            touch.gameObject.AddComponent<ModeGuideSwipe>().Swiped = step => GoTo(page + step);
            fallback = LobbyKit.Rect(picture, "Fallback").Place(Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
            LobbyKit.Face(fallback, LobbyKit.ModeColour(guide.Mode), 14);
            var shot = LobbyKit.Rect(picture, "Photo").Place(Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
            photo = shot.gameObject.AddComponent<Image>();
            photo.preserveAspect = true; photo.raycastTarget = false;

            title = LobbyKit.Display(card, "", 46, LobbyKit.Navy);
            title.name = "Guide title";
            title.enableAutoSizing = true; title.fontSizeMin = 30; title.fontSizeMax = 46;
            titleRect = title.rectTransform;

            line = LobbyKit.Text(card, "", 28, LobbyKit.Muted, TextAlignmentOptions.Top);
            line.name = "Guide line";
            line.textWrappingMode = TextWrappingModes.Normal;
            line.overflowMode = TextOverflowModes.Ellipsis;
            lineRect = line.rectTransform;

            back = Quiet(LobbyKit.Pill(card, "Guide back", "Back", 24, Back));
            ((RectTransform)back.transform).Pin(Vector2.zero, new Vector2(32, 28), new Vector2(180, 64));
            next = Quiet(LobbyKit.Primary(card, "Guide next", "Next", Next, 28, 24));
            ((RectTransform)next.transform).Pin(new Vector2(1, 0), new Vector2(-32, 28), new Vector2(250, 68));
            nextLabel = next.GetComponentInChildren<TextMeshProUGUI>(true);

            dots = LobbyKit.Row(card, "Dots", 12);
            dots.Pin(new Vector2(.5f, 0), new Vector2(0, 50), new Vector2(360, 24));
            var row = dots.GetComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childForceExpandHeight = false;
            for (int i = 0; i < guide.Steps.Count; i++)
            {
                var dot = LobbyKit.Rect(dots, "Dot " + (i + 1)).Paint(LobbyKit.Line, 7);
                dot.raycastTarget = false;
                dotImages.Add(dot);
            }

            Layout(canvasRect.rect.size);
            GoTo(0);
            LobbyPop.On(card).Play(new Vector2(0, -28));
            Focus();
        }

        static Button Quiet(Button button)
        {
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        void Layout(Vector2 size)
        {
            laidOut = size;
            float width = Mathf.Max(480f, Mathf.Min(CardWidth, size.x - 48f)), height = Mathf.Max(520f, Mathf.Min(CardHeight, size.y - 40f));
            card.sizeDelta = new Vector2(width, height);
            float pictureWidth = Mathf.Max(200f, Mathf.Min(width - 72f, (height - Chrome) * 16f / 9f)), pictureHeight = pictureWidth * 9f / 16f;
            picture.Pin(new Vector2(.5f, 1), new Vector2(0, -Header), new Vector2(pictureWidth, pictureHeight));
            float below = Header + pictureHeight + 18f;
            titleRect.Pin(new Vector2(.5f, 1), new Vector2(0, -below), new Vector2(width - 96f, 58f));
            lineRect.Pin(new Vector2(.5f, 1), new Vector2(0, -below - 62f), new Vector2(width - 140f, 84f));
            float art = Mathf.Min(pictureHeight * .6f, 300f);
            foreach (Transform child in fallback)
                if (child.TryGetComponent(out RawImage raw)) ((RectTransform)child).sizeDelta = new Vector2(art, art);
        }

        public void Next()
        {
            if (closing) return;
            if (page >= PageCount - 1) Close();
            else GoTo(page + 1);
        }

        public void Back()
        {
            if (!closing && page > 0) GoTo(page - 1);
        }

        public void GoTo(int index)
        {
            if (closing || guide == null) return;
            page = Mathf.Clamp(index, 0, PageCount - 1);
            var step = guide.Steps[page];
            title.text = step.Title;
            line.text = ModeGuides.Line(step, device);
            var sprite = ModeGuides.Picture(step.Image);
            photo.sprite = sprite;
            photo.enabled = sprite;
            fallback.gameObject.SetActive(!sprite);
            if (!sprite) ShowArt(step.Art);
            back.gameObject.SetActive(page > 0);
            nextLabel.text = page >= PageCount - 1 ? "GOT IT" : "NEXT";
            for (int i = 0; i < dotImages.Count; i++)
            {
                bool on = i == page;
                dotImages[i].color = on ? LobbyKit.Sun : LobbyKit.Line;
                dotImages[i].Size(on ? 38f : 14f, 14f);
            }
        }

        void ShowArt(string art)
        {
            if (fallbackArt == art && fallback.childCount > 0) return;
            fallbackArt = art;
            ModeGuides.Art(fallback, guide.Mode, art, Mathf.Min(picture.sizeDelta.y * .6f, 300f));
        }

        void Focus()
        {
            var events = EventSystem.current;
            if (!events || !next) return;
            var selected = events.currentSelectedGameObject;
            if (!selected || !selected.activeInHierarchy || !selected.transform.IsChildOf(transform)) events.SetSelectedGameObject(next.gameObject);
        }

        void Update()
        {
            if (closing) return;
            if (KeepWhile != null && !KeepWhile()) { Close(); return; }
            var size = canvasRect.rect.size;
            if (size != laidOut) Layout(size);
            var pressed = Pressed(device);
            if (pressed.HasValue && pressed.Value != device)
            {
                device = pressed.Value;
                GoTo(page);
            }
            if (ClosePressed()) { Close(); return; }
            int step = Paging();
            if (step > 0) Next();
            else if (step < 0) Back();
            if (!closing) Focus();
        }

        internal static ControlDevice? Pressed(ControlDevice? current)
        {
            var screen = Touchscreen.current;
            if (screen != null && (screen.primaryTouch.press.wasPressedThisFrame || screen.primaryTouch.press.wasReleasedThisFrame)) return ControlDevice.Touch;
            foreach (var pad in Gamepad.all) if (PadPressed(pad)) return ControlDevice.Gamepad;
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            bool desk = (keys != null && keys.anyKey.wasPressedThisFrame) ||
                (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.leftButton.wasReleasedThisFrame));
            if (!desk) return null;
            return current is ControlDevice.KeysLeft or ControlDevice.KeysRight ? current : ControlDevice.MouseKeyboard;
        }

        static bool PadPressed(Gamepad pad) => pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame ||
            pad.buttonWest.wasPressedThisFrame || pad.buttonNorth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame ||
            pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame ||
            pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame;

        static bool ClosePressed()
        {
            var keys = Keyboard.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame) return true;
            foreach (var pad in Gamepad.all) if (pad.buttonEast.wasPressedThisFrame) return true;
            return false;
        }

        int Paging()
        {
            int step = 0;
            var keys = Keyboard.current;
            if (keys != null)
            {
                if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame) step = 1;
                else if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame) step = -1;
            }
            float stick = 0f;
            foreach (var pad in Gamepad.all)
            {
                if (pad.dpad.right.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame) step = 1;
                else if (pad.dpad.left.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame) step = -1;
                float x = pad.leftStick.x.ReadValue();
                if (Mathf.Abs(x) > Mathf.Abs(stick)) stick = x;
            }
            int direction = stick > Stick ? 1 : stick < -Stick ? -1 : 0;
            if (direction != 0 && direction != stickHeld && step == 0) step = direction;
            if (direction != 0) stickHeld = direction;
            else if (Mathf.Abs(stick) < .3f) stickHeld = 0;
            return step;
        }

        public void Close()
        {
            if (closing) return;
            closing = true;
            ModeGuides.Forget(this);
            gameObject.SetActive(false);
            Destroy(gameObject);
            var done = closed;
            closed = null;
            done?.Invoke();
            var events = EventSystem.current;
            if (!events) return;
            var selected = events.currentSelectedGameObject;
            if ((!selected || selected.transform.IsChildOf(transform)) && returnFocus && returnFocus.activeInHierarchy)
                events.SetSelectedGameObject(returnFocus);
        }

        void OnDestroy()
        {
            if (!closing) ModeGuides.Forget(this);
        }
    }

    public sealed class ModeGuideSwipe : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        Vector2 start;
        public Action<int> Swiped;

        public void OnBeginDrag(PointerEventData data) => start = data.position;

        public void OnDrag(PointerEventData data) { }

        public void OnEndDrag(PointerEventData data)
        {
            float moved = data.position.x - start.x;
            if (Mathf.Abs(moved) >= Mathf.Max(60f, Screen.width * .06f)) Swiped?.Invoke(moved < 0f ? 1 : -1);
        }
    }
}
