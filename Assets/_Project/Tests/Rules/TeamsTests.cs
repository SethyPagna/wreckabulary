using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class TeamsTests
    {
        const int DuoSize = 2, CoopSize = 4, BattleSides = 2, BattleSeats = 8, NoSides = 0, MostSeats = 8;

        static bool[] FirstHumans(int seats, int humans) => Enumerable.Range(0, seats).Select(i => i < humans).ToArray();

        static bool[] Mask(int seats, int bits) => Enumerable.Range(0, seats).Select(i => (bits & (1 << i)) != 0).ToArray();

        static IEnumerable<bool[]> EveryMask(int seats) => Enumerable.Range(0, 1 << seats).Select(bits => Mask(seats, bits));

        static string Show(bool[] human) => string.Concat(human.Select(h => h ? 'H' : 'b'));

        static int[] SizesOf(int[] teams) => teams.GroupBy(t => t).OrderBy(g => g.Key).Select(g => g.Count()).ToArray();

        [Test]
        public void DuosPutsCouchPlayersOnTheSameTeam()
        {
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, Teams.Assign(4, DuoSize, FirstHumans(4, 2)), "two humans against two bots");
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, Teams.Assign(4, DuoSize, FirstHumans(4, 3)), "humans 1 and 2 together, human 3 with the bot");
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, Teams.Assign(4, DuoSize, FirstHumans(4, 4)), "1 and 2 against 3 and 4");
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1 }, Teams.For(4, DuoSize, NoSides, FirstHumans(4, 2)));
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, Teams.Assign(3, DuoSize, FirstHumans(3, 3)), "three seats keep the two and one split");
        }

        [Test]
        public void TeamBattleFillsTheFirstSideWithHumansUpToItsShare()
        {
            CollectionAssert.AreEqual(new[] { 0, 0, 1, 1, 0, 1, 0, 1 }, Teams.Sides(BattleSeats, BattleSides, FirstHumans(BattleSeats, 2)));
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 1, 1, 1, 0, 1 }, Teams.Sides(BattleSeats, BattleSides, FirstHumans(BattleSeats, 3)));
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 1, 1, 1, 1 }, Teams.Sides(BattleSeats, BattleSides, FirstHumans(BattleSeats, 4)));
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 1, 1, 1, 1 }, Teams.Sides(BattleSeats, BattleSides, FirstHumans(BattleSeats, 5)), "the fifth human joins the bots' side");
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 1, 1, 1, 1 }, Teams.For(BattleSeats, CoopSize, BattleSides, FirstHumans(BattleSeats, BattleSeats)));
            CollectionAssert.AreEqual(new[] { 0, 0, 1 }, Teams.For(3, CoopSize, BattleSides, FirstHumans(3, 2)), "a short table still keeps the couch together");
        }

        [Test]
        public void OneHumanOrNoneKeepsTheOldDeal()
        {
            for (int seats = 0; seats <= MostSeats; seats++)
                foreach (bool[] human in EveryMask(seats).Where(m => m.Count(h => h) < Teams.FewestHumansToGroup))
                {
                    CollectionAssert.AreEqual(Teams.Assign(seats, DuoSize), Teams.Assign(seats, DuoSize, human), $"Duos {Show(human)}");
                    CollectionAssert.AreEqual(Teams.Sides(seats, BattleSides), Teams.Sides(seats, BattleSides, human), $"Team Battle {Show(human)}");
                    CollectionAssert.AreEqual(Enumerable.Range(0, seats).ToArray(), Teams.Places(seats, DuoSize, NoSides, human), $"places {Show(human)}");
                }
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 1 }, Teams.Assign(4, DuoSize, FirstHumans(4, 1)));
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 1, 0, 1, 0, 1 }, Teams.Sides(BattleSeats, BattleSides, FirstHumans(BattleSeats, 1)));
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 1 }, Teams.Assign(4, DuoSize, null), "no human list: the old deal");
        }

        [Test]
        public void ModesWithoutRivalTeamsKeepTheOldDeal()
        {
            for (int seats = 0; seats <= CoopSize; seats++)
                foreach (bool[] human in EveryMask(seats))
                {
                    CollectionAssert.AreEqual(Teams.Assign(seats, 1), Teams.Assign(seats, 1, human), $"everyone for themselves {Show(human)}");
                    CollectionAssert.AreEqual(Teams.Assign(seats, CoopSize), Teams.Assign(seats, CoopSize, human), $"co-op {Show(human)}");
                    CollectionAssert.AreEqual(Enumerable.Range(0, seats).ToArray(), Teams.Places(seats, 1, NoSides, human));
                    CollectionAssert.AreEqual(Enumerable.Range(0, seats).ToArray(), Teams.Places(seats, CoopSize, NoSides, human));
                }
        }

        [Test]
        public void EveryTableKeepsTheOldTeamSizesAndGivesEachSeatOneOldPlace()
        {
            for (int seats = 0; seats <= MostSeats; seats++)
                for (int teamSize = 1; teamSize <= CoopSize; teamSize++)
                    foreach (int sides in new[] { NoSides, BattleSides })
                        foreach (bool[] human in EveryMask(seats))
                        {
                            string what = $"{seats} seats, team size {teamSize}, {sides} sides, {Show(human)}";
                            int[] old = Teams.For(seats, teamSize, sides), teams = Teams.For(seats, teamSize, sides, human);
                            int[] places = Teams.Places(seats, teamSize, sides, human);
                            CollectionAssert.AreEqual(SizesOf(old), SizesOf(teams), what);
                            CollectionAssert.AreEquivalent(Enumerable.Range(0, seats).ToArray(), places, what);
                            for (int seat = 0; seat < seats; seat++) Assert.AreEqual(old[places[seat]], teams[seat], what);
                        }
        }

        [Test]
        public void HumansFillTeamsInSeatOrderAndBotsKeepTheirOrder()
        {
            foreach ((int seats, int teamSize, int sides) in new[] { (4, DuoSize, NoSides), (3, DuoSize, NoSides), (6, DuoSize, NoSides), (BattleSeats, CoopSize, BattleSides) })
                foreach (bool[] human in EveryMask(seats).Where(m => m.Count(h => h) >= Teams.FewestHumansToGroup))
                {
                    string what = $"{seats} seats, team size {teamSize}, {sides} sides, {Show(human)}";
                    int[] teams = Teams.For(seats, teamSize, sides, human), places = Teams.Places(seats, teamSize, sides, human);
                    int[] humanTeams = Enumerable.Range(0, seats).Where(i => human[i]).Select(i => teams[i]).ToArray();
                    int[] botPlaces = Enumerable.Range(0, seats).Where(i => !human[i]).Select(i => places[i]).ToArray();
                    CollectionAssert.AreEqual(humanTeams.OrderBy(t => t).ToArray(), humanTeams, what + ": humans fill team 0 first, in seat order");
                    CollectionAssert.AreEqual(botPlaces.OrderBy(p => p).ToArray(), botPlaces, what + ": bots keep seat order in the places left");
                    int mixed = Enumerable.Range(0, seats).GroupBy(i => teams[i]).Count(g => g.Any(i => human[i]) && g.Any(i => !human[i]));
                    Assert.LessOrEqual(mixed, 1, what + ": at most one team mixes humans and bots");
                    Assert.AreEqual(0, teams[Array.IndexOf(human, true)], what + ": the first human is on team 0");
                    Assert.IsTrue(Teams.AreTeammates(humanTeams[0], humanTeams[1]), what + ": the first two humans are team mates");
                }
        }

        [Test]
        public void HumansInLaterSeatsAreStillGroupedTogether()
        {
            CollectionAssert.AreEqual(new[] { 1, 0, 1, 0 }, Teams.Assign(4, DuoSize, new[] { false, true, false, true }));
            CollectionAssert.AreEqual(new[] { 1, 1, 0, 0 }, Teams.Assign(4, DuoSize, new[] { false, false, true, true }));
            CollectionAssert.AreEqual(new[] { 0, 1, 1, 0 }, Teams.Assign(4, DuoSize, new[] { true, false, false, true }));
        }

        [Test]
        public void PlacesKeepEachTeamOnItsOldStartSpots()
        {
            CollectionAssert.AreEqual(new[] { 0, 2, 1, 3 }, Teams.Places(4, DuoSize, NoSides, FirstHumans(4, 2)));
            CollectionAssert.AreEqual(new[] { 0, 2, 1, 3 }, Teams.Places(4, DuoSize, NoSides, FirstHumans(4, 3)));
            CollectionAssert.AreEqual(new[] { 0, 2, 1, 3 }, Teams.Places(4, DuoSize, NoSides, FirstHumans(4, 4)));
            CollectionAssert.AreEqual(new[] { 0, 2, 1, 3, 4, 5, 6, 7 }, Teams.Places(BattleSeats, CoopSize, BattleSides, FirstHumans(BattleSeats, 2)));
            CollectionAssert.AreEqual(new[] { 0, 2, 4, 6, 1, 3, 5, 7 }, Teams.Places(BattleSeats, CoopSize, BattleSides, FirstHumans(BattleSeats, 4)));
            CollectionAssert.AreEqual(new[] { 0, 2, 1, 3 }, Teams.Places(new[] { 0, 1, 0, 1 }, FirstHumans(4, 2)));
        }

        [TestCase("house_pinwheel.json")]
        [TestCase("house_courtyard.json")]
        [TestCase("house_flat.json")]
        [TestCase("house_terrace.json")]
        [TestCase("house_walkup.json")]
        public void CouchDuosStillClosesAStartRoomOfEachTeamFirst(string file)
        {
            var house = HouseLayout.FromJson(TestData.Read(file), file);
            int seats = house.Spawns.Count;
            for (int humans = Teams.FewestHumansToGroup; humans <= seats; humans++)
            {
                int[] teams = Teams.Assign(seats, DuoSize, FirstHumans(seats, humans)), places = Teams.Places(seats, DuoSize, NoSides, FirstHumans(seats, humans));
                var teamAtSpawn = Enumerable.Range(0, seats).ToDictionary(seat => places[seat], seat => teams[seat]);
                var closing = house.ClearOutOrders["Duos"].Where(room => house.Spawns.Any(s => s.Room == room))
                    .Select(room => teamAtSpawn[house.Spawns.FindIndex(s => s.Room == room)]).Take(2).ToArray();
                Assert.AreEqual(2, closing.Length, $"{file}: Duos closes at least two start rooms");
                Assert.AreNotEqual(closing[0], closing[1], $"{file}, {humans} humans: the first two start rooms to close belong to different teams");
            }
        }

        [Test]
        public void TheHumanListMustCoverEverySeat()
        {
            Assert.Throws<ArgumentException>(() => Teams.Assign(4, DuoSize, new[] { true, true }));
            Assert.Throws<ArgumentException>(() => Teams.Places(new[] { 0, 1, 0, 1 }, new[] { true, true, false, false, false }));
            Assert.Throws<ArgumentNullException>(() => Teams.Places(null, new bool[0]));
            Assert.Throws<ArgumentOutOfRangeException>(() => Teams.Assign(4, 0, FirstHumans(4, 2)));
            Assert.Throws<ArgumentOutOfRangeException>(() => Teams.Sides(-1, BattleSides, new bool[0]));
        }
    }
}
