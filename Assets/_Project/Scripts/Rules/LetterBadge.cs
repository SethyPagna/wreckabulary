using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public static class LetterBadge
    {
        public const string SeatInitials = "WORD";
        public static IReadOnlyList<string> Letters { get; } =
            Array.AsReadOnly(Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).ToArray());

        public static bool IsLetter(string letter) => letter != null && letter.Length == 1 && letter[0] >= 'A' && letter[0] <= 'Z';

        public static string SeatInitial(int seat) => SeatInitials[((seat % SeatInitials.Length) + SeatInitials.Length) % SeatInitials.Length].ToString();

        public static string For(Outfit outfit, int seat) =>
            IsLetter(outfit?.Letter) ? outfit.Letter : SeatInitial(outfit != null && outfit.Seat >= 0 ? outfit.Seat : seat);

        public static Outfit With(Outfit outfit, string letter)
        {
            if (outfit == null || (letter != null && !IsLetter(letter))) return null;
            var next = outfit.Clone();
            next.Letter = letter;
            return next;
        }

        public static Outfit ForSeat(Outfit outfit, int seat)
        {
            if (outfit == null) return null;
            var next = outfit.Clone();
            next.Seat = seat;
            return next;
        }
    }
}
