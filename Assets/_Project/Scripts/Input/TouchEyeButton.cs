using UnityEngine;
using UnityEngine.EventSystems;

namespace Wreckabulary
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class TouchEyeButton : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
    {
        CameraRig lessee;
        int pointer = int.MinValue;
        float scale = 1f;

        public bool Held => pointer != int.MinValue;

        public float Scale
        {
            get => scale;
            set { scale = value; Rescale(); }
        }

        public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

        public void OnPointerDown(PointerEventData e)
        {
            var rig = CameraRig.Instance;
            if (Held || !rig || !rig.isActiveAndEnabled) return;
            pointer = e.pointerId;
            lessee = rig;
            rig.BeginLook();
            Rescale();
        }

        public void OnDrag(PointerEventData e)
        {
            if (Held && e.pointerId == pointer && lessee) lessee.OrbitDrag(e.delta);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId == pointer) Release();
        }

        void OnDisable() => Release();
        void OnApplicationFocus(bool focused) { if (!focused) Release(); }

        void Release()
        {
            if (!Held) return;
            pointer = int.MinValue;
            if (lessee) lessee.EndLook();
            lessee = null;
            Rescale();
        }

        void Rescale() => transform.localScale = Vector3.one * (scale * (Held ? TouchActionButton.PressedScale : 1f));
    }
}
