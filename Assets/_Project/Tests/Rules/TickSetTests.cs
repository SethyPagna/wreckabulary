using System;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public class TickSetTests
    {
        static readonly string[] Houses = { "pinwheel", "cottage", "loft", "manor" };

        [Test]
        public void ANewSetTicksEveryOptionSoTheFirstPlayHasTheWidestPool()
        {
            var set = new TickSet(Houses);
            Assert.IsTrue(set.All);
            Assert.AreEqual(Houses, set.Ticked);
        }

        [Test]
        public void UntickingStopsAtTheLastTickedOption()
        {
            var set = new TickSet(Houses);
            Assert.IsTrue(set.Toggle("pinwheel"));
            Assert.IsTrue(set.Toggle("cottage"));
            Assert.IsTrue(set.Toggle("loft"));
            Assert.IsFalse(set.CanUntick("manor"));
            Assert.IsFalse(set.Toggle("manor"), "The last ticked option cannot be unticked.");
            Assert.AreEqual(new[] { "manor" }, set.Ticked);
            Assert.IsTrue(set.Toggle("cottage"));
            Assert.IsTrue(set.CanUntick("manor"));
            Assert.AreEqual(new[] { "cottage", "manor" }, set.Ticked, "Ticked options keep the listed order.");
        }

        [Test]
        public void UnknownOptionsAreRejectedWhenToggledAndDroppedWhenLoaded()
        {
            var set = new TickSet(Houses, new[] { "loft", "castle" });
            Assert.AreEqual(new[] { "loft" }, set.Ticked);
            Assert.Throws<ArgumentException>(() => set.Toggle("castle"));
            Assert.IsTrue(TickSet.Load(Houses, "castle+moat").All, "A save with nothing valid falls back to every option.");
        }

        [Test]
        public void ASavedSelectionLoadsBackExactly()
        {
            var set = new TickSet(Houses);
            set.Toggle("cottage"); set.Toggle("manor");
            string saved = set.Save();
            Assert.AreEqual("pinwheel+loft", saved);
            var loaded = TickSet.Load(Houses, saved);
            Assert.AreEqual(set.Ticked, loaded.Ticked);
            Assert.IsTrue(TickSet.Load(Houses, "").All);
            Assert.IsTrue(TickSet.Load(Houses, null).All);
        }

        [Test]
        public void TickAllRestoresTheWholePool()
        {
            var set = new TickSet(Houses, new[] { "loft" });
            set.TickAll();
            Assert.IsTrue(set.All);
        }

        [Test]
        public void TheSummaryNamesTheTickedOptionsOrSaysAll()
        {
            var set = new TickSet(Houses);
            Assert.AreEqual("Every house", set.Summary(id => id, "Every house"));
            set.Toggle("pinwheel"); set.Toggle("manor");
            Assert.AreEqual("COTTAGE, LOFT", set.Summary(id => id.ToUpperInvariant(), "Every house"));
        }

        [Test]
        public void EmptyOrDuplicatedOptionListsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new TickSet(Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new TickSet(new[] { "loft", "loft" }));
            Assert.Throws<ArgumentException>(() => new TickSet(new[] { "loft", "" }));
            Assert.Throws<ArgumentNullException>(() => new TickSet(null));
        }
    }
}
