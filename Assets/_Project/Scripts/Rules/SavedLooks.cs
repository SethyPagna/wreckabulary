using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wreckabulary.Rules
{
    public sealed class SavedLook
    {
        public string Name { get; }
        public string OutfitText { get; }

        internal SavedLook(string name, string outfitText)
        {
            Name = name;
            OutfitText = outfitText;
        }

        public Outfit ToOutfit() => Outfit.Deserialize(OutfitText);
    }

    public sealed class SavedLookCheck
    {
        public Outfit Outfit { get; }
        public IReadOnlyList<string> Problems { get; }
        public IReadOnlyList<string> Missing { get; }
        public string Reason { get; }
        public bool Wearable => Outfit != null && Problems.Count == 0 && Missing.Count == 0;

        internal SavedLookCheck(Outfit outfit, IReadOnlyList<string> problems, IReadOnlyList<string> missing, string reason)
        {
            Outfit = outfit;
            Problems = problems;
            Missing = missing;
            Reason = reason;
        }
    }

    public sealed class SavedLooks
    {
        public const int Slots = 6;
        public const int NameLength = 16;
        public const int Version = 1;
        public const int MaxOutfitLength = 2048;
        public const int MaxStoredLength = 32768;
        public const string NamePrefix = "Look ";
        public const string NeedsName = "Give the look a name.";
        public const string NoSuchLook = "There is no look in that slot.";
        public const string NotAnOutfit = "That outfit can't be saved.";
        public static readonly string Full = "All " + Slots.ToString(CultureInfo.InvariantCulture) + " look slots are full.";

        const char EntryBreak = '/', FieldBreak = ',', Escape = '%';
        const int EscapeDigits = 4;
        const char LineSeparator = '\u2028', ParagraphSeparator = '\u2029';
        static readonly string VersionText = Version.ToString(CultureInfo.InvariantCulture);

        readonly List<SavedLook> looks = new List<SavedLook>();

        public IReadOnlyList<SavedLook> All => looks;
        public int Count => looks.Count;
        public bool IsFull => looks.Count >= Slots;
        public SavedLook this[int index] => Has(index) ? looks[index] : null;
        public bool Has(int index) => index >= 0 && index < looks.Count;

        public string Add(string name, Outfit outfit) => Add(name, outfit?.Serialize());

        public string Add(string name, string outfitText)
        {
            if (IsFull) return Full;
            string clean = CleanName(name);
            if (clean == null) return NeedsName;
            if (!IsOutfit(outfitText)) return NotAnOutfit;
            looks.Add(new SavedLook(clean, outfitText));
            return null;
        }

        public string Replace(int index, Outfit outfit) => Replace(index, outfit?.Serialize());

        public string Replace(int index, string outfitText)
        {
            if (!Has(index)) return NoSuchLook;
            if (!IsOutfit(outfitText)) return NotAnOutfit;
            looks[index] = new SavedLook(looks[index].Name, outfitText);
            return null;
        }

        public string Rename(int index, string name)
        {
            if (!Has(index)) return NoSuchLook;
            string clean = CleanName(name);
            if (clean == null) return NeedsName;
            looks[index] = new SavedLook(clean, looks[index].OutfitText);
            return null;
        }

        public string Remove(int index)
        {
            if (!Has(index)) return NoSuchLook;
            looks.RemoveAt(index);
            return null;
        }

        public int IndexOf(Outfit outfit) => outfit == null ? -1 : IndexOf(outfit.Serialize());

        public int IndexOf(string outfitText)
        {
            for (int i = 0; i < looks.Count; i++)
                if (looks[i].OutfitText == outfitText) return i;
            return -1;
        }

        public string SuggestName()
        {
            for (int number = 1; ; number++)
            {
                string name = NamePrefix + number.ToString(CultureInfo.InvariantCulture);
                if (!Named(name)) return name;
            }
        }

        bool Named(string name)
        {
            foreach (var look in looks)
                if (look.Name == name) return true;
            return false;
        }

        public static string CleanName(string raw)
        {
            if (raw == null) return null;
            var text = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (char.IsSurrogate(c))
                {
                    if (!char.IsSurrogatePair(raw, i)) continue;
                    text.Append(c).Append(raw[++i]);
                }
                else if (!Unprintable(c)) text.Append(c);
            }
            string name = Shorten(text.ToString().Trim());
            return name.Length == 0 ? null : name;
        }

        static string Shorten(string name)
        {
            if (name.Length <= NameLength) return name;
            int keep = char.IsHighSurrogate(name[NameLength - 1]) ? NameLength - 1 : NameLength;
            return name.Substring(0, keep).TrimEnd();
        }

        static bool Unprintable(char c) => char.IsControl(c) || c == LineSeparator || c == ParagraphSeparator;

        public static bool IsOutfit(string outfitText)
        {
            if (string.IsNullOrEmpty(outfitText) || outfitText.Length > MaxOutfitLength) return false;
            foreach (char c in outfitText)
                if (Unprintable(c) || char.IsSurrogate(c)) return false;
            return Outfit.Deserialize(outfitText).Pieces.Count > 0;
        }

        public SavedLookCheck Check(int index, WardrobeCatalogue wardrobe, Career career, ItemCatalogue items = null) =>
            Check(this[index], wardrobe, career, items);

        public static SavedLookCheck Check(SavedLook look, WardrobeCatalogue wardrobe, Career career, ItemCatalogue items = null)
        {
            if (wardrobe == null) throw new ArgumentNullException(nameof(wardrobe));
            if (career == null) throw new ArgumentNullException(nameof(career));
            if (look == null) return new SavedLookCheck(null, Array.Empty<string>(), Array.Empty<string>(), NoSuchLook);
            var outfit = look.ToOutfit();
            var problems = wardrobe.Problems(outfit);
            if (items != null) AddSkinProblems(problems, outfit, items);
            var missing = career.Missing(outfit);
            return new SavedLookCheck(outfit, problems, missing, Reason(problems, missing, wardrobe));
        }

        static void AddSkinProblems(List<string> problems, Outfit outfit, ItemCatalogue items)
        {
            foreach (var choice in outfit.ItemSkins)
            {
                if (!items.TryGet(choice.Key, out var item)) problems.Add($"there is no item called {choice.Key}");
                else if (!item.HasSkin(choice.Value)) problems.Add($"{choice.Key} has no {choice.Value} style");
            }
        }

        static string Reason(IReadOnlyList<string> problems, IReadOnlyList<string> missing, WardrobeCatalogue wardrobe)
        {
            if (problems.Count > 0) return "This look no longer fits: " + problems[0] + ".";
            return missing.Count > 0 ? MissingReason(missing[0], wardrobe) : null;
        }

        static string MissingReason(string id, WardrobeCatalogue wardrobe)
        {
            int split = id.IndexOf(':');
            string kind = id.Substring(0, split), value = id.Substring(split + 1);
            if (kind == "piece") return $"You don't own {wardrobe.NameOf(value)} yet.";
            if (kind == "colour") return $"You don't own the {wardrobe.Colour("Top", value)?.Name ?? value} colour yet.";
            return $"You don't own the {value} gear style yet.";
        }

        public string Serialize()
        {
            var text = new StringBuilder(VersionText);
            foreach (var look in looks)
                text.Append(EntryBreak).Append(Encode(look.Name)).Append(FieldBreak).Append(Encode(look.OutfitText));
            return text.ToString();
        }

        public static SavedLooks Deserialize(string text)
        {
            TryDeserialize(text, out var looks);
            return looks;
        }

        public static bool TryDeserialize(string text, out SavedLooks looks)
        {
            looks = new SavedLooks();
            if (string.IsNullOrEmpty(text)) return true;
            if (text.Length > MaxStoredLength) return false;
            string[] entries = text.Split(EntryBreak);
            if (entries[0] != VersionText) return false;
            for (int i = 1; i < entries.Length && !looks.IsFull; i++)
            {
                var look = Read(entries[i]);
                if (look != null) looks.looks.Add(look);
            }
            return true;
        }

        static SavedLook Read(string entry)
        {
            string[] fields = entry.Split(FieldBreak);
            if (fields.Length < 2) return null;
            string name = CleanName(Decode(fields[0]));
            string outfitText = Decode(fields[1]);
            return name != null && IsOutfit(outfitText) ? new SavedLook(name, outfitText) : null;
        }

        static string Encode(string value)
        {
            var text = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c == Escape || c == EntryBreak || c == FieldBreak || Unprintable(c))
                    text.Append(Escape).Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                else text.Append(c);
            }
            return text.ToString();
        }

        static string Decode(string value)
        {
            var text = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] != Escape) { text.Append(value[i]); continue; }
                if (i + EscapeDigits >= value.Length) return null;
                if (!int.TryParse(value.Substring(i + 1, EscapeDigits), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out int code))
                    return null;
                text.Append((char)code);
                i += EscapeDigits;
            }
            return text.ToString();
        }
    }
}
