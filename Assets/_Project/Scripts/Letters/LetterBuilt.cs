using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// An object assembled from the blocks of its own word, e.g. a SOFA made of S, O, F and A.
    /// Blocks are laid out left to right in rows, stacked upwards, with the pivot at the bottom centre.
    /// </summary>
    public class LetterBuilt : MonoBehaviour
    {
        public string word = "SOFA";
        public Vector3 blockSize = new(0.5f, 0.5f, 0.5f);
        [Tooltip("Blocks per row. 0 puts every letter in one row, 1 makes a column.")]
        public int perRow;
        public float gap = 0.02f;
        public Color color = new(0.85f, 0.63f, 0.40f);
        public bool colliders = true;

        [SerializeField] List<Transform> blocks = new();
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
            int n = word.Length;
            int cols = perRow <= 0 ? n : Mathf.Min(perRow, n);
            int rows = Mathf.CeilToInt(n / (float)cols);
            var mat = GameAssets.I.Tinted(color);

            for (int i = 0; i < n; i++)
            {
                int row = i / cols, col = i % cols;
                int inRow = row == rows - 1 ? n - row * cols : cols;
                float x = (col - (inRow - 1) * 0.5f) * (blockSize.x + gap);
                float y = blockSize.y * 0.5f + row * (blockSize.y + gap);

                var b = LetterBlocks.Create(word[i].ToString(), blockSize, mat, transform, colliders, skipBottom: rows == 1 || row == 0);
                b.transform.localPosition = new Vector3(x, y, 0f);
                blocks.Add(b.transform);
            }
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
