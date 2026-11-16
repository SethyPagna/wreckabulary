using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Surroundings
{
    readonly struct Flat
    {
        public readonly double X, Z;

        public Flat(double x, double z)
        {
            X = x; Z = z;
        }

        public static Flat operator +(Flat a, Flat b) => new Flat(a.X + b.X, a.Z + b.Z);
        public static Flat operator -(Flat a, Flat b) => new Flat(a.X - b.X, a.Z - b.Z);
        public static Flat operator *(Flat a, double s) => new Flat(a.X * s, a.Z * s);

        public double Dot(Flat o) => X * o.X + Z * o.Z;
        public double Length => Math.Sqrt(X * X + Z * Z);
        public Flat Unit => Length > 1e-12 ? this * (1.0 / Length) : this;
        public Flat Right => new Flat(Z, -X);
        public Flat Left => new Flat(-Z, X);

        public static Flat Heading(double yawDegrees)
        {
            double r = yawDegrees * Math.PI / 180.0;
            return new Flat(Math.Sin(r), Math.Cos(r));
        }

        public double Yaw => Shapes.Wrap360(Math.Atan2(X, Z) * 180.0 / Math.PI);
    }

    readonly struct Rect
    {
        public readonly double MinX, MinZ, MaxX, MaxZ;

        public Rect(double minX, double minZ, double maxX, double maxZ)
        {
            MinX = Math.Min(minX, maxX); MinZ = Math.Min(minZ, maxZ);
            MaxX = Math.Max(minX, maxX); MaxZ = Math.Max(minZ, maxZ);
        }

        public static Rect Of(RoomBox room) => new Rect(room.MinX, room.MinZ, room.MaxX, room.MaxZ);

        public Rect Grow(double by) => new Rect(MinX - by, MinZ - by, MaxX + by, MaxZ + by);
        public bool Contains(Flat p) => p.X >= MinX && p.X <= MaxX && p.Z >= MinZ && p.Z <= MaxZ;
        public bool Overlaps(Rect o) => MinX < o.MaxX && o.MinX < MaxX && MinZ < o.MaxZ && o.MinZ < MaxZ;
        public Flat Centre => new Flat((MinX + MaxX) * .5, (MinZ + MaxZ) * .5);

        public Flat[] Corners() => new[] { new Flat(MinX, MinZ), new Flat(MaxX, MinZ), new Flat(MaxX, MaxZ), new Flat(MinX, MaxZ) };

        public double Distance(Flat p)
        {
            double dx = Math.Max(0, Math.Max(MinX - p.X, p.X - MaxX));
            double dz = Math.Max(0, Math.Max(MinZ - p.Z, p.Z - MaxZ));
            return Math.Sqrt(dx * dx + dz * dz);
        }
    }

    readonly struct Footprint
    {
        public readonly Flat Centre;
        public readonly double HalfWidth, HalfDepth, Yaw;
        readonly Flat across, along;

        public Footprint(double x, double z, double width, double depth, double yaw)
        {
            Centre = new Flat(x, z);
            HalfWidth = width * .5; HalfDepth = depth * .5; Yaw = yaw;
            along = Flat.Heading(yaw);
            across = along.Right;
        }

        public Flat Along => along;
        public Flat Across => across;

        public Flat[] Corners() => new[]
        {
            Centre + across * -HalfWidth + along * -HalfDepth, Centre + across * HalfWidth + along * -HalfDepth,
            Centre + across * HalfWidth + along * HalfDepth, Centre + across * -HalfWidth + along * HalfDepth,
        };

        public Rect Bounds()
        {
            double hx = Math.Abs(across.X) * HalfWidth + Math.Abs(along.X) * HalfDepth;
            double hz = Math.Abs(across.Z) * HalfWidth + Math.Abs(along.Z) * HalfDepth;
            return new Rect(Centre.X - hx, Centre.Z - hz, Centre.X + hx, Centre.Z + hz);
        }

        public bool Contains(Flat p, double slack = 1e-4)
        {
            var d = p - Centre;
            return Math.Abs(d.Dot(across)) <= HalfWidth + slack && Math.Abs(d.Dot(along)) <= HalfDepth + slack;
        }

        public bool Overlaps(Footprint o, double pad = 0) => Shapes.ConvexOverlap(Corners(), o.Corners(), pad);
        public bool Overlaps(Rect r) => Shapes.ConvexOverlap(Corners(), r.Corners(), 0);
        public double Distance(Rect r) => Shapes.ConvexDistance(Corners(), r.Corners());
        public double Distance(Footprint o) => Shapes.ConvexDistance(Corners(), o.Corners());
    }

    static class Shapes
    {
        public static double Wrap360(double degrees)
        {
            double d = degrees % 360.0;
            return d < 0 ? d + 360.0 : d;
        }

        public static bool ConvexOverlap(IReadOnlyList<Flat> a, IReadOnlyList<Flat> b, double pad)
        {
            return !Separated(a, b, a, pad) && !Separated(a, b, b, pad);
        }

        static bool Separated(IReadOnlyList<Flat> a, IReadOnlyList<Flat> b, IReadOnlyList<Flat> edges, double pad)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[(i + 1) % edges.Count] - edges[i];
                if (edge.Length < 1e-12) continue;
                var axis = edge.Left.Unit;
                Project(a, axis, out double aMin, out double aMax);
                Project(b, axis, out double bMin, out double bMax);
                if (aMax + pad <= bMin + 1e-9 || bMax + pad <= aMin + 1e-9) return true;
            }
            return false;
        }

        static void Project(IReadOnlyList<Flat> points, Flat axis, out double min, out double max)
        {
            min = double.MaxValue; max = double.MinValue;
            foreach (var p in points)
            {
                double d = p.Dot(axis);
                if (d < min) min = d;
                if (d > max) max = d;
            }
        }

        public static double ConvexDistance(IReadOnlyList<Flat> a, IReadOnlyList<Flat> b)
        {
            if (ConvexOverlap(a, b, 0)) return 0;
            double best = double.MaxValue;
            for (int i = 0; i < a.Count; i++)
                for (int j = 0; j < b.Count; j++)
                {
                    best = Math.Min(best, PointToSegment(a[i], b[j], b[(j + 1) % b.Count]));
                    best = Math.Min(best, PointToSegment(b[j], a[i], a[(i + 1) % a.Count]));
                }
            return best;
        }

        public static double PointToSegment(Flat p, Flat a, Flat b)
        {
            var ab = b - a;
            double length = ab.Dot(ab);
            double t = length < 1e-12 ? 0 : Math.Max(0, Math.Min(1, (p - a).Dot(ab) / length));
            return (p - (a + ab * t)).Length;
        }

        public static double SegmentToRect(Flat a, Flat b, Rect r) => ConvexDistance(new[] { a, b }, r.Corners());

        public static List<Flat> Hull(IEnumerable<Flat> points)
        {
            var sorted = points.OrderBy(p => p.X).ThenBy(p => p.Z).ToList();
            if (sorted.Count < 3) return sorted;
            var hull = new List<Flat>();
            for (int pass = 0; pass < 2; pass++)
            {
                int start = hull.Count;
                foreach (var p in sorted)
                {
                    while (hull.Count >= start + 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0) hull.RemoveAt(hull.Count - 1);
                    hull.Add(p);
                }
                hull.RemoveAt(hull.Count - 1);
                sorted.Reverse();
            }
            return hull;
        }

        static double Cross(Flat o, Flat a, Flat b) => (a.X - o.X) * (b.Z - o.Z) - (a.Z - o.Z) * (b.X - o.X);

        public static List<Flat> SweptNorth(Footprint f, double reach)
        {
            var corners = f.Corners();
            return Hull(corners.Concat(corners.Select(c => c + new Flat(0, reach))));
        }
    }

    sealed class FenceRun
    {
        public Flat Start, End, Normal;
        public bool StartConvex, EndConvex;
        public string Side;

        public Flat Direction => (End - Start).Unit;
        public double Length => (End - Start).Length;
    }

    static class Outline
    {
        const double Eps = 1e-3;

        public static List<FenceRun> Fences(HouseLayout house)
        {
            float ground = house.Rooms.Count == 0 ? 0f : house.Rooms.Min(r => r.FloorY);
            var all = house.Rooms.Select(Rect.Of).ToList();
            var runs = new List<FenceRun>();
            foreach (var room in house.Rooms)
            {
                if (!room.Outdoor || Math.Abs(room.FloorY - ground) >= RoomBox.StoreyTolerance) continue;
                var r = Rect.Of(room);
                AddSide(runs, all, r.MinX, r.MaxX, r.MaxZ, new Flat(0, 1), "north");
                AddSide(runs, all, r.MinX, r.MaxX, r.MinZ, new Flat(0, -1), "south");
                AddSide(runs, all, r.MinZ, r.MaxZ, r.MaxX, new Flat(1, 0), "east");
                AddSide(runs, all, r.MinZ, r.MaxZ, r.MinX, new Flat(-1, 0), "west");
            }
            var merged = Merge(runs);
            MarkCorners(merged);
            return merged;
        }

        static void AddSide(List<FenceRun> runs, List<Rect> rooms, double from, double to, double line, Flat normal, string side)
        {
            bool alongX = normal.X == 0;
            var open = new List<(double a, double b)> { (from, to) };
            foreach (var o in rooms)
            {
                var probe = alongX ? new Flat((from + to) * .5, line + normal.Z * Eps) : new Flat(line + normal.X * Eps, (from + to) * .5);
                bool beyond = alongX ? probe.Z > o.MinZ && probe.Z < o.MaxZ : probe.X > o.MinX && probe.X < o.MaxX;
                if (!beyond) continue;
                open = Subtract(open, alongX ? o.MinX : o.MinZ, alongX ? o.MaxX : o.MaxZ);
            }
            foreach (var (a, b) in open)
            {
                if (b - a < Eps * 10) continue;
                var start = alongX ? new Flat(a, line) : new Flat(line, a);
                var end = alongX ? new Flat(b, line) : new Flat(line, b);
                runs.Add(new FenceRun { Start = start, End = end, Normal = normal, Side = side });
            }
        }

        static List<(double, double)> Subtract(List<(double a, double b)> intervals, double cutFrom, double cutTo)
        {
            var result = new List<(double, double)>();
            foreach (var (a, b) in intervals)
            {
                if (cutTo <= a || cutFrom >= b) { result.Add((a, b)); continue; }
                if (cutFrom > a) result.Add((a, cutFrom));
                if (cutTo < b) result.Add((cutTo, b));
            }
            return result;
        }

        public static List<(double from, double to)> Without(List<(double from, double to)> intervals, double cutFrom, double cutTo) =>
            Subtract(intervals, cutFrom, cutTo);

        static List<FenceRun> Merge(List<FenceRun> runs)
        {
            var result = new List<FenceRun>();
            foreach (var group in runs.GroupBy(r => (r.Side, Line: Math.Round(r.Normal.X != 0 ? r.Start.X : r.Start.Z, 3))).OrderBy(g => SideOrder(g.Key.Side)).ThenBy(g => g.Key.Line))
            {
                bool alongX = group.First().Normal.X == 0;
                var spans = group.Select(r => alongX ? (a: r.Start.X, b: r.End.X) : (a: r.Start.Z, b: r.End.Z)).OrderBy(s => s.a).ToList();
                var current = spans[0];
                for (int i = 1; i <= spans.Count; i++)
                {
                    if (i < spans.Count && spans[i].a <= current.b + Eps) { current.b = Math.Max(current.b, spans[i].b); continue; }
                    var first = group.First();
                    double line = alongX ? first.Start.Z : first.Start.X;
                    result.Add(new FenceRun
                    {
                        Start = alongX ? new Flat(current.a, line) : new Flat(line, current.a),
                        End = alongX ? new Flat(current.b, line) : new Flat(line, current.b),
                        Normal = first.Normal, Side = first.Side,
                    });
                    if (i < spans.Count) current = spans[i];
                }
            }
            return result;
        }

        static int SideOrder(string side) => side == "north" ? 0 : side == "east" ? 1 : side == "south" ? 2 : 3;

        static void MarkCorners(List<FenceRun> runs)
        {
            foreach (var run in runs)
                foreach (var other in runs)
                {
                    if (other == run || Math.Abs(other.Normal.Dot(run.Normal)) > Eps) continue;
                    if (Convex(run, run.End, other)) run.EndConvex = true;
                    if (Convex(run, run.Start, other)) run.StartConvex = true;
                }
        }

        static bool Convex(FenceRun run, Flat corner, FenceRun other)
        {
            bool touches = (other.Start - corner).Length < Eps || (other.End - corner).Length < Eps;
            if (!touches) return false;
            var awayAlongRun = (corner - (run.Start + run.End) * .5).Unit;
            var awayAlongOther = (corner - (other.Start + other.End) * .5).Unit;
            return awayAlongRun.Dot(other.Normal) > .5 && awayAlongOther.Dot(run.Normal) > .5;
        }
    }
}
