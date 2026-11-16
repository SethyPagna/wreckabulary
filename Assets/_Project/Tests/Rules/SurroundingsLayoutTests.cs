using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Surroundings;

namespace Wreckabulary.Rules.Tests
{
    public static class SurroundingsFixtures
    {
        static readonly string[] HouseSlots = { "bk_plaster", "bk_roof", "bk_paint", "bk_glass_dark", "bk_glass_lit" };

        public static string Piece(string id, string family, double w, double d, double h, int tris, string slots, string far = null)
        {
            string list = string.Join(",", slots.Split(' ').Select(s => "\"" + s + "\""));
            string farPart = far == null ? "" : $", \"far\": \"{far}\"";
            return string.Format(CultureInfo.InvariantCulture,
                "{{ \"id\": \"{0}\", \"family\": \"{1}\", \"width\": {2}, \"depth\": {3}, \"height\": {4}, \"tris\": {5}, \"slots\": [{6}]{7} }}",
                id, family, w, d, h, tris, list, farPart);
        }

        public static string Manifest(string kit, IEnumerable<string> pieces) =>
            $"{{ \"kit\": \"{kit}\", \"fbx\": \"Assets/_Project/Art/Surroundings/Kit/{kit}.fbx\", \"pieces\": [ {string.Join(", ", pieces)} ] }}";

        public static IEnumerable<string> ContractBuildings()
        {
            string houseSlots = string.Join(" ", HouseSlots);
            foreach (var (id, w, d, h, tris) in new[] { ("house_detached_a", 10.0, 9.0, 8.6, 2000), ("house_detached_b", 11.0, 8.0, 8.2, 2000), ("house_detached_c", 9.0, 10.0, 7.6, 2000), ("house_bungalow_a", 12.0, 8.0, 5.6, 1600) })
            {
                yield return Piece(id, "house", w, d, h, tris, houseSlots, id + "_far");
                yield return Piece(id + "_far", "house", w, d, h, 160, "bk_plaster bk_roof");
            }
            yield return Piece("garage_a", "garage", 4, 6, 3, 500, "bk_plaster bk_roof bk_paint");
            yield return Piece("shed_a", "shed", 2.4, 2, 2.4, 400, "bk_wood bk_roof");
            yield return Piece("landmark_water_tower", "landmark", 8, 8, 24, 1500, "bk_metal bk_paint bk_wood");
            yield return Piece("card_roofline_a", "card", 80, .2, 14, 120, "bk_card");
            yield return Piece("card_hills_a", "card", 160, .2, 30, 80, "bk_card");
        }

