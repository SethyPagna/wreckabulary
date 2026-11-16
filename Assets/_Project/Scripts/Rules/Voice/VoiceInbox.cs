using System;
using System.Threading;

namespace Wreckabulary.Rules.Voice
{
    public sealed class VoicePacket
    {
        public ushort Sequence;
        public uint Timestamp;
        public VoiceFrameFlags Flags;
        public double Arrival;
        public readonly float[] Samples;
        public readonly byte[] Fec;
        public int FecLength;

        public VoicePacket(int frameSamples)
        {
            Samples = new float[frameSamples];
            Fec = new byte[VoiceFormat.MaxFecBytes];
        }

        public void Set(ushort sequence, uint timestamp, float[] samples, int offset, VoiceFrameFlags flags, double arrival, byte[] fec, int fecLength)
        {
            Sequence = sequence;
            Timestamp = timestamp;
            Flags = flags;
            Arrival = arrival;
            if (samples == null) Array.Clear(Samples, 0, Samples.Length);
            else Array.Copy(samples, offset, Samples, 0, Samples.Length);
            FecLength = fec == null ? 0 : Math.Max(0, Math.Min(Math.Min(fecLength, fec.Length), Fec.Length));
            if (FecLength > 0) Array.Copy(fec, 0, Fec, 0, FecLength);
        }

        public void CopyFrom(VoicePacket other) =>
            Set(other.Sequence, other.Timestamp, other.Samples, 0, other.Flags, other.Arrival, other.Fec, other.FecLength);
    }

    public sealed class VoiceInbox
    {
        readonly VoicePacket[] slots;
        int written, read, overflow;

        public VoiceInbox(int frameSamples, int capacity = 64)
        {
            slots = new VoicePacket[Math.Max(2, capacity)];
            for (int i = 0; i < slots.Length; i++) slots[i] = new VoicePacket(frameSamples);
        }

        public int Capacity => slots.Length;
        public int Count => Volatile.Read(ref written) - Volatile.Read(ref read);
        public int Overflow => Volatile.Read(ref overflow);

        public bool Write(ushort sequence, uint timestamp, float[] samples, int offset, VoiceFrameFlags flags, double arrival,
            byte[] fec = null, int fecLength = 0)
        {
            int w = written;
            if (w - Volatile.Read(ref read) >= slots.Length)
            {
                Interlocked.Increment(ref overflow);
                return false;
            }
            slots[Index(w)].Set(sequence, timestamp, samples, offset, flags, arrival, fec, fecLength);
            Volatile.Write(ref written, w + 1);
            return true;
        }

        public VoicePacket Peek()
        {
            int r = read;
            return r == Volatile.Read(ref written) ? null : slots[Index(r)];
        }

        public void Pop() => Volatile.Write(ref read, read + 1);

        public void Clear() => Volatile.Write(ref read, Volatile.Read(ref written));

        int Index(int counter) => (int)((uint)counter % (uint)slots.Length);
    }
}
