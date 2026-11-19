using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules
{
    public sealed class Finish
    {
        public string Id { get; }
        public string Name { get; }
        public string Tagline { get; }
        public int Price { get; }
        public bool InCustomize { get; }
        public uint Swatch { get; }
        public uint Accent { get; }
        public string Emblem { get; }
        public IReadOnlyList<string> OnlyFor { get; }
        public string Suffix => "_" + Id;

        public Finish(string id, string name, string tagline, int price, bool inCustomize, uint swatch, uint accent, params string[] onlyFor)
        {
            Id = id; Name = name; Tagline = tagline; Price = price; InCustomize = inCustomize;
            Swatch = swatch; Accent = accent; Emblem = "UI/Finishes/" + id;
            OnlyFor = onlyFor ?? Array.Empty<string>();
        }

        public bool Covers(string word) => OnlyFor.Count == 0 || OnlyFor.Contains(word, StringComparer.OrdinalIgnoreCase);

        public string MaterialFor(string family) => family + Suffix;

        public override string ToString() => Id;
    }

    public static class Finishes
    {
        public static Finish Classic { get; } = new Finish(Skin.Standard, "Classic", "Warm woods, brass trim and woven fabric", 0, true, 0xE6A55A, 0x764B31);

        public static IReadOnlyList<Finish> All { get; } = new[]
        {
            Classic,
            new Finish("Candy", "Candy", "Glossy sweets with candy stripes and sprinkles", 250, true, 0xFF9DC6, 0xC7A8FF),
            new Finish("Arcade", "Arcade", "Midnight cabinets, neon trim and a glowing grid", 400, true, 0x26206A, 0x2BE6FF),
            new Finish("Winter", "Winter", "Frosted pine, holly red and soft snowflecks", 0, true, 0x2E7A5C, 0xEEF4F8, "SHIELD", "SODA"),
        };

        public static IEnumerable<string> Ids => All.Select(finish => finish.Id);

        public static Finish Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var finish in All)
                if (string.Equals(finish.Id, id, StringComparison.Ordinal)) return finish;
            return null;
        }

        public static IEnumerable<Finish> ForItem(string word, IReadOnlyList<string> itemSkins)
        {
            foreach (var finish in All)
                if (finish == Classic || itemSkins != null && itemSkins.Contains(finish.Id) && finish.Covers(word))
                    yield return finish;
        }

        public static string FamilyOf(string materialName)
        {
            if (string.IsNullOrEmpty(materialName)) return null;
            foreach (var finish in All)
                if (materialName.Length > finish.Suffix.Length && materialName.EndsWith(finish.Suffix, StringComparison.Ordinal))
                    return materialName.Substring(0, materialName.Length - finish.Suffix.Length);
            return null;
        }

        public static string FinishOf(string materialName)
        {
            string family = FamilyOf(materialName);
            return family == null ? null : materialName.Substring(family.Length + 1);
        }
    }
}