        public static IEnumerable<string> ContractStreet()
        {
            yield return Piece("road_straight", "road", 7, 8, .05, 60, "bk_road bk_line");
            yield return Piece("road_bend", "road", 11, 11, .05, 200, "bk_road bk_line");
            yield return Piece("road_circle", "road", 22, 22, .05, 300, "bk_road bk_stone");
            yield return Piece("pavement_straight", "pavement", 2.2, 8, .15, 80, "bk_stone");
            yield return Piece("pavement_corner", "pavement", 2.2, 2.2, .15, 150, "bk_stone");
            yield return Piece("driveway", "ground", 3, 6, .04, 40, "bk_stone");
            yield return Piece("verge", "ground", 2, 8, .05, 20, "bk_grass");
            yield return Piece("hedge_2m", "hedge", 2, .8, 1.4, 150, "bk_leaf");
            yield return Piece("hedge_end", "hedge", .8, .8, 1.4, 100, "bk_leaf");
            yield return Piece("fence_picket_2m", "fence", 2, .1, 1.0, 120, "bk_paint");
            yield return Piece("fence_board_2m", "fence", 2, .1, 1.8, 60, "bk_wood");
            yield return Piece("gate_picket", "fence", 1.2, .1, 1.0, 120, "bk_paint");
            yield return Piece("lamp_post", "street", .3, .3, 4.6, 200, "bk_metal bk_glass_lit");
            yield return Piece("mailbox", "street", .45, .4, 1.25, 150, "bk_metal bk_paint");
            yield return Piece("bin_wheelie", "street", .6, .7, 1.05, 200, "bk_metal");
            yield return Piece("bench_park", "street", 1.8, .6, .85, 250, "bk_wood bk_metal");
            yield return Piece("planter_box", "street", 1.2, .5, .6, 150, "bk_wood bk_leaf");
            yield return Piece("tree_round_a", "tree", 4, 4, 6, 500, "bk_leaf bk_bark", "tree_far");
            yield return Piece("tree_round_b", "tree", 5.5, 5.5, 7.5, 600, "bk_leaf bk_bark", "tree_far");
            yield return Piece("tree_round_c", "tree", 3, 3, 4.5, 400, "bk_leaf bk_bark", "tree_far");
            yield return Piece("tree_poplar", "tree", 2.2, 2.2, 9, 400, "bk_leaf bk_bark", "tree_far");
            yield return Piece("tree_far", "tree", 4, 4, 7, 60, "bk_leaf bk_bark");
            yield return Piece("bush_a", "bush", 1.2, 1.2, .9, 200, "bk_leaf");
            yield return Piece("bush_b", "bush", 1.6, 1.6, 1.1, 250, "bk_leaf");
            yield return Piece("flowerbed_2m", "flower", 2, .8, .35, 200, "bk_leaf bk_paint");
            yield return Piece("car_hatch", "vehicle", 1.8, 3.9, 1.5, 700, "bk_paint bk_metal bk_glass_dark");
            yield return Piece("car_estate", "vehicle", 1.85, 4.5, 1.55, 800, "bk_paint bk_metal bk_glass_dark");
            yield return Piece("van_small", "vehicle", 2.0, 4.8, 2.1, 800, "bk_paint bk_metal bk_glass_dark");
            yield return Piece("trampoline", "garden", 3, 3, .9, 400, "bk_metal bk_paint");
            yield return Piece("washing_line", "garden", 4, .4, 2.0, 200, "bk_wood bk_paint");
            yield return Piece("swing_set", "garden", 3, 1.5, 2.2, 300, "bk_metal bk_paint");
            yield return Piece("garden_table_set", "garden", 1.8, 1.8, .8, 400, "bk_wood");
            yield return Piece("sprinkler", "garden", .3, .3, .3, 60, "bk_metal");
        }

        public static SurroundingsKit ContractKit(params string[] extra) =>
            SurroundingsKit.FromJson(Manifest("buildings", ContractBuildings()), "kit_buildings.json")
                .Add(Manifest("street", ContractStreet().Concat(extra)), "kit_street.json");

        public static SurroundingsKit TestKit() => ContractKit(
            Piece("post", "street", .4, .4, 2.0, 50, "bk_metal"),
            Piece("board", "fence", 3, .2, 1.0, 50, "bk_wood"),
            Piece("mast", "street", .3, .3, 4.0, 50, "bk_metal"),
            Piece("hut", "street", 2, 2, 3.2, 50, "bk_wood"),
            Piece("house_box", "house", 6, 6, 5, 500, "bk_plaster bk_roof"),
            Piece("tower_box", "building", 6, 6, 9, 500, "bk_plaster"),
            Piece("heavy", "street", 2, 2, 2, 50000, "bk_stone"),
            Piece("rainbow_a", "street", 1, 1, 1, 10, "bk_plaster bk_brick bk_stone bk_roof bk_wood"),
            Piece("rainbow_b", "street", 1, 1, 1, 10, "bk_paint bk_metal bk_glass_dark bk_glass_lit bk_leaf"),
            Piece("rainbow_c", "street", 1, 1, 1, 10, "bk_bark bk_road bk_line"));

        public static RoomBox Room(string name, float minX, float minZ, float maxX, float maxZ, bool outdoor = false, float floorY = 0f) =>
            new RoomBox { Name = name, MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ, Outdoor = outdoor, FloorY = floorY };

        public static HouseLayout House(params RoomBox[] rooms)
        {
            var house = new HouseLayout { Name = "test" };
            house.Rooms.AddRange(rooms);
            return house;
        }

