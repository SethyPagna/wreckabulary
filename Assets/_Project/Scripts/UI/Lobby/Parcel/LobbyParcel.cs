using UnityEngine;
using UnityEngine.UI;

namespace Wreckabulary
{
    public static class LobbyParcel
    {
        public const string GrainName = "Kraft grain", TapeName = "Masking tape", StampName = "Postage stamp";
        public static readonly Color Ink = LobbyKit.Hex(0x3B2A1C), TapeColour = LobbyKit.Hex(0xF4E9C9, .88f);
        public static readonly Color Stamp = LobbyKit.Hex(0xD9583F), Kraft = LobbyKit.Hex(0xD9B98B);
        public const float TitleSize = 40f;
        internal const int GrainSize = 128;
        static Texture2D grain;
        static Sprite tape, dashes;

        public static void Box(RectTransform panel, bool taped = true)
        {
            if (!panel) return;
            if (!panel.Find(GrainName)) Grain(panel, .16f);
            if (!taped || panel.Find(TapeName)) return;
            Tape(panel, new Vector2(0f, 1f), new Vector2(34f, -6f), 38f);
            Tape(panel, new Vector2(1f, 1f), new Vector2(-34f, -6f), -38f);
        }

        public static void DressPage(Transform root)
        {
            if (!root) return;
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == "Panel" && rect.GetComponent<Image>()) Box(rect);
            foreach (var label in root.GetComponentsInChildren<TMPro.TMP_Text>())
                if (label.name == "Title" && label.fontSize >= 30f && !label.GetComponentInParent<LobbyShopOfferView>(true)
                    && !string.IsNullOrWhiteSpace(label.text) && label.text.Length <= 16)
                    LobbyTileWord.Replace(label, TitleSize);
        }

        public static void ShippingLabel(RectTransform body)
        {
            if (!body || body.Find("Postcard dashes")) return;
            Dashes(body, new Color(Ink.r, Ink.g, Ink.b, .45f), 6f);
            var code = Barcode(body, "PLAY", new Color(Ink.r, Ink.g, Ink.b, .8f));
            code.Pin(new Vector2(0f, .5f), new Vector2(16f, 0f), new Vector2(22f, 44f));
            code.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var stamp = LobbyKit.Rect(body, "Stamp").Pin(new Vector2(1f, .5f), new Vector2(-14f, 0f), new Vector2(24f, 30f));
            stamp.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            stamp.localRotation = Quaternion.Euler(0f, 0f, -8f);
            var paper = stamp.gameObject.AddComponent<Image>();
            paper.color = LobbyKit.Hex(0xFFF6E8); paper.raycastTarget = false;
            Dashes(stamp, Stamp, 2f);
            var mark = LobbyKit.Display(stamp, "W", 16, Stamp);
            mark.rectTransform.Fill();
            mark.raycastTarget = false;
        }

        public static RawImage Grain(RectTransform rect, float strength)
        {
            var layer = LobbyKit.Rect(rect, GrainName).Place(Vector2.zero, Vector2.one, new Vector2(6, 6), new Vector2(-6, -6));
            layer.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            layer.SetSiblingIndex(0);
            var image = layer.gameObject.AddComponent<RawImage>();
            image.texture = GrainTexture();
            image.color = new Color(1f, 1f, 1f, strength);
            image.raycastTarget = false;
            layer.gameObject.AddComponent<LobbyParcelGrain>();
            return image;
        }

        public static Image Tape(RectTransform rect, Vector2 anchor, Vector2 offset, float degrees, float length = 104f, float width = 30f)
        {
            var strip = LobbyKit.Rect(rect, TapeName).Pin(anchor, offset, new Vector2(length, width));
            strip.pivot = new Vector2(.5f, .5f);
            strip.anchoredPosition = offset;
            strip.localRotation = Quaternion.Euler(0f, 0f, degrees);
            strip.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var image = strip.gameObject.AddComponent<Image>();
            image.sprite = TapeSprite();
            image.color = TapeColour;
            image.raycastTarget = false;
            return image;
        }

