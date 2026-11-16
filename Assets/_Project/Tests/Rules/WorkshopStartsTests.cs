using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class WorkshopStartsTests
    {
        string folder;

        [SetUp]
        public void SetUp() => folder = Path.Combine(Path.GetTempPath(), "wordbound-starts-test-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown() { if (Directory.Exists(folder)) Directory.Delete(folder, true); }

        static ItemCatalogue Items()
        {
            string copied = Path.Combine(TestContext.CurrentContext.TestDirectory, "FlagshipCatalogue.json");
            return File.Exists(copied) ? ItemCatalogue.FromJson(File.ReadAllText(copied)) : TestData.Catalogue();
        }

        static CoursePlayRequirements Freeplay => MatchStateValidation.Requirements(ModeDefinition.For(WorkshopStarts.BlankMode));

        static WorkshopSnapshot Map(string id, string title)
        {
            var draft = WorkshopTemplates.Create(WorkshopTemplate.FreestyleWorld).Snapshot.Edit();
            draft.Course.Id = id; draft.Course.Title = title;
            return new WorkshopSnapshot(draft);
        }

        [Test]
        public void BlankMapIsOneRoomWithStartSpotsAndIsReadyToTestInFreeplay()
        {
            foreach (var catalogue in new[] { CourseTemplates.ProofCatalogue(), WordboundFlagshipContent.Create(Items()).Geometry })
            {
                var blank = WorkshopStarts.Blank(catalogue, "new-map", "New map");
                Assert.AreEqual(1, blank.Layers.Count); Assert.AreEqual(1, blank.Modules.Count);
                CollectionAssert.AreEqual(new[] { "Freestyle" }, blank.SupportedModes);
                Assert.AreEqual(WorkshopStarts.StartSpots, blank.Spawns.Count(spawn => spawn.Kind == "Start"));
                Assert.IsEmpty(blank.Zones); Assert.IsEmpty(blank.Puzzles); Assert.IsEmpty(blank.Connections);
                var check = CourseValidator.Validate(blank, catalogue, Freeplay);
                Assert.IsTrue(check.Ok, string.Join("\n", check.Issues.Select(issue => issue.ToString())));
                var snapshot = new WorkshopSnapshot(blank);
                Assert.IsTrue(new WorkshopHistory(snapshot).Preview(catalogue, Freeplay).Success);
                Assert.AreEqual(CourseJson.ExportDocument(blank), snapshot.CourseJsonText);
            }
        }

        [Test]
        public void NumberedTitlesSkipTakenNamesIgnoringCaseAndStayWithinTheLimit()
        {
            Assert.AreEqual("Race circuit", WorkshopStarts.Numbered("Race circuit", new[] { "Arena" }));
            Assert.AreEqual("Race circuit 3", WorkshopStarts.Numbered("Race circuit", new[] { "race circuit", "Race circuit 2" }));
            Assert.AreEqual("New map", WorkshopStarts.Numbered("  ", null));
            Assert.AreEqual("Garden copy 2", WorkshopStarts.CopyTitle("Garden", new[] { "Garden copy" }));
            string longName = new string('a', 80);
            string numbered = WorkshopStarts.Numbered(longName, new[] { longName });
            Assert.AreEqual(80, numbered.Length); StringAssert.EndsWith(" 2", numbered);
        }

        [Test]
        public void LatestIsTheNewestReadableMapThatCanOpen()
        {
            var store = new WorkshopStorage(folder);
            store.Save(Map("older", "Older")); store.Save(Map("newer", "Newer")); store.Save(Map("blocked", "Blocked"));
            File.WriteAllText(Path.Combine(folder, "broken" + WorkshopStorage.AutosaveExtension), "not a map");
            var stamps = new[] { ("older", 1), ("newer", 2), ("blocked", 3), ("broken", 4) };
            foreach (var (id, hour) in stamps)
                File.SetLastWriteTimeUtc(Path.Combine(folder, id + WorkshopStorage.AutosaveExtension), new DateTime(2026, 11, 1, hour, 0, 0, DateTimeKind.Utc));
            var library = WorkshopStorage.Library(folder);
            Assert.AreEqual("newer", WorkshopStarts.Latest(library, entry => entry.CourseId != "blocked").CourseId);
            Assert.AreEqual("blocked", WorkshopStarts.Latest(library, null).CourseId);
            Assert.IsNull(WorkshopStarts.Latest(library, entry => false));
            Assert.IsNull(WorkshopStarts.Latest(null, null));
        }

        [Test]
        public void DuplicateMakesANamedCopyWithItsModeAndLeavesTheOriginal()
        {
            var store = new WorkshopStorage(folder);
            var original = Map("garden", "Garden");
            store.Save(original); store.SaveSettings("garden", "Freestyle", RaceFormat.Solo);
            byte[] before = File.ReadAllBytes(Path.Combine(folder, "garden" + WorkshopStorage.AutosaveExtension));
            var copy = WorkshopStarts.Duplicate(store, "garden", "Garden copy");
            Assert.AreNotEqual("garden", copy.Course().Id); Assert.AreEqual("Garden copy", copy.Course().Title);
            Assert.AreEqual(original.Course().Modules.Count, copy.Course().Modules.Count);
            Assert.IsTrue(store.TryLoadSettings(copy.Course().Id, out string mode, out _)); Assert.AreEqual("Freestyle", mode);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(Path.Combine(folder, "garden" + WorkshopStorage.AutosaveExtension)));
            CollectionAssert.AreEquivalent(new[] { "garden", copy.Course().Id }, WorkshopStorage.Library(folder).Select(entry => entry.CourseId));
        }

        [Test]
        public void DeleteHidesTheMapButKeepsItsFile()
        {
            var store = new WorkshopStorage(folder);
            store.Save(Map("garden", "Garden")); store.Save(Map("keep", "Keep"));
            string kept = WorkshopStarts.Delete(store, "garden");
            Assert.IsTrue(File.Exists(kept));
            Assert.AreEqual("garden", WorkshopSnapshot.Recover(File.ReadAllText(kept)).Course().Id);
            CollectionAssert.AreEqual(new[] { "keep" }, WorkshopStorage.Library(folder).Select(entry => entry.CourseId));
            Assert.IsFalse(store.HasRecovery("garden"));
        }
    }
}
