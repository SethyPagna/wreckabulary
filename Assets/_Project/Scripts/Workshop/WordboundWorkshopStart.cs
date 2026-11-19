using System;
using System.IO;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public enum WorkshopStarter { Blank, RaceCircuit, Arena, FreeplayWorld, ParcelDepot, LostAddress }

    public sealed partial class WordboundWorkshop
    {
#if UNITY_EDITOR || GSK_QA
        static readonly bool QaBuild = true;
#else
        static readonly bool QaBuild = false;
#endif
        static readonly WorkshopStarter[] Starters =
            { WorkshopStarter.RaceCircuit, WorkshopStarter.Arena, WorkshopStarter.FreeplayWorld, WorkshopStarter.ParcelDepot, WorkshopStarter.LostAddress };
        bool startScreen;
        string deleteConfirm = "", assignedTitle = "", assignedKey = "";
        WorkshopStarter? starterDecision;
        WorkshopMapEntry continueEntry;
        public bool StartOpen => libraryOpen && startScreen;

        public void ShowStart()
        {
            if (History == null) return;
            Visible = true; Refresh();
            if (OnlineStarting || RecoveryChoicePending) return;
            OpenLibrary(); startScreen = true;
        }

        void CloseStart()
        {
            if (!startScreen) return;
            startScreen = libraryOpen = false; deleteConfirm = "";
        }

        void LeaveStart()
        {
            CloseStart();
            if (History.Revision != savedRevision) { RequestClose(); return; }
            CancelGesture(); Visible = false; CloseRequested?.Invoke();
        }

        static string StarterLabel(WorkshopStarter starter) => starter switch
        {
            WorkshopStarter.Blank => WorkshopStarts.BlankTitle, WorkshopStarter.RaceCircuit => TemplateLabel(WorkshopTemplate.RaceCircuit),
            WorkshopStarter.Arena => TemplateLabel(WorkshopTemplate.CombatArena), WorkshopStarter.FreeplayWorld => TemplateLabel(WorkshopTemplate.FreestyleWorld),
            WorkshopStarter.ParcelDepot => "Parcel Depot", _ => "The Lost Address"
        };

        static string StarterHint(WorkshopStarter starter) => starter switch
        {
            WorkshopStarter.Blank => "One empty room with start spots · ready to test in " + WorkshopModes.Name("Freestyle"),
            WorkshopStarter.RaceCircuit => "A small race with three laps", WorkshopStarter.Arena => "A room for knockout battles",
            WorkshopStarter.FreeplayWorld => "Two rooms to explore and spell in", WorkshopStarter.ParcelDepot => "The Parcel Depot course",
            _ => "The four-floor Lost Address course"
        };

        string StarterBlocker(WorkshopStarter starter) => starter == WorkshopStarter.ParcelDepot ?
            !HasParcelService ? "Parcel Depot isn't available here." : parcelServiceUnavailable ?? "" :
            starter == WorkshopStarter.LostAddress && flagshipContent == null ? "The Lost Address isn't available here." : "";

        void QueueStarter(WorkshopStarter starter)
        {
            if (RecoveryChoicePending || OnlineStarting || starterDecision != null || StarterBlocker(starter).Length != 0) return;
            starterDecision = starter;
        }

        public void NewMap(WorkshopStarter starter)
        {
            RequireRecoveryChoice();
            if (OnlineStarting) throw new InvalidOperationException("Wait for online preparation to finish before starting a new map.");
            string blocker = StarterBlocker(starter);
            if (blocker.Length != 0) throw new InvalidOperationException(blocker);
            SaveAndRememberDraft();
            string baseFolder = storageFolder ?? DefaultFolder;
            var session = StarterSession(starter, baseFolder, out var template, out bool flagship, out bool parcel);
            string title = WorkshopStarts.Numbered(StarterLabel(starter), WorkshopStorage.Library(baseFolder).Select(entry => entry.Title));
            session.History = new WorkshopHistory(session.Storage.SaveAs(session.History.Current, title));
            if (flagship) flagshipDraftHash = flagshipContent.Hash;
            ActivateDraft(session, false, template, flagship, parcel);
            if (parcel) parcelServiceDraft = CaptureDraft();
            RememberLast(); RememberMode();
            ShowStatus(display.Title + " · a new map of your own · it saves itself");
        }

        DraftSession StarterSession(WorkshopStarter starter, string baseFolder, out WorkshopTemplate? template, out bool flagship, out bool parcel)
        {
            template = null; flagship = parcel = false;
            if (starter == WorkshopStarter.ParcelDepot)
            {
                parcel = true;
                return new DraftSession { History = new WorkshopHistory(parcelServiceSource), Catalogue = parcelServiceCatalogue, Play = parcelServicePlay,
                    Storage = new WorkshopStorage(Path.Combine(baseFolder, "local-courses", WordboundSoloEntry.ParcelServiceWorkshopFolder)), Folder = baseFolder };
            }
            if (starter == WorkshopStarter.LostAddress || starter == WorkshopStarter.Blank && flagshipContent != null)
            {
                flagship = true;
                bool blank = starter == WorkshopStarter.Blank;
                var course = blank ? WorkshopStarts.Blank(flagshipContent.Geometry, "blank", WorkshopStarts.BlankTitle) : flagshipContent.CopyDocument();
                return new DraftSession { History = new WorkshopHistory(new WorkshopSnapshot(course)), Catalogue = flagshipContent.Geometry,
                    Play = MatchStateValidation.Requirements(WorkshopModes.Definition(blank ? WorkshopStarts.BlankMode : "TimeRun")),
                    Storage = new WorkshopStorage(Path.Combine(baseFolder, "templates", "Flagship")), Folder = baseFolder };
            }
            var chosen = starter == WorkshopStarter.RaceCircuit ? WorkshopTemplate.RaceCircuit :
                starter == WorkshopStarter.Arena ? WorkshopTemplate.CombatArena : WorkshopTemplate.FreestyleWorld;
            template = chosen;
            var preset = WorkshopTemplates.Create(chosen);
            var snapshot = starter == WorkshopStarter.Blank ?
                new WorkshopSnapshot(WorkshopStarts.Blank(preset.Catalogue, "blank", WorkshopStarts.BlankTitle)) : preset.Snapshot;
            return new DraftSession { History = new WorkshopHistory(snapshot), Catalogue = preset.Catalogue, Play = preset.Play,
                Storage = new WorkshopStorage(Path.Combine(baseFolder, "templates", chosen.ToString())), Folder = baseFolder };
        }

        void RefreshLibrary()
        {
            try { libraryEntries = WorkshopStorage.Library(storageFolder ?? storage.Folder); libraryError = ""; }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            { libraryEntries = Array.Empty<WorkshopMapEntry>(); libraryError = error.Message; }
            continueEntry = WorkshopStarts.Latest(libraryEntries, entry => OpenBlocker(entry).Length == 0);
            assignedKey = assignedDraft != null && !UsesAssignedCourse ? SessionKey(assignedDraft) : "";
        }

        static string MapKey(string folder, string id) => Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar).ToLowerInvariant() + "|" + id;
        static string EntryKey(WorkshopMapEntry entry) => MapKey(entry.Folder, entry.CourseId);
        static string SessionKey(DraftSession session) => session?.History == null || session.Storage == null ? "" :
            MapKey(session.Storage.Folder, session.History.Current.Course().Id);
        static bool Holds(DraftSession session, WorkshopMapEntry entry) => SessionKey(session) == EntryKey(entry);

        string DeleteBlocker(WorkshopMapEntry entry) => IsCurrentMap(entry) ? "Open another map before deleting this one." :
            assignedKey.Length != 0 && assignedKey == EntryKey(entry) ? "This map belongs to the course you opened · it can't be deleted here." : "";

        void DuplicateMap(WorkshopMapEntry entry)
        {
            RequireRecoveryChoice();
            if (!entry.Readable) throw new InvalidOperationException(entry.Problem);
            if (IsCurrentMap(entry)) KeepCurrentMap();
            var copy = WorkshopStarts.Duplicate(new WorkshopStorage(entry.Folder), entry.CourseId,
                WorkshopStarts.CopyTitle(entry.Title, libraryEntries.Select(item => item.Title)));
            RefreshLibrary();
            ShowStatus("Copied · " + copy.Course().Title + " is in My maps");
        }

        void DeleteMap(WorkshopMapEntry entry)
        {
            RequireRecoveryChoice();
            deleteConfirm = "";
            string blocker = DeleteBlocker(entry);
            if (blocker.Length != 0) throw new InvalidOperationException(blocker);
            WorkshopStarts.Delete(new WorkshopStorage(entry.Folder), entry.CourseId);
            foreach (var template in drafts.Where(pair => Holds(pair.Value, entry)).Select(pair => pair.Key).ToList()) drafts.Remove(template);
            if (Holds(flagshipDraft, entry)) flagshipDraft = null;
            if (Holds(parcelServiceDraft, entry)) parcelServiceDraft = null;
            RefreshLibrary();
            ShowStatus("Deleted " + (entry.Readable ? entry.Title : "the unreadable save"));
        }
    }
}
