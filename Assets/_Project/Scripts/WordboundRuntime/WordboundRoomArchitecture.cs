using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.WordboundRuntime
{
    [DisallowMultipleComponent]
    public sealed class WordboundRoomArchitecture : MonoBehaviour, IDisposable
    {
        const float Recess = .012f;
        const int MaximumVerticesPerBatch = 60000;
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly Dictionary<Material, Batch> batches = new Dictionary<Material, Batch>();
        readonly List<KeyValuePair<Material, Batch>> completedBatches = new List<KeyValuePair<Material, Batch>>();
        readonly Dictionary<WordboundRoomPalette, PaletteMaterials> materials = new Dictionary<WordboundRoomPalette, PaletteMaterials>();
        readonly Dictionary<WordboundRoomFinish, Material> railings = new Dictionary<WordboundRoomFinish, Material>();
        readonly Dictionary<string, WordboundRoomFinish> finishes = new Dictionary<string, WordboundRoomFinish>(StringComparer.Ordinal);
        readonly List<WallPiece> wallPieces = new List<WallPiece>();
        Dictionary<string, ResolvedCourseModule> modules;
        WordboundRoomStyleProfile profile;
        WordboundCourseWorld world;
        Material artwork;
        Material woodwork;
        int decoratedWallPieces;
        bool splitSharedWalls;

        public int InsetCount { get; private set; }
        public int RenderBatchCount { get; private set; }
        public int OpeningBandCount { get; private set; }

        public static void Apply(WordboundCourseWorld world, WordboundCourseWorldOptions options)
        {
            if (!options.EnableRoomArchitecture) return;
            var profile = options.RoomStyle ? options.RoomStyle : Resources.Load<WordboundRoomStyleProfile>(WordboundRoomStyleProfile.ResourcePath);
            if (!profile || !profile.Enabled) return;
            profile.Validate();
            var view = world.gameObject.AddComponent<WordboundRoomArchitecture>();
            view.world = world; view.profile = profile;
            view.Build(options);
        }

        void Build(WordboundCourseWorldOptions options)
        {
            modules = world.Course.Geometry.ToDictionary(m => m.Id, StringComparer.Ordinal);
            foreach (var module in modules.Values)
            {
                var finish = profile.ForModule(module.Id);
                if (finish != null) finishes.Add(module.Id, finish);
            }
            splitSharedWalls = finishes.Count > 0 && !options.WallMaterial;
            if (splitSharedWalls) CollectWallPieces();
            foreach (var collider in world.Surfaces)
            {
                var node = collider.GetComponent<WordboundCourseNode>();
                if (!modules.TryGetValue(node.ModuleId, out var module)) continue;
                if (node.Kind == WordboundCourseNodeKind.Floor && options.FloorMaterial ||
                    node.Kind == WordboundCourseNodeKind.Wall && options.WallMaterial ||
                    node.Kind == WordboundCourseNodeKind.Connection && options.ConnectorMaterial) continue;
                finishes.TryGetValue(module.Id, out var finish);
                var palette = RoomMaterials(module);
                if (node.Kind == WordboundCourseNodeKind.Floor && !options.FloorMaterial)
                    Floor(collider, palette, finish);
                else if (node.Kind == WordboundCourseNodeKind.Wall && !options.WallMaterial)
                    Wall(collider, module, palette, finish);
                else if (node.Kind == WordboundCourseNodeKind.Connection && !options.ConnectorMaterial)
                    collider.GetComponent<Renderer>().sharedMaterial = Woodwork();
            }
            foreach (var pair in completedBatches.Concat(batches))
            {
                var mesh = new Mesh { name = "Room architecture " + RenderBatchCount };
                mesh.SetVertices(pair.Value.Vertices); mesh.SetNormals(pair.Value.Normals);
                mesh.SetUVs(0, pair.Value.Uvs); mesh.SetTriangles(pair.Value.Triangles, 0);
                mesh.RecalculateBounds(); mesh.RecalculateTangents();
                ownedMeshes.Add(mesh);
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = pair.Key;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                RenderBatchCount++;
            }
            batches.Clear(); completedBatches.Clear();
        }

        void Floor(Collider collider, PaletteMaterials palette, WordboundRoomFinish finish)
        {
            var filter = collider.GetComponent<MeshFilter>();
            var mesh = Instantiate(filter.sharedMesh);
            mesh.name = "Room floor and ceiling " + collider.name;
            if (finish != null && (finish.Ground != Surfaces.Kind.Planks || finish.GroundTile > 0))
            {
                Vector2 from = Surfaces.Tile(Surfaces.Kind.Planks);
                Vector2 to = finish.GroundTile > 0 ? Vector2.one * finish.GroundTile : Surfaces.Tile(finish.Ground);
                var uvs = mesh.uv;
                for (int i = 0; i < uvs.Length; i++) uvs[i] = new Vector2(uvs[i].x * from.x / to.x, uvs[i].y * from.y / to.y);
                mesh.uv = uvs; mesh.RecalculateTangents();
            }
            var original = mesh.triangles; var normals = mesh.normals;
            var indices = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int i = 0; i < original.Length; i += 3)
            {
                float y = normals[original[i]].y;
                int slot = y > .9f ? 0 : y < -.9f ? 1 : 2;
                indices[slot].Add(original[i]); indices[slot].Add(original[i + 1]); indices[slot].Add(original[i + 2]);
            }
            mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(indices[i], i);
            ownedMeshes.Add(mesh); filter.sharedMesh = mesh;
            collider.GetComponent<Renderer>().sharedMaterials = new[] { palette.Floor, palette.Ceiling, palette.Frame };
        }

        void Wall(Collider collider, ResolvedCourseModule module, PaletteMaterials palette, WordboundRoomFinish finish)
        {
            Vector3 center = world.transform.InverseTransformPoint(collider.transform.position);
            Vector3 size = collider.transform.localScale;
            bool fixedX = FixedX(center, module);
            Vector3 inward = fixedX ? Vector3.right * (center.x < module.Position.X / 1000f ? 1 : -1) :
                Vector3.forward * (center.z < module.Position.Z / 1000f ? 1 : -1);
            float thickness = fixedX ? size.x : size.z;
            if (thickness <= Recess * 2 || size.y < .001f) return;
            collider.GetComponent<Renderer>().enabled = false;
            Vector3 right = Vector3.Cross(Vector3.up, inward);
            Vector3 face = center + inward * thickness * .5f;
            float width = fixedX ? size.z : size.x;
            float bottom = center.y - size.y * .5f;
            float roomFloor = module.Bounds.Min.Y / 1000f;
            float fixedValue = fixedX ? center.x : center.z;
            var piece = Piece(center, size, fixedX);
            bool outdoor = finish != null && finish.Outdoor;

            if (!splitSharedWalls)
            {
                var backing = size;
                if (fixedX) backing.x -= Recess * 2; else backing.z -= Recess * 2;
                Box(center, backing, palette.Wall);
                if (profile.FrameRouteOpenings)
                    RouteOpeningBands(piece, fixedValue, module, fixedX, inward, right, face);
            }
            else
            {
                float depth = thickness * .5f - Recess;
                var free = new List<Rect> { piece };
                var shared = new List<KeyValuePair<Rect, string>>();
                foreach (var other in wallPieces)
                {
                    if (other.ModuleId == module.Id || other.FixedX != fixedX || Mathf.Abs(other.Fixed - fixedValue) > .003f) continue;
                    foreach (var part in free)
                    {
                        var overlap = Rect.MinMaxRect(Mathf.Max(part.xMin, other.Rect.xMin), Mathf.Max(part.yMin, other.Rect.yMin),
                            Mathf.Min(part.xMax, other.Rect.xMax), Mathf.Min(part.yMax, other.Rect.yMax));
                        if (overlap.width > .001f && overlap.height > .001f) shared.Add(new KeyValuePair<Rect, string>(overlap, other.ModuleId));
                    }
                    free = Subtract(free, other.Rect);
                }
                foreach (var part in shared)
                {
                    bool openNeighbour = finishes.TryGetValue(part.Value, out var neighbour) && neighbour.Outdoor;
                    if (!outdoor) Slab(part.Key, fixedX, fixedValue, inward, 0, depth, palette.Wall);
                    else if (!openNeighbour)
                    {
                        Slab(part.Key, fixedX, fixedValue, inward, 0, depth, RoomMaterials(modules[part.Value]).Wall);
                        if (profile.FrameRouteOpenings) RouteOpeningBands(part.Key, fixedValue, module, fixedX, inward, right, face);
                    }
                    else if (string.CompareOrdinal(module.Id, part.Value) < 0)
                        Boundary(part.Key, fixedX, fixedValue, inward, depth, roomFloor, palette, finish);
                }
                foreach (var part in free)
                    if (outdoor) Boundary(part, fixedX, fixedValue, inward, depth, roomFloor, palette, finish);
                    else Slab(part, fixedX, fixedValue, inward, -depth, depth, palette.Wall);
                if (!outdoor && profile.FrameRouteOpenings)
                    RouteOpeningBands(piece, fixedValue, module, fixedX, inward, right, face);
            }

            if (outdoor || size.y < 4.1f || width < 3.5f || bottom > roomFloor + .001f ||
                decoratedWallPieces >= profile.MaximumDecoratedWallPieces) return;
            int count = Mathf.Min(profile.MaximumInsetsPerWallPiece, Mathf.FloorToInt(width / 6f));
            if (count == 0 && width >= 4f && profile.MaximumInsetsPerWallPiece > 0) count = 1;
            if (count == 0) return;
            decoratedWallPieces++;
            float insetWidth = Mathf.Min(profile.InsetWidth, width / count - 1.2f);
            float insetHeight = Mathf.Min(profile.InsetHeight, size.y - 1.8f);
            float frameWidth = Mathf.Min(profile.FrameWidth, Mathf.Min(insetWidth, insetHeight) * .2f);
            for (int i = 0; i < count; i++)
            {
                float x = -width * .5f + width * (i + .5f) / count;
                Vector3 at = face + right * x;
                at.y = roomFloor + 1.1f + insetHeight * .5f;
                RoundedPanel(at - inward * .006f, right, inward, insetWidth, insetHeight,
                    Mathf.Min(.8f, insetWidth * .22f), palette.Frame);
                RoundedPanel(at - inward * .003f, right, inward, insetWidth - frameWidth * 2,
                    insetHeight - frameWidth * 2, Mathf.Min(.66f, insetWidth * .18f), palette.Inset);
                FlatBox(at - Vector3.up * (insetHeight * .5f + .07f), right, inward,
                    insetWidth + .2f, .14f, .006f, palette.Accent);
                if (profile.WallArtwork && i == 0 && (module.LayerId == "Attic" || module.Id.EndsWith("ParcelGate", StringComparison.Ordinal)))
                    Artwork(at - inward * .001f, right, inward, insetWidth - .8f, insetHeight - .8f);
                else
                {
                    float barWidth = Mathf.Min(.12f, frameWidth);
                    FlatBox(at, right, inward, barWidth, insetHeight - .45f, .002f, palette.Frame);
                    FlatBox(at + Vector3.up * .12f, right, inward, insetWidth - .45f, barWidth, .002f, palette.Frame);
                }
                InsetCount++;
            }
        }

        Material Woodwork()
        {
            if (!woodwork) woodwork = Make(profile.WoodworkMaterial, profile.Woodwork, "Route woodwork", Surfaces.Kind.Wood);
            return woodwork;
        }

        void RouteOpeningBands(Rect solid, float fixedValue, ResolvedCourseModule module,
            bool fixedX, Vector3 inward, Vector3 right, Vector3 face)
        {
            var emitted = new HashSet<Rect>();
            foreach (var connection in world.Course.Connections)
            {
                if (OpeningBandCount >= profile.MaximumOpeningBands) return;
                Vector3 a = new Vector3(connection.From.X, connection.From.Y, connection.From.Z) / 1000f;
                Vector3 b = new Vector3(connection.To.X, connection.To.Y, connection.To.Z) / 1000f;
                float from = fixedX ? a.x : a.z, to = fixedX ? b.x : b.z;
                float delta = to - from, t;
                if (Mathf.Abs(delta) < .0001f)
                {
                    if ((connection.Kind != "Walk" && connection.Kind != "Door") || Mathf.Abs(from - fixedValue) > .0001f ||
                        connection.FromModuleId != module.Id && connection.ToModuleId != module.Id) continue;
                    t = 0;
                }
                else
                {
                    t = (fixedValue - from) / delta;
                    if (t < 0 || t > 1) continue;
                }
                Vector3 point = Vector3.Lerp(a, b, t);
                float across = fixedX ? point.z : point.x;
                float halfWidth = connection.WidthMm / 2000f;
                float allowance = Mathf.Abs(delta) < .0001f ? 0 : Mathf.Abs((b.y - a.y) / delta) * WordboundCourseWorld.WallThickness / 2;
                var aperture = Rect.MinMaxRect(across - halfWidth, point.y - WordboundCourseWorld.FloorThickness - allowance,
                    across + halfWidth, point.y + WordboundCourseWorld.ConnectorHeadroom + allowance);
                float w = profile.DoorwayFrameWidth;
                Band(Rect.MinMaxRect(aperture.xMin - w, aperture.yMin, aperture.xMin, aperture.yMax));
                Band(Rect.MinMaxRect(aperture.xMax, aperture.yMin, aperture.xMax + w, aperture.yMax));
                Band(Rect.MinMaxRect(aperture.xMin - w, aperture.yMax, aperture.xMax + w, aperture.yMax + w));
            }

            void Band(Rect requested)
            {
                if (OpeningBandCount >= profile.MaximumOpeningBands) return;
                var clipped = Rect.MinMaxRect(Mathf.Max(solid.xMin, requested.xMin), Mathf.Max(solid.yMin, requested.yMin),
                    Mathf.Min(solid.xMax, requested.xMax), Mathf.Min(solid.yMax, requested.yMax));
                if (clipped.width <= .001f || clipped.height <= .001f || !emitted.Add(clipped)) return;
                Vector3 at = face;
                if (fixedX) at.z = clipped.center.x; else at.x = clipped.center.x;
                at.y = clipped.center.y;
                FlatBox(at - inward * .004f, right, inward, clipped.width, clipped.height, .008f, Woodwork());
                OpeningBandCount++;
            }
        }

        void Artwork(Vector3 at, Vector3 right, Vector3 normal, float width, float height)
        {
            if (!artwork)
            {
                var template = GameAssets.I && GameAssets.I.tintBase ? GameAssets.I.tintBase :
                    Surfaces.Get(Surfaces.Kind.Plaster, Color.white);
                artwork = new Material(template) { name = "Optional townhouse wall illustration", color = Color.white };
                artwork.mainTexture = profile.WallArtwork;
                if (artwork.HasProperty("_BaseMap")) artwork.SetTexture("_BaseMap", profile.WallArtwork);
                if (artwork.HasProperty("_BumpMap")) { artwork.SetTexture("_BumpMap", null); artwork.DisableKeyword("_NORMALMAP"); }
                ownedMaterials.Add(artwork);
            }
            float ratio = (float)profile.WallArtwork.width / profile.WallArtwork.height;
            float fittedWidth = Mathf.Min(width, height * ratio), fittedHeight = fittedWidth / ratio;
            Quad(at, right * fittedWidth * .5f, Vector3.up * fittedHeight * .5f, normal, artwork);
        }

        void FlatBox(Vector3 face, Vector3 right, Vector3 inward, float width, float height, float depth, Material material)
        {
            Vector3 size = new Vector3(Mathf.Abs(right.x) * width + Mathf.Abs(inward.x) * depth,
                height, Mathf.Abs(right.z) * width + Mathf.Abs(inward.z) * depth);
            Box(face - inward * depth * .5f, size, material);
        }

        void RoundedPanel(Vector3 center, Vector3 right, Vector3 normal, float width, float height, float radius, Material material)
        {
            radius = Mathf.Min(radius, Mathf.Min(width, height) * .49f);
            var batch = BatchFor(material, 37);
            int first = batch.Vertices.Count;
            batch.Vertices.Add(center); batch.Normals.Add(normal); batch.Uvs.Add(new Vector2(.5f, .5f));
            for (int corner = 0; corner < 4; corner++)
            {
                float sx = corner < 2 ? 1 : -1, sy = corner == 0 || corner == 3 ? 1 : -1;
                var origin = new Vector2(sx * (width * .5f - radius), sy * (height * .5f - radius));
                for (int step = 0; step <= 8; step++)
                {
                    float angle = (90 - corner * 90 - step * 90f / 8) * Mathf.Deg2Rad;
                    Vector2 point = origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    batch.Vertices.Add(center + right * point.x + Vector3.up * point.y);
                    batch.Normals.Add(normal); batch.Uvs.Add(new Vector2(point.x / width + .5f, point.y / height + .5f));
                }
            }
            int total = 36;
            for (int i = 0; i < total; i++)
            { batch.Triangles.Add(first); batch.Triangles.Add(first + 1 + (i + 1) % total); batch.Triangles.Add(first + 1 + i); }
        }

        void Box(Vector3 center, Vector3 size, Material material)
        {
            Quad(center + Vector3.right * size.x * .5f, Vector3.back * size.z * .5f, Vector3.up * size.y * .5f, Vector3.right, material, true);
            Quad(center - Vector3.right * size.x * .5f, Vector3.forward * size.z * .5f, Vector3.up * size.y * .5f, Vector3.left, material, true);
            Quad(center + Vector3.forward * size.z * .5f, Vector3.right * size.x * .5f, Vector3.up * size.y * .5f, Vector3.forward, material, true);
            Quad(center - Vector3.forward * size.z * .5f, Vector3.left * size.x * .5f, Vector3.up * size.y * .5f, Vector3.back, material, true);
            Quad(center + Vector3.up * size.y * .5f, Vector3.right * size.x * .5f, Vector3.back * size.z * .5f, Vector3.up, material, true);
            Quad(center - Vector3.up * size.y * .5f, Vector3.right * size.x * .5f, Vector3.forward * size.z * .5f, Vector3.down, material, true);
        }

        void Quad(Vector3 center, Vector3 right, Vector3 up, Vector3 normal, Material material, bool metreUv = false)
        {
            var batch = BatchFor(material, 4); int first = batch.Vertices.Count;
            batch.Vertices.AddRange(new[] { center - right - up, center + right - up, center + right + up, center - right + up });
            batch.Normals.AddRange(new[] { normal, normal, normal, normal });
            float u = metreUv ? right.magnitude * 2 / 3 : 1, v = metreUv ? up.magnitude * 2 / 3 : 1;
            batch.Uvs.AddRange(new[] { Vector2.zero, new Vector2(u, 0), new Vector2(u, v), new Vector2(0, v) });
            batch.Triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
        }

        Batch BatchFor(Material material, int additional)
        {
            if (!batches.TryGetValue(material, out var batch)) batches.Add(material, batch = new Batch());
            if (batch.Vertices.Count + additional > MaximumVerticesPerBatch)
            {
                completedBatches.Add(new KeyValuePair<Material, Batch>(material, batch));
                batch = new Batch(); batches[material] = batch;
            }
            return batch;
        }

        void CollectWallPieces()
        {
            foreach (var collider in world.Surfaces)
            {
                var node = collider.GetComponent<WordboundCourseNode>();
                if (node.Kind != WordboundCourseNodeKind.Wall || !modules.TryGetValue(node.ModuleId, out var module)) continue;
                Vector3 center = world.transform.InverseTransformPoint(collider.transform.position);
                Vector3 size = collider.transform.localScale;
                bool fixedX = FixedX(center, module);
                wallPieces.Add(new WallPiece(module.Id, fixedX, fixedX ? center.x : center.z, Piece(center, size, fixedX)));
            }
        }

        static bool FixedX(Vector3 center, ResolvedCourseModule module) =>
            Mathf.Abs(center.x - module.Bounds.Min.X / 1000f) < .001f || Mathf.Abs(center.x - module.Bounds.Max.X / 1000f) < .001f;

        static Rect Piece(Vector3 center, Vector3 size, bool fixedX)
        {
            float across = fixedX ? center.z : center.x, width = fixedX ? size.z : size.x;
            return Rect.MinMaxRect(across - width * .5f, center.y - size.y * .5f, across + width * .5f, center.y + size.y * .5f);
        }

        void Slab(Rect rect, bool fixedX, float fixedValue, Vector3 inward, float near, float far, Material material)
        {
            Vector3 at = fixedX ? new Vector3(fixedValue, rect.center.y, rect.center.x) : new Vector3(rect.center.x, rect.center.y, fixedValue);
            Vector3 size = fixedX ? new Vector3(far - near, rect.height, rect.width) : new Vector3(rect.width, rect.height, far - near);
            Box(at + inward * ((near + far) * .5f), size, material);
        }

        void Boundary(Rect rect, bool fixedX, float fixedValue, Vector3 inward, float depth, float roomFloor,
            PaletteMaterials palette, WordboundRoomFinish finish)
        {
            float top = Mathf.Min(rect.yMax, roomFloor + finish.BoundaryHeight);
            if (top > rect.yMin + .001f)
            {
                float coping = Mathf.Max(rect.yMin, top - .06f);
                if (coping > rect.yMin + .001f)
                    Slab(Rect.MinMaxRect(rect.xMin, rect.yMin, rect.xMax, coping), fixedX, fixedValue, inward, -depth * .7f, depth * .7f, palette.Wall);
                Slab(Rect.MinMaxRect(rect.xMin, coping, rect.xMax, top), fixedX, fixedValue, inward, -depth, depth, palette.Frame);
            }
            float bottom = Mathf.Max(rect.yMin, top);
            if (rect.yMax - bottom < .6f || rect.width < .1f) return;
            var railing = Railing(finish);
            float post = Mathf.Min(.04f, depth), rail = Mathf.Min(.025f, depth), bar = Mathf.Min(.015f, depth);
            int bays = Mathf.Max(1, Mathf.CeilToInt(rect.width / 2.4f));
            var posts = new float[bays + 1];
            for (int i = 0; i <= bays; i++)
            {
                posts[i] = Mathf.Lerp(rect.xMin + .04f, rect.xMax - .04f, i / (float)bays);
                Slab(Rect.MinMaxRect(posts[i] - .04f, bottom, posts[i] + .04f, rect.yMax), fixedX, fixedValue, inward, -post, post, railing);
            }
            Slab(Rect.MinMaxRect(rect.xMin, rect.yMax - .28f, rect.xMax, rect.yMax - .23f), fixedX, fixedValue, inward, -rail, rail, railing);
            Slab(Rect.MinMaxRect(rect.xMin, bottom + .12f, rect.xMax, bottom + .17f), fixedX, fixedValue, inward, -rail, rail, railing);
            for (float x = rect.xMin + .12f; x < rect.xMax - .1f; x += .15f)
                if (posts.All(p => Mathf.Abs(p - x) > .07f))
                    Slab(Rect.MinMaxRect(x - .015f, bottom, x + .015f, rect.yMax - .03f), fixedX, fixedValue, inward, -bar, bar, railing);
        }

        static List<Rect> Subtract(List<Rect> input, Rect hole)
        {
            var result = new List<Rect>();
            foreach (var r in input)
            {
                float left = Mathf.Max(r.xMin, hole.xMin), right = Mathf.Min(r.xMax, hole.xMax);
                float bottom = Mathf.Max(r.yMin, hole.yMin), top = Mathf.Min(r.yMax, hole.yMax);
                if (right <= left || top <= bottom) { result.Add(r); continue; }
                Keep(result, r.xMin, r.yMin, left, r.yMax);
                Keep(result, right, r.yMin, r.xMax, r.yMax);
                Keep(result, left, r.yMin, right, bottom);
                Keep(result, left, top, right, r.yMax);
            }
            return result;
        }

        static void Keep(List<Rect> list, float x1, float y1, float x2, float y2)
        { if (x2 - x1 > .001f && y2 - y1 > .001f) list.Add(Rect.MinMaxRect(x1, y1, x2, y2)); }

        PaletteMaterials RoomMaterials(ResolvedCourseModule module) =>
            finishes.TryGetValue(module.Id, out var finish) ? Materials(finish.Palette, finish.Ground) :
                Materials(profile.ForLayer(module.LayerId), Surfaces.Kind.Planks);

        Material Railing(WordboundRoomFinish finish)
        {
            if (!railings.TryGetValue(finish, out var found)) railings.Add(finish, found = Make(null, finish.Railing, "Railing"));
            return found;
        }

        PaletteMaterials Materials(WordboundRoomPalette palette, Surfaces.Kind ground)
        {
            if (materials.TryGetValue(palette, out var found)) return found;
            found = new PaletteMaterials {
                Floor = Make(palette.FloorMaterial, palette.Floor, "Floor", ground), Wall = Make(palette.WallMaterial, palette.Wall, "Wall"),
                Ceiling = Make(palette.CeilingMaterial, palette.Ceiling, "Ceiling"), Frame = Make(palette.FrameMaterial, palette.Frame, "Frame"),
                Inset = Make(palette.InsetMaterial, palette.Inset, "Inset"), Accent = Make(palette.AccentMaterial, palette.Accent, "Accent")
            };
            materials.Add(palette, found); return found;
        }

        Material Make(Material assigned, Color colour, string role, Surfaces.Kind surface = Surfaces.Kind.Plaster)
        {
            if (assigned) return assigned;
            var value = new Material(Surfaces.Get(surface, colour)) { name = "Room " + role, color = colour };
            if (surface != Surfaces.Kind.Planks && value.HasProperty("_Smoothness")) value.SetFloat("_Smoothness", role == "Inset" ? .45f : .16f);
            ownedMaterials.Add(value); return value;
        }

        void OnDestroy() => Dispose();

        public void Dispose()
        {
            foreach (var mesh in ownedMeshes) Release(mesh);
            foreach (var material in ownedMaterials) Release(material);
            ownedMeshes.Clear(); ownedMaterials.Clear();
        }
        static void Release(UnityEngine.Object value)
        { if (!value) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }

        sealed class PaletteMaterials { public Material Floor, Wall, Ceiling, Frame, Inset, Accent; }
        sealed class WallPiece
        {
            public readonly string ModuleId; public readonly bool FixedX; public readonly float Fixed; public readonly Rect Rect;
            public WallPiece(string module, bool fixedX, float value, Rect rect) { ModuleId = module; FixedX = fixedX; Fixed = value; Rect = rect; }
        }
        sealed class Batch
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }
    }
}
