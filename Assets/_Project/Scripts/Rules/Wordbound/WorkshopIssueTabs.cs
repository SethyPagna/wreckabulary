using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public static class WorkshopIssueTabs
    {
        public const int Build = 0, Fill = 1, Rules = 2, Share = 3, Tabs = 4;
        static readonly (string path, int tab)[] Paths =
        {
            ("$.modules", Build), ("$.layers", Build), ("$.connections", Build), ("$.reservedVolumes", Build),
            ("$.zones", Fill), ("$.puzzles", Fill), ("$.supplies", Fill), ("$.physicalSites", Fill), ("$.placements", Fill),
            ("$.spawns", Rules), ("$.recoveryAnchors", Rules), ("$.checkpoints", Rules), ("$.routeSections", Rules),
            ("$.laps", Rules), ("$.supportedModes", Rules), ("$.play", Rules), ("$.storyAnchors", Rules), ("$.layouts", Rules)
        };

        public static int Tab(CourseIssue issue, CourseDocument document)
        {
            if (issue == null) throw new ArgumentNullException(nameof(issue));
            if (issue.Code == "mode.unsupported") return Rules;
            string path = issue.Path ?? "";
            foreach (var (prefix, tab) in Paths)
                if (path.StartsWith(prefix, StringComparison.Ordinal)) return tab;
            return document == null ? Share : Owner(issue.EntityId ?? "", document);
        }

        public static int[] Counts(IEnumerable<CourseIssue> issues, CourseDocument document)
        {
            var counts = new int[Tabs];
            foreach (var issue in issues ?? Enumerable.Empty<CourseIssue>()) counts[Tab(issue, document)]++;
            return counts;
        }

        static int Owner(string id, CourseDocument d)
        {
            if (id.Length == 0) return Share;
            if (d.Zones.Any(x => x.Id == id) || d.Puzzles.Any(x => x.Id == id) || d.Supplies.Any(x => x.Id == id)) return Fill;
            if (d.Spawns.Any(x => x.Id == id) || d.RecoveryAnchors.Any(x => x.Id == id) || d.Checkpoints.Any(x => x.Id == id) ||
                d.RouteSections.Any(x => x.Id == id)) return Rules;
            if (d.Modules.Any(x => x.Id == id) || d.Connections.Any(x => x.Id == id) || d.Layers.Any(x => x.Id == id)) return Build;
            return Share;
        }
    }
}
