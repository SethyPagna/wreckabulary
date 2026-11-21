using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public sealed class MatchStatLine
    {
        public int Seat { get; }
        public string Name { get; }
        public int Team { get; }
        public bool Bot { get; }
        public int Knockouts { get; internal set; }
        public int Falls { get; internal set; }
        public int Crafted { get; internal set; }
        public int Broken { get; internal set; }
        public int Score { get; internal set; }
        public float Damage { get; internal set; }

        internal MatchStatLine(int seat, string name, int team, bool bot)
        {
            Seat = seat;
            Name = name ?? "";
            Team = team;
            Bot = bot;
        }
    }

    public sealed class MatchStats
    {
        readonly Dictionary<int, MatchStatLine> bySeat = new Dictionary<int, MatchStatLine>();
        readonly List<MatchStatLine> lines = new List<MatchStatLine>();
        readonly Dictionary<int, int> sideScores = new Dictionary<int, int>();

        public static MatchStats Current { get; private set; } = new MatchStats();

        public static MatchStats Begin() => Current = new MatchStats();

        public IReadOnlyList<MatchStatLine> Lines => lines;

        public MatchStatLine Enter(int seat, string name, int team, bool bot)
        {
            if (bySeat.TryGetValue(seat, out var known)) return known;
            var line = new MatchStatLine(seat, name, team, bot);
            bySeat[seat] = line;
            lines.Add(line);
            return line;
        }

        public MatchStatLine Of(int seat) => bySeat.TryGetValue(seat, out var line) ? line : null;

        public void Knockout(int victim, int attacker)
        {
            var fallen = Of(victim);
            if (fallen != null) fallen.Falls++;
            var winner = attacker == victim ? null : Of(attacker);
            if (winner != null) winner.Knockouts++;
        }

        public void Craft(int seat)
        {
            var line = Of(seat);
            if (line != null) line.Crafted++;
        }

        public void Break(int seat)
        {
            var line = Of(seat);
            if (line != null) line.Broken++;
        }

        public void Hurt(int attacker, float amount)
        {
            var line = Of(attacker);
            if (line != null && amount > 0f) line.Damage += amount;
        }

        public void SetScore(int seat, int score)
        {
            var line = Of(seat);
            if (line != null) line.Score = score;
        }

        public void SetSideScore(int team, int score) => sideScores[team] = score;

        public int SideScore(int team) =>
            sideScores.TryGetValue(team, out int set) ? set : lines.Where(l => l.Team == team).Sum(l => l.Score);

        public IReadOnlyList<MatchStatLine> Side(int team) => lines.Where(l => l.Team == team).ToList();

        public IReadOnlyList<int> Sides() => lines.Select(l => l.Team).Distinct().OrderBy(t => t).ToList();

        public IReadOnlyList<MatchStatLine> Ranked() =>
            lines.OrderByDescending(l => l.Score).ThenByDescending(l => l.Knockouts).ThenBy(l => l.Falls).ThenBy(l => l.Seat).ToList();
    }
}
