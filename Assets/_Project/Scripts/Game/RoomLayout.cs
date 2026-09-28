using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>A room built in Creative: which objects, where, and which way they face. Saved as JSON.</summary>
    [Serializable]
    public class RoomLayout
    {
        [Serializable]
        public struct Piece
        {
            public string word;
            public Vector3 position;
            public float yaw;
        }

        public List<Piece> pieces = new();

        public int Count => pieces.Count;

        /// <summary>Everything letter-built that's standing in the scene right now (not held, not broken).</summary>
        public static RoomLayout Capture()
        {
            var layout = new RoomLayout();
            foreach (var s in UnityEngine.Object.FindObjectsByType<Smashable>())
            {
                if (s.IsBroken || !s.GetComponent<LetterBuilt>()) continue;
                var rb = s.GetComponent<Rigidbody>();
                if (rb && rb.isKinematic) continue; // being carried
                layout.pieces.Add(new Piece { word = s.Word, position = s.transform.position, yaw = s.transform.eulerAngles.y });
            }
            return layout;
        }

        /// <summary>Builds every piece under <paramref name="parent"/>, standing upright.</summary>
        public void Build(Transform parent)
        {
            foreach (var p in pieces)
                FurnitureCatalog.Spawn(p.word, new Vector3(p.position.x, Mathf.Max(0f, p.position.y), p.position.z), p.yaw, parent);
        }

        /// <summary>Removes every letter-built object in the scene.</summary>
        public static void ClearScene()
        {
            foreach (var s in UnityEngine.Object.FindObjectsByType<Smashable>())
                if (s.GetComponent<LetterBuilt>()) UnityEngine.Object.Destroy(s.gameObject);
        }

        // ---- Save slots ----

        public static string SlotPath(int slot) => Path.Combine(Application.persistentDataPath, "rooms", $"room_{slot}.json");

        public void Save(int slot)
        {
            var path = SlotPath(slot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(this, true));
        }

        public static bool Exists(int slot) => File.Exists(SlotPath(slot));

        public static RoomLayout Load(int slot) =>
            Exists(slot) ? JsonUtility.FromJson<RoomLayout>(File.ReadAllText(SlotPath(slot))) : null;

        public static void Delete(int slot)
        {
            if (Exists(slot)) File.Delete(SlotPath(slot));
        }
    }
}
