using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Art
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class CuteFace : MonoBehaviour
    {
        public const string RootName = "Cute face";
        const float EyeX = .074f, EyeY = .04f;

        SkinnedMeshRenderer head;
        Transform root, headBone;
        CuteHair fallbackHair;
        string eyeStyle;
        Material hairMaterial, skinTemplate;
        readonly List<Transform> eyes = new List<Transform>();
        int blink = -1;

        public static CuteFace Attach(GameObject model)
        {
            if (!model) return null;
            SkinnedMeshRenderer face = null;
            foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.name == "SK_Head") { face = skin; break; }
            if (!face) return null;
            var existing = face.GetComponent<CuteFace>();
            if (existing) return existing;
            Transform bone = null;
            foreach (var b in face.bones) if (b && b.name == "head") { bone = b; break; }
            if (!bone) return null;
            var cute = face.gameObject.AddComponent<CuteFace>();
            cute.Build(face, bone);
            return cute;
        }

        void Build(SkinnedMeshRenderer face, Transform bone)
        {
            head = face;
            var shared = face.sharedMaterials;
            var skin = shared.Length > 0 ? shared[0] : null;
            Material hairTemplate = null;
            for (int i = 0; i < shared.Length; i++)
            {
                string canonical = shared[i] ? WardrobeRig.CanonicalMaterial(shared[i].name) : "";
                if (canonical == "hair") hairTemplate = shared[i];
                if (canonical == "eyes" || canonical == "hair") shared[i] = CuteShapes.Hidden(shared[i]);
            }
            face.sharedMaterials = shared;
            if (face.sharedMesh) blink = face.sharedMesh.GetBlendShapeIndex("Blink");

            var blush = CuteShapes.Paint(skin, "cute_blush", CuteShapes.Hex(0xff9db4), .15f);
            var mouth = CuteShapes.Paint(skin, "cute_mouth", CuteShapes.Hex(0x5b3440), .4f);

            headBone = bone;
            root = CuteShapes.Group(bone, RootName);
            root.gameObject.layer = face.gameObject.layer;
            skinTemplate = skin;
            Eyes(DefaultEyes);
            foreach (float side in new[] { -1f, 1f })
            {
                var cheekAt = CuteShapes.OnFace(side * .128f, -.012f, -.002f, out var cheekNormal);
                CuteShapes.Part(root, side < 0 ? "Cheek L" : "Cheek R", CuteShapes.Sphere, blush, cheekAt, CuteShapes.Facing(cheekNormal), new Vector3(.058f, .032f, .014f));
            }

            var smile = new List<Vector3>();
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 5f - 1f;
                smile.Add(CuteShapes.OnFace(t * .019f, -.034f - .009f * (1f - t * t), .003f, out _));
            }
            CuteShapes.Part(root, "Smile", CuteShapes.Tube(smile, .0042f, false, 8), mouth, Vector3.zero, Quaternion.identity, Vector3.one);
            if (hairTemplate)
            {
                hairMaterial = CuteHair.Paint(skin);
                fallbackHair = CuteHair.Create(root, "Hair", DefaultHair, hairMaterial);
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = face.gameObject.layer;
        }

        public static bool IsCodeBuilt(Transform node)
        {
            for (var t = node; t; t = t.parent)
                if (t.name == RootName || CuteWear.Handles(t.name)) return true;
            return false;
        }

        public static readonly string[] EyeStyles = { "Sparkle", "Button", "Dot", "Happy", "Starry", "Lashes", "Jelly" };
        public static string DefaultEyes = "Sparkle";

        public void UseEyes(string id)
        {
            if (!root || !skinTemplate) return;
            Eyes(id);
            foreach (var eye in eyes)
                foreach (var t in eye.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = head.gameObject.layer;
        }

        void Eyes(string style)
        {
            foreach (var old in eyes) if (old) { if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
            eyes.Clear();
            eyeStyle = style;
            var skin = skinTemplate;
            var ink = CuteShapes.Paint(skin, "cute_eye", CuteShapes.Hex(0x2a1d2b), .92f);
            var iris = CuteShapes.Paint(skin, "cute_iris", CuteShapes.Hex(0x6b4560), .9f);
            var shine = CuteShapes.Paint(skin, "cute_shine", Color.white, 0f, .9f);
            var sphere = CuteShapes.Sphere;
            foreach (float side in new[] { -1f, 1f })
            {
                var at = CuteShapes.OnFace(side * EyeX, EyeY, .001f, out var normal);
                var eye = CuteShapes.Group(root, side < 0 ? "Eye L" : "Eye R", at, CuteShapes.Facing(normal));
                float outer = -side;
                void Part(string name, Material m, float x, float y, float z, float w, float h, float d) =>
                    CuteShapes.Part(eye, name, sphere, m, new Vector3(x, y, z), Quaternion.identity, new Vector3(w, h, d));
                void Glossy()
                {
                    Part("Pupil", ink, 0f, 0f, 0f, .064f, .084f, .03f);
                    Part("Iris", iris, 0f, -.017f, .008f, .042f, .036f, .016f);
                    Part("Sparkle", shine, .011f, .018f, .013f, .026f, .028f, .01f);
                    Part("Glint", shine, -.013f, -.016f, .013f, .011f, .011f, .008f);
                }
                switch (style)
                {
                    case "Button":
                        Part("Pupil", ink, 0f, -.004f, 0f, .052f, .056f, .026f);
                        Part("Sparkle", shine, .01f, .008f, .011f, .016f, .016f, .008f);
                        break;
                    case "Dot":
                        Part("Pupil", ink, 0f, -.008f, 0f, .03f, .036f, .018f);
                        Part("Sparkle", shine, .006f, -.001f, .008f, .009f, .009f, .006f);
                        break;
                    case "Happy":
                    {
                        var arc = new List<Vector3>();
                        for (int i = 0; i <= 12; i++)
                        {
                            float t = i / 6f - 1f;
                            arc.Add(new Vector3(t * .026f, -.01f + .02f * (1f - t * t), .006f));
                        }
                        CuteShapes.Part(eye, "Arc", CuteShapes.Tube(arc, .0055f, false, 8), ink, Vector3.zero, Quaternion.identity, Vector3.one);
                        break;
                    }
                    case "Starry":
                    {
                        var violet = CuteShapes.Paint(skin, "cute_iris_violet", CuteShapes.Hex(0x8a5cc7), .9f);
                        Part("Pupil", ink, 0f, 0f, 0f, .064f, .084f, .03f);
                        Part("Iris", violet, 0f, -.012f, .008f, .05f, .052f, .018f);
                        CuteShapes.Part(eye, "Star", CuteShapes.Slab(CuteShapes.Star(.014f, .0065f), .004f), shine,
                            new Vector3(.008f, .016f, .015f), Quaternion.identity, Vector3.one);
                        Part("Glint", shine, -.013f, -.02f, .014f, .009f, .009f, .006f);
                        break;
                    }
                    case "Lashes":
                        Glossy();
                        foreach (float deg in new[] { 15f, 42f, 70f })
                        {
                            float a = deg * Mathf.Deg2Rad;
                            var root0 = new Vector3(outer * .03f * Mathf.Cos(a), .04f * Mathf.Sin(a), .006f);
                            var dir = new Vector3(outer * Mathf.Cos(a + .35f), Mathf.Sin(a + .35f), 0f);
                            var lash = new List<Vector3> { root0, root0 + dir * .009f, root0 + dir * .016f + new Vector3(outer * .003f, 0f, 0f) };
                            CuteShapes.Part(eye, "Lash", CuteShapes.Tube(lash, .003f, false, 6), ink, Vector3.zero, Quaternion.identity, Vector3.one);
                        }
                        break;
                    case "Jelly":
                    {
                        var deep = CuteShapes.Paint(skin, "cute_eye_teal", CuteShapes.Hex(0x1d4a57), .92f);
                        var teal = CuteShapes.Paint(skin, "cute_iris_teal", CuteShapes.Hex(0x3fb7ae), .9f);
                        Part("Pupil", deep, 0f, 0f, 0f, .064f, .084f, .03f);
                        Part("Iris", teal, 0f, -.01f, .008f, .05f, .058f, .018f);
                        Part("Core", deep, 0f, -.006f, .013f, .022f, .03f, .01f);
                        Part("Sparkle", shine, .012f, .02f, .015f, .022f, .024f, .008f);
                        Part("Glint", shine, -.012f, -.022f, .015f, .01f, .01f, .006f);
                        break;
                    }
                    default:
                        Glossy();
                        break;
                }
                eyes.Add(eye);
            }
        }

        public static string DefaultHair = "Swept";

        public void UseHair(string id)
        {
            if (!hairMaterial || !root) return;
            if (fallbackHair) { if (Application.isPlaying) Destroy(fallbackHair.gameObject); else DestroyImmediate(fallbackHair.gameObject); }
            fallbackHair = CuteHair.Create(root, "Hair", id, hairMaterial);
        }

        void LateUpdate()
        {
            if (!head || !root) return;
            bool shown = head.enabled && head.gameObject.activeInHierarchy;
            if (root.gameObject.activeSelf != shown) root.gameObject.SetActive(shown);
            if (!shown) return;
            float closed = blink >= 0 ? Mathf.Clamp01(head.GetBlendShapeWeight(blink) / 100f) : 0f;
            var scale = new Vector3(1f, Mathf.Lerp(1f, .1f, closed), 1f);
            foreach (var eye in eyes) if (eye) eye.localScale = scale;
            string wornHair = null, wornEyes = null;
            for (int i = 0; i < headBone.childCount; i++)
            {
                var child = headBone.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                if (child.name.StartsWith(CuteWear.HairPrefix, System.StringComparison.Ordinal)) wornHair = child.name;
                else if (child.name.StartsWith(CuteWear.EyesPrefix, System.StringComparison.Ordinal)) wornEyes = child.name.Substring(CuteWear.EyesPrefix.Length);
            }
            if (fallbackHair && fallbackHair.gameObject.activeSelf != (wornHair == null)) fallbackHair.gameObject.SetActive(wornHair == null);
            string wantEyes = wornEyes ?? DefaultEyes;
            if (wantEyes != eyeStyle) UseEyes(wantEyes);
        }

        void OnDestroy()
        {
            if (!root) return;
            if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject);
        }
    }
}
