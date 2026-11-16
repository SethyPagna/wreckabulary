using System;

namespace Wreckabulary.Rules.Voice
{
    public sealed class VoiceJitterOptions
    {
        public double StartDelayMs { get; set; } = 60;
        public double MinDelayMs { get; set; } = 40;
        public double MaxDelayMs { get; set; } = 200;
        public double MarginMs { get; set; } = 10;
        public double ShrinkMsPerSecond { get; set; } = 8;
        public double FloorRiseMsPerSecond { get; set; } = 5;
        public double Percentile { get; set; } = .95;
        public double Forget { get; set; } = .995;
        public double IdleAfterMs { get; set; } = 400;
        public float QuietRms { get; set; } = .01f;
        public int Capacity { get; set; } = 64;
    }

    public enum VoiceInsert { Stored, Duplicate, Late, Resync }

    public sealed class VoiceJitterBuffer
    {
        public const int Bins = 100;
        public const double BinSeconds = .004;
        const int SeenSize = 256;

        readonly int capacity, frameSamples, rate;
        readonly float[][] frames;
        readonly ushort[] sequences;
        readonly long[] stamps;
        readonly bool[] present;
        readonly VoiceFrameFlags[] flags;
        readonly float[] levels;
        readonly byte[][] fec;
        readonly int[] fecLength;
        readonly int[] seen = new int[SeenSize];
        readonly double[] histogram = new double[Bins];
        readonly double minTarget, maxTarget, startTarget, marginBase, shrink, floorRise, percentile, forget, frameSeconds;

        ushort next, newest, stampSequence;
        long stampReference;
        uint lastRaw;
        long lastExtended;
        bool anchored, hasFloor, hasPrevious, hasStamp, hasRaw;
        double floor, lastArrival, previousTransit, histogramTotal, lastTargetUpdate, chunkSeconds;

        public VoiceJitterBuffer(int sampleRate, VoiceJitterOptions options = null)
        {
            options ??= new VoiceJitterOptions();
            rate = VoiceFormat.ValidRate(sampleRate);
            frameSamples = VoiceFormat.FrameSamples(rate);
            frameSeconds = frameSamples / (double)rate;
            capacity = Math.Max(8, Math.Min(512, options.Capacity));
            frames = new float[capacity][];
            fec = new byte[capacity][];
            for (int i = 0; i < capacity; i++)
            {
                frames[i] = new float[frameSamples];
                fec[i] = new byte[VoiceFormat.MaxFecBytes];
            }
            sequences = new ushort[capacity];
            stamps = new long[capacity];
            present = new bool[capacity];
            flags = new VoiceFrameFlags[capacity];
            levels = new float[capacity];
            fecLength = new int[capacity];
            maxTarget = Math.Max(frameSeconds, (options.MaxDelayMs - VoiceFormat.FrameMs) / 1000.0);
            minTarget = Math.Min(maxTarget, Math.Max(0, options.MinDelayMs / 1000.0));
            startTarget = Clamp(options.StartDelayMs / 1000.0, minTarget, maxTarget);
            MaxDelaySeconds = Math.Max(maxTarget, options.MaxDelayMs / 1000.0);
            marginBase = Math.Max(0, options.MarginMs / 1000.0);
            shrink = Math.Max(0, options.ShrinkMsPerSecond / 1000.0);
            floorRise = Math.Max(0, options.FloorRiseMsPerSecond / 1000.0);
            percentile = Clamp(options.Percentile, .5, .999);
            forget = Clamp(options.Forget, .9, .9999);
            chunkSeconds = frameSeconds;
            Reset();
        }

        public int Capacity => capacity;
        public int FrameSamples => frameSamples;
        public int SampleRate => rate;
        public ushort Next => next;
        public bool Anchored => anchored;
        public int Count { get; private set; }
        public bool Rewinding { get; set; }
        public double TargetSeconds { get; private set; }
        public double MaxDelaySeconds { get; }
        public double JitterSeconds { get; private set; }
        public double LastRelativeSeconds { get; private set; }
        public bool HasFloor => hasFloor;
        public long Received { get; private set; }
        public long Duplicates { get; private set; }
        public long Late { get; private set; }
        public long Resyncs { get; private set; }

