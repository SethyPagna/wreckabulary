using System.Collections.Generic;
using System.Linq;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public static class WordSolver
    {
        public static int[] Count(IEnumerable<char> letters)
        {
            var counts = new int[26];
            foreach (char c in letters)
            {
                int i = char.ToUpperInvariant(c) - 'A';
                if (i >= 0 && i < 26) counts[i]++;
            }
            return counts;
        }

        public static bool CanSpell(int[] have, string word)
        {
            var need = new int[26];
            foreach (char c in word)
            {
                int i = char.ToUpperInvariant(c) - 'A';
                if (i < 0 || i >= 26 || ++need[i] > have[i]) return false;
            }
            return true;
        }

        public static string Missing(int[] have, string word)
        {
            var left = (int[])have.Clone();
            var missing = new System.Text.StringBuilder();
            foreach (char c in word)
            {
                int i = char.ToUpperInvariant(c) - 'A';
                if (i >= 0 && i < 26 && left[i] > 0) left[i]--;
                else missing.Append(c);
            }
            return missing.ToString();
        }

        public static SpellRole RoleOf(WordEntry entry, IReadOnlyCollection<string> objectives = null)
        {
            if (objectives != null && objectives.Contains(entry.word)) return SpellRole.Objective;
            return GameConfig.Current.Items.TryGet(entry.word, out var item) && item.Enabled ? SpellChoice.RoleOf(item) : RoleOf(entry.category);
        }

        static SpellRole RoleOf(WordCategory category) => category switch
        {
            WordCategory.Weapon => SpellRole.Weapon,
            WordCategory.Defence => SpellRole.Shield,
            WordCategory.Chaos => SpellRole.Shot,
            _ => SpellRole.Gadget,
        };

        public static List<WordEntry> Spellable(IEnumerable<WordEntry> words, IEnumerable<char> letters) =>
            Spellable(words, letters, SpellNeeds.Unarmed, null);

        public static List<WordEntry> Spellable(IEnumerable<WordEntry> words, IEnumerable<char> letters, SpellNeeds needs, IReadOnlyCollection<string> objectives)
        {
            var have = Count(letters);
            return SpellChoice.Order(words.Where(w => CanSpell(have, w.word)), w => w.word, w => RoleOf(w, objectives), needs);
        }

        public static List<WordEntry> Spellable(WordDatabase db, IEnumerable<char> letters) => Spellable(db.Words, letters);

        public static List<(WordEntry entry, string missing)> Hints(IEnumerable<WordEntry> words, IEnumerable<char> letters,
                                                                    int capacity, int maxMissing = 1) =>
            Hints(words, letters, capacity, SpellNeeds.Unarmed, null, maxMissing);

        public static List<(WordEntry entry, string missing)> Hints(IEnumerable<WordEntry> words, IEnumerable<char> letters, int capacity,
                                                                    SpellNeeds needs, IReadOnlyCollection<string> objectives, int maxMissing = 1)
        {
            var have = Count(letters);
            var near = words.Where(w => !w.hidden && w.word.Length <= capacity && !CanSpell(have, w.word))
                            .Select(w => (entry: w, missing: Missing(have, w.word)))
                            .Where(h => h.missing.Length <= maxMissing);
            return SpellChoice.Order(near, h => h.entry.word, h => RoleOf(h.entry, objectives), needs);
        }
    }
}
