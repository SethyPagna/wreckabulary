using UnityEngine;
using UnityEngine.InputSystem;

namespace Wreckabulary
{
    /// <summary>
    /// A gamepad's Select/View button goes back to the house. Esc opens the HUD's pause card instead
    /// (Home is on it, as in the browser edition); only a scene without a HUD lets Esc go straight home.
    /// </summary>
    public class BackToHub : MonoBehaviour
    {
        GameHud hud;

        void Update()
        {
            if (!hud) hud = FindFirstObjectByType<GameHud>();
            bool pressed = !hud && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (var pad in Gamepad.all) pressed |= pad.selectButton.wasPressedThisFrame;
            if (pressed)
            {
                if (Time.timeScale == 0f) Time.timeScale = 1f;
                Session.GoHome();
            }
        }
    }
}
