using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public sealed class LastPlayChoice
    {
        public const string StorageKey = "wv.lobby.last";
        public const string Version = "v1";
        public const string Practice = "practice", Matchmaking = "matchmaking", Workshop = "workshop";
        public const string ShownHouse = "house", ShownCourse = "course";
        static readonly string[] Tabs = { Practice, Matchmaking, Workshop };
        const string Yes = "1", No = "0", Separators = ";=+";
        static readonly string[] Keys = { "tab", "mode", "maps", "course", "courseMap", "shown", "chosen" };

        public string Tab { get; }
        public string Mode { get; }
        public IReadOnlyList<string> Houses { get; }
        public string Course { get; }
        public string CourseMap { get; }
        public bool OnCourse { get; }
        public bool Chosen { get; }

        public string Maps => string.Join("+", Houses);

        public LastPlayChoice(string tab, string mode, IEnumerable<string> houses, string course, string courseMap, bool onCourse, bool chosen)
        {
            if (!Tabs.Contains(tab)) throw new ArgumentException("Choose the Practice, Matchmaking or Create tab.", nameof(tab));
            var ticked = (houses ?? Array.Empty<string>()).ToArray();
            if (!Word(mode) || !Word(course) || !Word(courseMap) || !ticked.All(Word) ||
                ticked.Distinct(StringComparer.Ordinal).Count() != ticked.Length)
                throw new ArgumentException("A remembered choice needs plain, distinct ids.");
            Tab = tab; Mode = mode; Houses = ticked; Course = course; CourseMap = courseMap; OnCourse = onCourse; Chosen = chosen;
        }

        public string Save() => string.Join(";", Version, "tab=" + Tab, "mode=" + Mode, "maps=" + Maps,
            "course=" + Course, "courseMap=" + CourseMap, "shown=" + (OnCourse ? ShownCourse : ShownHouse), "chosen=" + (Chosen ? Yes : No));

        public static LastPlayChoice Load(string saved, IEnumerable<string> modes, IEnumerable<string> houses,
            IEnumerable<string> courses, IEnumerable<string> courseMaps)
        {
            var values = Parse(saved);
            if (values == null) return null;
            string shown = values["shown"], chosen = values["chosen"];
            var installed = (houses ?? Array.Empty<string>()).ToArray();
            var ticked = Ticked(values["maps"], installed);
            if (ticked == null || !Tabs.Contains(values["tab"]) || !Listed(modes, values["mode"]) ||
                !Listed(courses, values["course"]) || !Listed(courseMaps, values["courseMap"]) ||
                (shown != ShownHouse && shown != ShownCourse) || (chosen != Yes && chosen != No))
                return null;
            return new LastPlayChoice(values["tab"], values["mode"], ticked, values["course"], values["courseMap"], shown == ShownCourse, chosen == Yes);
        }

        static Dictionary<string, string> Parse(string saved)
        {
            var parts = (saved ?? "").Split(';');
            if (parts[0] != Version) return null;
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string part in parts.Skip(1))
            {
                int split = part.IndexOf('=');
                if (split <= 0 || values.ContainsKey(part.Substring(0, split))) return null;
                values[part.Substring(0, split)] = part.Substring(split + 1);
            }
            return Keys.All(values.ContainsKey) ? values : null;
        }

        static string[] Ticked(string maps, string[] installed)
        {
            if (maps.Length == 0) return Array.Empty<string>();
            var listed = maps.Split('+');
            if (!listed.All(Word) || listed.Distinct(StringComparer.Ordinal).Count() != listed.Length) return null;
            var known = installed.Where(listed.Contains).ToArray();
            return known.Length == installed.Length ? Array.Empty<string>() : known;
        }

        static bool Listed(IEnumerable<string> options, string id) => options != null && Word(id) && options.Contains(id);

        static bool Word(string id) => !string.IsNullOrEmpty(id) &&
            id.All(letter => !char.IsWhiteSpace(letter) && !char.IsControl(letter) && Separators.IndexOf(letter) < 0);
    }
}
