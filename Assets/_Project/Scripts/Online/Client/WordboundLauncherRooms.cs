namespace Wreckabulary.Online
{
    public sealed partial class WordboundLauncher
    {
        bool friendsWorkshop;

        void OpenRoom(WordboundRoomRequest room)
        {
            if (room.InviteFriends) { menu.InviteFriends(); return; }
            if (room.PartyInvite != null) { menu.JoinFriendsParty(room.PartyInvite); return; }
            menu.ReturnToFriends();
            if (room.CustomMap)
            {
                friendsWorkshop = true;
                OpenWorkshop();
                return;
            }
            if (room.Invite != null) menu.JoinFriendRoom(room.Invite);
            else menu.HostRoom(room.Mode, room.Format, room.Layout);
        }

        void BackToWorkshop()
        {
            if (destroying || !workshop) return;
            if (menu) menu.Visible = false;
            workshop.Visible = true;
        }
    }
}
