using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wreckabulary
{
    /// <summary>
    /// The empty space round a dialog or a centred page: a click there closes it, as on the web.
    /// A click that lands on the dialog itself (a child) bubbles up here too, so only a press that
    /// started on the shade counts.
    /// </summary>
    public sealed class LobbyShade : MonoBehaviour, IPointerClickHandler
    {
        public Action Clicked;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || e.pointerPressRaycast.gameObject != gameObject) return;
            Clicked?.Invoke();
        }
    }
}
