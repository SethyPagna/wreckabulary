using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// When the mouse cursor is captured, as the web edition's pointer lock: only for a mouse player in the
    /// third-person view, and only while nothing on screen needs the pointer (pause, the Tab bag, the craft
    /// drawer, the typewriter, round buttons) and the window has focus.
    /// </summary>
    public static class CursorPolicy
    {
        /// <summary>Whether this policy has the cursor locked now.</summary>
        public static bool Locked { get; private set; }

        public static bool WantsLock(bool mouseLook, bool pointerNeeded, bool focused) => mouseLook && !pointerNeeded && focused;

        public static void Apply(bool locked)
        {
            if (locked == Locked && (Cursor.lockState == CursorLockMode.Locked) == locked) return;
            Locked = locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetLocked() => Locked = false;
    }
}
