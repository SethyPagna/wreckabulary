namespace Wreckabulary
{
    public sealed class WordboundRoomRequest
    {
        public string Mode { get; }
        public string Format { get; }
        public int Layout { get; }
        public string Invite { get; }
        public bool CustomMap { get; }
        public bool InviteFriends { get; }
        public string PartyInvite { get; }
        public bool Party => InviteFriends || PartyInvite != null;
        public bool Hosting => Invite == null && !CustomMap && !Party;

        WordboundRoomRequest(string mode, string format, int layout, string invite, bool customMap, bool inviteFriends = false, string partyInvite = null)
        {
            Mode = mode; Format = format; Layout = layout; Invite = invite; CustomMap = customMap;
            InviteFriends = inviteFriends; PartyInvite = partyInvite;
        }

        public static WordboundRoomRequest Host(string mode, string format, int layout) => new WordboundRoomRequest(mode, format, layout, null, false);
        public static WordboundRoomRequest Join(string invite) => new WordboundRoomRequest(null, null, 0, (invite ?? "").Trim(), false);
        public static WordboundRoomRequest HostCustomMap() => new WordboundRoomRequest(null, null, 0, null, true);
        public static WordboundRoomRequest StartParty() => new WordboundRoomRequest(null, null, 0, null, false, true);
        public static WordboundRoomRequest JoinParty(string partyInvite) => new WordboundRoomRequest(null, null, 0, null, false, false, (partyInvite ?? "").Trim());
    }

    public static class WordboundRoomEntry
    {
        static WordboundRoomRequest pending;
        static bool returned;

        public static void Request(WordboundRoomRequest request)
        {
            WordboundSoloEntry.CancelRequest();
            WordboundWorkshopEntry.CancelRequest();
            WordboundQuickPlayEntry.CancelRequest();
            pending = request;
            returned = false;
        }

        public static WordboundRoomRequest TakeRequest()
        {
            var result = pending;
            pending = null;
            return result;
        }

        public static void CancelRequest() => pending = null;

        public static void MarkReturned() => returned = true;

        public static void CancelReturn() => returned = false;

        public static bool TakeReturn()
        {
            bool result = returned;
            returned = false;
            return result;
        }
    }
}
