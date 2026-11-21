using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules
{
    public static class PlayPageText
    {
        public const string StartHere = "Start here", OpenWord = "Open", StartingIn = "starting in ";
        public const string CodeHint = "Have a code? Paste it here";
        public const string NotACode = "That isn't a party or room code. Ask your friend to copy it again.";
        public const string SoloOnly = "Solo only", PartiesUpTo = "Parties of up to ", AllModes = "All modes", ModesWord = " modes";
        public const string NoModeFits = "No ticked mode fits this party", PartyOf = "Party of ", PartySkips = "your party skips it";
        const string NotOpenOne = " is not open to the builder yet.", NotOpenMany = " are not open to the builder yet.";

        public static IReadOnlyList<HouseModeFamily> Families(bool tutorialDone)
        {
            var learn = HouseModes.FamilyOf(HouseModes.Tutorial);
            if (tutorialDone) return HouseModes.Families;
            return new[] { learn }.Concat(HouseModes.Families.Where(family => family != learn)).ToArray();
        }

        public static int StartCount(string searchTitle)
        {
            string line = searchTitle ?? "";
            int at = line.LastIndexOf(StartingIn, StringComparison.Ordinal);
            if (at < 0) return -1;
            string number = line.Substring(at + StartingIn.Length).Trim();
            return int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out int count) && count > 0 ? count : -1;
        }

        public static IReadOnlyList<PartyPlate> OnlineParty(IReadOnlyList<string> lines)
        {
            var plates = new List<PartyPlate>();
            if (lines == null) return plates;
            foreach (string line in lines)
            {
                if (plates.Count >= PartyPlates.Places || string.IsNullOrWhiteSpace(line)) continue;
                var member = PartyPlates.ParseMember(line);
                if (member.Name.Length == 0) continue;
                var kind = member.You ? PartyPlateKind.You : PartyPlateKind.Friend;
                plates.Add(new PartyPlate(kind, plates.Count, member.Name, PartyPlates.Tag(member.Leader, member.Ready)));
            }
            return plates;
        }

        public static string PartyReason(QuickPlaylist list, int partySize)
        {
            if (list == null || list.Fits(partySize)) return "";
            return list.MaximumParty <= 1 ? SoloOnly : PartiesUpTo + list.MaximumParty.ToString(CultureInfo.InvariantCulture);
        }

        public static string SkipLine(string reason) => string.IsNullOrEmpty(reason) ? "" : reason + MapCardText.Separator + PartySkips;

        public static IReadOnlyList<QuickPlaylist> Playable(QuickPlayChoices choices, int partySize) =>
            choices == null ? Array.Empty<QuickPlaylist>() : choices.Searched(partySize).Where(list => list.Fits(partySize)).ToArray();

        public static string SearchLine(QuickPlayChoices choices, int partySize)
        {
            var playable = Playable(choices, partySize);
            if (playable.Count == 0) return NoModeFits;
            bool sized = playable.Any(list => QuickPlaylists.IsTeamMode(list.Mode));
            string team = QuickPlayCards.TeamLabel(choices.SizeFor(partySize));
            if (playable.Count == 1) return WorkshopModes.Name(playable[0].Mode) + (sized ? " " + team : "");
            string count = playable.Count == QuickPlayCards.Ids.Count ? AllModes : playable.Count.ToString(CultureInfo.InvariantCulture) + ModesWord;
            return sized ? count + MapCardText.Separator + team : count;
        }

        public static string PartyLine(int partySize, string region)
        {
            string line = PartyOf + Math.Max(1, partySize).ToString(CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(region) ? line : line + MapCardText.Separator + region;
        }

        public static string BuilderNote(IReadOnlyList<string> closed)
        {
            var names = (closed ?? Array.Empty<string>()).Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();
            if (names.Length == 0) return "";
            string list = names.Length == 1 ? names[0] : string.Join(", ", names.Take(names.Length - 1)) + " and " + names[names.Length - 1];
            return list + (names.Length == 1 ? NotOpenOne : NotOpenMany);
        }
    }
}
