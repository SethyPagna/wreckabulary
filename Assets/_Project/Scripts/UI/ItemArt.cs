using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.UI
{
    public static class ItemArt
    {
        static readonly Dictionary<string, Texture2D> finishes = new Dictionary<string, Texture2D>(StringComparer.Ordinal);

        public static bool TryGet(string id, out Texture2D texture, out Rect uv)
        {
            texture = OriginalItemArt.Find(id);
            uv = new Rect(0f, 0f, 1f, 1f);
            if (texture) return true;
            if (string.IsNullOrEmpty(id)) return false;
            if (GeneratedItemArt.TryGet(id, out var sprite))
            {
                texture = sprite.texture;
                var region = sprite.textureRect;
                uv = new Rect(region.x / texture.width, region.y / texture.height,
                    region.width / texture.width, region.height / texture.height);
                return true;
            }
            return false;
        }

        public static string FinishPath(string id, string finish) => "UI/Items/Finishes/" + id + "_" + finish;

        public static Texture2D FinishIcon(string id, string finish)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(finish) || finish == Skin.Standard) return null;
            string path = FinishPath(id, finish);
            if (!finishes.TryGetValue(path, out var icon)) finishes[path] = icon = Resources.Load<Texture2D>(path);
            return icon;
        }

        public static bool TryGet(string id, string finish, out Texture2D texture, out Rect uv)
        {
            texture = FinishIcon(id, finish);
            uv = new Rect(0f, 0f, 1f, 1f);
            return texture || TryGet(id, out texture, out uv);
        }

        public static bool Apply(RawImage image, string id) => Apply(image, id, null);

        public static bool Apply(RawImage image, string id, string finish)
        {
            bool found = TryGet(id, finish, out var texture, out var uv);
            image.texture = texture;
            image.uvRect = uv;
            image.enabled = found;
            return found;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => finishes.Clear();
    }
}
