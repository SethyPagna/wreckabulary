using System;
using System.Collections.Generic;
using System.Linq;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules.Surroundings
{
    sealed class ChainLine
    {
        public Flat Origin, Dir;
        public double From, To, Yaw, Scale = 1, Margin = .25;
        public SurroundingsPiece Body, Cap;
        public bool CapStart, CapEnd, Carve, Fill;
        public string Feature, Path;
        public uint Paint = SurroundingsSlots.NoTint;
        public List<(double from, double to)> Cuts = new List<(double, double)>();
    }

    sealed class PlotFrame
    {
        public Flat Front, Right, FrontCentre;
        public double Width, Depth;

        public Flat At(double u, double v) => FrontCentre + Right * u - Front * v;

        public static PlotFrame Of(PlotSpec p)
        {
            var n = p.Front;
            var r = n.Right;
            double sizeX = p.Area.MaxX - p.Area.MinX, sizeZ = p.Area.MaxZ - p.Area.MinZ;
            double depth = Math.Abs(n.X) * sizeX + Math.Abs(n.Z) * sizeZ;
            return new PlotFrame
            {
                Front = n, Right = r, Width = Math.Abs(r.X) * sizeX + Math.Abs(r.Z) * sizeZ, Depth = depth,
                FrontCentre = p.Area.Centre + n * (depth * .5),
            };
        }
    }

    sealed class PlotState
    {
        public PlotFrame Frame;
        public SurroundingsPiece House;
        public double DrivewayU;
        public bool HasDriveway;
        public uint Plaster = SurroundingsSlots.NoTint, Roof = SurroundingsSlots.NoTint;
        public double HouseWidth => House?.Width ?? 0;
        public double HouseDepth => House?.Depth ?? 0;
    }

    sealed partial class LayoutBuilder
    {
        readonly Dictionary<string, List<(double from, double to)>> kerbs = new Dictionary<string, List<(double, double)>>(StringComparer.Ordinal);
        readonly Dictionary<string, PlotState> plots = new Dictionary<string, PlotState>(StringComparer.Ordinal);

        static double Long(SurroundingsPiece p) => Math.Max(p.Width, p.Depth);
        static double Short(SurroundingsPiece p) => Math.Min(p.Width, p.Depth);

        static double YawAlong(SurroundingsPiece piece, Flat dir, Flat front) => piece.Width >= piece.Depth ? front.Yaw : dir.Yaw;

        static IEnumerable<double> Tiles(double a, double b, double length)
        {
            double span = b - a;
            if (span < length * .95 || length <= 0) yield break;
            if (span <= length) { yield return (a + b) * .5; yield break; }
            int n = (int)Math.Ceiling(span / length - 1e-6);
            for (int k = 0; k < n - 1; k++) yield return a + (k + .5) * length;
            yield return b - length * .5;
        }

        List<(double from, double to)> Chain(ChainLine c)
        {
            double length = Long(c.Body) * c.Scale, thick = Short(c.Body) * c.Scale;
            var spans = new List<(double a, double b, bool capA, bool capB)> { (c.From, c.To, c.CapStart, c.CapEnd) };
            var cuts = new List<(double, double)>(c.Cuts);
            if (c.Carve) cuts.AddRange(Blockers(c, thick));
            foreach (var (x, y) in cuts.OrderBy(cut => cut.Item1))
                spans = spans.SelectMany(s => Cut(s, x, y)).ToList();
            var free = new List<(double, double)>();
            foreach (var (a, b, capA, capB) in spans)
            {
                double capLength = c.Cap == null ? 0 : c.Cap.Width * c.Scale;
                double bodyA = a + (capA ? capLength : 0), bodyB = b - (capB ? capLength : 0);
                if (bodyB - bodyA < length * .95) continue;
                free.Add((a, b));
                foreach (double t in Tiles(bodyA, bodyB, length)) Link(c, c.Body, t, c.Yaw);
                if (c.Cap == null) continue;
                if (capA) Link(c, c.Cap, bodyA - capLength * .5, (c.Dir * -1).Left.Yaw);
                if (capB) Link(c, c.Cap, bodyB + capLength * .5, c.Dir.Left.Yaw);
            }
            return free;
        }

        static IEnumerable<(double, double, bool, bool)> Cut((double a, double b, bool capA, bool capB) s, double x, double y)
        {
            if (y <= s.a || x >= s.b) { yield return s; yield break; }
            if (x > s.a) yield return (s.a, x, s.capA, true);
            if (y < s.b) yield return (y, s.b, true, s.capB);
        }

        IEnumerable<(double, double)> Blockers(ChainLine c, double thick)
        {
            var mid = c.Origin + c.Dir * ((c.From + c.To) * .5);
            var strip = new Footprint(mid.X, mid.Z, thick + .1, c.To - c.From, c.Dir.Yaw);
            var bounds = strip.Bounds();
            foreach (var other in items)
            {
                if (other.Feature == c.Feature || !other.Bounds.Overlaps(bounds) || !other.Shape.Overlaps(strip)) continue;
                var along = other.Shape.Corners().Select(p => (p - c.Origin).Dot(c.Dir)).ToList();
                yield return (along.Min() - c.Margin, along.Max() + c.Margin);
            }
        }

        void Link(ChainLine c, SurroundingsPiece piece, double t, double yaw)
        {
            var at = c.Origin + c.Dir * t;
            Place(new Ask { Piece = piece.Id, X = at.X, Z = at.Z, Yaw = yaw, Scale = c.Scale, Feature = c.Feature, Path = c.Path, Fill = c.Fill, Paint = c.Paint });
        }

        void PlaceStreets()
        {
            foreach (var s in plan.Streets)
            {
                var road = Resolve(s.Road, s.Path + ".road");
                var span = s.To - s.From;
                if (road != null)
                    Chain(new ChainLine { Origin = s.From, Dir = span.Unit, To = span.Length, Body = road, Yaw = YawAlong(road, span.Unit, span.Unit.Left), Feature = "street " + s.Id, Path = s.Path });
                if (s.End != null)
                    Place(new Ask { Piece = s.End.Piece, X = s.End.At.X, Z = s.End.At.Z, Yaw = s.End.Yaw, Scale = s.End.Scale, Feature = "street " + s.Id, Path = s.End.Path });
            }
            foreach (var s in plan.Streets)
            {
                if (s.PavementSides == "none") continue;
                var pavement = Resolve(s.Pavement, s.Path + ".pavement");
                if (pavement == null) continue;
                var span = s.To - s.From;
                var dir = span.Unit;
                foreach (string side in SidesOf(s.PavementSides))
                {
                    var lateral = side == "left" ? dir.Left : dir.Right;
                    double yaw = YawAlong(pavement, dir, dir.Left) + (side == "left" ? 180 : 0);
                    kerbs[s.Id + ":" + side] = Chain(new ChainLine
                    {
                        Origin = s.From + lateral * (s.Width * .5 + Short(pavement) * .5), Dir = dir, To = span.Length, Body = pavement, Yaw = yaw,
                        Carve = true, Margin = 0, Feature = "street " + s.Id, Path = $"{s.Path}.pavement.{side}",
                    });
                }
            }
        }

        static IEnumerable<string> SidesOf(string sides) => sides == "both" ? new[] { "left", "right" } : new[] { sides };

        void PlacePlots()
        {
            foreach (var p in plan.Plots)
            {
                var state = new PlotState { Frame = PlotFrame.Of(p) };
                plots[p.Id] = state;
                var f = state.Frame;
                string feature = "plot " + p.Id;
                if (p.House != null && (state.House = Resolve(p.House, p.Path + ".house")) != null)
                {
                    state.Plaster = p.Plaster ?? NextTint("plaster");
                    state.Roof = p.Roof ?? NextTint("roof");
                    var at = f.At(p.Along, p.Setback + state.House.Depth * .5);
                    var house = Place(new Ask { Piece = p.House, X = at.X, Z = at.Z, Yaw = f.Front.Yaw, Feature = feature, Path = p.Path + ".house", Plaster = state.Plaster, Roof = state.Roof });
                    if (house != null && !house.Shape.Corners().All(c => p.Area.Grow(.01).Contains(c)))
                        Problem("plan", p.Path, $"{p.House} does not fit inside the plot");
                }
                if (p.Driveway != null) PlaceDriveway(p, state, feature);
                if (p.Garage != null) PlaceGarage(p, state, feature);
                if (p.Car != null) PlaceCar(p, state, feature);
            }
        }

        void PlaceDriveway(PlotSpec p, PlotState state, string feature)
        {
            var drive = Resolve("driveway", p.Path + ".driveway");
            if (drive == null) return;
            var f = state.Frame;
            double sign = p.Driveway == "left" ? -1 : 1;
            state.DrivewayU = p.Along + sign * (state.HouseWidth * .5 + .8 + Short(drive) * .5);
            state.HasDriveway = true;
            if (Math.Abs(state.DrivewayU) + Short(drive) * .5 > f.Width * .5 + .01)
                Problem("plan", p.Path, "the driveway does not fit beside the house");
            double end = p.Garage != null ? p.Setback : p.Setback + state.HouseDepth * .5;
            var dir = f.Front * -1;
            Chain(new ChainLine { Origin = f.At(state.DrivewayU, 0), Dir = dir, To = Math.Max(Long(drive), end), Body = drive, Yaw = YawAlong(drive, dir, f.Front), Feature = feature, Path = p.Path + ".driveway" });
        }

        void PlaceGarage(PlotSpec p, PlotState state, string feature)
        {
            var garage = Resolve(p.Garage, p.Path + ".garage");
            if (garage == null) return;
            var f = state.Frame;
            double u = state.HasDriveway ? state.DrivewayU : p.Along + state.HouseWidth * .5 + .8 + garage.Width * .5;
            var at = f.At(u, p.Setback + garage.Depth * .5);
            Place(new Ask { Piece = p.Garage, X = at.X, Z = at.Z, Yaw = f.Front.Yaw, Feature = feature, Path = p.Path + ".garage", Plaster = state.Plaster, Roof = state.Roof });
        }

        void PlaceCar(PlotSpec p, PlotState state, string feature)
        {
            if (!state.HasDriveway) { Problem("plan", p.Path, "a car needs a driveway"); return; }
            var car = Resolve(p.Car, p.Path + ".car");
            if (car == null) return;
            var at = state.Frame.At(state.DrivewayU, .6 + car.Depth * .5);
            Place(new Ask { Piece = p.Car, X = at.X, Z = at.Z, Yaw = (state.Frame.Front * -1).Yaw, Feature = feature, Path = p.Path + ".car", Paint = p.Paint ?? NextTint("paint") });
        }

        void PlaceProps()
        {
            foreach (var prop in plan.Props) PlaceProp(prop, "props");
        }

        Item PlaceProp(PropSpec prop, string feature) => Place(new Ask
        {
            Piece = prop.Piece, X = prop.At.X, Z = prop.At.Z, Yaw = prop.Yaw, Scale = prop.Scale, Y = prop.Y, Feature = feature, Path = prop.Path,
            Plaster = prop.Plaster ?? SurroundingsSlots.NoTint, Roof = prop.Roof ?? SurroundingsSlots.NoTint, Paint = prop.Paint ?? SurroundingsSlots.NoTint,
        });

        void PlaceRows()
        {
            for (int i = 0; i < plan.Rows.Count; i++)
            {
                var row = plan.Rows[i];
                var piece = Resolve(row.Piece, row.Path);
                if (piece == null) continue;
                var span = row.To - row.From;
                var dir = span.Unit;
                double yaw = YawAlong(piece, dir, dir.Left) + (row.Flip ? 180 : 0);
                string feature = "row " + i;
                uint paint = row.Paint ?? SurroundingsSlots.NoTint;
                if (row.Every <= 0)
                {
                    Chain(new ChainLine { Origin = row.From, Dir = dir, To = span.Length, Body = piece, Yaw = yaw, Scale = row.Scale, Feature = feature, Path = row.Path, Paint = paint });
                    continue;
                }
                for (double t = 0; t <= span.Length + 1e-6; t += row.Every)
                {
                    var at = row.From + dir * t;
                    Place(new Ask { Piece = row.Piece, X = at.X, Z = at.Z, Yaw = yaw, Scale = row.Scale, Feature = feature, Path = row.Path, Paint = paint });
                }
            }
        }

        void PlaceLandmark()
        {
            if (plan.Landmark != null) PlaceProp(plan.Landmark, "landmark");
        }

        void PlaceEdging()
        {
            var runs = Outline.Fences(house);
            var laid = new Dictionary<FenceRun, (EdgingSpec spec, SurroundingsPiece body, double offset)>();
            foreach (var run in runs)
            {
                if (!plan.Edging.TryGetValue(run.Side, out var spec) && !plan.Edging.TryGetValue("default", out spec)) continue;
                if (spec.None) continue;
                var body = Resolve(spec.Piece, spec.Path);
                if (body != null) laid[run] = (spec, body, PushOut(run, body, spec.Offset));
            }
            foreach (var run in runs.Where(laid.ContainsKey))
            {
                var (spec, body, offset) = laid[run];
                var cap = spec.End == null ? null : Resolve(spec.End, spec.Path + ".end");
                Chain(new ChainLine
                {
                    Origin = run.Start + run.Normal * (offset + Short(body) * .5), Dir = run.Direction,
                    From = -Reach(run, run.Start, run.StartConvex, laid), To = run.Length + Reach(run, run.End, run.EndConvex, laid),
                    Body = body, Cap = cap, Yaw = YawAlong(body, run.Direction, run.Normal), CapStart = !run.StartConvex, CapEnd = !run.EndConvex,
                    Carve = true, Fill = true, Feature = "edging", Path = spec.Path,
                });
            }
        }

        static double Reach(FenceRun run, Flat corner, bool convex, Dictionary<FenceRun, (EdgingSpec spec, SurroundingsPiece body, double offset)> laid)
        {
            if (!convex) return -(SurroundingsLayout.Clearance + .05);
            double reach = 0;
            foreach (var pair in laid)
            {
                var other = pair.Key;
                if (other == run || Math.Abs(other.Normal.Dot(run.Normal)) > .5) continue;
                if ((other.Start - corner).Length < 1e-3 || (other.End - corner).Length < 1e-3)
                    reach = Math.Max(reach, pair.Value.offset + Short(pair.Value.body));
            }
            return reach;
        }

        double PushOut(FenceRun run, SurroundingsPiece body, double offset)
        {
            double yaw = YawAlong(body, run.Direction, run.Normal);
            for (double o = offset; o < offset + 40; o += .05)
            {
                var origin = run.Start + run.Normal * (o + Short(body) * .5);
                bool clear = Tiles(0, run.Length, Long(body)).All(t =>
                {
                    var at = origin + run.Direction * t;
                    var shape = ShapeOf(body, at.X, at.Z, yaw, 1);
                    float y = OnPlinth(at.X, at.Z) ? plan.Ground.PlinthY : plan.Ground.Y;
                    bool boom = body.Height > SurroundingsLayout.BoomHeight && DistanceToFootprint(shape) < SurroundingsLayout.BoomReach;
                    return !boom && !CoversPlay(shape, y + body.Height);
                });
                if (clear) return Math.Round(o, 3);
            }
            return offset;
        }

        void PlaceStreetLife()
        {
            foreach (var s in plan.Streets)
            {
                if (s.Lamps != null) PlaceLamps(s);
                if (s.Parking != null) PlaceParking(s);
            }
        }

        void PlaceLamps(StreetSpec s)
        {
            var lamp = Resolve(s.Lamps.Piece, s.Lamps.Path);
            if (lamp == null) return;
            var span = s.To - s.From;
            var dir = span.Unit;
            foreach (string side in SidesOf(s.Lamps.Side))
            {
                if (!kerbs.TryGetValue(s.Id + ":" + side, out var spans)) { Problem("plan", s.Lamps.Path, $"lamps on the {side} need a pavement there"); continue; }
                var lateral = side == "left" ? dir.Left : dir.Right;
                for (double t = s.Lamps.Phase; t <= span.Length; t += s.Lamps.Every)
                {
                    if (!spans.Any(k => t >= k.from + .6 && t <= k.to - .6)) continue;
                    var at = s.From + dir * t + lateral * (s.Width * .5 + s.Lamps.Inset);
                    Place(new Ask { Piece = lamp.Id, X = at.X, Z = at.Z, Yaw = (lateral * -1).Yaw, Feature = $"street {s.Id} lamps", Path = s.Lamps.Path, Fill = true, OnPavements = true });
                }
            }
        }

        void PlaceParking(StreetSpec s)
        {
            var spec = s.Parking;
            var span = s.To - s.From;
            var dir = span.Unit;
            foreach (string side in SidesOf(spec.Side))
            {
                var lateral = side == "left" ? dir.Left : dir.Right;
                int k = 0;
                for (double t = 3 + spec.Phase; t <= span.Length - 3; t += spec.Every, k++)
                {
                    var rng = Random($"parking:{s.Id}:{side}", k);
                    bool parked = rng.NextFloat() < spec.Chance;
                    var car = spec.Pieces.Count == 0 ? null : Resolve(spec.Pieces[rng.Range(0, spec.Pieces.Count)], spec.Path);
                    if (!parked || car == null) continue;
                    var at = s.From + dir * t + lateral * (s.Width * .5 - .25 - Short(car) * .5);
                    double yaw = dir.Yaw + (side == "left" ? 180 : 0);
                    var shape = ShapeOf(car, at.X, at.Z, yaw, 1);
                    var near = items.FirstOrDefault(i => spec.ClearOf.Contains(i.Piece.Id) && i.Shape.Distance(shape) < spec.Reach);
                    if (near != null) { dropped.Add($"street {s.Id} parking {car.Id} at ({F(at.X)}, {F(at.Z)}): keeps {F(spec.Reach)} m clear of {near.Piece.Id}"); continue; }
                    Place(new Ask { Piece = car.Id, X = at.X, Z = at.Z, Yaw = yaw, Feature = $"street {s.Id} parking", Path = spec.Path, Fill = true, AvoidOwn = true, OnRoads = true, Paint = NextTint("paint") });
                }
            }
        }

        void PlaceGardens()
        {
            foreach (var p in plan.Plots)
            {
                var state = plots[p.Id];
                string feature = $"plot {p.Id} garden";
                if (p.Fence != null) PlaceFence(p, state, feature);
                if (p.Flowers && state.House != null && p.Setback >= 2) PlaceFlowers(p, state, feature);
                PlaceBushes(p, state, feature);
                PlaceTrees(p, state, feature);
                PlaceExtras(p, state, feature);
            }
        }

        void PlaceFence(PlotSpec p, PlotState state, string feature)
        {
            var fence = Resolve(p.Fence.Piece, p.Fence.Path);
            if (fence == null) return;
            var f = state.Frame;
            double w = f.Width, d = f.Depth, inset = .3;
            foreach (string side in p.Fence.Sides)
            {
                var line = side == "front" ? (origin: f.At(-w * .5 + inset, inset), dir: f.Right, length: w - 2 * inset, face: f.Front)
                    : side == "rear" ? (origin: f.At(-w * .5 + inset, d - inset), dir: f.Right, length: w - 2 * inset, face: f.Front * -1)
                    : side == "left" ? (origin: f.At(-w * .5 + inset, inset), dir: f.Front * -1, length: d - 2 * inset, face: f.Right * -1)
                    : side == "right" ? (origin: f.At(w * .5 - inset, inset), dir: f.Front * -1, length: d - 2 * inset, face: f.Right)
                    : throw new FormatException($"{p.Fence.Path}.sides: \"{side}\" must be front, rear, left or right");
                var chain = new ChainLine { Origin = line.origin, Dir = line.dir, To = line.length, Body = fence, Yaw = YawAlong(fence, line.dir, line.face), Carve = true, Fill = true, Feature = feature, Path = p.Fence.Path };
                if (side == "front" && state.House != null)
                {
                    double door = p.Along + w * .5 - inset;
                    chain.Cuts.Add((door - .9, door + .9));
                    if (p.Fence.Gate != null)
                    {
                        var at = f.At(p.Along, inset);
                        Place(new Ask { Piece = p.Fence.Gate, X = at.X, Z = at.Z, Yaw = YawAlong(fence, line.dir, line.face), Feature = feature, Path = p.Fence.Path + ".gate", Fill = true });
                    }
                }
                Chain(chain);
            }
        }

        void PlaceFlowers(PlotSpec p, PlotState state, string feature)
        {
            var bed = Resolve("flowerbed_2m", p.Path + ".flowers");
            if (bed == null) return;
            var f = state.Frame;
            double span = state.HouseWidth - .6;
            var chain = new ChainLine { Origin = f.At(p.Along - span * .5, p.Setback - .2 - Short(bed) * .5), Dir = f.Right, To = span, Body = bed, Yaw = YawAlong(bed, f.Right, f.Front), Carve = true, Fill = true, Feature = feature, Path = p.Path + ".flowers" };
            chain.Cuts.Add((span * .5 - .9, span * .5 + .9));
            Chain(chain);
        }

        void PlaceBushes(PlotSpec p, PlotState state, string feature)
        {
            var f = state.Frame;
            double w = f.Width, set = p.Setback;
            var spots = new List<(double u, double v)>();
            if (state.House != null && set >= 3.2) { spots.Add((p.Along - 1.5, set - 2.6)); spots.Add((p.Along + 1.5, set - 2.6)); }
            spots.Add((-w * .5 + 1.3, 1.5));
            spots.Add((w * .5 - 1.3, 1.5));
            spots.Add((-w * .5 + 1.3, set + state.HouseDepth * .5));
            spots.Add((w * .5 - 1.3, set + state.HouseDepth * .5));
            for (int i = 0; i < Math.Min(p.Bushes, spots.Count); i++)
            {
                var rng = Random($"bush:{p.Id}", i);
                var at = f.At(spots[i].u + rng.Range(-.25f, .25f), spots[i].v + rng.Range(-.25f, .25f));
                string piece = rng.NextFloat() < .5f ? "bush_a" : "bush_b";
                Place(new Ask { Piece = piece, X = at.X, Z = at.Z, Yaw = rng.Range(0, 360), Feature = feature, Path = p.Path + ".bushes", Fill = true, AvoidOwn = true });
            }
        }

        (double from, double to) BackGarden(PlotSpec p, PlotState state)
        {
            double from = state.House != null ? p.Setback + state.HouseDepth + 1.5 : 1.5;
            return (from, state.Frame.Depth - 1.2);
        }

        void PlaceTrees(PlotSpec p, PlotState state, string feature)
        {
            if (p.Trees <= 0 || p.TreePieces.Count == 0) return;
            var f = state.Frame;
            var (from, to) = BackGarden(p, state);
            if (to - from < 1) { dropped.Add($"plot {p.Id} trees: no back garden"); return; }
            double w = f.Width, d = f.Depth;
            var spots = new[] { (-w * .5 + 2.6, d - 2.8), (w * .5 - 2.6, d - 3.4), (-w / 6, d - 3.2), (w / 5, d - 2.4), (0.0, (from + to) * .5), (-w * .3, (from + to) * .5) };
            int planted = 0;
            for (int s = 0; s < spots.Length && planted < p.Trees; s++)
            {
                var rng = Random($"tree:{p.Id}", s);
                double v = Math.Max(from, Math.Min(to, spots[s].Item2 + rng.Range(-.8f, .8f)));
                var at = f.At(spots[s].Item1 + rng.Range(-.8f, .8f), v);
                string piece = p.TreePieces[rng.Range(0, p.TreePieces.Count)];
                if (Place(new Ask { Piece = piece, X = at.X, Z = at.Z, Yaw = rng.Range(0, 360), Scale = rng.Range(.9f, 1.1f), Feature = feature, Path = p.Path + ".trees", Fill = true, AvoidOwn = true, Quiet = true }) != null) planted++;
            }
            if (planted < p.Trees) dropped.Add($"plot {p.Id} trees: planted {planted} of {p.Trees}");
        }

        void PlaceExtras(PlotSpec p, PlotState state, string feature)
        {
            if (p.Extras.Count == 0) return;
            var f = state.Frame;
            var (from, to) = BackGarden(p, state);
            double mid = (from + to) * .5, w = f.Width;
            var spots = new List<(double u, double v)>();
            foreach (double v in new[] { mid, mid - 2, mid + 2 })
                foreach (double u in new[] { -w * .25, w * .25, 0 })
                    spots.Add((u, Math.Max(from, Math.Min(to, v))));
            var rng = Random($"extras:{p.Id}");
            rng.Shuffle(spots);
            for (int e = 0; e < p.Extras.Count; e++)
            {
                bool placed = false;
                foreach (var (u, v) in spots)
                {
                    var at = f.At(u, v);
                    if (Place(new Ask { Piece = p.Extras[e], X = at.X, Z = at.Z, Yaw = f.Front.Yaw + 90 * (e % 2), Feature = feature, Path = $"{p.Path}.extras[{e}]", Fill = true, AvoidOwn = true, Quiet = true }) != null) { placed = true; break; }
                }
                if (!placed) dropped.Add($"plot {p.Id} {p.Extras[e]}: no room in the back garden");
            }
        }

        void PlaceBelts()
        {
            for (int i = 0; i < plan.Belts.Count; i++)
            {
                var belt = plan.Belts[i];
                if (belt.Pieces.Count == 0) continue;
                var span = belt.To - belt.From;
                var dir = span.Unit;
                int k = 0;
                for (double t = 0; t <= span.Length + 1e-6; t += belt.Every, k++)
                {
                    var rng = Random($"belt:{i}", k);
                    var at = belt.From + dir * (t + (rng.NextFloat() - .5) * belt.Jitter) + dir.Left * ((rng.NextFloat() - .5) * belt.Jitter * 2);
                    string piece = belt.Pieces[rng.Range(0, belt.Pieces.Count)];
                    Place(new Ask { Piece = piece, X = at.X, Z = at.Z, Yaw = rng.Range(0, 360), Scale = rng.Range(belt.Scale.min, belt.Scale.max), Feature = "belt " + i, Path = belt.Path, Fill = true, AvoidOwn = true });
                }
            }
        }

        void PlaceScatter()
        {
            var s = plan.Scatter;
            if (s == null) return;
            double half = Math.Max(footprint.MaxX - footprint.MinX, footprint.MaxZ - footprint.MinZ) * .5 + s.To;
            int cells = (int)Math.Ceiling(half / s.Cell);
            for (int i = -cells; i <= cells; i++)
                for (int j = -cells; j <= cells; j++)
                {
                    var rng = Random("far", (i + 10000L) * 100000L + (j + 10000L));
                    var at = Centre + new Flat(i * s.Cell + (rng.NextFloat() - .5) * s.Jitter, j * s.Cell + (rng.NextFloat() - .5) * s.Jitter);
                    double roll = rng.NextFloat();
                    if (roll < s.HouseChance && s.Houses.Count > 0) ScatterHouse(s, rng, at);
                    else if (roll < s.HouseChance + s.TreeChance && s.Trees.Count > 0) ScatterTrees(s, rng, at);
                }
        }

        bool InScatterBand(ScatterSpec s, SurroundingsPiece piece, Flat at, double yaw)
        {
            double d = DistanceToFootprint(ShapeOf(piece, at.X, at.Z, yaw, 1));
            return d >= s.From && d <= s.To;
        }

        void ScatterHouse(ScatterSpec s, Pcg32 rng, Flat at)
        {
            var piece = Resolve(s.Houses[rng.Range(0, s.Houses.Count)], s.Path + ".houses");
            if (piece == null) return;
            double yaw = Math.Round((Centre - at).Yaw / 90) * 90;
            if (!InScatterBand(s, piece, at, yaw)) return;
            Place(new Ask { Piece = piece.Id, X = at.X, Z = at.Z, Yaw = yaw, Feature = "far", Path = s.Path, Fill = true, AvoidOwn = true, Plaster = NextTint("plaster"), Roof = NextTint("roof") });
        }

        void ScatterTrees(ScatterSpec s, Pcg32 rng, Flat at)
        {
            int count = rng.Range(1, 4);
            for (int t = 0; t < count; t++)
            {
                var piece = Resolve(s.Trees[rng.Range(0, s.Trees.Count)], s.Path + ".trees");
                var spot = at + new Flat(rng.Range(-4f, 4f), rng.Range(-4f, 4f));
                if (piece == null || !InScatterBand(s, piece, spot, 0)) continue;
                Place(new Ask { Piece = piece.Id, X = spot.X, Z = spot.Z, Yaw = rng.Range(0, 360), Scale = rng.Range(.85f, 1.2f), Feature = "far", Path = s.Path, Fill = true, AvoidOwn = true, Quiet = t > 0 });
            }
        }

        void PlaceCards()
        {
            for (int i = 0; i < plan.Cards.Count; i++)
            {
                var ring = plan.Cards[i];
                for (int k = 0; k < ring.Count; k++)
                {
                    var rng = Random($"cards:{i}", k);
                    double radius = ring.Radius + (rng.NextFloat() - .5) * 2 * ring.Jitter;
                    var at = Centre + Flat.Heading(ring.Phase + k * 360.0 / ring.Count) * radius;
                    Place(new Ask { Piece = ring.Piece, X = at.X, Z = at.Z, Yaw = (Centre - at).Yaw, Feature = "cards " + i, Path = $"{ring.Path}[{k}]" });
                }
            }
        }

        void PlaceMovers()
        {
            if (plan.Movers.Count > SurroundingsBudget.MaxMovers)
                Problem("movers", "movers", $"{plan.Movers.Count} movers, at most {SurroundingsBudget.MaxMovers}");
            foreach (var m in plan.Movers)
            {
                var piece = Resolve(m.Piece, m.Path);
                var path = MoverPath(m);
                if (piece == null || path == null) continue;
                for (int i = 0; i < path.Count; i++)
                {
                    var a = path[i];
                    var b = path[(i + 1) % path.Count];
                    double gap = rooms.Min(r => Shapes.SegmentToRect(a, b, r));
                    if (gap < SurroundingsLayout.MoverClearance)
                        Problem("movers", m.Path, $"{m.Piece} path passes {F(gap)} m from the footprint, at least {SurroundingsLayout.MoverClearance} m");
                }
                movers.Add(new SurroundingsMover
                {
                    Piece = piece.Id, Speed = Q(m.Speed, .001), Phase = Q(m.Phase, .001), Loop = true, Source = m.Path, PaintTint = m.Paint ?? NextTint("paint"),
                    Path = path.Select(p => new SurroundingsPoint(Q(p.X, .001), plan.Ground.Y, Q(p.Z, .001))).ToList(),
                });
            }
        }

        List<Flat> MoverPath(MoverSpec m)
        {
            if (m.Street == null)
            {
                if (m.Points.Count >= 2) return m.Points;
                Problem("movers", m.Path, "needs a street or a path of at least 2 points");
                return null;
            }
            var s = plan.Streets.FirstOrDefault(st => st.Id == m.Street);
            if (s == null) { Problem("movers", m.Path, $"names street \"{m.Street}\", which the plan does not have"); return null; }
            var dir = (s.To - s.From).Unit;
            return new List<Flat> { s.From + dir.Right * m.Lane, s.To + dir.Right * m.Lane, s.To + dir.Left * m.Lane, s.From + dir.Left * m.Lane };
        }
    }
}
