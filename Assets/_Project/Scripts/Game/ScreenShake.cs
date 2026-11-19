using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public static class ScreenShake
    {
        public const string Key = "wv.shake";

        static ShakeLevel? level;

        public static ShakeLevel Level
        {
            get => level ??= ShakeLevels.Parse(PlayerPrefs.GetInt(Key, (int)ShakeLevel.Full));
            set
            {
                level = value;
                PlayerPrefs.SetInt(Key, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static float Scale => ShakeLevels.Scale(Level);

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
            level = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ForgetCache() => level = null;
    }
}
