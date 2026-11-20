using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class SavedLooksTests
    {
        const string Beach = "Back=Satchel:canvas;Badge=TBadge;Bottoms=Joggers:denim;Footwear=Boots:cherry;Gloves=Mittens:putty;Hair=HairBob:honey;Headwear=Cap:sunflower;Top=Crewneck:sunflower|BAT=Classic|W";
        const string Cosy = "Bottoms=Joggers:cocoa;Footwear=Boots:tan;Gloves=Mittens:ink;Headwear=Hood;Top=Hoodie:tomato|";

        static string DefaultText() => TestData.Wardrobe().Default.Serialize();

        static SavedLooks Filled(int count)
        {
            var looks = new SavedLooks();
            for (int i = 0; i < count; i++) Assert.IsNull(looks.Add("Look " + (i + 1), i % 2 == 0 ? Beach : Cosy));
            return looks;
        }

        [Test]
        public void ANewCollectionIsEmptyWithSixSlots()
        {
            var looks = new SavedLooks();
            Assert.AreEqual(6, SavedLooks.Slots);
            Assert.AreEqual(0, looks.Count);
            Assert.IsFalse(looks.IsFull);
            Assert.IsEmpty(looks.All);
            Assert.IsNull(looks[0]);
            Assert.IsNull(looks[-1]);
            Assert.IsFalse(looks.Has(0));
        }

        [Test]
        public void AddKeepsTheExactOutfitStringUnderACleanName()
        {
            var looks = new SavedLooks();
            Assert.IsNull(looks.Add("  Beach day  ", Beach));
            Assert.AreEqual(1, looks.Count);
            Assert.AreEqual("Beach day", looks[0].Name);
            Assert.AreEqual(Beach, looks[0].OutfitText);
            Assert.AreEqual(Beach, looks[0].ToOutfit().Serialize(), "the stored string is the online outfit format");
        }

        [Test]
        public void AddingAnOutfitObjectStoresItsSerializedForm()
        {
            var wardrobe = TestData.Wardrobe();
            var outfit = LetterBadge.With(wardrobe.Wear(wardrobe.Default, "Headwear", "Cap"), "Q");
            outfit.ItemSkins["BAT"] = "Candy";
            var looks = new SavedLooks();
            Assert.IsNull(looks.Add("Capped", outfit));
            Assert.AreEqual(outfit.Serialize(), looks[0].OutfitText);
            Assert.AreEqual(0, looks.IndexOf(outfit));
            Assert.AreEqual(SavedLooks.NotAnOutfit, looks.Add("Nothing", (Outfit)null));
            Assert.AreEqual(SavedLooks.NotAnOutfit, looks.Add("Bare", new Outfit()));
            Assert.AreEqual(1, looks.Count);
        }

        [Test]
        public void TheSeventhLookIsRefusedAndNothingChanges()
        {
            var looks = Filled(SavedLooks.Slots);
            Assert.IsTrue(looks.IsFull);
            string before = looks.Serialize();
            Assert.AreEqual(SavedLooks.Full, looks.Add("One more", Beach));
            Assert.AreEqual("All 6 look slots are full.", SavedLooks.Full);
            Assert.AreEqual(SavedLooks.Slots, looks.Count);
            Assert.AreEqual(before, looks.Serialize());
            Assert.IsNull(looks.Remove(0));
            Assert.IsNull(looks.Add("One more", Beach), "a freed slot can be used again");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void ANameWithNothingToShowIsRefused(string name)
        {
            var looks = new SavedLooks();
            Assert.IsNull(SavedLooks.CleanName(name));
            Assert.AreEqual(SavedLooks.NeedsName, looks.Add(name, Beach));
            Assert.AreEqual(0, looks.Count);
        }

        [TestCase("A", "A")]
        [TestCase("  Party  ", "Party")]
        [TestCase("Two  words", "Two  words")]
        [TestCase("Caf\u00e9 \u96ea", "Caf\u00e9 \u96ea")]
        [TestCase("a/b,c%d", "a/b,c%d")]
        [TestCase("1234567890123456", "1234567890123456")]
        [TestCase("12345678901234567", "1234567890123456")]
        [TestCase("123456789012345 7", "123456789012345")]
        [TestCase("Hi \ud83d\ude00", "Hi \ud83d\ude00")]
        [TestCase("123456789012345\ud83d\ude00", "123456789012345")]
        public void NamesAreTrimmedStrippedOfControlCharactersAndCutAtSixteen(string raw, string expected)
        {
            Assert.AreEqual(expected, SavedLooks.CleanName(raw));
            Assert.LessOrEqual(SavedLooks.CleanName(raw).Length, SavedLooks.NameLength);
            Assert.AreEqual(expected, SavedLooks.CleanName(expected), "a clean name stays as it is");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("|")]
        [TestCase("|BAT=Candy|W")]
        [TestCase("not an outfit")]
        [TestCase("=Hoodie")]
        public void TextWithoutAnyWornPieceIsNotAnOutfit(string text)
        {
            var looks = new SavedLooks();
            Assert.IsFalse(SavedLooks.IsOutfit(text));
            Assert.AreEqual(SavedLooks.NotAnOutfit, looks.Add("Name", text));
            Assert.AreEqual(0, looks.Count);
        }

        [Test]
        public void ControlCharactersAndBrokenSurrogatesNeverReachALook()
        {
            foreach (string name in new[] { "\t\r\n", "\u0000\u0007\u001f", "\u007f\u0085", "\ud83d", "\ude00\ud83d", "\u2028\u2029" })
            {
                Assert.IsNull(SavedLooks.CleanName(name));
                Assert.AreEqual(SavedLooks.NeedsName, new SavedLooks().Add(name, Beach));
            }
            Assert.AreEqual("Raincoat", SavedLooks.CleanName("Rain\tcoat\n"));
            Assert.AreEqual("Hi  there", SavedLooks.CleanName("Hi \ud83d there \ude00"));
            Assert.AreEqual("ab", SavedLooks.CleanName("a\u2028b"));
            foreach (string outfit in new[] { "Top=Hoodie\n", "Top=Hoodie\u0000", "Top=Hoodie|\u001f", "Top=Hood\ud83die", "Top=Hoodie\u2028|" })
            {
                Assert.IsFalse(SavedLooks.IsOutfit(outfit));
                Assert.AreEqual(SavedLooks.NotAnOutfit, new SavedLooks().Add("Name", outfit));
            }
            foreach (string stored in new[] { "1/Name,Top=Hoodie\n|", "1/\u0000,Top=Hoodie|", "1/%d83d,Top=Hoodie|", "1/Name,Top=Hoodie%d83d|" })
            {
                Assert.IsTrue(SavedLooks.TryDeserialize(stored, out var looks));
                Assert.AreEqual(0, looks.Count);
            }
            var kept = SavedLooks.Deserialize("1/Na%0009me\u0001,Top=Hoodie|");
            Assert.AreEqual("Name", kept[0].Name, "a stored name is cleaned again on load");
        }

        [Test]
        public void AnOutfitLongerThanTheLimitIsNotStored()
        {
            string longest = "Top=Hoodie|" + string.Join(";", Enumerable.Range(0, 400).Select(i => "I" + i + "=S"));
            Assert.Greater(longest.Length, SavedLooks.MaxOutfitLength);
            Assert.IsFalse(SavedLooks.IsOutfit(longest));
            Assert.IsTrue(SavedLooks.IsOutfit(longest.Substring(0, SavedLooks.MaxOutfitLength)));
        }

        [Test]
        public void TwoLooksMayShareANameAndAnOutfit()
        {
            var looks = new SavedLooks();
            Assert.IsNull(looks.Add("Twin", Beach));
            Assert.IsNull(looks.Add("Twin", Beach));
            Assert.IsNull(looks.Add("twin", Cosy));
            Assert.AreEqual(new[] { "Twin", "Twin", "twin" }, looks.All.Select(look => look.Name).ToArray());
            Assert.AreEqual(0, looks.IndexOf(Beach), "the first match wins");
            Assert.AreEqual(2, looks.IndexOf(Cosy));
            Assert.AreEqual(-1, looks.IndexOf(DefaultText()));
            Assert.AreEqual(-1, looks.IndexOf((string)null));
            Assert.AreEqual(-1, looks.IndexOf((Outfit)null));
        }

        [Test]
        public void TheSuggestedNameIsTheFirstFreeNumber()
        {
            var looks = new SavedLooks();
            Assert.AreEqual("Look 1", looks.SuggestName());
            Assert.IsNull(looks.Add(looks.SuggestName(), Beach));
            Assert.IsNull(looks.Add("Look 3", Beach));
            Assert.AreEqual("Look 2", looks.SuggestName());
            Assert.IsNull(looks.Add(looks.SuggestName(), Beach));
            Assert.AreEqual("Look 4", looks.SuggestName());
            Assert.IsNull(looks.Remove(0));
            Assert.AreEqual("Look 1", looks.SuggestName());
            var full = Filled(SavedLooks.Slots);
            Assert.AreEqual("Look 7", full.SuggestName());
            Assert.LessOrEqual(full.SuggestName().Length, SavedLooks.NameLength);
        }

        [Test]
        public void ReplaceKeepsTheNameAndRenameKeepsTheOutfit()
        {
            var looks = Filled(3);
            Assert.IsNull(looks.Replace(1, Beach));
            Assert.AreEqual("Look 2", looks[1].Name);
            Assert.AreEqual(Beach, looks[1].OutfitText);
            Assert.IsNull(looks.Rename(1, " Sunny "));
            Assert.AreEqual("Sunny", looks[1].Name);
            Assert.AreEqual(Beach, looks[1].OutfitText);
            Assert.AreEqual(3, looks.Count);
            Assert.AreEqual("Look 1", looks[0].Name);
            Assert.AreEqual("Look 3", looks[2].Name);
            var wardrobe = TestData.Wardrobe();
            Assert.IsNull(looks.Replace(0, wardrobe.Default));
            Assert.AreEqual(wardrobe.Default.Serialize(), looks[0].OutfitText);
        }

        [Test]
        public void RemoveClosesTheGapAndKeepsTheOrder()
        {
            var looks = Filled(4);
            Assert.IsNull(looks.Remove(1));
            Assert.AreEqual(new[] { "Look 1", "Look 3", "Look 4" }, looks.All.Select(look => look.Name).ToArray());
            Assert.IsNull(looks.Remove(2));
            Assert.IsNull(looks.Remove(0));
            Assert.AreEqual(new[] { "Look 3" }, looks.All.Select(look => look.Name).ToArray());
        }

        [TestCase(-1)]
        [TestCase(2)]
        [TestCase(6)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        public void ChangesToASlotThatHoldsNoLookAreRefused(int index)
        {
            var looks = Filled(2);
            string before = looks.Serialize();
            Assert.AreEqual(SavedLooks.NoSuchLook, looks.Replace(index, Beach));
            Assert.AreEqual(SavedLooks.NoSuchLook, looks.Rename(index, "Name"));
            Assert.AreEqual(SavedLooks.NoSuchLook, looks.Remove(index));
            Assert.IsNull(looks[index]);
            Assert.AreEqual(before, looks.Serialize());
        }

        [Test]
        public void ABadReplacementOrNameLeavesTheLookAlone()
        {
            var looks = Filled(1);
            string before = looks.Serialize();
            Assert.AreEqual(SavedLooks.NotAnOutfit, looks.Replace(0, "|"));
            Assert.AreEqual(SavedLooks.NotAnOutfit, looks.Replace(0, (Outfit)null));
            Assert.AreEqual(SavedLooks.NeedsName, looks.Rename(0, "  "));
            Assert.AreEqual(before, looks.Serialize());
        }

        [Test]
        public void TheStoredFormIsOneVersionedLine()
        {
            var looks = new SavedLooks();
            Assert.AreEqual("1", looks.Serialize());
            Assert.IsNull(looks.Add("Beach day", Cosy));
            Assert.IsNull(looks.Add("50% off, w/ hat", "Top=Hoodie|"));
            Assert.AreEqual("1/Beach day," + Cosy + "/50%0025 off%002c w%002f hat,Top=Hoodie|", looks.Serialize());
            Assert.IsFalse(looks.Serialize().Any(char.IsControl));
        }

        [Test]
        public void ARoundTripKeepsEveryNameAndOutfitInOrder()
        {
            var looks = new SavedLooks();
            string[] names = { "Beach day", "a/b,c%d", "%0041", "Caf\u00e9 \u96ea", "Hi \ud83d\ude00", "," };
            string[] outfits = { Beach, Cosy, DefaultText(), "Top=Hoodie|", "Top=Crewneck:sky;Face=Glasses|SHIELD=Arcade;BAT=Candy|Z", Beach };
            for (int i = 0; i < names.Length; i++) Assert.IsNull(looks.Add(names[i], outfits[i]), names[i]);
            string stored = looks.Serialize();
            Assert.IsTrue(SavedLooks.TryDeserialize(stored, out var loaded));
            Assert.AreEqual(names, loaded.All.Select(look => look.Name).ToArray());
            Assert.AreEqual(outfits, loaded.All.Select(look => look.OutfitText).ToArray());
            Assert.AreEqual(stored, loaded.Serialize(), "saving again writes the same bytes");
            Assert.AreEqual(stored, SavedLooks.Deserialize(stored).Serialize());
        }

        [Test]
        public void EveryShopLookSurvivesARoundTripByteForByte()
        {
            var wardrobe = TestData.Wardrobe();
            var looks = new SavedLooks();
            foreach (var bundle in CosmeticBundles.All)
                Assert.IsNull(looks.Add(bundle.Name, bundle.Apply(wardrobe.Default, wardrobe)), bundle.Id);
            Assert.AreEqual(SavedLooks.Slots, looks.Count);
            var loaded = SavedLooks.Deserialize(looks.Serialize());
            for (int i = 0; i < CosmeticBundles.All.Count; i++)
            {
                var bundle = CosmeticBundles.All[i];
                string expected = bundle.Apply(wardrobe.Default, wardrobe).Serialize();
                Assert.AreEqual(expected, loaded[i].OutfitText, bundle.Id);
                Assert.AreEqual(expected, loaded[i].ToOutfit().Serialize(), bundle.Id);
                Assert.LessOrEqual(loaded[i].Name.Length, SavedLooks.NameLength);
            }
        }

        [TestCase(null)]
        [TestCase("")]
        public void NothingStoredLoadsAsAnEmptyCollection(string text)
        {
            Assert.IsTrue(SavedLooks.TryDeserialize(text, out var looks));
            Assert.AreEqual(0, looks.Count);
            Assert.AreEqual(0, SavedLooks.Deserialize(text).Count);
        }

        [TestCase(" ")]
        [TestCase("garbage")]
        [TestCase("0")]
        [TestCase("2/Name,Top=Hoodie|")]
        [TestCase("01/Name,Top=Hoodie|")]
        [TestCase("/Name,Top=Hoodie|")]
        [TestCase("{\"v\":1,\"looks\":[]}")]
        [TestCase("Top=Hoodie|")]
        public void TextThatIsNotASavedLooksLineLoadsEmptyAndSaysSo(string text)
        {
            Assert.IsFalse(SavedLooks.TryDeserialize(text, out var looks));
            Assert.IsNotNull(looks);
            Assert.AreEqual(0, looks.Count);
            Assert.AreEqual(0, SavedLooks.Deserialize(text).Count);
        }

        [TestCase("1/")]
        [TestCase("1//")]
        [TestCase("1/NameOnly")]
        [TestCase("1/,Top=Hoodie|")]
        [TestCase("1/   ,Top=Hoodie|")]
        [TestCase("1/Name,")]
        [TestCase("1/Name,|")]
        [TestCase("1/Name,not an outfit")]
        [TestCase("1/Na%zzzzme,Top=Hoodie|")]
        [TestCase("1/Na%00,Top=Hoodie|")]
        [TestCase("1/Name%,Top=Hoodie|")]
        [TestCase("1/Name,Top=Hoodie|%")]
        [TestCase("1/Name,Top=Hoodie%000a|")]
        [TestCase("1/%0009%000a,Top=Hoodie|")]
        public void ABrokenEntryIsDroppedWithoutThrowing(string text)
        {
            Assert.IsTrue(SavedLooks.TryDeserialize(text, out var looks));
            Assert.AreEqual(0, looks.Count);
        }

        [Test]
        public void GoodEntriesAroundBrokenOnesAreKeptInOrder()
        {
            string text = "1/First," + Beach + "/Broken/%zz,x/Second," + Cosy + "/,/Third,Top=Hoodie|,ignored extra field";
            var looks = SavedLooks.Deserialize(text);
            Assert.AreEqual(new[] { "First", "Second", "Third" }, looks.All.Select(look => look.Name).ToArray());
            Assert.AreEqual(new[] { Beach, Cosy, "Top=Hoodie|" }, looks.All.Select(look => look.OutfitText).ToArray());
            Assert.AreEqual("1/First," + Beach + "/Second," + Cosy + "/Third,Top=Hoodie|", looks.Serialize());
        }

        [Test]
        public void StoredDataBeyondTheLimitsIsCutDownNotTrusted()
        {
            string many = "1" + string.Concat(Enumerable.Range(1, 9).Select(i => "/N" + i + ",Top=Hoodie|"));
            var looks = SavedLooks.Deserialize(many);
            Assert.AreEqual(SavedLooks.Slots, looks.Count);
            Assert.AreEqual("N1", looks[0].Name);
            Assert.AreEqual("N6", looks[5].Name);

            var named = SavedLooks.Deserialize("1/A very long name that never fitted,Top=Hoodie|");
            Assert.AreEqual("A very long name", named[0].Name);

            string longOutfit = "Top=Hoodie|" + new string('x', SavedLooks.MaxOutfitLength);
            Assert.AreEqual(0, SavedLooks.Deserialize("1/Name," + longOutfit).Count);

            string huge = "1/Name,Top=Hoodie|" + new string('/', SavedLooks.MaxStoredLength);
            Assert.IsFalse(SavedLooks.TryDeserialize(huge, out var none));
            Assert.AreEqual(0, none.Count);
        }

        [Test]
        public void HostileTextNeverThrows()
        {
            string[] pieces = { "1", "/", ",", "%", "%0", "%002f", "%zzzz", "%d83d", "Top=Hoodie|", "Name", "\u0000", "\ud83d", "\ude00", "|", "=", ";", ":", " ", "2", "\n" };
            uint seed = 12345;
            for (int round = 0; round < 2000; round++)
            {
                var text = new System.Text.StringBuilder();
                seed = seed * 1664525u + 1013904223u;
                int length = (int)(seed >> 24) % 12;
                for (int i = 0; i < length; i++)
                {
                    seed = seed * 1664525u + 1013904223u;
                    text.Append(pieces[(int)((seed >> 16) % (uint)pieces.Length)]);
                }
                string candidate = text.ToString();
                Assert.DoesNotThrow(() => SavedLooks.TryDeserialize(candidate, out _), candidate);
                var looks = SavedLooks.Deserialize(candidate);
                Assert.LessOrEqual(looks.Count, SavedLooks.Slots);
                foreach (var look in looks.All)
                {
                    Assert.AreEqual(look.Name, SavedLooks.CleanName(look.Name), candidate);
                    Assert.IsTrue(SavedLooks.IsOutfit(look.OutfitText), candidate);
                }
                Assert.AreEqual(looks.Serialize(), SavedLooks.Deserialize(looks.Serialize()).Serialize(), candidate);
            }
        }

        [Test]
        public void TheDefaultOutfitIsWearableByANewPlayer()
        {
            var wardrobe = TestData.Wardrobe();
            var looks = new SavedLooks();
            Assert.IsNull(looks.Add("Everyday", wardrobe.Default));
            var check = looks.Check(0, wardrobe, new Career(), TestData.Catalogue());
            Assert.IsTrue(check.Wearable);
            Assert.IsNull(check.Reason);
            Assert.IsEmpty(check.Problems);
            Assert.IsEmpty(check.Missing);
            Assert.AreEqual(wardrobe.Default.Serialize(), check.Outfit.Serialize());
        }

        [Test]
        public void EveryShopLookIsWearableExactlyWhenThePlayerOwnsIt()
        {
            var wardrobe = TestData.Wardrobe();
            var items = TestData.Catalogue();
            var career = new Career();
            foreach (var bundle in CosmeticBundles.All)
            {
                var looks = new SavedLooks();
                var outfit = bundle.Apply(wardrobe.Default, wardrobe);
                Assert.IsNull(looks.Add(bundle.Name, outfit));
                var check = looks.Check(0, wardrobe, career, items);
                Assert.IsEmpty(check.Problems, bundle.Id);
                CollectionAssert.AreEquivalent(career.Missing(outfit).ToArray(), check.Missing.ToArray(), bundle.Id);
                Assert.AreEqual(bundle.OwnedBy(career), check.Wearable, bundle.Id);
                Assert.AreEqual(check.Wearable, check.Reason == null, bundle.Id);
            }
        }

        [Test]
        public void AnUnownedPieceIsReportedByNameAndTheLookIsLeftAsSaved()
        {
            var wardrobe = TestData.Wardrobe();
            var looks = new SavedLooks();
            var outfit = wardrobe.Wear(wardrobe.Default, "Headwear", "Headphones");
            Assert.IsNotNull(outfit);
            Assert.IsNull(looks.Add("Arcade ears", outfit));
            string stored = looks.Serialize();
            var career = new Career { Coins = 1000 };

            var locked = looks.Check(0, wardrobe, career);
            Assert.IsFalse(locked.Wearable);
            Assert.IsEmpty(locked.Problems);
            Assert.AreEqual(new[] { "piece:Headphones" }, locked.Missing.ToArray());
            Assert.AreEqual("You don't own Headphones yet.", locked.Reason);
            Assert.AreEqual(outfit.Serialize(), locked.Outfit.Serialize(), "the check never swaps the piece out");
            Assert.AreEqual(stored, looks.Serialize());

            Assert.IsNull(career.Buy("look:arcade"));
            var owned = looks.Check(0, wardrobe, career);
            Assert.IsTrue(owned.Wearable);
            Assert.IsNull(owned.Reason);
        }

        [Test]
        public void AnUnownedColourOrGearStyleIsReportedWithItsReason()
        {
            var wardrobe = TestData.Wardrobe();
            var career = new Career();
            var grape = wardrobe.Default.Clone();
            grape.Colours["Top"] = "grape";
            var candy = wardrobe.Default.Clone();
            candy.ItemSkins["BAT"] = "Candy";
            var looks = new SavedLooks();
            Assert.IsNull(looks.Add("Grape", grape));
            Assert.IsNull(looks.Add("Candy bat", candy));

            var colour = looks.Check(0, wardrobe, career, TestData.Catalogue());
            Assert.IsFalse(colour.Wearable);
            Assert.AreEqual(new[] { "colour:grape" }, colour.Missing.ToArray());
            Assert.AreEqual("You don't own the Grape colour yet.", colour.Reason);

            var style = looks.Check(1, wardrobe, career, TestData.Catalogue());
            Assert.IsFalse(style.Wearable);
            Assert.IsEmpty(style.Problems, "BAT does have a Candy style");
            Assert.AreEqual(new[] { "skin:Candy" }, style.Missing.ToArray());
            Assert.AreEqual("You don't own the Candy gear style yet.", style.Reason);

            career.Coins = 1000;
            Assert.IsNull(career.Buy("colour:grape"));
            Assert.IsNull(career.Buy("skin:Candy"));
            Assert.IsTrue(looks.Check(0, wardrobe, career, TestData.Catalogue()).Wearable);
            Assert.IsTrue(looks.Check(1, wardrobe, career, TestData.Catalogue()).Wearable);
        }

        [Test]
        public void ALookWhosePieceLeftTheCatalogueIsReportedNotRepaired()
        {
            var wardrobe = TestData.Wardrobe();
            var looks = new SavedLooks();
            Assert.IsNull(looks.Add("Snow day", wardrobe.Wear(wardrobe.Default, "Headwear", "Beanie")));
            string saved = looks[0].OutfitText;
            Assert.IsTrue(looks.Check(0, wardrobe, new Career()).Wearable);

            var later = TestData.Wardrobe();
            later.Pieces.RemoveAll(piece => piece.Id == "Beanie");
            var loaded = SavedLooks.Deserialize(looks.Serialize());
            Assert.AreEqual(1, loaded.Count, "loading keeps a look even when it can no longer be worn");
            var check = loaded.Check(0, later, new Career());
            Assert.IsFalse(check.Wearable);
            CollectionAssert.Contains(check.Problems.ToArray(), "there is no piece called Beanie");
            Assert.AreEqual("This look no longer fits: there is no piece called Beanie.", check.Reason);
            Assert.AreEqual(saved, loaded[0].OutfitText);
            Assert.AreEqual(saved, check.Outfit.Serialize());
        }

        [Test]
        public void ALookThatBreaksAWardrobeRuleIsReported()
        {
            var wardrobe = TestData.Wardrobe();
            var career = new Career();
            var looks = new SavedLooks();
            Assert.IsNull(looks.Add("Hood only", "Bottoms=Joggers;Footwear=Boots;Gloves=Mittens;Headwear=Hood;Top=Crewneck|"));
            Assert.IsNull(looks.Add("No trousers", "Footwear=Boots;Gloves=Mittens;Top=Hoodie|"));
            Assert.IsNull(looks.Add("Odd colour", "Bottoms=Joggers;Footwear=Boots;Gloves=Mittens;Top=Hoodie:plaid|"));
            Assert.IsNull(looks.Add("Wrong slot", "Bottoms=Joggers;Footwear=Boots;Gloves=Mittens;Top=Hoodie;Wings=Cap|"));

            CollectionAssert.Contains(looks.Check(0, wardrobe, career).Problems.ToArray(), "Hood needs Hoodie");
            CollectionAssert.Contains(looks.Check(1, wardrobe, career).Problems.ToArray(), "Bottoms can't be empty");
            CollectionAssert.Contains(looks.Check(2, wardrobe, career).Problems.ToArray(), "plaid is not a Top colour");
            CollectionAssert.Contains(looks.Check(3, wardrobe, career).Problems.ToArray(), "there is no Wings slot");
            for (int i = 0; i < looks.Count; i++)
            {
                var check = looks.Check(i, wardrobe, career);
                Assert.IsFalse(check.Wearable, looks[i].Name);
                StringAssert.StartsWith("This look no longer fits: ", check.Reason);
                Assert.AreEqual(wardrobe.Problems(looks[i].ToOutfit()).ToArray(), check.Problems.ToArray(), "the wardrobe's own check decides");
            }
        }

        [Test]
        public void AGearStyleTheItemNoLongerHasIsReportedWhenItemsAreGiven()
        {
            var wardrobe = TestData.Wardrobe();
            var items = TestData.Catalogue();
            var career = new Career();
            var looks = new SavedLooks();
            Assert.IsNull(looks.Add("Ghost item", wardrobe.Default.Serialize().Replace("|", "|ZZZZ=Classic")));
            Assert.IsNull(looks.Add("Ghost style", wardrobe.Default.Serialize().Replace("|", "|BAT=Plaid")));
            Assert.IsFalse(items.TryGet("ZZZZ", out _));
            Assert.IsFalse(items.Get("BAT").HasSkin("Plaid"));

            var item = looks.Check(0, wardrobe, career, items);
            Assert.IsFalse(item.Wearable);
            Assert.AreEqual(new[] { "there is no item called ZZZZ" }, item.Problems.ToArray());
            Assert.AreEqual("This look no longer fits: there is no item called ZZZZ.", item.Reason);

            var style = looks.Check(1, wardrobe, career, items);
            Assert.IsFalse(style.Wearable);
            Assert.AreEqual(new[] { "BAT has no Plaid style" }, style.Problems.ToArray());
            Assert.AreEqual(new[] { "skin:Plaid" }, style.Missing.ToArray());

            Assert.IsTrue(looks.Check(0, wardrobe, career).Wearable, "without the item catalogue only the wardrobe and ownership are checked");
        }

        [Test]
        public void CheckingAnEmptySlotSaysThereIsNoLook()
        {
            var wardrobe = TestData.Wardrobe();
            var looks = new SavedLooks();
            foreach (int index in new[] { -1, 0, 5, 6 })
            {
                var check = looks.Check(index, wardrobe, new Career());
                Assert.IsFalse(check.Wearable);
                Assert.IsNull(check.Outfit);
                Assert.AreEqual(SavedLooks.NoSuchLook, check.Reason);
                Assert.IsEmpty(check.Problems);
                Assert.IsEmpty(check.Missing);
            }
            Assert.Throws<System.ArgumentNullException>(() => looks.Check(0, null, new Career()));
            Assert.Throws<System.ArgumentNullException>(() => looks.Check(0, wardrobe, null));
        }
    }
}
