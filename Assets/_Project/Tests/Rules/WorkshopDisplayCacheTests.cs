using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WorkshopDisplayCacheTests
    {
        static ItemCatalogue Items()
        {
            string copied = Path.Combine(TestContext.CurrentContext.TestDirectory, "FlagshipCatalogue.json");
            return File.Exists(copied) ? ItemCatalogue.FromJson(File.ReadAllText(copied)) : TestData.Catalogue();
        }

        [Test]
        public void SelectionClickOnTheLostAddressReusesTheDisplayWithoutParsingOrValidating()
        {
            var content = WordboundFlagshipContent.Create(Items());
            var play = MatchStateValidation.Requirements(ModeDefinition.For("TimeRun"));
            var history = new WorkshopHistory(new WorkshopSnapshot(content.CopyDocument()));
            int parses = 0;
            var cache = new WorkshopDisplayCache(json => { parses++; return CourseJson.ImportDocument(json).Document; });
            Assert.IsTrue(cache.Update(history.Current, content.Geometry, play));
            Assert.AreEqual(1, parses);
            var document = cache.Document; var validation = cache.Validation;
            var rooms = document.Modules.Select(module => module.Id).Take(3).ToArray();
            foreach (string room in rooms)
            {
                history.Select(new[] { room });
                Assert.IsFalse(cache.Update(history.Current, content.Geometry, play), room);
                Assert.AreSame(document, cache.Document); Assert.AreSame(validation, cache.Validation);
                Assert.AreSame(document, cache.For(history.Current));
            }
            Assert.AreEqual(1, parses, "A selection click parses the course 0 times on the editor side.");
            history.Execute("Rename", draft => draft.Course.Title = "Renamed");
            Assert.IsTrue(cache.Update(history.Current, content.Geometry, play));
            Assert.AreEqual(2, parses, "A content edit parses exactly once for display and validation.");
            Assert.AreEqual("Renamed", cache.Document.Title); Assert.AreNotSame(validation, cache.Validation);
            var freeplay = MatchStateValidation.Requirements(ModeDefinition.For("Freestyle"));
            var before = cache.Validation;
            Assert.IsTrue(cache.Update(history.Current, content.Geometry, freeplay));
            Assert.AreEqual(2, parses, "Changing the mode re-validates without parsing.");
            Assert.AreNotSame(before, cache.Validation);
            CollectionAssert.AreEqual(WorkshopIssueTabs.Counts(cache.Validation.Issues, cache.Document), cache.IssueTabs);
        }

        [Test]
        public void ValidationMatchesAFreshCheckAfterSelectionOnlyChanges()
        {
            var preset = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            var history = new WorkshopHistory(preset.Snapshot);
            var cache = new WorkshopDisplayCache();
            cache.Update(history.Current, preset.Catalogue, preset.Play);
            history.Select(new[] { history.Current.Course().Modules[0].Id });
            cache.Update(history.Current, preset.Catalogue, preset.Play);
            var fresh = CourseValidator.Validate(history.Current.Course(), preset.Catalogue, preset.Play);
            CollectionAssert.AreEqual(fresh.Issues.Select(issue => issue.ToString()), cache.Validation.Issues.Select(issue => issue.ToString()));
            Assert.AreEqual(CourseJson.ExportDocument(history.Current.Course()), CourseJson.ExportDocument(cache.Document));
        }
    }
}
