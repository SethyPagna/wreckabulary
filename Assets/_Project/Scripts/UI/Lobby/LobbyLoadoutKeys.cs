using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Wreckabulary
{
    public sealed class LobbyLoadoutKeys : MonoBehaviour
    {
        public const int Previous = -1, Next = 1;
        const float LeaveTilt = .25f;
        public Action<int> Step;

        static TMP_InputField Field
        {
            get
            {
                var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
                return selected && selected.TryGetComponent(out TMP_InputField field) && field.isFocused ? field : null;
            }
        }

        static bool Typing => Field;

        static bool PadLeaves()
        {
            foreach (var pad in Gamepad.all)
                if (pad.buttonEast.wasPressedThisFrame || pad.leftStick.ReadValue().sqrMagnitude > LeaveTilt || pad.dpad.ReadValue().sqrMagnitude > LeaveTilt)
                    return true;
            return false;
        }

        static bool Blocked
        {
            get
            {
                var menu = LobbyMenu.Instance;
                return Typing || KeyBindings.Busy || ModeGuides.Open || (menu && menu.Starting);
            }
        }

        static int Pressed()
        {
            foreach (var pad in Gamepad.all)
            {
                if (pad.rightTrigger.wasPressedThisFrame) return Next;
                if (pad.leftTrigger.wasPressedThisFrame) return Previous;
            }
            var keyboard = Keyboard.current;
            if (keyboard == null) return 0;
            if (keyboard.pageDownKey.wasPressedThisFrame) return Next;
            return keyboard.pageUpKey.wasPressedThisFrame ? Previous : 0;
        }

        void Update()
        {
            var field = Field;
            if (field && PadLeaves()) field.DeactivateInputField();
            if (Step == null) return;
            int step = Pressed();
            if (step != 0 && !Blocked) Step(step);
        }
    }
}
