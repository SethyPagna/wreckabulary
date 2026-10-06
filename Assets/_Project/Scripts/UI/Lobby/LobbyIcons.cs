using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// The lobby's flat glyphs (home, settings, power, trophy, cart, coin, close), drawn once
    /// from signed-distance shapes into white alpha textures. UI images tint them, so the
    /// project needs no icon files and every glyph stays crisp at its drawn size.
    /// </summary>
    public static class LobbyIcons
    {
        public const string Home = "home", Settings = "settings", Power = "power", Trophy = "trophy",
            Cart = "cart", Coin = "coin", Close = "close", Turn = "turn";
        public static readonly string[] All = { Home, Settings, Power, Trophy, Cart, Coin, Close, Turn };
        const int Size = 128;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string name)
        {
            if (cache.TryGetValue(name, out var sprite) && sprite) return sprite;
            Func<Vector2, float> shape = Shape(name);
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            { name = "Lobby icon " + name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            float pixel = 2f / Size;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    // Pixel centre in [-1, 1] with y up; one pixel of soft edge.
                    var p = new Vector2((x + .5f) * pixel - 1f, (y + .5f) * pixel - 1f);
                    float cover = Mathf.Clamp01(.5f - shape(p) / pixel);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(.5f, .5f), Size);
            sprite.name = texture.name;
            cache[name] = sprite;
            return sprite;
        }

        static Sprite rounded;

        /// <summary>A white rounded square for nine-sliced panels and buttons (corner radius 10 px).</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded) return rounded;
                const int size = 48, radius = 10;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { name = "Lobby rounded", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        var p = new Vector2(x + .5f, y + .5f);
                        float d = Box(p, new Vector2(size / 2f, size / 2f), new Vector2(size / 2f, size / 2f), radius);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(.5f - d) * 255));
                    }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                rounded = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100, 0,
                    SpriteMeshType.FullRect, new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
                rounded.name = texture.name;
                return rounded;
            }
        }

        static Func<Vector2, float> Shape(string name)
        {
            switch (name)
            {
                case Home:
                    return p => Cut(Union(Polygon(p, new Vector2(-.92f, .02f), new Vector2(0, .86f), new Vector2(.92f, .02f)),
                            Box(p, new Vector2(0, -.38f), new Vector2(.62f, .44f), .04f)),
                        Box(p, new Vector2(0, -.52f), new Vector2(.17f, .3f), .05f));
                case Settings:
                    return p =>
                    {
                        float gear = Circle(p, Vector2.zero, .6f);
                        for (int i = 0; i < 8; i++)
                        {
                            float a = i * Mathf.PI / 4f;
                            var local = Rotate(p, -a);
                            gear = Union(gear, Box(local, new Vector2(0, .7f), new Vector2(.15f, .2f), .04f));
                        }
                        return Cut(gear, Circle(p, Vector2.zero, .27f));
                    };
                case Power:
                    return p =>
                    {
                        float ring = Mathf.Abs(Circle(p, new Vector2(0, -.08f), .62f)) - .11f;
                        // Open the ring at the top where the stroke passes through.
                        float gap = Polygon(p, Vector2.zero, new Vector2(-.62f, 1.05f), new Vector2(.62f, 1.05f));
                        return Union(Cut(ring, gap), Segment(p, new Vector2(0, .1f), new Vector2(0, .86f)) - .11f);
                    };
                case Trophy:
                    return p =>
                    {
                        float cup = Union(Box(p, new Vector2(0, .52f), new Vector2(.46f, .3f), .02f),
                            Cut(Circle(p, new Vector2(0, .24f), .46f), Box(p, new Vector2(0, .7f), new Vector2(1f, .46f), 0)));
                        float handles = Union(Mathf.Abs(Circle(p, new Vector2(-.5f, .44f), .22f)) - .07f,
                            Mathf.Abs(Circle(p, new Vector2(.5f, .44f), .22f)) - .07f);
                        handles = Cut(handles, Box(p, Vector2.zero, new Vector2(.44f, 1f), 0));
                        float stem = Box(p, new Vector2(0, -.36f), new Vector2(.1f, .2f), .02f);
                        float foot = Box(p, new Vector2(0, -.7f), new Vector2(.4f, .11f), .05f);
                        return Union(Union(cup, handles), Union(stem, foot));
                    };
                case Cart:
                    return p =>
                    {
                        float basket = Polygon(p, new Vector2(-.52f, .42f), new Vector2(.86f, .42f), new Vector2(.62f, -.22f), new Vector2(-.36f, -.22f));
                        float handle = Union(Segment(p, new Vector2(-.52f, .42f), new Vector2(-.72f, .74f)) - .08f,
                            Segment(p, new Vector2(-.72f, .74f), new Vector2(-.95f, .74f)) - .08f);
                        float rail = Segment(p, new Vector2(-.4f, -.4f), new Vector2(.68f, -.4f)) - .07f;
                        float wheels = Union(Circle(p, new Vector2(-.3f, -.68f), .15f), Circle(p, new Vector2(.56f, -.68f), .15f));
                        return Union(Union(basket, handle), Union(rail, wheels));
                    };
                case Coin:
                    return p => Cut(Circle(p, Vector2.zero, .9f), Mathf.Abs(Circle(p, Vector2.zero, .64f)) - .07f);
                case Close:
                    return p => Union(Segment(p, new Vector2(-.6f, -.6f), new Vector2(.6f, .6f)) - .12f,
                        Segment(p, new Vector2(-.6f, .6f), new Vector2(.6f, -.6f)) - .12f);
                case Turn:
                    return p =>
                    {
                        // A flat ellipse with arrow heads at both ends: "drag to turn".
                        var squashed = new Vector2(p.x, p.y * 2.6f);
                        float arc = (Mathf.Abs(Circle(squashed, Vector2.zero, .8f)) - .16f) / 2.6f;
                        arc = Cut(arc, Box(p, new Vector2(0, .3f), new Vector2(.42f, .3f), 0));
                        float left = Polygon(p, new Vector2(-.62f, .02f), new Vector2(-.22f, .2f), new Vector2(-.28f, -.2f));
                        float right = Polygon(p, new Vector2(.62f, .02f), new Vector2(.22f, .2f), new Vector2(.28f, -.2f));
                        return Union(arc, Union(left, right));
                    };
                default:
                    throw new ArgumentException("No lobby icon called " + name, nameof(name));
            }
        }

        static float Union(float a, float b) => Mathf.Min(a, b);
        static float Cut(float a, float b) => Mathf.Max(a, -b);
        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static Vector2 Rotate(Vector2 p, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(c * p.x - s * p.y, s * p.x + c * p.y);
        }

        static float Box(Vector2 p, Vector2 c, Vector2 half, float round)
        {
            var d = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * round;
            return new Vector2(Mathf.Max(d.x, 0), Mathf.Max(d.y, 0)).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0) - round;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Signed distance to a simple polygon (negative inside).</summary>
        static float Polygon(Vector2 p, params Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i];
                var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
    }
}
