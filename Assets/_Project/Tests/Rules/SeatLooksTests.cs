using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class SeatLooksTests
    {
        const int CouchSeats = 4, MostSeats = 8;
        WardrobeCatalogue w;

        [SetUp]
        public void Load() => w = TestData.Wardrobe();

        Outfit Unstyled()
        {
            var look = w.Default.Clone();
            foreach (string slot in w.FallbackSlots) { look.Pieces.Remove(slot); look.Colours.Remove(slot); }
            Assert.IsTrue(w.Fits(look), "a look without hair or eyes still fits");
            return look;
        }

        Outfit Seat(int seat) => SeatLooks.WithSeatStyle(w, Unstyled(), seat, Career.FreePieces);

        [Test]
        public void PickIsStableWrapsAndLeavesUnseatedLooksAlone()
        {
            var options = new[] { "a", "b", "c" };
            Assert.AreEqual("a", SeatLooks.Pick(0, options));
            Assert.AreEqual("c", SeatLooks.Pick(2, options));
            Assert.AreEqual("a", SeatLooks.Pick(3, options), "more seats than options wrap round");
            Assert.AreEqual(SeatLooks.Pick(1, options), SeatLooks.Pick(1, options), "the same seat always gets the same piece");
            Assert.IsNull(SeatLooks.Pick(-1, options), "no seat, no pick");
            Assert.IsNull(SeatLooks.Pick(0, new string[0]));
            Assert.IsNull(SeatLooks.Pick(0, null));
        }

        [Test]
        public void EveryCouchSeatWearsItsOwnFreeHairAndEyes()
        {
            var looks = Enumerable.Range(0, CouchSeats).Select(Seat).ToList();
            foreach (string slot in w.FallbackSlots)
            {
                var worn = looks.Select(look => look.PieceIn(slot)).ToList();
                Assert.IsFalse(worn.Contains(null), slot + " is worn by every seat");
                Assert.AreEqual(CouchSeats, worn.Distinct().Count(), slot + ": " + string.Join(",", worn));
                foreach (string piece in worn)
                {
                    Assert.IsTrue(Career.FreePieces.Contains(piece), piece + " is free");
                    Assert.AreEqual(slot, w.Piece(piece).Slot);
                }
            }
            CollectionAssert.AreEqual(new[] { "HairSwept", "HairBob", "HairWavy", "HairBuns" }, looks.Select(look => look.PieceIn("Hair")));
            CollectionAssert.AreEqual(new[] { "EyesSparkle", "EyesButton", "EyesDot", "EyesHappy" }, looks.Select(look => look.PieceIn("Eyes")));
            foreach (var look in looks) Assert.IsEmpty(w.Problems(look));
        }

        [Test]
        public void SeatStylesShapeHairAndEyesButNeverColourTheSkin()
        {
            const string HairTint = "cute_hair";
            foreach (var piece in w.PiecesFor("Hair"))
                CollectionAssert.AreEqual(new[] { HairTint }, piece.TintMaterials, piece.Id + " colours only the hair");
            foreach (var piece in w.PiecesFor("Eyes"))
                Assert.IsEmpty(piece.TintMaterials, piece.Id + " carries no colour, so the face keeps its own");
            for (int seat = 0; seat < MostSeats; seat++)
            {
                var styled = Seat(seat);
                Assert.AreEqual(w.Default.ColourOf("Hair"), w.ColourFor(styled, "Hair").Id, "seat " + seat + " keeps the natural hair colour");
                foreach (string slot in w.Slots.Where(slot => !w.FallbackSlots.Contains(slot)))
                    Assert.AreEqual(Unstyled().ColourOf(slot), styled.ColourOf(slot), "seat " + seat + " " + slot);
            }
        }

        [Test]
        public void SeatOneKeepsTheFaceEveryoneAlreadyKnows()
        {
            var first = Seat(0);
            foreach (string slot in w.FallbackSlots)
                Assert.AreEqual(w.Default.PieceIn(slot), first.PieceIn(slot), slot + " matches the hair and eyes drawn when none are stored");
        }

        [Test]
        public void EightSeatsStillLookDifferent()
        {
            var faces = Enumerable.Range(0, MostSeats).Select(seat => Seat(seat).PieceIn("Hair") + "+" + Seat(seat).PieceIn("Eyes")).ToList();
            Assert.AreEqual(MostSeats, faces.Distinct().Count(), string.Join(",", faces));
        }

        [Test]
        public void OnlyHairAndEyesChangeSoTheSeatKeepsItsColoursAndClothes()
        {
            var plain = Unstyled();
            plain.Colours["Top"] = "mint";
            plain.Seat = 2;
            plain.Letter = "Q";
            var styled = SeatLooks.WithSeatStyle(w, plain, 2, Career.FreePieces);
            Assert.AreNotSame(plain, styled);
            Assert.IsNull(plain.PieceIn("Hair"), "the look passed in is not edited");
            foreach (string slot in w.Slots.Where(slot => !w.FallbackSlots.Contains(slot)))
            {
                Assert.AreEqual(plain.PieceIn(slot), styled.PieceIn(slot), slot);
                Assert.AreEqual(plain.ColourOf(slot), styled.ColourOf(slot), slot + " colour");
            }
            Assert.AreEqual("mint", styled.ColourOf("Top"));
            Assert.IsNull(styled.ColourOf("Hair"), "hair keeps the default colour");
            Assert.AreEqual(2, styled.Seat);
            Assert.AreEqual("Q", styled.Letter);
        }

        [Test]
        public void AChosenHairOrEyesIsNeverReplaced()
        {
            var chosen = Unstyled();
            chosen.Pieces["Hair"] = "HairBuns";
            var styled = SeatLooks.WithSeatStyle(w, chosen, 1, Career.FreePieces);
            Assert.AreEqual("HairBuns", styled.PieceIn("Hair"));
            Assert.AreEqual("EyesButton", styled.PieceIn("Eyes"), "only the missing slot is filled");
        }

        [Test]
        public void PaidOrUnknownPiecesAreNeverHandedOut()
        {
            var allowed = new[] { "HairWavy", "EyesJelly", "Hoodie", "NoSuchPiece" };
            CollectionAssert.AreEqual(new[] { "HairWavy" }, SeatLooks.FreePieces(w, "Hair", allowed));
            CollectionAssert.AreEqual(new[] { "EyesJelly" }, SeatLooks.FreePieces(w, "Eyes", allowed));
            for (int seat = 0; seat < CouchSeats; seat++)
            {
                var styled = SeatLooks.WithSeatStyle(w, Unstyled(), seat, allowed);
                Assert.AreEqual("HairWavy", styled.PieceIn("Hair"));
                Assert.AreEqual("EyesJelly", styled.PieceIn("Eyes"));
            }
            var none = SeatLooks.WithSeatStyle(w, Unstyled(), 1, new string[0]);
            Assert.IsNull(none.PieceIn("Hair"), "nothing free leaves the built-in hair");
            Assert.IsNull(none.PieceIn("Eyes"));
            Assert.IsNull(SeatLooks.WithSeatStyle(w, Unstyled(), -1, Career.FreePieces).PieceIn("Hair"), "a look with no seat is left alone");
            Assert.IsEmpty(SeatLooks.FreePieces(w, "Hair", null));
            Assert.Throws<ArgumentNullException>(() => SeatLooks.WithSeatStyle(null, Unstyled(), 0, allowed));
            Assert.Throws<ArgumentNullException>(() => SeatLooks.WithSeatStyle(w, null, 0, allowed));
        }

        Outfit Wearing(string hair, string eyes)
        {
            var look = Unstyled();
            if (hair != null) look.Pieces["Hair"] = hair;
            if (eyes != null) look.Pieces["Eyes"] = eyes;
            return look;
        }

        Outfit Seat(int seat, IReadOnlyList<Outfit> saved) => SeatLooks.WithSeatStyle(w, Unstyled(), seat, Career.FreePieces, saved);

        [Test]
        public void DefaultSeatsNeverCopyTheHairOrEyesOfASavedSeat()
        {
            var saved = new Outfit[CouchSeats];
            saved[0] = Wearing("HairBob", "EyesButton");
            var bots = Enumerable.Range(1, CouchSeats - 1).Select(seat => Seat(seat, saved)).ToList();
            CollectionAssert.AreEqual(new[] { "HairSwept", "HairWavy", "HairBuns" }, bots.Select(look => look.PieceIn("Hair")), "the three bots share out what player 1 left");
            CollectionAssert.AreEqual(new[] { "EyesSparkle", "EyesDot", "EyesHappy" }, bots.Select(look => look.PieceIn("Eyes")));
            Assert.IsFalse(bots.Any(look => look.PieceIn("Hair") == "HairBob" || look.PieceIn("Eyes") == "EyesButton"));
        }

        [Test]
        public void UnsavedSeatsStayDifferentWhereverTheSavedSeatsSit()
        {
            var saved = new Outfit[CouchSeats];
            saved[0] = Wearing("HairWavy", "EyesJelly");
            saved[2] = Wearing("HairBuns", "EyesStarry");
            Assert.AreEqual(0, SeatLooks.Rank(0, saved));
            Assert.AreEqual(0, SeatLooks.Rank(1, saved), "seat 2 is the first seat without a look");
            Assert.AreEqual(1, SeatLooks.Rank(3, saved), "seat 4 is the second");
            Assert.AreEqual(3, SeatLooks.Rank(3, null));
            Assert.AreEqual(-1, SeatLooks.Rank(-1, saved));
            var second = Seat(1, saved);
            var fourth = Seat(3, saved);
            Assert.AreEqual("HairSwept", second.PieceIn("Hair"));
            Assert.AreEqual("HairBob", fourth.PieceIn("Hair"));
            Assert.AreNotEqual(second.PieceIn("Eyes"), fourth.PieceIn("Eyes"));
            var hair = new[] { saved[0], second, saved[2], fourth }.Select(look => look.PieceIn("Hair")).ToList();
            Assert.AreEqual(CouchSeats, hair.Distinct().Count(), string.Join(",", hair));
        }

        [Test]
        public void ASaveFromBeforeHairExistedCountsAsTheBuiltInHairAndEyes()
        {
            var saved = new Outfit[CouchSeats];
            saved[0] = Wearing(null, null);
            CollectionAssert.AreEquivalent(new[] { "HairSwept" }, SeatLooks.WornByOthers(w, saved, 1, "Hair"));
            CollectionAssert.AreEquivalent(new[] { "EyesSparkle" }, SeatLooks.WornByOthers(w, saved, 1, "Eyes"));
            Assert.IsEmpty(SeatLooks.WornByOthers(w, saved, 0, "Hair"), "a seat does not avoid itself");
            Assert.IsEmpty(SeatLooks.WornByOthers(w, null, 1, "Hair"));
            var bot = Seat(1, saved);
            Assert.AreEqual("HairBob", bot.PieceIn("Hair"));
            Assert.AreEqual("EyesButton", bot.PieceIn("Eyes"));
        }

        [Test]
        public void WhenEveryStyleIsTakenASeatStillGetsOne()
        {
            var saved = new[] { Wearing("HairSwept", "EyesSparkle"), Wearing("HairBob", "EyesButton"), Wearing("HairWavy", "EyesDot"), Wearing("HairBuns", "EyesHappy"), null };
            var late = Seat(4, saved);
            Assert.IsTrue(Career.FreePieces.Contains(late.PieceIn("Hair")), "hair runs out first, so one is shared");
            Assert.IsFalse(saved.Where(look => look != null).Any(look => look.PieceIn("Eyes") == late.PieceIn("Eyes")), "eyes are still free to differ");
            var none = Enumerable.Range(0, CouchSeats).Select(seat => Seat(seat, new Outfit[CouchSeats])).Select(look => look.PieceIn("Hair"));
            CollectionAssert.AreEqual(new[] { "HairSwept", "HairBob", "HairWavy", "HairBuns" }, none, "no saves: the plain seat order");
        }

        [Test]
        public void SeatLooksSaveAndLoadLikeAnyOtherAndOldSavesStayAsTheyAre()
        {
            for (int seat = 0; seat < CouchSeats; seat++)
            {
                var styled = Seat(seat);
                string text = styled.Serialize();
                var loaded = Outfit.Deserialize(text);
                Assert.AreEqual(text, loaded.Serialize());
                Assert.IsTrue(w.Fits(loaded), text);
                Assert.AreSame(loaded, w.Sanitize(loaded));
            }
            const string old = "Back=Satchel:olive;Badge=TBadge;Bottoms=Joggers:olive;Footwear=Boots:forest;Gloves=Mittens:charcoal;Top=Hoodie:mint|";
            var saved = Outfit.Deserialize(old);
            Assert.AreSame(saved, w.Sanitize(saved), "a saved look without hair is used as it is");
            Assert.AreEqual(old, w.Sanitize(saved).Serialize(), "and keeps its bytes");
            Assert.IsNull(saved.PieceIn("Hair"));
        }
    }
}
