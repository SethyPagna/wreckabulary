using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wreckabulary
{
    public sealed class WordboundPaperLook : IDisposable
    {
        const string BodyFontPath = "Fonts/Nunito-ExtraBold", DisplayFontPath = "Fonts/LilitaOne-Regular";
        const int TextureSize = 48, ButtonCorner = 12, PanelCorner = 10, FieldCorner = 10, ChipCorner = 6, BarCorner = 5;
        const int Edge = 2, FocusEdge = 3, ButtonSlice = 14, PanelSlice = 12, ChipSlice = 8, BarSlice = 6, PrimaryGrow = 2;
        const int TapeTexelsWide = 96, TapeTexelsHigh = 26, TapeSeed = 77;
        const float PanelGrain = .14f, BackdropGrain = .22f, GrainInset = 6;
        const float TapeLength = 78, TapeThickness = 22, TapeTurn = 38, TapeOverhang = 4;
        const float TickSize = 18, TickInset = 10, TickGap = 8, TickMark = 3, UnderlineHeight = 3, UnderlineInset = 8, RingOutset = 3;
        const float FrameInset = 4;

        readonly List<Texture2D> owned = new List<Texture2D>();
        GUIStyle dressedPanel, ring;
        int dressedGeneration = -1;
        Texture2D tape, disc;

        public GUIStyle Tab { get; private set; }
        public GUIStyle TabOn { get; private set; }
        public GUIStyle Toggle { get; private set; }
        public GUIStyle ToggleOn { get; private set; }
        public GUIStyle Problem { get; private set; }
        public GUIStyle CardFace { get; private set; }
        public bool Dressed => dressedPanel != null;

        public void Dress(WordboundUiStyles ui)
        {
            var skin = GUI.skin;
            if (ui == null || ui.Panel == null || !skin || !ReferenceEquals(skin.box, ui.Panel)) return;
            if (ReferenceEquals(dressedPanel, ui.Panel) && dressedGeneration == ui.Generation) return;
            ReleaseTextures();
            var body = Resources.Load<Font>(BodyFontPath);
            var display = Resources.Load<Font>(DisplayFontPath);
            if (body)
            {
                skin.font = body;
                foreach (var style in new[] { skin.label, skin.button, skin.textField, skin.textArea, skin.box, skin.toggle,
                    ui.Wrapped, ui.SingleLine, ui.Selected, ui.DisabledText }) style.font = body;
            }
            if (display) { ui.Heading.font = display; ui.Primary.font = display; }
            ui.Primary.fontSize = skin.button.fontSize + PrimaryGrow;
            ui.SingleLine.padding = new RectOffset(ui.SingleLine.padding.left, ui.SingleLine.padding.right, 0, 0);
            foreach (var style in new[] { skin.label, ui.Heading, ui.Wrapped, ui.SingleLine }) Tint(style, LobbyKit.Navy);
            DressButtons(skin, ui);
            DressFields(skin);
            DressPanels(skin);
            DressScrollbars(skin);
            skin.settings.cursorColor = LobbyKit.Navy;
            BuildExtras(skin, ui);
            dressedPanel = ui.Panel; dressedGeneration = ui.Generation;
        }

        void DressButtons(GUISkin skin, WordboundUiStyles ui)
        {
            var card = Shape(LobbyKit.Card, LobbyKit.Line, ButtonCorner, Edge);
            var hover = Shape(LobbyKit.Raised, LobbyKit.Line, ButtonCorner, Edge);
            var focus = Shape(LobbyKit.Card, LobbyKit.Focus, ButtonCorner, FocusEdge);
            var press = Shape(LobbyKit.Raised, LobbyKit.Focus, ButtonCorner, FocusEdge);
            var chosen = Shape(LobbyKit.Raised, LobbyKit.Selection, ButtonCorner, Edge);
            var chosenHot = Shape(LobbyKit.Raised, LobbyKit.Selection, ButtonCorner, FocusEdge);
            var primary = Shape(LobbyKit.PrimaryFill, LobbyKit.PrimaryFill, ButtonCorner, 0);
            var primaryHot = Shape(LobbyKit.SunHi, LobbyKit.Navy, ButtonCorner, Edge);
            var off = Shape(LobbyKit.Short, LobbyKit.Short, ButtonCorner, 0);
            Paint(skin.button, ButtonSlice, card, hover, press, focus, chosen, chosenHot, chosenHot, chosenHot, LobbyKit.Navy);
            Paint(ui.Selected, ButtonSlice, chosen, chosenHot, chosenHot, chosenHot, chosen, chosenHot, chosenHot, chosenHot, LobbyKit.Navy);
            Paint(ui.Primary, ButtonSlice, primary, primaryHot, primaryHot, primaryHot, primary, primaryHot, primaryHot, primaryHot, LobbyKit.PrimaryInk);
            Paint(ui.DisabledText, ButtonSlice, off, off, off, off, off, off, off, off, LobbyKit.Faded);
        }

        void DressFields(GUISkin skin)
        {
            var field = Shape(LobbyKit.Card, LobbyKit.Line, FieldCorner, Edge);
            var focus = Shape(LobbyKit.Card, LobbyKit.Focus, FieldCorner, FocusEdge);
            foreach (var style in new[] { skin.textField, skin.textArea })
                Paint(style, ButtonSlice, field, field, focus, focus, field, field, focus, focus, LobbyKit.Navy);
        }

        void DressPanels(GUISkin skin)
        {
            var sheet = Shape(LobbyKit.Panel, LobbyKit.Line, PanelCorner, Edge);
            Paint(skin.box, PanelSlice, sheet, sheet, sheet, sheet, sheet, sheet, sheet, sheet, LobbyKit.Navy);
        }

        void DressScrollbars(GUISkin skin)
        {
            var track = Shape(LobbyKit.Raised, LobbyKit.Raised, BarCorner, 0);
            var thumb = Shape(LobbyKit.Line, LobbyKit.Line, BarCorner, 0);
            foreach (var style in new[] { skin.verticalScrollbar, skin.horizontalScrollbar })
                Paint(style, BarSlice, track, track, track, track, track, track, track, track, LobbyKit.Navy);
            foreach (var style in new[] { skin.verticalScrollbarThumb, skin.horizontalScrollbarThumb })
                Paint(style, BarSlice, thumb, thumb, thumb, thumb, thumb, thumb, thumb, thumb, LobbyKit.Navy);
        }

        void BuildExtras(GUISkin skin, WordboundUiStyles ui)
        {
            var hot = Shape(LobbyKit.Raised, LobbyKit.Raised, ChipCorner, 0);
            Tab = new GUIStyle(skin.button) { wordWrap = false, clipping = TextClipping.Overflow, alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(6, 6, 4, 6) };
            Paint(Tab, ChipSlice, null, hot, hot, null, null, hot, hot, null, LobbyKit.Navy);
            TabOn = new GUIStyle(Tab);
            foreach (var state in States(TabOn)) state.textColor = LobbyKit.Selection;
            int indent = Mathf.RoundToInt(TickInset + TickSize + TickGap);
            Toggle = new GUIStyle(skin.button);
            Toggle.padding = new RectOffset(indent, Toggle.padding.right, Toggle.padding.top, Toggle.padding.bottom);
            ToggleOn = new GUIStyle(ui.Selected);
            ToggleOn.padding = new RectOffset(indent, ToggleOn.padding.right, ToggleOn.padding.top, ToggleOn.padding.bottom);
            Problem = new GUIStyle(ui.Wrapped);
            Tint(Problem, LobbyKit.Hot);
            ring = new GUIStyle();
            Paint(ring, ButtonSlice, Shape(Color.clear, LobbyKit.Focus, ButtonCorner, FocusEdge), null, null, null, null, null, null, null, LobbyKit.Navy);
            CardFace = new GUIStyle();
            Paint(CardFace, ChipSlice, Shape(LobbyKit.Card, LobbyKit.Line, ChipCorner, Edge), null, null, null, null, null, null, null, LobbyKit.Navy);
            tape = TapeTexture();
            disc = Shape(Color.white, Color.white, TextureSize / 2, 0);
        }

        public void Backdrop(Rect screen)
        {
            if (Event.current.type != EventType.Repaint) return;
            var tint = GUI.color;
            try
            {
                GUI.color = LobbyParcel.Kraft;
                GUI.DrawTexture(screen, Texture2D.whiteTexture, ScaleMode.StretchToFill, false);
                Grain(screen, BackdropGrain);
            }
            finally { GUI.color = tint; }
        }

        public void Sheet(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            var inner = new Rect(rect.x + GrainInset, rect.y + GrainInset, rect.width - GrainInset * 2, rect.height - GrainInset * 2);
            if (inner.width <= 0 || inner.height <= 0) return;
            var tint = GUI.color;
            try { Grain(inner, PanelGrain); }
            finally { GUI.color = tint; }
        }

        void Grain(Rect rect, float strength)
        {
            var grain = LobbyParcel.GrainTexture();
            if (!grain) return;
            GUI.color = new Color(1, 1, 1, strength);
            GUI.DrawTextureWithTexCoords(rect, grain,
                new Rect(0, 0, rect.width / LobbyParcel.GrainSize, rect.height / LobbyParcel.GrainSize), true);
        }

        public void Tape(Rect card)
        {
            if (Event.current.type != EventType.Repaint || !tape) return;
            Strip(new Vector2(card.xMin + TapeOverhang, card.yMin + TapeOverhang), -TapeTurn);
            Strip(new Vector2(card.xMax - TapeOverhang, card.yMin + TapeOverhang), TapeTurn);
        }

        void Strip(Vector2 centre, float degrees)
        {
            var tint = GUI.color; var matrix = GUI.matrix;
            try
            {
                GUI.matrix = Matrix4x4.identity; GUIUtility.RotateAroundPivot(degrees, centre); GUI.matrix = matrix * GUI.matrix;
                GUI.color = LobbyParcel.TapeColour;
                GUI.DrawTexture(new Rect(centre.x - TapeLength * .5f, centre.y - TapeThickness * .5f, TapeLength, TapeThickness),
                    tape, ScaleMode.StretchToFill, true);
            }
            finally { GUI.color = tint; GUI.matrix = matrix; }
        }

        public void TickBox(Rect button, bool on)
        {
            if (Event.current.type != EventType.Repaint) return;
            var box = new Rect(button.x + TickInset, button.center.y - TickSize * .5f, TickSize, TickSize);
            var tint = GUI.color;
            try
            {
                GUI.color = on ? LobbyKit.Selection : LobbyKit.Muted;
                GUI.DrawTexture(box, Texture2D.whiteTexture);
                if (!on)
                {
                    GUI.color = LobbyKit.Card;
                    GUI.DrawTexture(new Rect(box.x + Edge, box.y + Edge, box.width - Edge * 2, box.height - Edge * 2), Texture2D.whiteTexture);
                    return;
                }
                var check = LobbyIcons.Get(LobbyIcons.Check);
                GUI.color = LobbyKit.OnSelection;
                if (check && check.texture)
                    GUI.DrawTexture(new Rect(box.x + TickMark, box.y + TickMark, box.width - TickMark * 2, box.height - TickMark * 2),
                        check.texture, ScaleMode.ScaleToFit, true);
            }
            finally { GUI.color = tint; }
        }

        public void FocusRing(Rect rect)
        {
            if (Event.current.type != EventType.Repaint || ring == null) return;
            ring.Draw(new Rect(rect.x - RingOutset, rect.y - RingOutset, rect.width + RingOutset * 2, rect.height + RingOutset * 2),
                false, false, false, false);
        }

        public void Underline(Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;
            var tint = GUI.color;
            try
            {
                GUI.color = LobbyKit.Selection;
                GUI.DrawTexture(new Rect(rect.x + UnderlineInset, rect.yMax - UnderlineHeight, Mathf.Max(1, rect.width - UnderlineInset * 2),
                    UnderlineHeight), Texture2D.whiteTexture);
            }
            finally { GUI.color = tint; }
        }

        public void Dot(Rect rect, Color colour)
        {
            if (Event.current.type != EventType.Repaint || !disc) return;
            var tint = GUI.color;
            try { GUI.color = colour; GUI.DrawTexture(rect, disc, ScaleMode.StretchToFill, true); }
            finally { GUI.color = tint; }
        }

        public void Picture(Rect rect, Texture texture, Rect uv, bool crop, float alpha = 1)
        {
            if (Event.current.type != EventType.Repaint || !texture) return;
            CardFace?.Draw(rect, false, false, false, false);
            var inner = new Rect(rect.x + FrameInset, rect.y + FrameInset, rect.width - FrameInset * 2, rect.height - FrameInset * 2);
            if (inner.width <= 0 || inner.height <= 0) return;
            float source = uv.width * texture.width / Mathf.Max(1f, uv.height * texture.height), target = inner.width / inner.height;
            if (crop && source > target) { float keep = uv.width * target / source; uv = new Rect(uv.x + (uv.width - keep) * .5f, uv.y, keep, uv.height); }
            else if (crop && source < target) { float keep = uv.height * source / target; uv = new Rect(uv.x, uv.y + (uv.height - keep) * .5f, uv.width, keep); }
            else if (!crop)
            {
                var size = source > target ? new Vector2(inner.width, inner.width / source) : new Vector2(inner.height * source, inner.height);
                inner = new Rect(inner.center - size * .5f, size);
            }
            var tint = GUI.color;
            try { GUI.color = new Color(1, 1, 1, alpha); GUI.DrawTextureWithTexCoords(inner, texture, uv, true); }
            finally { GUI.color = tint; }
        }

        static void Tint(GUIStyle style, Color colour)
        {
            foreach (var state in States(style)) state.textColor = colour;
        }

        static void Paint(GUIStyle style, int slice, Texture2D normal, Texture2D hover, Texture2D active, Texture2D focused,
            Texture2D onNormal, Texture2D onHover, Texture2D onActive, Texture2D onFocused, Color text)
        {
            style.border = new RectOffset(slice, slice, slice, slice);
            Set(style.normal, normal, text); Set(style.hover, hover, text); Set(style.active, active, text); Set(style.focused, focused, text);
            Set(style.onNormal, onNormal, text); Set(style.onHover, onHover, text); Set(style.onActive, onActive, text); Set(style.onFocused, onFocused, text);
        }

        static void Set(GUIStyleState state, Texture2D background, Color text)
        {
            state.background = background;
#if UNITY_EDITOR
            state.scaledBackgrounds = null;
#endif
            state.textColor = text;
        }

        static GUIStyleState[] States(GUIStyle style) => new[] { style.normal, style.hover, style.active, style.focused,
            style.onNormal, style.onHover, style.onActive, style.onFocused };

        Texture2D Shape(Color fill, Color edge, int radius, int edgeWidth)
        {
            var texture = Owned(TextureSize, TextureSize, "Wordbound paper shape");
            var pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
                for (int x = 0; x < TextureSize; x++)
                {
                    float px = Mathf.Min(x + .5f, TextureSize - x - .5f), py = Mathf.Min(y + .5f, TextureSize - y - .5f);
                    float inset = Mathf.Min(px, py);
                    if (radius > 0 && px < radius && py < radius)
                        inset = radius - Mathf.Sqrt((radius - px) * (radius - px) + (radius - py) * (radius - py));
                    Color value = edgeWidth > 0 ? Color.Lerp(edge, fill, Mathf.Clamp01(inset - edgeWidth + .5f)) : fill;
                    value.a *= Mathf.Clamp01(inset + .5f);
                    pixels[y * TextureSize + x] = value;
                }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }

        Texture2D TapeTexture()
        {
            var texture = Owned(TapeTexelsWide, TapeTexelsHigh, "Wordbound paper tape");
            var pixels = new Color32[TapeTexelsWide * TapeTexelsHigh];
            var random = new System.Random(TapeSeed);
            for (int y = 0; y < TapeTexelsHigh; y++)
            {
                int left = 1 + random.Next(4), right = TapeTexelsWide - 2 - random.Next(4);
                for (int x = 0; x < TapeTexelsWide; x++)
                {
                    float streak = .88f + .12f * Mathf.PerlinNoise(x * .08f, y * .9f);
                    byte alpha = x >= left && x <= right ? (byte)(255 * streak) : (byte)0;
                    pixels[y * TapeTexelsWide + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixels); texture.Apply(false, true);
            return texture;
        }

        Texture2D Owned(int width, int height, string name)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = name, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            owned.Add(texture);
            return texture;
        }

        void ReleaseTextures()
        {
            foreach (var texture in owned)
                if (texture) { if (Application.isPlaying) Object.Destroy(texture); else Object.DestroyImmediate(texture); }
            owned.Clear(); tape = disc = null;
        }

        public void Dispose()
        {
            ReleaseTextures();
            dressedPanel = ring = null; dressedGeneration = -1;
            Tab = TabOn = Toggle = ToggleOn = Problem = CardFace = null;
        }
    }
}
