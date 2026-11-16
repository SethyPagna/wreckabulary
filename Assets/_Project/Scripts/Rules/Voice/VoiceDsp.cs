using System;

namespace Wreckabulary.Rules.Voice
{
    public sealed class VoiceHighPass
    {
        public const double DefaultCutoff = 90;
        static readonly double[] Q = { .54119610, 1.30656296 };

        readonly double[] b0 = new double[2], b1 = new double[2], b2 = new double[2], a1 = new double[2], a2 = new double[2];
        readonly double[] z1 = new double[2], z2 = new double[2];

        public VoiceHighPass(int sampleRate, double cutoff = DefaultCutoff)
        {
            int rate = VoiceFormat.ValidRate(sampleRate);
            Cutoff = Math.Max(10, Math.Min(rate * .45, cutoff));
            double w = 2 * Math.PI * Cutoff / rate, cos = Math.Cos(w), sin = Math.Sin(w);
            for (int s = 0; s < 2; s++)
            {
                double alpha = sin / (2 * Q[s]), a0 = 1 + alpha;
                b0[s] = (1 + cos) / 2 / a0;
                b1[s] = -(1 + cos) / a0;
                b2[s] = (1 + cos) / 2 / a0;
                a1[s] = -2 * cos / a0;
                a2[s] = (1 - alpha) / a0;
            }
        }

        public double Cutoff { get; }

        public void Reset()
        {
            Array.Clear(z1, 0, 2);
            Array.Clear(z2, 0, 2);
        }

        public void Process(float[] samples, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                double x = samples[offset + i];
                for (int s = 0; s < 2; s++)
                {
                    double y = b0[s] * x + z1[s];
                    z1[s] = b1[s] * x - a1[s] * y + z2[s];
                    z2[s] = b2[s] * x - a2[s] * y;
                    x = y;
                }
                if (double.IsNaN(x) || double.IsInfinity(x))
                {
                    Reset();
                    x = 0;
                }
                samples[offset + i] = (float)x;
            }
        }
    }

    public sealed class VoiceFft
    {
        readonly int size, bits;
        readonly int[] reverse;
        readonly double[] cos, sin;

        public VoiceFft(int size)
        {
            if (size < 4 || (size & (size - 1)) != 0) throw new ArgumentException("The FFT size must be a power of two.", nameof(size));
            this.size = size;
            while ((1 << bits) < size) bits++;
            reverse = new int[size];
            for (int i = 0; i < size; i++)
            {
                int r = 0;
                for (int b = 0; b < bits; b++) r |= ((i >> b) & 1) << (bits - 1 - b);
                reverse[i] = r;
            }
            cos = new double[size / 2];
            sin = new double[size / 2];
            for (int i = 0; i < size / 2; i++)
            {
                cos[i] = Math.Cos(2 * Math.PI * i / size);
                sin[i] = Math.Sin(2 * Math.PI * i / size);
            }
        }

        public int Size => size;

        public void Forward(double[] re, double[] im) => Run(re, im, -1);

        public void Inverse(double[] re, double[] im)
        {
            Run(re, im, 1);
            double k = 1.0 / size;
            for (int i = 0; i < size; i++)
            {
                re[i] *= k;
                im[i] *= k;
            }
        }

        void Run(double[] re, double[] im, int sign)
        {
            for (int i = 0; i < size; i++)
            {
                int j = reverse[i];
                if (j <= i) continue;
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
            for (int length = 2; length <= size; length <<= 1)
            {
                int half = length >> 1, stride = size / length;
                for (int start = 0; start < size; start += length)
                    for (int k = 0; k < half; k++)
                    {
                        double wr = cos[k * stride], wi = sign * sin[k * stride];
                        int a = start + k, b = a + half;
                        double tr = re[b] * wr - im[b] * wi, ti = re[b] * wi + im[b] * wr;
                        re[b] = re[a] - tr;
                        im[b] = im[a] - ti;
                        re[a] += tr;
                        im[a] += ti;
                    }
            }
        }
    }

    public sealed class VoiceLimiter
    {
        public const float DefaultThreshold = .7f, DefaultCeiling = .98f;
        public const double ReleaseSeconds = .08;

        readonly float threshold, ceiling, release;
        float envelope;

        public VoiceLimiter(int sampleRate, float threshold = DefaultThreshold, float ceiling = DefaultCeiling)
        {
            int rate = VoiceFormat.ValidRate(sampleRate);
            this.ceiling = Math.Max(.1f, Math.Min(1f, ceiling));
            this.threshold = Math.Max(.05f, Math.Min(this.ceiling * .95f, threshold));
            release = (float)Math.Exp(-1.0 / (ReleaseSeconds * rate));
        }

        public float Threshold => threshold;
        public float Ceiling => ceiling;
        public float GainReduction { get; private set; } = 1f;

        public void Reset()
        {
            envelope = 0f;
            GainReduction = 1f;
        }

        public void Process(float[] samples, int offset, int count)
        {
            float lowest = 1f;
            for (int i = 0; i < count; i++)
            {
                float x = samples[offset + i];
                if (float.IsNaN(x) || float.IsInfinity(x)) x = 0f;
                float a = Math.Abs(x);
                envelope = a > envelope ? a : envelope * release + a * (1f - release);
                float gain = Gain(envelope);
                if (gain < lowest) lowest = gain;
                samples[offset + i] = x * gain;
            }
            GainReduction = lowest;
        }

        public float Gain(float level)
        {
            if (level <= threshold) return 1f;
            float room = ceiling - threshold, over = (level - threshold) / room;
            return (threshold + room * over / (1f + over)) / level;
        }
    }
}
