using System;
using System.Collections.Generic;
using System.Threading;

namespace Wreckabulary.Rules.Sound
{
    public readonly struct MixEntry
    {
        public readonly double Start;
        public readonly double End;
        public readonly string Name;
        public readonly bool Admitted;
        public readonly float Gain;

        public MixEntry(double start, double end, string name, bool admitted, float gain)
        {
            Start = start;
            End = end;
            Name = name;
            Admitted = admitted;
            Gain = gain;
        }
    }

    public sealed class MixLog
    {
        readonly MixEntry[] entries;
        int count;

        public MixLog(int capacity = 8192) => entries = new MixEntry[Math.Max(16, capacity)];

        public int Count => count;
        public bool Full => count >= entries.Length;
        public MixEntry this[int index] => entries[index];

        public void Add(double start, double seconds, string name, bool admitted, float gain)
        {
            if (count >= entries.Length) return;
            entries[count++] = new MixEntry(start, start + Math.Max(0, seconds), name, admitted, gain);
        }

        public void Clear() => count = 0;

        public List<(double start, double end)> Spans(bool admittedOnly)
        {
            var spans = new List<(double, double)>(count);
            for (int i = 0; i < count; i++)
                if (!admittedOnly || entries[i].Admitted) spans.Add((entries[i].Start, entries[i].End));
            return spans;
        }
    }

    public static class Overlap
    {
        public static int Max(IReadOnlyList<(double start, double end)> spans)
        {
            int best = 0, now = 0;
            foreach (var (_, delta) in Events(spans))
            {
                now += delta;
                if (now > best) best = now;
            }
            return best;
        }

        public static double ShareAbove(IReadOnlyList<(double start, double end)> spans, int threshold, double from, double to)
        {
            if (!(to > from)) return 0;
            double above = 0, previous = from;
            int now = 0;
            foreach (var (time, delta) in Events(spans))
            {
                double t = Math.Max(from, Math.Min(to, time));
                if (now > threshold) above += t - previous;
                previous = t;
                now += delta;
            }
            if (now > threshold) above += to - previous;
            return above / (to - from);
        }

        static List<(double time, int delta)> Events(IReadOnlyList<(double start, double end)> spans)
        {
            var events = new List<(double, int)>(spans.Count * 2);
            foreach (var (start, end) in spans)
            {
                if (!(end > start)) continue;
                events.Add((start, 1));
                events.Add((end, -1));
            }
            events.Sort((a, b) => a.Item1 != b.Item1 ? a.Item1.CompareTo(b.Item1) : a.Item2.CompareTo(b.Item2));
            return events;
        }
    }

    public sealed class MixRecorder
    {
        readonly float[] buffer;
        int written;

        public MixRecorder(int capacitySamples) => buffer = new float[Math.Max(1024, capacitySamples)];

        public int Channels { get; private set; }
        public int Written => Volatile.Read(ref written);
        public bool Full => Written >= buffer.Length;

        public void Write(float[] data, int channels)
        {
            if (data == null || channels <= 0) return;
            if (Channels == 0) Channels = channels;
            if (channels != Channels) return;
            int at = Volatile.Read(ref written);
            int n = Math.Min(data.Length, buffer.Length - at);
            if (n <= 0) return;
            Array.Copy(data, 0, buffer, at, n);
            Volatile.Write(ref written, at + n);
        }

        public float[] ToArray()
        {
            int n = Written;
            var copy = new float[n];
            Array.Copy(buffer, copy, n);
            return copy;
        }
    }
}
