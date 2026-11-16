using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class MapCardTextTests
    {
        static HouseLayout Read(string id) => HouseLayout.FromJson(TestData.Read("house_" + id + ".json"), "house_" + id + ".json");

        static RoomBox Room(string name, float floor = 0f, bool outdoor = false) =>
            new RoomBox { Name = name, MinX = 0, MaxX = 4, MinZ = 0, MaxZ = 4, FloorY = floor, Outdoor = outdoor };

        [TestCase(1, 1, "", "1 floor · 1 room")]
        [TestCase(2, 9, "garden", "2 floors · 9 rooms · garden")]
        [TestCase(4, 12, "roof garden", "4 floors · 12 rooms · roof garden")]
        public void TheHouseLineCountsFloorsAndRoomsAndNamesTheOutdoors(int floors, int rooms, string outdoors, string line) =>
            Assert.AreEqual(line, MapCardText.HouseLine(floors, rooms, outdoors));

        [Test]
        public void HallsLandingsAndOutdoorSpacesAreNotCountedAsRooms()
        {
            var house = new HouseLayout();
            house.Rooms.Add(Room("Kitchen"));
            house.Rooms.Add(Room("Hall"));
            house.Rooms.Add(Room("Bedroom", 3f));
            house.Rooms.Add(Room("Landing", 3f));
            house.Rooms.Add(Room("Garden", 0f, true));
            Assert.AreEqual("2 floors · 2 rooms · garden", MapCardText.HouseLine(house));
        }

        [Test]
        public void AnOutdoorSpaceOnlyUpstairsIsARoofGarden()
        {
            var house = new HouseLayout();
            house.Rooms.Add(Room("Lounge"));
            house.Rooms.Add(Room("Roof", 3f, true));
            Assert.AreEqual("roof garden", MapCardText.Outdoors(house));
            Assert.AreEqual("2 floors · 1 room · roof garden", MapCardText.HouseLine(house));
        }

        [Test]
        public void AnEmptyOrMissingHouseHasNoLine()
        {
            Assert.AreEqual("", MapCardText.HouseLine(null));
            Assert.AreEqual("", MapCardText.HouseLine(new HouseLayout()));
        }

        [TestCase("pinwheel")]
        [TestCase("courtyard")]
        [TestCase("flat")]
        [TestCase("terrace")]
        [TestCase("walkup")]
        public void EveryInstalledHouseHasAShortLineThatStartsWithItsFloors(string id)
        {
            var house = Read(id);
            string line = MapCardText.HouseLine(house);
            int floors = house.StoreyFloors().Count;
            StringAssert.StartsWith(floors + (floors == 1 ? " floor · " : " floors · "), line);
            Assert.LessOrEqual(line.Length, 40, line);
        }
    }
}
