using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    public sealed class LobbyPlayColumnPlan
    {
        public int RowsFit { get; }
        public int Stack { get; }
        public bool Wrapped { get; }
        public float Width { get; }
        public float Height { get; }
        public float BandHeight { get; }
        public IReadOnlyList<int> Columns { get; }
        public IReadOnlyList<int> Lines { get; }

        public LobbyPlayColumnPlan(int rowsFit, int stack, bool wrapped, float width, float height, float bandHeight, int[] columns, int[] lines)
        {
            RowsFit = rowsFit; Stack = stack; Wrapped = wrapped; Width = width; Height = height; BandHeight = bandHeight;
            Columns = columns; Lines = lines;
        }

        public (int column, int row) Cell(int entry) => (entry / Stack, entry % Stack);

        public int Rows(int entries) => Mathf.Min(entries, Stack);

        public float SectionWidth(int section) => Columns[section] * Width + (Columns[section] - 1) * LobbyPlayColumnFlow.Spacing;

        public float StackHeight(int rows) => rows <= 0 ? 0f : rows * Height + (rows - 1) * LobbyPlayColumnFlow.Spacing;
    }

    public sealed class LobbyPlayColumnFlow : MonoBehaviour
    {
        public const string Caption = "Section caption";
        public const float Spacing = 12, SectionSpacing = 28, CaptionHeight = 28, CaptionGap = 6;
        public const float MinimumWidth = 400, MaximumWidth = 760, MinimumHeight = 120, MaximumHeight = 220;
        public const float Head = CaptionHeight + CaptionGap;
        float arrangedWidth = -1f, arrangedRoom = -1f;
        int arrangedCards = -1;
        bool fitting;

        public LobbyPlayColumnPlan Plan { get; private set; }

        public static LobbyPlayColumnPlan Layout(IReadOnlyList<int> counts, float width, float height)
        {
            int sections = counts.Count, most = sections == 0 ? 0 : Mathf.Max(0, counts.Max());
            if (most == 0) return new LobbyPlayColumnPlan(1, 1, false, 0f, 0f, 0f, new int[sections], new int[0]);
            bool bounded = height > 0;
            int rowsFit = bounded ? Mathf.Max(1, Mathf.FloorToInt((height - Head + Spacing) / (MinimumHeight + Spacing))) : most;
            var columns = counts.Select(count => Mathf.Max(1, Mathf.CeilToInt(count / (float)rowsFit))).ToArray();
            int total = columns.Sum();
            float cardWidth = (width - Spacing * (total - sections) - SectionSpacing * (sections - 1)) / total;
            bool wrapped = cardWidth < MinimumWidth && (total > sections || sections > 1);
            int perLine = sections, stack = rowsFit;
            if (wrapped)
            {
                columns = counts.Select(_ => 1).ToArray();
                perLine = Mathf.Clamp(Mathf.FloorToInt((width + SectionSpacing) / (MinimumWidth + SectionSpacing)), 1, sections);
                cardWidth = (width - SectionSpacing * (perLine - 1)) / perLine;
                stack = most;
            }
            cardWidth = Mathf.Floor(Mathf.Min(MaximumWidth, Mathf.Max(1f, cardWidth)));
            int rows = Mathf.Min(most, stack);
            float cardHeight = bounded
                ? Mathf.Floor(Mathf.Clamp((height - Head - Spacing * (rows - 1)) / rows, MinimumHeight, MaximumHeight))
                : MaximumHeight;
            var lines = new List<int>();
            for (int at = 0; at < sections; at += perLine) lines.Add(Mathf.Min(perLine, sections - at));
            float band = 0f;
            int start = 0;
            foreach (int size in lines)
            {
                int tallest = 0;
                for (int i = start; i < start + size; i++) tallest = Mathf.Max(tallest, Mathf.Min(counts[i], stack));
                band += Head + tallest * cardHeight + Mathf.Max(0, tallest - 1) * Spacing + SectionSpacing;
                start += size;
            }
            return new LobbyPlayColumnPlan(rowsFit, stack, wrapped, cardWidth, cardHeight, band - SectionSpacing, columns, lines.ToArray());
        }

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();
        void LateUpdate() => Fit();

        public void Fit()
        {
            if (fitting) return;
            float width = Available();
            if (width <= 0) return;
            float room = Room();
            var sections = Sections();
            int cards = sections.Sum(section => Cards(section).Count);
            if (cards == 0 || cards == arrangedCards && Mathf.Approximately(width, arrangedWidth) && Mathf.Approximately(room, arrangedRoom)) return;
            fitting = true;
            try
            {
                Arrange(sections, width, room);
                arrangedWidth = width; arrangedRoom = room; arrangedCards = cards;
            }
            finally { fitting = false; }
        }

        float Available()
        {
            float own = ((RectTransform)transform).rect.width;
            if (!(transform.parent is RectTransform parent) || !parent.TryGetComponent(out VerticalLayoutGroup group)) return own;
            return parent.rect.width - group.padding.horizontal;
        }

        float Room()
        {
            var scroll = GetComponentInParent<ScrollRect>();
            if (!scroll || !scroll.viewport) return 0f;
            float above = 0f;
            if (transform.parent is RectTransform parent && parent.TryGetComponent(out VerticalLayoutGroup group))
            {
                above = group.padding.top;
                for (int i = 0; i < transform.GetSiblingIndex(); i++)
                {
                    var sibling = (RectTransform)parent.GetChild(i);
                    if (!sibling.gameObject.activeSelf || sibling.TryGetComponent(out LayoutElement element) && element.ignoreLayout) continue;
                    above += LayoutUtility.GetPreferredHeight(sibling) + group.spacing;
                }
            }
            return Mathf.Max(1f, scroll.viewport.rect.height - above);
        }

        List<RectTransform> Sections() =>
            transform.Cast<Transform>().Where(child => child.gameObject.activeSelf).Select(child => (RectTransform)child).ToList();

        static List<RectTransform> Cards(Transform section) =>
            section.Cast<Transform>().Where(child => child.name != Caption && child.gameObject.activeSelf).Select(child => (RectTransform)child).ToList();

        void Arrange(List<RectTransform> sections, float width, float room)
        {
            var cards = sections.Select(Cards).ToList();
            Plan = Layout(cards.Select(list => list.Count).ToArray(), width, room);
            float y = 0f;
            int start = 0;
            foreach (int size in Plan.Lines)
            {
                float x = 0f, tallest = 0f;
                for (int i = start; i < start + size; i++)
                {
                    float sectionWidth = Plan.SectionWidth(i), sectionHeight = Head + Plan.StackHeight(Plan.Rows(cards[i].Count));
                    Set(sections[i], new Vector2(x, -y), new Vector2(sectionWidth, sectionHeight));
                    if (sections[i].Find(Caption) is RectTransform caption) Set(caption, Vector2.zero, new Vector2(sectionWidth, CaptionHeight));
                    for (int entry = 0; entry < cards[i].Count; entry++)
                    {
                        var (column, row) = Plan.Cell(entry);
                        Set(cards[i][entry], new Vector2(column * (Plan.Width + Spacing), -Head - row * (Plan.Height + Spacing)),
                            new Vector2(Plan.Width, Plan.Height));
                    }
                    x += sectionWidth + SectionSpacing;
                    tallest = Mathf.Max(tallest, sectionHeight);
                }
                y += tallest + SectionSpacing;
                start += size;
            }
            if (!TryGetComponent(out LayoutElement element) || !Mathf.Approximately(element.preferredHeight, Plan.BandHeight))
                ((RectTransform)transform).Size(-1, Plan.BandHeight);
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

    public sealed class LobbyPlayRowCard : MonoBehaviour
    {
        public const float Inset = 8, TextGap = 14, TickRoom = 60, TitleShare = .48f, LineGap = 4;
        public RectTransform Picture, Title, Detail;
        Vector2 arranged = -Vector2.one;

        void OnEnable() => Fit();
        void OnRectTransformDimensionsChange() => Fit();

        public void Fit()
        {
            var size = ((RectTransform)transform).rect.size;
            if (!Picture || !Title || !Detail || size.x <= 0 || size.y <= 0 || size == arranged) return;
            arranged = size;
            float inner = Mathf.Max(1f, size.y - Inset * 2), pictureWidth = inner * LobbyPlayCardGrid.PictureAspect;
            float text = Inset + pictureWidth + TextGap, titleHeight = Mathf.Floor(inner * TitleShare);
            Set(Picture, new Vector2(Inset, -Inset), new Vector2(pictureWidth, inner));
            Set(Title, new Vector2(text, -Inset), new Vector2(Mathf.Max(1f, size.x - text - TickRoom), titleHeight));
            Set(Detail, new Vector2(text, -Inset - titleHeight - LineGap),
                new Vector2(Mathf.Max(1f, size.x - text - TextGap), Mathf.Max(1f, inner - titleHeight - LineGap)));
        }

        static void Set(RectTransform rect, Vector2 position, Vector2 size)
        {
            var corner = new Vector2(0, 1);
            rect.anchorMin = rect.anchorMax = rect.pivot = corner;
            rect.anchoredPosition = position; rect.sizeDelta = size;
        }
    }
}
