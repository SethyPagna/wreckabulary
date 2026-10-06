using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Wreckabulary
{
    /// <summary>Turns the lobby avatar when you drag across the open view behind the menus.</summary>
    public sealed class LobbyDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        /// <summary>Degrees per pixel of horizontal drag at 1080p.</summary>
        public const float DegreesPerPixel = .4f;

        /// <summary>Called with the degrees to turn by.</summary>
        public Action<float> Turned;

        public void OnBeginDrag(PointerEventData data) { }

        public void OnDrag(PointerEventData data) =>
            Turned?.Invoke(-data.delta.x * DegreesPerPixel * 1080f / Mathf.Max(1, Screen.height));
    }
}
