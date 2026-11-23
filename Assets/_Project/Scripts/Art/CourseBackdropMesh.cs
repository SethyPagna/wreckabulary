using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Rules.Surroundings;

namespace Wreckabulary.Art
{
    public static class CourseBackdropMesh
    {
        static readonly Color32 Untinted = new(255, 255, 255, 0);

        sealed class SlotMesh
        {
            public Vector3[] positions, normals;
            public Color32[] colours;
            public int[] indices;
        }

        sealed class Buffer
        {
            public readonly List<Vector3> positions = new(), normals = new();
            public readonly List<Color32> colours = new();
            public readonly List<int> indices = new();
            public int pieces;
        }

        sealed class PieceData
        {
            public Vector3[] positions, normals;
            public Color32[] colours;
        }

        public static GameObject Build(CourseSurroundingsResult layout, CourseBackdropKit kit, string name, bool readable, List<Mesh> owned)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (!kit) throw new ArgumentNullException(nameof(kit));
            var groups = new Dictionary<(SurroundingsRing band, string slot), List<Buffer>>();
            var slotCache = new Dictionary<(string, int), SlotMesh>();
            var pieceCache = new Dictionary<string, PieceData>(StringComparer.Ordinal);
            foreach (var placement in layout.Placements)
            {
                var piece = kit.Find(placement.Piece);
                if (piece == null || !piece.mesh) throw new InvalidOperationException($"The course backdrop kit has no mesh for {placement.Piece}.");
                var band = layout.BandOf(placement);
                var matrix = Matrix4x4.TRS(new Vector3(placement.X, placement.Y, placement.Z), Quaternion.Euler(0f, placement.Yaw, 0f), Vector3.one * placement.Scale) * piece.Space;
                int submeshes = Mathf.Min(piece.mesh.subMeshCount, piece.slots.Length);
                for (int s = 0; s < submeshes; s++)
                {
                    string slot = piece.slots[s];
                    if (layout.Skips(slot)) continue;
                    var source = SlotOf(piece, s, slotCache, pieceCache);
                    var buffer = Current(groups, band, slot, source.positions.Length);
                    Append(buffer, source, matrix, placement.TintFor(slot));
                    buffer.pieces++;
                }
            }
            foreach (var zone in layout.Ground)
            {
                var corners = Corners(zone);
                var buffer = Current(groups, SurroundingsRing.Far, zone.Slot, corners.Count);
                AppendGround(buffer, zone, corners);
            }
            var root = Assemble(layout, kit, name, readable, owned, groups);
            if (!layout.Skyline.Empty) CourseSkylineMesh.Build(layout.Skyline, kit, name, readable, owned).transform.SetParent(root.transform, false);
            return root;
        }

        static SlotMesh SlotOf(CourseBackdropKit.Piece piece, int submesh, Dictionary<(string, int), SlotMesh> slots, Dictionary<string, PieceData> pieces)
        {
            if (slots.TryGetValue((piece.id, submesh), out var cached)) return cached;
            if (!pieces.TryGetValue(piece.id, out var data))
            {
                var mesh = piece.mesh;
                var positions = mesh.vertices;
                var normals = mesh.normals;
                var colours = mesh.colors32;
                data = new PieceData
                {
                    positions = positions,
                    normals = normals.Length == positions.Length ? normals : Enumerable.Repeat(Vector3.up, positions.Length).ToArray(),
                    colours = colours.Length == positions.Length ? colours : Enumerable.Repeat(Untinted, positions.Length).ToArray(),
                };
                pieces.Add(piece.id, data);
            }
            var source = piece.mesh.GetIndices(submesh);
            var remap = new Dictionary<int, int>();
            var used = new List<int>();
            var indices = new int[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (!remap.TryGetValue(source[i], out int local)) { local = used.Count; remap.Add(source[i], local); used.Add(source[i]); }
                indices[i] = local;
            }
            var slot = new SlotMesh
            {
                positions = used.Select(v => data.positions[v]).ToArray(),
                normals = used.Select(v => data.normals[v]).ToArray(),
                colours = used.Select(v => data.colours[v]).ToArray(),
                indices = indices,
            };
            slots.Add((piece.id, submesh), slot);
            return slot;
        }

        static Buffer Current(Dictionary<(SurroundingsRing, string), List<Buffer>> groups, SurroundingsRing band, string slot, int adding)
        {
            if (!groups.TryGetValue((band, slot), out var parts)) groups[(band, slot)] = parts = new List<Buffer> { new() };
            var part = parts[parts.Count - 1];
            if (part.positions.Count > 0 && part.positions.Count + adding > SurroundingsLayout.MergeVertexLimit) parts.Add(part = new Buffer());
            return part;
        }

