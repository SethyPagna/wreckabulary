using System;

namespace Wreckabulary.Rules.Sound
{
    public enum CuePriority { Low, Normal, High }

    public sealed class CueGate
    {
        readonly double[] ends;
        readonly int[] owners;
        readonly double[] lastStart;

        public int Capacity => ends.Length;
        public int PerCue { get; }
        public double Cooldown { get; }
        public double BusySeconds { get; }

        public CueGate(int cues, int capacity, int perCue, double cooldown, double busySeconds = .25)
        {
            if (cues <= 0) throw new ArgumentOutOfRangeException(nameof(cues));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            ends = new double[capacity];
            owners = new int[capacity];
            lastStart = new double[cues];
            PerCue = Math.Max(1, perCue);
            Cooldown = Math.Max(0, cooldown);
            BusySeconds = Math.Max(.01, busySeconds);
            Clear();
        }

        public static int CapFor(int capacity, CuePriority priority) =>
            Math.Max(1, capacity - (CuePriority.High - (CuePriority)Math.Max(0, Math.Min(2, (int)priority))));

        public void Clear()
        {
            for (int i = 0; i < ends.Length; i++) { ends[i] = double.NegativeInfinity; owners[i] = -1; }
            for (int i = 0; i < lastStart.Length; i++) lastStart[i] = double.NegativeInfinity;
        }

        public int Active(double now)
        {
            int n = 0;
            for (int i = 0; i < ends.Length; i++) if (ends[i] > now) n++;
            return n;
        }

        public int ActiveFor(int cue, double now)
        {
            int n = 0;
            for (int i = 0; i < ends.Length; i++) if (ends[i] > now && owners[i] == cue) n++;
            return n;
        }

        public bool CoolingDown(int cue, double now) => cue >= 0 && cue < lastStart.Length && now - lastStart[cue] < Cooldown;

        public bool Admit(int cue, CuePriority priority, double now, double seconds)
        {
            if (cue < 0 || cue >= lastStart.Length || double.IsNaN(now)) return false;
            if (now - lastStart[cue] < Cooldown) return false;
            int total = 0, same = 0, free = -1;
            for (int i = 0; i < ends.Length; i++)
            {
                if (ends[i] > now)
                {
                    total++;
                    if (owners[i] == cue) same++;
                }
                else if (free < 0) free = i;
            }
            if (same >= PerCue || total >= CapFor(ends.Length, priority) || free < 0) return false;
            ends[free] = now + Math.Max(.01, Math.Min(BusySeconds, double.IsNaN(seconds) ? BusySeconds : seconds));
            owners[free] = cue;
            lastStart[cue] = now;
            return true;
        }
    }

    public sealed class SoundJitter
    {
        public const double PitchSpread = .03;
        public const double GainSpreadDb = 1.5;
        public static readonly double[] Pitches = { .97, .99, 1.01, 1.03 };

        uint state;

        public SoundJitter(uint seed) => state = seed == 0 ? 0x2545F491u : seed;

        public double Next01()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return state / 4294967296.0;
        }

        public int Variant(int count, int previous)
        {
            if (count <= 1) return 0;
            int pick = (int)(Next01() * count) % count;
            return pick == previous ? (pick + 1 + (int)(Next01() * (count - 1)) % (count - 1)) % count : pick;
        }

        public double Gain() => GainFor(Next01());

        public static double GainFor(double unit) => Math.Pow(10, (Math.Max(0, Math.Min(1, unit)) * 2 - 1) * GainSpreadDb / 20);

        public static double GainFor(uint hash) => GainFor((hash & 0xFFFF) / 65535.0);

        public static double PitchFor(uint hash) => 1 + ((hash >> 16 & 0xFFFF) / 65535.0 * 2 - 1) * PitchSpread;
    }

    public static class CouchAttenuation
    {
        public const double NearMeters = 6, FarMeters = 24, FarGain = .5;

        public static double Falloff(double meters)
        {
            if (double.IsNaN(meters) || meters <= NearMeters) return 1;
            if (meters >= FarMeters) return FarGain;
            double t = (meters - NearMeters) / (FarMeters - NearMeters);
            return FarGain + (1 - FarGain) * (.5 + .5 * Math.Cos(Math.PI * t));
        }

        public static double FocusDistance(double earX, double earY, double earZ, double forwardX, double forwardY, double forwardZ,
            double x, double y, double z)
        {
            double focusX = earX, focusZ = earZ;
            if (forwardY < -.05)
            {
                double t = (y - earY) / forwardY;
                if (t > 0 && t < 200)
                {
                    focusX = earX + forwardX * t;
                    focusZ = earZ + forwardZ * t;
                }
            }
            double dx = x - focusX, dz = z - focusZ;
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
