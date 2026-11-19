using System;

namespace Wreckabulary.Rules.Voice
{
    public sealed class VoiceConcealer
    {
        public const double HoldSeconds = .01, FadeSeconds = .06, ComfortFromSeconds = .02, ComfortFullSeconds = .06,
            RampSeconds = .004, FadeOutSeconds = .01;
        public const float DefaultComfort = .0005f, MinComfort = .0002f, MaxComfort = .002f, Voiced = .3f;

        readonly int rate, minLag, maxLag, window, ramp, hold, fadeEnd, comfortFrom, comfortFull, fadeOutLength;
        readonly float[] past, loop;
        VoiceRandom random = new VoiceRandom(0x5eed1234u);
        int period, phase, elapsed, fadeOutAt;
        float offset, noise, floorLevel = DefaultComfort, fadePitch, fadeComfort;
        bool fadingOut;

        public VoiceConcealer(int sampleRate)
        {
            rate = VoiceFormat.ValidRate(sampleRate);
            minLag = Math.Max(2, rate / 400);
            maxLag = Math.Max(minLag + 2, rate / 66);
            window = Math.Max(16, rate / 100);
            ramp = Math.Max(1, (int)(rate * RampSeconds));
            hold = (int)(rate * HoldSeconds);
            fadeEnd = (int)(rate * FadeSeconds);
            comfortFrom = (int)(rate * ComfortFromSeconds);
            comfortFull = (int)(rate * ComfortFullSeconds);
            fadeOutLength = Math.Max(1, (int)(rate * FadeOutSeconds));
            past = new float[Math.Max(2 * maxLag, window + maxLag) + 8];
            loop = new float[maxLag + 1];
        }

        public bool Active { get; private set; }
        public bool Comfort => Active && !fadingOut && elapsed >= comfortFull;
        public bool Silent => Active && fadingOut && elapsed - fadeOutAt >= fadeOutLength;
        public int Period => period;
        public float Voicing { get; private set; }
        public float ComfortLevel => floorLevel;
        public int RampSamples => ramp;

        public void Reset()
        {
            Array.Clear(past, 0, past.Length);
            Active = fadingOut = false;
            floorLevel = DefaultComfort;
            noise = 0f;
        }

        public void Remember(float[] samples, int offset, int count)
        {
            if (count >= past.Length)
            {
                Array.Copy(samples, offset + count - past.Length, past, 0, past.Length);
                return;
            }
            Array.Copy(past, count, past, 0, past.Length - count);
            Array.Copy(samples, offset, past, past.Length - count, count);
        }

        public void Floor(float rms)
        {
            if (float.IsNaN(rms) || rms <= 0f) return;
            floorLevel = rms < floorLevel ? rms : floorLevel * 1.002f;
            floorLevel = Math.Max(MinComfort, Math.Min(MaxComfort, floorLevel));
        }

        public void Begin()
        {
            int n = past.Length;
            period = Pitch();
            int k = Math.Max(1, period / 4);
            for (int i = 0; i < period; i++) loop[i] = past[n - period + i];
            for (int i = period - k; i < period; i++)
            {
                float w = (i - (period - k) + 1) / (float)(k + 1);
                loop[i] = (1f - w) * past[n - period + i] + w * past[n - 2 * period + i];
            }
            offset = past[n - 1] - loop[period - 1];
            phase = 0;
            elapsed = 0;
            fadingOut = false;
            Active = true;
        }

        public void FadeOut()
        {
            if (!Active) Begin();
            if (fadingOut) return;
            fadePitch = PitchGain(elapsed);
            fadeComfort = ComfortGain(elapsed);
            fadeOutAt = elapsed;
            fadingOut = true;
        }

        public void Stop()
        {
            Active = false;
            fadingOut = false;
        }

        public void Generate(float[] output, int offset0, int count)
        {
            if (!Active) Begin();
            for (int i = 0; i < count; i++) output[offset0 + i] = NextSample();
        }

        public void BlendInto(float[] frame, int offset0, int count)
        {
            if (!Active) Begin();
            int n = Math.Min(ramp, count);
            for (int i = 0; i < n; i++)
            {
                float w = (i + 1) / (float)(n + 1);
                frame[offset0 + i] = NextSample() * (1f - w) + frame[offset0 + i] * w;
            }
            Stop();
        }

        float NextSample()
        {
            float pitchGain, comfortGain;
            if (fadingOut)
            {
                float left = 1f - Math.Min(1f, (elapsed - fadeOutAt) / (float)fadeOutLength);
                pitchGain = fadePitch * left;
                comfortGain = fadeComfort * left;
            }
            else
            {
                pitchGain = PitchGain(elapsed);
                comfortGain = ComfortGain(elapsed);
            }
            float value = 0f;
            if (pitchGain > 0f)
            {
                value = loop[phase] * pitchGain;
                if (elapsed < ramp) value += offset * (1f - elapsed / (float)ramp) * pitchGain;
            }
            phase = phase + 1 >= period ? 0 : phase + 1;
            if (comfortGain > 0f) value += Noise() * floorLevel * comfortGain;
            if (elapsed < int.MaxValue) elapsed++;
            return value;
        }

        float PitchGain(int at)
        {
            if (at < hold) return 1f;
            if (at >= fadeEnd) return 0f;
            return 1f - (at - hold) / (float)(fadeEnd - hold);
        }

        float ComfortGain(int at)
        {
            if (at <= comfortFrom) return 0f;
            if (at >= comfortFull) return 1f;
            return (at - comfortFrom) / (float)(comfortFull - comfortFrom);
        }

        float Noise()
        {
            float white = random.Signed() * 1.7320508f;
            noise += .25f * (white - noise);
            return noise * 2.6457513f;
        }

        int Pitch()
        {
            int n = past.Length, start = n - window, step = Math.Max(1, rate / 8000);
            double energy = 0;
            for (int i = 0; i < window; i++) energy += past[start + i] * (double)past[start + i];
            if (energy <= 1e-12)
            {
                Voicing = 0f;
                return maxLag;
            }
            int best = maxLag;
            double bestScore = -1;
            for (int lag = minLag; lag <= maxLag; lag += step)
            {
                double score = Score(start, lag, energy);
                if (score > bestScore) { bestScore = score; best = lag; }
            }
            int low = Math.Max(minLag, best - step), high = Math.Min(maxLag, best + step);
            for (int lag = low; lag <= high; lag++)
            {
                double score = Score(start, lag, energy);
                if (score > bestScore) { bestScore = score; best = lag; }
            }
            Voicing = (float)Math.Max(0, bestScore);
            return bestScore < Voiced ? maxLag : best;
        }

        double Score(int start, int lag, double energy)
        {
            double cross = 0, other = 0;
            int from = start - lag;
            for (int i = 0; i < window; i++)
            {
                double a = past[start + i], b = past[from + i];
                cross += a * b;
                other += b * b;
            }
            return other <= 1e-12 ? 0 : cross / Math.Sqrt(energy * other);
        }
    }
}
