using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary
{
    public static class LobbyMapSnapshots
    {
        public const string Folder = "MapSnapshots", ToyboxArena = "ToyboxArena";
        static readonly Dictionary<string, Sprite> loaded = new Dictionary<string, Sprite>();

        public static Sprite For(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (loaded.TryGetValue(id, out var sprite) && sprite) return sprite;
            sprite = Resources.Load<Sprite>(Folder + "/" + id);
            if (sprite) loaded[id] = sprite; else loaded.Remove(id);
            return sprite;
        }

        public static string CourseKey(string mode) =>
            OnlineMaps.CourseFor(mode) == WordboundFlagshipContent.CourseId ? WordboundSoloEntry.LostAddressMap : ToyboxArena;

        public static Sprite ForMode(string mode) => string.IsNullOrEmpty(mode) ? null : For(CourseKey(mode));

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget() => loaded.Clear();
    }
}
