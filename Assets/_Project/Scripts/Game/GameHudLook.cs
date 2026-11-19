using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Wreckabulary
{
    public partial class GameHud
    {
        const float EyeSize = 44f, EyeGap = 12f, MinimapSide = 252f;

        void BuildLookEye(RectTransform column)
        {
            var holder = Rect("Look around", column, new Vector2(0f, 1f), new Vector2(-EyeGap, -MinimapSide), new Vector2(EyeSize, EyeSize));
            holder.pivot = new Vector2(1f, 0f);
            var face = eyeRect = Panel("Eye", holder, new Vector2(.5f, .5f), Vector2.zero, new Vector2(EyeSize, EyeSize), new Vector2(.5f, .5f), Glass, true);
            var body = face.GetComponent<Image>();
            body.raycastTarget = true;
            var icon = CreateImage("Icon", face, new Vector2(.5f, .5f), Vector2.zero, new Vector2(28f, 28f), OnGame);
            icon.sprite = LobbyIcons.Get(LobbyIcons.Eye); icon.preserveAspect = true;
            AddHudShadow(icon);
            var cap = Panel("Key cap", face, new Vector2(.5f, 0f), new Vector2(0f, -4f), new Vector2(56f, 22f), new Vector2(.5f, 1f), KeyPill);
            var key = Text("Key", cap, new Vector2(.5f, .5f), Vector2.zero, new Vector2(52f, 20f), 15f, OnGame);
            key.enableAutoSizing = true; key.fontSizeMin = 10f; key.fontSizeMax = 15f;
            holder.gameObject.AddComponent<LookEye>().Bind(this, face.gameObject, body, cap.gameObject, key);
        }

        sealed class LookEye : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler, IInitializePotentialDragHandler
        {
            GameHud hud;
            GameObject face, cap;
            Image body;
            TextMeshProUGUI key;
            CameraRig lessee;
            InputBinding shownFor;
            int shownVersion = -1, pointer;
            bool held;

            public void Bind(GameHud owner, GameObject eyeFace, Image eyeBody, GameObject keyCap, TextMeshProUGUI keyText)
            {
                hud = owner; face = eyeFace; body = eyeBody; cap = keyCap; key = keyText;
                Refresh();
            }

            void Update() => Refresh();

            void Refresh()
            {
                if (!hud) return;
                bool show = !hud.TouchControlsShown && !Application.isMobilePlatform;
                if (face.activeSelf != show) face.SetActive(show);
                if (!show) { Release(); return; }
                body.color = held ? GlassActive : Glass;
                var binding = hud.LocalPlayer ? hud.LocalPlayer.Binding : null;
                if (shownVersion == KeyBindings.Version && ReferenceEquals(shownFor, binding)) return;
                shownVersion = KeyBindings.Version; shownFor = binding;
                key.text = ControlHints.LookAround(binding);
                cap.SetActive(key.text.Length > 0);
            }

            public void OnInitializePotentialDrag(PointerEventData e) => e.useDragThreshold = false;

            public void OnPointerDown(PointerEventData e)
            {
                var rig = CameraRig.Instance;
                if (held || !face.activeSelf || !rig || !rig.isActiveAndEnabled) return;
                held = true; pointer = e.pointerId; lessee = rig;
                rig.BeginLook();
            }

            public void OnDrag(PointerEventData e)
            {
                if (held && e.pointerId == pointer && lessee) lessee.OrbitDrag(e.delta);
            }

            public void OnPointerUp(PointerEventData e)
            {
                if (e.pointerId == pointer) Release();
            }

            void OnDisable() => Release();

            void Release()
            {
                if (!held) return;
                held = false;
                if (lessee) lessee.EndLook();
                lessee = null;
            }
        }
    }
}
