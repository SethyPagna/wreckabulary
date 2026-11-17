using System;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Voice
{
    public sealed class VoiceChannel
    {
        public const string RoomPrefix = "wv1-";
        public const int RoomHashLength = 32;
        public static readonly VoiceChannel None = new("", "");

        public string Key { get; }
        public string Region { get; }
        public bool IsSet => Key.Length != 0;

        public VoiceChannel(string key, string region)
        {
            Region = IsRegion(region) ? region : "";
            Key = Region.Length != 0 && !string.IsNullOrWhiteSpace(key) ? key : "";
        }

        public static bool IsRegion(string region) =>
            region != null && region.Length >= 2 && region.Length <= 8 && region.All(c => c >= 'a' && c <= 'z');

        public static string RoomName(VoiceRoom room, string self)
        {
            if (room == null || !room.Channel.IsSet) return null;
            var me = room.Find(self);
            var heard = room.HeardBy(self);
            if (me == null || heard.Count == 0) return null;
            string scope = room.Scope == VoiceScope.Team ? "team:" + me.Team : "all";
            var ids = heard.Select(m => m.PlayerId).Append(self).OrderBy(id => id, StringComparer.Ordinal);
            string digest = WordboundIdentity.Sha256(room.Channel.Key + "\n" + scope + "\n" + string.Join("\n", ids));
            return RoomPrefix + digest.Substring(0, RoomHashLength);
        }

        public static int Capacity(VoiceRoom room, string self) => room == null ? 0 : room.HeardBy(self).Count + 1;

        public static bool Accepts(VoiceRoom room, string self, string speaker) => room != null && room.CanHear(self, speaker);
    }
}
