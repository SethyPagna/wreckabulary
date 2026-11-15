using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Tests
{
    public sealed class CourseGuidanceTests
    {
        const string Player = "guide-player", Mate = "guide-mate", Rival = "guide-rival", Seed = "0123456789abcdef";

        sealed class Match
        {
            public readonly ItemCatalogue Items = TestData.Catalogue();
            public readonly ModeDefinition Mode;
            public readonly ResolvedCourse Course;
            public readonly MatchProgress Progress;
            readonly WordboundSeat[] seats;
            readonly string courseHash, manifestHash;

            public Match(string modeId, WordboundSeat[] roster, bool parcel = false, ResolvedCourse course = null)
            {
                Mode = ModeDefinition.For(modeId);
                seats = roster;
                if (course == null && Mode.Objective != WordboundObjective.Knockouts)
                {
                    var result = parcel ? WordboundParcelServiceContent.Create(Items).Resolve(Seed, MatchStateValidation.Requirements(Mode))
                        : WordboundFlagshipContent.Create(Items).Resolve(Seed, MatchStateValidation.Requirements(Mode));
                    Assert.IsTrue(result.Success, string.Join("\n", result.Issues));
                    course = result.Manifest;
                }
                Course = course;
                courseHash = Course?.SourceHash ?? new string('c', 64);
                manifestHash = Course?.Hash ?? new string('d', 64);
                Progress = new MatchProgress(Mode, seats, Mode.IsRace ? MatchStateValidation.Route(Course) : null);
            }

            public MatchSnapshot Frame(WorldVector? at = null, IEnumerable<WorldBodyState> extra = null,
                IEnumerable<MatchMechanismState> mechanisms = null, MatchPracticeState practice = null, string matchId = "guide")
            {
                var binding = new MatchBinding(matchId, MatchSnapshotCodec.ProtocolVersion, Mode.Hash, courseHash, manifestHash,
                    new string('a', 64), new string('b', 64), seats);
                var bodies = new List<WorldBodyState>();
                if (at.HasValue) bodies.Add(new WorldBodyState("player:0", WorldBodyKind.Player, at.Value, sourceId: Player));
                if (extra != null) bodies.AddRange(extra);
                return new MatchSnapshot(binding, 0, 1, 1, Progress.Capture(), new Economy(Items, new GameRules()).Capture(),
                    new CommandJournal(binding.Hash, seats.Select(s => s.PlayerId)).Capture(), Array.Empty<MatchActorState>(), bodies,
                    Array.Empty<MatchResourceState>(), mechanisms ?? Array.Empty<MatchMechanismState>(), Array.Empty<MatchRandomState>(),
                    Array.Empty<string>(), practice: practice);
            }

            public TeamProgress Team => Progress.Team(0);
            public ResolvedCourseCheckpoint Checkpoint => Course.Laps[Team.Lap].Checkpoints[Team.NextCheckpoint];

            public void Advance(params ProgressEvent[] events) => Progress.Advance(Progress.Tick + 1, (Progress.Tick + 1) * 17, events);

            public void SolvePuzzles() => Advance(Checkpoint.PuzzleIds.Select(id => new ProgressEvent(ProgressEventKind.PuzzleSatisfied, Player, id, Team.Lap)).ToArray());

            public void ReachCheckpoint()
            {
                SolvePuzzles();
                Advance(new ProgressEvent(ProgressEventKind.CheckpointReached, Player, Checkpoint.Id, Team.Lap));
            }

            public void ReachFinishStage()
            {
                int lap = Team.Lap;
                while (Team.Lap == lap && Team.NextCheckpoint < Course.Laps[lap].Checkpoints.Count) ReachCheckpoint();
            }

            public void FinishLap()
            {
                ReachFinishStage();
                if (Team.KeepsakeCarrier == null) Assert.IsTrue(Progress.TakeKeepsake(Player));
                Advance(new ProgressEvent(ProgressEventKind.FinishReached, Player, Course.Laps[Team.Lap].FinishModuleId, Team.Lap));
            }
        }

        static WordboundSeat[] Solo => new[] { new WordboundSeat(Player, 0, 0) };
        static WorldVector Metres(CoursePoint p) => new WorldVector(p.X / 1000f, p.Y / 1000f, p.Z / 1000f);
        static WorldVector Anchor(ResolvedCourse course, ResolvedCourseCheckpoint checkpoint) =>
            Metres(course.RecoveryAnchors.Single(r => r.Id == checkpoint.RecoveryAnchorId).Position);
        static void AreEqual(WorldVector expected, WorldVector actual) => Assert.Less(expected.DistanceSquared(actual), .000001f);

        [TestCase(false), TestCase(true)]
        public void RaceStepNamesEachPuzzleThenWalksToItsCheckpoint(bool parcel)
        {
            var m = new Match("TimeRun", Solo, parcel);
            int puzzles = 0;
            for (int i = 0; i < Math.Min(6, m.Course.Laps[0].Checkpoints.Count); i++)
            {
                var checkpoint = m.Checkpoint;
                var step = CourseGuidance.Next(m.Mode, m.Course, m.Frame(), Player);
                if (checkpoint.PuzzleIds.Count > 0)
                {
                    puzzles++;
                    Assert.AreEqual(CourseTargetKind.Puzzle, step.Kind, step.Text);
                    var site = m.Course.PhysicalSites.Single(s => s.Lap == m.Course.Laps[0].Number &&
                        checkpoint.PuzzleIds.Contains(s.PuzzleId) && s.CheckpointId == checkpoint.Id && Metres(s.Position).DistanceSquared(step.Target) < .000001f);
                    var puzzle = m.Course.Puzzles.Single(p => p.Id == site.PuzzleId);
                    Assert.AreEqual(CourseGuidance.Task(puzzle, site), step.Text);
                    StringAssert.Contains(puzzle.RecipeId, step.Text);
                    m.SolvePuzzles();
                    step = CourseGuidance.Next(m.Mode, m.Course, m.Frame(), Player);
                }
                Assert.AreEqual(CourseTargetKind.Checkpoint, step.Kind);
                Assert.AreEqual("Walk to checkpoint " + (i + 1) + ".", step.Text);
                AreEqual(Anchor(m.Course, checkpoint), step.Target);
                m.ReachCheckpoint();
                Assert.AreEqual(i + 1, m.Team.NextCheckpoint);
            }
            Assert.Greater(puzzles, 0, "The opening checkpoints must include at least one puzzle.");
        }

        [Test]
        public void LostAddressThirdCheckpointNamesTable()
        {
            var m = new Match("TimeRun", Solo);
            m.ReachCheckpoint(); m.ReachCheckpoint();
            StringAssert.Contains("TABLE", CourseGuidance.Next(m.Mode, m.Course, m.Frame(), Player).Text);
        }

        [Test]
        public void FinishStageFollowsTheKeepsakeAndItsCarrier()
        {
            var m = new Match("TimeRun", new[] { new WordboundSeat(Player, 0, 0), new WordboundSeat(Mate, 0, 1) });
            m.ReachFinishStage();
            var lap = m.Course.Laps[0];
            var finish = Anchor(m.Course, lap.Checkpoints[lap.Checkpoints.Count - 1]);
            var dropped = new WorldVector(1, 0, 2);
            var keepsake = new WorldBodyState("keepsake:0", WorldBodyKind.Keepsake, dropped, ownerTeam: 0, protectedResource: true);
            var step = CourseGuidance.Next(m.Mode, m.Course, m.Frame(extra: new[] { keepsake }), Player);
            Assert.AreEqual("Pick up the keepsake, then reach the lap finish.", step.Text);
            Assert.AreEqual(CourseTargetKind.Keepsake, step.Kind); AreEqual(dropped, step.Target);
            step = CourseGuidance.Next(m.Mode, m.Course, m.Frame(), Player);
            Assert.AreEqual(CourseTargetKind.Finish, step.Kind); AreEqual(finish, step.Target);
            Assert.IsTrue(m.Progress.TakeKeepsake(Mate));
            step = CourseGuidance.Next(m.Mode, m.Course, m.Frame(), Player);
            Assert.AreEqual("A teammate has the keepsake. Meet at the lap finish.", step.Text);
            Assert.AreEqual("Carry the keepsake to the lap finish.", CourseGuidance.Next(m.Mode, m.Course, m.Frame(), Mate).Text);
            AreEqual(finish, step.Target);
        }

        [Test]
        public void FinishedCourseClearsGuidanceAndSummarisesTheRun()
        {
            var m = new Match("TimeRun", Solo, parcel: true);
            for (int lap = 0; lap < 3; lap++) m.FinishLap();
            var frame = m.Frame();
            Assert.AreEqual("Win", frame.Progress.Result.Outcome);
            var step = CourseGuidance.Next(m.Mode, m.Course, frame, Player);
            Assert.IsFalse(step.HasTarget); Assert.AreEqual("", step.Text);
            Assert.AreEqual("Course complete.", CourseGuidance.Race(m.Course, frame.Progress.Teams.Single(), Player).Text);
            Assert.AreEqual("Confirming result…", CourseGuidance.Result(m.Mode, m.Course, frame, Player).Title);
            var summary = CourseGuidance.Summary(m.Mode, m.Course, frame, Player, "Win", new[] { 0 });
            Assert.AreEqual("You won!", summary.Title); Assert.IsTrue(summary.Won);
            CollectionAssert.AreEqual(new[] { "Time " + CourseGuidance.Clock(frame.Progress.ElapsedTicks / ModeDefinition.TickRate),
                "Finished all 3 laps" }, summary.Rows);
            Assert.AreEqual("Time's up", CourseGuidance.Summary(m.Mode, m.Course, frame, Player, "TimeExpired", new int[0]).Title);
            Assert.AreEqual("Draw", CourseGuidance.Summary(m.Mode, m.Course, frame, Player, "Draw", new[] { 0 }).Title);
            Assert.IsNull(CourseGuidance.Result(m.Mode, m.Course, new Match("TimeRun", Solo, course: m.Course).Frame(), Player));
        }

        [Test]
        public void RelaySummaryNamesTheWinningTeamAndBothStandings()
        {
            var timeRun = new Match("TimeRun", Solo);
            var m = new Match("TeamRace", new[] { new WordboundSeat(Player, 0, 0), new WordboundSeat(Rival, 1, 0) }, course: timeRun.Course);
            m.ReachCheckpoint();
            var summary = CourseGuidance.Summary(m.Mode, m.Course, m.Frame(), Player, "Win", new[] { 1 });
            Assert.AreEqual("Team 2 won", summary.Title); Assert.IsFalse(summary.Won);
            int count = m.Course.Laps[0].Checkpoints.Count;
            CollectionAssert.AreEqual(new[] { "Your team · Lap 1 of 3 · 1 of " + count + " checkpoints",
                "Team 2 · Lap 1 of 3 · 0 of " + count + " checkpoints" }, summary.Rows.Skip(1));
        }

        [TestCase(false), TestCase(true)]
        public void PracticeStepPrefersTheActiveStationThenTheNearestUnpractisedOne(bool parcel)
        {
            var m = new Match("Freestyle", Solo, parcel);
            var sites = m.Course.PhysicalSites;
            Assert.Greater(sites.Count, 1);
            var first = sites[0];
            var step = CourseGuidance.Next(m.Mode, m.Course, m.Frame(Metres(first.Position)), Player);
            Assert.AreEqual("Walk to the nearest practice station.", step.Text);
            Assert.AreEqual(CourseTargetKind.Station, step.Kind);
            Assert.AreEqual(0, Metres(first.Position).DistanceSquared(step.Target), .000001f);
            var done = new[] { new MatchMechanismState(first.PuzzleId, 0, first.Lap - 1, true) };
            step = CourseGuidance.Next(m.Mode, m.Course, m.Frame(Metres(first.Position), mechanisms: done), Player);
            var target = step.Target;
            Assert.IsTrue(sites.Any(s => Metres(s.Position).DistanceSquared(target) < .000001f && (s.PuzzleId != first.PuzzleId || s.Lap != first.Lap)),
                "A practised station is never the next target.");
            var active = sites.Last(s => s.PuzzleId != first.PuzzleId || s.Lap != first.Lap);
            step = CourseGuidance.Next(m.Mode, m.Course, m.Frame(Metres(first.Position), mechanisms: done,
                practice: new MatchPracticeState(active.Hash, 1)), Player);
            var puzzle = m.Course.Puzzles.Single(p => p.Id == active.PuzzleId);
            Assert.AreEqual("Practice · " + CourseGuidance.Task(puzzle, active), step.Text);
            AreEqual(Metres(active.Position), step.Target);
            var all = sites.Select(s => (s.PuzzleId, s.Lap)).Distinct().Select(s => new MatchMechanismState(s.PuzzleId, 0, s.Lap - 1, true)).ToArray();
            step = CourseGuidance.Next(m.Mode, m.Course, m.Frame(Metres(first.Position), mechanisms: all), Player);
            Assert.AreEqual("You've practised every station here.", step.Text); Assert.IsFalse(step.HasTarget);
        }

        [Test]
        public void TasksAlwaysSaySpellAndNameTheRecipe()
        {
            var m = new Match("TimeRun", Solo);
            foreach (var puzzle in m.Course.Puzzles)
            {
                string text = CourseGuidance.Task(puzzle, null);
                StringAssert.Contains(puzzle.RecipeId, text);
                StringAssert.Contains("pell " + puzzle.RecipeId, text);
                Assert.IsFalse(text.IndexOf("craft", StringComparison.OrdinalIgnoreCase) >= 0, text);
            }
        }

        [Test]
        public void DistanceRoundsToWholeMetresAndNamesTheFloor()
        {
            var m = new Match("TimeRun", Solo);
            Assert.IsFalse(CourseGuidance.TryDistance(m.Frame(), Player, new WorldVector(1, 0, 0), out _, out _));
            Assert.IsTrue(CourseGuidance.TryDistance(m.Frame(new WorldVector(0, 0, 0)), Player, new WorldVector(3, 4, 0), out float metres, out float rise));
            Assert.AreEqual(5, metres, .0001f); Assert.AreEqual(4, rise, .0001f);
            Assert.AreEqual("5 m · upstairs", CourseGuidance.DistanceText(metres, rise));
            Assert.AreEqual("14 m · downstairs", CourseGuidance.DistanceText(14.4f, -2));
            Assert.AreEqual("15 m", CourseGuidance.DistanceText(14.6f, 1));
            Assert.AreEqual("1 m", CourseGuidance.DistanceText(.2f, 0));
            Assert.AreEqual("02:05", CourseGuidance.Clock(125)); Assert.AreEqual("00:00", CourseGuidance.Clock(-3));
        }

        [Test]
        public void EdgeKeepsVisibleTargetsAndClampsOthersToTheBorder()
        {
            var visible = CourseGuidance.Edge(500, 250, 10, 1000, 500, 50);
            Assert.IsTrue(visible.OnScreen); Assert.AreEqual(500, visible.X); Assert.AreEqual(250, visible.Y); Assert.AreEqual(180, visible.Angle);
            var right = CourseGuidance.Edge(2000, 250, 10, 1000, 500, 50);
            Assert.IsFalse(right.OnScreen); Assert.AreEqual(950, right.X, .01f); Assert.AreEqual(250, right.Y, .01f); Assert.AreEqual(90, right.Angle, .01f);
            var above = CourseGuidance.Edge(500, 900, 5, 1000, 500, 50);
            Assert.AreEqual(450, above.Y, .01f); Assert.AreEqual(0, above.Angle, .01f);
            var behind = CourseGuidance.Edge(600, 250, -5, 1000, 500, 50);
            Assert.IsFalse(behind.OnScreen); Assert.AreEqual(50, behind.X, .01f); Assert.AreEqual(-90, behind.Angle, .01f);
            var straightBehind = CourseGuidance.Edge(500, 250, -1, 1000, 500, 50);
            Assert.AreEqual(50, straightBehind.Y, .01f); Assert.AreEqual(180, Math.Abs(straightBehind.Angle), .01f);
            var corner = CourseGuidance.Edge(5000, 5000, 1, 1000, 500, 50);
            Assert.LessOrEqual(corner.X, 950.01f); Assert.LessOrEqual(corner.Y, 450.01f);
            Assert.IsTrue(corner.X >= 949.99f || corner.Y >= 449.99f);
        }

        [Test]
        public void ToastsReportPuzzlesCheckpointsAndLapsOnceAndIgnoreReplays()
        {
            var m = new Match("TimeRun", Solo, parcel: true);
            while (m.Checkpoint.PuzzleIds.Count == 0) m.ReachCheckpoint();
            var start = m.Frame();
            string recipe = m.Course.Puzzles.Single(p => p.Id == m.Checkpoint.PuzzleIds[0]).RecipeId;
            int number = m.Team.NextCheckpoint + 1, count = m.Course.Laps[0].Checkpoints.Count;
            m.SolvePuzzles();
            var solved = m.Frame();
            CollectionAssert.Contains(CourseGuidance.Toasts(m.Mode, m.Course, start, solved, Player), recipe + " solved");
            m.Advance(new ProgressEvent(ProgressEventKind.CheckpointReached, Player, m.Checkpoint.Id, 0));
            var reached = m.Frame();
            CollectionAssert.AreEqual(new[] { "Checkpoint " + number + "/" + count + " reached" },
                CourseGuidance.Toasts(m.Mode, m.Course, solved, reached, Player));
            Assert.IsEmpty(CourseGuidance.Toasts(m.Mode, m.Course, reached, reached, Player));
            Assert.IsEmpty(CourseGuidance.Toasts(m.Mode, m.Course, reached, start, Player));
            Assert.IsEmpty(CourseGuidance.Toasts(m.Mode, m.Course, start, m.Frame(matchId: "other-match"), Player));
            Assert.IsEmpty(CourseGuidance.Toasts(m.Mode, m.Course, null, reached, Player));
            var expected = new[] { "Lap 1 done · 2 laps to go", "Lap 2 done · final lap", "Course complete!" };
            for (int lap = 0; lap < 3; lap++)
            {
                m.ReachFinishStage();
                if (m.Team.KeepsakeCarrier == null) Assert.IsTrue(m.Progress.TakeKeepsake(Player));
                var before = m.Frame();
                m.Advance(new ProgressEvent(ProgressEventKind.FinishReached, Player, m.Course.Laps[lap].FinishModuleId, lap));
                CollectionAssert.AreEqual(new[] { expected[lap] }, CourseGuidance.Toasts(m.Mode, m.Course, before, m.Frame(), Player));
            }
        }

        [Test]
        public void KnockoutsToastAndNameTheWinnerByPlayerNumber()
        {
            var m = new Match("FreeForAll", new[] { new WordboundSeat(Player, 0, 0), new WordboundSeat(Rival, 1, 0) });
            for (int i = 0; i < ModeDefinition.RecoveryProtectionSeconds * ModeDefinition.TickRate + 1; i++) m.Advance();
            var before = m.Frame();
            m.Advance(new ProgressEvent(ProgressEventKind.Knockout, Player, Rival));
            var after = m.Frame();
            CollectionAssert.AreEqual(new[] { "Knockout! 1/" + m.Mode.KnockoutTarget }, CourseGuidance.Toasts(m.Mode, null, before, after, Player));
            Assert.IsEmpty(CourseGuidance.Toasts(m.Mode, null, before, after, Rival));
            var lost = CourseGuidance.Summary(m.Mode, null, after, Rival, "Win", new[] { 0 });
            Assert.AreEqual("P1 won", lost.Title);
            CollectionAssert.AreEqual(new[] { "Your knockouts: 0", "Most knockouts: 1" }, lost.Rows.Skip(1));
            Assert.AreEqual("You won!", CourseGuidance.Summary(m.Mode, null, after, Player, "Win", new[] { 0 }).Title);
            Assert.AreEqual("P2 won", CourseGuidance.Summary(m.Mode, null, after, Player, "Win", new[] { 1 }).Title);
            Assert.IsFalse(CourseGuidance.Next(m.Mode, null, after, Player).HasTarget);
        }

        [Test]
        public void PracticeToastAppearsOnceWhenAStationIsSolved()
        {
            var m = new Match("Freestyle", Solo);
            var site = m.Course.PhysicalSites[0];
            var open = new[] { new MatchMechanismState(site.PuzzleId, 0, site.Lap - 1) };
            var done = new[] { new MatchMechanismState(site.PuzzleId, 0, site.Lap - 1, true) };
            var before = m.Frame(mechanisms: open);
            m.Advance();
            var after = m.Frame(mechanisms: done);
            string recipe = m.Course.Puzzles.Single(p => p.Id == site.PuzzleId).RecipeId;
            CollectionAssert.AreEqual(new[] { "Practice complete · " + recipe }, CourseGuidance.Toasts(m.Mode, m.Course, before, after, Player));
            m.Advance();
            Assert.IsEmpty(CourseGuidance.Toasts(m.Mode, m.Course, after, m.Frame(mechanisms: done), Player));
        }
    }
}
