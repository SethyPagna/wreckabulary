using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public static class Teams
    {
        public const int NoTeam = -1;

        public static bool AreTeammates(int a, int b) => a != NoTeam && a == b;

        public static bool AreHostile(int a, int b) => !AreTeammates(a, b);

        public static int[] Assign(int players, int teamSize)
        {
            if (players < 0) throw new ArgumentOutOfRangeException(nameof(players));
            if (teamSize < 1) throw new ArgumentOutOfRangeException(nameof(teamSize));
            var teams = new int[players];
            if (teamSize == 1)
            {
                for (int i = 0; i < players; i++) teams[i] = i;
                return teams;
            }
            int teamCount = Math.Max(1, (players + teamSize - 1) / teamSize);
            for (int i = 0; i < players; i++) teams[i] = i % teamCount;
            return teams;
        }

        public static int[] Sides(int players, int sides)
        {
            if (players < 0) throw new ArgumentOutOfRangeException(nameof(players));
            if (sides < 1) throw new ArgumentOutOfRangeException(nameof(sides));
            var teams = new int[players];
            for (int i = 0; i < players; i++) teams[i] = i % sides;
            return teams;
        }

        public static int[] For(int players, int teamSize, int sides) => sides > 0 ? Sides(players, sides) : Assign(players, teamSize);

        public const int FewestHumansToGroup = 2;

        public static int[] Assign(int players, int teamSize, IReadOnlyList<bool> human) => Seated(Assign(players, teamSize), human);

        public static int[] Sides(int players, int sides, IReadOnlyList<bool> human) => Seated(Sides(players, sides), human);

        public static int[] For(int players, int teamSize, int sides, IReadOnlyList<bool> human) => Seated(For(players, teamSize, sides), human);

        public static int[] Places(int players, int teamSize, int sides, IReadOnlyList<bool> human) => Places(For(players, teamSize, sides), human);

        public static int[] Places(IReadOnlyList<int> deal, IReadOnlyList<bool> human)
        {
            if (deal == null) throw new ArgumentNullException(nameof(deal));
            int seats = deal.Count;
            if (human != null && human.Count != seats) throw new ArgumentException("Every seat needs a human flag.", nameof(human));
            var places = new int[seats];
            for (int i = 0; i < seats; i++) places[i] = i;
            if (!Regroups(deal, human)) return places;
            var grouped = Enumerable.Range(0, seats).OrderBy(i => deal[i]).ToArray();
            var taken = new bool[seats];
            int next = 0;
            for (int seat = 0; seat < seats; seat++)
            {
                if (!human[seat]) continue;
                places[seat] = grouped[next++];
                taken[places[seat]] = true;
            }
            int free = 0;
            for (int seat = 0; seat < seats; seat++)
            {
                if (human[seat]) continue;
                while (taken[free]) free++;
                places[seat] = free++;
            }
            return places;
        }

        static bool Regroups(IReadOnlyList<int> deal, IReadOnlyList<bool> human)
        {
            if (human == null || human.Count(h => h) < FewestHumansToGroup) return false;
            int teams = deal.Distinct().Count();
            return teams > 1 && teams < deal.Count;
        }

        static int[] Seated(int[] deal, IReadOnlyList<bool> human)
        {
            var places = Places(deal, human);
            var teams = new int[deal.Length];
            for (int i = 0; i < teams.Length; i++) teams[i] = deal[places[i]];
            return teams;
        }

        public static string SideName(int team) => team >= 0 ? $"Team {team + 1}" : "";
    }

    public enum RoundState { Ongoing, Won, Draw }

    public readonly struct RoundOutcome
    {
        public readonly RoundState State;
        public readonly int WinningTeam;

        public RoundOutcome(RoundState state, int team)
        {
            State = state;
            WinningTeam = team;
        }

        public override string ToString() => State == RoundState.Won ? $"team {WinningTeam} wins" : State.ToString();
    }

    public readonly struct Combatant
    {
        public readonly int PlayerId;
        public readonly int Team;
        public readonly LifeState Life;

        public Combatant(int playerId, int team, LifeState life)
        {
            PlayerId = playerId;
            Team = team;
            Life = life;
        }
    }

    public static class WinCheck
    {
        public static RoundOutcome Evaluate(IEnumerable<Combatant> combatants)
        {
            var standing = combatants.Where(c => c.Life == LifeState.Alive).Select(c => c.Team).Distinct().ToList();
            if (standing.Count >= 2) return new RoundOutcome(RoundState.Ongoing, Teams.NoTeam);
            if (standing.Count == 1) return new RoundOutcome(RoundState.Won, standing[0]);
            return new RoundOutcome(RoundState.Draw, Teams.NoTeam);
        }

        public static List<int> Unrevivable(IEnumerable<Combatant> combatants)
        {
            var list = combatants.ToList();
            var teamsWithSomeoneUp = new HashSet<int>(list.Where(c => c.Life == LifeState.Alive).Select(c => c.Team));
            return list.Where(c => c.Life == LifeState.Downed && !teamsWithSomeoneUp.Contains(c.Team)).Select(c => c.PlayerId).ToList();
        }
    }

    public sealed class MatchScore
    {
        readonly Dictionary<int, int> wins = new Dictionary<int, int>();
        public int RoundsToWin { get; }
        public int RoundsPlayed { get; private set; }
        public int MatchWinner { get; private set; } = Teams.NoTeam;

        public MatchScore(int roundsToWin)
        {
            if (roundsToWin < 1) throw new ArgumentOutOfRangeException(nameof(roundsToWin));
            RoundsToWin = roundsToWin;
        }

        public int Wins(int team) => wins.TryGetValue(team, out int w) ? w : 0;
        public bool IsOver => MatchWinner != Teams.NoTeam;

        public void Record(RoundOutcome outcome)
        {
            if (IsOver) throw new InvalidOperationException("The match is already over.");
            if (outcome.State == RoundState.Ongoing) throw new ArgumentException("The round hasn't finished.");
            RoundsPlayed++;
            if (outcome.State != RoundState.Won) return;
            int w = Wins(outcome.WinningTeam) + 1;
            wins[outcome.WinningTeam] = w;
            if (w >= RoundsToWin) MatchWinner = outcome.WinningTeam;
        }
    }
}
