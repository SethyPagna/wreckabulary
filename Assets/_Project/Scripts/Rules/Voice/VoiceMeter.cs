using System;

namespace Wreckabulary.Rules.Voice
{
    public static class VoiceMeter
    {
        public const float FloorDecibels = -60f, ReleasePerSecond = 2.5f;

        public static float Level(float[] samples, int count)
        {
            if (samples == null) return 0f;
            count = Math.Min(count, samples.Length);
            if (count <= 0) return 0f;
            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                float sample = samples[i];
                if (float.IsNaN(sample) || float.IsInfinity(sample)) continue;
                sum += sample * sample;
            }
            double rms = Math.Sqrt(sum / count);
            if (rms <= 0) return 0f;
            double decibels = 20 * Math.Log10(rms);
            return Clamp01((float)((decibels - FloorDecibels) / -FloorDecibels));
        }

        public static float Smooth(float shown, float target, float dt)
        {
            shown = Clamp01(shown);
            target = Clamp01(target);
            if (target >= shown) return target;
            return Math.Max(target, shown - ReleasePerSecond * Math.Max(0f, dt));
        }

        static float Clamp01(float value) => float.IsNaN(value) ? 0f : value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
