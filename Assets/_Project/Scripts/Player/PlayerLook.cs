using System;
using System.Collections.Generic;
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

        public static readonly string[] Hats = { "", "CAP", "HAT", "BOW", "CROWN", "BEANIE", "PARTY", "HALO", "TIARA", "CHEF" };
        static readonly Color[] HatColours =
        {
            default, new(0.25f, 0.45f, 0.85f), new(0.3f, 0.24f, 0.22f), new(0.95f, 0.45f, 0.65f),
            new(0.98f, 0.8f, 0.25f), new(0.85f, 0.3f, 0.3f), new(0.6f, 0.4f, 0.9f),
            new(1f, 0.93f, 0.55f), new(0.8f, 0.84f, 0.92f), new(0.97f, 0.97f, 0.95f),
        };

        public static readonly string[] Extras = { "", "SPECS", "SCARF", "TIE", "CAPE" };
        static readonly Color[] ExtraColours =
        {
            default, new(0.2f, 0.19f, 0.22f), new(0.85f, 0.25f, 0.3f), new(0.22f, 0.28f, 0.55f), new(0.72f, 0.18f, 0.24f),
        };

        // The roommate's shape, in its visual's space: a capsule 1.24 tall with a 0.42 radius, face towards +z.
        // The top of the head is a dome centred at y = 0.82; the eyes reach up to y = 1.085.
        const float HeadCentre = 0.82f, HeadRadius = 0.42f;

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
                Replace(p.visual, HatName, LetterBuilt.Wrapped(Hats[hat], HatColours[hat], p.visual, HatLetters(Hats[hat])).transform);

            int extra = Wrap(look.extra, Extras.Length);
            if (extra > 0)
            {
                string word = Extras[extra] == "SPECS" ? "OO" : Extras[extra];
                Replace(p.visual, ExtraName, LetterBuilt.Wrapped(word, ExtraColours[extra], p.visual, ExtraLetters(Extras[extra])).transform);
            }
        }

        /// <summary>A band hugging the head's dome from latitude <paramref name="fromDeg"/> upwards.</summary>
        static LetterBend.Wrap Dome(float fromDeg, float height, float thickness, float lift = 0.012f)
        {
            float lat = fromDeg * Mathf.Deg2Rad;
            float r = HeadRadius + lift + thickness * 0.5f;
            return new LetterBend.Wrap
            {
                radius = r * Mathf.Cos(lat), y = HeadCentre + r * Mathf.Sin(lat),
                lean = fromDeg, curl = r, height = height, thickness = thickness,
            };
        }

        static IEnumerable<(int, LetterBend.Wrap)> HatLetters(string hat)
        {
            switch (hat)
            {
                case "CAP":
                {
                    // C and A curve over the top of the head; P lies flat out front as the peak.
                    foreach (var l in LetterBend.Around(hat, 0, 2, Dome(40f, 0.39f, 0.06f), 360f, 2f)) yield return l;
                    var dome = Dome(40f, 0f, 0.06f);
                    yield return (2, new LetterBend.Wrap { radius = dome.radius + 0.26f, y = dome.y - 0.02f, lean = 88f, width = 0.8f, height = 0.28f, thickness = 0.04f });
                    break;
                }
                case "HAT":
                {
                    // A bowler: H and A make the dome; the T lies flat all the way round, upside down so its bar is the brim's edge.
                    var dome = Dome(40f, 0.41f, 0.08f);
                    foreach (var l in LetterBend.Around(hat, 0, 2, dome, 360f, 2f)) yield return l;
                    foreach (var l in LetterBend.Around(hat, 2, 1, new LetterBend.Wrap { radius = dome.radius + 0.14f, y = dome.y - 0.03f, height = 0.17f, thickness = 0.04f, lean = 90f, roll = 180f }, 360f, 0f)) yield return l;
                    break;
                }
                case "BOW":
                {
                    // B and W are the loops, O the knot, perched on the side of the head.
                    var knot = new LetterBend.Wrap { axis = new Vector3(0.14f, 0f, -1.35f), radius = 1.55f, thickness = 0.07f };
                    yield return (0, With(knot, angle: 5.5f, y: 1.13f, width: 0.26f, height: 0.24f, roll: 18f));
                    yield return (1, With(knot, angle: 0f, y: 1.18f, width: 0.12f, height: 0.13f));
                    yield return (2, With(knot, angle: -5.5f, y: 1.13f, width: 0.28f, height: 0.24f, roll: -18f));
                    break;
                }
                case "CROWN":
                    // The points of the W and N make the crown's spikes.
                    foreach (var l in LetterBend.Around(hat, 0, 5, new LetterBend.Wrap { radius = 0.27f, y = 1.15f, height = 0.22f, thickness = 0.05f, lean = -10f }, 360f)) yield return l;
                    break;
                case "BEANIE":
                    foreach (var l in LetterBend.Around(hat, 0, 6, Dome(40f, 0.41f, 0.08f), 360f, 1f)) yield return l;
                    break;
                case "PARTY":
                    // A cone: the letters lean in until they meet at the tip.
                    foreach (var l in LetterBend.Around(hat, 0, 5, new LetterBend.Wrap { radius = 0.25f, y = 1.17f, height = 0.5f, thickness = 0.06f, lean = 28f }, 360f, 2f)) yield return l;
                    break;
                case "HALO":
                    // Lying flat and floating above the head.
                    foreach (var l in LetterBend.Around(hat, 0, 4, new LetterBend.Wrap { radius = 0.36f, y = 1.45f, height = 0.1f, thickness = 0.05f, lean = 90f }, 360f, 8f)) yield return l;
                    break;
                case "TIARA":
                    foreach (var l in LetterBend.Around(hat, 0, 5, new LetterBend.Wrap { radius = 0.33f, y = 1.1f, height = 0.15f, thickness = 0.04f, lean = 8f }, 150f, 2f)) yield return l;
                    break;
                case "CHEF":
                    // Flaring out and curling back over the top into a puff, like a mushroom.
                    foreach (var l in LetterBend.Around(hat, 0, 4, new LetterBend.Wrap { radius = 0.24f, y = 1.17f, height = 0.5f, thickness = 0.07f, lean = -40f, curl = 0.22f }, 360f, 1f)) yield return l;
                    break;
            }
        }

        static IEnumerable<(int, LetterBend.Wrap)> ExtraLetters(string extra)
        {
            switch (extra)
            {
                case "SPECS":
                    // Two O's curved over the eyes.
                    var lens = new LetterBend.Wrap { radius = 0.52f, y = 0.915f, width = 0.19f, height = 0.18f, thickness = 0.035f, lean = 12f };
                    yield return (0, With(lens, angle: 19f));
                    yield return (1, With(lens, angle: -19f));
                    break;
                case "SCARF":
                    // S C A R wrap round the neck; the F hangs down the front as the loose end.
                    foreach (var l in LetterBend.Around(extra, 0, 4, new LetterBend.Wrap { radius = 0.46f, y = 0.7f, height = 0.2f, thickness = 0.13f, lean = 4f }, 360f, 1f)) yield return l;
                    yield return (4, new LetterBend.Wrap { angle = 38f, radius = 0.54f, y = 0.32f, width = 0.22f, height = 0.38f, thickness = 0.1f, lean = 6f, roll = -8f });
                    break;
                case "TIE":
                    // T is the knot under the chin, I the long middle, E the tip.
                    yield return (0, new LetterBend.Wrap { radius = 0.47f, y = 0.64f, width = 0.3f, height = 0.16f, thickness = 0.06f });
                    yield return (1, new LetterBend.Wrap { radius = 0.47f, y = 0.42f, width = 0.14f, height = 0.22f, thickness = 0.06f });
                    yield return (2, new LetterBend.Wrap { radius = 0.415f, y = 0.22f, width = 0.22f, height = 0.2f, thickness = 0.06f, lean = -13f });
                    break;
                case "CAPE":
                    // Round the back, flaring out towards the floor.
                    foreach (var l in LetterBend.Around(extra, 0, 4, new LetterBend.Wrap { angle = 180f, radius = 0.57f, y = 0.1f, height = 0.68f, thickness = 0.07f, lean = 10f }, 160f, 1f)) yield return l;
                    break;
            }
        }

        static LetterBend.Wrap With(LetterBend.Wrap w, float angle, float? y = null, float? width = null, float? height = null, float roll = 0f)
        {
            w.angle = angle;
            if (y.HasValue) w.y = y.Value;
            if (width.HasValue) w.width = width.Value;
            if (height.HasValue) w.height = height.Value;
            w.roll = roll;
            return w;
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
