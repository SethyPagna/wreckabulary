using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public enum ShakeLevel { Off, Low, Full }

    public static class ShakeLevels
    {
        public static float Scale(ShakeLevel level) => level switch { ShakeLevel.Off => 0f, ShakeLevel.Low => .5f, _ => 1f };

        public static string Label(ShakeLevel level) => level switch { ShakeLevel.Off => "OFF", ShakeLevel.Low => "LOW", _ => "FULL" };

        public static ShakeLevel Step(ShakeLevel level, int step) => Parse(Math.Max(0, Math.Min(2, (int)level + step)));

        public static ShakeLevel Parse(int stored) => stored >= 0 && stored <= 2 ? (ShakeLevel)stored : ShakeLevel.Full;
    }

    public static class RecipeStars
    {
        public const string Legend = "More stars, stronger effects";

        public static int Count(RecipeTier tier) => tier switch { RecipeTier.Uncommon => 1, RecipeTier.Rare => 2, RecipeTier.Epic => 3, _ => 0 };
    }

    public static class SeatTags
    {
        public static bool ShowsTag(bool human, int humans, bool shoulderView) => !human || (humans > 1 && !shoulderView);

        public static bool ShowsRing(bool wrecked) => !wrecked;

        public static bool MarksYou(bool you, bool wrecked) => you && !wrecked;

        public static string Name(string name, bool you, int humans) => you && humans == 1 ? "You" : name ?? "";
    }

    public static class HouseGoals
    {
        public static string Line(string mode) => HouseModes.Goal(mode);

        public static bool Rounds(string mode) => HouseModes.Rounds(mode);

        public static string RoundTag(int round, int roundsToWin) => $"ROUND {round} · FIRST TO {roundsToWin}";

        public static string BannerTitle(int round, string modeName) => $"Round {round} · {modeName}";

        public static string BannerLine(string mode, int roundsToWin) => $"{Line(mode)} · First to {roundsToWin}";

        public const string PointsToWin = "pointsToWin";

        public static string Banner(string mode, GameRules rules) =>
            Rounds(mode) ? BannerLine(mode, rules?.RoundsToWin ?? 3)
            : rules != null && rules.KnockoutsToWin > 0 ? $"{Line(mode)} · First to {rules.KnockoutsToWin}"
            : rules != null && rules.Setting(PointsToWin, 0) > 0 ? $"{Line(mode)} · First to {rules.Setting(PointsToWin, 0)}"
            : rules != null && rules.TeamLives > 0 ? $"{Line(mode)} · {rules.TeamLives} lives each"
            : Line(mode);

        public static string Blurb(string mode, int roundsToWin) => Blurb(mode, new GameRules { RoundsToWin = roundsToWin });

        public static string Blurb(string mode, GameRules rules) =>
            HouseModes.Has(mode) && mode != HouseModes.Tutorial ? HouseModes.Blurb(mode, rules) : HouseModes.Blurb(HouseModes.Dibs, rules);

        public static string NumberWord(int n) => n switch
        {
            1 => "one", 2 => "two", 3 => "three", 4 => "four", 5 => "five", 6 => "six", 7 => "seven", 8 => "eight", 9 => "nine",
            _ => n.ToString(),
        };
    }

    public readonly struct SeatResult
    {
        public readonly string Name;
        public readonly int Wins, Health;
        public readonly bool Won, Wrecked, Downed, You, Human;

        public SeatResult(string name, int wins, int health, bool won, bool wrecked, bool downed, bool you, bool human)
        {
            Name = name ?? ""; Wins = wins; Health = health; Won = won; Wrecked = wrecked; Downed = downed; You = you; Human = human;
        }
    }

    public sealed class RoundSummary
    {
        public readonly int Round, Target;
        public readonly bool Final, TimeUp;
        public readonly IReadOnlyList<SeatResult> Seats;

        public RoundSummary(int round, int target, bool final, bool timeUp, IReadOnlyList<SeatResult> seats)
        {
            Round = round; Target = target; Final = final; TimeUp = timeUp; Seats = seats ?? Array.Empty<SeatResult>();
        }

        public int Humans => Seats.Count(s => s.Human);
        public bool Solo => Humans == 1;
        public bool Draw => !Seats.Any(s => s.Won);
        public bool YouWon => Seats.Any(s => s.Won && s.You);
        public int Winners => Seats.Count(s => s.Won);
        public int YourWins => Seats.Where(s => s.You).Select(s => s.Wins).DefaultIfEmpty(0).First();
    }

    public static class HouseResultText
    {
        public static string Winner(RoundSummary s)
        {
            if (s.Draw) return "";
            if (s.Solo && s.YouWon) return s.Winners > 1 ? "Your team" : "You";
            return string.Join(" + ", s.Seats.Where(seat => seat.Won).Select(seat => seat.Name));
        }

        static bool Plural(RoundSummary s) => s.Winners > 1 && !(s.Solo && s.YouWon);

        static bool SaidAsYou(RoundSummary s) => s.Solo && s.YouWon && s.Winners == 1;

        public static string Eyebrow(RoundSummary s)
        {
            string lead = s.Final ? "MATCH OVER" : $"ROUND {s.Round}";
            if (s.Draw) return lead + " · DRAW";
            string verb = Plural(s) || SaidAsYou(s) ? "WIN" : "WINS";
            return $"{lead} · {Winner(s).ToUpperInvariant()} {verb}";
        }

        public static string Heading(RoundSummary s)
        {
            if (s.Draw) return s.TimeUp ? "Time’s up. A perfectly messy draw." : "Nobody left standing. A draw.";
            if (s.Winners > 1) return s.Solo && s.YouWon ? "Your team wins!" : $"{Winner(s)} win!";
            return SaidAsYou(s) ? "You called dibs!" : $"{Winner(s)} called dibs!";
        }

        public static string Flavour(RoundSummary s)
        {
            if (s.Draw) return $"Draws don’t count. First to {s.Target} takes the house.";
            if (s.Final)
            {
                if (s.Solo && s.YouWon) return "The house is yours.";
                return $"{Winner(s)} {(Plural(s) ? "take" : "takes")} the house.";
            }
            if (!s.Solo) return $"First to {s.Target} takes the house.";
            int left = Math.Max(1, s.Target - s.YourWins);
            return $"{left} more round {(left == 1 ? "win" : "wins")} to take the house.";
        }

        public static string Line(SeatResult seat, int target, int humans)
        {
            string state = seat.Wrecked ? "wrecked" : seat.Downed ? "down" : $"{Math.Max(0, seat.Health)} HP";
            string line = $"{SeatTags.Name(seat.Name, seat.You, humans)} · {seat.Wins}/{target} wins · {state}";
            return seat.Won ? "<b>" + line + "</b>" : line;
        }

        public static string[] Lines(RoundSummary s)
        {
            int humans = s.Humans;
            return s.Seats.Select(seat => Line(seat, s.Target, humans)).ToArray();
        }
    }

    public readonly struct BattleSeat
    {
        public readonly string Name;
        public readonly int Knockouts, Team;
        public readonly bool Won, You, Human;

        public BattleSeat(string name, int knockouts, int team, bool won, bool you, bool human)
        {
            Name = name ?? ""; Knockouts = knockouts; Team = team; Won = won; You = you; Human = human;
        }
    }

    public sealed class BattleSummary
    {
        public readonly IReadOnlyList<BattleSeat> Seats;
        public readonly IReadOnlyList<int> Lives;
        public readonly int Target, WinningTeam, LosingTeam;
        public readonly bool TimeUp, SuddenDeath;

        public BattleSummary(IReadOnlyList<BattleSeat> seats, int target, bool timeUp, bool suddenDeath,
            IReadOnlyList<int> lives = null, int winningTeam = Teams.NoTeam, int losingTeam = Teams.NoTeam)
        {
            Seats = seats ?? Array.Empty<BattleSeat>();
            Lives = lives ?? Array.Empty<int>();
            Target = target; TimeUp = timeUp; SuddenDeath = suddenDeath; WinningTeam = winningTeam; LosingTeam = losingTeam;
        }

        public bool Sides => Lives.Count > 0;
        public int Humans => Seats.Count(s => s.Human);
        public bool Solo => Humans == 1;
        public bool Draw => !Seats.Any(s => s.Won);
        public bool YouWon => Seats.Any(s => s.Won && s.You);
        public int YourTeam => Seats.Where(s => s.You).Select(s => s.Team).DefaultIfEmpty(Teams.NoTeam).First();
        public BattleSeat Best => Seats.FirstOrDefault(s => s.Won);
    }

    public static class BattleResultText
    {
        public static string Knockouts(int n) => n == 1 ? "1 knockout" : $"{n} knockouts";

        public static string LifeCount(int n) => n == 1 ? "1 life" : $"{n} lives";

        public static string Lives(int n) => LifeCount(n) + " left";

        static string SideName(BattleSummary s, int team) => s.Solo && team == s.YourTeam ? "Your team" : Teams.SideName(team);

        public static string Winner(BattleSummary s)
        {
            if (s.Draw) return "";
            if (s.Sides) return SideName(s, s.WinningTeam);
            return s.Solo && s.YouWon ? "You" : s.Best.Name;
        }

        public static string Eyebrow(BattleSummary s)
        {
            if (s.Draw) return "MATCH OVER · DRAW";
            string verb = !s.Sides && s.Solo && s.YouWon ? "WIN" : "WINS";
            return $"MATCH OVER · {Winner(s).ToUpperInvariant()} {verb}";
        }

        public static string Heading(BattleSummary s)
        {
            if (s.Draw) return s.Sides ? "Both teams fell together. A draw." : "Nobody scored. A draw.";
            if (s.Sides) return $"{Winner(s)} wins!";
            string who = Winner(s), verb = s.Solo && s.YouWon ? "win" : "wins";
            return s.SuddenDeath ? $"{who} {verb} in sudden death!" : $"{who} {verb} with {Knockouts(s.Best.Knockouts)}!";
        }

        public static string Flavour(BattleSummary s)
        {
            if (s.SuddenDeath) return "Tied when time ran out. The next knockout decided it.";
            if (s.Sides)
                return s.TimeUp ? "More lives left when time ran out."
                    : s.LosingTeam != Teams.NoTeam ? $"{SideName(s, s.LosingTeam)} ran out of lives." : "Nobody left standing.";
            return s.TimeUp ? "Most knockouts when time ran out." : $"First to {Knockouts(s.Target)}.";
        }

        public static string[] Lines(BattleSummary s)
        {
            int humans = s.Humans;
            string Name(BattleSeat seat) => SeatTags.Name(seat.Name, seat.You, humans);
            if (!s.Sides)
                return s.Seats.Select((seat, order) => (seat, order)).OrderByDescending(x => x.seat.Knockouts).ThenBy(x => x.order)
                    .Select(x => Bold($"{Name(x.seat)} · {Knockouts(x.seat.Knockouts)}", x.seat.Won)).ToArray();
            var lines = new List<string>();
            for (int team = 0; team < s.Lives.Count; team++)
            {
                lines.Add(Bold($"{SideName(s, team)} · {Lives(s.Lives[team])}", team == s.WinningTeam));
                var crew = s.Seats.Where(seat => seat.Team == team).Select(seat => $"{Name(seat)} {seat.Knockouts} KO").ToArray();
                if (crew.Length > 0) lines.Add(string.Join(" · ", crew));
            }
            return lines.ToArray();
        }

        static string Bold(string line, bool on) => on ? "<b>" + line + "</b>" : line;
    }
}
