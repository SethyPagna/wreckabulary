using System;

namespace Wreckabulary.Rules.Voice
{
    public sealed class VoiceNoiseSuppressor
    {
        public const double DefaultFloorDb = -20, WindowSeconds = 1.5, BandLow = 250, BandHigh = 4000;
        const int Subwindows = 5;
        const double Smoothing = .8, Directed = .98, Bias = 2.0, Oversubtract = 3.0;

        readonly int size, hop, bins, perSub, bandLow, bandHigh;
        readonly VoiceFft fft;
        readonly double[] window, re, im, power, smooth, noise, current, gainPrevious, posteriorPrevious;
        readonly double[][] stored;
        readonly float[] input, output, queue;
        readonly double floorGain;
        int filled, read, hops, subHops, subIndex, storedCount;
        double bandSignal, bandNoise;

        public VoiceNoiseSuppressor(int sampleRate, double floorDb = DefaultFloorDb)
        {
            int rate = VoiceFormat.ValidRate(sampleRate);
            size = rate >= 44100 ? 1024 : rate >= 22050 ? 512 : 256;
            hop = size / 2;
            bins = size / 2 + 1;
            fft = new VoiceFft(size);
            window = new double[size];
            for (int i = 0; i < size; i++) window[i] = Math.Sin(Math.PI * i / size);
            re = new double[size];
            im = new double[size];
            power = new double[bins];
            smooth = new double[bins];
            noise = new double[bins];
            current = new double[bins];
            gainPrevious = new double[bins];
            posteriorPrevious = new double[bins];
            stored = new double[Subwindows][];
            for (int i = 0; i < Subwindows; i++) stored[i] = new double[bins];
            input = new float[size];
            output = new float[size];
            queue = new float[hop];
            perSub = Math.Max(1, (int)Math.Round(WindowSeconds * rate / hop / Subwindows));
            bandLow = Math.Max(1, (int)(BandLow * size / rate));
            bandHigh = Math.Min(bins - 1, (int)(BandHigh * size / rate));
            FloorDb = Math.Min(0, floorDb);
            floorGain = Math.Pow(10, FloorDb / 20);
            Reset();
        }

        public int FftSize => size;
        public int Latency => size;
        public double FloorDb { get; }
        public bool Enabled { get; set; } = true;

        public void Reset()
        {
            Array.Clear(input, 0, size);
            Array.Clear(output, 0, size);
            Array.Clear(queue, 0, hop);
            Array.Clear(smooth, 0, bins);
            Array.Clear(noise, 0, bins);
            for (int i = 0; i < bins; i++)
            {
                gainPrevious[i] = 1;
                posteriorPrevious[i] = 1;
                current[i] = double.MaxValue;
            }
            filled = size - hop;
            read = 0;
            hops = subHops = subIndex = storedCount = 0;
            bandSignal = bandNoise = 0;
        }

        public void Process(float[] samples, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float x = samples[offset + i];
                input[filled++] = float.IsNaN(x) || float.IsInfinity(x) ? 0f : x;
                samples[offset + i] = queue[read++];
                if (filled == size) Block();
            }
        }

        public double TakeBandSnrDb()
        {
            double snr = bandNoise <= 1e-20 ? (bandSignal > 0 ? 60 : 0) : 10 * Math.Log10(Math.Max(1e-6, bandSignal / bandNoise));
            bandSignal = bandNoise = 0;
            return snr;
        }

        void Block()
        {
            for (int i = 0; i < size; i++)
            {
                re[i] = input[i] * window[i];
                im[i] = 0;
            }
            fft.Forward(re, im);
            hops++;
            double signalSum = 0, noiseSum = 0;
            for (int k = 0; k < bins; k++)
            {
                double p = re[k] * re[k] + im[k] * im[k];
                power[k] = p;
                smooth[k] = hops == 1 ? p : Smoothing * smooth[k] + (1 - Smoothing) * p;
                if (smooth[k] < current[k]) current[k] = smooth[k];
            }
            if (++subHops >= perSub)
            {
                Array.Copy(current, stored[subIndex], bins);
                subIndex = (subIndex + 1) % Subwindows;
                if (storedCount < Subwindows) storedCount++;
                for (int k = 0; k < bins; k++) current[k] = smooth[k];
                subHops = 0;
            }
            for (int k = 0; k < bins; k++)
            {
                double low = current[k];
                for (int s = 0; s < storedCount; s++) low = Math.Min(low, stored[s][k]);
                noise[k] = Math.Max(1e-20, low * Bias);
                double posterior = power[k] / (noise[k] * Oversubtract);
                double prior = Directed * gainPrevious[k] * gainPrevious[k] * posteriorPrevious[k] + (1 - Directed) * Math.Max(posterior - 1, 0);
                double gain = Enabled ? Math.Max(floorGain, prior / (1 + prior)) : 1;
                gainPrevious[k] = gain;
                posteriorPrevious[k] = Math.Min(posterior, 1e6);
                if (k >= bandLow && k <= bandHigh)
                {
                    signalSum += power[k];
                    noiseSum += noise[k];
                }
                re[k] *= gain;
                im[k] *= gain;
                if (k > 0 && k < size - k)
                {
                    re[size - k] *= gain;
                    im[size - k] *= gain;
                }
            }
            bandSignal += signalSum;
            bandNoise += noiseSum;
            fft.Inverse(re, im);
            for (int i = 0; i < size; i++) output[i] += (float)(re[i] * window[i]);
            Array.Copy(output, 0, queue, 0, hop);
            Array.Copy(output, hop, output, 0, size - hop);
            Array.Clear(output, size - hop, hop);
            Array.Copy(input, hop, input, 0, size - hop);
            filled = size - hop;
            read = 0;
        }
    }
}
