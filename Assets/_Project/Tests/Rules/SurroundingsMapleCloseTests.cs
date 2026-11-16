using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Surroundings;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class SurroundingsMapleCloseTests
    {
        const string Folder = "Assets/_Project/Data/Surroundings";
        static readonly string[] RealKits = { "kit_buildings.json", "kit_street.json" };

        static string Read(string file) => File.ReadAllText(Path.Combine(TestData.Root, Folder, file));

        static SurroundingsPlan Plan() => SurroundingsPlan.FromJson(Read("pinwheel.json"), "pinwheel.json");

        static HouseLayout Pinwheel() => HouseLayout.FromJson(TestData.Read(Plan().HouseFile), Plan().HouseFile);

        static SurroundingsResult WithContractKit() => SurroundingsLayout.Build(Plan(), SurroundingsFixtures.ContractKit(), Pinwheel());

        static SurroundingsKit LoadRealKits()
        {
            string folder = Path.Combine(TestData.Root, Folder);
            var missing = RealKits.Where(k => !File.Exists(Path.Combine(folder, k))).ToList();
            if (missing.Count > 0)
                Assert.Fail($"Maple Close needs the real kit manifests in {Folder}; missing {string.Join(", ", missing)}. " +
                            "Lanes E2 (kit_buildings.json) and E3 (kit_street.json) write them from their Blender runs.");
            var kit = new SurroundingsKit();
            foreach (string file in Directory.GetFiles(folder, "kit_*.json").OrderBy(f => f, StringComparer.Ordinal))
                kit.Add(File.ReadAllText(file), Path.GetFileName(file));
            return kit;
        }

        [Test]
        public void MapleCloseBuildsWithTheRealKits()
        {
            var kit = LoadRealKits();
            SurroundingsResult r = null;
            try { r = SurroundingsLayout.Build(Plan(), kit, Pinwheel()); }
            catch (SurroundingsException e) { Assert.Fail(e.Message); }
            Assert.LessOrEqual(r.Budget.High.Renderers, SurroundingsBudget.HighRenderers);
            Assert.LessOrEqual(r.Budget.Low.Triangles, SurroundingsBudget.LowTriangles);
            AssertNeverCoversPlay(r, kit);
        }

        [Test]
        public void MapleCloseBuildsWithTheContractKit()
        {
            SurroundingsResult r = null;
            try { r = WithContractKit(); }
            catch (SurroundingsException e) { Assert.Fail(e.Message); }
            Assert.AreEqual("pinwheel", r.MapId);
            Assert.AreEqual("Maple Close", r.Name);
            Assert.AreEqual("MapleMorning", r.LightId);
            Assert.AreEqual("maple_close", r.AmbienceId);
            string budget = $"High {r.Budget.High.Renderers} renderers {r.Budget.High.Triangles} tris; Low {r.Budget.Low.Renderers} renderers {r.Budget.Low.Triangles} tris";
            Assert.LessOrEqual(r.Budget.High.Renderers, SurroundingsBudget.HighRenderers, budget);
            Assert.LessOrEqual(r.Budget.High.Triangles, SurroundingsBudget.HighTriangles, budget);
            Assert.LessOrEqual(r.Budget.Low.Renderers, SurroundingsBudget.LowRenderers, budget);
            Assert.LessOrEqual(r.Budget.Low.Triangles, SurroundingsBudget.LowTriangles, budget);
        }

        [Test]
        public void MapleCloseIsTheSameEveryTime()
        {
            var a = WithContractKit();
            var b = WithContractKit();
            Assert.AreEqual(a.Hash, b.Hash);
            Assert.AreEqual(a.Canonical, b.Canonical);
        }

        [Test]
        public void MapleClosePlayVolumeWrapsThePinwheel()
        {
            var v = WithContractKit().PlayVolume;
            Assert.AreEqual(-20.2f, v.MinX, 1e-4);
            Assert.AreEqual(20.2f, v.MaxX, 1e-4);
            Assert.AreEqual(-20.2f, v.MinZ, 1e-4);
            Assert.AreEqual(20.2f, v.MaxZ, 1e-4);
            Assert.AreEqual(-.3f, v.MinY, 1e-4);
            Assert.AreEqual(3.4f, v.MaxY, 1e-4);
        }

        [Test]
        public void MapleCloseNeverCoversPlay()
        {
            AssertNeverCoversPlay(WithContractKit(), SurroundingsFixtures.ContractKit());
        }

        [Test]
        public void MapleCloseHasAnEdgeOnEverySide()
        {
            var edging = WithContractKit().Placements.Where(p => p.Source == "edging").ToList();
            Assert.IsTrue(edging.Any(p => p.Piece == "hedge_2m" && p.Z > 20 && p.Z < 22.5), "north hedge");
            Assert.IsTrue(edging.Any(p => p.Piece == "hedge_2m" && p.X > 20 && p.X < 22.5), "east hedge");
            Assert.IsTrue(edging.Any(p => p.Piece == "hedge_2m" && p.Z < -21), "south hedge, pushed out");
            Assert.IsTrue(edging.Any(p => p.Piece == "fence_board_2m" && Math.Abs(p.X + 21.5) < .1), "west board fence at x -21.5");
            var east = edging.Where(p => p.X > 20 && p.X < 22.5).ToList();
            Assert.IsTrue(east.Any(p => p.Piece == "hedge_2m" && Math.Abs(p.Z) < 1), "no gate: the hedge runs unbroken past the Patio, so nothing implies an exit");
            Assert.IsFalse(WithContractKit().Placements.Any(p => p.Piece.StartsWith("gate_") && p.X > 20 && p.X < 27), "no gate on our side of the street");
            Assert.IsFalse(east.Any(p => p.Z > -9.5 && p.Z < -6.5), "the driveway stays open");
        }

        [Test]
        public void MapleCloseShowsDetailEastAndWestOfTheHouse()
        {
            var r = WithContractKit();
            var kit = SurroundingsFixtures.ContractKit();
            var near = r.Placements.Where(p => p.Ring == SurroundingsRing.Near).ToList();
            Assert.IsTrue(near.Any(p => p.Piece == "mailbox" && p.X > 26 && p.X < 26.9), "the mailbox stands at the pavement edge");
            Assert.IsTrue(near.Any(p => p.Piece == "car_estate" && p.X > 21 && p.X < 27), "the family car on the driveway");
            Assert.IsTrue(near.Any(p => p.Piece == "pavement_straight" && p.X > 26.5 && p.X < 29.5));
            Assert.IsTrue(r.Placements.Any(p => p.Piece == "lamp_post" && p.X > 27 && p.X < 29.2 && Math.Abs(p.Z) < 20), "lamp posts on the near pavement");
            Assert.IsTrue(r.Placements.Any(p => p.Piece.StartsWith("car_") && p.X > 29 && p.X < 32 && p.Source.EndsWith("parking")), "cars parked at the kerb");
            foreach (string piece in new[] { "shed_a", "trampoline", "washing_line" })
                Assert.IsTrue(r.Placements.Any(p => p.Piece == piece && p.X < -21.5 && p.X > -31.6), piece + " in the west neighbour's garden, inside the couch view");
            Assert.IsTrue(r.Placements.Where(p => p.X < -21.5 && p.X > -35 && Math.Abs(p.Z) < 16).All(p => p.Piece == "tree_round_c" || kit.Piece(p.Piece).Height <= 2.5), "the west garden stays low");
        }

        [Test]
        public void MapleCloseHasEightOrMoreTintedNeighbours()
        {
            var houses = WithContractKit().Placements.Where(p => p.Piece.StartsWith("house_") && !p.Piece.EndsWith("_far")).ToList();
            Assert.GreaterOrEqual(houses.Count, 8);
            Assert.IsTrue(houses.All(h => h.Ring == SurroundingsRing.Mid));
            Assert.GreaterOrEqual(houses.Select(h => h.PlasterTint).Distinct().Count(), 6, "pastel plaster tints vary");
            Assert.GreaterOrEqual(houses.Select(h => h.RoofTint).Distinct().Count(), 3, "roof tints vary");
            Assert.IsTrue(houses.Any(h => h.X >= 46 && h.Yaw == 270), "houses across the street face west");
            Assert.IsTrue(houses.Any(h => h.Z >= 32), "houses to the north");
            Assert.IsTrue(houses.Any(h => h.X <= -34), "the west neighbour");
            Assert.IsTrue(houses.Any(h => h.Z <= -40), "houses to the south");
        }

        [Test]
        public void MapleCloseLandmarkStandsNorthWithAClearSightline()
        {
            var r = WithContractKit();
            var tower = r.Placements.Single(p => p.Piece == "landmark_water_tower");
            Assert.AreEqual(0, tower.X, 1e-3);
            Assert.AreEqual(140, tower.Z - 20, 1);
            Assert.AreEqual(SurroundingsRing.Far, tower.Ring);
            Assert.IsTrue(r.Dropped.Any(d => d.Contains("sightline")), "the sightline from the garden cleared some fill");
        }

        [Test]
        public void MapleCloseFarRingHoldsOnlyFarPiecesCardsAndTheLandmark()
        {
            var r = WithContractKit();
            var far = r.Placements.Where(p => p.Ring == SurroundingsRing.Far).ToList();
            Assert.IsTrue(far.All(p => p.Piece.EndsWith("_far") || p.Card || p.Piece.StartsWith("landmark_")));
            Assert.IsTrue(far.Any(p => p.Piece == "tree_far"));
            Assert.IsTrue(far.Any(p => p.Piece.StartsWith("house_") && p.Piece.EndsWith("_far")));
            Assert.AreEqual(30, far.Count(p => p.Card));
            Assert.IsFalse(r.Placements.Any(p => p.Ring != SurroundingsRing.Far && p.Piece.EndsWith("_far")));
        }

        [Test]
        public void MapleCloseGroundReadsAsGardensVergesSoilAndAnOliveFarField()
        {
            var r = WithContractKit();
            Assert.AreEqual(1, r.Budget.High.GroundRenderers, "every ground zone shares the grass slot");
            Assert.AreEqual(1, r.Budget.Low.GroundRenderers);
            var lawns = r.Ground.Where(g => g.Source.StartsWith("lawn ") && !g.Source.EndsWith("stripe")).ToList();
            Assert.AreEqual(20, lawns.Count, "a front and a back lawn for each of the ten neighbours");
            Assert.GreaterOrEqual(lawns.Select(l => l.Tint).Distinct().Count(), 6);
            Assert.IsTrue(r.Ground.Any(g => g.Source.EndsWith("stripe")), "mown stripes");
            var hedges = r.Placements.Where(p => p.Piece == "hedge_2m" || p.Piece == "flowerbed_2m").ToList();
            var soil = r.Ground.Where(g => g.Source.StartsWith("soil")).ToList();
            Assert.IsTrue(hedges.All(h => soil.Any(s => s.MinX <= h.X && s.MaxX >= h.X && s.MinZ <= h.Z && s.MaxZ >= h.Z)), "soil under every hedge and flowerbed");
            var field = r.Ground.First();
            Assert.AreEqual(600f, field.Radius, 1e-3);
            Assert.AreEqual(0xBDB98Fu, field.Tint, "olive far field");
            var near = r.Ground.Single(g => g.Source == "ground fade 4");
            Assert.AreEqual(0xFFFFFFu, near.Tint, "verges keep the lightest tone");
            Assert.AreEqual(-.35, near.Y, 1e-5);
            Assert.IsTrue(r.Ground.Where(g => g.Source.StartsWith("lawn")).All(l => l.Tint != 0xFFFFFFu), "lawns are darker than the verges");
            Assert.LessOrEqual(r.Budget.Low.Renderers, SurroundingsBudget.LowRenderers);
        }

        [Test]
        public void MapleCloseMoversLoopTheFarRoad()
        {
            var r = WithContractKit();
            Assert.AreEqual(2, r.Movers.Count);
            Assert.IsTrue(r.Movers.All(m => m.Path.All(p => p.Z < -55)));
            Assert.AreEqual(.5, r.Movers[1].Phase - r.Movers[0].Phase, 1e-4, "half a loop apart, so they never meet");
        }

        static void AssertNeverCoversPlay(SurroundingsResult r, SurroundingsKit kit)
        {
            var v = r.PlayVolume;
            foreach (var p in r.Placements)
            {
                var piece = kit.Piece(p.Piece);
                var corners = Corners(p, piece);
                string where = $"{p.Piece} ({p.Source}) at ({p.X}, {p.Z})";
                Assert.IsFalse(Overlaps(corners, -20.3, -20.3, 20.3, 20.3), where + " is inside the footprint + 0.3 m");
                double reach = (p.Y + piece.Height * p.Scale - v.MinY) * PartyCamera.Back / PartyCamera.Rise;
                Assert.IsFalse(Overlaps(corners, v.MinX, v.MinZ - reach, v.MaxX, v.MaxZ), where + " covers the play volume in the couch view");
            }
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
