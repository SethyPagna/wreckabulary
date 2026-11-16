using System;

namespace Wreckabulary.Rules.Sound
{
    public sealed class MasterDynamics
    {
        public const double CeilingDb = -1.0;
        public const double LimitDb = -1.5;
        public const double ThresholdDb = -12;
        public const double Ratio = 2;
        public const double KneeDb = 6;
        public const double AttackSeconds = .01, ReleaseSeconds = .2;
        public const double DetectorSeconds = .05, LimitReleaseSeconds = .08;
        public const double DuckFallSeconds = .08, DuckRiseSeconds = .45;

        static readonly double Ceiling = Math.Pow(10, CeilingDb / 20);
        static readonly double Knee = Math.Pow(10, (CeilingDb - 1.5) / 20);
        static readonly double Limit = Math.Pow(10, LimitDb / 20);

        double attack, release, detector, limitRelease, duckFall, duckRise;
        double envelope, gainDb, limitGain = 1, duck = 1;
        volatile float duckTarget = 1;

        public int Rate { get; private set; }
        public double PeakIn { get; private set; }
        public double PeakOut { get; private set; }
        public double DeepestReductionDb { get; private set; }
        public long LimitedFrames { get; private set; }
        public long Frames { get; private set; }
        public float Duck { get => duckTarget; set => duckTarget = float.IsNaN(value) ? 1f : Math.Max(0f, Math.Min(1f, value)); }
        public double CurrentDuck => duck;

        public MasterDynamics(int rate = 48000) => Configure(rate);

        public void Configure(int rate)
        {
            if (rate <= 0) rate = 48000;
            Rate = rate;
            attack = Coefficient(AttackSeconds, rate);
            release = Coefficient(ReleaseSeconds, rate);
            detector = Math.Exp(-1 / (DetectorSeconds * rate));
            limitRelease = Coefficient(LimitReleaseSeconds, rate);
            duckFall = Coefficient(DuckFallSeconds, rate);
            duckRise = Coefficient(DuckRiseSeconds, rate);
        }

        static double Coefficient(double seconds, int rate) => 1 - Math.Exp(-1 / (seconds * rate));

        public void ResetStats()
        {
            PeakIn = PeakOut = DeepestReductionDb = 0;
            LimitedFrames = Frames = 0;
        }

        public void SnapDuck() => duck = duckTarget;

        public static double GainComputer(double levelDb)
        {
            double over = levelDb - ThresholdDb;
            if (2 * over < -KneeDb) return 0;
            if (2 * Math.Abs(over) <= KneeDb) return (1 / Ratio - 1) * (over + KneeDb / 2) * (over + KneeDb / 2) / (2 * KneeDb);
            return (1 / Ratio - 1) * over;
        }

        public static double SoftCeiling(double x)
        {
            double a = Math.Abs(x);
            if (a <= Knee) return x;
            double shaped = Knee + (Ceiling - Knee) * Math.Tanh((a - Knee) / (Ceiling - Knee));
            return x < 0 ? -shaped : shaped;
        }

        public void Process(float[] data, int channels)
        {
            if (data == null || channels <= 0) return;
            double target = duckTarget;
            int frames = data.Length / channels;
            for (int f = 0; f < frames; f++)
            {
                int at = f * channels;
                duck += (target - duck) * (target < duck ? duckFall : duckRise);
                double peak = 0;
                bool broken = false;
                for (int c = 0; c < channels; c++)
                {
                    double v = data[at + c];
                    if (double.IsNaN(v) || double.IsInfinity(v)) { broken = true; break; }
                    v = Math.Abs(v);
                    if (v > peak) peak = v;
                }
                if (broken)
                {
                    for (int c = 0; c < channels; c++) data[at + c] = 0;
                    envelope = 0; gainDb = 0; limitGain = 1;
                    continue;
                }
                if (peak > PeakIn) PeakIn = peak;
                peak *= duck;
                envelope = peak > envelope ? peak : envelope * detector;
                double wanted = envelope > 1e-6 ? GainComputer(20 * Math.Log10(envelope)) : 0;
                gainDb += (wanted - gainDb) * (wanted < gainDb ? attack : release);
                if (gainDb < DeepestReductionDb) DeepestReductionDb = gainDb;
                double gain = duck * (gainDb < -1e-4 ? Math.Pow(10, gainDb / 20) : 1);
                double loudest = 0;
                for (int c = 0; c < channels; c++)
                {
                    double v = Math.Abs(data[at + c] * gain);
                    if (v > loudest) loudest = v;
                }
                double need = loudest > Limit ? Limit / loudest : 1;
                double released = limitGain + (1 - limitGain) * limitRelease;
                limitGain = need < released ? need : released;
                if (need < 1) LimitedFrames++;
                double total = gain * limitGain;
                for (int c = 0; c < channels; c++)
                {
                    double y = SoftCeiling(data[at + c] * total);
                    double a = Math.Abs(y);
                    if (a > PeakOut) PeakOut = a;
                    data[at + c] = (float)y;
                }
            }
            Frames += frames;
        }
    }
}
