using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.UI
{
    public static class WardrobeArt
    {
        public const string Folder = "UI/Wardrobe";
        const float PixelsPerUnit = 100f;
        static readonly Vector2 Centre = new Vector2(.5f, .5f);
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public static string PathFor(string slot, string piece) => Folder + "/" + slot + "/" + piece;

        public static Sprite For(string slot, string piece)
        {
            if (!IsId(slot) || !IsId(piece)) return null;
            string path = PathFor(slot, piece);
            if (cache.TryGetValue(path, out var known) && (known || known is null)) return known;
            var loaded = Load(path);
            cache[path] = loaded;
            return loaded;
        }

        public static void Forget() => cache.Clear();

        static Sprite Load(string path)
        {
            var sprite = Resources.Load<Sprite>(path);
            if (sprite) return sprite;
            var texture = Resources.Load<Texture2D>(path);
            if (!texture) return null;
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Centre, PixelsPerUnit);
            sprite.name = texture.name;
            return sprite;
        }

        static bool IsId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (char c in id)
                if (!(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '_')) return false;
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => cache.Clear();
    }
}
