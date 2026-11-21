using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wreckabulary
{
    public sealed class LobbyShade : MonoBehaviour, IPointerClickHandler
    {
        const float Settled = .0001f;
        public Action Clicked;
        readonly Vector3[] corners = new Vector3[4];
        Canvas canvas;

        void OnEnable() => Cover();

        void LateUpdate() => Cover();

        void OnTransformParentChanged() => canvas = null;

        public void Cover()
        {
            if (!(transform is RectTransform rect) || !(rect.parent is RectTransform parent)) return;
            if (!canvas) canvas = GetComponentInParent<Canvas>();
            if (!canvas || !(canvas.rootCanvas.transform is RectTransform screen)) return;
            screen.GetWorldCorners(corners);
            Vector2 low = parent.InverseTransformPoint(corners[0]), high = parent.InverseTransformPoint(corners[2]);
            var area = parent.rect;
            Vector2 min = low - area.min, max = high - area.max;
            if (rect.anchorMin != Vector2.zero || rect.anchorMax != Vector2.one)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
            }
            if ((rect.offsetMin - min).sqrMagnitude > Settled || (rect.offsetMax - max).sqrMagnitude > Settled)
            {
                rect.offsetMin = min;
                rect.offsetMax = max;
            }
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || e.pointerPressRaycast.gameObject != gameObject) return;
            Clicked?.Invoke();
        }
    }
}
