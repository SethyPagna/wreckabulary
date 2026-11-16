using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules.Sound
{
    public readonly struct ToyPartial
    {
        public readonly double Ratio;
        public readonly double Weight;

        public ToyPartial(double ratio, double weight)
        {
            Ratio = ratio;
            Weight = weight;
        }
    }

    public sealed class ToyPart
    {
        public bool Noise;
        public double Start, Length, From, To, Amp, Attack, Decay, Glide, Q;
        public ToyPartial[] Partials = Array.Empty<ToyPartial>();
    }

    public static class ToyCueSynth
    {
        public const int Rate = 48000;
        public const double CeilingDb = -3.2;
        public const double BandLimit = 0.42;
        const double Root2 = 1.41421356237;

        public static readonly string[] Cues =
            { "Pickup", "Break", "Craft", "Hit", "Block", "Jump", "Dodge", "Swing", "Out", "Protect", "Boost", "Blast", "Heal", "Tick" };

        static readonly ToyPartial[] Soft = { new ToyPartial(1, 1), new ToyPartial(2, .16), new ToyPartial(3, .04) };
        static readonly ToyPartial[] Bell = { new ToyPartial(1, 1), new ToyPartial(2, .22), new ToyPartial(3, .07), new ToyPartial(4, .025) };
        static readonly ToyPartial[] Hollow = { new ToyPartial(1, 1), new ToyPartial(2, .3), new ToyPartial(3, .1) };
        static readonly ToyPartial[] Pure = { new ToyPartial(1, 1), new ToyPartial(2, .08) };
        static readonly ToyPartial[] Wood = { new ToyPartial(1, 1), new ToyPartial(2, .12) };

        static readonly Dictionary<string, double> Trims = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["Pickup"] = -2, ["Craft"] = 0, ["Jump"] = -1, ["Dodge"] = -1, ["Protect"] = -1, ["Boost"] = -1, ["Heal"] = -1,
            ["Break"] = 0, ["Hit"] = 0, ["Block"] = 0, ["Blast"] = -2, ["Swing"] = -1, ["Out"] = -3, ["Tick"] = 0,
        };

        static readonly Dictionary<string, ToyPart[]> Recipes = new Dictionary<string, ToyPart[]>(StringComparer.Ordinal)
        {
            ["Pickup"] = new[] { Tone(0, .09, 659.26, 659.26, .8, .004, .06, Soft), Tone(.045, .1, 830.61, 830.61, 1, .004, .06, Soft) },
            ["Craft"] = new[]
            {
                Tone(0, .12, 523.25, 523.25, .8, .005, .09, Bell), Tone(.07, .12, 659.26, 659.26, .8, .005, .09, Bell),
                Tone(.14, .24, 783.99, 783.99, 1, .005, .13, Bell), Tone(.14, .22, 1046.5, 1046.5, .22, .008, .11, Pure),
            },
            ["Heal"] = new[] { Tone(0, .14, 392, 392, .8, .012, .12, Soft), Tone(.08, .24, 587.33, 587.33, 1, .012, .15, Soft) },
            ["Protect"] = new[] { Tone(0, .24, 440, 440, .9, .006, .11, Bell), Tone(.03, .22, 587.33, 587.33, .8, .006, .1, Bell) },
            ["Boost"] = new[]
            {
                Tone(0, .08, 587.33, 587.33, .8, .004, .06, Soft), Tone(.04, .08, 739.99, 739.99, .85, .004, .06, Soft),
                Tone(.08, .13, 880, 880, 1, .004, .08, Soft),
            },
            ["Out"] = new[] { Tone(0, .15, 392, 392, .9, .008, .2, Hollow), Tone(.13, .24, 329.63, 293.66, 1, .008, .2, Hollow, .2) },
            ["Jump"] = new[] { Tone(0, .14, 330, 560, 1, .006, .1, Pure, .12) },
            ["Dodge"] = new[] { Puff(0, .14, 520, 1100, 1, .03, .09, 1.3) },
            ["Swing"] = new[] { Puff(0, .13, 380, 720, 1, .028, .08, 1.1) },
            ["Hit"] = new[] { Tone(0, .12, 260, 170, 1, .002, .06, Hollow, .05), Puff(0, .04, 900, 600, .35, .002, .015) },
            ["Block"] = new[] { Tone(0, .1, 620, 620, 1, .0015, .03, Wood), Tone(0, .1, 410, 410, .45, .0015, .034, Wood) },
            ["Break"] = new[]
            {
                Tone(0, .17, 180, 120, 1, .002, .055, Hollow, .1), Puff(0, .2, 1100, 500, .55, .003, .07),
                Tone(.05, .05, 520, 520, .28, .0015, .013, Wood), Tone(.095, .05, 440, 440, .24, .0015, .013, Wood),
            },
            ["Blast"] = new[] { Tone(0, .36, 160, 70, 1, .004, .13, Pure, .3), Puff(0, .34, 900, 300, 1, .006, .1) },
            ["Tick"] = new[] { Tone(0, .035, 1050, 1050, 1, .001, .008, Pure), Tone(0, .035, 525, 525, .5, .001, .01, Pure) },
        };

        static ToyPart Tone(double start, double length, double from, double to, double amp, double attack, double decay,
            ToyPartial[] partials, double glide = 0) =>
            new ToyPart { Start = start, Length = length, From = from, To = to, Amp = amp, Attack = attack, Decay = decay, Partials = partials, Glide = glide };

        static ToyPart Puff(double start, double length, double from, double to, double amp, double attack, double decay, double q = 0) =>
            new ToyPart { Noise = true, Start = start, Length = length, From = from, To = to, Amp = amp, Attack = attack, Decay = decay, Q = q };

        public static bool Has(string cue) => cue != null && Recipes.ContainsKey(cue);

        public static double TrimDb(string cue) => cue != null && Trims.TryGetValue(cue, out double trim) ? trim : 0;

        public static IReadOnlyList<ToyPart> Parts(string cue) => Recipes[cue];

        public static double HighestPartial(string cue, int rate = Rate, double pitch = 1)
        {
            double highest = 0;
            foreach (var part in Recipes[cue])
            {
                double top = Math.Max(part.From, part.To) * pitch;
                if (part.Noise) { highest = Math.Max(highest, Math.Min(top * 1.6, .4 * rate)); continue; }
                foreach (var partial in part.Partials)
                    if (partial.Ratio * top < BandLimit * rate) highest = Math.Max(highest, partial.Ratio * top);
            }
            return highest;
        }

        public static float[] Render(string cue, int rate = Rate, double pitch = 1)
        {
            if (!Has(cue)) throw new ArgumentException("Unknown cue " + cue, nameof(cue));
            if (rate < 8000) throw new ArgumentOutOfRangeException(nameof(rate));
            var parts = Recipes[cue];
            double end = 0;
            foreach (var part in parts) end = Math.Max(end, part.Start + part.Length);
            int n = (int)Math.Ceiling(end * rate) + 1;
            var buffer = new double[n + 2];
            int index = Array.IndexOf(Cues, cue);
            for (int k = 0; k < parts.Length; k++)
            {
                if (parts[k].Noise) RenderPuff(buffer, parts[k], rate, pitch, 0x9E3779B9u ^ (uint)(index * 7919 + k * 104729));
                else RenderTone(buffer, parts[k], rate, pitch);
            }
            Finish(buffer, n, rate);
            double peak = 0;
            for (int i = 0; i < n; i++) peak = Math.Max(peak, Math.Abs(buffer[i]));
            var output = new float[n];
            if (peak <= 0) return output;
            double gain = Math.Pow(10, (CeilingDb + TrimDb(cue)) / 20) / peak;
            for (int i = 0; i < n; i++) output[i] = (float)(buffer[i] * gain);
            return output;
        }

        static void Finish(double[] buffer, int n, int rate)
        {
            int fadeIn = Math.Max(2, (int)Math.Round(.001 * rate));
            int fadeOut = Math.Max(2, (int)Math.Round(.004 * rate));
            for (int j = 0; j < Math.Min(fadeIn, n); j++) buffer[j] *= .5 - .5 * Math.Cos(Math.PI * j / fadeIn);
            for (int j = 0; j < Math.Min(fadeOut, n); j++) buffer[n - 1 - j] *= .5 - .5 * Math.Cos(Math.PI * j / fadeOut);
            double sum = 0, weights = 0;
            for (int i = 0; i < n; i++)
            {
                sum += buffer[i];
                weights += .5 - .5 * Math.Cos(2 * Math.PI * i / (n - 1));
            }
            double mean = sum / weights;
            for (int i = 0; i < n; i++) buffer[i] -= mean * (.5 - .5 * Math.Cos(2 * Math.PI * i / (n - 1)));
        }

        static void RenderTone(double[] buffer, ToyPart part, int rate, double pitch)
        {
            int start = (int)Math.Round(part.Start * rate);
            int count = (int)Math.Round(part.Length * rate);
            double top = Math.Max(part.From, part.To) * pitch;
            var used = new List<(double ratio, double weight, double tau)>(part.Partials.Length);
            foreach (var partial in part.Partials)
                if (partial.Ratio * top < BandLimit * rate)
                    used.Add((partial.Ratio, partial.Weight, part.Decay / (1 + .5 * (partial.Ratio - 1))));
            double release = Math.Min(.03, part.Length * .4);
            double phase = 0;
            for (int j = 0; j < count; j++)
            {
                double t = (double)j / rate;
                double f = part.From * pitch;
                if (part.Glide > 0 && part.To != part.From) f = part.From * pitch * Math.Pow(part.To / part.From, Math.Min(t / part.Glide, 1));
                double a = t < part.Attack ? .5 - .5 * Math.Cos(Math.PI * t / part.Attack) : 1;
                double e = t > part.Length - release ? .5 + .5 * Math.Cos(Math.PI * (t - (part.Length - release)) / release) : 1;
                double s = 0;
                foreach (var (ratio, weight, tau) in used) s += weight * Math.Exp(-t / tau) * Math.Sin(ratio * phase);
                buffer[start + j] += part.Amp * a * e * s;
                phase += 2 * Math.PI * f / rate;
            }
        }

        static void RenderPuff(double[] buffer, ToyPart part, int rate, double pitch, uint seed)
        {
            int start = (int)Math.Round(part.Start * rate);
            int count = (int)Math.Round(part.Length * rate);
            double ic1 = 0, ic2 = 0, jc1 = 0, jc2 = 0;
            uint state = seed == 0 ? 1u : seed;
            double damping = part.Q > 0 ? 1 / part.Q : Root2;
            for (int j = 0; j < count; j++)
            {
                double t = (double)j / rate;
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                double noise = state / 4294967296.0 * 2 - 1;
                double fc = Math.Min(part.From * pitch * Math.Pow(part.To / part.From, Math.Min(t / part.Length, 1)), .4 * rate);
                double g = Math.Tan(Math.PI * fc / rate);
                Svf(ref ic1, ref ic2, noise, g, damping, out double band, out double low);
                double v = part.Q > 0 ? band : low;
                double g2 = Math.Tan(Math.PI * Math.Min(fc * 1.6, .4 * rate) / rate);
                Svf(ref jc1, ref jc2, v, g2, Root2, out _, out v);
                buffer[start + j] += part.Amp * Envelope(j, rate, part.Length, part.Attack, part.Decay) * v;
            }
        }

        static double Envelope(int j, int rate, double length, double attack, double decay)
        {
            double t = (double)j / rate;
            double a = t < attack ? .5 - .5 * Math.Cos(Math.PI * t / attack) : 1;
            double release = Math.Min(.03, length * .4);
            double r = t > length - release ? .5 + .5 * Math.Cos(Math.PI * (t - (length - release)) / release) : 1;
            return a * Math.Exp(-t / decay) * r;
        }

        static void Svf(ref double ic1, ref double ic2, double v0, double g, double k, out double band, out double low)
        {
            double a1 = 1 / (1 + g * (g + k));
            double a2 = g * a1;
            double a3 = g * a2;
            double v3 = v0 - ic2;
            band = a1 * ic1 + a2 * v3;
            low = ic2 + a2 * ic1 + a3 * v3;
            ic1 = 2 * band - ic1;
            ic2 = 2 * low - ic2;
        }

        public static double Checksum(float[] samples)
        {
            double sum = 0;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * Math.Sin((i + 1) * .001);
            return sum;
        }
    }
}
