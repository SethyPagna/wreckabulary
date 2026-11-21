using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Wreckabulary.Rules
{
    public enum MatchAwardKind { GoodSport, Mvp, Knockouts, Speller, Smasher, Hitter, Survivor, Medic }

    public enum MatchStatKind { Score, Knockouts, Crafted, Broken, Damage, Falls, Revives }

    public static class MatchAwards
    {
        public const int NumbersShown = 3;
        public const string MvpTitle = "Most Valuable Player", KnockoutsTitle = "Knockout Artist", SpellerTitle = "Word Wizard";
        public const string SmasherTitle = "Wrecking Ball", HitterTitle = "Heavy Hitter", SurvivorTitle = "Hard to Wreck", DefaultTitle = "Good Sport";
        public const string MedicTitle = "Lifesaver";
        public const string KnockoutsLabel = "KO", CraftedLabel = "WORDS", BrokenLabel = "SMASHED", DamageLabel = "DAMAGE", FallsLabel = "FALLS";
        public const string ScoreLabelDefault = "SCORE", WinsLabel = "WINS", ItemsLabel = "ITEMS", RiddlesLabel = "RIDDLES";
        public const string RevivesLabel = "REVIVES", PackedLabel = "PACKED", UnlockedLabel = "UNLOCKED", PlacedLabel = "PLACED";

        static readonly MatchAwardKind[] Trades = { MatchAwardKind.Knockouts, MatchAwardKind.Speller, MatchAwardKind.Smasher, MatchAwardKind.Hitter };
        static readonly MatchAwardKind[] CrewTrades = { MatchAwardKind.Medic, MatchAwardKind.Speller, MatchAwardKind.Smasher };
        static readonly MatchStatKind[] Rounded = { MatchStatKind.Knockouts, MatchStatKind.Crafted, MatchStatKind.Broken, MatchStatKind.Damage };

        public static string Title(MatchAwardKind kind) => kind switch
        {
            MatchAwardKind.Mvp => MvpTitle,
            MatchAwardKind.Knockouts => KnockoutsTitle,
            MatchAwardKind.Speller => SpellerTitle,
            MatchAwardKind.Smasher => SmasherTitle,
            MatchAwardKind.Hitter => HitterTitle,
            MatchAwardKind.Survivor => SurvivorTitle,
            MatchAwardKind.Medic => MedicTitle,
            _ => DefaultTitle,
        };

        public static MatchStatKind Counted(MatchAwardKind kind) => kind switch
        {
            MatchAwardKind.Knockouts => MatchStatKind.Knockouts,
            MatchAwardKind.Speller => MatchStatKind.Crafted,
            MatchAwardKind.Smasher => MatchStatKind.Broken,
            MatchAwardKind.Hitter => MatchStatKind.Damage,
            MatchAwardKind.Survivor => MatchStatKind.Falls,
            MatchAwardKind.Medic => MatchStatKind.Revives,
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
                MatchStatKind.Revives => line.Revives,
                _ => line.Score,
            };
        }

        public static bool Busy(MatchStatLine line) =>
            line != null && (line.Score > 0 || line.Knockouts > 0 || line.Crafted > 0 || line.Broken > 0 || line.Damage > 0f);

        public static bool Earned(MatchAwardKind kind, MatchStatLine line) => kind switch
        {
            MatchAwardKind.GoodSport => false,
            MatchAwardKind.Survivor => Busy(line),
            _ => Value(line, Counted(kind)) > 0,
        };

        public static IReadOnlyDictionary<int, MatchAwardKind> Pick(MatchStats stats, int winningTeam = Teams.NoTeam)
        {
            if (stats == null) return new Dictionary<int, MatchAwardKind>();
            var ranked = stats.Ranked();
            return Hand(ranked, winningTeam != Teams.NoTeam ? ranked.FirstOrDefault(l => l.Team == winningTeam) : ranked.FirstOrDefault(Busy), Trades);
        }

        public static IReadOnlyDictionary<int, MatchAwardKind> PickCrew(MatchStats stats)
        {
            if (stats == null) return new Dictionary<int, MatchAwardKind>();
            var ranked = stats.Ranked();
            return Hand(ranked, ranked.Count > 1 ? ranked.FirstOrDefault(l => l.Score > 0) : null, CrewTrades);
        }

        static Dictionary<int, MatchAwardKind> Hand(IReadOnlyList<MatchStatLine> ranked, MatchStatLine best, MatchAwardKind[] trades)
        {
            var picked = new Dictionary<int, MatchAwardKind>();
            if (best != null) picked[best.Seat] = MatchAwardKind.Mvp;
            foreach (var trade in trades)
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
            HouseModes.MovingOut => PackedLabel,
            HouseModes.Escape => UnlockedLabel,
            HouseModes.MovingDay => PlacedLabel,
            _ => "",
        };

        public static bool ScoreCountsKnockouts(string modeId) => modeId == HouseModes.FreeForAll || modeId == HouseModes.War;

        public static bool Shows(MatchStatKind kind, string modeId) => kind switch
        {
            MatchStatKind.Knockouts => !ScoreCountsKnockouts(modeId) && !Warmup.Crew(modeId),
            MatchStatKind.Damage => !Warmup.Crew(modeId),
            _ => true,
        };

        public static string Label(MatchStatKind kind, string modeId = null)
        {
            switch (kind)
            {
                case MatchStatKind.Knockouts: return KnockoutsLabel;
                case MatchStatKind.Crafted: return CraftedLabel;
                case MatchStatKind.Broken: return BrokenLabel;
                case MatchStatKind.Damage: return DamageLabel;
                case MatchStatKind.Falls: return FallsLabel;
                case MatchStatKind.Revives: return RevivesLabel;
                default:
                    string label = ScoreLabel(modeId);
                    return label.Length > 0 ? label : ScoreLabelDefault;
            }
        }

        public static IReadOnlyList<MatchStatKind> Columns(string modeId = null)
        {
            var shown = new List<MatchStatKind>(NumbersShown);
            if (ScoreLabel(modeId).Length > 0) shown.Add(MatchStatKind.Score);
            foreach (var number in Rounded)
            {
                if (shown.Count == NumbersShown) break;
                if (Shows(number, modeId)) shown.Add(number);
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
        public bool Titled { get; }
        public string Title => Titled ? MatchAwards.Title(Award) : "";
        public IReadOnlyList<MatchEndNumber> Numbers { get; }

        public MatchEndCard(MatchStatLine line, string name, bool winner, bool you, MatchAwardKind award, IReadOnlyList<MatchEndNumber> numbers)
        {
            Seat = line.Seat; Team = line.Team; Bot = line.Bot;
            Name = name ?? ""; Winner = winner; You = you; Award = award;
            Titled = MatchAwards.Earned(award, line);
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
        public bool Crew { get; }
        public bool Lost => !Draw && !Won && (Crew || HomeTeam != Teams.NoTeam);
        public IReadOnlyList<MatchEndCard> Cards { get; }

        public MatchEndBoard(string banner, string modeLine, bool twoSides, bool draw, bool won, int winningTeam, int homeTeam, IReadOnlyList<MatchEndCard> cards,
            bool crew = false)
        {
            Banner = banner ?? ""; ModeLine = modeLine ?? ""; TwoSides = twoSides; Draw = draw; Won = won;
            WinningTeam = winningTeam; HomeTeam = homeTeam; Cards = cards ?? Array.Empty<MatchEndCard>(); Crew = crew;
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
                .Select(l => Card(l, awards, modeId, !draw && l.Team == winners, l.Seat == you)).ToList();
            return new MatchEndBoard(Banner(stats, winners, humans), ModeLine(modeId, place), stats.Sides().Count == 2, draw,
                !draw && home == winners, winners, home, cards);
        }

        internal static MatchEndCard Card(MatchStatLine line, IReadOnlyDictionary<int, MatchAwardKind> awards, string modeId, bool winner, bool you)
        {
            var award = awards.TryGetValue(line.Seat, out var given) ? given : MatchAwardKind.GoodSport;
            var numbers = MatchAwards.Columns(modeId)
                .Select(kind => new MatchEndNumber(kind, MatchAwards.Label(kind, modeId), MatchAwards.Value(line, kind))).ToList();
            return new MatchEndCard(line, line.Name, winner, you, award, numbers);
        }

        public static string Tag(MatchEndCard card, bool crew)
        {
            if (card == null) return "";
            string seat = SeatTags.Tag(card.You, card.Bot);
            if (!card.Winner || crew) return seat;
            return seat.Length == 0 ? WinnerTag : WinnerTag + Dot + seat;
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

    public enum CrewOutcome { Won, TimeUp, Wiped }

    public static class CrewEnd
    {
        public const string Packed = "ALL PACKED", Out = "EVERYBODY OUT", Furnished = "FURNISHED", Done = "WELL DONE";
        public const string TimeUp = "OUT OF TIME", Wiped = "CREW DOWN";

        public static CrewOutcome Outcome(bool won, bool wiped) => won ? CrewOutcome.Won : wiped ? CrewOutcome.Wiped : CrewOutcome.TimeUp;

        public static string Banner(string modeId, CrewOutcome outcome) => outcome switch
        {
            CrewOutcome.TimeUp => TimeUp,
            CrewOutcome.Wiped => Wiped,
            _ => modeId switch
            {
                HouseModes.MovingOut => Packed,
                HouseModes.Escape => Out,
                HouseModes.MovingDay => Furnished,
                _ => Done,
            },
        };

        public static MatchEndBoard Build(MatchStats stats, CrewOutcome outcome, string modeId, string place = null)
        {
            stats ??= new MatchStats();
            bool won = outcome == CrewOutcome.Won;
            var humans = stats.Lines.Where(l => !l.Bot).ToList();
            int you = humans.Count == 1 ? humans[0].Seat : -1;
            var awards = MatchAwards.PickCrew(stats);
            var cards = stats.Ranked().Select(l => MatchEnd.Card(l, awards, modeId, won, l.Seat == you)).ToList();
            return new MatchEndBoard(Banner(modeId, outcome), MatchEnd.ModeLine(modeId, place), false, false, won, Teams.NoTeam, Teams.NoTeam, cards, crew: true);
        }
    }
}
