using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public readonly struct Srgb
    {
        public readonly float R, G, B;
        public Srgb(float r, float g, float b) { R = r; G = g; B = b; }
        public static Srgb Of(Colourway colour) => new Srgb(colour.R, colour.G, colour.B);
    }

    public static class SeatLooks
    {
        public const double SeatGap = 20, OtherSeatGap = 12;

        public static double DeltaE76(Srgb a, Srgb b)
        {
            var (l1, a1, b1) = Lab(a);
            var (l2, a2, b2) = Lab(b);
            return Math.Sqrt((l1 - l2) * (l1 - l2) + (a1 - a2) * (a1 - a2) + (b1 - b2) * (b1 - b2));
        }

        public static (double L, double A, double B) Lab(Srgb c)
        {
            double r = Linear(c.R), g = Linear(c.G), b = Linear(c.B);
            double x = (r * .4124 + g * .3576 + b * .1805) / .95047;
            double y = r * .2126 + g * .7152 + b * .0722;
            double z = (r * .0193 + g * .1192 + b * .9505) / 1.08883;
            double fx = F(x), fy = F(y), fz = F(z);
            return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
        }

        static double Linear(double v) => v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4);
        static double F(double t) => t > .008856 ? Math.Pow(t, 1.0 / 3) : 7.787 * t + 16.0 / 116;

        public static IReadOnlyList<string> TopColours(IReadOnlyList<Colourway> palette, IReadOnlyList<Srgb> seats, IEnumerable<string> allowed)
        {
            var free = new HashSet<string>(allowed ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var options = palette.Where(c => free.Contains(c.Id)).ToList();
            if (options.Count == 0 || seats.Count == 0) return Array.Empty<string>();
            var best = new int[seats.Count];
            var current = new int[seats.Count];
            double bestCost = double.PositiveInfinity;
            Search(0, 0, options, seats, current, ref best, ref bestCost);
            if (double.IsPositiveInfinity(bestCost))
                return seats.Select(seat => options.OrderBy(c => DeltaE76(Srgb.Of(c), seat)).First().Id).ToArray();
            return best.Select(i => options[i].Id).ToArray();
        }

        static void Search(int seat, double cost, List<Colourway> options, IReadOnlyList<Srgb> seats, int[] current, ref int[] best, ref double bestCost)
        {
            if (cost >= bestCost) return;
            if (seat == seats.Count)
            {
                bestCost = cost;
                best = (int[])current.Clone();
                return;
            }
            for (int i = 0; i < options.Count; i++)
            {
                var colour = Srgb.Of(options[i]);
                bool fits = true;
                for (int other = 0; other < seats.Count && fits; other++)
                    if (other != seat && DeltaE76(colour, seats[other]) < OtherSeatGap) fits = false;
                for (int earlier = 0; earlier < seat && fits; earlier++)
                    if (current[earlier] == i || DeltaE76(colour, Srgb.Of(options[current[earlier]])) < SeatGap) fits = false;
                if (!fits) continue;
                current[seat] = i;
                Search(seat + 1, cost + DeltaE76(colour, seats[seat]), options, seats, current, ref best, ref bestCost);
            }
        }
    }
}
