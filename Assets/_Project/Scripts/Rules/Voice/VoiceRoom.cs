using System;
using System.Collections.Generic;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Voice
{
    public enum VoiceScope { Nobody, Team, Everyone }

    public sealed class VoiceMember
    {
        public string PlayerId { get; }
        public int Team { get; }
        public int Slot { get; }

        public VoiceMember(string playerId, int team = 0, int slot = 0)
        {
            PlayerId = playerId;
            Team = team;
            Slot = slot;
        }
    }

    public sealed class VoiceRoom
    {
        public static readonly VoiceRoom Empty = new(VoiceScope.Nobody, Array.Empty<VoiceMember>());

        readonly Dictionary<string, VoiceMember> byId;

        public VoiceScope Scope { get; }
        public IReadOnlyList<VoiceMember> Members { get; }
        public VoiceChannel Channel { get; private set; } = VoiceChannel.None;

        VoiceRoom(VoiceScope scope, IEnumerable<VoiceMember> members)
        {
            byId = new Dictionary<string, VoiceMember>(StringComparer.Ordinal);
            foreach (var member in members ?? Array.Empty<VoiceMember>())
                if (member != null && WordboundIdentity.IsId(member.PlayerId) && !byId.ContainsKey(member.PlayerId))
                    byId.Add(member.PlayerId, member);
            Members = byId.Values.OrderBy(m => m.Team).ThenBy(m => m.Slot).ThenBy(m => m.PlayerId, StringComparer.Ordinal).ToArray();
            Scope = Members.Count == 0 ? VoiceScope.Nobody : scope;
        }

        public static VoiceRoom Party(IEnumerable<string> playerIds) =>
            new(VoiceScope.Everyone, (playerIds ?? Array.Empty<string>()).Select((id, slot) => new VoiceMember(id, 0, slot)));

        public static VoiceRoom Match(ModeDefinition mode, IEnumerable<WordboundSeat> seats)
        {
            if (mode == null) throw new ArgumentNullException(nameof(mode));
            return new(ScopeFor(mode), (seats ?? Array.Empty<WordboundSeat>()).Where(s => s != null)
                .Select(s => new VoiceMember(s.PlayerId, s.Team, s.Slot)));
        }

        public static VoiceRoom House(HouseMode mode, IEnumerable<VoiceMember> members)
        {
            if (mode == null) throw new ArgumentNullException(nameof(mode));
            return new(mode.Teams ? VoiceScope.Team : VoiceScope.Everyone, members);
        }

        public VoiceRoom On(VoiceChannel channel) => new(Scope, Members) { Channel = channel ?? VoiceChannel.None };

        public static VoiceScope ScopeFor(ModeDefinition mode) =>
            mode.TeamCount > 1 && mode.TeamCapacity > 1 ? VoiceScope.Team : VoiceScope.Everyone;

        public VoiceMember Find(string playerId) =>
            playerId != null && byId.TryGetValue(playerId, out var member) ? member : null;

        public bool CanHear(string listener, string speaker)
        {
            if (Scope == VoiceScope.Nobody || listener == null || speaker == null || listener == speaker) return false;
            var from = Find(speaker);
            var to = Find(listener);
            if (from == null || to == null) return false;
            return Scope == VoiceScope.Everyone || from.Team == to.Team;
        }

        public IReadOnlyList<VoiceMember> HeardBy(string listener) =>
            Members.Where(member => CanHear(listener, member.PlayerId)).ToArray();
    }
}
