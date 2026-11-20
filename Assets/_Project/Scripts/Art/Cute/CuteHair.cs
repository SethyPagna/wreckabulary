using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Art
{
    [DisallowMultipleComponent]
    public sealed class CuteHair : MonoBehaviour
    {
        public const string MaterialName = "cute_hair";
        public static readonly Color DefaultColour = CuteShapes.Hex(0x4a3229);

        MeshFilter shell;
        Mesh fullHair, flatHair;
        Transform buns, headBone;
        readonly List<Renderer> hats = new List<Renderer>();

        public string Style { get; private set; }

        public static Material Paint(Material template) => CuteShapes.Paint(template, MaterialName, DefaultColour, .55f);

        public static CuteHair Create(Transform parent, string name, string style, Material material)
        {
            if (!parent || !material) return null;
            var group = CuteShapes.Group(parent, name);
            var hair = group.gameObject.AddComponent<CuteHair>();
            hair.Build(FindHair(style), material);
            foreach (var t in group.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = parent.gameObject.layer;
            return hair;
        }

        void Build(HairStyle style, Material material)
        {
            Style = style.Id;
            fullHair = HairShell(style, false);
            flatHair = HairShell(style, true);
            shell = CuteShapes.Part(transform, "Shell", fullHair, material, Vector3.zero, Quaternion.identity, Vector3.one, true).GetComponent<MeshFilter>();
            if (style.Buns)
            {
                buns = CuteShapes.Group(transform, "Buns");
                foreach (float side in new[] { -1f, 1f })
                {
                    var at = CuteShapes.OnHead(new Vector3(side * .55f, .8f, .12f).normalized, .045f, out var normal);
                    CuteShapes.Part(buns, "Bun", CuteShapes.Sphere, material, at, CuteShapes.Upright(normal), new Vector3(.12f, .11f, .12f), true);
                }
            }
            for (var t = transform.parent; t; t = t.parent)
                if (t.name == "head") { headBone = t; break; }
            Transform model = headBone;
            while (model && !model.GetComponent<Animator>()) model = model.parent;
            if (model)
                foreach (var other in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (other.name == "SK_Hood" || other.name == "SK_Cap") hats.Add(other);
        }

        void LateUpdate()
        {
            if (!shell || !headBone) return;
            bool tucked = HatOn();
            var mesh = tucked ? flatHair : fullHair;
            if (shell.sharedMesh != mesh) shell.sharedMesh = mesh;
            bool hideBuns = tucked || HeadDecorOn();
            if (buns && buns.gameObject.activeSelf == hideBuns) buns.gameObject.SetActive(!hideBuns);
        }

        bool HeadDecorOn()
        {
            for (int i = 0; i < headBone.childCount; i++)
            {
                var child = headBone.GetChild(i);
                if (child.gameObject.activeSelf && CuteWear.IsHeadDecor(child.name)) return true;
            }
            return false;
        }

        bool HatOn()
        {
            foreach (var hat in hats) if (hat && hat.enabled) return true;
            for (int i = 0; i < headBone.childCount; i++)
            {
                var child = headBone.GetChild(i);
                if ((child.name == "Sig_Beanie" || child.name == "Sig_Headphones") && child.gameObject.activeSelf) return true;
            }
            return false;
        }

        void OnDestroy()
        {
            foreach (var mesh in new[] { fullHair, flatHair })
                if (mesh) { if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
        }

        public sealed class HairStyle
        {
            public string Id;
            public float Front, Side, Back, FrontEnd = .5f, SideAt = 1.5f;
            public (float at, float half, float drop)[] Locks = new (float, float, float)[0];
            public float Puff;
            public bool Buns;

            public HairStyle Ring(int count, float drop, float offset = 0f)
            {
                var ring = new List<(float, float, float)>(Locks);
                for (int i = 0; i < count; i++)
                    ring.Add((Wrap(offset + i * Mathf.PI * 2f / count), Mathf.PI / count * 1.35f, drop));
                Locks = ring.ToArray();
                return this;
            }
        }

        static (float, float, float)[] Mirror(params (float at, float half, float drop)[] side)
        {
            var both = new List<(float, float, float)>();
            foreach (var (at, half, drop) in side) { both.Add((at, half, drop)); both.Add((-at, half, drop)); }
            return both.ToArray();
        }

        public static readonly HairStyle[] HairStyles =
        {
            new HairStyle
            {
                Id = "Swept", Front = .56f, Side = -.1f, Back = -.4f,
                Locks = new[]
                {
                    (-1.05f, .26f, .4f), (-.66f, .26f, .33f), (-.27f, .26f, .29f), (.14f, .24f, .2f), (.53f, .26f, .27f), (.94f, .26f, .37f),
                    (1.45f, .3f, .1f), (-1.45f, .3f, .1f), (2.25f, .36f, .1f), (-2.25f, .36f, .1f), (2.85f, .36f, .13f), (-2.85f, .36f, .13f),
                },
            },
            new HairStyle { Id = "Bob", Front = .32f, Side = -.48f, Back = -.46f, FrontEnd = .8f, SideAt = 1.45f, Puff = .012f }.Ring(26, .045f),
            new HairStyle { Id = "Wavy", Front = .5f, Side = -.32f, Back = -.46f, FrontEnd = .6f, Puff = .014f }.Ring(9, .22f, .35f),
            new HairStyle
            {
                Id = "Buns", Front = .58f, Side = -.08f, Back = -.36f, Buns = true,
                Locks = Mirror((.29f, .26f, .26f), (.72f, .26f, .33f), (1.08f, .24f, .38f), (2.55f, .4f, .1f), (Mathf.PI, .4f, .12f)),
            },
        };

        public static string DefaultHair = "Swept";

        public static HairStyle FindHair(string id)
        {
            foreach (var style in HairStyles) if (style.Id == id) return style;
            return HairStyles[0];
        }

        const int HairCols = 240, HairRows = 30, HairRim = 7;

        static float Wrap(float angle) => Mathf.Repeat(angle + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;

        static float Hairline(HairStyle style, float azimuth, out float cover, out float groove)
        {
            float a = Mathf.Abs(azimuth);
            float line = a < style.SideAt
                ? Mathf.Lerp(style.Front, style.Side, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(style.FrontEnd, style.SideAt, a)))
                : Mathf.Lerp(style.Side, style.Back, Mathf.SmoothStep(0f, 1f, (a - style.SideAt) / (Mathf.PI - style.SideAt)));
            float drop = 0f; cover = 0f; bool near = false;
            foreach (var (at, half, depth) in style.Locks)
            {
                float x = Mathf.Abs(Wrap(azimuth - at)) / half;
                if (x < 1.5f) near = true;
                if (x >= 1f) continue;
                float round = Mathf.Sqrt(1f - x * x);
                drop = Mathf.Max(drop, depth * round);
                cover = Mathf.Max(cover, round);
            }
            groove = near ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.5f, .95f, 1f - cover)) : 0f;
            return Mathf.Clamp(line - drop, -.7f, .9f);
        }

        static float Thickness(HairStyle style, Vector3 d, float along, float cover, float groove, bool flat)
        {
            if (flat) return .007f;
            float lift = .018f + .012f * Mathf.Max(0f, d.y) + .01f * Mathf.Max(0f, d.z) + style.Puff * (1f - Mathf.Abs(d.y));
            float tip = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, 1f, along));
            lift += .005f * cover * tip;
            lift *= 1f - .5f * groove * tip;
            return lift;
        }

        static Vector3 Around(float polar, Vector3 flat) => Mathf.Cos(polar) * Vector3.up + Mathf.Sin(polar) * flat;

        static Mesh HairShell(HairStyle style, bool flat)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            int stride = HairRows + 1 + HairRim;
            for (int j = 0; j < HairCols; j++)
            {
                float azimuth = (j / (float)HairCols - .5f) * Mathf.PI * 2f;
                var outward = new Vector3(Mathf.Sin(azimuth), 0f, -Mathf.Cos(azimuth));
                float edge = Mathf.Acos(Hairline(style, azimuth, out float cover, out float groove));
                for (int i = 0; i <= HairRows; i++)
                {
                    float along = i / (float)HairRows;
                    var d = Around(edge * along, outward);
                    verts.Add(CuteShapes.OnHead(d, Thickness(style, d, along, cover, groove, flat), out _));
                }
                float lift = Thickness(style, Around(edge, outward), 1f, cover, groove, flat);
                for (int k = 1; k <= HairRim; k++)
                {
                    float t = k / (float)HairRim * Mathf.PI * .5f;
                    var d = Around(edge + Mathf.Sin(t) * lift * 2.6f, outward);
                    verts.Add(CuteShapes.OnHead(d, lift * Mathf.Cos(t) - (k == HairRim ? .003f : 0f), out _));
                }
            }
            for (int j = 0; j < HairCols; j++)
                for (int i = 0; i < stride - 1; i++)
                {
                    int a = j * stride + i, b = (j + 1) % HairCols * stride + i;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(b); tris.Add(b + 1); tris.Add(a + 1);
                }
            var mesh = new Mesh { name = $"Cute hair {style.Id}{(flat ? " (under hat)" : "")}" };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
