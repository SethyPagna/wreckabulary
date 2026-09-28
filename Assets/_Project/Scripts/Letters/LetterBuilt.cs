using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// An object assembled from 3D copies of its own letters, shaped like the thing it spells
    /// (see <see cref="LetterShapes"/>). Words without a recipe stand in rows, stacked upwards.
    /// The pivot is at the bottom centre.
    /// </summary>
    public class LetterBuilt : MonoBehaviour
    {
        public string word = "SOFA";
        [Tooltip("Letter size for words without a shape recipe: width (max), height, thickness.")]
        public Vector3 blockSize = new(0.5f, 0.5f, 0.5f);
        [Tooltip("Letters per row for words without a recipe. 0 puts every letter in one row, 1 makes a column.")]
        public int perRow;
        public float gap = 0.02f;
        public Color color = new(0.85f, 0.63f, 0.40f);
        public bool colliders = true;

        [SerializeField] List<Transform> blocks = new();
        /// <summary>One transform per letter, in word order (used to burst letters from where they were).</summary>
        public IReadOnlyList<Transform> Blocks => blocks;

        public void Build()
        {
            foreach (var b in blocks)
            {
                if (!b) continue;
                if (Application.isPlaying) Destroy(b.gameObject);
                else DestroyImmediate(b.gameObject);
            }
            blocks.Clear();

            word = word.ToUpperInvariant();
            var assets = GameAssets.I;
            // Alternate two shades so neighbouring letters read as separate letters.
            var light = assets.Tinted(color);
            var dark = assets.Tinted(Color.Lerp(color, Color.black, 0.18f));
            var slots = new Transform[word.Length];

            foreach (var p in LetterShapes.For(word, blockSize, perRow, gap))
            {
                char c = word[p.index];
                var mesh = assets.LetterMesh(c);
                if (!mesh) continue;

                var go = new GameObject($"Letter_{c}") { layer = gameObject.layer };
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = p.tinted ? assets.Tinted(p.tint) : p.index % 2 == 0 ? light : dark;
                Place(go.transform, mesh, p);
                if (colliders)
                {
                    var box = go.AddComponent<BoxCollider>();
                    box.center = mesh.bounds.center;
                    box.size = mesh.bounds.size;
                }
                slots[p.index] = go.transform;
            }
            blocks.AddRange(slots);
        }

        /// <summary>Scales and rotates a normalised letter mesh so it fills the placement's box.</summary>
        public static void Place(Transform t, Mesh mesh, LetterShapes.Placement p)
        {
            var b = mesh.bounds;
            float width = p.keepAspect ? Mathf.Min(p.width, p.height * b.size.x / b.size.y * 1.1f) : p.width;
            var scale = new Vector3(width / b.size.x, p.height / b.size.y, p.thickness / b.size.z);
            var rot = Quaternion.Euler(0f, p.yaw, 0f) * Quaternion.Euler(p.flat ? 90f : 0f, 0f, 0f);
            t.localRotation = rot;
            t.localScale = scale;
            t.localPosition = p.centre - rot * Vector3.Scale(scale, b.center);
        }

        /// <summary>Creates and builds a new letter-built object.</summary>
        public static LetterBuilt Spawn(string word, Vector3 blockSize, int perRow, Color color, Transform parent, bool colliders = true)
        {
            var go = new GameObject(word);
            go.transform.SetParent(parent, false);
            var built = go.AddComponent<LetterBuilt>();
            built.word = word;
            built.blockSize = blockSize;
            built.perRow = perRow;
            built.color = color;
            built.colliders = colliders;
            built.Build();
            return built;
        }
    }
}
