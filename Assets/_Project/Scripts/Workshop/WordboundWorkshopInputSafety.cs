using System;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public sealed partial class WordboundWorkshop
    {
        readonly WordboundPadFocusGate workshopPadGate = new WordboundPadFocusGate();
        bool workshopTyping;
        bool padPanel, padDialog, padCounting, padClicking, padReveal, padAiming;
        int padCount, padTargets, padFocus, padPress = -1, padTabs = -1, padHeld, padScreen, padEditorFocus, padScroll;
        float padRepeatAt;
        string padFocusLabel = "", padPressLabel = "";
        CoursePoint? padZoneStart;
        Rect libraryScrollRect;
        const string PanelHint = "Panel · D-pad or stick moves · A presses · LB/RB change step · B returns to the map";
        const string MapHint = "Map · D-pad selects · stick moves · X turns · A frames · View opens the panel";

        bool PadNavigating => Visible && !RecoveryChoicePending && (DialogOpen ? padDialog : padPanel);

        void SuspendWorkshopInput()
        {
            recoveryDecision = 0;
            sourceDecision = 0;
            starterDecision = null;
            controllerSourceDecision = false;
            workshopPadGate.Suspend();
            controllerTools = false;
            padPress = -1;
            CancelGesture();
        }

        void CaptureWorkshopTextFocus() => workshopTyping =
            (GUI.GetNameOfFocusedControl() ?? "").StartsWith("workshop-text:", StringComparison.Ordinal);

        void OnApplicationFocus(bool focused) { if (!focused) SuspendWorkshopInput(); }

        void BeginPadPass() { padCounting = true; padCount = 0; }

        bool EndPadPass()
        {
            padCounting = false;
            padTargets = padCount;
            if (padFocus >= padTargets) padFocus = Mathf.Max(0, padTargets - 1);
            padReveal = false;
            if (padPress >= 0 && padPress < padTargets && PadNavigating) return true;
            padPress = -1;
            return false;
        }

        void PadClickPass()
        {
            var input = Event.current; var type = input.type;
            padClicking = true; padCount = 0;
            input.type = EventType.Used;
            try { DrawWorkshop(); }
            finally { padClicking = false; padPress = -1; input.type = type; }
        }

        bool PadHit(string label)
        {
            if (!GUI.enabled) return false;
            if (padClicking) return padCount++ == padPress && label == padPressLabel;
            if (!padCounting || Event.current.type != EventType.Repaint) return false;
            if (padCount++ == padFocus && PadNavigating) PadFocused(label, GUILayoutUtility.GetLastRect());
            return false;
        }

        bool PadFlip(bool value, bool next, string label) => PadHit(label) ? !value : next;

        int PadSegments(int value, string[] labels, int columns)
        {
            if (!GUI.enabled || labels.Length == 0) return value;
            if (padClicking)
            {
                for (int i = 0; i < labels.Length; i++)
                    if (padCount++ == padPress && labels[i] == padPressLabel) value = i;
                return value;
            }
            if (!padCounting || Event.current.type != EventType.Repaint) return value;
            var area = GUILayoutUtility.GetLastRect();
            columns = Mathf.Clamp(columns, 1, labels.Length);
            int rows = (labels.Length + columns - 1) / columns;
            for (int i = 0; i < labels.Length; i++)
                if (padCount++ == padFocus && PadNavigating)
                    PadFocused(labels[i], new Rect(area.x + area.width * (i % columns) / columns, area.y + area.height * (i / columns) / rows,
                        area.width / columns, area.height / rows));
            return value;
        }

        bool PadButton(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
        {
            bool clicked = ui.Button(content, style, options);
            return PadHit(content.text.Length != 0 ? content.text : content.tooltip) | clicked;
        }

        bool PadToggle(bool value, GUIContent content, GUIStyle style, params GUILayoutOption[] options) =>
            PadFlip(value, GUILayout.Toggle(value, content, style, options), content.text);

        int PadToolbar(int selected, string[] labels, params GUILayoutOption[] options) =>
            PadSegments(GUILayout.Toolbar(selected, labels, options), labels, labels.Length);

        int PadToolbar(int selected, GUIContent[] labels, params GUILayoutOption[] options) =>
            PadSegments(GUILayout.Toolbar(selected, labels, options), labels.Select(label => label.text).ToArray(), labels.Length);

        void PadFocused(string label, Rect rect)
        {
            padFocusLabel = label ?? "";
            var tint = GUI.color;
            try
            {
                GUI.color = new Color(ui.Ink.r, ui.Ink.g, ui.Ink.b, .85f);
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2, rect.width, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.x, rect.y, 2, rect.height), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(rect.xMax - 2, rect.y, 2, rect.height), Texture2D.whiteTexture);
            }
            finally { GUI.color = tint; }
            if (!padReveal) return;
            if (padScroll == 1) inspectorScroll.y = Reveal(inspectorScroll.y, rect, inspectorScrollRect.height);
            else if (padScroll == 2) libraryScroll.y = Reveal(libraryScroll.y, rect, libraryScrollRect.height);
        }

        static float Reveal(float scroll, Rect rect, float visible) => visible <= 1 ? scroll :
            rect.yMin < scroll ? Mathf.Max(0, rect.yMin - 4) : rect.yMax > scroll + visible ? rect.yMax - visible + 4 : scroll;

        int PadTabFocus(int tab) => padTabs < 0 ? 0 : padTabs + tab;

        static int PadDirection(Gamepad pad)
        {
            var dpad = pad.dpad.ReadValue();
            var aim = dpad.sqrMagnitude > .25f ? dpad : pad.leftStick.ReadValue();
            return aim.y > .55f || aim.x < -.55f ? -1 : aim.y < -.55f || aim.x > .55f ? 1 : 0;
        }

        bool PadNavigate(Gamepad pad)
        {
            int want = PadDirection(pad);
            bool acted = false;
            if (want == 0) padHeld = 0;
            else if (want != padHeld || Time.unscaledTime >= padRepeatAt)
            {
                padRepeatAt = Time.unscaledTime + (want != padHeld ? .35f : .09f);
                padHeld = want; acted = true;
                if (padTargets > 0) { padFocus = (padFocus + want + padTargets) % padTargets; padReveal = true; }
            }
            if (pad.buttonSouth.wasPressedThisFrame)
            {
                acted = true;
                if (padTargets > 0) { padPress = padFocus; padPressLabel = padFocusLabel; }
            }
            return acted;
        }

        void TrackPadScreen()
        {
            int screen = OnlineStarting ? 5 : pendingImport != null ? 4 : sharing ? 1 : libraryOpen ? 2 : filesOpen ? 3 : 0;
            if (screen == padScreen) return;
            if (padScreen == 0) padEditorFocus = padFocus;
            padScreen = screen; padFocus = screen == 0 ? padEditorFocus : 0;
            padPress = -1; padHeld = 0; padReveal = true; padDialog = false;
            if (screen != 0) workshopTyping = false;
            workshopPadGate.Suspend();
        }

        void DialogControllerInput(bool focused)
        {
            if (controllerMoving || controllerTools) CancelGesture();
            controllerTools = false;
            var pad = Gamepad.current;
            if (!workshopPadGate.CanRead(pad, focused && Visible && !OnlineStarting,
                workshopTyping || DesktopBinding.Typing || KeyBindings.GameplayBlocked)) return;
            if (pad.buttonEast.wasPressedThisFrame) { padDialog = true; CloseDialog(); return; }
            if (!padDialog)
            {
                int direction = PadDirection(pad);
                if (direction == 0 && !pad.buttonSouth.wasPressedThisFrame) return;
                padDialog = true; padHeld = direction; padRepeatAt = Time.unscaledTime + .35f;
                return;
            }
            float scroll = pad.rightStick.ReadValue().y;
            if (Mathf.Abs(scroll) > .3f) libraryScroll.y = Mathf.Max(0, libraryScroll.y - scroll * Time.unscaledDeltaTime * 900);
            PadNavigate(pad);
        }

        void CloseDialog()
        {
            if (pendingImport != null) SetPendingImport(null);
            else if (sharing) sharing = false;
            else if (libraryOpen && deleteConfirm.Length != 0) deleteConfirm = "";
            else if (libraryOpen && startScreen) LeaveStart();
            else if (libraryOpen) libraryOpen = false;
            else if (filesOpen) filesOpen = false;
        }

        void PanelControllerInput(Gamepad pad)
        {
            if (pad.buttonEast.wasPressedThisFrame) { padPanel = false; ShowStatus(MapHint); return; }
            if (pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame)
            {
                int tab = (StepTab(step) + (pad.rightShoulder.wasPressedThisFrame ? 1 : StepTabs.Length - 1)) % StepTabs.Length;
                ShowStep(tab == 0 ? WorkshopStep.Build : tab == 1 ? lastFill : tab == 2 ? WorkshopStep.Rules : WorkshopStep.Share);
                padFocus = PadTabFocus(tab); padReveal = true;
                return;
            }
            float scroll = pad.rightStick.ReadValue().y;
            if (Mathf.Abs(scroll) > .3f) inspectorScroll.y = Mathf.Max(0, inspectorScroll.y - scroll * Time.unscaledDeltaTime * 900);
            PadNavigate(pad);
        }

        bool PadArmed => placing != null || drawingZone;

        string AimHint() => placing != null ? "A places " + ObjectLabel(placing) + " at the + · left stick moves the view · B cancels" :
            zoneMode == 0 ? "A places the object at the + · left stick moves the view · B cancels" :
            padZoneStart == null ? "A marks one corner at the + · left stick moves the view · B cancels" :
            "A marks the opposite corner · left stick moves the view · B cancels";

        CoursePoint ViewCentre()
        {
            EnsureView();
            return new CoursePoint((int)(view.Target.x * 1000), ActiveHeight, (int)(view.Target.z * 1000));
        }

        void PlaceAtViewCentre()
        {
            var at = ViewCentre();
            if (placing != null)
            {
                string definition = placing;
                CancelGesture();
                Edit("Place room", draft => WorkshopCommands.AddModule(draft, definition, at));
                if (status == "Place room") ShowStatus(ObjectLabel(definition) + " placed · stick moves it · X turns it");
                return;
            }
            if (zoneMode != 0 && padZoneStart == null) { padZoneStart = at; padAiming = true; ShowStatus(AimHint()); return; }
            var from = padZoneStart ?? at;
            drawingZone = padAiming = false; padZoneStart = null;
            DrawZone(from, at);
        }

        void PadAimOverlay()
        {
            if (!padAiming || !PadArmed) return;
            if (padZoneStart.HasValue)
            {
                var start = padZoneStart.Value; var end = ViewCentre();
                var corners = Corners(new CourseBox(new CoursePoint(Math.Min(start.X, end.X), start.Y, Math.Min(start.Z, end.Z)),
                    new CoursePoint(Math.Max(start.X, end.X), start.Y, Math.Max(start.Z, end.Z))));
                for (int i = 0; i < 4; i++) DrawWorldLine(corners[i], corners[(i + 1) % 4], new Color(.4f, 1, .8f));
            }
            ViewHandle(LocalViewport.center, "+", "A places it here", 22, 11, true);
        }
    }
}
