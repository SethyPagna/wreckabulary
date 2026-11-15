using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public enum ProgressEventKind { PuzzleSatisfied, CheckpointReached, FinishReached, Knockout, Fell }

    public sealed class ProgressEvent
    {
        public ProgressEventKind Kind { get; }
        public string PlayerId { get; }
        public string Target { get; }
        public int Lap { get; }
        public ProgressEvent(ProgressEventKind kind, string playerId, string target = "", int lap = 0)
        { Kind = kind; PlayerId = playerId; Target = target; Lap = lap; }
    }

    public sealed class TeamProgress
    {
        public const int OptionCount = 3;
        readonly HashSet<string> puzzles = new HashSet<string>(StringComparer.Ordinal);
        readonly string[] options = BlankOptions();
        public int Team { get; internal set; }
        public int Lap { get; internal set; }
        public int NextCheckpoint { get; internal set; }
        public int OrderedObjectives { get; internal set; }
        public long ProgressTick { get; internal set; }
        public string SafeCheckpoint { get; internal set; }
        public string KeepsakeCarrier { get; internal set; }
        public string KeepsakeLocation { get; internal set; }
        public long KeepsakeRecoveryAt { get; internal set; } = -1;
        public int RecoveryGeneration { get; internal set; }
        public bool Finished { get; internal set; }
        public IReadOnlyList<string> CompletedPuzzles => Array.AsReadOnly(puzzles.OrderBy(p => p, StringComparer.Ordinal).ToArray());
        public IReadOnlyList<string> Options => Array.AsReadOnly((string[])options.Clone());
        public bool HasPuzzle(string id) => puzzles.Contains(id);
        internal bool AddPuzzle(string id) => puzzles.Add(id);
        internal void ClearPuzzles() => puzzles.Clear();
        internal string Option(int lap) => options[lap];
        internal void SetOption(int lap, string optionId) => options[lap] = optionId ?? throw new ArgumentNullException(nameof(optionId));
        internal TeamProgress Copy()
        {
            var result = new TeamProgress
            {
                Team = Team, Lap = Lap, NextCheckpoint = NextCheckpoint, OrderedObjectives = OrderedObjectives,
                ProgressTick = ProgressTick, SafeCheckpoint = SafeCheckpoint, KeepsakeCarrier = KeepsakeCarrier,
                KeepsakeLocation = KeepsakeLocation, KeepsakeRecoveryAt = KeepsakeRecoveryAt,
                RecoveryGeneration = RecoveryGeneration, Finished = Finished
            };
            foreach (string puzzle in puzzles) result.puzzles.Add(puzzle);
            Array.Copy(options, result.options, OptionCount);
            return result;
        }

        static string[] BlankOptions()
        {
            var blank = new string[OptionCount];
            Array.Fill(blank, "");
            return blank;
        }
    }

    public sealed class PlayerProgress
    {
        public string Id { get; internal set; }
        public int Team { get; internal set; }
        public int Slot { get; internal set; }
        public int Knockouts { get; internal set; }
        public bool Connected { get; internal set; } = true;
        public bool Returned { get; internal set; }
        public long ReconnectDeadlineMs { get; internal set; } = -1;
        public long ProtectedUntil { get; internal set; }
        public long RespawnAt { get; internal set; } = -1;
        public bool Eliminated { get; internal set; }
        public int RecoveryGeneration { get; internal set; }
        internal PlayerProgress Copy() => (PlayerProgress)MemberwiseClone();
    }

    public sealed class MatchResult
    {
        public string Outcome { get; }
        public IReadOnlyList<int> WinningTeams { get; }
        public long Tick { get; }
        public bool Confirmed { get; internal set; }
        internal MatchResult(string outcome, IEnumerable<int> winners, long tick)
        { Outcome = outcome; WinningTeams = Array.AsReadOnly(winners.OrderBy(t => t).ToArray()); Tick = tick; }
        internal MatchResult Copy() => new MatchResult(Outcome, WinningTeams, Tick) { Confirmed = Confirmed };
    }

    public sealed class ProgressSnapshot
    {
        public string DefinitionHash { get; }
        public long Tick { get; }
        public long ElapsedTicks { get; }
        public long WallClockMs { get; }
        public bool Migrating { get; }
        public IReadOnlyList<TeamProgress> Teams { get; }
        public IReadOnlyList<PlayerProgress> Players { get; }
        public MatchResult Result { get; }
        internal ProgressSnapshot(string definitionHash, long tick, long elapsed, long wallClock, bool migrating,
            IEnumerable<TeamProgress> teams, IEnumerable<PlayerProgress> players, MatchResult result)
        {
            DefinitionHash = definitionHash;
            Tick = tick; ElapsedTicks = elapsed; WallClockMs = wallClock; Migrating = migrating;
            Teams = Array.AsReadOnly(teams.OrderBy(t => t.Team).Select(t => t.Copy()).ToArray());
            Players = Array.AsReadOnly(players.OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => p.Copy()).ToArray());
            Result = result?.Copy();
        }
    }

    public sealed class MatchProgress
    {
        const string RouteVariantsField = "wordbound.route-variants";
        readonly Dictionary<string, PlayerProgress> players;
        readonly Dictionary<int, TeamProgress> teams;
        readonly ReadOnlyCollection<RaceLap> laps;
        readonly string definitionHash;
        TeamProgress[] orderedTeams;
        public ModeDefinition Mode { get; }
        public long Tick { get; private set; }
        public long ElapsedTicks { get; private set; }
        public long WallClockMs { get; private set; }
        public bool Migrating { get; private set; }
        public MatchResult Result { get; private set; }
        public IReadOnlyList<RaceLap> Laps => laps;
        public TeamProgress Team(int team) => teams.TryGetValue(team, out var value) ? value.Copy() : null;
        public PlayerProgress Player(string player) => player != null && players.TryGetValue(player, out var value) ? value.Copy() : null;
        internal TeamProgress LiveTeam(int team) => teams.TryGetValue(team, out var value) ? value : null;
        internal TeamProgress RequiredTeam(int team) => teams[team];
        internal PlayerProgress LivePlayer(string player) => player != null && players.TryGetValue(player, out var value) ? value : null;
        internal IReadOnlyList<TeamProgress> LiveTeams => orderedTeams ??= teams.Values.OrderBy(t => t.Team).ToArray();

        public MatchProgress(ModeDefinition mode, IEnumerable<WordboundSeat> roster,
            IEnumerable<RaceLap> route = null, long wallClockMs = 0)
        {
            Mode = mode ?? throw new ArgumentNullException(nameof(mode));
            var seats = roster?.ToArray() ?? Array.Empty<WordboundSeat>();
            var errors = mode.ValidateRoster(seats);
            if (errors.Count != 0) throw new ArgumentException(string.Join(" ", errors), nameof(roster));
            var lapArray = route?.ToArray() ?? Array.Empty<RaceLap>();
            if (mode.IsRace && (lapArray.Length != mode.Laps || lapArray.Any(l => l == null)))
                throw new ArgumentException("A race requires exactly three resolved lap plans.", nameof(route));
            if (!mode.IsRace && lapArray.Length != 0)
                throw new ArgumentException("A non-race mode cannot use a race route.", nameof(route));
            if (lapArray.Length != 0 && lapArray[0].HasChoice)
                throw new ArgumentException("The first lap cannot offer a choice.", nameof(route));
            if (wallClockMs < 0 || wallClockMs > long.MaxValue - 120000)
                throw new ArgumentOutOfRangeException(nameof(wallClockMs));
            laps = Array.AsReadOnly(lapArray);
            definitionHash = DefinitionHash(mode, seats, lapArray);
            players = seats.ToDictionary(s => s.PlayerId, s => new PlayerProgress
            {
                Id = s.PlayerId, Team = s.Team, Slot = s.Slot,
                ProtectedUntil = ModeDefinition.RecoveryProtectionSeconds * ModeDefinition.TickRate
            }, StringComparer.Ordinal);
            teams = seats.Select(s => s.Team).Distinct().ToDictionary(t => t, t => new TeamProgress
            { Team = t, SafeCheckpoint = mode.IsRace ? laps[0].Start : "spawn", KeepsakeLocation = mode.IsRace ? laps[0].Start : "" });
            WallClockMs = wallClockMs;
        }

        public void Advance(long nextTick, long wallClockMs, IEnumerable<ProgressEvent> hostObservations = null)
            => AdvanceCore(nextTick, wallClockMs, hostObservations, Array.Empty<ProgressEvent>());

        internal void AdvanceWithThrownKnockouts(long nextTick, long wallClockMs, IEnumerable<ProgressEvent> hostObservations,
            IReadOnlyCollection<ProgressEvent> resolvedThrownKnockouts)
            => AdvanceCore(nextTick, wallClockMs, hostObservations, resolvedThrownKnockouts);

        void AdvanceCore(long nextTick, long wallClockMs, IEnumerable<ProgressEvent> hostObservations,
            IReadOnlyCollection<ProgressEvent> resolvedThrownKnockouts)
        {
            if (nextTick != Tick + 1 || nextTick < 1) throw new ArgumentOutOfRangeException(nameof(nextTick), "Resolve consecutive ticks.");
            var observations = hostObservations?.Take(4097).ToArray() ?? Array.Empty<ProgressEvent>();
            if (observations.Length > 4096 || !ValidObservations(observations))
                throw new ArgumentException("Observation batch is invalid.", nameof(hostObservations));
            if (resolvedThrownKnockouts == null || resolvedThrownKnockouts.Count > 256 ||
                !ResolvedKnockouts(resolvedThrownKnockouts, observations))
                throw new ArgumentException("Thrown knockout credit must identify an exact resolved observation.");
            UpdateWallClock(wallClockMs);
            if (Migrating || Result != null) return;
            Tick = nextTick;
            ElapsedTicks++;
            foreach (var p in players.Values)
                if (!p.Returned && p.Eliminated && p.RespawnAt >= 0 && Tick >= p.RespawnAt)
                {
                    p.Eliminated = false; p.RespawnAt = -1; p.RecoveryGeneration++;
                    p.ProtectedUntil = Tick + ModeDefinition.RecoveryProtectionSeconds * ModeDefinition.TickRate;
                }
            foreach (var team in teams.Values)
                if (team.KeepsakeRecoveryAt >= 0 && Tick >= team.KeepsakeRecoveryAt)
                    RecoverKeepsake(team);

            var ordered = observations.Length == 0 ? observations : observations.OrderBy(o => o.PlayerId, StringComparer.Ordinal)
                .ThenBy(o => o.Kind).ThenBy(o => o.Target, StringComparer.Ordinal).ToArray();
            bool knockouts = Contains(ordered, ProgressEventKind.Knockout);
            var eligibleAttackers = knockouts ? new HashSet<string>(players.Values
                .Where(p => p.Connected && !p.Eliminated).Select(p => p.Id), StringComparer.Ordinal) : null;
            foreach (var observation in ordered)
                if (observation.Kind == ProgressEventKind.PuzzleSatisfied) ApplyPuzzle(observation);
            foreach (var observation in ordered)
                if (observation.Kind == ProgressEventKind.CheckpointReached) ApplyCheckpoint(observation);
            foreach (var observation in ordered)
                if (observation.Kind == ProgressEventKind.Fell) RecoverPlayer(observation.PlayerId);
            var candidates = new HashSet<int>();
            foreach (var observation in ordered)
                if (observation.Kind == ProgressEventKind.FinishReached && ApplyFinish(observation))
                    candidates.Add(players[observation.PlayerId].Team);
            if (knockouts)
            {
                var knockedOut = new HashSet<string>(StringComparer.Ordinal);
                foreach (var observation in ordered)
                    if (observation.Kind == ProgressEventKind.Knockout)
                        ApplyKnockout(observation, knockedOut, eligibleAttackers, resolvedThrownKnockouts.Contains(observation));
            }
            if (Mode.Objective == WordboundObjective.Knockouts)
                foreach (var p in players.Values)
                    if (p.Knockouts >= Mode.KnockoutTarget) candidates.Add(p.Team);
            if (candidates.Count != 0)
                Result = new MatchResult(candidates.Count == 1 ? "Win" : "Draw", candidates, Tick);
            else if (Mode.TimeLimitSeconds > 0 && ElapsedTicks >= (long)Mode.TimeLimitSeconds * ModeDefinition.TickRate)
                Timeout();
        }

        static bool ValidObservations(ProgressEvent[] observations)
        {
            foreach (var o in observations)
                if (o == null || o.Kind < ProgressEventKind.PuzzleSatisfied || o.Kind > ProgressEventKind.Fell) return false;
            return true;
        }

        static bool ResolvedKnockouts(IReadOnlyCollection<ProgressEvent> knockouts, ProgressEvent[] observations)
        {
            foreach (var o in knockouts)
                if (o == null || o.Kind != ProgressEventKind.Knockout || Array.IndexOf(observations, o) < 0) return false;
            return true;
        }

        static bool Contains(ProgressEvent[] events, ProgressEventKind kind)
        {
            foreach (var e in events) if (e.Kind == kind) return true;
            return false;
        }

        public bool ChooseOption(string playerId, int lap, string optionId)
        {
            if (!Mode.IsRace || !CanAct(playerId, out var p) || lap < 1 || lap >= laps.Count || !laps[lap].HasChoice ||
                string.IsNullOrEmpty(optionId) || laps[lap].For(optionId) == null) return false;
            var team = teams[p.Team];
            if (team.Option(lap) != "" || ChoiceLocked(team, lap)) return false;
            team.SetOption(lap, optionId);
            return true;
        }

        public bool TakeKeepsake(string playerId)
        {
            if (!CanAct(playerId, out var p) || !WordboundPracticePolicy.HasKeepsake(Mode)) return false;
            var team = teams[p.Team];
            if (team.Finished || team.KeepsakeCarrier != null) return false;
            team.KeepsakeCarrier = playerId; team.KeepsakeRecoveryAt = -1; team.KeepsakeLocation = "";
            return true;
        }

        public bool DropKeepsake(string playerId, string reachableLocation = null)
        {
            if (!players.TryGetValue(playerId ?? "", out var p)) return false;
            var team = teams[p.Team];
            if (!WordboundPracticePolicy.HasKeepsake(Mode) || team.KeepsakeCarrier != playerId) return false;
            team.KeepsakeCarrier = null;
            team.KeepsakeLocation = reachableLocation ?? "";
            team.KeepsakeRecoveryAt = Tick + ModeDefinition.KeepsakeRecoverySeconds * ModeDefinition.TickRate;
            return true;
        }

        public bool RecoverPlayer(string playerId)
        {
            if (!CanAct(playerId, out var p)) return false;
            DropKeepsake(playerId);
            p.Eliminated = true;
            p.RespawnAt = Tick + 2 * ModeDefinition.TickRate;
            return true;
        }

        public bool Disconnect(string playerId, long wallClockMs)
        {
            UpdateWallClock(wallClockMs);
            if (!players.TryGetValue(playerId ?? "", out var p) || !p.Connected || p.Returned) return false;
            p.Connected = false;
            p.ReconnectDeadlineMs = checked(wallClockMs + ModeDefinition.ReconnectSeconds * 1000L);
            DropKeepsake(playerId);
            return true;
        }

        public bool Reconnect(string playerId, long wallClockMs)
        {
            UpdateWallClock(wallClockMs);
            if (!players.TryGetValue(playerId ?? "", out var p) || p.Connected || p.Returned ||
                p.ReconnectDeadlineMs <= wallClockMs || p.ReconnectDeadlineMs < 0) return false;
            p.Connected = true; p.ReconnectDeadlineMs = -1;
            return true;
        }

        public bool Return(string playerId, long wallClockMs)
        {
            UpdateWallClock(wallClockMs);
            if (!players.TryGetValue(playerId ?? "", out var p) || p.Returned) return false;
            DropKeepsake(playerId);
            p.Returned = true;
            p.Connected = false;
            p.ReconnectDeadlineMs = -1;
            p.Eliminated = false;
            p.RespawnAt = -1;
            p.ProtectedUntil = 0;
            return true;
        }

        public void BeginMigration() => Migrating = true;
        public void CompleteMigration(long wallClockMs) { UpdateWallClock(wallClockMs); Migrating = false; }

        public ProgressSnapshot Capture() => new ProgressSnapshot(definitionHash, Tick, ElapsedTicks, WallClockMs, Migrating,
            teams.Values, players.Values, Result);

        public void Restore(ProgressSnapshot snapshot)
        {
            if (snapshot == null || snapshot.DefinitionHash != definitionHash ||
                snapshot.Tick < 0 || snapshot.Tick > long.MaxValue - 120000 || snapshot.ElapsedTicks != snapshot.Tick ||
                snapshot.WallClockMs < 0 || snapshot.WallClockMs > long.MaxValue - 120000 ||
                snapshot.Players.Count != players.Count || snapshot.Teams.Count != teams.Count ||
                snapshot.Players.Select(p => p.Id).Distinct(StringComparer.Ordinal).Count() != players.Count ||
                snapshot.Teams.Select(t => t.Team).Distinct().Count() != teams.Count ||
                !snapshot.Players.All(p => players.TryGetValue(p.Id, out var expected) && p.Team == expected.Team && p.Slot == expected.Slot) ||
                !snapshot.Teams.All(t => teams.ContainsKey(t.Team)))
                throw new ArgumentException("Snapshot roster does not match this match.", nameof(snapshot));
            foreach (var p in snapshot.Players)
                if (p.Knockouts < 0 || p.RecoveryGeneration < 0 || p.ProtectedUntil < 0 ||
                    p.ReconnectDeadlineMs < -1 || p.RespawnAt < -1 ||
                    p.ReconnectDeadlineMs > snapshot.WallClockMs + ModeDefinition.ReconnectSeconds * 1000L ||
                    p.ProtectedUntil > snapshot.Tick + ModeDefinition.RecoveryProtectionSeconds * ModeDefinition.TickRate ||
                    (p.RespawnAt >= 0 && (p.RespawnAt <= snapshot.Tick || p.RespawnAt > snapshot.Tick + 2 * ModeDefinition.TickRate)) ||
                    (Mode.Objective != WordboundObjective.Knockouts && p.Knockouts != 0) ||
                    (p.Connected && p.ReconnectDeadlineMs != -1) || (!p.Connected && !p.Returned && p.ReconnectDeadlineMs < 0) ||
                    (p.Returned && (p.Connected || p.ReconnectDeadlineMs != -1 || p.Eliminated || p.RespawnAt != -1)) ||
                    (p.Eliminated != (p.RespawnAt >= 0)))
                    throw new ArgumentException("Snapshot player state is invalid.", nameof(snapshot));
            foreach (var t in snapshot.Teams)
            {
                if (t.Lap < 0 || t.Lap > Mode.Laps || t.NextCheckpoint < 0 ||
                    t.ProgressTick < 0 || t.ProgressTick > snapshot.Tick || t.OrderedObjectives < 0 ||
                    t.KeepsakeRecoveryAt < -1 || t.RecoveryGeneration < 0 ||
                    (Mode.IsRace && t.Lap < Mode.Laps && t.NextCheckpoint > laps[t.Lap].Checkpoints.Count) ||
                    (Mode.IsRace && t.Finished != (t.Lap == Mode.Laps)) ||
                    (t.KeepsakeCarrier != null && (!players.TryGetValue(t.KeepsakeCarrier, out var p) || p.Team != t.Team ||
                        t.KeepsakeRecoveryAt != -1 || t.KeepsakeLocation != "")))
                    throw new ArgumentException("Snapshot progression is invalid.", nameof(snapshot));
                if (t.KeepsakeLocation == null || (t.KeepsakeLocation != "" && !WordboundIdentity.IsId(t.KeepsakeLocation)) ||
                    (t.KeepsakeRecoveryAt >= 0 && (t.KeepsakeRecoveryAt <= snapshot.Tick ||
                        t.KeepsakeRecoveryAt > snapshot.Tick + ModeDefinition.KeepsakeRecoverySeconds * ModeDefinition.TickRate)) ||
                    (t.KeepsakeCarrier != null && snapshot.Players.Any(p => p.Id == t.KeepsakeCarrier && (!p.Connected || p.Eliminated))))
                    throw new ArgumentException("Snapshot keepsake state is invalid.", nameof(snapshot));
                for (int lap = 0; lap < TeamProgress.OptionCount; lap++)
                {
                    string option = t.Option(lap);
                    bool valid = option == "" ? lap >= laps.Count || !laps[lap].HasChoice || !ChoiceLocked(t, lap) :
                        lap < laps.Count && laps[lap].HasChoice && laps[lap].For(option) != null;
                    if (!valid) throw new ArgumentException("Snapshot route options are invalid.", nameof(snapshot));
                }
                if (!Mode.IsRace)
                {
                    if (t.Lap != 0 || t.NextCheckpoint != 0 || t.OrderedObjectives != 0 || t.ProgressTick != 0 ||
                        t.SafeCheckpoint != "spawn" || t.Finished || t.CompletedPuzzles.Count != 0 ||
                        (WordboundPracticePolicy.Supports(Mode) ? t.KeepsakeLocation != "" && t.KeepsakeLocation != "spawn" :
                            t.KeepsakeCarrier != null || t.KeepsakeLocation != "" ||
                            t.KeepsakeRecoveryAt != -1 || t.RecoveryGeneration != 0))
                        throw new ArgumentException("Non-race snapshot contains race progression.", nameof(snapshot));
                    continue;
                }
                int expectedObjectives = laps.Take(t.Lap).Sum(l => l.Checkpoints.Count + 1) + t.NextCheckpoint;
                var current = t.Finished ? null : Variant(t, t.Lap).Checkpoints;
                string expectedSafe = t.Finished ? laps[laps.Count - 1].Finish :
                    t.NextCheckpoint == 0 ? laps[t.Lap].Start : current[t.NextCheckpoint - 1].Id;
                bool solvedPrefix = t.Finished || current.Take(t.NextCheckpoint).All(c => c.Satisfied(t));
                var allowedPuzzles = t.Finished ? Array.Empty<string>() :
                    current.Take(Math.Min(t.NextCheckpoint + 1, current.Count)).SelectMany(c => c.Puzzles).ToArray();
                var keepsakeLocations = laps.Take(t.Lap).SelectMany((l, i) => new[] { l.Start, l.Finish }.Concat(Variant(t, i).Checkpoints.Select(c => c.Id)))
                    .Concat(t.Finished ? Array.Empty<string>() : new[] { laps[t.Lap].Start }.Concat(current.Take(t.NextCheckpoint).Select(c => c.Id)));
                if (t.Finished && t.NextCheckpoint != 0 || t.OrderedObjectives != expectedObjectives || t.SafeCheckpoint != expectedSafe ||
                    !solvedPrefix || t.CompletedPuzzles.Any(id => !allowedPuzzles.Contains(id)) ||
                    (t.OrderedObjectives == 0 ? t.ProgressTick != 0 : t.ProgressTick == 0) ||
                    (t.KeepsakeCarrier == null && t.KeepsakeRecoveryAt == -1 && !keepsakeLocations.Contains(t.KeepsakeLocation)))
                    throw new ArgumentException("Snapshot ordered objectives or puzzle prefix is invalid.", nameof(snapshot));
            }
            bool expired = Mode.TimeLimitSeconds > 0 && snapshot.ElapsedTicks >= (long)Mode.TimeLimitSeconds * ModeDefinition.TickRate;
            var finishers = snapshot.Teams.Where(t => t.Finished).Select(t => t.Team).ToArray();
            var scorers = Mode.Objective == WordboundObjective.Knockouts ?
                snapshot.Players.Where(p => p.Knockouts >= Mode.KnockoutTarget).Select(p => p.Team).ToArray() : Array.Empty<int>();
            if (snapshot.Result == null)
            {
                if (expired || finishers.Length != 0 || scorers.Length != 0)
                    throw new ArgumentException("Snapshot omits a completed match result.", nameof(snapshot));
            }
            else
            {
                var r = snapshot.Result;
                int[] winners = finishers.Length != 0 ? finishers : scorers;
                string outcome;
                if (winners.Length == 0 && expired && Mode.Id == "TimeRun") outcome = "TimeExpired";
                else
                {
                    if (winners.Length == 0 && expired && Mode.IsRace)
                    {
                        int best = snapshot.Teams.Max(t => t.OrderedObjectives);
                        long earliest = snapshot.Teams.Where(t => t.OrderedObjectives == best).Min(t => t.ProgressTick);
                        winners = snapshot.Teams.Where(t => t.OrderedObjectives == best && t.ProgressTick == earliest).Select(t => t.Team).ToArray();
                    }
                    else if (winners.Length == 0 && expired && Mode.Objective == WordboundObjective.Knockouts)
                    {
                        int best = snapshot.Players.Max(p => p.Knockouts);
                        winners = snapshot.Players.Where(p => p.Knockouts == best).Select(p => p.Team).ToArray();
                    }
                    outcome = winners.Length == 1 ? "Win" : "Draw";
                    if (winners.Length == 0) throw new ArgumentException("Snapshot declares an unsupported match result.", nameof(snapshot));
                }
                if (r.Tick != snapshot.Tick || r.Outcome != outcome ||
                    !r.WinningTeams.SequenceEqual(winners.OrderBy(t => t)) || r.WinningTeams.Distinct().Count() != r.WinningTeams.Count)
                    throw new ArgumentException("Snapshot result disagrees with authoritative progression.", nameof(snapshot));
            }
            players.Clear(); teams.Clear();
            foreach (var p in snapshot.Players) players.Add(p.Id, p.Copy());
            foreach (var t in snapshot.Teams) teams.Add(t.Team, t.Copy());
            orderedTeams = null;
            Tick = snapshot.Tick; ElapsedTicks = snapshot.ElapsedTicks; WallClockMs = snapshot.WallClockMs;
            Migrating = snapshot.Migrating; Result = snapshot.Result?.Copy();
        }

        internal void ConfirmResult(long acknowledgedTick)
        {
            if (Result != null && acknowledgedTick >= Result.Tick) Result.Confirmed = true;
        }

        bool CanAct(string playerId, out PlayerProgress player)
        {
            player = null;
            return !Migrating && Result == null && playerId != null && players.TryGetValue(playerId, out player) &&
                player.Connected && !player.Eliminated && !player.Returned;
        }

        bool ValidRaceEvent(ProgressEvent e, out TeamProgress team)
        {
            team = null;
            if (!Mode.IsRace || !CanAct(e.PlayerId, out var player)) return false;
            team = teams[player.Team];
            return !team.Finished && e.Lap == team.Lap && team.Lap < laps.Count;
        }

        RaceLapVariant Variant(TeamProgress team, int lap) => laps[lap].For(team.Option(lap));

        bool ChoiceLocked(TeamProgress team, int lap) =>
            team.Finished || team.Lap > lap || team.Lap == lap && team.NextCheckpoint >= laps[lap].Divergence;

        void LockChoices(TeamProgress team)
        {
            for (int lap = 1; lap < laps.Count; lap++)
                if (laps[lap].HasChoice && team.Option(lap) == "" && ChoiceLocked(team, lap))
                    team.SetOption(lap, laps[lap].Variants[0].OptionId);
        }

        void ApplyPuzzle(ProgressEvent e)
        {
            if (!ValidRaceEvent(e, out var team)) return;
            var lap = Variant(team, team.Lap);
            if (team.NextCheckpoint >= lap.Checkpoints.Count) return;
            if (lap.Checkpoints[team.NextCheckpoint].Puzzles.Contains(e.Target)) team.AddPuzzle(e.Target);
        }

        void ApplyCheckpoint(ProgressEvent e)
        {
            if (!ValidRaceEvent(e, out var team)) return;
            var lap = Variant(team, team.Lap);
            if (team.NextCheckpoint >= lap.Checkpoints.Count) return;
            var checkpoint = lap.Checkpoints[team.NextCheckpoint];
            if (checkpoint.Id != e.Target || !checkpoint.Satisfied(team)) return;
            team.NextCheckpoint++; team.OrderedObjectives++; team.ProgressTick = Tick;
            team.SafeCheckpoint = checkpoint.Id;
            LockChoices(team);
        }

        bool ApplyFinish(ProgressEvent e)
        {
            if (!ValidRaceEvent(e, out var team)) return false;
            var lap = laps[team.Lap];
            if (e.Target != lap.Finish || team.NextCheckpoint != lap.Checkpoints.Count || team.KeepsakeCarrier != e.PlayerId) return false;
            team.Lap++; team.NextCheckpoint = 0; team.ClearPuzzles();
            team.OrderedObjectives++; team.ProgressTick = Tick;
            team.Finished = team.Lap == Mode.Laps;
            team.SafeCheckpoint = team.Finished ? lap.Finish : laps[team.Lap].Start;
            LockChoices(team);
            return team.Finished;
        }

        void ApplyKnockout(ProgressEvent e, HashSet<string> knockedOut, HashSet<string> eligibleAttackers, bool resolvedThrownHit)
        {
            if (!Mode.EnemyDamage || !players.TryGetValue(e.PlayerId ?? "", out var attacker) ||
                !players.TryGetValue(e.Target ?? "", out var victim) || attacker.Returned ||
                resolvedThrownHit && !attacker.Connected && attacker.ReconnectDeadlineMs <= WallClockMs ||
                !eligibleAttackers.Contains(attacker.Id) && !resolvedThrownHit || !victim.Connected ||
                victim.Eliminated || victim.ProtectedUntil > Tick || !Mode.CanDamage(attacker.Team, victim.Team) ||
                !knockedOut.Add(victim.Id)) return;
            if (Mode.Objective == WordboundObjective.Knockouts) attacker.Knockouts++;
            DropKeepsake(victim.Id);
            victim.Eliminated = true; victim.RespawnAt = Tick + 2 * ModeDefinition.TickRate;
        }

        void RecoverKeepsake(TeamProgress team)
        {
            team.KeepsakeCarrier = null; team.KeepsakeLocation = team.SafeCheckpoint;
            team.KeepsakeRecoveryAt = -1; team.RecoveryGeneration++;
        }

        void Timeout()
        {
            if (Mode.Id == "TimeRun") { Result = new MatchResult("TimeExpired", Array.Empty<int>(), Tick); return; }
            if (Mode.IsRace)
            {
                int best = teams.Values.Max(t => t.OrderedObjectives);
                long earliest = teams.Values.Where(t => t.OrderedObjectives == best).Min(t => t.ProgressTick);
                var winners = teams.Values.Where(t => t.OrderedObjectives == best && t.ProgressTick == earliest).Select(t => t.Team).ToArray();
                Result = new MatchResult(winners.Length == 1 ? "Win" : "Draw", winners, Tick);
            }
            else if (Mode.Objective == WordboundObjective.Knockouts)
            {
                int best = players.Values.Max(p => p.Knockouts);
                var winners = players.Values.Where(p => p.Knockouts == best).Select(p => p.Team).ToArray();
                Result = new MatchResult(winners.Length == 1 ? "Win" : "Draw", winners, Tick);
            }
        }

        void UpdateWallClock(long now)
        {
            if (now < WallClockMs || now > long.MaxValue - 120000) throw new ArgumentOutOfRangeException(nameof(now));
            WallClockMs = now;
        }

        static string DefinitionHash(ModeDefinition mode, WordboundSeat[] seats, RaceLap[] route)
        {
            var fields = new List<string> { mode.Hash, seats.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) };
            foreach (var seat in seats.OrderBy(s => s.PlayerId, StringComparer.Ordinal))
            {
                fields.Add(seat.PlayerId);
                fields.Add(seat.Team.ToString(System.Globalization.CultureInfo.InvariantCulture));
                fields.Add(seat.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            fields.Add(route.Length.ToString(System.Globalization.CultureInfo.InvariantCulture));
            foreach (var lap in route)
            {
                fields.Add(lap.Start);
                fields.Add(lap.Finish);
                fields.Add(lap.Checkpoints.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                foreach (var checkpoint in lap.Checkpoints)
                {
                    fields.Add(checkpoint.Id);
                    fields.Add(checkpoint.Puzzles.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    fields.AddRange(checkpoint.Puzzles);
                }
            }
            if (route.Any(l => l.HasChoice || l.Variants.Any(v => v.Checkpoints.Any(c => c.ExplicitSolutions))))
            {
                fields.Add(RouteVariantsField);
                foreach (var lap in route)
                {
                    fields.Add(lap.Variants.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    foreach (var variant in lap.Variants)
                    {
                        fields.Add(variant.OptionId);
                        fields.Add(variant.Checkpoints.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        foreach (var checkpoint in variant.Checkpoints) fields.AddRange(checkpoint.Identity());
                    }
                }
            }
            return WordboundIdentity.Sha256(string.Concat(fields.Select(field =>
                field.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + field)));
        }
    }
}
