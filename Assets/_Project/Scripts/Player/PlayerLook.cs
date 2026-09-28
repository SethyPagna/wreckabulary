using System;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>How a roommate looks: body colour, the letter on their sweater, a hat and an extra.</summary>
    [Serializable]
    public struct PlayerLook
    {
        public int colour;
        public char initial;
        public int hat;
        public int extra;
    }

    /// <summary>
    /// The wardrobe's options. In keeping with everything else, hats and extras are spelled out in little
    /// letters: a CROWN is the word CROWN sitting on your head, SPECS are two O's in front of your eyes.
    /// </summary>
    public static class Looks
    {
        public static readonly (string name, Color color)[] Colours =
        {
            ("Coral", new Color(0.91f, 0.34f, 0.29f)),
            ("Sky", new Color(0.25f, 0.56f, 0.88f)),
            ("Leaf", new Color(0.36f, 0.73f, 0.39f)),
            ("Sunny", new Color(0.95f, 0.70f, 0.24f)),
            ("Grape", new Color(0.56f, 0.38f, 0.78f)),
            ("Teal", new Color(0.2f, 0.66f, 0.64f)),
            ("Peach", new Color(0.98f, 0.62f, 0.45f)),
            ("Rose", new Color(0.93f, 0.46f, 0.66f)),
        };

        public static readonly string[] Hats = { "", "CAP", "HAT", "BOW", "CROWN", "BEANIE", "PARTY" };
        static readonly Color[] HatColours =
        {
            default, new(0.25f, 0.45f, 0.85f), new(0.45f, 0.3f, 0.2f), new(0.95f, 0.45f, 0.65f),
            new(0.98f, 0.8f, 0.25f), new(0.85f, 0.3f, 0.3f), new(0.6f, 0.4f, 0.9f),
        };

        public static readonly string[] Extras = { "", "SPECS", "SCARF" };

        public const string HatName = "Hat";
        public const string ExtraName = "Extra";

        /// <summary>The look a slot starts with: the classic four colours and W, O, R, D.</summary>
        public static PlayerLook Default(int index) => new()
        {
            colour = index % 4,
            initial = GameAssets.I.PlayerInitial(index),
        };

        public static PlayerLook Random()
        {
            var r = new System.Random();
            return new PlayerLook
            {
                colour = r.Next(Colours.Length),
                initial = (char)('A' + r.Next(26)),
                hat = r.Next(Hats.Length),
                extra = r.Next(Extras.Length),
            };
        }

        public static string ColourName(PlayerLook l) => Colours[Wrap(l.colour, Colours.Length)].name;
        public static string HatLabel(PlayerLook l) => Hats[Wrap(l.hat, Hats.Length)] is { Length: > 0 } h ? h : "None";
        public static string ExtraLabel(PlayerLook l) => Extras[Wrap(l.extra, Extras.Length)] is { Length: > 0 } e ? e : "None";

        public static int Wrap(int i, int n) => ((i % n) + n) % n;

        /// <summary>Dresses a roommate: colour and sweater letter, then swaps the hat and extra.</summary>
        public static void Apply(PlayerController p, PlayerLook look)
        {
            var colour = Colours[Wrap(look.colour, Colours.Length)].color;
            char initial = look.initial is >= 'A' and <= 'Z' ? look.initial : 'W';
            p.SetAppearance(colour, initial);
            p.Look = look;
            if (!p.visual) return;

            Replace(p.visual, HatName, null);
            Replace(p.visual, ExtraName, null);

            int hat = Wrap(look.hat, Hats.Length);
            if (hat > 0)
            {
                var built = LetterBuilt.Spawn(Hats[hat], Vector3.one * 0.14f, 0, HatColours[hat], p.visual, colliders: false);
                Replace(p.visual, HatName, built.transform);
                built.transform.localPosition = new Vector3(0f, 1.25f, 0.02f);
            }

            switch (Extras[Wrap(look.extra, Extras.Length)])
            {
                case "SPECS":
                    // Two O's in front of the eyes.
                    var specs = LetterBuilt.Spawn("OO", new Vector3(0.2f, 0.2f, 0.04f), 0, new Color(0.2f, 0.19f, 0.22f), p.visual, colliders: false);
                    Replace(p.visual, ExtraName, specs.transform);
                    specs.transform.localPosition = new Vector3(0f, 0.9f, 0.44f);
                    break;
                case "SCARF":
                    var scarf = LetterBuilt.Spawn("SCARF", new Vector3(0.14f, 0.14f, 0.06f), 0, new Color(0.85f, 0.25f, 0.3f), p.visual, colliders: false);
                    Replace(p.visual, ExtraName, scarf.transform);
                    scarf.transform.localPosition = new Vector3(0f, 0.72f, 0.46f);
                    break;
            }
        }

        static void Replace(Transform parent, string name, Transform replacement)
        {
            var old = parent.Find(name);
            if (old && old != replacement)
            {
                // Rename first so a Find straight after doesn't pick up the one being destroyed.
                old.name = name + " (old)";
                if (Application.isPlaying) UnityEngine.Object.Destroy(old.gameObject);
                else UnityEngine.Object.DestroyImmediate(old.gameObject);
            }
            if (replacement) replacement.name = name;
        }
    }
}
