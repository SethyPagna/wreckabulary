using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public static class QuickPlayRegions
    {
        public static readonly string[] Ids = { "asia", "eu", "us" };

        public static string Nearest(int utcOffsetMinutes) =>
            utcOffsetMinutes <= -150 ? "us" : utcOffsetMinutes >= 240 ? "asia" : "eu";

        public static string Name(string id) => id switch
        {
            "asia" => "Asia", "eu" => "Europe", "us" => "US", _ => id ?? ""
        };

        public static int LocalOffsetMinutes()
        {
            try { return (int)Math.Round(TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes); }
            catch (TimeZoneNotFoundException) { return 0; }
            catch (InvalidTimeZoneException) { return 0; }
        }
    }

    public sealed class QuickPlaylist
    {
        public string Id { get; }
        public string Mode { get; }
        public string Format { get; }
        public string Label { get; }
        public string Detail { get; }
        public string Group { get; }
        public int TeamSize { get; }
        public int Teams { get; }
        public int Players => TeamSize * Teams;
        public int MaximumParty => Math.Min(QuickPlaylists.MaximumParty, TeamSize);
        public bool Fill => Mode != "FreeForAll";

        internal QuickPlaylist(string id, string mode, string format, int teamSize, int teams, string label, string detail, string group)
        {
            Id = id; Mode = mode; Format = format; TeamSize = teamSize; Teams = teams; Label = label; Detail = detail; Group = group;
        }

        public bool Fits(int partySize) => partySize >= 1 && partySize <= MaximumParty;
    }

    public static class QuickPlaylists
    {
        public const int MaximumParty = 4;
        public static readonly string[] Modes = { "TimeRun", "TeamRace", "BrawlRace", "FreeForAll", "Freestyle" };
        public static readonly string[] Formats = { "Solo", "Duo", "Squad" };
        public const string Classic = "Classic", Arcade = "Arcade";
        public static readonly ReadOnlyCollection<string> Groups = Array.AsReadOnly(new[] { Classic, Arcade });

        public static readonly ReadOnlyCollection<QuickPlaylist> All = Array.AsReadOnly(new[]
        {
            new QuickPlaylist("relay-2v2", "TeamRace", "Duo", 2, 2, "Team Race 2v2", "Teams of 2 race to the delivery", Classic),
            new QuickPlaylist("relay-4v4", "TeamRace", "Squad", 4, 2, "Team Race 4v4", "Teams of 4 race to the delivery", Classic),
            new QuickPlaylist("brawl-2v2", "BrawlRace", "Duo", 2, 2, "Brawl Race 2v2", "Teams of 2 race, and fighting is on", Classic),
            new QuickPlaylist("brawl-4v4", "BrawlRace", "Squad", 4, 2, "Brawl Race 4v4", "Teams of 4 race, and fighting is on", Classic),
            new QuickPlaylist("ffa", "FreeForAll", "Solo", 1, 8, "Free-for-All · 8", "Up to 8 fight, most knockouts wins", Classic),
            new QuickPlaylist("coop", "TimeRun", "Solo", 4, 1, "Time Trial · 4", "Four of you beat the clock together", Arcade),
            new QuickPlaylist("freeplay", "Freestyle", "Solo", 8, 1, "Free Roam · 8", "Up to 8 explore and play, no timer", Arcade)
        });

        public static readonly ReadOnlyCollection<string> Ids = Array.AsReadOnly(All.Select(list => list.Id).ToArray());
        public static readonly ReadOnlyCollection<string> StartTicked = Array.AsReadOnly(Ids.Where(id => id != "freeplay").ToArray());

        public static QuickPlaylist Find(string id) => All.FirstOrDefault(list => list.Id == id);

        public static QuickPlaylist For(string mode, string format) =>
            All.FirstOrDefault(list => list.Mode == mode && list.Format == format);

        public static bool IsTeamMode(string mode) => mode == "TeamRace" || mode == "BrawlRace";
    }

    public sealed class QuickPlayChoices
    {
        public const string DefaultName = "Player";
        public const int MaximumName = 32;
        public const string StorageKey = "wb.online.quickplay";

        public TickSet Playlists { get; }
        public string Name { get; private set; } = DefaultName;

        public string Summary(int shown = 2)
        {
            if (Playlists.All) return "All playlists";
            return Brief(Playlists.Ticked.Select(id => QuickPlaylists.Find(id).Label).ToArray(), shown);
        }

        public string PartyLine(int partySize)
        {
            var ticked = Playlists.Ticked.Select(QuickPlaylists.Find).Where(list => list != null).ToArray();
            var fits = ticked.Where(list => list.Fits(partySize)).Select(list => list.Label).ToArray();
            var skips = ticked.Where(list => !list.Fits(partySize)).Select(list => list.Label).ToArray();
            if (fits.Length == 0) return QuickPlayPicker.Blocked(this, partySize, false);
            string line = "Searches " + Brief(fits, 2);
            return skips.Length == 0 ? line : line + MapCardText.Separator + "skips " + Brief(skips, 1) + " for a party of " + partySize;
        }

        static string Brief(string[] names, int shown)
        {
            if (names.Length <= shown + 1) return string.Join(", ", names);
            return string.Join(", ", names.Take(shown)) + " + " + (names.Length - shown) + " more";
        }

        public QuickPlayChoices(string saved = null)
        {
            var values = Parse(saved);
            string lists = Value(values, "lists");
            var ticked = string.IsNullOrEmpty(lists) ? Array.Empty<string>() :
                lists.Split('+').Where(id => QuickPlaylists.Find(id) != null).ToArray();
            Playlists = new TickSet(QuickPlaylists.Ids, ticked.Length > 0 ? ticked : QuickPlaylists.StartTicked.ToArray());
            ChooseName(Value(values, "name"));
        }

        public void ChooseName(string name)
        {
            string trimmed = (name ?? "").Trim();
            if (trimmed.Length > MaximumName) trimmed = trimmed.Substring(0, MaximumName).Trim();
            Name = trimmed.Length == 0 ? DefaultName : trimmed;
        }

        public string Save() => string.Join(";", "v2", "lists=" + Playlists.Save(), "name=" + Uri.EscapeDataString(Name));

        static Dictionary<string, string> Parse(string saved)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            var parts = (saved ?? "").Split(';');
            if (parts[0] != "v1" && parts[0] != "v2") return values;
            foreach (string part in parts.Skip(1))
            {
                int split = part.IndexOf('=');
                if (split > 0) values[part.Substring(0, split)] = part.Substring(split + 1);
            }
            if (values.TryGetValue("name", out string escaped))
            {
                try { values["name"] = Uri.UnescapeDataString(escaped); }
                catch (UriFormatException) { values.Remove("name"); }
            }
            return values;
        }

        static string Value(Dictionary<string, string> values, string key) => values.TryGetValue(key, out string value) ? value : null;
    }

    public sealed class QuickPlayPick
    {
        public string Mode { get; }
        public string Format { get; }
        public string Seed { get; }
        public string Region { get; }
        public bool Fill => Mode != "FreeForAll";
        public QuickPlaylist Playlist => QuickPlaylists.For(Mode, Format);

        public QuickPlayPick(string mode, string format, string seed, string region)
        {
            if (Array.IndexOf(QuickPlaylists.Modes, mode) < 0 || Array.IndexOf(QuickPlaylists.Formats, format) < 0 ||
                string.IsNullOrEmpty(seed) || Array.IndexOf(QuickPlayRegions.Ids, region) < 0)
                throw new ArgumentException("A Quick Play pick needs a mode, team size, map and region.");
            Mode = mode; Format = format; Seed = seed; Region = region;
        }

        public bool Same(QuickPlayPick other) => other != null && Mode == other.Mode && Format == other.Format &&
            Seed == other.Seed && Region == other.Region;
    }

    public static class QuickPlayPicker
    {
        public const long RotationMilliseconds = 10L * 60 * 1000;

        public static long Slot(long unixMilliseconds) => (long)Math.Floor(unixMilliseconds / (double)RotationMilliseconds);

        public static QuickPlayPick Pick(QuickPlayChoices choices, IReadOnlyList<string> maps, Func<string, string, string, bool> offered,
            int partySize, long slot, string region, QuickPlayPick keep = null, string partySeed = null)
        {
            if (choices == null) throw new ArgumentNullException(nameof(choices));
            if (maps == null) throw new ArgumentNullException(nameof(maps));
            if (offered == null) throw new ArgumentNullException(nameof(offered));
            var kept = keep?.Playlist;
            if (kept != null && choices.Playlists.IsTicked(kept.Id) && kept.Fits(partySize) &&
                offered(keep.Mode, keep.Format, keep.Seed) && (partySeed == null || keep.Seed == partySeed))
                return new QuickPlayPick(keep.Mode, keep.Format, keep.Seed, region);
            var lists = QuickPlaylists.All;
            int start = Start(slot, lists.Count);
            for (int i = 0; i < lists.Count; i++)
            {
                var list = lists[(start + i) % lists.Count];
                if (!choices.Playlists.IsTicked(list.Id) || !list.Fits(partySize)) continue;
                string seed = partySeed != null ? offered(list.Mode, list.Format, partySeed) ? partySeed : null :
                    SeedFor(maps, list, slot, offered);
                if (seed != null) return new QuickPlayPick(list.Mode, list.Format, seed, region);
            }
            return null;
        }

        public static string Blocked(QuickPlayChoices choices, int partySize, bool partyMap)
        {
            var ticked = choices.Playlists.Ticked.Select(QuickPlaylists.Find).ToArray();
            if (ticked.All(list => !list.Fits(partySize)))
            {
                if (partySize > QuickPlaylists.MaximumParty) return "A party has room for " + QuickPlaylists.MaximumParty + " players.";
                if (partySize > 1 && ticked.All(list => list.TeamSize == 1))
                    return "Free-for-All is solo, and a party always plays on one team. Tick a team playlist, or play your friends in a Custom match.";
                var room = QuickPlaylists.All.Where(list => list.Fits(partySize)).Select(list => list.Label).ToArray();
                return "A party of " + partySize + " needs bigger teams. Tick " + JoinOr(room) + ".";
            }
            return partyMap ? "Your party's map has no public match in the ticked playlists. Tick another playlist." :
                "No public map is open for the ticked playlists. Tick another playlist.";
        }

        static string JoinOr(string[] names) => names.Length < 2 ? string.Concat(names) :
            string.Join(", ", names.Take(names.Length - 1)) + " or " + names[names.Length - 1];

        static string SeedFor(IReadOnlyList<string> maps, QuickPlaylist list, long slot, Func<string, string, string, bool> offered)
        {
            if (maps.Count == 0) return null;
            int start = Start(slot, maps.Count);
            for (int i = 0; i < maps.Count; i++)
            {
                string seed = maps[(start + i) % maps.Count];
                if (!string.IsNullOrEmpty(seed) && offered(list.Mode, list.Format, seed)) return seed;
            }
            return null;
        }

        static int Start(long slot, int count) => (int)(((slot % count) + count) % count);
    }

    public enum QuickPlayGoal { Search, Party }

    public enum QuickPlayStep { None, Wait, Connect, CreateParty, LeaveParty, ApplySettings, Ready, Queue, WaitForMembers, WaitForLeader, Searching, PartyReady, Blocked }

    public struct QuickPlayState
    {
        public bool MatchActive, RetryRequired, Busy, Authenticated, HasParty, HasAttempt, QuickPlayAttempt;
        public bool IsLeader, SelfReady, AllReady, SeedMatches, SettingsMatch, HasRoom;
        public int Members;
    }

    public static class QuickPlayFlow
    {
        public static QuickPlayStep Next(QuickPlayState state, QuickPlayGoal goal)
        {
            if (state.MatchActive) return QuickPlayStep.None;
            if (state.RetryRequired) return QuickPlayStep.Blocked;
            if (state.Busy) return QuickPlayStep.Wait;
            if (!state.Authenticated) return QuickPlayStep.Connect;
            if (!state.HasParty) return QuickPlayStep.CreateParty;
            if (goal == QuickPlayGoal.Party)
                return state.HasRoom || !state.IsLeader ? QuickPlayStep.PartyReady :
                    state.HasAttempt || state.SettingsMatch ? QuickPlayStep.Blocked : QuickPlayStep.ApplySettings;
            if (state.HasAttempt) return state.QuickPlayAttempt ? QuickPlayStep.Searching : QuickPlayStep.Blocked;
            if (!state.IsLeader) return state.SelfReady ? QuickPlayStep.WaitForLeader : QuickPlayStep.Ready;
            if (!state.SeedMatches) return state.Members == 1 ? QuickPlayStep.LeaveParty : QuickPlayStep.Blocked;
            if (!state.SettingsMatch) return QuickPlayStep.ApplySettings;
            if (!state.SelfReady) return QuickPlayStep.Ready;
            if (!state.AllReady) return QuickPlayStep.WaitForMembers;
            return QuickPlayStep.Queue;
        }

        public static bool Acts(QuickPlayStep step) => step == QuickPlayStep.Connect || step == QuickPlayStep.CreateParty ||
            step == QuickPlayStep.LeaveParty || step == QuickPlayStep.ApplySettings || step == QuickPlayStep.Ready || step == QuickPlayStep.Queue;

        public static string Elapsed(double seconds)
        {
            long whole = seconds <= 0 || double.IsNaN(seconds) ? 0 : (long)Math.Floor(Math.Min(seconds, 359999));
            return whole / 60 + ":" + (whole % 60).ToString("00");
        }
    }
}
