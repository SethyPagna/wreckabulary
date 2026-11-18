using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Wreckabulary.Rules.Tests
{
    public sealed class PracticeMapsTests
    {
        static readonly string[] Houses = { "pinwheel", "flat", "courtyard", "terrace", "walkup" };

        static string Name(string id) => id.Substring(0, 1).ToUpperInvariant() + id.Substring(1) + " House";

        [Test]
        public void AFirstVisitTicksEveryHouseAndANewSaveLoadsBack()
        {
            var maps = PracticeMaps.Load(Houses, null);
            Assert.IsTrue(maps.All);
            Assert.AreEqual(PracticeMaps.Every, PracticeMaps.Label(maps, Name));
            Assert.IsTrue(PracticeMaps.Toggle(maps, "flat", false));
            Assert.IsTrue(PracticeMaps.Toggle(maps, "walkup", false));
            var again = PracticeMaps.Load(Houses, maps.Save());
            Assert.AreEqual(new[] { "pinwheel", "courtyard", "terrace" }, again.Ticked);
            Assert.AreEqual("Any of 3 houses", PracticeMaps.Label(again, Name));
            Assert.AreEqual(new[] { "terrace" }, PracticeMaps.Load(Houses, "terrace+castle").Ticked, "a removed house is dropped from the save");
            Assert.IsTrue(PracticeMaps.Load(Houses, "castle").All, "a save with no installed house ticks every house again");
        }

        [Test]
        public void TheLastTickedHouseCannotBeUnticked()
        {
            var maps = PracticeMaps.Only(Houses, "courtyard");
            Assert.AreEqual(new[] { "courtyard" }, maps.Ticked);
            Assert.AreEqual("Courtyard House", PracticeMaps.Label(maps, Name), "one ticked house is named");
            Assert.IsFalse(PracticeMaps.Toggle(maps, "courtyard", false));
            Assert.AreEqual(new[] { "courtyard" }, maps.Ticked);
            Assert.IsTrue(PracticeMaps.Toggle(maps, "flat", false));
            Assert.AreEqual(new[] { "flat", "courtyard" }, maps.Ticked, "ticks keep the installed order");
            Assert.IsTrue(PracticeMaps.Toggle(maps, "courtyard", false));
            Assert.AreEqual(new[] { "flat" }, maps.Ticked);
            Assert.Throws<ArgumentException>(() => PracticeMaps.Toggle(maps, "castle", false));
            Assert.Throws<ArgumentException>(() => PracticeMaps.Only(Houses, "castle"));
            Assert.Throws<ArgumentNullException>(() => PracticeMaps.Toggle(null, "flat", false));
        }

        [Test]
        public void TappingAHouseWhileASavedHomeIsChosenTicksItAndNeverUnticks()
        {
            var maps = PracticeMaps.Load(Houses, "pinwheel+flat");
            Assert.IsTrue(PracticeMaps.Toggle(maps, "flat", true), "a ticked house stays ticked");
            Assert.AreEqual(new[] { "pinwheel", "flat" }, maps.Ticked);
            Assert.IsTrue(PracticeMaps.Toggle(maps, "walkup", true));
            Assert.AreEqual(new[] { "pinwheel", "flat", "walkup" }, maps.Ticked);
            var single = PracticeMaps.Only(Houses, "pinwheel");
            Assert.IsTrue(PracticeMaps.Toggle(single, "pinwheel", true), "leaving a saved home for its only house is allowed");
            Assert.AreEqual(new[] { "pinwheel" }, single.Ticked);
        }

        [Test]
        public void EachMatchPicksATickedHouseFromTheInjectedRoll()
        {
            var maps = PracticeMaps.Load(Houses, "flat+terrace+walkup");
            var asked = new List<int>();
            Assert.AreEqual("flat", PracticeMaps.Pick(maps, count => { asked.Add(count); return 0; }));
            Assert.AreEqual("terrace", PracticeMaps.Pick(maps, count => 1));
            Assert.AreEqual("walkup", PracticeMaps.Pick(maps, count => count - 1));
            Assert.AreEqual(new[] { 3 }, asked, "the roll sees only the ticked houses");
            var seen = Enumerable.Range(0, 3).Select(i => PracticeMaps.Pick(maps, _ => i)).ToArray();
            CollectionAssert.AreEqual(maps.Ticked, seen, "every ticked house can come up");
            CollectionAssert.IsSubsetOf(Enumerable.Range(0, 50).Select(i => PracticeMaps.Pick(maps, count => i % count)).Distinct(), maps.Ticked);
        }

        [Test]
        public void OneTickedHousePlaysThatHouseWithoutRolling()
        {
            var maps = PracticeMaps.Only(Houses, "walkup");
            Assert.AreEqual("walkup", PracticeMaps.Pick(maps, _ => throw new InvalidOperationException("no roll for one house")));
        }

        [Test]
        public void ARollOutsideTheTickedHousesIsRejected()
        {
            var maps = PracticeMaps.Load(Houses, "flat+terrace");
            Assert.Throws<ArgumentOutOfRangeException>(() => PracticeMaps.Pick(maps, count => count));
            Assert.Throws<ArgumentOutOfRangeException>(() => PracticeMaps.Pick(maps, _ => -1));
            Assert.Throws<ArgumentNullException>(() => PracticeMaps.Pick(maps, null));
            Assert.Throws<ArgumentNullException>(() => PracticeMaps.Pick(null, _ => 0));
        }
    }
}
