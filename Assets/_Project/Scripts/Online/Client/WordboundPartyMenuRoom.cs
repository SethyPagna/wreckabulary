using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundPartyMenu
    {
        public const string BadRoomCode = "That room code is incomplete. Ask your friend to copy it again.";
        const string NoRoomMap = "No map is open for this mode yet. Go back and pick another mode.";
        const string RoomClosed = "The room closed. Go back and make or join a new one.";
        bool roomFlow, roomHost, roomBusy, roomLeaving, roomCopied, roomCodeShown, returnToFriends;
        Action roomBack;

        public bool InRoom => roomFlow;

        bool RoomShown => roomFlow && coordinator != null && !coordinator.MatchActive;

        public void ReturnToFriends() => returnToFriends = true;

        public bool CanHostAuthoredRoom => coordinator != null && coordinator.Party == null && !coordinator.Busy &&
            !coordinator.RetryRequired && !launching && !returning && !roomFlow && !lobbySearch;

        public void HostRoom(string selectedMode, string selectedFormat, int layout)
        {
            if (!OpenRoom(true)) return;
            _ = RunRoom(async () =>
            {
                var choices = publicCourses.Choices(selectedMode, selectedFormat);
                if (choices.Count == 0) { localError = NoRoomMap; return; }
                var picked = choices[Math.Max(0, Math.Min(layout, choices.Count - 1))];
                mode = Array.IndexOf(Modes, selectedMode); format = Array.IndexOf(Formats, selectedFormat);
                region = Array.IndexOf(Regions, AutoRegion()); fill = FriendRooms.Fill(selectedMode);
                seed = picked.Seed; customSeedMode = false;
                if (!await RoomStep(ConnectForRoom)) return;
                if (!await RoomStep(CreateParty)) return;
                await EnterRoom("PrivateCreate", null);
            });
        }

        public void JoinFriendRoom(string text)
        {
            if (!OpenRoom(false)) return;
            WordboundRoomInvite invite;
            try { invite = new WordboundRoomInvite(text); }
            catch (Exception) { localError = BadRoomCode; return; }
            _ = RunRoom(async () =>
            {
                if (invite.PoolVersion != coordinator.PoolVersion)
                { localError = "The room uses a different map release. Update the game, then join again."; return; }
                mode = Array.IndexOf(Modes, invite.Mode); format = Array.IndexOf(Formats, invite.Format);
                region = Array.IndexOf(Regions, invite.Region); fill = FriendRooms.Fill(invite.Mode);
                seed = privateSeed = invite.Seed;
                customSeedMode = publicCourses.Find(invite.Mode, invite.Format, invite.Seed) == null;
                if (!await RoomStep(ConnectForRoom)) return;
                if (!await RoomStep(CreateParty)) return;
                await EnterRoom("PrivateJoin", invite.Code);
            });
        }

        public async Task HostAuthoredRoom(WordboundClientContent content, Action back)
        {
            if (!CanHostAuthoredRoom) throw new InvalidOperationException("Finish the pending online action first.");
            if (!coordinator.Authenticated)
            {
                await ConnectForRoom();
                if (!string.IsNullOrEmpty(coordinator.Error)) throw new InvalidOperationException(coordinator.Error);
            }
            await OpenAuthoredPrivate(content, AutoRegion(), FriendRooms.Fill(content.Mode.Id));
            if (!OpenRoom(true)) return;
            roomBack = back;
            _ = RunRoom(() => EnterRoom("PrivateCreate", null));
        }

        bool OpenRoom(bool host)
        {
            if (coordinator == null || roomFlow || lobbySearch || launching || returning || coordinator.MatchActive) return false;
            roomFlow = true; roomHost = host; roomCopied = false; roomCodeShown = false; roomBack = null; matchFoundAt = -1;
            returnToFriends = true; localError = null;
            partyFocus.Reset("room-leave"); revealPartySelection = true;
            return true;
        }

        Task ConnectForRoom() => coordinator.Authenticated ? Task.CompletedTask : coordinator.Connect(RememberName());

        async Task EnterRoom(string channel, string code)
        {
            if (!await RoomStep(() => coordinator.SetReady(true))) return;
            await RoomStep(() => coordinator.Queue(channel, code));
        }

        async Task<bool> RoomStep(Func<Task> action)
        {
            if (lifetime.IsCancellationRequested || roomLeaving) return false;
            await action();
            return string.IsNullOrEmpty(coordinator.Error) && !coordinator.RetryRequired &&
                !lifetime.IsCancellationRequested && !roomLeaving;
        }

        async Task RunRoom(Func<Task> work)
        {
            roomBusy = true;
            try { await work(); }
            catch (Exception error) { if (!lifetime.IsCancellationRequested) localError = WordboundPartyCoordinator.Friendly(error); }
            finally { roomBusy = false; }
        }

        string RoomCode()
        {
            var assignment = coordinator.Party?.Assignment;
            return assignment != null && assignment.Has("privateCode") && !assignment["privateCode"].IsNull
                ? assignment["privateCode"].String() : null;
        }

        IReadOnlyList<(int team, string player)> RoomSeats()
        {
            var assignment = coordinator.Party?.Assignment;
            if (assignment == null || !assignment.Has("roster")) return Array.Empty<(int, string)>();
            return assignment["roster"].Items.Select(seat => (seat["teamId"].Int(), seat["playerId"].String())).ToArray();
        }

        string RoomTitle()
        {
            var settings = coordinator.Party?.Settings;
            string roomMode = settings?.Mode ?? (mode >= 0 ? Modes[mode] : null), roomFormat = settings?.Format ?? (format >= 0 ? Formats[format] : "Solo");
            if (roomMode == null) return roomHost ? "Your room" : "Joining a room";
            string where = coordinator.Party?.Workshop != null ? OnlineMaps.CustomMap : RoomMapName(roomMode, roomFormat, coordinator.Seed ?? seed);
            return FriendRooms.Title(roomMode, roomFormat) + MapCardText.Separator + where;
        }

        string RoomMapName(string roomMode, string roomFormat, string roomSeed)
        {
            var choices = publicCourses.Choices(roomMode, roomFormat);
            int index = choices.Select(course => course.Seed).ToList().IndexOf(roomSeed);
            return index < 0 ? OnlineMaps.PrivateMap : OnlineMaps.Slot(choices[index].Content.CourseId, index);
        }

        string RoomStatus(string code)
        {
            string state = LobbyAssignmentState();
            if (roomLeaving) return "Leaving the room…";
            if (launching) return "Joining match…";
            if (state == "ADMITTING" || state == "STARTED") return StartLine("Everyone's in");
            if (code == null) return roomHost ? "Making your room…" : "Joining the room…";
            var settings = coordinator.Party.Settings;
            string count = FriendRooms.Waiting(settings.Mode, settings.Format, RoomSeats().Count);
            return roomHost ? count : count + MapCardText.Separator + "you're ready, the host starts";
        }

        IEnumerable<string> RoomMembers()
        {
            var seats = RoomSeats();
            if (seats.Count == 0) yield break;
            var names = seats.Select((seat, index) => seat.player == coordinator.PlayerId ? "You" : index == 0 ? "Host" : "Player " + (index + 1)).ToArray();
            var settings = coordinator.Party.Settings;
            if (!FriendRooms.IsTeamMode(settings.Mode))
            {
                yield return string.Join(", ", names);
                yield break;
            }
            for (int team = 0; team < FriendRooms.Teams(settings.Mode); team++)
            {
                var members = seats.Select((seat, index) => (seat, index)).Where(entry => entry.seat.team == team).Select(entry => names[entry.index]).ToArray();
                int open = FriendRooms.TeamSize(settings.Format) - members.Length;
                var shown = members.Concat(Enumerable.Repeat("open", Math.Max(0, open)));
                yield return "Team " + (team + 1) + ": " + string.Join(", ", shown);
            }
        }

        string RoomProblem()
        {
            if (roomLeaving) return null;
            if (coordinator.RetryRequired) return coordinator.Error ?? "The last request was not confirmed.";
            if (!string.IsNullOrEmpty(localError)) return localError;
            if (!string.IsNullOrEmpty(coordinator.Error)) return coordinator.Error;
            if (!roomBusy && !coordinator.Busy && !launching && !returning && coordinator.Party?.HasAttempt != true) return RoomClosed;
            return null;
        }

        bool CanLeaveRoom => !roomLeaving && !launching && !returning && !coordinator.RetryRequired && LobbyAssignmentState() != "STARTED";

        bool CanStartRoom(string code)
        {
            if (!roomHost || code == null || roomLeaving || roomBusy || launching || returning || coordinator.Busy || !coordinator.IsLeader ||
                LobbyAssignmentState() != "FORMING") return false;
            var settings = coordinator.Party.Settings;
            return FriendRooms.CanStart(settings.Mode, settings.Format, RoomSeats().Count);
        }

        void CopyRoomCode(string code)
        {
            var party = coordinator.Party;
            if (party == null || code == null) return;
            try
            {
                GUIUtility.systemCopyBuffer = WordboundRoomInvite.Encode(code, party.Settings, coordinator.Seed);
                roomCopied = true;
            }
            catch (Exception error) { localError = WordboundPartyCoordinator.Friendly(error); }
        }

        void DrawRoom(Vector2 viewport)
        {
            string code = RoomCode();
            if (code != null && !roomCodeShown && Event.current.type == EventType.Layout)
            {
                roomCodeShown = true;
                partyFocus.Reset("room-copy");
            }
            float width = Mathf.Min(viewport.x - 32, 820), height = Mathf.Min(viewport.y - 32, ui.RowHeight * 8.6f);
            GUILayout.BeginArea(new Rect((viewport.x - width) * .5f, (viewport.y - height) * .5f, width, height), ui.Panel);
            GUILayout.Label(RoomTitle(), ui.Heading);
            GUILayout.Label(RoomStatus(code), ui.Wrapped);
            string problem = RoomProblem();
            if (problem != null) GUILayout.Label(problem, ui.Wrapped);
            GUILayout.Space(ui.Gap);
            foreach (string line in RoomMembers()) GUILayout.Label(line, ui.Wrapped);
            GUILayout.FlexibleSpace();
            bool enabled = GUI.enabled;
            if (code != null)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("Room code  " + FriendRooms.ShortCode(code), ui.Heading);
                GUILayout.FlexibleSpace();
                GUILayout.BeginVertical(GUILayout.Width(240));
                GUI.enabled = enabled && coordinator.Party != null;
                PartyControl("room-copy", roomCopied ? "Copied ✓" : "Copy code", () => CopyRoomCode(code));
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
                PartyControl("room-retry", "Retry", () => Invoke(() => coordinator.Retry()), height: ui.RowHeight * 1.4f);
            }
            else
            {
                GUI.enabled = enabled && CanLeaveRoom;
                PartyControl("room-leave", problem == null ? "Leave" : "Back", () => { _ = LeaveRoom(); }, height: ui.RowHeight * 1.4f);
            }
            GUI.enabled = enabled;
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (roomHost)
            {
                GUILayout.BeginVertical(GUILayout.Width(240));
                GUI.enabled = enabled && CanStartRoom(code);
                PartyControl("room-start", "Start", () => Invoke(() => coordinator.StartPrivate()), height: ui.RowHeight * 1.4f);
                GUI.enabled = enabled;
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        async Task LeaveRoom()
        {
            if (roomLeaving || coordinator == null) return;
            roomLeaving = true;
            localError = null;
            try
            {
                float giveUp = Time.unscaledTime + LeaveWaitSeconds;
                while ((coordinator.Busy || roomBusy) && Time.unscaledTime < giveUp)
                {
                    if (lifetime.IsCancellationRequested) return;
                    await Task.Delay(100);
                }
                if (coordinator.Busy || roomBusy) { localError = "The room is still busy. Try again."; return; }
                bool failed = false;
                if (coordinator.Party?.HasAttempt == true && coordinator.IsLeader)
                {
                    await coordinator.CancelQueue();
                    failed = !string.IsNullOrEmpty(coordinator.Error);
                }
                if (!failed && coordinator.Party != null && coordinator.Party.HasAttempt != true)
                {
                    await coordinator.LeaveParty();
                    failed = !string.IsNullOrEmpty(coordinator.Error);
                }
                if (lifetime.IsCancellationRequested || failed || coordinator.RetryRequired || coordinator.Party != null) return;
                roomFlow = false;
                var back = roomBack;
                roomBack = null;
                if (back != null) back();
                else RequestClose();
            }
            catch (Exception error)
            {
                if (!lifetime.IsCancellationRequested) localError = WordboundPartyCoordinator.Friendly(error);
            }
            finally { roomLeaving = false; }
        }
    }
}
