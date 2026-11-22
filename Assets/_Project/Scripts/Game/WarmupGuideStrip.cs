using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class WarmupGuideStrip : MonoBehaviour
    {
        public const string RootName = "Warm-up strip";
        public const float MinFont = 15f;
        const float Width = 520f, MinCornerWidth = 460f, Height = 212f, Pad = 14f, Margin = 38f, TopGap = 12f, NarrowGutter = 76f;
        const float CoverSlack = 40f, CornerReturnSeconds = 1f;
        const float TrayHalfWidth = 250f, TrayGap = 20f;
        const float HeaderHeight = 34f, HeaderGap = 8f, TitleWidth = 132f, ClockWidth = 74f, ClockGap = 4f, ReadyGap = 8f;
        const float PictureHeight = 118f, PictureWidth = PictureHeight, PictureGap = 14f;
        const float CardTitleHeight = 28f, CardLineGap = 2f, CardLineHeight = 72f;
        const float DotSize = 10f, DotWide = 26f, DotGap = 6f, TipHeight = 22f, TipBottom = 10f;
        const float TitleFont = 22f, ClockFont = 26f, ReadyFont = 18f, CardTitleFont = 21f, CardLineFont = 18f, TipFont = 16f;
        const float ArtShare = .7f, ArtWordShare = .5f;
        const int PanelRadius = 18, PictureRadius = 10, DotRadius = 5;
        static readonly Color Fill = Hex(0x173b3c, .94f), Edge = Hex(0xcadac6, .33f), Ink = Hex(0xfff1d8), Muted = Hex(0xfff1d8, .78f);
        static readonly Color Slot = Hex(0xffffff, .08f), DotOff = Hex(0xfff1d8, .3f), Accent = Hex(0xffd46a);

        GameHud hud;
        ModeGuide guide;
        RectTransform root, picture, fallback, dots;
        Image photo;
        TextMeshProUGUI title, clock, ready, cardTitle, cardLine, tip;
        readonly List<Image> dotImages = new();
        int shownCard = -1, shownSeconds = int.MinValue, shownReady = -1, shownVoters = -1;
        string shownKey;
        bool keyShown, laidCorner, closed, pictured, covered;
        float laidWidth = -1f, laidInset = -1f, clearSince = -10f;

        public bool Built => root;
        public bool Shown => root && root.gameObject.activeInHierarchy;
        public RectTransform Root => root;
        public string Mode => guide?.Mode;
        public int Card => shownCard;
        public int CardCount => guide?.Steps.Count ?? 0;
        public string TitleText => title ? title.text : "";
        public string ClockText => clock ? clock.text : "";
        public string ReadyText => ready ? ready.text : "";
        public string TipText => tip ? tip.text : "";
        public string CardTitleText => cardTitle ? cardTitle.text : "";
        public string CardLineText => cardLine ? cardLine.text : "";
        public bool PictureShown => pictured;
        public bool InCorner => laidCorner;
        public bool MovedForPlayer => covered;

        public static WarmupGuideStrip Attach(GameObject host, GameHud display, string modeId)
        {
            var book = ModeGuideBook.For(modeId);
            if (!host || !display || book == null || book.Steps.Count == 0) return null;
            var strip = host.AddComponent<WarmupGuideStrip>();
            strip.hud = display;
            strip.guide = book;
            return strip;
        }

        public void Show(Warmup state, string startKey)
        {
            if (!root && !closed) Build();
            if (!root || state == null) return;
            int seconds = Mathf.CeilToInt(state.TimeLeft);
            if (seconds != shownSeconds)
            {
                shownSeconds = seconds;
                clock.text = state.Clock;
            }
            if (!keyShown || state.ReadyCount != shownReady || state.Voters != shownVoters || startKey != shownKey)
            {
                keyShown = true; shownReady = state.ReadyCount; shownVoters = state.Voters; shownKey = startKey;
                ready.text = state.ReadyLine(startKey);
            }
            int card = state.Card(guide.Steps.Count);
            if (card != shownCard) Turn(card);
        }

        public void Close()
        {
            closed = true;
            DropRoot();
            Destroy(this);
        }

        void DropRoot()
        {
            if (!root) return;
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
            root = null;
        }

        void OnDestroy() => DropRoot();

        void LateUpdate()
        {
            if (!root) return;
            bool show = hud && !hud.BagOpen && !hud.ResultShown;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (show) Place();
        }

        void Build()
        {
            var canvas = hud && hud.UiCanvas ? hud.UiCanvas.transform : null;
            if (!canvas) return;
            var safe = canvas.Find("Safe HUD");
            root = Box(RootName, safe ? safe : canvas, LobbyIcons.RoundedSprite(PanelRadius), Fill);
            root.SetAsFirstSibling();
            var quiet = root.gameObject.AddComponent<CanvasGroup>();
            quiet.blocksRaycasts = quiet.interactable = false;
            Stretch(Box("Edge", root, LobbyIcons.FrameSprite(PanelRadius, 1), Edge), 0f, 0f, 0f, 0f);
            BuildHeader();
            BuildCard();
            tip = Label("Tip", root, LobbyFonts.Body, TipFont, Muted, TextAlignmentOptions.MidlineLeft);
            Row(tip.rectTransform, Pad, Pad, TipBottom, TipHeight, false);
            tip.text = Warmup.Tip;
            Place();
        }

        void BuildHeader()
        {
            title = Label("Title", root, LobbyFonts.Black, TitleFont, Ink, TextAlignmentOptions.MidlineLeft);
            Corner(title.rectTransform, Pad, Pad, TitleWidth, HeaderHeight);
            title.text = Warmup.Title;
            clock = Label("Clock", root, LobbyFonts.Display, ClockFont, Accent, TextAlignmentOptions.MidlineLeft);
            Corner(clock.rectTransform, Pad + TitleWidth + ClockGap, Pad, ClockWidth, HeaderHeight);
            ready = Label("Ready", root, LobbyFonts.Body, ReadyFont, Ink, TextAlignmentOptions.MidlineRight);
            Row(ready.rectTransform, Pad + TitleWidth + ClockGap + ClockWidth + ReadyGap, Pad, Pad, HeaderHeight, true);
        }

        void BuildCard()
        {
            float top = Pad + HeaderHeight + HeaderGap, left = Pad + PictureWidth + PictureGap;
            picture = Box("Picture", root, LobbyIcons.RoundedSprite(PictureRadius), Slot);
            Corner(picture, Pad, top, PictureWidth, PictureHeight);
            fallback = Box("Fallback", picture, LobbyIcons.RoundedSprite(PictureRadius), LobbyKit.ModeColour(guide.Mode));
            Stretch(fallback, 0f, 0f, 0f, 0f);
            var shot = new GameObject("Photo", typeof(RectTransform)).GetComponent<RectTransform>();
            shot.SetParent(picture, false);
            Stretch(shot, 0f, 0f, 0f, 0f);
            photo = shot.gameObject.AddComponent<Image>();
            photo.preserveAspect = true; photo.raycastTarget = false;
            cardTitle = Label("Card title", root, LobbyFonts.Black, CardTitleFont, Ink, TextAlignmentOptions.MidlineLeft);
            Row(cardTitle.rectTransform, left, Pad, top, CardTitleHeight, true);
            cardLine = Label("Card line", root, LobbyFonts.Body, CardLineFont, Muted, TextAlignmentOptions.TopLeft);
            cardLine.textWrappingMode = TextWrappingModes.Normal;
            Row(cardLine.rectTransform, left, Pad, top + CardTitleHeight + CardLineGap, CardLineHeight, true);
            dots = new GameObject("Dots", typeof(RectTransform)).GetComponent<RectTransform>();
            dots.SetParent(root, false);
            Corner(dots, left, top + PictureHeight - DotSize, DotWide + (guide.Steps.Count - 1) * (DotSize + DotGap), DotSize);
            for (int i = 0; i < guide.Steps.Count; i++)
            {
                var dot = Box("Dot " + (i + 1), dots, LobbyIcons.RoundedSprite(DotRadius), DotOff);
                dot.anchorMin = dot.anchorMax = dot.pivot = new Vector2(0f, .5f);
                dotImages.Add(dot.GetComponent<Image>());
            }
        }

        void Turn(int index)
        {
            shownCard = index;
            var step = guide.Steps[index];
            cardTitle.text = step.Title;
            cardLine.text = ModeGuides.Line(step, ModeGuides.Fallback());
            photo.sprite = null;
            photo.enabled = false;
            fallback.gameObject.SetActive(true);
            ShowArt(step.Art);
            float x = 0f;
            for (int i = 0; i < dotImages.Count; i++)
            {
                bool on = i == index;
                float wide = on ? DotWide : DotSize;
                dotImages[i].color = on ? Accent : DotOff;
                dotImages[i].rectTransform.anchoredPosition = new Vector2(x, 0f);
                dotImages[i].rectTransform.sizeDelta = new Vector2(wide, DotSize);
                x += wide + DotGap;
            }
        }

        void ShowArt(string art)
        {
            ModeGuides.Art(fallback, guide.Mode, art, PictureHeight * ArtShare);
            pictured = fallback.GetComponentInChildren<RawImage>(true);
            foreach (var graphic in fallback.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            foreach (var word in fallback.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                word.enableAutoSizing = true; word.fontSizeMin = MinFont; word.fontSizeMax = PictureHeight * ArtWordShare;
            }
        }

        void Place()
        {
            var area = ((RectTransform)root.parent).rect;
            float room = area.width * .5f - TrayHalfWidth - TrayGap - Margin;
            bool fits = !hud.TouchControlsShown && area.width >= area.height && room >= MinCornerWidth;
            covered = fits && PlayerUnderCorner(Mathf.Min(Width, room));
            bool corner = fits && !covered;
            float width = Mathf.Min(Width, corner ? room : area.width - NarrowGutter);
            float inset = corner ? 0f : hud.TopInset;
            if (corner == laidCorner && Mathf.Approximately(width, laidWidth) && Mathf.Approximately(inset, laidInset)) return;
            laidCorner = corner; laidWidth = width; laidInset = inset;
            root.anchorMin = root.anchorMax = root.pivot = corner ? new Vector2(1f, 0f) : new Vector2(.5f, 1f);
            root.anchoredPosition = corner ? new Vector2(-Margin, Margin) : new Vector2(0f, -TopGap - inset);
            root.sizeDelta = new Vector2(width, Height);
        }

        bool PlayerUnderCorner(float width)
        {
            bool under = SomeoneUnder(width);
            if (under) { clearSince = -10f; return true; }
            if (!covered) return false;
            if (clearSince < 0f) clearSince = Time.unscaledTime;
            return Time.unscaledTime - clearSince < CornerReturnSeconds;
        }

        bool SomeoneUnder(float width)
        {
            var lens = Camera.main;
            var parent = (RectTransform)root.parent;
            if (!lens || !parent) return false;
            var area = parent.rect;
            var spot = Rect.MinMaxRect(area.xMax - Margin - width - CoverSlack, area.yMin, area.xMax, area.yMin + Margin + Height + CoverSlack);
            var canvas = parent.GetComponentInParent<Canvas>();
            var eye = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            foreach (var p in World.Players)
            {
                if (!p || p.Binding == null || p.Binding is BotBinding) continue;
                var screen = lens.WorldToScreenPoint(p.transform.position);
                if (screen.z <= 0f) continue;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, eye, out var local) && spot.Contains(local)) return true;
            }
            return false;
        }

        static Color Hex(int rgb, float a = 1f) => new(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, a);

        static RectTransform Box(string label, Transform parent, Sprite sprite, Color colour)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.type = Image.Type.Sliced; image.color = colour; image.raycastTarget = false;
            return rt;
        }

        static TextMeshProUGUI Label(string label, Transform parent, TMP_FontAsset font, float size, Color colour, TextAlignmentOptions alignment)
        {
            var rt = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            text.font = font ? font : GameAssets.I ? GameAssets.I.font : TMP_Settings.defaultFontAsset;
            text.color = colour; text.alignment = alignment; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            text.fontSize = size;
            text.enableAutoSizing = true; text.fontSizeMin = Mathf.Min(MinFont, size); text.fontSizeMax = size;
            return text;
        }

        static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom); rt.offsetMax = new Vector2(-right, -top);
        }

        static void Corner(RectTransform rt, float left, float top, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(left, -top);
            rt.sizeDelta = new Vector2(width, height);
        }

        static void Row(RectTransform rt, float left, float right, float edge, float height, bool fromTop)
        {
            float y = fromTop ? 1f : 0f;
            rt.anchorMin = new Vector2(0f, y); rt.anchorMax = new Vector2(1f, y); rt.pivot = new Vector2(.5f, y);
            rt.offsetMin = new Vector2(left, fromTop ? -edge - height : edge);
            rt.offsetMax = new Vector2(-right, fromTop ? -edge : edge + height);
        }
    }
}
