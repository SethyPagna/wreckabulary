using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wreckabulary
{
    /// <summary>
    /// Keeps a keyboard or controller selection in a lobby scroll view: when the selection moves to something in
    /// the list that is cut off, the list scrolls just far enough to show it. It only acts when the selection
    /// changes, so it never fights the mouse wheel.
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public sealed class LobbyScrollFollow : MonoBehaviour
    {
        /// <summary>Room kept between the selection and the view's edge, for its lift, drop and focus ring.</summary>
        const float Margin = 14f;
        ScrollRect scroll;
        GameObject last;

        void Awake() => scroll = GetComponent<ScrollRect>();

        void LateUpdate()
        {
            var system = EventSystem.current;
            var selected = system ? system.currentSelectedGameObject : null;
            if (selected == last) return;
            last = selected;
            if (selected && scroll.content && selected.transform.IsChildOf(scroll.content))
                Show(scroll, (RectTransform)selected.transform);
        }

        /// <summary>Scrolls a vertical list the least it takes to show <paramref name="target"/>.</summary>
        public static void Show(ScrollRect scroll, RectTransform target)
        {
            Canvas.ForceUpdateCanvases();
            var view = scroll.viewport ? scroll.viewport : (RectTransform)scroll.transform;
            var content = scroll.content;
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view, target);
            var shown = view.rect;
            // Positive when the target pokes out of the top, negative out of the bottom.
            float over = 0f;
            if (bounds.max.y + Margin > shown.yMax) over = bounds.max.y + Margin - shown.yMax;
            else if (bounds.min.y - Margin < shown.yMin) over = bounds.min.y - Margin - shown.yMin;
            if (over == 0f) return;
            // The list hangs from the view's top, so it moves down (y falls) to show what is above.
            var position = content.anchoredPosition;
            position.y = Mathf.Clamp(position.y - over, 0f, Mathf.Max(0f, content.rect.height - shown.height));
            content.anchoredPosition = position;
            scroll.velocity = Vector2.zero;
        }
    }
}
