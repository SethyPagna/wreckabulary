using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Wreckabulary
{
    /// <summary>
    /// The web lobby's button feel on a uGUI control: the body lifts and tilts under the pointer or a
    /// controller, sinks when pressed, and its hard drop shadow grows and shrinks with it. A keyboard or
    /// controller also sees the web's orange focus ring; the mouse doesn't, like :focus-visible.
    /// A disabled control fades. It works on plain rects too (recipe cards), and never changes the
    /// selection, so code that puts a controller on GO or CANCEL decides alone.
    /// </summary>
    public sealed class LobbyPress : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        /// <summary>What moves; the root stays put for layouts and hit tests.</summary>
        public RectTransform Body;
        /// <summary>Tilt is in uGUI degrees (anticlockwise), so the web's rotate(-2deg) is +2 here.</summary>
        public float Lift = 2f, Sink = 2f, Tilt, Scale = 1f, HoverScale = 1f;
        /// <summary>The body's hard shadow, its resting depth and its depth while pressed.</summary>
        public Shadow Drop;
        public float DropRest, DropPressed = 1f;
        public Graphic Ring;
        /// <summary>False for a label that is off on purpose and should stay bold (the shop's WEARING stamp).</summary>
        public bool FadeOff = true;
        /// <summary>Called when the pointer or a controller arrives or leaves, for colour changes.</summary>
        public Action<bool> Hot;

        Selectable selectable;
        CanvasGroup fade;
        bool over, down, selected, ringed;
        bool? wasHot, wasOff;

        static int checkedFrame;
        static bool navigating;

        /// <summary>True once a key or a pad drives the lobby, until the mouse moves or clicks.</summary>
        public static bool Navigating
        {
            get
            {
                if (checkedFrame == Time.frameCount) return navigating;
                checkedFrame = Time.frameCount;
                var mouse = Mouse.current;
                if (mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.wasPressedThisFrame)) navigating = false;
                else if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame) navigating = true;
                else
                    foreach (var pad in Gamepad.all)
                        if (pad.leftStick.ReadValue().sqrMagnitude > .25f || pad.dpad.ReadValue().sqrMagnitude > .25f ||
                            pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame)
                        { navigating = true; break; }
                return navigating;
            }
        }

        public bool IsHot => wasHot == true;

        void Awake() => selectable = GetComponent<Selectable>();

        public void OnPointerEnter(PointerEventData data) => over = true;
        public void OnPointerExit(PointerEventData data) { over = false; down = false; }
        public void OnPointerDown(PointerEventData data)
        {
            if (data.button != PointerEventData.InputButton.Left) return;
            down = true;
            // Runs after the button selects itself on the same press, so a click shows no ring.
            ringed = false;
        }
        public void OnPointerUp(PointerEventData data) => down = false;
        public void OnSelect(BaseEventData data) { selected = true; ringed = true; }
        public void OnDeselect(BaseEventData data) { selected = false; ringed = false; }

        void OnDisable()
        {
            over = down = false;
            wasHot = null;
            if (!Body) return;
            Body.anchoredPosition = Vector2.zero;
            Body.localRotation = Quaternion.identity;
            Body.localScale = Vector3.one * Scale;
            SetDrop(DropRest);
        }

        void Update()
        {
            if (!Body) return;
            bool off = selectable && !selectable.IsInteractable();
            bool focus = selected && Navigating;
            bool hot = !off && (over || focus);
            if (wasOff != off)
            {
                wasOff = off;
                if (off && FadeOff && !fade) fade = Body.gameObject.AddComponent<CanvasGroup>();
                if (fade) fade.alpha = off && FadeOff ? .45f : 1f;
            }
            if (wasHot != hot) { wasHot = hot; Hot?.Invoke(hot); }
            if (Ring) Ring.enabled = focus && ringed && !off;

            float y = off ? 0f : down && over ? -Sink : hot ? Lift : 0f;
            float k = 1f - Mathf.Exp(-25f * Time.unscaledDeltaTime);
            var position = Vector2.Lerp(Body.anchoredPosition, new Vector2(0f, y), k);
            if ((position - new Vector2(0f, y)).sqrMagnitude < .0004f) position = new Vector2(0f, y);
            Body.anchoredPosition = position;
            float tilt = hot && !down ? Tilt : 0f;
            Body.localRotation = Quaternion.Slerp(Body.localRotation, Quaternion.Euler(0, 0, tilt), k);
            float scale = Scale * (hot && !down ? HoverScale : 1f);
            Body.localScale = Vector3.Lerp(Body.localScale, Vector3.one * scale, k);
            // The shadow's foot stays on the floor while the body rises, and flattens under a press.
            SetDrop(Mathf.Max(DropPressed, DropRest + position.y));
        }

        void SetDrop(float depth)
        {
            if (!Drop || DropRest <= 0f) return;
            var distance = new Vector2(0f, -Mathf.Round(depth * 2f) / 2f);
            if (Drop.effectDistance != distance) Drop.effectDistance = distance;
        }
    }
}
