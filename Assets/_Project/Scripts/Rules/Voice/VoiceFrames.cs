using System;
using System.Diagnostics;

namespace Wreckabulary.Rules.Voice
{
    [Flags]
    public enum VoiceFrameFlags : byte { None = 0, Start = 1, End = 2 }

    public enum VoiceFrameKind : byte { Silence, Data, Recovered, Concealed, Comfort }

    public interface IVoiceFec
    {
        bool Recover(ushort missing, byte[] next, int nextLength, float[] output, int offset, int count);
    }

    public static class VoiceFormat
    {
        public const int DefaultRate = 48000, FrameMs = 20, FramesPerSecond = 1000 / FrameMs, MaxFecBytes = 512;

        public static int FrameSamples(int sampleRate) => Math.Max(1, ValidRate(sampleRate) / FramesPerSecond);

        public static int ValidRate(int sampleRate) => sampleRate < 8000 ? 8000 : sampleRate > 96000 ? 96000 : sampleRate;

        public static int Diff(ushort a, ushort b) => (short)(ushort)(a - b);

        public static ushort Next(ushort sequence, int steps = 1) => (ushort)(sequence + steps);

        public static float Rms(float[] samples, int offset, int count)
        {
            if (samples == null || count <= 0) return 0f;
            double sum = 0;
            for (int i = 0; i < count; i++)
            {
                float x = samples[offset + i];
                sum += x * x;
            }
            return (float)Math.Sqrt(sum / count);
        }

        public static float Decibels(double linear) => linear <= 1e-10 ? -200f : (float)(20 * Math.Log10(linear));

        public static float Linear(double decibels) => (float)Math.Pow(10, decibels / 20);
    }

    public static class VoiceClock
    {
        static readonly double Tick = 1.0 / Stopwatch.Frequency;

        public static double Now => Stopwatch.GetTimestamp() * Tick;
    }

    public struct VoiceRandom
    {
        uint state;

        public VoiceRandom(uint seed) => state = seed == 0 ? 0x9e3779b9u : seed;

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        public double NextDouble() => (NextUInt() >> 8) * (1.0 / 16777216.0);

        public float Signed() => (float)(NextDouble() * 2 - 1);
    }
}
