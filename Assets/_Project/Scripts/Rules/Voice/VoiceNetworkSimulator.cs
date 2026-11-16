using System;

namespace Wreckabulary.Rules.Voice
{
    public sealed class VoiceNetworkProfile
    {
        public double LossPercent { get; set; }
        public double BurstLossPercent { get; set; }
        public double MeanBurstFrames { get; set; } = 4;
        public double JitterMs { get; set; }
        public double ReorderPercent { get; set; }
        public double DuplicatePercent { get; set; }
        public double BaseLatencyMs { get; set; } = 40;
        public double DriftPercent { get; set; }
        public uint Seed { get; set; } = 1;

        public static VoiceNetworkProfile Clean => new VoiceNetworkProfile();

        public static VoiceNetworkProfile Bad => new VoiceNetworkProfile
        {
            LossPercent = 5, BurstLossPercent = 6, MeanBurstFrames = 3, JitterMs = 60, ReorderPercent = 3, DuplicatePercent = 1, Seed = 7,
        };

        public VoiceNetworkProfile Clone() => (VoiceNetworkProfile)MemberwiseClone();
    }

    public sealed class VoiceNetworkSimulator
    {
        readonly VoicePacket[] pool;
        readonly double[] due;
        readonly long[] order;
        readonly int[] heap, free;
        readonly double frameSeconds;
        VoiceNetworkProfile profile;
        VoiceRandom random;
        int count, freeCount;
        long stamp;
        bool bad;

        public VoiceNetworkSimulator(int frameSamples, VoiceNetworkProfile profile = null, int capacity = 256, int sampleRate = VoiceFormat.DefaultRate)
        {
            capacity = Math.Max(4, capacity);
            pool = new VoicePacket[capacity];
            for (int i = 0; i < capacity; i++) pool[i] = new VoicePacket(frameSamples);
            due = new double[capacity];
            order = new long[capacity];
            heap = new int[capacity];
            free = new int[capacity];
            frameSeconds = frameSamples / (double)VoiceFormat.ValidRate(sampleRate);
            Profile = profile;
            Clear();
        }

        public VoiceNetworkProfile Profile
        {
            get => profile;
            set
            {
                profile = (value ?? VoiceNetworkProfile.Clean).Clone();
                random = new VoiceRandom(profile.Seed);
                bad = false;
            }
        }

        public long Sent { get; private set; }
        public long Lost { get; private set; }
        public long Duplicated { get; private set; }
        public long Reordered { get; private set; }
        public long Delivered { get; private set; }
        public long Overflow { get; private set; }
        public int Pending => count;

        public void Clear()
        {
            count = 0;
            freeCount = pool.Length;
            for (int i = 0; i < pool.Length; i++) free[i] = pool.Length - 1 - i;
        }

        public void Send(ushort sequence, uint timestamp, float[] samples, int offset, VoiceFrameFlags flags, double now,
            byte[] fec = null, int fecLength = 0)
        {
            Sent++;
            if (Drops())
            {
                Lost++;
                return;
            }
            double delay = Math.Max(0, profile.BaseLatencyMs) / 1000.0 + random.NextDouble() * Math.Max(0, profile.JitterMs) / 1000.0;
            if (Chance(profile.ReorderPercent))
            {
                delay += frameSeconds + .001;
                Reordered++;
            }
            Enqueue(sequence, timestamp, samples, offset, flags, now + delay, fec, fecLength);
            if (Chance(profile.DuplicatePercent))
            {
                Enqueue(sequence, timestamp, samples, offset, flags, now + delay + .001 + random.NextDouble() * .03, fec, fecLength);
                Duplicated++;
            }
        }

        public int Deliver(double now, VoiceReceiver receiver)
        {
            int delivered = 0;
            while (count > 0 && due[heap[0]] <= now)
            {
                int slot = Pop();
                var packet = pool[slot];
                if (receiver == null || receiver.Push(packet.Sequence, packet.Timestamp, packet.Samples, 0, packet.Flags, due[slot],
                        packet.Fec, packet.FecLength))
                {
                    delivered++;
                    Delivered++;
                }
                free[freeCount++] = slot;
            }
            return delivered;
        }

        bool Drops()
        {
            double burst = Math.Max(0, Math.Min(95, profile.BurstLossPercent)) / 100.0;
            if (burst > 0)
            {
                double leave = 1.0 / Math.Max(1, profile.MeanBurstFrames), enter = burst * leave / (1 - burst);
                bad = bad ? random.NextDouble() >= leave : random.NextDouble() < enter;
            }
            else bad = false;
            bool independent = Chance(profile.LossPercent);
            return bad || independent;
        }

        bool Chance(double percent) => percent > 0 && random.NextDouble() * 100 < percent;

        void Enqueue(ushort sequence, uint timestamp, float[] samples, int offset, VoiceFrameFlags flags, double at, byte[] fec, int fecLength)
        {
            if (freeCount == 0)
            {
                Overflow++;
                return;
            }
            int slot = free[--freeCount];
            pool[slot].Set(sequence, timestamp, samples, offset, flags, at, fec, fecLength);
            due[slot] = at;
            order[slot] = stamp++;
            int i = count++;
            heap[i] = slot;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (!Before(heap[i], heap[parent])) break;
                (heap[i], heap[parent]) = (heap[parent], heap[i]);
                i = parent;
            }
        }

        int Pop()
        {
            int top = heap[0];
            heap[0] = heap[--count];
            int i = 0;
            while (true)
            {
                int left = 2 * i + 1, right = left + 1, best = i;
                if (left < count && Before(heap[left], heap[best])) best = left;
                if (right < count && Before(heap[right], heap[best])) best = right;
                if (best == i) break;
                (heap[i], heap[best]) = (heap[best], heap[i]);
                i = best;
            }
            return top;
        }

        bool Before(int a, int b) => due[a] < due[b] || (due[a] == due[b] && order[a] < order[b]);
    }
}