        public static HouseLayout Hall() => House(Room("Hall", -10, -10, 10, 10));

        public static HouseLayout Garden() => House(Room("Garden", -10, -10, 10, 10, outdoor: true));

        public static string Plan(string body, string map = "test") =>
            "{ \"map\": \"" + map + "\", \"name\": \"Test\", \"house\": \"test.json\", \"light\": \"TestLight\", \"ambience\": \"test_amb\"" +
            (string.IsNullOrEmpty(body) ? "" : ", " + body) + " }";

        public static string Prop(string piece, double x, double z, double yaw = 0, double scale = 1) =>
            string.Format(CultureInfo.InvariantCulture, "{{ \"piece\": \"{0}\", \"at\": [{1}, {2}], \"yaw\": {3}, \"scale\": {4} }}", piece, x, z, yaw, scale);

        public static string Props(params string[] props) => "\"props\": [" + string.Join(", ", props) + "]";

        public static SurroundingsResult Draft(string body, HouseLayout house = null, string map = "test") =>
            SurroundingsLayout.Draft(SurroundingsPlan.FromJson(Plan(body, map), "test.json"), TestKit(), house ?? Hall());

        public static bool Has(SurroundingsResult r, string rule) => r.Problems.Any(p => p.StartsWith(rule + ":", StringComparison.Ordinal));

        public static string Show(SurroundingsResult r) => r.Problems.Count == 0 ? "no problems" : string.Join("\n", r.Problems);
    }

    [TestFixture]
    public class SurroundingsLayoutTests
    {
        [Test]
        public void ResultCarriesIdsAndAGroundDisc()
        {
            var r = SurroundingsFixtures.Draft("");
            Assert.IsTrue(r.Valid, SurroundingsFixtures.Show(r));
            Assert.AreEqual("test", r.MapId);
            Assert.AreEqual("TestLight", r.LightId);
            Assert.AreEqual("test_amb", r.AmbienceId);
            Assert.AreEqual(1, r.Ground.Count);
            Assert.AreEqual(SurroundingsGroundShape.Disc, r.Ground[0].Shape);
            Assert.AreEqual(600f, r.Ground[0].Radius, 1e-4);
            Assert.AreEqual(-.35f, r.Ground[0].Y, 1e-4);
        }

        [Test]
        public void PlayVolumeIsTheFootprintPlusMarginFromTheVolumeFloorToTheTopFloorHeadroom()
        {
            var house = SurroundingsFixtures.House(SurroundingsFixtures.Room("Hall", -10, -6, 12, 8), SurroundingsFixtures.Room("Loft", -4, -6, 4, 2, floorY: 3f));
            var v = SurroundingsFixtures.Draft("", house).PlayVolume;
            Assert.AreEqual(-10.2f, v.MinX, 1e-4);
            Assert.AreEqual(-6.2f, v.MinZ, 1e-4);
            Assert.AreEqual(12.2f, v.MaxX, 1e-4);
            Assert.AreEqual(8.2f, v.MaxZ, 1e-4);
            Assert.AreEqual(-.3f, v.MinY, 1e-4, "just above the ground top, so a ground fragment never has the volume behind it");
            Assert.AreEqual(6.4f, v.MaxY, 1e-4);
        }

        [Test]
        public void BuildThrowsWithEveryProblemAndDraftDoesNot()
        {
            string body = SurroundingsFixtures.Props(SurroundingsFixtures.Prop("post", 10.3, 0), SurroundingsFixtures.Prop("nope", 30, 0));
            var plan = SurroundingsPlan.FromJson(SurroundingsFixtures.Plan(body), "test.json");
            var e = Assert.Throws<SurroundingsException>(() => SurroundingsLayout.Build(plan, SurroundingsFixtures.TestKit(), SurroundingsFixtures.Hall()));
            Assert.IsTrue(e.Problems.Any(p => p.StartsWith("clearance:")), e.Message);
            Assert.IsTrue(e.Problems.Any(p => p.StartsWith("kit:")), e.Message);
            Assert.DoesNotThrow(() => SurroundingsLayout.Draft(plan, SurroundingsFixtures.TestKit(), SurroundingsFixtures.Hall()));
        }

