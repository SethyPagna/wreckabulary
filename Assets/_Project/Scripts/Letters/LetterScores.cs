namespace Wreckabulary
{
    public enum LetterRarity { Common, Rare, Legendary }

    /// <summary>Letter values (Scrabble-style) and rarity tiers.</summary>
    public static class LetterScores
    {
        //                         A  B  C  D  E  F  G  H  I  J  K  L  M  N  O  P  Q  R  S  T  U  V  W  X  Y  Z
        static readonly int[] v = { 1, 3, 3, 2, 1, 4, 2, 4, 1, 8, 5, 1, 3, 1, 1, 3, 10, 1, 1, 1, 1, 4, 4, 8, 4, 10 };

        public static int ValueOf(char c)
        {
            int i = char.ToUpperInvariant(c) - 'A';
            return i >= 0 && i < 26 ? v[i] : 0;
        }

        public static LetterRarity RarityOf(char c)
        {
            int s = ValueOf(c);
            if (s >= 8) return LetterRarity.Legendary;
            if (s >= 4) return LetterRarity.Rare;
            return LetterRarity.Common;
        }

        public static int ScoreOf(string word)
        {
            int total = 0;
            foreach (char c in word) total += ValueOf(c);
            return total;
        }
    }
}
