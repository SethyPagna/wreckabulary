using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules.Surroundings
{
    public static class CourseSkyline
    {
        public const int HighRenderers = 6, HighTriangles = 60000, LowRenderers = 2, LowTriangles = 12000;
        public const string WallSlot = SurroundingsSlots.Plaster, GlassSlot = "bk_glass_dark", MetalSlot = "bk_metal";

        public static readonly IReadOnlyList<string> Slots = new[] { WallSlot, GlassSlot, MetalSlot };

        public static IReadOnlyList<SkylineMeshPart> Parts(CourseSkylineResult skyline) =>
            CourseSkylineGeometry.Parts(skyline ?? throw new ArgumentNullException(nameof(skyline)));
    }

    sealed class CourseSkylineLot
    {
        public long Key;
        public Footprint Lot;
        public Rect Bounds;
        public Flat At;
        public double Yaw, Width, Depth, Height, Distance, Heading, Weight;
        public int Cluster;
        public bool Downtown, Dropped;
        public CourseFacadeSpec Facade;
        public uint Wall, Pane;
        public double Roll;
    }

    sealed class CourseSkylinePlanner
    {
        const double Slack = 1e-6, RingLift = .3, CoreFloor = .75;

        readonly CourseSkylineSpec spec;
        readonly ulong seed;
        readonly Rect box;
        readonly Flat centre;
        readonly float groundY, clear;
        readonly List<double> headings = new List<double>();
        readonly List<CourseSkylineLot> lots = new List<CourseSkylineLot>();
        readonly List<string> thinned = new List<string>();

        public CourseSkylinePlanner(CourseSkylineSpec spec, ulong seed, Rect box, float groundY, float clear)
        {
            this.spec = spec;
            this.seed = seed;
            this.box = box;
            this.groundY = groundY;
            this.clear = clear;
            centre = box.Centre;
        }

        IEnumerable<CourseSkylineLot> Live => lots.Where(l => !l.Dropped);

        Pcg32 Random(string purpose, long index = 0) => Pcg32.ForStream(seed, "skyline:" + purpose, index);

        static float Q(double value, double step) => (float)(Math.Round(value / step, MidpointRounding.AwayFromZero) * step);

        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        static double Clamp01(double t) => t < 0 ? 0 : t > 1 ? 1 : t;

        static double Apart(double a, double b)
        {
            double d = Math.Abs(Shapes.Wrap360(a) - Shapes.Wrap360(b));
            return d > 180 ? 360 - d : d;
        }

        public void Plan(Func<Footprint, Rect, bool> blocked)
        {
            PickHeadings();
            double reach = Math.Max(spec.Downtown.Band.To, spec.Ring.To) + spec.Cell;
            double half = Math.Max(box.MaxX - box.MinX, box.MaxZ - box.MinZ) * .5 + reach;
            int cells = (int)Math.Ceiling(half / spec.Cell);
            for (int i = -cells; i <= cells; i++)
                for (int j = -cells; j <= cells; j++)
                    Consider(i, j, blocked);
        }

        void PickHeadings()
        {
            var rng = Random("downtown");
            var clusters = spec.Downtown.Clusters;
            int count = rng.Range(clusters.min, clusters.max + 1);
            double first = rng.NextDouble() * 360;
            double turn = rng.Range(spec.Downtown.Second, 180f) * (rng.NextFloat() < .5f ? 1 : -1);
            headings.Add(Q(first, .01));
            if (count > 1) headings.Add(Q(Shapes.Wrap360(first + turn), .01));
        }

        (double weight, int cluster) WeightAt(double heading)
        {
            double best = 0;
            int cluster = 0;
            for (int k = 0; k < headings.Count; k++)
            {
                double d = Apart(heading, headings[k]) / spec.Downtown.Spread;
                double w = Math.Exp(-d * d);
                if (w > best) { best = w; cluster = k; }
            }
            return (best, cluster);
        }

        void Consider(int i, int j, Func<Footprint, Rect, bool> blocked)
        {
            long key = (i + 10000L) * 100000L + (j + 10000L);
            var rng = Random("lot", key);
            double roll = rng.NextDouble(), tallness = rng.NextDouble(), wide = rng.NextDouble(), deep = rng.NextDouble();
            double jx = rng.NextDouble(), jz = rng.NextDouble(), turn = rng.NextDouble(), look = rng.NextDouble();
            double wallPick = rng.NextDouble(), panePick = rng.NextDouble(), lowRoll = rng.NextDouble(), outlier = rng.NextDouble();
            var cell = centre + new Flat(i * spec.Cell, j * spec.Cell);
            double distance = box.Distance(cell);
            var (weight, cluster) = WeightAt((cell - centre).Yaw);
            var down = spec.Downtown.Band;
            var ring = spec.Ring;
            bool inDown = distance >= down.From && distance <= down.To && weight >= spec.Downtown.Core;
            bool inRing = distance >= ring.From && distance <= ring.To;
            if (!inDown && !inRing) return;
            double chance = inDown ? Lerp(ring.Chance, down.Chance, weight) : ring.Chance;
            if (roll >= chance) return;
            double height = !inDown && outlier < ring.TallChance ? Lerp(ring.Tall.min, ring.Tall.max, tallness) : Height(inDown, weight, distance, tallness);
            var widths = inDown ? down.Width : ring.Width;
            double width = Math.Min(Lerp(widths.min, widths.max, wide), spec.LotLimit), depth = Math.Min(Lerp(widths.min, widths.max, deep), spec.LotLimit);
            double yaw = 90 * Math.Min(3, (int)(turn * 4));
            double lotW = width + 2 * spec.Plaza, lotD = depth + 2 * spec.Plaza;
            bool square = Math.Abs(yaw % 180) < 1;
            double spanX = square ? lotW : lotD, spanZ = square ? lotD : lotW;
            double freeX = Math.Min(spec.Jitter, Math.Max(0, spec.Cell - spec.Gap - spanX) * .5), freeZ = Math.Min(spec.Jitter, Math.Max(0, spec.Cell - spec.Gap - spanZ) * .5);
            var at = cell + new Flat((jx * 2 - 1) * freeX, (jz * 2 - 1) * freeZ);
            at = new Flat(Q(at.X, .001), Q(at.Z, .001));
            var shape = new Footprint(at.X, at.Z, Q(lotW, .001), Q(lotD, .001), yaw);
            var bounds = shape.Bounds();
            double gap = shape.Distance(box);
            if (gap < clear - Slack || blocked(shape, bounds)) return;
            var facade = PickFacade((height - ring.Height.min) / Math.Max(1, down.Height.max - ring.Height.min), look);
            lots.Add(new CourseSkylineLot
            {
                Key = key, Lot = shape, Bounds = bounds, At = at, Yaw = yaw, Width = Q(width, .001), Depth = Q(depth, .001), Height = Q(height, .01),
                Distance = gap, Heading = (at - centre).Yaw, Weight = weight, Cluster = cluster, Downtown = inDown, Facade = facade,
                Wall = facade.Walls[Math.Min(facade.Walls.Count - 1, (int)(wallPick * facade.Walls.Count))],
                Pane = facade.Panes[Math.Min(facade.Panes.Count - 1, (int)(panePick * facade.Panes.Count))],
                Roll = lowRoll,
            });
        }

        double Height(bool inDown, double weight, double distance, double tallness)
        {
            var ring = spec.Ring.Height;
            var band = spec.Downtown.Band;
            double middle = (band.From + band.To) * .5, half = Math.Max(1, (band.To - band.From) * .5);
            double core = Clamp01(1 - Math.Abs(distance - middle) / half);
            double lift = inDown ? weight * (CoreFloor + (1 - CoreFloor) * core) : weight * RingLift;
            double low = Lerp(ring.min, band.Height.min, lift), high = Lerp(ring.max, band.Height.max, lift);
            return Lerp(low, high, tallness);
        }

        CourseFacadeSpec PickFacade(double tall, double pick)
        {
            float t = (float)Clamp01(tall);
            double total = spec.Facades.Sum(f => f.WeightAt(t));
            if (total <= 0) return spec.Facades[0];
            double at = pick * total;
            foreach (var facade in spec.Facades)
            {
                at -= facade.WeightAt(t);
                if (at < 0) return facade;
            }
            return spec.Facades[spec.Facades.Count - 1];
        }

        public HashSet<int> Thin(IReadOnlyList<string> pieces, IReadOnlyList<string> sources, IReadOnlyList<Footprint> cards)
        {
            var dropped = new HashSet<int>();
            var downtown = Live.Where(l => l.Downtown).ToList();
            for (int c = 0; c < cards.Count; c++)
            {
                if (!spec.ThinCards.Contains(pieces[c])) continue;
                if (!downtown.Any(l => l.Lot.Overlaps(cards[c], spec.CardGap))) continue;
                dropped.Add(c);
                thinned.Add(sources[c]);
            }
            for (int c = 0; c < cards.Count; c++)
            {
                if (dropped.Contains(c)) continue;
                foreach (var lot in Live.ToList())
                    if (lot.Lot.Overlaps(cards[c], spec.CardGap)) lot.Dropped = true;
            }
            return dropped;
        }

        public bool Covers(Footprint shape, Rect bounds)
        {
            foreach (var lot in lots)
                if (!lot.Dropped && lot.Bounds.Overlaps(bounds) && lot.Lot.Overlaps(shape)) return true;
            return false;
        }

        public CourseSkylineResult Finish(CourseSurroundingsTier tier, List<string> problems)
        {
            var live = Live.ToList();
            var kept = Trim(live, true, out int trimmed);
            if (tier == CourseSurroundingsTier.Low)
            {
                var picked = kept.Where(k => k.lot.Roll < (k.lot.Downtown ? spec.KeepDowntown : spec.KeepRing)).Select(k => k.lot).ToList();
                kept = Trim(picked, false, out trimmed);
            }
            var towers = kept.Select(k => k.tower).ToList();
            for (int n = 0; n < towers.Count; n++) towers[n].Ordinal = n;
            var result = CourseSkylineResult.None(tier);
            result.Headings = headings.Select(h => (float)h).ToList();
            result.Towers = towers;
            result.Groups = CourseSkylineGeometry.Groups(towers);
            result.ThinnedCards = thinned.ToList();
            result.Trimmed = trimmed;
            Check(result, problems);
            result.Canonical = Canonical(result);
            result.Hash = Hash64.ToHex(Hash64.Of(result.Canonical));
            return result;
        }

        List<(CourseSkylineLot lot, CourseTower tower)> Trim(List<CourseSkylineLot> chosen, bool full, out int trimmed)
        {
            int rendererLimit = full ? CourseSkyline.HighRenderers : CourseSkyline.LowRenderers;
            int triangleLimit = full ? CourseSkyline.HighTriangles : CourseSkyline.LowTriangles;
            var kept = chosen.Select(l => (lot: l, tower: Tower(l, full))).ToList();
            int triangles = kept.Sum(k => k.tower.Triangles);
            var order = kept.OrderBy(k => k.lot.Downtown ? 1 : 0).ThenBy(k => k.lot.Height).ThenByDescending(k => k.lot.Distance).ThenByDescending(k => k.lot.Key).ToList();
            trimmed = 0;
            foreach (var entry in order)
            {
                if (triangles <= triangleLimit && CourseSkylineGeometry.Groups(kept.Select(k => k.tower)).Sum(g => g.Renderers) <= rendererLimit) break;
                kept.Remove(entry);
                triangles -= entry.tower.Triangles;
                trimmed++;
            }
            return kept;
        }

        CourseTower Tower(CourseSkylineLot lot, bool full) => new CourseTower
        {
            X = (float)lot.At.X, Z = (float)lot.At.Z, Yaw = (float)lot.Yaw, Width = (float)lot.Width, Depth = (float)lot.Depth,
            LotWidth = (float)lot.Lot.HalfWidth * 2, LotDepth = (float)lot.Lot.HalfDepth * 2, Ground = Q(groundY, .001), Height = (float)lot.Height,
            Distance = Q(lot.Distance, .01), Heading = Q(lot.Heading, .01), Weight = Q(lot.Weight, .001), Downtown = lot.Downtown, Facade = lot.Facade.Id,
            Wall = lot.Wall, Pane = lot.Pane, Source = lot.Downtown ? "downtown " + lot.Cluster : "ring",
            Boxes = new CourseTowerBuilder(spec, lot, groundY, full).Build(Random("tower", lot.Key)),
        };

        void Check(CourseSkylineResult result, List<string> problems)
        {
            void Problem(string line) { if (!problems.Contains(line)) problems.Add(line); }
            if (result.Renderers > result.RendererLimit) Problem($"skyline budget: {result.Tier}: {result.Renderers} renderers, at most {result.RendererLimit}");
            if (result.Triangles > result.TriangleLimit) Problem($"skyline budget: {result.Tier}: {result.Triangles} triangles, at most {result.TriangleLimit}");
            foreach (var tower in result.Towers)
                foreach (var b in tower.Boxes)
                {
                    var shape = new Footprint(b.X, b.Z, b.Width, b.Depth, b.Yaw);
                    if (shape.Distance(box) < clear - Slack) Problem($"skyline clear: the {b.Part} of the {tower.Source} tower at ({tower.X}, {tower.Z}) is inside the {clear} m margin");
                    if (b.Y < groundY - Slack) Problem($"skyline ground: the {b.Part} of the {tower.Source} tower at ({tower.X}, {tower.Z}) sinks below the street");
                }
        }

        static string Canonical(CourseSkylineResult r)
        {
            var text = new StringBuilder();
            text.Append("skyline ").Append(r.Tier).Append(' ').Append(string.Join(",", r.Headings.Select(h => F(h)))).Append('\n');
            foreach (string card in r.ThinnedCards) text.Append("thin ").Append(card).Append('\n');
            foreach (var t in r.Towers)
            {
                text.Append("tower ").Append(string.Join(" ", new[] { t.X, t.Z, t.Yaw, t.Width, t.Depth, t.Height, t.Top }.Select(F))).Append(' ')
                    .Append(t.Facade).Append(' ').Append(Hex(t.Wall)).Append(' ').Append(Hex(t.Pane)).Append(' ').Append(t.Source).Append(' ')
                    .Append(t.Boxes.Count).Append(' ').Append(t.Triangles).Append('\n');
            }
            foreach (var g in r.Groups) text.Append("group ").Append(g.Slot).Append(' ').Append(g.Boxes).Append(' ').Append(g.Triangles).Append(' ').Append(g.Renderers).Append('\n');
            return text.ToString();
        }

        static string F(float x) => (x == 0 ? 0.0 : x).ToString("0.###", CultureInfo.InvariantCulture);

        static string Hex(uint rgb) => rgb.ToString("x6", CultureInfo.InvariantCulture);
    }

    sealed class CourseTowerBuilder
    {
        const int Draws = 40;
        const float PlazaHeight = .2f, PlinthOut = .6f, ShopLift = .6f, ShopShare = .5f, ShopProud = .1f, RoofSlab = .5f;
        const float ParapetOut = .15f, ParapetHeight = 1.1f, PierWidth = .8f, PierProud = .12f, PierSpacing = 5.5f, CornerPier = 1f;
        const float MinSegment = 6f, RoofMargin = 1.2f, HutWidth = 3.2f, HutDepth = 3.8f, HutHeight = 2.8f;
        const float StandSize = 2.4f, StandHeight = 1.8f, TankSize = 2.9f, TankHeight = 3.2f, CapSize = 2.2f, CapHeight = .6f;
        const float AcWidth = 2f, AcDepth = 1.3f, AcHeight = 1.1f, AntennaSize = .4f;
        const float CrownLower = .62f, CrownUpper = .36f, CrownLowerHeight = 3.5f, CrownUpperHeight = 4f;
        const float FinWidth = .3f;
        const int MaxPiers = 4, MaxFins = 7;

        readonly CourseSkylineSpec spec;
        readonly CourseSkylineLot lot;
        readonly float ground;
        readonly bool full;
        readonly List<SkylineBox> boxes = new List<SkylineBox>();
        readonly float[] draw = new float[Draws];
        Flat across, along;

        public CourseTowerBuilder(CourseSkylineSpec spec, CourseSkylineLot lot, float ground, bool full)
        {
            this.spec = spec;
            this.lot = lot;
            this.ground = ground;
            this.full = full;
        }

        float R(int k) => draw[k];

        T Pick<T>(IReadOnlyList<T> list, int k) => list[Math.Min(list.Count - 1, (int)(R(k) * list.Count))];

        static float Q(double value) => (float)(Math.Round(value / .001, MidpointRounding.AwayFromZero) * .001);

        public List<SkylineBox> Build(Pcg32 rng)
        {
            for (int k = 0; k < Draws; k++) draw[k] = rng.NextFloat();
            along = Flat.Heading(lot.Yaw);
            across = along.Right;
            float width = (float)lot.Width, depth = (float)lot.Depth, height = (float)lot.Height;
            float plinth = Math.Min(spec.Plinth.min + (spec.Plinth.max - spec.Plinth.min) * R(0), height * .3f);
            float storey = spec.Storey.min + (spec.Storey.max - spec.Storey.min) * R(1);
            float glass = lot.Facade.Glass.min + (lot.Facade.Glass.max - lot.Facade.Glass.min) * R(2);
            uint trim = Pick(spec.Trim, 3);
            if (full) Add("plaza", CourseSkyline.WallSlot, 0, 0, ground, (float)lot.Lot.HalfWidth * 2, (float)lot.Lot.HalfDepth * 2, PlazaHeight, spec.Pavement, true);
            Add("plinth", CourseSkyline.WallSlot, 0, 0, ground, width + 2 * PlinthOut, depth + 2 * PlinthOut, plinth, trim, true);
            if (full) Add("shop", CourseSkyline.GlassSlot, 0, 0, ground + ShopLift, width + 2 * (PlinthOut + ShopProud), depth + 2 * (PlinthOut + ShopProud), (plinth - ShopLift) * ShopShare, lot.Pane, false);
            var segments = Segments(height - plinth, storey);
            float y = ground + plinth, w = width, d = depth, ox = 0, oz = 0;
            for (int s = 0; s < segments.Count; s++)
            {
                if (s > 0)
                {
                    float nw = Math.Max(MinSegment, w * (.68f + .18f * R(8 + s * 4))), nd = Math.Max(MinSegment, d * (.68f + .18f * R(9 + s * 4)));
                    ox += (w - nw) * .5f * (R(10 + s * 4) * 2 - 1) * .8f;
                    oz += (d - nd) * .5f * (R(11 + s * 4) * 2 - 1) * .8f;
                    w = nw;
                    d = nd;
                }
                Segment(ox, oz, y, w, d, segments[s], storey, glass, trim);
                y += segments[s] + RoofSlab;
            }
            if (full) Roof(ox, oz, y, w, d, trim);
            return boxes;
        }

        List<float> Segments(float body, float storey)
        {
            int count = lot.Height < 30 ? 1 : lot.Height < 55 ? 1 + (int)(R(4) * 2) : 1 + (int)(R(4) * 3);
            count = Math.Max(1, Math.Min(count, (int)((body - RoofSlab) / (2 * storey + RoofSlab))));
            float free = body - count * RoofSlab;
            var shares = new List<float>();
            if (count == 1) shares.Add(1);
            else if (count == 2) { float first = .6f + .2f * R(5); shares.Add(first); shares.Add(1 - first); }
            else { float first = .5f + .15f * R(5), second = (1 - first) * (.55f + .2f * R(6)); shares.Add(first); shares.Add(second); shares.Add(1 - first - second); }
            return shares.Select(s => s * free).ToList();
        }

        void Segment(float ox, float oz, float y, float w, float d, float height, float storey, float glass, uint trim)
        {
            float inset = spec.Inset;
            Add("glass", CourseSkyline.GlassSlot, ox, oz, y, w - 2 * inset, d - 2 * inset, height, lot.Pane, false);
            int storeys = Math.Max(2, (int)Math.Round(height / storey));
            int bands = full ? storeys : (storeys + spec.StoreysPerBand - 1) / spec.StoreysPerBand;
            float step = height / bands, band = step * (1 - glass);
            for (int k = 0; k < bands; k++) Add("band", CourseSkyline.WallSlot, ox, oz, y + k * step, w, d, band, lot.Wall, false);
            if (full && lot.Facade.Piers) Ribs("pier", ox, oz, y, w, d, height, PierWidth, PierSpacing, MaxPiers, true);
            if (full && lot.Facade.Fins > 0) Ribs("fin", ox, oz, y, w, d, height, FinWidth, lot.Facade.Fins, MaxFins, false);
            Add("roof", CourseSkyline.WallSlot, ox, oz, y + height, w, d, RoofSlab, spec.Roof, true);
            if (full) Add("parapet", CourseSkyline.WallSlot, ox, oz, y + height, w + 2 * ParapetOut, d + 2 * ParapetOut, ParapetHeight, trim, false);
        }

        void Ribs(string part, float ox, float oz, float y, float w, float d, float height, float width, float spacing, int most, bool corners)
        {
            float rib = spec.Inset + PierProud * 2;
            foreach (int side in new[] { -1, 1 })
            {
                float faceZ = side * (d * .5f + PierProud - rib * .5f), faceX = side * (w * .5f + PierProud - rib * .5f);
                foreach (float t in Spots(w, spacing, most)) Add(part, CourseSkyline.WallSlot, ox + t, oz + faceZ, y, width, rib, height, lot.Wall, false);
                foreach (float t in Spots(d, spacing, most)) Add(part, CourseSkyline.WallSlot, ox + faceX, oz + t, y, rib, width, height, lot.Wall, false);
                if (!corners) continue;
                foreach (int other in new[] { -1, 1 })
                    Add(part, CourseSkyline.WallSlot, ox + side * (w * .5f + PierProud - CornerPier * .5f), oz + other * (d * .5f + PierProud - CornerPier * .5f), y, CornerPier, CornerPier, height, lot.Wall, false);
            }
        }

        static IEnumerable<float> Spots(float face, float spacing, int most)
        {
            int count = Math.Max(1, Math.Min(most, (int)(face / spacing) - 1));
            for (int i = 0; i < count; i++) yield return -face * .5f + face * (i + 1) / (count + 1);
        }

        void Roof(float ox, float oz, float y, float w, float d, uint trim)
        {
            bool crown = lot.Downtown && lot.Height >= 60 && R(20) < .45f;
            bool antenna = lot.Downtown && lot.Height >= 50 && R(21) < .5f;
            float top = y;
            if (crown)
            {
                Add("crown", CourseSkyline.WallSlot, ox, oz, y, w * CrownLower, d * CrownLower, CrownLowerHeight, lot.Wall, true);
                Add("crown", CourseSkyline.WallSlot, ox, oz, y + CrownLowerHeight, w * CrownUpper, d * CrownUpper, CrownUpperHeight, trim, true);
                top = y + CrownLowerHeight + CrownUpperHeight;
            }
            else Details(ox, oz, y, w, d, trim);
            if (antenna) Add("antenna", CourseSkyline.MetalSlot, ox, oz, top, AntennaSize, AntennaSize, 8 + 10 * R(22), SurroundingsSlots.NoTint, true);
        }

        void Details(float ox, float oz, float y, float w, float d, uint trim)
        {
            float qx = w * .25f, qz = d * .25f, roomX = w * .5f - RoofMargin, roomZ = d * .5f - RoofMargin;
            if (roomX < AcWidth || roomZ < AcWidth) return;
            int first = (int)(R(23) * 4) % 4;
            var quads = Enumerable.Range(0, 4).Select(k => (first + k) % 4).Select(q => (x: ox + (q % 2 == 0 ? -qx : qx), z: oz + (q < 2 ? -qz : qz))).ToList();
            if (roomX >= HutWidth && roomZ >= HutDepth) Add("hut", CourseSkyline.WallSlot, quads[0].x, quads[0].z, y, HutWidth, HutDepth, HutHeight, trim, true);
            float tankChance = lot.Facade.Piers ? .7f : lot.Facade.Glass.max >= .7f ? .1f : .4f;
            if (R(24) < tankChance && roomX >= TankSize && roomZ >= TankSize)
            {
                var spot = quads[3];
                Add("stand", CourseSkyline.MetalSlot, spot.x, spot.z, y, StandSize, StandSize, StandHeight, SurroundingsSlots.NoTint, true);
                Add("tank", CourseSkyline.WallSlot, spot.x, spot.z, y + StandHeight, TankSize, TankSize, TankHeight, spec.Tank, true);
                Add("tank", CourseSkyline.WallSlot, spot.x, spot.z, y + StandHeight + TankHeight, CapSize, CapSize, CapHeight, spec.Roof, true);
            }
            int units = 1 + (int)(R(25) * 3);
            for (int k = 0; k < units; k++)
            {
                var spot = quads[1 + k % 2];
                float shift = units > 2 && k == 2 ? AcDepth * 1.4f : 0;
                Add("ac", CourseSkyline.MetalSlot, spot.x, spot.z + shift, y, AcWidth, AcDepth, AcHeight, SurroundingsSlots.NoTint, true);
            }
        }

        void Add(string part, string slot, float lx, float lz, float y, float width, float depth, float height, uint tint, bool top)
        {
            if (width <= 0 || depth <= 0 || height <= 0) return;
            var at = lot.At + across * lx + along * lz;
            boxes.Add(new SkylineBox
            {
                Part = part, Slot = slot, X = Q(at.X), Y = Q(y), Z = Q(at.Z), Width = Q(width), Depth = Q(depth), Height = Q(height),
                Yaw = (float)lot.Yaw, Tint = tint, Top = top,
            });
        }
    }

    static class CourseSkylineGeometry
    {
        public static List<SkylineGroup> Groups(IEnumerable<CourseTower> towers)
        {
            var list = towers.ToList();
            var groups = new List<SkylineGroup>();
            foreach (string slot in CourseSkyline.Slots)
            {
                var group = new SkylineGroup { Slot = slot };
                int part = 0;
                foreach (var b in list.SelectMany(t => t.Boxes).Where(b => b.Slot == slot))
                {
                    if (part > 0 && part + b.Vertices > SurroundingsLayout.MergeVertexLimit) { group.Renderers++; part = 0; }
                    part += b.Vertices;
                    group.Boxes++;
                    group.Triangles += b.Triangles;
                    group.Vertices += b.Vertices;
                }
                if (group.Boxes == 0) continue;
                group.Renderers++;
                groups.Add(group);
            }
            return groups;
        }

        sealed class Builder
        {
            public readonly List<float> Positions = new List<float>(), Normals = new List<float>();
            public readonly List<uint> Colours = new List<uint>();
            public readonly List<int> Indices = new List<int>();
            public int Vertices => Colours.Count;
        }

        public static List<SkylineMeshPart> Parts(CourseSkylineResult skyline)
        {
            var parts = new List<SkylineMeshPart>();
            foreach (string slot in CourseSkyline.Slots)
            {
                var builder = new Builder();
                int index = 0;
                foreach (var b in skyline.Boxes.Where(b => b.Slot == slot))
                {
                    if (builder.Vertices > 0 && builder.Vertices + b.Vertices > SurroundingsLayout.MergeVertexLimit)
                    {
                        parts.Add(Part(slot, index++, builder));
                        builder = new Builder();
                    }
                    Append(builder, b);
                }
                if (builder.Vertices > 0) parts.Add(Part(slot, index, builder));
            }
            return parts;
        }

        static SkylineMeshPart Part(string slot, int index, Builder builder) => new SkylineMeshPart
        {
            Slot = slot, Index = index, Positions = builder.Positions.ToArray(), Normals = builder.Normals.ToArray(),
            Colours = builder.Colours.ToArray(), Indices = builder.Indices.ToArray(),
        };

        static void Append(Builder builder, SkylineBox b)
        {
            double yaw = b.Yaw * Math.PI / 180;
            var along = (x: Math.Sin(yaw), z: Math.Cos(yaw));
            var across = (x: along.z, z: -along.x);
            double hw = b.Width * .5, hd = b.Depth * .5, hh = b.Height * .5, cy = b.Y + hh;
            Face(builder, b.Tint, (b.X + along.x * hd, cy, b.Z + along.z * hd), (along.x, 0, along.z), (across.x * hw, 0, across.z * hw), (0, hh, 0));
            Face(builder, b.Tint, (b.X - along.x * hd, cy, b.Z - along.z * hd), (-along.x, 0, -along.z), (-across.x * hw, 0, -across.z * hw), (0, hh, 0));
            Face(builder, b.Tint, (b.X + across.x * hw, cy, b.Z + across.z * hw), (across.x, 0, across.z), (-along.x * hd, 0, -along.z * hd), (0, hh, 0));
            Face(builder, b.Tint, (b.X - across.x * hw, cy, b.Z - across.z * hw), (-across.x, 0, -across.z), (along.x * hd, 0, along.z * hd), (0, hh, 0));
            if (b.Top) Face(builder, b.Tint, (b.X, b.Y + b.Height, b.Z), (0, 1, 0), (along.x * hd, 0, along.z * hd), (across.x * hw, 0, across.z * hw));
        }

        static void Face(Builder builder, uint tint, (double x, double y, double z) c, (double x, double y, double z) n, (double x, double y, double z) u, (double x, double y, double z) v)
        {
            int start = builder.Vertices;
            foreach (var (su, sv) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
            {
                builder.Positions.Add((float)(c.x + u.x * su + v.x * sv));
                builder.Positions.Add((float)(c.y + u.y * su + v.y * sv));
                builder.Positions.Add((float)(c.z + u.z * su + v.z * sv));
                builder.Normals.Add((float)n.x);
                builder.Normals.Add((float)n.y);
                builder.Normals.Add((float)n.z);
                builder.Colours.Add(tint);
            }
            builder.Indices.Add(start);
            builder.Indices.Add(start + 1);
            builder.Indices.Add(start + 2);
            builder.Indices.Add(start);
            builder.Indices.Add(start + 2);
            builder.Indices.Add(start + 3);
        }
    }
}
