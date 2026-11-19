using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public class WordboundLetterFanTests
    {
        static (float x, float z)[] Rest(WordboundLetterFan fan, params (long order, float x, float ground, float z)[] tiles)
        {
            fan.Clear();
            foreach (var tile in tiles) fan.Add(tile.order, tile.x, tile.ground, tile.z);
            fan.Spread();
            var shown = new (float x, float z)[tiles.Length];
            for (int i = 0; i < tiles.Length; i++) shown[i] = (tiles[i].x + fan.OffsetX(i), tiles[i].z + fan.OffsetZ(i));
            return shown;
        }

        static (long, float, float, float)[] Stack(int count, float x = 1.5f, float z = -2f)
        {
            var tiles = new (long, float, float, float)[count];
            for (int i = 0; i < count; i++) tiles[i] = ((count - i) * 3 + 10, x, 0f, z);
            return tiles;
        }

        static float Distance((float x, float z) a, (float x, float z) b) => (float)Math.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));

        [Test]
        public void ALoneTileStaysOnItsBody()
        {
            var shown = Rest(new WordboundLetterFan(), (4, 2f, 0f, 3f));
            Assert.AreEqual((2f, 3f), shown[0]);
        }

        [Test]
        public void TwoStackedTilesSitSideBySideAroundTheirBody()
        {
            var shown = Rest(new WordboundLetterFan(), (8, 1f, 0f, 1f), (3, 1f, 0f, 1f));
            Assert.AreEqual(WordboundLetterFan.Spacing, Distance(shown[0], shown[1]), 1e-4f);
            Assert.AreEqual(1f, (shown[0].x + shown[1].x) / 2f, 1e-4f);
            Assert.AreEqual(1f, (shown[0].z + shown[1].z) / 2f, 1e-4f);
            Assert.Less(shown[1].x, shown[0].x, "the lower tile number takes the first slot");
        }

        [TestCase(3), TestCase(6), TestCase(7), TestCase(12), TestCase(19), TestCase(40)]
        public void StackedTilesFanOutWithoutOverlapAroundTheirCentre(int count)
        {
            var shown = Rest(new WordboundLetterFan(), Stack(count));
            float x = 0, z = 0;
            for (int i = 0; i < count; i++)
            {
                x += shown[i].x; z += shown[i].z;
                for (int j = i + 1; j < count; j++)
                    Assert.GreaterOrEqual(Distance(shown[i], shown[j]), WordboundLetterFan.Spacing - 1e-4f, $"tiles {i} and {j} of {count}");
            }
            Assert.AreEqual(1.5f, x / count, 1e-3f);
            Assert.AreEqual(-2f, z / count, 1e-3f);
        }

        [Test]
        public void SevenLettersFitWithinOneTileOfTheirBody()
        {
            var shown = Rest(new WordboundLetterFan(), Stack(7, 0f, 0f));
            foreach (var spot in shown) Assert.LessOrEqual(Distance(spot, (0f, 0f)), WordboundLetterFan.Spacing + 1e-4f);
        }

        [Test]
        public void EachTileKeepsItsSlotWhateverOrderTheViewsArrive()
        {
            var fan = new WordboundLetterFan();
            var first = Rest(fan, (5, 0f, 0f, 0f), (2, 0f, 0f, 0f), (9, 0f, 0f, 0f));
            var second = Rest(fan, (9, 0f, 0f, 0f), (5, 0f, 0f, 0f), (2, 0f, 0f, 0f));
            Assert.AreEqual(first[0].x, second[1].x, 1e-5f); Assert.AreEqual(first[0].z, second[1].z, 1e-5f);
            Assert.AreEqual(first[1].x, second[2].x, 1e-5f); Assert.AreEqual(first[1].z, second[2].z, 1e-5f);
            Assert.AreEqual(first[2].x, second[0].x, 1e-5f); Assert.AreEqual(first[2].z, second[0].z, 1e-5f);
        }

        [Test]
        public void JitteredStacksShowTheSameLayoutAsExactOnes()
        {
            var fan = new WordboundLetterFan();
            var exact = Rest(fan, (1, 0f, 0f, 0f), (2, 0f, 0f, 0f), (3, 0f, 0f, 0f), (4, 0f, 0f, 0f));
            var jittered = Rest(fan, (1, .02f, .12f, -.03f), (2, -.01f, .24f, .03f), (3, .03f, 0f, 0f), (4, -.04f, .1f, 0f));
            for (int i = 0; i < 4; i++) Assert.AreEqual(0f, Distance(exact[i], jittered[i]), 1e-4f, "tile " + i);
        }

        [Test]
        public void TilesAlreadyApartAreLeftAlone()
        {
            var fan = new WordboundLetterFan();
            Rest(fan, (1, 0f, 0f, 0f), (2, .3f, 0f, 0f), (3, 0f, 0f, .31f), (4, 5f, 0f, 5f), (5, 5f, 0f, 5f));
            for (int i = 0; i < 3; i++) Assert.AreEqual((0f, 0f), (fan.OffsetX(i), fan.OffsetZ(i)), "tile " + i);
            Assert.AreNotEqual(0f, fan.OffsetX(3), "only the stacked pair moves");
        }

        [Test]
        public void StacksOnDifferentFloorsDoNotMerge()
        {
            var fan = new WordboundLetterFan();
            Rest(fan, (1, 0f, 0f, 0f), (2, 0f, 3f, 0f));
            Assert.AreEqual((0f, 0f), (fan.OffsetX(0), fan.OffsetZ(0)));
            Assert.AreEqual((0f, 0f), (fan.OffsetX(1), fan.OffsetZ(1)));
        }

        [Test]
        public void SlotsFormRingsAtLeastOneSpacingApart()
        {
            var slots = new List<(float x, float z)>();
            for (int i = 0; i < 61; i++) slots.Add(WordboundLetterFan.Slot(i));
            Assert.AreEqual((0f, 0f), slots[0]);
            Assert.AreEqual(WordboundLetterFan.Spacing, Distance(slots[6], (0f, 0f)), 1e-4f);
            Assert.AreEqual(2 * WordboundLetterFan.Spacing, Distance(slots[7], (0f, 0f)), 1e-4f);
            for (int i = 0; i < slots.Count; i++)
                for (int j = i + 1; j < slots.Count; j++)
                    Assert.GreaterOrEqual(Distance(slots[i], slots[j]), WordboundLetterFan.Spacing - 1e-4f, $"slots {i} and {j}");
        }

        [Test]
        public void ClearReusesTheFanForALargerCrowd()
        {
            var fan = new WordboundLetterFan();
            Rest(fan, Stack(2));
            var shown = Rest(fan, Stack(30));
            Assert.AreEqual(30, fan.Count);
            Assert.GreaterOrEqual(Distance(shown[0], shown[29]), WordboundLetterFan.Spacing - 1e-4f);
            fan.Clear();
            Assert.AreEqual(0, fan.Count);
        }
    }
}
