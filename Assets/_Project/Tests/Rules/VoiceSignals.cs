using System;
using System.Collections.Generic;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Rules.Tests
{
    public sealed class VoiceSpeech
    {
        public VoiceSpeech(float[] samples, bool[] active, int rate)
        {
            Samples = samples;
            Active = active;
            Rate = rate;
        }

        public float[] Samples { get; }
        public bool[] Active { get; }
        public int Rate { get; }
        public int FrameSamples => VoiceFormat.FrameSamples(Rate);
        public int Frames => Samples.Length / FrameSamples;
    }

    public static class VoiceSignals
    {
        static readonly Dictionary<string, VoiceSpeech> cache = new Dictionary<string, VoiceSpeech>();

        public static VoiceSpeech Speech(int rate, double seconds, uint seed, float activeRms = .1f, bool fricatives = true)
        {
            string key = rate + "|" + seconds + "|" + seed + "|" + activeRms + "|" + fricatives;
            lock (cache)
                if (cache.TryGetValue(key, out var hit)) return hit;
            var made = MakeSpeech(rate, seconds, seed, activeRms, fricatives);
            lock (cache) cache[key] = made;
            return made;
        }

        static VoiceSpeech MakeSpeech(int rate, double seconds, uint seed, float activeRms, bool fricatives)
        {
            int n = (int)(rate * seconds), frame = VoiceFormat.FrameSamples(rate);
            var x = new float[n];
            var on = new bool[n];
            var random = new VoiceRandom(seed);
            var amps = new double[64];
            int i = (int)(rate * .3);
            double phase = 0;
            float hiss = 0f;
            while (i < n)
            {
                int talk = (int)(rate * (1.6 + .8 * random.NextDouble())), pause = (int)(rate * (.4 + .4 * random.NextDouble()));
                int end = Math.Min(n, i + talk), start = i, fade = rate / 25;
                bool hissy = fricatives && random.NextDouble() < .35;
                int hissEnd = hissy ? start + rate / 22 : start;
                int voiceFrom = hissEnd;
                while (i < end)
                {
                    int length = (int)(rate * (.16 + .12 * random.NextDouble()));
                    int stop = Math.Min(end, i + length);
                    double f0a = 100 + 100 * random.NextDouble(), f0b = 100 + 100 * random.NextDouble();
                    double f1 = 350 + 450 * random.NextDouble(), f2 = 900 + 1300 * random.NextDouble(), f3 = 2500 + 300 * random.NextDouble();
                    for (int s = i; s < stop; s++)
                    {
                        double tau = (s - i) / (double)Math.Max(1, stop - i);
                        double f0 = f0a + (f0b - f0a) * tau;
                        if (((s - i) & 63) == 0) Harmonics(amps, f0, f1, f2, f3);
                        phase += 2 * Math.PI * f0 / rate;
                        if (phase > 2 * Math.PI) phase -= 2 * Math.PI;
                        double sin1 = Math.Sin(phase), cos1 = Math.Cos(phase), previous = 0, current = sin1, sum = 0;
                        for (int k = 1; k < amps.Length && amps[k] > 0; k++)
                        {
                            sum += amps[k] * current;
                            double following = 2 * cos1 * current - previous;
                            previous = current;
                            current = following;
                        }
                        double envelope = .25 + .75 * Math.Pow(Math.Sin(Math.PI * tau), 2);
                        double edge = Math.Min(1, Math.Min((s - start) / (double)fade, (end - s) / (double)fade));
                        double voiced = s < voiceFrom ? 0 : Math.Min(1, (s - voiceFrom) / (double)fade);
                        x[s] = (float)(sum * envelope * Math.Max(0, edge) * voiced);
                        if (s < hissEnd + fade && hissy)
                        {
                            float white = random.Signed();
                            hiss = white - hiss * .2f;
                            double hissEnvelope = Math.Min(1, (s - start) / (double)(rate / 200)) * Math.Max(0, Math.Min(1, (hissEnd + fade - s) / (double)fade));
                            x[s] += (float)(hiss * .35 * hissEnvelope);
                        }
                        on[s] = true;
                    }
                    i = stop;
                }
                i = end + pause;
            }
            double energy = 0;
            int count = 0;
            for (int s = 0; s < n; s++)
                if (on[s]) { energy += x[s] * (double)x[s]; count++; }
            float scale = count == 0 ? 1f : (float)(activeRms / Math.Sqrt(energy / count));
            for (int s = 0; s < n; s++) x[s] *= scale;
            int frames = n / frame;
            var active = new bool[frames];
            for (int f = 0; f < frames; f++) active[f] = on[f * frame + frame / 2];
            return new VoiceSpeech(x, active, rate);
        }

        static void Harmonics(double[] amps, double f0, double f1, double f2, double f3)
        {
            Array.Clear(amps, 0, amps.Length);
            for (int k = 1; k < amps.Length; k++)
            {
                double f = k * f0;
                if (f > 3800) break;
                double g = Math.Exp(-Math.Pow((f - f1) / 160, 2)) + .7 * Math.Exp(-Math.Pow((f - f2) / 220, 2)) +
                           .35 * Math.Exp(-Math.Pow((f - f3) / 260, 2)) + .04;
                amps[k] = g / Math.Sqrt(k);
            }
        }

        public static float[] Fan(int rate, double seconds, float rms, uint seed)
        {
            int n = (int)(rate * seconds);
            var x = new float[n];
            var random = new VoiceRandom(seed);
            double b0 = 0, b1 = 0, b2 = 0, b3 = 0, b4 = 0, b5 = 0, b6 = 0;
            for (int i = 0; i < n; i++)
            {
                double white = random.Signed();
                b0 = .99886 * b0 + white * .0555179;
                b1 = .99332 * b1 + white * .0750759;
                b2 = .96900 * b2 + white * .1538520;
                b3 = .86650 * b3 + white * .3104856;
                b4 = .55000 * b4 + white * .5329522;
                b5 = -.7616 * b5 - white * .0168980;
                x[i] = (float)(b0 + b1 + b2 + b3 + b4 + b5 + b6 + white * .5362);
                b6 = white * .115926;
            }
            return Scale(x, rms);
        }

        public static float[] Rumble(int rate, double seconds, float rms, uint seed)
        {
            int n = (int)(rate * seconds);
            var x = new float[n];
            var random = new VoiceRandom(seed);
            double low = 0, low2 = 0, a = 1 - Math.Exp(-2 * Math.PI * 500 / rate);
            for (int i = 0; i < n; i++)
            {
                double white = random.Signed();
                low += a * (white - low);
                low2 += a * (low - low2);
                x[i] = (float)(low2 * 3 + white * .15);
            }
            return Scale(x, rms);
        }

        public static float[] Hum(int rate, double seconds, float rms, double mains = 60)
        {
            int n = (int)(rate * seconds);
            var x = new float[n];
            double[] weights = { 1, .7, .5, .35, .25, .15 };
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)rate, sum = 0;
                for (int k = 0; k < weights.Length; k++) sum += weights[k] * Math.Sin(2 * Math.PI * mains * (k + 1) * t + k);
                x[i] = (float)sum;
            }
            return Scale(x, rms);
        }

        public static float[] Keyboard(int rate, double seconds, float peak, uint seed, double perSecond = 7)
        {
            int n = (int)(rate * seconds);
            var x = new float[n];
            var random = new VoiceRandom(seed);
            int at = 0;
            while (true)
            {
                at += (int)(rate * (-Math.Log(1 - random.NextDouble() * .999) / perSecond)) + rate / 50;
                if (at >= n) break;
                double resonance = 2000 + 2000 * random.NextDouble(), gain = peak * (.6 + .4 * random.NextDouble());
                int length = rate / 100;
                for (int i = 0; i < length && at + i < n; i++)
                {
                    double t = i / (double)rate, decay = Math.Exp(-t / .0015);
                    x[at + i] += (float)(gain * decay * (.6 * random.Signed() + .8 * Math.Sin(2 * Math.PI * resonance * t)));
                }
            }
            return x;
        }

        public static float[] Breath(int rate, double seconds, float rms, uint seed)
        {
            int n = (int)(rate * seconds);
            var x = new float[n];
            var random = new VoiceRandom(seed);
            double low = 0, high = 0, a = 1 - Math.Exp(-2 * Math.PI * 1800 / rate), b = 1 - Math.Exp(-2 * Math.PI * 400 / rate);
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)rate, cycle = t % 2.2;
                double envelope = cycle < 1.0 ? Math.Pow(Math.Sin(Math.PI * cycle / 1.0), 2) : 0;
                double white = random.Signed();
                low += a * (white - low);
                high += b * (low - high);
                x[i] = (float)((low - high) * envelope);
            }
            return Scale(x, rms);
        }

        public static float[] Mix(params float[][] parts)
        {
            int n = int.MaxValue;
            foreach (var part in parts) n = Math.Min(n, part.Length);
            var x = new float[n];
            foreach (var part in parts)
                for (int i = 0; i < n; i++) x[i] += part[i];
            return x;
        }

        public static float[] Gain(float[] x, float gain)
        {
            var y = new float[x.Length];
            for (int i = 0; i < x.Length; i++) y[i] = x[i] * gain;
            return y;
        }

        public static float[] Scale(float[] x, float rms)
        {
            double energy = 0;
            for (int i = 0; i < x.Length; i++) energy += x[i] * (double)x[i];
            double now = Math.Sqrt(energy / Math.Max(1, x.Length));
            float k = now <= 0 ? 0f : (float)(rms / now);
            for (int i = 0; i < x.Length; i++) x[i] *= k;
            return x;
        }

        public static double MaxStep(float[] x, int from = 1, int to = -1)
        {
            if (to < 0) to = x.Length;
            double max = 0;
            for (int i = Math.Max(1, from); i < to; i++) max = Math.Max(max, Math.Abs(x[i] - x[i - 1]));
            return max;
        }

        public static int Jumps(float[] x, double threshold)
        {
            int jumps = 0;
            for (int i = 1; i < x.Length; i++)
                if (Math.Abs(x[i] - x[i - 1]) > threshold) jumps++;
            return jumps;
        }

        public static double Energy(float[] x, int from = 0, int to = -1)
        {
            if (to < 0) to = x.Length;
            double sum = 0;
            for (int i = from; i < to; i++) sum += x[i] * (double)x[i];
            return sum;
        }

        public static double Db(double ratio) => ratio <= 0 ? -200 : 10 * Math.Log10(ratio);
    }
}
