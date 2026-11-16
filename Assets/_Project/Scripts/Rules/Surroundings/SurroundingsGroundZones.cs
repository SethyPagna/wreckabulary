using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Surroundings
{
    sealed partial class LayoutBuilder
    {
        public const float FadeStep = .01f, LawnLift = .004f, StripeLift = .008f, SoilLift = .012f;

        List<uint> lawnOrder;
        int lawnNext;

        List<SurroundingsGround> Ground()
        {
            var zones = new List<SurroundingsGround>();
            var g = plan.Ground;
            int fades = g.Fades.Count;
            if (g.Radius > 0) zones.Add(Disc(Centre, g.Radius, g.Slot, g.Tint, g.Y - FadeStep * fades, "ground"));
            for (int k = 0; k < fades; k++)
                zones.Add(Disc(Centre, g.Fades[k].radius, g.Slot, g.Fades[k].tint, g.Y - FadeStep * (fades - 1 - k), $"ground fade {k}"));
            foreach (var z in plan.Zones) AddZone(zones, z);
            if (plan.Lawns != null && plan.Lawns.Tones.Count > 0)
                foreach (var p in plan.Plots) AddLawns(zones, p, plots[p.Id]);
            if (plan.Soil != null) AddSoil(zones);
            return zones.OrderBy(z => z.Y).ToList();
        }

        SurroundingsGround Disc(Flat at, double radius, string slot, uint tint, float y, string source) => new SurroundingsGround
        {
            Shape = SurroundingsGroundShape.Disc, Slot = slot, Tint = tint, Y = Q(y, .001), Source = source,
            CentreX = Q(at.X, .001), CentreZ = Q(at.Z, .001), Radius = Q(radius, .001),
            MinX = Q(at.X - radius, .001), MinZ = Q(at.Z - radius, .001), MaxX = Q(at.X + radius, .001), MaxZ = Q(at.Z + radius, .001),
        };

        SurroundingsGround Patch(Rect r, uint tint, float y, string source) => new SurroundingsGround
        {
            Shape = SurroundingsGroundShape.Rect, Slot = plan.Ground.Slot, Tint = tint, Y = Q(y, .001), Source = source,
            CentreX = Q(r.Centre.X, .001), CentreZ = Q(r.Centre.Z, .001),
            MinX = Q(r.MinX, .001), MinZ = Q(r.MinZ, .001), MaxX = Q(r.MaxX, .001), MaxZ = Q(r.MaxZ, .001),
        };

        void AddZone(List<SurroundingsGround> zones, ZoneSpec z)
        {
            float y = z.Y ?? plan.Ground.Y + LawnLift;
            if (z.Disc)
            {
                zones.Add(Disc(z.Area.Centre, z.Radius, z.Slot ?? plan.Ground.Slot, z.Tint, y, z.Path));
                return;
            }
            var patch = Patch(z.Area, z.Tint, y, z.Path);
            if (z.Slot != null) patch.Slot = z.Slot;
            zones.Add(patch);
            if (!z.Stripes || plan.Lawns == null || plan.Lawns.Stripe <= 0) return;
            var r = z.Area;
            bool alongZ = r.MaxZ - r.MinZ >= r.MaxX - r.MinX;
            double from = alongZ ? r.MinZ : r.MinX, to = alongZ ? r.MaxZ : r.MaxX;
            foreach (var (a, b) in Stripes(from, to))
                zones.Add(Patch(alongZ ? new Rect(r.MinX, a, r.MaxX, b) : new Rect(a, r.MinZ, b, r.MaxZ), Lighter(z.Tint), y + StripeLift - LawnLift, z.Path + " stripe"));
        }

        IEnumerable<(double a, double b)> Stripes(double from, double to)
        {
            double width = plan.Lawns.Stripe;
            for (double a = from + width; a < to - 1e-6; a += 2 * width)
                yield return (a, Math.Min(to, a + width));
        }

        uint NextLawn()
        {
            if (lawnOrder == null)
            {
                lawnOrder = plan.Lawns.Tones.ToList();
                var rng = Random("lawns");
                rng.Shuffle(lawnOrder);
            }
            return lawnOrder[lawnNext++ % lawnOrder.Count];
        }

        uint Lighter(uint rgb)
        {
            double shift = plan.Lawns?.Shift ?? .07;
            uint Channel(int bits) => (uint)Math.Min(255, Math.Round(((rgb >> bits) & 0xFF) * (1 + shift))) << bits;
            return Channel(16) | Channel(8) | Channel(0);
        }

        void AddLawns(List<SurroundingsGround> zones, PlotSpec p, PlotState state)
        {
            var f = state.Frame;
            float y = plan.Ground.Y + LawnLift;
            double split = state.House != null ? p.Setback : 0;
            if (split > 0) AddGarden(zones, f, 0, split, NextLawn(), y, $"lawn {p.Id} front");
            AddGarden(zones, f, split, f.Depth, NextLawn(), y, $"lawn {p.Id} back");
        }

        void AddGarden(List<SurroundingsGround> zones, PlotFrame f, double fromV, double toV, uint tone, float y, string source)
        {
            zones.Add(Patch(Span(f, -f.Width * .5, fromV, f.Width * .5, toV), tone, y, source));
            if (plan.Lawns.Stripe <= 0) return;
            foreach (var (a, b) in Stripes(-f.Width * .5, f.Width * .5))
                zones.Add(Patch(Span(f, a, fromV, b, toV), Lighter(tone), y + StripeLift - LawnLift, source + " stripe"));
        }

        static Rect Span(PlotFrame f, double u0, double v0, double u1, double v1)
        {
            var a = f.At(u0, v0);
            var b = f.At(u1, v1);
            return new Rect(a.X, a.Z, b.X, b.Z);
        }

        void AddSoil(List<SurroundingsGround> zones)
        {
            float y = plan.Ground.Y + SoilLift;
            foreach (var item in items.Where(i => plan.Soil.Under.Contains(i.Piece.Id)))
                zones.Add(Patch(item.Bounds.Grow(plan.Soil.Margin), plan.Soil.Tint, y, "soil " + item.Feature));
        }
    }
}
