using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public enum CourseTargetKind { None, Puzzle, Checkpoint, Finish, Keepsake, Station }

    public readonly struct CourseStep
    {
        readonly string text;
        public string Text => text ?? "";
        public CourseTargetKind Kind { get; }
        public WorldVector Target { get; }
        public bool HasTarget => Kind != CourseTargetKind.None;
        public CourseStep(string text, CourseTargetKind kind = CourseTargetKind.None, WorldVector target = default)
        { this.text = text; Kind = kind; Target = target; }
    }

    public readonly struct CourseEdge
    {
        public bool OnScreen { get; }
        public float X { get; }
        public float Y { get; }
        public float Angle { get; }
        public CourseEdge(bool onScreen, float x, float y, float angle) { OnScreen = onScreen; X = x; Y = y; Angle = angle; }
    }

    public sealed class CourseResultSummary
    {
        public string Title { get; }
        public IReadOnlyList<string> Rows { get; }
        public bool Won { get; }
        public CourseResultSummary(string title, IEnumerable<string> rows, bool won)
        { Title = title; Rows = Array.AsReadOnly(rows.ToArray()); Won = won; }
    }

    public static class CourseGuidance
    {
        public const float FloorRise = 1.5f;
        const string PickUp = "Pick up the keepsake, then reach the lap finish.";

        public static CourseStep Next(ModeDefinition mode, ResolvedCourse course, MatchSnapshot frame, string actorId)
        {
            if (mode == null || course == null || frame == null || frame.Progress.Migrating || frame.Progress.Result != null)
                return default;
            int team = TeamOf(frame, actorId);
            if (team < 0) return default;
            if (mode.IsRace)
            {
                var progress = Team(frame.Progress, team);
                return progress == null ? default : Race(course, progress, actorId, frame);
            }
            return WordboundPracticePolicy.Supports(mode) ? Practice(course, frame, actorId) : default;
        }

        public static CourseStep Race(ResolvedCourse course, TeamProgress team, string actorId, MatchSnapshot frame = null)
        {
            if (team.Finished || team.Lap < 0 || team.Lap >= course.Laps.Count) return new CourseStep("Course complete.");
            var lap = course.Laps[team.Lap];
            if (lap.Checkpoints.Count == 0) return new CourseStep("Carry the keepsake to the lap finish.");
            if (team.NextCheckpoint >= lap.Checkpoints.Count)
            {
                var finish = Anchor(course, lap.Checkpoints[lap.Checkpoints.Count - 1]);
                if (team.KeepsakeCarrier == actorId) return Toward("Carry the keepsake to the lap finish.", CourseTargetKind.Finish, finish);
                if (team.KeepsakeCarrier != null) return Toward("A teammate has the keepsake. Meet at the lap finish.", CourseTargetKind.Finish, finish);
                var keepsake = Keepsake(frame, team.Team);
                return keepsake != null ? new CourseStep(PickUp, CourseTargetKind.Keepsake, keepsake.Position) :
                    Toward(PickUp, CourseTargetKind.Finish, finish);
            }
            var checkpoint = lap.Checkpoints[team.NextCheckpoint];
            var done = new HashSet<string>(team.CompletedPuzzles, StringComparer.Ordinal);
            string number = (team.NextCheckpoint + 1).ToString(CultureInfo.InvariantCulture);
            if (Satisfied(checkpoint, done))
                return Toward("Walk to checkpoint " + number + ".", CourseTargetKind.Checkpoint, Anchor(course, checkpoint));
            foreach (string id in checkpoint.PuzzleIds)
            {
                if (done.Contains(id)) continue;
                var puzzle = Puzzle(course, id);
                if (puzzle == null || !puzzle.PrerequisiteIds.All(done.Contains)) continue;
                var site = Site(course, lap.Number, id, checkpoint.Id);
                return site != null ? Toward(Task(puzzle, site), CourseTargetKind.Puzzle, site.Position) : new CourseStep(Task(puzzle, null));
            }
            return new CourseStep("Solve the marked puzzle to open checkpoint " + number + ".");
        }

        public static CourseStep Practice(ResolvedCourse course, MatchSnapshot frame, string actorId)
        {
            if (course == null || frame == null) return default;
            var selected = WordboundPracticePolicy.Selected(course, frame.Practice);
            if (selected != null && !Practised(frame, selected))
            {
                var puzzle = Puzzle(course, selected.PuzzleId);
                if (puzzle != null) return Toward("Practice · " + Task(puzzle, selected), CourseTargetKind.Station, selected.Position);
            }
            var body = Body(frame, actorId);
            ResolvedCoursePhysicalSite nearest = null;
            float best = float.MaxValue;
            foreach (var site in course.PhysicalSites)
            {
                if (Practised(frame, site)) continue;
                float distance = body == null ? 0 : Metres(site.Position).DistanceSquared(body.Position);
                if (distance < best || distance == best && string.CompareOrdinal(site.Hash, nearest.Hash) < 0)
                { best = distance; nearest = site; }
            }
            if (nearest != null) return Toward("Walk to the nearest practice station.", CourseTargetKind.Station, nearest.Position);
            return new CourseStep(course.PhysicalSites.Count == 0 ? "Explore the course." : "You've practised every station here.");
        }

        public static string Task(ResolvedCoursePuzzle puzzle, ResolvedCoursePhysicalSite site)
        {
            string word = puzzle.RecipeId;
            return puzzle.PresetId switch
            {
                "CollectLetters" => "Smash the marked furniture, collect its letters, and spell " + word + ".",
                "PlaceObject" => "Spell " + word + ", then place it on the marked socket.",
                "RedirectWind" => "Spell " + word + " and place it to push the loose " +
                    (string.IsNullOrEmpty(site?.WindReceiverRecipeId) ? "object" : site.WindReceiverRecipeId) + " onto its target.",
                "TimedMechanism" => "Spell " + word + " and use it to slow the arm, then cross with the keepsake.",
                "BuildRoute" => "Spell " + word + " and use it to reach the raised marker.",
                "BalanceWeight" => "Spell " + word + " and cover both floor pads with it.",
                _ => "Spell " + word + " and use it at the marked puzzle."
            };
        }

        public static bool TryDistance(MatchSnapshot frame, string actorId, WorldVector target, out float metres, out float rise)
        {
            var body = Body(frame, actorId);
            metres = body == null ? 0 : (float)Math.Sqrt(body.Position.DistanceSquared(target));
            rise = body == null ? 0 : target.Y - body.Position.Y;
            return body != null;
        }

        public static string DistanceText(float metres, float rise)
        {
            string text = Math.Max(1, (int)Math.Round(metres)).ToString(CultureInfo.InvariantCulture) + " m";
            return rise > FloorRise ? text + " · upstairs" : rise < -FloorRise ? text + " · downstairs" : text;
        }

        public static CourseEdge Edge(float x, float y, float depth, float width, float height, float margin)
        {
            float halfWidth = width / 2 - margin, halfHeight = height / 2 - margin;
            if (halfWidth <= 0 || halfHeight <= 0) return new CourseEdge(false, width / 2, height / 2, 0);
            float dx = x - width / 2, dy = y - height / 2;
            if (depth > 0 && Math.Abs(dx) <= halfWidth && Math.Abs(dy) <= halfHeight) return new CourseEdge(true, x, y, 180);
            if (depth <= 0) { dx = -dx; dy = -dy; }
            if (Math.Abs(dx) < .001f && Math.Abs(dy) < .001f) dy = -1;
            float scale = Math.Min(halfWidth / Math.Max(Math.Abs(dx), .001f), halfHeight / Math.Max(Math.Abs(dy), .001f));
            return new CourseEdge(false, width / 2 + dx * scale, height / 2 + dy * scale, (float)(Math.Atan2(dx, dy) * 180 / Math.PI));
        }

        public static IReadOnlyList<string> Toasts(ModeDefinition mode, ResolvedCourse course, MatchSnapshot before, MatchSnapshot after, string actorId)
        {
            if (mode == null || before == null || after == null || ReferenceEquals(before.Progress, after.Progress) ||
                before.Binding.Hash != after.Binding.Hash || before.Epoch != after.Epoch || before.Progress.Migrating ||
                after.Progress.Migrating || after.Progress.Tick <= before.Progress.Tick) return Array.Empty<string>();
            int team = TeamOf(after, actorId);
            if (team < 0) return Array.Empty<string>();
            var toasts = new List<string>(2);
            if (mode.IsRace) { if (course != null) RaceToasts(mode, course, Team(before.Progress, team), Team(after.Progress, team), toasts); }
            else if (mode.Objective == WordboundObjective.Knockouts)
            {
                var old = Player(before, actorId); var now = Player(after, actorId);
                if (old != null && now != null && now.Knockouts > old.Knockouts)
                    toasts.Add("Knockout! " + now.Knockouts + "/" + mode.KnockoutTarget);
            }
            else if (WordboundPracticePolicy.Supports(mode) && course != null)
                foreach (var mechanism in after.Mechanisms)
                    if (mechanism.Solved && !Solved(before, mechanism)) toasts.Add("Practice complete · " + Recipe(course, mechanism.PuzzleId));
            return toasts;
        }

        static void RaceToasts(ModeDefinition mode, ResolvedCourse course, TeamProgress before, TeamProgress after, List<string> toasts)
        {
            if (before == null || after == null || after.Lap < before.Lap ||
                after.Lap == before.Lap && after.NextCheckpoint < before.NextCheckpoint) return;
            if (after.Lap > before.Lap)
            {
                int left = mode.Laps - after.Lap;
                toasts.Add(after.Finished ? "Course complete!" : left == 1 ? "Lap " + after.Lap + " done · final lap" :
                    "Lap " + after.Lap + " done · " + left + " laps to go");
                return;
            }
            var earlier = before.CompletedPuzzles; var later = after.CompletedPuzzles;
            if (before.NextCheckpoint == after.NextCheckpoint && earlier.Count == later.Count) return;
            var old = new HashSet<string>(earlier, StringComparer.Ordinal);
            foreach (string id in later) if (!old.Contains(id)) toasts.Add(Recipe(course, id) + " solved");
            if (after.NextCheckpoint > before.NextCheckpoint && after.Lap < course.Laps.Count)
                toasts.Add("Checkpoint " + after.NextCheckpoint + "/" + course.Laps[after.Lap].Checkpoints.Count + " reached");
        }

        public static CourseResultSummary Result(ModeDefinition mode, ResolvedCourse course, MatchSnapshot frame, string actorId)
        {
            var result = frame?.Progress.Result;
            if (mode == null || result == null) return null;
            return result.Confirmed ? Summary(mode, course, frame, actorId, result.Outcome, result.WinningTeams) :
                new CourseResultSummary("Confirming result…", Array.Empty<string>(), false);
        }

        public static CourseResultSummary Summary(ModeDefinition mode, ResolvedCourse course, MatchSnapshot frame, string actorId,
            string outcome, IReadOnlyList<int> winners)
        {
            int team = TeamOf(frame, actorId);
            bool won = outcome != "Draw" && outcome != "TimeExpired" && winners.Contains(team);
            string title = outcome == "Draw" ? "Draw" : outcome == "TimeExpired" ? "Time's up" : won ? "You won!" :
                (mode.Objective == WordboundObjective.Knockouts ? Winner(frame, winners) : TeamName(winners.FirstOrDefault())) + " won";
            var rows = new List<string> { "Time " + Clock(frame.Progress.ElapsedTicks / ModeDefinition.TickRate) };
            if (mode.IsRace && course != null)
                foreach (var progress in frame.Progress.Teams)
                    rows.Add((frame.Progress.Teams.Count == 1 ? "" : (progress.Team == team ? "Your team" : TeamName(progress.Team)) + " · ") +
                        Standing(mode, course, progress));
            else if (mode.Objective == WordboundObjective.Knockouts && frame.Progress.Players.Count > 0)
            {
                rows.Add("Your knockouts: " + (Player(frame, actorId)?.Knockouts ?? 0));
                rows.Add("Most knockouts: " + frame.Progress.Players.Max(p => p.Knockouts));
            }
            return new CourseResultSummary(title, rows, won);
        }

        public static string Clock(long seconds)
        {
            seconds = Math.Max(0, seconds);
            return (seconds / 60).ToString("00", CultureInfo.InvariantCulture) + ":" + (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        }

        static string Standing(ModeDefinition mode, ResolvedCourse course, TeamProgress team)
        {
            if (team.Finished || team.Lap >= course.Laps.Count) return "Finished all " + mode.Laps + " laps";
            int lap = Math.Max(0, team.Lap);
            return "Lap " + (lap + 1) + " of " + mode.Laps + " · " + team.NextCheckpoint + " of " +
                course.Laps[lap].Checkpoints.Count + " checkpoints";
        }

        static string TeamName(int team) => "Team " + (team + 1).ToString(CultureInfo.InvariantCulture);

        static string Winner(MatchSnapshot frame, IReadOnlyList<int> winners)
        {
            var seats = frame.Binding.Seats;
            for (int i = 0; i < seats.Count; i++)
                if (winners.Contains(seats[i].Team)) return "P" + (i + 1).ToString(CultureInfo.InvariantCulture);
            return "Another player";
        }

        static bool Satisfied(ResolvedCourseCheckpoint checkpoint, HashSet<string> done) => checkpoint.Solutions.Count == 0
            ? checkpoint.PuzzleIds.All(done.Contains) : checkpoint.Solutions.Any(solution => solution.PuzzleIds.All(done.Contains));

        static bool Practised(MatchSnapshot frame, ResolvedCoursePhysicalSite site)
        {
            foreach (var mechanism in frame.Mechanisms)
                if (mechanism.Solved && mechanism.Team == 0 && mechanism.Lap == site.Lap - 1 && mechanism.PuzzleId == site.PuzzleId) return true;
            return false;
        }

        static bool Solved(MatchSnapshot frame, MatchMechanismState state)
        {
            foreach (var mechanism in frame.Mechanisms)
                if (mechanism.Solved && mechanism.Team == state.Team && mechanism.Lap == state.Lap && mechanism.PuzzleId == state.PuzzleId) return true;
            return false;
        }

        static CourseStep Toward(string text, CourseTargetKind kind, ResolvedCourseRecovery anchor) =>
            anchor == null ? new CourseStep(text) : Toward(text, kind, anchor.Position);

        static CourseStep Toward(string text, CourseTargetKind kind, CoursePoint point) => new CourseStep(text, kind, Metres(point));

        static WorldVector Metres(CoursePoint point) => new WorldVector(point.X / 1000f, point.Y / 1000f, point.Z / 1000f);

        static ResolvedCourseRecovery Anchor(ResolvedCourse course, ResolvedCourseCheckpoint checkpoint)
        {
            foreach (var anchor in course.RecoveryAnchors) if (anchor.Id == checkpoint.RecoveryAnchorId) return anchor;
            return null;
        }

        static ResolvedCoursePuzzle Puzzle(ResolvedCourse course, string id)
        {
            foreach (var puzzle in course.Puzzles) if (puzzle.Id == id) return puzzle;
            return null;
        }

        static string Recipe(ResolvedCourse course, string puzzleId) => Puzzle(course, puzzleId)?.RecipeId ?? "Puzzle";

        static ResolvedCoursePhysicalSite Site(ResolvedCourse course, int lap, string puzzleId, string checkpointId)
        {
            foreach (var site in course.PhysicalSites)
                if (site.Lap == lap && site.PuzzleId == puzzleId && site.CheckpointId == checkpointId) return site;
            return null;
        }

        static int TeamOf(MatchSnapshot frame, string actorId)
        {
            foreach (var seat in frame.Binding.Seats) if (seat.PlayerId == actorId) return seat.Team;
            return -1;
        }

        static TeamProgress Team(ProgressSnapshot progress, int team)
        {
            foreach (var candidate in progress.Teams) if (candidate.Team == team) return candidate;
            return null;
        }

        static PlayerProgress Player(MatchSnapshot frame, string actorId)
        {
            foreach (var player in frame.Progress.Players) if (player.Id == actorId) return player;
            return null;
        }

        static WorldBodyState Body(MatchSnapshot frame, string actorId)
        {
            if (frame == null) return null;
            foreach (var body in frame.Bodies)
                if (body.Kind == WorldBodyKind.Player && body.Active && body.SourceId == actorId) return body;
            return null;
        }

        static WorldBodyState Keepsake(MatchSnapshot frame, int team)
        {
            if (frame == null) return null;
            foreach (var body in frame.Bodies)
                if (body.Kind == WorldBodyKind.Keepsake && body.Active && body.OwnerTeam == team) return body;
            return null;
        }
    }
}
