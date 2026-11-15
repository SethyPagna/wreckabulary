using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public static class CourseVariantRules
    {
        public const int MinimumChoiceOptions = 2;
        public const int FirstChoiceLap = 2;
        public const int LengthTolerancePercent = 10;
        public const int ShortcutMinimumSavingPercent = 15;
        public const int ShortcutMaximumSavingPercent = 40;
        public const int StoryAnchorMinimumRadiusMm = 100;
        public const int StoryAnchorMaximumRadiusMm = 50000;
        public const int MaximumLayoutWeight = 10000;
    }

    public static partial class CourseValidator
    {
        internal static IEnumerable<string> CandidatePuzzles(CourseCheckpoint c) =>
            c.PuzzleIds.Concat(c.Solutions.OrderBy(s => s.Id, StringComparer.Ordinal).SelectMany(s => s.PuzzleIds));

        static void ValidateVariantShapes(CourseDocument d, CourseValidation v)
        {
            foreach (var c in d.Connections)
                if (c.Scope != CourseConnection.SharedScope && c.Scope != CourseConnection.TeamScope ||
                    c.Scope == CourseConnection.TeamScope && string.IsNullOrEmpty(c.GatePuzzleId))
                    v.Add("connection.scope", "$.connections", c.Id, "Choose Shared or Team; a team-scoped connection needs a gate puzzle.");
            foreach (var section in d.RouteSections)
            {
                var options = section.Alternatives.Select(a => a.OptionId).ToList();
                if (section.Kind != CourseRouteSection.SeededKind && section.Kind != CourseRouteSection.ChoiceKind)
                    v.Add("route.kind", "$.routeSections", section.Id, "Choose a Seeded or Choice section.");
                else if (section.Kind == CourseRouteSection.SeededKind ? options.Any(o => o != "") :
                    options.Count < CourseVariantRules.MinimumChoiceOptions || options.Count > CourseAlternative.ChoiceOptions.Count ||
                    !options.OrderBy(o => o, StringComparer.Ordinal).SequenceEqual(CourseAlternative.ChoiceOptions.Take(options.Count)))
                    v.Add("choice.options", "$.routeSections", section.Id, "Seeded alternatives have no option; a choice offers options A and B, or A, B and C.");
            }
            foreach (var checkpoint in d.Checkpoints)
            {
                Check(checkpoint.Solutions, CourseLimits.Solutions, x => x.Id, "solutions", v);
                foreach (var solution in checkpoint.Solutions)
                {
                    IdList(solution.PuzzleIds, "solutions", solution.Id, v);
                    if (solution.ShortcutConnectionId != "" && !ValidId(solution.ShortcutConnectionId))
                        v.Add("shortcut.reference", "$.checkpoints", checkpoint.Id, "Shortcut connection IDs must be valid.");
                }
                if (checkpoint.Solutions.Count != 0 && (checkpoint.PuzzleIds.Count != 0 || checkpoint.Solutions.Any(s => s.PuzzleIds.Count == 0)))
                    v.Add("checkpoint.solutions", "$.checkpoints", checkpoint.Id, "A multi-solution checkpoint lists its puzzles only inside its nonempty solutions.");
                var union = checkpoint.Solutions.SelectMany(s => s.PuzzleIds).ToList();
                if (union.Distinct(StringComparer.Ordinal).Count() != union.Count)
                    v.Add("solution.overlap", "$.checkpoints", checkpoint.Id, "Solutions of one checkpoint never share a puzzle.");
            }
            foreach (var anchor in d.StoryAnchors)
            {
                if (!CourseStoryAnchor.Kinds.Contains(anchor.Kind))
                    v.Add("anchor.kind", "$.storyAnchors", anchor.Id, "Choose Hide, Post, Home, Storm, Spill, Job or Exit.");
                if (anchor.RadiusMm < CourseVariantRules.StoryAnchorMinimumRadiusMm || anchor.RadiusMm > CourseVariantRules.StoryAnchorMaximumRadiusMm)
                    v.Add("anchor.radius", "$.storyAnchors", anchor.Id, "Story anchor radius must be 0.1–50 m.");
            }
            foreach (var layout in d.Layouts)
            {
                IdList(layout.ZoneIds, "layouts", layout.Id, v);
                IdList(layout.SpawnIds, "layouts", layout.Id, v);
                if (layout.Weight < 1 || layout.Weight > CourseVariantRules.MaximumLayoutWeight)
                    v.Add("layout.weight", "$.layouts", layout.Id, "Layout weight must be 1–10000.");
            }
        }

        static void ValidateVariants(CourseDocument d, CourseCatalogue catalogue, CoursePlayRequirements play, CourseValidation v)
        {
            var modules = d.Modules.ToDictionary(m => m.Id, StringComparer.Ordinal);
            var connections = d.Connections.ToDictionary(c => c.Id, StringComparer.Ordinal);
            var puzzles = d.Puzzles.ToDictionary(p => p.Id, StringComparer.Ordinal);
            var checkpoints = d.Checkpoints.ToDictionary(c => c.Id, StringComparer.Ordinal);
            var sections = d.RouteSections.ToDictionary(s => s.Id, StringComparer.Ordinal);
            foreach (var checkpoint in d.Checkpoints)
                foreach (var solution in checkpoint.Solutions.Where(s => s.ShortcutConnectionId != ""))
                {
                    if (!connections.TryGetValue(solution.ShortcutConnectionId, out var shortcut))
                    { v.Add("shortcut.reference", "$.checkpoints", checkpoint.Id, "Shortcut connection does not exist."); continue; }
                    int letters = Letters(solution, puzzles);
                    if (checkpoint.Solutions.Any(other => other != solution && Letters(other, puzzles) >= letters))
                        v.Add("shortcut.longest", "$.checkpoints", checkpoint.Id, "Only the strictly longest solution can open a shortcut.");
                    if (shortcut.Scope != CourseConnection.TeamScope)
                        v.Add("shortcut.scope", "$.connections", shortcut.Id, "A shortcut is a team-scoped gate.");
                    if (!solution.PuzzleIds.Contains(shortcut.GatePuzzleId))
                        v.Add("shortcut.gate", "$.connections", shortcut.Id, "A shortcut opens with a puzzle of its own solution.");
                }
            foreach (var lap in d.Laps)
            {
                var choices = lap.SectionIds.Select((id, index) => (Section: sections[id], Index: index))
                    .Where(x => x.Section.Kind == CourseRouteSection.ChoiceKind).ToList();
                if (choices.Count > 1) v.Add("choice.count", "$.laps", lap.Number.ToString(), "A lap offers at most one choice.");
                if (choices.Any(x => x.Index == 0)) v.Add("choice.first", "$.laps", lap.Number.ToString(), "A choice never opens a lap.");
                if (choices.Count != 0 && lap.Number < CourseVariantRules.FirstChoiceLap)
                    v.Add("choice.lap", "$.laps", lap.Number.ToString(), "Choices appear in laps 2 and 3 only.");
            }
            bool schema3 = CourseJson.EffectiveSchema(d) >= 3;
            foreach (var section in d.RouteSections)
            {
                var lengths = section.Alternatives.Select(a => PathLength(section.EntryModuleId, a.ConnectionIds, modules, connections)).ToList();
                bool balanced = lengths.Max() * 100 <= lengths.Min() * (100 + CourseVariantRules.LengthTolerancePercent);
                if (section.Kind == CourseRouteSection.ChoiceKind)
                {
                    if (section.Alternatives.Select(a => a.CheckpointIds.Count).Distinct().Count() != 1)
                        v.Add("choice.checkpoints", "$.routeSections", section.Id, "Every option of a choice has the same number of checkpoints.");
                    if (!balanced) v.Add("choice.length", "$.routeSections", section.Id, "Choice options keep their authored lengths within 10%.");
                }
                else if (schema3 && !balanced)
                    v.Add("route.length", "$.routeSections", section.Id, "Seeded alternatives keep their authored lengths within 10%.");
                foreach (var alternative in section.Alternatives)
                    ValidateShortcutLegs(d, section, alternative, modules, connections, checkpoints, v);
            }
            foreach (var anchor in d.StoryAnchors)
                if (!modules.TryGetValue(anchor.ModuleId, out var module) ||
                    !catalogue.Bounds(module).Contains(anchor.Position, anchor.RadiusMm))
                    v.Add("anchor.location", "$.storyAnchors", anchor.Id, "Keep the story anchor and its radius inside its module.");
            var zoneIds = new HashSet<string>(d.Zones.Select(z => z.Id), StringComparer.Ordinal);
            var spawnIds = new HashSet<string>(d.Spawns.Select(s => s.Id), StringComparer.Ordinal);
            foreach (var layout in d.Layouts)
            {
                if (layout.ZoneIds.Any(id => !zoneIds.Contains(id)) || layout.SpawnIds.Any(id => !spawnIds.Contains(id)))
                { v.Add("layout.reference", "$.layouts", layout.Id, "Layouts reference existing zones and spawns."); continue; }
                var active = CourseResolver.LayoutSpawnIds(d, layout.Id);
                var spawns = d.Spawns.Where(s => active.Contains(s.Id)).ToList();
                if (spawns.Count(s => s.Kind == "Start") < play.MaxOccupants ||
                    play.Capability == CourseCapability.Arena && spawns.Count(s => s.Kind == "Respawn") < play.MaxOccupants)
                    v.Add("layout.capacity", "$.layouts", layout.Id, "Every layout provides enough starts and arena respawns for the selected capacity.");
            }
        }

        static void ValidateShortcutLegs(CourseDocument d, CourseRouteSection section, CourseAlternative alternative,
            Dictionary<string, CourseModule> modules, Dictionary<string, CourseConnection> connections,
            Dictionary<string, CourseCheckpoint> checkpoints, CourseValidation v)
        {
            var trail = Trail(d, section.EntryModuleId, alternative.ConnectionIds, new CourseValidation(), alternative.Id);
            var steps = new List<int>();
            int step = 0;
            foreach (string id in alternative.CheckpointIds)
                steps.Add(step = CheckpointStep(checkpoints[id], trail, alternative.ConnectionIds, step));
            for (int i = 0; i < alternative.CheckpointIds.Count; i++)
            {
                var checkpoint = checkpoints[alternative.CheckpointIds[i]];
                foreach (var solution in checkpoint.Solutions.Where(s => connections.ContainsKey(s.ShortcutConnectionId)))
                {
                    var shortcut = connections[solution.ShortcutConnectionId];
                    var next = i + 1 < alternative.CheckpointIds.Count ? checkpoints[alternative.CheckpointIds[i + 1]] : null;
                    if (next == null || shortcut.FromModuleId != checkpoint.ModuleId || shortcut.ToModuleId != next.ModuleId)
                    { v.Add("shortcut.route", "$.routeSections", alternative.Id, "A shortcut joins its checkpoint's room directly to the next checkpoint's room."); continue; }
                    long leg = PathLength(trail[steps[i]], alternative.ConnectionIds.Skip(steps[i]).Take(steps[i + 1] - steps[i]), modules, connections);
                    long direct = Distance(modules[checkpoint.ModuleId].Position, shortcut.From) + Distance(shortcut.From, shortcut.To) +
                        Distance(shortcut.To, modules[next.ModuleId].Position);
                    long saved = leg - direct;
                    if (leg <= 0 || saved * 100 < CourseVariantRules.ShortcutMinimumSavingPercent * leg ||
                        saved * 100 > CourseVariantRules.ShortcutMaximumSavingPercent * leg)
                        v.Add("shortcut.saving", "$.routeSections", alternative.Id, "A shortcut saves 15–40% of the leg it replaces.");
                }
            }
        }

        static int Letters(CourseSolution solution, Dictionary<string, CoursePuzzle> puzzles) =>
            solution.PuzzleIds.Sum(id => puzzles[id].RequiredLetters.Length);

        static long PathLength(string entry, IEnumerable<string> connectionIds,
            Dictionary<string, CourseModule> modules, Dictionary<string, CourseConnection> connections)
        {
            long total = 0;
            string current = entry;
            foreach (string id in connectionIds)
            {
                var edge = connections[id];
                bool forward = edge.FromModuleId == current;
                string next = forward ? edge.ToModuleId : edge.FromModuleId;
                var from = forward ? edge.From : edge.To;
                var to = forward ? edge.To : edge.From;
                total += Distance(modules[current].Position, from) + Distance(from, to) + Distance(to, modules[next].Position);
                current = next;
            }
            return total;
        }

        static long Distance(CoursePoint a, CoursePoint b)
        {
            double dx = (double)a.X - b.X, dy = (double)a.Y - b.Y, dz = (double)a.Z - b.Z;
            return (long)Math.Round(Math.Sqrt(dx * dx + dy * dy + dz * dz), MidpointRounding.AwayFromZero);
        }
    }
}