        public static Image Dashes(RectTransform rect, Color colour, float inset = 7f)
        {
            var edge = LobbyKit.Rect(rect, "Postcard dashes").Place(Vector2.zero, Vector2.one, new Vector2(inset, inset), new Vector2(-inset, -inset));
            edge.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            var image = edge.gameObject.AddComponent<Image>();
            image.sprite = DashSprite();
            image.type = Image.Type.Tiled;
            image.fillCenter = false;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        public static RectTransform Barcode(Transform parent, string seed, Color colour)
        {
            var root = LobbyKit.Rect(parent, "Barcode");
            uint hash = 2166136261;
            foreach (char c in seed ?? "") hash = (hash ^ c) * 16777619;
            float x = 0f;
            var widths = new System.Collections.Generic.List<(float at, float width)>();
            while (x < 1f)
            {
                hash = hash * 1664525 + 1013904223;
                float bar = .012f + (hash >> 28) * .004f;
                float gap = .01f + ((hash >> 24) & 7) * .003f;
                if (x + bar > 1f) break;
                widths.Add((x, bar));
                x += bar + gap;
            }
            foreach (var (at, width) in widths)
            {
                var bar = LobbyKit.Rect(root, "Bar").Place(new Vector2(at, 0f), new Vector2(at + width, 1f));
                var image = bar.gameObject.AddComponent<Image>();
                image.color = colour; image.raycastTarget = false;
            }
            return root;
        }

        internal static Texture2D GrainTexture()
        {
            if (grain) return grain;
            grain = new Texture2D(GrainSize, GrainSize, TextureFormat.RGBA32, false)
            { name = "Kraft grain", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var alpha = new float[GrainSize * GrainSize];
            var random = new System.Random(4122);
            for (int i = 0; i < alpha.Length; i++) alpha[i] = (float)random.NextDouble() * .18f;
            for (int f = 0; f < 150; f++)
            {
                float x = random.Next(GrainSize), y = random.Next(GrainSize);
                float angle = (float)(random.NextDouble() * Mathf.PI), length = 4 + random.Next(12);
                float strength = .35f + (float)random.NextDouble() * .45f;
                for (float t = 0; t < length; t += .5f)
                {
                    int px = ((int)(x + Mathf.Cos(angle) * t) % GrainSize + GrainSize) % GrainSize;
                    int py = ((int)(y + Mathf.Sin(angle) * t) % GrainSize + GrainSize) % GrainSize;
                    alpha[py * GrainSize + px] = Mathf.Max(alpha[py * GrainSize + px], strength);
                }
            }
            for (int s = 0; s < 70; s++) alpha[random.Next(alpha.Length)] = .9f;
            var pixels = new Color32[alpha.Length];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(0x6B, 0x4A, 0x2A, (byte)(Mathf.Clamp01(alpha[i]) * 255));
            grain.SetPixels32(pixels);
            grain.Apply(false, true);
            return grain;
        }

        static Sprite TapeSprite()
        {
            if (tape) return tape;
            const int W = 96, H = 28;
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false)
            { name = "Masking tape", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[W * H];
            var random = new System.Random(77);
            var left = new int[H]; var right = new int[H];
            for (int y = 0; y < H; y++) { left[y] = 1 + ((y / 3) % 2 == 0 ? random.Next(4) : random.Next(2)); right[y] = W - 2 - ((y / 3) % 2 == 0 ? random.Next(4) : random.Next(2)); }
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    bool inside = x >= left[y] && x <= right[y];
                    float streak = .9f + .1f * Mathf.PerlinNoise(x * .08f, y * .9f);
                    byte a = inside ? (byte)(255 * streak) : (byte)0;
                    pixels[y * W + x] = new Color32(255, 255, 255, a);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return tape = Sprite.Create(texture, new Rect(0, 0, W, H), new Vector2(.5f, .5f), 100f);
        }

        static Sprite DashSprite()
        {
            if (dashes) return dashes;
            const int S = 24, Line = 2, Dash = 8;
            var texture = new Texture2D(S, S, TextureFormat.RGBA32, false)
            { name = "Postcard dashes", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    bool edgeX = x < Line || x >= S - Line, edgeY = y < Line || y >= S - Line;
                    bool dashAlongX = (x % 12) < Dash, dashAlongY = (y % 12) < Dash;
                    bool on = (edgeY && dashAlongX) || (edgeX && dashAlongY);
                    pixels[y * S + x] = new Color32(255, 255, 255, on ? (byte)255 : (byte)0);
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return dashes = Sprite.Create(texture, new Rect(0, 0, S, S), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(Line, Line, Line, Line));
        }
    }
}
