using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;
using Wreckabulary.Workshop;

namespace Wreckabulary.EditorTests
{
    public sealed class WordboundWorkshopStartTests
    {
        const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic, Static = BindingFlags.Static | BindingFlags.NonPublic;
        string folder;
        GameObject owner;
        WordboundWorkshop workshop;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "wordbound-workshop-start-" + Guid.NewGuid().ToString("N"));
            owner = new GameObject("Wordbound workshop start test");
            workshop = owner.AddComponent<WordboundWorkshop>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(owner);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        static WordboundFlagshipContent LostAddress() => WordboundFlagshipContent.Create(
            Wreckabulary.Rules.ItemCatalogue.FromJson(File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Data/Config/items.json"))));

        [Test]
        public void TheStartScreenIsADialogThatAnyOpenOrResumeCloses()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            workshop.ShowStart();
            Assert.IsTrue(workshop.StartOpen); Assert.IsTrue(workshop.Visible);
            Assert.IsFalse((bool)Call("ControllerAllowed", true), "The map ignores the pad behind the start screen.");
            workshop.ResumeEditing();
            Assert.IsFalse(workshop.StartOpen); Assert.IsFalse((bool)Field("libraryOpen").GetValue(workshop));
            workshop.ShowStart();
            workshop.OpenTemplate(WorkshopTemplate.CombatArena, folder);
            Assert.IsFalse(workshop.StartOpen);
            workshop.ShowStart();
            workshop.OpenTemplate(WorkshopTemplate.CombatArena, folder);
            Assert.IsFalse(workshop.StartOpen, "Opening the map already in use still closes the start screen.");
        }

        [Test]
        public void LeavingTheStartScreenClosesWithoutSavingAnUntouchedMap()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            string id = workshop.History.Current.Course().Id;
            int closed = 0; workshop.CloseRequested += () => closed++;
            workshop.ShowStart();
            Call("LeaveStart");
            Assert.AreEqual(1, closed); Assert.IsFalse(workshop.Visible); Assert.IsFalse(workshop.StartOpen);
            Assert.IsFalse(new WorkshopStorage(Path.Combine(folder, "templates", "RaceCircuit")).HasRecovery(id));
        }

