using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules.Sound
{
    public struct Biquad
    {
        public double B0, B1, B2, A1, A2;
        double z1, z2;

        public Biquad(double b0, double b1, double b2, double a1, double a2)
        {
            B0 = b0; B1 = b1; B2 = b2; A1 = a1; A2 = a2;
            z1 = z2 = 0;
        }

        public double Process(double x)
        {
            double y = B0 * x + z1;
            z1 = B1 * x - A1 * y + z2;
            z2 = B2 * x - A2 * y;
            return y;
        }

        public void Reset() => z1 = z2 = 0;
    }

    public sealed class MixReport
    {
        public double PeakDbfs = -120, IntegratedLufs = -120, MomentaryMaxLufs = -120, ShortTermMaxLufs = -120, ShortTermMinLufs = -120;
        public double LoudnessRangeLu;
        public int ClippedSamples, Frames;
        public override string ToString() =>
            $"peak {PeakDbfs:0.00} dBFS, clipped {ClippedSamples}, integrated {IntegratedLufs:0.0} LUFS, momentary max {MomentaryMaxLufs:0.0}, " +
            $"short-term {ShortTermMinLufs:0.0}..{ShortTermMaxLufs:0.0} LUFS, LRA {LoudnessRangeLu:0.0} LU";
    }

    public static class Loudness
    {
        public const double ClipLevel = .999;

        public static void KWeighting(int rate, out Biquad shelf, out Biquad high)
        {
            const double gain = 3.999843853973347, q = .7071752369554196, fc = 1681.974450955533;
            double k = Math.Tan(Math.PI * fc / rate);
            double vh = Math.Pow(10, gain / 20);
            double vb = Math.Pow(vh, .4996667741545416);
            double a0 = 1 + k / q + k * k;
            shelf = new Biquad((vh + vb * k / q + k * k) / a0, 2 * (k * k - vh) / a0, (vh - vb * k / q + k * k) / a0,
                2 * (k * k - 1) / a0, (1 - k / q + k * k) / a0);
            const double q2 = .5003270373238773, fc2 = 38.13547087613982;
            k = Math.Tan(Math.PI * fc2 / rate);
            a0 = 1 + k / q2 + k * k;
            high = new Biquad(1, -2, 1, 2 * (k * k - 1) / a0, (1 - k / q2 + k * k) / a0);
        }

        public static double Lufs(double meanSquare) => -.691 + 10 * Math.Log10(Math.Max(meanSquare, 1e-20));

        public static double[] SubBlocks(float[] interleaved, int channels, int rate, int frames = -1)
        {
            if (channels <= 0) throw new ArgumentOutOfRangeException(nameof(channels));
            int total = frames < 0 ? interleaved.Length / channels : frames;
            int step = (int)Math.Round(.01 * rate);
            var filters = new Biquad[channels * 2];
            for (int c = 0; c < channels; c++) KWeighting(rate, out filters[c * 2], out filters[c * 2 + 1]);
            var blocks = new double[(total + step - 1) / step];
            for (int i = 0; i < total; i++)
            {
                double power = 0;
                for (int c = 0; c < channels; c++)
                {
                    double y = filters[c * 2 + 1].Process(filters[c * 2].Process(interleaved[i * channels + c]));
                    power += y * y;
                }
                blocks[i / step] += power;
            }
            return blocks;
        }

        static double[] Windows(double[] blocks, int size, int hop, int step, bool complete = false)
        {
            int count = complete && blocks.Length >= size ? (blocks.Length - size) / hop + 1 : (blocks.Length + hop - 1) / hop;
            var result = new double[Math.Max(1, count)];
            for (int w = 0; w < result.Length; w++)
            {
                double sum = 0;
                int start = w * hop;
                for (int b = start; b < start + size && b < blocks.Length; b++) sum += blocks[b];
                result[w] = Lufs(sum / ((double)size * step));
            }
            return result;
        }

        public static double MomentaryMax(float[] mono, int rate)
        {
            var momentary = Windows(SubBlocks(mono, 1, rate), 40, 1, (int)Math.Round(.01 * rate));
            double best = -120;
            foreach (double m in momentary) best = Math.Max(best, m);
            return best;
        }

        public static MixReport Measure(float[] interleaved, int channels, int rate, int frames = -1)
        {
            int total = frames < 0 ? interleaved.Length / channels : frames;
            var report = new MixReport { Frames = total };
            double peak = 0;
            for (int i = 0; i < total * channels; i++)
            {
                double v = Math.Abs(interleaved[i]);
                if (v > peak) peak = v;
                if (v >= ClipLevel) report.ClippedSamples++;
            }
            report.PeakDbfs = peak > 0 ? 20 * Math.Log10(peak) : -120;
            int step = (int)Math.Round(.01 * rate);
            var blocks = SubBlocks(interleaved, channels, rate, total);
            var momentary = Windows(blocks, 40, 1, step);
            var shortTerm = Windows(blocks, 300, 10, step, true);
            var gatingBlocks = Windows(blocks, 40, 10, step, true);
            foreach (double m in momentary) report.MomentaryMaxLufs = Math.Max(report.MomentaryMaxLufs, m);
            report.IntegratedLufs = Gated(gatingBlocks, 10);
            var kept = new List<double>();
            double shortMean = Gated(shortTerm, 1000);
            foreach (double s in shortTerm)
            {
                if (s <= -70 || s <= shortMean - 20) continue;
                kept.Add(s);
            }
            if (kept.Count > 0)
            {
                kept.Sort();
                report.ShortTermMaxLufs = kept[kept.Count - 1];
                report.ShortTermMinLufs = kept[0];
                report.LoudnessRangeLu = kept.Count > 2 ? Percentile(kept, .95) - Percentile(kept, .10) : 0;
            }
            return report;
        }

        static double Gated(double[] values, double relative)
        {
            double sum = 0;
            int count = 0;
            foreach (double v in values) if (v > -70) { sum += Math.Pow(10, (v + .691) / 10); count++; }
            if (count == 0) return -120;
            double mean = Lufs(sum / count);
            double kept = 0;
            int n = 0;
            foreach (double v in values) if (v > -70 && v > mean - relative) { kept += Math.Pow(10, (v + .691) / 10); n++; }
            return n == 0 ? -120 : Lufs(kept / n);
        }

        static double Percentile(List<double> sorted, double p)
        {
            double index = p * (sorted.Count - 1);
            int low = (int)Math.Floor(index);
            int high = Math.Min(sorted.Count - 1, low + 1);
            return sorted[low] + (sorted[high] - sorted[low]) * (index - low);
        }
    }
}
