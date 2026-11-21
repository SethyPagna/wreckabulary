using System;
using System.Collections.Generic;
using System.Globalization;

namespace Wreckabulary.Rules
{
    public enum PartyPlateKind { You, Couch, Friend, Open }

    public sealed class PartyMember
    {
        public string Name { get; }
        public bool You { get; }
        public bool Leader { get; }
        public bool Ready { get; }

        public PartyMember(string name, bool you, bool leader, bool ready)
        {
            Name = name ?? "";
            You = you;
            Leader = leader;
            Ready = ready;
        }
    }

    public sealed class PartyPlate
    {
        public PartyPlateKind Kind { get; }
        public int Place { get; }
        public string Name { get; }
        public string Tag { get; }
        public string Initial { get; }
        public bool Stands => Kind != PartyPlateKind.Open;
        public string Key => (int)Kind + "|" + Place.ToString(CultureInfo.InvariantCulture) + "|" + Name + "|" + Tag;

        public PartyPlate(PartyPlateKind kind, int place, string name, string tag)
        {
            Kind = kind;
            Place = place;
            Name = name ?? "";
            Tag = tag ?? "";
            Initial = kind == PartyPlateKind.Open ? "+" : PartyPlates.Initial(Name);
        }
    }

    public readonly struct PlateBox
    {
        public readonly float Left, Bottom, Right, Top;

        public PlateBox(float left, float bottom, float right, float top)
        {
            Left = left; Bottom = bottom; Right = right; Top = top;
        }

        public float Width => Right - Left;
        public float Height => Top - Bottom;
        public float CentreX => (Left + Right) * .5f;

        public static PlateBox Under(float centreX, float top, float width, float height) =>
            new PlateBox(centreX - width * .5f, top - height, centreX + width * .5f, top);

        public PlateBox At(float left, float bottom) => new PlateBox(left, bottom, left + Width, bottom + Height);

        public PlateBox Grown(float by) => new PlateBox(Left - by, Bottom - by, Right + by, Top + by);

        public bool Overlaps(PlateBox other) =>
            Left < other.Right && other.Left < Right && Bottom < other.Top && other.Bottom < Top;
    }

    public static class PartyPlates
    {
        public const int Places = 4;
        public const string OpenLabel = "Add player", OpenHint = "Add a player on this PC";
        public const string LeaderTag = "LEADER", ReadyTag = "READY";
        public const string YouWord = "You", LeaderWord = "leader", ReadyWord = "ready";
        public const string CouchPrefix = "Player ";
        public const long NotRecorded = -1;
        public const double InkContrast = 4.5;
        public const float OpenGap = .86f;
        const double EvenLuminance = .179;
        const int InkSteps = 16;
        const int SecondsPerMinute = 60, MinutesPerHour = 60;

        public static string CouchName(int place) => CouchPrefix + (place + 1).ToString(CultureInfo.InvariantCulture);

        public static PartyMember ParseMember(string line)
        {
            var parts = (line ?? "").Split(new[] { MapCardText.Separator }, StringSplitOptions.None);
            string name = parts[0].Trim();
            bool leader = false, ready = false;
            for (int i = 1; i < parts.Length; i++)
            {
                string word = parts[i].Trim();
                if (string.Equals(word, LeaderWord, StringComparison.OrdinalIgnoreCase)) leader = true;
                else if (string.Equals(word, ReadyWord, StringComparison.OrdinalIgnoreCase)) ready = true;
            }
            return new PartyMember(name, string.Equals(name, YouWord, StringComparison.OrdinalIgnoreCase), leader, ready);
        }

        public static string Tag(bool leader, bool ready) =>
            leader && ready ? LeaderTag + MapCardText.Separator + ReadyTag : leader ? LeaderTag : ready ? ReadyTag : "";

        public static string Initial(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return "?";
            if (name.StartsWith(CouchPrefix, StringComparison.Ordinal) && name.Length > CouchPrefix.Length)
            {
                string number = name.Substring(CouchPrefix.Length);
                bool digits = true;
                foreach (char c in number) digits &= c >= '0' && c <= '9';
                if (digits) return "P" + number;
            }
            return name.Substring(0, 1).ToUpperInvariant();
        }

        public static IReadOnlyList<PartyPlate> Lineup(string you, int couchGuests, IReadOnlyList<string> friendLines)
        {
            var members = new List<PartyMember>();
            if (friendLines != null)
                foreach (string line in friendLines)
                    if (!string.IsNullOrWhiteSpace(line)) members.Add(ParseMember(line));
            PartyMember self = null;
            foreach (var member in members)
                if (member.You) { self = member; break; }
            string name = string.IsNullOrWhiteSpace(you) ? YouWord : you.Trim();
            var plates = new List<PartyPlate>
            {
                new PartyPlate(PartyPlateKind.You, 0, name, self == null ? "" : Tag(self.Leader, self.Ready)),
            };
            int guests = Math.Min(Math.Max(couchGuests, 0), Places - 1);
            for (int place = 1; place <= guests; place++)
                plates.Add(new PartyPlate(PartyPlateKind.Couch, place, CouchName(place), ""));
            foreach (var member in members)
            {
                if (member.You || member.Name.Length == 0) continue;
                if (plates.Count >= Places) break;
                plates.Add(new PartyPlate(PartyPlateKind.Friend, plates.Count, member.Name, Tag(member.Leader, member.Ready)));
            }
            if (plates.Count < Places) plates.Add(new PartyPlate(PartyPlateKind.Open, plates.Count, OpenLabel, ""));
            return plates;
        }

