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
        public Action<int> Step;

        static bool Typing
        {
            get
            {
                var selected = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
                return selected && selected.TryGetComponent(out TMP_InputField field) && field.isFocused;
            }
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
            if (Step == null) return;
            int step = Pressed();
            if (step != 0 && !Blocked) Step(step);
        }
    }
}
