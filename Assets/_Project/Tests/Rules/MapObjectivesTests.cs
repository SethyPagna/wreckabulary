using System;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class MapObjectivesTests
    {
        [TestCase("house_pinwheel.json")]
        [TestCase("house_courtyard.json")]
        [TestCase("house_flat.json")]
        [TestCase("house_terrace.json")]
        public void EveryMapHasValidGeometryAndEscapeOrders(string file)
        {
            var house = HouseLayout.FromJson(TestData.Read(file), file);
            Assert.IsEmpty(house.Validate(TestData.Catalogue()));
            foreach (string mode in new[] { "Dibs", "Duos", "MovingOut" })
            {
                Assert.IsTrue(house.ClearOutOrders.ContainsKey(mode));
                Assert.IsEmpty(house.Graph().CheckClosureOrder(house.ClearOutOrders[mode], house.NeverClose));
            }
        }

        [TestCase("house_pinwheel.json")]
        [TestCase("house_courtyard.json")]
        [TestCase("house_flat.json")]
        [TestCase("house_terrace.json")]
        public void KeepsakesArePhysicalFurnitureAndTheVanRemainsOpen(string file)
        {
            var house = HouseLayout.FromJson(TestData.Read(file), file);
            Assert.AreEqual(3, house.Keepsakes.Count);
            foreach (var keepsake in house.Keepsakes)
                Assert.IsTrue(house.Furniture.Any(f => f.Room == keepsake.Room && Math.Abs(f.X - keepsake.X) < .01f && Math.Abs(f.Z - keepsake.Z) < .01f),
                    $"{file}: keepsake at ({keepsake.X}, {keepsake.Z}) needs a physical prop");
            Assert.IsFalse(house.ClearOutOrders["MovingOut"].Contains(house.ExtractionRoom));
            Assert.IsTrue(house.Room(house.ExtractionRoom).Contains(house.ExtractionX, house.ExtractionZ));
            Assert.IsTrue(house.Graph().Connected(house.Rooms.Select(r => r.Name).Except(house.ClearOutOrders["MovingOut"]).ToArray()));
        }

        [TestCase("house_pinwheel.json")]
        [TestCase("house_courtyard.json")]
        [TestCase("house_flat.json")]
        [TestCase("house_terrace.json")]
        public void MovingDayChecklistUsesKnownFurnitureWordsInExistingRooms(string file)
        {
            var house = HouseLayout.FromJson(TestData.Read(file), file);
            Assert.AreEqual(4, house.MovingDay.Count);
            foreach (var objective in house.MovingDay)
            {
                Assert.IsTrue(TestData.Catalogue().TryGet(objective.Word, out _));
                Assert.IsTrue(house.Rooms.Any(r => r.Name == objective.Room));
            }
        }

        [Test]
        public void TheFlatIsOneFloorAroundAHallEveryRoomOpensOnto()
        {
            var flat = HouseLayout.FromJson(TestData.Read("house_flat.json"), "house_flat.json");
            Assert.IsTrue(flat.Rooms.All(r => r.FloorY == 0f), "one floor");
            CollectionAssert.AreEquivalent(flat.Rooms.Select(r => r.Name).Where(n => n != "Hall"), flat.Graph().Neighbours("Hall"));
            CollectionAssert.Contains(flat.NeverClose, "Hall");
            Assert.IsFalse(HomeDesigner.Supports("flat"), "homes are designed only on the maps the web edition knows too");
            Assert.IsTrue(HomeDesigner.Supports("pinwheel") && HomeDesigner.Supports("courtyard"));
        }

        [Test]
        public void TheTerraceHouseHasAnUpstairsReachedOnlyByItsStairs()
        {
            var terrace = HouseLayout.FromJson(TestData.Read("house_terrace.json"), "house_terrace.json");
            CollectionAssert.AreEqual(new[] { 0f, 3f }, terrace.StoreyFloors());
            var stairs = terrace.Stairs.Single();
            Assert.AreEqual("Hall", stairs.Lower);
            Assert.AreEqual("Landing", stairs.Upper);
            var downstairs = terrace.Rooms.Where(r => r.FloorY == 0f).Select(r => r.Name).ToArray();
            Assert.IsTrue(terrace.Graph().Connected(downstairs), "the ground floor works on its own");
            var upstairs = terrace.Rooms.Where(r => r.FloorY == 3f).Select(r => r.Name).ToArray();
            Assert.IsTrue(terrace.Graph().Connected(upstairs.Append("Hall").ToArray()), "upstairs is reached from the hall");
            foreach (string room in downstairs.Where(r => r != "Hall"))
                Assert.IsFalse(terrace.Graph().Connected(upstairs.Append(room).ToArray()), $"upstairs is not reached from the {room}");
            Assert.AreEqual(2, terrace.Spawns.Count(s => terrace.Room(s.Room).FloorY == 0f), "two players start downstairs");
            Assert.AreEqual(2, terrace.Spawns.Count(s => terrace.Room(s.Room).FloorY == 3f), "two players start upstairs");
            Assert.AreEqual(0f, terrace.Room(terrace.ExtractionRoom).FloorY, "the van parks on the ground floor");
            CollectionAssert.AreEquivalent(new[] { "Hall", "Landing" }, terrace.NeverClose);
            Assert.IsTrue(terrace.MovingDay.Any(m => terrace.Room(m.Room).FloorY == 3f), "Moving Day sends something upstairs");
            Assert.IsFalse(HomeDesigner.Supports("terrace"));
        }

        [Test]
        public void CourtyardOffersMoreSpaceAndWiderDoorways()
        {
            var courtyard = HouseLayout.FromJson(TestData.Read("house_courtyard.json"));
            var pinwheel = TestData.House();
            float Area(HouseLayout h) => h.Rooms.Sum(r => (r.MaxX - r.MinX) * (r.MaxZ - r.MinZ));
            Assert.Greater(Area(courtyard), Area(pinwheel) * 2f);
            Assert.Greater(courtyard.Doors.Min(d => d.Width), pinwheel.Doors.Min(d => d.Width));
            Assert.AreEqual(12f, courtyard.Room("Garden").MaxX - courtyard.Room("Garden").MinX);
        }
    }
}
