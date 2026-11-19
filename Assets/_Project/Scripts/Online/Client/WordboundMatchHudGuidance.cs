using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        public const string IntroSeenKey = "wv.courses.intro";
        const string IntroTitle = "Courses work a little differently";
        const double IntroMinimumSeconds = 8, IntroMaximumSeconds = 30, ToastSeconds = 3;
        const int VisibleToasts = 2, QueuedToasts = 4;
        static readonly Color ArrowTint = new Color(1f, .78f, .25f);
        static GUIContent[] letterContents;
        readonly List<(string Text, double Until)> toasts = new List<(string, double)>();
        readonly GUIContent noteContent = new GUIContent(), chipContent = new GUIContent(), extraContent = new GUIContent(),
            introTitleContent = new GUIContent(IntroTitle), introContent = new GUIContent();
        CourseStep step;
        MatchSnapshot stepFrame;
        string distanceText = "", tileText = "";
        int distanceMetres = -1, distanceFloor, extraShown = -1;
        (bool Pad, int Version) introKey = (false, -1);
        bool introPending;
        double introShownAt = double.NaN;
        LetterBag tileBag;
        Texture2D arrowTexture;
        GUIStyle tileStyle, chipStyle, noteStyle, cardTitle;

        CourseStep CurrentStep()
        {
            if (!ReferenceEquals(stepFrame, frame))
            {
                stepFrame = frame;
                step = mode == null || course == null ? default : CourseGuidance.Next(mode, course, frame, actorId);
            }
            return step;
        }

        void ObserveProgress(MatchSnapshot before, MatchSnapshot after)
        {
            if (before == null || after == null || mode == null) return;
            foreach (string text in CourseGuidance.Toasts(mode, course, before, after, actorId)) AddToast(text, Time.unscaledTimeAsDouble);
        }

        void AddToast(string text, double now)
        {
            toasts.RemoveAll(toast => toast.Until <= now);
            toasts.Add((text, now + ToastSeconds));
            while (toasts.Count > QueuedToasts) toasts.RemoveAt(0);
        }

        void PrepareIntro()
        {
            introPending = PlayerPrefs.GetInt(IntroSeenKey, 0) == 0;
            introShownAt = double.NaN;
        }

        void NoteIntroInput(PlayerCommands input)
        {
            if (!introPending || double.IsNaN(introShownAt) || Time.unscaledTimeAsDouble - introShownAt < IntroMinimumSeconds) return;
            if (input.attack || input.grab || input.spellDown || input.slot > 0 || input.swap || input.drop || input.jump) CloseIntro();
        }

        void CloseIntro()
        {
            if (!introPending) return;
            introPending = false;
            PlayerPrefs.SetInt(IntroSeenKey, 1);
            PlayerPrefs.Save();
        }

        string[] IntroLines() => new[] {
            Press(WordboundControl.Interact, "on a letter to collect it.", "Collect each letter by hand."),
            Press(WordboundControl.Spell, "to open your items, pick one, then choose Spell.", "Open your items, pick one, then choose Spell."),
            HandKey(0).Length == 0 || HandKey(1).Length == 0 ? "Pick Hand 1 or Hand 2 from your items." :
                "[" + HandKey(0) + "] and [" + HandKey(1) + "] pick Hand 1 and Hand 2.",
            mode != null && mode.IsRace ? "Follow the arrow to the next checkpoint." :
                WordboundPracticePolicy.Supports(mode) ? "Follow the arrow to a practice station." : "Spell an item, then knock out your rivals."
        };

        string Press(WordboundControl control, string withKey, string withoutKey)
        {
            string key = view ? view.ControlHint(control) : "";
            return string.IsNullOrEmpty(key) ? withoutKey : "Press [" + key + "] " + withKey;
        }

        string DistanceLabel()
        {
            var current = CurrentStep();
            if (!current.HasTarget || !CourseGuidance.TryDistance(frame, actorId, current.Target, out float metres, out float rise)) return "";
            int rounded = Math.Max(1, (int)Math.Round(metres));
            int floor = rise > CourseGuidance.FloorRise ? 1 : rise < -CourseGuidance.FloorRise ? -1 : 0;
            if (rounded != distanceMetres || floor != distanceFloor)
            {
                distanceMetres = rounded; distanceFloor = floor;
                distanceText = CourseGuidance.DistanceText(metres, rise);
            }
            return distanceText;
        }

        void PrepareGuidanceStyles()
        {
            tileStyle = new GUIStyle(ui.Panel) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
                padding = new RectOffset(0, 0, 0, 0), wordWrap = false, clipping = TextClipping.Overflow };
            tileStyle.normal.textColor = ui.Ink;
            chipStyle = new GUIStyle(overlayBox) { alignment = TextAnchor.MiddleCenter, wordWrap = false, padding = new RectOffset(8, 8, 3, 3) };
            chipStyle.normal.textColor = ui.Ink;
            noteStyle = new GUIStyle(chipStyle) { fontStyle = FontStyle.Bold, padding = new RectOffset(14, 14, 7, 7) };
            cardTitle = new GUIStyle(overlayLabel) { fontStyle = FontStyle.Bold };
        }

        bool GuidanceVisible => initialized && view && controller && frame != null && !view.MenuOpen;

        void DrawArrow(float width, float height)
        {
            if (!GuidanceVisible) return;
            var current = CurrentStep();
            var camera = view.Camera;
            var world = controller.CourseWorld;
            if (!current.HasTarget || !camera || !world || controller.IsSimulationPaused || controller.Fault != "" ||
                frame.Progress.Result != null) return;
            Vector3 point = world.transform.TransformPoint(new Vector3(current.Target.X, current.Target.Y + 1.4f, current.Target.Z));
            Vector3 screen = camera.WorldToScreenPoint(point);
            float scale = Screen.width / Mathf.Max(1f, width);
            var edge = CourseGuidance.Edge(screen.x, screen.y, screen.z, Screen.width, Screen.height, 64 * scale);
            var at = new Vector2(edge.X / scale, height - edge.Y / scale);
            Matrix4x4 matrix = GUI.matrix;
            Color color = GUI.color;
            try
            {
                GUI.matrix = matrix * Matrix4x4.TRS(at, Quaternion.Euler(0, 0, edge.Angle), Vector3.one);
                GUI.color = new Color(0, 0, 0, .45f);
                GUI.DrawTexture(new Rect(-15, -13, 30, 30), ArrowTexture());
                GUI.color = ArrowTint;
                GUI.DrawTexture(new Rect(-13, -15, 26, 26), ArrowTexture());
            }
            finally { GUI.matrix = matrix; GUI.color = color; }
            string label = DistanceLabel();
            if (label.Length == 0) return;
            chipContent.text = label;
            var size = chipStyle.CalcSize(chipContent);
            var offset = edge.OnScreen ? new Vector2(0, -30) : (new Vector2(width * .5f, height * .5f) - at).normalized * 36;
            GUI.Box(new Rect(at.x + offset.x - size.x * .5f, at.y + offset.y - size.y * .5f, size.x, size.y), chipContent, chipStyle);
        }

        void DrawTiles(Rect panel, float maximumWidth)
        {
            if (!initialized || tileText.Length == 0 || tileStyle == null) return;
            const float size = 24, gap = 3;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((maximumWidth + gap) / (size + gap)));
            var (shown, extra) = TileSplit(tileText.Length, perRow * 2);
            int cells = shown + (extra > 0 ? 1 : 0);
            int rows = (cells + perRow - 1) / perRow;
            float top = panel.yMin - rows * (size + gap);
            if (extra != extraShown) { extraShown = extra; extraContent.text = "+" + extra; }
            for (int i = 0; i < cells; i++)
            {
                var rect = new Rect(18 + i % perRow * (size + gap), top + i / perRow * (size + gap), size, size);
                GUI.Box(rect, i < shown ? LetterContent(tileText[i]) : extraContent, tileStyle);
            }
        }

        static GUIContent LetterContent(char letter)
        {
            if (letterContents == null)
            {
                letterContents = new GUIContent[LetterBag.Alphabet];
                for (int i = 0; i < letterContents.Length; i++) letterContents[i] = new GUIContent(((char)('A' + i)).ToString());
            }
            return LetterBag.IsLetter(letter) ? letterContents[letter - 'A'] : GUIContent.none;
        }

        void DrawNotes(float width)
        {
            if (!GuidanceVisible || noteStyle == null) return;
            double now = Time.unscaledTimeAsDouble;
            float top = 18;
            for (int i = toasts.Count - 1, shown = 0; i >= 0 && shown < VisibleToasts; i--)
            {
                if (toasts[i].Until <= now) continue;
                noteContent.text = toasts[i].Text;
                var size = noteStyle.CalcSize(noteContent);
                float wide = Mathf.Min(size.x, width * .5f);
                var rect = new Rect((width - wide) * .5f, top, wide, size.y);
                GUI.Box(rect, noteContent, noteStyle);
                top = rect.yMax + 6; shown++;
            }
            if (introPending && !controller.IsSimulationPaused) DrawIntro(width, top, now);
        }

        void DrawIntro(float width, float top, double now)
        {
            if (double.IsNaN(introShownAt)) introShownAt = now;
            else if (now - introShownAt >= IntroMaximumSeconds) { CloseIntro(); return; }
            var key = (view.UsesGamepad, KeyBindings.Version);
            if (!key.Equals(introKey)) { introKey = key; introContent.text = string.Join("\n", IntroLines()); }
            float wide = Mathf.Min(460, width * .5f), inner = wide - overlayBox.padding.horizontal;
            float titleHeight = cardTitle.CalcHeight(introTitleContent, inner), bodyHeight = overlayLabel.CalcHeight(introContent, inner);
            var area = new Rect((width - wide) * .5f, top, wide, titleHeight + bodyHeight + overlayBox.padding.vertical + 4);
            GUI.Box(area, GUIContent.none, overlayBox);
            GUI.Label(new Rect(area.x + overlayBox.padding.left, area.y + overlayBox.padding.top, inner, titleHeight), introTitleContent, cardTitle);
            GUI.Label(new Rect(area.x + overlayBox.padding.left, area.y + overlayBox.padding.top + titleHeight + 4, inner, bodyHeight),
                introContent, overlayLabel);
        }

        Texture2D ArrowTexture()
        {
            if (arrowTexture) return arrowTexture;
            const int size = 32;
            arrowTexture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = "Course arrow", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float half = (size - 1 - y) * .5f;
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(half - Mathf.Abs(x - (size - 1) * .5f) + .5f) * 255));
            }
            arrowTexture.SetPixels32(pixels);
            arrowTexture.Apply(false, true);
            return arrowTexture;
        }

        void DisposeGuidance()
        {
            if (!arrowTexture) return;
            if (Application.isPlaying) Destroy(arrowTexture); else DestroyImmediate(arrowTexture);
            arrowTexture = null;
        }
    }
}
