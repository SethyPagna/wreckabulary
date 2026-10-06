using System;
using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// A real-time countdown for a lobby choice that undoes itself (a new display): it writes the seconds left
    /// into a label and acts when they run out. It sits on the menu, so it keeps counting with its page closed.
    /// </summary>
    public sealed class LobbyCountdown : MonoBehaviour
    {
        public float Ends;
        public Action Done;
        TextMeshProUGUI label;
        Func<int, string> words;
        int shown = -1;

        /// <summary>Where to write the seconds left, and how. The label may go with its page; the count goes on.</summary>
        public void Show(TextMeshProUGUI target, Func<int, string> say)
        {
            label = target;
            words = say;
            shown = -1;
            Update();
        }

        void Update()
        {
            float left = Ends - Time.unscaledTime;
            if (left <= 0f)
            {
                var done = Done;
                Done = null;
                done?.Invoke();
                return;
            }
            int seconds = Mathf.CeilToInt(left);
            if (seconds == shown || !label || words == null) return;
            shown = seconds;
            label.text = words(seconds);
        }
    }
}
