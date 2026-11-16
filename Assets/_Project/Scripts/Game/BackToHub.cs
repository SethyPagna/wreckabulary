using UnityEngine;
using UnityEngine.InputSystem;

namespace Wreckabulary
{
    [DefaultExecutionOrder(-40)]
    public class BackToHub : MonoBehaviour
    {
        GameHud hud;

        void Start() => hud = FindAnyObjectByType<GameHud>();

        void Update()
        {
            if (KeyBindings.Busy) return;
            if (!hud) hud = FindFirstObjectByType<GameHud>();
            bool pressed = !hud && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (var pad in Gamepad.all) pressed |= pad.selectButton.wasPressedThisFrame;
            if (hud && !pressed) pressed = StartPauses(hud);
            if (!pressed) return;
            if (hud) hud.TogglePause();
            else Session.GoHome();
        }

        public static bool StartPauses(GameHud hud)
        {
            if (!hud || hud.ResultShown || !(hud.Paused || hud.LeaveAsked || InPlay())) return false;
            foreach (var p in World.Players)
                if (p && p.Binding is GamepadBinding seat && seat.Pad != null && seat.Pad.added && seat.Pad.startButton.wasPressedThisFrame) return true;
            return false;
        }

        static bool InPlay()
        {
            var rounds = RoundManager.Instance;
            return !rounds || !rounds.isActiveAndEnabled || rounds.Phase == Phase.Playing;
        }
    }
}
