using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    [TestFixture]
    public class FinishesTests
    {
        [Test]
        public void TheRegistryListsTheFourFinishesInShopOrder()
        {
            CollectionAssert.AreEqual(new[] { "Classic", "Candy", "Arcade", "Winter" }, Finishes.All.Select(f => f.Id));
            CollectionAssert.AreEqual(Finishes.All.Select(f => f.Id), Finishes.Ids);
            Assert.AreSame(Finishes.All[0], Finishes.Classic);
            Assert.AreEqual(Skin.Standard, Finishes.Classic.Id);
            foreach (var finish in Finishes.All)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(finish.Name), finish.Id);
                Assert.IsFalse(string.IsNullOrWhiteSpace(finish.Tagline), finish.Id);
                Assert.LessOrEqual(finish.Tagline.Length, 56, finish.Id + " tagline stays one short line");
                Assert.AreEqual("UI/Finishes/" + finish.Id, finish.Emblem);
                Assert.AreEqual("_" + finish.Id, finish.Suffix);
                Assert.IsTrue(finish.InCustomize, finish.Id);
                Assert.LessOrEqual(finish.Swatch, 0xFFFFFFu);
                Assert.LessOrEqual(finish.Accent, 0xFFFFFFu);
            }
        }

        [Test]
        public void NamesTaglinesAndSwatchesAreUnique()
        {
            Assert.AreEqual(Finishes.All.Count, Finishes.All.Select(f => f.Name).Distinct().Count());
            Assert.AreEqual(Finishes.All.Count, Finishes.All.Select(f => f.Tagline).Distinct().Count());
            foreach (var a in Finishes.All)
            {
                Assert.Greater(Distance(a.Swatch, a.Accent), 60, a.Id + " disc gradient has two readable ends");
                foreach (var b in Finishes.All.Where(b => b != a))
                    Assert.Greater(Distance(a.Swatch, b.Swatch), 60, a.Id + " vs " + b.Id);
            }
        }

        [Test]
        public void PricesMatchTheShopAndFreeFinishesAreOwned()
        {
            var career = new Career();
            foreach (var finish in Finishes.All)
            {
                var offer = Career.Shop.FirstOrDefault(o => o.Kind == "skin" && o.Value == finish.Id);
                if (finish.Price == 0)
                {
                    Assert.IsNull(offer, finish.Id + " is free, so the shop does not sell it");
                    Assert.IsTrue(career.Owns("skin", finish.Id), finish.Id);
                }
                else
                {
                    Assert.IsNotNull(offer, finish.Id);
                    Assert.AreEqual(finish.Price, offer.Price, finish.Id);
                    Assert.IsFalse(career.Owns("skin", finish.Id), finish.Id);
                }
            }
            Assert.IsTrue(Career.Shop.Where(o => o.Kind == "skin").All(o => Finishes.Find(o.Value) != null), "every finish the shop sells is registered");
        }

        [Test]
        public void FindIsExactAndRejectsUnknownIds()
        {
            Assert.AreEqual("Candy", Finishes.Find("Candy").Name);
            Assert.IsNull(Finishes.Find("candy"));
            Assert.IsNull(Finishes.Find("Gold"));
            Assert.IsNull(Finishes.Find(""));
            Assert.IsNull(Finishes.Find(null));
        }

        [Test]
        public void ForItemKeepsClassicFirstAndOnlyListedFinishes()
        {
            CollectionAssert.AreEqual(new[] { "Classic", "Candy", "Arcade" },
                Finishes.ForItem("BAT", new List<string> { "Arcade", "Classic", "Candy" }).Select(f => f.Id));
            CollectionAssert.AreEqual(new[] { "Classic" }, Finishes.ForItem("BEAR", new List<string>()).Select(f => f.Id));
            CollectionAssert.AreEqual(new[] { "Classic" }, Finishes.ForItem("BEAR", null).Select(f => f.Id));
            CollectionAssert.AreEqual(new[] { "Classic", "Candy" }, Finishes.ForItem("SOAP", new[] { "Classic", "Candy", "Gold" }).Select(f => f.Id));
        }

        [Test]
        public void WinterOnlyCoversItsTwoRecipes()
        {
            var winter = Finishes.Find("Winter");
            CollectionAssert.AreEquivalent(new[] { "SHIELD", "SODA" }, winter.OnlyFor);
            var listed = new[] { "Classic", "Candy", "Arcade", "Winter" };
            CollectionAssert.Contains(Finishes.ForItem("SHIELD", listed).Select(f => f.Id).ToList(), "Winter");
            CollectionAssert.Contains(Finishes.ForItem("soda", listed).Select(f => f.Id).ToList(), "Winter");
            CollectionAssert.DoesNotContain(Finishes.ForItem("BAT", listed).Select(f => f.Id).ToList(), "Winter");
            Assert.IsTrue(Finishes.All.Where(f => f != winter).All(f => f.OnlyFor.Count == 0 && f.Covers("BAT")));
        }

        [TestCase("wood_light_Classic", "wood_light", "Classic")]
        [TestCase("wood_dark_Candy", "wood_dark", "Candy")]
        [TestCase("glass_Arcade", "glass", "Arcade")]
        [TestCase("metal_Winter", "metal", "Winter")]
        [TestCase("fabric_main", null, null)]
        [TestCase("_Classic", null, null)]
        [TestCase("wood_Gold", null, null)]
        [TestCase("wood_classic", null, null)]
        [TestCase(null, null, null)]
        public void MaterialNamesSplitIntoFamilyAndFinish(string name, string family, string finish)
        {
            Assert.AreEqual(family, Finishes.FamilyOf(name));
            Assert.AreEqual(finish, Finishes.FinishOf(name));
            if (family != null) Assert.AreEqual(name, Finishes.Find(finish).MaterialFor(family));
        }

        static double Distance(uint a, uint b)
        {
            int dr = (int)(a >> 16 & 0xFF) - (int)(b >> 16 & 0xFF), dg = (int)(a >> 8 & 0xFF) - (int)(b >> 8 & 0xFF), db = (int)(a & 0xFF) - (int)(b & 0xFF);
            return Math.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}
