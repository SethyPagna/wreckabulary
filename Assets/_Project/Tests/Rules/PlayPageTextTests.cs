using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public class PlayPageTextTests
    {
        static readonly string[] FourMembers = { "You · leader · ready", "Player 2 · ready", "Player 3 · ready", "Player 4 · not ready" };

        [Test]
        public void TheTutorialColumnLeadsUntilTheTutorialIsDone()
        {
            CollectionAssert.AreEqual(HouseModes.Families, PlayPageText.Families(true), "a finished tutorial keeps the usual order");
            var first = PlayPageText.Families(false);
            Assert.AreEqual(HouseModes.FamilyOf(HouseModes.Tutorial), first[0], "a new player sees the Tutorial column first");
            CollectionAssert.AreEquivalent(HouseModes.Families, first, "no column is lost or doubled");
            CollectionAssert.AreEqual(HouseModes.Families.Where(family => family != first[0]), first.Skip(1), "the rest keep their order");
        }

        [Test]
        public void TheStartCountIsReadFromTheMatchFoundLine()
        {
            Assert.AreEqual(4, PlayPageText.StartCount("Match found · starting in 4"));
            Assert.AreEqual(1, PlayPageText.StartCount("Match found · starting in 1"));
            Assert.AreEqual(-1, PlayPageText.StartCount("Joining match…"));
            Assert.AreEqual(-1, PlayPageText.StartCount("Searching 0:42"));
            Assert.AreEqual(-1, PlayPageText.StartCount("Match found · starting in 0"));
            Assert.AreEqual(-1, PlayPageText.StartCount("Match found · starting in -2"));
            Assert.AreEqual(-1, PlayPageText.StartCount(""));
            Assert.AreEqual(-1, PlayPageText.StartCount(null));
        }

        [Test]
        public void TheOnlinePartyBecomesPlatesWithSeatsAndTags()
        {
            var plates = PlayPageText.OnlineParty(FourMembers);
            CollectionAssert.AreEqual(new[] { "You", "Player 2", "Player 3", "Player 4" }, plates.Select(plate => plate.Name));
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, plates.Select(plate => plate.Place), "each member keeps the seat colour of its number");
            CollectionAssert.AreEqual(new[] { "LEADER · READY", "READY", "READY", "" }, plates.Select(plate => plate.Tag));
            CollectionAssert.AreEqual(new[] { "Y", "P2", "P3", "P4" }, plates.Select(plate => plate.Initial));
            Assert.AreEqual(PartyPlateKind.You, plates[0].Kind);
            Assert.IsTrue(plates.Skip(1).All(plate => plate.Kind == PartyPlateKind.Friend));
            Assert.IsTrue(plates.All(plate => plate.Stands));
            CollectionAssert.IsEmpty(PlayPageText.OnlineParty(null));
            CollectionAssert.IsEmpty(PlayPageText.OnlineParty(new[] { "", "  " }));
            Assert.AreEqual(PartyPlates.Places, PlayPageText.OnlineParty(FourMembers.Concat(new[] { "Player 5 · ready" }).ToArray()).Count,
                "never more plates than seats");
            Assert.AreEqual("Player 2", PlayPageText.OnlineParty(new[] { "Player 2 · leader · ready" })[0].Name);
            Assert.AreEqual(0, PlayPageText.OnlineParty(new[] { "Player 2 · leader · ready" })[0].Place);
        }

        [Test]
        public void ACardThePartyCannotPlaySaysWhy()
        {
            var ffa = QuickPlaylists.Find("ffa");
            Assert.AreEqual("", PlayPageText.PartyReason(ffa, 1), "a lone player plays Free-for-All");
            Assert.AreEqual(PlayPageText.SoloOnly, PlayPageText.PartyReason(ffa, 2));
            Assert.AreEqual(PlayPageText.SoloOnly, PlayPageText.PartyReason(ffa, 4));
            Assert.AreEqual("Parties of up to 2", PlayPageText.PartyReason(QuickPlaylists.Find("relay-2v2"), 3));
            Assert.AreEqual("", PlayPageText.PartyReason(QuickPlaylists.Find("relay-4v4"), 4));
            Assert.AreEqual("", PlayPageText.PartyReason(QuickPlaylists.Find("coop"), 4));
            Assert.AreEqual("", PlayPageText.PartyReason(null, 4));
            Assert.AreEqual("Solo only · your party skips it", PlayPageText.SkipLine(PlayPageText.SoloOnly));
            Assert.AreEqual("", PlayPageText.SkipLine(""));
            Assert.AreEqual("", PlayPageText.SkipLine(null));
        }

        [Test]
        public void TheSummaryCountsOnlyWhatThePartyCanPlay()
        {
            var start = new QuickPlayChoices();
            Assert.AreEqual("4 modes · 2v2", PlayPageText.SearchLine(start, 1), "the first visit ticks four cards");
            Assert.AreEqual("3 modes · 4v4", PlayPageText.SearchLine(start, 3), "Free-for-All is solo, so a party of three searches three");
            Assert.AreEqual("3 modes · 4v4", PlayPageText.SearchLine(start, 4));
            var all = new QuickPlayChoices("v2;lists=relay-2v2+brawl-2v2+ffa+coop+freeplay;size=Duo");
            Assert.AreEqual("All modes · 2v2", PlayPageText.SearchLine(all, 1));
            Assert.AreEqual("4 modes · 4v4", PlayPageText.SearchLine(all, 4), "never claims all when one is skipped");
            Assert.AreEqual("Team Race 2v2", PlayPageText.SearchLine(new QuickPlayChoices("v2;lists=relay-2v2;size=Duo"), 1));
            Assert.AreEqual("Free-for-All", PlayPageText.SearchLine(new QuickPlayChoices("v2;lists=ffa;size=Duo"), 1), "no team size for a mode without teams");
            Assert.AreEqual("2 modes", PlayPageText.SearchLine(new QuickPlayChoices("v2;lists=coop+freeplay;size=Duo"), 1));
            Assert.AreEqual(PlayPageText.NoModeFits, PlayPageText.SearchLine(new QuickPlayChoices("v2;lists=ffa;size=Duo"), 2));
            foreach (int size in new[] { 1, 2, 3, 4 })
                StringAssert.DoesNotContain("·  ", PlayPageText.SearchLine(start, size));
            CollectionAssert.AreEqual(new[] { "relay-4v4", "brawl-4v4", "coop" }, PlayPageText.Playable(start, 4).Select(list => list.Id));
            CollectionAssert.IsEmpty(PlayPageText.Playable(null, 1));
        }

        [Test]
        public void ThePartyLineIsShortAndNamesTheRegion()
        {
            Assert.AreEqual("Party of 4 · Asia", PlayPageText.PartyLine(4, "Asia"));
            Assert.AreEqual("Party of 1", PlayPageText.PartyLine(0, ""));
            Assert.AreEqual("Party of 2", PlayPageText.PartyLine(2, null));
        }

        [Test]
        public void TheBuilderSaysWhichHousesItCannotUseYet()
        {
            Assert.AreEqual("City Flat is not open to the builder yet.", PlayPageText.BuilderNote(new[] { "City Flat" }));
            Assert.AreEqual("City Flat and Terrace House are not open to the builder yet.", PlayPageText.BuilderNote(new[] { "City Flat", "Terrace House" }));
            Assert.AreEqual("City Flat, Terrace House and Walk-up Apartments are not open to the builder yet.",
                PlayPageText.BuilderNote(new[] { "City Flat", "Terrace House", "Walk-up Apartments" }));
            Assert.AreEqual("", PlayPageText.BuilderNote(new string[0]));
            Assert.AreEqual("", PlayPageText.BuilderNote(null));
            Assert.AreEqual("City Flat is not open to the builder yet.", PlayPageText.BuilderNote(new[] { "", "City Flat", null }));
        }

        [Test]
        public void ThePlayerFacingWordsAvoidLocalAndOffline()
        {
            foreach (string line in new[] { PlayPageText.CodeHint, PlayPageText.NotACode, PlayPageText.SoloOnly, PlayPageText.NoModeFits, PlayPageText.StartHere })
            {
                StringAssert.DoesNotContain("local", line.ToLowerInvariant(), line);
                StringAssert.DoesNotContain("offline", line.ToLowerInvariant(), line);
            }
        }
    }
}
