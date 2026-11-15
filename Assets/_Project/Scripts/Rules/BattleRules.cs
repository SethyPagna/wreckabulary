using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public readonly struct Spot
    {
        public readonly float X, Y, Z;

        public Spot(float x, float y, float z) { X = x; Y = y; Z = z; }

        public float DistanceSq(Spot o) => (X - o.X) * (X - o.X) + (Y - o.Y) * (Y - o.Y) + (Z - o.Z) * (Z - o.Z);
    }

    public sealed class KnockoutRace
    {
        readonly Dictionary<int, int> knockouts = new Dictionary<int, int>();
        readonly List<int> seats = new List<int>();

        public int Target { get; }
        public float Seconds { get; }
        public int Winner { get; private set; } = -1;
        public bool Over => Winner >= 0;
        public bool TimeUp { get; private set; }
        public bool SuddenDeath => TimeUp && !Over;
        public bool WonInSuddenDeath { get; private set; }
        public IReadOnlyList<int> Seats => seats;

        public KnockoutRace(int target, float seconds)
        {
            if (target < 0) throw new ArgumentOutOfRangeException(nameof(target));
            Target = target;
            Seconds = seconds;
        }

        public void Enter(int seat)
        {
            if (knockouts.ContainsKey(seat)) return;
            knockouts[seat] = 0;
            seats.Add(seat);
        }

        public int Of(int seat) => knockouts.TryGetValue(seat, out int n) ? n : 0;

        public bool Knockout(int victim, int attacker)
        {
            if (Over || attacker < 0 || attacker == victim || !knockouts.ContainsKey(attacker)) return false;
            int n = ++knockouts[attacker];
            if (Target > 0 && n >= Target) Winner = attacker;
            else if (TimeUp && Leader >= 0) { Winner = Leader; WonInSuddenDeath = true; }
            return true;
        }

        public void Tick(float elapsed)
        {
            if (Over || TimeUp || Seconds <= 0f || elapsed < Seconds) return;
            TimeUp = true;
            Winner = Leader;
        }

        public int Leader
        {
            get
            {
                int best = -1, top = -1;
                bool shared = false;
                foreach (int seat in seats)
                {
                    int n = knockouts[seat];
                    if (n > top) { top = n; best = seat; shared = false; }
                    else if (n == top) shared = true;
                }
                return shared ? -1 : best;
            }
        }

        public IEnumerable<int> Standings => seats.Select((seat, order) => (seat, order))
            .OrderByDescending(s => knockouts[s.seat]).ThenBy(s => s.order).Select(s => s.seat);
    }

    public sealed class WarScore
    {
        readonly int[] lives;
        readonly int[] knockouts;

        public int Sides => lives.Length;
        public float Seconds { get; }
        public int Winner { get; private set; } = Teams.NoTeam;
        public bool Draw { get; private set; }
        public bool Over => Winner != Teams.NoTeam || Draw;
        public bool TimeUp { get; private set; }
        public bool SuddenDeath => TimeUp && !Over;
        public bool WonInSuddenDeath { get; private set; }
        public int Loser { get; private set; } = Teams.NoTeam;
        bool tiedAtBell;

        public WarScore(int sides, int startingLives, float seconds)
        {
            if (sides < 2) throw new ArgumentOutOfRangeException(nameof(sides));
            if (startingLives < 0) throw new ArgumentOutOfRangeException(nameof(startingLives));
            lives = Enumerable.Repeat(startingLives, sides).ToArray();
            knockouts = new int[sides];
            Seconds = seconds;
        }

        public int Lives(int side) => side >= 0 && side < lives.Length ? lives[side] : 0;

        public int KnockoutsBy(int side) => side >= 0 && side < knockouts.Length ? knockouts[side] : 0;

        public bool Knockout(int side, int bySide = Teams.NoTeam)
        {
            if (side < 0 || side >= lives.Length) return false;
            if (bySide >= 0 && bySide < knockouts.Length && bySide != side) knockouts[bySide]++;
            if (lives[side] == 0) return false;
            lives[side]--;
            return true;
        }

        public void Grant(int side)
        {
            if (side >= 0 && side < lives.Length && !Over) lives[side]++;
        }

        public void Evaluate(IReadOnlyList<int> up, float elapsed)
        {
            if (Over) return;
            var left = Enumerable.Range(0, lives.Length).Where(s => lives[s] > 0 || Up(up, s) > 0).ToList();
            if (left.Count == 0) { Draw = true; return; }
            if (left.Count == 1)
            {
                Decide(left[0], Enumerable.Range(0, lives.Length).First(s => s != left[0]));
                return;
            }
            if (!TimeUp && Seconds > 0f && elapsed >= Seconds) TimeUp = true;
            if (!TimeUp) return;
            int leader = Leader(up);
            if (leader < 0) { tiedAtBell = true; return; }
            Decide(leader, Enumerable.Range(0, lives.Length).Where(s => s != leader).OrderBy(s => Strength(up, s)).First());
        }

        void Decide(int winner, int loser)
        {
            Winner = winner;
            Loser = loser;
            WonInSuddenDeath = tiedAtBell;
        }

        public int Strength(IReadOnlyList<int> up, int side) => Lives(side) + Up(up, side);

        int Leader(IReadOnlyList<int> up)
        {
            int best = Teams.NoTeam, top = -1;
            bool shared = false;
            for (int s = 0; s < lives.Length; s++)
            {
                int n = Strength(up, s);
                if (n > top) { top = n; best = s; shared = false; }
                else if (n == top) shared = true;
            }
            return shared ? Teams.NoTeam : best;
        }

        static int Up(IReadOnlyList<int> up, int side) => up != null && side < up.Count ? up[side] : 0;
    }

    public sealed class RoomHold
    {
        public float Seconds { get; }
        public int Holder { get; private set; } = Teams.NoTeam;
        public float Progress { get; private set; }
        public float Fraction => Seconds > 0f ? Math.Min(1f, Progress / Seconds) : 0f;

        public RoomHold(float seconds) => Seconds = seconds;

        public static int HolderOf(IEnumerable<int> sidesInRoom)
        {
            int holder = Teams.NoTeam;
            foreach (int side in sidesInRoom)
            {
                if (holder == Teams.NoTeam) holder = side;
                else if (side != holder) return Teams.NoTeam;
            }
            return holder;
        }

        public int Tick(int holder, float dt)
        {
            if (Seconds <= 0f || holder == Teams.NoTeam) return Teams.NoTeam;
            if (holder != Holder) { Holder = holder; Progress = 0f; }
            Progress += dt;
            if (Progress < Seconds) return Teams.NoTeam;
            Progress -= Seconds;
            return holder;
        }
    }

    public static class SpawnPick
    {
        public static int Farthest(IReadOnlyList<Spot> spawns, IEnumerable<int> allowed, IEnumerable<Spot> enemies, int fallback = 0)
        {
            var foes = enemies?.ToList() ?? new List<Spot>();
            int best = -1;
            float bestScore = float.MinValue;
            foreach (int i in allowed ?? Enumerable.Range(0, spawns.Count))
            {
                if (i < 0 || i >= spawns.Count) continue;
                float score = foes.Count == 0 ? 0f : foes.Min(f => spawns[i].DistanceSq(f));
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best >= 0 ? best : fallback;
        }
    }

    public static class HouseEnds
    {
        public static int[] Split(IReadOnlyList<Spot> spawns)
        {
            var sides = new int[spawns.Count];
            if (spawns.Count < 2) return sides;
            int a = 0, b = 1;
            float far = -1f;
            for (int i = 0; i < spawns.Count; i++)
                for (int j = i + 1; j < spawns.Count; j++)
                {
                    float d = spawns[i].DistanceSq(spawns[j]);
                    if (d > far) { far = d; a = i; b = j; }
                }
            for (int i = 0; i < spawns.Count; i++)
                sides[i] = i == a ? 0 : i == b ? 1 : spawns[i].DistanceSq(spawns[a]) <= spawns[i].DistanceSq(spawns[b]) ? 0 : 1;
            return sides;
        }

        public static List<int> Of(int[] split, int side)
        {
            var list = new List<int>();
            for (int i = 0; i < split.Length; i++) if (split[i] == side) list.Add(i);
            if (list.Count == 0) for (int i = 0; i < split.Length; i++) list.Add(i);
            return list;
        }

        public static string MiddleRoom(HouseLayout layout, IReadOnlyList<Spot> spawns, int[] split)
        {
            if (layout == null || layout.Rooms.Count == 0) return null;
            Spot Centre(int side)
            {
                var mine = Enumerable.Range(0, spawns.Count).Where(i => split[i] == side).Select(i => spawns[i]).ToList();
                if (mine.Count == 0) mine = spawns.ToList();
                return mine.Count == 0 ? new Spot(0f, 0f, 0f) : new Spot(mine.Average(s => s.X), mine.Average(s => s.Y), mine.Average(s => s.Z));
            }
            Spot one = Centre(0), two = Centre(1);
            var middle = new Spot((one.X + two.X) * .5f, Math.Min(one.Y, two.Y), (one.Z + two.Z) * .5f);
            var spawnRooms = new HashSet<string>(layout.Spawns.Select(s => s.Room), StringComparer.Ordinal);
            float Score(RoomBox r)
            {
                float x = (r.MinX + r.MaxX) * .5f - middle.X, z = (r.MinZ + r.MaxZ) * .5f - middle.Z, y = (r.FloorY - middle.Y) * 2f;
                return x * x + y * y + z * z;
            }
            var pick = layout.Rooms.Where(r => !r.Outdoor && r.Area >= 9f && !spawnRooms.Contains(r.Name)).OrderBy(Score).FirstOrDefault()
                ?? layout.Rooms.OrderBy(Score).First();
            return pick.Name;
        }
    }
}
