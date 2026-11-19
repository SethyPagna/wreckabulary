using UnityEngine;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        const float EyeSize = 44f, EyeIcon = 28f, EyeShadow = 2f, KeyCapWidth = 56f, KeyCapHeight = 22f, KeyCapGap = 4f,
            KeyFontMax = 15f, KeyFontMin = 10f, KeyTextWidth = 52f, EyeMargin = 18f;
        static readonly Color EyeGlass = new Color32(0x1d, 0x2b, 0x2b, 71), EyeGlassHeld = new Color32(0x1d, 0x2b, 0x2b, 102),
            EyeInk = new Color32(0xff, 0xf7, 0xe8, 255), EyeShade = new Color32(0x0b, 0x1f, 0x1f, 140),
            KeyCapFill = new Color32(0x1e, 0x2a, 0x2e, 204);
        readonly GUIContent eyeKey = new GUIContent();
        GUIStyle eyeKeyStyle;
        Texture2D eyeDisc, keyCap;
        int eyeControl, eyeStyleGeneration = -1;
        float eyeUnit, eyeKeyUnit;
        string eyeKeyText;
        bool eyeHeld;
        Rect eyeRect, keyCapRect;

        static float HouseScale(float pixelsWide, float pixelsHigh) => Mathf.Sqrt(pixelsWide / 1920f * (pixelsHigh / 1080f));

        bool LookEyeShown => initialized && view && !view.MenuOpen && !Application.isMobilePlatform;

        void DrawLookEye(float width)
        {
            if (!LookEyeShown) { ReleaseEye(); return; }
            float scale = Screen.width / Mathf.Max(1f, width);
            PlaceEye(width, HouseScale(Screen.width, Screen.height) / Mathf.Max(.01f, scale));
            eyeControl = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            switch (e.GetTypeForControl(eyeControl))
            {
                case EventType.MouseDown:
                    if (e.button != 0 || !PressEye(e.mousePosition)) break;
                    GUIUtility.hotControl = eyeControl;
                    e.Use();
                    break;
                case EventType.MouseDrag:
                    if (!eyeHeld || GUIUtility.hotControl != eyeControl) break;
                    DragEye(new Vector2(e.delta.x, -e.delta.y) * scale);
                    e.Use();
                    break;
                case EventType.MouseUp:
                    if (!eyeHeld || GUIUtility.hotControl != eyeControl) break;
                    ReleaseEye();
                    e.Use();
                    break;
                case EventType.Repaint:
                    PaintEye(eyeUnit);
                    break;
            }
        }

        void PlaceEye(float width, float unit)
        {
            eyeUnit = unit;
            float size = EyeSize * unit;
            eyeRect = new Rect(width - EyeMargin - size, EyeMargin, size, size);
            keyCapRect = new Rect(eyeRect.center.x - KeyCapWidth * unit * .5f, eyeRect.yMax + KeyCapGap * unit,
                KeyCapWidth * unit, KeyCapHeight * unit);
        }

        bool PressEye(Vector2 at)
        {
            if (eyeHeld || !LookEyeShown || !eyeRect.Contains(at)) return false;
            eyeHeld = true;
            view.BeginLook();
            return true;
        }

        void DragEye(Vector2 screenPixels)
        {
            if (eyeHeld && view) view.OrbitDrag(screenPixels);
        }

        void PaintEye(float unit)
        {
            var color = GUI.color;
            try
            {
                GUI.color = eyeHeld ? EyeGlassHeld : EyeGlass;
                GUI.DrawTexture(eyeRect, EyeDisc());
                var icon = LobbyIcons.Get(LobbyIcons.Eye).texture;
                float side = EyeIcon * unit;
                var at = new Rect(eyeRect.center.x - side * .5f, eyeRect.center.y - side * .5f, side, side);
                GUI.color = EyeShade;
                GUI.DrawTexture(new Rect(at.x, at.y + EyeShadow * unit, side, side), icon);
                GUI.color = EyeInk;
                GUI.DrawTexture(at, icon);
                eyeKey.text = view.LookAroundHint;
                if (eyeKey.text.Length == 0) return;
                GUI.color = KeyCapFill;
                GUI.DrawTexture(keyCapRect, KeyCap());
                GUI.color = Color.white;
                GUI.Label(keyCapRect, eyeKey, KeyStyle(unit));
            }
            finally { GUI.color = color; }
        }

        GUIStyle KeyStyle(float unit)
        {
            if (eyeKeyStyle == null || eyeStyleGeneration != ui.Generation)
            {
                eyeKeyStyle = new GUIStyle(ui.SingleLine) { alignment = TextAnchor.MiddleCenter, wordWrap = false,
                    clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
                eyeKeyStyle.normal.textColor = EyeInk;
                eyeStyleGeneration = ui.Generation;
                eyeKeyText = null;
            }
            if (eyeKeyText == eyeKey.text && eyeKeyUnit == unit) return eyeKeyStyle;
            eyeKeyText = eyeKey.text; eyeKeyUnit = unit;
            int font = Mathf.Max(1, Mathf.RoundToInt(KeyFontMax * unit)), smallest = Mathf.Max(1, Mathf.RoundToInt(KeyFontMin * unit));
            eyeKeyStyle.fontSize = font;
            while (font > smallest && eyeKeyStyle.CalcSize(eyeKey).x > KeyTextWidth * unit) eyeKeyStyle.fontSize = --font;
            return eyeKeyStyle;
        }

        void ReleaseEye()
        {
            if (!eyeHeld) return;
            eyeHeld = false;
            if (eyeControl != 0 && GUIUtility.hotControl == eyeControl) GUIUtility.hotControl = 0;
            if (view) view.EndLook();
        }

        Texture2D EyeDisc()
        {
            if (!eyeDisc) eyeDisc = Rounded("Course eye", 64, 64, 31f);
            return eyeDisc;
        }

        Texture2D KeyCap()
        {
            if (!keyCap) keyCap = Rounded("Course eye key", 112, 44, 17f);
            return keyCap;
        }

        static Texture2D Rounded(string name, int wide, int high, float radius)
        {
            var texture = new Texture2D(wide, high, TextureFormat.RGBA32, false)
            { name = name, hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[wide * high];
            float innerX = wide * .5f - 1f - radius, innerY = high * .5f - 1f - radius;
            for (int y = 0; y < high; y++)
                for (int x = 0; x < wide; x++)
                {
                    float dx = Mathf.Max(Mathf.Abs(x + .5f - wide * .5f) - innerX, 0f);
                    float dy = Mathf.Max(Mathf.Abs(y + .5f - high * .5f) - innerY, 0f);
                    float cover = Mathf.Clamp01(.5f - (Mathf.Sqrt(dx * dx + dy * dy) - radius));
                    pixels[y * wide + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        void DisposeLookEye()
        {
            foreach (var texture in new[] { eyeDisc, keyCap })
                if (texture) { if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture); }
            eyeDisc = keyCap = null;
        }
    }
}
