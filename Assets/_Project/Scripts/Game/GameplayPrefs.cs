using System;
using UnityEngine;

namespace Wreckabulary
{
    public static class GameplayPrefs
    {
        public const string DamageKey = "wv.pref.damage", EffectsKey = "wv.pref.effects", NamesKey = "wv.pref.names", TipsKey = "wv.pref.tips";
        public const string OldDamageKey = "wv.feedback.damage", OldTipsKey = "wv.tips";
        public static readonly string[] Keys = { DamageKey, EffectsKey, NamesKey, TipsKey };

        public static bool Default(string key) => key != NamesKey;

        public static event Action<string> Changed;

        public static bool DamageNumbers { get => Get(DamageKey); set => Set(DamageKey, value); }
        public static bool EffectWords { get => Get(EffectsKey); set => Set(EffectsKey, value); }
        public static bool FurnitureNames { get => Get(NamesKey); set => Set(NamesKey, value); }
        public static bool ControlTips { get => Get(TipsKey); set => Set(TipsKey, value); }

        public static void ResetToDefaults(params string[] keys)
        {
            var reset = keys.Length > 0 ? keys : Keys;
            foreach (string key in reset) Forget(key);
            PlayerPrefs.Save();
            foreach (string key in reset) Changed?.Invoke(key);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Migrate()
        {
            Adopt(DamageKey);
            Adopt(TipsKey);
        }

        static string OldKey(string key) => key switch { DamageKey => OldDamageKey, TipsKey => OldTipsKey, _ => null };

        static void Adopt(string key)
        {
            string old = OldKey(key);
            if (old == null || !PlayerPrefs.HasKey(old)) return;
            if (!PlayerPrefs.HasKey(key)) PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(old));
            PlayerPrefs.DeleteKey(old);
            PlayerPrefs.Save();
        }

        static void Forget(string key)
        {
            PlayerPrefs.DeleteKey(key);
            if (OldKey(key) is { } old) PlayerPrefs.DeleteKey(old);
        }

        static bool Get(string key)
        {
            Adopt(key);
            return PlayerPrefs.GetInt(key, Default(key) ? 1 : 0) != 0;
        }

        static void Set(string key, bool value)
        {
            Forget(key);
            PlayerPrefs.SetInt(key, value ? 1 : 0);
            PlayerPrefs.Save();
            Changed?.Invoke(key);
        }
    }
}
