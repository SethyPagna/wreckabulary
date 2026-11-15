using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class RaceSolution
    {
        public const int MaximumPuzzles = 64;
        public string Id { get; }
        public IReadOnlyList<string> PuzzleIds { get; }
        public string ShortcutConnectionId { get; }

        public RaceSolution(string id, IEnumerable<string> puzzleIds, string shortcutConnectionId = "")
        {
            var puzzles = puzzleIds?.ToArray();
            if (!WordboundIdentity.IsId(id)) throw new ArgumentException("Invalid solution ID.", nameof(id));
            if (puzzles == null || puzzles.Length < 1 || puzzles.Length > MaximumPuzzles || puzzles.Any(p => !WordboundIdentity.IsId(p)) ||
                puzzles.Distinct(StringComparer.Ordinal).Count() != puzzles.Length)
                throw new ArgumentException("A solution needs bounded, unique puzzle IDs.", nameof(puzzleIds));
            if (shortcutConnectionId == null || shortcutConnectionId != "" && !WordboundIdentity.IsId(shortcutConnectionId))
                throw new ArgumentException("Invalid shortcut connection ID.", nameof(shortcutConnectionId));
            Id = id; PuzzleIds = Array.AsReadOnly(puzzles); ShortcutConnectionId = shortcutConnectionId;
        }

        RaceSolution(string[] puzzles) { Id = ""; PuzzleIds = Array.AsReadOnly(puzzles); ShortcutConnectionId = ""; }

        internal static RaceSolution Whole(string[] puzzles) => new RaceSolution(puzzles);

        public bool Satisfied(TeamProgress team) => team != null && PuzzleIds.All(team.HasPuzzle);
    }

    public sealed class RaceCheckpoint
    {
        public const int MaximumSolutions = CourseLimits.Solutions;
        public string Id { get; }
        public IReadOnlyList<string> Puzzles { get; }
        public IReadOnlyList<RaceSolution> Solutions { get; }
        internal bool ExplicitSolutions { get; }

        public RaceCheckpoint(string id, params string[] puzzles)
        {
            if (!WordboundIdentity.IsId(id)) throw new ArgumentException("Invalid checkpoint ID.", nameof(id));
            if (puzzles == null || puzzles.Length > RaceSolution.MaximumPuzzles || puzzles.Any(p => !WordboundIdentity.IsId(p)) ||
                puzzles.Distinct(StringComparer.Ordinal).Count() != puzzles.Length)
                throw new ArgumentException("Puzzle IDs must be bounded and unique.", nameof(puzzles));
            var copy = (string[])puzzles.Clone();
            Id = id; Puzzles = Array.AsReadOnly(copy);
            Solutions = Array.AsReadOnly(new[] { RaceSolution.Whole(copy) });
        }

        public RaceCheckpoint(string id, IEnumerable<RaceSolution> solutions)
        {
            if (!WordboundIdentity.IsId(id)) throw new ArgumentException("Invalid checkpoint ID.", nameof(id));
            var list = solutions?.ToArray();
            if (list == null || list.Length < 1 || list.Length > MaximumSolutions || list.Any(s => s == null) ||
                list.Select(s => s.Id).Distinct(StringComparer.Ordinal).Count() != list.Length)
                throw new ArgumentException("A checkpoint needs one to three uniquely named solutions.", nameof(solutions));
            var ordered = list.OrderBy(s => s.Id, StringComparer.Ordinal).ToArray();
            var union = ordered.SelectMany(s => s.PuzzleIds).ToArray();
            if (union.Length > RaceSolution.MaximumPuzzles || union.Distinct(StringComparer.Ordinal).Count() != union.Length)
                throw new ArgumentException("Solutions of one checkpoint never share a puzzle.", nameof(solutions));
            Id = id; Puzzles = Array.AsReadOnly(union); Solutions = Array.AsReadOnly(ordered); ExplicitSolutions = true;
        }

        public bool Satisfied(TeamProgress team) => Solutions.Any(s => s.Satisfied(team));

        internal IEnumerable<string> Identity()
        {
            yield return Id;
            yield return Solutions.Count.ToString(CultureInfo.InvariantCulture);
            foreach (var solution in Solutions)
            {
                yield return solution.Id;
                yield return solution.PuzzleIds.Count.ToString(CultureInfo.InvariantCulture);
                foreach (string puzzle in solution.PuzzleIds) yield return puzzle;
                yield return solution.ShortcutConnectionId;
            }
        }
    }

    public sealed class RaceLapVariant
    {
        public const int MaximumCheckpoints = 256;
        public string OptionId { get; }
        public IReadOnlyList<RaceCheckpoint> Checkpoints { get; }

        public RaceLapVariant(string optionId, params RaceCheckpoint[] checkpoints)
        {
            if (optionId == null || optionId != "" && !CourseAlternative.ChoiceOptions.Contains(optionId))
                throw new ArgumentException("A lap variant is the plain route or option A, B or C.", nameof(optionId));
            if (checkpoints == null || checkpoints.Length < 1 || checkpoints.Length > MaximumCheckpoints ||
                checkpoints.Any(c => c == null) ||
                checkpoints.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != checkpoints.Length ||
                checkpoints.SelectMany(c => c.Puzzles).Distinct(StringComparer.Ordinal).Count() != checkpoints.Sum(c => c.Puzzles.Count))
                throw new ArgumentException("Each lap needs unique, ordered checkpoints and puzzle IDs.");
            OptionId = optionId;
            Checkpoints = Array.AsReadOnly((RaceCheckpoint[])checkpoints.Clone());
        }
    }

    public sealed class RaceLap
    {
        public string Start { get; }
        public string Finish { get; }
        public string Story { get; }
        public IReadOnlyList<RaceLapVariant> Variants { get; }
        public IReadOnlyList<RaceCheckpoint> Checkpoints => Variants[0].Checkpoints;
        public bool HasChoice => Variants.Count > 1;
        public int Divergence { get; }

        public RaceLap(string start, string finish, string story, params RaceCheckpoint[] checkpoints)
        {
            RequireLabels(start, finish, story);
            Start = start; Finish = finish; Story = story;
            Variants = Array.AsReadOnly(new[] { new RaceLapVariant("", checkpoints) });
            Divergence = Checkpoints.Count;
        }

        public RaceLap(string start, string finish, string story, IEnumerable<RaceLapVariant> variants)
        {
            RequireLabels(start, finish, story);
            var list = variants?.ToArray();
            if (list == null || list.Length < 1 || list.Any(v => v == null))
                throw new ArgumentException("A lap needs at least one route.", nameof(variants));
            list = list.OrderBy(v => v.OptionId, StringComparer.Ordinal).ToArray();
            bool plain = list.Length == 1 && list[0].OptionId == "";
            bool choice = list.Length >= CourseVariantRules.MinimumChoiceOptions && list.Length <= CourseAlternative.ChoiceOptions.Count &&
                list.Select(v => v.OptionId).SequenceEqual(CourseAlternative.ChoiceOptions.Take(list.Length));
            if (!plain && !choice)
                throw new ArgumentException("A lap has one plain route, or options A and B, or A, B and C.", nameof(variants));
            if (list.Select(v => v.Checkpoints.Count).Distinct().Count() != 1)
                throw new ArgumentException("Every option of a lap has the same number of checkpoints.", nameof(variants));
            Start = start; Finish = finish; Story = story;
            Variants = Array.AsReadOnly(list);
            int shared = 0;
            while (shared < Checkpoints.Count &&
                list.All(v => v.Checkpoints[shared].Identity().SequenceEqual(list[0].Checkpoints[shared].Identity()))) shared++;
            Divergence = shared;
        }

        public RaceLapVariant For(string optionId) =>
            optionId == "" ? Variants[0] : Variants.FirstOrDefault(v => v.OptionId == optionId);

        public static RaceLap[] FromCourse(ResolvedCourse course)
        {
            if (course == null) throw new ArgumentNullException(nameof(course));
            return course.Laps.Select(lap => new RaceLap(lap.StartModuleId, lap.FinishModuleId, lap.NarrativeId,
                course.LapVariants.Where(v => v.Number == lap.Number)
                    .Select(v => new RaceLapVariant(v.OptionId, v.Checkpoints.Select(Checkpoint).ToArray())))).ToArray();
        }

        static RaceCheckpoint Checkpoint(ResolvedCourseCheckpoint c) => c.Solutions.Count == 0 ?
            new RaceCheckpoint(c.Id, c.PuzzleIds.ToArray()) :
            new RaceCheckpoint(c.Id, c.Solutions.Select(s => new RaceSolution(s.Id, s.PuzzleIds, s.ShortcutConnectionId)));

        static void RequireLabels(string start, string finish, string story)
        {
            if (!WordboundIdentity.IsId(start) || !WordboundIdentity.IsId(finish))
                throw new ArgumentException("A lap needs valid start and finish IDs.");
            if (story == null || story.Length > 160) throw new ArgumentException("Story label is too long.");
        }
    }
}
