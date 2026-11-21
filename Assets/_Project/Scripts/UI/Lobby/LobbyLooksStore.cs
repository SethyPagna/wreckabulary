using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public sealed class LobbyLooksStore
    {
        public const string LooksKey = "wv.looks", SeenKey = "wv.wardrobe.seen";
        const char SeenBreak = '+';

        SavedLooks memory;
        HashSet<string> seenMemory;

        public static bool Stored => LobbyMenu.RememberChoices;
        public bool Readable { get; private set; } = true;

        public SavedLooks Load()
        {
            if (!Stored)
            {
                Readable = true;
                return memory ??= new SavedLooks();
            }
            Readable = SavedLooks.TryDeserialize(PlayerPrefs.GetString(LooksKey, ""), out var looks);
            return memory = looks;
        }

        public bool Save(SavedLooks looks)
        {
            if (looks == null) return false;
            memory = looks;
            if (!Stored) return true;
            if (!SavedLooks.TryDeserialize(PlayerPrefs.GetString(LooksKey, ""), out _))
            {
                Readable = false;
                return false;
            }
            PlayerPrefs.SetString(LooksKey, looks.Serialize());
            PlayerPrefs.Save();
            return true;
        }

        public ISet<string> LoadSeen()
        {
            if (!Stored) return seenMemory;
            if (!PlayerPrefs.HasKey(SeenKey)) return null;
            return new HashSet<string>(PlayerPrefs.GetString(SeenKey, "").Split(new[] { SeenBreak }, StringSplitOptions.RemoveEmptyEntries),
                StringComparer.Ordinal);
        }

        public void SaveSeen(IEnumerable<string> ids)
        {
            seenMemory = new HashSet<string>(ids ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            if (!Stored) return;
            PlayerPrefs.SetString(SeenKey, string.Join(SeenBreak.ToString(), seenMemory.OrderBy(id => id, StringComparer.Ordinal)));
            PlayerPrefs.Save();
        }
    }
}
