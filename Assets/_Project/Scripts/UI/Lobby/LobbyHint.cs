using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wreckabulary
{
    /// <summary>A small label under an icon button while the pointer or a controller is on it.</summary>
    public sealed class LobbyHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public string Text;
        GameObject shown;

        public void OnPointerEnter(PointerEventData data) => Show(true);
        public void OnPointerExit(PointerEventData data) => Show(false);
        public void OnSelect(BaseEventData data) => Show(true);
        public void OnDeselect(BaseEventData data) => Show(false);
        void OnDisable() => Show(false);

        void Show(bool on)
        {
            if (!on) { if (shown) Destroy(shown); shown = null; return; }
            if (shown || string.IsNullOrEmpty(Text)) return;
            // Hangs just below the button, slid sideways to stay on screen (Home sits close to the left edge).
            // Everything under the scaled root canvas has scale 1, so its units match anchoredPosition.
            float width = Mathf.Max(120, Text.Length * 11 + 28);
            var self = (RectTransform)transform;
            var root = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
            float centre = root.InverseTransformPoint(self.TransformPoint(self.rect.center)).x;
            float half = width / 2 + 8;
            float shift = Mathf.Clamp(centre, root.rect.xMin + half, root.rect.xMax - half) - centre;
            var tip = LobbyKit.Rect(transform, "Hint").Pin(new Vector2(.5f, 0f), new Vector2(shift, -6), new Vector2(width, 34));
            tip.pivot = new Vector2(.5f, 1f);
            tip.Paint(LobbyKit.Panel, true).raycastTarget = false;
            var label = LobbyKit.Text(tip, Text, 18, LobbyKit.Cream, TextAlignmentOptions.Center);
            label.rectTransform.Fill();
            // Hints draw above the bar and the pages.
            var canvas = tip.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 500;
            shown = tip.gameObject;
        }
    }
}
