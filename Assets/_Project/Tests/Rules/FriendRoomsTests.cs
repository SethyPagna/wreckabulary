using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public class FriendRoomsTests
    {
        const string Invite = "wb-room:0123456789abcdef:TeamRace:Duo:asia:p1:0000000000000002:m1";

        [Test]
        public void TheRoomModesAreTheFourThatSoloFriendsCanStart()
        {
            CollectionAssert.AreEqual(new[] { "relay", "brawl", "ffa", "freeplay" }, FriendRooms.All.Select(room => room.Id));
            CollectionAssert.AreEqual(new[] { "TeamRace", "BrawlRace", "FreeForAll", "Freestyle" }, FriendRooms.All.Select(room => room.Mode));
            Assert.IsFalse(FriendRooms.All.Any(room => room.Mode == "TimeRun"), "Solo friends can share a Time Trial team only as a full four.");
            foreach (var room in FriendRooms.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(room.Label), room.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(room.Detail), room.Id);
                Assert.AreSame(room, FriendRooms.Find(room.Id));
            }
            Assert.IsTrue(FriendRooms.Find("relay").Teams); Assert.IsFalse(FriendRooms.Find("ffa").Teams);
        }

        [Test]
        public void SeatsAndStartRulesMatchTheServiceSettings()
        {
            foreach (string mode in new[] { "TimeRun", "TeamRace", "BrawlRace", "FreeForAll", "Freestyle" })
                foreach (string format in FriendRooms.TeamSizes)
                {
                    if (!FriendRooms.IsTeamMode(mode) && format != "Solo") continue;
                    var settings = new OnlineSettings(mode, format, FriendRooms.Fill(mode), "asia", "p1", "m1");
                    Assert.AreEqual(settings.TeamCount, FriendRooms.Teams(mode), mode + format);
                    int seats = settings.Mode == "FreeForAll" || settings.Mode == "Freestyle" ? FriendRooms.MostPlayers : settings.TeamCapacity * settings.TeamCount;
                    Assert.AreEqual(seats, FriendRooms.Capacity(mode, format), mode + format);
                }
            Assert.AreEqual(4, FriendRooms.Needed("TeamRace", "Duo")); Assert.AreEqual(8, FriendRooms.Needed("BrawlRace", "Squad"));
            Assert.AreEqual(2, FriendRooms.Needed("TeamRace", "Solo")); Assert.AreEqual(2, FriendRooms.Needed("FreeForAll", "Solo"));
            Assert.AreEqual(1, FriendRooms.Needed("Freestyle", "Solo"));
            Assert.IsFalse(FriendRooms.CanStart("TeamRace", "Duo", 3)); Assert.IsTrue(FriendRooms.CanStart("TeamRace", "Duo", 4));
            Assert.IsFalse(FriendRooms.CanStart("FreeForAll", "Solo", 1)); Assert.IsTrue(FriendRooms.CanStart("FreeForAll", "Solo", 8));
            Assert.IsFalse(FriendRooms.CanStart("FreeForAll", "Solo", 9)); Assert.IsTrue(FriendRooms.CanStart("Freestyle", "Solo", 1));
            Assert.IsTrue(FriendRooms.Fill("TeamRace")); Assert.IsTrue(FriendRooms.Fill("BrawlRace"));
            Assert.IsFalse(FriendRooms.Fill("FreeForAll"), "Free-for-All never fills."); Assert.IsFalse(FriendRooms.Fill("Freestyle"));
        }

        [Test]
        public void TheWordsSayWhoIsNeeded()
        {
            Assert.AreEqual("Team Race 2v2", FriendRooms.Title("TeamRace", "Duo"));
            Assert.AreEqual("Brawl Race 1v1", FriendRooms.Title("BrawlRace", "Solo"));
            Assert.AreEqual("Free-for-All", FriendRooms.Title("FreeForAll", "Solo"));
            Assert.AreEqual("Needs 4 players", FriendRooms.Players("TeamRace", "Duo"));
            Assert.AreEqual("2 to 8 players", FriendRooms.Players("FreeForAll", "Solo"));
            Assert.AreEqual("1 to 8 players", FriendRooms.Players("Freestyle", "Solo"));
            Assert.AreEqual("3 / 4 players · needs 4 to start", FriendRooms.Waiting("TeamRace", "Duo", 3));
            Assert.AreEqual("2 / 8 players · ready to start", FriendRooms.Waiting("FreeForAll", "Solo", 2));
            Assert.AreEqual("0123 4567 89AB CDEF", FriendRooms.ShortCode("0123456789abcdef"));
        }

        [Test]
        public void OnlyACompleteRoomInviteLooksLikeACode()
        {
            Assert.IsTrue(FriendRooms.LooksLikeInvite(Invite));
            Assert.IsTrue(FriendRooms.LooksLikeInvite("  " + Invite + "\n"));
            Assert.IsFalse(FriendRooms.LooksLikeInvite(null)); Assert.IsFalse(FriendRooms.LooksLikeInvite(""));
            Assert.IsFalse(FriendRooms.LooksLikeInvite("0123456789abcdef"), "The short code alone is not enough to find the room.");
            Assert.IsFalse(FriendRooms.LooksLikeInvite(Invite.Replace("wb-room", "wb-party")));
            Assert.IsFalse(FriendRooms.LooksLikeInvite(Invite.Replace("0123456789abcdef", "0123456789ABCDEF")));
            Assert.IsFalse(FriendRooms.LooksLikeInvite(Invite + ":extra"));
            Assert.IsFalse(FriendRooms.LooksLikeInvite(Invite.Replace(":asia:", "::")));
            Assert.IsFalse(FriendRooms.LooksLikeInvite(Invite + new string('a', FriendRooms.InviteLength)));
        }

        [Test]
        public void OnlyACompletePartyInviteLooksLikeAPartyCode()
        {
            const string party = "wb-party:20000000-0000-4000-8000-00000000000a:1234567890abcdef:0123456789abcdef";
            Assert.IsTrue(FriendRooms.LooksLikePartyInvite(party));
            Assert.IsTrue(FriendRooms.LooksLikePartyInvite("  " + party + "\n"));
            Assert.LessOrEqual(party.Length, FriendRooms.InviteLength, "The shared code field holds a whole party code.");
            Assert.IsFalse(FriendRooms.LooksLikePartyInvite(null)); Assert.IsFalse(FriendRooms.LooksLikePartyInvite(""));
            Assert.IsFalse(FriendRooms.LooksLikePartyInvite(Invite), "A room code is not a party code.");
            Assert.IsFalse(FriendRooms.LooksLikeInvite(party), "A party code is not a room code.");
            Assert.IsFalse(FriendRooms.LooksLikePartyInvite(party.Replace("wb-party", "wb-room")));
            Assert.IsFalse(FriendRooms.LooksLikePartyInvite(party.Replace("1234567890abcdef", "1234567890ABCDEF")));
            Assert.IsFalse(FriendRooms.LooksLikePartyInvite(party.Replace("-00000000000a", "-00000000000A")));
            Assert.IsFalse(FriendRooms.LooksLikePartyInvite(party.Replace("20000000-0000-4000-8000-", "20000000000040008000")));
            Assert.IsFalse(FriendRooms.LooksLikePartyInvite(party + ":extra"));
            Assert.IsFalse(FriendRooms.LooksLikePartyInvite(party.Substring(0, party.Length - 1)));
            Assert.AreEqual("wb-party", FriendRooms.PartyPrefix);
        }

        [Test]
        public void TheHostChoiceRemembersModeTeamSizeAndLayout()
        {
            var fresh = new FriendRoomChoice();
            Assert.AreEqual("relay", fresh.Mode); Assert.AreEqual("Duo", fresh.Format); Assert.AreEqual(0, fresh.Layout);
            Assert.AreEqual("Team Race 2v2 · The Lost Address", fresh.Summary());
            Assert.IsTrue(fresh.Choose("brawl")); Assert.IsTrue(fresh.ChooseTeamSize("Squad")); Assert.IsTrue(fresh.ChooseLayout(3));
            Assert.IsFalse(fresh.Choose("coop")); Assert.IsFalse(fresh.ChooseTeamSize("Trio")); Assert.IsFalse(fresh.ChooseLayout(FriendRooms.Layouts));
            var loaded = new FriendRoomChoice(fresh.Save());
            Assert.AreEqual("brawl", loaded.Mode); Assert.AreEqual("Squad", loaded.Format); Assert.AreEqual(3, loaded.Layout);
            Assert.AreEqual("Brawl Race 4v4 · The Lost Address 4", loaded.Summary());
            Assert.IsTrue(loaded.Choose("ffa"));
            Assert.AreEqual("Solo", loaded.Format, "Team size only applies to team modes.");
            Assert.AreEqual("Squad", loaded.TeamSize, "The team size is kept for the next team mode.");
            Assert.AreEqual("Free-for-All · Toybox Arena 4", loaded.Summary());
            Assert.AreEqual("relay", new FriendRoomChoice("v9;mode=ffa").Mode);
            Assert.AreEqual("relay", new FriendRoomChoice("v1;mode=nope;team=x;layout=99;junk").Mode);
        }

        [Test]
        public void MapNamesAreTheCourseTitles()
        {
            var items = TestData.Catalogue();
            var documents = new[] {
                WordboundFlagshipContent.Create(items).CopyDocument(), WordboundParcelServiceContent.Create(items).CopyDocument(),
                CourseTemplates.CombatArena(), CourseTemplates.RaceCircuit(), CourseTemplates.FreestyleWorld()
            };
            foreach (var document in documents) Assert.AreEqual(document.Title, OnlineMaps.Name(document.Id), document.Id);
            Assert.AreEqual("The Lost Address", OnlineMaps.Name(WordboundFlagshipContent.CourseId));
            Assert.IsNull(OnlineMaps.Name("someone-elses-course")); Assert.IsNull(OnlineMaps.Name(null));
            CollectionAssert.AreEqual(new[] { "The Lost Address", "The Lost Address 2", "The Lost Address 3", "The Lost Address 4" },
                Enumerable.Range(0, FriendRooms.Layouts).Select(slot => OnlineMaps.For("TeamRace", slot)));
            CollectionAssert.AreEqual(new[] { "Toybox Arena", "Toybox Arena 2", "Toybox Arena 3", "Toybox Arena 4" },
                Enumerable.Range(0, FriendRooms.Layouts).Select(slot => OnlineMaps.For("FreeForAll", slot)));
            Assert.AreEqual(OnlineMaps.PrivateMap, OnlineMaps.Slot("someone-elses-course", 0));
            Assert.AreEqual(OnlineMaps.PrivateMap, OnlineMaps.Slot(WordboundFlagshipContent.CourseId, -1));
        }

        [Test]
        public void EveryRoomModeHasFourSlotsOfItsCourseInThePublicCatalogue()
        {
            const string path = "Assets/_Project/Resources/Wordbound/public-catalogue.json";
            var rows = Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, path)), path)["courses"].Items;
            var pairs = new System.Collections.Generic.List<(string mode, string format)>();
            foreach (var room in FriendRooms.All)
                foreach (string size in room.Teams ? FriendRooms.TeamSizes.ToArray() : new[] { "Solo" }) pairs.Add((room.Mode, size));
            foreach (var list in QuickPlaylists.All) pairs.Add((list.Mode, list.Format));
            foreach (var (mode, format) in pairs.Distinct())
            {
                var slots = rows.Where(row => row["mode"].String() == mode && row["format"].String() == format)
                    .OrderBy(row => row["seed"].String(), StringComparer.Ordinal).ToArray();
                Assert.AreEqual(FriendRooms.Layouts, slots.Length, mode + " " + format);
                foreach (var slot in slots)
                    Assert.AreEqual(OnlineMaps.CourseFor(mode), slot["content"]["courseId"].String(), mode + " " + format + " " + slot["seed"].String());
            }
        }
    }
}
