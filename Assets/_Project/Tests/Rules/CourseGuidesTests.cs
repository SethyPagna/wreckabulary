using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class CourseGuidesTests
    {
        [Test]
        public void EveryCourseModeOpensItsOwnGuide()
        {
            foreach (string mode in WorkshopModes.All)
            {
                string guide = CourseGuides.For(mode);
                Assert.IsNotNull(guide, mode);
                Assert.IsTrue(CourseGuides.Has(mode), mode);
                Assert.IsTrue(ModeGuideBook.Has(guide), mode);
                Assert.IsFalse(HouseModes.Has(guide), mode + " must not open a house guide");
                Assert.AreEqual(WorkshopModes.Name(mode), ModeGuideBook.Name(guide), mode);
                Assert.AreEqual(ModeDefinition.For(mode).Id, ModeGuideBook.For(guide).Mode, mode);
            }
            CollectionAssert.AllItemsAreUnique(WorkshopModes.All.Select(CourseGuides.For).ToArray());
        }

        [Test]
        public void EachCourseModeMapsToTheNamedGuide()
        {
            Assert.AreEqual(ModeGuideBook.TimeTrial, CourseGuides.For("TimeRun"));
            Assert.AreEqual(ModeGuideBook.Freeplay, CourseGuides.For("Freestyle"));
            Assert.AreEqual(ModeGuideBook.TeamRace, CourseGuides.For("TeamRace"));
            Assert.AreEqual(ModeGuideBook.BrawlRace, CourseGuides.For("BrawlRace"));
            Assert.AreEqual(ModeGuideBook.ArenaFreeForAll, CourseGuides.For("FreeForAll"));
            Assert.AreEqual(ModeGuideBook.Arena, ModeGuideBook.Place(CourseGuides.For("FreeForAll")));
        }

        [Test]
        public void HouseModesAndUnknownIdsHaveNoCourseGuide()
        {
            foreach (var house in HouseModes.All)
            {
                Assert.IsNull(CourseGuides.For(house.Id), house.Id);
                Assert.IsFalse(CourseGuides.Has(house.Id), house.Id);
            }
            foreach (string id in new[] { null, "", "timerun", "Wordbound", "Workshop" })
            {
                Assert.IsNull(CourseGuides.For(id), id);
                Assert.IsFalse(CourseGuides.Has(id), id);
            }
        }

        [Test]
        public void TheRowUsesTheSameWordsAsTheGuideTitle()
        {
            Assert.AreEqual("How to play", CourseGuides.RowLabel);
            StringAssert.EndsWith(CourseGuides.RowLabel, ModeGuideBook.Eyebrow(CourseGuides.For("TimeRun")));
        }
    }
}
