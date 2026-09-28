using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Wreckabulary.EditorTools
{
    /// <summary>
    /// Builds chunky 3D letters A–Z from the font. TextMeshPro stores each glyph as a signed distance field,
    /// so the outline is traced with marching squares on that field and extruded into a solid.
    /// Each mesh is normalised: height 1 (baseline at y = 0), centred on x, <see cref="Depth"/> thick on z,
    /// with its front face towards -z.
    /// </summary>
    public static class LetterMeshBuilder
    {
        public const float Depth = 0.35f;
        const float Iso = 0.5f;
        const int TargetRows = 28;

        public static Mesh[] BuildAll(TMP_FontAsset font, string folder)
        {
            var pixels = ReadAtlas(font.atlasTexture, out int atlasW, out int atlasH);
            int pad = font.atlasPadding;
            var meshes = new Mesh[26];

            for (int i = 0; i < 26; i++)
            {
                char c = (char)('A' + i);
                if (!font.characterLookupTable.TryGetValue(c, out var ch))
                {
                    Debug.LogError($"[Wreckabulary] Font has no glyph for {c}");
                    continue;
                }
                var r = ch.glyph.glyphRect;
                var field = Field(pixels, atlasW, atlasH, r.x - pad, r.y - pad, r.width + pad * 2, r.height + pad * 2,
                                  Mathf.Max(1, (r.height + pad * 2) / TargetRows), out int nx, out int ny);
                var mesh = Extrude(field, nx, ny);
                mesh.name = $"Letter_{c}";
                meshes[i] = SaveMesh(mesh, $"{folder}/Letter_{c}.asset");
            }
            return meshes;
        }

        // ---- SDF sampling ----

        static byte[] ReadAtlas(Texture2D atlas, out int w, out int h)
        {
            w = atlas.width;
            h = atlas.height;
            // The atlas isn't CPU-readable, so copy it through the GPU.
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(atlas, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            var alpha = new byte[px.Length];
            for (int i = 0; i < px.Length; i++) alpha[i] = px[i].a;
            return alpha;
        }

        /// <summary>Samples the distance field over a rectangle, averaging stride × stride blocks. Outside the rect counts as empty.</summary>
        static float[,] Field(byte[] alpha, int atlasW, int atlasH, int x0, int y0, int w, int h, int stride, out int nx, out int ny)
        {
            nx = w / stride + 2;
            ny = h / stride + 2;
            var f = new float[nx, ny];
            for (int i = 0; i < nx; i++)
            for (int j = 0; j < ny; j++)
            {
                // A one-sample empty border so every outline closes.
                if (i == 0 || j == 0 || i == nx - 1 || j == ny - 1) continue;
                float sum = 0f;
                int n = 0;
                for (int dx = 0; dx < stride; dx++)
                for (int dy = 0; dy < stride; dy++)
                {
                    int px = x0 + (i - 1) * stride + dx, py = y0 + (j - 1) * stride + dy;
                    if (px < 0 || py < 0 || px >= atlasW || py >= atlasH) { n++; continue; }
                    sum += alpha[py * atlasW + px] / 255f;
                    n++;
                }
                f[i, j] = sum / n;
            }
            return f;
        }

        // ---- Marching squares + extrusion ----

        class Builder
        {
            public readonly List<Vector3> verts = new();
            public readonly List<Vector3> normals = new();
            public readonly List<int> tris = new();

            /// <summary>Adds a triangle facing along n (fixes the winding so it's visible from that side).</summary>
            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 n)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f) (b, c) = (c, b);
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);
                normals.Add(n); normals.Add(n); normals.Add(n);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            }

            /// <summary>A flat polygon (convex, counter-clockwise) on both the front and back faces.</summary>
            public void Faces(List<Vector2> poly)
            {
                float z = Depth * 0.5f;
                for (int k = 1; k < poly.Count - 1; k++)
                {
                    Tri(V(poly[0], -z), V(poly[k], -z), V(poly[k + 1], -z), Vector3.back);
                    Tri(V(poly[0], z), V(poly[k], z), V(poly[k + 1], z), Vector3.forward);
                }
            }

            /// <summary>A wall along an outline segment. The shape's inside is to the left of a→b.</summary>
            public void Side(Vector2 a, Vector2 b)
            {
                var d = b - a;
                if (d.sqrMagnitude < 1e-8f) return;
                var n = new Vector3(d.y, -d.x, 0f).normalized;
                float z = Depth * 0.5f;
                Tri(V(a, -z), V(b, -z), V(b, z), n);
                Tri(V(a, -z), V(b, z), V(a, z), n);
            }

            static Vector3 V(Vector2 p, float z) => new(p.x, p.y, z);
        }

        static Mesh Extrude(float[,] f, int nx, int ny)
        {
            var b = new Builder();
            var poly = new List<Vector2>(8);
            var isEdge = new List<bool>(8);

            for (int j = 0; j < ny - 1; j++)
            {
                int runStart = -1;
                for (int i = 0; i <= nx - 1; i++)
                {
                    // Solid cells are merged into one rectangle per run to keep the vertex count down.
                    bool solid = i < nx - 1 && f[i, j] >= Iso && f[i + 1, j] >= Iso && f[i + 1, j + 1] >= Iso && f[i, j + 1] >= Iso;
                    if (solid)
                    {
                        if (runStart < 0) runStart = i;
                        continue;
                    }
                    if (runStart >= 0)
                    {
                        b.Faces(new List<Vector2> { new(runStart, j), new(i, j), new(i, j + 1), new(runStart, j + 1) });
                        runStart = -1;
                    }
                    if (i == nx - 1) break;

                    // Walk the cell's corners counter-clockwise, keeping the inside part and the crossing points.
                    poly.Clear();
                    isEdge.Clear();
                    var corners = new[] { new Vector2Int(i, j), new Vector2Int(i + 1, j), new Vector2Int(i + 1, j + 1), new Vector2Int(i, j + 1) };
                    for (int k = 0; k < 4; k++)
                    {
                        var p = corners[k];
                        var q = corners[(k + 1) % 4];
                        float fp = f[p.x, p.y], fq = f[q.x, q.y];
                        if (fp >= Iso) { poly.Add(p); isEdge.Add(false); }
                        if ((fp >= Iso) != (fq >= Iso))
                        {
                            float t = (Iso - fp) / (fq - fp);
                            poly.Add(Vector2.Lerp(p, q, t));
                            isEdge.Add(true);
                        }
                    }
                    if (poly.Count < 3) continue;
                    b.Faces(poly);
                    // Polygon edges between two crossing points run through the cell: that's the outline.
                    for (int k = 0; k < poly.Count; k++)
                    {
                        int n = (k + 1) % poly.Count;
                        if (isEdge[k] && isEdge[n]) b.Side(poly[k], poly[n]);
                    }
                }
            }
            return Normalise(b);
        }

        /// <summary>Scales to height 1 with the baseline at 0 and x centred. Depth is kept as is.</summary>
        static Mesh Normalise(Builder b)
        {
            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            foreach (var v in b.verts)
            {
                minX = Mathf.Min(minX, v.x); maxX = Mathf.Max(maxX, v.x);
                minY = Mathf.Min(minY, v.y); maxY = Mathf.Max(maxY, v.y);
            }
            float k = 1f / (maxY - minY);
            float cx = (minX + maxX) * 0.5f;
            for (int i = 0; i < b.verts.Count; i++)
            {
                var v = b.verts[i];
                b.verts[i] = new Vector3((v.x - cx) * k, (v.y - minY) * k, v.z);
            }

            var mesh = new Mesh { indexFormat = b.verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(b.verts);
            mesh.SetNormals(b.normals);
            mesh.SetTriangles(b.tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Overwrites an existing mesh asset in place so references (and GUIDs) survive a rebuild.</summary>
        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!existing)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            existing.Clear();
            existing.indexFormat = mesh.indexFormat;
            existing.SetVertices(mesh.vertices);
            existing.SetNormals(mesh.normals);
            existing.SetTriangles(mesh.triangles, 0);
            existing.RecalculateBounds();
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(mesh);
            return existing;
        }
    }
}
