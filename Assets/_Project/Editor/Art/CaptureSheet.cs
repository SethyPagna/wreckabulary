using System;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public sealed class CaptureSheet
    {
        public const int GlyphWidth = 3, GlyphHeight = 5, GlyphAdvance = GlyphWidth + 1;
        const ushort Dash = 0b000_000_111_000_000;
        static readonly ushort[] Letters =
        {
            0b010_101_111_101_101, 0b110_101_110_101_110, 0b011_100_100_100_011, 0b110_101_101_101_110, 0b111_100_110_100_111,
            0b111_100_110_100_100, 0b011_100_101_101_011, 0b101_101_111_101_101, 0b111_010_010_010_111, 0b001_001_001_101_010,
            0b101_101_110_101_101, 0b100_100_100_100_111, 0b101_111_111_101_101, 0b110_101_101_101_101, 0b010_101_101_101_010,
            0b110_101_110_100_100, 0b010_101_101_110_011, 0b110_101_110_101_101, 0b011_100_010_001_110, 0b111_010_010_010_010,
            0b101_101_101_101_111, 0b101_101_101_101_010, 0b101_101_111_111_101, 0b101_101_010_101_101, 0b101_101_010_010_010,
            0b111_001_010_100_111
        };
        static readonly ushort[] Digits =
        {
            0b111_101_101_101_111, 0b010_110_010_010_111, 0b110_001_010_100_111, 0b110_001_010_001_110, 0b101_101_111_001_001,
            0b111_100_110_001_110, 0b011_100_110_101_010, 0b111_001_010_010_010, 0b010_101_010_101_010, 0b010_101_011_001_110
        };

        readonly Color32[] pixels;
        public int Width { get; }
        public int Height { get; }

        public CaptureSheet(int width, int height, Color32 background)
        {
            if (width <= 0 || height <= 0) throw new ArgumentException("A sheet needs a positive size.");
            Width = width; Height = height;
            pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = background;
        }

        public void Paste(Color32[] image, int imageWidth, int imageHeight, int left, int top)
        {
            if (image == null || image.Length != imageWidth * imageHeight) throw new ArgumentException("The pasted picture does not match its size.");
            for (int y = 0; y < imageHeight; y++)
            {
                int row = Height - top - imageHeight + y;
                if (row < 0 || row >= Height) continue;
                for (int x = 0; x < imageWidth; x++)
                {
                    int column = left + x;
                    if (column < 0 || column >= Width) continue;
                    var over = image[y * imageWidth + x];
                    var under = pixels[row * Width + column];
                    int a = over.a, rest = 255 - a;
                    pixels[row * Width + column] = new Color32((byte)((over.r * a + under.r * rest) / 255), (byte)((over.g * a + under.g * rest) / 255),
                        (byte)((over.b * a + under.b * rest) / 255), 255);
                }
            }
        }

        public void Label(string text, int left, int top, int scale, Color32 colour)
        {
            if (string.IsNullOrEmpty(text) || scale <= 0) return;
            for (int i = 0; i < text.Length; i++)
            {
                ushort glyph = Glyph(char.ToUpperInvariant(text[i]));
                for (int gy = 0; gy < GlyphHeight; gy++)
                    for (int gx = 0; gx < GlyphWidth; gx++)
                    {
                        int bit = (GlyphHeight - 1 - gy) * GlyphWidth + (GlyphWidth - 1 - gx);
                        if ((glyph >> bit & 1) == 0) continue;
                        Fill(left + (i * GlyphAdvance + gx) * scale, top + gy * scale, scale, scale, colour);
                    }
            }
        }

        public void Fill(int left, int top, int width, int height, Color32 colour)
        {
            for (int y = 0; y < height; y++)
            {
                int row = Height - 1 - (top + y);
                if (row < 0 || row >= Height) continue;
                for (int x = 0; x < width; x++)
                {
                    int column = left + x;
                    if (column >= 0 && column < Width) pixels[row * Width + column] = colour;
                }
            }
        }

        public void Save(string path)
        {
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
                texture.SetPixels32(pixels); texture.Apply(false);
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { if (texture) Object.DestroyImmediate(texture); }
        }

        public static Color32[] Decode(byte[] png, out int width, out int height)
        {
            Texture2D texture = null;
            try
            {
                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
                if (png == null || !texture.LoadImage(png)) throw new InvalidOperationException("The picture could not be decoded.");
                width = texture.width; height = texture.height;
                return texture.GetPixels32();
            }
            finally { if (texture) Object.DestroyImmediate(texture); }
        }

        public static Color32[] Resize(Color32[] source, int sourceWidth, int sourceHeight, int width, int height)
        {
            if (source == null || source.Length != sourceWidth * sourceHeight || width <= 0 || height <= 0) throw new ArgumentException("The picture does not match its size.");
            var result = new Color32[width * height];
            float stepX = sourceWidth / (float)width, stepY = sourceHeight / (float)height;
            for (int y = 0; y < height; y++)
            {
                int y0 = Mathf.Min(sourceHeight - 1, Mathf.FloorToInt(y * stepY)), y1 = Mathf.Max(y0 + 1, Mathf.Min(sourceHeight, Mathf.FloorToInt((y + 1) * stepY)));
                for (int x = 0; x < width; x++)
                {
                    int x0 = Mathf.Min(sourceWidth - 1, Mathf.FloorToInt(x * stepX)), x1 = Mathf.Max(x0 + 1, Mathf.Min(sourceWidth, Mathf.FloorToInt((x + 1) * stepX)));
                    long r = 0, g = 0, b = 0, a = 0; int count = 0;
                    for (int sy = y0; sy < y1; sy++)
                        for (int sx = x0; sx < x1; sx++)
                        {
                            var c = source[sy * sourceWidth + sx];
                            r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a; count++;
                        }
                    result[y * width + x] = a == 0 ? new Color32(0, 0, 0, 0)
                        : new Color32((byte)(r / a), (byte)(g / a), (byte)(b / a), (byte)(a / count));
                }
            }
            return result;
        }

        static ushort Glyph(char symbol)
        {
            if (symbol >= 'A' && symbol <= 'Z') return Letters[symbol - 'A'];
            if (symbol >= '0' && symbol <= '9') return Digits[symbol - '0'];
            return symbol == '-' ? Dash : (ushort)0;
        }
    }
}
