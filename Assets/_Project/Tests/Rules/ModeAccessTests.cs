using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public class ModeAccessTests
    {
        static string Line(string id) => ModeAccess.For(id).Line;

        static string[] Board => HouseModes.Families.SelectMany(HouseModes.ModesIn).ToArray();

        [Test]
        public void EveryHouseModeSaysWhoPlaysOnThisScreen()
        {
            foreach (string id in new[] { HouseModes.Dibs, HouseModes.FreeForAll, HouseModes.ShoppingList, HouseModes.RiddleRush })
                Assert.AreEqual("1 to 4 on this screen · bots fill", Line(id), id);
            foreach (string id in new[] { HouseModes.Duos, HouseModes.War })
                Assert.AreEqual("1 to 4 on this screen · teams · bots fill", Line(id), id);
            foreach (string id in new[] { HouseModes.MovingOut, HouseModes.MovingDay, HouseModes.Escape })
                Assert.AreEqual("1 to 4 on this screen · co-op", Line(id), id);
            Assert.AreEqual("1 player", Line(HouseModes.Tutorial));
        }

        [Test]
        public void CoursesSayWhereElseTheyPlay()
        {
            Assert.AreEqual("1 player here · or 4 together online", Line("TimeRun"));
            Assert.AreEqual("1 player here · or up to 8 online", Line("Freestyle"));
            Assert.AreEqual("Online · 2v2 or 4v4", Line("TeamRace"));
            Assert.AreEqual("Online · 2v2 or 4v4", Line("BrawlRace"));
            Assert.AreEqual("Online · up to 8", Line("FreeForAll"));
        }

        [Test]
        public void EveryModeOnTheBoardHasALineThatNeverSaysOfflineOrLocal()
        {
            Assert.AreEqual(12, Board.Length);
            foreach (string id in Board)
            {
                string line = Line(id);
                Assert.IsNotEmpty(line, id);
                Assert.IsFalse(line.ToLowerInvariant().Contains("offline") || line.ToLowerInvariant().Contains("local"), id + ": " + line);
                Assert.IsTrue(ModeAccess.For(id).SameScreen, id);
            }
        }

        [Test]
        public void SeatsBotsTeamsAndCoopComeFromTheHouseModeTable()
        {
            foreach (var mode in HouseModes.All)
            {
                var access = ModeAccess.For(mode.Id);
                Assert.IsTrue(access.SameScreen, mode.Id);
                Assert.IsFalse(access.PlaysOnline, mode.Id + " has no online playlist");
                Assert.IsNull(access.OnlineCard, mode.Id);
                Assert.AreEqual(mode.BotsPlay, access.Bots, mode.Id);
                Assert.AreEqual(mode.BotsPlay, access.Line.Contains("bots"), mode.Id + " claims bots only when bots play");
                Assert.AreEqual(mode.Teams, access.Teams, mode.Id);
                Assert.AreEqual(mode.Coop, access.Coop, mode.Id);
                Assert.AreEqual(mode.MinSeats, access.MinSeats, mode.Id);
                Assert.AreEqual(System.Math.Min(mode.MaxSeats, ModeAccess.ScreenSeats), access.MaxSeats, mode.Id);
                Assert.LessOrEqual(access.MaxSeats, ModeAccess.ScreenSeats, mode.Id + " never promises more seats than one screen has");
            }
            Assert.AreEqual(8, HouseModes.Find(HouseModes.War).MaxSeats);
            Assert.AreEqual(4, ModeAccess.For(HouseModes.War).MaxSeats);
        }

        [Test]
        public void OnlineFactsComeFromThePlaylistsAndRooms()
        {
            foreach (var card in QuickPlayCards.All)
            {
                var access = ModeAccess.For(card.Mode);
                Assert.IsTrue(access.PlaysOnline, card.Id);
                Assert.AreEqual(card.Id, access.OnlineCard, card.Mode);
                Assert.AreEqual(card.Playlists.Max(list => list.Players), access.OnlinePlayers, card.Id);
                Assert.AreEqual(card.Sized, access.TeamSizes.Count > 0, card.Id);
                Assert.AreEqual(FriendRooms.All.Any(room => room.Mode == card.Mode), access.PrivateRoom, card.Id);
                StringAssert.Contains("nline", access.Line, card.Id);
            }
            CollectionAssert.AreEqual(new[] { 2, 4 }, ModeAccess.For("TeamRace").TeamSizes);
            Assert.AreEqual(4, ModeAccess.For("TimeRun").OnlineNeeded);
            Assert.AreEqual(1, ModeAccess.For("Freestyle").OnlineNeeded);
            Assert.AreEqual(2, ModeAccess.For("FreeForAll").OnlineNeeded);
            Assert.IsFalse(ModeAccess.For("FreeForAll").Fill);
            Assert.IsTrue(ModeAccess.For("TeamRace").Fill);
            Assert.IsFalse(ModeAccess.For("TimeRun").PrivateRoom);
        }

        [Test]
        public void OnlyTheTwoSoloCoursesPlayBothHereAndOnline()
        {
            var both = QuickPlaylists.Modes.Concat(HouseModes.All.Select(mode => mode.Id))
                .Where(id => ModeAccess.For(id).SameScreen && ModeAccess.For(id).PlaysOnline).ToArray();
            CollectionAssert.AreEquivalent(new[] { "TimeRun", "Freestyle" }, both);
            Assert.AreEqual("coop", ModeAccess.For("TimeRun").OnlineCard);
            Assert.AreEqual("freeplay", ModeAccess.For("Freestyle").OnlineCard);
            Assert.IsTrue(ModeAccess.SoloCourse("TimeRun") && ModeAccess.SoloCourse("Freestyle"));
            Assert.IsFalse(ModeAccess.SoloCourse("TeamRace") || ModeAccess.SoloCourse(HouseModes.Dibs) || ModeAccess.SoloCourse(null));
            Assert.AreEqual(1, ModeAccess.For("TimeRun").MaxSeats);
        }

        [Test]
        public void AnUnknownModeSaysNothing()
        {
            foreach (string id in new[] { null, "", "NoSuchMode", "Workshop" })
            {
                var access = ModeAccess.For(id);
                Assert.AreEqual("", access.Line, id);
                Assert.IsFalse(access.SameScreen || access.PlaysOnline, id);
                Assert.IsNull(access.OnlineCard, id);
            }
        }
    }
}
