using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules
{
    public sealed class Riddle
    {
        public string Answer { get; }
        public string Clue { get; }
        public string Hint { get; }

        public Riddle(string answer, string clue, string hint = null)
        {
            Answer = (answer ?? "").Trim().ToUpperInvariant();
            Clue = (clue ?? "").Trim();
            Hint = (hint ?? "").Trim();
        }

        public bool Spellable => LetterBag.IsWord(Answer);
        public LetterBag Letters => Spellable ? LetterBag.FromWord(Answer) : new LetterBag();
    }

    public sealed class RiddleBook
    {
        public const string FileName = "riddles_house.json";
        public const int MaxClueLength = 80;
        readonly List<Riddle> riddles;

        public IReadOnlyList<Riddle> All => riddles;

        public RiddleBook(IEnumerable<Riddle> all) => riddles = (all ?? Enumerable.Empty<Riddle>()).ToList();

        public static RiddleBook FromJson(string json, string source = FileName)
        {
            var root = Json.Parse(json, source);
            if (!root.Has("riddles")) throw root.Error("needs a riddles list");
            return new RiddleBook(root["riddles"].Items.Select(n => new Riddle(n["answer"].String(), n["clue"].String(), n["hint"].String(""))));
        }

        public static int LengthMarker(string clue)
        {
            if (string.IsNullOrEmpty(clue) || clue[clue.Length - 1] != ')') return -1;
            int open = clue.LastIndexOf('(');
            if (open < 0) return -1;
            string inside = clue.Substring(open + 1, clue.Length - open - 2);
            return int.TryParse(inside, NumberStyles.None, CultureInfo.InvariantCulture, out int n) ? n : -1;
        }

        public List<string> Validate(IEnumerable<string> recipeWords)
        {
            var recipes = new HashSet<string>(recipeWords ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            var problems = new List<string>();
            if (riddles.Count == 0) problems.Add("there are no riddles");
            var answers = new HashSet<string>(StringComparer.Ordinal);
            var clues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var riddle in riddles)
            {
                string name = riddle.Answer.Length > 0 ? riddle.Answer : "(blank answer)";
                if (!riddle.Spellable) { problems.Add($"{name}: the answer must be A-Z letters"); continue; }
                if (!recipes.Contains(riddle.Answer)) problems.Add($"{name} is not a House recipe word");
                if (!answers.Add(riddle.Answer)) problems.Add($"{name} has more than one riddle");
                if (riddle.Clue.Length == 0) { problems.Add($"{name} has no clue"); continue; }
                if (!clues.Add(riddle.Clue)) problems.Add($"{name} shares its clue with another riddle");
                if (riddle.Clue.Length > MaxClueLength) problems.Add($"{name}: the clue has {riddle.Clue.Length} characters; keep it to {MaxClueLength}");
                if (LengthMarker(riddle.Clue) != riddle.Answer.Length) problems.Add($"{name}: the clue must end with ({riddle.Answer.Length})");
                Check(riddle, "clue", riddle.Clue, recipes, problems);
                Check(riddle, "hint", riddle.Hint, recipes, problems);
            }
            return problems;
        }

        static void Check(Riddle riddle, string part, string text, HashSet<string> recipes, List<string> problems)
        {
            if (text.ToUpperInvariant().Contains(riddle.Answer)) problems.Add($"{riddle.Answer}: the {part} gives the answer away");
            foreach (string word in Words(text))
                if (word != riddle.Answer && Names(word, recipes, out string other)) problems.Add($"{riddle.Answer}: the {part} names {other}, another answer");
        }

        static IEnumerable<string> Words(string text)
        {
            int start = -1;
            for (int i = 0; i <= text.Length; i++)
            {
                bool letter = i < text.Length && char.IsLetter(text[i]);
                if (letter && start < 0) start = i;
                else if (!letter && start >= 0) { yield return text.Substring(start, i - start).ToUpperInvariant(); start = -1; }
            }
        }

        static bool Names(string word, HashSet<string> recipes, out string recipe)
        {
            foreach (string form in new[] { word, word.EndsWith("ES") ? word.Substring(0, word.Length - 2) : null, word.EndsWith("S") ? word.Substring(0, word.Length - 1) : null })
                if (form != null && recipes.Contains(form)) { recipe = form; return true; }
            recipe = null;
            return false;
        }

        public static LetterBag Supply(HouseLayout house)
        {
            var bag = new LetterBag();
            if (house == null) return bag;
            foreach (var furniture in house.Furniture)
                if (LetterBag.IsWord(furniture.Word)) bag.Add(LetterBag.FromWord(furniture.Word));
            return bag;
        }

        public List<Riddle> For(LetterBag supply, int maxLetters, ICollection<string> recipes = null) =>
            riddles.Where(r => r.Spellable && r.Answer.Length <= maxLetters && (recipes == null || recipes.Contains(r.Answer))
                && supply != null && supply.Contains(r.Letters)).ToList();

        public List<Riddle> For(HouseLayout house, int maxLetters, ICollection<string> recipes = null) => For(Supply(house), maxLetters, recipes);
    }

    public sealed class RiddleDeck
    {
        readonly List<Riddle> pool;
        readonly List<Riddle> waiting = new List<Riddle>();
        Pcg32 random;
        Riddle last;

        public RiddleDeck(IEnumerable<Riddle> riddles, ulong seed)
        {
            pool = (riddles ?? Enumerable.Empty<Riddle>()).ToList();
            random = Pcg32.ForStream(seed, "riddles/deck");
            Refill();
        }

        public int Count => pool.Count;
        public int Left => waiting.Count;

        void Refill()
        {
            waiting.Clear();
            waiting.AddRange(pool);
            random.Shuffle(waiting);
            if (waiting.Count > 1 && waiting[0] == last) (waiting[0], waiting[waiting.Count - 1]) = (waiting[waiting.Count - 1], waiting[0]);
        }

        public Riddle Draw(Func<Riddle, bool> usable = null)
        {
            if (pool.Count == 0) return null;
            if (waiting.Count == 0) Refill();
            int at = waiting.FindIndex(r => usable == null || usable(r));
            if (at < 0)
            {
                Refill();
                at = waiting.FindIndex(r => r != last && (usable == null || usable(r)));
            }
            if (at < 0) return null;
            last = waiting[at];
            waiting.RemoveAt(at);
            return last;
        }
    }

    public enum RiddleStage { Waiting, Asking, Solved, Won }

    public enum RiddleEvent { None, Asked, Refurnish, Hint }

    public sealed class RiddleRace
    {
        public const int PointsToWin = 5;
        public const float HintSeconds = 40f, PassSeconds = 40f, BeatSeconds = 3f, BotKnowsMin = 8f, BotKnowsMax = 15f;

        readonly RiddleDeck deck;
        readonly int[] points;
        readonly float[] knowsAt;
        Pcg32 random;

        public RiddleRace(RiddleDeck deck, int seats, ulong seed, int target = PointsToWin, float hintAfter = HintSeconds, float beat = BeatSeconds)
        {
            if (seats < 1) throw new ArgumentOutOfRangeException(nameof(seats));
            if (target < 1) throw new ArgumentOutOfRangeException(nameof(target));
            this.deck = deck ?? throw new ArgumentNullException(nameof(deck));
            points = new int[seats];
            knowsAt = new float[seats];
            random = Pcg32.ForStream(seed, "riddles/bots");
            Target = target;
            HintAfter = hintAfter;
            Beat = beat;
        }

        public int Target { get; }
        public float HintAfter { get; }
        public float Beat { get; }
        public float PassAfter { get; set; } = PassSeconds;
        public int Passes { get; private set; }
        public string LastPassed { get; private set; }
        public float KnowsMin { get; set; } = BotKnowsMin;
        public float KnowsMax { get; set; } = BotKnowsMax;
        public int Seats => points.Length;
        public RiddleStage Stage { get; private set; } = RiddleStage.Waiting;
        public Riddle Current { get; private set; }
        public float Elapsed { get; private set; }
        public int Asked { get; private set; }
        public int Solver { get; private set; } = -1;
        public int Winner => Stage == RiddleStage.Won ? Solver : -1;
        public bool HintShown => Stage == RiddleStage.Asking && Elapsed >= HintAfter;
        public float UntilHint => Stage == RiddleStage.Asking ? Math.Max(0f, HintAfter - Elapsed) : 0f;

        bool Seat(int seat) => seat >= 0 && seat < points.Length;
        public int Points(int seat) => Seat(seat) ? points[seat] : 0;
        public float KnowsAt(int seat) => Seat(seat) ? knowsAt[seat] : float.PositiveInfinity;
        public bool Knows(int seat) => Stage == RiddleStage.Asking && Seat(seat) && Elapsed >= knowsAt[seat];

        public RiddleEvent Start(Func<Riddle, bool> usable = null)
        {
            Array.Clear(points, 0, points.Length);
            Asked = 0;
            Solver = -1;
            Passes = 0;
            LastPassed = null;
            return Ask(usable);
        }

        RiddleEvent Ask(Func<Riddle, bool> usable)
        {
            var next = deck.Draw(usable);
            var result = RiddleEvent.Asked;
            if (next == null && usable != null)
            {
                next = deck.Draw();
                result = RiddleEvent.Refurnish;
            }
            if (next == null) { Stage = RiddleStage.Waiting; Current = null; return RiddleEvent.None; }
            Current = next;
            Stage = RiddleStage.Asking;
            Elapsed = 0f;
            Solver = -1;
            Asked++;
            for (int i = 0; i < knowsAt.Length; i++) knowsAt[i] = random.Range(KnowsMin, KnowsMax);
            return result;
        }

        public bool Solve(int seat, string word)
        {
            if (Stage != RiddleStage.Asking || Current == null || !Seat(seat) || !string.Equals(word, Current.Answer, StringComparison.OrdinalIgnoreCase)) return false;
            points[seat]++;
            Solver = seat;
            Elapsed = 0f;
            Stage = points[seat] >= Target ? RiddleStage.Won : RiddleStage.Solved;
            return true;
        }

        public RiddleEvent Tick(float seconds, Func<Riddle, bool> usable = null)
        {
            if (seconds <= 0f || Stage == RiddleStage.Waiting || Stage == RiddleStage.Won) return RiddleEvent.None;
            bool hinted = HintShown;
            Elapsed += seconds;
            if (Stage == RiddleStage.Solved) return Elapsed >= Beat ? Ask(usable) : RiddleEvent.None;
            if (Elapsed >= HintAfter + PassAfter)
            {
                Passes++;
                LastPassed = Current.Answer;
                return Ask(usable);
            }
            return !hinted && HintShown ? RiddleEvent.Hint : RiddleEvent.None;
        }

        public char Tile(int index)
        {
            if (Current == null || index < 0 || index >= Current.Answer.Length) return '\0';
            return Stage != RiddleStage.Asking || (index == 0 && HintShown) ? Current.Answer[index] : '\0';
        }

        public IEnumerable<int> Standings() => Enumerable.Range(0, points.Length).OrderByDescending(s => points[s]).ThenBy(s => s);

        public int Solved => points.Sum();

        public int Leader()
        {
            int best = -1, top = 0;
            bool tied = false;
            for (int seat = 0; seat < points.Length; seat++)
            {
                if (points[seat] > top) { top = points[seat]; best = seat; tied = false; }
                else if (points[seat] == top && top > 0) tied = true;
            }
            return tied ? -1 : best;
        }
    }

    public static class RiddleHints
    {
        public const int MaxNamed = 3;

        public static List<int> Sources(string answer, IReadOnlyList<string> furniture, int maxNamed = MaxNamed)
        {
            var picked = new List<int>();
            if (!LetterBag.IsWord(answer) || furniture == null) return picked;
            for (int i = 0; i < furniture.Count && picked.Count < maxNamed; i++)
                if (furniture[i] == answer) picked.Add(i);
            if (picked.Count > 0) return picked;
            var missing = LetterBag.FromWord(answer);
            while (!missing.IsEmpty)
            {
                int best = -1, bestGain = 0;
                for (int i = 0; i < furniture.Count; i++)
                {
                    if (picked.Contains(i) || !LetterBag.IsWord(furniture[i])) continue;
                    int gain = Overlap(missing, furniture[i]);
                    if (gain > bestGain) { best = i; bestGain = gain; }
                }
                if (best < 0) break;
                picked.Add(best);
                foreach (char c in furniture[best]) missing.TryRemove(c);
            }
            return picked;
        }

        static int Overlap(LetterBag missing, string word)
        {
            var left = new LetterBag(missing);
            int gain = 0;
            foreach (char c in word) if (left.TryRemove(c)) gain++;
            return gain;
        }
    }

    public static class RiddleText
    {
        public const string Refurnished = "Fresh furniture for the next riddle!";
        public const string Tie = "Time’s up. It’s a tie!";
        public const string NoRiddles = "No riddles for this house yet.";

        static string Mode => HouseModes.Name(HouseModes.RiddleRush);

        public static string Opening(int target) => $"Read the riddle and spell the answer. First to {target} points wins.";

        public static string OneMore(string who) => $"{who}: one more point to win!";

        public static string UntilHint(float seconds)
        {
            int left = Math.Max(0, (int)Math.Ceiling(seconds));
            return $"Hint in {left / 60}:{left % 60:00}";
        }

        public static string StartsWith(Riddle riddle) => riddle == null || riddle.Answer.Length == 0 ? ""
            : $"Starts with {riddle.Answer[0]}" + (riddle.Hint.Length > 0 ? ". " + riddle.Hint : "");

        public static string Spelled(string who, string answer) => $"{who} spelled {answer}!";

        public static string Passed(string answer) => $"Nobody got it. It was {answer}.";

        public static string Points(int points) => points == 1 ? "1 point" : $"{points} points";

        public static string Heading(string winner, bool you) =>
            string.IsNullOrEmpty(winner) ? Tie : you ? $"You win {Mode}!" : $"{winner} wins {Mode}!";

        public static string Eyebrow(string winner, bool you) =>
            string.IsNullOrEmpty(winner) ? "MATCH OVER · DRAW" : $"MATCH OVER · {winner.ToUpperInvariant()} {(you ? "WIN" : "WINS")}";

        public static string Flavour(int solved) => solved == 1 ? "1 riddle solved." : $"{solved} riddles solved.";

        public static string Line(string name, int points, bool won)
        {
            string line = $"{name} · {Points(points)}";
            return won ? "<b>" + line + "</b>" : line;
        }
    }
}
