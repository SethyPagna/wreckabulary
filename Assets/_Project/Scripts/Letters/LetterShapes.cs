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
            /// <summary>Its own colour (a tree's trunk and leaves) instead of the object's colour.</summary>
            public Color tint;
            public bool tinted;
        }

        // Accent colours for letters that differ from the object's own colour.
        static readonly Color Leaf = new(0.42f, 0.7f, 0.35f), Bark = new(0.52f, 0.36f, 0.22f), Petal = new(0.95f, 0.6f, 0.75f),
            Yellow = new(0.98f, 0.82f, 0.3f), Red = new(0.88f, 0.3f, 0.26f), Sky = new(0.62f, 0.8f, 0.95f),
            Ivory = new(0.97f, 0.94f, 0.86f), Black = new(0.2f, 0.19f, 0.22f), Metal = new(0.72f, 0.75f, 0.78f),
            Skin = new(0.98f, 0.8f, 0.66f), Terracotta = new(0.77f, 0.41f, 0.29f);

        /// <summary>An upright letter. (x, y, z) is its centre.</summary>
        static Placement U(int i, float x, float y, float z, float w, float h, float t, float yaw = 0f, Color? c = null) =>
            new() { index = i, centre = new Vector3(x, y, z), width = w, height = h, thickness = t, yaw = yaw,
                    tint = c ?? default, tinted = c.HasValue };

        /// <summary>A letter lying flat. "len" runs away from the camera; t is how tall the slab is.</summary>
        static Placement F(int i, float x, float y, float z, float w, float len, float t, float yaw = 0f, Color? c = null) =>
            new() { index = i, centre = new Vector3(x, y, z), width = w, height = len, thickness = t, flat = true, yaw = yaw,
                    tint = c ?? default, tinted = c.HasValue };

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

            // ---------------------------------------------------------------- living room extras

            // A cushion pile: two layers of three flat letters.
            ["PILLOW"] = new[]
            {
                F(0, -0.38f, 0.07f, -0.1f, 0.36f, 0.4f, 0.14f, 8f),
                F(1, 0f, 0.07f, -0.12f, 0.36f, 0.4f, 0.14f, -5f),
                F(2, 0.38f, 0.07f, -0.08f, 0.36f, 0.4f, 0.14f, 4f),
                F(3, -0.2f, 0.21f, 0.05f, 0.36f, 0.4f, 0.14f, -10f),
                F(4, 0.18f, 0.21f, 0.06f, 0.36f, 0.4f, 0.14f, 7f),
                F(5, 0f, 0.35f, 0.02f, 0.36f, 0.4f, 0.14f, 15f),
            },
            // D the body, O the speaker, I the aerial, R and A the knobs.
            ["RADIO"] = new[]
            {
                U(0, 0.12f, 0.24f, -0.15f, 0.08f, 0.08f, 0.05f, 0f, Black),
                U(1, 0.22f, 0.24f, -0.15f, 0.08f, 0.08f, 0.05f, 0f, Black),
                U(2, 0f, 0.19f, 0f, 0.62f, 0.38f, 0.26f),
                U(3, 0.24f, 0.6f, 0.05f, 0.05f, 0.44f, 0.05f, 0f, Metal),
                U(4, -0.13f, 0.2f, -0.15f, 0.24f, 0.24f, 0.05f, 0f, Black),
            },
            // A crate: X across the front, B and O as the sides.
            ["BOX"] = new[]
            {
                U(0, -0.3f, 0.3f, 0f, 0.55f, 0.6f, 0.08f, 90f),
                U(1, 0.3f, 0.3f, 0f, 0.55f, 0.6f, 0.08f, 90f),
                U(2, 0f, 0.3f, -0.27f, 0.62f, 0.6f, 0.08f),
            },
            // An upright piano: A the body standing on the floor, P and N the legs under the I keyboard, O the music stand on top.
            ["PIANO"] = new[]
            {
                U(0, -0.55f, 0.3f, -0.3f, 0.14f, 0.6f, 0.14f),
                F(1, 0f, 0.7f, -0.36f, 1.1f, 0.24f, 0.05f, 0f, Ivory),
                U(2, 0f, 0.65f, 0.1f, 1.3f, 1.3f, 0.42f),
                U(3, 0.55f, 0.3f, -0.3f, 0.14f, 0.6f, 0.14f),
                U(4, 0f, 1.42f, 0.02f, 0.4f, 0.22f, 0.04f, 0f, Ivory),
            },
            // A bar stool: S the cushion, T the leg, O O the footrest and base, L a little footrest.
            ["STOOL"] = new[]
            {
                F(0, 0f, 0.57f, 0f, 0.46f, 0.46f, 0.08f),
                U(1, 0f, 0.27f, 0f, 0.34f, 0.5f, 0.12f),
                F(2, 0f, 0.2f, 0f, 0.38f, 0.38f, 0.04f, 0f, Metal),
                F(3, 0f, 0.02f, 0f, 0.44f, 0.44f, 0.04f),
                U(4, 0f, 0.32f, -0.18f, 0.16f, 0.12f, 0.05f, 0f, Metal),
            },
            // A standing fan: A the stand, F and N the blades.
            ["FAN"] = new[]
            {
                U(0, -0.2f, 1.02f, -0.06f, 0.36f, 0.22f, 0.04f, -20f, Sky),
                U(1, 0f, 0.46f, 0f, 0.32f, 0.92f, 0.22f),
                U(2, 0.2f, 1.02f, -0.06f, 0.36f, 0.22f, 0.04f, 20f, Sky),
            },

            // ---------------------------------------------------------------- kitchen

            // O is the oven, S T V E the four burners on top.
            ["STOVE"] = new[]
            {
                F(0, -0.22f, 0.66f, -0.18f, 0.34f, 0.3f, 0.05f, 0f, Black),
                F(1, 0.22f, 0.66f, -0.18f, 0.34f, 0.3f, 0.05f, 0f, Black),
                U(2, 0f, 0.32f, 0f, 0.96f, 0.64f, 0.8f),
                F(3, -0.22f, 0.66f, 0.18f, 0.34f, 0.3f, 0.05f, 0f, Black),
                F(4, 0.22f, 0.66f, 0.18f, 0.34f, 0.3f, 0.05f, 0f, Black),
            },
            // F the freezer, I the tall fridge, R D G E fridge magnets.
            ["FRIDGE"] = new[]
            {
                U(0, 0f, 1.72f, 0f, 0.9f, 0.4f, 0.8f),
                U(1, 0f, 0.76f, 0f, 0.9f, 1.52f, 0.8f),
                U(2, -0.22f, 1.1f, -0.42f, 0.14f, 0.14f, 0.04f, 0f, Red),
                U(3, 0.12f, 0.9f, -0.42f, 0.14f, 0.14f, 0.04f, 0f, Sky),
                U(4, -0.05f, 1.28f, -0.42f, 0.14f, 0.14f, 0.04f, 0f, Yellow),
                U(5, 0.25f, 1.2f, -0.42f, 0.14f, 0.14f, 0.04f, 0f, Leaf),
            },
            // S the basin, I the tap, N and K the cupboards.
            ["SINK"] = new[]
            {
                F(0, 0f, 0.85f, -0.04f, 0.62f, 0.46f, 0.1f, 0f, Metal),
                U(1, 0f, 1.04f, 0.24f, 0.1f, 0.3f, 0.1f, 0f, Metal),
                U(2, -0.28f, 0.4f, 0f, 0.55f, 0.8f, 0.7f),
                U(3, 0.28f, 0.4f, 0f, 0.55f, 0.8f, 0.7f),
            },
            // P the handle, A the pan, N the rim.
            ["PAN"] = new[]
            {
                F(0, 0.42f, 0.04f, 0f, 0.14f, 0.44f, 0.05f, 90f, Black),
                F(1, 0f, 0.03f, 0f, 0.46f, 0.46f, 0.06f, 0f, Metal),
                U(2, 0f, 0.08f, 0.22f, 0.44f, 0.1f, 0.03f, 0f, Metal),
            },
            // O the pot, P a handle, T the lid knob.
            ["POT"] = new[]
            {
                U(0, -0.31f, 0.32f, 0f, 0.12f, 0.12f, 0.1f, 90f),
                U(1, 0f, 0.2f, 0f, 0.5f, 0.4f, 0.45f),
                U(2, 0f, 0.46f, 0f, 0.18f, 0.12f, 0.1f, 0f, Black),
            },
            // E the body, T the lid, K the spout, L the handle, E the base, T the knob.
            ["KETTLE"] = new[]
            {
                U(0, 0.27f, 0.26f, 0f, 0.16f, 0.2f, 0.12f),
                U(1, 0f, 0.19f, 0f, 0.42f, 0.36f, 0.36f),
                U(2, 0f, 0.41f, 0f, 0.22f, 0.1f, 0.2f),
                U(3, 0f, 0.5f, 0f, 0.08f, 0.08f, 0.08f, 0f, Black),
                U(4, -0.26f, 0.28f, 0f, 0.12f, 0.24f, 0.1f, 90f, Black),
                F(5, 0f, 0.015f, 0f, 0.44f, 0.4f, 0.03f),
            },
            // U is the cup, C the handle, P the saucer.
            ["CUP"] = new[]
            {
                U(0, 0.15f, 0.13f, 0f, 0.1f, 0.12f, 0.06f, 90f),
                U(1, 0f, 0.13f, 0f, 0.22f, 0.24f, 0.22f),
                F(2, 0f, 0.012f, 0f, 0.32f, 0.32f, 0.02f, 0f, Ivory),
            },
            // W the bowl, O its foot, B and L fruit inside.
            ["BOWL"] = new[]
            {
                U(0, -0.1f, 0.3f, 0f, 0.13f, 0.13f, 0.13f, 0f, Red),
                F(1, 0f, 0.015f, 0f, 0.3f, 0.3f, 0.03f),
                U(2, 0f, 0.14f, 0f, 0.52f, 0.24f, 0.46f),
                U(3, 0.1f, 0.3f, 0f, 0.13f, 0.13f, 0.13f, 0f, Yellow),
            },

            // ---------------------------------------------------------------- bedroom

            // S and F the sides, H E L the shelves.
            ["SHELF"] = new[]
            {
                U(0, -0.56f, 0.6f, 0f, 0.12f, 1.2f, 0.36f),
                F(1, 0f, 0.32f, 0f, 1.0f, 0.34f, 0.06f),
                F(2, 0f, 0.66f, 0f, 1.0f, 0.34f, 0.06f),
                F(3, 0f, 1.0f, 0f, 1.0f, 0.34f, 0.06f),
                U(4, 0.56f, 0.6f, 0f, 0.12f, 1.2f, 0.36f),
            },
            // O the mirror, I the stand, R R the feet, M the crest, R a finial.
            ["MIRROR"] = new[]
            {
                U(0, 0f, 1.56f, 0f, 0.34f, 0.16f, 0.06f),
                U(1, 0f, 0.32f, 0f, 0.08f, 0.62f, 0.08f),
                F(2, -0.18f, 0.025f, 0f, 0.2f, 0.22f, 0.05f),
                F(3, 0.18f, 0.025f, 0f, 0.2f, 0.22f, 0.05f),
                U(4, 0f, 1.04f, 0f, 0.62f, 0.9f, 0.06f, 0f, Sky),
                U(5, 0f, 1.7f, 0f, 0.1f, 0.1f, 0.06f),
            },
            // T the arms, E the head, D the body, D the feet, Y the ears.
            ["TEDDY"] = new[]
            {
                U(0, 0f, 0.3f, -0.04f, 0.5f, 0.14f, 0.14f),
                U(1, 0f, 0.6f, 0f, 0.3f, 0.28f, 0.26f),
                U(2, 0f, 0.22f, 0f, 0.4f, 0.4f, 0.3f),
                F(3, 0f, 0.04f, -0.1f, 0.36f, 0.2f, 0.08f),
                U(4, 0f, 0.8f, 0f, 0.32f, 0.12f, 0.1f),
            },
            // C and T the doors, O and S the knobs, L and E the feet.
            ["CLOSET"] = new[]
            {
                U(0, -0.24f, 0.92f, 0f, 0.46f, 1.64f, 0.6f),
                F(1, -0.36f, 0.04f, 0f, 0.2f, 0.2f, 0.08f, 0f, Bark),
                U(2, -0.08f, 0.95f, -0.32f, 0.08f, 0.08f, 0.05f, 0f, Yellow),
                U(3, 0.08f, 0.95f, -0.32f, 0.08f, 0.08f, 0.05f, 0f, Yellow),
                F(4, 0.36f, 0.04f, 0f, 0.2f, 0.2f, 0.08f, 0f, Bark),
                U(5, 0.24f, 0.92f, 0f, 0.46f, 1.64f, 0.6f),
            },

            // ---------------------------------------------------------------- garden

            // T is the trunk and branches, R E E the leafy crown.
            ["TREE"] = new[]
            {
                U(0, 0f, 0.75f, 0f, 1.0f, 1.5f, 0.42f, 0f, Bark),
                U(1, -0.38f, 1.62f, 0f, 0.66f, 0.58f, 0.55f, 0f, Leaf),
                U(2, 0.38f, 1.62f, 0f, 0.66f, 0.58f, 0.55f, 0f, Leaf),
                U(3, 0f, 2.05f, 0f, 0.62f, 0.56f, 0.55f, 0f, Leaf),
            },
            ["BUSH"] = new[]
            {
                U(0, -0.34f, 0.24f, 0f, 0.48f, 0.48f, 0.48f),
                U(1, 0.02f, 0.3f, 0.06f, 0.5f, 0.56f, 0.5f),
                U(2, 0.36f, 0.23f, -0.02f, 0.46f, 0.46f, 0.46f),
                U(3, 0f, 0.6f, 0.02f, 0.44f, 0.4f, 0.42f),
            },
            // L the stem, F a leaf, O the middle, W E R the petals.
            ["FLOWER"] = new[]
            {
                U(0, -0.13f, 0.34f, 0f, 0.18f, 0.16f, 0.05f, 0f, Leaf),
                U(1, 0f, 0.32f, 0f, 0.08f, 0.64f, 0.08f, 0f, Leaf),
                U(2, 0f, 0.82f, 0f, 0.32f, 0.32f, 0.1f, 0f, Yellow),
                U(3, 0f, 1.06f, 0.02f, 0.3f, 0.18f, 0.06f, 0f, Petal),
                U(4, -0.26f, 0.82f, 0.02f, 0.2f, 0.26f, 0.06f, 0f, Petal),
                U(5, 0.26f, 0.82f, 0.02f, 0.2f, 0.26f, 0.06f, 0f, Petal),
            },
            // R a leaf, O the bloom, S the stem, E the pot.
            ["ROSE"] = new[]
            {
                U(0, -0.11f, 0.32f, 0f, 0.14f, 0.12f, 0.05f, 0f, Leaf),
                U(1, 0f, 0.64f, 0f, 0.3f, 0.3f, 0.2f, 0f, Red),
                U(2, 0f, 0.35f, 0f, 0.08f, 0.44f, 0.08f, 0f, Leaf),
                U(3, 0f, 0.09f, 0f, 0.28f, 0.18f, 0.24f, 0f, Terracotta),
            },
            // B and H the ends, E and N the seat, C the backrest.
            ["BENCH"] = new[]
            {
                U(0, -0.76f, 0.23f, 0f, 0.28f, 0.46f, 0.46f, 0f, Metal),
                F(1, -0.36f, 0.49f, 0f, 0.72f, 0.46f, 0.08f),
                F(2, 0.36f, 0.49f, 0f, 0.72f, 0.46f, 0.08f),
                U(3, 0f, 0.72f, 0.21f, 1.44f, 0.36f, 0.06f),
                U(4, 0.76f, 0.23f, 0f, 0.28f, 0.46f, 0.46f, 0f, Metal),
            },
            ["FENCE"] = new[]
            {
                U(0, -0.64f, 0.45f, 0f, 0.28f, 0.9f, 0.08f),
                U(1, -0.32f, 0.45f, 0f, 0.28f, 0.9f, 0.08f),
                U(2, 0f, 0.45f, 0f, 0.28f, 0.9f, 0.08f),
                U(3, 0.32f, 0.45f, 0f, 0.28f, 0.9f, 0.08f),
                U(4, 0.64f, 0.45f, 0f, 0.28f, 0.9f, 0.08f),
            },
            // I and N the posts, S the top bar, W the seat, G a bird on top.
            ["SWING"] = new[]
            {
                U(0, 0f, 1.66f, 0f, 1.4f, 0.16f, 0.12f),
                U(1, 0f, 0.46f, 0f, 0.46f, 0.14f, 0.26f, 0f, Red),
                U(2, -0.62f, 0.8f, 0f, 0.1f, 1.6f, 0.12f),
                U(3, 0.62f, 0.8f, 0f, 0.1f, 1.6f, 0.12f),
                U(4, 0.3f, 1.84f, 0f, 0.16f, 0.16f, 0.1f, 0f, Yellow),
            },
            // G the body, N the face, O the nose, M the beard, E the hat.
            ["GNOME"] = new[]
            {
                U(0, 0f, 0.17f, 0f, 0.34f, 0.34f, 0.3f, 0f, Sky),
                U(1, 0f, 0.45f, 0f, 0.26f, 0.22f, 0.26f, 0f, Skin),
                U(2, 0f, 0.45f, -0.15f, 0.08f, 0.08f, 0.08f, 0f, Red),
                U(3, 0f, 0.33f, -0.14f, 0.26f, 0.14f, 0.1f, 0f, Ivory),
                U(4, 0f, 0.72f, 0f, 0.3f, 0.36f, 0.26f, 0f, Red),
            },
            ["ROCK"] = new[]
            {
                U(0, -0.24f, 0.22f, 0f, 0.46f, 0.44f, 0.5f, 12f),
                U(1, 0.16f, 0.26f, 0.06f, 0.52f, 0.52f, 0.52f, -8f),
                U(2, 0.34f, 0.15f, -0.18f, 0.3f, 0.3f, 0.3f, 20f),
                U(3, -0.02f, 0.5f, 0.02f, 0.36f, 0.3f, 0.36f, -15f),
            },
            // A garden hose snaking across the grass.
            ["HOSE"] = new[]
            {
                F(0, -0.6f, 0.03f, 0f, 0.36f, 0.4f, 0.06f, 20f),
                F(1, -0.2f, 0.03f, 0.1f, 0.36f, 0.4f, 0.06f, -15f),
                F(2, 0.2f, 0.03f, 0f, 0.36f, 0.4f, 0.06f, 25f),
                F(3, 0.6f, 0.03f, 0.08f, 0.36f, 0.4f, 0.06f, -10f),
            },
            ["POND"] = new[]
            {
                F(0, -0.4f, 0.015f, -0.3f, 0.8f, 0.7f, 0.03f),
                F(1, 0.4f, 0.015f, -0.3f, 0.8f, 0.7f, 0.03f),
                F(2, -0.4f, 0.015f, 0.4f, 0.8f, 0.7f, 0.03f),
                F(3, 0.4f, 0.015f, 0.4f, 0.8f, 0.7f, 0.03f),
            },

            // ---------------------------------------------------------------- weapons (held from the bottom)

            // S the pommel, W the crossguard, O R D the blade.
            ["SWORD"] = new[]
            {
                U(0, 0f, 0.05f, 0f, 0.1f, 0.1f, 0.06f, 0f, Yellow),
                U(1, 0f, 0.16f, 0f, 0.32f, 0.08f, 0.06f, 0f, Yellow),
                U(2, 0f, 0.36f, 0f, 0.1f, 0.3f, 0.04f),
                U(3, 0f, 0.66f, 0f, 0.1f, 0.3f, 0.04f),
                U(4, 0f, 0.95f, 0f, 0.1f, 0.26f, 0.04f),
            },
            // B the knob, A the handle, T the barrel.
            ["BAT"] = new[]
            {
                U(0, 0f, 0.06f, 0f, 0.12f, 0.12f, 0.1f),
                U(1, 0f, 0.28f, 0f, 0.08f, 0.32f, 0.08f),
                U(2, 0f, 0.68f, 0f, 0.2f, 0.48f, 0.18f),
            },
            // A the handle, X the blade, E the back of the head.
            ["AXE"] = new[]
            {
                U(0, 0f, 0.32f, 0f, 0.08f, 0.64f, 0.08f, 0f, Bark),
                U(1, 0.12f, 0.64f, 0f, 0.3f, 0.26f, 0.05f, 0f, Metal),
                U(2, -0.08f, 0.64f, 0f, 0.12f, 0.2f, 0.05f, 0f, Metal),
            },
            // S P E the shaft, R a binding, A the point.
            ["SPEAR"] = new[]
            {
                U(0, 0f, 0.17f, 0f, 0.07f, 0.34f, 0.07f, 0f, Bark),
                U(1, 0f, 0.51f, 0f, 0.07f, 0.34f, 0.07f, 0f, Bark),
                U(2, 0f, 0.85f, 0f, 0.07f, 0.34f, 0.07f, 0f, Bark),
                U(3, 0f, 1.06f, 0f, 0.12f, 0.1f, 0.08f, 0f, Red),
                U(4, 0f, 1.26f, 0f, 0.18f, 0.3f, 0.05f, 0f, Metal),
            },
            // C the back, A N N the barrel, O the muzzle, N the sight.
            ["CANNON"] = new[]
            {
                U(0, 0f, 0.1f, 0f, 0.3f, 0.2f, 0.3f),
                U(1, 0f, 0.3f, 0f, 0.28f, 0.2f, 0.28f),
                U(2, 0f, 0.5f, 0f, 0.26f, 0.2f, 0.26f),
                U(3, 0f, 0.7f, 0f, 0.26f, 0.2f, 0.26f),
                U(4, 0f, 0.92f, 0f, 0.32f, 0.24f, 0.32f),
                U(5, 0f, 0.5f, 0.16f, 0.08f, 0.08f, 0.06f, 0f, Yellow),
            },
        };

        public static bool HasRecipe(string word) => Recipes.ContainsKey(word);
        public static IEnumerable<string> RecipeWords => Recipes.Keys;

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