        [Test]
        public void PlacementsAreRoundedSoTheBakeGetsExactValues()
        {
            var r = SurroundingsFixtures.Draft(SurroundingsFixtures.Props(SurroundingsFixtures.Prop("post", 30.12345, 4.98765, 12.3456, 1.23456)));
            var p = r.Placements.Single();
            Assert.AreEqual(30.123, p.X, 1e-5);
            Assert.AreEqual(4.988, p.Z, 1e-5);
            Assert.AreEqual(12.35, p.Yaw, 1e-4);
            Assert.AreEqual(1.235, p.Scale, 1e-5);
            Assert.AreEqual(-.35, p.Y, 1e-5);
        }

        [Test]
        public void PiecesOnThePlinthSitOnItsTop()
        {
            var r = SurroundingsFixtures.Draft(SurroundingsFixtures.Props(SurroundingsFixtures.Prop("post", 10.8, 0), SurroundingsFixtures.Prop("post", 12, 0)));
            Assert.AreEqual(-.15, r.Placements[0].Y, 1e-5);
            Assert.AreEqual(-.35, r.Placements[1].Y, 1e-5);
        }

        [Test]
        public void TintsGoToTheirSlots()
        {
            string body = "\"props\": [ { \"piece\": \"house_box\", \"at\": [40, 0], \"plaster\": \"#FFE9E0\", \"roof\": \"#9FA3AD\", \"paint\": \"#C8564B\" } ]";
            var p = SurroundingsFixtures.Draft(body).Placements.Single();
            Assert.AreEqual(0xFFE9E0u, p.TintFor("bk_plaster"));
            Assert.AreEqual(0x9FA3ADu, p.TintFor("bk_roof"));
            Assert.AreEqual(0xC8564Bu, p.TintFor("bk_paint"));
            Assert.AreEqual(0xFFFFFFu, p.TintFor("bk_glass_dark"));
        }

        [Test]
        public void SameInputsGiveTheSameHashAndTheMapIdSeedsTheJitter()
        {
            string body = "\"treeBelts\": [ { \"pieces\": [\"tree_round_a\", \"tree_round_b\"], \"from\": [-40, 30], \"to\": [40, 30], \"every\": 9, \"jitter\": 3, \"scale\": [0.9, 1.1] } ]," +
                          "\"farScatter\": { \"from\": 72, \"to\": 140, \"cell\": 30, \"houses\": [\"house_detached_a_far\"], \"houseChance\": 0.5, \"treeChance\": 0.3 }";
            var a = SurroundingsFixtures.Draft(body);
            var b = SurroundingsFixtures.Draft(body);
            var other = SurroundingsFixtures.Draft(body, map: "other");
            Assert.IsTrue(a.Valid, SurroundingsFixtures.Show(a));
            Assert.AreEqual(a.Canonical, b.Canonical);
            Assert.AreEqual(a.Hash, b.Hash);
            Assert.AreEqual(16, a.Hash.Length);
            Assert.AreNotEqual(a.Hash, other.Hash, "the jitter is seeded from the map id");
            Assert.IsTrue(a.Placements.Any(p => p.Ring == SurroundingsRing.Far && p.Piece.EndsWith("_far")));
        }

        [Test]
        public void HashChangesWhenAPlacementMoves()
        {
            var a = SurroundingsFixtures.Draft(SurroundingsFixtures.Props(SurroundingsFixtures.Prop("post", 30, 0)));
            var b = SurroundingsFixtures.Draft(SurroundingsFixtures.Props(SurroundingsFixtures.Prop("post", 30.001, 0)));
            Assert.AreNotEqual(a.Hash, b.Hash);
        }

