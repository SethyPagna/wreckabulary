using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyPlayPageView : MonoBehaviour
    {
        public RectTransform Panel, Body, Queues, Choices, Summary;
        public TextMeshProUGUI Title, Subtitle;
        public Button Close;
        [Min(8)] public Vector2 ContentInset = new Vector2(20, 16);
        [Range(72, 140)] public float HeaderHeight = 88;
        [Range(88, 140)] public float SummaryHeight = 88;
        [Range(600, 1100)] public float NavigationMaximumWidth = 760;
        [Range(44, 64)] public float NavigationHeight = 48;
        UnityAction closeListener;
        bool optionLayout;
        float optionWidth = -1;

        public void Validate()
        {
            LobbyShopLayout.ValidateTree(this);
            foreach (var role in new Component[] { Panel, Body, Queues, Choices, Summary, Title, Subtitle, Close })
                LobbyShopLayout.Require(this, role, "Play frame role");
            if (Title == Subtitle) throw new InvalidOperationException("Play title and subtitle must be separate text roles.");
            if (!Finite(NavigationMaximumWidth) || NavigationMaximumWidth < 600 || NavigationMaximumWidth > 1100 ||
                !Finite(NavigationHeight) || NavigationHeight < 44 || NavigationHeight > 64)
                throw new InvalidOperationException("Play navigation needs finite bounded width/height.");
            var regions = new[] { Queues, Choices, Summary };
            for (int i = 0; i < regions.Length; i++)
                for (int j = i + 1; j < regions.Length; j++)
                    if (regions[i].IsChildOf(regions[j]) || regions[j].IsChildOf(regions[i]))
                        throw new InvalidOperationException("Play dynamic regions must not contain one another.");
            if (!Title.transform.IsChildOf(Panel) || !Close.transform.IsChildOf(Panel) ||
                regions.Any(region => Title.transform.IsChildOf(region) || Close.transform.IsChildOf(region)))
                throw new InvalidOperationException("Play title and Close must stay in the panel header, outside dynamic regions.");
            if (new[] { Panel, Body, Queues, Choices, Summary }.Distinct().Count() != 5 ||
                !Body.IsChildOf(Panel) || !Queues.IsChildOf(Panel) || !Choices.IsChildOf(Body) || !Summary.IsChildOf(Body) ||
                !Choices.GetComponent<VerticalLayoutGroup>() || !Close.GetComponent<LobbyPress>() ||
                Close.onClick.GetPersistentEventCount() != 0)
                throw new InvalidOperationException("Play needs separate panel, queue, choice and summary regions with a runtime-bound Close action.");
        }

        public void Bind(Action close, string subtitle, bool compact, Vector2 maximum)
        {
            Validate();
            if (closeListener != null) Close.onClick.RemoveListener(closeListener);
            closeListener = () => close(); Close.onClick.AddListener(closeListener);
            Subtitle.text = subtitle ?? "";
            if (compact)
            {
                if (!Panel.TryGetComponent(out LobbyCompactPanel fit)) fit = Panel.gameObject.AddComponent<LobbyCompactPanel>();
                fit.MaximumSize = maximum; fit.Fit();
            }
        }

        public void BindOptions(Action close, string subtitle)
        {
            Bind(close, subtitle, false, Vector2.zero);
            ArrangeOptions();
        }

        public void ArrangeOptions()
        {
            Validate();
            optionLayout = true;
            Panel.Fill();
            FitOptions();
        }

        void FitOptions()
        {
            optionWidth = Panel.rect.width;
            Title.gameObject.SetActive(false);
            Subtitle.gameObject.SetActive(false);
            float width = Mathf.Min(NavigationMaximumWidth, Mathf.Max(1, Panel.rect.width - ContentInset.x * 2 - 88));
            Queues.Place(new Vector2(.5f, 1), new Vector2(.5f, 1),
                new Vector2(-width * .5f, -ContentInset.y - NavigationHeight), new Vector2(width * .5f, -ContentInset.y));
            ((RectTransform)Close.transform).Pin(Vector2.one, new Vector2(-16, -16), new Vector2(40, 40));
            float header = Mathf.Max(HeaderHeight, NavigationHeight + ContentInset.y * 2);
            Body.Place(Vector2.zero, Vector2.one, ContentInset, new Vector2(-ContentInset.x, -header));
            Choices.Place(Vector2.zero, Vector2.one, new Vector2(0, SummaryHeight + 12), Vector2.zero);
            Summary.Place(Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, SummaryHeight));
        }

        void LateUpdate() { if (optionLayout && !Mathf.Approximately(optionWidth, Panel.rect.width)) FitOptions(); }

        void OnDestroy() { if (Close && closeListener != null) Close.onClick.RemoveListener(closeListener); }
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }


    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class LobbyPlayCardGrid : MonoBehaviour
    {
        public const float PictureAspect = 16f / 9f, Inset = 8, CaptionHeight = 40, PreferredWidth = 384, ShortestCard = 140, ViewportMargin = 28;
        public float MinimumWidth = 264;
        public int MaximumColumns = 3;
        GridLayoutGroup grid;
        bool fitting;
        Vector2 viewportSize;

        public static float HeightFor(float width) => Mathf.Max(0f, width - Inset * 2) / PictureAspect + CaptionHeight + Inset;

        public static (int columns, Vector2 cell) Layout(float available, float spacing, float minimumWidth, int maximumColumns, float viewportHeight)
        {
            int columns = Mathf.Clamp(Mathf.FloorToInt((available + spacing) / (Mathf.Min(PreferredWidth, minimumWidth) + spacing)), 1, Mathf.Max(1, maximumColumns));
            float width = Mathf.Max(1f, Mathf.Min(PreferredWidth, (available - (columns - 1) * spacing) / columns));
            float height = HeightFor(width);
            if (viewportHeight > 0) height = Mathf.Min(height, Mathf.Max(ShortestCard, viewportHeight - ViewportMargin));
            return (columns, new Vector2(width, height));
        }

        void OnEnable() { grid = GetComponent<GridLayoutGroup>(); Fit(); }
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() => Fit();

        internal void Fit()
        {
            if (!grid || fitting) return;
            fitting = true;
            try { FitCurrent(); }
            finally { fitting = false; }
        }

        void FitCurrent()
        {
            var rect = (RectTransform)transform;
            if (rect.rect.width <= 0 || transform.childCount == 0) return;
            var scroll = GetComponentInParent<ScrollRect>();
            var nextViewportSize = scroll && scroll.viewport ? scroll.viewport.rect.size : Vector2.zero;
            bool changed = nextViewportSize != viewportSize;
            viewportSize = nextViewportSize;
            var (columns, cell) = Layout(rect.rect.width, grid.spacing.x, MinimumWidth, MaximumColumns, viewportSize.y);
            int rows = Mathf.CeilToInt(transform.childCount / (float)columns);
            float height = rows * cell.y + (rows - 1) * grid.spacing.y;
            if (grid.startCorner != GridLayoutGroup.Corner.UpperLeft) { grid.startCorner = GridLayoutGroup.Corner.UpperLeft; changed = true; }
            if (grid.childAlignment != TextAnchor.UpperLeft) { grid.childAlignment = TextAnchor.UpperLeft; changed = true; }
            if (grid.constraint != GridLayoutGroup.Constraint.FixedColumnCount) { grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; changed = true; }
            if (grid.constraintCount != columns) { grid.constraintCount = columns; changed = true; }
            if (grid.cellSize != cell) { grid.cellSize = cell; changed = true; }
            var size = GetComponent<LayoutElement>();
            if (!size || !Mathf.Approximately(size.preferredHeight, height)) { rect.Size(-1, height); changed = true; }
            if (changed && scroll && scroll.content)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
                var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
                if (selected && selected.transform.IsChildOf(scroll.content))
                    LobbyScrollFollow.Show(scroll, (RectTransform)selected.transform);
            }
        }
    }

    public sealed class LobbyPlayCardBand : MonoBehaviour
    {
        public const string Caption = "Section caption";
        public float Spacing = 12, SectionSpacing = 28, CaptionHeight = 28, CaptionGap = 6, MinimumWidth = 120;
        float arrangedWidth = -1f;
        bool fitting;

        public static float CardWidth(float available, int cards, int sections, float spacing, float sectionSpacing, float minimum)
        {
            if (cards <= 0) return 0f;
            float room = available - spacing * (cards - sections) - sectionSpacing * Mathf.Max(0, sections - 1);
            return Mathf.Clamp(room / cards, minimum, LobbyPlayCardGrid.PreferredWidth);
        }

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() => Fit();

        public void Fit()
        {
            float width = ((RectTransform)transform).rect.width;
            if (fitting || width <= 0 || Mathf.Approximately(width, arrangedWidth)) return;
            fitting = true;
            try { Arrange(width); arrangedWidth = width; }
            finally { fitting = false; }
        }

        void Arrange(float width)
        {
            var sections = transform.Cast<Transform>().Where(child => child.gameObject.activeSelf).Select(child => (RectTransform)child).ToList();
            int cards = sections.Sum(section => Cards(section).Count());
            if (cards == 0) return;
            float cardWidth = CardWidth(width, cards, sections.Count, Spacing, SectionSpacing, MinimumWidth);
            float cardHeight = LobbyPlayCardGrid.HeightFor(cardWidth), top = CaptionHeight + CaptionGap, x = 0f;
            foreach (var section in sections)
            {
                int count = Cards(section).Count();
                float sectionWidth = count * cardWidth + Mathf.Max(0, count - 1) * Spacing, at = 0f;
                Set(section, new Vector2(x, 0), new Vector2(sectionWidth, top + cardHeight));
                foreach (Transform child in section)
                {
                    var item = (RectTransform)child;
                    if (child.name == Caption) { Set(item, Vector2.zero, new Vector2(sectionWidth, CaptionHeight)); continue; }
                    Set(item, new Vector2(at, -top), new Vector2(cardWidth, cardHeight));
                    at += cardWidth + Spacing;
                }
                x += sectionWidth + SectionSpacing;
            }
            ((RectTransform)transform).Size(-1, top + cardHeight);
        }

        static IEnumerable<Transform> Cards(Transform section) => section.Cast<Transform>().Where(child => child.name != Caption);

        static void Set(RectTransform rect, Vector2 position, Vector2 size)
        {
            var corner = new Vector2(0, 1);
            if (rect.anchorMin != corner || rect.anchorMax != corner || rect.pivot != corner)
            { rect.anchorMin = rect.anchorMax = rect.pivot = corner; }
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
            if (rect.sizeDelta != size) rect.sizeDelta = size;
        }
    }

    public sealed class LobbyPlayModeDock : MonoBehaviour
    {
        public RectTransform Choices, Goal;
        public float MaximumWidth = 1160, GoalHeight = 30, GoalGap = 2;
        bool fitting;

        void LateUpdate() => Fit();

        public void Fit()
        {
            if (fitting || !Choices || !(transform.parent is RectTransform parent)) return;
            fitting = true;
            try
            {
                var row = (RectTransform)transform;
                var layout = GetComponent<LayoutElement>();
                float height = Mathf.Max(44, layout ? layout.preferredHeight : row.rect.height);
                float width = Mathf.Min(MaximumWidth, Mathf.Max(1, parent.rect.width));
                Place(row, width, 0, height);
                var families = GetComponentInChildren<LobbyPlayModeFamilies>();
                if (families) families.Fit();
                height = Mathf.Max(44, layout ? layout.preferredHeight : row.rect.height);
                Place(row, width, 0, height);
                float below = height;
                if (Goal)
                {
                    Place(Goal, width, height + GoalGap, GoalHeight);
                    below += GoalGap + GoalHeight;
                }
                var choicesTop = new Vector2(Choices.offsetMax.x, -below - 12);
                if (Choices.offsetMax != choicesTop) Choices.offsetMax = choicesTop;
            }
            finally { fitting = false; }
        }

        static void Place(RectTransform rect, float width, float top, float height)
        {
            var anchor = new Vector2(.5f, 1);
            var minimum = new Vector2(-width * .5f, -top - height); var maximum = new Vector2(width * .5f, -top);
            if (rect.anchorMin != anchor || rect.anchorMax != anchor || rect.offsetMin != minimum || rect.offsetMax != maximum)
                rect.Place(anchor, anchor, minimum, maximum);
        }
    }

    public sealed class LobbyPlayGoalInfo : MonoBehaviour
    {
        public const float Size = 34, Gap = 6;
        public TMP_Text Line;
        public RectTransform Icon;
        float placedWidth = -1f;
        string placedText;

        void LateUpdate() => Fit();

        public void Fit()
        {
            if (!Line || !Icon) return;
            var rect = Line.rectTransform.rect;
            if (rect.width <= 0 || Mathf.Approximately(rect.width, placedWidth) && placedText == Line.text) return;
            placedWidth = rect.width; placedText = Line.text;
            var room = new Vector4(Size + Gap, 0, Size + Gap, 0);
            if (Line.margin != room) Line.margin = room;
            Line.ForceMeshUpdate();
            var bounds = Line.textBounds;
            float end = bounds.size.x > 0 ? bounds.max.x : 0f;
            Icon.anchoredPosition = new Vector2(end - rect.center.x + Gap + Size * .5f, 0f);
        }
    }

    public sealed class LobbyPlayModeFamilies : MonoBehaviour
    {
        public const string Caption = "Family caption";
        public float ButtonWidth = 104, ButtonHeight = 52, CaptionHeight = 22;
        public float Spacing = 6, FamilySpacing = 28, LineSpacing = 8, OneLineScale = .8f, RowFooter = 10;
        public bool OneRow;
        readonly Dictionary<Transform, float> natural = new Dictionary<Transform, float>();
        float arrangedWidth = -1f;
        int arrangedCount = -1;
        bool fitting;

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() => Fit();

        float Available => OneRow && transform.parent is RectTransform view ? view.rect.width : ((RectTransform)transform).rect.width;

        public void Fit()
        {
            float width = Available;
            if (fitting || width <= 0) return;
            int count = ActiveFamilies();
            if (count == arrangedCount && Mathf.Approximately(width, arrangedWidth)) return;
            fitting = true;
            try
            {
                Arrange(width);
                arrangedWidth = width; arrangedCount = count;
            }
            finally { fitting = false; }
        }

        int ActiveFamilies()
        {
            int count = 0;
            foreach (Transform child in transform)
                if (child.gameObject.activeSelf) count++;
            return count;
        }

        void Arrange(float width)
        {
            var families = transform.Cast<Transform>().Where(child => child.gameObject.activeSelf).Select(child => (RectTransform)child).ToList();
            if (families.Count == 0) return;
            if (OneRow)
            {
                float sum = families.Sum(Natural), gaps = FamilySpacing * (families.Count - 1);
                width = sum * Mathf.Clamp(Mathf.Max(0f, width - gaps) / Mathf.Max(1f, sum), OneLineScale, 1f) + gaps;
                var own = (RectTransform)transform;
                if (!Mathf.Approximately(own.rect.width, width)) own.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            }
            var lines = new List<List<RectTransform>>();
            int start = 0;
            var sizes = OneRow ? new[] { families.Count } : Lines(families.Select(Natural).ToList(), FamilySpacing, width, OneLineScale);
            foreach (int size in sizes)
            {
                lines.Add(families.GetRange(start, size));
                start += size;
            }
            float rowHeight = CaptionHeight + ButtonHeight, y = 0f;
            foreach (var line in lines)
            {
                float sum = line.Sum(Natural);
                float lineScale = Mathf.Min(1f, Mathf.Max(0f, width - FamilySpacing * (line.Count - 1)) / Mathf.Max(1f, sum));
                float lineWidth = sum * lineScale + FamilySpacing * (line.Count - 1);
                float x = Mathf.Max(0f, (width - lineWidth) * .5f);
                foreach (var family in line)
                {
                    float familyWidth = Natural(family) * lineScale;
                    Set(family, new Vector2(x, -y), new Vector2(familyWidth, rowHeight));
                    float at = 0f;
                    foreach (Transform child in family)
                    {
                        var item = (RectTransform)child;
                        if (child.name == Caption) { Set(item, Vector2.zero, new Vector2(familyWidth, CaptionHeight)); continue; }
                        float itemWidth = Choice(item) * lineScale;
                        Set(item, new Vector2(at, -CaptionHeight), new Vector2(itemWidth, ButtonHeight));
                        at += itemWidth + Spacing * lineScale;
                    }
                    x += familyWidth + FamilySpacing;
                }
                y += rowHeight + LineSpacing;
            }
            float height = y - LineSpacing;
            if (!TryGetComponent(out LayoutElement element)) element = gameObject.AddComponent<LayoutElement>();
            if (!Mathf.Approximately(element.preferredHeight, height)) { element.preferredHeight = height; element.minHeight = height; }
            if (!OneRow) return;
            var content = (RectTransform)transform;
            if (!Mathf.Approximately(content.rect.height, height)) content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            if (transform.parent.TryGetComponent(out LayoutElement band) && !Mathf.Approximately(band.preferredHeight, height + RowFooter))
                band.preferredHeight = band.minHeight = height + RowFooter;
        }

        public static int[] Lines(IReadOnlyList<float> widths, float spacing, float width, float minimumScale)
        {
            int count = widths.Count;
            if (count == 0) return new int[0];
            int[] best = null;
            float widestBest = float.MaxValue;
            for (int mask = 0; mask < 1 << (count - 1); mask++)
            {
                var sizes = new List<int>();
                float line = widths[0], widest = 0f;
                int members = 1;
                bool fits = true;
                for (int i = 1; i <= count && fits; i++)
                {
                    if (i < count && (mask & (1 << (i - 1))) == 0) { line += widths[i]; members++; continue; }
                    float gaps = spacing * (members - 1);
                    fits = members == 1 || line * minimumScale + gaps <= width;
                    widest = Mathf.Max(widest, line + gaps);
                    sizes.Add(members);
                    if (i < count) { line = widths[i]; members = 1; }
                }
                if (!fits) continue;
                if (best == null || sizes.Count < best.Length || sizes.Count == best.Length && widest < widestBest)
                { best = sizes.ToArray(); widestBest = widest; }
            }
            return best;
        }

        float Natural(Transform family)
        {
            if (natural.TryGetValue(family, out float known)) return known;
            float choices = 0f, caption = 0f; int count = 0;
            foreach (Transform child in family)
            {
                if (child.name == Caption) { caption = Preferred(child) + 2f; continue; }
                choices += Choice((RectTransform)child); count++;
            }
            float result = Mathf.Max(caption, choices + Spacing * Mathf.Max(0, count - 1));
            natural[family] = result;
            return result;
        }

        float Choice(RectTransform item)
        {
            if (natural.TryGetValue(item, out float known)) return known;
            float result = item.GetComponent<Selectable>() ? Mathf.Max(ButtonWidth, Preferred(item) + 28f) : Preferred(item) + 4f;
            natural[item] = result;
            return result;
        }

        static float Preferred(Transform item)
        {
            var text = item.GetComponentInChildren<TMP_Text>(true);
            return text ? text.GetPreferredValues(text.text).x : 0f;
        }

        static void Set(RectTransform rect, Vector2 position, Vector2 size)
        {
            var corner = new Vector2(0, 1);
            if (rect.anchorMin != corner || rect.anchorMax != corner || rect.pivot != corner)
            { rect.anchorMin = rect.anchorMax = rect.pivot = corner; }
            if (rect.anchoredPosition != position) rect.anchoredPosition = position;
            if (rect.sizeDelta != size) rect.sizeDelta = size;
        }
    }
}
