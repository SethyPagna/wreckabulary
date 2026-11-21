using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public enum ScoreCentre { None, Score, Target, Clock }

    public readonly struct ScoreSeat
    {
        public readonly int Seat, Team, Score, SideScore;
        public readonly string Name;
        public readonly bool Scored, Mine;

        public ScoreSeat(int seat, string name, int team, int score, int sideScore, bool scored, bool mine)
        {
            Seat = seat; Name = name ?? ""; Team = team; Score = score; SideScore = sideScore; Scored = scored; Mine = mine;
        }
    }

    public sealed class ScoreRow
    {
        public int Seat { get; }
        public string Name { get; }
        public int Team { get; }
        public int Knockouts { get; }
        public int Falls { get; }
        public int Spelled { get; }
        public int Smashed { get; }
        public int Score { get; }
        public bool Mine { get; }

        internal ScoreRow(int seat, string name, int team, int knockouts, int falls, int spelled, int smashed, int score, bool mine)
        {
            Seat = seat; Name = name ?? ""; Team = team; Knockouts = knockouts; Falls = falls; Spelled = spelled; Smashed = smashed;
            Score = score; Mine = mine;
        }

        public int Column(int column) => column switch
        {
            0 => Knockouts, 1 => Falls, 2 => Spelled, 3 => Smashed, 4 => Score,
            _ => throw new ArgumentOutOfRangeException(nameof(column)),
        };
    }

    public sealed class ScoreBlock
    {
        public int Team { get; }
        public int Total { get; }
        public bool Mine { get; }
        public bool Ranked { get; }
        public IReadOnlyList<ScoreRow> Rows { get; }

        internal ScoreBlock(int team, int total, bool mine, bool ranked, IReadOnlyList<ScoreRow> rows)
        {
            Team = team; Total = total; Mine = mine; Ranked = ranked; Rows = rows;
        }
    }

    public static class ScoreLine
    {
        public const int NoLead = -1, Left = 0, Right = 1;
        public const float CentreWidth = 212f, CentreGap = 10f, ClockWidth = 132f, ClockRow = 56f;
        public const string Separator = ":";
        public static readonly IReadOnlyList<string> Columns = new[] { "KO", "FALLS", "SPELLED", "SMASHED", "SCORE" };

        public static int Side(int team, int slot) => team >= 0 ? team : -2 - slot;

        public static bool SameSide(IReadOnlyList<int> teams, int a, int b) => Side(teams[a], a) == Side(teams[b], b);

        public static int Sides(IReadOnlyList<int> teams)
        {
            if (teams == null) return 0;
            int sides = 0;
            for (int i = 0; i < teams.Count; i++)
            {
                bool seen = false;
                for (int j = 0; j < i && !seen; j++) seen = SameSide(teams, i, j);
                if (!seen) sides++;
            }
            return sides;
        }

        public static bool TwoSides(IReadOnlyList<int> teams) => Sides(teams) == 2;

        public static ScoreCentre Centre(IReadOnlyList<int> teams, bool scored, bool target)
        {
            if (teams == null || !PlayerBar.Visible(teams.Count)) return ScoreCentre.None;
            if (target) return ScoreCentre.Target;
            return scored && TwoSides(teams) ? ScoreCentre.Score : ScoreCentre.None;
        }

        public static ScoreCentre Centre(IReadOnlyList<int> teams, bool scored, bool target, bool clocked)
        {
            var centre = Centre(teams, scored, target);
            return centre == ScoreCentre.None && clocked && teams != null ? ScoreCentre.Clock : centre;
        }

        public static float Width(ScoreCentre centre) => centre switch
        {
            ScoreCentre.None => 0f,
            ScoreCentre.Clock => ClockWidth,
            _ => CentreWidth,
        };

        public static int Split(IReadOnlyList<int> teams, int[] order, int count)
        {
            if (teams == null || order == null || count < 2) return 0;
            if (TwoSides(teams))
                for (int i = 1; i < count; i++)
                    if (!SameSide(teams, order[i], order[0])) return i;
            return (count + 1) / 2;
        }

        public static float Arrange(IReadOnlyList<int> teams, int[] order, float[] centres, bool centred, out int split) =>
            Arrange(teams, order, centres, centred ? CentreWidth : 0f, out split);

        public static float Arrange(IReadOnlyList<int> teams, int[] order, float[] centres, float centreWidth, out int split)
        {
            split = 0;
            float width = PlayerBar.Arrange(teams, order, centres);
            if (!(centreWidth > 0f) || teams == null || order == null || centres == null) return width;
            int n = Math.Min(teams.Count, Math.Min(order.Length, centres.Length));
            if (n == 0) return centreWidth;
            if (n < 2) return width;
            split = Split(teams, order, n);
            float half = PlayerBar.CardWidth(n) * .5f, reach = centreWidth * .5f + CentreGap;
            float left = centres[split - 1] + half, right = centres[split] - half;
            float leftSpan = left - (centres[0] - half), rightSpan = centres[n - 1] + half - right;
            for (int i = 0; i < n; i++) centres[i] += i < split ? -reach - left : reach - right;
            return 2f * (reach + Math.Max(leftSpan, rightSpan));
        }

        public static int Best(IReadOnlyList<int> teams, IReadOnlyList<int> scores, int slot)
        {
            if (teams == null || scores == null || slot < 0 || slot >= teams.Count) return 0;
            int best = int.MinValue;
            for (int i = 0; i < teams.Count && i < scores.Count; i++)
                if (SameSide(teams, i, slot)) best = Math.Max(best, scores[i]);
            return best == int.MinValue ? 0 : best;
        }

        public static string Text(int left, int right) => $"{left} {Separator} {right}";

        public static string Progress(int done, int total) => total > 0 ? $"{done}/{total}" : done.ToString();

        public static string Value(int score, int total, bool progress) => progress ? Progress(score, total) : score.ToString();

        public static int Lead(int left, int right) => left > right ? Left : right > left ? Right : NoLead;

        public static int Leader(IReadOnlyList<int> scores)
        {
            if (scores == null) return NoLead;
            int leader = NoLead, best = 0;
            for (int i = 0; i < scores.Count; i++)
            {
                if (scores[i] > best) { best = scores[i]; leader = i; }
                else if (scores[i] == best) leader = NoLead;
            }
            return leader;
        }

        public static IReadOnlyList<ScoreBlock> Board(IReadOnlyList<ScoreSeat> seats, MatchStats stats)
        {
            var known = Seats(seats, stats);
            var teams = new List<int>(known.Count);
            foreach (var seat in known) teams.Add(seat.Team);
            var blocks = new List<ScoreBlock>();
            if (known.Count == 0) return blocks;
            if (!TwoSides(teams))
            {
                blocks.Add(Block(known, teams, stats, -1, true));
                return blocks;
            }
            int first = FirstSlot(teams), other = 0;
            while (SameSide(teams, other, first)) other++;
            blocks.Add(Block(known, teams, stats, first, false));
            blocks.Add(Block(known, teams, stats, other, false));
            return blocks;
        }

        static List<ScoreSeat> Seats(IReadOnlyList<ScoreSeat> seats, MatchStats stats)
        {
            var known = new List<ScoreSeat>();
            if (seats != null) known.AddRange(seats);
            if (known.Count > 0 || stats == null) return known;
            foreach (var line in stats.Lines) known.Add(new ScoreSeat(line.Seat, line.Name, line.Team, line.Score, 0, false, false));
            return known;
        }

        static int FirstSlot(IReadOnlyList<int> teams)
        {
            if (!PlayerBar.Grouped(teams)) return 0;
            int first = 0;
            for (int i = 1; i < teams.Count; i++) if (teams[i] < teams[first]) first = i;
            return first;
        }

        static ScoreBlock Block(List<ScoreSeat> seats, List<int> teams, MatchStats stats, int slot, bool ranked)
        {
            var rows = new List<ScoreRow>();
            bool mine = false;
            for (int i = 0; i < seats.Count; i++)
            {
                if (!ranked && !SameSide(teams, i, slot)) continue;
                rows.Add(Row(seats[i], stats?.Of(seats[i].Seat)));
                mine |= seats[i].Mine;
            }
            rows.Sort(Order);
            return new ScoreBlock(ranked ? Teams.NoTeam : seats[slot].Team, ranked ? 0 : seats[slot].SideScore, mine, ranked, rows);
        }

        static ScoreRow Row(ScoreSeat seat, MatchStatLine line)
        {
            int score = line != null && line.Score != 0 ? line.Score : seat.Scored ? seat.Score : 0;
            return line == null
                ? new ScoreRow(seat.Seat, seat.Name, seat.Team, 0, 0, 0, 0, score, seat.Mine)
                : new ScoreRow(seat.Seat, seat.Name.Length > 0 ? seat.Name : line.Name, seat.Team, line.Knockouts, line.Falls, line.Crafted, line.Broken, score, seat.Mine);
        }

        static int Order(ScoreRow a, ScoreRow b)
        {
            if (a.Score != b.Score) return b.Score.CompareTo(a.Score);
            if (a.Knockouts != b.Knockouts) return b.Knockouts.CompareTo(a.Knockouts);
            if (a.Falls != b.Falls) return a.Falls.CompareTo(b.Falls);
            return a.Seat.CompareTo(b.Seat);
        }
    }
}
