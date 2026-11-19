namespace Wreckabulary.Online
{
    public sealed partial class WordboundLauncher
    {
        const string LobbyUnavailable = "The main lobby is unavailable in this build.";
        WordboundPartyHost lobbyParty;
        bool lobbyPartyReturning;

        public bool HoldsLobbyParty => lobbyParty && !lobbyParty.Closing;

        static WordboundPartyHost LobbyPartyFor(string soloMode, bool workshopEntry, bool quickPlay, WordboundRoomRequest room)
        {
            var hub = WordboundPartyHost.Current;
            if (!hub || hub.Closing) return null;
            if (hub.TakeEntering() && soloMode == null && !workshopEntry && !quickPlay && room == null) return hub;
            hub.LeaveAndClose(WordboundPartyHost.LeftForOtherPlay);
            return null;
        }

        void AdoptLobbyParty(WordboundPartyHost hub)
        {
            menu = hub.Adopt(this);
            lobbyParty = hub;
            menu.BeginMatchRequested = BeginMatch;
            menu.ReturnRequested = ReturnMatch;
            menu.LeaveRequested = LeaveMatch;
            menu.LeaveAvailable = () => activeClient && activeClient.CanLeaveLocally;
            menu.CloseRequested += BackToLobby;
        }

        void TickLobbyParty()
        {
            if (!lobbyParty || destroying || lobbyPartyReturning || activeLaunch != null || activeClient || localClient) return;
            if (lobbyParty.Closing || !lobbyParty.ReadyForLobby) return;
            lobbyPartyReturning = true;
            WordboundQuickPlayEntry.MarkReturned();
            if (Session.GoHome()) return;
            WordboundQuickPlayEntry.CancelReturn();
            failure = LobbyUnavailable;
        }

        void ReleaseLobbyParty()
        {
            var hub = lobbyParty;
            lobbyParty = null;
            if (!hub) return;
            if (menu) menu.CloseRequested -= BackToLobby;
            hub.Release(this);
            menu = null;
        }
    }
}
