using System;

namespace Wreckabulary.Rules.Voice
{
    public sealed class VoiceActivity
    {
        public const double HangoverSeconds = .3, MinVoicing = .5, MinSnrDb = 6, StrongSnrDb = 12, MaxBurstShare = .45;
        public const int AttackFrames = 2, AttackWindow = 3, Blocks = 8;

        readonly int rate, step, minLag, maxLag, hangoverFrames;
        readonly float[] past;
        int history, hang;

        public VoiceActivity(int sampleRate)
        {
            rate = VoiceFormat.ValidRate(sampleRate);
            step = Math.Max(1, rate / 8000);
            minLag = Math.Max(2, rate / 400);
            maxLag = Math.Max(minLag + 1, rate / 80);
            hangoverFrames = (int)Math.Round(HangoverSeconds * VoiceFormat.FramesPerSecond);
            past = new float[VoiceFormat.FrameSamples(rate) + maxLag];
        }

        public bool Active { get; private set; }
        public bool Likely { get; private set; }
        public float Voicing { get; private set; }
        public float SnrDb { get; private set; }
        public float BurstShare { get; private set; }

        public void Reset()
        {
            history = hang = 0;
            Array.Clear(past, 0, past.Length);
            Active = Likely = false;
            Voicing = SnrDb = BurstShare = 0f;
        }

        public bool Update(float[] frame, int offset, int count, double snrDb)
        {
            SnrDb = (float)snrDb;
            BurstShare = Burst(frame, offset, count);
            Voicing = Periodicity(frame, offset, count);
            Likely = snrDb >= MinSnrDb && BurstShare <= MaxBurstShare &&
                     (Voicing >= MinVoicing || (snrDb >= StrongSnrDb && Voicing >= MinVoicing * .7));
            history = ((history << 1) | (Likely ? 1 : 0)) & ((1 << AttackWindow) - 1);
            int recent = 0;
            for (int i = 0; i < AttackWindow; i++) recent += (history >> i) & 1;
            if (recent >= AttackFrames || (Active && Likely))
            {
                Active = true;
                hang = hangoverFrames;
            }
            else if (Active && --hang <= 0) Active = false;
            return Active;
        }

        float Periodicity(float[] frame, int offset, int count)
        {
            Remember(frame, offset, count);
            int n = past.Length, length = Math.Min(count, n - maxLag), start = n - length;
            if (length < 8) return 0f;
            double energy = 0;
            for (int i = 0; i < length; i += step) energy += past[start + i] * (double)past[start + i];
            if (energy <= 1e-12) return 0f;
            double best = 0;
            for (int lag = minLag; lag <= maxLag; lag += step)
            {
                double cross = 0, other = 0;
                int from = start - lag;
                for (int i = 0; i < length; i += step)
                {
                    double a = past[start + i], b = past[from + i];
                    cross += a * b;
                    other += b * b;
                }
                if (other <= 1e-12) continue;
                double score = cross / Math.Sqrt(energy * other);
                if (score > best) best = score;
            }
            return (float)best;
        }

        void Remember(float[] frame, int offset, int count)
        {
            if (count >= past.Length)
            {
                Array.Copy(frame, offset + count - past.Length, past, 0, past.Length);
                return;
            }
            Array.Copy(past, count, past, 0, past.Length - count);
            Array.Copy(frame, offset, past, past.Length - count, count);
        }

        static float Burst(float[] frame, int offset, int count)
        {
            int block = Math.Max(1, count / Blocks);
            double total = 0, peak = 0;
            for (int b = 0; b < Blocks; b++)
            {
                double sum = 0;
                for (int i = b * block; i < (b + 1) * block && i < count; i++) sum += frame[offset + i] * (double)frame[offset + i];
                total += sum;
                if (sum > peak) peak = sum;
            }
            return total <= 1e-12 ? 0f : (float)(peak / total);
        }
    }

    public sealed class VoiceAutoGain
    {
        public const double TargetDb = -20, MinGainDb = -20, MaxGainDb = 30, DownDbPerSecond = 30, UpDbPerSecond = 12, Follow = .1;

        readonly double downStep, upStep;
        double level = Math.Pow(10, TargetDb / 10), gainDb;
        float applied = 1f;

        public VoiceAutoGain(int sampleRate)
        {
            downStep = DownDbPerSecond / VoiceFormat.FramesPerSecond;
            upStep = UpDbPerSecond / VoiceFormat.FramesPerSecond;
        }

        public bool Enabled { get; set; } = true;
        public double GainDb => gainDb;
        public double LevelDb => 10 * Math.Log10(Math.Max(1e-12, level));

        public void Reset()
        {
            level = Math.Pow(10, TargetDb / 10);
            gainDb = 0;
            applied = 1f;
        }

        public void Process(float[] frame, int offset, int count, bool speech)
        {
            if (speech && Enabled)
            {
                float rms = VoiceFormat.Rms(frame, offset, count);
                level += Follow * (rms * (double)rms - level);
                double want = Math.Max(MinGainDb, Math.Min(MaxGainDb, TargetDb - LevelDb));
                gainDb = want < gainDb ? Math.Max(want, gainDb - downStep) : Math.Min(want, gainDb + upStep);
            }
            float target = Enabled ? (float)Math.Pow(10, gainDb / 20) : 1f, start = applied;
            for (int i = 0; i < count; i++)
                frame[offset + i] *= start + (target - start) * ((i + 1) / (float)count);
            applied = target;
        }
    }
}
