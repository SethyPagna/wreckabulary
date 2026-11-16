using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WorkshopIssueTabsTests
    {
        static CoursePlayRequirements Play(string mode) => MatchStateValidation.Requirements(ModeDefinition.For(mode));

        static int[] Tabs(CourseDocument course, CoursePlayRequirements play) =>
            WorkshopIssueTabs.Counts(CourseValidator.Validate(course, CourseTemplates.ProofCatalogue(), play).Issues, course);

        [Test]
        public void EachBrokenPartCountsOnTheStepThatFixesIt()
        {
            var room = CourseTemplates.FreestyleWorld(); room.Modules[1].DefinitionId = "Missing";
            Assert.Greater(Tabs(room, Play("Freestyle"))[WorkshopIssueTabs.Build], 0);
            var objects = CourseTemplates.FreestyleWorld(); objects.Zones[0].Pool.Clear();
            Assert.Greater(Tabs(objects, Play("Freestyle"))[WorkshopIssueTabs.Fill], 0);
            var starts = CourseTemplates.FreestyleWorld(); starts.Spawns.RemoveAt(0);
            var counts = Tabs(starts, Play("Freestyle"));
            Assert.Greater(counts[WorkshopIssueTabs.Rules], 0); Assert.AreEqual(0, counts[WorkshopIssueTabs.Build]);
            var mode = CourseTemplates.FreestyleWorld();
            Assert.Greater(Tabs(mode, Play("TimeRun"))[WorkshopIssueTabs.Rules], 0);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, Tabs(CourseTemplates.FreestyleWorld(), Play("Freestyle")));
        }

        [TestCase("$.modules[0]", "", WorkshopIssueTabs.Build)]
        [TestCase("$.connections", "", WorkshopIssueTabs.Build)]
        [TestCase("$.zones[1].bounds", "", WorkshopIssueTabs.Fill)]
        [TestCase("$.physicalSites", "", WorkshopIssueTabs.Fill)]
        [TestCase("$.recoveryAnchors", "", WorkshopIssueTabs.Rules)]
        [TestCase("$.laps", "", WorkshopIssueTabs.Rules)]
        [TestCase("$.title", "", WorkshopIssueTabs.Share)]
        [TestCase("", "GardenProps", WorkshopIssueTabs.Fill)]
        [TestCase("", "StartSafe", WorkshopIssueTabs.Rules)]
        [TestCase("", "GardenDoor", WorkshopIssueTabs.Build)]
        [TestCase("", "unknown", WorkshopIssueTabs.Share)]
        public void PathsComeFirstThenTheOwningEntity(string path, string entity, int tab)
        {
            var course = CourseTemplates.FreestyleWorld();
            Assert.AreEqual(tab, WorkshopIssueTabs.Tab(new CourseIssue("any", path, entity, "message"), course));
            Assert.AreEqual(WorkshopIssueTabs.Rules, WorkshopIssueTabs.Tab(new CourseIssue("mode.unsupported", "", "", "m"), course));
            Assert.AreEqual(4, WorkshopIssueTabs.Counts(null, course).Length);
            Assert.AreEqual(0, WorkshopIssueTabs.Counts(Enumerable.Empty<CourseIssue>(), course).Sum());
        }
    }
}
