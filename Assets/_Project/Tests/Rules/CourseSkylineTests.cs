using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Surroundings;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class CourseSkylineTests
    {
        const string Folder = "Assets/_Project/Data/Surroundings";
        const string StyleFile = "Courses/course_city.json";
        const string DepotId = "wordbound-parcel-service-v2";
        const double Tolerance = 1e-3;
        const float LowestBody = 20f, TallestBody = 90f, TallestTop = 120f, VisibleAboveWalls = 45f;

        static readonly string[] Shapes = { "arena", "depot", "flagship", "offset", "ribbon" };
        static readonly CourseSurroundingsTier[] Tiers = { CourseSurroundingsTier.High, CourseSurroundingsTier.Low };
        static readonly string[] Details = { "plaza", "shop", "pier", "fin", "parapet", "hut", "stand", "tank", "ac", "crown", "antenna" };
        static readonly Dictionary<(string, string, CourseSurroundingsTier), CourseSurroundingsResult> built = new Dictionary<(string, string, CourseSurroundingsTier), CourseSurroundingsResult>();
        static SurroundingsKit kit;
        static CourseSurroundingsStyle style;

        static string Read(string file) => File.ReadAllText(Path.Combine(TestData.Root, Folder, file));

        static SurroundingsKit Kit()
        {
            if (kit != null) return kit;
            var loaded = new SurroundingsKit();
            foreach (string file in Directory.GetFiles(Path.Combine(TestData.Root, Folder), "kit_*.json").OrderBy(f => f, StringComparer.Ordinal))
                loaded.Add(File.ReadAllText(file), Path.GetFileName(file));
            return kit = loaded;
        }

        static CourseSurroundingsStyle Style() => style ??= CourseSurroundingsStyle.FromJson(Read(StyleFile), StyleFile);

        static string WithoutSkyline(string json)
        {
            int key = json.IndexOf("\"skyline\"", StringComparison.Ordinal);
            Assert.Greater(key, 0, "the style has a skyline block");
            return json.Substring(0, json.LastIndexOf(',', key)) + "\n}\n";
        }

        static CourseFootprint Footprint(string shape)
        {
            switch (shape)
            {
                case "arena": return new CourseFootprint(-10, -10, 10, 10, 0, 4);
                case "depot": return new CourseFootprint(-36, -20, 36, 20, 0, 7.2f);
                case "flagship": return new CourseFootprint(-48, -48, 48, 24, 0, 30);
                case "offset": return new CourseFootprint(130.5f, -260.25f, 171.25f, -228f, 12.5f, 18f);
                default: return new CourseFootprint(-70, -6, 70, 6, 0, 3);
            }
        }

        static CourseSurroundingsResult Build(string shape, CourseSurroundingsTier tier, string id = DepotId)
        {
            if (built.TryGetValue((shape, id, tier), out var cached)) return cached;
            try { return built[(shape, id, tier)] = CourseSurroundings.Build(Style(), Kit(), id, Footprint(shape), tier); }
            catch (SurroundingsException e) { Assert.Fail(e.Message); return null; }
        }

        static IEnumerable<TestCaseData> Cases() =>
            from shape in Shapes from tier in Tiers select new TestCaseData(shape, tier).SetName($"{shape} {tier}");

        static (double x, double z)[] Corners(double x, double z, double width, double depth, double yawDegrees)
        {
            double yaw = yawDegrees * Math.PI / 180, hw = width * .5, hd = depth * .5;
            double ax = Math.Sin(yaw), az = Math.Cos(yaw), rx = az, rz = -ax;
            return new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }
                .Select(c => (x + rx * hw * c.Item1 + ax * hd * c.Item2, z + rz * hw * c.Item1 + az * hd * c.Item2)).ToArray();
        }

        static (double x, double z)[] Corners(SurroundingsPlacement p)
        {
            var piece = Kit().Piece(p.Piece);
            return Corners(p.X, p.Z, piece.Width * p.Scale, piece.Depth * p.Scale, p.Yaw);
        }

        static (double x, double z)[] Lot(CourseTower t) => Corners(t.X, t.Z, t.LotWidth, t.LotDepth, t.Yaw);

        static bool InLot(CourseTower t, (double x, double z) point)
        {
            double yaw = t.Yaw * Math.PI / 180, dx = point.x - t.X, dz = point.z - t.Z;
            double along = dx * Math.Sin(yaw) + dz * Math.Cos(yaw), across = dx * Math.Cos(yaw) - dz * Math.Sin(yaw);
            return Math.Abs(across) <= t.LotWidth * .5 + Tolerance && Math.Abs(along) <= t.LotDepth * .5 + Tolerance;
        }

        static bool Overlap((double x, double z)[] a, (double x, double z)[] b, double pad = 0)
        {
            foreach (var polygon in new[] { a, b })
                for (int i = 0; i < polygon.Length; i++)
                {
                    var p = polygon[i];
                    var q = polygon[(i + 1) % polygon.Length];
                    double nx = -(q.z - p.z), nz = q.x - p.x, length = Math.Sqrt(nx * nx + nz * nz);
                    if (length < 1e-12) continue;
                    nx /= length;
                    nz /= length;
                    double aMin = a.Min(v => v.x * nx + v.z * nz), aMax = a.Max(v => v.x * nx + v.z * nz);
                    double bMin = b.Min(v => v.x * nx + v.z * nz), bMax = b.Max(v => v.x * nx + v.z * nz);
                    if (aMax + pad <= bMin + Tolerance || bMax + pad <= aMin + Tolerance) return false;
                }
            return true;
        }

        static (double x, double z)[] Grown(CourseFootprint f, double by) =>
            new[] { (f.MinX - by, (double)f.MinZ - by), (f.MaxX + by, f.MinZ - by), (f.MaxX + by, f.MaxZ + by), (f.MinX - by, f.MaxZ + by) };

        [Test]
        public void TheStyleBringsASkylineThatOnlyUsesKitSlots()
        {
            Assert.IsTrue(Style().HasSkyline);
            var slots = new HashSet<string>(Kit().Pieces.SelectMany(p => p.Slots));
            foreach (string slot in CourseSkyline.Slots) Assert.IsTrue(slots.Contains(slot), $"{slot} has no backdrop material in the kit");
            Assert.IsFalse(CourseSurroundingsStyle.FromJson(WithoutSkyline(Read(StyleFile)), "bare.json").HasSkyline);
        }

        [Test]
        public void TheSameCourseAndBoundsAlwaysGiveTheSameSkyline()
        {
            foreach (var tier in Tiers)
            {
                var first = CourseSurroundings.Build(Style(), Kit(), DepotId, Footprint("depot"), tier);
                var second = CourseSurroundings.Build(Style(), Kit(), DepotId, Footprint("depot"), tier);
                Assert.IsFalse(first.Skyline.Empty, tier.ToString());
                Assert.AreEqual(first.Skyline.Hash, second.Skyline.Hash, tier.ToString());
                Assert.AreEqual(first.Skyline.Canonical, second.Skyline.Canonical, tier.ToString());
                StringAssert.Contains("skyline " + first.Skyline.Hash, first.Canonical, "the town hash covers the skyline");
            }
        }

        [Test]
        public void ADifferentCourseOrSizeGivesADifferentSkyline()
        {
            var depot = Build("depot", CourseSurroundingsTier.High).Skyline;
            var other = Build("depot", CourseSurroundingsTier.High, "wordbound-lost-address").Skyline;
            Assert.AreNotEqual(depot.Hash, other.Hash);
            CollectionAssert.AreNotEqual(depot.Headings, other.Headings, "the downtown turns with the course id");
            Assert.AreNotEqual(depot.Hash, Build("flagship", CourseSurroundingsTier.High).Skyline.Hash);
        }

        [Test]
        public void TheDowntownFacesDifferentWaysOnDifferentCourses()
        {
            var headings = new List<float>();
            var clusters = new HashSet<int>();
            for (int n = 0; n < 16; n++)
            {
                var skyline = CourseSurroundings.Build(Style(), Kit(), "course-" + n, Footprint("arena"), CourseSurroundingsTier.High).Skyline;
                Assert.That(skyline.Headings.Count, Is.InRange(1, 2));
                clusters.Add(skyline.Headings.Count);
                headings.Add(skyline.Headings[0]);
            }
            Assert.GreaterOrEqual(headings.Select(h => (int)(h / 45f)).Distinct().Count(), 5, string.Join(", ", headings));
            CollectionAssert.AreEquivalent(new[] { 1, 2 }, clusters, "some courses get one downtown and some get two");
        }

        [TestCaseSource(nameof(Cases))]
        public void NothingStandsInsideTheClearMarginOrOnTheTown(string shape, CourseSurroundingsTier tier)
        {
            var result = Build(shape, tier);
            var skyline = result.Skyline;
            var f = result.Footprint;
            var margin = Grown(f, result.Clear);
            Assert.IsNotEmpty(skyline.Towers);
            foreach (var t in skyline.Towers)
            {
                Assert.AreEqual(result.GroundY, t.Ground, 1e-3);
                foreach (var b in t.Boxes)
                {
                    Assert.IsFalse(Overlap(b.Corners(), margin), $"the {b.Part} of the tower at ({t.X}, {t.Z}) is inside the course bounds plus {result.Clear} m");
                    Assert.GreaterOrEqual(b.Y, result.GroundY - 1e-3, $"the {b.Part} of the tower at ({t.X}, {t.Z}) sinks below the street");
                    Assert.IsTrue(b.Corners().All(c => InLot(t, c)), $"the {b.Part} of the tower at ({t.X}, {t.Z}) leaves its lot");
                }
                var lot = Lot(t);
                foreach (var p in result.Placements)
                {
                    bool hit = Overlap(lot, Corners(p));
                    Assert.IsFalse(hit, $"the {t.Source} tower at ({t.X}, {t.Z}) overlaps {p.Piece} ({p.Source})");
                }
            }
            for (int a = 0; a < skyline.Towers.Count; a++)
                for (int b = a + 1; b < skyline.Towers.Count; b++)
                    Assert.IsFalse(Overlap(Lot(skyline.Towers[a]), Lot(skyline.Towers[b])), $"towers {a} and {b} share ground");
        }

        [TestCaseSource(nameof(Shapes))]
        public void HeightsRiseFromTheRingToTheDowntown(string shape)
        {
            var towers = Build(shape, CourseSurroundingsTier.High).Skyline.Towers;
            foreach (var t in towers)
            {
                Assert.That(t.Height, Is.InRange(LowestBody - Tolerance, TallestBody + Tolerance), $"the {t.Source} tower at ({t.X}, {t.Z})");
                Assert.That(t.Top - t.Ground, Is.InRange(t.Height - Tolerance, TallestTop), $"the {t.Source} tower at ({t.X}, {t.Z})");
            }
            var downtown = towers.Where(t => t.Downtown).ToList();
            var ring = towers.Where(t => !t.Downtown).ToList();
            Assert.IsNotEmpty(downtown);
            Assert.IsNotEmpty(ring);
            Assert.Greater(downtown.Average(t => t.Height), ring.Average(t => t.Height) + 10, "the downtown is clearly taller than the ring");
            Assert.GreaterOrEqual(towers.Count(t => t.Height >= VisibleAboveWalls), 5, "enough towers rise well above the course walls");
            Assert.GreaterOrEqual(towers.Max(t => t.Height), 60f);
        }

        [TestCaseSource(nameof(Cases))]
        public void EachTierFitsItsSkylineBudget(string shape, CourseSurroundingsTier tier)
        {
            var result = Build(shape, tier);
            var skyline = result.Skyline;
            Assert.IsTrue(result.Valid, string.Join("\n", result.Problems));
            bool high = tier == CourseSurroundingsTier.High;
            Assert.AreEqual(high ? CourseSkyline.HighRenderers : CourseSkyline.LowRenderers, skyline.RendererLimit);
            Assert.AreEqual(high ? CourseSkyline.HighTriangles : CourseSkyline.LowTriangles, skyline.TriangleLimit);
            Assert.LessOrEqual(skyline.Renderers, skyline.RendererLimit);
            Assert.LessOrEqual(skyline.Triangles, skyline.TriangleLimit);
            Assert.AreEqual(skyline.Boxes.Sum(b => b.Triangles), skyline.Triangles);
            Assert.LessOrEqual(result.Renderers, result.RendererLimit, "the town keeps its own budget");
            Assert.LessOrEqual(result.Triangles, result.TriangleLimit, "the town keeps its own budget");
        }

        [TestCaseSource(nameof(Shapes))]
        public void TheLowSkylineIsALighterCopyOfTheHighSkyline(string shape)
        {
            var high = Build(shape, CourseSurroundingsTier.High).Skyline;
            var low = Build(shape, CourseSurroundingsTier.Low).Skyline;
            Assert.Less(low.Towers.Count, high.Towers.Count);
            Assert.Less(low.Triangles, high.Triangles);
            Assert.LessOrEqual(low.Renderers, high.Renderers);
            var spots = high.Towers.ToDictionary(t => (t.X, t.Z), t => t);
            foreach (var t in low.Towers)
            {
                Assert.IsTrue(spots.TryGetValue((t.X, t.Z), out var twin), $"the low tower at ({t.X}, {t.Z}) is not in the high skyline");
                Assert.AreEqual(twin.Yaw, t.Yaw);
                Assert.AreEqual(twin.Height, t.Height);
                Assert.Less(t.Boxes.Count(b => b.Part == "band"), twin.Boxes.Count(b => b.Part == "band") + 1, "low has no more window bands");
                Assert.Less(t.Triangles, twin.Triangles);
            }
            Assert.IsFalse(low.Boxes.Any(b => Details.Contains(b.Part)), "low has no plazas, piers, fins, parapets or roof details");
            Assert.IsFalse(low.Groups.Any(g => g.Slot == CourseSkyline.MetalSlot), "low draws walls and glass only");
            Assert.Less(low.Boxes.Count(b => b.Part == "band"), high.Boxes.Count(b => b.Part == "band"), "low has fewer window bands");
            Assert.IsTrue(high.Boxes.Any(b => b.Part == "hut" || b.Part == "tank" || b.Part == "crown"), "high has roof details");
        }

        [Test]
        public void TheGeometryMatchesTheBoxesAndFacesOutward()
        {
            var skyline = Build("depot", CourseSurroundingsTier.High).Skyline;
            var parts = CourseSkyline.Parts(skyline);
            Assert.AreEqual(skyline.Renderers, parts.Count);
            Assert.AreEqual(skyline.Vertices, parts.Sum(p => p.Vertices));
            Assert.AreEqual(skyline.Triangles, parts.Sum(p => p.Triangles));
            CollectionAssert.AreEquivalent(CourseSkyline.Slots, parts.Select(p => p.Slot).Distinct());
            foreach (string slot in CourseSkyline.Slots)
            {
                var boxes = skyline.Boxes.Where(b => b.Slot == slot).ToList();
                var mine = parts.Where(p => p.Slot == slot).OrderBy(p => p.Index).ToList();
                int box = 0, face = 0;
                foreach (var part in mine)
                {
                    Assert.LessOrEqual(part.Vertices, SurroundingsLayout.MergeVertexLimit);
                    Assert.AreEqual(part.Vertices, part.Colours.Length);
                    Assert.AreEqual(part.Positions.Length, part.Normals.Length);
                    Assert.IsTrue(part.Indices.All(i => i >= 0 && i < part.Vertices));
                    Assert.IsTrue(part.Colours.All(c => c <= 0xFFFFFF));
                    for (int v = 0; v < part.Vertices; v += 4)
                    {
                        var b = boxes[box];
                        Vec P(int k) => new Vec(part.Positions[(v + k) * 3], part.Positions[(v + k) * 3 + 1], part.Positions[(v + k) * 3 + 2]);
                        var n = new Vec(part.Normals[v * 3], part.Normals[v * 3 + 1], part.Normals[v * 3 + 2]);
                        var middle = (P(0) + P(1) + P(2) + P(3)) * .25;
                        var centre = new Vec(b.X, b.Y + b.Height * .5, b.Z);
                        Assert.Greater((middle - centre).Dot(n), 0, $"face {face} of the {b.Part} at ({b.X}, {b.Z}) points inwards");
                        Assert.AreEqual(b.Tint, part.Colours[v]);
                        if (++face == b.Faces) { face = 0; box++; }
                    }
                    for (int i = 0; i < part.Indices.Length; i += 3)
                    {
                        Vec Corner(int k) => new Vec(part.Positions[part.Indices[i + k] * 3], part.Positions[part.Indices[i + k] * 3 + 1], part.Positions[part.Indices[i + k] * 3 + 2]);
                        var normal = new Vec(part.Normals[part.Indices[i] * 3], part.Normals[part.Indices[i] * 3 + 1], part.Normals[part.Indices[i] * 3 + 2]);
                        Assert.Greater((Corner(1) - Corner(0)).Cross(Corner(2) - Corner(0)).Dot(normal), 0, $"triangle {i / 3} of {part.Label} is wound inwards");
                    }
                }
                Assert.AreEqual(boxes.Count, box, $"every {slot} box became faces");
            }
        }

        [Test]
        public void TheRooflineCardsGiveWayDowntownAndTheHillsStay()
        {
            var bare = CourseSurroundingsStyle.FromJson(WithoutSkyline(Read(StyleFile)), "bare.json");
            int thinned = 0;
            foreach (string shape in Shapes)
            {
                var result = Build(shape, CourseSurroundingsTier.High);
                var plain = CourseSurroundings.Build(bare, Kit(), DepotId, Footprint(shape), CourseSurroundingsTier.High);
                Assert.IsTrue(plain.Skyline.Empty);
                Assert.IsFalse(plain.Canonical.Contains("skyline "), "a style without a skyline builds the town as before");
                Assert.AreEqual(plain.Count("card_hills_a"), result.Count("card_hills_a"), "the hills ring stays whole");
                Assert.AreEqual(plain.Count("card_roofline_a") - result.Skyline.ThinnedCards.Count, result.Count("card_roofline_a"));
                thinned += result.Skyline.ThinnedCards.Count;
                foreach (var card in result.Placements.Where(p => p.Card))
                    foreach (var t in result.Skyline.Towers)
                        Assert.IsFalse(Overlap(Lot(t), Corners(card)), $"{card.Piece} ({card.Source}) cuts through the tower at ({t.X}, {t.Z})");
            }
            Assert.Greater(thinned, 0, "some roofline cards give way to downtown towers");
        }

        [Test]
        public void TheDepotGetsADowntownAndARingOfBlocks()
        {
            var skyline = Build("depot", CourseSurroundingsTier.High).Skyline;
            string summary = $"{skyline.Towers.Count} towers, {skyline.Towers.Count(t => t.Downtown)} downtown, {skyline.Renderers} renderers, {skyline.Triangles} tris, " +
                $"facades {string.Join("/", skyline.Towers.GroupBy(t => t.Facade).Select(g => g.Key + " " + g.Count()))}";
            TestContext.WriteLine(summary);
            Assert.GreaterOrEqual(skyline.Towers.Count, 30, summary);
            Assert.GreaterOrEqual(skyline.Towers.Count(t => t.Downtown), 8, summary);
            Assert.GreaterOrEqual(skyline.Towers.Select(t => t.Facade).Distinct().Count(), 3, summary);
            Assert.GreaterOrEqual(skyline.Towers.Select(t => t.Wall).Distinct().Count(), 6, summary);
            Assert.That(skyline.Renderers, Is.InRange(3, CourseSkyline.HighRenderers), summary);
        }

        [Test]
        public void AHugeCourseKeepsTheSkylineInBudget()
        {
            var huge = new CourseFootprint(-300, -300, 300, 300, 0, 10);
            foreach (var tier in Tiers)
            {
                var result = CourseSurroundings.Draft(Style(), Kit(), "huge", huge, tier);
                Assert.IsTrue(result.Valid, string.Join("\n", result.Problems));
                Assert.IsNotEmpty(result.Skyline.Towers);
                Assert.LessOrEqual(result.Skyline.Triangles, result.Skyline.TriangleLimit);
                Assert.LessOrEqual(result.Skyline.Renderers, result.Skyline.RendererLimit);
            }
        }

        [Test]
        public void ABrokenSkylineStyleIsRefused()
        {
            string json = Read(StyleFile);
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"cell\": 36", "\"cell\": 20"), "cell.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"clusters\": [1, 2]", "\"clusters\": [1, 3]"), "clusters.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"glass\": [0.72, 0.82]", "\"glass\": [0.72, 0.95]"), "glass.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"height\": [48, 90]", "\"height\": [48, 260]"), "height.json"));
        }

        readonly struct Vec
        {
            readonly double x, y, z;

            public Vec(double x, double y, double z)
            {
                this.x = x; this.y = y; this.z = z;
            }

            public static Vec operator +(Vec a, Vec b) => new Vec(a.x + b.x, a.y + b.y, a.z + b.z);
            public static Vec operator -(Vec a, Vec b) => new Vec(a.x - b.x, a.y - b.y, a.z - b.z);
            public static Vec operator *(Vec a, double s) => new Vec(a.x * s, a.y * s, a.z * s);

            public double Dot(Vec o) => x * o.x + y * o.y + z * o.z;
            public Vec Cross(Vec o) => new Vec(y * o.z - z * o.y, z * o.x - x * o.z, x * o.y - y * o.x);
        }
    }
}
