using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Word World-style recipes: how a word's own letters are stretched, stacked and laid down to make
    /// the object's shape (the B of BED is the headboard, the E the mattress, the D the footboard).
    /// Words without a recipe stand in a row, or in rows of <c>perRow</c>.
    /// Coordinates: pivot at the bottom centre, x to the right, z away from the camera.
    /// </summary>
    public static class LetterShapes
    {
        public struct Placement
        {
            public int index;
            /// <summary>Centre of the letter in object space.</summary>
            public Vector3 centre;
            /// <summary>Letter width, height (along its own up) and thickness.</summary>
            public float width, height, thickness;
            /// <summary>Lying on its back with its top pointing away from the camera, readable from above.</summary>
            public bool flat;
            public float yaw;
            /// <summary>Keep the letter's own proportions, using width only as a maximum (default rows).</summary>
            public bool keepAspect;
        }

        /// <summary>An upright letter. (x, y, z) is its centre.</summary>
        static Placement U(int i, float x, float y, float z, float w, float h, float t, float yaw = 0f) =>
            new() { index = i, centre = new Vector3(x, y, z), width = w, height = h, thickness = t, yaw = yaw };

        /// <summary>A letter lying flat. "len" runs away from the camera; t is how tall the slab is.</summary>
        static Placement F(int i, float x, float y, float z, float w, float len, float t, float yaw = 0f) =>
            new() { index = i, centre = new Vector3(x, y, z), width = w, height = len, thickness = t, flat = true, yaw = yaw };

        static readonly Dictionary<string, Placement[]> Recipes = new()
        {
            // S and A are the armrests, O the backrest, F the seat cushion.
            ["SOFA"] = new[]
            {
                U(0, -1.1f, 0.36f, 0f, 0.4f, 0.72f, 0.95f),
                U(1, 0f, 0.62f, 0.33f, 1.8f, 0.86f, 0.28f),
                U(2, 0f, 0.22f, -0.1f, 1.8f, 0.44f, 0.75f),
                U(3, 1.1f, 0.36f, 0f, 0.4f, 0.72f, 0.95f),
            },
            // T and E are the legs, A B L the tabletop.
            ["TABLE"] = new[]
            {
                U(0, -0.8f, 0.21f, 0f, 0.34f, 0.42f, 0.8f),
                F(1, -0.55f, 0.47f, 0f, 0.55f, 0.9f, 0.1f),
                F(2, 0f, 0.47f, 0f, 0.55f, 0.9f, 0.1f),
                F(3, 0.55f, 0.47f, 0f, 0.55f, 0.9f, 0.1f),
                U(4, 0.8f, 0.21f, 0f, 0.34f, 0.42f, 0.8f),
            },
            // B the headboard, E the mattress, D the footboard.
            ["BED"] = new[]
            {
                U(0, -1.05f, 0.55f, 0f, 0.4f, 1.1f, 1.5f),
                F(1, 0f, 0.2f, 0f, 1.7f, 1.4f, 0.36f),
                U(2, 1.05f, 0.35f, 0f, 0.36f, 0.7f, 1.5f),
            },
            // A floor lamp: L the base, A and M the stand, P the shade.
            ["LAMP"] = new[]
            {
                U(0, 0f, 0.14f, 0f, 0.7f, 0.28f, 0.7f),
                U(1, 0f, 0.53f, 0f, 0.3f, 0.5f, 0.3f),
                U(2, 0f, 1.03f, 0f, 0.3f, 0.5f, 0.3f),
                U(3, 0f, 1.56f, 0f, 0.72f, 0.56f, 0.72f),
            },
            // H the backrest, A the seat, C I R the legs.
            ["CHAIR"] = new[]
            {
                U(0, -0.25f, 0.22f, -0.22f, 0.16f, 0.44f, 0.16f),
                U(1, 0f, 0.86f, 0.25f, 0.62f, 0.8f, 0.12f),
                F(2, 0f, 0.5f, 0f, 0.62f, 0.56f, 0.12f),
                U(3, -0.25f, 0.22f, 0.22f, 0.12f, 0.44f, 0.16f),
                U(4, 0.25f, 0.22f, -0.22f, 0.16f, 0.44f, 0.16f),
            },
            // D and K the pedestals, E S the desktop.
            ["DESK"] = new[]
            {
                U(0, -0.62f, 0.37f, 0f, 0.44f, 0.74f, 0.8f),
                F(1, -0.28f, 0.79f, 0f, 0.56f, 0.86f, 0.1f),
                F(2, 0.28f, 0.79f, 0f, 0.56f, 0.86f, 0.1f),
                U(3, 0.62f, 0.37f, 0f, 0.44f, 0.74f, 0.8f),
            },
            // T the stand, V the screen.
            ["TV"] = new[]
            {
                U(0, 0f, 0.18f, 0f, 0.5f, 0.36f, 0.3f),
                U(1, 0f, 0.71f, 0f, 1.0f, 0.7f, 0.14f),
            },
            ["RUG"] = new[]
            {
                F(0, -0.7f, 0.025f, 0f, 0.66f, 1.3f, 0.05f),
                F(1, 0f, 0.025f, 0f, 0.66f, 1.3f, 0.05f),
                F(2, 0.7f, 0.025f, 0f, 0.66f, 1.3f, 0.05f),
            },
            // P the pot, then the letters grow into leaves.
            ["PLANT"] = new[]
            {
                U(0, 0f, 0.2f, 0f, 0.42f, 0.4f, 0.42f),
                U(1, 0f, 0.58f, 0f, 0.3f, 0.36f, 0.3f),
                U(2, 0f, 0.95f, 0f, 0.5f, 0.38f, 0.4f),
                U(3, 0f, 1.34f, 0f, 0.62f, 0.4f, 0.45f),
                U(4, 0f, 1.75f, 0f, 0.78f, 0.42f, 0.5f),
            },
            // A grandfather clock with the O as its face.
            ["CLOCK"] = new[]
            {
                U(0, 0f, 0.18f, 0f, 0.5f, 0.36f, 0.4f),
                U(1, 0f, 0.62f, 0f, 0.3f, 0.52f, 0.3f),
                U(2, 0f, 1.17f, 0f, 0.6f, 0.58f, 0.3f),
                U(3, 0f, 1.62f, 0f, 0.42f, 0.32f, 0.3f),
                U(4, 0f, 1.95f, 0f, 0.5f, 0.34f, 0.3f),
            },
            // A vase that bulges in the middle.
            ["VASE"] = new[]
            {
                U(0, 0f, 0.15f, 0f, 0.3f, 0.3f, 0.3f),
                U(1, 0f, 0.48f, 0f, 0.46f, 0.36f, 0.42f),
                U(2, 0f, 0.86f, 0f, 0.5f, 0.4f, 0.45f),
                U(3, 0f, 1.21f, 0f, 0.34f, 0.3f, 0.3f),
            },
            // A pile of books.
            ["BOOKS"] = new[]
            {
                F(0, 0f, 0.06f, 0f, 0.66f, 0.46f, 0.12f, 6f),
                F(1, 0.02f, 0.19f, 0f, 0.62f, 0.44f, 0.12f, -8f),
                F(2, -0.02f, 0.32f, 0f, 0.64f, 0.46f, 0.12f, 4f),
                F(3, 0.01f, 0.45f, 0f, 0.6f, 0.42f, 0.12f, -5f),
                F(4, 0f, 0.58f, 0f, 0.58f, 0.42f, 0.12f, 9f),
            },
            ["PLATE"] = new[]
            {
                F(0, -0.46f, 0.025f, 0f, 0.22f, 0.24f, 0.05f),
                F(1, -0.23f, 0.025f, 0f, 0.22f, 0.24f, 0.05f),
                F(2, 0f, 0.025f, 0f, 0.22f, 0.24f, 0.05f),
                F(3, 0.23f, 0.025f, 0f, 0.22f, 0.24f, 0.05f),
                F(4, 0.46f, 0.025f, 0f, 0.22f, 0.24f, 0.05f),
            },
            // M the body, U the handle, G the saucer.
            ["MUG"] = new[]
            {
                U(0, 0f, 0.19f, 0f, 0.3f, 0.28f, 0.28f),
                U(1, 0.22f, 0.2f, 0f, 0.14f, 0.18f, 0.06f, 90f),
                F(2, 0f, 0.02f, 0f, 0.4f, 0.4f, 0.04f),
            },
            // A duck: D the body, U the tail, C the head, K the beak.
            ["DUCK"] = new[]
            {
                U(0, 0f, 0.18f, 0f, 0.5f, 0.36f, 0.34f),
                U(1, -0.3f, 0.3f, 0f, 0.16f, 0.2f, 0.2f),
                U(2, 0.2f, 0.47f, 0f, 0.26f, 0.26f, 0.26f),
                U(3, 0.38f, 0.44f, 0f, 0.14f, 0.12f, 0.16f),
            },
        };

        public static bool HasRecipe(string word) => Recipes.ContainsKey(word);

        /// <summary>Where each letter goes. Falls back to rows of upright letters.</summary>
        public static List<Placement> For(string word, Vector3 block, int perRow, float gap)
        {
            if (Recipes.TryGetValue(word, out var recipe) && recipe.Length == word.Length)
                return new List<Placement>(recipe);

            var list = new List<Placement>(word.Length);
            int n = word.Length;
            int cols = perRow <= 0 ? n : Mathf.Min(perRow, n);
            int rows = Mathf.CeilToInt(n / (float)cols);
            for (int i = 0; i < n; i++)
            {
                int row = i / cols, col = i % cols;
                int inRow = row == rows - 1 ? n - row * cols : cols;
                float x = (col - (inRow - 1) * 0.5f) * (block.x + gap);
                float y = block.y * 0.5f + row * (block.y + gap);
                var p = U(i, x, y, 0f, block.x, block.y, block.z);
                p.keepAspect = true;
                list.Add(p);
            }
            return list;
        }
    }
}
