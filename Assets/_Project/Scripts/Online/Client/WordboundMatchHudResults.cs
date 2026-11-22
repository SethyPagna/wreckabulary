using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        const string KeepPlayingLabel = "Keep playing", LeaveLabel = "Leave match", PlayAgainLabel = "Play again";
        const float FooterMinWidth = 180f, FooterPad = 40f;
        readonly GUIContent footerMeasure = new GUIContent();
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
            RecordRunTime();
            ReleaseDefense();
            crafting = false;
            OpenPage(results: true);
            ResetMenuInput();
            menuFocus.Reset(PlayAgainAvailable ? "play-again" : "return");
            view.SetMenuOpen(true);
        }

        void RequestLeave()
        {
            if (!MatchRunning) { Leave(); return; }
            OpenPage();
            leaveConfirm = true;
            menuFocus.Reset("leave-cancel");
        }

        void ConfirmLeave()
        {
            leaveConfirm = false;
            Leave();
        }

        void Leave()
        {
            RecordRunTime();
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
            GUILayout.FlexibleSpace();
            if (leaveConfirm)
            {
                if (MenuButton("leave-cancel", KeepPlayingLabel, height, FooterWidth(KeepPlayingLabel))) QueueMenuAction(CancelLeave);
                if (MenuButton("leave-confirm", LeaveLabel, height, FooterWidth(LeaveLabel))) QueueMenuAction(ConfirmLeave);
            }
            else
            {
                if (PlayAgainAvailable && MenuButton("play-again", PlayAgainLabel, height, FooterWidth(PlayAgainLabel))) QueueMenuAction(PlayAgain);
                string back = MatchRunning ? LeaveLabel : returnLabel;
                if (MenuButton("return", back, height, FooterWidth(back))) QueueMenuAction(RequestLeave);
            }
            GUILayout.FlexibleSpace();
        }

        GUILayoutOption FooterWidth(string label)
        {
            footerMeasure.text = "› " + label;
            return GUILayout.Width(Mathf.Max(FooterMinWidth, Mathf.Ceil(ui.SingleLine.CalcSize(footerMeasure).x) + FooterPad));
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
