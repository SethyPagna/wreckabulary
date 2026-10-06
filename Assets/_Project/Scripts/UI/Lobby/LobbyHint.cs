using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wreckabulary
{
    /// <summary>
    /// A small label under an icon button: after a short pause under the pointer (so sweeping across the
    /// bar doesn't flash every name), at once for a keyboard or controller, fading in, sized to its words.
    /// </summary>
    public sealed class LobbyHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        /// <summary>The web's hover pause before a hint, and its fade.</summary>
        public const float Delay = .35f, FadeIn = .12f;
        public string Text;
        GameObject shown;
        CanvasGroup fade;
        float showAt = -1f, shownAt;

        public void OnPointerEnter(PointerEventData data) { if (!shown) showAt = Time.unscaledTime + Delay; }
        public void OnPointerExit(PointerEventData data) => Hide();
        public void OnSelect(BaseEventData data) { if (LobbyPress.Navigating) Show(); }
        public void OnDeselect(BaseEventData data) => Hide();
        void OnDisable() => Hide();

        void Update()
        {
            if (showAt >= 0f && Time.unscaledTime >= showAt) Show();
            if (fade && fade.alpha < 1f) fade.alpha = Mathf.Clamp01((Time.unscaledTime - shownAt) / FadeIn);
        }

        void Hide()
        {
            showAt = -1f;
            if (shown) Destroy(shown);
            shown = null;
        }

        void Show()
        {
            showAt = -1f;
            if (shown || string.IsNullOrEmpty(Text)) return;
            var tip = LobbyKit.Rect(transform, "Hint");
            var label = LobbyKit.Text(tip, Text, 17, LobbyKit.Cream, TextAlignmentOptions.Center);
            float width = Mathf.Ceil(label.GetPreferredValues(Text).x) + 28;
            // Hangs just below the button, slid sideways to stay on screen (Home sits close to the left edge).
            // Everything under the scaled root canvas has scale 1, so its units match anchoredPosition.
            var self = (RectTransform)transform;
            var root = (RectTransform)GetComponentInParent<Canvas>().rootCanvas.transform;
            float centre = root.InverseTransformPoint(self.TransformPoint(self.rect.center)).x;
            float half = width / 2 + 8;
            float shift = Mathf.Clamp(centre, root.rect.xMin + half, root.rect.xMax - half) - centre;
            tip.Pin(new Vector2(.5f, 0f), new Vector2(shift, -8), new Vector2(width, 36));
            tip.pivot = new Vector2(.5f, 1f);
            tip.SetAsLastSibling();
            tip.Paint(LobbyKit.Panel, 8).raycastTarget = false;
            LobbyKit.Frame(tip, LobbyKit.ChipEdge, 8, 2);
            label.transform.SetAsLastSibling();
            label.rectTransform.Fill();
            // Hints draw above the bar and the pages.
            var canvas = tip.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 500;
            fade = tip.gameObject.AddComponent<CanvasGroup>();
            fade.alpha = 0f;
            fade.blocksRaycasts = false;
            shownAt = Time.unscaledTime;
            shown = tip.gameObject;
        }
    }
}
