using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public enum HouseModeFamily { Learn, Battle, Coop, WordGames, Courses }

    public sealed class HouseMode
    {
        public string Id = "", Name = "", Goal = "", Tag = "", Blurb = "";
        public HouseModeFamily Family;
        public int MinSeats = 1, MaxSeats = 4, BotSeats;
        public int Sides;
        public bool Teams, Coop, Rounds;

        public bool BotsPlay => BotSeats > 0;
    }

    public static class HouseModes
    {
        public const string Tutorial = "Tutorial", Dibs = "Dibs", Duos = "Duos", FreeForAll = "Deathmatch", War = "War";
        public const string MovingOut = "MovingOut", MovingDay = "MovingDay", Escape = "Escape";
        public const string ShoppingList = "ShoppingList", RiddleRush = "RiddleRush";

        static readonly HouseMode[] Entries =
        {
            new HouseMode
            {
                Id = Tutorial, Name = "Tutorial", Family = HouseModeFamily.Learn, Tag = "PLAY & LEARN",
                Goal = "No pressure. Just play.", Blurb = "A friendly room to try smashing, spelling and every skill.",
                MaxSeats = 1,
            },
            new HouseMode
            {
                Id = Dibs, Name = "Dibs!", Family = HouseModeFamily.Battle, Tag = "HOUSE PARTY", Rounds = true, BotSeats = 4,
                Goal = "Last one standing wins the round",
                Blurb = "Smash, spell, survive. Last one standing wins the round. First to {rounds} takes the house.",
            },
            new HouseMode
            {
                Id = Duos, Name = "Duos", Family = HouseModeFamily.Battle, Tag = "2V2 · TEAMS", Rounds = true, Teams = true, BotSeats = 4,
                Goal = "Revive your partner · last team standing",
                Blurb = "Hold interact beside a downed partner to revive them. Last team standing wins the round. First to {rounds} takes the house.",
            },
            new HouseMode
            {
                Id = FreeForAll, Name = "Free-for-All", Family = HouseModeFamily.Battle, Tag = "HOUSE PARTY", BotSeats = 4,
                Goal = "Most knockouts wins",
                Blurb = "Everyone against everyone. You come back {respawn} seconds after a knockout. First to {knockouts} knockouts wins, or the most when time runs out.",
            },
            new HouseMode
            {
                Id = War, Name = "War", Family = HouseModeFamily.Battle, Tag = "TWO TEAMS", Teams = true, Sides = 2, MaxSeats = 8, BotSeats = 8,
                Goal = "Knock out the other team’s lives",
                Blurb = "Two teams with {lives} lives each. Every knockout costs a life. A team out of lives with nobody standing loses. Hold the middle room for extra lives.",
            },
            new HouseMode
            {
                Id = MovingOut, Name = "Moving Out", Family = HouseModeFamily.Coop, Tag = "CO-OP", Coop = true,
                Goal = "Bring every keepsake to the van",
                Blurb = "Bring every keepsake to the van, then get the whole crew there before the house is packed.",
            },
            new HouseMode
            {
                Id = MovingDay, Name = "Moving Day", Family = HouseModeFamily.Coop, Tag = "CO-OP", Coop = true,
                Goal = "Furnish the marked rooms",
                Blurb = "Spell the checklist furniture and set each piece down in its marked room.",
            },
            new HouseMode
            {
                Id = Escape, Name = "Escape", Family = HouseModeFamily.Coop, Tag = "CO-OP", Coop = true,
                Goal = "Open the doors, find the keys, get out",
                Blurb = "Spell the barred doors open, find the keys, then get everyone out.",
            },
            new HouseMode
            {
                Id = ShoppingList, Name = "Shopping List", Family = HouseModeFamily.WordGames, Tag = "WORD GAMES", Rounds = true, BotSeats = 4,
                Goal = "Spell everything on your list first",
                Blurb = "Everyone gets the same list of items. Spell each one in order, then hit, place or use it to tick it off. First to {rounds} rounds wins.",
            },
            new HouseMode
            {
                Id = RiddleRush, Name = "Riddle Rush", Family = HouseModeFamily.WordGames, Tag = "WORD GAMES", BotSeats = 4,
                Goal = "Solve the riddle, spell the answer",
                Blurb = "Read the riddle, find the letters and spell the answer before anyone else.",
            },
        };

        static readonly Dictionary<string, HouseMode> ById = Entries.ToDictionary(m => m.Id, StringComparer.Ordinal);

        public static IReadOnlyList<HouseMode> All => Entries;

        public static IEnumerable<HouseMode> InFamily(HouseModeFamily family) => Entries.Where(m => m.Family == family);

        public static bool Has(string id) => id != null && ById.ContainsKey(id);

        public static HouseMode Find(string id) => id != null && ById.TryGetValue(id, out var mode) ? mode : null;

        public static string Name(string id) => Find(id)?.Name ?? id ?? "";

        public static string Goal(string id) => Find(id)?.Goal ?? "";

        public static bool Rounds(string id) => Find(id)?.Rounds ?? false;

        public static int BotSeats(string id) => Find(id)?.BotSeats ?? 4;

        public static string FamilyName(HouseModeFamily family) => family switch
        {
            HouseModeFamily.Learn => "Learn",
            HouseModeFamily.Battle => "Battle",
            HouseModeFamily.Coop => "Co-op",
            HouseModeFamily.WordGames => "Word games",
            _ => "Courses",
        };

        public static string Blurb(string id, GameRules rules)
        {
            var mode = Find(id);
            if (mode == null) return "";
            rules ??= new GameRules();
            return mode.Blurb
                .Replace("{rounds}", HouseGoals.NumberWord(rules.RoundsToWin))
                .Replace("{knockouts}", rules.KnockoutsToWin.ToString())
                .Replace("{lives}", rules.TeamLives.ToString())
                .Replace("{respawn}", HouseGoals.NumberWord((int)Math.Round(Math.Max(0f, rules.RespawnSeconds))));
        }
    }
}
