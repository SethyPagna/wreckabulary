using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        bool leaveConfirm, resultsOpen, resultsShown, playAgainSent;
        CourseResultSummary menuSummary;

        bool MatchRunning => frame != null && frame.Progress.Result == null && controller && controller.Fault == "" &&
            persistentNotice.Length == 0;

        bool PlayAgainAvailable => PlayAgainRequested != null && !playAgainSent && frame?.Progress.Result?.Confirmed == true &&
            controller && controller.Fault == "";

        bool ResultsPageOpen => resultsOpen && !helpOpen && !journalOpen && !leaveConfirm && frame?.Progress.Result != null;

        bool ItemsPageOpen => !helpOpen && !journalOpen && !leaveConfirm && !ResultsPageOpen;

        string MenuTitle() => helpOpen ? "Help" : journalOpen ? "Journal" : leaveConfirm ? "Leave match" :
            ResultsPageOpen ? "Results" : "Items";

        void OpenPage(bool results = false, bool journal = false, bool help = false)
        {
            resultsOpen = results; journalOpen = journal; helpOpen = help; leaveConfirm = false;
            recipeScroll = Vector2.zero;
        }

        void ShowResultsOnce()
        {
            var result = frame?.Progress.Result;
            if (resultsShown || result == null || !result.Confirmed || !view) return;
            resultsShown = true;
            ReleaseDefense();
            crafting = false;
            OpenPage(results: true);
            ResetMenuInput();
            menuFocus.Reset(PlayAgainAvailable ? "play-again" : "return");
            view.SetMenuOpen(true);
        }

        void RequestLeave()
        {
            if (!MatchRunning) { ReturnRequested?.Invoke(); return; }
            OpenPage();
            leaveConfirm = true;
            menuFocus.Reset("leave-cancel");
        }

        void ConfirmLeave()
        {
            leaveConfirm = false;
            ReturnRequested?.Invoke();
        }

        void CancelLeave()
        {
            leaveConfirm = false;
            menuFocus.Reset("return");
        }

        void PlayAgain()
        {
            if (!PlayAgainAvailable) return;
            playAgainSent = true;
            PlayAgainRequested?.Invoke();
        }

        void MenuFooter()
        {
            var height = GUILayout.MinHeight(ui.RowHeight);
            if (leaveConfirm)
            {
                if (MenuButton("leave-cancel", "Keep playing", height)) QueueMenuAction(CancelLeave);
                if (MenuButton("leave-confirm", "Leave match", height)) QueueMenuAction(ConfirmLeave);
                return;
            }
            if (PlayAgainAvailable && MenuButton("play-again", "Play again", height)) QueueMenuAction(PlayAgain);
            if (MenuButton("return", MatchRunning ? "Leave match" : returnLabel, height)) QueueMenuAction(RequestLeave);
        }

        void LeavePage(Rect body)
        {
            GUILayout.BeginArea(body);
            GUILayout.Label("Leave this match now?", ui.Heading);
            GUILayout.Label("Choose Keep playing to go back to the course.", ui.Wrapped);
            GUILayout.EndArea();
        }

        void ResultsPage(Rect body)
        {
            GUILayout.BeginArea(body);
            recipeScroll = GUILayout.BeginScrollView(recipeScroll);
            if (menuSummary != null)
            {
                GUILayout.Label(menuSummary.Title, ui.Heading);
                GUILayout.Space(ui.Gap);
                foreach (string row in menuSummary.Rows) GUILayout.Label(row, ui.Wrapped);
            }
            if (menuEpilogue != null)
            {
                GUILayout.Space(12);
                GUILayout.Label("Ending · " + menuEpilogue.Title, ui.Wrapped);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
