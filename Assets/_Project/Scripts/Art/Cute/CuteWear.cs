using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Art
{
    public static class CuteWear
    {
        public const string Prefix = "Cute_";
        public const string TintMaterial = "cute_main";
        static readonly Vector3 EyeL = new Vector3(-.074f, .04f, 0f), EyeR = new Vector3(.074f, .04f, 0f);

        public const string HairPrefix = "Cute_Hair_", EyesPrefix = "Cute_Eyes_";

        public static bool Handles(string rigid) => !string.IsNullOrEmpty(rigid) && rigid.StartsWith(Prefix, System.StringComparison.Ordinal);

        public static readonly string[] HairCovers = { "SK_Hood", "SK_Cap", "Sig_Beanie", "Sig_Headphones" };

        public static bool CoversHair(string name) => !string.IsNullOrEmpty(name) && System.Array.IndexOf(HairCovers, name) >= 0;

        public static bool IsHeadDecor(string rigid) => Handles(rigid) &&
            !rigid.StartsWith(HairPrefix, System.StringComparison.Ordinal) && !rigid.StartsWith(EyesPrefix, System.StringComparison.Ordinal) &&
            !rigid.Contains("Specs") && !rigid.Contains("Shades");

        public static Transform Build(string rigid, Transform bone, Material template)
        {
            if (!Handles(rigid) || !bone || !template) return null;
            if (rigid.StartsWith(HairPrefix, System.StringComparison.Ordinal))
                return CuteHair.Create(bone, rigid, rigid.Substring(HairPrefix.Length), CuteHair.Paint(template))?.transform;
            if (rigid.StartsWith(EyesPrefix, System.StringComparison.Ordinal))
            {
                var marker = CuteShapes.Group(bone, rigid);
                marker.gameObject.layer = bone.gameObject.layer;
                return marker;
            }
            var root = CuteShapes.Group(bone, rigid);
            var main = CuteShapes.Paint(template, TintMaterial, Color.white, .55f);
            var pink = CuteShapes.Paint(template, "cute_inner", CuteShapes.Hex(0xffb3c8), .3f);
            bool built = true;
            switch (rigid)
            {
                case "Cute_RoundSpecs": Specs(root, main, null, null); break;
                case "Cute_HeartShades": Specs(root, main, CuteShapes.Heart(.12f),CuteShapes.Paint(template, "cute_lens_rose", CuteShapes.Hex(0x8a2f5a), .95f)); break;
                case "Cute_StarShades": Specs(root, main, Rounded(CuteShapes.Star(.07f, .042f), 3),CuteShapes.Paint(template, "cute_lens_night", CuteShapes.Hex(0x3b3266), .95f)); break;
                case "Cute_CatEars": CatEars(root, main, pink); break;
                case "Cute_BearEars": BearEars(root, main, pink); break;
                case "Cute_BunnyEars": BunnyEars(root, main, pink); break;
                case "Cute_Bow": Bow(root, main); break;
                case "Cute_FlowerClip": Flower(root, main, CuteShapes.Paint(template, "cute_pollen", CuteShapes.Hex(0xffd65c), .3f)); break;
                case "Cute_Sprout": Sprout(root, CuteShapes.Paint(template, "cute_leaf", CuteShapes.Hex(0x7fd66a), .45f)); break;
                default: built = false; break;
            }
            if (!built)
            {
                if (Application.isPlaying) Object.Destroy(root.gameObject); else Object.DestroyImmediate(root.gameObject);
                return null;
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = bone.gameObject.layer;
            return root;
        }

        static void Specs(Transform root, Material frame, List<Vector2> lensShape, Material lens)
        {
            const float Out = .013f;
            foreach (var eye in new[] { EyeL, EyeR })
            {
                var centre = CuteShapes.OnFace(eye.x, eye.y, Out, out var normal);
                var facing = CuteShapes.Facing(normal);
                var outline = lensShape ?? CuteShapes.Circle(.058f, 40);
                var rim = new List<Vector3>();
                var shape = lensShape != null ? Grow(outline, .006f) : outline;
                foreach (var p in shape) rim.Add(centre + facing * new Vector3(p.x, p.y, 0f));
                CuteShapes.Part(root, "Rim", CuteShapes.Tube(rim, .0075f, true, 8), frame, Vector3.zero, Quaternion.identity, Vector3.one);
                if (lens) CuteShapes.Part(root, "Lens", CuteShapes.Slab(outline, .006f), lens, centre, facing, Vector3.one);

                float side = Mathf.Sign(eye.x);
                var corner = shape[0];
                foreach (var p in shape) if (side * p.x + .35f * p.y > side * corner.x + .35f * corner.y) corner = p;
                var start = centre + facing * new Vector3(corner.x, corner.y, 0f);
                var hinge = centre + facing * new Vector3(corner.x + side * .006f, corner.y, -.008f);
                var from = (hinge - CuteShapes.HeadCentre).normalized;
                var to = new Vector3(side, (hinge.y - CuteShapes.HeadCentre.y) / CuteShapes.HeadRadii.y, .25f).normalized;
                float rise = Vector3.Distance(hinge, CuteShapes.OnHead(from, 0f, out _));
                var temple = new List<Vector3> { start, hinge };
                for (int i = 1; i <= 10; i++)
                {
                    float t = i / 10f;
                    temple.Add(CuteShapes.OnHead(Vector3.Slerp(from, to, t), Mathf.Lerp(rise, .004f, Mathf.SmoothStep(0f, 1f, t)), out _));
                }
                CuteShapes.Part(root, "Arm", CuteShapes.Tube(temple, .0036f, false, 6), frame, Vector3.zero, Quaternion.identity, Vector3.one);
            }
            var bridge = new List<Vector3>();
            for (int i = 0; i <= 6; i++)
            {
                float t = i / 3f - 1f;
                bridge.Add(CuteShapes.OnFace(t * .019f, .056f + .006f * (1f - t * t), Out + .002f, out _));
            }
            CuteShapes.Part(root, "Bridge", CuteShapes.Tube(bridge, .006f, false, 6), frame, Vector3.zero, Quaternion.identity, Vector3.one);
        }

        static List<Vector2> Grow(List<Vector2> outline, float by)
        {
            var grown = new List<Vector2>(outline.Count);
            foreach (var p in outline) grown.Add(p + p.normalized * by);
            return grown;
        }

        static List<Vector2> Rounded(List<Vector2> outline, int passes)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                var next = new List<Vector2>(outline.Count * 2);
                for (int i = 0; i < outline.Count; i++)
                {
                    Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                    next.Add(Vector2.Lerp(a, b, .25f)); next.Add(Vector2.Lerp(a, b, .75f));
                }
                outline = next;
            }
            return outline;
        }

        static void CatEars(Transform root, Material main, Material inner)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var at = CuteShapes.OnHead(new Vector3(side * .6f, .8f, -.02f), .012f, out var normal);
                var tilt = Quaternion.AngleAxis(-side * 10f, Vector3.forward) * CuteShapes.Upright(normal);
                var ear = CuteShapes.Group(root, "Ear", at, tilt);
                CuteShapes.Part(ear, "Outer", CuteShapes.Puff, main, Vector3.zero, Quaternion.identity, new Vector3(.12f, .1f, .07f), true);
                CuteShapes.Part(ear, "Inner", CuteShapes.Puff, inner, new Vector3(0f, .012f, .02f), Quaternion.identity, new Vector3(.07f, .07f, .034f));
            }
        }

        static void BearEars(Transform root, Material main, Material inner)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var at = CuteShapes.OnHead(new Vector3(side * .64f, .77f, -.1f), .04f, out var normal);
                var ear = CuteShapes.Group(root, "Ear", at, CuteShapes.Upright(normal));
                CuteShapes.Part(ear, "Outer", CuteShapes.Sphere, main, Vector3.zero, Quaternion.identity, new Vector3(.1f, .1f, .055f), true);
                CuteShapes.Part(ear, "Inner", CuteShapes.Sphere, inner, new Vector3(0f, .004f, .021f), Quaternion.identity, new Vector3(.058f, .058f, .018f));
            }
        }

        static void BunnyEars(Transform root, Material main, Material inner)
        {
            foreach (float side in new[] { -1f, 1f })
            {
                var at = CuteShapes.OnHead(new Vector3(side * .3f, .95f, .02f), .02f, out var normal);
                var tilt = Quaternion.AngleAxis(-side * 14f, Vector3.forward) * CuteShapes.Upright(normal);
                var ear = CuteShapes.Group(root, "Ear", at, tilt);
                CuteShapes.Part(ear, "Outer", CuteShapes.Sphere, main, new Vector3(0f, .1f, 0f), Quaternion.identity, new Vector3(.06f, .22f, .04f), true);
                CuteShapes.Part(ear, "Inner", CuteShapes.Sphere, inner, new Vector3(0f, .1f, .013f), Quaternion.identity, new Vector3(.032f, .16f, .016f));
            }
        }

        static void Bow(Transform root, Material main)
        {
            var at = CuteShapes.OnHead(new Vector3(.5f, .78f, -.3f), .02f, out var normal);
            var bow = CuteShapes.Group(root, "Bow", at, CuteShapes.Facing(normal) * Quaternion.AngleAxis(-20f, Vector3.forward));
            foreach (float side in new[] { -1f, 1f })
            {
                var loop = Quaternion.AngleAxis(side * 90f, Vector3.forward);
                CuteShapes.Part(bow, "Loop", CuteShapes.Puff, main, new Vector3(side * .074f, 0f, .01f), loop, new Vector3(.09f, .075f, .045f), true);
            }
            CuteShapes.Part(bow, "Knot", CuteShapes.Sphere, main, new Vector3(0f, 0f, .016f), Quaternion.identity, new Vector3(.036f, .04f, .032f));
        }

        static void Flower(Transform root, Material petals, Material middle)
        {
            var at = CuteShapes.OnHead(new Vector3(.78f, .5f, -.38f), .04f, out var normal);
            var flower = CuteShapes.Group(root, "Flower", at, CuteShapes.Facing(normal));
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + .3f;
                var p = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * .038f;
                CuteShapes.Part(flower, "Petal", CuteShapes.Sphere, petals, p, Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg), new Vector3(.06f, .048f, .022f), true);
            }
            CuteShapes.Part(flower, "Middle", CuteShapes.Sphere, middle, new Vector3(0f, 0f, .008f), Quaternion.identity, new Vector3(.036f, .036f, .022f));
        }

        static void Sprout(Transform root, Material leaf)
        {
            var top = CuteShapes.OnHead(new Vector3(.04f, 1f, -.05f), -.004f, out _);
            var stem = new List<Vector3> { top, top + new Vector3(0f, .03f, -.004f), top + new Vector3(.006f, .055f, -.002f), top + new Vector3(.004f, .072f, .004f) };
            CuteShapes.Part(root, "Stem", CuteShapes.Tube(stem, .006f, false, 6), leaf, Vector3.zero, Quaternion.identity, Vector3.one, true);
            var tip = stem[stem.Count - 1];
            foreach (float side in new[] { -1f, 1f })
            {
                var rot = Quaternion.Euler(0f, side * 10f, side * 28f);
                CuteShapes.Part(root, "Leaf", CuteShapes.Sphere, leaf, tip + new Vector3(side * .03f, .008f, 0f), rot, new Vector3(.062f, .024f, .036f), true);
            }
        }
    }
}
