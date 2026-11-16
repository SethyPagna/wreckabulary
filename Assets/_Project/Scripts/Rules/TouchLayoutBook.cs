using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Wreckabulary.Rules
{
    public enum TouchTemplate { Classic, Claw, Custom1, Custom2, Custom3 }

    public sealed class TouchLayoutBook
    {
        public const string Header = "wb-touch-book 1", FileName = "layouts.txt", Backup = ".bak";
        public const int Slots = 3, NameLength = 16, MaximumBytes = 8192;
        public const string Restored = "Your touch layouts were restored from a backup.";
        public static readonly IReadOnlyList<TouchTemplate> Templates = (TouchTemplate[])Enum.GetValues(typeof(TouchTemplate));
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        readonly TouchLayout[] customs = new TouchLayout[Slots];
        readonly string[] names = new string[Slots];
        TouchTemplate active;

        public TouchTemplate Active
        {
            get => active;
            set => active = Has(value) ? value : TouchTemplate.Classic;
        }

        public static bool IsCustom(TouchTemplate template) => template >= TouchTemplate.Custom1 && template <= TouchTemplate.Custom3;
        public static TouchTemplate Custom(int slot) => TouchTemplate.Custom1 + slot;
        static int Slot(TouchTemplate template) => (int)template - (int)TouchTemplate.Custom1;

        public static string DefaultName(TouchTemplate template) => template switch
        {
            TouchTemplate.Classic => "Classic",
            TouchTemplate.Claw => "Claw",
            _ => "Custom " + (Slot(template) + 1).ToString(CultureInfo.InvariantCulture),
        };

        public string Name(TouchTemplate template) => IsCustom(template) && names[Slot(template)] != null ? names[Slot(template)] : DefaultName(template);

        public bool Has(TouchTemplate template) =>
            template == TouchTemplate.Classic || template == TouchTemplate.Claw || IsCustom(template) && customs[Slot(template)] != null;

        public TouchLayout Stored(TouchTemplate template) => IsCustom(template) ? customs[Slot(template)] : null;

        public void Store(TouchTemplate template, TouchLayout layout, string name = null)
        {
            if (!IsCustom(template)) throw new ArgumentException("Classic and Claw can't be changed.", nameof(template));
            customs[Slot(template)] = layout ?? throw new ArgumentNullException(nameof(layout));
            if (name != null) Rename(template, name);
        }

        public void Rename(TouchTemplate template, string name)
        {
            if (!IsCustom(template)) throw new ArgumentException("Classic and Claw can't be renamed.", nameof(template));
            string clean = CleanName(name);
            names[Slot(template)] = clean.Length == 0 || clean == DefaultName(template) ? null : clean;
        }

        public TouchTemplate SaveTarget(TouchTemplate from)
        {
            if (IsCustom(from)) return from;
            for (int slot = 0; slot < Slots; slot++) if (customs[slot] == null) return Custom(slot);
            return TouchTemplate.Custom1;
        }

        public TouchLayout Resolve(TouchTemplate template, float width, float height)
        {
            if (template == TouchTemplate.Claw) return TouchLayout.Claw(width, height);
            var stored = Stored(template);
            return stored != null && width > 0f && height > 0f ? stored.Clamped(width / height) : TouchLayout.Classic(width, height);
        }

        public TouchLayout Current(float width, float height) => Resolve(Active, width, height);

        public TouchLayoutBook Clone()
        {
            var copy = new TouchLayoutBook();
            Array.Copy(customs, copy.customs, Slots);
            Array.Copy(names, copy.names, Slots);
            copy.active = active;
            return copy;
        }

        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var clean = new StringBuilder(NameLength);
            bool space = false;
            foreach (char c in name)
            {
                if (char.IsWhiteSpace(c)) { space = clean.Length > 0; continue; }
                if (char.IsControl(c) || char.IsSurrogate(c)) continue;
                if (space)
                {
                    if (clean.Length >= NameLength - 1) break;
                    clean.Append(' ');
                    space = false;
                }
                if (clean.Length >= NameLength) break;
                clean.Append(c);
            }
            return clean.ToString();
        }

        public string ToText()
        {
            var text = new StringBuilder(Header).Append('\n');
            text.Append("active ").Append(active.ToString()).Append('\n');
            for (int slot = 0; slot < Slots; slot++)
            {
                if (customs[slot] == null) continue;
                text.Append("slot ").Append((slot + 1).ToString(CultureInfo.InvariantCulture)).Append(' ').Append(customs[slot].Encode());
                if (names[slot] != null) text.Append(' ').Append(names[slot]);
                text.Append('\n');
            }
            return text.ToString();
        }

        public static TouchLayoutBook Parse(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var lines = text.Replace("\r", "").Split('\n');
            if (lines[0].Trim() != Header) return null;
            var book = new TouchLayoutBook();
            string wanted = null;
            for (int i = 1; i < lines.Length; i++)
            {
                var words = lines[i].Trim().Split(new[] { ' ' }, 4, StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 2 && words[0] == "active") wanted = words[1];
                else if (words.Length >= 3 && words[0] == "slot" && int.TryParse(words[1], NumberStyles.None, CultureInfo.InvariantCulture, out int number)
                    && number >= 1 && number <= Slots && TouchLayout.TryDecode(words[2], out var layout, out _))
                    book.Store(Custom(number - 1), layout, words.Length == 4 ? words[3] : null);
            }
            if (wanted != null && Enum.TryParse(wanted, false, out TouchTemplate template) && Enum.IsDefined(typeof(TouchTemplate), template))
                book.Active = template;
            return book;
        }

        public static TouchLayoutBook Load(string directory, out string problem)
        {
            problem = null;
            string path = Path.Combine(directory, FileName);
            foreach (string file in new[] { path, path + Backup })
            {
                var book = Read(file, out string error);
                if (book != null)
                {
                    if (file != path) problem = Restored;
                    return book;
                }
                problem ??= error;
            }
            return new TouchLayoutBook();
        }

        static TouchLayoutBook Read(string file, out string error)
        {
            error = null;
            try
            {
                if (!File.Exists(file)) return null;
                using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (input.Length > MaximumBytes) { error = "Saved touch layouts are too large to read."; return null; }
                using var reader = new StreamReader(input, Utf8, false);
                var book = Parse(reader.ReadToEnd());
                if (book == null) error = "Saved touch layouts could not be read.";
                return book;
            }
            catch (IOException e) { error = "Could not load touch layouts: " + e.Message; return null; }
            catch (UnauthorizedAccessException e) { error = "Could not load touch layouts: " + e.Message; return null; }
        }

        public bool TrySave(string directory, out string error)
        {
            error = null;
            string temporary = null;
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, FileName);
                byte[] bytes = Utf8.GetBytes(ToText());
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(bytes, 0, bytes.Length);
                    output.Flush(true);
                }
                if (!File.Exists(path)) File.Move(temporary, path);
                else File.Replace(temporary, path, Read(path, out _) != null ? path + Backup : null);
                temporary = null;
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            {
                error = "Could not save touch layouts: " + e.Message;
                return false;
            }
            finally
            {
                if (temporary != null)
                {
                    try { File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }
    }
}
