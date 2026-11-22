using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        const string KitBodyFontPath = "Fonts/Nunito-ExtraBold", KitBlackFontPath = "Fonts/Nunito-Black", KitGlyphPath = "UI/Glyphs/";
        const float KitDrop = 2f, KitOutline = 1f, CapPad = 7f, CapMinWidth = 24f, CapHeight = 24f, PartGap = 6f,
            PillPadX = 14f, PillPadY = 6f, PillMinHeight = 36f, PillCorner = 18f;
        const int KitTextureSize = 32, KitSmallRadius = 4, KitLargeRadius = 9, KitRimStroke = 2,
            KitCapFont = 14, KitPromptFont = 16, KitPromptMinFont = 13, KitKeyWordFont = 15;
        static readonly Color HudInk = KitHex(0x173b3c), HudOnGame = KitHex(0xfff7e8), HudShade = KitHex(0x0b1f1f, .8f),
            HudOutline = KitHex(0x0b1f1f, .69f), HudTile = KitHex(0xf4dca7), HudTileEdge = KitHex(0xbba06b),
            HudEmptyCell = KitHex(0xead9b7, .35f), HudCellRim = KitHex(0x1e2a2e, .55f), HudReserved = KitHex(0xc8e6d8),
            HudReservedInk = KitHex(0x2f6b5c), HudGlass = KitHex(0x1d2b2b, .28f), HudGlassActive = KitHex(0x1d2b2b, .40f),
            HudGlassEdge = KitHex(0xfff7e8, .85f), HudGlassIdle = KitHex(0xffffff, .17f), HudGold = KitHex(0xffe7b8),
            HudBrand = KitHex(0xf2d295), HudBarTrack = KitHex(0x0b1f1f, .78f), HudAmber = KitHex(0xffc568), HudCritical = KitHex(0xd16849),
            HudKeyPill = KitHex(0x1e2a2e, .8f), HudTileGot = KitHex(0xe8c48b), HudTileGotInk = KitHex(0x3b2614),
            HudHealth = KitHex(0x419681), HudEmptyHand = KitHex(0xfff7e8, .72f);
        Texture2D kitSmall, kitLarge, kitSmallRing, kitLargeRing;
        GUIStyle kitSmallShape, kitLargeShape, kitSmallRim, kitLargeRim, kitPill;
        GUIStyle kitTitle, kitTag, kitDetail, kitNumber, kitTile, kitCap, kitPrompt, kitPromptWrap, kitKeyWord, kitWord, kitBrand;
        Font kitBodyFont, kitBlackFont;
        bool kitFontsLoaded;
        int kitGeneration;
        readonly Dictionary<string, Texture2D> kitGlyphs = new Dictionary<string, Texture2D>(System.StringComparer.Ordinal);

        static Color KitHex(uint rgb, float alpha = 1f) =>
            new Color((rgb >> 16 & 255) / 255f, (rgb >> 8 & 255) / 255f, (rgb & 255) / 255f, alpha);

        Font KitBody { get { LoadKitFonts(); return kitBodyFont; } }
        Font KitBlack { get { LoadKitFonts(); return kitBlackFont; } }

        void LoadKitFonts()
        {
            if (kitFontsLoaded) return;
            kitFontsLoaded = true;
            kitBodyFont = Resources.Load<Font>(KitBodyFontPath);
            kitBlackFont = Resources.Load<Font>(KitBlackFontPath);
        }

        Texture2D KitGlyph(string name)
        {
            if (!kitGlyphs.TryGetValue(name, out var glyph)) kitGlyphs[name] = glyph = Resources.Load<Texture2D>(KitGlyphPath + name);
            return glyph;
        }

        void PrepareKit()
        {
            if (kitTitle != null && kitSmall && kitLarge && kitSmallRing && kitLargeRing) return;
            DisposeKit();
            kitSmall = KitShape("Course HUD small", KitSmallRadius, 0f);
            kitLarge = KitShape("Course HUD large", KitLargeRadius, 0f);
            kitSmallRing = KitShape("Course HUD small rim", KitSmallRadius, KitRimStroke - .5f);
            kitLargeRing = KitShape("Course HUD large rim", KitLargeRadius, KitRimStroke);
            kitSmallShape = KitSliced(kitSmall, KitSmallRadius + 1);
            kitLargeShape = KitSliced(kitLarge, KitLargeRadius + 2);
            kitSmallRim = KitSliced(kitSmallRing, KitSmallRadius + 1);
            kitLargeRim = KitSliced(kitLargeRing, KitLargeRadius + 2);
            kitPill = KitSliced(KeyCap(), (int)PillCorner);
            kitTitle = KitText(KitBlack, PlateTitleFont, TextAnchor.MiddleLeft);
            kitTag = KitText(KitBlack, PlateTagFont, TextAnchor.MiddleLeft);
            kitDetail = KitText(KitBody, PlateDetailFont, TextAnchor.UpperLeft, true);
            kitNumber = KitText(KitBlack, VitalsNumberFont, TextAnchor.MiddleLeft);
            kitTile = KitText(KitBlack, TrayTileFont, TextAnchor.MiddleCenter);
            kitCap = KitText(KitBlack, KitCapFont, TextAnchor.MiddleCenter);
            kitPrompt = KitText(KitBody, KitPromptFont, TextAnchor.MiddleLeft);
            kitPromptWrap = KitText(KitBody, KitPromptFont, TextAnchor.MiddleCenter, true);
            kitKeyWord = KitText(KitBody, KitKeyWordFont, TextAnchor.MiddleLeft);
            kitWord = KitText(KitBlack, HandWordFont, TextAnchor.MiddleRight);
            kitHandKey = KitText(KitBlack, HandKeyFont, TextAnchor.MiddleCenter);
            kitBrand = KitText(KitBlack, PlateBrandFont, TextAnchor.MiddleCenter);
            kitBrand.richText = true;
            kitGeneration++;
        }

        static GUIStyle KitText(Font font, int size, TextAnchor anchor, bool wrap = false)
        {
            var style = new GUIStyle
            {
                font = font, fontSize = size, alignment = anchor, wordWrap = wrap, richText = false,
                clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0)
            };
            style.normal.textColor = Color.white;
            return style;
        }

        static GUIStyle KitSliced(Texture2D texture, int border)
        {
            var style = new GUIStyle { border = new RectOffset(border, border, border, border) };
            style.normal.background = texture;
            return style;
        }

        static Texture2D KitShape(string name, float radius, float stroke)
        {
            const int size = KitTextureSize;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            { name = name, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float inner = size * .5f - radius;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + .5f - size * .5f) - inner, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + .5f - size * .5f) - inner, 0f);
                    float distance = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    float cover = Mathf.Clamp01(.5f - distance);
                    if (stroke > 0f) cover *= Mathf.Clamp01(.5f + distance + stroke);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        static bool Painting => Event.current != null && Event.current.type == EventType.Repaint;

        static void KitFill(Rect area, GUIStyle shape, Color colour)
        {
            if (!Painting || shape == null || area.width <= 0f || area.height <= 0f) return;
            Color tint = GUI.color, background = GUI.backgroundColor;
            GUI.color = Color.white;
            GUI.backgroundColor = colour;
            shape.Draw(area, false, false, false, false);
            GUI.color = tint;
            GUI.backgroundColor = background;
        }

        static void KitImage(Rect area, Texture texture, Color colour)
        {
            if (!Painting || !texture) return;
            Color tint = GUI.color;
            GUI.color = colour;
            GUI.DrawTexture(area, texture, ScaleMode.ScaleToFit, true);
            GUI.color = tint;
        }

        static void KitPlain(Rect area, GUIContent text, GUIStyle style, Color colour)
        {
            if (!Painting || text == null || string.IsNullOrEmpty(text.text)) return;
            Color tint = GUI.color;
            GUI.color = colour;
            GUI.Label(area, text, style);
            GUI.color = tint;
        }

        static void KitInk(Rect area, GUIContent text, GUIStyle style, Color colour)
        {
            if (!Painting || text == null || string.IsNullOrEmpty(text.text)) return;
            Color tint = GUI.color;
            GUI.color = new Color(HudShade.r, HudShade.g, HudShade.b, HudShade.a * colour.a);
            GUI.Label(new Rect(area.x, area.y + KitDrop, area.width, area.height), text, style);
            GUI.color = new Color(HudOutline.r, HudOutline.g, HudOutline.b, HudOutline.a * colour.a);
            GUI.Label(new Rect(area.x - KitOutline, area.y - KitOutline, area.width, area.height), text, style);
            GUI.Label(new Rect(area.x + KitOutline, area.y - KitOutline, area.width, area.height), text, style);
            GUI.Label(new Rect(area.x - KitOutline, area.y + KitOutline, area.width, area.height), text, style);
            GUI.Label(new Rect(area.x + KitOutline, area.y + KitOutline, area.width, area.height), text, style);
            GUI.color = colour;
            GUI.Label(area, text, style);
            GUI.color = tint;
        }

        float CapWidth(GUIContent key) => Mathf.Max(CapMinWidth, Mathf.Ceil(kitCap.CalcSize(key).x) + CapPad * 2f);

        void PaintCap(Rect area, GUIContent key, bool onGlass)
        {
            KitFill(area, kitLargeShape, onGlass ? HudOnGame : HudKeyPill);
            KitPlain(area, key, kitCap, onGlass ? HudInk : HudOnGame);
        }

        Vector2 MeasurePrompt(KeyPrompt prompt, float maxWidth)
        {
            if (prompt.Count == 0 || kitPrompt == null) return Vector2.zero;
            prompt.Fit(this, kitPrompt, kitPromptWrap, Mathf.Max(1f, maxWidth - PillPadX * 2f));
            return new Vector2(prompt.Width + PillPadX * 2f, Mathf.Max(PillMinHeight, prompt.Height + PillPadY * 2f));
        }

        void PaintPromptAt(KeyPrompt prompt, Rect area)
        {
            if (prompt.Count == 0 || !Painting) return;
            KitFill(area, kitPill, KeyCapFill);
            var text = prompt.Wrapped ? kitPromptWrap : kitPrompt;
            text.fontSize = prompt.Font;
            if (prompt.Wrapped)
            {
                KitPlain(new Rect(area.x + PillPadX, area.y, area.width - PillPadX * 2f, area.height), prompt.Part(0), text, HudOnGame);
                return;
            }
            float x = area.center.x - prompt.Width * .5f, middle = area.center.y;
            for (int i = 0; i < prompt.Count; i++)
            {
                float wide = prompt.PartWidth(i);
                if (prompt.IsCap(i)) PaintCap(new Rect(x, middle - CapHeight * .5f, wide, CapHeight), prompt.Part(i), true);
                else KitPlain(new Rect(x, area.y, wide, area.height), prompt.Part(i), text, HudOnGame);
                x += wide + PartGap;
            }
        }

        float PaintInline(KeyPrompt prompt, float x, float middle)
        {
            if (prompt.Count == 0 || !prompt.HasCaps || kitKeyWord == null) return 0f;
            prompt.Fit(this, kitKeyWord, null, float.MaxValue);
            if (!Painting) return prompt.Width;
            for (int i = 0; i < prompt.Count; i++)
            {
                float wide = prompt.PartWidth(i);
                if (prompt.IsCap(i)) PaintCap(new Rect(x, middle - CapHeight * .5f, wide, CapHeight), prompt.Part(i), false);
                else KitInk(new Rect(x, middle - CapHeight * .5f, wide, CapHeight), prompt.Part(i), kitKeyWord, HudOnGame);
                x += wide + PartGap;
            }
            return prompt.Width;
        }

        void DisposeKit()
        {
            foreach (var texture in new[] { kitSmall, kitLarge, kitSmallRing, kitLargeRing })
                if (texture) { if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture); }
            kitSmall = kitLarge = kitSmallRing = kitLargeRing = null;
            kitTitle = null;
        }

        sealed class KeyPrompt
        {
            readonly List<GUIContent> parts = new List<GUIContent>();
            readonly List<bool> caps = new List<bool>();
            readonly List<float> widths = new List<float>();
            string source;
            int count, fitGeneration = -1;
            float fitRoom = -1f;
            GUIStyle fitStyle;

            public int Count => count;
            public bool HasCaps { get; private set; }
            public bool Wrapped { get; private set; }
            public float Width { get; private set; }
            public float Height { get; private set; }
            public int Font { get; private set; }
            public string Text => source ?? "";
            public GUIContent Part(int index) => parts[index];
            public bool IsCap(int index) => caps[index];
            public float PartWidth(int index) => widths[index];

            public void Set(string text)
            {
                if (string.Equals(text, source, System.StringComparison.Ordinal)) return;
                source = text;
                count = 0;
                HasCaps = false;
                fitGeneration = -1;
                if (string.IsNullOrEmpty(text)) return;
                int at = 0;
                while (at < text.Length)
                {
                    int open = text.IndexOf('[', at);
                    int close = open < 0 || open + 2 > text.Length ? -1 : text.IndexOf(']', open + 2);
                    if (close < 0) { Add(text.Substring(at), false); return; }
                    if (open > at) Add(text.Substring(at, open - at), false);
                    Add(text.Substring(open + 1, close - open - 1), true);
                    at = close + 1;
                }
            }

            void Add(string value, bool cap)
            {
                value = value.Trim();
                if (value.Length == 0) return;
                if (count == parts.Count) { parts.Add(new GUIContent()); caps.Add(false); widths.Add(0f); }
                parts[count].text = value;
                caps[count] = cap;
                HasCaps |= cap;
                count++;
            }

            public void Fit(WordboundMatchHud hud, GUIStyle text, GUIStyle wrap, float room)
            {
                if (fitGeneration == hud.kitGeneration && fitStyle == text && Mathf.Approximately(fitRoom, room)) return;
                fitGeneration = hud.kitGeneration; fitStyle = text; fitRoom = room;
                int size = wrap != null ? KitPromptFont : text.fontSize, smallest = wrap != null ? KitPromptMinFont : size;
                Measure(hud, text, size);
                while (wrap != null && Width > room && size > smallest) Measure(hud, text, --size);
                Wrapped = wrap != null && Width > room && count == 1 && !HasCaps;
                if (!Wrapped) return;
                wrap.fontSize = size;
                Width = room;
                Height = wrap.CalcHeight(parts[0], room);
            }

            void Measure(WordboundMatchHud hud, GUIStyle text, int size)
            {
                text.fontSize = size;
                Font = size;
                float total = 0f, high = 0f;
                for (int i = 0; i < count; i++)
                {
                    if (caps[i]) { widths[i] = hud.CapWidth(parts[i]); high = Mathf.Max(high, CapHeight); }
                    else
                    {
                        var measured = text.CalcSize(parts[i]);
                        widths[i] = Mathf.Ceil(measured.x);
                        high = Mathf.Max(high, measured.y);
                    }
                    total += widths[i] + (i > 0 ? PartGap : 0f);
                }
                Width = total;
                Height = high;
                Wrapped = false;
            }
        }
    }
}
