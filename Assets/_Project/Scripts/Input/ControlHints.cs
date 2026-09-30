using UnityEngine.InputSystem;

namespace Wreckabulary
{
    /// <summary>
    /// Control names for prompts. The desktop keys are read from <see cref="DesktopBinding"/>, so a rebind
    /// shows up here; then come the couch keyboard halves (left, right) and the gamepad.
    /// </summary>
    public static class ControlHints
    {
        static DesktopBinding Desktop => DesktopBinding.Shared;

        static string Key(InputAction action) => action.GetBindingDisplayString().ToUpperInvariant();

        /// <summary>"Press SPACE, J, . or A to join".</summary>
        public static string Join(string verb) => $"Press {Key(Desktop.Jump)}, J, . or A to {verb}";

        public static string Players => $"Up to 4 roommates: {Key(Desktop.Jump)} plays with the mouse, J and . share one keyboard, plus gamepads";

        public static string Attack => $"{Key(Desktop.Attack)}, J, / or X";
        public static string Grab => $"{Key(Desktop.Interact)}, Space, . or RT";
        public static string Spell => $"{Key(Desktop.Spell)}, K, Right Shift or Y";
        public static string Jump => $"{Key(Desktop.Jump)}, U, comma or A";
        // The left keyboard half dodges on Left Shift too.
        public static string Dodge => $"{Key(Desktop.Dodge)}, Right Ctrl or B";
        public static string Block => $"{Key(Desktop.Block)}, L, ; or LT";
        public static string Move => "WASD, arrow keys or the left stick";
    }
}
