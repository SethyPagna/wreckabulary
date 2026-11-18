using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Surroundings;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class SurroundingsHousePlansTests
    {
        const string Folder = "Assets/_Project/Data/Surroundings";
        const string MapleClose = "pinwheel";

        static readonly string[] Maps = { "courtyard", "flat", "terrace", "walkup" };

        static readonly Dictionary<string, (int houses, int trees)> Targets = new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            ["courtyard"] = (20, 180),
            ["flat"] = (30, 150),
            ["terrace"] = (20, 150),
            ["walkup"] = (36, 130),
        };

        static readonly Dictionary<string, SurroundingsResult> Built = new Dictionary<string, SurroundingsResult>(StringComparer.Ordinal);
        static SurroundingsKit kit;

        static string Read(string file) => File.ReadAllText(Path.Combine(TestData.Root, Folder, file));

        static SurroundingsPlan Plan(string map) => SurroundingsPlan.FromJson(Read(map + ".json"), map + ".json");

        static HouseLayout HouseOf(SurroundingsPlan plan) => HouseLayout.FromJson(TestData.Read(plan.HouseFile), plan.HouseFile);

        static SurroundingsKit Kit()
        {
            if (kit != null) return kit;
            string folder = Path.Combine(TestData.Root, Folder);
            var loaded = new SurroundingsKit();
            foreach (string file in Directory.GetFiles(folder, "kit_*.json").OrderBy(f => f, StringComparer.Ordinal))
                loaded.Add(File.ReadAllText(file), Path.GetFileName(file));
            Assert.IsTrue(loaded.Has("house_detached_a") && loaded.Has("road_straight"), $"the real kit manifests in {Folder} are missing");
            return kit = loaded;
        }

        static SurroundingsResult Fresh(string map)
        {
            var plan = Plan(map);
            try { return SurroundingsLayout.Build(plan, Kit(), HouseOf(plan)); }
            catch (SurroundingsException e) { Assert.Fail(e.Message); return null; }
        }

        static SurroundingsResult Layout(string map)
        {
            if (!Built.TryGetValue(map, out var result)) Built[map] = result = Fresh(map);
            return result;
        }

        static List<SurroundingsPlacement> Neighbours(SurroundingsResult r) =>
            r.Placements.Where(p => p.Piece.StartsWith("house_", StringComparison.Ordinal) && !p.Piece.EndsWith("_far", StringComparison.Ordinal) && p.Source.StartsWith("plot ", StringComparison.Ordinal)).ToList();

        static int Trees(SurroundingsResult r) => r.Placements.Count(p => p.Piece.StartsWith("tree_", StringComparison.Ordinal));

        static int FarHouses(SurroundingsResult r) => r.Placements.Count(p => p.Piece.StartsWith("house_", StringComparison.Ordinal) && p.Piece.EndsWith("_far", StringComparison.Ordinal));

        static int Rooflines(SurroundingsResult r) => r.Placements.Count(p => p.Piece == "card_roofline_a");

        static string Budget(SurroundingsResult r) =>
            $"High {r.Budget.High.Renderers} renderers {r.Budget.High.Triangles} tris; Low {r.Budget.Low.Renderers} renderers {r.Budget.Low.Triangles} tris";

        [TestCaseSource(nameof(Maps))]
        public void EachPlanParsesForItsHouse(string map)
        {
            var plan = Plan(map);
            Assert.AreEqual(map, plan.MapId);
            Assert.AreEqual("house_" + map + ".json", plan.HouseFile);
            Assert.AreEqual("MapleMorning", plan.LightId);
            Assert.AreEqual("maple_close", plan.AmbienceId);
            Assert.IsFalse(string.IsNullOrWhiteSpace(plan.Name));
            Assert.IsFalse(plan.Name.IndexOf("Wordbound", StringComparison.OrdinalIgnoreCase) >= 0, "players never see the code name");
            Assert.Greater(HouseOf(plan).Rooms.Count, 0);
        }

        [Test]
        public void EveryNeighbourhoodHasItsOwnName()
        {
            var names = Maps.Concat(new[] { MapleClose }).Select(m => Plan(m).Name).ToList();
            Assert.AreEqual(names.Count, names.Distinct(StringComparer.OrdinalIgnoreCase).Count(), string.Join(", ", names));
        }

        [TestCaseSource(nameof(Maps))]
        public void EachPlanBuildsWithTheRealKits(string map)
        {
            var r = Layout(map);
            Assert.IsTrue(r.Valid, string.Join("\n", r.Problems));
            Assert.AreEqual(map, r.MapId);
            Assert.AreEqual(Plan(map).Name, r.Name);
        }

        [TestCaseSource(nameof(Maps))]
        public void EachPlanStaysInsideTheHighAndLowBudgets(string map)
        {
            var r = Layout(map);
            string budget = Budget(r);
            Assert.LessOrEqual(r.Budget.High.Renderers, SurroundingsBudget.HighRenderers, budget);
            Assert.LessOrEqual(r.Budget.High.Triangles, SurroundingsBudget.HighTriangles, budget);
            Assert.LessOrEqual(r.Budget.Low.Renderers, SurroundingsBudget.LowRenderers, budget);
            Assert.LessOrEqual(r.Budget.Low.Triangles, SurroundingsBudget.LowTriangles, budget);
            Assert.LessOrEqual(r.Movers.Count, SurroundingsBudget.MaxMovers);
            Assert.AreEqual(1, r.Budget.High.GroundRenderers, "every ground zone shares the grass slot");
        }

        [TestCaseSource(nameof(Maps))]
        public void EachPlanIsTheSameEveryTime(string map)
        {
            var a = Fresh(map);
            var b = Fresh(map);
            Assert.AreEqual(a.Hash, b.Hash);
            Assert.AreEqual(a.Canonical, b.Canonical);
        }

        [TestCaseSource(nameof(Maps))]
        public void EachPlanNeverIntrudesIntoThePlayBox(string map)
        {
            var r = Layout(map);
            var house = HouseOf(Plan(map));
            var v = r.PlayVolume;
            double minX = house.Rooms.Min(x => x.MinX), minZ = house.Rooms.Min(x => x.MinZ);
            double maxX = house.Rooms.Max(x => x.MaxX), maxZ = house.Rooms.Max(x => x.MaxZ);
            Assert.AreEqual(minX - SurroundingsLayout.PlayMargin, v.MinX, 1e-4);
            Assert.AreEqual(minZ - SurroundingsLayout.PlayMargin, v.MinZ, 1e-4);
            Assert.AreEqual(maxX + SurroundingsLayout.PlayMargin, v.MaxX, 1e-4);
            Assert.AreEqual(maxZ + SurroundingsLayout.PlayMargin, v.MaxZ, 1e-4);
            Assert.AreEqual(SurroundingsLayout.PlayFloor, v.MinY, 1e-4);
            Assert.AreEqual(house.Rooms.Max(x => x.FloorY) + SurroundingsLayout.PlayHeadroom, v.MaxY, 1e-4);
            double gap = SurroundingsLayout.Clearance;
            foreach (var p in r.Placements)
            {
                var piece = Kit().Piece(p.Piece);
                var corners = Corners(p, piece);
                string where = $"{p.Piece} ({p.Source}) at ({p.X}, {p.Z})";
                foreach (var room in house.Rooms)
                    Assert.IsFalse(Overlaps(corners, room.MinX - gap, room.MinZ - gap, room.MaxX + gap, room.MaxZ + gap), $"{where} is inside {room.Name} + {gap} m");
                double reach = (p.Y + piece.Height * p.Scale - v.MinY) * PartyCamera.Back / PartyCamera.Rise;
                Assert.IsFalse(Overlaps(corners, v.MinX, v.MinZ - reach, v.MaxX, v.MaxZ), where + " covers the play volume in the couch view");
            }
            foreach (var m in r.Movers)
                for (int i = 0; i < m.Path.Count; i++)
                {
                    var a = m.Path[i];
                    var b = m.Path[(i + 1) % m.Path.Count];
                    Assert.GreaterOrEqual(SegmentToRect(a.X, a.Z, b.X, b.Z, minX, minZ, maxX, maxZ), SurroundingsLayout.MoverClearance - 1e-3, $"{m.Piece} ({m.Source}) drives too close to the house");
                }
        }

        [TestCaseSource(nameof(Maps))]
        public void EachPlanFarRingHoldsOnlyFarPiecesCardsAndTheLandmark(string map)
        {
            var r = Layout(map);
            var far = r.Placements.Where(p => p.Ring == SurroundingsRing.Far).ToList();
            Assert.IsTrue(far.All(p => p.Piece.EndsWith("_far", StringComparison.Ordinal) || p.Card || p.Piece.StartsWith("landmark_", StringComparison.Ordinal)));
            Assert.IsFalse(r.Placements.Any(p => p.Ring != SurroundingsRing.Far && p.Piece.EndsWith("_far", StringComparison.Ordinal)));
            Assert.IsTrue(far.Any(p => p.Piece == "tree_far"));
            Assert.IsTrue(Neighbours(r).All(h => h.Ring == SurroundingsRing.Mid), "every neighbouring house stands in the mid ring");
        }

        [TestCaseSource(nameof(Maps))]
        public void EachPlanHasMoreMassThanMapleClose(string map)
        {
            var maple = Layout(MapleClose);
            var r = Layout(map);
            var target = Targets[map];
            int houses = Neighbours(r).Count, trees = Trees(r);
            Assert.GreaterOrEqual(houses, Math.Max(target.houses, Neighbours(maple).Count), "neighbouring houses on plots");
            Assert.GreaterOrEqual(trees, Math.Max(target.trees, Trees(maple)), "tree placements");
            Assert.Greater(FarHouses(r), FarHouses(maple), "far scatter houses");
            Assert.Greater(Rooflines(r), Rooflines(maple), "roofline cards");
            Assert.GreaterOrEqual(r.Placements.Where(p => p.Card).Select(p => p.Source).Distinct().Count(), 3, "two roofline rings and the hills");
            Assert.GreaterOrEqual(Neighbours(r).Select(h => h.PlasterTint).Distinct().Count(), 6, "plaster tints vary");
        }

        [TestCaseSource(nameof(Maps))]
        public void EachPlanHasStreetLifeAndALandmarkDueNorth(string map)
        {
            var r = Layout(map);
            var house = HouseOf(Plan(map));
            Assert.Greater(r.Placements.Count(p => p.Piece == "lamp_post"), 20, "lamps line the streets");
            Assert.Greater(r.Placements.Count(p => p.Source.EndsWith("parking", StringComparison.Ordinal)), 8, "cars parked at the kerb");
            Assert.GreaterOrEqual(r.Movers.Count, 2, "traffic on a street without parking");
            var tower = r.Placements.Single(p => p.Piece == "landmark_water_tower");
            Assert.AreEqual(SurroundingsRing.Far, tower.Ring);
            Assert.Greater(tower.Z, house.Rooms.Max(x => x.MaxZ) + 100);
            Assert.Less(Math.Abs(tower.X), tower.Z * Math.Tan(15 * Math.PI / 180));
        }

        [TestCaseSource(nameof(Maps))]
        public void EachPlanHasNeighboursNorthEastAndWest(string map)
        {
            var house = HouseOf(Plan(map));
            var near = Neighbours(Layout(map));
            Assert.IsTrue(near.Any(h => h.Z > house.Rooms.Max(x => x.MaxZ) + 10), "houses to the north, at the top of the couch view");
            Assert.IsTrue(near.Any(h => h.X > house.Rooms.Max(x => x.MaxX) + 8), "houses to the east");
            Assert.IsTrue(near.Any(h => h.X < house.Rooms.Min(x => x.MinX) - 8), "houses to the west");
        }

        [TestCase("courtyard")]
        [TestCase("flat")]
        [TestCase("walkup")]
        public void TheSquareAndTownBlocksHaveNeighboursOnAllFourSides(string map)
        {
            var house = HouseOf(Plan(map));
            var near = Neighbours(Layout(map));
            double minX = house.Rooms.Min(x => x.MinX), minZ = house.Rooms.Min(x => x.MinZ);
            double maxX = house.Rooms.Max(x => x.MaxX), maxZ = house.Rooms.Max(x => x.MaxZ);
            bool Alongside(SurroundingsPlacement h) => h.Z > minZ - 6 && h.Z < maxZ + 6;
            Assert.GreaterOrEqual(near.Count(h => h.Z < minZ - 10), 6, "a row of houses to the south");
            Assert.GreaterOrEqual(near.Count(h => h.Z > maxZ + 10), 6, "a row of houses to the north");
            Assert.GreaterOrEqual(near.Count(h => h.X > maxX + 8 && Alongside(h)), 3, "houses facing the east side");
            Assert.GreaterOrEqual(near.Count(h => h.X < minX - 8 && Alongside(h)), 3, "houses facing the west side");
        }

        [Test]
        public void TheCourtyardSquareIsLinedWithTrees()
        {
            var lining = Layout("courtyard").Placements.Where(p => p.Source.StartsWith("belt ", StringComparison.Ordinal) && p.Ring == SurroundingsRing.Near).ToList();
            Assert.GreaterOrEqual(lining.Count, 30);
            Assert.IsTrue(lining.Any(p => p.Z > 28) && lining.Any(p => p.Z < -28) && lining.Any(p => p.X > 28) && lining.Any(p => p.X < -28), "trees on all four sides of the square");
        }

        [Test]
        public void TheTerraceRowCarriesOnEastAndWestOfTheHouse()
        {
            var house = HouseOf(Plan("terrace"));
            double front = house.Rooms.Max(x => x.MaxZ);
            var near = Neighbours(Layout("terrace"));
            var row = near.Where(h => Math.Abs(h.Yaw) < 1e-3 && h.Z < front && h.Z > front - 12).ToList();
            Assert.GreaterOrEqual(row.Count(h => h.X > 0), 4, "the row continues east, facing the street like the house");
            Assert.GreaterOrEqual(row.Count(h => h.X < 0), 4, "the row continues west");
            Assert.GreaterOrEqual(near.Count(h => Math.Abs(h.Yaw - 180) < 1e-3 && h.Z > front + 10), 10, "the opposite row across the street");
        }

        static double SegmentToRect(double ax, double az, double bx, double bz, double minX, double minZ, double maxX, double maxZ)
        {
            double best = double.MaxValue;
            for (int i = 0; i <= 200; i++)
            {
                double t = i / 200.0, x = ax + (bx - ax) * t, z = az + (bz - az) * t;
                double dx = Math.Max(0, Math.Max(minX - x, x - maxX)), dz = Math.Max(0, Math.Max(minZ - z, z - maxZ));
                best = Math.Min(best, Math.Sqrt(dx * dx + dz * dz));
            }
            return best;
        }

        static (double x, double z)[] Corners(SurroundingsPlacement p, SurroundingsPiece piece)
        {
            double r = p.Yaw * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
            double hw = piece.Width * p.Scale * .5, hd = piece.Depth * p.Scale * .5;
            return new[] { (-hw, -hd), (hw, -hd), (hw, hd), (-hw, hd) }
                .Select(l => (p.X + l.Item1 * c + l.Item2 * s, p.Z - l.Item1 * s + l.Item2 * c)).ToArray();
        }

        static bool Overlaps((double x, double z)[] box, double minX, double minZ, double maxX, double maxZ)
        {
            if (box.Max(q => q.x) <= minX || box.Min(q => q.x) >= maxX || box.Max(q => q.z) <= minZ || box.Min(q => q.z) >= maxZ) return false;
            var rect = new[] { (minX, minZ), (maxX, minZ), (maxX, maxZ), (minX, maxZ) };
            for (int i = 0; i < 2; i++)
            {
                double ax = box[i + 1].x - box[i].x, az = box[i + 1].z - box[i].z;
                double nx = -az, nz = ax;
                double bMin = box.Min(q => q.x * nx + q.z * nz), bMax = box.Max(q => q.x * nx + q.z * nz);
                double rMin = rect.Min(q => q.Item1 * nx + q.Item2 * nz), rMax = rect.Max(q => q.Item1 * nx + q.Item2 * nz);
                if (bMax <= rMin + 1e-9 || rMax <= bMin + 1e-9) return false;
            }
            return true;
        }
    }
}
