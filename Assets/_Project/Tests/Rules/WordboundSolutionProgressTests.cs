using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class WordboundSolutionProgressTests
    {
        static RaceCheckpoint Words(string id, string prefix) => new RaceCheckpoint(id, new[]
        {
            new RaceSolution("short", new[] { prefix + "-bed" }),
            new RaceSolution("long", new[] { prefix + "-stool", prefix + "-plate" }, "dash")
        });

        static RaceLap[] Route() => new[]
        {
            new RaceLap("start", "finish", "lap0", Words("porch", "p"), new RaceCheckpoint("hall", "h-key")),
            new RaceLap("start", "finish", "lap1", new[]
            {
                new RaceLapVariant("B", new RaceCheckpoint("gate", "g-key"), Words("south", "s"), new RaceCheckpoint("home")),
                new RaceLapVariant("A", new RaceCheckpoint("gate", "g-key"), Words("north", "n"), new RaceCheckpoint("home"))
            }),
            new RaceLap("start", "finish", "lap2", new RaceCheckpoint("attic", "a-key"))
        };

        static MatchProgress Race(string mode = "TeamRace") => new MatchProgress(ModeDefinition.For(mode),
            mode == "TimeRun" ? new[] { new WordboundSeat("a", 0) } : new[] { new WordboundSeat("a", 0), new WordboundSeat("b", 1) }, Route());

        static ProgressEvent E(ProgressEventKind kind, string player, string target = "", int lap = 0) =>
            new ProgressEvent(kind, player, target, lap);

        static void Step(MatchProgress match, params ProgressEvent[] events) =>
            match.Advance(match.Tick + 1, match.WallClockMs + 16, events);

        static void Pass(MatchProgress match, string player, int lap, string checkpoint, params string[] puzzles) =>
            Step(match, puzzles.Select(p => E(ProgressEventKind.PuzzleSatisfied, player, p, lap))
                .Concat(new[] { E(ProgressEventKind.CheckpointReached, player, checkpoint, lap) }).ToArray());

        static void FinishFirstLap(MatchProgress match, string player)
        {
            match.TakeKeepsake(player);
            Pass(match, player, 0, "porch", "p-bed");
            Pass(match, player, 0, "hall", "h-key");
            Step(match, E(ProgressEventKind.FinishReached, player, "finish", 0));
        }

        static object Internal(TeamProgress team, string method, params object[] arguments) =>
            typeof(TeamProgress).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(team, arguments);

        [Test]
        public void AnyOneCompleteSolutionPassesTheCheckpoint()
        {
            var match = Race();
            Pass(match, "a", 0, "porch", "p-stool", "h-key");
            Assert.AreEqual(0, match.Team(0).NextCheckpoint);
            CollectionAssert.AreEqual(new[] { "p-stool" }, match.Team(0).CompletedPuzzles);
            Pass(match, "a", 0, "porch", "p-plate");
            Assert.AreEqual(1, match.Team(0).NextCheckpoint);
            Assert.AreEqual("porch", match.Team(0).SafeCheckpoint);
            Pass(match, "b", 0, "porch", "p-bed");
            Assert.AreEqual(1, match.Team(1).NextCheckpoint);
            Assert.AreEqual(1, match.Team(1).OrderedObjectives);
            var porch = match.Laps[0].Checkpoints[0];
            CollectionAssert.AreEqual(new[] { "long", "short" }, porch.Solutions.Select(s => s.Id));
            CollectionAssert.AreEqual(new[] { "p-stool", "p-plate", "p-bed" }, porch.Puzzles);
            Assert.AreEqual("dash", porch.Solutions[0].ShortcutConnectionId);
        }

        [Test]
        public void PlainCheckpointsStillRequireEveryPuzzleAndKeepTheirIdentity()
        {
            var plain = Enumerable.Range(0, 3).Select(n => new RaceLap("start", "finish", "lap" + n,
                new RaceCheckpoint("first", "x", "y"), new RaceCheckpoint("second"))).ToArray();
            var variant = Enumerable.Range(0, 3).Select(n => new RaceLap("start", "finish", "lap" + n,
                new[] { new RaceLapVariant("", new RaceCheckpoint("first", "x", "y"), new RaceCheckpoint("second")) })).ToArray();
            var explicitSolution = Enumerable.Range(0, 3).Select(n => new RaceLap("start", "finish", "lap" + n,
                new RaceCheckpoint("first", new[] { new RaceSolution("both", new[] { "x", "y" }) }), new RaceCheckpoint("second"))).ToArray();
            var roster = new[] { new WordboundSeat("a", 0) };
            var mode = ModeDefinition.For("TimeRun");
            var match = new MatchProgress(mode, roster, plain);
            Pass(match, "a", 0, "first", "x");
            Assert.AreEqual(0, match.Team(0).NextCheckpoint);
            Pass(match, "a", 0, "first", "y");
            Assert.AreEqual(1, match.Team(0).NextCheckpoint);
            Assert.AreEqual(1, plain[0].Checkpoints[0].Solutions.Count);
            Assert.AreEqual("", plain[0].Checkpoints[0].Solutions[0].Id);
            Assert.IsFalse(plain[0].HasChoice);
            Assert.AreEqual(2, plain[0].Divergence);
            Assert.AreSame(plain[0].Checkpoints, plain[0].For("").Checkpoints);
            Assert.DoesNotThrow(() => new MatchProgress(mode, roster, variant).Restore(match.Capture()));
            Assert.Throws<ArgumentException>(() => new MatchProgress(mode, roster, explicitSolution).Restore(match.Capture()));
            CollectionAssert.AreEqual(new[] { "", "", "" }, match.Team(0).Options);
        }

        [Test]
        public void RestoreAcceptsAnySolvedPrefixAndRejectsIncompleteOrForeignPuzzles()
        {
            var match = Race();
            Pass(match, "a", 0, "porch", "p-bed", "p-stool");
            Assert.AreEqual(1, match.Team(0).NextCheckpoint);
            var snapshot = match.Capture();
            Assert.DoesNotThrow(() => Race().Restore(snapshot));
            var partial = match.Capture();
            Internal(partial.Teams[0], "ClearPuzzles");
            Internal(partial.Teams[0], "AddPuzzle", "p-stool");
            var target = Race();
            Assert.Throws<ArgumentException>(() => target.Restore(partial));
            Assert.AreEqual(0, target.Tick);
            var foreign = match.Capture();
            Internal(foreign.Teams[0], "AddPuzzle", "a-key");
            Assert.Throws<ArgumentException>(() => Race().Restore(foreign));
            var next = match.Capture();
            Internal(next.Teams[0], "AddPuzzle", "h-key");
            Assert.DoesNotThrow(() => Race().Restore(next));
        }

        [Test]
        public void FirstTeammateChoiceWinsUntilTheRouteDivergesThenTheDefaultLocks()
        {
            var match = Race();
            Assert.AreEqual(1, match.Laps[1].Divergence);
            CollectionAssert.AreEqual(new[] { "A", "B" }, match.Laps[1].Variants.Select(v => v.OptionId));
            Assert.IsFalse(match.ChooseOption("a", 0, "A"));
            Assert.IsFalse(match.ChooseOption("a", 2, "A"));
            Assert.IsFalse(match.ChooseOption("a", 1, "C"));
            Assert.IsFalse(match.ChooseOption("a", 1, ""));
            Assert.IsFalse(match.ChooseOption("stranger", 1, "A"));
            Assert.IsTrue(match.ChooseOption("a", 1, "B"));
            Assert.IsFalse(match.ChooseOption("a", 1, "A"));
            CollectionAssert.AreEqual(new[] { "", "B", "" }, match.Team(0).Options);
            FinishFirstLap(match, "a");
            FinishFirstLap(match, "b");
            Assert.AreEqual("", match.Team(1).Options[1]);
            Pass(match, "b", 1, "gate", "g-key");
            Assert.AreEqual("A", match.Team(1).Options[1]);
            Assert.IsFalse(match.ChooseOption("b", 1, "B"));
            var snapshot = match.Capture();
            Assert.DoesNotThrow(() => Race().Restore(snapshot));
            CollectionAssert.AreEqual(new[] { "", "B", "" }, snapshot.Teams[0].Options);
            foreach (var (team, lap, option) in new[] { (1, 1, ""), (0, 1, "C"), (0, 2, "A"), (0, 0, "A") })
            {
                var forged = match.Capture();
                Internal(forged.Teams[team], "SetOption", lap, option);
                Assert.Throws<ArgumentException>(() => Race().Restore(forged), team + "/" + lap + "/" + option);
            }
        }

        [Test]
        public void ChosenVariantDrivesPuzzlesCheckpointsAndKeepsakeLocations()
        {
            var match = Race();
            match.ChooseOption("a", 1, "B");
            FinishFirstLap(match, "a");
            FinishFirstLap(match, "b");
            Pass(match, "a", 1, "gate", "g-key");
            Pass(match, "b", 1, "gate", "g-key");
            Pass(match, "a", 1, "north", "n-bed");
            Assert.AreEqual(1, match.Team(0).NextCheckpoint);
            CollectionAssert.DoesNotContain(match.Team(0).CompletedPuzzles, "n-bed");
            Pass(match, "a", 1, "south", "s-bed");
            Pass(match, "b", 1, "south", "s-bed");
            Assert.AreEqual(2, match.Team(0).NextCheckpoint);
            Assert.AreEqual(1, match.Team(1).NextCheckpoint);
            CollectionAssert.DoesNotContain(match.Team(1).CompletedPuzzles, "s-bed");
            Assert.AreEqual("south", match.Team(0).SafeCheckpoint);
            Assert.IsTrue(match.DropKeepsake("a"));
            while (match.Team(0).KeepsakeRecoveryAt >= 0) Step(match);
            Assert.AreEqual("south", match.Team(0).KeepsakeLocation);
            var snapshot = match.Capture();
            Assert.DoesNotThrow(() => Race().Restore(snapshot));
            var mislaid = match.Capture();
            typeof(TeamProgress).GetProperty(nameof(TeamProgress.KeepsakeLocation)).SetValue(mislaid.Teams[0], "north");
            Assert.Throws<ArgumentException>(() => Race().Restore(mislaid));
        }

        [TestCase(false), TestCase(true)]
        public void TimeoutRanksTeamsOnDifferentOptionsByObjectivesThenTick(bool simultaneous)
        {
            var match = Race();
            Assert.IsTrue(match.ChooseOption("a", 1, "A"));
            Assert.IsTrue(match.ChooseOption("b", 1, "B"));
            FinishFirstLap(match, "a");
            FinishFirstLap(match, "b");
            if (simultaneous)
            {
                Step(match, E(ProgressEventKind.PuzzleSatisfied, "a", "g-key", 1), E(ProgressEventKind.CheckpointReached, "a", "gate", 1),
                    E(ProgressEventKind.PuzzleSatisfied, "b", "g-key", 1), E(ProgressEventKind.CheckpointReached, "b", "gate", 1));
                Step(match, E(ProgressEventKind.PuzzleSatisfied, "a", "n-bed", 1), E(ProgressEventKind.CheckpointReached, "a", "north", 1),
                    E(ProgressEventKind.PuzzleSatisfied, "b", "s-stool", 1), E(ProgressEventKind.PuzzleSatisfied, "b", "s-plate", 1),
                    E(ProgressEventKind.CheckpointReached, "b", "south", 1));
            }
            else
            {
                Pass(match, "b", 1, "gate", "g-key");
                Pass(match, "b", 1, "south", "s-stool", "s-plate");
                Pass(match, "a", 1, "gate", "g-key");
                Pass(match, "a", 1, "north", "n-bed");
            }
            Assert.AreEqual(5, match.Team(0).OrderedObjectives);
            Assert.AreEqual(5, match.Team(1).OrderedObjectives);
            Assert.AreEqual("A", match.Team(0).Options[1]);
            Assert.AreEqual("B", match.Team(1).Options[1]);
            while (match.Result == null) Step(match);
            Assert.AreEqual(1500L * 60, match.Tick);
            Assert.AreEqual(simultaneous ? "Draw" : "Win", match.Result.Outcome);
            CollectionAssert.AreEqual(simultaneous ? new[] { 0, 1 } : new[] { 1 }, match.Result.WinningTeams);
            Assert.DoesNotThrow(() => Race().Restore(match.Capture()));
        }

        [Test]
        public void VariantsAndSolutionsKeepTheirStructuralInvariants()
        {
            var gate = new RaceCheckpoint("gate", "g");
            Assert.Throws<ArgumentException>(() => new RaceLap("s", "f", "", new[] {
                new RaceLapVariant("A", gate), new RaceLapVariant("B", gate, new RaceCheckpoint("extra")) }));
            Assert.Throws<ArgumentException>(() => new RaceLap("s", "f", "", new[] { new RaceLapVariant("A", gate), new RaceLapVariant("C", gate) }));
            Assert.Throws<ArgumentException>(() => new RaceLap("s", "f", "", new[] { new RaceLapVariant("A", gate) }));
            Assert.Throws<ArgumentException>(() => new RaceLap("s", "f", "", new[] { new RaceLapVariant("", gate), new RaceLapVariant("A", gate) }));
            Assert.Throws<ArgumentException>(() => new RaceLapVariant("D", gate));
            Assert.Throws<ArgumentException>(() => new RaceCheckpoint("c", new[] { new RaceSolution("x", new[] { "p" }), new RaceSolution("y", new[] { "p" }) }));
            Assert.Throws<ArgumentException>(() => new RaceCheckpoint("c", new[] { new RaceSolution("x", new[] { "p" }), new RaceSolution("x", new[] { "q" }) }));
            Assert.Throws<ArgumentException>(() => new RaceCheckpoint("c", Enumerable.Range(0, 4).Select(i => new RaceSolution("s" + i, new[] { "p" + i }))));
            Assert.Throws<ArgumentException>(() => new RaceCheckpoint("c", Array.Empty<RaceSolution>()));
            Assert.Throws<ArgumentException>(() => new RaceSolution("x", Array.Empty<string>()));
            Assert.Throws<ArgumentException>(() => new RaceSolution("x", new[] { "p" }, "bad id"));
            Assert.Throws<ArgumentException>(() => new RaceSolution("", new[] { "p" }));
            var choiceFirst = Route();
            choiceFirst[0] = choiceFirst[1];
            Assert.Throws<ArgumentException>(() => new MatchProgress(ModeDefinition.For("TimeRun"), new[] { new WordboundSeat("a", 0) }, choiceFirst));
            var free = new MatchProgress(ModeDefinition.For("Freestyle"), new[] { new WordboundSeat("a", 0) });
            Assert.IsFalse(free.ChooseOption("a", 1, "A"));
            CollectionAssert.AreEqual(new[] { "", "", "" }, free.Team(0).Options);
        }
    }
}
