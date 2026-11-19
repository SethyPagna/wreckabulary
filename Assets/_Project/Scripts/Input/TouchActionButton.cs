using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wreckabulary
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TouchActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public const float PressedScale = .94f, UnavailableAlpha = .38f;
        public TouchAction action;
        public System.Action pressed;
        public bool sendsInput = true;
        int pointer = int.MinValue;
        Image image;
        Color resting;
        CanvasGroup group;
        bool available = true;
        float scale = 1f, opacity = 1f;

        public bool Held => pointer != int.MinValue;

        public float Scale
        {
            get => scale;
            set { scale = value; Rescale(); }
        }

        public float Opacity
        {
            get => opacity;
            set { opacity = Mathf.Clamp01(value); Fade(); }
        }

        void Awake()
        {
            image = GetComponent<Image>();
            if (image) resting = image.color;
            group = GetComponent<CanvasGroup>();
            Fade();
            Rescale();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!available || pointer != int.MinValue) return;
            pointer = e.pointerId;
            if (sendsInput) TouchBinding.Shared.SetHeld(action, true);
            pressed?.Invoke();
            if (image) image.color = Color.Lerp(resting, Color.white, 0.25f);
            Rescale();
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointer) Release();
        }

        void OnDisable() => Release();
        void OnApplicationFocus(bool focused) { if (!focused) Release(); }

        public void SetAvailable(bool value)
        {
            if (available == value) return;
            available = value;
            if (!value) Release();
            Fade();
        }

        void Fade()
        {
            if (group) group.alpha = opacity * (available ? 1f : UnavailableAlpha);
        }

        void Rescale() => transform.localScale = Vector3.one * (scale * (Held ? PressedScale : 1f));

        void Release()
        {
            pointer = int.MinValue;
            if (sendsInput) TouchBinding.Shared.SetHeld(action, false);
            if (image) image.color = resting;
            Rescale();
        }
    }
}