        public double ChunkSeconds
        {
            get => chunkSeconds;
            set => chunkSeconds = double.IsNaN(value) ? frameSeconds : Math.Max(0, Math.Min(1, value));
        }

        public double MarginSeconds => marginBase + Math.Max(0, chunkSeconds - frameSeconds);

        public void Reset()
        {
            for (int i = 0; i < capacity; i++) present[i] = false;
            for (int i = 0; i < SeenSize; i++) seen[i] = -1;
            Array.Clear(histogram, 0, Bins);
            histogramTotal = 0;
            Count = 0;
            anchored = hasFloor = hasPrevious = hasStamp = hasRaw = false;
            lastRaw = 0;
            lastExtended = stampReference = 0;
            next = newest = 0;
            floor = lastArrival = previousTransit = lastTargetUpdate = 0;
            JitterSeconds = LastRelativeSeconds = 0;
            TargetSeconds = startTarget;
            Rewinding = false;
        }

        public void Flush()
        {
            for (int i = 0; i < capacity; i++) present[i] = false;
            Count = 0;
            anchored = false;
        }

        public VoiceInsert Insert(VoicePacket packet)
        {
            ushort sequence = packet.Sequence;
            int key = sequence & (SeenSize - 1);
            if (seen[key] == sequence) { Duplicates++; return VoiceInsert.Duplicate; }
            seen[key] = sequence;
            long stamp = Unwrap(packet.Timestamp);
            Measure(packet.Arrival, stamp);
            if (!anchored) Anchor(sequence);
            int ahead = VoiceFormat.Diff(sequence, next);
            if (ahead < 0)
            {
                if (Rewinding && -ahead < capacity && (Count == 0 || VoiceFormat.Diff(newest, sequence) < capacity))
                {
                    next = sequence;
                    ahead = 0;
                }
                else
                {
                    Late++;
                    Grow(LastRelativeSeconds + MarginSeconds);
                    return VoiceInsert.Late;
                }
            }
            var result = VoiceInsert.Stored;
            if (ahead >= capacity)
            {
                Flush();
                Anchor(sequence);
                Resyncs++;
                result = VoiceInsert.Resync;
            }
            int slot = Slot(sequence);
            Array.Copy(packet.Samples, 0, frames[slot], 0, frameSamples);
            sequences[slot] = sequence;
            stamps[slot] = stamp;
            flags[slot] = packet.Flags;
            levels[slot] = VoiceFormat.Rms(packet.Samples, 0, frameSamples);
            fecLength[slot] = packet.FecLength;
            if (packet.FecLength > 0) Array.Copy(packet.Fec, 0, fec[slot], 0, packet.FecLength);
            present[slot] = true;
            Count++;
            if (!hasStamp) Remember(sequence, stamp);
            if (Count == 1 || VoiceFormat.Diff(sequence, newest) > 0) newest = sequence;
            Received++;
            return result;
        }

        public bool Has(ushort sequence)
        {
            int slot = Slot(sequence);
            return present[slot] && sequences[slot] == sequence;
        }

        public VoiceFrameFlags FlagsOf(ushort sequence) => Has(sequence) ? flags[Slot(sequence)] : VoiceFrameFlags.None;

        public float LevelOf(ushort sequence) => Has(sequence) ? levels[Slot(sequence)] : 0f;

        public int Distance()
        {
            if (Count == 0) return -1;
            for (int d = 0; d < capacity; d++)
                if (Has(VoiceFormat.Next(next, d))) return d;
            return -1;
        }

        public void SeekNearest()
        {
            int d = Distance();
            if (d > 0) next = VoiceFormat.Next(next, d);
        }

        public bool Take(float[] into, out VoiceFrameFlags frameFlags, out float level)
        {
            frameFlags = VoiceFrameFlags.None;
            level = 0f;
            if (!Has(next)) return false;
            int slot = Slot(next);
            Array.Copy(frames[slot], 0, into, 0, frameSamples);
            frameFlags = flags[slot];
            level = levels[slot];
            Remember(next, stamps[slot]);
            present[slot] = false;
            Count--;
            next = VoiceFormat.Next(next);
            return true;
        }

