using System.Collections.Generic;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class TeamColoursTests
    {
        const int Red = 1, Blue = 2, Green = 3, Yellow = 4;
        static readonly int[] Palette = { Red, Blue, Green, Yellow };

        static ColourSeat[] Seats(params int[] teams)
        {
            var seats = new ColourSeat[teams.Length];
            for (int i = 0; i < teams.Length; i++) seats[i] = new ColourSeat(i, teams[i], Palette[i % Palette.Length]);
            return seats;
        }

        static int[] Frames(IReadOnlyList<ColourSeat> seats)
        {
            var frames = new List<int>();
            TeamColours.Frames(seats, frames);
            return frames.ToArray();
        }

        static int[] Hues(IReadOnlyList<ColourSeat> seats, int[] frames)
        {
            var hues = new int[frames.Length];
            for (int i = 0; i < frames.Length; i++) hues[i] = seats[frames[i]].Hue;
            return hues;
        }

        [Test]
        public void EveryoneForThemselvesWearsTheirOwnColour()
        {
            var seats = Seats(0, 1, 2, 3);
            Assert.IsFalse(TeamColours.Sided(seats));
            Assert.IsFalse(TeamColours.Accented(seats), "No accent: the frame already is the seat's own colour.");
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, Frames(seats));
        }

        [Test]
        public void ACrewIsOneSideAndKeepsItsOwnColours()
        {
            var crew = Seats(0, 0, 0);
            Assert.IsFalse(TeamColours.Sided(crew), "One side has no other side to tell apart.");
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Frames(crew));
            CollectionAssert.AreEqual(new[] { 0 }, Frames(Seats(0)), "A lone seat is itself.");
        }

        [Test]
        public void DuosFrameBothTeamMatesInTheTeamColourAndKeepTheirOwnAsAnAccent()
        {
            var duos = Seats(0, 0, 1, 1);
            Assert.IsTrue(TeamColours.Sided(duos));
            Assert.IsTrue(TeamColours.Accented(duos));
            CollectionAssert.AreEqual(new[] { 0, 0, 2, 2 }, Frames(duos), "P1's red for P1 and P2, AI 3's green for both bots, as the line-up shows.");
            CollectionAssert.AreEqual(new[] { Red, Red, Green, Green }, Hues(duos, Frames(duos)));
        }

        [Test]
        public void TeamBattleColoursEachSideByItsFirstSeat()
        {
            var war = Seats(0, 0, 1, 1, 0, 1, 0, 1);
            CollectionAssert.AreEqual(new[] { Red, Red, Green, Green, Red, Green, Red, Green }, Hues(war, Frames(war)),
                "The four left frames are all red, the four right ones all green.");
            var mixed = Seats(0, 1, 0, 1);
            CollectionAssert.AreEqual(new[] { 0, 1, 0, 1 }, Frames(mixed), "Seat order need not be grouped.");
        }

        [Test]
        public void TwoSidesNeverShareAColour()
        {
            var humansTogether = Seats(0, 0, 0, 0, 1, 1, 1, 1);
            var hues = Hues(humansTogether, Frames(humansTogether));
            Assert.AreEqual(Red, hues[0], "The side with P1 keeps P1's red.");
            Assert.AreEqual(Blue, hues[4], "Seat 5 is red again, so the other side takes its next member's blue.");
            CollectionAssert.AreEqual(new[] { Red, Red, Red, Red, Blue, Blue, Blue, Blue }, hues);
            var sameHue = new[] { new ColourSeat(0, 0, Red), new ColourSeat(1, 0, Red), new ColourSeat(2, 1, Red), new ColourSeat(3, 1, Red) };
            CollectionAssert.AreEqual(new[] { 0, 0, 2, 2 }, Frames(sameHue), "With no other colour to take, a side keeps its first seat's.");
        }

        [Test]
        public void TheFirstSeatByIdLeadsWhateverTheListOrder()
        {
            var seats = new[] { new ColourSeat(3, 1, Yellow), new ColourSeat(1, 0, Blue), new ColourSeat(2, 1, Green), new ColourSeat(0, 0, Red) };
            CollectionAssert.AreEqual(new[] { 2, 3, 2, 3 }, Frames(seats));
            Assert.AreEqual(3, TeamColours.Frame(seats, 1));
            Assert.AreEqual(2, TeamColours.Frame(seats, 0));
        }

        [Test]
        public void SeatsWithoutATeamStandAlone()
        {
            var seats = Seats(0, 0, Teams.NoTeam, Teams.NoTeam);
            Assert.IsTrue(TeamColours.Sided(seats));
            CollectionAssert.AreEqual(new[] { 0, 0, 2, 3 }, Frames(seats), "Two seats without a team are two sides of one.");
            Assert.IsFalse(TeamColours.Sided(Seats(Teams.NoTeam, Teams.NoTeam)));
        }

        [Test]
        public void MissingInputIsHarmless()
        {
            Assert.IsFalse(TeamColours.Sided(null));
            Assert.AreEqual(TeamColours.None, TeamColours.Frame(null, 0));
            Assert.AreEqual(TeamColours.None, TeamColours.Frame(Seats(0, 1), 2));
            Assert.AreEqual(TeamColours.None, TeamColours.Frame(Seats(0, 1), -1));
            var frames = new List<int> { 9 };
            TeamColours.Frames(null, frames);
            CollectionAssert.IsEmpty(frames);
            Assert.DoesNotThrow(() => TeamColours.Frames(Seats(0, 1), null));
        }
    }
}
