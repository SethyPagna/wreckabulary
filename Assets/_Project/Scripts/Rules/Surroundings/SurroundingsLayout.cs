using System;
using System.Collections.Generic;
using System.Linq;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules.Surroundings
{
    public static class SurroundingsLayout
    {
        public const float NearReach = 8f, MidReach = 70f, FarReach = 350f;
        public const float Clearance = .3f, PlayMargin = .2f, PlayFloor = -.3f, PlayHeadroom = 3.4f;
        public const float BoomReach = 3.5f, BoomHeight = 3.3f, MoverClearance = 4f;
        public const int MergeVertexLimit = 60000;

        public static SurroundingsResult Build(SurroundingsPlan plan, SurroundingsKit kit, HouseLayout house)
        {
            var result = Draft(plan, kit, house);
            if (!result.Valid) throw new SurroundingsException(plan.MapId, result.Problems);
            return result;
        }

        public static SurroundingsResult Draft(SurroundingsPlan plan, SurroundingsKit kit, HouseLayout house)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (kit == null) throw new ArgumentNullException(nameof(kit));
            if (house == null) throw new ArgumentNullException(nameof(house));
            if (house.Rooms.Count == 0) throw new ArgumentException($"{house.Name}: a house with no rooms has no footprint", nameof(house));
            return new LayoutBuilder(plan, kit, house).Run();
        }

        public static float CouchReach(float top) => (top - PlayFloor) * PartyCamera.Back / PartyCamera.Rise;
    }

    sealed class Item
    {
        public SurroundingsPlacement Placement;
        public SurroundingsPiece Piece;
        public Footprint Shape;
        public Rect Bounds;
        public string Feature, Path;
        public bool Fill;

        public float Top => Placement.Y + Piece.Height * Placement.Scale;
    }

    sealed class Ask
    {
        public string Piece, Feature, Path;
        public double X, Z, Yaw, Scale = 1;
        public float? Y;
        public uint Plaster = SurroundingsSlots.NoTint, Roof = SurroundingsSlots.NoTint, Paint = SurroundingsSlots.NoTint;
        public bool Fill, AvoidOwn, OnRoads, OnPavements, Quiet;
    }

    sealed class Corridor
    {
        public Footprint Shape;
        public Flat From, Dir;
        public double Length, Eye, AimY;
        public string Path;
    }

    sealed partial class LayoutBuilder
    {
        readonly SurroundingsPlan plan;
        readonly SurroundingsKit kit;
        readonly HouseLayout house;
        readonly ulong seed;
        readonly List<Rect> rooms;
        readonly List<Rect> plinth;
        readonly Rect footprint, play;
        readonly float playTop;
        readonly List<Item> items = new List<Item>();
        readonly List<string> problems = new List<string>();
        readonly List<string> dropped = new List<string>();
        readonly List<Corridor> corridors = new List<Corridor>();
        readonly List<SurroundingsMover> movers = new List<SurroundingsMover>();
        readonly Dictionary<string, (List<uint> order, int next)> palettes = new Dictionary<string, (List<uint>, int)>(StringComparer.Ordinal);

        public LayoutBuilder(SurroundingsPlan plan, SurroundingsKit kit, HouseLayout house)
        {
            this.plan = plan;
            this.kit = kit;
            this.house = house;
            seed = Hash64.Of(plan.MapId);
            rooms = house.Rooms.Select(Rect.Of).ToList();
            footprint = new Rect(rooms.Min(r => r.MinX), rooms.Min(r => r.MinZ), rooms.Max(r => r.MaxX), rooms.Max(r => r.MaxZ));
            play = footprint.Grow(SurroundingsLayout.PlayMargin);
            playTop = house.Rooms.Max(r => r.FloorY) + SurroundingsLayout.PlayHeadroom;
            float ground = house.Rooms.Min(r => r.FloorY);
            plinth = house.Rooms.Where(r => Math.Abs(r.FloorY - ground) < RoomBox.StoreyTolerance).Select(r => Rect.Of(r).Grow(plan.Ground.PlinthReach)).ToList();
        }

        Flat Centre => footprint.Centre;

        public SurroundingsResult Run()
        {
            MakeCorridors();
            PlaceStreets();
            PlacePlots();
            PlaceProps();
            PlaceRows();
            PlaceLandmark();
            PlaceEdging();
            PlaceStreetLife();
            PlaceGardens();
            PlaceBelts();
            PlaceScatter();
            PlaceCards();
            PlaceMovers();
            CheckSpacing();
            CheckPavements();
            CheckLandmark();
            return Finish();
        }

        Pcg32 Random(string purpose, long index = 0) => Pcg32.ForStream(seed, purpose, index);

        SurroundingsPiece Resolve(string id, string path)
        {
            var piece = kit.Piece(id);
            if (piece == null) Problem("kit", path, $"unknown piece \"{id}\"");
            return piece;
        }

        void Problem(string rule, string path, string detail)
        {
            string line = $"{rule}: {path}: {detail}";
            if (!problems.Contains(line)) problems.Add(line);
        }

        uint NextTint(string palette)
        {
            if (!palettes.TryGetValue(palette, out var state))
            {
                var order = plan.PaletteOf(palette).ToList();
                var rng = Random("palette:" + palette);
                rng.Shuffle(order);
                state = (order, 0);
            }
            if (state.order.Count == 0) return SurroundingsSlots.NoTint;
            uint tint = state.order[state.next % state.order.Count];
            palettes[palette] = (state.order, state.next + 1);
            return tint;
        }

        static float Q(double value, double step) => (float)(Math.Round(value / step, MidpointRounding.AwayFromZero) * step);

        static string F(double v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        static Footprint ShapeOf(SurroundingsPiece piece, double x, double z, double yaw, double scale) =>
            new Footprint(x, z, piece.Width * scale, piece.Depth * scale, yaw);

        double DistanceToFootprint(Footprint f)
        {
            double best = double.MaxValue;
            foreach (var r in rooms) best = Math.Min(best, f.Distance(r));
            return best;
        }

        bool OnPlinth(double x, double z) => plinth.Any(r => r.Contains(new Flat(x, z)));

        Item Place(Ask ask)
        {
            var piece = Resolve(ask.Piece, ask.Path);
            if (piece == null) return null;
            double yaw = Shapes.Wrap360(ask.Yaw);
            var shape = ShapeOf(piece, Q(ask.X, .001), Q(ask.Z, .001), Q(yaw, .01), Q(ask.Scale, .001));
            double distance = DistanceToFootprint(shape);
            var ring = RingOf(distance);
            if (ring == SurroundingsRing.Far && !piece.AllowedFar && piece.Far != null && kit.Has(piece.Far))
            {
                piece = kit.Piece(piece.Far);
                shape = ShapeOf(piece, shape.Centre.X, shape.Centre.Z, shape.Yaw, Q(ask.Scale, .001));
                distance = DistanceToFootprint(shape);
                ring = RingOf(distance);
            }
            var item = new Item
            {
                Piece = piece, Shape = shape, Bounds = shape.Bounds(), Feature = ask.Feature, Path = ask.Path, Fill = ask.Fill,
                Placement = new SurroundingsPlacement
                {
                    Piece = piece.Id, X = Q(shape.Centre.X, .001), Z = Q(shape.Centre.Z, .001), Yaw = Q(yaw, .01), Scale = Q(ask.Scale, .001),
                    Y = Q(ask.Y ?? (OnPlinth(shape.Centre.X, shape.Centre.Z) ? plan.Ground.PlinthY : plan.Ground.Y), .001),
                    Ring = ring, Card = piece.IsCard, PlasterTint = ask.Plaster, RoofTint = ask.Roof, PaintTint = ask.Paint, Source = ask.Feature,
                },
            };
            var broken = Broken(item, distance);
            if (ask.Fill && broken == null) broken = Blocked(item, ask);
            if (broken != null)
            {
                if (ask.Fill)
                {
                    if (!ask.Quiet) dropped.Add($"{ask.Feature} {piece.Id} at ({F(shape.Centre.X)}, {F(shape.Centre.Z)}): {broken}");
                    return null;
                }
                int colon = broken.IndexOf(':');
                Problem(broken.Substring(0, colon), ask.Path, $"{piece.Id} at ({F(shape.Centre.X)}, {F(shape.Centre.Z)}){broken.Substring(colon + 1)}");
            }
            items.Add(item);
            return item;
        }

        static SurroundingsRing RingOf(double distance) =>
            distance < SurroundingsLayout.NearReach ? SurroundingsRing.Near : distance < SurroundingsLayout.MidReach ? SurroundingsRing.Mid : SurroundingsRing.Far;

        string Broken(Item item, double distance)
        {
            var piece = item.Piece;
            var shape = item.Shape;
            if (distance > SurroundingsLayout.FarReach) return $"ring: {F(distance)} m out is beyond the far ring ({SurroundingsLayout.FarReach} m)";
            if (item.Placement.Ring == SurroundingsRing.Far && !piece.AllowedFar) return $"ring: the far ring takes only _far pieces, cards and the landmark ({F(distance)} m out)";
            if (item.Placement.Ring != SurroundingsRing.Far && piece.IsFarStandIn) return $"ring: _far pieces belong in the far ring, not {item.Placement.Ring} ({F(distance)} m out)";
            if (rooms.Any(r => shape.Overlaps(r.Grow(SurroundingsLayout.Clearance)))) return "clearance: overlaps the playable footprint + 0.3 m";
            if (CoversPlay(shape, item.Top)) return $"couch: {F(item.Top)} m top covers the play volume in the couch view";
            float tall = piece.Height * item.Placement.Scale;
            if (tall > SurroundingsLayout.BoomHeight && distance < SurroundingsLayout.BoomReach) return $"boom: {F(tall)} m tall within {F(distance)} m of the footprint";
            foreach (var c in corridors)
                if (BlocksSight(item, c)) return $"sightline: blocks {c.Path}";
            return null;
        }

        bool CoversPlay(Footprint shape, double top)
        {
            double reach = SurroundingsLayout.CouchReach((float)top);
            return Shapes.ConvexOverlap(Shapes.SweptNorth(shape, Math.Max(0, reach)), play.Corners(), 0);
        }

        bool BlocksSight(Item item, Corridor c)
        {
            if (item.Piece.IsLandmark || !item.Shape.Overlaps(c.Shape)) return false;
            double near = item.Shape.Corners().Min(p => (p - c.From).Dot(c.Dir));
            double t = Math.Max(0, Math.Min(c.Length, near));
            double sight = c.Eye + (c.AimY - c.Eye) * (t / c.Length);
            return item.Top > sight;
        }

        string Blocked(Item item, Ask ask)
        {
            foreach (var other in items)
            {
                if (other.Feature == item.Feature && !ask.AvoidOwn) continue;
                if (!other.Bounds.Overlaps(item.Bounds)) continue;
                if (ask.OnRoads && (other.Piece.IsRoad || other.Piece.IsPavement)) continue;
                if (ask.OnPavements && other.Piece.IsPavement) continue;
                if (item.Piece.IsFlat && other.Piece.IsFlat) continue;
                if (item.Shape.Overlaps(other.Shape)) return $"overlaps {other.Piece.Id} ({other.Feature})";
            }
            return null;
        }

        void MakeCorridors()
        {
            foreach (var s in plan.Sightlines)
            {
                Flat to;
                double aim;
                if (s.To.HasValue) { to = s.To.Value; aim = s.Eye; }
                else
                {
                    var mark = plan.Landmark == null ? null : kit.Piece(plan.Landmark.Piece);
                    if (mark == null) { Problem("sightline", s.Path, "aims at the landmark, but the plan has no landmark in the kit"); continue; }
                    to = plan.Landmark.At;
                    aim = plan.Ground.Y + mark.Height * plan.Landmark.Scale * .5;
                }
                var span = to - s.From;
                if (span.Length < 1) { Problem("sightline", s.Path, "is shorter than 1 m"); continue; }
                var dir = span.Unit;
                var mid = s.From + span * .5;
                corridors.Add(new Corridor
                {
                    Shape = new Footprint(mid.X, mid.Z, s.Width, span.Length, dir.Yaw), From = s.From, Dir = dir,
                    Length = span.Length, Eye = s.Eye, AimY = aim, Path = s.Path,
                });
            }
        }

        void CheckSpacing()
        {
            var buildings = items.Where(i => i.Piece.IsBuilding).ToList();
            for (int a = 0; a < buildings.Count; a++)
                for (int b = a + 1; b < buildings.Count; b++)
                    if (buildings[a].Bounds.Overlaps(buildings[b].Bounds) && buildings[a].Shape.Overlaps(buildings[b].Shape))
                        Problem("spacing", buildings[b].Path, $"{buildings[b].Piece.Id} overlaps building {buildings[a].Piece.Id} ({buildings[a].Path})");
        }

        void CheckPavements()
        {
            var pavements = items.Where(i => i.Piece.IsPavement).ToList();
            foreach (var item in items.Where(i => plan.StreetFurniture.Contains(i.Piece.Id)))
            {
                bool on = item.Shape.Corners().All(c => pavements.Any(p => p.Shape.Contains(c, .01)));
                if (!on) Problem("pavement", item.Path, $"{item.Piece.Id} at ({F(item.Placement.X)}, {F(item.Placement.Z)}) is street furniture off the pavement");
            }
        }

        void CheckLandmark()
        {
            var mark = items.FirstOrDefault(i => i.Feature == "landmark");
            if (mark == null) return;
            double north = mark.Placement.Z - Centre.Z, side = Math.Abs(mark.Placement.X - Centre.X);
            if (mark.Placement.Z <= footprint.MaxZ || side > north * Math.Tan(15 * Math.PI / 180))
                Problem("landmark", mark.Path, $"{mark.Piece.Id} must stand north of the map, within 15° of due north");
        }
    }
}