        [Test]
        public void HedgesChainOutsideEveryOuterFenceAndNotAlongIndoorWalls()
        {
            var house = SurroundingsFixtures.House(
                SurroundingsFixtures.Room("Garden", -10, -10, 10, 4, outdoor: true),
                SurroundingsFixtures.Room("Shed", -10, 4, -4, 10),
                SurroundingsFixtures.Room("Lawn", -4, 4, 10, 10, outdoor: true));
            var r = SurroundingsFixtures.Draft("\"edging\": { \"default\": { \"piece\": \"hedge_2m\", \"end\": \"hedge_end\", \"offset\": 1.0 } }", house);
            Assert.IsTrue(r.Valid, SurroundingsFixtures.Show(r));
            var hedges = r.Placements.Where(p => p.Source == "edging").ToList();
            Assert.IsTrue(hedges.Any(p => p.Z > 10 && p.X > 0), "north of the lawn");
            Assert.IsTrue(hedges.Any(p => p.X > 10 && Math.Abs(p.Z) < 9), "east");
            Assert.IsTrue(hedges.Any(p => p.Z < -10 && Math.Abs(p.X) < 9), "south");
            Assert.IsTrue(hedges.Any(p => p.X < -10 && p.Z < 0), "west of the garden");
            Assert.IsFalse(hedges.Any(p => p.Z > 10 && p.X < -4.3), "nothing along the shed's north wall");
            Assert.IsFalse(hedges.Any(p => p.X < -10 && p.Z > 4.3), "nothing along the shed's west wall");
            Assert.IsFalse(hedges.Any(p => p.X < 10 && p.Z < 10 && p.Z > -10 && p.X > -10), "nothing inside");
            Assert.AreEqual(2, hedges.Count(p => p.Piece == "hedge_end"), "both fences end at the shed, so each gets an end piece");
        }

        [Test]
        public void SouthHedgeIsPushedOutUntilTheCouchRuleHolds()
        {
            var r = SurroundingsFixtures.Draft("\"edging\": { \"default\": { \"piece\": \"hedge_2m\", \"offset\": 1.0 } }", SurroundingsFixtures.Garden());
            Assert.IsTrue(r.Valid, SurroundingsFixtures.Show(r));
            var north = r.Placements.Where(p => p.Source == "edging" && p.Z > 10).Max(p => p.Z);
            var south = r.Placements.Where(p => p.Source == "edging" && p.Z < -10).Min(p => p.Z);
            Assert.AreEqual(11.4, north, 1e-3, "north keeps the 1 m offset");
            float top = -.35f + 1.4f;
            double nearFace = south + .4;
            Assert.LessOrEqual(nearFace + SurroundingsLayout.CouchReach(top), -10.2 + 1e-6, "the swept hedge top misses the play volume");
            Assert.LessOrEqual(1.4, (-10 - nearFace) * PartyCamera.Rise / PartyCamera.Back, "so height <= gap x Rise/Back");
            Assert.Greater(-south, 11.4 + .05, "south is pushed further out than north");
        }

        [Test]
        public void HedgesGapAroundAGateAndEndThere()
        {
            string body = "\"edging\": { \"default\": { \"piece\": \"hedge_2m\", \"end\": \"hedge_end\", \"offset\": 1.0 } }, " +
                          SurroundingsFixtures.Props(SurroundingsFixtures.Prop("gate_picket", 11.4, 0, 90));
            var r = SurroundingsFixtures.Draft(body, SurroundingsFixtures.Garden());
            Assert.IsTrue(r.Valid, SurroundingsFixtures.Show(r));
            var east = r.Placements.Where(p => p.Source == "edging" && p.X > 10).ToList();
            Assert.IsFalse(east.Any(p => Math.Abs(p.Z) < 1.0), "the gate stays clear");
            Assert.AreEqual(2, east.Count(p => p.Piece == "hedge_end"), "both sides of the gate get an end piece");
        }

