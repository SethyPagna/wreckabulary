using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules.Wordbound
{
    public sealed class WordboundLetterFan
    {
        public const float Spacing = .32f, Overlap = .27f, Storey = .25f;

        struct Spot
        {
            public long Order;
            public float X, Ground, Z, OffsetX, OffsetZ;
        }

        readonly List<Spot> spots = new List<Spot>();
        int[] groups = Array.Empty<int>(), members = Array.Empty<int>();

        public int Count => spots.Count;
        public void Clear() => spots.Clear();
        public float OffsetX(int index) => spots[index].OffsetX;
        public float OffsetZ(int index) => spots[index].OffsetZ;

        public int Add(long order, float x, float ground, float z)
        {
            spots.Add(new Spot { Order = order, X = x, Ground = ground, Z = z });
            return spots.Count - 1;
        }

        public void Spread()
        {
            int count = spots.Count;
            if (groups.Length < count) { groups = new int[count * 2]; members = new int[count * 2]; }
            for (int i = 0; i < count; i++) groups[i] = i;
            for (int i = 0; i < count; i++)
                for (int j = i + 1; j < count; j++)
                    if (Overlapping(spots[i], spots[j])) Join(i, j);
            for (int i = 0; i < count; i++) groups[i] = Root(i);
            for (int i = 0; i < count; i++)
                if (groups[i] == i) Lay(i, count);
        }

        public static (float x, float z) Slot(int index)
        {
            if (index <= 0) return (0f, 0f);
            int ring = 1;
            while (3 * ring * (ring + 1) < index) ring++;
            int step = index - 1 - 3 * ring * (ring - 1);
            double angle = 2 * Math.PI * step / (6 * ring);
            return ((float)(Math.Cos(angle) * ring * Spacing), (float)(Math.Sin(angle) * ring * Spacing));
        }

        void Lay(int group, int count)
        {
            int size = 0;
            float bodiesX = 0, bodiesZ = 0;
            for (int i = group; i < count; i++)
            {
                if (groups[i] != group) continue;
                members[size++] = i;
                bodiesX += spots[i].X;
                bodiesZ += spots[i].Z;
            }
            if (size == 1) { Place(group, 0f, 0f); return; }
            SortByOrder(size);
            float slotsX = 0, slotsZ = 0;
            for (int s = 0; s < size; s++) { var slot = Slot(s); slotsX += slot.x; slotsZ += slot.z; }
            float originX = (bodiesX - slotsX) / size, originZ = (bodiesZ - slotsZ) / size;
            for (int s = 0; s < size; s++)
            {
                var slot = Slot(s);
                var spot = spots[members[s]];
                Place(members[s], originX + slot.x - spot.X, originZ + slot.z - spot.Z);
            }
        }

        void SortByOrder(int size)
        {
            for (int i = 1; i < size; i++)
            {
                int member = members[i], j = i - 1;
                while (j >= 0 && spots[members[j]].Order > spots[member].Order) { members[j + 1] = members[j]; j--; }
                members[j + 1] = member;
            }
        }

        void Place(int index, float x, float z)
        {
            var spot = spots[index];
            spot.OffsetX = x;
            spot.OffsetZ = z;
            spots[index] = spot;
        }

        int Root(int index)
        {
            while (groups[index] != index) index = groups[index] = groups[groups[index]];
            return index;
        }

        void Join(int a, int b)
        {
            int rootA = Root(a), rootB = Root(b);
            if (rootA == rootB) return;
            if (rootA < rootB) groups[rootB] = rootA; else groups[rootA] = rootB;
        }

        static bool Overlapping(Spot a, Spot b)
        {
            float dx = a.X - b.X, dz = a.Z - b.Z;
            return dx * dx + dz * dz < Overlap * Overlap && Math.Abs(a.Ground - b.Ground) < Storey;
        }
    }
}
