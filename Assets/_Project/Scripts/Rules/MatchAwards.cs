using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules
{
    public enum MatchAwardKind { GoodSport, Mvp, Knockouts, Speller, Smasher, Hitter, Survivor }

    public enum MatchStatKind { Score, Knockouts, Crafted, Broken, Damage, Falls }

    public static class MatchAwards
    {
        public const int NumbersShown = 3;
        public const string MvpTitle = "Most Valuable Player", KnockoutsTitle = "Knockout Artist", SpellerTitle = "Word Wizard";
        public const string SmasherTitle = "Wrecking Ball", HitterTitle = "Heavy Hitter", SurvivorTitle = "Hard to Wreck", DefaultTitle = "Good Sport";
        public const string KnockoutsLabel = "KO", CraftedLabel = "WORDS", BrokenLabel = "SMASHED", DamageLabel = "DAMAGE", FallsLabel = "FALLS";
        public const string ScoreLabelDefault = "SCORE", WinsLabel = "WINS", ItemsLabel = "ITEMS", RiddlesLabel = "RIDDLES";

        static readonly MatchAwardKind[] Trades = { MatchAwardKind.Knockouts, MatchAwardKind.Speller, MatchAwardKind.Smasher, MatchAwardKind.Hitter };
        static readonly MatchStatKind[] Rounded = { MatchStatKind.Knockouts, MatchStatKind.Crafted, MatchStatKind.Broken, MatchStatKind.Damage };
        static readonly MatchStatKind[] Brawler = { MatchStatKind.Knockouts, MatchStatKind.Damage, MatchStatKind.Falls, MatchStatKind.Crafted };
        static readonly MatchStatKind[] Wordy = { MatchStatKind.Crafted, MatchStatKind.Broken, MatchStatKind.Knockouts, MatchStatKind.Damage };
        static readonly MatchStatKind[] Smashing = { MatchStatKind.Broken, MatchStatKind.Crafted, MatchStatKind.Knockouts, MatchStatKind.Damage };
        static readonly MatchStatKind[] Hitting = { MatchStatKind.Damage, MatchStatKind.Knockouts, MatchStatKind.Falls, MatchStatKind.Crafted };
        static readonly MatchStatKind[] Steady = { MatchStatKind.Falls, MatchStatKind.Knockouts, MatchStatKind.Crafted, MatchStatKind.Broken };

        public static string Title(MatchAwardKind kind) => kind switch
        {
            MatchAwardKind.Mvp => MvpTitle,
            MatchAwardKind.Knockouts => KnockoutsTitle,
            MatchAwardKind.Speller => SpellerTitle,
            MatchAwardKind.Smasher => SmasherTitle,
            MatchAwardKind.Hitter => HitterTitle,
            MatchAwardKind.Survivor => SurvivorTitle,
            _ => DefaultTitle,
        };

        public static MatchStatKind Counted(MatchAwardKind kind) => kind switch
        {
            MatchAwardKind.Knockouts => MatchStatKind.Knockouts,
            MatchAwardKind.Speller => MatchStatKind.Crafted,
            MatchAwardKind.Smasher => MatchStatKind.Broken,
            MatchAwardKind.Hitter => MatchStatKind.Damage,
            MatchAwardKind.Survivor => MatchStatKind.Falls,
            _ => MatchStatKind.Score,
        };

        public static int Value(MatchStatLine line, MatchStatKind kind)
        {
            if (line == null) return 0;
            return kind switch
            {
                MatchStatKind.Knockouts => line.Knockouts,
                MatchStatKind.Crafted => line.Crafted,
                MatchStatKind.Broken => line.Broken,
                MatchStatKind.Damage => (int)Math.Round(line.Damage, MidpointRounding.AwayFromZero),
                MatchStatKind.Falls => line.Falls,
                _ => line.Score,
            };
        }

        public static bool Busy(MatchStatLine line) =>
            line != null && (line.Score > 0 || line.Knockouts > 0 || line.Crafted > 0 || line.Broken > 0 || line.Damage > 0f);

        public static IReadOnlyDictionary<int, MatchAwardKind> Pick(MatchStats stats, int winningTeam = Teams.NoTeam)
        {
            var picked = new Dictionary<int, MatchAwardKind>();
            if (stats == null) return picked;
            var ranked = stats.Ranked();
            var best = winningTeam != Teams.NoTeam ? ranked.FirstOrDefault(l => l.Team == winningTeam) : ranked.FirstOrDefault(Busy);
            if (best != null) picked[best.Seat] = MatchAwardKind.Mvp;
            foreach (var trade in Trades)
            {
                var counted = Counted(trade);
                var holder = ranked.Where(l => !picked.ContainsKey(l.Seat) && Value(l, counted) > 0)
                    .OrderByDescending(l => Value(l, counted)).FirstOrDefault();
                if (holder != null) picked[holder.Seat] = trade;
            }
            if (ranked.Count > 1)
            {
                int fewest = ranked.Min(l => l.Falls);
                var steady = ranked.Any(l => l.Falls > fewest) ? ranked.FirstOrDefault(l => !picked.ContainsKey(l.Seat) && l.Falls == fewest) : null;
                if (steady != null) picked[steady.Seat] = MatchAwardKind.Survivor;
            }
            foreach (var line in ranked)
                if (!picked.ContainsKey(line.Seat)) picked[line.Seat] = MatchAwardKind.GoodSport;
            return picked;
        }

        public static string ScoreLabel(string modeId) => modeId switch
        {
            HouseModes.Dibs => WinsLabel,
            HouseModes.Duos => WinsLabel,
            HouseModes.ShoppingList => ItemsLabel,
            HouseModes.RiddleRush => RiddlesLabel,
            HouseModes.FreeForAll => KnockoutsLabel,
            HouseModes.War => KnockoutsLabel,
            _ => "",
        };

        public static bool ScoreCountsKnockouts(string modeId) => modeId == HouseModes.FreeForAll || modeId == HouseModes.War;

        public static string Label(MatchStatKind kind, string modeId = null)
        {
            switch (kind)
            {
                case MatchStatKind.Knockouts: return KnockoutsLabel;
                case MatchStatKind.Crafted: return CraftedLabel;
                case MatchStatKind.Broken: return BrokenLabel;
                case MatchStatKind.Damage: return DamageLabel;
                case MatchStatKind.Falls: return FallsLabel;
                default:
                    string label = ScoreLabel(modeId);
                    return label.Length > 0 ? label : ScoreLabelDefault;
            }
        }

        static MatchStatKind[] Favoured(MatchAwardKind kind) => kind switch
        {
            MatchAwardKind.Knockouts => Brawler,
            MatchAwardKind.Speller => Wordy,
            MatchAwardKind.Smasher => Smashing,
            MatchAwardKind.Hitter => Hitting,
            MatchAwardKind.Survivor => Steady,
            _ => Rounded,
        };

        public static IReadOnlyList<MatchStatKind> Numbers(MatchAwardKind kind, string modeId = null)
        {
            var shown = new List<MatchStatKind>(NumbersShown);
            bool scored = ScoreLabel(modeId).Length > 0, knockouts = ScoreCountsKnockouts(modeId);
            if (scored) shown.Add(MatchStatKind.Score);
            foreach (var number in Favoured(kind))
            {
                if (shown.Count == NumbersShown) break;
                if (number == MatchStatKind.Knockouts && knockouts) continue;
                shown.Add(number);
            }
            return shown;
        }
    }

    public sealed class MatchEndNumber
    {
        public MatchStatKind Kind { get; }
        public string Label { get; }
        public int Value { get; }
        public string Text => Value.ToString(CultureInfo.InvariantCulture);

        public MatchEndNumber(MatchStatKind kind, string label, int value)
        {
            Kind = kind; Label = label ?? ""; Value = value;
        }
    }

    public sealed class MatchEndCard
    {
        public int Seat { get; }
        public int Team { get; }
        public string Name { get; }
        public bool Bot { get; }
        public bool Winner { get; }
        public bool You { get; }
        public MatchAwardKind Award { get; }
        public string Title => MatchAwards.Title(Award);
        public IReadOnlyList<MatchEndNumber> Numbers { get; }

        public MatchEndCard(MatchStatLine line, string name, bool winner, bool you, MatchAwardKind award, IReadOnlyList<MatchEndNumber> numbers)
        {
            Seat = line.Seat; Team = line.Team; Bot = line.Bot;
            Name = name ?? ""; Winner = winner; You = you; Award = award;
            Numbers = numbers ?? Array.Empty<MatchEndNumber>();
        }
    }

    public sealed class MatchEndBoard
    {
        public string Banner { get; }
        public string ModeLine { get; }
        public bool TwoSides { get; }
        public bool Draw { get; }
        public bool Won { get; }
        public int WinningTeam { get; }
        public int HomeTeam { get; }
        public bool Lost => !Draw && HomeTeam != Teams.NoTeam && !Won;
        public IReadOnlyList<MatchEndCard> Cards { get; }

        public MatchEndBoard(string banner, string modeLine, bool twoSides, bool draw, bool won, int winningTeam, int homeTeam, IReadOnlyList<MatchEndCard> cards)
        {
            Banner = banner ?? ""; ModeLine = modeLine ?? ""; TwoSides = twoSides; Draw = draw; Won = won;
            WinningTeam = winningTeam; HomeTeam = homeTeam; Cards = cards ?? Array.Empty<MatchEndCard>();
        }
    }

    public static class MatchEnd
    {
        public const string Victory = "VICTORY", Defeat = "DEFEAT", DrawWord = "DRAW", YouWin = "YOU WIN", BotTag = "BOT", WinnerTag = "WINNER";
        public const string ScoreGap = " - ", Joiner = " + ", Dot = " · ";

        public static MatchEndBoard Build(MatchStats stats, int winningTeam, string modeId, string place = null)
        {
            stats ??= new MatchStats();
            var lines = stats.Lines;
            bool draw = winningTeam == Teams.NoTeam || !lines.Any(l => l.Team == winningTeam);
            int winners = draw ? Teams.NoTeam : winningTeam;
            var humans = lines.Where(l => !l.Bot).ToList();
            int you = humans.Count == 1 ? humans[0].Seat : -1;
            int home = Home(humans);
            var awards = MatchAwards.Pick(stats, winners);
            var cards = stats.Ranked().OrderByDescending(l => !draw && l.Team == winners)
                .Select(l => Card(l, awards, modeId, !draw && l.Team == winners, l.Seat == you, humans.Count)).ToList();
            return new MatchEndBoard(Banner(stats, winners, humans), ModeLine(modeId, place), stats.Sides().Count == 2, draw,
                !draw && home == winners, winners, home, cards);
        }

        static MatchEndCard Card(MatchStatLine line, IReadOnlyDictionary<int, MatchAwardKind> awards, string modeId, bool winner, bool you, int humans)
        {
            var award = awards.TryGetValue(line.Seat, out var given) ? given : MatchAwardKind.GoodSport;
            var numbers = MatchAwards.Numbers(award, modeId)
                .Select(kind => new MatchEndNumber(kind, MatchAwards.Label(kind, modeId), MatchAwards.Value(line, kind))).ToList();
            return new MatchEndCard(line, SeatTags.Name(line.Name, you, humans), winner, you, award, numbers);
        }

        public static int Home(IReadOnlyList<MatchStatLine> humans) =>
            humans != null && humans.Count > 0 && humans.All(h => h.Team == humans[0].Team) ? humans[0].Team : Teams.NoTeam;

        public static string ModeLine(string modeId, string place = null)
        {
            string mode = HouseModes.Name(modeId).ToUpperInvariant();
            return string.IsNullOrEmpty(place) ? mode : mode + Dot + place.ToUpperInvariant();
        }

        public static string Banner(MatchStats stats, int winningTeam, IReadOnlyList<MatchStatLine> humans)
        {
            var sides = stats.Sides();
            var winners = winningTeam == Teams.NoTeam ? new List<MatchStatLine>() : stats.Side(winningTeam).ToList();
            bool draw = winners.Count == 0;
            int home = Home(humans);
            if (sides.Count == 2)
            {
                int first = home != Teams.NoTeam ? home : draw ? sides[0] : winningTeam;
                if (first != sides[0] && first != sides[1]) first = sides[0];
                int second = sides[0] == first ? sides[1] : sides[0];
                string word = draw ? DrawWord : home == Teams.NoTeam ? Named(Teams.SideName(winningTeam), false) : home == winningTeam ? Victory : Defeat;
                return word + " " + Count(stats.SideScore(first)) + ScoreGap + Count(stats.SideScore(second));
            }
            if (draw) return DrawWord;
            if (home == winningTeam) return humans.Count == 1 && winners.Count == 1 ? YouWin : Victory;
            return Named(string.Join(Joiner, winners.OrderBy(l => l.Seat).Select(l => l.Name)), winners.Count > 1);
        }

        static string Named(string who, bool plural) => who.ToUpperInvariant() + (plural ? " WIN" : " WINS");

        static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
    }
}
