using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class FriendRoomMode
    {
        public string Id { get; }
        public string Mode { get; }
        public string Label { get; }
        public string Detail { get; }
        public bool Teams => FriendRooms.IsTeamMode(Mode);

        public FriendRoomMode(string id, string mode, string label, string detail)
        {
            Id = id; Mode = mode; Label = label; Detail = detail;
        }
    }

    public static class FriendRooms
    {
        public const string StorageKey = "wb.online.room";
        public const string InvitePrefix = "wb-room", PartyPrefix = "wb-party";
        public const int Layouts = 4;
        public const int MostPlayers = 8;
        public const int InviteLength = 160;

        public static readonly IReadOnlyList<FriendRoomMode> All = Array.AsReadOnly(new[]
        {
            new FriendRoomMode("relay", "TeamRace", "Team Race", "Two teams race to the delivery"),
            new FriendRoomMode("brawl", "BrawlRace", "Brawl Race", "Two teams race, and fighting is on"),
            new FriendRoomMode("ffa", "FreeForAll", "Free-for-All", "2 to 8 fight, most knockouts wins"),
            new FriendRoomMode("freeplay", "Freestyle", "Free Roam", "1 to 8 explore and play, no timer"),
        });

        public static readonly IReadOnlyList<string> TeamSizes = Array.AsReadOnly(new[] { "Solo", "Duo", "Squad" });

        public static FriendRoomMode Find(string id) => All.FirstOrDefault(room => room.Id == id);

        public static bool IsTeamMode(string mode) => mode == "TeamRace" || mode == "BrawlRace";

        public static int TeamSize(string format) => format == "Squad" ? 4 : format == "Duo" ? 2 : 1;

        public static string TeamLabel(string format) => TeamSize(format) + "v" + TeamSize(format);

        public static string Format(FriendRoomMode room, string teamSize) =>
            room != null && room.Teams && TeamSizes.Contains(teamSize) ? teamSize : "Solo";

        public static bool Fill(string mode) => IsTeamMode(mode) || mode == "TimeRun";

        public static int Teams(string mode) => IsTeamMode(mode) ? 2 : mode == "FreeForAll" ? MostPlayers : 1;

        public static int Capacity(string mode, string format) =>
            IsTeamMode(mode) ? 2 * TeamSize(format) : mode == "TimeRun" ? 4 : MostPlayers;

        public static int Needed(string mode, string format) =>
            mode == "Freestyle" ? 1 : mode == "FreeForAll" ? 2 : Capacity(mode, format);

        public static bool CanStart(string mode, string format, int players) =>
            players >= Needed(mode, format) && players <= Capacity(mode, format);

        public static string Name(string mode) => mode switch
        {
            "TeamRace" => "Team Race",
            "BrawlRace" => "Brawl Race",
            "FreeForAll" => "Free-for-All",
            "Freestyle" => "Free Roam",
            "TimeRun" => "Time Trial",
            _ => mode ?? ""
        };

        public static string Title(string mode, string format) =>
            IsTeamMode(mode) ? Name(mode) + " " + TeamLabel(format) : Name(mode);

        public static string Players(string mode, string format)
        {
            int needed = Needed(mode, format), most = Capacity(mode, format);
            return needed == most ? "Needs " + most + " players" : needed + " to " + most + " players";
        }

        public static string Waiting(string mode, string format, int players)
        {
            int most = Capacity(mode, format);
            string count = players + " / " + most + " players";
            if (CanStart(mode, format, players)) return count + MapCardText.Separator + "ready to start";
            return count + MapCardText.Separator + "needs " + Needed(mode, format) + " to start";
        }

        public static bool LooksLikeInvite(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string trimmed = text.Trim();
            if (trimmed.Length > InviteLength) return false;
            var parts = trimmed.Split(':');
            return parts.Length == 8 && parts[0] == InvitePrefix && Hex16(parts[1]) && Hex16(parts[6]) &&
                parts.Skip(2).Take(4).All(part => part.Length > 0) && parts[7].Length > 0;
        }

        public static bool LooksLikePartyInvite(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            var parts = text.Trim().Split(':');
            return parts.Length == 4 && parts[0] == PartyPrefix && Uuid(parts[1]) && Hex16(parts[2]) && Hex16(parts[3]);
        }

        static bool Uuid(string value) => value.Length == 36 && value.Select((c, index) =>
            index == 8 || index == 13 || index == 18 || index == 23 ? c == '-' : c >= '0' && c <= '9' || c >= 'a' && c <= 'f').All(ok => ok);

        public static string ShortCode(string code) =>
            string.IsNullOrEmpty(code) || code.Length != 16 ? code ?? "" :
            string.Join(" ", Enumerable.Range(0, 4).Select(index => code.Substring(index * 4, 4))).ToUpperInvariant();

        static bool Hex16(string value) => value.Length == 16 && value.All(c => c >= '0' && c <= '9' || c >= 'a' && c <= 'f');
    }

    public static class OnlineMaps
    {
        public const string CustomMap = "Custom map", PrivateMap = "Private map";

        public static string Name(string courseId) => courseId switch
        {
            WordboundFlagshipContent.CourseId => "The Lost Address",
            WordboundParcelServiceContent.CourseId => WordboundParcelServiceContent.CourseTitle,
            "arena-proof" => "Toybox Arena",
            "race-proof" => "Lost Parcel Circuit",
            "freestyle-proof" => "Parcel Playroom",
            _ => null
        };

        public static string CourseFor(string mode) => mode == "FreeForAll" ? "arena-proof" : WordboundFlagshipContent.CourseId;

        public static string Slot(string courseId, int slot)
        {
            string name = Name(courseId);
            if (name == null || slot < 0) return PrivateMap;
            return slot == 0 ? name : name + " " + (slot + 1);
        }

        public static string For(string mode, int slot) => Slot(CourseFor(mode), slot);
    }

    public sealed class FriendRoomChoice
    {
        public string Mode { get; private set; } = "relay";
        public string TeamSize { get; private set; } = "Duo";
        public int Layout { get; private set; }

        public FriendRoomChoice(string saved = null)
        {
            if (string.IsNullOrEmpty(saved)) return;
            var parts = saved.Split(';');
            if (parts.Length == 0 || parts[0] != "v1") return;
            foreach (var part in parts.Skip(1))
            {
                int split = part.IndexOf('=');
                if (split <= 0) continue;
                string key = part.Substring(0, split), value = part.Substring(split + 1);
                if (key == "mode") Choose(value);
                else if (key == "team") ChooseTeamSize(value);
                else if (key == "layout" && int.TryParse(value, out int layout)) ChooseLayout(layout);
            }
        }

        public FriendRoomMode Selected => FriendRooms.Find(Mode);

        public string Format => FriendRooms.Format(Selected, TeamSize);

        public bool Choose(string id)
        {
            if (FriendRooms.Find(id) == null) return false;
            Mode = id;
            return true;
        }

        public bool ChooseTeamSize(string format)
        {
            if (!FriendRooms.TeamSizes.Contains(format)) return false;
            TeamSize = format;
            return true;
        }

        public bool ChooseLayout(int layout)
        {
            if (layout < 0 || layout >= FriendRooms.Layouts) return false;
            Layout = layout;
            return true;
        }

        public string Summary() => FriendRooms.Title(Selected.Mode, Format) + MapCardText.Separator + OnlineMaps.For(Selected.Mode, Layout);

        public string Save() => "v1;mode=" + Mode + ";team=" + TeamSize + ";layout=" + Layout;
    }
}
