using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wreckabulary
{
    [RequireComponent(typeof(ScrollRect))]
    public sealed class LobbyScrollFollow : MonoBehaviour
    {
        const float Margin = 14f;
        ScrollRect scroll;
        GameObject last;
        Vector2 viewportSize, contentSize;

        void Awake() => scroll = GetComponent<ScrollRect>();

        void LateUpdate()
        {
            var system = EventSystem.current;
            var selected = system ? system.currentSelectedGameObject : null;
            var view = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;
            var nextViewportSize = view.rect.size;
            var nextContentSize = scroll.content ? scroll.content.rect.size : Vector2.zero;
            if (selected == last && nextViewportSize == viewportSize && nextContentSize == contentSize) return;
            last = selected;
            viewportSize = nextViewportSize;
            contentSize = nextContentSize;
            if (selected && scroll.content && selected.transform.IsChildOf(scroll.content))
                Show(scroll, (RectTransform)selected.transform);
        }

        public static void Show(ScrollRect scroll, RectTransform target)
        {
            Canvas.ForceUpdateCanvases();
            var view = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;
            var content = scroll.content;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view, target);
            var shown = view.rect;
            var position = content.anchoredPosition;
            if (scroll.vertical)
            {
                float over = 0f;
                if (bounds.max.y + Margin > shown.yMax) over = bounds.max.y + Margin - shown.yMax;
                else if (bounds.min.y - Margin < shown.yMin) over = bounds.min.y - Margin - shown.yMin;
                position.y = Mathf.Clamp(position.y - over, 0f, Mathf.Max(0f, content.rect.height - shown.height));
            }
            if (scroll.horizontal && content.rect.width > shown.width)
            {
                float over = 0f;
                if (bounds.max.x + Margin > shown.xMax) over = bounds.max.x + Margin - shown.xMax;
                else if (bounds.min.x - Margin < shown.xMin) over = bounds.min.x - Margin - shown.xMin;
                var all = RectTransformUtility.CalculateRelativeRectTransformBounds(view, content);
                position.x += Mathf.Clamp(-over, shown.xMax - all.max.x, shown.xMin - all.min.x);
            }
            if (position == content.anchoredPosition) return;
            content.anchoredPosition = position;
            scroll.velocity = Vector2.zero;
        }
    }
}
