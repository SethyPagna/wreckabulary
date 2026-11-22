using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class PlayerBarTests
    {
        static (int[] order, float[] centres, float width, int split) Strip(float centre, params int[] teams)
        {
            var order = new int[PlayerBar.MostSeats];
            var centres = new float[PlayerBar.MostSeats];
            float width = PlayerBar.Strip(teams, order, centres, centre, out int split);
            return (order, centres, width, split);
        }

        [Test]
        public void TheBarShowsForTwoOrMoreSeats()
        {
            Assert.IsFalse(PlayerBar.Visible(0));
            Assert.IsFalse(PlayerBar.Visible(1), "A solo player reads their own stats bottom left.");
            Assert.IsTrue(PlayerBar.Visible(2));
            Assert.IsTrue(PlayerBar.Visible(8));
        }

        [Test]
        public void FourSeatsGetRoomyCardsAndEightGetSnugOnes()
        {
            Assert.AreEqual(68f, PlayerBar.Portrait(4));
            Assert.AreEqual(PlayerBar.Portrait(4), PlayerBar.CardWidth(4), "A card is exactly its portrait, so cards can touch.");
            Assert.AreEqual(56f, PlayerBar.Portrait(8));
            Assert.AreEqual(PlayerBar.Portrait(5), PlayerBar.CardWidth(5));
            Assert.AreEqual(PlayerBar.Portrait(4), PlayerBar.HealthY(4), "The health bar sits directly under the portrait, no gap.");
            Assert.AreEqual(PlayerBar.CardWidth(4) - PlayerBar.HealthWidth(4), PlayerBar.Hairline, "Health bars span the card but for a hairline between neighbours.");
            Assert.LessOrEqual(PlayerBar.Hairline, 1f);
            foreach (int seats in new[] { 4, 8 })
            {
                Assert.AreEqual(PlayerBar.HealthWidth(seats) - PlayerBar.StaminaTrim, PlayerBar.StaminaWidth(seats), .01f, "Stamina runs from the health bar's left end to just short of its right end.");
                Assert.That(PlayerBar.StaminaTrim, Is.InRange(3f, 5f), "...a tiny space, about 4 units at 1080p...");
                Assert.Greater(PlayerBar.CardWidth(seats) - PlayerBar.StaminaWidth(seats), PlayerBar.Hairline, "...so each stamina bar ends before the next card's begins.");
            }
            Assert.GreaterOrEqual(PlayerBar.StaminaHeight, 8f, "Stamina is thick enough to notice.");
            Assert.AreEqual(2f * PlayerBar.Outline, PlayerBar.SideGap, "Two sides meet where their outlines touch.");
            Assert.AreEqual(PlayerBar.CardHeight(4) + 2f * PlayerBar.Outline, PlayerBar.BoxHeight(4));
            Assert.AreEqual(PlayerBar.CardHeight(4, false) + 2f * PlayerBar.Outline, PlayerBar.BoxHeight(4, false));
            Assert.Less(PlayerBar.Outline, PlayerBar.PlatePad, "Outlines stay on the plate.");
            Assert.Less(PlayerBar.CardHeight(8), PlayerBar.CardHeight(4));
            Assert.AreEqual(PlayerBar.CardHeight(4), PlayerBar.Height);
            Assert.AreEqual(PlayerBar.Height + PlayerBar.PlatePad + 12f, PlayerBar.Shift, "Top-centre text moves down by the bar, its backing plate and a 12 px gap.");
            Assert.AreEqual(408f + 20f, PlayerBar.PlateWidth(408f), "The plate pads the cards by 10 px each side.");
            Assert.AreEqual(PlayerBar.CardHeight(8) + 20f, PlayerBar.PlateHeight(8));
            Assert.GreaterOrEqual(PlayerBar.StaminaY(4), PlayerBar.HealthY(4) + PlayerBar.HealthHeight, "Stamina sits under the health bar.");
            if (PlayerBar.StaminaShown)
                Assert.GreaterOrEqual(PlayerBar.ScoreY(4), PlayerBar.StaminaY(4) + PlayerBar.StaminaHeight, "The score sits under the stamina bar, with no name row between.");
            float stamina = PlayerBar.StaminaShown ? PlayerBar.StaminaGap + PlayerBar.StaminaHeight : 0f;
            foreach (int seats in new[] { 4, 8 })
            {
                Assert.AreEqual(PlayerBar.Portrait(seats) + PlayerBar.HealthGap + PlayerBar.HealthHeight + stamina + PlayerBar.ScoreGap + PlayerBar.ScoreHeight,
                    PlayerBar.CardHeight(seats), .01f, "A card is its rows stacked, nothing more: portrait, health, stamina, score.");
                Assert.AreEqual(PlayerBar.Portrait(seats) + PlayerBar.HealthHeight + stamina, PlayerBar.CardHeight(seats, false), .01f, "Without scores the card ends under its stamina.");
                Assert.AreEqual(PlayerBar.CardHeight(seats), PlayerBar.CardHeight(seats, true));
            }
            Assert.AreEqual(124f, PlayerBar.CardHeight(4), "Shorter than round 7's 148 now the names are gone.");
        }

        [Test]
        public void TheHealthNumberFitsItsBarAndTheBarFitsThePhoneWindow()
        {
            Assert.GreaterOrEqual(PlayerBar.NumberMin, 16f, "Bar numbers read at 1248x576.");
            Assert.GreaterOrEqual(PlayerBar.NumberSize, PlayerBar.NumberMin);
            Assert.GreaterOrEqual(PlayerBar.HealthHeight, PlayerBar.NumberSize, "The number fits inside its bar.");
            Assert.Less(PlayerBar.StaminaHeight, PlayerBar.HealthHeight / 2f, "Stamina stays a slim second line.");
            Assert.GreaterOrEqual(PlayerBar.SoloNumberSize, PlayerBar.NumberSize, "Alone, the number is bigger than a seat card's.");
            Assert.GreaterOrEqual(PlayerBar.SoloHeight, PlayerBar.SoloNumberSize, "...and still fits its bar.");
            Assert.Less(PlayerBar.SoloStaminaHeight, PlayerBar.SoloHeight / 2f);
            Assert.Greater(PlayerBar.SoloWidth, PlayerBar.RoomyPortrait, "The solo bar is wider than a card's.");
            const float phoneHeight = 576f / .58878f;
            Assert.LessOrEqual(PlayerBar.Top + PlayerBar.PlateHeight(PlayerBar.RoomySeats), phoneHeight / 5f, "The bar and its plate take under a fifth of the 1248x576 window.");
            Assert.LessOrEqual(PlayerBar.Top + PlayerBar.PlateHeight(PlayerBar.MostSeats), phoneHeight / 5f);
        }

        [Test]
        public void FreeForAllCardsTouchAndEachSeatIsItsOwnSide()
        {
            var (order, centres, width, split) = Strip(0f, 0, 1, 2, 3);
            float card = PlayerBar.CardWidth(4);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, First(order, 4));
            Assert.AreEqual(0, split, "No centre block, no split.");
            Assert.AreEqual(4 * card + 3 * PlayerBar.SideGap + 2f * PlayerBar.Outline, width, .01f, "Cards, the outlines between them and the two outer outlines; no other gaps.");
            Assert.AreEqual(-centres[0], centres[3], .01f, "Centred on the screen.");
            for (int i = 1; i < 4; i++)
            {
                Assert.AreEqual(card + PlayerBar.SideGap, centres[i] - centres[i - 1], .01f, "Neighbours are only their two outlines apart.");
                Assert.IsFalse(PlayerBar.Joined(new[] { 0, 1, 2, 3 }, order, i, split), "Every seat gets its own outline.");
            }
        }

        [Test]
        public void ACrewIsOneRowOfSeatColourOutlines()
        {
            Assert.IsFalse(PlayerBar.Grouped(new[] { 0, 0, 0 }), "Co-op: everyone on one side.");
            Assert.IsFalse(PlayerBar.Grouped(new[] { 0, 1, 2, 3 }), "Everyone against everyone.");
            Assert.IsTrue(PlayerBar.Grouped(new[] { 0, 1, 0, 1 }));
            var (order, centres, width, _) = Strip(0f, 0, 0, 0);
            float card = PlayerBar.CardWidth(3);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, First(order, 3));
            Assert.AreEqual(3 * card + 2f * PlayerBar.SideGap + 2f * PlayerBar.Outline, width, .01f);
            Assert.AreEqual(-centres[0], centres[2], .01f, "Centred on the screen.");
            for (int i = 1; i < 3; i++)
            {
                Assert.AreEqual(card + PlayerBar.SideGap, centres[i] - centres[i - 1], .01f, "Crewmates' outlines touch, with no other gap.");
                Assert.IsFalse(PlayerBar.Joined(new[] { 0, 0, 0 }, order, i, 0), "No shared crew outline: each card keeps its own seat-colour outline.");
            }
            Assert.IsFalse(PlayerBar.Joined(new[] { 0, 0, 0 }, order, 0, 0));
        }

        [Test]
        public void ACrewStaysTogetherWithTheClockTouchingOnItsRight()
        {
            foreach (var (centre, seats) in new[] { (ScoreLine.ClockWidth, 2), (ScoreLine.ClockWidth, 4), (ScoreLine.CentreWidth, 3), (ScoreLine.ClockWidth, 8) })
            {
                var teams = new int[seats];
                var order = new int[PlayerBar.MostSeats];
                var centres = new float[PlayerBar.MostSeats];
                float width = PlayerBar.Strip(teams, order, centres, centre, out int split, out float centreX);
                float card = PlayerBar.CardWidth(seats), half = card * .5f;
                string at = $"{seats} seats, centre {centre}";
                Assert.AreEqual(seats, split, $"{at}: the whole crew sits left of the centre block.");
                Assert.AreEqual(seats * card + (seats - 1) * PlayerBar.SideGap + 2f * PlayerBar.Outline + centre, width, .01f, $"{at}: a touching row of outlined cards, then the block; no other gaps.");
                Assert.AreEqual(-width * .5f + PlayerBar.Outline, centres[0] - half, .01f, $"{at}: the pair is centred on screen.");
                Assert.AreEqual(width * .5f, centreX + centre * .5f, .01f, $"{at}: the block closes the bar on the right.");
                Assert.AreEqual(centreX - centre * .5f, centres[seats - 1] + half + PlayerBar.Outline, .01f, $"{at}: the last crewmate's outline meets the block.");
                Assert.Greater(centreX, 0f, $"{at}: the block sits right of the crew.");
                for (int i = 1; i < seats; i++)
                {
                    Assert.AreEqual(card + PlayerBar.SideGap, centres[i] - centres[i - 1], .01f, $"{at}: crewmates' outlines touch.");
                    Assert.IsFalse(PlayerBar.Joined(teams, order, i, split), $"{at}: each crewmate keeps its own seat-colour outline.");
                }
            }
            Assert.IsTrue(PlayerBar.OneCrew(new[] { 2, 2, 2 }, 3));
            Assert.IsTrue(PlayerBar.OneCrew(new[] { 0, 0, 1 }, 2), "Only the seats on the bar count.");
            Assert.IsFalse(PlayerBar.OneCrew(new[] { 0, 0, 1 }, 3));
            Assert.IsFalse(PlayerBar.OneCrew(new[] { -1, -1 }, 2), "No team means each seat is its own side.");
            Assert.IsFalse(PlayerBar.OneCrew(new[] { 0 }, 1), "One seat is not a crew bar.");
            Assert.IsFalse(PlayerBar.OneCrew(null, 2));
        }

        [Test]
        public void VersusAndEveryoneForThemselvesKeepTheBlockInTheMiddle()
        {
            foreach (var (centre, teams) in new[] { (ScoreLine.CentreWidth, new[] { 0, 1, 0, 1 }), (ScoreLine.ClockWidth, new[] { 0, 1, 2, 3 }), (ScoreLine.ClockWidth, new[] { 0, 1 }), (ScoreLine.CentreWidth, new[] { 0, 0, 0, 0, 1, 1, 1, 1 }) })
            {
                var order = new int[PlayerBar.MostSeats];
                var centres = new float[PlayerBar.MostSeats];
                float width = PlayerBar.Strip(teams, order, centres, centre, out int split, out float centreX);
                Assert.AreEqual(0f, centreX, $"{string.Join(",", teams)}: the block stays in the middle.");
                Assert.AreEqual(PlayerBar.Strip(teams, new int[PlayerBar.MostSeats], new float[PlayerBar.MostSeats], centre, out int plain), width, .01f, "Both overloads agree.");
                Assert.AreEqual(plain, split);
                Assert.Greater(split, 0);
                Assert.Less(split, teams.Length);
            }
            var crewOnly = new float[PlayerBar.MostSeats];
            PlayerBar.Strip(new[] { 0, 0, 0 }, new int[PlayerBar.MostSeats], crewOnly, 0f, out int none, out float noBlock);
            Assert.AreEqual(0, none, "A crew with no clock is one centred strip, as before.");
            Assert.AreEqual(0f, noBlock);
            Assert.AreEqual(-crewOnly[0], crewOnly[2], .01f);
        }

        [Test]
        public void DuosTouchInsideATeamAndMeetTheScoreBlock()
        {
            var teams = new[] { 0, 1, 0, 1 };
            var (order, centres, width, split) = Strip(ScoreLine.CentreWidth, teams);
            float card = PlayerBar.CardWidth(4), half = card * .5f, reach = ScoreLine.CentreWidth * .5f + PlayerBar.Outline;
            CollectionAssert.AreEqual(new[] { 0, 2, 1, 3 }, First(order, 4), "P1 and P3 left, P2 and P4 right.");
            Assert.AreEqual(2, split);
            Assert.AreEqual(card, centres[1] - centres[0], .01f, "Teammates touch.");
            Assert.AreEqual(card, centres[3] - centres[2], .01f);
            Assert.AreEqual(-reach, centres[1] + half, .01f, "The left team's outline meets the score block's.");
            Assert.AreEqual(reach, centres[2] - half, .01f, "So does the right team's.");
            Assert.AreEqual(ScoreLine.CentreWidth + 4f * PlayerBar.Outline + 4f * card, width, .01f);
            Assert.IsTrue(PlayerBar.Joined(teams, order, 1, split));
            Assert.IsFalse(PlayerBar.Joined(teams, order, 2, split), "The centre ends one outline and starts the next.");
            Assert.IsTrue(PlayerBar.Joined(teams, order, 3, split));
        }

        [Test]
        public void FreeForAllWithAClockKeepsTwoSeatsEachSide()
        {
            var (_, centres, width, split) = Strip(ScoreLine.ClockWidth, 0, 1, 2, 3);
            float card = PlayerBar.CardWidth(4), half = card * .5f, reach = ScoreLine.ClockWidth * .5f + PlayerBar.Outline;
            Assert.AreEqual(2, split, "Two seats left of the clock, two right, as before.");
            Assert.AreEqual(-reach, centres[1] + half, .01f);
            Assert.AreEqual(reach, centres[2] - half, .01f);
            Assert.AreEqual(card + PlayerBar.SideGap, centres[1] - centres[0], .01f);
            Assert.AreEqual(2f * (reach + 2f * card + PlayerBar.SideGap + PlayerBar.Outline), width, .01f);
            Assert.AreEqual(ScoreLine.ClockWidth, PlayerBar.Strip(new int[0], new int[8], new float[8], ScoreLine.ClockWidth, out split), "With no cards the bar is the clock.");
            Assert.AreEqual(0f, PlayerBar.Strip(null, new int[8], new float[8], 0f, out split));
        }

        [Test]
        public void WarFitsEightTouchingSnugCardsNarrowerThanBefore()
        {
            var (order, centres, width, split) = Strip(ScoreLine.CentreWidth, 0, 0, 0, 0, 1, 1, 1, 1);
            float card = PlayerBar.CardWidth(8);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, order);
            Assert.AreEqual(4, split);
            Assert.AreEqual(ScoreLine.CentreWidth + 4f * PlayerBar.Outline + 8f * card, width, .01f);
            Assert.Less(width, 892f, "Narrower than the spaced 4v4 bar (892).");
            for (int i = 1; i < 8; i++)
                if (i != split) Assert.AreEqual(card, centres[i] - centres[i - 1], .01f, "Teammates touch.");
            Assert.Greater(centres[4] - card * .5f, centres[3] + card * .5f, "The score block sits between the teams.");
            var (mixed, _, _, _) = Strip(ScoreLine.CentreWidth, 0, 1, 0, 1, 0, 1, 0, 1);
            CollectionAssert.AreEqual(new[] { 0, 2, 4, 6, 1, 3, 5, 7 }, mixed, "Stable: seat order inside each team.");
        }

        [Test]
        public void ANarrowScreenScalesTheBarDown()
        {
            Assert.AreEqual(1f, PlayerBar.Scale(696f, 1920f));
            Assert.AreEqual(1f, PlayerBar.Scale(0f, 1920f));
            Assert.AreEqual(1f, PlayerBar.Scale(696f, 696f + 20f + 48f), "The backing plate fits exactly.");
            float scale = PlayerBar.Scale(696f, 600f);
            Assert.AreEqual((600f - 48f) / 716f, scale, 1e-4f);
            Assert.LessOrEqual(PlayerBar.PlateWidth(696f) * scale, 600f - 2f * PlayerBar.Edge + .01f, "The plate, not just the cards, fits.");
        }

        [Test]
        public void TheBarDropsBelowWhateverWouldCoverIt()
        {
            float half = 204f, height = PlayerBar.Height;
            Assert.AreEqual(PlayerBar.Top, PlayerBar.ClearTop(half, height, Array.Empty<BarBlock>()));
            var aside = new List<BarBlock> { new(-960f, -500f, 28f, 128f), new(670f, 922f, 30f, 282f) };
            Assert.AreEqual(PlayerBar.Top, PlayerBar.ClearTop(half, height, aside), "The match label and the minimap sit beside a 4-seat bar.");
            var pause = new List<BarBlock> { new(-35f, 35f, 27f, 97f) };
            Assert.AreEqual(97f + PlayerBar.Margin, PlayerBar.ClearTop(half, height, pause), "A touch pause button at the top centre pushes the bar under it.");
            var stacked = new List<BarBlock> { new(150f, 240f, 200f, 260f), new(-35f, 35f, 27f, 97f) };
            Assert.AreEqual(260f + PlayerBar.Margin, PlayerBar.ClearTop(half, height, stacked), "Dropping under one button can meet the next.");
            Assert.AreEqual(PlayerBar.Shift, PlayerBar.ShiftFor(PlayerBar.Top, 1f));
            Assert.AreEqual(PlayerBar.Shift + 95f, PlayerBar.ShiftFor(105f, 1f), .01f);
            Assert.AreEqual(PlayerBar.ShiftFor(PlayerBar.Top, 1f), PlayerBar.ShiftFor(PlayerBar.Top, 1f, PlayerBar.Height), .01f);
            Assert.AreEqual(56f + PlayerBar.PlatePad + PlayerBar.Clearance, PlayerBar.ShiftFor(PlayerBar.Top, 1f, 56f), .01f, "A shorter plate moves the notices down less.");
            float pad = PlayerBar.PlatePad;
            Assert.AreEqual(97f + PlayerBar.Margin + pad, PlayerBar.ClearTop(half, height, pause, pad), "The plate, not just the cards, clears the button.");
            var nearEdge = new List<BarBlock> { new(half + 4f, half + 60f, 20f, 70f) };
            Assert.AreEqual(PlayerBar.Top, PlayerBar.ClearTop(half, height, nearEdge), "Beside the cards...");
            Assert.AreEqual(70f + PlayerBar.Margin + pad, PlayerBar.ClearTop(half, height, nearEdge, pad), "...but under the plate's padding.");
            float plateBottom = PlayerBar.Top + height + pad;
            Assert.GreaterOrEqual(10f + PlayerBar.Shift - plateBottom, PlayerBar.Clearance, "A notice that started at the bar's top clears the plate by 12 px.");
        }

        [Test]
        public void SeatColoursGetBrighterButKeepTheirHue()
        {
            var palette = new[] { (0.91f, 0.34f, 0.29f), (0.25f, 0.56f, 0.88f), (0.36f, 0.73f, 0.39f), (0.95f, 0.70f, 0.24f) };
            foreach (var (r, g, b) in palette)
            {
                var (vr, vg, vb) = PlayerBar.Vivid(r, g, b);
                float max = Math.Max(r, Math.Max(g, b)), vmax = Math.Max(vr, Math.Max(vg, vb)), vmin = Math.Min(vr, Math.Min(vg, vb));
                Assert.GreaterOrEqual(vmax, .95f - 1e-4f, "bright enough for dark floors");
                Assert.GreaterOrEqual(vmax, max - 1e-4f);
                Assert.GreaterOrEqual((vmax - vmin) / vmax, .6f - 1e-4f, "saturated enough for light floors");
                Assert.AreEqual(Dominant(r, g, b), Dominant(vr, vg, vb), "same hue family");
            }
            Assert.AreEqual((.95f, .95f, .95f), PlayerBar.Vivid(.5f, .5f, .5f), "A grey stays grey.");
            Assert.AreEqual((0f, 0f, 0f), PlayerBar.Vivid(0f, 0f, 0f));
        }

        static int Dominant(float r, float g, float b) => r >= g && r >= b ? 0 : g >= b ? 1 : 2;

        static int[] First(int[] values, int count)
        {
            var head = new int[count];
            Array.Copy(values, head, count);
            return head;
        }

        [Test]
        public void StatusesFitThePortrait()
        {
            Assert.AreEqual("", PlayerBar.Status(true, true, 4f), "A wrecked seat is a grey portrait with a cross, no squeezed word.");
            Assert.AreEqual("", PlayerBar.Status(true, false, 0f));
            Assert.AreEqual("REVIVE\n7s", PlayerBar.Status(false, true, 6.2f), "Two short lines fit a portrait at a readable size.");
            Assert.AreEqual("REVIVE\n0s", PlayerBar.Status(false, true, -1f));
            Assert.AreEqual("", PlayerBar.Status(false, false, 0f));
            Assert.GreaterOrEqual(PlayerBar.StatusMin, 15f, "Never below the 15 pt floor.");
            Assert.GreaterOrEqual(PlayerBar.StatusSize, PlayerBar.StatusMin);
            Assert.AreEqual("W", PlayerBar.Letter('w'));
            Assert.AreEqual("D", PlayerBar.Letter('D'));
        }
    }
}
