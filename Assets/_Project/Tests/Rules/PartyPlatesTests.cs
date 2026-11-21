using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class PartyPlatesTests
    {
        static readonly Srgb Paper = new Srgb(.98f, .95f, .88f), Night = new Srgb(.08f, .08f, .12f);
        static readonly Srgb[] Seats =
        {
            new Srgb(.91f, .34f, .29f), new Srgb(.25f, .56f, .88f), new Srgb(.36f, .73f, .39f), new Srgb(.95f, .70f, .24f),
        };

        [Test]
        public void ASoloPlayerGetsTheirOwnPlateAndOneOpenPlace()
        {
            var plates = PartyPlates.Lineup("Housemate", 0, null);
            Assert.AreEqual(2, plates.Count);
            Assert.AreEqual(PartyPlateKind.You, plates[0].Kind);
            Assert.AreEqual("Housemate", plates[0].Name);
            Assert.AreEqual("H", plates[0].Initial);
            Assert.AreEqual("", plates[0].Tag, "a couch has no leader or ready state");
            Assert.AreEqual(PartyPlateKind.Open, plates[1].Kind);
            Assert.AreEqual(PartyPlates.OpenLabel, plates[1].Name);
            Assert.AreEqual(1, plates[1].Place, "the open place is the next one to fill");
            Assert.IsFalse(plates[1].Stands);
            Assert.AreEqual(1, PartyPlates.Standing(plates));
        }

        [TestCase(1, 2, true)]
        [TestCase(2, 3, true)]
        [TestCase(3, 4, false)]
        [TestCase(9, 4, false)]
        [TestCase(-2, 1, true)]
        public void CouchGuestsStandInSeatOrderAndAFullPartyHasNoOpenPlace(int guests, int standing, bool open)
        {
            var plates = PartyPlates.Lineup("Sam", guests, null);
            Assert.AreEqual(standing, PartyPlates.Standing(plates));
            Assert.AreEqual(open, plates.Any(plate => plate.Kind == PartyPlateKind.Open));
            Assert.LessOrEqual(plates.Count(plate => plate.Kind == PartyPlateKind.Open), 1, "one soft plate at most");
            for (int place = 1; place < standing; place++)
            {
                Assert.AreEqual(PartyPlateKind.Couch, plates[place].Kind);
                Assert.AreEqual(place, plates[place].Place);
                Assert.AreEqual("Player " + (place + 1), plates[place].Name);
                Assert.AreEqual("P" + (place + 1), plates[place].Initial);
                Assert.AreEqual("", plates[place].Tag);
            }
        }

        [Test]
        public void AnOnlinePartyTagsTheLeaderAndWhoIsReady()
        {
            var plates = PartyPlates.Lineup("Sam", 0, new[] { "You · ready", "Player 2 · leader · ready", "Player 3 · not ready" });
            Assert.AreEqual(4, plates.Count);
            Assert.AreEqual("Sam", plates[0].Name, "your own plate keeps your name");
            Assert.AreEqual(PartyPlates.ReadyTag, plates[0].Tag);
            Assert.AreEqual(PartyPlateKind.Friend, plates[1].Kind);
            Assert.AreEqual("Player 2", plates[1].Name);
            Assert.AreEqual("LEADER · READY", plates[1].Tag);
            Assert.AreEqual("P2", plates[1].Initial);
            Assert.AreEqual("Player 3", plates[2].Name);
            Assert.AreEqual("", plates[2].Tag, "not ready shows no tag");
            Assert.AreEqual(PartyPlateKind.Open, plates[3].Kind);
            Assert.AreEqual(2, PartyPlates.Friends(plates));
            Assert.AreEqual(3, PartyPlates.Standing(plates));
        }

        [Test]
        public void FriendsStandAfterCouchGuestsAndNeverPastFourPlaces()
        {
            var plates = PartyPlates.Lineup("Sam", 2, new[] { "You · leader · not ready", "Player 2 · ready", "Player 3 · ready" });
            Assert.AreEqual(4, plates.Count);
            CollectionAssert.AreEqual(new[] { PartyPlateKind.You, PartyPlateKind.Couch, PartyPlateKind.Couch, PartyPlateKind.Friend },
                plates.Select(plate => plate.Kind).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, plates.Select(plate => plate.Place).ToArray());
            Assert.AreEqual(PartyPlates.LeaderTag, plates[0].Tag);
            Assert.AreEqual(1, PartyPlates.Friends(plates));
        }

        [TestCase("You · leader · ready", "You", true, true, true)]
        [TestCase("Player 2 · not ready", "Player 2", false, false, false)]
        [TestCase("Player 4 · ready · weak link", "Player 4", false, false, true)]
        [TestCase("  Mo  ", "Mo", false, false, false)]
        [TestCase("", "", false, false, false)]
        [TestCase(null, "", false, false, false)]
        public void AMemberLineIsReadByItsParts(string line, string name, bool you, bool leader, bool ready)
        {
            var member = PartyPlates.ParseMember(line);
            Assert.AreEqual(name, member.Name);
            Assert.AreEqual(you, member.You);
            Assert.AreEqual(leader, member.Leader);
            Assert.AreEqual(ready, member.Ready);
        }

        [Test]
        public void BlankLinesAndAMissingNameNeverMakeAPlate()
        {
            var plates = PartyPlates.Lineup("  ", 0, new[] { "", "   ", " · ready" });
            Assert.AreEqual(PartyPlates.YouWord, plates[0].Name, "a missing name falls back to You");
            Assert.AreEqual(2, plates.Count);
            Assert.AreEqual(PartyPlateKind.Open, plates[1].Kind);
        }

        [TestCase("Housemate", "H")]
        [TestCase("zed", "Z")]
        [TestCase("Player 12", "P12")]
        [TestCase("Player One", "P")]
        [TestCase("", "?")]
        public void TheChipShowsAnInitial(string name, string initial) => Assert.AreEqual(initial, PartyPlates.Initial(name));

        [Test]
        public void TheSignatureChangesWithNamesTagsAndPlaces()
        {
            string solo = PartyPlates.Signature(PartyPlates.Lineup("Sam", 0, null));
            Assert.AreEqual(solo, PartyPlates.Signature(PartyPlates.Lineup("Sam", 0, null)));
            Assert.AreNotEqual(solo, PartyPlates.Signature(PartyPlates.Lineup("Sam", 1, null)));
            Assert.AreNotEqual(solo, PartyPlates.Signature(PartyPlates.Lineup("Sama", 0, null)));
            Assert.AreNotEqual(PartyPlates.Signature(PartyPlates.Lineup("Sam", 0, new[] { "You · ready" })),
                PartyPlates.Signature(PartyPlates.Lineup("Sam", 0, new[] { "You · not ready" })));
        }

        [Test]
        public void StageOffsetsKeepEveryoneApartAndTheOpenPlaceLeftOfTheGroup()
        {
            Assert.AreEqual(0f, PartyPlates.Offset(0, 1));
            Assert.AreEqual(1f, PartyPlates.Scale(1));
            Assert.AreEqual(1f, PartyPlates.Scale(2));
            Assert.AreEqual(.90f, PartyPlates.Scale(3));
            Assert.AreEqual(.82f, PartyPlates.Scale(4));
            for (int count = 1; count <= PartyPlates.Places; count++)
            {
                var offsets = Enumerable.Range(0, count).Select(place => PartyPlates.Offset(place, count)).OrderBy(x => x).ToArray();
                for (int i = 1; i < offsets.Length; i++)
                    Assert.GreaterOrEqual(offsets[i] - offsets[i - 1], .8f, count + " players stand apart");
                Assert.AreEqual(0f, offsets.Sum(), .001f, count + " players stay centred");
                Assert.AreEqual(offsets[0] - PartyPlates.OpenGap, PartyPlates.OpenOffset(count), .001f, "the open place is one step left");
            }
        }

        [Test]
        public void APlateClearOfThePlayCardStaysUnderItsCharacter()
        {
            var bounds = new PlateBox(0, 0, 1920, 1080);
            var play = new PlateBox(1594, 26, 1894, 222);
            var plate = PlateBox.Under(960, 292, 220, 56);
            var settled = PartyPlates.Settle(plate, bounds, play, 12);
            Assert.AreEqual(plate.Left, settled.Left);
            Assert.AreEqual(plate.Bottom, settled.Bottom);
            Assert.AreEqual(220f, settled.Width);
            Assert.AreEqual(56f, settled.Height);
        }

        [Test]
        public void APlateThatWouldTouchThePlayCardSlidesLeftOfIt()
        {
            var bounds = new PlateBox(0, 0, 1920, 1080);
            var play = new PlateBox(1594, 26, 1894, 260);
            var settled = PartyPlates.Settle(PlateBox.Under(1540, 292, 220, 56), bounds, play, 12);
            Assert.IsFalse(settled.Overlaps(play));
            Assert.AreEqual(1594 - 12, settled.Right, .01f, "it stops a margin short of the card");
            Assert.AreEqual(236f, settled.Bottom, "and keeps its height on screen");
        }

        [Test]
        public void WithNoRoomBesideThePlayCardThePlateRisesAboveIt()
        {
            var bounds = new PlateBox(0, 0, 400, 1080);
            var play = new PlateBox(100, 26, 400, 260);
            var settled = PartyPlates.Settle(PlateBox.Under(250, 292, 220, 56), bounds, play, 12);
            Assert.IsFalse(settled.Overlaps(play));
            Assert.AreEqual(272f, settled.Bottom, .01f);
        }

        [Test]
        public void APlateNeverLeavesTheScreen()
        {
            var bounds = new PlateBox(0, 0, 1280, 720);
            var left = PartyPlates.Settle(PlateBox.Under(-40, 200, 160, 48), bounds, null, 12);
            Assert.AreEqual(0f, left.Left);
            Assert.AreEqual(160f, left.Right);
            var right = PartyPlates.Settle(PlateBox.Under(1300, 20, 160, 48), bounds, null, 12);
            Assert.AreEqual(1280f, right.Right);
            Assert.AreEqual(0f, right.Bottom);
        }

        [Test]
        public void SeatColoursBecomeReadableNamesOnLightAndDarkPaper()
        {
            foreach (var paper in new[] { Paper, Night })
                foreach (var seat in Seats)
                {
                    var ink = PartyPlates.Ink(seat, paper);
                    Assert.GreaterOrEqual(PartyPlates.Contrast(ink, paper), PartyPlates.InkContrast - .01);
                    if (PartyPlates.Contrast(seat, paper) >= PartyPlates.InkContrast) Assert.AreEqual(seat.R, ink.R, "a readable seat colour is kept");
                    else Assert.Less(PartyPlates.Contrast(ink, paper), PartyPlates.InkContrast + .1, "only as far from the seat colour as reading needs");
                }
            var red = PartyPlates.Ink(Seats[0], Paper);
            Assert.Greater(red.R, red.G + .2f, "red stays red");
            var blue = PartyPlates.Ink(Seats[1], Paper);
            Assert.Greater(blue.B, blue.R + .2f, "blue stays blue");
        }

        [Test]
        public void AColourThatAlreadyReadsWellIsKept()
        {
            var navy = new Srgb(.1f, .12f, .3f);
            var ink = PartyPlates.Ink(navy, Paper);
            Assert.AreEqual(navy.R, ink.R);
            Assert.AreEqual(navy.G, ink.G);
            Assert.AreEqual(navy.B, ink.B);
        }

        [TestCase(0, 0L, "")]
        [TestCase(0, 7200L, "")]
        [TestCase(1, PartyPlates.NotRecorded, "1 match played")]
        [TestCase(12, PartyPlates.NotRecorded, "12 matches played")]
        [TestCase(12, 59L, "12 matches played")]
        [TestCase(12, 2700L, "12 matches · 45 min")]
        [TestCase(12, 12000L, "12 matches · 3 h 20 min")]
        [TestCase(1234, 7200L, "1234 matches · 2 h")]
        public void TheCareerLineCountsMatchesAndAddsTimeOnlyWhenItIsRecorded(int matches, long seconds, string line) =>
            Assert.AreEqual(line, PartyPlates.CareerLine(matches, seconds));

        [TestCase(-1L, "")]
        [TestCase(0L, "")]
        [TestCase(60L, "1 min")]
        [TestCase(3599L, "59 min")]
        [TestCase(3600L, "1 h")]
        [TestCase(360000L, "100 h")]
        public void PlayTimeReadsAsHoursAndMinutes(long seconds, string text) => Assert.AreEqual(text, PartyPlates.PlayTime(seconds));
    }
}
