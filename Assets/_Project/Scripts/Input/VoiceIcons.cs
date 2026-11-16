using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    public static class VoiceIcons
    {
        public const string Mic = "voice-mic", MicOff = "voice-mic-off", Speaker = "voice-speaker", SpeakerOff = "voice-speaker-off",
            Wave = "voice-wave";
        public static readonly string[] All = { Mic, MicOff, Speaker, SpeakerOff, Wave };
        const int Size = 96;
        const float Half = 2.2f / 24f;
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string name)
        {
            if (cache.TryGetValue(name, out var texture) && texture) return texture;
            var shape = Shape(name);
            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            { name = "Voice icon " + name, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[Size * Size];
            float pixel = 2f / Size;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                {
                    var p = new Vector2((x + .5f) * pixel - 1f, (y + .5f) * pixel - 1f);
                    float cover = Mathf.Clamp01(.5f - shape(p) / pixel);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            cache[name] = texture;
            return texture;
        }

        public static string MicIcon(bool off) => off ? MicOff : Mic;
        public static string SpeakerIcon(bool off) => off ? SpeakerOff : Speaker;

        static Func<Vector2, float> Shape(string name) => name switch
        {
            Mic => Microphone,
            MicOff => p => Crossed(Microphone, p),
            Speaker => Loudspeaker,
            SpeakerOff => p => Crossed(Loudspeaker, p),
            Wave => p => Mathf.Min(Mathf.Min(Segment(p, G(5.5f, 9.5f), G(5.5f, 14.5f)), Segment(p, G(12, 5)
                , G(12, 19))), Segment(p, G(18.5f, 9.5f), G(18.5f, 14.5f))) - 1.6f * Half,
            _ => throw new ArgumentException("No voice icon called " + name, nameof(name)),
        };

        static float Microphone(Vector2 p) => Mathf.Min(Box(p, G(12, 8.5f), new Vector2(3.4f / 12f, 5.5f / 12f), 3.4f / 12f),
            Mathf.Min(Arc(p, G(12, 10.5f), 6.6f / 12f, 180f, 360f), Mathf.Min(Segment(p, G(12, 17.2f), G(12, 20.5f)),
                Segment(p, G(8.2f, 20.5f), G(15.8f, 20.5f)))) - Half);

        static float Loudspeaker(Vector2 p) => Mathf.Min(Polygon(p, G(3, 9), G(7.5f, 9), G(12.5f, 4.5f), G(12.5f, 19.5f), G(7.5f, 15), G(3, 15)),
            Mathf.Min(Arc(p, G(12.5f, 12), 4f / 12f, -50f, 50f), Arc(p, G(12.5f, 12), 7.5f / 12f, -50f, 50f)) - Half);

        static float Crossed(Func<Vector2, float> icon, Vector2 p)
        {
            float slash = Segment(p, G(3.5f, 3.5f), G(20.5f, 20.5f));
            return Mathf.Min(Mathf.Max(icon(p), -(slash - Half * 2.2f)), slash - Half);
        }

        static Vector2 G(float x, float y) => new Vector2((x - 12f) / 12f, (12f - y) / 12f);

        static float Arc(Vector2 p, Vector2 c, float r, float from, float to)
        {
            var q = p - c;
            float span = Mathf.Repeat(to - from, 360f);
            if (span == 0f) span = 360f;
            if (Mathf.Repeat(Mathf.Atan2(q.y, q.x) * Mathf.Rad2Deg - from, 360f) <= span) return Mathf.Abs(q.magnitude - r);
            Vector2 a = c + r * new Vector2(Mathf.Cos(from * Mathf.Deg2Rad), Mathf.Sin(from * Mathf.Deg2Rad));
            Vector2 b = c + r * new Vector2(Mathf.Cos(to * Mathf.Deg2Rad), Mathf.Sin(to * Mathf.Deg2Rad));
            return Mathf.Min((p - a).magnitude, (p - b).magnitude);
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
