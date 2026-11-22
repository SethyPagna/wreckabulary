using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyPlayDetailEntry
    {
        public string Id = "", Title = "", Place = "", Who = "", Guide = "", Emblem = "";
        public Sprite Picture;
        public IReadOnlyList<string> Steps = Array.Empty<string>();
    }

    public sealed class LobbyPlayDetail : MonoBehaviour
    {
        public const string FrameName = "Detail frame", PictureName = "Detail picture", ScrimName = "Detail scrim", PlaceName = "Detail place";
        public const string EmblemName = "Detail emblem";
        public const string TitleName = "Detail title", WhoName = "Detail who", StepName = "Detail step ", GuideName = "Detail guide";
        public const string GuideWord = "How to play", StepSeparator = "  ";
        public const float Padding = 16, Gap = 12, StepGap = 8, TitleHeight = 40, WhoHeight = 28, StepHeight = 32, GuideHeight = 52;
        public const float PictureAspect = 16f / 9f, SmallestPicture = 90, PlaceInset = 12, PlaceLift = 8, ScrimShare = .5f, ScrimDepth = .8f, TintShare = .14f;
        public const float TitleSize = 28, PlaceSize = 24, WhoSize = 17, StepSize = 18, GuideSize = 20, SmallestLabel = 15;
        public const float EmblemSize = 160, EmblemFrom = .12f, EmblemTo = .88f;
        public const int MostSteps = 4, FrameRadius = 10, FaceRadius = 14, GuideRadius = 12;

        readonly Dictionary<string, LobbyPlayDetailEntry> entries = new Dictionary<string, LobbyPlayDetailEntry>(StringComparer.Ordinal);
        readonly List<TextMeshProUGUI> steps = new List<TextMeshProUGUI>();
        RectTransform frame, emblem;
        Image fill, picture;
        Mask mask;
        TextMeshProUGUI place, title, who;
        Button guide;
        LobbyPlayDetailEntry shown;
        Vector2 arranged = -Vector2.one;
        int arrangedSteps = -1;
        bool arrangedGuide, arrangedWho;

        public Action<string> Changed;

        public string Shown => shown?.Id;

        public Button Guide => guide;

        public static LobbyPlayDetail Create(Transform parent, string name)
        {
            var rect = LobbyKit.Rect(parent, name);
            LobbyKit.Face(rect, LobbyKit.Card, FaceRadius, LobbyKit.Line, 1, 0);
            var detail = rect.gameObject.AddComponent<LobbyPlayDetail>();
            detail.Build(rect);
            return detail;
        }

        void Build(RectTransform rect)
        {
            frame = LobbyKit.Rect(rect, FrameName);
            fill = frame.Paint(Color.white, FrameRadius);
            fill.raycastTarget = false;
            mask = frame.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            picture = LobbyKit.Rect(frame, PictureName).Fill().gameObject.AddComponent<Image>();
            picture.preserveAspect = true; picture.raycastTarget = false;
            emblem = LobbyKit.Rect(frame, EmblemName).Place(new Vector2(0, EmblemFrom), new Vector2(1, EmblemTo));
            var shade = LobbyKit.Rect(frame, ScrimName).Place(Vector2.zero, new Vector2(1, ScrimShare)).Paint(Color.white, FrameRadius);
            shade.raycastTarget = false;
            var dark = LobbyKit.ScrimNavy;
            LobbyKit.Gradient(shade, new Color(dark.r, dark.g, dark.b, 0f), new Color(dark.r, dark.g, dark.b, ScrimDepth));
            place = Label(frame, PlaceName, PlaceSize, Color.white, TextAlignmentOptions.BottomLeft, true);
            place.rectTransform.Place(Vector2.zero, new Vector2(1, ScrimShare), new Vector2(PlaceInset, PlaceLift), new Vector2(-PlaceInset, 0));
            title = Label(rect, TitleName, TitleSize, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft, true);
            who = Label(rect, WhoName, WhoSize, LobbyKit.Muted, TextAlignmentOptions.MidlineLeft, false);
            for (int i = 0; i < MostSteps; i++)
                steps.Add(Label(rect, StepName + (i + 1), StepSize, LobbyKit.Navy, TextAlignmentOptions.MidlineLeft, false));
            guide = LobbyKit.Button(rect, GuideName, LobbyKit.Raised, OpenGuide, GuideRadius, LobbyKit.Line, 1);
            var word = Label(guide.Body(), "Label", GuideSize, LobbyKit.Navy, TextAlignmentOptions.Center, false);
            word.text = GuideWord;
            word.rectTransform.Fill();
            guide.gameObject.SetActive(false);
        }

        static TextMeshProUGUI Label(Transform parent, string name, float size, Color colour, TextAlignmentOptions align, bool bold)
        {
            var text = LobbyKit.Text(parent, "", size, colour, align, bold ? FontStyles.Bold : FontStyles.Normal);
            text.name = name;
            text.enableAutoSizing = true; text.fontSizeMin = SmallestLabel; text.fontSizeMax = size;
            return text;
        }

        public void Add(LobbyPlayDetailEntry entry)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.Id)) entries[entry.Id] = entry;
        }

        public bool Has(string id) => id != null && entries.ContainsKey(id);

        public void Show(string id)
        {
            if (id == null || !entries.TryGetValue(id, out var entry) || entry == shown) return;
            shown = entry;
            bool pictured = entry.Picture;
            picture.sprite = entry.Picture; picture.enabled = pictured;
            mask.showMaskGraphic = !pictured;
            fill.color = pictured ? Color.white : Color.Lerp(LobbyKit.Raised, LobbyKit.Selection, TintShare);
            Stand(pictured ? null : entry.Emblem);
            Write(place, entry.Place); Write(title, entry.Title); Write(who, entry.Who);
            for (int i = 0; i < steps.Count; i++)
            {
                bool used = i < entry.Steps.Count;
                steps[i].gameObject.SetActive(used);
                if (used) steps[i].text = (i + 1) + StepSeparator + entry.Steps[i];
            }
            guide.gameObject.SetActive(Guided(entry));
            Fit();
            Changed?.Invoke(entry.Id);
        }

        void Stand(string item)
        {
            LobbyKit.Clear(emblem);
            var art = string.IsNullOrEmpty(item) ? null : LobbyKit.ItemImage(emblem, item, EmblemSize);
            if (!art) return;
            art.rectTransform.Fill();
            var square = art.gameObject.AddComponent<AspectRatioFitter>();
            square.aspectMode = AspectRatioFitter.AspectMode.FitInParent; square.aspectRatio = 1;
        }

        static void Write(TMP_Text text, string value)
        {
            text.text = value ?? "";
            text.gameObject.SetActive(!string.IsNullOrEmpty(value));
        }

        static bool Guided(LobbyPlayDetailEntry entry) => !string.IsNullOrEmpty(entry.Guide) && ModeGuides.Has(entry.Guide);

        void OpenGuide()
        {
            if (shown != null && Guided(shown)) ModeGuides.Show(shown.Guide);
        }

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();

        public void Fit()
        {
            if (shown == null || !frame) return;
            var size = ((RectTransform)transform).rect.size;
            if (size.x <= 0 || size.y <= 0) return;
            bool guided = Guided(shown), named = !string.IsNullOrEmpty(shown.Who);
            float room = size.y - Padding * 2;
            int lines = Mathf.Min(shown.Steps.Count, steps.Count);
            while (lines > 0 && Words(lines, named, guided) > room) lines--;
            for (int i = 0; i < steps.Count; i++)
                if (steps[i].gameObject.activeSelf != i < lines) steps[i].gameObject.SetActive(i < lines);
            if (size == arranged && lines == arrangedSteps && guided == arrangedGuide && named == arrangedWho) return;
            arranged = size; arrangedSteps = lines; arrangedGuide = guided; arrangedWho = named;
            float inner = Mathf.Max(1f, size.x - Padding * 2);
            float height = Mathf.Min(inner / PictureAspect, room - Gap - Words(lines, named, guided));
            bool pictured = height >= SmallestPicture;
            if (frame.gameObject.activeSelf != pictured) frame.gameObject.SetActive(pictured);
            float y = Padding;
            if (pictured)
            {
                Put(frame, y, Mathf.Min(inner, height * PictureAspect), height);
                y += height + Gap;
            }
            Put(title.rectTransform, y, inner, TitleHeight);
            y += TitleHeight;
            if (named)
            {
                Put(who.rectTransform, y, inner, WhoHeight);
                y += WhoHeight;
            }
            if (lines > 0) y += StepGap;
            for (int i = 0; i < lines; i++)
            {
                Put(steps[i].rectTransform, y, inner, StepHeight);
                y += StepHeight;
            }
            if (guided) Put((RectTransform)guide.transform, y + Gap, inner, GuideHeight);
        }

        static float Words(int lines, bool named, bool guided) =>
            TitleHeight + (named ? WhoHeight : 0f) + (lines > 0 ? StepGap + lines * StepHeight : 0f) + (guided ? Gap + GuideHeight : 0f);

        static void Put(RectTransform rect, float top, float width, float height)
        {
            var corner = new Vector2(0, 1);
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = new Vector2(Padding, -top);
            rect.sizeDelta = new Vector2(width, height);
        }
    }

    public sealed class LobbyPlayDetailSource : MonoBehaviour, ISelectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public const float Dwell = .18f;
        public LobbyPlayDetail Detail;
        public string Id;
        float showAt = -1f;

        public void OnSelect(BaseEventData data) => Show();
        public void OnPointerEnter(PointerEventData data) => showAt = Time.unscaledTime + Dwell;
        public void OnPointerExit(PointerEventData data) => showAt = -1f;

        void OnDisable() => showAt = -1f;

        void Update()
        {
            if (showAt < 0f || Time.unscaledTime < showAt) return;
            showAt = -1f;
            Show();
        }

        void Show()
        {
            if (Detail) Detail.Show(Id);
        }
    }

    public sealed class LobbyPlaySpare : MonoBehaviour
    {
        public RectTransform Content;
        public float MinimumHeight = 200, MaximumHeight = 248, MinimumWidth;
        float arranged = -1f;

        public bool Shown => arranged > 0f;

        public float Height => Mathf.Max(0f, arranged);

        public void Watch(RectTransform content, float minimumWidth)
        {
            Content = content; MinimumWidth = minimumWidth;
            if (transform.parent is RectTransform parent) Apply(0f, parent);
            Fit();
        }

        void OnEnable() => Fit();
        void LateUpdate() => Fit();

        public void Fit()
        {
            if (!Content || !(transform.parent is RectTransform parent) || !parent.TryGetComponent(out VerticalLayoutGroup group)) return;
            var scroll = GetComponentInParent<ScrollRect>();
            if (!scroll || !scroll.viewport || scroll.viewport.rect.height <= 0) return;
            float used = group.padding.vertical;
            foreach (Transform child in parent)
            {
                if (child == transform || !child.gameObject.activeSelf) continue;
                if (child.TryGetComponent(out LayoutElement element) && element.ignoreLayout) continue;
                used += LayoutUtility.GetPreferredHeight((RectTransform)child) + group.spacing;
            }
            float room = Mathf.Floor(scroll.viewport.rect.height - used);
            bool fits = room >= MinimumHeight && parent.rect.width - group.padding.horizontal >= MinimumWidth;
            Apply(fits ? Mathf.Min(MaximumHeight, room) : 0f, parent);
        }

        void Apply(float height, RectTransform parent)
        {
            if (Mathf.Approximately(height, arranged)) return;
            arranged = height;
            if (!TryGetComponent(out LayoutElement own)) own = gameObject.AddComponent<LayoutElement>();
            own.ignoreLayout = height <= 0f;
            own.preferredHeight = own.minHeight = height;
            own.flexibleHeight = 0;
            if (Content.gameObject.activeSelf != height > 0f) Content.gameObject.SetActive(height > 0f);
            LayoutRebuilder.MarkLayoutForRebuild(parent);
        }
    }
}
