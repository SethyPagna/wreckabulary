using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Art
{
    public static class CuteShapes
    {
        public static readonly Vector3 HeadCentre = new Vector3(0f, .06f, -.04f);
        public static readonly Vector3 HeadRadii = new Vector3(.224f, .215f, .175f);

        static Mesh sphere, cone;
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { sphere = null; cone = null; materials.Clear(); hiddenSource = null; }

        public static Vector3 OnFace(float x, float y, float lift, out Vector3 normal)
        {
            var c = HeadCentre; var r = HeadRadii;
            float u = (x - c.x) / r.x, v = (y - c.y) / r.y;
            float w = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u - v * v));
            var p = new Vector3(x, y, c.z - r.z * w);
            normal = Normal(p);
            return p + normal * lift;
        }

        public static Vector3 OnHead(Vector3 dir, float lift, out Vector3 normal)
        {
            dir.Normalize();
            var r = HeadRadii;
            float t = 1f / Mathf.Sqrt(Sq(dir.x / r.x) + Sq(dir.y / r.y) + Sq(dir.z / r.z));
            var p = HeadCentre + dir * t;
            normal = Normal(p);
            return p + normal * lift;
        }

        static Vector3 Normal(Vector3 p)
        {
            var c = HeadCentre; var r = HeadRadii;
            return new Vector3((p.x - c.x) / (r.x * r.x), (p.y - c.y) / (r.y * r.y), (p.z - c.z) / (r.z * r.z)).normalized;
        }

        static float Sq(float x) => x * x;

        public static Quaternion Upright(Vector3 normal)
        {
            var forward = Vector3.ProjectOnPlane(Vector3.back, normal);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(Vector3.up, normal);
            return Quaternion.LookRotation(forward, normal);
        }

        public static Quaternion Facing(Vector3 normal) => Quaternion.LookRotation(normal, Vector3.up);

        public static Mesh Sphere => sphere ? sphere : sphere = BuiltinSphere();

        static Mesh BuiltinSphere()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            go.SetActive(false);
            if (Application.isPlaying) Object.Destroy(go); else Object.DestroyImmediate(go);
            return mesh;
        }

        public static Mesh Cone => cone ? cone : cone = BuildCone(20);

        public static Mesh Puff => puff ? puff : puff = BuildPuff(24, 14);
        static Mesh puff;

        static Mesh BuildPuff(int sides, int rings)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                float radius = .5f * (1f - .5f * t) * Mathf.Sqrt(Mathf.Max(0f, 1f - t * t));
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * radius, t, Mathf.Sin(a) * radius));
                }
            }
            for (int ring = 0; ring < rings; ring++)
                for (int s = 0; s < sides; s++)
                {
                    int a = ring * (sides + 1) + s, b = a + sides + 1;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            int centre = verts.Count;
            verts.Add(new Vector3(0f, -.08f, 0f));
            for (int s = 0; s < sides; s++) { tris.Add(centre); tris.Add(s); tris.Add(s + 1); }
            var mesh = new Mesh { name = "Cute puff" };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildCone(int sides)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            int rings = 6;
            for (int ring = 0; ring <= rings; ring++)
            {
                float t = ring / (float)rings;
                float radius = .5f * (1f - t) + .5f * .14f * Mathf.Sin(t * Mathf.PI) * (1f - t);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    verts.Add(new Vector3(Mathf.Cos(a) * radius, t, Mathf.Sin(a) * radius));
                }
            }
            for (int ring = 0; ring < rings; ring++)
                for (int s = 0; s < sides; s++)
                {
                    int a = ring * (sides + 1) + s, b = a + sides + 1;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            int centre = verts.Count;
            verts.Add(Vector3.zero);
            for (int s = 0; s < sides; s++) { tris.Add(centre); tris.Add(s); tris.Add(s + 1); }
            var mesh = new Mesh { name = "Cute cone" };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public static Mesh Tube(IList<Vector3> path, float radius, bool closed, int sides = 10)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            int count = path.Count;
            int rings = closed ? count + 1 : count;
            Vector3 side = Vector3.zero;
            for (int i = 0; i < rings; i++)
            {
                var p = path[i % count];
                var prev = path[closed ? (i - 1 + count) % count : Mathf.Max(0, i - 1)];
                var next = path[closed ? (i + 1) % count : Mathf.Min(count - 1, i + 1)];
                var along = (next - prev).normalized;
                side = Vector3.ProjectOnPlane(side.sqrMagnitude > 0f ? side : Vector3.forward, along);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(Vector3.up, along);
                if (side.sqrMagnitude < 1e-6f) side = Vector3.ProjectOnPlane(Vector3.right, along);
                side.Normalize();
                var up = Vector3.Cross(side, along).normalized;
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2f;
                    verts.Add(p + (side * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
                }
            }
            for (int i = 0; i < rings - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = i * (sides + 1) + s, b = a + sides + 1;
                    tris.Add(a); tris.Add(b); tris.Add(a + 1);
                    tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
                }
            if (!closed)
            {
                Cap(verts, tris, path[0], 0, sides, true);
                Cap(verts, tris, path[count - 1], (rings - 1) * (sides + 1), sides, false);
            }
            var mesh = new Mesh { name = "Cute tube" };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        static void Cap(List<Vector3> verts, List<int> tris, Vector3 centre, int start, int sides, bool flip)
        {
            int c = verts.Count;
            verts.Add(centre);
            for (int s = 0; s < sides; s++)
            {
                if (flip) { tris.Add(c); tris.Add(start + s); tris.Add(start + s + 1); }
                else { tris.Add(c); tris.Add(start + s + 1); tris.Add(start + s); }
            }
        }

        public static Mesh Slab(IList<Vector2> outline, float thickness)
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            int n = outline.Count;
            float area = 0f;
            for (int i = 0; i < n; i++) { var a = outline[i]; var b = outline[(i + 1) % n]; area += a.x * b.y - b.x * a.y; }
            if (area < 0f) { var flipped = new List<Vector2>(outline); flipped.Reverse(); outline = flipped; }
            float half = thickness * .5f;
            verts.Add(new Vector3(0, 0, -half * 1.4f));
            verts.Add(new Vector3(0, 0, half * 1.4f));
            for (int i = 0; i < n; i++) verts.Add(new Vector3(outline[i].x, outline[i].y, -half));
            for (int i = 0; i < n; i++) verts.Add(new Vector3(outline[i].x, outline[i].y, half));
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                tris.Add(0); tris.Add(2 + j); tris.Add(2 + i);
                tris.Add(1); tris.Add(2 + n + i); tris.Add(2 + n + j);
                tris.Add(2 + i); tris.Add(2 + j); tris.Add(2 + n + i);
                tris.Add(2 + j); tris.Add(2 + n + j); tris.Add(2 + n + i);
            }
            var mesh = new Mesh { name = "Cute slab" };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public static List<Vector2> Heart(float width, int points = 48)
        {
            var list = new List<Vector2>();
            float k = width / 32f;
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)points * Mathf.PI * 2f;
                float x = 16f * Mathf.Pow(Mathf.Sin(t), 3f);
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2f * t) - 2f * Mathf.Cos(3f * t) - Mathf.Cos(4f * t);
                list.Add(new Vector2(x, y + 3f) * k);
            }
            return list;
        }

        public static List<Vector2> Star(float outer, float inner, int tips = 5)
        {
            var list = new List<Vector2>();
            for (int i = 0; i < tips * 2; i++)
            {
                float a = Mathf.PI * .5f + i * Mathf.PI / tips;
                float r = i % 2 == 0 ? outer : inner;
                list.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            return list;
        }

        public static List<Vector2> Circle(float radius, int points = 32)
        {
            var list = new List<Vector2>();
            for (int i = 0; i < points; i++)
            {
                float a = i / (float)points * Mathf.PI * 2f;
                list.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            return list;
        }

        public static Material Paint(Material template, string name, Color colour, float smoothness, float glow = 0f)
        {
            if (materials.TryGetValue(name, out var cached) && cached) return cached;
            template = Visible(template);
            if (!template) return null;
            var material = new Material(template) { name = name };
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
            if (material.HasProperty("_BumpMap")) { material.SetTexture("_BumpMap", null); material.DisableKeyword("_NORMALMAP"); }
            if (material.HasProperty("_MetallicGlossMap")) { material.SetTexture("_MetallicGlossMap", null); material.DisableKeyword("_METALLICSPECGLOSSMAP"); }
            if (material.HasProperty("_OcclusionMap")) material.SetTexture("_OcclusionMap", null);
            material.SetColor("_BaseColor", colour);
            material.SetColor("_Color", colour);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (glow > 0f && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                material.SetColor("_EmissionColor", colour * glow);
            }
            materials[name] = material;
            return material;
        }

        public const string HiddenName = "cute_hidden";

        public static readonly string[] HiddenPasses =
        {
            "UniversalForward", "UniversalForwardOnly", "UniversalGBuffer", "ShadowCaster", "DepthOnly", "DepthNormals",
            "DepthNormalsOnly", "MotionVectors", "XRMotionVectors", "Universal2D", "SRPDefaultUnlit", "Meta", "ForwardLit", "GBuffer",
        };

        static readonly string[] HiddenBlendOff = { "_SrcBlend", "_SrcBlendAlpha", "_ZWrite" };
        static readonly string[] HiddenBlendKeep = { "_DstBlend", "_DstBlendAlpha" };
        static Material hiddenSource;

        static Material Visible(Material template) =>
            template && hiddenSource && template.name == HiddenName ? hiddenSource : template;

        public static Material Hidden(Material template)
        {
            if (materials.TryGetValue(HiddenName, out var cached) && cached) return cached;
            template = Visible(template);
            if (!template) return null;
            hiddenSource = template;
            var material = new Material(template) { name = HiddenName };
            foreach (string pass in HiddenPasses) material.SetShaderPassEnabled(pass, false);
            foreach (string property in HiddenBlendOff) if (material.HasProperty(property)) material.SetFloat(property, 0f);
            foreach (string property in HiddenBlendKeep) if (material.HasProperty(property)) material.SetFloat(property, 1f);
            if (material.HasProperty("_AlphaClip")) material.SetFloat("_AlphaClip", 1f);
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 1f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0f));
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            materials[HiddenName] = material;
            return material;
        }

        public static bool DrawsNothing(Material material)
        {
            if (!material) return false;
            foreach (string pass in HiddenPasses) if (material.GetShaderPassEnabled(pass)) return false;
            return true;
        }

        public static void KeepOnRegularPath(Renderer renderer)
        {
            if (renderer && !renderer.GetComponent<CuteRegularPath>()) renderer.gameObject.AddComponent<CuteRegularPath>();
        }

        public static bool OnRegularPath(Renderer renderer) => renderer && renderer.GetComponent<CuteRegularPath>();

        public static Transform Part(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale, bool shadows = false)
        {
            var go = new GameObject(name);
            go.layer = parent.gameObject.layer;
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = position; t.localRotation = rotation; t.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            KeepOnRegularPath(renderer);
            return t;
        }

        public static Transform Group(Transform parent, string name, Vector3 position = default, Quaternion? rotation = null)
        {
            var go = new GameObject(name) { layer = parent.gameObject.layer };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            return go.transform;
        }

        public static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
    }
}
