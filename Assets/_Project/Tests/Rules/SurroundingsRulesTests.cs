using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Surroundings;
using static Wreckabulary.Rules.Tests.SurroundingsFixtures;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class SurroundingsRulesTests
    {
        [TestCase(10.45, true)]
        [TestCase(10.55, false)]
        public void FootprintClearanceKeepsPiecesOffThePlayableFootprintPlusClearance(double x, bool broken)
        {
            var r = Draft(Props(Prop("post", x, 0)));
            Assert.AreEqual(broken, Has(r, "clearance"), Show(r));
        }

        [Test]
        public void FootprintClearanceUsesTheRotatedFootprint()
        {
            Assert.IsTrue(Draft(Props(Prop("board", 10.8, 0, 90))).Valid, "end-on, the board stays clear");
            Assert.IsTrue(Has(Draft(Props(Prop("board", 10.8, 0, 45))), "clearance"), "turned 45 degrees, its corner reaches the footprint");
        }

        [Test]
        public void FootprintClearanceCoversEveryRoomOfTheUnion()
        {
            var house = House(Room("Hall", -10, -10, 10, 10), Room("Annex", 10, -4, 16, 4));
            Assert.IsTrue(Has(Draft(Props(Prop("post", 16.45, 0)), house), "clearance"));
            Assert.IsTrue(Draft(Props(Prop("post", 16.55, 0)), house).Valid);
        }

        static double CouchGap(float height) => SurroundingsLayout.CouchReach(-.35f + height);

        [TestCase(.05, false)]
        [TestCase(-.05, true)]
        public void CouchRuleKeepsTheCameraSideLowEnoughToStayOffThePlayVolume(double slack, bool broken)
        {
            double gap = CouchGap(2f) + slack;
            var r = Draft(Props(Prop("post", 0, -10.2 - gap - .2)));
            Assert.AreEqual(broken, Has(r, "couch"), Show(r));
            if (!broken) Assert.LessOrEqual(2.0, (gap + .2) * PartyCamera.Rise / PartyCamera.Back, "a piece that passes also meets height <= gap x Rise/Back");
        }

        [Test]
        public void CouchRuleUsesThePartyCameraConstants()
        {
            Assert.AreEqual((1.65 - SurroundingsLayout.PlayFloor) * PartyCamera.Back / PartyCamera.Rise, SurroundingsLayout.CouchReach(1.65f), 1e-5);
        }

        [Test]
        public void CouchRuleOnlyBindsOnTheCameraSide()
        {
            double gap = CouchGap(2f) - .5;
            Assert.IsTrue(Draft(Props(Prop("post", 10.2 + gap + .2, 0), Prop("post", -10.2 - gap - .2, 0), Prop("post", 0, 10.2 + gap + .2))).Valid);
        }

        [Test]
        public void CouchRuleSweepsTheWholeRotatedFootprint()
        {
            Assert.IsTrue(Has(Draft(Props(Prop("board", 11.2, -11.7, 45))), "couch"), "its west corner sits south of the play volume");
            Assert.IsTrue(Draft(Props(Prop("board", 12.5, -12.6, 90))).Valid, "end-on and east of the volume it is clear");
        }

        [TestCase(3.0, true)]
        [TestCase(3.6, false)]
        public void CameraBoomKeepsTallPiecesAwayFromTheFootprint(double distance, bool broken)
        {
            var r = Draft(Props(Prop("mast", 10 + distance + .15, 0)));
            Assert.AreEqual(broken, Has(r, "boom"), Show(r));
        }

        [Test]
        public void CameraBoomAllowsLowPiecesClose()
        {
            Assert.IsTrue(Draft(Props(Prop("hut", 10 + 1 + 1, 0))).Valid, "3.2 m is under the 3.3 m boom height");
        }

        [Test]
        public void SpacingRefusesOverlappingBuildings()
        {
            Assert.IsTrue(Has(Draft(Props(Prop("house_box", 30, 0), Prop("house_box", 34, 0))), "spacing"));
            Assert.IsTrue(Has(Draft(Props(Prop("house_box", 30, 0), Prop("tower_box", 30, 5.5))), "spacing"), "a building family counts too");
            Assert.IsTrue(Draft(Props(Prop("house_box", 30, 0), Prop("house_box", 36.5, 0))).Valid);
        }

        [Test]
        public void SpacingLetsNonBuildingsOverlap()
        {
            Assert.IsTrue(Draft(Props(Prop("house_box", 30, 0), Prop("bush_a", 32, 0))).Valid);
        }

        const string Street = "\"streets\": [ { \"id\": \"s\", \"from\": [24, -32], \"to\": [24, 32], \"width\": 7 } ]";

        [Test]
        public void StreetFurnitureStaysOnPavements()
        {
            Assert.IsTrue(Draft(Street + ", " + Props(Prop("lamp_post", 24 + 3.5 + .5, 0), Prop("bench_park", 24 - 3.5 - 1.1, 5, 90))).Valid);
            Assert.IsTrue(Has(Draft(Street + ", " + Props(Prop("lamp_post", 30, 0))), "pavement"));
            Assert.IsTrue(Has(Draft(Street + ", " + Props(Prop("bench_park", 24 + 3.5 + .3, 0, 0))), "pavement"), "a bench hanging over the kerb");
        }

        [Test]
        public void StreetFurnitureListComesFromThePlan()
        {
            Assert.IsTrue(Draft("\"streetFurniture\": [\"mailbox\"], " + Props(Prop("lamp_post", 40, 0))).Valid);
            Assert.IsTrue(Has(Draft("\"streetFurniture\": [\"mailbox\"], " + Props(Prop("mailbox", 40, 0))), "pavement"));
        }

        [Test]
        public void RingsSplitAtEightAndSeventyMetres()
        {
            var r = Draft(Props(Prop("post", 10 + 7.6, 0), Prop("post", 10 + 8.4, 0), Prop("post", 10 + 69.6, 0), Prop("tree_far", 10 + 72.5, 0)));
            Assert.IsTrue(r.Valid, Show(r));
            CollectionAssert.AreEqual(new[] { SurroundingsRing.Near, SurroundingsRing.Mid, SurroundingsRing.Mid, SurroundingsRing.Far }, r.Placements.Select(p => p.Ring).ToArray());
        }

        [Test]
        public void FarRingSwapsAPieceForItsFarStandIn()
        {
            var r = Draft(Props(Prop("house_detached_a", 100, 0), Prop("tree_round_a", 0, 120)));
            Assert.IsTrue(r.Valid, Show(r));
            CollectionAssert.AreEqual(new[] { "house_detached_a_far", "tree_far" }, r.Placements.Select(p => p.Piece).ToArray());
            Assert.IsTrue(r.Placements.All(p => p.Ring == SurroundingsRing.Far));
        }

        [Test]
        public void FarRingRefusesFullPiecesWithoutAStandIn()
        {
            Assert.IsTrue(Has(Draft(Props(Prop("post", 100, 0))), "ring"));
            Assert.IsTrue(Draft(Props(Prop("card_hills_a", 0, 200, 180), Prop("landmark_water_tower", 0, 140))).Valid, "cards and the landmark belong far");
        }

        [Test]
        public void MidAndNearRefuseFarPieces()
        {
            Assert.IsTrue(Has(Draft(Props(Prop("tree_far", 40, 0))), "ring"));
            Assert.IsTrue(Has(Draft(Props(Prop("house_detached_b_far", 25, 0))), "ring"));
        }

        [Test]
        public void NothingStandsBeyondTheFarRing()
        {
            Assert.IsTrue(Has(Draft(Props(Prop("tree_far", 380, 0))), "ring"));
        }

        [Test]
        public void FillThatBreaksARingIsDroppedNotReported()
        {
            var r = Draft("\"treeBelts\": [ { \"pieces\": [\"tree_far\"], \"from\": [20, 20], \"to\": [120, 20], \"every\": 10, \"jitter\": 0 } ]");
            Assert.IsTrue(r.Valid, Show(r));
            Assert.IsTrue(r.Placements.All(p => p.Ring == SurroundingsRing.Far));
            Assert.IsTrue(r.Dropped.Any(d => d.Contains("ring")));
        }

        [Test]
        public void HighBudgetCapsTriangles()
        {
            var r = Draft(Props(Prop("heavy", 30, 0), Prop("heavy", 33, 0), Prop("heavy", 36, 0), Prop("heavy", 39, 0)));
            Assert.AreEqual(200000, r.Budget.High.Triangles);
            Assert.IsTrue(r.Problems.Any(p => p.StartsWith("budget: High") && p.Contains("triangles")), Show(r));
        }

        [Test]
        public void HighBudgetCapsRenderers()
        {
            var props = Enumerable.Range(0, 20).Select(i => Prop("heavy", 30 + i % 10 * 3, i / 10 * 3)).ToArray();
            var r = Draft(Props(props));
            Assert.Greater(r.Budget.High.Renderers, SurroundingsBudget.HighRenderers);
            Assert.IsTrue(r.Problems.Any(p => p.StartsWith("budget: High") && p.Contains("renderers")), Show(r));
        }

        [Test]
        public void LowBudgetIsTheNearRingPlusCards()
        {
            var r = Draft(Props(Prop("rainbow_a", 12, 0), Prop("rainbow_b", 12, 3), Prop("rainbow_c", 12, 6), Prop("rainbow_a", 40, 0)));
            Assert.AreEqual(13 + 1, r.Budget.Low.Renderers, "13 slots in the Near ring plus the ground");
            Assert.AreEqual(30, r.Budget.Low.Triangles, "the Mid piece is not in Low");
            Assert.IsTrue(r.Problems.Any(p => p.StartsWith("budget: Low") && p.Contains("renderers")), Show(r));
        }

        [Test]
        public void MoversStayFourMetresOutside()
        {
            string Mover(params double[] xz) => "{ \"piece\": \"car_hatch\", \"path\": [" +
                string.Join(", ", Enumerable.Range(0, xz.Length / 2).Select(i => string.Format(CultureInfo.InvariantCulture, "[{0}, {1}]", xz[2 * i], xz[2 * i + 1]))) + "] }";
            Assert.IsTrue(Draft("\"movers\": [" + Mover(14.5, -20, 14.5, 20) + "]").Valid);
            Assert.IsTrue(Has(Draft("\"movers\": [" + Mover(13.5, -20, 13.5, 20) + "]"), "movers"));
            Assert.IsTrue(Has(Draft("\"movers\": [" + Mover(20, -20, 20, 20, -20, 20) + "]"), "movers"), "the loop closes back across the house");
        }

        [Test]
        public void MoversAreCappedAtTwelve()
        {
            string one = "{ \"piece\": \"car_hatch\", \"path\": [[40, -20], [40, 20]] }";
            Assert.IsTrue(Draft("\"movers\": [" + string.Join(", ", Enumerable.Repeat(one, 12)) + "]").Valid);
            Assert.IsTrue(Has(Draft("\"movers\": [" + string.Join(", ", Enumerable.Repeat(one, 13)) + "]"), "movers"));
        }

        [Test]
        public void EveryPieceIdMustExistInTheKits()
        {
            var r = Draft("\"plots\": [ { \"id\": \"p\", \"rect\": [40, -10, 64, 10], \"front\": \"west\", \"house\": \"house_zz\" } ], " +
                          "\"treeBelts\": [ { \"pieces\": [\"tree_zz\"], \"from\": [40, 40], \"to\": [60, 40], \"every\": 10 } ], " +
                          "\"movers\": [ { \"piece\": \"car_zz\", \"path\": [[40, -20], [40, 20]] } ]");
            var kit = r.Problems.Where(p => p.StartsWith("kit:")).ToList();
            Assert.IsTrue(kit.Any(p => p.Contains("house_zz")), Show(r));
            Assert.IsTrue(kit.Any(p => p.Contains("tree_zz")), Show(r));
            Assert.IsTrue(kit.Any(p => p.Contains("car_zz")), Show(r));
        }

        const string Mark = "\"landmark\": { \"piece\": \"landmark_water_tower\", \"at\": [0, 140] }, \"sightlines\": [ { \"from\": [0, 8], \"to\": \"landmark\", \"width\": 6 } ]";

        [Test]
        public void SightlineKeepsTheLandmarkInView()
        {
            Assert.IsTrue(Has(Draft(Mark + ", " + Props(Prop("house_box", 1, 40))), "sightline"));
            Assert.IsTrue(Draft(Mark + ", " + Props(Prop("house_box", 7, 40), Prop("bush_a", 0, 20))).Valid, "beside the corridor, or low in it, is fine");
        }

        [Test]
        public void SightlineDropsFillInTheCorridor()
        {
            var r = Draft(Mark + ", \"treeBelts\": [ { \"pieces\": [\"tree_round_a\"], \"from\": [-30, 40], \"to\": [30, 40], \"every\": 6, \"jitter\": 0 } ]");
            Assert.IsTrue(r.Valid, Show(r));
            Assert.IsFalse(r.Placements.Any(p => p.Piece == "tree_round_a" && System.Math.Abs(p.X) < 5));
            Assert.IsTrue(r.Dropped.Any(d => d.Contains("sightline")));
        }

        [Test]
        public void LandmarkStandsNorthInLineWithTheMinimap()
        {
            Assert.IsTrue(Draft("\"landmark\": { \"piece\": \"landmark_water_tower\", \"at\": [20, 140] }").Valid);
            Assert.IsTrue(Has(Draft("\"landmark\": { \"piece\": \"landmark_water_tower\", \"at\": [0, -140] }"), "landmark"));
            Assert.IsTrue(Has(Draft("\"landmark\": { \"piece\": \"landmark_water_tower\", \"at\": [100, 120] }"), "landmark"));
        }
    }
}
