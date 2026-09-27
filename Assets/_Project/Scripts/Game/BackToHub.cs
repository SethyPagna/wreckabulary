using UnityEngine;
using UnityEngine.InputSystem;

namespace Wreckabulary
{
    /// <summary>Esc or a gamepad's Select/View button goes back to the house.</summary>
    public class BackToHub : MonoBehaviour
    {
        void Update()
        {
            bool pressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (var pad in Gamepad.all) pressed |= pad.selectButton.wasPressedThisFrame;
            if (pressed) Session.GoHome();
        }
    }
}
