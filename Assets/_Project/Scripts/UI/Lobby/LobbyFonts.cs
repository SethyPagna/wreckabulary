using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Wreckabulary
{
    /// <summary>
    /// The web lobby's type, made at runtime from the OFL fonts in Resources/Fonts: Lilita One for
    /// tabs, titles, buttons and numbers, Nunito ExtraBold for text and Nunito Black for small caps.
    /// Made at runtime so nothing on disk is re-serialised. Stroke and Drop are shared presets for the
    /// web's navy text stroke and hard text shadow: an outline set on a label makes a material for that
    /// label, and LobbyKit.Clear would leak one on every redraw. Without the font files the lobby falls
    /// back to the game font.
    /// </summary>
    public static class LobbyFonts
    {
        static TMP_FontAsset display, body, black;
        static Material stroke, drop;
        static bool failed;

        public static TMP_FontAsset Display { get { Load(); return display; } }
        public static TMP_FontAsset Body { get { Load(); return body; } }
        public static TMP_FontAsset Black { get { Load(); return black; } }
        /// <summary>Lilita with a navy stroke under the letters.</summary>
        public static Material Stroke { get { Load(); return stroke; } }
        /// <summary>Lilita with the stroke and a hard navy drop below it, for page titles.</summary>
        public static Material Drop { get { Load(); return drop; } }

        static void Load()
        {
            if (display || failed) return;
            display = Make("LilitaOne-Regular", 72, 10);
            body = Make("Nunito-ExtraBold", 56, 7);
            black = Make("Nunito-Black", 56, 7);
            if (!display || !body || !black) { failed = true; display = body = black = null; return; }
            stroke = Preset(display, false);
            drop = Preset(display, true);
        }

        static TMP_FontAsset Make(string file, int size, int padding)
        {
            var font = Resources.Load<Font>("Fonts/" + file);
            if (!font) { Debug.LogWarning("Lobby font missing: Resources/Fonts/" + file); return null; }
            // Room round each glyph for the stroke and the drop, which are drawn from the same distance field.
            var asset = TMP_FontAsset.CreateFontAsset(font, size, padding, GlyphRenderMode.SDFAA, 1024, 1024);
            if (!asset) return null;
            asset.name = file;
            // The lobby's strings use "·" and TMP's ellipsis; the game font has both.
            if (GameAssets.I && GameAssets.I.font) asset.fallbackFontAssetTable = new List<TMP_FontAsset> { GameAssets.I.font };
            return asset;
        }

        static Material Preset(TMP_FontAsset font, bool withDrop)
        {
            var material = new Material(font.material) { name = font.name + (withDrop ? " Drop" : " Stroke") };
            // The mobile shader centres the outline on the letter edge; dilating the face by the same
            // amount puts the whole stroke outside, under the fill, as the web draws it.
            material.EnableKeyword(ShaderUtilities.Keyword_Outline);
            material.SetColor("_OutlineColor", LobbyKit.Navy);
            material.SetFloat("_OutlineWidth", .28f);
            material.SetFloat("_FaceDilate", .28f);
            if (withDrop)
            {
                material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                material.SetColor("_UnderlayColor", LobbyKit.Navy);
                material.SetFloat("_UnderlayOffsetX", 0f);
                material.SetFloat("_UnderlayOffsetY", -.55f);
                material.SetFloat("_UnderlayDilate", .28f);
                material.SetFloat("_UnderlaySoftness", 0f);
            }
            ShaderUtilities.UpdateShaderRatios(material);
            return material;
        }
    }
}
