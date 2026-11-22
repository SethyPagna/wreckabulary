using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public readonly struct ColourSeat
    {
        public readonly int Id, Team, Hue;

        public ColourSeat(int id, int team, int hue)
        {
            Id = id; Team = team; Hue = hue;
        }
    }

    public static class TeamColours
    {
        public const int None = -1;

        public static bool Sided(IReadOnlyList<ColourSeat> seats)
        {
            if (seats == null) return false;
            bool shared = false, mixed = false;
            for (int i = 0; i < seats.Count; i++)
                for (int j = i + 1; j < seats.Count; j++)
                    if (Together(seats, i, j)) shared = true;
                    else mixed = true;
            return shared && mixed;
        }

        public static bool Accented(IReadOnlyList<ColourSeat> seats) => Sided(seats);

        public static int Frame(IReadOnlyList<ColourSeat> seats, int seat)
        {
            if (seats == null || seat < 0 || seat >= seats.Count) return None;
            var frames = new List<int>(seats.Count);
            Frames(seats, frames);
            return frames[seat];
        }

        public static void Frames(IReadOnlyList<ColourSeat> seats, List<int> frames)
        {
            if (frames == null) return;
            frames.Clear();
            if (seats == null) return;
            for (int i = 0; i < seats.Count; i++) frames.Add(i);
            if (!Sided(seats)) return;
            int lastId = int.MinValue, lastSlot = None;
            while (true)
            {
                int leader = NextLeader(seats, lastId, lastSlot);
                if (leader == None) return;
                int bearer = Bearer(seats, frames, leader);
                for (int m = 0; m < seats.Count; m++) if (Together(seats, m, leader)) frames[m] = bearer;
                lastId = seats[leader].Id; lastSlot = leader;
            }
        }

        static bool Together(IReadOnlyList<ColourSeat> seats, int a, int b) =>
            ScoreLine.Side(seats[a].Team, a) == ScoreLine.Side(seats[b].Team, b);

        static bool Earlier(IReadOnlyList<ColourSeat> seats, int a, int b) =>
            seats[a].Id < seats[b].Id || (seats[a].Id == seats[b].Id && a < b);

        static bool Leads(IReadOnlyList<ColourSeat> seats, int seat)
        {
            for (int i = 0; i < seats.Count; i++)
                if (i != seat && Together(seats, i, seat) && Earlier(seats, i, seat)) return false;
            return true;
        }

        static int NextLeader(IReadOnlyList<ColourSeat> seats, int lastId, int lastSlot)
        {
            int next = None;
            for (int i = 0; i < seats.Count; i++)
            {
                bool after = seats[i].Id > lastId || (seats[i].Id == lastId && i > lastSlot);
                if (after && Leads(seats, i) && (next == None || Earlier(seats, i, next))) next = i;
            }
            return next;
        }

        static int Bearer(IReadOnlyList<ColourSeat> seats, List<int> frames, int leader)
        {
            int bearer = None;
            for (int m = 0; m < seats.Count; m++)
                if (Together(seats, m, leader) && !Taken(seats, frames, leader, seats[m].Hue) && (bearer == None || Earlier(seats, m, bearer))) bearer = m;
            return bearer == None ? leader : bearer;
        }

        static bool Taken(IReadOnlyList<ColourSeat> seats, List<int> frames, int leader, int hue)
        {
            for (int i = 0; i < seats.Count; i++)
                if (Leads(seats, i) && Earlier(seats, i, leader) && seats[frames[i]].Hue == hue) return true;
            return false;
        }
    }
}
