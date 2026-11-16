using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class BattleRulesTests
    {
        static KnockoutRace Race(int target = 3, float seconds = 240f, int seats = 4)
        {
            var race = new KnockoutRace(target, seconds);
            for (int i = 0; i < seats; i++) race.Enter(i);
            return race;
        }

        [Test]
        public void FirstToTheTargetWinsTheRace()
        {
            var race = Race();
            Assert.IsTrue(race.Knockout(1, 0));
            Assert.IsTrue(race.Knockout(2, 0));
            Assert.IsFalse(race.Over);
            Assert.IsTrue(race.Knockout(3, 0));
            Assert.AreEqual(0, race.Winner);
            Assert.IsFalse(race.TimeUp);
            Assert.IsFalse(race.Knockout(1, 2), "the race is over");
        }

        [Test]
        public void SelfAndUnknownKnockoutsScoreNothing()
        {
            var race = Race();
            Assert.IsFalse(race.Knockout(1, 1));
            Assert.IsFalse(race.Knockout(1, -1));
            Assert.IsFalse(race.Knockout(1, 9));
            Assert.IsTrue(race.Seats.All(s => race.Of(s) == 0));
        }

        [Test]
        public void TheLoneLeaderWinsWhenTimeRunsOut()
        {
            var race = Race(10, 60f);
            race.Knockout(0, 2);
            race.Tick(59.9f);
            Assert.IsFalse(race.Over);
            race.Tick(60f);
            Assert.AreEqual(2, race.Winner);
            Assert.IsTrue(race.TimeUp);
            Assert.IsFalse(race.WonInSuddenDeath);
        }

        [Test]
        public void ATieAtTheBellGoesToSuddenDeathForTheLeaders()
        {
            var race = Race(10, 60f);
            race.Knockout(3, 0);
            race.Knockout(3, 1);
            race.Tick(60f);
            Assert.IsTrue(race.SuddenDeath);
            Assert.AreEqual(-1, race.Winner);
            race.Knockout(0, 2);
            Assert.IsTrue(race.SuddenDeath, "a trailing knockout leaves the leaders tied");
            race.Knockout(2, 1);
            Assert.AreEqual(1, race.Winner);
            Assert.IsTrue(race.WonInSuddenDeath);
            Assert.IsFalse(race.SuddenDeath);
        }

        [Test]
        public void ANilAllBellMeansTheNextKnockoutWins()
        {
            var race = Race(10, 30f);
            race.Tick(31f);
            Assert.IsTrue(race.SuddenDeath);
            race.Knockout(0, 3);
            Assert.AreEqual(3, race.Winner);
        }

        [Test]
        public void StandingsSortByKnockoutsThenSeat()
        {
            var race = Race(10);
            race.Knockout(0, 2); race.Knockout(0, 2); race.Knockout(2, 3);
            Assert.AreEqual(new[] { 2, 3, 0, 1 }, race.Standings.ToArray());
            Assert.AreEqual(2, race.Leader);
        }

        [Test]
        public void AKnockoutCostsALifeUntilThePoolIsEmpty()
        {
            var war = new WarScore(2, 2, 300f);
            Assert.IsTrue(war.Knockout(1, 0));
            Assert.IsTrue(war.Knockout(1, 0));
            Assert.AreEqual(0, war.Lives(1));
            Assert.IsFalse(war.Knockout(1, 0), "no lives left: the player stays out");
            Assert.AreEqual(3, war.KnockoutsBy(0));
            Assert.AreEqual(2, war.Lives(0));
            Assert.IsFalse(war.Knockout(5));
        }

        [Test]
        public void ATeamOutOfLivesWithNobodyStandingLoses()
        {
            var war = new WarScore(2, 0, 300f);
            war.Evaluate(new[] { 2, 1 }, 10f);
            Assert.IsFalse(war.Over, "both teams still stand");
            war.Evaluate(new[] { 2, 0 }, 11f);
            Assert.AreEqual(0, war.Winner);
            Assert.AreEqual(1, war.Loser);
            Assert.IsFalse(war.TimeUp);
        }

        [Test]
        public void LivesKeepATeamInWhileItsPlayersAreDown()
        {
            var war = new WarScore(2, 1, 300f);
            war.Evaluate(new[] { 1, 0 }, 10f);
            Assert.IsFalse(war.Over);
        }

        [Test]
        public void TimeUpGoesToTheStrongerTeamThenSuddenDeath()
        {
            var war = new WarScore(2, 3, 60f);
            war.Knockout(1, 0);
            war.Evaluate(new[] { 4, 4 }, 60f);
            Assert.AreEqual(0, war.Winner);
            Assert.IsTrue(war.TimeUp);

            var tied = new WarScore(2, 3, 60f);
            tied.Evaluate(new[] { 4, 4 }, 61f);
            Assert.IsTrue(tied.SuddenDeath);
            tied.Knockout(0, 1);
            tied.Evaluate(new[] { 4, 4 }, 62f);
            Assert.AreEqual(1, tied.Winner);
            Assert.AreEqual(0, tied.Loser);
            Assert.IsTrue(tied.WonInSuddenDeath);
        }

        [Test]
        public void BothTeamsGoneTogetherIsADraw()
        {
            var war = new WarScore(2, 0, 60f);
            war.Evaluate(new[] { 0, 0 }, 5f);
            Assert.IsTrue(war.Draw);
            Assert.AreEqual(Teams.NoTeam, war.Winner);
        }

        [Test]
        public void NoBonusLivesAfterTheMatchIsDecided()
        {
            var war = new WarScore(2, 0, 60f);
            war.Grant(0);
            Assert.AreEqual(1, war.Lives(0));
            war.Evaluate(new[] { 1, 0 }, 1f);
            war.Grant(1);
            Assert.AreEqual(0, war.Lives(1), "no lives after the match is decided");
        }

        [Test]
        public void HoldingTheRoomAloneEarnsALifeEveryInterval()
        {
            var hold = new RoomHold(20f);
            Assert.AreEqual(Teams.NoTeam, hold.Tick(0, 19f));
            Assert.AreEqual(Teams.NoTeam, hold.Tick(Teams.NoTeam, 5f), "contested or empty pauses");
            Assert.AreEqual(0, hold.Tick(0, 1f));
            Assert.AreEqual(0f, hold.Progress, 1e-4f);
            hold.Tick(0, 10f);
            Assert.AreEqual(Teams.NoTeam, hold.Tick(1, 15f), "a new holder starts from zero");
            Assert.AreEqual(1, hold.Tick(1, 5f));
            Assert.AreEqual(Teams.NoTeam, new RoomHold(0f).Tick(0, 100f), "off when holdBonus is 0");
        }

        [Test]
        public void OnlyOneSideInTheRoomHoldsIt()
        {
            Assert.AreEqual(1, RoomHold.HolderOf(new[] { 1, 1 }));
            Assert.AreEqual(Teams.NoTeam, RoomHold.HolderOf(new[] { 1, 0 }));
            Assert.AreEqual(Teams.NoTeam, RoomHold.HolderOf(new int[0]));
        }

        [Test]
        public void RespawnsPickTheSpotFarthestFromEnemies()
        {
            var spawns = new[] { new Spot(0, 0, 0), new Spot(10, 0, 0), new Spot(0, 0, 10), new Spot(10, 0, 10) };
            Assert.AreEqual(3, SpawnPick.Farthest(spawns, Enumerable.Range(0, 4), new[] { new Spot(1, 0, 1) }));
            Assert.AreEqual(1, SpawnPick.Farthest(spawns, new[] { 0, 1 }, new[] { new Spot(1, 0, 1), new Spot(0, 0, 9) }));
            Assert.AreEqual(2, SpawnPick.Farthest(spawns, new[] { 2, 3 }, new Spot[0]), "no enemies: the first allowed spot");
            Assert.AreEqual(7, SpawnPick.Farthest(spawns, new int[0], new Spot[0], 7));
            Assert.AreEqual(0, SpawnPick.Farthest(spawns, Enumerable.Range(0, 4), new[] { new Spot(10, 4, 10), new Spot(10, 0, 0), new Spot(0, 0, 10) }));
        }

        [Test]
        public void SidesAlternateSeatsSoColoursNeverCross()
        {
            Assert.AreEqual(new[] { 0, 1, 0, 1, 0, 1, 0, 1 }, Teams.Sides(8, 2));
            Assert.AreEqual(new[] { 0, 1, 0 }, Teams.For(3, 4, 2));
            Assert.AreEqual(new[] { 0, 1, 0, 1 }, Teams.For(4, 2, 0), "no sides: the old team-size split");
            Assert.AreEqual("Team 1", Teams.SideName(0));
            Assert.AreEqual("", Teams.SideName(Teams.NoTeam));
        }

        static readonly string[] Houses = { "pinwheel", "courtyard", "flat", "terrace", "walkup" };

        static (HouseLayout layout, List<Spot> spawns) Load(string id)
        {
            var layout = HouseLayout.FromJson(TestData.Read($"house_{id}.json"), $"house_{id}.json");
            var spawns = layout.Spawns.Select(s => new Spot(s.X, layout.Room(s.Room).FloorY, s.Z)).ToList();
            return (layout, spawns);
        }

        [TestCaseSource(nameof(Houses))]
        public void EveryHouseSplitsIntoTwoEndsWithAHoldRoomBetween(string id)
        {
            var (layout, spawns) = Load(id);
            var ends = HouseEnds.Split(spawns);
            Assert.AreEqual(spawns.Count, ends.Length);
            Assert.IsNotEmpty(HouseEnds.Of(ends, 0).Where(i => ends[i] == 0).ToList(), id);
            Assert.IsNotEmpty(HouseEnds.Of(ends, 1).Where(i => ends[i] == 1).ToList(), id);
            string middle = HouseEnds.MiddleRoom(layout, spawns, ends);
            Assert.IsNotNull(layout.Room(middle), id);
            Assert.IsFalse(layout.Room(middle).Outdoor, id);
            Assert.IsFalse(layout.Spawns.Any(s => s.Room == middle), $"{id}: {middle} holds a spawn");
        }

        [Test]
        public void ASingleSpawnServesBothEnds()
        {
            var ends = HouseEnds.Split(new[] { new Spot(0, 0, 0) });
            Assert.AreEqual(new[] { 0 }, ends);
            Assert.AreEqual(new List<int> { 0 }, HouseEnds.Of(ends, 1));
        }
    }
}
