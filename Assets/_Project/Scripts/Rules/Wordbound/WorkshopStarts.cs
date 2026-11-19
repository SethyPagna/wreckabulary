using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public static class WorkshopStarts
    {
        public const string BlankTitle = "New map", BlankMode = "Freestyle", RoomId = "Room", GroundId = "Ground";
        public const int StartSpots = 8;
        const int TitleLimit = 80;

        public static CourseDocument Blank(CourseCatalogue catalogue, string id, string title)
        {
            if (catalogue == null) throw new ArgumentNullException(nameof(catalogue));
            var room = catalogue.Modules.Values.OrderBy(item => item.Id, StringComparer.Ordinal).FirstOrDefault()
                ?? throw new ArgumentException("This room set has no rooms.");
            var course = new CourseDocument { Id = id, Title = title, SupportedModes = new List<string> { BlankMode } };
            course.Layers.Add(new CourseLayer { Id = GroundId });
            course.Modules.Add(new CourseModule { Id = RoomId, DefinitionId = room.Id, LayerId = GroundId });
            int back = -room.DepthMm / 2 + Math.Min(2000, room.DepthMm / 4);
            for (int i = 0; i < StartSpots; i++)
                course.Spawns.Add(new CourseSpawn { Id = "Start" + i, ModuleId = RoomId,
                    Position = new CoursePoint(-room.WidthMm / 2 + room.WidthMm * (i + 1) / (StartSpots + 1), 0, back) });
            course.RecoveryAnchors.Add(new CourseRecovery { Id = "Respawn", ModuleId = RoomId, Position = new CoursePoint(0, 0, 0) });
            return course;
        }

        public static string Numbered(string name, IEnumerable<string> taken)
        {
            string stem = (name ?? "").Trim();
            if (stem.Length == 0) stem = BlankTitle;
            var used = new HashSet<string>((taken ?? Enumerable.Empty<string>()).Where(title => title != null)
                .Select(title => title.Trim()), StringComparer.OrdinalIgnoreCase);
            for (int number = 1; number <= 9999; number++)
            {
                string suffix = number == 1 ? "" : " " + number.ToString(CultureInfo.InvariantCulture);
                string candidate = (stem.Length + suffix.Length > TitleLimit ? stem.Substring(0, TitleLimit - suffix.Length).TrimEnd() : stem) + suffix;
                if (!used.Contains(candidate)) return candidate;
            }
            throw new InvalidOperationException("Choose a different map name.");
        }

        public static string CopyTitle(string title, IEnumerable<string> taken) => Numbered((title ?? "").Trim() + " copy", taken);

        public static WorkshopMapEntry Latest(IEnumerable<WorkshopMapEntry> entries, Func<WorkshopMapEntry, bool> openable) =>
            (entries ?? Enumerable.Empty<WorkshopMapEntry>()).Where(entry => entry.Readable && (openable == null || openable(entry)))
                .OrderByDescending(entry => entry.SavedUtc).FirstOrDefault();

        public static WorkshopSnapshot Duplicate(WorkshopStorage storage, string id, string title)
        {
            if (storage == null) throw new ArgumentNullException(nameof(storage));
            var copy = storage.SaveAs(storage.Recover(id, out _), title);
            if (storage.TryLoadSettings(id, out string mode, out RaceFormat format))
                storage.SaveSettings(copy.Course().Id, mode, format);
            return copy;
        }

        public static string Delete(WorkshopStorage storage, string id) =>
            (storage ?? throw new ArgumentNullException(nameof(storage))).SetAside(id);
    }
}
