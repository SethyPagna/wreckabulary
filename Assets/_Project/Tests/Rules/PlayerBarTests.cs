using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class PlayerBarTests
    {
        static (int[] order, float[] centres, float width) Arrange(params int[] teams)
        {
            var order = new int[PlayerBar.MostSeats];
            var centres = new float[PlayerBar.MostSeats];
            float width = PlayerBar.Arrange(teams, order, centres);
            return (order, centres, width);
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
            Assert.AreEqual(96f, PlayerBar.CardWidth(4));
            Assert.AreEqual(68f, PlayerBar.Portrait(4));
            Assert.AreEqual(78f, PlayerBar.CardWidth(5));
            Assert.AreEqual(56f, PlayerBar.Portrait(8));
            Assert.Less(PlayerBar.CardHeight(8), PlayerBar.CardHeight(4));
            Assert.AreEqual(PlayerBar.CardHeight(4), PlayerBar.Height);
            Assert.AreEqual(PlayerBar.Height + PlayerBar.PlatePad + 12f, PlayerBar.Shift, "Top-centre text moves down by the bar, its backing plate and a 12 px gap.");
            Assert.AreEqual(408f + 20f, PlayerBar.PlateWidth(408f), "The plate pads the cards by 10 px each side.");
            Assert.AreEqual(PlayerBar.CardHeight(8) + 20f, PlayerBar.PlateHeight(8));
            Assert.Greater(PlayerBar.NameY(4), PlayerBar.HealthY(4) + PlayerBar.HealthHeight - .01f, "The name sits under the health bar.");
            Assert.Greater(PlayerBar.HealthY(4), PlayerBar.Portrait(4), "The health bar sits right under the portrait.");
        }

        [Test]
        public void FreeForAllKeepsSeatOrderCentredOnTheScreen()
        {
            var (order, centres, width) = Arrange(0, 1, 2, 3);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, First(order, 4));
            Assert.AreEqual(4 * 96f + 3 * 8f, width, .01f);
            Assert.AreEqual(-centres[0], centres[3], .01f, "Centred on the screen.");
            for (int i = 1; i < 4; i++) Assert.AreEqual(96f + 8f, centres[i] - centres[i - 1], .01f);
        }

        [Test]
        public void OneTeamOrNoSharedTeamsIsNotSplit()
        {
            Assert.IsFalse(PlayerBar.Grouped(new[] { 0, 0, 0 }), "Co-op: everyone on one side.");
            Assert.IsFalse(PlayerBar.Grouped(new[] { 0, 1, 2, 3 }), "Everyone against everyone.");
            Assert.IsTrue(PlayerBar.Grouped(new[] { 0, 1, 0, 1 }));
            var (order, _, width) = Arrange(0, 0, 0);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, First(order, 3));
            Assert.AreEqual(3 * 96f + 2 * 8f, width, .01f);
        }

        [Test]
        public void DuosSortByTeamThenSeatAndSplitTheTeams()
        {
            var (order, centres, width) = Arrange(0, 1, 0, 1);
            CollectionAssert.AreEqual(new[] { 0, 2, 1, 3 }, First(order, 4), "P1 and P3 left, P2 and P4 right.");
            Assert.AreEqual(4 * 96f + 2 * 8f + 36f, width, .01f);
            Assert.AreEqual(96f + 36f, centres[2] - centres[1], .01f, "A 36 px gap between the teams.");
            Assert.AreEqual(-centres[0], centres[3], .01f);
        }

        [Test]
        public void WarFitsEightSnugCardsInUnderNineHundredPixels()
        {
            var (order, centres, width) = Arrange(0, 0, 0, 0, 1, 1, 1, 1);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, order);
            Assert.LessOrEqual(width, 900f);
            Assert.AreEqual(8 * 78f + 6 * 6f + 36f, width, .01f);
            Assert.Less(centres[3] + 39f + 30f, centres[4] - 39f, "Team two starts after the gap.");
            var (mixed, _, _) = Arrange(0, 1, 0, 1, 0, 1, 0, 1);
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
        public void HealthTurnsCoralAtAThirdAndStatusesFitThePortrait()
        {
            Assert.IsTrue(PlayerBar.Healthy(.36f));
            Assert.IsFalse(PlayerBar.Healthy(.35f));
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