        [Test]
        public void StreetsLayRoadsPavementsLampsAndKeepParkedCarsClearOfDriveways()
        {
            string body = "\"streets\": [ { \"id\": \"s\", \"from\": [24, -32], \"to\": [24, 32], \"width\": 7, " +
                          "\"lamps\": { \"side\": \"left\", \"every\": 16, \"phase\": 8 }, " +
                          "\"parking\": { \"side\": \"left\", \"every\": 6, \"chance\": 1, \"pieces\": [\"car_hatch\"], \"reach\": 3.5 } } ], " +
                          SurroundingsFixtures.Props(SurroundingsFixtures.Prop("driveway", 16.0, 0, 90));
            var r = SurroundingsFixtures.Draft(body);
            Assert.IsTrue(r.Valid, SurroundingsFixtures.Show(r));
            Assert.AreEqual(8, r.Placements.Count(p => p.Piece == "road_straight"));
            Assert.AreEqual(16, r.Placements.Count(p => p.Piece == "pavement_straight"));
            var lamps = r.Placements.Where(p => p.Piece == "lamp_post").ToList();
            Assert.AreEqual(4, lamps.Count);
            Assert.IsTrue(lamps.All(l => Math.Abs(l.X - (24 - 3.5 - .45)) < 1e-3), "lamps stand on the left pavement near the kerb");
            var cars = r.Placements.Where(p => p.Piece == "car_hatch").ToList();
            Assert.IsNotEmpty(cars);
            Assert.IsFalse(cars.Any(c => Math.Abs(c.Z) < 1.5 + 1.95 + 3.5 - .5), "no car parks across the driveway mouth");
            Assert.IsTrue(r.Dropped.Any(d => d.Contains("clear of driveway")));
        }

        [Test]
        public void CrossingStreetsCutThePavementAtTheJunction()
        {
            string body = "\"streets\": [ { \"id\": \"a\", \"from\": [-40, -40], \"to\": [40, -40], \"width\": 7 }, { \"id\": \"b\", \"from\": [0, -36.5], \"to\": [0, -20.5], \"width\": 7 } ]";
            var r = SurroundingsFixtures.Draft(body);
            Assert.IsTrue(r.Valid, SurroundingsFixtures.Show(r));
            var northPavement = r.Placements.Where(p => p.Piece == "pavement_straight" && Math.Abs(p.Z - (-40 + 3.5 + 1.1)) < 1e-3).ToList();
            Assert.IsFalse(northPavement.Any(p => Math.Abs(p.X) < 3.5 + 4 - .01), "street a's north pavement stops at street b");
        }

        [Test]
        public void FillTreesYieldToRoads()
        {
            string body = "\"streets\": [ { \"id\": \"s\", \"from\": [30, -40], \"to\": [30, 40], \"width\": 7, \"pavementSides\": \"none\" } ], " +
                          "\"treeBelts\": [ { \"pieces\": [\"tree_round_a\"], \"from\": [14, 20], \"to\": [60, 20], \"every\": 3, \"jitter\": 0 } ]";
            var r = SurroundingsFixtures.Draft(body);
            Assert.IsTrue(r.Valid, SurroundingsFixtures.Show(r));
            var trees = r.Placements.Where(p => p.Piece == "tree_round_a").ToList();
            Assert.IsNotEmpty(trees);
            Assert.IsFalse(trees.Any(t => Math.Abs(t.X - 30) < 3.5 + 2), "no tree stands on the road");
            Assert.IsTrue(r.Dropped.Any(d => d.StartsWith("belt 0") && d.Contains("road_straight")));
        }

