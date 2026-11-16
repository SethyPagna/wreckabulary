using System;

namespace Wreckabulary.Rules
{
    public sealed class TipRetirement
    {
        public const float Seconds = 20f;
        public const int Matches = 3;
        const int Bits = 2, Most = (1 << Bits) - 1;

        readonly float[] shown;
        readonly int[] lastMatch;
        int match = 1;

        public int Seen { get; private set; }

        public TipRetirement(int tips, int seen = 0)
        {
            if (tips < 1 || tips * Bits > 31) throw new ArgumentOutOfRangeException(nameof(tips));
            shown = new float[tips];
            lastMatch = new int[tips];
            Seen = seen;
        }

        public static int MatchesOf(int seen, int tip) => (seen >> (tip * Bits)) & Most;

        public int MatchesShown(int tip) => MatchesOf(Seen, tip);

        public float SecondsShown(int tip) => shown[tip];

        public void BeginMatch() => match++;

        public bool Spent(int tip) => shown[tip] >= Seconds || (lastMatch[tip] != match && MatchesShown(tip) >= Matches);

        public bool Show(int tip, float seconds)
        {
            if (Spent(tip)) return true;
            if (lastMatch[tip] != match)
            {
                lastMatch[tip] = match;
                int count = Math.Min(MatchesShown(tip) + 1, Most);
                Seen = (Seen & ~(Most << (tip * Bits))) | (count << (tip * Bits));
            }
            if (seconds > 0f) shown[tip] += seconds;
            return shown[tip] >= Seconds;
        }

        public void Clear()
        {
            Array.Clear(shown, 0, shown.Length);
            Array.Clear(lastMatch, 0, lastMatch.Length);
            Seen = 0;
        }
    }
}
