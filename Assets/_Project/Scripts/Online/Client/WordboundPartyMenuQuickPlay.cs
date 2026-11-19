using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundPartyMenu
    {
        const string CouldNotStart = "Matchmaking could not start. Go back and press Find match again.";
        const float LeaveWaitSeconds = 10f;
        public const int StartCountdownSeconds = 5;
        Func<string> loadQuickChoices = () => PlayerPrefs.GetString(QuickPlayChoices.StorageKey, "");
        Action<string> saveQuickChoices = value => { PlayerPrefs.SetString(QuickPlayChoices.StorageKey, value); PlayerPrefs.Save(); };
        Func<long> quickPlayClock;
        Func<int> utcOffsetMinutes = QuickPlayRegions.LocalOffsetMinutes;
        QuickPlayChoices quickChoices;
        QuickPlayPick quickPick;
        string[] quickMapSeeds;
        bool lobbySearch, lobbyLeaving, quickPlayArmed, quickPlayRunning, quickPlayLeft;
        float lobbySearchSince = -1, matchFoundAt = -1;
        Func<float> searchClock = () => Time.unscaledTime;

        QuickPlayChoices QuickChoices
        {
            get
            {
                if (quickChoices != null || publicCourses == null || publicCourses.IsClosed) return quickChoices;
                quickMapSeeds = publicCourses.Courses.Select(course => course.Seed).Distinct(StringComparer.Ordinal)
                    .OrderBy(seed => seed, StringComparer.Ordinal).ToArray();
                quickChoices = new QuickPlayChoices(loadQuickChoices?.Invoke());
                displayName = quickChoices.Name;
                return quickChoices;
            }
        }

        public bool Searching => coordinator?.Party?.HasAttempt == true && coordinator.Party.Channel == "QuickPlay";

        public void StartMatchmaking()
        {
            if (coordinator == null || QuickChoices == null || lobbySearch) return;
            lobbySearch = true; lobbySearchSince = searchClock(); matchFoundAt = -1;
            partyFocus.Reset("lobby-cancel"); revealPartySelection = true;
            StartQuickPlay();
        }

        bool LobbySearchShown => lobbySearch && coordinator != null && !coordinator.MatchActive;

        void TrackMatchFound()
        {
            if (coordinator == null) return;
            string state = LobbyAssignmentState();
            bool found = (lobbySearch || roomFlow || friendsParty) && (state == "ADMITTING" || state == "STARTED" || launching || coordinator.MatchActive);
            if (!found) matchFoundAt = -1;
            else if (matchFoundAt < 0) matchFoundAt = searchClock();
        }

        int StartCountdown()
        {
            TrackMatchFound();
            return matchFoundAt < 0 ? -1 : Mathf.Max(0, Mathf.CeilToInt(StartCountdownSeconds - (searchClock() - matchFoundAt)));
        }

        bool LaunchHeld() => (lobbySearch || roomFlow || friendsParty) && StartCountdown() != 0;

        string StartLine(string found)
        {
            int left = StartCountdown();
            return left > 0 ? found + Wreckabulary.Rules.MapCardText.Separator + "starting in " + left : "Joining match…";
        }

        string AutoRegion() => QuickPlayRegions.Nearest(utcOffsetMinutes());

        long NowMilliseconds() => quickPlayClock?.Invoke() ?? coordinator?.ServerClock?.Now() ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        bool Offered(string selectedMode, string selectedFormat, string selectedSeed) =>
            publicCourses.Find(selectedMode, selectedFormat, selectedSeed) != null;

        void SaveQuickChoices()
        {
            if (quickChoices != null) saveQuickChoices?.Invoke(quickChoices.Save());
        }

        string RememberName()
        {
            var choices = QuickChoices;
            if (choices == null) return displayName;
            choices.ChooseName(displayName);
            displayName = choices.Name;
            SaveQuickChoices();
            return displayName;
        }

        void RequestClose()
        {
            if (!HeldByLobby)
            {
                if (returnToFriends) WordboundRoomEntry.MarkReturned();
                else WordboundQuickPlayEntry.MarkReturned();
            }
            CloseRequested?.Invoke();
        }

        QuickPlayPick CurrentPick(out string blocked)
        {
            var choices = QuickChoices;
            var party = coordinator.Party;
            int members = party?.Members.Count ?? 0;
            int size = Math.Max(1, members);
            string partySeed = members > 1 || friendsParty && party != null ? coordinator.Seed : null;
            var settings = party?.Settings;
            var keep = partySeed != null && !invitePartySettings && Array.IndexOf(QuickPlayRegions.Ids, settings.Region) >= 0 ?
                new QuickPlayPick(settings.Mode, settings.Format, partySeed, settings.Region) : null;
            var pick = QuickPlayPicker.Pick(choices, quickMapSeeds, Offered, size, QuickPlayPicker.Slot(NowMilliseconds()),
                AutoRegion(), keep, partySeed);
            blocked = pick == null ? QuickPlayPicker.Blocked(choices, size, partySeed != null) : null;
            return pick;
        }

        void UsePick(QuickPlayPick pick)
        {
            quickPick = pick;
            mode = Array.IndexOf(Modes, pick.Mode); format = Array.IndexOf(Formats, pick.Format);
            region = Array.IndexOf(Regions, pick.Region); fill = pick.Fill; seed = pick.Seed; customSeedMode = false;
        }

        OnlineSettings QuickSettings() => coordinator.Settings(quickPick.Mode, quickPick.Format, quickPick.Fill, quickPick.Region);

        QuickPlayState QuickState()
        {
            var party = coordinator.Party;
            var settings = party?.Settings;
            var self = party?.Members.FirstOrDefault(member => member.PlayerId == coordinator.PlayerId);
            return new QuickPlayState {
                MatchActive = coordinator.MatchActive || launching || returning,
                RetryRequired = coordinator.RetryRequired, Busy = coordinator.Busy,
                Authenticated = coordinator.Authenticated, HasParty = party != null,
                HasAttempt = party?.HasAttempt == true, QuickPlayAttempt = party?.Channel == "QuickPlay",
                IsLeader = coordinator.IsLeader, SelfReady = self?.Ready == true,
                AllReady = party != null && party.Members.All(member => member.Ready), Members = party?.Members.Count ?? 0,
                HasRoom = party != null && party.Members.Count < Math.Min(QuickPlaylists.MaximumParty, settings.TeamCapacity),
                SeedMatches = party != null && quickPick != null && coordinator.Seed == quickPick.Seed && coordinator.PublicAvailable,
                SettingsMatch = settings != null && quickPick != null && settings.Mode == quickPick.Mode &&
                    settings.Format == quickPick.Format && settings.Region == quickPick.Region && settings.Fill == quickPick.Fill
            };
        }

        void StartQuickPlay()
        {
            if (coordinator == null || QuickChoices == null || quickPlayArmed || launching || returning ||
                coordinator.MatchActive || coordinator.RetryRequired) return;
            localError = null; quickPlayLeft = false; quickPlayArmed = true;
            SaveQuickChoices();
            _ = AdvanceQuickPlay();
        }

        void StopQuickPlay(string reason = null)
        {
            quickPlayArmed = false;
            if (!string.IsNullOrEmpty(reason)) localError = reason;
        }

        void TickQuickPlay()
        {
            if (quickPlayArmed && !quickPlayRunning && !coordinator.Busy && !launching && !returning) _ = AdvanceQuickPlay();
        }

        async Task AdvanceQuickPlay()
        {
            if (quickPlayRunning || !quickPlayArmed || coordinator == null) return;
            quickPlayRunning = true;
            try
            {
                var last = QuickPlayStep.None;
                for (int guard = 0; guard < 10; guard++)
                {
                    if (!quickPlayArmed || lifetime.IsCancellationRequested) return;
                    if (!string.IsNullOrEmpty(localError)) { StopQuickPlay(); return; }
                    if (coordinator.Party == null || coordinator.IsLeader)
                    {
                        var pick = CurrentPick(out string blocked);
                        if (pick == null) { StopQuickPlay(blocked); return; }
                        UsePick(pick);
                    }
                    var state = QuickState();
                    var step = QuickPlayFlow.Next(state, QuickPlayGoal.Search);
                    if (QuickPlayFlow.Acts(step) && step == last || step == QuickPlayStep.LeaveParty && quickPlayLeft)
                    { StopQuickPlay(CouldNotStart); return; }
                    last = step;
                    switch (step)
                    {
                        case QuickPlayStep.Connect: await coordinator.Connect(QuickChoices.Name); break;
                        case QuickPlayStep.CreateParty: await CreateQuickParty(); break;
                        case QuickPlayStep.LeaveParty: quickPlayLeft = true; await coordinator.LeaveParty(); break;
                        case QuickPlayStep.ApplySettings: invitePartySettings = false; await coordinator.ChangeSettings(QuickSettings()); break;
                        case QuickPlayStep.Ready: await coordinator.SetReady(true); break;
                        case QuickPlayStep.Queue: await coordinator.Queue("QuickPlay"); break;
                        case QuickPlayStep.Wait: case QuickPlayStep.WaitForMembers: return;
                        case QuickPlayStep.Blocked: StopQuickPlay(BlockedLine(state)); return;
                        default: StopQuickPlay(); return;
                    }
                    if (!string.IsNullOrEmpty(coordinator.Error)) { StopQuickPlay(); return; }
                }
                StopQuickPlay(CouldNotStart);
            }
            catch (Exception error)
            {
                if (lifetime.IsCancellationRequested) return;
                StopQuickPlay();
                localError = WordboundPartyCoordinator.Friendly(error);
            }
            finally { quickPlayRunning = false; }
        }

        Task CreateQuickParty()
        {
            _ = publicCourses.ResolveSelection(itemSource, ruleSource, quickPick.Mode, quickPick.Format, quickPick.Seed);
            return coordinator.CreateParty(QuickSettings(), quickPick.Seed);
        }

        static string BlockedLine(QuickPlayState state) => state.RetryRequired || state.HasAttempt ? null :
            "This party plays a private map. Go back and use Custom match for private rooms.";

        string LobbyAssignmentState()
        {
            var assignment = coordinator.Party?.Assignment;
            return assignment != null && assignment.Has("state") ? assignment["state"].String() : null;
        }

        string LobbySearchProblem()
        {
            if (quickPlayArmed || Searching || launching || returning || lobbyLeaving) return null;
            if (coordinator.RetryRequired) return coordinator.Error ?? "The last request was not confirmed.";
            return !string.IsNullOrEmpty(localError) ? localError : !string.IsNullOrEmpty(coordinator.Error) ? coordinator.Error :
                "The search ended. Go back and press Find match again.";
        }

        string LobbySearchTitle()
        {
            string state = LobbyAssignmentState();
            if (lobbyLeaving) return friendsParty ? "Back to your party…" : "Back to the lobby…";
            if (launching) return "Joining match…";
            if (state == "ADMITTING" || state == "STARTED") return StartLine("Match found");
            if (LobbySearchProblem() != null) return "Matchmaking stopped";
            return "Searching " + QuickPlayFlow.Elapsed(lobbySearchSince < 0 ? 0 : searchClock() - lobbySearchSince);
        }

        bool CanLeaveLobbySearch => !lobbyLeaving && !launching && !returning && !coordinator.RetryRequired &&
            LobbyAssignmentState() != "STARTED";

        void DrawLobbySearch(Vector2 viewport)
        {
            float height = ui.RowHeight * 2.6f;
            GUILayout.BeginArea(new Rect(0, viewport.y - height, viewport.x, height), ui.Panel);
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label(LobbySearchTitle(), ui.Heading);
            string problem = LobbySearchProblem();
            if (problem != null) GUILayout.Label(problem, ui.Wrapped);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical(GUILayout.Width(240));
            GUILayout.FlexibleSpace();
            bool enabled = GUI.enabled;
            if (coordinator.RetryRequired)
            {
                GUI.enabled = enabled && !coordinator.Busy;
                PartyControl("lobby-retry", "Retry", () => Invoke(() => coordinator.Retry()), height: ui.RowHeight * 1.4f);
            }
            else
            {
                GUI.enabled = enabled && CanLeaveLobbySearch;
                PartyControl("lobby-cancel", problem == null ? "Cancel" : "Back", () => { _ = LeaveLobbySearch(); }, height: ui.RowHeight * 1.4f);
            }
            GUI.enabled = enabled;
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        async Task LeaveLobbySearch()
        {
            if (lobbyLeaving || coordinator == null) return;
            lobbyLeaving = true;
            StopQuickPlay();
            localError = null;
            try
            {
                float giveUp = Time.unscaledTime + LeaveWaitSeconds;
                while ((coordinator.Busy || quickPlayRunning) && Time.unscaledTime < giveUp)
                {
                    if (lifetime.IsCancellationRequested) return;
                    await Task.Delay(100);
                }
                if (coordinator.Busy || quickPlayRunning) { localError = "Matchmaking is still busy. Try again."; return; }
                bool failed = false;
                if (coordinator.Party?.HasAttempt == true && coordinator.IsLeader)
                {
                    await coordinator.CancelQueue();
                    failed = !string.IsNullOrEmpty(coordinator.Error);
                }
                if (friendsParty)
                {
                    if (lifetime.IsCancellationRequested || failed || coordinator.RetryRequired) return;
                    lobbySearch = false;
                    partyFocus.Reset("party-find"); revealPartySelection = true;
                    return;
                }
                if (!failed && coordinator.Party != null && coordinator.Party.HasAttempt != true)
                {
                    await coordinator.LeaveParty();
                    failed = !string.IsNullOrEmpty(coordinator.Error);
                }
                if (lifetime.IsCancellationRequested || failed || coordinator.RetryRequired || coordinator.Party != null) return;
                lobbySearch = false;
                RequestClose();
            }
            catch (Exception error)
            {
                if (!lifetime.IsCancellationRequested) localError = WordboundPartyCoordinator.Friendly(error);
            }
            finally { lobbyLeaving = false; }
        }
    }
}