        [Test]
        public void PlotsPlaceAHouseFacingTheFrontWithDrivewayCarAndGarden()
        {
            string body = "\"palette\": { \"plaster\": [\"#FFE9E0\", \"#E6F0FF\"], \"roof\": [\"#9FA3AD\"], \"paint\": [\"#C8564B\"] }, " +
                          "\"plots\": [ { \"id\": \"p\", \"rect\": [40, -10, 64, 10], \"front\": \"west\", \"house\": \"house_detached_a\", \"setback\": 7, " +
                          "\"driveway\": \"left\", \"garage\": \"garage_a\", \"car\": \"car_hatch\", \"trees\": 2, \"bushes\": 2, \"extras\": [\"trampoline\"], " +
                          "\"fence\": { \"piece\": \"fence_picket_2m\", \"sides\": [\"front\"], \"gate\": \"gate_picket\" } } ]";
            var r = SurroundingsFixtures.Draft(body);
            Assert.IsTrue(r.Valid, SurroundingsFixtures.Show(r));
            var house = r.Placements.Single(p => p.Piece == "house_detached_a");
            Assert.AreEqual(270, house.Yaw, 1e-3, "the front faces west");
            Assert.AreEqual(40 + 7 + 4.5, house.X, 1e-3);
            Assert.AreNotEqual(0xFFFFFFu, house.PlasterTint);
            var garage = r.Placements.Single(p => p.Piece == "garage_a");
            Assert.AreEqual(house.PlasterTint, garage.PlasterTint, "the garage matches the house");
            var car = r.Placements.Single(p => p.Piece == "car_hatch");
            Assert.Less(car.Z, house.Z - 5, "the driveway is on the house's left, south when facing west");
            Assert.AreEqual(0xC8564Bu, car.PaintTint);
            Assert.AreEqual(2, r.Placements.Count(p => p.Piece.StartsWith("tree_round") && p.Source == "plot p garden"));
            Assert.IsTrue(r.Placements.Any(p => p.Piece == "trampoline"));
            Assert.IsTrue(r.Placements.Any(p => p.Piece == "gate_picket"));
            Assert.IsFalse(r.Placements.Any(p => p.Piece == "fence_picket_2m" && Math.Abs(p.Z - car.Z) < 1.5), "the front fence leaves the driveway open");
        }

        [Test]
        public void CardsRingTheMapFacingTheCentre()
        {
            var r = SurroundingsFixtures.Draft("\"cards\": [ { \"piece\": \"card_hills_a\", \"radius\": 300, \"count\": 12, \"phase\": 0 } ]");
            Assert.IsTrue(r.Valid, SurroundingsFixtures.Show(r));
            var cards = r.Placements.Where(p => p.Card).ToList();
            Assert.AreEqual(12, cards.Count);
            foreach (var c in cards)
            {
                Assert.AreEqual(300, Math.Sqrt(c.X * c.X + c.Z * c.Z), .01);
                double facing = Math.Atan2(-c.X, -c.Z) * 180 / Math.PI;
                Assert.AreEqual(0, Math.IEEERemainder(c.Yaw - facing, 360), .05, "faces the centre");
                Assert.AreEqual(SurroundingsRing.Far, c.Ring);
                Assert.IsTrue(c.InLow, "cards go into the Low set");
            }
        }

        [Test]
        public void MoversLoopAlongAStreet()
        {
            string body = "\"streets\": [ { \"id\": \"m\", \"from\": [-40, -40], \"to\": [40, -40], \"width\": 7 } ], " +
                          "\"movers\": [ { \"piece\": \"car_hatch\", \"street\": \"m\", \"lane\": 1.75, \"speed\": 5, \"phase\": 0.5, \"paint\": \"#E6C25A\" } ]";
            var r = SurroundingsFixtures.Draft(body);
            Assert.IsTrue(r.Valid, SurroundingsFixtures.Show(r));
            var m = r.Movers.Single();
            Assert.AreEqual(4, m.Path.Count);
            Assert.IsTrue(m.Loop);
            Assert.AreEqual(-41.75, m.Path[0].Z, 1e-3, "out on the right lane");
            Assert.AreEqual(-38.25, m.Path[2].Z, 1e-3, "back on the left lane");
            Assert.AreEqual(0xE6C25Au, m.TintFor("bk_paint"));
            Assert.AreEqual(1, r.Budget.High.Movers);
            Assert.AreEqual(0, r.Budget.Low.Movers);
        }

