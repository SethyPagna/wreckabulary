using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Voice;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class VoiceRoomTests
    {
        static WordboundSeat[] TwoTeams(int perTeam) => Enumerable.Range(0, 2 * perTeam)
            .Select(i => new WordboundSeat("p" + i, i / perTeam, i % perTeam)).ToArray();

        [Test]
        public void RelayTeamsHearOnlyTheirOwnTeam()
        {
            foreach (string id in new[] { "TeamRace", "BrawlRace" })
                foreach (var format in new[] { RaceFormat.Duo, RaceFormat.Squad })
                {
                    var mode = ModeDefinition.For(id, format);
                    int size = (int)format;
                    var seats = TwoTeams(size);
                    CollectionAssert.IsEmpty(mode.ValidateRoster(seats), id + " " + format);
                    var room = VoiceRoom.Match(mode, seats);
                    Assert.AreEqual(VoiceScope.Team, room.Scope, id + " " + format);
                    Assert.IsTrue(room.CanHear("p0", "p1"), "teammates hear each other");
                    Assert.IsFalse(room.CanHear("p0", "p" + size), "the other team stays private");
                    Assert.IsFalse(room.CanHear("p" + size, "p0"));
                    CollectionAssert.AreEqual(Enumerable.Range(1, size - 1).Select(i => "p" + i), room.HeardBy("p0").Select(m => m.PlayerId));
                }
        }

        [Test]
        public void FreeForAllAndSingleTeamModesLetEveryoneHear()
        {
            var ffa = ModeDefinition.For("FreeForAll");
            var seats = Enumerable.Range(0, 4).Select(i => new WordboundSeat("p" + i, i, 0)).ToArray();
            CollectionAssert.IsEmpty(ffa.ValidateRoster(seats));
            var room = VoiceRoom.Match(ffa, seats);
            Assert.AreEqual(VoiceScope.Everyone, room.Scope);
            Assert.IsTrue(room.CanHear("p0", "p3"));
            Assert.AreEqual(3, room.HeardBy("p2").Count);
            foreach (string id in new[] { "TimeRun", "Freestyle" })
            {
                var mode = ModeDefinition.For(id);
                Assert.AreEqual(1, mode.TeamCount, id);
                var together = VoiceRoom.Match(mode, Enumerable.Range(0, 3).Select(i => new WordboundSeat("p" + i, 0, i)));
                Assert.AreEqual(VoiceScope.Everyone, together.Scope, id);
                Assert.IsTrue(together.CanHear("p0", "p2"), id);
            }
        }

        [Test]
        public void EveryOnlineModeHasADefinedScope()
        {
            Assert.AreEqual(VoiceScope.Everyone, VoiceRoom.ScopeFor(ModeDefinition.For("TimeRun")));
            Assert.AreEqual(VoiceScope.Everyone, VoiceRoom.ScopeFor(ModeDefinition.For("Freestyle")));
            Assert.AreEqual(VoiceScope.Everyone, VoiceRoom.ScopeFor(ModeDefinition.For("FreeForAll")));
            Assert.AreEqual(VoiceScope.Everyone, VoiceRoom.ScopeFor(ModeDefinition.For("TeamRace", RaceFormat.Solo)), "one-player teams are a free-for-all");
            Assert.AreEqual(VoiceScope.Everyone, VoiceRoom.ScopeFor(ModeDefinition.For("BrawlRace", RaceFormat.Solo)));
            Assert.AreEqual(VoiceScope.Team, VoiceRoom.ScopeFor(ModeDefinition.For("TeamRace", RaceFormat.Duo)));
            Assert.AreEqual(VoiceScope.Team, VoiceRoom.ScopeFor(ModeDefinition.For("BrawlRace", RaceFormat.Squad)));
        }

        [Test]
        public void ThePartyLobbyIsEveryoneInThePartyInJoinOrder()
        {
            var room = VoiceRoom.Party(new[] { "c-3", "a-1", "b-2", "a-1", "not valid", null });
            Assert.AreEqual(VoiceScope.Everyone, room.Scope);
            CollectionAssert.AreEqual(new[] { "c-3", "a-1", "b-2" }, room.Members.Select(m => m.PlayerId), "duplicates and bad ids drop out");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, room.Members.Select(m => m.Slot));
            Assert.IsTrue(room.CanHear("b-2", "c-3"));
            Assert.AreEqual(1, room.Find("a-1").Slot);
            Assert.IsNull(room.Find("zzz"));
        }

        [Test]
        public void NobodyHearsThemselvesOrPeopleOutsideTheRoom()
        {
            var room = VoiceRoom.Party(new[] { "me", "you" });
            Assert.IsFalse(room.CanHear("me", "me"));
            Assert.IsFalse(room.CanHear("me", "ghost"));
            Assert.IsFalse(room.CanHear("ghost", "me"));
            Assert.IsFalse(room.CanHear(null, "me"));
            Assert.AreEqual(VoiceScope.Nobody, VoiceRoom.Empty.Scope);
            Assert.IsFalse(VoiceRoom.Empty.CanHear("me", "you"));
            Assert.AreEqual(VoiceScope.Nobody, VoiceRoom.Party(null).Scope);
            Assert.AreEqual(VoiceScope.Nobody, VoiceRoom.Match(ModeDefinition.For("FreeForAll"), null).Scope);
        }

        [Test]
        public void HouseTeamModesKeepVoiceInTheTeam()
        {
            var members = new[] { new VoiceMember("p1", 0, 0), new VoiceMember("p2", 0, 1), new VoiceMember("p3", 1, 0), new VoiceMember("p4", 1, 1) };
            foreach (string id in new[] { HouseModes.Duos, HouseModes.War })
            {
                var room = VoiceRoom.House(HouseModes.Find(id), members);
                Assert.AreEqual(VoiceScope.Team, room.Scope, id);
                Assert.IsTrue(room.CanHear("p1", "p2"), id);
                Assert.IsFalse(room.CanHear("p1", "p3"), id);
            }
            var dibs = VoiceRoom.House(HouseModes.Find(HouseModes.Dibs), members);
            Assert.AreEqual(VoiceScope.Everyone, dibs.Scope);
            Assert.IsTrue(dibs.CanHear("p1", "p4"));
        }

        [Test]
        public void HeardByListsTeammatesByTeamThenSlot()
        {
            var seats = new[] { new WordboundSeat("z", 1, 1), new WordboundSeat("y", 0, 1), new WordboundSeat("x", 1, 0), new WordboundSeat("w", 0, 0) };
            var room = VoiceRoom.Match(ModeDefinition.For("TeamRace", RaceFormat.Duo), seats);
            CollectionAssert.AreEqual(new[] { "w", "y", "x", "z" }, room.Members.Select(m => m.PlayerId));
            CollectionAssert.AreEqual(new[] { "z" }, room.HeardBy("x").Select(m => m.PlayerId));
        }
    }
}
