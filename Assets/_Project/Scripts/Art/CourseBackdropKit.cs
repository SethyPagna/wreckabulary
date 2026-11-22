using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Surroundings;

namespace Wreckabulary.Art
{
    public sealed class CourseBackdropKit : ScriptableObject
    {
        public const string ResourcePath = "Surroundings/CourseKit";

        [Serializable]
        public sealed class Piece
        {
            public string id;
            public Mesh mesh;
            public string[] slots = Array.Empty<string>();
            public Quaternion rotation = Quaternion.identity;
            public Vector3 scale = Vector3.one;

            public Matrix4x4 Space => Matrix4x4.TRS(Vector3.zero, rotation, scale);
        }

        [Serializable]
        public sealed class Slot
        {
            public string id;
            public Material material;
        }

        [SerializeField] TextAsset[] manifests = Array.Empty<TextAsset>();
        [SerializeField] TextAsset style;
        [SerializeField] List<Piece> pieces = new();
        [SerializeField] List<Slot> slots = new();

        SurroundingsKit kit;
        CourseSurroundingsStyle parsedStyle;
        Dictionary<string, Piece> byId;
        Dictionary<string, Material> materials;

        public IReadOnlyList<Piece> Pieces => pieces;
        public IReadOnlyList<Slot> Slots => slots;
        public IReadOnlyList<TextAsset> Manifests => manifests;
        public TextAsset StyleAsset => style;

        public static CourseBackdropKit Load() => Resources.Load<CourseBackdropKit>(ResourcePath);

        public void Configure(IEnumerable<TextAsset> kitManifests, TextAsset courseStyle, IEnumerable<Piece> kitPieces, IEnumerable<Slot> kitSlots)
        {
            manifests = kitManifests.ToArray();
            style = courseStyle;
            pieces = kitPieces.ToList();
            slots = kitSlots.ToList();
            Forget();
        }

        void OnValidate() => Forget();

        void Forget()
        {
            kit = null;
            parsedStyle = null;
            byId = null;
            materials = null;
        }

        public SurroundingsKit Kit()
        {
            if (kit != null) return kit;
            if (manifests.Length == 0 || manifests.Any(m => !m)) throw new InvalidOperationException($"{name}: the course backdrop kit has no kit manifests.");
            SurroundingsKit loaded = null;
            foreach (var manifest in manifests)
                loaded = loaded == null ? SurroundingsKit.FromJson(manifest.text, manifest.name) : loaded.Add(manifest.text, manifest.name);
            return kit = loaded;
        }

        public CourseSurroundingsStyle Style()
        {
            if (parsedStyle != null) return parsedStyle;
            if (!style) throw new InvalidOperationException($"{name}: the course backdrop kit has no style.");
            return parsedStyle = CourseSurroundingsStyle.FromJson(style.text, style.name);
        }

        public Piece Find(string id)
        {
            byId ??= pieces.Where(p => p != null && !string.IsNullOrEmpty(p.id)).GroupBy(p => p.id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            return id != null && byId.TryGetValue(id, out var piece) ? piece : null;
        }

        public Material MaterialFor(string slot)
        {
            materials ??= slots.Where(s => s != null && !string.IsNullOrEmpty(s.id)).GroupBy(s => s.id, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First().material, StringComparer.Ordinal);
            return slot != null && materials.TryGetValue(slot, out var material) ? material : null;
        }

        public List<string> Gaps()
        {
            var gaps = new List<string>();
            var surroundings = Kit();
            var needed = new List<string>();
            foreach (string id in Style().Pieces)
            {
                needed.Add(id);
                var far = surroundings.Piece(id)?.Far;
                if (far != null) needed.Add(far);
            }
            foreach (string id in needed.Distinct(StringComparer.Ordinal))
            {
                var piece = Find(id);
                if (piece == null || !piece.mesh) { gaps.Add($"piece {id} has no mesh"); continue; }
                foreach (string slot in piece.slots)
                    if (!MaterialFor(slot)) gaps.Add($"slot {slot} of {id} has no material");
            }
            return gaps.Distinct(StringComparer.Ordinal).ToList();
        }
    }
}