        public static int Standing(IReadOnlyList<PartyPlate> plates) => Count(plates, null);

        public static int Friends(IReadOnlyList<PartyPlate> plates) => Count(plates, PartyPlateKind.Friend);

        static int Count(IReadOnlyList<PartyPlate> plates, PartyPlateKind? kind)
        {
            int count = 0;
            if (plates != null)
                foreach (var plate in plates)
                    if (plate.Stands && (kind == null || plate.Kind == kind)) count++;
            return count;
        }

        public static string Signature(IReadOnlyList<PartyPlate> plates)
        {
            var keys = new List<string>();
            if (plates != null)
                foreach (var plate in plates) keys.Add(plate.Key);
            return string.Join("\n", keys);
        }

        public static float Offset(int place, int count) => count switch
        {
            2 => place == 0 ? -.48f : .48f,
            3 => place == 0 ? 0f : place == 1 ? -.86f : .86f,
            4 => place == 0 ? -.41f : place == 1 ? -1.23f : place == 2 ? .41f : 1.23f,
            _ => 0f,
        };

        public static float Scale(int count) => count == 4 ? .82f : count == 3 ? .90f : 1f;

        public static float OpenOffset(int count)
        {
            float leftmost = 0f;
            for (int place = 0; place < count; place++) leftmost = Math.Min(leftmost, Offset(place, count));
            return leftmost - OpenGap;
        }

        public static PlateBox Settle(PlateBox plate, PlateBox bounds, PlateBox? blocked, float margin)
        {
            var box = Inside(plate, bounds);
            if (blocked is not { } card || !box.Overlaps(card.Grown(margin))) return box;
            float left = card.Left - margin - box.Width;
            if (left >= bounds.Left) return box.At(left, box.Bottom);
            return Inside(box.At(box.Left, card.Top + margin), bounds);
        }

        static PlateBox Inside(PlateBox box, PlateBox bounds)
        {
            float left = Math.Max(bounds.Left, Math.Min(box.Left, bounds.Right - box.Width));
            float bottom = Math.Max(bounds.Bottom, Math.Min(box.Bottom, bounds.Top - box.Height));
            return box.At(left, bottom);
        }

        public static double Luminance(Srgb colour) =>
            .2126 * Linear(colour.R) + .7152 * Linear(colour.G) + .0722 * Linear(colour.B);

        public static double Contrast(Srgb a, Srgb b)
        {
            double first = Luminance(a), second = Luminance(b);
            return (Math.Max(first, second) + .05) / (Math.Min(first, second) + .05);
        }

        public static Srgb Ink(Srgb colour, Srgb paper)
        {
            if (Contrast(colour, paper) >= InkContrast) return colour;
            float toward = Luminance(paper) >= EvenLuminance ? 0f : 1f;
            float low = 0f, high = 1f;
            for (int step = 0; step < InkSteps; step++)
            {
                float middle = (low + high) * .5f;
                if (Contrast(Mix(colour, toward, middle), paper) >= InkContrast) high = middle;
                else low = middle;
            }
            return Mix(colour, toward, high);
        }

        static Srgb Mix(Srgb colour, float toward, float amount) => new Srgb(
            colour.R + (toward - colour.R) * amount, colour.G + (toward - colour.G) * amount, colour.B + (toward - colour.B) * amount);

        static double Linear(double value) => value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);

        public static string CareerLine(int matches, long secondsPlayed)
        {
            if (matches <= 0) return "";
            string count = matches.ToString(CultureInfo.InvariantCulture) + (matches == 1 ? " match" : " matches");
            string time = PlayTime(secondsPlayed);
            return time.Length == 0 ? count + " played" : count + MapCardText.Separator + time;
        }

        public static string PlayTime(long seconds)
        {
            if (seconds < SecondsPerMinute) return "";
            long minutes = seconds / SecondsPerMinute, hours = minutes / MinutesPerHour;
            minutes %= MinutesPerHour;
            string hourText = hours.ToString(CultureInfo.InvariantCulture) + " h";
            string minuteText = minutes.ToString(CultureInfo.InvariantCulture) + " min";
            return hours == 0 ? minuteText : minutes == 0 ? hourText : hourText + " " + minuteText;
        }
    }
}
