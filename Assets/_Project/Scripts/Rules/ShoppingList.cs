using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public enum ShoppingKind { Weapon, Deployable, Tool }

    public enum ShoppingAct { Hit, Place, Use }

    public readonly struct ShoppingItem : IEquatable<ShoppingItem>
    {
        public readonly string Word;
        public readonly ShoppingKind Kind;

        public ShoppingItem(string word, ShoppingKind kind)
        {
            Word = word;
            Kind = kind;
        }

        public ShoppingAct Act => ShoppingList.ActFor(Kind);

        public bool Equals(ShoppingItem other) => Word == other.Word && Kind == other.Kind;
        public override bool Equals(object obj) => obj is ShoppingItem other && Equals(other);
        public override int GetHashCode() => (Word?.GetHashCode() ?? 0) * 3 + (int)Kind;
        public override string ToString() => Word;
    }

    public static class ShoppingList
    {
        public const int Length = 4;
        public const int Copies = 4;

        public static ShoppingKind? KindOf(ItemDefinition item)
        {
            if (item == null || !item.Enabled || item.Category is ItemCategory.Furniture or ItemCategory.Legacy) return null;
            return SpellChoice.RoleOf(item) switch
            {
                SpellRole.Weapon or SpellRole.Shot => ShoppingKind.Weapon,
                SpellRole.Heal or SpellRole.Boost => ShoppingKind.Tool,
                SpellRole.Gadget when item.Deploy != null => ShoppingKind.Deployable,
                _ => null,
            };
        }

        public static ShoppingAct ActFor(ShoppingKind kind) => kind switch
        {
            ShoppingKind.Weapon => ShoppingAct.Hit,
            ShoppingKind.Deployable => ShoppingAct.Place,
            _ => ShoppingAct.Use,
        };

        public static LetterBag Supply(HouseLayout house)
        {
            var bag = new LetterBag();
            if (house == null) return bag;
            foreach (var f in house.Furniture)
                if (LetterBag.IsWord(f.Word)) bag.Add(LetterBag.FromWord(f.Word));
            return bag;
        }

        public static int CopiesIn(LetterBag supply, string word)
        {
            if (supply == null || !LetterBag.IsWord(word)) return 0;
            var need = LetterBag.FromWord(word);
            int copies = int.MaxValue;
            foreach (char c in need.Letters().Distinct()) copies = Math.Min(copies, supply[c] / need[c]);
            return copies == int.MaxValue ? 0 : copies;
        }

        public static List<ShoppingItem> Candidates(IEnumerable<ItemDefinition> items, LetterBag supply, int maxLetters, int copies = Copies)
        {
            var found = new List<ShoppingItem>();
            foreach (var item in items ?? Enumerable.Empty<ItemDefinition>())
            {
                var kind = KindOf(item);
                if (kind == null || item.Id.Length > maxLetters || CopiesIn(supply, item.Id) < copies) continue;
                if (found.Any(f => f.Word == item.Id)) continue;
                found.Add(new ShoppingItem(item.Id, kind.Value));
            }
            found.Sort((a, b) => string.CompareOrdinal(a.Word, b.Word));
            return found;
        }

        public static List<ShoppingItem> Draw(IEnumerable<ItemDefinition> items, LetterBag supply, int maxLetters, int seed,
            int length = Length, int copies = Copies)
        {
            var pool = Candidates(items, supply, maxLetters, copies);
            var dice = new Dice(seed);
            var weapons = pool.Where(i => i.Kind == ShoppingKind.Weapon).ToList();
            var others = pool.Where(i => i.Kind != ShoppingKind.Weapon).ToList();
            dice.Shuffle(weapons);
            dice.Shuffle(others);
            var picked = others.Take(Math.Min(1, length)).ToList();
            picked.AddRange(weapons.Take(length - picked.Count));
            picked.AddRange(others.Skip(1).Take(length - picked.Count));
            dice.Shuffle(picked);
            return picked;
        }

        public static int RoundSeed(int matchSeed, int round)
        {
            unchecked
            {
                ulong mixed = Dice.Mix(((ulong)(uint)matchSeed << 32) | (uint)round);
                return (int)(mixed ^ (mixed >> 32));
            }
        }

        public static string Prompt(ShoppingItem item, bool spelled) => !spelled ? "Spell it" : item.Act switch
        {
            ShoppingAct.Hit => "Hit someone with it",
            ShoppingAct.Place => "Place it",
            _ => "Use it",
        };

        public static string Progress(int done, int total) => $"{done}/{total}";

        public static string ResultLine(string name, int done, int total, int wins, int target, bool won)
        {
            string line = $"{name} · {done} of {total} items · {wins}/{target} wins";
            return won ? "<b>" + line + "</b>" : line;
        }

        static bool IsYou(string name) => name == "You";

        public static string Eyebrow(int round, bool final, string winner) =>
            (final ? "MATCH OVER" : $"ROUND {round}")
            + (string.IsNullOrEmpty(winner) ? " · DRAW" : $" · {winner.ToUpperInvariant()} {(IsYou(winner) ? "WIN" : "WINS")}");

        public static string Heading(string winner, bool listDone) =>
            string.IsNullOrEmpty(winner) ? "Time’s up. Nobody is ahead, so it’s a draw."
            : listDone ? $"{winner} got everything on the list!"
            : $"Time’s up. {winner} ticked off the most.";

        public static string Flavour(string winner, bool final, int target) =>
            final && !string.IsNullOrEmpty(winner) ? $"{winner} {(IsYou(winner) ? "win" : "wins")} Shopping List."
            : $"First to {target} rounds wins. Next round, new list.";

        sealed class Dice
        {
            ulong state;

            public Dice(int seed) => state = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x2545F4914F6CDD1DUL;

            public static ulong Mix(ulong z)
            {
                unchecked
                {
                    z += 0x9E3779B97F4A7C15UL;
                    z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                    z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                    return z ^ (z >> 31);
                }
            }

            public int Next(int below)
            {
                state = Mix(state);
                return (int)(state % (ulong)below);
            }

            public void Shuffle<T>(IList<T> list)
            {
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = Next(i + 1);
                    (list[i], list[j]) = (list[j], list[i]);
                }
            }
        }
    }

    public sealed class ShoppingRace
    {
        sealed class Card
        {
            public int Step;
            public double LastTick = double.PositiveInfinity;
            public readonly HashSet<string> Spelled = new HashSet<string>(StringComparer.Ordinal);
        }

        readonly Dictionary<int, Card> cards = new Dictionary<int, Card>();
        readonly List<int> seats = new List<int>();

        public IReadOnlyList<ShoppingItem> Items { get; }
        public IReadOnlyList<int> Seats => seats;
        public int? Winner { get; private set; }

        public ShoppingRace(IReadOnlyList<ShoppingItem> items)
        {
            Items = items?.ToList() ?? throw new ArgumentNullException(nameof(items));
        }

        public void Join(int seat)
        {
            if (cards.ContainsKey(seat)) return;
            cards.Add(seat, new Card());
            seats.Add(seat);
        }

        public int Done(int seat) => cards.TryGetValue(seat, out var card) ? card.Step : 0;

        public bool Finished(int seat) => Items.Count > 0 && Done(seat) >= Items.Count;

        public ShoppingItem? Current(int seat)
        {
            if (!cards.TryGetValue(seat, out var card) || card.Step >= Items.Count) return null;
            return Items[card.Step];
        }

        public string Wanted(int seat) => Current(seat)?.Word;

        public bool HasSpelled(int seat, string word) => word != null && cards.TryGetValue(seat, out var card) && card.Spelled.Contains(word);

        public bool CurrentSpelled(int seat) => HasSpelled(seat, Wanted(seat));

        public bool Spelled(int seat, string word)
        {
            if (Winner != null || word == null || !cards.TryGetValue(seat, out var card)) return false;
            for (int i = card.Step; i < Items.Count; i++)
                if (Items[i].Word == word) return card.Spelled.Add(word);
            return false;
        }

        public bool Count(int seat, string word, ShoppingAct act, double time)
        {
            if (Winner != null || !cards.TryGetValue(seat, out var card) || card.Step >= Items.Count) return false;
            var item = Items[card.Step];
            if (item.Word != word || item.Act != act || !card.Spelled.Contains(word)) return false;
            card.Step++;
            card.LastTick = time;
            if (card.Step >= Items.Count) Winner = seat;
            return true;
        }

        public List<int> Standings() => seats
            .OrderByDescending(Done)
            .ThenBy(s => cards[s].LastTick)
            .ThenBy(s => seats.IndexOf(s))
            .ToList();

        public int? Leader()
        {
            if (Winner != null) return Winner;
            var order = Standings();
            if (order.Count == 0 || Done(order[0]) == 0) return null;
            if (order.Count > 1 && Done(order[1]) == Done(order[0]) && cards[order[1]].LastTick.Equals(cards[order[0]].LastTick)) return null;
            return order[0];
        }
    }
}
