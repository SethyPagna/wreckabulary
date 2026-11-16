using System;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Surroundings;
using static Wreckabulary.Rules.Tests.SurroundingsFixtures;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class SurroundingsGroundTests
    {
        const string Lawns = "\"lawns\": { \"tones\": [\"#D3DFC6\", \"#C8D7B9\"], \"stripe\": 2, \"stripeShift\": 0.06 }";
        const string Plot = "\"plots\": [ { \"id\": \"p\", \"rect\": [40, -10, 64, 10], \"front\": \"west\", \"house\": \"house_detached_a\", \"setback\": 7, \"bushes\": 0, \"trees\": 0, \"flowers\": false } ]";

        [Test]
        public void FarFieldFadesInRingsThatStepDownUnderTheNearField()
        {
            var r = Draft("\"ground\": { \"tint\": \"#BDB98F\", \"fade\": [ { \"radius\": 150, \"tint\": \"#D6D4B2\" }, { \"radius\": 100, \"tint\": \"#FFFFFF\" } ] }");
            Assert.IsTrue(r.Valid, Show(r));
            CollectionAssert.AreEqual(new[] { 600f, 150f, 100f }, r.Ground.Select(g => g.Radius).ToArray());
            CollectionAssert.AreEqual(new[] { 0xBDB98Fu, 0xD6D4B2u, 0xFFFFFFu }, r.Ground.Select(g => g.Tint).ToArray());
            Assert.AreEqual(-.37, r.Ground[0].Y, 1e-5, "the olive field sits lowest");
            Assert.AreEqual(-.35, r.Ground[2].Y, 1e-5, "the near field is the ground top the pieces stand on");
            Assert.AreEqual(1, r.Budget.High.GroundRenderers, "every ring shares one slot");
        }

        [Test]
        public void PlotsGetAFrontAndBackLawnToneWithMownStripes()
        {
            var r = Draft(Lawns + ", " + Plot);
            Assert.IsTrue(r.Valid, Show(r));
            var front = r.Ground.Single(g => g.Source == "lawn p front");
            var back = r.Ground.Single(g => g.Source == "lawn p back");
            Assert.AreEqual(40, front.MinX, 1e-3);
            Assert.AreEqual(47, front.MaxX, 1e-3, "the front lawn runs to the house front");
            Assert.AreEqual(47, back.MinX, 1e-3);
            Assert.AreEqual(64, back.MaxX, 1e-3);
            Assert.AreNotEqual(front.Tint, back.Tint, "front and back differ");
            var stripes = r.Ground.Where(g => g.Source == "lawn p front stripe").ToList();
            Assert.AreEqual(5, stripes.Count);
            Assert.IsTrue(stripes.All(s => s.MinX == front.MinX && s.MaxX == front.MaxX && s.MaxZ - s.MinZ == 2f), "stripes run from the street to the house");
            Assert.IsTrue(stripes.All(s => s.Y > front.Y), "stripes lie on the lawn");
            uint t = front.Tint, l = stripes[0].Tint;
            foreach (int bits in new[] { 16, 8, 0 })
                Assert.AreEqual(Math.Min(255, Math.Round(((t >> bits) & 0xFF) * 1.06)), (l >> bits) & 0xFF, "a close, lighter tone");
            Assert.AreEqual(1, r.Budget.High.GroundRenderers);
            Assert.AreEqual(1, r.Budget.Low.GroundRenderers);
        }

        [Test]
        public void SoilLiesUnderEveryHedge()
        {
            var r = Draft("\"edging\": { \"default\": { \"piece\": \"hedge_2m\", \"offset\": 1.0 } }, \"soil\": { \"under\": [\"hedge_2m\"], \"margin\": 0.2, \"tint\": \"#9C7A5E\" }", Garden());
            Assert.IsTrue(r.Valid, Show(r));
            var soil = r.Ground.Where(g => g.Source.StartsWith("soil")).ToList();
            var hedges = r.Placements.Where(p => p.Piece == "hedge_2m").ToList();
            Assert.AreEqual(hedges.Count, soil.Count);
            foreach (var h in hedges)
                Assert.IsTrue(soil.Any(s => s.MinX <= h.X - .4 && s.MaxX >= h.X + .4 && s.MinZ <= h.Z - .4 && s.MaxZ >= h.Z + .4), $"soil under the hedge at ({h.X}, {h.Z})");
            Assert.IsTrue(soil.All(s => s.Tint == 0x9C7A5Eu && s.Y > -.35f), "a bark strip just above the grass");
            Assert.AreEqual(soil.Max(s => s.Y), r.Ground.Max(g => g.Y), "soil lies on top");
        }

        [Test]
        public void PlanZonesTakeDiscsRectsAndStripes()
        {
            var r = Draft(Lawns + ", \"zones\": [ { \"disc\": [30, 40, 4], \"tint\": \"#FFFFFF\" }, { \"rect\": [20, -10, 26, 10], \"tint\": \"#CFDCC1\", \"stripes\": true } ]");
            Assert.IsTrue(r.Valid, Show(r));
            var island = r.Ground.Single(g => g.Shape == SurroundingsGroundShape.Disc && g.Radius == 4f);
            Assert.AreEqual(30, island.CentreX, 1e-3);
            Assert.AreEqual(40, island.CentreZ, 1e-3);
            var yard = r.Ground.Where(g => g.Shape == SurroundingsGroundShape.Rect && g.Source.EndsWith("stripe")).ToList();
            Assert.AreEqual(5, yard.Count, "the 20 m side is cut into 2 m stripes, every other one lighter");
            Assert.IsTrue(yard.All(s => s.MinX == 20f && s.MaxX == 26f));
        }

        [Test]
        public void FadeRingsMustShrinkOutsideIn()
        {
            Assert.Throws<FormatException>(() => SurroundingsPlan.FromJson(Plan("\"ground\": { \"fade\": [ { \"radius\": 100, \"tint\": \"#FFFFFF\" }, { \"radius\": 150, \"tint\": \"#FFFFFF\" } ] }"), "bad.json"));
        }
    }
}
