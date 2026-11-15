using System;

namespace Wreckabulary.Rules
{
    internal static class SnapshotOrder
    {
        internal static T[] Ordinal<T>(T[] values, Func<T, string> key)
        {
            if (values.Length < 2) return values;
            var keys = new string[values.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = key(values[i]);
            bool ascending = true;
            for (int i = 1; i < keys.Length && ascending; i++) ascending = string.CompareOrdinal(keys[i - 1], keys[i]) < 0;
            if (ascending) return values;
            var order = Positions(values.Length);
            Array.Sort(order, (a, b) =>
            {
                int compared = string.CompareOrdinal(keys[a], keys[b]);
                return compared != 0 ? compared : a.CompareTo(b);
            });
            return Arrange(values, order);
        }

        internal static T[] Numeric<T>(T[] values, Func<T, int> key)
        {
            if (values.Length < 2) return values;
            var keys = new int[values.Length];
            for (int i = 0; i < keys.Length; i++) keys[i] = key(values[i]);
            bool ascending = true;
            for (int i = 1; i < keys.Length && ascending; i++) ascending = keys[i - 1] < keys[i];
            if (ascending) return values;
            var order = Positions(values.Length);
            Array.Sort(order, (a, b) =>
            {
                int compared = keys[a].CompareTo(keys[b]);
                return compared != 0 ? compared : a.CompareTo(b);
            });
            return Arrange(values, order);
        }

        static int[] Positions(int count)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            return order;
        }

        static T[] Arrange<T>(T[] values, int[] order)
        {
            var result = new T[values.Length];
            for (int i = 0; i < result.Length; i++) result[i] = values[order[i]];
            return result;
        }
    }
}
