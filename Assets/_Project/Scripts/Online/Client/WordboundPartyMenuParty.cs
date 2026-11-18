using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundPartyMenu
    {
        public const string BadPartyCode = "That party code is incomplete. Ask your friend to copy it again.";
        public const string WaitingForLeader = "Waiting for the leader to find a match";
        bool friendsParty, friendsPartyBusy, friendsPartyLeaving, friendsPartyCopied, friendsPartyCodeShown, invitePartySettings;
        float friendsPartySearchSince = -1;
        string friendsPartyReadyRevision;

        public bool InFriendsParty => friendsParty;

        bool FriendsPartyShown => friendsParty && !lobbySearch && coordinator != null && !coordinator.MatchActive;

        bool FriendsPartySearching => coordinator?.Party?.HasAttempt == true && coordinator.Party.Channel == "QuickPlay";

        public void InviteFriends()
        {
            if (!OpenFriendsParty()) return;
            _ = RunFriendsParty(async () =>
            {
                var choices = QuickChoices;
                if (choices == null) { localError = "Online play has no public maps in this build."; return; }
                if (!await RoomStep(ConnectForRoom)) return;
                var maps = publicCourses.Choices("Freestyle", "Solo");
                if (maps.Count == 0) { localError = "No public map is open for a party yet."; return; }
                long slot = QuickPlayPicker.Slot(NowMilliseconds());
                var map = maps[(int)(((slot % maps.Count) + maps.Count) % maps.Count)];
                UsePick(new QuickPlayPick("Freestyle", "Solo", map.Seed, AutoRegion()));
                invitePartySettings = true;
                if (!await RoomStep(CreateQuickParty)) return;
                await RoomStep(() => coordinator.SetReady(true));
            });
        }

        public void JoinFriendsParty(string text)
        {
            if (!OpenFriendsParty()) return;
            try { _ = new WordboundPartyInvite(text); }
            catch (Exception) { localError = BadPartyCode; return; }
            _ = RunFriendsParty(async () =>
            {
                if (!await RoomStep(ConnectForRoom)) return;
                await RoomStep(() => coordinator.JoinParty(text.Trim()));
            });
        }

        bool OpenFriendsParty()
        {
            if (coordinator == null || friendsParty || roomFlow || lobbySearch || launching || returning || coordinator.MatchActive) return false;
            friendsParty = true; friendsPartyCopied = false; friendsPartyCodeShown = false; friendsPartyReadyRevision = null; friendsPartySearchSince = -1;
            returnToFriends = false; localError = null; matchFoundAt = -1;
            partyFocus.Reset("party-leave"); revealPartySelection = true;
            return true;
        }

        async Task RunFriendsParty(Func<Task> work)
        {
            friendsPartyBusy = true;
            try { await work(); }
            catch (Exception error) { if (!lifetime.IsCancellationRequested) localError = WordboundPartyCoordinator.Friendly(error); }
            finally { friendsPartyBusy = false; }
        }

        void TickFriendsParty()
        {
            if (!friendsParty || coordinator == null) return;
            if (FriendsPartySearching) { if (friendsPartySearchSince < 0) friendsPartySearchSince = searchClock(); }
            else friendsPartySearchSince = -1;
            if (friendsPartyBusy || friendsPartyLeaving || lobbySearch || quickPlayRunning || coordinator.Busy || launching || returning ||
                coordinator.RetryRequired || coordinator.Content == null) return;
            var party = coordinator.Party;
            if (party == null || party.HasAttempt) return;
            var self = party.Members.FirstOrDefault(member => member.PlayerId == coordinator.PlayerId);
            if (self == null || self.Ready || friendsPartyReadyRevision == party.Revision) return;
            friendsPartyReadyRevision = party.Revision;
            _ = RunFriendsParty(() => RoomStep(() => coordinator.SetReady(true)));
        }

        string FriendsPartyStatus()
        {
            var party = coordinator.Party;
            if (friendsPartyLeaving) return "Leaving the party…";
            if (party == null) return coordinator.Authenticated ? "Making your party…" : "Connecting…";
            if (launching) return "Joining match…";
            string state = LobbyAssignmentState();
            if (state == "ADMITTING" || state == "STARTED") return StartLine("Match found");
            if (FriendsPartySearching)
                return "Searching " + QuickPlayFlow.Elapsed(friendsPartySearchSince < 0 ? 0 : searchClock() - friendsPartySearchSince);
            if (!coordinator.IsLeader) return WaitingForLeader;
            return QuickChoices?.PartyLine(party.Members.Count) ?? "";
        }

        string FriendsPartyProblem()
        {
            if (friendsPartyLeaving) return null;
            if (coordinator.RetryRequired) return coordinator.Error ?? "The last request was not confirmed.";
            if (!string.IsNullOrEmpty(localError)) return localError;
            if (!string.IsNullOrEmpty(coordinator.Error)) return coordinator.Error;
            return null;
        }

        bool CanFindPartyMatch => friendsParty && coordinator.Party != null && coordinator.IsLeader && !coordinator.Party.HasAttempt &&
            !coordinator.Busy && !coordinator.RetryRequired && !friendsPartyBusy && !friendsPartyLeaving && !launching && !returning;

        bool CanLeaveFriendsParty => !friendsPartyLeaving && !launching && !returning && !coordinator.RetryRequired &&
            LobbyAssignmentState() != "STARTED";

        void CopyPartyCode()
        {
            try
            {
                GUIUtility.systemCopyBuffer = coordinator.CopyInvite();
                friendsPartyCopied = true;
            }
            catch (Exception error) { localError = WordboundPartyCoordinator.Friendly(error); }
        }

        void DrawFriendsParty(Vector2 viewport)
        {
            var party = coordinator.Party;
            if (party != null && coordinator.Content != null && !friendsPartyCodeShown && Event.current.type == EventType.Layout)
            {
                friendsPartyCodeShown = true;
                partyFocus.Reset("party-copy");
            }
            float width = Mathf.Min(viewport.x - 32, 820), height = Mathf.Min(viewport.y - 32, ui.RowHeight * 9.4f);
            GUILayout.BeginArea(new Rect((viewport.x - width) * .5f, (viewport.y - height) * .5f, width, height), ui.Panel);
            GUILayout.Label(party == null ? "Your party" : "Party" + MapCardText.Separator + party.Members.Count + " of " + QuickPlaylists.MaximumParty, ui.Heading);
            GUILayout.Label(FriendsPartyStatus(), ui.Wrapped);
            string problem = FriendsPartyProblem();
            if (problem != null) GUILayout.Label(problem, ui.Wrapped);
            GUILayout.Space(ui.Gap);
            if (party != null) DrawMembers(party);
            GUILayout.FlexibleSpace();
            bool enabled = GUI.enabled;
            if (party != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Party code  " + FriendRooms.ShortCode(party.Invite), ui.Heading);
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical(GUILayout.Width(240));
                GUI.enabled = enabled && coordinator.Content != null;
                PartyControl("party-copy", friendsPartyCopied ? "Copied ✓" : "Copy code", CopyPartyCode);
                GUI.enabled = enabled;
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(ui.Gap);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical(GUILayout.Width(240));
            if (coordinator.RetryRequired)
            {
                GUI.enabled = enabled && !coordinator.Busy;
                PartyControl("party-retry", "Retry", () => Invoke(() => coordinator.Retry()), height: ui.RowHeight * 1.4f);
            }
            else
            {
                GUI.enabled = enabled && CanLeaveFriendsParty;
                PartyControl("party-leave", "Leave", () => { _ = LeaveFriendsParty(); }, height: ui.RowHeight * 1.4f);
            }
            GUI.enabled = enabled;
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (party != null && coordinator.IsLeader)
            {
                GUILayout.BeginVertical(GUILayout.Width(240));
                GUI.enabled = enabled && CanFindPartyMatch;
                PartyControl("party-find", "Find match", StartMatchmaking, height: ui.RowHeight * 1.4f);
                GUI.enabled = enabled;
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        async Task LeaveFriendsParty()
        {
            if (friendsPartyLeaving || coordinator == null) return;
            friendsPartyLeaving = true;
            StopQuickPlay();
            localError = null;
            try
            {
                float giveUp = Time.unscaledTime + LeaveWaitSeconds;
                while ((coordinator.Busy || friendsPartyBusy || quickPlayRunning) && Time.unscaledTime < giveUp)
                {
                    if (lifetime.IsCancellationRequested) return;
                    await Task.Delay(100);
                }
                if (coordinator.Busy || friendsPartyBusy || quickPlayRunning) { localError = "The party is still busy. Try again."; return; }
                if (coordinator.Party != null)
                {
                    await coordinator.LeaveParty();
                    if (!string.IsNullOrEmpty(coordinator.Error)) return;
                }
                if (lifetime.IsCancellationRequested || coordinator.RetryRequired || coordinator.Party != null) return;
                friendsParty = false;
                lobbySearch = false;
                RequestClose();
            }
            catch (Exception error)
            {
                if (!lifetime.IsCancellationRequested) localError = WordboundPartyCoordinator.Friendly(error);
            }
            finally { friendsPartyLeaving = false; }
        }
    }
}