        [Test]
        public void EachStarterMakesANewNumberedMapAndKeepsTheStarterDefaultUnsaved()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            workshop.ShowStart();
            workshop.NewMap(WorkshopStarter.RaceCircuit);
            Assert.IsFalse(workshop.StartOpen);
            var first = workshop.History.Current.Course();
            Assert.AreEqual("Race circuit", first.Title); Assert.AreNotEqual("race-proof", first.Id);
            Assert.AreEqual("TimeRun", workshop.PlayRequirements.ModeId);
            Assert.IsFalse(workshop.History.CanUndo);
            workshop.NewMap(WorkshopStarter.RaceCircuit);
            Assert.AreEqual("Race circuit 2", workshop.History.Current.Course().Title);
            var scoped = new WorkshopStorage(Path.Combine(folder, "templates", "RaceCircuit"));
            Assert.IsFalse(scoped.HasRecovery("race-proof"), "The starter itself is never saved over.");
            Assert.IsTrue(scoped.HasRecovery(first.Id));
            CollectionAssert.IsSubsetOf(new[] { "Race circuit", "Race circuit 2" }, WorkshopStorage.Library(folder).Select(entry => entry.Title).ToList());
            workshop.NewMap(WorkshopStarter.Arena);
            Assert.AreEqual("Arena", workshop.History.Current.Course().Title);
            Assert.AreEqual("FreeForAll", workshop.PlayRequirements.ModeId);
        }

        [Test]
        public void TheDeferredStarterChoiceRunsOnTheNextFocusedUpdate()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            workshop.ShowStart();
            Call("QueueStarter", WorkshopStarter.FreeplayWorld);
            Call("ProcessSourceChoice", false);
            Assert.IsTrue(workshop.StartOpen, "An unfocused window drops the queued choice.");
            Call("QueueStarter", WorkshopStarter.FreeplayWorld);
            Call("ProcessSourceChoice", true);
            Assert.IsFalse(workshop.StartOpen);
            Assert.AreEqual("Freeplay world", workshop.History.Current.Course().Title);
        }

        [Test]
        public void ABlankMapIsOneRoomReadyToTestInFreeplay()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            workshop.NewMap(WorkshopStarter.Blank);
            AssertBlank(CourseTemplates.ProofCatalogue().Hash, Path.Combine(folder, "templates", "FreestyleWorld"));
            var content = LostAddress();
            workshop.ConfigureFlagship(content);
            workshop.NewMap(WorkshopStarter.Blank);
            AssertBlank(content.Geometry.Hash, Path.Combine(folder, "templates", "Flagship"));
            Assert.AreEqual("New map 2", workshop.History.Current.Course().Title);
            Assert.AreEqual("Big room", (string)typeof(WordboundWorkshop).GetMethod("ObjectLabel", Static).Invoke(null, new object[] { "FlagshipRoom" }));
        }

        void AssertBlank(string catalogueHash, string scopedFolder)
        {
            var course = workshop.History.Current.Course();
            Assert.AreEqual(1, course.Modules.Count); Assert.IsEmpty(course.Zones);
            Assert.AreEqual(catalogueHash, workshop.Catalogue.Hash);
            Assert.AreEqual("Freestyle", workshop.PlayRequirements.ModeId);
            Assert.IsTrue(workshop.CanPlaytestLocally);
            var preview = workshop.Preview();
            Assert.IsTrue(preview.Success, string.Join("\n", preview.Issues.Select(issue => issue.Message)));
            Assert.IsTrue(new WorkshopStorage(scopedFolder).HasRecovery(course.Id));
            Assert.IsTrue(new WorkshopStorage(scopedFolder).TryLoadSettings(course.Id, out string mode, out _));
            Assert.AreEqual("Freestyle", mode);
        }

        [Test]
        public void MyMapsCopiesAndDeletesWithoutTouchingTheOpenMap()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            workshop.NewMap(WorkshopStarter.FreeplayWorld);
            string other = workshop.History.Current.Course().Id;
            workshop.NewMap(WorkshopStarter.FreeplayWorld);
            string open = workshop.History.Current.Course().Id;
            Call("OpenLibrary");
            var entries = Entries();
            var current = entries.Single(entry => entry.CourseId == open);
            Call("DuplicateMap", current);
            Assert.AreEqual(open, workshop.History.Current.Course().Id, "Copying keeps editing the open map.");
            Assert.IsTrue(Entries().Any(entry => entry.Title == "Freeplay world 2 copy"));
            Assert.IsNotEmpty((string)Call("DeleteBlocker", current));
            Assert.Throws<InvalidOperationException>(() => Call("DeleteMap", current));
            Call("DeleteMap", Entries().Single(entry => entry.CourseId == other));
            Assert.IsFalse(Entries().Any(entry => entry.CourseId == other));
            Assert.IsTrue(Directory.GetFiles(Path.Combine(folder, "templates", "FreestyleWorld")).Any(path => Path.GetFileName(path).StartsWith(other, StringComparison.Ordinal) && path.Contains(".unreadable-")),
                "A deleted map's file is kept aside, not destroyed.");
        }

        [Test]
        public void ContinueOffersTheNewestMapThatCanOpen()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            workshop.NewMap(WorkshopStarter.Arena);
            string arena = workshop.History.Current.Course().Id;
            workshop.ShowStart();
            Assert.AreEqual(arena, ((WorkshopMapEntry)Field("continueEntry").GetValue(workshop)).CourseId);
        }

        [Test]
        public void TestExplainsWhyItIsOffAndOffersOneFix()
        {
            workshop.OpenTemplate(WorkshopTemplate.CombatArena, folder);
            workshop.PlaytestRequested += _ => { };
            workshop.ConfigureLocalPlaytest(true);
            Assert.AreEqual("Test needs Time Trial or Freeplay", Property<string>("TestBlocker"));
            workshop.EnableFreestyleSandbox();
            Assert.AreEqual("", Property<string>("TestBlocker"));
            workshop.SelectMode("FreeForAll");
            Assert.AreEqual("Test needs Time Trial or Freeplay", Property<string>("TestBlocker"));
        }

        [Test]
        public void StepTabsCountTheProblemsEachStepFixes()
        {
            workshop.OpenTemplate(WorkshopTemplate.FreestyleWorld, folder);
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0 }, (int[])Call("TabIssues"));
            workshop.History.Execute("Remove a start", draft => draft.Course.Spawns.RemoveAt(0));
            Call("Refresh");
            var tabs = (int[])Call("TabIssues");
            Assert.Greater(tabs[WorkshopIssueTabs.Rules], 0); Assert.AreEqual(0, tabs[WorkshopIssueTabs.Build]);
        }

        [Test]
        public void ASelectionClickReusesTheParsedMapAndItsCheck()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            var display = Field("display").GetValue(workshop); var validation = Field("validation").GetValue(workshop);
            workshop.History.Select(new[] { workshop.History.Current.Course().Modules[0].Id });
            Call("Refresh");
            Assert.AreSame(display, Field("display").GetValue(workshop));
            Assert.AreSame(validation, Field("validation").GetValue(workshop));
            workshop.History.Execute("Rename", draft => draft.Course.Title = "Renamed");
            Call("Refresh");
            Assert.AreNotSame(display, Field("display").GetValue(workshop));
        }

        [Test]
        public void ExitWithoutChangesKeepsTheSavedFileAndItsBackup()
        {
            workshop.OpenTemplate(WorkshopTemplate.RaceCircuit, folder);
            workshop.History.Execute("Rename", draft => draft.Course.Title = "Saved once");
            Assert.IsTrue((bool)Call("SaveNow"));
            string primary = Path.Combine(folder, "templates", "RaceCircuit", WorkshopStorage.FileStem(workshop.History.Current.Course().Id) + WorkshopStorage.AutosaveExtension);
            var written = File.GetLastWriteTimeUtc(primary);
            workshop.RequestClose();
            Assert.AreEqual(written, File.GetLastWriteTimeUtc(primary));
            Assert.IsFalse(File.Exists(primary + ".bak"), "An unchanged exit doesn't rotate the backup.");
        }

        [Test]
        public void PlayerWordsReplaceInternalNames()
        {
            var actions = (string[])Field("controllerActions").GetValue(workshop);
            CollectionAssert.Contains(actions, "My maps"); CollectionAssert.DoesNotContain(actions, "Assigned course");
            CollectionAssert.AreEqual(new[] { "Start points", "Checkpoints", "Paths", "Laps", "Respawn spots" },
                (string[])typeof(WordboundWorkshop).GetField("RouteTabs", Static).GetValue(null));
            var labels = new List<string>();
            foreach (WorkshopStarter starter in Enum.GetValues(typeof(WorkshopStarter)))
                labels.Add((string)typeof(WordboundWorkshop).GetMethod("StarterLabel", Static).Invoke(null, new object[] { starter }));
            CollectionAssert.AreEqual(new[] { "New map", "Race circuit", "Arena", "Freeplay world", "Parcel Depot", "The Lost Address" }, labels);
            foreach (string label in labels) StringAssert.DoesNotContain("Flagship", label);
        }

        List<WorkshopMapEntry> Entries()
        {
            Call("RefreshLibrary");
            return ((IReadOnlyList<WorkshopMapEntry>)Field("libraryEntries").GetValue(workshop)).ToList();
        }

        static FieldInfo Field(string name) => typeof(WordboundWorkshop).GetField(name, Instance) ?? throw new MissingFieldException(name);
        T Property<T>(string name) => (T)typeof(WordboundWorkshop).GetProperty(name, Instance).GetValue(workshop);
        object Call(string name, params object[] args)
        {
            var method = typeof(WordboundWorkshop).GetMethods(Instance).Single(item => item.Name == name && item.GetParameters().Length == args.Length);
            try { return method.Invoke(workshop, args); }
            catch (TargetInvocationException error) { throw error.InnerException ?? error; }
        }
    }
}
