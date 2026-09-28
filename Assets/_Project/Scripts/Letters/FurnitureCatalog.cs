using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// How furniture words look when spelled into existence (Moving Day). Unknown words get a plain
    /// wooden row of blocks, so new checklist items work without code changes.
    /// </summary>
    public static class FurnitureCatalog
    {
        struct Spec
        {
            public Vector3 block;
            public int perRow;
            public Color color;
            public float mass;
            public float health;
        }

        static Spec S(float r, float g, float b, float mass, float health = 25f, float block = 0.45f, int perRow = 0) =>
            new() { block = Vector3.one * block, perRow = perRow, color = new Color(r, g, b), mass = mass, health = health };

        static readonly Dictionary<string, Spec> Specs = new()
        {
            ["BED"] = new Spec { block = new Vector3(0.8f, 0.5f, 1.6f), color = new Color(0.45f, 0.6f, 0.85f), mass = 5f },
            ["SOFA"] = new Spec { block = new Vector3(0.75f, 0.7f, 0.8f), color = new Color(0.78f, 0.4f, 0.29f), mass = 5f },
            ["TABLE"] = new Spec { block = new Vector3(0.5f, 0.45f, 0.8f), color = new Color(0.66f, 0.45f, 0.29f), mass = 4f },
            ["DESK"] = new Spec { block = new Vector3(0.6f, 0.7f, 0.8f), color = new Color(0.54f, 0.35f, 0.23f), mass = 4.5f },
            ["LAMP"] = new Spec { block = Vector3.one * 0.4f, perRow = 1, color = new Color(0.95f, 0.83f, 0.54f), mass = 2f },
            ["CHAIR"] = new Spec { block = Vector3.one * 0.4f, perRow = 3, color = new Color(0.71f, 0.51f, 0.35f), mass = 3f },
            ["RUG"] = new Spec { block = new Vector3(0.9f, 0.08f, 1.4f), color = new Color(0.85f, 0.55f, 0.6f), mass = 2f },
            ["TV"] = new Spec { block = new Vector3(0.6f, 0.5f, 0.25f), color = new Color(0.25f, 0.25f, 0.3f), mass = 2f },
            ["PLANT"] = new Spec { block = Vector3.one * 0.4f, perRow = 1, color = new Color(0.44f, 0.68f, 0.35f), mass = 2.5f },
            ["CLOCK"] = new Spec { block = Vector3.one * 0.35f, perRow = 3, color = new Color(0.85f, 0.76f, 0.6f), mass = 2f },
            ["VASE"] = S(0.37f, 0.66f, 0.63f, 2.5f, 15f),
            ["MUG"] = S(0.37f, 0.56f, 0.78f, 0.5f, 8f),
            ["PLATE"] = S(0.95f, 0.94f, 0.9f, 0.6f, 8f),
            ["BOOKS"] = S(0.55f, 0.35f, 0.62f, 3f, 20f),
            ["PILLOW"] = S(0.9f, 0.64f, 0.7f, 1.5f, 12f),
            ["RADIO"] = S(0.88f, 0.54f, 0.24f, 2.5f, 18f),
            ["BOX"] = S(0.78f, 0.63f, 0.43f, 2f, 10f),
            // Living room extras
            ["PIANO"] = S(0.25f, 0.2f, 0.22f, 12f, 50f),
            ["STOOL"] = S(0.72f, 0.3f, 0.28f, 2f, 15f),
            ["FAN"] = S(0.9f, 0.9f, 0.88f, 2f, 15f),
            // Kitchen
            ["STOVE"] = S(0.92f, 0.9f, 0.85f, 10f, 40f),
            ["FRIDGE"] = S(0.93f, 0.95f, 0.97f, 14f, 50f),
            ["SINK"] = S(0.6f, 0.78f, 0.74f, 8f, 35f),
            ["PAN"] = S(0.3f, 0.3f, 0.33f, 1.5f, 15f),
            ["POT"] = S(0.75f, 0.35f, 0.3f, 2f, 15f),
            ["KETTLE"] = S(0.95f, 0.75f, 0.35f, 1.5f, 12f),
            ["CUP"] = S(0.95f, 0.92f, 0.85f, 0.4f, 6f),
            ["BOWL"] = S(0.55f, 0.72f, 0.88f, 0.8f, 8f),
            // Bedroom
            ["SHELF"] = S(0.66f, 0.47f, 0.32f, 5f, 30f),
            ["MIRROR"] = S(0.8f, 0.66f, 0.45f, 3f, 12f),
            ["TEDDY"] = S(0.72f, 0.52f, 0.34f, 1f, 15f),
            ["CLOSET"] = S(0.6f, 0.44f, 0.6f, 14f, 50f),
            // Garden
            ["TREE"] = S(0.52f, 0.36f, 0.22f, 16f, 60f),
            ["BUSH"] = S(0.36f, 0.62f, 0.32f, 4f, 20f),
            ["FLOWER"] = S(0.42f, 0.7f, 0.35f, 1f, 8f),
            ["ROSE"] = S(0.42f, 0.7f, 0.35f, 1f, 8f),
            ["BENCH"] = S(0.62f, 0.44f, 0.3f, 8f, 35f),
            ["FENCE"] = S(0.96f, 0.95f, 0.9f, 4f, 20f),
            ["SWING"] = S(0.55f, 0.4f, 0.28f, 6f, 30f),
            ["GNOME"] = S(0.3f, 0.5f, 0.85f, 1.5f, 12f),
            ["ROCK"] = S(0.6f, 0.6f, 0.62f, 6f, 50f),
            ["HOSE"] = S(0.35f, 0.65f, 0.3f, 1f, 10f),
            ["POND"] = S(0.45f, 0.68f, 0.9f, 3f, 20f),
        };

        static readonly Spec Default = new() { block = Vector3.one * 0.45f, color = new Color(0.8f, 0.6f, 0.42f), mass = 3f, health = 25f };

        public static bool Knows(string word) => Specs.ContainsKey(word);

        public static Smashable Spawn(string word, Vector3 position, float yaw, Transform parent)
        {
            var spec = Specs.TryGetValue(word, out var s) ? s : Default;
            return Furniture.Create(parent, word, position, yaw, spec.block, spec.perRow, spec.color, spec.mass,
                                    spec.health > 0f ? spec.health : 25f);
        }

        /// <summary>Spells a piece of furniture into existence just in front of the player.</summary>
        public static Smashable Summon(PlayerController p, string word)
        {
            var at = p.transform.position + p.Facing * 1.5f + Vector3.up * 0.2f;
            float yaw = Quaternion.LookRotation(p.Facing).eulerAngles.y;
            return Spawn(word, at, yaw, World.Transient);
        }
    }
}
