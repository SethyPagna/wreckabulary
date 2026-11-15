using UnityEngine;

namespace Wreckabulary
{
    public static class CombatFeedbackOptions
    {
        public const string DamageKey = GameplayPrefs.DamageKey;
        public const string PointsKey = "wv.feedback.points";
        public static bool ShowDamage { get => GameplayPrefs.DamageNumbers; set => GameplayPrefs.DamageNumbers = value; }
        public static bool ShowPoints { get => PlayerPrefs.GetInt(PointsKey, 1) != 0; set => Save(PointsKey, value); }

        static void Save(string key, bool value)
        {
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(PointsKey);
            GameplayPrefs.ResetToDefaults(GameplayPrefs.DamageKey, GameplayPrefs.EffectsKey);
        }
    }
}
