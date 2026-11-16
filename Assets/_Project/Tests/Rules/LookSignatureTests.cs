using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class LookSignatureTests
    {
        WardrobeCatalogue w;

        [SetUp]
        public void Load() => w = TestData.Wardrobe();

        Outfit Worn(CosmeticBundle look) => look.Apply(w.Default, w);

        static string Shape(Outfit outfit, bool withBadge) => string.Join(",", outfit.Pieces
            .Where(kv => withBadge || kv.Key != "Badge").OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Value));

        [Test]
        public void NoTwoLooksShareASilhouetteEvenWithoutTheirBadges()
        {
            var shapes = CosmeticBundles.All.ToDictionary(look => look.Id, look => Shape(Worn(look), false));
            Assert.AreEqual(shapes.Count, shapes.Values.Distinct().Count(), string.Join("\n", shapes.Select(kv => kv.Key + ": " + kv.Value)));
            var meshes = CosmeticBundles.All.Select(look => Shape(Worn(look), true)).ToList();
            Assert.AreEqual(meshes.Count, meshes.Distinct().Count());
            Assert.AreNotEqual(shapes["sunroom"], shapes["arcade"]);
            Assert.AreNotEqual(shapes["courier"], shapes["winter"]);
        }

        [Test]
        public void EveryLookWearsItsOwnEmblemBadge()
        {
            Assert.AreEqual(CosmeticBundles.All.Count, CosmeticBundles.All.Select(look => look.Signature).Distinct().Count());
            Assert.AreEqual(CosmeticBundles.All.Count, CosmeticBundles.All.Select(look => look.Emblem).Distinct().Count());
            Assert.AreEqual(CosmeticBundles.All.Count, CosmeticBundles.All.Select(look => look.Name).Distinct().Count());
            foreach (var look in CosmeticBundles.All)
            {
                var badge = w.Piece(look.Signature);
                Assert.IsNotNull(badge, look.Id);
                Assert.AreEqual("Badge", badge.Slot, look.Id);
                Assert.IsFalse(badge.Letter, "an emblem is not the Letter badge");
                Assert.IsNotEmpty(badge.Rigid, look.Id);
                Assert.AreEqual(look.Signature, Worn(look).PieceIn("Badge"), look.Id);
                Assert.AreEqual(CosmeticBundle.EmblemFolder + look.Id, look.Emblem);
                Assert.IsNotEmpty(look.Tagline, look.Id);
                Assert.LessOrEqual(look.Tagline.Length, 60, look.Id);
                Assert.IsNotEmpty(look.Collection, look.Id);
                Assert.AreNotEqual("Arcade Social", look.Collection, look.Id);
                foreach (string piece in look.Exclusive)
                {
                    Assert.IsNotNull(w.Piece(piece), look.Id + " grants " + piece);
                    Assert.AreSame(look, CosmeticBundles.GrantedBy(piece), piece + " belongs to one look");
                    Assert.IsFalse(Career.FreePieces.Contains(piece), piece + " is exclusive");
                }
            }
            var emblems = w.PiecesFor("Badge").Where(p => !p.Letter).Select(p => p.Id).ToList();
            CollectionAssert.AreEquivalent(CosmeticBundles.All.Select(look => look.Signature), emblems, "every emblem in the wardrobe is some look's signature");
            Assert.IsNull(CosmeticBundles.GrantedBy("TBadge"));
            Assert.IsNull(CosmeticBundles.GrantedBy("Satchel"));
        }

        [Test]
        public void LooksDifferBelowTheWaistAndOnTheBack()
        {
            var worn = CosmeticBundles.All.Select(Worn).ToList();
            Assert.AreEqual(worn.Count, worn.Select(o => o.ColourOf("Bottoms")).Distinct().Count(), "bottoms colour");
            Assert.AreEqual(worn.Count, worn.Select(o => o.ColourOf("Footwear")).Distinct().Count(), "boots colour");
            Assert.AreEqual(worn.Count, worn.Select(o => o.PieceIn("Back") + ":" + o.ColourOf("Back")).Distinct().Count(), "bag");
            foreach (var o in worn)
                foreach (var kv in o.Colours) Assert.IsNotNull(w.Colour(kv.Key, kv.Value), kv.Key + " " + kv.Value);
        }

        static List<Srgb> SeatColours()
        {
            string asset = File.ReadAllText(Path.Combine(TestData.Root, "Assets", "_Project", "Resources", "GameAssets.asset"));
            string block = asset.Substring(asset.IndexOf("playerColors:", StringComparison.Ordinal));
            block = block.Substring(0, block.IndexOf("playerInitials:", StringComparison.Ordinal));
            float F(Group g) => float.Parse(g.Value, CultureInfo.InvariantCulture);
            return Regex.Matches(block, @"r: ([\d.]+), g: ([\d.]+), b: ([\d.]+)").Cast<Match>()
                .Select(m => new Srgb(F(m.Groups[1]), F(m.Groups[2]), F(m.Groups[3]))).ToList();
        }

        [Test]
        public void DefaultSeatOutfitsAreEasyToTellApart()
        {
            var seats = SeatColours();
            Assert.AreEqual(4, seats.Count);
            var tops = SeatLooks.TopColours(w.Palettes["Top"], seats, Career.FreeColours);
            CollectionAssert.AreEqual(new[] { "tomato", "pool", "mint", "sunflower" }, tops);
            var colours = tops.Select(id => Srgb.Of(w.Colour("Top", id))).ToList();
            for (int a = 0; a < 4; a++)
            {
                Assert.IsTrue(Career.FreeColours.Contains(tops[a]), tops[a] + " is free");
                for (int b = 0; b < 4; b++)
                {
                    if (a == b) continue;
                    Assert.GreaterOrEqual(SeatLooks.DeltaE76(colours[a], colours[b]), SeatLooks.SeatGap, $"P{a + 1} vs P{b + 1} tops");
                    Assert.GreaterOrEqual(SeatLooks.DeltaE76(colours[a], seats[b]), SeatLooks.OtherSeatGap, $"P{a + 1} top vs P{b + 1} colour");
                }
            }
            Assert.Less(SeatLooks.DeltaE76(new Srgb(.5f, .5f, .5f), new Srgb(.5f, .5f, .5f)), 1e-9);
            Assert.AreEqual(100, SeatLooks.Lab(new Srgb(1, 1, 1)).L, .01);
        }

        [Test]
        public void SeatColoursFallBackToTheNearestWhenNothingFits()
        {
            var palette = new List<Colourway> { new Colourway { Id = "a", Name = "A", R = 1, G = 0, B = 0 } };
            var seats = new[] { new Srgb(1, 0, 0), new Srgb(.98f, 0, 0) };
            CollectionAssert.AreEqual(new[] { "a", "a" }, SeatLooks.TopColours(palette, seats, new[] { "a" }));
            Assert.IsEmpty(SeatLooks.TopColours(palette, seats, new string[0]));
        }

        IEnumerable<Outfit> EveryCombination()
        {
            var optional = new[] { "Headwear", "Face", "Back", "Badge" };
            foreach (var top in w.PiecesFor("Top"))
            foreach (string headwear in Choices("Headwear"))
            foreach (string face in Choices("Face"))
            foreach (string back in Choices("Back"))
            foreach (string badge in Choices("Badge"))
            {
                var o = w.Default.Clone();
                o.Pieces["Top"] = top.Id;
                var picks = new[] { headwear, face, back, badge };
                for (int i = 0; i < optional.Length; i++)
                    if (picks[i] == null) o.Pieces.Remove(optional[i]); else o.Pieces[optional[i]] = picks[i];
                yield return o;
            }
        }

        IEnumerable<string> Choices(string slot) => new string[] { null }.Concat(w.PiecesFor(slot).Select(p => p.Id));

        [Test]
        public void EveryPieceCombinationIsValidOrRejectedByARule()
        {
            int valid = 0, rejected = 0;
            foreach (var outfit in EveryCombination())
            {
                var problems = w.Problems(outfit);
                bool hoodWithoutHoodie = outfit.PieceIn("Headwear") == "Hood" && outfit.PieceIn("Top") != "Hoodie";
                Assert.AreEqual(hoodWithoutHoodie, problems.Count > 0, outfit.Serialize() + ": " + string.Join("; ", problems));
                foreach (string problem in problems) StringAssert.Contains(" needs ", problem, "only compatibility rules reject a complete outfit");
                if (problems.Count == 0) valid++; else rejected++;
                Assert.IsTrue(w.Fits(w.Sanitize(outfit)));
                foreach (var piece in w.Pieces)
                {
                    var next = w.Wear(w.Sanitize(outfit), piece.Slot, piece.Id);
                    if (next != null) Assert.IsTrue(w.Fits(next), piece.Id);
                }
                float lift = w.LiftFor(outfit, "Badge");
                Assert.AreEqual(outfit.PieceIn("Back") == "MessengerBag" ? .012f : 0f, lift, 1e-6f, "a badge rides on the messenger strap");
                Assert.AreEqual(0f, w.LiftFor(outfit, "Headwear"));
            }
            Assert.Greater(valid, 300);
            Assert.Greater(rejected, 0);
        }

        [Test]
        public void OneHeadwearAtATimeAndEveryNewPieceHangsFromABone()
        {
            Assert.IsEmpty(w.Validate());
            CollectionAssert.IsSubsetOf(new[] { "Cap", "Hood", "Headphones", "Beanie" }, w.PiecesFor("Headwear").Select(p => p.Id).ToList());
            var crewneck = w.Wear(w.Default, "Top", "Crewneck");
            CollectionAssert.IsSubsetOf(new[] { "Cap", "Headphones", "Beanie" }, w.Choices(crewneck, "Headwear"));
            var beanie = w.Wear(w.Default, "Headwear", "Beanie");
            Assert.AreEqual("Beanie", beanie.PieceIn("Headwear"));
            Assert.AreEqual("Headphones", w.Wear(beanie, "Headwear", "Headphones").PieceIn("Headwear"), "a new hat replaces the old one");
            foreach (var piece in w.Pieces.Where(p => !string.IsNullOrEmpty(p.Rigid)))
                CollectionAssert.Contains(new[] { "head", "chest" }, piece.Bone, piece.Id);
            foreach (var piece in w.Pieces)
            {
                Assert.IsNotEmpty(piece.Name, piece.Id + " has a player-facing name");
                Assert.AreEqual(piece.Name, w.NameOf(piece.Id));
            }
            Assert.AreEqual("Letter badge", w.NameOf("TBadge"));
            Assert.AreEqual("Nope", w.NameOf("Nope"));
            Assert.AreEqual(1, w.Pieces.Count(p => p.Letter));
            Assert.AreEqual("SK_Badge_T", w.Piece("TBadge").Mesh, "the skinned T stays as the fallback");
        }

        [Test]
        public void OldOutfitStringsLoadAndSaveByteForByte()
        {
            var legacy = new[]
            {
                w.Default.Serialize(),
                "Back=Satchel:olive;Badge=TBadge;Bottoms=Joggers:olive;Face=:classic;Footwear=Boots:forest;Gloves=Mittens:charcoal;Headwear=Hood:mint;Top=Hoodie:mint|",
                "Back=Satchel:navy;Badge=TBadge;Bottoms=Joggers:charcoal;Face=Glasses:sky;Footwear=Boots:navy;Gloves=Mittens:ink;Headwear=Cap:charcoal;Top=Crewneck:grape|SHIELD=Arcade",
            };
            foreach (string text in legacy)
            {
                var o = Outfit.Deserialize(text);
                Assert.AreEqual(text, o.Serialize());
                Assert.IsNull(o.Letter);
                Assert.IsTrue(w.Fits(o), text);
                Assert.AreSame(o, w.Sanitize(o));
            }
            Assert.IsFalse(w.Default.Serialize().Contains("||"));
        }

        [Test]
        public void TheLetterBadgeShowsTheSeatInitialUntilAPlayerPicksALetter()
        {
            var o = w.Default.Clone();
            CollectionAssert.AreEqual(new[] { "W", "O", "R", "D" }, Enumerable.Range(0, 4).Select(seat => LetterBadge.For(o, seat)));
            Assert.AreEqual("W", LetterBadge.For(o, 4));
            Assert.AreEqual(26, LetterBadge.Letters.Count);
            Assert.AreEqual("O", LetterBadge.For(LetterBadge.ForSeat(o, 1), 0), "the outfit's own seat wins");
            var picked = LetterBadge.With(o, "Q");
            Assert.AreEqual("Q", LetterBadge.For(picked, 2));
            Assert.IsNull(o.Letter, "picking never mutates the worn outfit");
            string text = picked.Serialize();
            Assert.AreEqual(o.Serialize() + "|Q", text);
            Assert.AreEqual("Q", Outfit.Deserialize(text).Letter);
            Assert.AreEqual(text, Outfit.Deserialize(text).Serialize());
            Assert.AreEqual(o.Serialize(), LetterBadge.With(picked, null).Serialize());
            Assert.IsNull(LetterBadge.With(o, "q"));
            Assert.IsNull(LetterBadge.With(o, "QQ"));
            foreach (string bad in new[] { "|q", "|QQ", "|1", "|" })
                Assert.IsNull(Outfit.Deserialize(o.Serialize() + bad).Letter, bad);
            var seated = LetterBadge.ForSeat(picked, 3);
            Assert.AreEqual(picked.Serialize(), seated.Serialize(), "the seat is never saved");
            Assert.AreEqual(3, seated.Clone().Seat);
            var broken = seated.Clone();
            broken.Pieces["Top"] = "Tuxedo";
            var safe = w.Sanitize(broken);
            Assert.AreEqual("Q", safe.Letter);
            Assert.AreEqual(3, safe.Seat);
        }

        [Test]
        public void ExclusivePiecesCameWithTheirLook()
        {
            var career = new Career();
            Assert.IsTrue(career.Owns("piece", "SunBadge"), "free looks grant their signature");
            Assert.IsTrue(career.Owns("piece", "MessengerBag"));
            Assert.IsTrue(career.Owns("piece", "Beanie"));
            Assert.IsFalse(career.Owns("piece", "PixelBadge"));
            Assert.IsFalse(career.Owns("piece", "Headphones"));
            Assert.IsFalse(career.Owns("piece", "SweetBadge"));
            Assert.IsFalse(career.Owns("piece", "Tuxedo"));
            var arcade = Worn(CosmeticBundles.Find("arcade"));
            CollectionAssert.AreEquivalent(new[] { "piece:Headphones", "piece:PixelBadge", "colour:grape", "skin:Arcade" }, career.Missing(arcade));
            Assert.IsEmpty(career.Missing(Worn(CosmeticBundles.Find("courier"))));
            Assert.IsEmpty(career.Missing(w.Default));
            career.Coins = 550;
            Assert.IsNull(career.Buy("look:arcade"));
            Assert.IsEmpty(career.Missing(arcade));
            var loaded = Career.Deserialize(career.Serialize());
            Assert.IsTrue(loaded.Owns("piece", "Headphones"));
            Assert.IsTrue(loaded.Owns("piece", "PixelBadge"));
            Assert.IsFalse(loaded.Owns("piece", "SweetBadge"));
        }

        [Test]
        public void OldCareerSavesLoadUnchangedAndKeepStaysAMigrationPath()
        {
            const string saved = "{\"name\":\"Housemate\",\"coins\":42,\"xp\":350,\"matches\":3,\"wins\":1,\"owned\":[\"skin:Candy\",\"colour:bubblegum\",\"look:candy\",\"theme:lantern\"],\"bests\":{\"Dibs\":120},\"history\":[]}";
            var career = Career.Deserialize(saved);
            Assert.AreEqual(saved, career.Serialize());
            Assert.IsTrue(career.Owns("look", "candy"));
            Assert.IsTrue(career.Owns("piece", "SweetBadge"));
            Assert.IsFalse(career.Owns("piece", "PixelBadge"));
            Assert.IsFalse(career.Keep("Classic", "tomato"));
            Assert.IsFalse(career.Keep("Winter", "mint"), "free finishes and colours are never written to the save");
            Assert.IsFalse(career.Keep("Invented", "invented"), "only real shop items can be kept");
            Assert.AreEqual(saved, career.Serialize());
            Assert.IsTrue(Career.Shop.All(offer => offer.Kind != "piece"), "Keep cannot reach signature pieces");
            CollectionAssert.AreEqual(new[] { "look:candy", "look:arcade" }, Career.Shop.Where(o => o.Kind == "look").Select(o => o.Id));
            Assert.AreEqual(400, Career.Shop.First(o => o.Id == "look:candy").Price);
            Assert.AreEqual(550, Career.Shop.First(o => o.Id == "look:arcade").Price);
        }
    }
}
