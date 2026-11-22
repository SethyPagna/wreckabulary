using System;

namespace Wreckabulary.Rules
{
    public static class CameraPush
    {
        public const float Zoom = .16f, Pan = .3f, MaxPan = 3f, RiseShare = .6f, ReleaseSeconds = .6f;

        public static float Length(float seconds) => seconds > 0f ? seconds + ReleaseSeconds : 0f;

        public static float Weight(float elapsed, float seconds)
        {
            if (!(seconds > 0f) || !(elapsed >= 0f) || elapsed >= Length(seconds)) return 0f;
            float rise = seconds * RiseShare;
            if (elapsed < rise) return Smooth(elapsed / rise);
            if (elapsed <= seconds) return 1f;
            return 1f - Smooth((elapsed - seconds) / ReleaseSeconds);
        }

        public static float Narrow(float framed, float weight)
        {
            float amount = Clamp01(weight);
            return amount > 0f ? framed * (1f - Zoom * amount) : framed;
        }

        public static float Reach(float distance, float weight)
        {
            float amount = Clamp01(weight);
            if (!(distance > 0f) || amount <= 0f) return 0f;
            return Math.Min(distance * Pan, MaxPan) * amount;
        }

        static float Smooth(float t)
        {
            t = Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static float Clamp01(float value) => value > 0f ? value < 1f ? value : 1f : 0f;
    }
}
