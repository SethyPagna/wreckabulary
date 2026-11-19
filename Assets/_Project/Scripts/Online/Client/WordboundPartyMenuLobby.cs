using System;
using System.Collections.Generic;
using System.Linq;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundPartyMenu
    {
        public const string SearchEnded = "The search ended. Press Find match again.";
        internal bool HeldByLobby;

        internal bool LobbyWorking => friendsPartyBusy || friendsPartyLeaving || lobbyLeaving || quickPlayRunning ||
            coordinator != null && coordinator.Busy;

        internal bool LobbyInMatch => coordinator != null && (coordinator.MatchActive || currentLaunch != null || launching || returning);

        internal bool LobbyFound
        {
            get
            {
                if (coordinator == null) return false;
                string state = LobbyAssignmentState();
                return state == "ADMITTING" || state == "STARTED" || LobbyInMatch;
            }
        }

        internal bool LobbySearching => lobbySearch || FriendsPartySearching;

        internal bool LobbyMatchDue
        {
            get
            {
                if (!friendsParty || coordinator == null || LobbyInMatch) return false;
                string state = LobbyAssignmentState();
                return (state == "ADMITTING" || state == "STARTED") && StartCountdown() == 0;
            }
        }

        internal bool LobbyIdle => coordinator != null && !LobbyInMatch && !LobbyFound && !LobbyWorking && !coordinator.RetryRequired;

        internal string LobbyHeading => coordinator?.Party is { } party
            ? "Party" + MapCardText.Separator + party.Members.Count + " of " + QuickPlaylists.MaximumParty
            : "Your party";

        internal IReadOnlyList<string> LobbyMembers
        {
            get
            {
                var party = coordinator?.Party;
                return party == null ? Array.Empty<string>() : party.Members.Select(member => MemberLine(party, member)).ToArray();
            }
        }

        internal string LobbyStatus => coordinator == null ? "" : FriendsPartyStatus();

        internal string LobbyProblem => coordinator == null ? null : FriendsPartyProblem();

        internal string LobbySearchLine => coordinator == null ? "" : lobbySearch ? LobbySearchTitle() : FriendsPartyStatus();

        internal bool LobbyCopied => friendsPartyCopied;

        internal bool LobbyCanCopy => coordinator?.Party != null && coordinator.Content != null && !friendsPartyLeaving;

        internal bool LobbyCanLeave => friendsParty && coordinator != null && CanLeaveFriendsParty;

        internal bool LobbyCanFind => friendsParty && coordinator != null && QuickChoices != null && !lobbySearch && CanFindPartyMatch;

        internal bool LobbyCanCancel => lobbySearch && coordinator != null && coordinator.IsLeader && CanLeaveLobbySearch;

        internal bool LobbyCanRetry => coordinator != null && coordinator.RetryRequired && !coordinator.Busy;

        internal bool LobbySearchStopped => lobbySearch && coordinator != null && !quickPlayArmed && !quickPlayRunning && !lobbyLeaving &&
            coordinator.Party?.HasAttempt != true && !LobbyFound && !coordinator.Busy && !coordinator.RetryRequired;

        internal void LobbyCopy() { if (LobbyCanCopy) CopyPartyCode(); }

        internal void LobbyLeave() => _ = LeaveFriendsParty();

        internal void LobbyFind() { if (LobbyCanFind) StartMatchmaking(); }

        internal void LobbyCancel() { if (LobbyCanCancel) _ = LeaveLobbySearch(); }

        internal void LobbyRetry() { if (LobbyCanRetry) Invoke(() => coordinator.Retry()); }

        internal void EndStoppedLobbySearch()
        {
            if (!LobbySearchStopped) return;
            string reason = !string.IsNullOrEmpty(localError) ? localError :
                !string.IsNullOrEmpty(coordinator.Error) ? coordinator.Error : SearchEnded;
            lobbySearch = false;
            localError = reason;
        }
    }
}