        [Test]
        public void BudgetsCountRingSlotGroupsGroundAndMovers()
        {
            string body = SurroundingsFixtures.Props(SurroundingsFixtures.Prop("post", 13, 0), SurroundingsFixtures.Prop("post", 13, 3), SurroundingsFixtures.Prop("tree_round_a", 40, 0), SurroundingsFixtures.Prop("card_hills_a", 0, 300, 180));
            var r = SurroundingsFixtures.Draft(body);
            Assert.IsTrue(r.Valid, SurroundingsFixtures.Show(r));
            var high = r.Budget.High;
            Assert.AreEqual(1, high.GroundRenderers);
            Assert.AreEqual(1, high.RenderersIn(SurroundingsRing.Near));
            Assert.AreEqual(2, high.RenderersIn(SurroundingsRing.Mid));
            Assert.AreEqual(1, high.RenderersIn(SurroundingsRing.Far));
            Assert.AreEqual(5, high.Renderers);
            Assert.AreEqual(100 + 500 + 80, high.Triangles);
            var low = r.Budget.Low;
            Assert.AreEqual(1 + 1 + 1, low.Renderers, "Near post group, the card group and the ground");
            Assert.AreEqual(100 + 80, low.Triangles);
            Assert.AreEqual(2, high.Groups.Single(g => g.Ring == SurroundingsRing.Near && g.Slot == "bk_metal").Pieces);
        }

        [Test]
        public void MergedGroupsSplitPastTheVertexLimit()
        {
            var props = Enumerable.Range(0, 3).Select(i => SurroundingsFixtures.Prop("heavy", 40 + i * 3, 0)).ToArray();
            var r = SurroundingsFixtures.Draft(SurroundingsFixtures.Props(props));
            var group = r.Budget.High.Groups.Single(g => g.Slot == "bk_stone");
            Assert.AreEqual(150000, group.Triangles);
            Assert.AreEqual(450000, group.Vertices);
            Assert.AreEqual(8, group.Renderers, "ceil(450000 / 60000)");
        }

        [Test]
        public void KitManifestsReadOptionalFieldsAndRefuseDuplicates()
        {
            string json = "{ \"kit\": \"k\", \"fbx\": \"K.fbx\", \"pieces\": [ { \"id\": \"a\", \"width\": 1, \"depth\": 2, \"height\": 3, \"tris\": 90, \"slots\": [\"bk_wood\", \"bk_paint\"], " +
                          "\"slotTris\": { \"bk_wood\": 60, \"bk_paint\": 30 }, \"verts\": 120, \"fbx\": \"Other.fbx\" } ] }";
            var kit = SurroundingsKit.FromJson(json, "k.json");
            var a = kit.Piece("a");
            Assert.AreEqual("Other.fbx", a.Fbx);
            Assert.AreEqual("a", a.FbxObject);
            Assert.AreEqual(60, a.TrisIn("bk_wood"));
            Assert.AreEqual(30, a.TrisIn("bk_paint"));
            Assert.AreEqual(0, a.TrisIn("bk_leaf"));
            Assert.IsNull(kit.Piece("b"));
            Assert.Throws<FormatException>(() => kit.Add(json, "again.json"));
            var even = SurroundingsKit.FromJson("{ \"kit\": \"k\", \"pieces\": [ { \"id\": \"e\", \"width\": 1, \"depth\": 1, \"height\": 1, \"tris\": 101, \"slots\": [\"x\", \"y\"] } ] }").Piece("e");
            Assert.AreEqual(51, even.TrisIn("x"));
            Assert.AreEqual(50, even.TrisIn("y"));
        }

        [TestCase("\"palette\": { \"plaster\": [\"pink\"] }")]
        [TestCase("\"plots\": [ { \"id\": \"a\", \"rect\": [40, 0, 50, 10], \"front\": \"west\" }, { \"id\": \"a\", \"rect\": [40, 20, 50, 30], \"front\": \"west\" } ]")]
        [TestCase("\"edging\": { \"up\": { \"piece\": \"hedge_2m\" } }")]
        [TestCase("\"plots\": [ { \"id\": \"a\", \"rect\": [40, 0, 50, 10], \"front\": \"inward\" } ]")]
        [TestCase("\"streets\": [ { \"id\": \"has space\", \"from\": [0, 0], \"to\": [0, 8] } ]")]
        public void MalformedPlansAreRefused(string body)
        {
            Assert.Throws<FormatException>(() => SurroundingsPlan.FromJson(SurroundingsFixtures.Plan(body), "bad.json"));
        }
    }
}
