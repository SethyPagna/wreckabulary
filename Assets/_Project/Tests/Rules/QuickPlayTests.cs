using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public class QuickPlayTests
    {
        static readonly string[] Seeds = { "0000000000000001", "0000000000000002", "0000000000000003", "0123456789abcdef" };
        static readonly (string Mode, string Format)[] Variants = {
            ("TimeRun", "Solo"), ("TeamRace", "Solo"), ("TeamRace", "Duo"), ("TeamRace", "Squad"),
            ("BrawlRace", "Solo"), ("BrawlRace", "Duo"), ("BrawlRace", "Squad"), ("FreeForAll", "Solo"), ("Freestyle", "Solo")
        };

        static bool Everything(string mode, string format, string seed) =>
            Seeds.Contains(seed) && Variants.Contains((mode, format));

        static QuickPlayChoices Choices(string saved = null) => new QuickPlayChoices(saved);

        static QuickPlayChoices Only(params string[] ids) => Choices("v2;lists=" + string.Join("+", ids));

        static QuickPlayPick Pick(QuickPlayChoices choices, int party, long slot, string region = "asia",
            QuickPlayPick keep = null, string partySeed = null) =>
            QuickPlayPicker.Pick(choices, Seeds, Everything, party, slot, region, keep, partySeed);

        [Test]
        public void FirstLaunchTicksEveryPlaylistButFreeplay()
        {
            var choices = Choices();
            CollectionAssert.AreEqual(QuickPlaylists.StartTicked, choices.Playlists.Ticked);
            Assert.IsFalse(choices.Playlists.IsTicked("freeplay"), "A first Find match should land in a real match.");
            Assert.AreEqual("Player", choices.Name);
        }

        [Test]
        public void RememberedPlaylistsAndNameSurviveASaveAndLoad()
        {
            var choices = Choices();
            choices.Playlists.Toggle("ffa"); choices.Playlists.Toggle("freeplay"); choices.ChooseName("  Ana; =+%  ");
            string saved = choices.Save();
            StringAssert.StartsWith("v2;lists=", saved);
            var loaded = Choices(saved);
            Assert.AreEqual(new[] { "relay-2v2", "relay-4v4", "brawl-2v2", "brawl-4v4", "coop", "freeplay" }, loaded.Playlists.Ticked);
            Assert.AreEqual("Ana; =+%", loaded.Name);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("v9;lists=ffa")]
        [TestCase("garbage")]
        [TestCase("v2;lists=nope+mars;name=")]
        [TestCase("v2;lists;=x;name")]
        [TestCase("v1;modes=Freestyle;maps=ffff;region=eu;team=Duo")]
        public void BrokenOrForeignSavesFallBackToDefaults(string saved)
        {
            var choices = Choices(saved);
            CollectionAssert.AreEqual(QuickPlaylists.StartTicked, choices.Playlists.Ticked);
            Assert.AreEqual("Player", choices.Name);
        }

        [Test]
        public void AnOlderSaveKeepsOnlyTheName()
        {
            var choices = Choices("v1;modes=Freestyle;region=eu;team=Duo;name=Ana");
            Assert.AreEqual("Ana", choices.Name);
            CollectionAssert.AreEqual(QuickPlaylists.StartTicked, choices.Playlists.Ticked);
            StringAssert.DoesNotContain("region", choices.Save());
        }

        [Test]
        public void NamesAreTrimmedCappedAndNeverEmpty()
        {
            var choices = Choices();
            choices.ChooseName("   ");
            Assert.AreEqual("Player", choices.Name);
            choices.ChooseName(new string('x', 40));
            Assert.AreEqual(QuickPlayChoices.MaximumName, choices.Name.Length);
        }

        [TestCase(420, "asia")]
        [TestCase(330, "asia")]
        [TestCase(600, "asia")]
        [TestCase(240, "asia")]
        [TestCase(180, "eu")]
        [TestCase(0, "eu")]
        [TestCase(-60, "eu")]
        [TestCase(-120, "eu")]
        [TestCase(-150, "us")]
        [TestCase(-180, "us")]
        [TestCase(-300, "us")]
        [TestCase(-600, "us")]
        public void TheRegionIsAlwaysPickedFromTheLocalTimeZone(int minutes, string region) =>
            Assert.AreEqual(region, QuickPlayRegions.Nearest(minutes));

        [Test]
        public void TheSummaryNamesTheTickedPlaylistsBriefly()
        {
            Assert.AreEqual("wb.online.quickplay", QuickPlayChoices.StorageKey, "The lobby and the online menu share one saved choice.");
            Assert.AreEqual("Team Race 2v2, Team Race 4v4 + 4 more", Choices().Summary());
            var all = Choices(); all.Playlists.TickAll();
            Assert.AreEqual("All playlists", all.Summary());
            Assert.AreEqual("Brawl Race 4v4", Only("brawl-4v4").Summary());
            Assert.AreEqual("Team Race 2v2, Free-for-All · 8, Time Trial · 4", Only("relay-2v2", "ffa", "coop").Summary());
            Assert.AreEqual("Team Race 2v2, Team Race 4v4 + 2 more", Only("relay-2v2", "relay-4v4", "ffa", "coop").Summary());
        }

        [Test]
        public void ThePartyLineSaysWhatFindMatchSearchesAndWhatItSkips()
        {
            Assert.AreEqual("Searches Team Race 2v2, Team Race 4v4 + 4 more", Choices().PartyLine(1));
            Assert.AreEqual("Searches Team Race 2v2, Team Race 4v4 + 3 more · skips Free-for-All · 8 for a party of 2", Choices().PartyLine(2));
            Assert.AreEqual("Searches Team Race 4v4, Brawl Race 4v4, Time Trial · 4 · skips Team Race 2v2 + 2 more for a party of 3",
                Choices().PartyLine(3));
            Assert.AreEqual("Searches Free Roam · 8", Only("freeplay").PartyLine(4));
            Assert.AreEqual(QuickPlayPicker.Blocked(Only("ffa"), 2, false), Only("ffa").PartyLine(2), "Nothing fits, so it says why.");
            Assert.AreEqual(QuickPlayPicker.Blocked(Choices(), 5, false), Choices().PartyLine(5));
            StringAssert.StartsWith("A party has room for 4", Choices().PartyLine(5));
        }

        [Test]
        public void PlaylistsSitUnderClassicThenArcadeWithPlainNames()
        {
            CollectionAssert.AreEqual(new[] { "Classic", "Arcade" }, QuickPlaylists.Groups);
            CollectionAssert.AreEqual(new[] { "relay-2v2", "relay-4v4", "brawl-2v2", "brawl-4v4", "ffa" },
                QuickPlaylists.All.Where(list => list.Group == QuickPlaylists.Classic).Select(list => list.Id));
            CollectionAssert.AreEqual(new[] { "coop", "freeplay" },
                QuickPlaylists.All.Where(list => list.Group == QuickPlaylists.Arcade).Select(list => list.Id));
            CollectionAssert.AreEqual(QuickPlaylists.Ids, QuickPlaylists.Groups.SelectMany(group =>
                QuickPlaylists.All.Where(list => list.Group == group).Select(list => list.Id)), "Grouping keeps the card order.");
            CollectionAssert.AreEqual(new[] { "Team Race 2v2", "Team Race 4v4", "Brawl Race 2v2", "Brawl Race 4v4",
                "Free-for-All · 8", "Time Trial · 4", "Free Roam · 8" }, QuickPlaylists.All.Select(list => list.Label));
            foreach (var list in QuickPlaylists.All)
            {
                StringAssert.DoesNotContain("Relay", list.Label, "Nobody hands off in a race, so the name says race.");
                StringAssert.DoesNotContain("Freeplay", list.Label);
                Assert.LessOrEqual(list.Detail.Length, 40, list.Id + " keeps its card line short.");
            }
        }

        [Test]
        public void EveryPlaylistMatchesTheOnlineContractAndTheModeRules()
        {
            Assert.AreEqual(QuickPlaylists.All.Count, QuickPlaylists.Ids.Distinct().Count());
            foreach (var list in QuickPlaylists.All)
            {
                var settings = new OnlineSettings(list.Mode, list.Format, false, "asia", "p1", "v1");
                Assert.AreEqual(settings.TeamCapacity, list.TeamSize, list.Id);
                Assert.AreEqual(settings.TeamCount, list.Teams, list.Id);
                var rules = ModeDefinition.For(list.Mode, (RaceFormat)Enum.Parse(typeof(RaceFormat), list.Format));
                Assert.AreEqual(rules.MaximumPlayers, list.Players, list.Id);
                Assert.AreEqual(rules.TeamCount, list.Teams, list.Id);
                Assert.AreEqual(rules.TeamCapacity, list.TeamSize, list.Id);
                Assert.LessOrEqual(list.Players, 8, "Today's matches hold 8 seats.");
                Assert.IsTrue(Variants.Contains((list.Mode, list.Format)), list.Id + " has public courses.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(list.Detail), list.Id + " needs a card line.");
                Assert.AreEqual(list.Mode != "FreeForAll", list.Fill);
                Assert.AreSame(list, QuickPlaylists.For(list.Mode, list.Format));
            }
        }

        [Test]
        public void TeamSizesAreFixedByThePlaylist()
        {
            CollectionAssert.AreEquivalent(new[] { "Duo", "Squad" },
                QuickPlaylists.All.Where(list => QuickPlaylists.IsTeamMode(list.Mode)).Select(list => list.Format).Distinct());
            Assert.IsNull(QuickPlaylists.For("TeamRace", "Solo"), "1v1 relays stay in the private room and custom match page.");
            var choices = Choices(); choices.Playlists.TickAll();
            for (int party = 1; party <= 4; party++)
                for (long slot = 0; slot < 14; slot++)
                {
                    var pick = Pick(choices, party, slot);
                    Assert.IsNotNull(pick.Playlist, party + "/" + slot);
                    Assert.IsTrue(pick.Playlist.Fits(party), pick.Playlist.Id + " for " + party);
                }
        }

        [TestCase("ffa", 1, true)]
        [TestCase("ffa", 2, false)]
        [TestCase("relay-2v2", 2, true)]
        [TestCase("relay-2v2", 3, false)]
        [TestCase("brawl-4v4", 4, true)]
        [TestCase("relay-4v4", 5, false)]
        [TestCase("coop", 4, true)]
        [TestCase("freeplay", 4, true)]
        [TestCase("freeplay", 5, false)]
        [TestCase("coop", 0, false)]
        public void APartyMustFitOnOneTeam(string id, int party, bool fits) =>
            Assert.AreEqual(fits, QuickPlaylists.Find(id).Fits(party));

        [Test]
        public void WithEverythingTickedTheRotationFeaturesOnePlaylistAndMapPerSlot()
        {
            var choices = Choices(); choices.Playlists.TickAll();
            for (long slot = 0; slot < 28; slot++)
            {
                var pick = Pick(choices, 1, slot);
                var featured = QuickPlaylists.All[(int)(slot % QuickPlaylists.All.Count)];
                Assert.AreEqual(featured.Mode, pick.Mode, "slot " + slot); Assert.AreEqual(featured.Format, pick.Format, "slot " + slot);
                Assert.AreEqual(Seeds[slot % 4], pick.Seed, "slot " + slot);
                Assert.AreEqual("asia", pick.Region); Assert.AreEqual(pick.Mode != "FreeForAll", pick.Fill);
            }
        }

        [Test]
        public void TheDefaultsNeverLandInFreeplay()
        {
            var choices = Choices();
            for (long slot = 0; slot < 28; slot++) Assert.AreNotEqual("Freestyle", Pick(choices, 1, slot).Mode, "slot " + slot);
        }

        [Test]
        public void PlayersWhoTickTheFeaturedPlaylistLandInTheSameQueue()
        {
            var everything = Choices();
            var fours = Only("relay-4v4", "brawl-4v4");
            const long featuredRelayFours = 8;
            var a = Pick(everything, 1, featuredRelayFours, "eu");
            var b = Pick(fours, 1, featuredRelayFours, "eu");
            Assert.AreEqual("relay-4v4", a.Playlist.Id);
            Assert.IsTrue(a.Same(b), "Overlapping choices meet on the featured playlist and map.");
        }

        [Test]
        public void EveryPickStaysInsideTheTickedPlaylistsAndEveryMapComesRound()
        {
            var choices = Only("brawl-2v2", "coop");
            var seen = new HashSet<string>();
            for (long slot = -40; slot < 40; slot++)
            {
                var pick = Pick(choices, 1, slot, "us");
                Assert.IsTrue(choices.Playlists.IsTicked(pick.Playlist.Id), pick.Playlist.Id);
                seen.Add(pick.Playlist.Id + "/" + pick.Seed);
            }
            Assert.AreEqual(8, seen.Count, "Both playlists on all four approved maps.");
        }

        [Test]
        public void APartyStaysOneTeamAndNeverEntersFreeForAll()
        {
            var choices = Choices();
            for (long slot = 0; slot < 14; slot++)
            {
                Assert.AreNotEqual("FreeForAll", Pick(choices, 2, slot).Mode);
                var three = Pick(choices, 3, slot);
                CollectionAssert.Contains(new[] { "relay-4v4", "brawl-4v4", "coop" }, three.Playlist.Id);
            }
            Assert.IsNull(Pick(Only("ffa"), 2, 0));
            Assert.IsNull(Pick(Only("relay-2v2", "brawl-2v2"), 3, 0), "A party is never split across teams.");
        }

        [Test]
        public void AFixedPartyMapIsKeptAndPlaylistsWithoutItAreSkipped()
        {
            string partySeed = Seeds[2];
            bool noTeamRace(string mode, string format, string seed) => Everything(mode, format, seed) && mode != "TeamRace";
            var pick = QuickPlayPicker.Pick(Choices(), Seeds, noTeamRace, 2, 0, "asia", partySeed: partySeed);
            Assert.AreEqual(partySeed, pick.Seed, "A party with members keeps the map its invite carries.");
            Assert.AreEqual("brawl-2v2", pick.Playlist.Id);
        }

        [Test]
        public void ACurrentPartyPlaylistIsKeptWhileItStaysTicked()
        {
            var choices = Choices();
            var keep = new QuickPlayPick("TeamRace", "Squad", Seeds[1], "eu");
            var kept = Pick(choices, 2, 0, "asia", keep, Seeds[1]);
            Assert.AreEqual("relay-4v4", kept.Playlist.Id); Assert.AreEqual("asia", kept.Region, "The region is always the automatic one.");
            choices.Playlists.Toggle("relay-4v4");
            var moved = Pick(choices, 2, 0, "asia", keep, Seeds[1]);
            Assert.AreEqual("relay-2v2", moved.Playlist.Id); Assert.AreEqual(Seeds[1], moved.Seed);
            var custom = new QuickPlayPick("TeamRace", "Solo", Seeds[1], "asia");
            Assert.IsNotNull(Pick(choices, 1, 0, "asia", custom, Seeds[1]).Playlist, "A custom match setting is not a playlist and is replaced.");
        }

        [Test]
        public void NothingOfferedMeansNoPickAndAPlainReason()
        {
            var choices = Choices();
            Assert.IsNull(QuickPlayPicker.Pick(choices, Seeds, (m, f, s) => false, 1, 0, "asia"));
            Assert.IsNull(QuickPlayPicker.Pick(choices, Array.Empty<string>(), Everything, 1, 0, "asia"));
            StringAssert.Contains("No public map is open", QuickPlayPicker.Blocked(choices, 1, false));
            StringAssert.Contains("Your party's map", QuickPlayPicker.Blocked(choices, 1, true));
            StringAssert.StartsWith("Free-for-All is solo, and a party always plays on one team", QuickPlayPicker.Blocked(Only("ffa"), 2, false));
            Assert.AreEqual("A party of 3 needs bigger teams. Tick Team Race 4v4, Brawl Race 4v4, Time Trial · 4 or Free Roam · 8.",
                QuickPlayPicker.Blocked(Only("relay-2v2", "brawl-2v2"), 3, false));
            Assert.AreEqual("A party has room for 4 players.", QuickPlayPicker.Blocked(choices, 5, false));
        }

        [Test]
        public void SlotsCountTenMinuteWindowsFromTheEpoch()
        {
            Assert.AreEqual(0, QuickPlayPicker.Slot(0));
            Assert.AreEqual(0, QuickPlayPicker.Slot(QuickPlayPicker.RotationMilliseconds - 1));
            Assert.AreEqual(1, QuickPlayPicker.Slot(QuickPlayPicker.RotationMilliseconds));
            Assert.AreEqual(-1, QuickPlayPicker.Slot(-1));
        }

        static QuickPlayState Guest() => new QuickPlayState();

        static QuickPlayState Leader(int members = 1) => new QuickPlayState {
            Authenticated = true, HasParty = true, IsLeader = true, Members = members, SeedMatches = true, SettingsMatch = true
        };

        [Test]
        public void OnePlayWalksFromGuestToSearching()
        {
            var goal = QuickPlayGoal.Search;
            Assert.AreEqual(QuickPlayStep.Connect, QuickPlayFlow.Next(Guest(), goal));
            Assert.AreEqual(QuickPlayStep.CreateParty, QuickPlayFlow.Next(new QuickPlayState { Authenticated = true }, goal));
            var party = Leader();
            Assert.AreEqual(QuickPlayStep.Ready, QuickPlayFlow.Next(party, goal));
            party.SelfReady = party.AllReady = true;
            Assert.AreEqual(QuickPlayStep.Queue, QuickPlayFlow.Next(party, goal));
            party.HasAttempt = party.QuickPlayAttempt = true;
            Assert.AreEqual(QuickPlayStep.Searching, QuickPlayFlow.Next(party, goal));
        }

        [Test]
        public void InviteStopsOnceThePartyHasRoomForAFriend()
        {
            Assert.AreEqual(QuickPlayStep.Connect, QuickPlayFlow.Next(Guest(), QuickPlayGoal.Party));
            Assert.AreEqual(QuickPlayStep.CreateParty, QuickPlayFlow.Next(new QuickPlayState { Authenticated = true }, QuickPlayGoal.Party));
            var roomy = Leader(); roomy.HasRoom = true; roomy.SeedMatches = false; roomy.SettingsMatch = false;
            Assert.AreEqual(QuickPlayStep.PartyReady, QuickPlayFlow.Next(roomy, QuickPlayGoal.Party), "A party with room is shared as it is.");
            var full = Leader(); full.SettingsMatch = false;
            Assert.AreEqual(QuickPlayStep.ApplySettings, QuickPlayFlow.Next(full, QuickPlayGoal.Party), "A full party first switches to a roomier playlist.");
            full.SettingsMatch = true;
            Assert.AreEqual(QuickPlayStep.Blocked, QuickPlayFlow.Next(full, QuickPlayGoal.Party));
            full.SettingsMatch = false; full.HasAttempt = true;
            Assert.AreEqual(QuickPlayStep.Blocked, QuickPlayFlow.Next(full, QuickPlayGoal.Party), "A searching party is not changed.");
            var member = Leader(4); member.IsLeader = false;
            Assert.AreEqual(QuickPlayStep.PartyReady, QuickPlayFlow.Next(member, QuickPlayGoal.Party), "Members can always copy the invite.");
        }

        [Test]
        public void ChangedChoicesAreAppliedBeforeReadyAndAMapChangeRemakesASoloParty()
        {
            var party = Leader(); party.SettingsMatch = false; party.SelfReady = party.AllReady = true;
            Assert.AreEqual(QuickPlayStep.ApplySettings, QuickPlayFlow.Next(party, QuickPlayGoal.Search));
            party.SeedMatches = false;
            Assert.AreEqual(QuickPlayStep.LeaveParty, QuickPlayFlow.Next(party, QuickPlayGoal.Search));
            party.Members = 2;
            Assert.AreEqual(QuickPlayStep.Blocked, QuickPlayFlow.Next(party, QuickPlayGoal.Search), "A shared party keeps its map.");
        }

        [Test]
        public void TheLeaderWaitsForEveryMemberAndMembersOnlyReadyUp()
        {
            var leader = Leader(2); leader.SelfReady = true;
            Assert.AreEqual(QuickPlayStep.WaitForMembers, QuickPlayFlow.Next(leader, QuickPlayGoal.Search));
            var member = Leader(2); member.IsLeader = false;
            Assert.AreEqual(QuickPlayStep.Ready, QuickPlayFlow.Next(member, QuickPlayGoal.Search));
            member.SelfReady = true;
            Assert.AreEqual(QuickPlayStep.WaitForLeader, QuickPlayFlow.Next(member, QuickPlayGoal.Search));
        }

        [Test]
        public void RetriesBusyRequestsMatchesAndPrivateRoomsStopTheFlow()
        {
            var state = Leader(); state.RetryRequired = true;
            Assert.AreEqual(QuickPlayStep.Blocked, QuickPlayFlow.Next(state, QuickPlayGoal.Search));
            state = Leader(); state.Busy = true;
            Assert.AreEqual(QuickPlayStep.Wait, QuickPlayFlow.Next(state, QuickPlayGoal.Search));
            state = Leader(); state.MatchActive = true;
            Assert.AreEqual(QuickPlayStep.None, QuickPlayFlow.Next(state, QuickPlayGoal.Search));
            state = Leader(); state.HasAttempt = true;
            Assert.AreEqual(QuickPlayStep.Blocked, QuickPlayFlow.Next(state, QuickPlayGoal.Search), "A private room attempt is not a search.");
        }

        [Test]
        public void OnlyRequestStepsAct()
        {
            var acting = Enum.GetValues(typeof(QuickPlayStep)).Cast<QuickPlayStep>().Where(QuickPlayFlow.Acts).ToArray();
            Assert.AreEqual(new[] { QuickPlayStep.Connect, QuickPlayStep.CreateParty, QuickPlayStep.LeaveParty,
                QuickPlayStep.ApplySettings, QuickPlayStep.Ready, QuickPlayStep.Queue }, acting);
        }

        [TestCase(0, "0:00")]
        [TestCase(-3, "0:00")]
        [TestCase(42.9, "0:42")]
        [TestCase(61, "1:01")]
        [TestCase(725, "12:05")]
        public void SearchTimeReadsAsMinutesAndSeconds(double seconds, string text) =>
            Assert.AreEqual(text, QuickPlayFlow.Elapsed(seconds));
    }
}
