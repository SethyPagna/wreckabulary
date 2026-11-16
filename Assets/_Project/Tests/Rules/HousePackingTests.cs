using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class HousePackingTests
    {
        const float SmallestEndgame = 140f, LargestEndgame = 260f;
        static readonly string[] Files = { "house_pinwheel.json", "house_courtyard.json", "house_flat.json", "house_terrace.json", "house_walkup.json" };

        static HouseLayout Load(string file) => HouseLayout.FromJson(TestData.Read(file), file);

        static IEnumerable<TestCaseData> PackingModes() =>
            from file in Files from mode in new[] { "Dibs", "Duos", "MovingOut", "Escape" } select new TestCaseData(file, mode);

        static IEnumerable<TestCaseData> BattleModes() =>
            from file in Files from mode in new[] { "Dibs", "Duos" } select new TestCaseData(file, mode);

        [TestCaseSource(nameof(PackingModes))]
        public void EveryListedRoomIsPackedBeforeTimeRunsOut(string file, string mode)
        {
            var house = Load(file);
            var rules = TestData.HouseRulesFor(mode);
            var schedule = new ClearOutSchedule(rules, house.ClearOutOrders[mode]);
            Assert.AreEqual(house.ClearOutOrders[mode].Count, schedule.Closures.Count);
            Assert.Less(schedule.Closures.Last().ClosedAt, rules.RoundTimeLimitSeconds, $"{file} {mode}: no warning promises a room the round never packs");
            Assert.GreaterOrEqual(schedule.Interval, rules.ClearOutFillSeconds, $"{file} {mode}: the Movers pack one room at a time");
        }

        [TestCaseSource(nameof(BattleModes))]
        public void DibsAndDuosEndInASpaceAboutOneRoomBig(string file, string mode)
        {
            var house = Load(file);
            var rules = TestData.HouseRulesFor(mode);
            var schedule = new ClearOutSchedule(rules, house.ClearOutOrders[mode]);
            var open = schedule.OpenRooms(house.Rooms.Select(r => r.Name), rules.RoundTimeLimitSeconds).ToArray();
            float area = open.Sum(r => house.Room(r).Area);
            Assert.That(area, Is.InRange(SmallestEndgame, LargestEndgame), $"{file} {mode}: {area} m² stays open ({string.Join(", ", open)})");
            Assert.IsTrue(house.Graph().Connected(open), $"{file} {mode}: the last rooms join up");
            CollectionAssert.IsSubsetOf(house.NeverClose, open, $"{file} {mode}: the protected rooms are the endgame");
        }

        [Test]
        public void OnlyTheWalkUpPacksFasterThanTheModeTimers()
        {
            foreach (string file in Files)
            {
                var house = Load(file);
                foreach (var order in house.ClearOutOrders)
                {
                    var rules = TestData.HouseRulesFor(order.Key);
                    double interval = new ClearOutSchedule(rules, order.Value).Interval;
                    bool paced = file == "house_walkup.json" && order.Key is "Dibs" or "Duos";
                    if (paced) Assert.Less(interval, rules.ClearOutInterval, $"{file} {order.Key}: nineteen rooms need a faster pace");
                    else Assert.AreEqual(rules.ClearOutInterval, interval, 1e-6, $"{file} {order.Key} keeps the mode timer");
                }
            }
            var walkup = Load("house_walkup.json");
            Assert.AreEqual(9.0, new ClearOutSchedule(TestData.HouseRulesFor("Dibs"), walkup.ClearOutOrders["Dibs"]).Interval, 1e-6);
            Assert.AreEqual(TestData.HouseRulesFor("Dibs").RoundTimeLimitSeconds, TestData.HouseRulesFor("Duos").RoundTimeLimitSeconds, "no round gets longer");
        }

        const string Box = @"{
 ""name"": ""Box"",
 ""rooms"": [ { ""name"": ""A"", ""bounds"": [0, 0, 4, 4] }, { ""name"": ""B"", ""bounds"": [4, 0, 8, 4] } ],
 ""doors"": [ { ""between"": [""A"", ""B""], ""at"": [4, 2] } ],
 ""spawns"": [],
 ""furniture"": [],
 ""neverClose"": [""A""],
 ""clearOutOrders"": { ""Dibs"": [""B""] }INTERVALS
}";

        static HouseLayout BoxWith(string intervals) =>
            HouseLayout.FromJson(Box.Replace("INTERVALS", intervals == null ? "" : @", ""clearOutIntervals"": " + intervals), "box");

        [Test]
        public void APackingIntervalIsOptionalAndMustFitAnOrder()
        {
            var rules = TestData.HouseRulesFor("Dibs");
            Assert.AreEqual(rules.ClearOutInterval, new ClearOutSchedule(rules, BoxWith(null).ClearOutOrders["Dibs"]).Interval, 1e-6);
            var paced = new ClearOutSchedule(rules, BoxWith(@"{ ""Dibs"": 12 }").ClearOutOrders["Dibs"]);
            Assert.AreEqual(12.0, paced.Interval, 1e-6);
            Assert.AreEqual(rules.ClearOutFirstAt, paced.Closures[0].FillAt, 1e-6, "the first room still goes at the mode's time");
            Assert.Throws<FormatException>(() => BoxWith(@"{ ""Duos"": 12 }"), "an interval needs an order");
            Assert.Throws<FormatException>(() => BoxWith(@"{ ""Dibs"": 0 }"));
            Assert.Throws<FormatException>(() => BoxWith(@"{ ""Dibs"": -4 }"));
            Assert.AreEqual(rules.ClearOutInterval, new ClearOutSchedule(rules, new List<string> { "B" }).Interval, 1e-6, "a plain list keeps the mode timer");
        }

        [Test]
        public void EveryHouseHasItsOwnMovingDayChecklist()
        {
            var lists = Files.Select(f => string.Join(",", Load(f).MovingDay.Select(m => m.Word).OrderBy(w => w, StringComparer.Ordinal))).ToArray();
            Assert.AreEqual(lists.Length, lists.Distinct().Count(), "no two houses ask for the same furniture: " + string.Join(" | ", lists));
        }

        [TestCaseSource(nameof(Files))]
        public void MovingDayFurnitureNeverBelongsWhereTheVanDropsIt(string file)
        {
            var house = Load(file);
            var catalogue = TestData.Catalogue();
            Assert.AreEqual(4, house.MovingDay.Count);
            foreach (var objective in house.MovingDay)
            {
                Assert.AreNotEqual(house.ExtractionRoom, objective.Room, $"{file}: {objective.Word} would already be home at the van");
                Assert.IsNotNull(house.Room(objective.Room), $"{file}: {objective.Room}");
                Assert.IsTrue(catalogue.TryGet(objective.Word, out var item) && item.Enabled, $"{file}: {objective.Word} can be spelled");
            }
        }
    }
}
