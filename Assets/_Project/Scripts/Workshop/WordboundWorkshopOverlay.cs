using System;
using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Workshop
{
    public static class WordboundWorkshopOverlay
    {
        public const float LineWidth = 2;
        public static bool Finite(Vector2 point) => !float.IsNaN(point.x) && !float.IsInfinity(point.x) &&
            !float.IsNaN(point.y) && !float.IsInfinity(point.y);

        public static bool ClipLine(Rect viewport, ref Vector2 from, ref Vector2 to)
        {
            const float inset = LineWidth * .5f + .5f;
            if (!Finite(from) || !Finite(to) || !Finite(viewport.position) || !Finite(viewport.size) ||
                viewport.width <= inset * 2 || viewport.height <= inset * 2) return false;
            var bounds = Rect.MinMaxRect(viewport.xMin + inset, viewport.yMin + inset, viewport.xMax - inset, viewport.yMax - inset);
            double dx = (double)to.x - from.x, dy = (double)to.y - from.y, first = 0, last = 1;
            if (!Clip(-dx, from.x - bounds.xMin, ref first, ref last) || !Clip(dx, bounds.xMax - from.x, ref first, ref last) ||
                !Clip(-dy, from.y - bounds.yMin, ref first, ref last) || !Clip(dy, bounds.yMax - from.y, ref first, ref last)) return false;
            var start = from;
            from = new Vector2(Mathf.Clamp((float)(start.x + dx * first), bounds.xMin, bounds.xMax),
                Mathf.Clamp((float)(start.y + dy * first), bounds.yMin, bounds.yMax));
            to = new Vector2(Mathf.Clamp((float)(start.x + dx * last), bounds.xMin, bounds.xMax),
                Mathf.Clamp((float)(start.y + dy * last), bounds.yMin, bounds.yMax));
            return (to - from).sqrMagnitude > .0001f;
        }

        public const float TurnRadius = 10, ArrowSize = 11;

        public static (Vector2 left, Vector2 right) ArrowHead(Vector2 from, Vector2 tip, float size)
        {
            var back = from - tip;
            back = back.sqrMagnitude < .0001f ? Vector2.down : back.normalized;
            return (tip + Turned(back, 28) * size, tip + Turned(back, -28) * size);
        }

        public static Vector2[] TurnArc(Vector2 centre, float radius, int segments)
        {
            var points = new Vector2[Mathf.Max(1, segments) + 1];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = (200 - 220f * i / (points.Length - 1)) * Mathf.Deg2Rad;
                points[i] = centre + new Vector2(Mathf.Cos(angle), -Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        static Vector2 Turned(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
        }

        static bool Clip(double direction, double offset, ref double first, ref double last)
        {
            if (direction == 0) return offset >= 0;
            double along = offset / direction;
            if (direction < 0) { if (along > last) return false; first = Math.Max(first, along); }
            else { if (along < first) return false; last = Math.Min(last, along); }
            return true;
        }

        public static bool PlaceLabel(Vector2 anchor, Vector2 size, Rect viewport, IList<Rect> occupied, out Rect result)
        {
            result = default;
            if (!Finite(anchor) || !Finite(size) || size.x <= 0 || size.y <= 0 || !viewport.Contains(anchor)) return false;
            for (int candidate = 0; candidate < 8; candidate++)
            {
                float x = anchor.x - size.x * .5f, y = anchor.y - size.y - 8;
                if (candidate == 1) { x = anchor.x + 12; y = anchor.y - size.y * .5f; }
                if (candidate == 2) { x = anchor.x - size.x - 12; y = anchor.y - size.y * .5f; }
                if (candidate == 3) y = anchor.y + 10;
                if (candidate >= 4)
                {
                    x = anchor.x + (candidate % 2 == 0 ? 12 : -size.x - 12);
                    y = candidate < 6 ? anchor.y - size.y - 10 : anchor.y + 10;
                }
                var rect = new Rect(x, y, size.x, size.y);
                if (rect.xMin < viewport.xMin + 4 || rect.yMin < viewport.yMin + 4 ||
                    rect.xMax > viewport.xMax - 4 || rect.yMax > viewport.yMax - 4) continue;
                bool overlap = false;
                for (int i = 0; i < occupied.Count; i++)
                    if (rect.Overlaps(occupied[i])) { overlap = true; break; }
                if (overlap) continue;
                result = rect;
                occupied.Add(Rect.MinMaxRect(rect.xMin - 3, rect.yMin - 3, rect.xMax + 3, rect.yMax + 3));
                return true;
            }
            return false;
        }
    }

    public sealed partial class WordboundWorkshop
    {
        struct ViewLabel
        {
            public Vector2 Anchor;
            public string Caption, Detail;
            public int Priority;
        }
        readonly List<ViewLabel> viewLabels = new List<ViewLabel>();
        readonly List<Rect> labelSpace = new List<Rect>();
        GUIStyle viewLabel, importantViewLabel, viewHandle, activeViewHandle;
        int handleStyleGeneration = -1;
        Rect LocalViewport => new Rect(0, 0, viewport.width, viewport.height);

        void BeginOverlay()
        {
            viewLabels.Clear(); labelSpace.Clear();
            labelSpace.Add(ToolbarReserved);
        }

        void AddViewLabel(CoursePoint position, string caption, string detail, int priority)
        {
            if (!view.TryProject(position, viewport, out var anchor) || !LocalViewport.Contains(anchor)) return;
            viewLabels.Add(new ViewLabel { Anchor = anchor, Caption = caption.Length <= 25 ? caption : caption.Substring(0, 24) + "…",
                Detail = detail, Priority = priority });
        }

        void DrawViewLabels()
        {
            if (viewLabel == null)
            {
                viewLabel = new GUIStyle(normalBox) { wordWrap = false, clipping = TextClipping.Clip, fontSize = 12,
                    padding = new RectOffset(6, 6, 2, 2) };
                importantViewLabel = new GUIStyle(viewLabel); importantViewLabel.normal.textColor = ui.Ink;
                importantViewLabel.font = heading.font;
            }
            viewLabels.Sort((a, b) => { int priority = b.Priority.CompareTo(a.Priority); return priority != 0 ? priority : string.CompareOrdinal(a.Detail, b.Detail); });
            foreach (var label in viewLabels)
            {
                var style = label.Priority >= 2 ? importantViewLabel : viewLabel;
                var content = new GUIContent(label.Caption, label.Detail);
                var size = style.CalcSize(content); size.y = 22;
                if (WordboundWorkshopOverlay.PlaceLabel(label.Anchor, size, LocalViewport, labelSpace, out var rect))
                    GUI.Label(rect, content, style);
            }
        }

        void OverlayBox(Rect rect, string caption, GUIStyle style = null)
        {
            if (!LocalViewport.Overlaps(rect)) return;
            labelSpace.Add(Rect.MinMaxRect(rect.xMin - 3, rect.yMin - 3, rect.xMax + 3, rect.yMax + 3));
            GUI.Box(rect, caption, style ?? GUI.skin.box);
        }

        void ViewHandle(Vector2 at, string caption, string hint, float size, float hitRadius, bool active = false)
        {
            if (handleStyleGeneration != styleGeneration)
            { handleStyleGeneration = styleGeneration; viewHandle = activeViewHandle = null; }
            var visual = new Rect(at.x - size * .5f, at.y - size * .5f, size, size);
            if (!LocalViewport.Overlaps(visual)) return;
            labelSpace.Add(new Rect(at.x - hitRadius - 3, at.y - hitRadius - 3, hitRadius * 2 + 6, hitRadius * 2 + 6));
            viewHandle ??= new GUIStyle(normalBox) { alignment = TextAnchor.MiddleCenter, fontSize = 12,
                padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            activeViewHandle ??= new GUIStyle(selectedBox) { alignment = TextAnchor.MiddleCenter, fontSize = 12,
                padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            GUI.Box(visual, new GUIContent(caption, hint), active ? activeViewHandle : viewHandle);
        }

        const string GizmoHint = "Drag the arrows to move · drag the curve to turn";
        static readonly Color TurnColour = new Color(1, .82f, .3f);

        bool Moving(WorkshopHandle handle) => dragging && activeHandle == handle;

        void HandleSpot(Vector2 at, float radius, string hint)
        {
            var area = new Rect(at.x - radius - 3, at.y - radius - 3, radius * 2 + 6, radius * 2 + 6);
            labelSpace.Add(area);
            GUI.Label(area, new GUIContent("", hint));
        }

        void ArrowHandle(Vector2 from, Vector2 tip, Color colour, bool active)
        {
            if (!LocalViewport.Contains(tip)) return;
            var (left, right) = WordboundWorkshopOverlay.ArrowHead(from, tip, WordboundWorkshopOverlay.ArrowSize);
            var tint = active ? Color.white : colour;
            DrawScreenLine(left, tip, tint); DrawScreenLine(right, tip, tint); DrawScreenLine(left, right, tint);
            HandleSpot(tip, 14, "Drag the arrow to move this way");
        }

        void IconHandle(Vector2 at, string icon, string hint, bool active)
        {
            if (!LocalViewport.Contains(at)) return;
            ViewHandle(at, "", hint, 22, 17, active);
            if (Event.current.type != EventType.Repaint) return;
            var tint = GUI.color;
            try { GUI.color = ui.Ink; GUI.DrawTexture(new Rect(at.x - 8, at.y - 8, 16, 16), LobbyIcons.Get(icon).texture, ScaleMode.ScaleToFit, true); }
            finally { GUI.color = tint; }
        }

        void TurnHandle(Vector2 centre, bool active)
        {
            if (!LocalViewport.Contains(centre)) return;
            var arc = WordboundWorkshopOverlay.TurnArc(centre, WordboundWorkshopOverlay.TurnRadius, 10);
            var tint = active ? Color.white : TurnColour;
            for (int i = 1; i < arc.Length; i++) DrawScreenLine(arc[i - 1], arc[i], tint);
            var (left, right) = WordboundWorkshopOverlay.ArrowHead(arc[arc.Length - 2], arc[arc.Length - 1], 6);
            DrawScreenLine(left, arc[arc.Length - 1], tint); DrawScreenLine(right, arc[arc.Length - 1], tint);
            HandleSpot(centre, 14, "Drag the curve to turn");
        }

        void DrawWorldLine(CoursePoint from, CoursePoint to, Color color)
        {
            if (Event.current.type != EventType.Repaint || !view.ProjectSegment(from, to, viewport, out var a, out var b)) return;
            DrawScreenLine(a, b, color);
        }

        void DrawScreenLine(Vector2 a, Vector2 b, Color color)
        {
            if (Event.current.type != EventType.Repaint || !WordboundWorkshopOverlay.ClipLine(LocalViewport, ref a, ref b)) return;
            var matrix = GUI.matrix; var previous = GUI.color;
            try
            {
                var delta = b - a;
                GUI.color = color;
                GUI.matrix = Matrix4x4.identity;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, a);
                GUI.matrix = matrix * GUI.matrix;
                GUI.DrawTexture(new Rect(a.x, a.y - WordboundWorkshopOverlay.LineWidth * .5f,
                    delta.magnitude, WordboundWorkshopOverlay.LineWidth), Texture2D.whiteTexture);
            }
            finally { GUI.matrix = matrix; GUI.color = previous; }
        }

        string RoomCaption(string id)
        {
            string prefix = History.Current.ActiveLayer + "_";
            return ObjectLabel(id.StartsWith(prefix, StringComparison.Ordinal) ? id.Substring(prefix.Length) : id);
        }

    }
}
