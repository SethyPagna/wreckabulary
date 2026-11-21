using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class StrayPiecesTests
    {
        const float BottleHalfHeight = .2f;

        static HouseLayout Pinwheel() => TestData.House();

        static HouseLayout Walkup() => HouseLayout.FromJson(TestData.Read("house_walkup.json"), "house_walkup.json");

        static IEnumerable<HouseLayout> Houses() =>
            Directory.GetFiles(Path.Combine(TestData.Root, "Assets/_Project/Data/Config"), "house_*.json").OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => HouseLayout.FromJson(File.ReadAllText(f), Path.GetFileName(f)));

        static StrayPlace Standing(HouseLayout house, float x, float z, float floor = 0f, float rise = 0f) =>
            StrayPieces.Locate(house, 0f, x, floor + rise + BottleHalfHeight, z, floor + rise);

        static bool ReturnsAfter(StrayPlace place, StrayHold hold, float speed, float seconds)
        {
            var watch = new StrayWatch();
            bool returns = false;
            for (float t = 0f; t < seconds - .001f; t += StrayPieces.CheckSeconds)
                returns = StrayPieces.Tick(ref watch, place, hold, speed, StrayPieces.CheckSeconds);
            return returns;
        }

        [Test]
        public void APieceOnAnyRoomFloorIsInPlace()
        {
            var house = Pinwheel();
            Assert.AreEqual(StrayPlace.InPlace, Standing(house, 6.5f, -.7f), "WATER where the Kitchen puts it");
            Assert.AreEqual(StrayPlace.InPlace, Standing(house, 0f, 0f), "pushed into the Playroom");
            Assert.AreEqual(StrayPlace.InPlace, Standing(house, 17f, 17f), "carried out to the Garden");
            Assert.AreEqual(StrayPlace.InPlace, Standing(house, 6f, 0f), "lying in a doorway line between two rooms");
            Assert.AreEqual(StrayPlace.InPlace, Standing(house, 13.8f, 13.8f), "pushed into a corner");
            Assert.AreEqual(StrayPlace.InPlace, Standing(house, 19.85f, 0f), "lying against the outer fence");
        }

        [Test]
        public void EveryFloorOfEveryShippedHouseIsInPlace()
        {
            foreach (var house in Houses())
                foreach (var room in house.Rooms)
                    for (int ix = 0; ix <= 4; ix++)
                        for (int iz = 0; iz <= 4; iz++)
                        {
                            float x = room.MinX + .15f + (room.MaxX - room.MinX - .3f) * ix / 4f;
                            float z = room.MinZ + .15f + (room.MaxZ - room.MinZ - .3f) * iz / 4f;
                            Assert.AreEqual(StrayPlace.InPlace, Standing(house, x, z, room.FloorY), $"{house.Name} {room.Name} at ({x}, {z})");
                            Assert.AreEqual(StrayPlace.InPlace, Standing(house, x, z, room.FloorY, 1f), $"{house.Name} {room.Name} on a table at ({x}, {z})");
                        }
        }

        [Test]
        public void EveryShippedPieceIsInPlaceWhereTheHousePutsIt()
        {
            foreach (var house in Houses())
                foreach (var f in house.Furniture)
                {
                    float floor = house.Room(f.Room).FloorY;
                    float bottom = floor + f.Y;
                    float rise = StrayPieces.Rise(house, f.X, bottom + BottleHalfHeight, f.Z, bottom);
                    Assert.AreEqual(f.Y, rise, .001f, $"{house.Name} {f.Word} home height");
                    Assert.AreEqual(StrayPlace.InPlace, StrayPieces.Locate(house, rise, f.X, bottom + BottleHalfHeight, f.Z, bottom), $"{house.Name} {f.Word} in {f.Room}");
                    Assert.AreSame(house.Room(f.Room), StrayPieces.FloorUnder(house, f.X, bottom + BottleHalfHeight, f.Z), $"{house.Name} {f.Word} home room");
                }
        }

        [Test]
        public void APieceBeyondTheOuterWallIsOutside()
        {
            var house = Pinwheel();
            Assert.AreEqual(StrayPlace.Outside, Standing(house, 20.2f, 0f));
            Assert.AreEqual(StrayPlace.Outside, Standing(house, 0f, -20.3f));
            Assert.AreEqual(StrayPlace.Outside, Standing(house, -30f, 40f));
            Assert.AreEqual(StrayPlace.InPlace, Standing(house, 20f + StrayPieces.WallSlack - .01f, 0f), "inside the wall's own thickness is not yet outside");
        }

        [Test]
        public void APieceUnderTheFloorIsBelowAndFarUnderIsLost()
        {
            var house = Pinwheel();
            Assert.AreEqual(StrayPlace.InPlace, StrayPieces.Locate(house, 0f, 0f, -.2f, 0f, -.4f), "sunk a little into the floor");
            Assert.AreEqual(StrayPlace.Below, StrayPieces.Locate(house, 0f, 0f, -.6f, 0f, -.8f));
            Assert.AreEqual(StrayPlace.Below, StrayPieces.Locate(house, 0f, 0f, -3.9f, 0f, -4.1f));
            Assert.AreEqual(StrayPlace.Lost, StrayPieces.Locate(house, 0f, 0f, -4.1f, 0f, -4.3f));
            Assert.AreEqual(StrayPlace.Lost, StrayPieces.Locate(house, 0f, 60f, -9f, 0f, -9.2f), "outside and falling");
        }

        [Test]
        public void APieceRestingAboveTheBotsReachIsHigh()
        {
            var house = Pinwheel();
            Assert.AreEqual(StrayPlace.InPlace, Standing(house, 8f, 0f, 0f, StrayPieces.ReachHeight - .05f), "on a counter");
            Assert.AreEqual(StrayPlace.High, Standing(house, 8f, 0f, 0f, StrayPieces.ReachHeight + .05f), "on top of a tall cabinet");
            Assert.AreEqual(StrayPlace.High, Standing(house, 5.3f, 4.5f, 0f, 1.7f), "a floor piece knocked onto the balcony");
        }

        [Test]
        public void APieceThatLivesHighStaysInPlaceAtItsOwnHeight()
        {
            var house = Pinwheel();
            var fan = house.Furniture.First(f => f.Word == "FAN" && f.Y > 1f);
            float rise = StrayPieces.Rise(house, fan.X, fan.Y + BottleHalfHeight, fan.Z, fan.Y);
            Assert.AreEqual(StrayPlace.InPlace, StrayPieces.Locate(house, rise, fan.X, fan.Y + BottleHalfHeight, fan.Z - 2f, fan.Y), "pushed along the balcony");
            Assert.AreEqual(StrayPlace.High, StrayPieces.Locate(house, rise, 8f, fan.Y + 1f + BottleHalfHeight, 0f, fan.Y + 1f), "thrown onto something higher still");
            Assert.AreEqual(StrayPlace.InPlace, StrayPieces.Locate(house, rise, 0f, BottleHalfHeight, 0f, 0f), "fallen to the floor");
        }

        [Test]
        public void UpperStoreysAndStairsCountAsFloor()
        {
            var house = Walkup();
            Assert.AreEqual("Lobby", StrayPieces.FloorUnder(house, -3f, .2f, 8f).Name);
            Assert.AreEqual("Landing1", StrayPieces.FloorUnder(house, -3f, 3.2f, 8f).Name);
            Assert.AreEqual("RoofGarden", StrayPieces.FloorUnder(house, -3f, 9.2f, 8f).Name);
            Assert.AreEqual(StrayPlace.InPlace, Standing(house, -3f, 8f, 3f));
            Assert.AreEqual(StrayPlace.InPlace, Standing(house, 15f, 8f, 9f), "the roof covers the whole block");
            Assert.AreEqual(StrayPlace.High, Standing(house, 15f, 8f, 0f, 2f), "resting high above the Yard");
            var stairs = house.Stairs.First(s => s.Lower == "Lobby");
            stairs.Along(stairs.Run * .5f, out float x, out float z);
            Assert.IsTrue(StrayPieces.OnStairs(house, x, 1.7f, z));
            Assert.AreEqual(StrayPlace.InPlace, StrayPieces.Locate(house, 0f, x, 1.7f, z, 1.5f), "half way up a flight is reachable, as the bots treat it");
            Assert.IsFalse(StrayPieces.OnStairs(house, x + 3f, 1.7f, z));
            Assert.AreEqual(StrayPlace.Outside, Standing(house, 19f, 0f, 3f), "off the side of the fire escape and outside the block");
        }

        [Test]
        public void ANearbyPieceCountsAsHome()
        {
            Assert.IsTrue(StrayPieces.AtHome(0f, 0f, 0f));
            Assert.IsTrue(StrayPieces.AtHome(.3f, .1f, -.3f));
            Assert.IsFalse(StrayPieces.AtHome(StrayPieces.HomeRadius, 0f, 0f));
            Assert.IsFalse(StrayPieces.AtHome(0f, StrayPieces.HomeRadius + .01f, 0f));
            Assert.IsFalse(StrayPieces.AtHome(.4f, 0f, .4f));
        }

        [Test]
        public void AStrayPieceReturnsOnlyAfterTheDelayAndOnceAtRest()
        {
            foreach (var place in new[] { StrayPlace.Outside, StrayPlace.Below, StrayPlace.High })
            {
                Assert.IsFalse(ReturnsAfter(place, StrayHold.None, 0f, StrayPieces.ReturnAfterSeconds - StrayPieces.CheckSeconds), $"{place} too soon");
                Assert.IsTrue(ReturnsAfter(place, StrayHold.None, 0f, StrayPieces.ReturnAfterSeconds), $"{place} after the delay");
                Assert.IsFalse(ReturnsAfter(place, StrayHold.None, StrayPieces.RestSpeed + 1f, 30f), $"{place} still moving");
            }
        }

        [Test]
        public void APieceThatStopsMovingMustRestBeforeItReturns()
        {
            var watch = new StrayWatch();
            for (int i = 0; i < 20; i++) Assert.IsFalse(StrayPieces.Tick(ref watch, StrayPlace.Outside, StrayHold.None, 3f, StrayPieces.CheckSeconds));
            Assert.IsFalse(StrayPieces.Tick(ref watch, StrayPlace.Outside, StrayHold.None, 0f, StrayPieces.CheckSeconds), "the first still check");
            Assert.IsTrue(StrayPieces.Tick(ref watch, StrayPlace.Outside, StrayHold.None, 0f, StrayPieces.CheckSeconds), "at rest for a second");
        }

        [Test]
        public void APieceInPlaceNeverReturnsAndForgetsItsTimeOut()
        {
            Assert.IsFalse(ReturnsAfter(StrayPlace.InPlace, StrayHold.None, 0f, 600f));
            var watch = new StrayWatch();
            for (int i = 0; i < 7; i++) StrayPieces.Tick(ref watch, StrayPlace.Outside, StrayHold.None, 0f, StrayPieces.CheckSeconds);
            Assert.Greater(watch.OutSeconds, 3f);
            Assert.IsFalse(StrayPieces.Tick(ref watch, StrayPlace.InPlace, StrayHold.None, 0f, StrayPieces.CheckSeconds));
            Assert.AreEqual(0f, watch.OutSeconds);
            Assert.AreEqual(0f, watch.RestSeconds);
            Assert.IsFalse(StrayPieces.Tick(ref watch, StrayPlace.Outside, StrayHold.None, 0f, StrayPieces.CheckSeconds), "the count starts again");
        }

        [Test]
        public void CarriedThrownGoalBrokenAndFrozenPiecesAreNeverMoved()
        {
            foreach (var hold in new[] { StrayHold.Carried, StrayHold.Thrown, StrayHold.Goal, StrayHold.Broken, StrayHold.Frozen, StrayHold.Goal | StrayHold.Frozen })
                foreach (var place in new[] { StrayPlace.Outside, StrayPlace.Below, StrayPlace.High, StrayPlace.Lost })
                {
                    Assert.IsFalse(ReturnsAfter(place, hold, 0f, 60f), $"{hold} {place}");
                    Assert.IsFalse(StrayPieces.Returns(place, hold, 600f, 600f), $"{hold} {place} decided directly");
                }
            var watch = new StrayWatch { OutSeconds = 3.5f, RestSeconds = 3.5f };
            Assert.IsFalse(StrayPieces.Tick(ref watch, StrayPlace.Outside, StrayHold.Carried, 0f, StrayPieces.CheckSeconds));
            Assert.AreEqual(0f, watch.OutSeconds, "picking a stray piece up starts its count again");
        }

        [Test]
        public void ALostPieceReturnsAtOnceEvenWhileFalling()
        {
            var watch = new StrayWatch();
            Assert.IsTrue(StrayPieces.Tick(ref watch, StrayPlace.Lost, StrayHold.None, 25f, StrayPieces.CheckSeconds));
            Assert.IsFalse(StrayPieces.Tick(ref watch, StrayPlace.Lost, StrayHold.Carried, 25f, StrayPieces.CheckSeconds));
        }

        [Test]
        public void TheDelayCanBeShortenedForTests()
        {
            var watch = new StrayWatch();
            Assert.IsTrue(StrayPieces.Tick(ref watch, StrayPlace.Outside, StrayHold.None, 0f, .25f, .2f));
            watch = new StrayWatch();
            Assert.IsFalse(StrayPieces.Tick(ref watch, StrayPlace.Outside, StrayHold.None, 0f, -5f), "time never runs backwards");
            Assert.AreEqual(0f, watch.OutSeconds);
        }

        [Test]
        public void TheFirstSpotIsHomeAndTheRestRingItInsideTheRoom()
        {
            var house = Pinwheel();
            var kitchen = house.Room("Kitchen");
            const float Radius = .2f;
            Assert.IsTrue(StrayPieces.TrySpot(kitchen, 10f, 4f, Radius, 0, out float x, out float z));
            Assert.AreEqual(10f, x);
            Assert.AreEqual(4f, z);
            var seen = new HashSet<(int, int)>();
            for (int i = 1; i < StrayPieces.SpotCount; i++)
            {
                Assert.IsTrue(StrayPieces.TrySpot(kitchen, 10f, 4f, Radius, i, out x, out z), $"spot {i} in the open");
                int ring = (i - 1) / StrayPieces.SpotsPerRing + 1;
                float away = (float)Math.Sqrt((x - 10f) * (x - 10f) + (z - 4f) * (z - 4f));
                Assert.AreEqual(StrayPieces.SpotStep(Radius) * ring, away, .001f, $"spot {i} distance");
                Assert.IsTrue(seen.Add(((int)Math.Round(x * 100f), (int)Math.Round(z * 100f))), $"spot {i} is new");
            }
            Assert.IsFalse(StrayPieces.TrySpot(kitchen, 10f, 4f, Radius, StrayPieces.SpotCount, out _, out _));
            Assert.IsFalse(StrayPieces.TrySpot(kitchen, 10f, 4f, Radius, -1, out _, out _));
        }

        [Test]
        public void SpotsNeverLeaveTheHomeRoomOrTouchItsWalls()
        {
            var house = Pinwheel();
            var kitchen = house.Room("Kitchen");
            const float Radius = .2f;
            int offered = 0;
            for (int i = 1; i < StrayPieces.SpotCount; i++)
            {
                if (!StrayPieces.TrySpot(kitchen, 6.5f, -.7f, Radius, i, out float x, out float z)) continue;
                offered++;
                Assert.GreaterOrEqual(x, kitchen.MinX + Radius + StrayPieces.WallSlack - .0001f);
                Assert.LessOrEqual(x, kitchen.MaxX - Radius - StrayPieces.WallSlack + .0001f);
                Assert.GreaterOrEqual(z, kitchen.MinZ + Radius + StrayPieces.WallSlack - .0001f);
                Assert.LessOrEqual(z, kitchen.MaxZ - Radius - StrayPieces.WallSlack + .0001f);
            }
            Assert.Greater(offered, 4, "a piece beside a wall still has spots on its open side");
            Assert.Less(offered, StrayPieces.SpotCount - 1, "the spots through the wall are refused");
            Assert.IsTrue(StrayPieces.TrySpot(null, 1f, 2f, Radius, 0, out _, out _), "home is always offered");
            Assert.IsFalse(StrayPieces.TrySpot(null, 1f, 2f, Radius, 1, out _, out _), "no room, no other spots");
        }

        [Test]
        public void BigPiecesStepFurtherThanSmallOnes()
        {
            Assert.AreEqual(StrayPieces.MinSpotStep, StrayPieces.SpotStep(.1f));
            Assert.AreEqual(2f + StrayPieces.SpotGap, StrayPieces.SpotStep(1f), .0001f);
            Assert.AreEqual(StrayPieces.MinSpotStep, StrayPieces.SpotStep(-3f));
        }

        [Test]
        public void AHouseWithNoRoomsMovesNothing()
        {
            Assert.AreEqual(StrayPlace.InPlace, StrayPieces.Locate(null, 0f, 99f, -99f, 99f, -99f));
            Assert.AreEqual(StrayPlace.InPlace, StrayPieces.Locate(new HouseLayout(), 0f, 99f, -99f, 99f, -99f));
            Assert.IsNull(StrayPieces.FloorUnder(null, 0f, 0f, 0f));
            Assert.AreEqual(0f, StrayPieces.Rise(new HouseLayout(), 0f, 2f, 0f, 2f));
        }
    }
}
