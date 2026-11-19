using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public static class PracticeMaps
    {
        public const string Every = "Any house";

        public static TickSet Load(IEnumerable<string> houses, string saved) => TickSet.Load(houses, saved);

        public static TickSet Only(IEnumerable<string> houses, string house)
        {
            var maps = new TickSet(houses);
            if (!maps.Has(house)) throw new ArgumentException("Choose one of the installed houses.", nameof(house));
            return new TickSet(maps.Options, new[] { house });
        }

        public static bool Toggle(TickSet maps, string house, bool savedHomeChosen)
        {
            if (maps == null) throw new ArgumentNullException(nameof(maps));
            if (!savedHomeChosen) return maps.Toggle(house);
            if (!maps.IsTicked(house)) maps.Toggle(house);
            return true;
        }

        public static string Pick(TickSet maps, Func<int, int> roll)
        {
            if (maps == null) throw new ArgumentNullException(nameof(maps));
            if (roll == null) throw new ArgumentNullException(nameof(roll));
            var ticked = maps.Ticked;
            if (ticked.Count == 1) return ticked[0];
            int at = roll(ticked.Count);
            if (at < 0 || at >= ticked.Count)
                throw new ArgumentOutOfRangeException(nameof(roll), "The random source must return an index below the ticked count.");
            return ticked[at];
        }

        public static string Label(TickSet maps, Func<string, string> name)
        {
            if (maps == null) throw new ArgumentNullException(nameof(maps));
            if (maps.Count == 1) return (name ?? (id => id))(maps.Ticked[0]);
            return maps.All ? Every : "Any of " + maps.Count + " houses";
        }
    }
}
