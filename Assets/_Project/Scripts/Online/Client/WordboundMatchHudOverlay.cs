using System;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        const string GuideRowId = "how-to-play";
        string headingText, lapText, knockoutText;
        ModeDefinition headingMode;
        bool guideOpen;
        int guideClosedFrame = -1;
        (int Lap, int Checkpoint, int Count) lapKey = (-1, -1, -1);
        int knockoutKey = -1;

        void DrawOverlay(float width, float height)
        {
            using var themeScope = ui.Apply(Theme);
            PrepareKit();
            Color background = GUI.backgroundColor;
            try
            {
                DrawArrow(width, height);
                var status = DrawTitlePlate(width);
                DrawVitals(height);
                var tray = DrawTray(width, height);
                DrawActionChip(width, height, tray);
                notesTop = SeatStripShown ? DrawSeatStrip(width, status) : DrawScoreStrip(width, status);
                DrawNotes(width);
                DrawVoice(width);
                DrawLookEye(width);
            }
            finally { GUI.backgroundColor = background; }
        }

        void PreparePanels()
        {
            preparedFrame = Time.frameCount;
            statusPanel.Begin();
            statusPanel.Add(Heading());
            statusTagRow = -1;
            var progress = TeamProgressOf(frame);
            if (frame != null)
            {
                if (mode.IsRace && progress != null)
                {
                    statusPanel.Add(LapLine(progress));
                    statusTagRow = 1;
                    string objective = CurrentStep().Text;
                    statusPanel.Add(objective.Length != 0 ? objective : ObjectiveText(progress));
                }
                else if (mode.Objective == WordboundObjective.Knockouts)
                {
                    statusPanel.Add(KnockoutLine());
                    statusTagRow = 1;
                }
                else if (WordboundPracticePolicy.Supports(mode) && CurrentStep().Text.Length != 0)
                    statusPanel.Add(CurrentStep().Text);
            }
            PrepareTray(progress);
            RefreshSeats();
            actionPrompt.Set(ContextPrompt());
            spellKeys.Set(view ? Hint(WordboundControl.Spell, SpellWord) : "");
            menuKeys.Set(view ? Hint(WordboundControl.Menu, MenuWord) : "");
            panelsReady = true;
        }

        TeamProgress TeamProgressOf(MatchSnapshot snapshot)
        {
            if (snapshot == null) return null;
            foreach (var progress in snapshot.Progress.Teams) if (progress.Team == team) return progress;
            return null;
        }

        string Heading()
        {
            if (headingText == null || !ReferenceEquals(headingMode, mode))
            {
                headingMode = mode;
                headingText = LobbyPlayModeLabels.Name(mode.Id);
            }
            return headingText;
        }

        string LapLine(TeamProgress progress)
        {
            int lap = Math.Min(progress.Lap, course.Laps.Count - 1);
            int count = course.Laps[lap].Checkpoints.Count;
            var key = (Math.Min(progress.Lap + 1, mode.Laps), Math.Min(progress.NextCheckpoint + 1, count), count);
            if (lapText == null || !key.Equals(lapKey))
            {
                lapKey = key;
                lapText = $"Lap {key.Item1}/{mode.Laps} · Checkpoint {key.Item2}/{count}";
            }
            return lapText;
        }

        string KnockoutLine()
        {
            int knockouts = 0;
            foreach (var player in frame.Progress.Players) if (player.Id == actorId) knockouts = player.Knockouts;
            if (knockoutText == null || knockouts != knockoutKey)
            {
                knockoutKey = knockouts;
                knockoutText = $"Knockouts {knockouts}/{mode.KnockoutTarget}";
            }
            return knockoutText;
        }

        string HandKey(int slot)
        {
            if (!view || view.ControlHint(WordboundControl.Menu).Length == 0) return "";
            return view.UsesGamepad ? "D-PAD " + ControlHints.PadHand(slot) :
                ControlHints.KeyOf(slot == 0 ? DesktopBinding.Shared.Hand1 : DesktopBinding.Shared.Hand2);
        }

        string HandsHint()
        {
            string first = HandKey(0), second = HandKey(1);
            return first.Length == 0 || second.Length == 0 ? "Hand 1 / Hand 2" : "[" + first + "] / [" + second + "] Hand 1 / Hand 2";
        }

        static (int Shown, int Extra) TileSplit(int letters, int maximum) =>
            letters <= maximum ? (letters, 0) : (Math.Max(0, maximum - 1), letters - Math.Max(0, maximum - 1));

        string ContextPrompt()
        {
            if (controller.Fault != "") return "Connection interrupted · " + returnLabel;
            if (persistentNotice.Length != 0) return persistentNotice;
            if (frame == null) return "Preparing the world…";
            if (frame.Progress.Result != null) return ResultText(frame.Progress.Result);
            if (controller.IsSimulationPaused) return "Waiting for the host…";
            if (actor != null && actor.Health <= 0) return "Recovering…";
            string transient = TransientNotice(Time.unscaledTimeAsDouble);
            if (transient.Length != 0) return transient;
            if (inventory?.Craft != null) return "Spelling " + inventory.Craft.ItemId + "…";
            if (actor == null || inventory == null || actor.Health <= 0 || !inventory.CanAct) return "Recovering…";
            var target = Target();
            if (target != null)
            {
                var kind = (WorldBodyKind)target.BodyKind;
                var item = kind == WorldBodyKind.Item ? ItemFor(target.BodyId.ToString()) : null;
                string action = InteractionCaption(kind, item?.Word, item != null && InteractionPuzzle(item) != null);
                if (action.Length != 0) return Hint(WordboundControl.Interact, action);
            }
            var nearby = WordboundPracticeGuidePlan.Nearest(course, frame, actorId);
            if (nearby != null && frame.Practice.SiteHash != nearby.Hash && !WordboundPracticeGuidePlan.Completed(frame, nearby))
                return Hint(WordboundControl.Interact, "Practise " + course.Puzzles.Single(p => p.Id == nearby.PuzzleId).RecipeId);
            if (inventory.CarriedFurniture >= 0) return "Hold " + Hint(WordboundControl.Drop, "Drop");
            var shield = SelectedShield();
            if (shield != null)
                return actor.Defense == null ? "Hold " + Hint(WordboundControl.Block, "Block") :
                    WordboundDefensePolicy.Raised(actor.Defense, items.Get(shield.Word).Shield, frame.Progress.Tick) ? "Blocking" : "Raising shield…";
            return showQuickTips ? Hint(WordboundControl.Attack, "Smash furniture") : "";
        }

        void SetNotice(string message)
        {
            notice = message ?? "";
            noticeUntil = Time.unscaledTimeAsDouble + 4;
        }

        string TransientNotice(double now) => now < noticeUntil ? notice : "";

        static string InteractionCaption(WorldBodyKind kind, string word, bool puzzle) => kind switch
        {
            WorldBodyKind.Tile => "Collect letter",
            WorldBodyKind.Keepsake => "Take keepsake",
            WorldBodyKind.Item when !string.IsNullOrEmpty(word) => (puzzle ? "Use " : "Pick up ") + word,
            _ => ""
        };

        ResolvedCoursePuzzle InteractionPuzzle(ItemInstance item)
        {
            var progress = frame.Progress.Teams.Single(t => t.Team == team);
            var puzzle = mode.IsRace && item.State == ItemState.Deployed && !progress.Finished
                ? course.Puzzles.FirstOrDefault(p => p.RecipeId == item.Word &&
                    course.Laps[progress.Lap].Checkpoints.Skip(progress.NextCheckpoint).Take(1)
                        .Any(c => c.PuzzleIds.Contains(p.Id)) && !progress.CompletedPuzzles.Contains(p.Id))
                : null;
            if (mode.Id == "Freestyle" && item.State == ItemState.Deployed)
            {
                var selected = WordboundPracticePolicy.Selected(course, frame.Practice);
                if (selected != null && !WordboundPracticeGuidePlan.Completed(frame, selected))
                    puzzle = course.Puzzles.FirstOrDefault(p => p.Id == selected.PuzzleId && p.RecipeId == item.Word);
            }
            return puzzle;
        }

        string GuideId => mode == null ? null : CourseGuides.For(mode.Id);

        bool GuideBlocksMenu => guideOpen || Time.frameCount == guideClosedFrame;

        void GuideRow()
        {
            if (!ModeGuides.Has(GuideId)) return;
            if (MenuButton(GuideRowId, CourseGuides.RowLabel)) QueueMenuAction(OpenGuide);
            GUILayout.Space(8);
        }

        void OpenGuide()
        {
            string id = GuideId;
            if (guideOpen || !ModeGuides.Has(id)) return;
            var panel = ModeGuides.Show(id, null, GuideClosed);
            if (!panel) return;
            guideOpen = true;
            panel.KeepWhile = () => this && view && view.MenuOpen && helpOpen;
        }

        void GuideClosed()
        {
            guideOpen = false;
            guideClosedFrame = Time.frameCount;
            if (helpOpen) menuFocus.Reset(GuideRowId);
        }

        void CloseGuide()
        {
            if (guideOpen) ModeGuides.Close();
        }

        bool GuideKeepsMenu()
        {
            if (!GuideBlocksMenu) return false;
            if (view.MenuOpen) return guideOpen;
            CloseGuide();
            view.SetMenuOpen(true);
            return true;
        }

        void HelpMenu()
        {
            recipeScroll = GUILayout.BeginScrollView(recipeScroll);
            GuideRow();
            if (frame != null && mode != null && course != null)
            {
                if (mode.IsRace) GUILayout.Label(ObjectiveText(frame.Progress.Teams.Single(t => t.Team == team)), ui.Wrapped);
                else if (mode.Id == "Freestyle")
                {
                    var nearby = WordboundPracticeGuidePlan.Nearest(course, frame, actorId);
                    GUILayout.Label(nearby == null ? "Explore and choose a practice station." : WordboundPracticeGuidePlan.Status(frame, nearby), ui.Wrapped);
                }
                GUILayout.Space(8);
            }
            GUILayout.Label("Smash furniture. Collect letters. Spell items.", ui.Wrapped);
            GUILayout.Space(4);
            GUILayout.Label(IntroTitle, ui.SingleLine);
            foreach (string row in IntroLines()) GUILayout.Label(row, ui.Wrapped);
            if (MenuButton("quick-tips", showQuickTips ? "Hide quick tips" : "Show quick tips"))
                QueueMenuAction(() => showQuickTips = !showQuickTips);
            GUILayout.Space(8);
            foreach (string row in HelpControls()) GUILayout.Label(row, ui.Wrapped);
            GUILayout.EndScrollView();
        }

        string ThrowHint()
        {
            string aim = view.ControlHint(WordboundControl.Aim), attack = view.ControlHint(WordboundControl.Attack);
            return string.IsNullOrEmpty(aim) || string.IsNullOrEmpty(attack) ? "Throw" : "[" + aim + "] + [" + attack + "] Throw";
        }

        string[] HelpControls() => new[] {
            Hint(WordboundControl.Interact, "Collect a letter, pick up or use"),
            Hint(WordboundControl.Attack, "Smash"),
            Hint(WordboundControl.Spell, "Spell an item"),
            ThrowHint(),
            "Hold " + Hint(WordboundControl.Drop, "Drop"),
            HandsHint(),
            Hint(WordboundControl.CycleGear, "Swap hands"),
            "Hold " + Hint(WordboundControl.Block, "Block with a shield"),
            Hint(WordboundControl.Menu, "Resume / menu")
        };
    }
}
