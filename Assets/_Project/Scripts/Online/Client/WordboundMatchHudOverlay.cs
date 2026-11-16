using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        GUIStyle overlayLabel, overlayBox;
        int overlayStyleGeneration = -1;
        string headingText, lapText, knockoutText, menuHintText;
        long headingSeconds = long.MinValue;
        (int Lap, int Checkpoint, int Count) lapKey = (-1, -1, -1);
        int knockoutKey = -1;
        (bool Pad, int Version) menuHintKey;
        (int Health, int First, int Second, int Equipped, bool Keepsake, bool Pad, int Version) trayKey;
        readonly List<string> trayRows = new List<string>();

        void DrawOverlay(float width, float height)
        {
            using var themeScope = ui.Apply(Theme);
            if (overlayStyleGeneration != ui.Generation)
            {
                overlayLabel = new GUIStyle(ui.Wrapped) { padding = new RectOffset(0, 0, 1, 1) };
                overlayLabel.normal.textColor = ui.Ink;
                overlayBox = new GUIStyle(ui.Panel) { padding = new RectOffset(10, 10, 6, 6) };
                PrepareGuidanceStyles();
                overlayStyleGeneration = ui.Generation;
            }
            Color background = GUI.backgroundColor;
            try
            {
                GUI.backgroundColor = new Color(1f, 1f, 1f, .78f);
                float available = width - 36;
                DrawArrow(width, height);
                statusPanel.DrawCompact(18, 18, Mathf.Min(380, available * .48f), overlayLabel, overlayBox, false, 0);
                var tray = inventoryPanel.DrawCompact(18, height - 18, available * .23f, overlayLabel, overlayBox, true, 0);
                DrawTiles(tray, available * .23f);
                actionPanel.DrawCompact(width * .5f, height - 18, available * .5f, overlayLabel, overlayBox, true, .5f);
                menuHintPanel.DrawCompact(width - 18, height - 18, available * .23f, overlayLabel, overlayBox, true, 1);
                DrawNotes(width);
                DrawVoice(width);
                DrawLookEye(width);
            }
            finally { GUI.backgroundColor = background; }
        }

        void PreparePanels()
        {
            preparedFrame = Time.frameCount;
            statusPanel.Begin(); actionPanel.Begin(); inventoryPanel.Begin(); menuHintPanel.Begin();
            statusPanel.Add(Heading());
            var progress = TeamProgressOf(frame);
            if (frame != null)
            {
                if (mode.IsRace && progress != null)
                {
                    statusPanel.Add(LapLine(progress));
                    string objective = CurrentStep().Text;
                    statusPanel.Add(objective.Length != 0 ? objective : ObjectiveText(progress));
                }
                else if (mode.Objective == WordboundObjective.Knockouts)
                    statusPanel.Add(KnockoutLine());
                else if (WordboundPracticePolicy.Supports(mode) && CurrentStep().Text.Length != 0)
                    statusPanel.Add(CurrentStep().Text);
            }
            PrepareTray(progress);
            string context = ContextPrompt();
            if (context.Length != 0) actionPanel.Add(context);
            menuHintPanel.Add(MenuHint());
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
            long seconds = frame != null && mode.TimeLimitSeconds > 0
                ? Math.Max(0, mode.TimeLimitSeconds - frame.Progress.ElapsedTicks / ModeDefinition.TickRate) : -1;
            if (headingText == null || seconds != headingSeconds)
            {
                headingSeconds = seconds;
                headingText = LobbyPlayModeLabels.Name(mode.Id) + (seconds < 0 ? "" : " · " + CourseGuidance.Clock(seconds));
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

        string MenuHint()
        {
            var key = (view && view.UsesGamepad, KeyBindings.Version);
            if (menuHintText == null || !key.Equals(menuHintKey))
            {
                menuHintKey = key;
                menuHintText = Hint(WordboundControl.Spell, "Spell") + " · " + Hint(WordboundControl.Menu, "Menu");
            }
            return menuHintText;
        }

        void PrepareTray(TeamProgress progress)
        {
            if (inventory == null) { tileText = ""; tileBag = null; }
            else if (tileBag == null || !inventory.Letters.Equals(tileBag))
            {
                tileBag = new LetterBag(inventory.Letters);
                tileText = tileBag.ToString();
            }
            if (actor == null || inventory == null) return;
            var key = (Mathf.CeilToInt(actor.Health), HeldItem(0)?.Id ?? -1, HeldItem(1)?.Id ?? -1, actor.EquippedSlot,
                progress != null && progress.KeepsakeCarrier == actorId, view && view.UsesGamepad, KeyBindings.Version);
            if (trayRows.Count == 0 || !key.Equals(trayKey))
            {
                trayKey = key;
                trayRows.Clear();
                trayRows.Add("Health " + key.Item1);
                for (int slot = 0; slot < Math.Min(2, inventory.Slots.Length); slot++) trayRows.Add(HandRow(slot));
                if (key.Item5) trayRows.Add("Carrying the keepsake");
            }
            foreach (string row in trayRows) inventoryPanel.Add(row);
        }

        string HandRow(int slot)
        {
            string key = HandKey(slot);
            return (key.Length == 0 ? "" : "[" + key + "] ") + "Hand " + (slot + 1) + ": " + (HeldItem(slot)?.Word ?? "empty") +
                (actor != null && actor.EquippedSlot == slot ? " (using)" : "");
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

        void HelpMenu()
        {
            recipeScroll = GUILayout.BeginScrollView(recipeScroll);
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
