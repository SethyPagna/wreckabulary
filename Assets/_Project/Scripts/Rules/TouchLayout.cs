using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Wreckabulary.Rules
{
    public enum TouchPart { Move, Aim, Attack, Dodge, Jump, Block, Grab, Spell, Drop, Book, Hand1, Hand2, Pause, Eye }

    public readonly struct TouchSpot : IEquatable<TouchSpot>
    {
        public readonly float X, Y, Size, Opacity;

        public TouchSpot(float x, float y, float size, float opacity = 1f)
        {
            X = TouchLayout.Range(x, 0f, 1f, .5f);
            Y = TouchLayout.Range(y, 0f, 1f, .5f);
            Size = TouchLayout.Range(size, TouchLayout.MinSize, TouchLayout.MaxSize, TouchLayout.MinSize);
            Opacity = TouchLayout.Range(opacity, TouchLayout.MinOpacity, 1f, 1f);
        }

        public TouchSpot At(float x, float y) => new TouchSpot(x, y, Size, Opacity);
        public TouchSpot Sized(float size) => new TouchSpot(X, Y, size, Opacity);
        public TouchSpot Faded(float opacity) => new TouchSpot(X, Y, Size, opacity);

        public bool Equals(TouchSpot other) => X == other.X && Y == other.Y && Size == other.Size && Opacity == other.Opacity;
        public override bool Equals(object other) => other is TouchSpot spot && Equals(spot);
        public override int GetHashCode() => HashCode.Combine(X, Y, Size, Opacity);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}) size {2:0.###} opacity {3:0.##}", X, Y, Size, Opacity);
    }

    public sealed class TouchLayout
    {
        public const int Count = 14, Version = 1, MaxCodeLength = 512;
        public const float MinSize = .06f, MaxSize = .3f, MinOpacity = .2f, GridX = 1f / 64f, GridY = 1f / 36f, Touching = .004f;
        public const string Prefix = "wb-touch";
        public const string NoCode = "Copy a touch layout code first.", NotACode = "That isn't a touch layout code.",
            Damaged = "That code is damaged. Copy it again.", Newer = "That code is from a newer version of the game.",
            Older = "That code is from an older version and can't be used.";
        public static readonly IReadOnlyList<TouchPart> Parts = (TouchPart[])Enum.GetValues(typeof(TouchPart));

        static readonly (float ax, float ay, float x, float y, float d)[] ClassicPins =
        {
            (0f, 0f, 135f, 128f, 190f), (1f, 0f, -128f, 142f, 152f), (1f, 0f, -280f, 167f, 112f), (1f, 0f, -250f, 297f, 89f),
            (1f, 0f, -123f, 294f, 89f), (1f, 0f, -384f, 270f, 89f), (1f, 0f, -384f, 161f, 89f), (1f, 0f, -503f, 270f, 89f),
            (1f, 0f, -384f, 51f, 76f), (1f, 0f, -503f, 161f, 76f), (0f, 0f, 290f, 75f, 76f), (0f, 0f, 290f, 180f, 76f),
            (.5f, 1f, 0f, -62f, 70f), (1f, 0f, -384f, 380f, 76f),
        };
        static readonly (float ax, float ay, float x, float y, float d)[] ClawPins =
        {
            (0f, 0f, 150f, 150f, 190f), (1f, 0f, -150f, 160f, 152f), (1f, 1f, -380f, -120f, 112f), (1f, 0f, -300f, 300f, 89f),
            (1f, 1f, -520f, -100f, 89f), (0f, 1f, 760f, -80f, 89f), (1f, 0f, -330f, 130f, 89f), (0f, 1f, 640f, -80f, 89f),
            (1f, 0f, -470f, 80f, 76f), (0f, 0f, 330f, 90f, 76f), (0f, 0f, 330f, 200f, 70f), (0f, 0f, 440f, 150f, 70f),
            (1f, 1f, -640f, -70f, 70f), (1f, 0f, -470f, 240f, 76f),
        };

        readonly TouchSpot[] spots;
        public float Opacity { get; }

        public TouchLayout(IReadOnlyList<TouchSpot> spots, float opacity = 1f)
        {
            if (spots == null || spots.Count != Count) throw new ArgumentException($"A touch layout has {Count} parts.", nameof(spots));
            this.spots = new TouchSpot[Count];
            for (int i = 0; i < Count; i++) this.spots[i] = spots[i];
            Opacity = Range(opacity, MinOpacity, 1f, 1f);
        }

        public TouchSpot this[TouchPart part] => spots[(int)part];

        public TouchLayout With(TouchPart part, TouchSpot spot)
        {
            var copy = (TouchSpot[])spots.Clone();
            copy[(int)part] = spot;
            return new TouchLayout(copy, Opacity);
        }

        public TouchLayout WithOpacity(float opacity) => new TouchLayout(spots, opacity);

        public TouchLayout Clamped(float aspect)
        {
            var copy = new TouchSpot[Count];
            for (int i = 0; i < Count; i++) copy[i] = Clamp(spots[i], aspect);
            return new TouchLayout(copy, Opacity);
        }

        public bool Matches(TouchLayout other)
        {
            if (other == null || other.Opacity != Opacity) return false;
            for (int i = 0; i < Count; i++) if (!spots[i].Equals(other.spots[i])) return false;
            return true;
        }

        public static TouchLayout Classic(float width, float height) => Pinned(ClassicPins, width, height);
        public static TouchLayout Claw(float width, float height) => Pinned(ClawPins, width, height);

        static TouchLayout Pinned((float ax, float ay, float x, float y, float d)[] pins, float width, float height)
        {
            if (!(width >= 1f) || float.IsInfinity(width)) width = 1920f;
            if (!(height >= 1f) || float.IsInfinity(height)) height = 1080f;
            float side = Math.Min(width, height);
            var placed = new TouchSpot[Count];
            for (int i = 0; i < Count; i++)
            {
                var (ax, ay, x, y, d) = pins[i];
                placed[i] = new TouchSpot((ax * width + x) / width, (ay * height + y) / height, d / side);
            }
            return new TouchLayout(placed).Clamped(width / height);
        }

        public static TouchSpot Clamp(TouchSpot spot, float aspect)
        {
            var (across, up) = Units(aspect);
            float rx = spot.Size / (2f * across), ry = spot.Size / (2f * up);
            return spot.At(Between(spot.X, rx, 1f - rx), Between(spot.Y, ry, 1f - ry));
        }

        public static TouchSpot Snap(TouchSpot spot, float aspect) =>
            Clamp(spot.At(Step(spot.X, GridX), Step(spot.Y, GridY)), aspect);

        public static bool Overlap(TouchSpot a, TouchSpot b, float aspect)
        {
            var (across, up) = Units(aspect);
            float dx = (a.X - b.X) * across, dy = (a.Y - b.Y) * up, reach = (a.Size + b.Size) / 2f - Touching;
            return reach > 0f && dx * dx + dy * dy < reach * reach;
        }

        public IReadOnlyList<(TouchPart a, TouchPart b)> Overlaps(float aspect)
        {
            var found = new List<(TouchPart, TouchPart)>();
            for (int i = 0; i < Count; i++)
                for (int j = i + 1; j < Count; j++)
                    if (Overlap(spots[i], spots[j], aspect)) found.Add(((TouchPart)i, (TouchPart)j));
            return found;
        }

        public static string OverlapWarning(IReadOnlyList<(TouchPart a, TouchPart b)> overlaps)
        {
            if (overlaps == null || overlaps.Count == 0) return "";
            string first = Label(overlaps[0].a) + " overlaps " + Label(overlaps[0].b);
            return overlaps.Count == 1 ? first + "." : $"{first} (+{overlaps.Count - 1} more).";
        }

        public static float Pinch(float size, float from, float to) =>
            from > 0f && to > 0f && IsFinite(from) && IsFinite(to) ? Range(size * to / from, MinSize, MaxSize, size) : size;

        public static string Label(TouchPart part) => part switch
        {
            TouchPart.Move => "MOVE",
            TouchPart.Aim => "AIM",
            TouchPart.Attack => "SMASH",
            TouchPart.Dodge => "DODGE",
            TouchPart.Jump => "JUMP",
            TouchPart.Block => "BLOCK",
            TouchPart.Grab => "INTERACT",
            TouchPart.Spell => "SPELL",
            TouchPart.Drop => "HOLD DROP",
            TouchPart.Book => "BOOK",
            TouchPart.Hand1 => "HAND 1",
            TouchPart.Hand2 => "HAND 2",
            TouchPart.Pause => "PAUSE",
            TouchPart.Eye => "EYE",
            _ => "",
        };

        public string Encode()
        {
            var bytes = new byte[2 + Count * 6];
            bytes[0] = Count;
            bytes[1] = Quantise(Opacity, MinOpacity, 1f);
            for (int i = 0; i < Count; i++)
            {
                var spot = spots[i];
                int at = 2 + i * 6, x = (int)Math.Round(spot.X * ushort.MaxValue), y = (int)Math.Round(spot.Y * ushort.MaxValue);
                bytes[at] = (byte)(x >> 8); bytes[at + 1] = (byte)x;
                bytes[at + 2] = (byte)(y >> 8); bytes[at + 3] = (byte)y;
                bytes[at + 4] = Quantise(spot.Size, MinSize, MaxSize);
                bytes[at + 5] = Quantise(spot.Opacity, MinOpacity, 1f);
            }
            string body = Prefix + ":" + Version.ToString(CultureInfo.InvariantCulture) + ":" + Base64Url(bytes);
            return body + ":" + Checksum(body);
        }

        public static bool TryDecode(string text, out TouchLayout layout, out string message)
        {
            layout = null;
            message = Read(Squeeze(text), out layout);
            return layout != null;
        }

        static string Read(string code, out TouchLayout layout)
        {
            layout = null;
            if (code.Length == 0) return NoCode;
            if (code.Length > MaxCodeLength) return NotACode;
            var parts = code.Split(':');
            if (parts.Length != 4 || !string.Equals(parts[0], Prefix, StringComparison.OrdinalIgnoreCase) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int version)) return NotACode;
            if (version > Version) return Newer;
            if (version < Version) return Older;
            string body = Prefix + ":" + parts[1] + ":" + parts[2];
            if (!string.Equals(parts[3], Checksum(body), StringComparison.OrdinalIgnoreCase)) return Damaged;
            var bytes = FromBase64Url(parts[2]);
            if (bytes == null || bytes.Length != 2 + Count * 6 || bytes[0] != Count) return Damaged;
            var read = new TouchSpot[Count];
            for (int i = 0; i < Count; i++)
            {
                int at = 2 + i * 6;
                read[i] = new TouchSpot((bytes[at] << 8 | bytes[at + 1]) / (float)ushort.MaxValue, (bytes[at + 2] << 8 | bytes[at + 3]) / (float)ushort.MaxValue,
                    Unquantise(bytes[at + 4], MinSize, MaxSize), Unquantise(bytes[at + 5], MinOpacity, 1f));
            }
            layout = new TouchLayout(read, Unquantise(bytes[1], MinOpacity, 1f));
            return null;
        }

        static string Squeeze(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var kept = new StringBuilder(Math.Min(text.Length, MaxCodeLength + 1));
            foreach (char c in text)
            {
                if (char.IsWhiteSpace(c)) continue;
                kept.Append(c);
                if (kept.Length > MaxCodeLength) break;
            }
            return kept.ToString();
        }

        static string Checksum(string text)
        {
            int crc = 0xFFFF;
            foreach (char c in text)
            {
                crc ^= (c & 0xFF) << 8;
                for (int bit = 0; bit < 8; bit++) crc = ((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1) & 0xFFFF;
            }
            return crc.ToString("x4", CultureInfo.InvariantCulture);
        }

        static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        static byte[] FromBase64Url(string text)
        {
            if (text.Length == 0 || text.Length % 4 == 1) return null;
            foreach (char c in text)
                if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_')) return null;
            string padded = text.Replace('-', '+').Replace('_', '/') + new string('=', (4 - text.Length % 4) % 4);
            try { return Convert.FromBase64String(padded); }
            catch (FormatException) { return null; }
        }

        static byte Quantise(float value, float min, float max) => (byte)Math.Round(Range((value - min) / (max - min), 0f, 1f, 0f) * 255f);
        static float Unquantise(byte value, float min, float max) => min + value / 255f * (max - min);

        static (float across, float up) Units(float aspect)
        {
            aspect = Range(aspect, .01f, 100f, 16f / 9f);
            return aspect >= 1f ? (aspect, 1f) : (1f, 1f / aspect);
        }

        static float Between(float value, float low, float high) => low > high ? .5f : value < low ? low : value > high ? high : value;
        static float Step(float value, float step) => (float)Math.Round(value / step) * step;
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static float Range(float value, float min, float max, float fallback) =>
            !IsFinite(value) ? fallback : value < min ? min : value > max ? max : value;
    }
}
