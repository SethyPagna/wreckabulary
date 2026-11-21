using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class DressingTests
    {
        static readonly string[] Slots = { "Hair", "Eyes", "Top", "Bottoms", "Gloves", "Footwear", "Headwear", "Face", "Back", "Badge" };

        static Outfit WithoutFallbacks(WardrobeCatalogue wardrobe)
        {
            var outfit = wardrobe.Default.Clone();
            foreach (string slot in wardrobe.FallbackSlots) outfit.Pieces.Remove(slot);
            Assert.IsTrue(wardrobe.Fits(outfit), "an outfit saved without hair and eyes still fits");
            return outfit;
        }

        [Test]
        public void WornIsTheStoredPieceOrTheDefaultOfAFallbackSlot()
        {
            var wardrobe = TestData.Wardrobe();
            var outfit = WithoutFallbacks(wardrobe);
            Assert.IsNull(outfit.PieceIn("Hair"));
            Assert.AreEqual("HairSwept", Dressing.Worn(wardrobe, outfit, "Hair"), "the hair the avatar draws anyway");
            Assert.AreEqual("EyesSparkle", Dressing.Worn(wardrobe, outfit, "Eyes"));
            Assert.AreEqual("Hoodie", Dressing.Worn(wardrobe, outfit, "Top"));
            Assert.IsNull(Dressing.Worn(wardrobe, outfit, "Face"), "an empty optional slot stays empty");
            outfit.Pieces["Hair"] = "HairBuns";
            Assert.AreEqual("HairBuns", Dressing.Worn(wardrobe, outfit, "Hair"), "a stored piece wins over the default");
            Assert.IsNull(Dressing.Worn(wardrobe, null, "Hair"));
            Assert.IsNull(Dressing.Worn(wardrobe, outfit, null));
        }

        [Test]
        public void ChoicesListNoneFirstOnlyForOptionalSlotsThenCatalogueOrder()
        {
            var wardrobe = TestData.Wardrobe();
            foreach (string slot in Slots)
            {
                var choices = Dressing.Choices(wardrobe, slot);
                var pieces = wardrobe.PiecesFor(slot).Select(piece => piece.Id).ToArray();
                bool optional = !wardrobe.RequiredSlots.Contains(slot);
                Assert.AreEqual(pieces.Length + (optional ? 1 : 0), choices.Count, slot);
                if (optional) Assert.IsNull(choices[0], slot + " starts with nothing");
                CollectionAssert.AreEqual(pieces, choices.Where(id => id != null).ToArray(), slot);
            }
            Assert.IsFalse(Dressing.Choices(wardrobe, "Hair").Contains(null), "hair is always chosen");
            Assert.IsFalse(Dressing.Choices(wardrobe, "Eyes").Contains(null), "eyes are always chosen");
            Assert.AreEqual(1, Dressing.Choices(wardrobe, "Bottoms").Count);
            Assert.AreEqual(11, Dressing.Choices(wardrobe, "Headwear").Count, "ten hats and nothing");
            Assert.IsEmpty(Dressing.Choices(wardrobe, "Tail"));
            Assert.AreEqual(38, Slots.Sum(slot => Dressing.Choices(wardrobe, slot).Count(id => id != null)), "every piece is offered once");
        }

        [Test]
        public void WearingAPlainPieceChangesOnlyItsSlot()
        {
            var wardrobe = TestData.Wardrobe();
            var change = Dressing.Wear(wardrobe, wardrobe.Default, "Face", "Glasses");
            Assert.IsTrue(change.Fits);
            Assert.AreEqual("Glasses", change.Outfit.PieceIn("Face"));
            Assert.IsEmpty(change.PutOn);
            Assert.IsEmpty(change.TakenOff);
            Assert.IsNull(wardrobe.Default.PieceIn("Face"), "the outfit passed in is not edited");
            var off = Dressing.Wear(wardrobe, change.Outfit, "Face", null);
            Assert.IsNull(off.Outfit.PieceIn("Face"));
            Assert.IsEmpty(off.TakenOff, "the slot you emptied yourself is not reported");
        }

        [Test]
        public void TheHoodBringsTheHoodieAndTheCrewneckTakesTheHoodOff()
        {
            var wardrobe = TestData.Wardrobe();
            Assert.AreEqual("Hood", wardrobe.Default.PieceIn("Headwear"));
            var crew = Dressing.Wear(wardrobe, wardrobe.Default, "Top", "Crewneck");
            Assert.AreEqual("Crewneck", crew.Outfit.PieceIn("Top"));
            Assert.IsNull(crew.Outfit.PieceIn("Headwear"));
            CollectionAssert.AreEqual(new[] { "Hood" }, crew.TakenOff.ToArray());
            Assert.IsEmpty(crew.PutOn);

            var hood = Dressing.Wear(wardrobe, crew.Outfit, "Headwear", "Hood");
            Assert.AreEqual("Hood", hood.Outfit.PieceIn("Headwear"));
            Assert.AreEqual("Hoodie", hood.Outfit.PieceIn("Top"));
            CollectionAssert.AreEqual(new[] { "Hoodie" }, hood.PutOn.ToArray());
            Assert.IsEmpty(hood.TakenOff, "the crewneck was replaced, not taken off");
            Assert.IsTrue(wardrobe.Fits(hood.Outfit));

            var cap = Dressing.Wear(wardrobe, wardrobe.Default, "Headwear", "Cap");
            Assert.AreEqual("Hoodie", cap.Outfit.PieceIn("Top"), "swapping the hood for a cap keeps the hoodie");
            Assert.IsEmpty(cap.PutOn);
            Assert.IsEmpty(cap.TakenOff);
        }

        [Test]
        public void WearMatchesTheCatalogueForEveryPieceOnTheDefaultOutfit()
        {
            var wardrobe = TestData.Wardrobe();
            foreach (string slot in Slots)
                foreach (string choice in Dressing.Choices(wardrobe, slot))
                {
                    var change = Dressing.Wear(wardrobe, wardrobe.Default, slot, choice);
                    Assert.IsTrue(change.Fits, slot + " " + choice);
                    Assert.AreEqual(choice, change.Outfit.PieceIn(slot), slot);
                    Assert.IsTrue(wardrobe.Fits(change.Outfit), slot + " " + choice);
                    Assert.AreEqual(wardrobe.Default.Serialize(), TestData.Wardrobe().Default.Serialize());
                }
        }

        [Test]
        public void WearRefusesWhatCannotFit()
        {
            var wardrobe = TestData.Wardrobe();
            Assert.IsFalse(Dressing.Wear(wardrobe, wardrobe.Default, "Top", null).Fits, "a required slot cannot be emptied");
            Assert.IsNull(Dressing.Wear(wardrobe, wardrobe.Default, "Top", null).Outfit);
            Assert.IsFalse(Dressing.Wear(wardrobe, wardrobe.Default, "Face", "Cap").Fits, "a hat is not a face piece");
            Assert.IsFalse(Dressing.Wear(wardrobe, wardrobe.Default, "Face", "Monocle").Fits, "unknown piece");
            Assert.IsFalse(Dressing.Wear(wardrobe, wardrobe.Default, "Tail", null).Fits, "unknown slot");
            Assert.IsFalse(Dressing.Wear(wardrobe, null, "Face", "Glasses").Fits);
            var refused = Dressing.Wear(wardrobe, wardrobe.Default, "Face", "Cap");
            Assert.IsEmpty(refused.PutOn);
            Assert.IsEmpty(refused.TakenOff);
        }

        [Test]
        public void TintableNamesThePaletteTheSwatchesComeFrom()
        {
            var wardrobe = TestData.Wardrobe();
            var outfit = WithoutFallbacks(wardrobe);
            Assert.IsTrue(Dressing.Tintable(wardrobe, outfit, "Hair", out string hair), "hair has colours before a style is stored");
            Assert.AreEqual("Hair", hair);
            Assert.IsTrue(Dressing.Tintable(wardrobe, outfit, "Bottoms", out string bottoms));
            Assert.AreEqual("Bottoms", bottoms);
            Assert.IsTrue(Dressing.Tintable(wardrobe, outfit, "Headwear", out string hood));
            Assert.AreEqual("Top", hood, "the hood matches the top");
            Assert.IsFalse(Dressing.Tintable(wardrobe, outfit, "Eyes", out string eyes), "eyes have no palette");
            Assert.IsNull(eyes);
            Assert.IsFalse(Dressing.Tintable(wardrobe, outfit, "Badge", out _), "badges are not tinted");
            Assert.IsFalse(Dressing.Tintable(wardrobe, outfit, "Face", out _), "nothing is worn there");
            var sprout = Dressing.Wear(wardrobe, outfit, "Headwear", "Sprout").Outfit;
            Assert.IsFalse(Dressing.Tintable(wardrobe, sprout, "Headwear", out _), "the sprout keeps its own colour");
            var cap = Dressing.Wear(wardrobe, outfit, "Headwear", "Cap").Outfit;
            Assert.IsTrue(Dressing.Tintable(wardrobe, cap, "Headwear", out string capPalette));
            Assert.AreEqual("Headwear", capPalette);
        }

        [Test]
        public void PaintingDefaultHairStoresTheDefaultPieceWithTheColour()
        {
            var wardrobe = TestData.Wardrobe();
            var outfit = WithoutFallbacks(wardrobe);
            string before = outfit.Serialize();
            var painted = Dressing.Paint(wardrobe, outfit, "Hair", "rose");
            Assert.IsNotNull(painted);
            Assert.AreEqual("rose", painted.ColourOf("Hair"));
            Assert.AreEqual("HairSwept", painted.PieceIn("Hair"), "the tint follows the normal piece path");
            Assert.IsNull(painted.PieceIn("Eyes"), "nothing else is filled in");
            Assert.AreEqual(before, outfit.Serialize(), "the outfit passed in is not edited");
            Assert.IsTrue(wardrobe.Fits(painted));
        }

        [Test]
        public void PaintChangesOnePartAndRefusesColoursOutsideThePalette()
        {
            var wardrobe = TestData.Wardrobe();
            var navy = Dressing.Paint(wardrobe, wardrobe.Default, "Bottoms", "navy");
            Assert.AreEqual("navy", navy.ColourOf("Bottoms"));
            Assert.AreEqual(wardrobe.Default.ColourOf("Top"), navy.ColourOf("Top"), "only the part picked changes");
            Assert.AreEqual(wardrobe.Default.PieceIn("Bottoms"), navy.PieceIn("Bottoms"));

            var hood = Dressing.Paint(wardrobe, wardrobe.Default, "Headwear", "mint");
            Assert.AreEqual("mint", hood.ColourOf("Top"), "painting the hood paints the top it matches");
            Assert.AreEqual(wardrobe.Default.ColourOf("Headwear"), hood.ColourOf("Headwear"));

            Assert.IsNull(Dressing.Paint(wardrobe, wardrobe.Default, "Bottoms", "tomato"), "tomato is not a bottoms colour");
            Assert.IsNull(Dressing.Paint(wardrobe, wardrobe.Default, "Bottoms", null));
            Assert.IsNull(Dressing.Paint(wardrobe, wardrobe.Default, "Eyes", "rose"), "eyes are not tinted");
            Assert.IsNull(Dressing.Paint(wardrobe, wardrobe.Default, "Face", "gold"), "an empty slot has nothing to paint");
        }

        [Test]
        public void OfferForMapsWhatIsMissingToWhatSellsIt()
        {
            Assert.AreEqual("look:candy", Dressing.OfferFor("piece:SweetBadge").Id);
            Assert.AreEqual("look:arcade", Dressing.OfferFor("piece:PixelBadge").Id);
            Assert.AreEqual("look:arcade", Dressing.OfferFor("piece:Headphones").Id);
            Assert.AreEqual("colour:sky", Dressing.OfferFor("colour:sky").Id);
            Assert.AreEqual(120, Dressing.OfferFor("colour:sky").Price);
            Assert.AreEqual("skin:Arcade", Dressing.OfferFor("skin:Arcade").Id);
            Assert.AreEqual("look:candy", Dressing.OfferFor("look:candy").Id);
            Assert.IsNull(Dressing.OfferFor("piece:Cap"), "a free piece is not sold");
            Assert.IsNull(Dressing.OfferFor("piece:HollyBadge"), "a free look has no offer");
            Assert.IsNull(Dressing.OfferFor("colour:tomato"), "a free colour is not sold");
            Assert.IsNull(Dressing.OfferFor("skin:Winter"));
            Assert.IsNull(Dressing.OfferFor("theme:candy"), "themes are not worn");
            Assert.IsNull(Dressing.OfferFor("SweetBadge"));
            Assert.IsNull(Dressing.OfferFor(""));
            Assert.IsNull(Dressing.OfferFor(null));
        }

        [Test]
        public void NeededListsEachOfferOnceAndSkipsWhatALookAlreadyBrings()
        {
            var wardrobe = TestData.Wardrobe();
            var career = new Career { Coins = 2000 };
            Assert.IsEmpty(Dressing.Needed(career, wardrobe.Default), "the default outfit is free");

            var sky = Dressing.Paint(wardrobe, wardrobe.Default, "Top", "sky");
            CollectionAssert.AreEqual(new[] { "colour:sky" }, Dressing.Needed(career, sky).Select(offer => offer.Id).ToArray());

            var sweet = Dressing.Wear(wardrobe, wardrobe.Default, "Badge", "SweetBadge").Outfit;
            sweet.Colours["Top"] = "bubblegum";
            sweet.ItemSkins["BAT"] = "Candy";
            sweet.ItemSkins["SHIELD"] = "Arcade";
            CollectionAssert.AreEqual(new[] { "look:candy", "skin:Arcade" }, Dressing.Needed(career, sweet).Select(offer => offer.Id).ToArray(),
                "Sugar Rush brings Bubblegum and Candy gear, so they are not asked for twice");

            var arcade = Dressing.Wear(wardrobe, wardrobe.Default, "Badge", "PixelBadge").Outfit;
            arcade = Dressing.Wear(wardrobe, arcade, "Headwear", "Headphones").Outfit;
            CollectionAssert.AreEqual(new[] { "look:arcade" }, Dressing.Needed(career, arcade).Select(offer => offer.Id).ToArray(),
                "two pieces from one look are one offer");

            Assert.IsNull(career.Buy("look:candy"));
            CollectionAssert.AreEqual(new[] { "skin:Arcade" }, Dressing.Needed(career, sweet).Select(offer => offer.Id).ToArray());
            Assert.IsNull(career.Buy("skin:Arcade"));
            Assert.IsEmpty(Dressing.Needed(career, sweet));
            Assert.IsEmpty(career.Missing(sweet), "nothing needed means nothing missing");
        }

        [Test]
        public void TheLookPriceIsWhatIsLeftToPay()
        {
            var wardrobe = TestData.Wardrobe();
            var career = new Career { Coins = 2000 };
            var sweet = Dressing.Wear(wardrobe, wardrobe.Default, "Badge", "SweetBadge").Outfit;
            var offer = Dressing.Needed(career, sweet).Single();
            Assert.AreEqual(400, career.PriceOf(offer));
            Assert.IsNull(career.Buy("colour:bubblegum"));
            Assert.AreEqual(250, career.PriceOf(Dressing.Needed(career, sweet).Single()), "the colour already bought comes off the look");
        }
    }
}
