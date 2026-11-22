using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Surroundings;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class CourseSurroundingsTests
    {
        const string Folder = "Assets/_Project/Data/Surroundings";
        const string StyleFile = "Courses/course_city.json";
        const string DepotId = "wordbound-parcel-service-v2";
        const double Tolerance = 1e-3;

        static readonly string[] Shapes = { "arena", "depot", "flagship", "offset", "ribbon" };
        static readonly CourseSurroundingsTier[] Tiers = { CourseSurroundingsTier.High, CourseSurroundingsTier.Low };
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

        static CourseFootprint Footprint(string shape)
        {
            switch (shape)
            {
                case "arena": return new CourseFootprint(-10, -10, 10, 10, 0, 4);
                case "depot": return new CourseFootprint(-36, -20, 36, 20, 0, 7.2f);
                case "flagship": return new CourseFootprint(-48, -42, 48, 18, 0, 30);
                case "offset": return new CourseFootprint(130.5f, -260.25f, 171.25f, -228f, 12.5f, 18f);
                default: return new CourseFootprint(-70, -6, 70, 6, 0, 3);
            }
        }

        static CourseSurroundingsResult Build(string shape, CourseSurroundingsTier tier, string id = DepotId)
        {
            try { return CourseSurroundings.Build(Style(), Kit(), id, Footprint(shape), tier); }
            catch (SurroundingsException e) { Assert.Fail(e.Message); return null; }
        }

        static IEnumerable<TestCaseData> Cases() =>
            from shape in Shapes from tier in Tiers select new TestCaseData(shape, tier).SetName($"{shape} {tier}");

        static (double x, double z)[] Corners(SurroundingsPlacement p)
        {
            var piece = Kit().Piece(p.Piece);
            double yaw = p.Yaw * Math.PI / 180, hw = piece.Width * p.Scale * .5, hd = piece.Depth * p.Scale * .5;
            double ax = Math.Sin(yaw), az = Math.Cos(yaw), rx = az, rz = -ax;
            return new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) }
                .Select(c => (p.X + rx * hw * c.Item1 + ax * hd * c.Item2, p.Z + rz * hw * c.Item1 + az * hd * c.Item2)).ToArray();
        }

        static bool Overlaps((double x, double z)[] shape, double minX, double minZ, double maxX, double maxZ)
        {
            var box = new[] { (minX, minZ), (maxX, minZ), (maxX, maxZ), (minX, maxZ) };
            foreach (var polygon in new[] { shape, box })
                for (int i = 0; i < 4; i++)
                {
                    var a = polygon[i];
                    var b = polygon[(i + 1) % 4];
                    double nx = -(b.Item2 - a.Item2), nz = b.Item1 - a.Item1;
                    double sMin = shape.Min(p => p.x * nx + p.z * nz), sMax = shape.Max(p => p.x * nx + p.z * nz);
                    double bMin = box.Min(p => p.Item1 * nx + p.Item2 * nz), bMax = box.Max(p => p.Item1 * nx + p.Item2 * nz);
                    if (sMax <= bMin + Tolerance || bMax <= sMin + Tolerance) return false;
                }
            return true;
        }

        [Test]
        public void TheStyleParsesAndNamesOnlyKitPieces()
        {
            var s = Style();
            Assert.AreEqual("course_city", s.Id);
            Assert.IsFalse(string.IsNullOrWhiteSpace(s.Name));
            Assert.IsFalse(s.Name.IndexOf("Wordbound", StringComparison.OrdinalIgnoreCase) >= 0, "players never see the code name");
            Assert.GreaterOrEqual(s.GroundDrop, CourseSurroundingsStyle.MinGroundDrop);
            foreach (string id in s.Pieces) Assert.IsTrue(Kit().Has(id), $"the style names {id}, which the kit lacks");
        }

        [Test]
        public void TheSameCourseAndBoundsAlwaysGiveTheSameCity()
        {
            foreach (var tier in Tiers)
            {
                var first = Build("depot", tier);
                var second = Build("depot", tier);
                Assert.AreEqual(first.Hash, second.Hash, tier.ToString());
                Assert.AreEqual(first.Canonical, second.Canonical, tier.ToString());
            }
        }

        [Test]
        public void ADifferentCourseOrSizeGivesADifferentCity()
        {
            string depot = Build("depot", CourseSurroundingsTier.High).Hash;
            Assert.AreNotEqual(depot, Build("depot", CourseSurroundingsTier.High, "wordbound-lost-address").Hash);
            Assert.AreNotEqual(depot, Build("flagship", CourseSurroundingsTier.High).Hash);
        }

        [TestCaseSource(nameof(Cases))]
        public void NothingStandsInsideTheClearMargin(string shape, CourseSurroundingsTier tier)
        {
            var result = Build(shape, tier);
            var f = result.Footprint;
            double clear = result.Clear;
            Assert.GreaterOrEqual(result.Distances.Min(), clear - Tolerance);
            foreach (var p in result.Placements)
            {
                Assert.IsFalse(Overlaps(Corners(p), f.MinX - clear, f.MinZ - clear, f.MaxX + clear, f.MaxZ + clear),
                    $"{p.Piece} ({p.Source}) at ({p.X}, {p.Z}) is inside the course bounds plus {clear} m");
                Assert.LessOrEqual(p.Y, f.Floor - CourseSurroundingsStyle.MinGroundDrop + .01f, $"{p.Piece} stands above the street level");
            }
            foreach (var g in result.Ground)
                Assert.Less(g.Y, f.Floor - CourseSurroundingsStyle.MinGroundDrop + .1f, $"{g.Source} is not below the course floor slab");
        }

        [TestCaseSource(nameof(Cases))]
        public void EachTierFitsItsBudget(string shape, CourseSurroundingsTier tier)
        {
            var result = Build(shape, tier);
            Assert.IsTrue(result.Valid, string.Join("\n", result.Problems));
            Assert.LessOrEqual(result.Renderers, result.RendererLimit);
            Assert.LessOrEqual(result.Triangles, result.TriangleLimit);
            Assert.AreEqual(tier == CourseSurroundingsTier.High ? CourseSurroundings.HighRenderers : CourseSurroundings.LowRenderers, result.RendererLimit);
        }

        [TestCaseSource(nameof(Shapes))]
        public void TheLowTierIsALighterCopyOfTheHighTier(string shape)
        {
            var high = Build(shape, CourseSurroundingsTier.High);
            var low = Build(shape, CourseSurroundingsTier.Low);
            Assert.Less(low.Placements.Count, high.Placements.Count);
            Assert.Less(low.Triangles, high.Triangles);
            Assert.LessOrEqual(low.Renderers, high.Renderers);
            var spots = new HashSet<(float, float, float)>(high.Placements.Select(p => (p.X, p.Z, p.Yaw)));
            foreach (var p in low.Placements) Assert.IsTrue(spots.Contains((p.X, p.Z, p.Yaw)), $"{p.Piece} at ({p.X}, {p.Z}) is not in the high tier");
            Assert.Zero(low.Count("lamp_post") + low.Count("landmark_water_tower") + low.Count("car_hatch") + low.Count("car_estate") + low.Count("van_small"));
            Assert.IsFalse(low.Placements.Any(p => p.Piece.StartsWith("house_", StringComparison.Ordinal) && !p.Piece.EndsWith("_far", StringComparison.Ordinal)), "low uses only stand-in houses");
            CollectionAssert.Contains(low.SkippedSlots, "bk_line");
            Assert.IsFalse(low.Groups.Any(g => g.Slot == "bk_line"));
            Assert.IsTrue(low.Groups.All(g => g.Ring == SurroundingsRing.Far), "low casts no shadows");
        }

        [Test]
        public void TheDepotGetsStreetsHousesTreesAndASkyline()
        {
            var result = Build("depot", CourseSurroundingsTier.High);
            int roads = result.Count("road_straight"), pavements = result.Count("pavement_straight");
            int houses = result.Placements.Count(p => p.Piece.StartsWith("house_", StringComparison.Ordinal) && !p.Piece.EndsWith("_far", StringComparison.Ordinal));
            int standIns = result.Placements.Count(p => p.Piece.StartsWith("house_", StringComparison.Ordinal) && p.Piece.EndsWith("_far", StringComparison.Ordinal));
            int trees = result.Placements.Count(p => p.Piece.StartsWith("tree_", StringComparison.Ordinal));
            int cards = result.Placements.Count(p => p.Card);
            string summary = $"roads {roads}, pavements {pavements}, houses {houses}, stand-ins {standIns}, trees {trees}, cards {cards}, " +
                $"lamps {result.Count("lamp_post")}, {result.Renderers} renderers, {result.Triangles} tris, trimmed {result.Trimmed}";
            TestContext.WriteLine(summary);
            Assert.GreaterOrEqual(roads, 60, summary);
            Assert.GreaterOrEqual(pavements, 100, summary);
            Assert.GreaterOrEqual(houses, 15, summary);
            Assert.GreaterOrEqual(standIns, 30, summary);
            Assert.GreaterOrEqual(trees, 20, summary);
            Assert.GreaterOrEqual(cards, 30, summary);
            Assert.GreaterOrEqual(result.Count("lamp_post"), 10, summary);
            Assert.AreEqual(1, result.Count("landmark_water_tower"), summary);
            Assert.IsTrue(result.Groups.Any(g => g.Ring == SurroundingsRing.Near) && result.Groups.Any(g => g.Ring == SurroundingsRing.Far));
        }

        [Test]
        public void TheCourseSitsOnAPavedLotInsideTheGround()
        {
            var result = Build("depot", CourseSurroundingsTier.High);
            var f = result.Footprint;
            var apron = result.Ground.Single(g => g.Source == "apron");
            Assert.LessOrEqual(apron.MinX, f.MinX - result.Clear);
            Assert.GreaterOrEqual(apron.MaxX, f.MaxX + result.Clear);
            Assert.LessOrEqual(apron.MinZ, f.MinZ - result.Clear);
            Assert.GreaterOrEqual(apron.MaxZ, f.MaxZ + result.Clear);
            var outer = result.Ground.OrderByDescending(g => g.Radius).First();
            Assert.AreEqual(SurroundingsGroundShape.Disc, outer.Shape);
            Assert.Greater(outer.Radius, result.Placements.Max(p => Math.Sqrt((p.X - f.CentreX) * (p.X - f.CentreX) + (p.Z - f.CentreZ) * (p.Z - f.CentreZ))));
        }

        [Test]
        public void AHugeCourseIsTrimmedToItsBudget()
        {
            var huge = new CourseFootprint(-300, -300, 300, 300, 0, 10);
            foreach (var tier in Tiers)
            {
                var result = CourseSurroundings.Draft(Style(), Kit(), "huge", huge, tier);
                Assert.IsTrue(result.Valid, string.Join("\n", result.Problems));
                Assert.LessOrEqual(result.Triangles, result.TriangleLimit);
                Assert.LessOrEqual(result.Renderers, result.RendererLimit);
                Assert.GreaterOrEqual(result.Distances.Min(), result.Clear - Tolerance);
            }
        }

        [Test]
        public void TheFootprintComesFromTheModuleBoxesInMillimetres()
        {
            var boxes = new[]
            {
                new CourseBox(new CoursePoint(-12000, 0, -4000), new CoursePoint(8000, 3600, 4000)),
                new CourseBox(new CoursePoint(0, 4000, 2000), new CoursePoint(26000, 7200, 10000)),
            };
            var f = CourseFootprint.Of(boxes);
            Assert.AreEqual(-12f, f.MinX, 1e-4);
            Assert.AreEqual(-4f, f.MinZ, 1e-4);
            Assert.AreEqual(26f, f.MaxX, 1e-4);
            Assert.AreEqual(10f, f.MaxZ, 1e-4);
            Assert.AreEqual(0f, f.Floor, 1e-4);
            Assert.AreEqual(7.2f, f.Top, 1e-4);
            Assert.Throws<ArgumentException>(() => CourseFootprint.Of(Array.Empty<CourseBox>()));
            Assert.Throws<ArgumentException>(() => new CourseFootprint(0, 0, 0, 5, 0, 1));
            Assert.Throws<ArgumentException>(() => new CourseFootprint(0, 0, 5, 5, 2, 1));
        }

        [Test]
        public void ABrokenStyleIsRefused()
        {
            string json = Read(StyleFile);
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"groundDrop\": 0.35", "\"groundDrop\": 0.1"), "drop.json"));
            Assert.Throws<FormatException>(() => CourseSurroundingsStyle.FromJson(json.Replace("\"clear\": 4.0", "\"clear\": 0.2"), "clear.json"));
            var unknown = CourseSurroundingsStyle.FromJson(json.Replace("\"landmark_water_tower\"", "\"landmark_missing\""), "unknown.json");
            var result = CourseSurroundings.Draft(unknown, Kit(), DepotId, Footprint("depot"), CourseSurroundingsTier.High);
            Assert.IsFalse(result.Valid);
            StringAssert.Contains("landmark_missing", string.Join("\n", result.Problems));
            Assert.Throws<SurroundingsException>(() => CourseSurroundings.Build(unknown, Kit(), DepotId, Footprint("depot"), CourseSurroundingsTier.High));
        }
    }
}