        static void Append(Buffer buffer, SlotMesh source, Matrix4x4 transform, uint tint)
        {
            int start = buffer.positions.Count;
            var normalMatrix = transform.inverse.transpose;
            for (int i = 0; i < source.positions.Length; i++)
            {
                buffer.positions.Add(transform.MultiplyPoint3x4(source.positions[i]));
                buffer.normals.Add(normalMatrix.MultiplyVector(source.normals[i]).normalized);
                buffer.colours.Add(Tint(source.colours[i], tint));
            }
            foreach (int index in source.indices) buffer.indices.Add(start + index);
        }

        static List<Vector3> Corners(SurroundingsGround zone)
        {
            if (zone.Shape == SurroundingsGroundShape.Rect)
                return new List<Vector3>
                {
                    new(zone.MinX, zone.Y, zone.MinZ), new(zone.MinX, zone.Y, zone.MaxZ), new(zone.MaxX, zone.Y, zone.MaxZ), new(zone.MaxX, zone.Y, zone.MinZ)
                };
            var points = new List<Vector3> { new(zone.CentreX, zone.Y, zone.CentreZ) };
            for (int i = CourseSurroundings.DiscSegments; i >= 0; i--)
            {
                float angle = i % CourseSurroundings.DiscSegments * Mathf.PI * 2f / CourseSurroundings.DiscSegments;
                points.Add(new Vector3(zone.CentreX + Mathf.Cos(angle) * zone.Radius, zone.Y, zone.CentreZ + Mathf.Sin(angle) * zone.Radius));
            }
            return points;
        }

        static void AppendGround(Buffer buffer, SurroundingsGround zone, List<Vector3> corners)
        {
            int start = buffer.positions.Count;
            var colour = Tint(Untinted, zone.Tint);
            foreach (var corner in corners)
            {
                buffer.positions.Add(corner);
                buffer.normals.Add(Vector3.up);
                buffer.colours.Add(colour);
            }
            for (int i = 1; i + 1 < corners.Count; i++)
            {
                buffer.indices.Add(start);
                buffer.indices.Add(start + i);
                buffer.indices.Add(start + i + 1);
            }
            buffer.pieces++;
        }

        static GameObject Assemble(CourseSurroundingsResult layout, CourseBackdropKit kit, string name, bool readable, List<Mesh> owned,
            Dictionary<(SurroundingsRing band, string slot), List<Buffer>> groups)
        {
            var root = new GameObject(name);
            var bands = new Dictionary<SurroundingsRing, Transform>();
            foreach (var pair in groups.OrderBy(g => g.Key.band).ThenBy(g => g.Key.slot, StringComparer.Ordinal))
            {
                if (!bands.TryGetValue(pair.Key.band, out var parent))
                {
                    parent = new GameObject(pair.Key.band.ToString()).transform;
                    parent.SetParent(root.transform, false);
                    bands.Add(pair.Key.band, parent);
                }
                var material = kit.MaterialFor(pair.Key.slot);
                if (!material) throw new InvalidOperationException($"The course backdrop kit has no material for {pair.Key.slot}.");
                for (int n = 0; n < pair.Value.Count; n++)
                {
                    var buffer = pair.Value[n];
                    if (buffer.positions.Count == 0) continue;
                    string label = pair.Key.slot + (n == 0 ? "" : "_" + (n + 1));
                    var mesh = new Mesh { name = $"{name} {pair.Key.band} {label}" };
                    owned.Add(mesh);
                    mesh.indexFormat = buffer.positions.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
                    mesh.SetVertices(buffer.positions);
                    mesh.SetNormals(buffer.normals);
                    mesh.SetColors(buffer.colours);
                    mesh.SetTriangles(buffer.indices, 0, false);
                    mesh.RecalculateBounds();
                    mesh.UploadMeshData(!readable);
                    var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.SetParent(parent, false);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.shadowCastingMode = layout.CastsShadows(pair.Key.band) ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Camera;
                    renderer.allowOcclusionWhenDynamic = true;
                    renderer.staticShadowCaster = false;
                }
            }
            return root;
        }

        static Color32 Tint(Color32 colour, uint tint)
        {
            if (tint == SurroundingsSlots.NoTint) return colour;
            byte Scale(byte channel, uint by) => (byte)((channel * (int)(by & 0xFF) + 127) / 255);
            return new Color32(Scale(colour.r, tint >> 16), Scale(colour.g, tint >> 8), Scale(colour.b, tint), colour.a);
        }
    }
}
