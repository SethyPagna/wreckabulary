using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Bends a letter round a roommate, like a band of knitting: along its width it follows a circle
    /// round the vertical axis, it can lean in towards the axis (90° lies it flat, top pointing in, for
    /// a brim or a halo) and it can curl over as it rises (so a beanie follows the top of the head).
    /// The front face points outwards and reads left to right from outside.
    /// </summary>
    public static class LetterBend
    {
        public struct Wrap
        {
            /// <summary>Centre of the letter round the body in degrees: 0 is straight ahead (+z), positive towards +x.</summary>
            public float angle;
            /// <summary>Distance from the axis at the letter's baseline.</summary>
            public float radius;
            /// <summary>Height of the baseline.</summary>
            public float y;
            /// <summary>Width along the curve, height along the letter's own up, and thickness.</summary>
            public float width, height, thickness;
            /// <summary>Degrees the letter tips in towards the axis. Negative flares out.</summary>
            public float lean;
            /// <summary>Radius the letter curls inwards along its height. 0 keeps it straight.</summary>
            public float curl;
            /// <summary>Degrees the letter turns in its own plane before bending (anticlockwise seen from outside).</summary>
            public float roll;
            /// <summary>Where the axis stands, so a bow can bend round a point that isn't the body's middle.</summary>
            public Vector3 axis;
        }

        static readonly Dictionary<string, Mesh> Cache = new();

        /// <summary>Spreads letters [from, from + count) of a word round the body, side by side in reading order.</summary>
        /// <param name="band">Shared settings; its angle is the middle of the spread and width is ignored.</param>
        /// <param name="arc">Degrees the letters cover together, gaps included.</param>
        public static IEnumerable<(int index, Wrap wrap)> Around(string word, int from, int count, Wrap band, float arc, float gap = 3f)
        {
            float total = 0f;
            for (int i = from; i < from + count; i++) total += Aspect(word[i]);
            // A full ring needs a gap after the last letter too; an open arc doesn't.
            int gaps = arc >= 359f ? count : count - 1;
            float perAspect = (arc - gap * gaps) / total;
            // Reading left to right from outside runs towards smaller angles.
            float at = band.angle + arc * 0.5f - (arc >= 359f ? gap * 0.5f : 0f);
            for (int i = from; i < from + count; i++)
            {
                float span = Aspect(word[i]) * perAspect;
                var w = band;
                w.angle = at - span * 0.5f;
                w.width = span * Mathf.Deg2Rad * band.radius;
                at -= span + gap;
                yield return (i, w);
            }
        }

        static float Aspect(char c)
        {
            var m = GameAssets.I.LetterMesh(c);
            return m ? m.bounds.size.x / m.bounds.size.y : 0.8f;
        }

        /// <summary>A bent copy of the letter's mesh. Copies are shared, so don't edit or destroy them.</summary>
        public static Mesh Bent(char c, Wrap w)
        {
            string key = $"{c}|{w.angle:F2}|{w.radius:F3}|{w.y:F3}|{w.width:F3}|{w.height:F3}|{w.thickness:F3}|{w.lean:F1}|{w.curl:F3}|{w.roll:F1}|{w.axis}";
            if (Cache.TryGetValue(key, out var cached) && cached) return cached;
            var src = GameAssets.I.LetterMesh(c);
            if (!src) return null;
            var mesh = Build(src, w);
            mesh.name = $"Bent_{c}";
            Cache[key] = mesh;
            return mesh;
        }

        static Mesh Build(Mesh src, Wrap w)
        {
            var b = src.bounds;
            var scale = new Vector3(w.width / b.size.x, w.height / b.size.y, w.thickness / b.size.z);
            var sv = src.vertices;
            var sn = src.normals;
            var st = src.triangles;

            // Long flat triangles would stay flat chords across the curve, so cut them up first.
            float bendRadius = Mathf.Min(w.radius, w.curl > 0f ? w.curl : float.MaxValue);
            float maxEdge = Mathf.Max(0.01f, bendRadius * 0.12f);

            var verts = new List<Vector3>(st.Length * 2);
            var normals = new List<Vector3>(st.Length * 2);
            for (int i = 0; i < st.Length; i += 3)
            {
                // Letter meshes have one normal per face, so the first corner's will do for the whole triangle.
                var n = Local(sn[st[i]], b, scale, w, normal: true);
                Split(Local(sv[st[i]], b, scale, w), Local(sv[st[i + 1]], b, scale, w), Local(sv[st[i + 2]], b, scale, w),
                      n, maxEdge, w, verts, normals, 0);
            }

            var tris = new int[verts.Count];
            for (int i = 0; i < tris.Length; i++) tris[i] = i;
            var mesh = new Mesh { indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Mesh space to flat letter space: (along the width from the centre, up from the baseline, outwards).
        /// The front face is at -z in the mesh, so outwards is -z. Rolled about the letter's middle.
        /// </summary>
        static Vector3 Local(Vector3 v, Bounds b, Vector3 scale, Wrap w, bool normal = false)
        {
            Vector3 p = normal
                ? new Vector3(v.x / scale.x, v.y / scale.y, -v.z / scale.z).normalized
                : new Vector3((v.x - b.center.x) * scale.x, (v.y - b.min.y) * scale.y - w.height * 0.5f, -(v.z - b.center.z) * scale.z);
            if (w.roll != 0f)
            {
                float r = w.roll * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
                p = new Vector3(p.x * cos - p.y * sin, p.x * sin + p.y * cos, p.z);
            }
            if (!normal) p.y += w.height * 0.5f;
            return p;
        }

        static void Split(Vector3 a, Vector3 b, Vector3 c, Vector3 n, float maxEdge, Wrap w, List<Vector3> verts, List<Vector3> normals, int depth)
        {
            // Only the width and height bend; thickness runs straight out.
            float ab = Flat(a - b), bc = Flat(b - c), ca = Flat(c - a);
            float longest = Mathf.Max(ab, Mathf.Max(bc, ca));
            if (longest > maxEdge && depth < 10)
            {
                if (longest == ab) { var m = (a + b) * 0.5f; Split(a, m, c, n, maxEdge, w, verts, normals, depth + 1); Split(m, b, c, n, maxEdge, w, verts, normals, depth + 1); }
                else if (longest == bc) { var m = (b + c) * 0.5f; Split(a, b, m, n, maxEdge, w, verts, normals, depth + 1); Split(a, m, c, n, maxEdge, w, verts, normals, depth + 1); }
                else { var m = (c + a) * 0.5f; Split(a, b, m, n, maxEdge, w, verts, normals, depth + 1); Split(m, b, c, n, maxEdge, w, verts, normals, depth + 1); }
                return;
            }
            foreach (var p in new[] { a, b, c })
            {
                verts.Add(Map(p, w, out var t, out var up, out var o));
                normals.Add((n.x * t + n.y * up + n.z * o).normalized);
            }
        }

        static float Flat(Vector3 d) => Mathf.Sqrt(d.x * d.x + d.y * d.y);

        /// <summary>Flat letter space onto the curve, with the frame there (along, up, out) for the normals.</summary>
        static Vector3 Map(Vector3 p, Wrap w, out Vector3 along, out Vector3 up, out Vector3 outward)
        {
            float lean = w.lean * Mathf.Deg2Rad;
            float curled = w.curl > 0f ? p.y / w.curl : 0f;
            float rise = w.curl > 0f ? w.curl * Mathf.Sin(curled) : p.y;
            float inward = w.curl > 0f ? w.curl * (1f - Mathf.Cos(curled)) : 0f;
            float tip = lean + curled;

            // In the (distance from axis, height) plane.
            float rho = w.radius - rise * Mathf.Sin(lean) - inward * Mathf.Cos(lean) + p.z * Mathf.Cos(tip);
            float y = w.y + rise * Mathf.Cos(lean) - inward * Mathf.Sin(lean) + p.z * Mathf.Sin(tip);
            float theta = w.angle * Mathf.Deg2Rad - p.x / w.radius;

            var dir = new Vector3(Mathf.Sin(theta), 0f, Mathf.Cos(theta));
            along = new Vector3(-dir.z, 0f, dir.x);
            up = Mathf.Cos(tip) * Vector3.up - Mathf.Sin(tip) * dir;
            outward = Mathf.Cos(tip) * dir + Mathf.Sin(tip) * Vector3.up;
            return w.axis + dir * rho + Vector3.up * y;
        }
    }
}