        public void Skip()
        {
            if (Has(next))
            {
                int slot = Slot(next);
                Remember(next, stamps[slot]);
                present[slot] = false;
                Count--;
            }
            next = VoiceFormat.Next(next);
        }

        public bool Recover(IVoiceFec decoder, float[] into)
        {
            if (decoder == null) return false;
            ushort after = VoiceFormat.Next(next);
            if (!Has(after)) return false;
            int slot = Slot(after);
            if (fecLength[slot] <= 0) return false;
            bool ok;
            try { ok = decoder.Recover(next, fec[slot], fecLength[slot], into, 0, frameSamples); }
            catch (Exception) { ok = false; }
            return ok;
        }

        public double StampSeconds(ushort sequence)
        {
            if (Has(sequence)) return stamps[Slot(sequence)] / (double)rate;
            if (!hasStamp) return 0;
            return (stampReference + (long)VoiceFormat.Diff(sequence, stampSequence) * frameSamples) / (double)rate;
        }

        public double DelayAt(double now) => hasFloor ? now - StampSeconds(next) - floor : 0;

        public void Calm(double now)
        {
            double want = Clamp(Percentile() + MarginSeconds, minTarget, maxTarget);
            double elapsed = Math.Max(0, now - lastTargetUpdate);
            lastTargetUpdate = Math.Max(lastTargetUpdate, now);
            if (want >= TargetSeconds) TargetSeconds = want;
            else TargetSeconds = Math.Max(want, TargetSeconds - shrink * elapsed);
        }

        public double Percentile()
        {
            if (histogramTotal <= 0) return 0;
            double wanted = histogramTotal * percentile, sum = 0;
            for (int i = 0; i < Bins; i++)
            {
                sum += histogram[i];
                if (sum >= wanted) return (i + 1) * BinSeconds;
            }
            return Bins * BinSeconds;
        }

        void Grow(double seconds)
        {
            double want = Clamp(seconds, minTarget, maxTarget);
            if (want > TargetSeconds) TargetSeconds = want;
        }

        void Measure(double arrival, long stamp)
        {
            double transit = arrival - stamp / (double)rate;
            if (!hasFloor)
            {
                floor = transit;
                lastArrival = arrival;
                lastTargetUpdate = arrival;
                hasFloor = true;
            }
            else
            {
                double elapsed = Math.Max(0, arrival - lastArrival);
                if (arrival > lastArrival) lastArrival = arrival;
                floor = Math.Min(floor + floorRise * elapsed, transit);
            }
            if (hasPrevious) JitterSeconds += (Math.Abs(transit - previousTransit) - JitterSeconds) / 16.0;
            previousTransit = transit;
            hasPrevious = true;
            double relative = Math.Max(0, transit - floor);
            LastRelativeSeconds = relative;
            int bin = Math.Min(Bins - 1, (int)(relative / BinSeconds));
            for (int i = 0; i < Bins; i++) histogram[i] *= forget;
            histogram[bin] += 1 - forget;
            histogramTotal = histogramTotal * forget + (1 - forget);
            Calm(arrival);
        }

        long Unwrap(uint raw)
        {
            if (!hasRaw)
            {
                hasRaw = true;
                lastRaw = raw;
                lastExtended = raw;
                return raw;
            }
            int step = (int)(raw - lastRaw);
            long extended = lastExtended + step;
            if (step > 0)
            {
                lastRaw = raw;
                lastExtended = extended;
            }
            return extended;
        }

        void Remember(ushort sequence, long stamp)
        {
            stampSequence = sequence;
            stampReference = stamp;
            hasStamp = true;
        }

        void Anchor(ushort sequence)
        {
            next = sequence;
            newest = sequence;
            anchored = true;
        }

        int Slot(ushort sequence) => sequence % capacity;

        static double Clamp(double value, double min, double max) => value < min ? min : value > max ? max : value;
    }
}
