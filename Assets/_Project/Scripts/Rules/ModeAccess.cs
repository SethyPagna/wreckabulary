using System;
using System.Collections.Generic;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules
{
    public sealed class ModeAccess
    {
        public const int ScreenSeats = 4;
        public const string OnThisScreen = "on this screen", OnePlayer = "1 player", Here = "here", Online = "online";
        public const string TeamsWord = "teams", CoopWord = "co-op", BotsFill = "bots fill", Together = "together", UpTo = "up to";
        public const string LastWon = "Last match: won", LastLost = "Last match: lost";

        static readonly ModeAccess Nowhere = new ModeAccess("");

        public string Id { get; }
        public bool SameScreen { get; private set; }
        public int MinSeats { get; private set; }
        public int MaxSeats { get; private set; }
        public bool Bots { get; private set; }
        public bool Teams { get; private set; }
        public bool Coop { get; private set; }
        public bool PlaysOnline { get; private set; }
        public string OnlineCard { get; private set; }
        public IReadOnlyList<int> TeamSizes { get; private set; } = Array.Empty<int>();
        public int OnlinePlayers { get; private set; }
        public int OnlineNeeded { get; private set; }
        public bool Fill { get; private set; }
        public bool PrivateRoom { get; private set; }

        ModeAccess(string id) { Id = id; }

        public static ModeAccess For(string id)
        {
            if (string.IsNullOrEmpty(id)) return Nowhere;
            var access = new ModeAccess(id);
            var house = HouseModes.Find(id);
            if (house != null) access.SeatHouse(house);
            else
            {
                if (SoloCourse(id)) access.SeatAlone();
                access.SeatOnline();
            }
            return access;
        }

        public static string LastMatch(IEnumerable<MatchRecord> history, string id)
        {
            if (history == null || string.IsNullOrEmpty(id)) return "";
            var last = history.FirstOrDefault(match => match != null && match.Mode == id);
            return last == null ? "" : last.Won ? LastWon : LastLost;
        }

        public static IReadOnlyList<string> Steps(string id)
        {
            var guide = ModeGuideBook.For(id);
            return guide == null ? Array.Empty<string>() : guide.Steps.Select(step => step.Title).ToArray();
        }

        public static bool SoloCourse(string id) =>
            !HouseModes.Has(id) && HouseModes.Families.Any(family => HouseModes.ModesIn(family).Contains(id));

        void SeatHouse(HouseMode house)
        {
            SameScreen = true;
            MaxSeats = Math.Max(1, Math.Min(house.MaxSeats, ScreenSeats));
            MinSeats = Math.Max(1, Math.Min(house.MinSeats, MaxSeats));
            Bots = house.BotsPlay; Teams = house.Teams; Coop = house.Coop;
        }

        void SeatAlone()
        {
            SameScreen = true;
            MinSeats = MaxSeats = 1;
        }

        void SeatOnline()
        {
            var lists = QuickPlaylists.All.Where(list => list.Mode == Id).ToArray();
            PrivateRoom = FriendRooms.All.Any(room => room.Mode == Id);
            if (lists.Length == 0) return;
            PlaysOnline = true;
            OnlineCard = QuickPlayCards.For(lists[0])?.Id;
            if (QuickPlaylists.IsTeamMode(Id)) TeamSizes = lists.Select(list => list.TeamSize).Distinct().OrderBy(size => size).ToArray();
            OnlinePlayers = lists.Max(list => list.Players);
            OnlineNeeded = lists.Min(list => FriendRooms.Needed(list.Mode, list.Format));
            Fill = lists.Any(list => list.Fill);
        }

        public string Line
        {
            get
            {
                string here = HereLine, online = OnlineLine;
                if (here.Length == 0) return online.Length == 0 ? "" : Capital(Online) + MapCardText.Separator + online;
                if (online.Length == 0) return here;
                return (MaxSeats == 1 ? here + " " + Here : here) + MapCardText.Separator + "or " + online + " " + Online;
            }
        }

        string HereLine
        {
            get
            {
                if (!SameScreen) return "";
                if (MaxSeats == 1) return OnePlayer;
                var parts = new List<string> { MinSeats + " to " + MaxSeats + " " + OnThisScreen };
                if (Teams) parts.Add(TeamsWord);
                if (Coop) parts.Add(CoopWord);
                if (Bots) parts.Add(BotsFill);
                return string.Join(MapCardText.Separator, parts);
            }
        }

        string OnlineLine
        {
            get
            {
                if (!PlaysOnline) return "";
                if (TeamSizes.Count > 0) return string.Join(" or ", TeamSizes.Select(size => size + "v" + size));
                return OnlineNeeded >= OnlinePlayers ? OnlinePlayers + " " + Together : UpTo + " " + OnlinePlayers;
            }
        }

        static string Capital(string word) => char.ToUpperInvariant(word[0]) + word.Substring(1);
    }
}
