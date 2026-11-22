using System;

namespace Wreckabulary.Rules
{
    public enum HealthBand { Healthy, Hurt, Critical }

    public static class SeatVitals
    {
        public const float HurtShare = .5f, CriticalShare = .25f;
        public const int Hidden = -1;
        const int DownBase = -2;

        public static float Share(float value, float max) => max > 0f && value > 0f ? Math.Min(1f, value / max) : 0f;

        public static HealthBand Band(float share) =>
            share <= CriticalShare ? HealthBand.Critical : share <= HurtShare ? HealthBand.Hurt : HealthBand.Healthy;

        public static int Reading(float health, bool alive, bool downed, float bleedOutLeft, bool countdown)
        {
            if (alive) return (int)Math.Ceiling(Math.Max(0f, health));
            return downed && countdown ? DownBase - (int)Math.Ceiling(Math.Max(0f, bleedOutLeft)) : Hidden;
        }

        public static string Label(int reading) =>
            reading >= 0 ? reading.ToString() : reading == Hidden ? "" : $"DOWN {DownBase - reading}s";

        public static bool Winded(float stamina, float dodgeCost) => stamina < dodgeCost;

        public static bool ShowsStamina(bool alive) => PlayerBar.StaminaShown && alive;
    }
}
