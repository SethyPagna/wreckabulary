using UnityEngine;

namespace Wreckabulary
{
    public enum Wave { Sine, Triangle, Square, Saw }

    /// <summary>
    /// A tiny synthesiser for placeholder sound: tones with pitch sweeps and plucky envelopes,
    /// filtered noise, and mixing into mono buffers. Writes wrap around, so loops are seamless.
    /// </summary>
    public static class Synth
    {
        public const int Rate = 22050;

        public static float[] Buffer(float seconds) => new float[Mathf.Max(1, Mathf.CeilToInt(seconds * Rate))];

        /// <summary>
        /// Adds a tone. The pitch glides from f0 to f1. It rises over <paramref name="attack"/>, then decays:
        /// <paramref name="decay"/> is how fast (0 holds it, with a short release at the end).
        /// </summary>
        public static void Tone(float[] b, float start, float dur, float f0, float f1, Wave wave, float vol,
                                float attack = 0.005f, float decay = 5f, float vibratoHz = 0f, float vibratoDepth = 0f,
                                bool wrap = false)
        {
            int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt(dur * Rate);
            double phase = 0;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate, k = t / dur;
                float f = f0 * Mathf.Pow(f1 / f0, k);
                if (vibratoHz > 0f) f *= 1f + Mathf.Sin(2f * Mathf.PI * vibratoHz * t) * vibratoDepth;
                phase += f / Rate;
                float p = (float)(phase - System.Math.Floor(phase));
                float v = wave switch
                {
                    Wave.Sine => Mathf.Sin(2f * Mathf.PI * p),
                    Wave.Triangle => 1f - 4f * Mathf.Abs(p - 0.5f),
                    Wave.Square => p < 0.5f ? 0.6f : -0.6f,
                    _ => (2f * p - 1f) * 0.6f,
                };
                Add(b, s0 + i, v * vol * Envelope(t, dur, attack, decay), wrap);
            }
        }

        /// <summary>Adds noise. <paramref name="smooth"/> near 0 is a low rumble, near 1 is a bright hiss.</summary>
        public static void Noise(float[] b, float start, float dur, float vol, float smooth, float attack = 0.002f,
                                 float decay = 6f, int seed = 1, bool wrap = false)
        {
            var rng = new System.Random(seed);
            int s0 = Mathf.RoundToInt(start * Rate), n = Mathf.RoundToInt(dur * Rate);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += (white - lp) * Mathf.Clamp01(smooth);
                Add(b, s0 + i, lp * vol * Envelope(t, dur, attack, decay), wrap);
            }
        }

        static float Envelope(float t, float dur, float attack, float decay)
        {
            if (t < attack) return t / attack;
            float release = Mathf.Min(0.02f, dur * 0.2f);
            float tail = t > dur - release ? (dur - t) / release : 1f;
            return (decay > 0f ? Mathf.Exp(-(t - attack) * decay / Mathf.Max(0.01f, dur)) : 1f) * tail;
        }

        static void Add(float[] b, int i, float v, bool wrap)
        {
            if (wrap) i = ((i % b.Length) + b.Length) % b.Length;
            if (i >= 0 && i < b.Length) b[i] += v;
        }

        /// <summary>Scales the buffer down if it would clip, then makes a clip.</summary>
        public static AudioClip Clip(string name, float[] b, float peakTarget = 0.9f)
        {
            float peak = 0f;
            foreach (var v in b) peak = Mathf.Max(peak, Mathf.Abs(v));
            if (peak > peakTarget)
                for (int i = 0; i < b.Length; i++) b[i] *= peakTarget / peak;
            var clip = AudioClip.Create(name, b.Length, 1, Rate, false);
            clip.SetData(b, 0);
            return clip;
        }

        public static float Midi(int note) => 440f * Mathf.Pow(2f, (note - 69) / 12f);
    }
}
