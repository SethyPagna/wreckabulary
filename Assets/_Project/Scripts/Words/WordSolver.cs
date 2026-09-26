using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary
{
    /// <summary>
    /// Finds which words can be spelled from a set of letters.
    /// Uses 26-slot letter counts, so each check is O(word length).
    /// </summary>
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

        /// <summary>Returns the letters still missing for a word ("SWORD" with no O → "O").</summary>
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

        /// <summary>Spellable words, best score first.</summary>
        public static List<WordEntry> Spellable(WordDatabase db, IEnumerable<char> letters)
        {
            var have = Count(letters);
            return db.words.Where(w => CanSpell(have, w.word))
                           .OrderByDescending(w => w.Score)
                           .ToList();
        }
    }
}
