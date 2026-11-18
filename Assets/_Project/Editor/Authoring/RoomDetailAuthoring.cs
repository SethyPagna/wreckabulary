using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTools
{
    public static class RoomDetailAuthoring
    {
        public const string MeshFolder = "Assets/_Project/Worlds/Generated/Meshes";
        public const string AssetStem = "detail1_shared";
        public const float Reach = 3.3f, Walkway = .85f, FurnitureMargin = .45f, MaxDepth = .95f;
        static readonly string[] Maps = { "pinwheel", "courtyard", "flat", "terrace", "walkup" };
        static readonly HashSet<string> Structure = new(StringComparer.Ordinal) { "Wall", "Wall trim", "Rug", "Plinth", "Lawn", "Stairs ramp" };

        [MenuItem("Wreckabulary/Authoring/Detail Current Worlds", priority = 22)]
        public static void DetailWorlds()
        {
            Guard();
            int rooms = 0;
            foreach (string map in Maps)
            {
                string path = "Assets/_Project/Resources/" + AuthoredHouse.ResourcePath(map) + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var world = root.GetComponent<AuthoredHouse>();
                    if (!world || !world.IsCurrent) throw new InvalidOperationException("Migrate the authored world first: " + path);
                    int added = ApplyMissing(world);
                    if (added == 0) continue;
                    rooms += added;
                    if (!PrefabUtility.SaveAsPrefabAsset(root, path)) throw new InvalidOperationException("Could not save room detail: " + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("ROOM_DETAIL_READY: " + rooms + " rooms detailed across five worlds.");
        }

        [MenuItem("Wreckabulary/Authoring/Report Room Detail", priority = 23)]
        public static void Report()
        {
            var lines = new List<string> { "# Room detail inventory", "", "Generated " + DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ") + " from the saved world prefabs.", "" };
            foreach (string map in Maps)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/" + AuthoredHouse.ResourcePath(map) + ".prefab");
                var world = prefab ? prefab.GetComponent<AuthoredHouse>() : null;
                if (!world) continue;
                var layout = GameConfig.Current.HouseFor(map);
                var roots = world.GetComponentsInChildren<RoomDetail>(true).Where(d => !d.Fitted).ToArray();
                int renderers = roots.Sum(r => r.GetComponentsInChildren<Renderer>(true).Length);
                long triangles = roots.Sum(r => Triangles(r.gameObject)), casting = roots.Sum(r => Triangles(r.gameObject, true));
                lines.Add("## " + layout.Name + " (" + map + ")");
                lines.Add("");
                lines.Add("World renderers " + world.GetComponentsInChildren<Renderer>(true).Length + ", of which detail " + renderers + "; detail triangles " + triangles +
                    ", of which shadow casting " + casting + ".");
                lines.Add("");
                lines.Add("| Room | Storey | Fitted runs | Renderers | Triangles | Shadow triangles | Added |");
                lines.Add("|---|---|---|---|---|---|---|");
                foreach (var room in layout.Rooms)
                {
                    var root = roots.FirstOrDefault(r => r.RoomId == room.Name);
                    if (!root) { lines.Add("| " + room.Name + " | " + layout.StoreyOf(room) + " | missing | | | | |"); continue; }
                    var fitted = root.GetComponentsInChildren<RoomDetail>(true).FirstOrDefault(d => d.Fitted);
                    lines.Add("| " + room.Name + (room.Outdoor ? " (outdoor)" : "") + " | " + root.OwnerStorey + " | " + (fitted ? fitted.Spans.Count : 0) + " | " +
                        root.GetComponentsInChildren<Renderer>(true).Length + " | " + Triangles(root.gameObject) + " | " + Triangles(root.gameObject, true) + " | " +
                        string.Join("; ", root.Pieces) + " |");
                }
                lines.Add("");
            }
            string path = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../BuildNotes/house-detail/rooms.md"));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllLines(path, lines);
            Debug.Log("ROOM_DETAIL_REPORT: " + path);
        }

        public static long Triangles(GameObject root, bool castersOnly = false)
        {
            long total = 0;
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                if (castersOnly && (!filter.TryGetComponent<Renderer>(out var renderer) || renderer.shadowCastingMode == ShadowCastingMode.Off)) continue;
                for (int i = 0; i < filter.sharedMesh.subMeshCount; i++) total += filter.sharedMesh.GetIndexCount(i) / 3;
            }
            return total;
        }

        public static int ApplyMissing(AuthoredHouse world)
        {
            var meshes = new List<Mesh>();
            var roots = Build(world, meshes);
            if (roots.Count == 0) return 0;
            SaveMeshes(world.MapId, meshes);
            foreach (var root in roots) WorldAuthoring.PersistAssets(root, AssetStem);
            AssetDatabase.SaveAssets();
            for (int edition = 1; edition < RoomDetail.CurrentEdition; edition++)
            {
                string old = MeshPath(edition, world.MapId);
                if (AssetDatabase.LoadMainAssetAtPath(old)) AssetDatabase.DeleteAsset(old);
            }
            return roots.Count;
        }

        public static List<GameObject> Build(AuthoredHouse world, List<Mesh> meshes)
        {
            Guard();
            if (!world || !world.GeometryRoot) throw new ArgumentException("An authored geometry root is required.", nameof(world));
            var layout = GameConfig.Current.HouseFor(world.MapId);
            foreach (var stale in world.GeometryRoot.GetComponentsInChildren<RoomDetail>(true)
                .Where(d => !d.Fitted && d.Edition < RoomDetail.CurrentEdition).ToList())
                UnityEngine.Object.DestroyImmediate(stale.gameObject, true);
            var done = world.GeometryRoot.GetComponentsInChildren<RoomDetail>(true)
                .Where(d => !d.Fitted && d.Edition >= RoomDetail.CurrentEdition).Select(d => d.RoomId).ToHashSet(StringComparer.Ordinal);
            var site = new Site(world, layout);
            var roots = new List<GameObject>();
            foreach (var room in layout.Rooms)
            {
                if (done.Contains(room.Name)) continue;
                roots.Add(new Detailer(site, room).Build(meshes));
            }
            return roots;
        }

        static void SaveMeshes(string map, List<Mesh> meshes)
        {
            if (meshes.Count == 0) return;
            string path = MeshPath(RoomDetail.CurrentEdition, map);
            int start = 0;
            if (!AssetDatabase.LoadMainAssetAtPath(path))
            {
                AssetDatabase.CreateAsset(meshes[0], path);
                start = 1;
            }
            for (int i = start; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], path);
            AssetDatabase.SaveAssets();
        }

        static string MeshPath(int edition, string map) =>
            MeshFolder + "/detail" + edition + "_v" + AuthoredHouse.CurrentContentVersion + "_" + map + ".asset";

        static void Guard()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Detail authored worlds outside Play mode.");
        }

        public static float ColliderReach(HouseLayout layout, RoomBox room)
        {
            var floors = layout.StoreyFloors();
            int storey = layout.StoreyOf(room);
            return storey + 1 < floors.Count ? Mathf.Min(Reach, floors[storey + 1] - .26f - room.FloorY) : Reach;
        }

        public static float CeilingHeight(HouseLayout layout, RoomBox room)
        {
            var floors = layout.StoreyFloors();
            int storey = layout.StoreyOf(room);
            return storey + 1 < floors.Count ? floors[storey + 1] - .28f : room.FloorY + 3f;
        }

        enum Kind { Plain, Kitchen, Bedroom, Kids, Play, Living, Flat, Study, Library, Dining, Bath, Hall, Lobby, Workshop, Garage, Attic, Cafe, Garden, Patio, Roof, Balcony }

        enum Fit { Counter, Wardrobe, BookcaseTall, BookcaseLow, Console, Fireplace, Cubbies, Dresser, Sideboard, Vanity, TowelRail, ShoeBench, Mailboxes, Radiator, Workbench, Shelving, Eaves, ServiceCounter, Banquette, Bed, Hedge, Planter, RoofPlant }

        static string Trimmed(string name) => name.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');

        static Kind KindOf(RoomBox room) => Trimmed(room.Name) switch
        {
            "Kitchen" or "CafeKitchen" or "Diner" => Kind.Kitchen,
            "Bedroom" or "BlueBedroom" or "RedBedroom" or "GreenBedroom" or "YellowBedroom" => Kind.Bedroom,
            "KidsRoom" => Kind.Kids,
            "Playroom" => Kind.Play,
            "LivingRoom" or "Lounge" => Kind.Living,
            "BlueFlat" or "RedFlat" or "GreenFlat" or "YellowFlat" => Kind.Flat,
            "Study" => Kind.Study,
            "Library" => Kind.Library,
            "Dining" => Kind.Dining,
            "Bathroom" => Kind.Bath,
            "Hall" or "Landing" or "Walkway" => Kind.Hall,
            "Lobby" => Kind.Lobby,
            "Garage" => Kind.Garage,
            "Workshop" or "Storeroom" or "Shed" => Kind.Workshop,
            "Attic" => Kind.Attic,
            "Cafe" => Kind.Cafe,
            "Garden" or "Yard" or "PoolLawn" or "Orchard" or "RoofGarden" => Kind.Garden,
            "Patio" or "FountainSquare" => Kind.Patio,
            "Roof" => Kind.Roof,
            "FireEscape" => Kind.Balcony,
            _ => room.Outdoor ? Kind.Patio : Kind.Plain
        };

        static Fit[] Program(Kind kind) => kind switch
        {
            Kind.Kitchen => new[] { Fit.Counter, Fit.Counter, Fit.Counter },
            Kind.Bedroom => new[] { Fit.Wardrobe, Fit.Dresser, Fit.BookcaseLow },
            Kind.Kids => new[] { Fit.Cubbies, Fit.Wardrobe, Fit.BookcaseLow },
            Kind.Play => new[] { Fit.Cubbies, Fit.BookcaseLow, Fit.Cubbies },
            Kind.Living => new[] { Fit.Fireplace, Fit.BookcaseTall, Fit.Console, Fit.BookcaseLow },
            Kind.Flat => new[] { Fit.Counter, Fit.BookcaseTall, Fit.Console },
            Kind.Study => new[] { Fit.BookcaseTall, Fit.BookcaseTall, Fit.Console },
            Kind.Library => new[] { Fit.BookcaseTall },
            Kind.Dining => new[] { Fit.Sideboard, Fit.Console, Fit.BookcaseLow },
            Kind.Bath => new[] { Fit.Vanity, Fit.TowelRail },
            Kind.Hall => new[] { Fit.ShoeBench, Fit.Console, Fit.BookcaseLow },
            Kind.Lobby => new[] { Fit.Mailboxes, Fit.ShoeBench, Fit.Console },
            Kind.Garage or Kind.Workshop => new[] { Fit.Workbench, Fit.Shelving, Fit.Shelving },
            Kind.Attic => new[] { Fit.Eaves },
            Kind.Cafe => new[] { Fit.ServiceCounter, Fit.Banquette, Fit.Banquette },
            Kind.Garden => new[] { Fit.Bed, Fit.Hedge, Fit.Bed },
            Kind.Patio => new[] { Fit.Planter, Fit.Bed, Fit.Hedge },
            Kind.Roof => new[] { Fit.RoofPlant },
            Kind.Plain => new[] { Fit.BookcaseLow, Fit.Console },
            _ => Array.Empty<Fit>()
        };

        static float Coverage(Kind kind) => kind switch
        {
            Kind.Library => .6f,
            Kind.Roof => .3f,
            Kind.Balcony => 0f,
            Kind.Garden or Kind.Patio => .55f,
            _ => .5f
        };

        static (float min, float max, float depth, bool tall) Spec(Fit fit) => fit switch
        {
            Fit.Counter => (1.2f, 4.2f, .62f, false),
            Fit.Wardrobe => (1.2f, 3f, .62f, true),
            Fit.BookcaseTall => (.9f, 3.6f, .36f, true),
            Fit.BookcaseLow => (.9f, 2.4f, .36f, false),
            Fit.Console => (1f, 2.2f, .42f, false),
            Fit.Fireplace => (1.9f, 1.9f, .45f, true),
            Fit.Cubbies => (1.05f, 3.15f, .4f, false),
            Fit.Dresser => (1f, 1.8f, .5f, false),
            Fit.Sideboard => (1.4f, 2.4f, .48f, false),
            Fit.Vanity => (.9f, 1.4f, .5f, false),
            Fit.TowelRail => (.6f, .6f, .16f, false),
            Fit.ShoeBench => (1f, 1.6f, .4f, false),
            Fit.Mailboxes => (1.2f, 2.4f, .32f, true),
            Fit.Radiator => (1f, 1f, .15f, false),
            Fit.Workbench => (1.6f, 2.8f, .7f, false),
            Fit.Shelving => (1f, 3f, .5f, true),
            Fit.Eaves => (1.2f, 4f, .55f, false),
            Fit.ServiceCounter => (2f, 4f, .7f, false),
            Fit.Banquette => (1.6f, 3.6f, .6f, false),
            Fit.Bed => (1.6f, 6f, .7f, false),
            Fit.Hedge => (2f, 8f, .75f, false),
            Fit.Planter => (1.2f, 2.4f, .65f, false),
            Fit.RoofPlant => (1f, 2.6f, .95f, false),
            _ => (1f, 2f, .4f, false)
        };

        const int White = 0, Cream = 1, LightGrey = 2, MidGrey = 3, DarkGrey = 4, Black = 5, Chrome = 6, Brass = 7,
            Books = 8, Toys = 16, Leaves = 22, Flowers = 27, Soil = 33, Bark = 34, DarkWood = 35, MidWood = 36, LightWood = 37,
            StoneLight = 38, StoneMid = 39, Brick = 40, Terracotta = 41, Glass = 42, Fabrics = 44, Candle = 51, Leather = 52,
            Slate = 53, Paper = 54, Hearth = 56, Firebox = 57, Ember = 58, Sage = 60, DuckEgg = 61, Rose = 62, Butter = 63;

        static readonly uint[] Swatches =
        {
            0xF7F4EE, 0xEDE3CF, 0xC9C6BF, 0x8E8C88, 0x55534F, 0x2A2826, 0xB9C3CC, 0xC8A050,
            0x9E3B35, 0x2F4A6E, 0x3D6B4A, 0xD2A63C, 0xC8692F, 0x2E7C7A, 0x6E3E63, 0xB89A70,
            0xD9483B, 0x3E7CC9, 0xF2C230, 0x5BAE4F, 0xF08A2E, 0x8A5BC2, 0x2F5A33, 0x4A7F3E,
            0x7DAA5A, 0x75803A, 0x3E7A68, 0xE58FB0, 0xF4F1E6, 0xF3D34A, 0xB79AD6, 0xD0473F,
            0xEE9A47, 0x5A4232, 0x7A5638, 0x5E3F2A, 0x8E6440, 0xC09A6B, 0xD6D0C2, 0xA8A196,
            0xA4573F, 0xC0714F, 0x9CC3D5, 0x7FB6C9, 0xB5473F, 0x3F5F8F, 0x5D8A5A, 0xD3A23F,
            0x8B8F96, 0xD99AA5, 0xB89A6E, 0xF2E6C8, 0x7A4E2E, 0x33393B, 0xFBF8F0, 0xD8CDB5,
            0xBDB3A2, 0x1E1C1B, 0xE0662C, 0xF1F1EE, 0x9DB59E, 0xA9CFD0, 0xD9A3A0, 0xF2DE9A,
        };

        static Material palette;

        static Vector2 Swatch(int index) => new(((index % 8) + .5f) / 8f, ((index / 8) + .5f) / 8f);

        static Material Palette()
        {
            if (palette) return palette;
            const int Size = 64;
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, true, false)
            { name = "Room detail palette", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    pixels[y * Size + x] = Surfaces.Hex(Swatches[(y / 8) * 8 + x / 8]);
            texture.SetPixels32(pixels);
            texture.Apply(true, false);
            var assets = GameAssets.I;
            var material = assets && assets.tintBase ? new Material(assets.tintBase) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "Room detail palette";
            material.hideFlags = HideFlags.DontSave;
            material.color = Color.white;
            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .16f);
            if (material.HasProperty("_BumpMap")) material.SetTexture("_BumpMap", null);
            material.DisableKeyword("_NORMALMAP");
            return palette = material;
        }

        sealed class Batch
        {
            sealed class Part
            {
                public readonly List<Vector3> Vertices = new();
                public readonly List<Vector3> Normals = new();
                public readonly List<Vector2> Uvs = new();
                public readonly List<int> Triangles = new();
            }

            static readonly (Vector3 normal, Vector3 up)[] Sides =
            {
                (Vector3.right, Vector3.up), (Vector3.left, Vector3.up), (Vector3.forward, Vector3.up),
                (Vector3.back, Vector3.up), (Vector3.up, Vector3.forward), (Vector3.down, Vector3.forward)
            };
            static readonly (float r, float u)[] Corners = { (-.5f, -.5f), (-.5f, .5f), (.5f, .5f), (.5f, -.5f) };
            readonly Dictionary<Material, Part> parts = new();

            public bool Empty => parts.Count == 0;

            Part For(Material material)
            {
                if (!parts.TryGetValue(material, out var part)) parts[material] = part = new Part();
                return part;
            }

            public void Box(Material material, Vector3 centre, Quaternion rotation, Vector3 size, Vector2 tile, Vector2? swatch, bool bottom = true, bool back = true)
            {
                var part = For(material);
                foreach (var (normal, up) in Sides)
                {
                    if (!bottom && normal == Vector3.down) continue;
                    if (!back && normal == Vector3.back) continue;
                    var right = Vector3.Cross(normal, up);
                    int first = part.Vertices.Count;
                    foreach (var (r, u) in Corners)
                    {
                        var local = Vector3.Scale(normal * .5f + right * r + up * u, size);
                        part.Vertices.Add(centre + rotation * local);
                        part.Normals.Add(rotation * normal);
                        if (swatch.HasValue) part.Uvs.Add(swatch.Value);
                        else
                        {
                            var w = local + size * .5f;
                            var uv = normal.y != 0f ? new Vector2(w.x, w.z) : normal.z != 0f ? new Vector2(w.x, w.y) : new Vector2(w.z, w.y);
                            part.Uvs.Add(new Vector2(uv.x / tile.x, uv.y / tile.y));
                        }
                    }
                    part.Triangles.Add(first); part.Triangles.Add(first + 1); part.Triangles.Add(first + 2);
                    part.Triangles.Add(first); part.Triangles.Add(first + 2); part.Triangles.Add(first + 3);
                }
            }

            public void Cylinder(Material material, Vector2 swatch, Vector3 bottom, Quaternion rotation, float lower, float upper, float height, int sides = 10, bool top = true)
            {
                var part = For(material);
                int first = part.Vertices.Count;
                var up = rotation * Vector3.up;
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    var radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    var normal = rotation * radial;
                    part.Vertices.Add(bottom + rotation * (radial * lower));
                    part.Vertices.Add(bottom + rotation * (radial * upper) + up * height);
                    part.Normals.Add(normal); part.Normals.Add(normal);
                    part.Uvs.Add(swatch); part.Uvs.Add(swatch);
                }
                for (int i = 0; i < sides; i++)
                {
                    int b0 = first + i * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                    part.Triangles.Add(b0); part.Triangles.Add(t0); part.Triangles.Add(t1);
                    part.Triangles.Add(b0); part.Triangles.Add(t1); part.Triangles.Add(b1);
                }
                if (!top || upper <= 0f) return;
                int centre = part.Vertices.Count;
                part.Vertices.Add(bottom + up * height); part.Normals.Add(up); part.Uvs.Add(swatch);
                for (int i = 0; i <= sides; i++)
                {
                    float a = i / (float)sides * Mathf.PI * 2f;
                    part.Vertices.Add(bottom + rotation * (new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * upper) + up * height);
                    part.Normals.Add(up); part.Uvs.Add(swatch);
                }
                for (int i = 0; i < sides; i++)
                {
                    part.Triangles.Add(centre); part.Triangles.Add(centre + 2 + i); part.Triangles.Add(centre + 1 + i);
                }
            }

            public void Blob(Material material, Vector2? swatch, Vector2 tile, Vector3 centre, Vector3 radii, int rings = 4, int segments = 7)
            {
                var part = For(material);
                int first = part.Vertices.Count;
                for (int j = 0; j <= rings; j++)
                {
                    float lat = Mathf.Lerp(-Mathf.PI * .5f, Mathf.PI * .5f, j / (float)rings);
                    for (int i = 0; i <= segments; i++)
                    {
                        float lon = i / (float)segments * Mathf.PI * 2f;
                        var unit = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon), Mathf.Sin(lat), Mathf.Cos(lat) * Mathf.Sin(lon));
                        var p = centre + Vector3.Scale(unit, radii);
                        part.Vertices.Add(p);
                        part.Normals.Add(new Vector3(unit.x / radii.x, unit.y / radii.y, unit.z / radii.z).normalized);
                        part.Uvs.Add(swatch ?? new Vector2((p.x + p.y) / tile.x, (p.z + p.y) / tile.y));
                    }
                }
                int stride = segments + 1;
                for (int j = 0; j < rings; j++)
                    for (int i = 0; i < segments; i++)
                    {
                        int b0 = first + j * stride + i, t0 = b0 + stride, b1 = b0 + 1, t1 = t0 + 1;
                        part.Triangles.Add(b0); part.Triangles.Add(t0); part.Triangles.Add(t1);
                        part.Triangles.Add(b0); part.Triangles.Add(t1); part.Triangles.Add(b1);
                    }
            }

            public List<Renderer> Emit(Transform group, string name, bool shadows, List<Mesh> meshes)
            {
                var renderers = new List<Renderer>();
                int n = 0;
                foreach (var pair in parts.OrderBy(p => p.Key.name, StringComparer.Ordinal))
                {
                    var part = pair.Value;
                    var mesh = new Mesh { name = name + " " + ++n };
                    if (part.Vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                    mesh.SetVertices(part.Vertices);
                    mesh.SetNormals(part.Normals);
                    mesh.SetUVs(0, part.Uvs);
                    mesh.SetTriangles(part.Triangles, 0);
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    var go = new GameObject(name);
                    go.transform.SetParent(group, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = pair.Key;
                    renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    renderer.receiveShadows = true;
                    renderers.Add(renderer);
                    meshes.Add(mesh);
                }
                return renderers;
            }
        }

        sealed class Face
        {
            public Vector3 Origin, Inward, Right;
            public float Width, Thickness;
            public readonly List<Vector2> Decor = new(), UpperUsed = new();
            public readonly List<float> Windows = new();
            public Quaternion Rotation => Quaternion.LookRotation(Inward);
            public float Half => Width * .5f;
            public Vector3 At(float x, float y, float z) => Origin + Right * x + Vector3.up * y + Inward * z;
            public float U(Vector3 p) => Vector3.Dot(p - Origin, Right);
            public float D(Vector3 p) => Vector3.Dot(p - Origin, Inward);
        }

        sealed class Site
        {
            public readonly AuthoredHouse World;
            public readonly HouseLayout Layout;
            public readonly Transform Space;
            public readonly List<Bounds> Furniture = new(), Props = new(), Rugs = new();
            public readonly List<(string room, string kind, Bounds bounds)> Decor = new();

            public Site(AuthoredHouse world, HouseLayout layout)
            {
                World = world;
                Layout = layout;
                Space = world.GeometryRoot;
                if (world.FurnitureRoot)
                    foreach (var item in world.FurnitureRoot.GetComponentsInChildren<Smashable>(true))
                        Furniture.Add(ModelVisual.BoundsIn(Space, item.gameObject));
                for (int s = 0; s < layout.StoreyFloors().Count; s++)
                {
                    var storey = Space.Find(RoomBuilder.StoreyName(s));
                    if (!storey) continue;
                    foreach (var renderer in storey.GetComponentsInChildren<Renderer>(true))
                    {
                        if (renderer.GetComponentInParent<RoomDressing>() || renderer.GetComponentInParent<RoomDetail>()) continue;
                        string name = renderer.name;
                        if (name == "Rug") { Rugs.Add(ModelVisual.BoundsIn(Space, renderer.gameObject)); continue; }
                        if (Structure.Contains(name) || name.EndsWith(" floor", StringComparison.Ordinal)) continue;
                        Props.Add(ModelVisual.BoundsIn(Space, renderer.gameObject));
                    }
                }
                foreach (var dressing in Space.GetComponentsInChildren<RoomDressing>(true))
                    foreach (Transform child in dressing.transform)
                        if (child.name is "Window vignette" or "Framed townhouse print" or "Wall sconce")
                            Decor.Add((dressing.RoomId, child.name, ModelVisual.BoundsIn(Space, child.gameObject)));
            }
        }

        sealed class Detailer
        {
            readonly Site site;
            readonly RoomBox room;
            readonly Kind kind;
            readonly int storey;
            readonly float ceiling, reach;
            readonly System.Random rng;
            readonly Batch finish = new(), fitted = new(), small = new(), overhead = new();
            readonly SortedDictionary<string, Batch> uppers = new(StringComparer.Ordinal);
            readonly List<Face> faces;
            readonly List<Bounds> placed = new(), spans = new();
            readonly List<string> notes = new();
            readonly Material pal, wood, dark, paint, top, tile, hedge, skirting, pegboard;
            readonly Dictionary<Material, Vector2> tiles = new();
            readonly int fabric, shade, frameTone;
            float budget;
            int next, counters;
            bool fireplace;
            Face f;
            float cx;
            Batch into;

            public Detailer(Site site, RoomBox room)
            {
                this.site = site;
                this.room = room;
                kind = KindOf(room);
                storey = site.Layout.StoreyOf(room);
                ceiling = CeilingHeight(site.Layout, room);
                reach = ColliderReach(site.Layout, room);
                rng = new System.Random(Seed(site.World.MapId + "/" + room.Name));
                pal = Palette();
                wood = Surface(Surfaces.Kind.Wood, kind is Kind.Study or Kind.Library or Kind.Cafe ? Surfaces.Hex(0x7A5236) : Surfaces.Hex(0xB78A5C));
                dark = Surface(Surfaces.Kind.Wood, Surfaces.Hex(0x5E412C));
                paint = Surface(Surfaces.Kind.Plaster, PaintOf(kind));
                top = Surface(Surfaces.Kind.Wood, Surfaces.Hex(0xC79A68));
                tile = Surface(Surfaces.Kind.Tile, kind == Kind.Bath ? Surfaces.Hex(0xDCEBEA) : Surfaces.Hex(0xF2EEE4));
                hedge = Surface(Surfaces.Kind.Lawn, Surfaces.Hex(0x4F7A45));
                skirting = Surface(Surfaces.Kind.Wood, Surfaces.Hex(0xF1E6D2));
                pegboard = Surface(Surfaces.Kind.Plaster, Surfaces.Hex(0xC9A47A));
                fabric = Fabrics + rng.Next(6);
                shade = new[] { Cream, Sage, DuckEgg, Rose, Butter, Brass }[rng.Next(6)];
                frameTone = new[] { DarkWood, Brass, Black, White }[rng.Next(4)];
                faces = Faces();
            }

            static int Seed(string text)
            {
                unchecked
                {
                    int hash = 17;
                    foreach (char c in text) hash = hash * 31 + c;
                    return hash;
                }
            }

            static Color PaintOf(Kind kind) => Surfaces.Hex(kind switch
            {
                Kind.Kitchen => 0xA9C2B4,
                Kind.Bedroom => 0xF1E6D2,
                Kind.Kids or Kind.Play => 0xF0D9A0,
                Kind.Living or Kind.Flat => 0xEDE2CC,
                Kind.Bath => 0xF4F2EC,
                Kind.Hall or Kind.Lobby => 0xE9DFCB,
                Kind.Dining => 0xB9C7CF,
                Kind.Attic => 0xE6D8BC,
                _ => 0xE4D9C3
            });

            Material Surface(Surfaces.Kind surfaceKind, Color colour)
            {
                var material = Surfaces.Get(surfaceKind, colour);
                tiles[material] = Surfaces.Tile(surfaceKind);
                return material;
            }

            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            bool Chance(float p) => rng.NextDouble() < p;
            int Pick(params int[] options) => options[rng.Next(options.Length)];

            List<Face> Faces()
            {
                var result = new List<Face>();
                var space = site.Space;
                foreach (var renderer in space.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (!renderer.enabled || !renderer.name.StartsWith("Wall", StringComparison.Ordinal) || !renderer.GetComponent<BoxCollider>()) continue;
                    if (renderer.GetComponentInParent<RoomDressing>() || renderer.GetComponentInParent<RoomDetail>()) continue;
                    var b = ModelVisual.BoundsIn(space, renderer.gameObject);
                    if (Mathf.Abs(b.min.y - room.FloorY) > .25f || b.size.y < 1f) continue;
                    bool alongX = b.size.x > b.size.z;
                    float edge = alongX ? b.center.z : b.center.x;
                    float minimum = alongX ? room.MinZ : room.MinX, maximum = alongX ? room.MaxZ : room.MaxX;
                    if (Mathf.Min(Mathf.Abs(edge - minimum), Mathf.Abs(edge - maximum)) > .35f) continue;
                    float start = Mathf.Max(alongX ? b.min.x : b.min.z, alongX ? room.MinX : room.MinZ);
                    float end = Mathf.Min(alongX ? b.max.x : b.max.z, alongX ? room.MaxX : room.MaxZ);
                    if (end - start < .35f) continue;
                    bool lowSide = Mathf.Abs(edge - minimum) < Mathf.Abs(edge - maximum);
                    var inward = alongX ? (lowSide ? Vector3.forward : Vector3.back) : (lowSide ? Vector3.right : Vector3.left);
                    float thickness = alongX ? b.size.z : b.size.x;
                    var centre = alongX ? new Vector3((start + end) * .5f, room.FloorY, edge) : new Vector3(edge, room.FloorY, (start + end) * .5f);
                    var face = new Face
                    {
                        Origin = centre + inward * (thickness * .5f), Inward = inward, Right = Quaternion.LookRotation(inward) * Vector3.right,
                        Width = end - start, Thickness = thickness
                    };
                    foreach (var (decorRoom, decorKind, bounds) in site.Decor)
                    {
                        if (decorRoom != room.Name || !Project(face, bounds, -.4f, .6f, out float u0, out float u1)) continue;
                        face.Decor.Add(new Vector2(u0 - .12f, u1 + .12f));
                        if (decorKind == "Window vignette") face.Windows.Add((u0 + u1) * .5f);
                    }
                    result.Add(face);
                }
                return result.OrderByDescending(face => face.Width).ThenBy(face => face.Origin.x).ThenBy(face => face.Origin.z).ToList();
            }

            static bool Project(Face face, Bounds b, float near, float far, out float u0, out float u1)
            {
                u0 = float.MaxValue; u1 = float.MinValue;
                float d0 = float.MaxValue, d1 = float.MinValue;
                for (int corner = 0; corner < 4; corner++)
                {
                    var p = new Vector3((corner & 1) == 0 ? b.min.x : b.max.x, face.Origin.y, (corner & 2) == 0 ? b.min.z : b.max.z);
                    float u = face.U(p), d = face.D(p);
                    u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u);
                    d0 = Mathf.Min(d0, d); d1 = Mathf.Max(d1, d);
                }
                return d1 > near && d0 < far && u1 > -face.Half && u0 < face.Half;
            }

            public GameObject Build(List<Mesh> meshes)
            {
                var parent = site.Space.Find(RoomBuilder.StoreyName(storey));
                if (!parent) throw new InvalidOperationException("Missing authored storey for " + room.Name);
                budget = Coverage(kind) * faces.Sum(face => face.Width);
                foreach (var face in faces) Fill(face);
                if (!room.Outdoor && kind is not (Kind.Workshop or Kind.Garage or Kind.Attic or Kind.Cafe)) Radiators();
                if (!room.Outdoor && kind is not (Kind.Bath or Kind.Kitchen or Kind.Workshop or Kind.Garage or Kind.Attic)) Gallery();
                Finish();
                Ceiling();

                var root = Group(parent, RoomDetail.RootPrefix + room.Name);
                root.SetPositionAndRotation(site.Space.position, site.Space.rotation);
                var marker = root.gameObject.AddComponent<RoomDetail>();
                marker.Configure(room.Name, storey, false);
                marker.SetPieces(notes.ToArray());
                if (!finish.Empty) finish.Emit(Group(root, "Room finish"), room.Name + " finish", false, meshes);
                if (!fitted.Empty || !small.Empty || spans.Count > 0)
                {
                    var group = Group(root, "Fitted detail");
                    var detail = group.gameObject.AddComponent<RoomDetail>();
                    detail.Configure(room.Name, storey, true);
                    detail.SetSpans(spans.ToArray());
                    foreach (var span in spans)
                    {
                        var box = group.gameObject.AddComponent<BoxCollider>();
                        box.center = span.center;
                        box.size = span.size;
                    }
                    fitted.Emit(group, room.Name + " fitted", true, meshes);
                    small.Emit(group, room.Name + " fitted small", false, meshes);
                }
                int n = 0;
                foreach (var pair in uppers)
                {
                    if (pair.Value.Empty) continue;
                    var group = Group(root, "Wall detail " + ++n);
                    var renderers = pair.Value.Emit(group, room.Name + " wall detail " + n, false, meshes);
                    group.gameObject.AddComponent<CutawaySurface>().Configure(CutawayKind.UpperWall, storey, room.Name, renderers.ToArray());
                }
                if (!overhead.Empty)
                {
                    var group = Group(root, "Ceiling detail");
                    var renderers = overhead.Emit(group, room.Name + " ceiling detail", false, meshes);
                    group.gameObject.AddComponent<CutawaySurface>().Configure(CutawayKind.Ceiling, storey, room.Name, renderers.ToArray());
                }
                return root.gameObject;
            }

            static Transform Group(Transform parent, string name)
            {
                var group = new GameObject(name).transform;
                group.SetParent(parent, false);
                return group;
            }

            List<Vector2> Blocks(Face face)
            {
                var blocks = new List<Vector2>();
                float band = MaxDepth + Walkway;
                foreach (var b in site.Furniture)
                    if (b.max.y > room.FloorY - .3f && b.min.y < room.FloorY + 2.6f && Project(face, b, -.3f, band, out float u0, out float u1))
                        blocks.Add(new Vector2(u0 - FurnitureMargin, u1 + FurnitureMargin));
                foreach (var b in site.Props)
                    if (b.max.y > room.FloorY - .2f && b.min.y < room.FloorY + 2.6f && Project(face, b, -.3f, band, out float u0, out float u1))
                        blocks.Add(new Vector2(u0 - .3f, u1 + .3f));
                foreach (var b in placed)
                    if (Project(face, b, -.05f, MaxDepth + .05f, out float u0, out float u1))
                        blocks.Add(new Vector2(u0 - .05f, u1 + .05f));
                var layout = site.Layout;
                foreach (var door in layout.Doors)
                {
                    var a = layout.Room(door.A); var b = layout.Room(door.B);
                    if ((a == null || !a.SameStorey(room)) && (b == null || !b.SameStorey(room))) continue;
                    Point(face, blocks, door.X, door.Z, 2f, door.Width * .5f + 1.1f);
                }
                foreach (var spawn in layout.Spawns)
                    if (spawn.Room == room.Name) Point(face, blocks, spawn.X, spawn.Z, 2.4f, 1.4f);
                foreach (var keepsake in layout.Keepsakes)
                    if (keepsake.Room == room.Name) Point(face, blocks, keepsake.X, keepsake.Z, 1.8f, 1f);
                if (layout.ExtractionRoom == room.Name) Point(face, blocks, layout.ExtractionX, layout.ExtractionZ, 2.5f, 2f);
                foreach (var stair in layout.Stairs)
                {
                    if (stair.Lower != room.Name && stair.Upper != room.Name) continue;
                    var b = new Bounds();
                    b.SetMinMax(new Vector3(stair.MinX - Stairway.StepOff, room.FloorY, stair.MinZ - Stairway.StepOff),
                        new Vector3(stair.MaxX + Stairway.StepOff, room.FloorY + 2f, stair.MaxZ + Stairway.StepOff));
                    if (Project(face, b, -.3f, band, out float u0, out float u1)) blocks.Add(new Vector2(u0 - .3f, u1 + .3f));
                }
                if (ExitWall(out var exitCentre, out float exitWidth))
                    Point(face, blocks, exitCentre.x, exitCentre.z, .6f, exitWidth * .5f + 1f);
                return blocks;
            }

            static void Point(Face face, List<Vector2> blocks, float x, float z, float depth, float half)
            {
                var p = new Vector3(x, face.Origin.y, z);
                float d = face.D(p), u = face.U(p);
                if (d < -.5f || d > depth || u < -face.Half - half || u > face.Half + half) return;
                blocks.Add(new Vector2(u - half, u + half));
            }

            bool ExitWall(out Vector3 centre, out float width)
            {
                centre = default; width = 0f;
                if (room.Outdoor) return false;
                var walls = new (Vector3 centre, Vector3 outward, float length)[]
                {
                    (new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY, room.MinZ), Vector3.back, room.MaxX - room.MinX),
                    (new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY, room.MaxZ), Vector3.forward, room.MaxX - room.MinX),
                    (new Vector3(room.MinX, room.FloorY, (room.MinZ + room.MaxZ) * .5f), Vector3.left, room.MaxZ - room.MinZ),
                    (new Vector3(room.MaxX, room.FloorY, (room.MinZ + room.MaxZ) * .5f), Vector3.right, room.MaxZ - room.MinZ),
                };
                float longest = 0f;
                foreach (var wall in walls)
                {
                    var probe = wall.centre + wall.outward * .6f + Vector3.up * .5f;
                    string at = site.Layout.RoomAt(probe.x, probe.y, probe.z);
                    if (!string.IsNullOrEmpty(at) && !site.Layout.IsOutdoor(at)) continue;
                    if (wall.length <= longest) continue;
                    longest = wall.length;
                    centre = wall.centre;
                    width = Mathf.Clamp(wall.length - 1f, 1.1f, 2.4f);
                }
                return longest > 0f;
            }

            static List<Vector2> Subtract(float a, float b, List<Vector2> blocks)
            {
                var result = new List<Vector2> { new(a, b) };
                foreach (var block in blocks)
                {
                    var next = new List<Vector2>();
                    foreach (var range in result)
                    {
                        if (block.y <= range.x || block.x >= range.y) { next.Add(range); continue; }
                        if (block.x > range.x) next.Add(new Vector2(range.x, block.x));
                        if (block.y < range.y) next.Add(new Vector2(block.y, range.y));
                    }
                    result = next;
                }
                return result;
            }

            static bool Overlaps(float a, float b, List<Vector2> blocks) => blocks.Any(k => k.x < b && k.y > a);

            void Fill(Face face)
            {
                var program = Program(kind);
                if (program.Length == 0) return;
                var queue = new Queue<Vector2>(Subtract(-face.Half + .04f, face.Half - .04f, Blocks(face))
                    .Where(r => r.y - r.x >= .9f).OrderByDescending(r => r.y - r.x));
                while (queue.Count > 0 && budget > .5f)
                {
                    var range = queue.Dequeue();
                    if (!Choose(face, program, range.x, range.y, out var fit, out float start, out float length)) continue;
                    Place(fit, face, start, length);
                    budget -= length;
                    var before = new Vector2(range.x, start - 1.1f);
                    var after = new Vector2(start + length + 1.1f, range.y);
                    foreach (var rest in new[] { before, after }.OrderByDescending(r => r.y - r.x))
                        if (rest.y - rest.x >= .9f) queue.Enqueue(rest);
                }
            }

            bool Choose(Face face, Fit[] program, float a, float b, out Fit fit, out float start, out float length)
            {
                for (int k = 0; k < program.Length; k++)
                {
                    fit = program[(next + k) % program.Length];
                    if (fit == Fit.Fireplace && fireplace) continue;
                    var spec = Spec(fit);
                    float lo = a, hi = b;
                    if (spec.tall)
                    {
                        var clear = Subtract(a, b, face.Decor).OrderByDescending(r => r.y - r.x).FirstOrDefault();
                        lo = clear.x; hi = clear.y;
                    }
                    float available = hi - lo;
                    length = Mathf.Min(spec.max, available, Mathf.Max(budget, spec.min));
                    if (fit == Fit.Cubbies) length = Mathf.Floor(length / .35f + .001f) * .35f;
                    if (available < spec.min || length < spec.min - .001f) continue;
                    bool lowCorner = lo <= -face.Half + .3f, highCorner = hi >= face.Half - .3f;
                    if (highCorner && !lowCorner) start = hi - length;
                    else if (lowCorner) start = lo;
                    else if (available < length * 2f + 1.1f) start = (lo + hi - length) * .5f;
                    else start = lo;
                    next = next + k + 1;
                    return true;
                }
                fit = default; start = 0f; length = 0f;
                return false;
            }

            void Place(Fit fit, Face face, float start, float length)
            {
                var spec = Spec(fit);
                f = face;
                cx = start + length * .5f;
                into = fitted;
                var near = face.At(cx, 0f, 0f);
                var size = Abs(face.Right) * length + Abs(face.Inward) * spec.depth + Vector3.up * reach;
                var span = new Bounds(near + face.Inward * (spec.depth * .5f) + Vector3.up * (reach * .5f), size);
                spans.Add(span);
                placed.Add(span);
                notes.Add(Label(fit) + " " + length.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " m");
                if (spec.tall) face.UpperUsed.Add(new Vector2(start, start + length));
                switch (fit)
                {
                    case Fit.Counter: Counter(length, start); break;
                    case Fit.Wardrobe: Wardrobe(length); break;
                    case Fit.BookcaseTall: Bookcase(length, true); break;
                    case Fit.BookcaseLow: Bookcase(length, false); break;
                    case Fit.Console: Console(length, .82f, .42f, start); break;
                    case Fit.Dresser: Console(length, .9f, .5f, start); break;
                    case Fit.Sideboard: Console(length, .9f, .48f, start); break;
                    case Fit.Fireplace: Fireplace(start); fireplace = true; break;
                    case Fit.Cubbies: Cubbies(length, start); break;
                    case Fit.Vanity: Vanity(length, start); break;
                    case Fit.TowelRail: TowelRail(); break;
                    case Fit.ShoeBench: ShoeBench(length, start); break;
                    case Fit.Mailboxes: Mailboxes(length); break;
                    case Fit.Radiator: Radiator(length); break;
                    case Fit.Workbench: Workbench(length, start); break;
                    case Fit.Shelving: Shelving(length); break;
                    case Fit.Eaves: Eaves(length); break;
                    case Fit.ServiceCounter: ServiceCounter(length, start); break;
                    case Fit.Banquette: Banquette(length, start); break;
                    case Fit.Bed: Bed(length); break;
                    case Fit.Hedge: Hedge(length); break;
                    case Fit.Planter: Planter(length); break;
                    case Fit.RoofPlant: RoofPlant(length); break;
                }
            }

            static Vector3 Abs(Vector3 v) => new(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

            static string Label(Fit fit) => fit switch
            {
                Fit.Counter => "Kitchen counter",
                Fit.Wardrobe => "Fitted wardrobe",
                Fit.BookcaseTall => "Tall bookcase",
                Fit.BookcaseLow => "Low bookcase",
                Fit.Console => "Console cabinet",
                Fit.Fireplace => "Fireplace and hearth",
                Fit.Cubbies => "Toy cubbies",
                Fit.Dresser => "Drawer unit",
                Fit.Sideboard => "Sideboard",
                Fit.Vanity => "Vanity unit",
                Fit.TowelRail => "Towel rail",
                Fit.ShoeBench => "Shoe bench",
                Fit.Mailboxes => "Mailbox wall",
                Fit.Radiator => "Radiator",
                Fit.Workbench => "Workbench",
                Fit.Shelving => "Steel shelving",
                Fit.Eaves => "Eaves cupboards",
                Fit.ServiceCounter => "Service counter",
                Fit.Banquette => "Banquette",
                Fit.Bed => "Flowerbed",
                Fit.Hedge => "Hedge",
                Fit.Planter => "Raised planter",
                _ => "Roof plant"
            };

            Batch Upper(Face face)
            {
                string side = face.Inward.ToString("F0") + " " + Mathf.RoundToInt(Vector3.Dot(face.Origin, face.Inward) * 10f);
                if (!uppers.TryGetValue(side, out var batch)) uppers[side] = batch = new Batch();
                return batch;
            }

            bool UpperFree(float start, float length) => !Overlaps(start, start + length, f.Decor) && !Overlaps(start, start + length, f.UpperUsed);

            void ClaimUpper(float start, float length, string note = null)
            {
                f.UpperUsed.Add(new Vector2(start, start + length));
                if (note != null) notes.Add(note);
            }

            Vector3 At(float x, float y, float z) => f.At(cx + x, y, z);

            void Solid(Material m, float x, float y, float z, float sx, float sy, float sz, Batch b = null, bool back = true) =>
                (b ?? into).Box(m, At(x, y, z), f.Rotation, new Vector3(sx, sy, sz), tiles[m], null, true, back);

            Batch Loose => into == fitted ? small : into;

            void Paint(int swatch, float x, float y, float z, float sx, float sy, float sz, Batch b = null, bool bottom = true, bool back = true) =>
                (b ?? Loose).Box(pal, At(x, y, z), f.Rotation, new Vector3(sx, sy, sz), Vector2.one, Swatch(swatch), bottom, back);

            void Can(int swatch, float x, float y, float z, float radius, float height, Batch b = null, float upper = -1f, int sides = 10) =>
                (b ?? Loose).Cylinder(pal, Swatch(swatch), At(x, y, z), f.Rotation, radius, upper < 0f ? radius : upper, height, sides);

            void Ball(int swatch, float x, float y, float z, float rx, float ry, float rz, Batch b = null)
            {
                bool tiny = Mathf.Max(rx, Mathf.Max(ry, rz)) < .09f;
                (b ?? Loose).Blob(pal, Swatch(swatch), Vector2.one, At(x, y, z), new Vector3(rx, ry, rz), 4, tiny ? 5 : 7);
            }

            void Doors(float length, float y, float height, float z, float width, bool knobs)
            {
                int doors = Mathf.Max(1, Mathf.RoundToInt(length / width));
                float w = length / doors;
                for (int i = 1; i < doors; i++) Paint(DarkGrey, -length * .5f + i * w, y, z, .012f, height - .04f, .006f);
                for (int i = 0; i < doors; i++)
                {
                    float x = -length * .5f + (i + (doors == 1 ? .5f : i % 2 == 0 ? .85f : .15f)) * w;
                    if (knobs) Paint(Brass, x, y + height * .25f, z + .012f, .03f, .03f, .03f);
                    else Paint(Chrome, -length * .5f + (i + .5f) * w, y + height * .5f - .06f, z + .012f, .14f, .018f, .024f);
                }
            }

            void Counter(float length, float start)
            {
                const float D = .62f;
                counters++;
                bool sink = counters == 1, hob = counters == 2 || (kind == Kind.Flat && length >= 2.4f);
                Solid(dark, 0f, .05f, (D - .07f) * .5f, length, .1f, D - .07f);
                Solid(paint, 0f, .48f, (D - .03f) * .5f, length, .76f, D - .03f);
                Doors(length, .1f, .62f, D - .03f, .6f, false);
                Paint(DarkGrey, 0f, .73f, D - .028f, length - .02f, .01f, .006f);
                Solid(top, 0f, .9f, D * .5f, length + .02f, .04f, D);
                Solid(tile, 0f, 1.2f, .016f, length, .56f, .028f, back: false);
                float sinkX = float.NaN, hobX = float.NaN;
                if (sink && hob && length >= 2.4f) { sinkX = -length * .25f; hobX = length * .25f; }
                else if (sink) sinkX = 0f;
                else if (hob) hobX = 0f;
                if (!float.IsNaN(sinkX))
                {
                    Paint(Chrome, sinkX, .921f, D * .48f, .62f, .006f, .46f);
                    Paint(MidGrey, sinkX, .923f, D * .48f, .5f, .004f, .36f);
                    Can(Chrome, sinkX, .92f, .1f, .022f, .26f);
                    Paint(Chrome, sinkX, 1.17f, .17f, .03f, .03f, .16f);
                }
                if (!float.IsNaN(hobX))
                {
                    Paint(Black, hobX, .922f, D * .5f, .6f, .008f, .52f);
                    foreach (var (dx, dz, r) in new[] { (-.15f, -.12f, .09f), (.15f, -.12f, .07f), (-.15f, .12f, .07f), (.15f, .12f, .09f) })
                        Can(DarkGrey, hobX + dx, .926f, D * .5f + dz, r, .004f);
                }
                for (float x = -length * .5f + .3f; x < length * .5f - .3f; x += .5f)
                {
                    if (Mathf.Abs(x - sinkX) < .5f || Mathf.Abs(x - hobX) < .5f || !Chance(.55f)) continue;
                    switch (rng.Next(4))
                    {
                        case 0:
                            for (int j = 0; j < 3; j++)
                            {
                                float h = R(.12f, .2f), radius = R(.04f, .055f), jx = x + j * .11f - .11f;
                                Can(Pick(Glass, Terracotta, Cream, Sage, Butter, DuckEgg), jx, .92f, .17f, radius, h);
                                Can(Pick(DarkWood, Chrome, Brass), jx, .92f + h, .17f, radius * 1.05f, .02f);
                            }
                            break;
                        case 1: Solid(top, x, .93f, .14f, .38f, .02f, .26f); break;
                        case 2:
                            Can(Pick(Chrome, Terracotta, White), x, .92f, .13f, .06f, .16f);
                            for (int j = 0; j < 3; j++) Paint(Pick(DarkWood, Black, LightWood), x - .02f + j * .02f, 1.14f, .13f, .014f, .18f, .014f);
                            break;
                        default: Paint(Pick(Butter, Sage, Chrome, Rose), x, 1.02f, .18f, .34f, .2f, .26f); break;
                    }
                }
                if (!UpperFree(start, length)) return;
                ClaimUpper(start, length, float.IsNaN(hobX) ? "Wall cabinets" : "Wall cabinets and cooker hood");
                var up = Upper(f);
                const float Y = 1.48f, H = .72f, Depth = .34f;
                var segments = new List<Vector2> { new(-length * .5f, length * .5f) };
                if (!float.IsNaN(hobX)) segments = Subtract(-length * .5f, length * .5f, new List<Vector2> { new(hobX - .38f, hobX + .38f) });
                foreach (var segment in segments)
                {
                    float w = segment.y - segment.x;
                    if (w < .4f) continue;
                    float x = (segment.x + segment.y) * .5f;
                    Solid(paint, x, Y + H * .5f, Depth * .5f, w, H, Depth, up, false);
                    int doors = Mathf.Max(1, Mathf.RoundToInt(w / .5f));
                    for (int i = 1; i < doors; i++) Paint(DarkGrey, segment.x + i * w / doors, Y + H * .5f, Depth + .002f, .012f, H - .04f, .006f, up);
                    for (int i = 0; i < doors; i++) Paint(Chrome, segment.x + (i + .5f) * w / doors, Y + .07f, Depth + .012f, .14f, .018f, .024f, up);
                }
                if (float.IsNaN(hobX)) return;
                Paint(Chrome, hobX, 1.6f, .26f, .62f, .1f, .5f, up);
                Paint(Chrome, hobX, 2.05f, .13f, .26f, .8f, .24f, up, back: false);
            }

            void Wardrobe(float length)
            {
                const float D = .62f, H = 2.1f;
                Solid(dark, 0f, .05f, (D - .06f) * .5f, length, .1f, D - .06f);
                Solid(paint, 0f, .1f + (H - .16f) * .5f, (D - .02f) * .5f, length, H - .16f, D - .02f, back: false);
                Solid(wood, 0f, H - .03f, D * .5f, length + .04f, .06f, D + .02f, back: false);
                Doors(length, .12f, H - .2f, D - .02f, .5f, true);
                int cases = length > 1.6f ? 2 : 1;
                for (int i = 0; i < cases; i++)
                    Paint(Pick(Leather, Fabrics + 1, Fabrics + 3, DarkGrey), (i - (cases - 1) * .5f) * .7f, H + .1f, D * .45f, .6f, .2f, .42f);
            }

            void Bookcase(float length, bool tall)
            {
                const float D = .36f;
                float H = tall ? 2f : 1f;
                int bays = Mathf.Max(1, Mathf.RoundToInt(length / .85f));
                float bay = length / bays;
                Solid(wood, 0f, H * .5f, .01f, length, H, .02f, back: false);
                for (int i = 0; i <= bays; i++)
                    Solid(wood, Mathf.Clamp(-length * .5f + i * bay, -length * .5f + .015f, length * .5f - .015f), H * .5f, D * .5f, .03f, H, D, back: false);
                float[] levels = tall ? new[] { .03f, .42f, .81f, 1.2f, 1.59f, H - .02f } : new[] { .03f, .49f, H - .02f };
                for (int k = 0; k < levels.Length; k++)
                    Solid(wood, 0f, levels[k], D * .5f, length, k == 0 || k == levels.Length - 1 ? .05f : .025f, D, back: false);
                for (int i = 0; i < bays; i++)
                    for (int k = 0; k < levels.Length - 1; k++)
                        BookRow(-length * .5f + i * bay + .035f, -length * .5f + (i + 1) * bay - .035f, levels[k] + .0125f, levels[k + 1] - levels[k] - .05f);
                if (!tall) Decor(length - .2f, H + .005f, D * .5f);
            }

            void BookRow(float a, float b, float y, float clear)
            {
                float x = a;
                while (x < b - .03f)
                {
                    double roll = rng.NextDouble();
                    if (roll < .1) { x += R(.06f, .16f); continue; }
                    if (roll < .17 && b - x > .3f)
                    {
                        float w = R(.18f, .24f), d = R(.16f, .22f), yy = y;
                        int n = rng.Next(2, 5);
                        for (int k = 0; k < n; k++)
                        {
                            float h = R(.03f, .05f);
                            Paint(Books + rng.Next(8), x + w * .5f, yy + h * .5f, .03f + d * .5f, w, h, d, bottom: false, back: false);
                            yy += h;
                        }
                        x += w + .02f;
                        continue;
                    }
                    if (roll < .21 && b - x > .12f)
                    {
                        Can(Pick(Candle, Terracotta, Brass, StoneLight), x + .05f, y, .14f, .04f, Mathf.Min(R(.1f, .18f), clear));
                        x += .12f;
                        continue;
                    }
                    float bw = R(.025f, .055f), bh = Mathf.Min(R(.18f, .3f), clear), bd = R(.19f, .25f);
                    if (x + bw > b) break;
                    Paint(Books + rng.Next(8), x + bw * .5f, y + bh * .5f, .03f + bd * .5f, bw, bh, bd, bottom: false, back: false);
                    x += bw + .003f;
                }
            }

            void Decor(float width, float y, float z, Batch b = null)
            {
                for (float x = -width * .5f + .12f; x < width * .5f - .12f; x += R(.3f, .5f))
                {
                    switch (rng.Next(4))
                    {
                        case 0:
                            Paint(frameTone, x, y + .13f, z, .2f, .26f, .025f, b);
                            Paint(Pick(Sage, DuckEgg, Rose, Butter, Glass), x, y + .13f, z + .014f, .15f, .2f, .004f, b);
                            break;
                        case 1:
                            Can(Candle, x, y, z, .03f, R(.12f, .24f), b);
                            Can(Candle, x + .07f, y, z, .03f, R(.08f, .16f), b);
                            break;
                        case 2:
                            Can(Pick(StoneLight, White, Terracotta), x, y, z, .06f, .16f, b, .03f);
                            Ball(Pick(Brass, StoneLight, Sage), x, y + .22f, z, .055f, .055f, .055f, b);
                            break;
                    }
                }
            }

            void Console(float length, float height, float depth, float start)
            {
                Solid(dark, 0f, .04f, (depth - .04f) * .5f, length, .08f, depth - .04f);
                Solid(kind is Kind.Study or Kind.Library or Kind.Dining ? wood : paint, 0f, (height - .03f) * .5f + .04f, depth * .5f - .01f, length, height - .11f, depth - .02f, back: false);
                Solid(wood, 0f, height - .015f, depth * .5f, length + .03f, .03f, depth + .01f, back: false);
                if (height > .85f)
                    for (int row = 1; row < 3; row++) Paint(DarkGrey, 0f, .08f + row * (height - .12f) / 3f, depth - .01f, length - .02f, .01f, .006f);
                Doors(length, .1f, height - .14f, depth - .02f, .55f, true);
                Decor(length - .1f, height, depth * .45f);
                if (!UpperFree(start, length)) return;
                ClaimUpper(start, length, kind is Kind.Hall or Kind.Lobby or Kind.Living or Kind.Bedroom or Kind.Flat ? "Wall mirror" : "Picture group");
                var up = Upper(f);
                float w = Mathf.Min(length * .6f, 1f);
                if (kind is Kind.Hall or Kind.Lobby or Kind.Living or Kind.Bedroom or Kind.Flat)
                {
                    Paint(Brass, 0f, height + .8f, .02f, w, .8f, .03f, up, back: false);
                    Paint(Glass, 0f, height + .8f, .037f, w - .08f, .72f, .004f, up);
                }
                else Frames(0f, height + .85f, up);
            }

            void Frames(float x, float y, Batch b)
            {
                var sizes = new[] { (.42f, .52f, -.42f, 0f), (.32f, .32f, .08f, .12f), (.32f, .42f, .48f, -.08f) };
                foreach (var (w, h, dx, dy) in sizes)
                {
                    Paint(frameTone, x + dx, y + dy, .02f, w, h, .03f, b, back: false);
                    Paint(Paper, x + dx, y + dy, .036f, w - .06f, h - .06f, .003f, b);
                    Paint(Pick(Sage, DuckEgg, Rose, Butter, Glass, Leaves + 2, Flowers + 2), x + dx, y + dy, .038f, w - .14f, h - .14f, .002f, b);
                }
            }

            void Fireplace(float start)
            {
                const float D = .45f;
                var chimney = Surface(Surfaces.Kind.Plaster, Surfaces.Hex(0xEFE6D6));
                Solid(chimney, 0f, 1.2f, D * .5f, 1.9f, 2.4f, D, back: false);
                Paint(Firebox, 0f, .42f, D + .004f, .74f, .66f, .008f);
                foreach (float side in new[] { -1f, 1f }) Paint(Hearth, side * .53f, .56f, D + .04f, .2f, 1.12f, .08f);
                Solid(wood, 0f, 1.15f, D + .1f, 1.5f, .07f, .28f);
                Paint(Black, 0f, .1f, D + .08f, .5f, .04f, .16f);
                foreach (float y in new[] { .16f, .25f })
                    Loose.Cylinder(pal, Swatch(Bark), At(-.22f, y, D + .08f + (y - .16f) * .3f), f.Rotation * Quaternion.Euler(0f, 0f, -90f), .05f, .05f, .44f, 8);
                Paint(Ember, 0f, .13f, D + .07f, .3f, .03f, .1f);
                Paint(Hearth, 0f, .02f, D + .26f, 1.7f, .04f, .52f);
                Decor(1.3f, 1.185f, D + .1f);
                if (!UpperFree(start, 1.9f)) return;
                notes.Add("Overmantel mirror");
                var up = Upper(f);
                Paint(Brass, 0f, 1.78f, D + .02f, .95f, .75f, .03f, up, back: false);
                Paint(Glass, 0f, 1.78f, D + .037f, .87f, .67f, .004f, up);
            }

            void Cubbies(float length, float start)
            {
                const float D = .4f, H = 1.05f, Cell = .35f;
                int columns = Mathf.Max(1, Mathf.RoundToInt(length / Cell));
                float w = length / columns;
                Solid(paint, 0f, H * .5f, .01f, length, H, .02f, back: false);
                for (int i = 0; i <= columns; i++)
                    Solid(paint, Mathf.Clamp(-length * .5f + i * w, -length * .5f + .0125f, length * .5f - .0125f), H * .5f, D * .5f, .025f, H, D, back: false);
                for (int r = 0; r <= 3; r++)
                    Solid(paint, 0f, Mathf.Clamp(r * Cell, .0125f, H - .0125f), D * .5f, length, .025f, D, back: false);
                for (int i = 0; i < columns; i++)
                    for (int r = 0; r < 3; r++)
                    {
                        if (!Chance(.6f)) continue;
                        float x = -length * .5f + (i + .5f) * w, y = r * Cell + .025f + .135f;
                        Paint(Pick(Toys, Toys + 1, Toys + 2, Toys + 3, Toys + 4, Toys + 5, Fabrics + 4), x, y, D * .55f, w - .05f, .27f, D - .06f);
                        Paint(DarkGrey, x, y + .07f, D - .028f, .1f, .03f, .004f);
                    }
                for (float x = -length * .5f + .15f; x < length * .5f - .15f; x += R(.3f, .45f))
                {
                    switch (rng.Next(3))
                    {
                        case 0:
                            int n = rng.Next(2, 5);
                            for (int k = 0; k < n; k++)
                                Loose.Box(pal, At(x + R(-.01f, .01f), H + .045f + k * .09f, D * .5f), f.Rotation * Quaternion.Euler(0f, R(-12f, 12f), 0f),
                                    Vector3.one * .085f, Vector2.one, Swatch(Toys + rng.Next(6)));
                            break;
                        case 1:
                            int body = Toys + rng.Next(6);
                            Paint(body, x, H + .06f, D * .5f, .2f, .07f, .1f);
                            Paint(body, x - .02f, H + .11f, D * .5f, .1f, .05f, .09f);
                            foreach (float wx in new[] { -.07f, .07f })
                                foreach (float wz in new[] { -.055f, .055f })
                                    Loose.Cylinder(pal, Swatch(Black), At(x + wx, H + .025f, D * .5f + wz), f.Rotation * Quaternion.Euler(90f, 0f, 0f), .025f, .025f, .02f, 8);
                            break;
                        default:
                            Can(Toys + 1, x, H, D * .5f, .015f, .2f);
                            for (int k = 0; k < 4; k++) Can(Toys + (k + 2) % 6, x, H + k * .04f, D * .5f, .07f - k * .012f, .035f);
                            break;
                    }
                }
                if (!UpperFree(start, length)) return;
                ClaimUpper(start, length, "Bunting");
                var up = Upper(f);
                Paint(White, 0f, 2.18f, .03f, length, .008f, .008f, up);
                for (float x = -length * .5f + .12f; x < length * .5f - .1f; x += .24f)
                    up.Box(pal, At(x, 2.1f, .03f), f.Rotation * Quaternion.Euler(0f, 0f, 45f), new Vector3(.1f, .1f, .005f), Vector2.one, Swatch(Toys + rng.Next(6)));
            }

            void Vanity(float length, float start)
            {
                const float D = .5f;
                Solid(dark, 0f, .04f, (D - .06f) * .5f, length, .08f, D - .06f);
                Solid(paint, 0f, .45f, (D - .02f) * .5f, length, .74f, D - .02f, back: false);
                Doors(length, .1f, .66f, D - .02f, .45f, false);
                Paint(StoneLight, 0f, .835f, D * .5f, length + .02f, .04f, D);
                Can(White, 0f, .855f, .27f, .21f, .1f, null, .23f, 14);
                Can(LightGrey, 0f, .956f, .27f, .17f, .004f, null, -1f, 14);
                Can(Chrome, 0f, .855f, .06f, .018f, .22f);
                Paint(Chrome, 0f, 1.06f, .11f, .025f, .025f, .12f);
                if (!UpperFree(start, length)) return;
                ClaimUpper(start, length, "Mirror and shelf");
                var up = Upper(f);
                float w = Mathf.Min(length, .9f);
                Paint(Chrome, 0f, 1.62f, .02f, w, .8f, .025f, up, back: false);
                Paint(Glass, 0f, 1.62f, .034f, w - .06f, .74f, .004f, up);
                Paint(White, 0f, 1.12f, .07f, .6f, .02f, .12f, up);
                for (float x = -.24f; x <= .24f; x += .12f) Can(Pick(DuckEgg, Rose, White, Sage, Butter), x, 1.13f, .07f, .025f, R(.08f, .15f), up);
            }

            void TowelRail()
            {
                foreach (float side in new[] { -.27f, .27f }) Paint(Chrome, side, .65f, .07f, .025f, 1.1f, .025f);
                for (int k = 0; k < 6; k++) Paint(Chrome, 0f, .25f + k * .17f, .07f, .54f, .02f, .02f);
                int towel = Pick(Fabrics, Fabrics + 1, Fabrics + 2, White, DuckEgg);
                Paint(towel, 0f, .95f, .1f, .44f, .5f, .03f);
                Paint(towel, 0f, 1.21f, .085f, .44f, .04f, .07f);
            }

            void ShoeBench(float length, float start)
            {
                const float D = .4f;
                Solid(wood, 0f, .44f, D * .5f, length, .05f, D);
                foreach (float side in new[] { -1f, 1f }) Solid(wood, side * (length * .5f - .025f), .21f, D * .5f, .05f, .42f, D);
                Solid(wood, 0f, .12f, D * .5f, length - .05f, .025f, D - .02f);
                Paint(fabric, 0f, .5f, D * .5f, length - .08f, .07f, D - .04f);
                for (float x = -length * .5f + .15f; x < length * .5f - .1f; x += .3f)
                {
                    if (!Chance(.7f)) continue;
                    int shoe = Pick(Black, Leather, Fabrics + 1, Fabrics, White, DarkGrey);
                    foreach (float side in new[] { -.055f, .055f }) Paint(shoe, x + side, .17f, .22f, .09f, .09f, .27f);
                }
                if (!UpperFree(start, length)) return;
                ClaimUpper(start, length, "Coat rail with coats");
                var up = Upper(f);
                Paint(DarkWood, 0f, 1.72f, .02f, length, .08f, .03f, up, back: false);
                for (float x = -length * .5f + .15f; x < length * .5f - .1f; x += .3f)
                {
                    Paint(Brass, x, 1.7f, .06f, .02f, .04f, .06f, up);
                    if (!Chance(.65f)) continue;
                    float h = R(.65f, .8f);
                    Paint(Pick(Fabrics, Fabrics + 1, Fabrics + 2, Fabrics + 3, Fabrics + 4, Leather), x, 1.68f - h * .5f, .1f, R(.3f, .38f), h, .14f, up);
                }
            }

            void Mailboxes(float length)
            {
                const float D = .32f;
                Solid(dark, 0f, .15f, D * .5f, length, .3f, D);
                Solid(wood, 0f, .95f, D * .5f, length, 1.3f, D, back: false);
                int columns = Mathf.Max(1, Mathf.RoundToInt(length / .3f));
                float w = length / columns;
                for (int i = 0; i < columns; i++)
                    for (int r = 0; r < 4; r++)
                    {
                        float x = -length * .5f + (i + .5f) * w, y = .38f + r * .3f + .14f;
                        Paint(Chrome, x, y, D + .004f, w - .04f, .26f, .008f);
                        Paint(Black, x, y + .06f, D + .009f, w * .5f, .015f, .004f);
                        Paint(Paper, x, y - .06f, D + .009f, .08f, .03f, .004f);
                    }
            }

            void Radiator(float length)
            {
                Paint(White, 0f, .45f, .075f, length, .6f, .09f, back: false);
                for (float x = -length * .5f + .04f; x < length * .5f - .02f; x += .06f) Paint(LightGrey, x, .45f, .122f, .012f, .56f, .006f);
                foreach (float side in new[] { -1f, 1f })
                {
                    Can(Chrome, side * (length * .5f + .03f), .02f, .07f, .014f, .16f);
                    Can(White, side * (length * .5f + .03f), .16f, .07f, .025f, .05f);
                }
            }

            void Workbench(float length, float start)
            {
                const float D = .7f;
                Solid(wood, 0f, .89f, D * .5f, length, .06f, D);
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float z in new[] { .06f, D - .06f })
                        Solid(dark, sx * (length * .5f - .06f), .43f, z, .07f, .86f, .07f);
                Solid(wood, 0f, .2f, D * .5f, length - .1f, .03f, D - .08f);
                Paint(DuckEgg, length * .5f - .2f, .98f, D - .08f, .18f, .12f, .14f);
                Paint(Toys, -length * .25f, 1.02f, .25f, .45f, .2f, .22f);
                Paint(Black, -length * .25f, 1.14f, .25f, .25f, .03f, .03f);
                for (float x = -length * .5f + .2f; x < length * .5f - .2f; x += R(.22f, .32f))
                {
                    if (!Chance(.7f)) continue;
                    int colour = Pick(Toys, Toys + 1, Toys + 3, White, Butter, Sage);
                    Can(colour, x, .215f, R(.2f, .5f), .085f, .19f);
                    Can(MidGrey, x, .405f, .35f, .087f, .012f);
                }
                if (!UpperFree(start, length)) return;
                ClaimUpper(start, length, "Pegboard with tools");
                var up = Upper(f);
                Solid(pegboard, 0f, 1.6f, .012f, length, .95f, .024f, up, false);
                for (float x = -length * .5f + .15f; x < length * .5f - .12f; x += R(.16f, .26f))
                {
                    switch (rng.Next(5))
                    {
                        case 0:
                            Paint(LightWood, x, 1.5f, .035f, .03f, .3f, .02f, up);
                            Paint(DarkGrey, x, 1.66f, .035f, .12f, .04f, .03f, up);
                            break;
                        case 1: Paint(Chrome, x, 1.55f, .03f, .025f, .22f, .01f, up); break;
                        case 2:
                            Paint(LightGrey, x, 1.6f, .03f, .1f, .4f, .005f, up);
                            Paint(Toys, x, 1.84f, .03f, .1f, .08f, .02f, up);
                            break;
                        case 3:
                            Paint(Toys + rng.Next(6), x, 1.68f, .04f, .03f, .1f, .03f, up);
                            Paint(Chrome, x, 1.56f, .04f, .008f, .14f, .008f, up);
                            break;
                        default: Paint(Butter, x, 1.75f, .045f, .07f, .07f, .04f, up); break;
                    }
                }
            }

            void Shelving(float length)
            {
                const float D = .5f, H = 1.9f;
                int columns = Mathf.Max(1, Mathf.RoundToInt(length / 1f));
                for (int i = 0; i <= columns; i++)
                {
                    float x = Mathf.Clamp(-length * .5f + i * length / columns, -length * .5f + .02f, length * .5f - .02f);
                    foreach (float z in new[] { .03f, D - .03f }) Paint(MidGrey, x, H * .5f, z, .035f, H, .035f);
                }
                float[] levels = { .12f, .57f, 1.02f, 1.47f, H - .02f };
                foreach (float y in levels) Paint(LightGrey, 0f, y, D * .5f, length, .025f, D);
                for (int k = 0; k < levels.Length - 1; k++)
                {
                    float y = levels[k] + .0125f;
                    for (float x = -length * .5f + .1f; x < length * .5f - .1f;)
                    {
                        switch (rng.Next(5))
                        {
                            case 0:
                                if (x + .4f > length * .5f) { x = length; break; }
                                Paint(Pick(Fabrics + 1, Fabrics + 2, StoneMid, Toys + 1, Fabrics + 4), x + .2f, y + .13f, D * .5f, .38f, .26f, .34f);
                                Paint(DarkGrey, x + .2f, y + .27f, D * .5f, .4f, .025f, .36f);
                                x += .44f;
                                break;
                            case 1:
                                for (int j = 0; j < 3 && x < length * .5f - .1f; j++, x += .19f)
                                {
                                    Can(Pick(Toys, Toys + 1, Toys + 2, Toys + 3, White, Sage), x + .09f, y, R(.15f, .35f), .08f, .17f);
                                    Can(MidGrey, x + .09f, y + .17f, .25f, .082f, .012f);
                                }
                                break;
                            case 2:
                                Can(Pick(Black, Fabrics + 4, Toys + 4), x + .13f, y, .25f, .12f, .08f, null, .12f, 12);
                                x += .3f;
                                break;
                            case 3:
                                if (x + .22f > length * .5f) { x = length; break; }
                                Paint(Pick(Toys, Toys + 3), x + .1f, y + .15f, .26f, .2f, .3f, .3f);
                                x += .26f;
                                break;
                            default: x += R(.15f, .35f); break;
                        }
                    }
                }
            }

            void Eaves(float length)
            {
                const float D = .55f, H = .75f;
                Solid(paint, 0f, H * .5f, D * .5f, length, H, D, back: false);
                Solid(wood, 0f, H + .015f, D * .5f, length + .02f, .03f, D + .01f, back: false);
                Doors(length, .06f, H - .1f, D, .6f, true);
                for (float x = -length * .5f + .4f; x < length * .5f - .4f; x += R(.8f, 1.2f))
                {
                    if (Chance(.5f))
                        for (int k = 0; k < 3; k++) Paint(Fabrics + rng.Next(6), x, H + .045f + k * .06f, D * .5f, .5f, .06f, .38f);
                    else Paint(Pick(Leather, DarkGrey, Fabrics + 1), x, H + .13f, D * .5f, .6f, .2f, .4f);
                }
            }

            void ServiceCounter(float length, float start)
            {
                const float D = .7f;
                Solid(dark, 0f, .5f, D * .5f - .01f, length, 1f, D - .02f, back: false);
                for (float x = -length * .5f + .06f; x < length * .5f; x += .12f) Paint(DarkWood, x, .5f, D - .016f, .03f, .9f, .008f);
                Solid(top, 0f, 1.025f, D * .5f, length + .04f, .05f, D + .02f);
                float till = -length * .3f, machine = length * .15f;
                Paint(Black, till, 1.13f, .3f, .34f, .16f, .28f);
                Paint(DarkGrey, till, 1.28f, .22f, .26f, .16f, .03f);
                Paint(Chrome, machine, 1.27f, .25f, .55f, .45f, .4f);
                Paint(Black, machine, 1.2f, .46f, .4f, .12f, .02f);
                foreach (float side in new[] { -.12f, .12f }) Can(Chrome, machine + side, 1.15f, .5f, .03f, .08f);
                Can(Glass, length * .38f, 1.05f, .5f, .06f, .16f);
                if (!UpperFree(start, length)) return;
                ClaimUpper(start, length, "Menu board");
                var up = Upper(f);
                float w = Mathf.Min(length, 2.2f);
                Solid(wood, 0f, 1.9f, .015f, w, .95f, .03f, up, false);
                Paint(Slate, 0f, 1.9f, .033f, w - .08f, .87f, .004f, up);
                foreach (float column in new[] { -.25f, .25f })
                    for (int k = 0; k < 5; k++)
                        Paint(Paper, column * w + R(-.04f, .04f), 2.22f - k * .14f, .036f, R(.25f, w * .35f), .025f, .002f, up);
            }

            void Banquette(float length, float start)
            {
                const float D = .6f;
                Solid(dark, 0f, .19f, (D - .04f) * .5f, length, .38f, D - .04f);
                Paint(fabric, 0f, .43f, D * .5f, length, .1f, D - .04f);
                Paint(fabric, 0f, .73f, .09f, length, .5f, .14f, back: false);
                for (float x = -length * .5f + .5f; x < length * .5f - .2f; x += .5f) Paint(DarkGrey, x, .73f, .162f, .012f, .46f, .006f);
                if (!UpperFree(start, length)) return;
                ClaimUpper(start, length, "Picture groups");
                var up = Upper(f);
                for (float x = -length * .5f + .7f; x < length * .5f - .5f; x += 1.4f) Frames(x, 1.65f, up);
            }

            void Bed(float length)
            {
                const float D = .7f;
                Solid(dark, 0f, .13f, D - .025f, length, .26f, .05f);
                foreach (float side in new[] { -1f, 1f }) Solid(dark, side * (length * .5f - .025f), .13f, D * .5f, .05f, .26f, D);
                Paint(Soil, 0f, .11f, D * .5f, length - .06f, .22f, D - .04f, bottom: false, back: false);
                for (float x = -length * .5f + .25f; x < length * .5f - .2f; x += R(.4f, .55f))
                {
                    if (Chance(.45f))
                    {
                        Paint(Leaves + 1, x, .52f, .2f, .025f, .6f, .025f, bottom: false);
                        int bloom = Flowers + rng.Next(6);
                        for (int k = 0; k < 2; k++) Ball(bloom, x + R(-.03f, .03f), .7f + k * .13f, .2f, .07f, .08f, .07f);
                    }
                    else Ball(Leaves + rng.Next(5), x, .42f, .22f, R(.2f, .28f), R(.2f, .3f), .19f);
                    Ball(Leaves + rng.Next(3), x + .14f, .3f, .5f, .13f, .1f, .13f);
                    int flower = Flowers + rng.Next(6);
                    for (int k = 0; k < 2; k++) Ball(flower, x + .14f + R(-.1f, .1f), .38f + R(0f, .05f), .5f + R(-.09f, .09f), .065f, .05f, .065f);
                }
            }

            void Hedge(float length)
            {
                const float D = .75f;
                var grain = tiles[hedge];
                Solid(hedge, 0f, .58f, D * .48f, length, 1.16f, D - .1f, back: false);
                for (float x = -length * .5f + .25f; x <= length * .5f - .2f; x += R(.55f, .75f))
                    small.Blob(hedge, null, grain, At(x, 1.12f, D * .48f), new Vector3(R(.36f, .46f), R(.16f, .24f), (D - .1f) * .55f));
                for (float x = -length * .5f + .4f; x <= length * .5f - .4f; x += R(1f, 1.4f))
                    small.Blob(hedge, null, grain, At(x, R(.3f, .9f), D - .1f), new Vector3(R(.2f, .3f), R(.18f, .3f), .12f));
                foreach (float side in new[] { -1f, 1f })
                    small.Blob(hedge, null, grain, At(side * length * .5f, .6f, D * .48f), new Vector3(.2f, .55f, (D - .1f) * .5f));
                Paint(Bark, 0f, .008f, D * .5f, length + .1f, .016f, D + .1f, bottom: false);
            }

            void Planter(float length)
            {
                const float D = .65f;
                Solid(wood, 0f, .26f, D * .5f, length, .52f, D - .02f, back: false);
                foreach (float y in new[] { .175f, .35f }) Paint(DarkWood, 0f, y, D - .008f, length, .01f, .006f);
                Paint(Soil, 0f, .53f, D * .5f, length - .06f, .02f, D - .1f, bottom: false);
                bool topiary = Chance(.5f);
                for (float x = -length * .5f + .3f; x < length * .5f - .2f; x += .6f)
                {
                    if (topiary)
                    {
                        Can(Bark, x, .54f, D * .5f, .025f, .25f);
                        Ball(Leaves + 1, x, .97f, D * .5f, .2f, .2f, .2f);
                    }
                    else
                        for (int k = 0; k < 4; k++)
                        {
                            float sx = x + R(-.15f, .15f), sz = D * .5f + R(-.12f, .12f);
                            Paint(Leaves + 4, sx, .72f, sz, .015f, .36f, .015f, bottom: false);
                            Ball(Flowers + 3, sx, .9f, sz, .03f, .07f, .03f);
                        }
                }
            }

            void RoofPlant(float length)
            {
                const float D = .95f;
                for (float x = -length * .5f + .1f; x < length * .5f - .3f;)
                {
                    switch (rng.Next(4))
                    {
                        case 0:
                            if (x + 1.05f > length * .5f) { x = length; break; }
                            Paint(LightGrey, x + .5f, .38f, D * .5f, 1f, .7f, .65f);
                            Can(DarkGrey, x + .5f, .73f, D * .5f, .26f, .01f, null, -1f, 14);
                            Paint(MidGrey, x + .5f, .02f, D * .5f, 1.04f, .04f, .7f);
                            x += 1.2f;
                            break;
                        case 1:
                            Can(Chrome, x + .15f, 0f, D * .45f, .12f, .7f);
                            Can(Chrome, x + .15f, .7f, D * .45f, .18f, .06f, null, .05f);
                            x += .5f;
                            break;
                        case 2:
                            if (x + .95f > length * .5f) { x = length; break; }
                            Can(Pick(StoneMid, DuckEgg, LightGrey), x + .45f, 0f, D * .5f, .42f, 1.2f, null, -1f, 14);
                            Can(DarkGrey, x + .45f, 1.2f, D * .5f, .44f, .05f, null, -1f, 14);
                            x += 1.05f;
                            break;
                        default:
                            if (x + .95f > length * .5f) { x = length; break; }
                            Paint(Brick, x + .45f, .6f, D * .5f, .9f, 1.2f, .6f);
                            foreach (float side in new[] { -.2f, .2f }) Can(Terracotta, x + .45f + side, 1.2f, D * .5f, .1f, .3f, null, .08f);
                            x += 1.1f;
                            break;
                    }
                }
            }

            void Radiators()
            {
                foreach (var face in faces)
                {
                    if (face.Windows.Count == 0) continue;
                    var free = Subtract(-face.Half + .04f, face.Half - .04f, Blocks(face));
                    foreach (float u in face.Windows)
                    {
                        if (!free.Any(r => r.x <= u - .55f && r.y >= u + .55f)) continue;
                        f = face; cx = u; into = fitted;
                        var spec = Spec(Fit.Radiator);
                        var span = new Bounds(face.At(u, reach * .5f, spec.depth * .5f), Abs(face.Right) * spec.max + Abs(face.Inward) * spec.depth + Vector3.up * reach);
                        spans.Add(span);
                        placed.Add(span);
                        Radiator(spec.max);
                        notes.Add("Radiator under window");
                    }
                }
            }

            void Gallery()
            {
                int clusters = 0;
                foreach (var face in faces)
                {
                    if (clusters >= 2) break;
                    var free = Subtract(-face.Half + .3f, face.Half - .3f, face.Decor.Concat(face.UpperUsed).ToList())
                        .Where(r => r.y - r.x >= 1.6f).OrderByDescending(r => r.y - r.x).ToList();
                    if (free.Count == 0) continue;
                    f = face; cx = (free[0].x + free[0].y) * .5f;
                    ClaimUpper(cx - .8f, 1.6f, "Picture group");
                    Frames(0f, 1.72f, Upper(face));
                    clusters++;
                }
            }

            void Finish()
            {
                bool planks = !room.Outdoor && kind is not (Kind.Kitchen or Kind.Bath or Kind.Garage) && Trimmed(room.Name) is not ("Workshop" or "Storeroom" or "Walkway");
                if (!room.Outdoor)
                    foreach (var face in faces)
                    {
                        f = face; cx = 0f; into = finish;
                        Solid(skirting, 0f, .06f, .052f, face.Width - .02f, .12f, .025f, back: false);
                        if (kind == Kind.Bath) Solid(tile, 0f, .545f, .05f, face.Width - .04f, 1.09f, .02f, back: false);
                    }
                if (!room.Outdoor && faces.Count > 0) notes.Add(kind == Kind.Bath ? "Skirting and tiled dado" : "Skirting boards");
                if (planks) { Margin(); notes.Add("Stained floor border"); }
                int rugs = Rugs();
                if (rugs > 0) notes.Add("Rug border and fringe x" + rugs);
                int doors = Thresholds();
                if (doors > 0) notes.Add("Door thresholds x" + doors);
                if (kind == Kind.Garage || Trimmed(room.Name) == "Workshop") { Hazard(); notes.Add("Floor safety lines and oil stains"); }
                if (room.Outdoor)
                {
                    if (FloorIsLawn()) { Stripes(); notes.Add("Mown lawn stripes"); }
                    else
                    {
                        Joints(kind == Kind.Balcony ? .15f : kind == Kind.Roof ? 1.2f : .9f, kind == Kind.Balcony ? DarkGrey : StoneMid);
                        notes.Add(kind == Kind.Balcony ? "Steel grating" : "Paving joints");
                    }
                    int stones = Stones();
                    if (stones > 0) notes.Add("Stepping stones x" + stones);
                }
            }

            bool FloorIsLawn() => Trimmed(room.Name) is "Garden" or "Orchard" or "PoolLawn" or "Yard" or "RoofGarden";

            void Flat(Batch b, int swatch, Vector3 centre, Vector3 size, float yaw = 0f) =>
                b.Box(pal, centre, Quaternion.Euler(0f, yaw, 0f), size, Vector2.one, Swatch(swatch), false);

            List<Rect> Holes(float grow)
            {
                var holes = new List<Rect>();
                foreach (var stair in site.Layout.Stairs)
                    if (stair.Lower == room.Name || stair.Upper == room.Name)
                        holes.Add(Rect.MinMaxRect(stair.MinX - grow, stair.MinZ - grow, stair.MaxX + grow, stair.MaxZ + grow));
                return holes;
            }

            void Strip(Batch b, Material material, int swatch, bool alongX, float fixedAt, float from, float to, float width, float y, float thickness)
            {
                var holes = Holes(.3f);
                var blocks = holes.Where(h => alongX ? fixedAt + width * .5f > h.yMin && fixedAt - width * .5f < h.yMax : fixedAt + width * .5f > h.xMin && fixedAt - width * .5f < h.xMax)
                    .Select(h => alongX ? new Vector2(h.xMin, h.xMax) : new Vector2(h.yMin, h.yMax)).ToList();
                foreach (var piece in Subtract(from, to, blocks))
                {
                    float length = piece.y - piece.x;
                    if (length < .1f) continue;
                    float mid = (piece.x + piece.y) * .5f;
                    var centre = alongX ? new Vector3(mid, room.FloorY + y, fixedAt) : new Vector3(fixedAt, room.FloorY + y, mid);
                    var size = alongX ? new Vector3(length, thickness, width) : new Vector3(width, thickness, length);
                    if (material) b.Box(material, centre, Quaternion.identity, size, tiles[material], null, false);
                    else b.Box(pal, centre, Quaternion.identity, size, Vector2.one, Swatch(swatch), false);
                }
            }

            void Margin()
            {
                var colour = Trimmed(room.Name) switch
                {
                    "Study" or "Library" => Surfaces.Hex(0xB68E6B),
                    "Shed" or "Attic" => Surfaces.Hex(0xA1805F),
                    _ => Surfaces.Hex(0xCBA37B)
                };
                var band = Surface(Surfaces.Kind.Planks, Color.Lerp(colour, Surfaces.Hex(0x4A3322), .32f));
                const float Inset = .1f, Width = .3f;
                Strip(finish, band, 0, true, room.MinZ + Inset + Width * .5f, room.MinX + Inset, room.MaxX - Inset, Width, .003f, .006f);
                Strip(finish, band, 0, true, room.MaxZ - Inset - Width * .5f, room.MinX + Inset, room.MaxX - Inset, Width, .003f, .006f);
                Strip(finish, band, 0, false, room.MinX + Inset + Width * .5f, room.MinZ + Inset + Width, room.MaxZ - Inset - Width, Width, .003f, .006f);
                Strip(finish, band, 0, false, room.MaxX - Inset - Width * .5f, room.MinZ + Inset + Width, room.MaxZ - Inset - Width, Width, .003f, .006f);
            }

            int Rugs()
            {
                int border = Pick(DarkWood, Brick, Slate, Fabrics + 1, Fabrics + 2, Leather), fringe = Pick(Cream, Paper, Candle), count = 0;
                foreach (var rug in site.Rugs)
                {
                    if (!room.Contains(rug.center.x, rug.center.z) || Mathf.Abs(rug.min.y - room.FloorY) > .1f) continue;
                    count++;
                    float y = rug.max.y + .001f, w = .09f, inset = .07f;
                    Flat(finish, border, new Vector3(rug.center.x, y, rug.min.z + inset + w * .5f), new Vector3(rug.size.x - inset * 2f, .002f, w));
                    Flat(finish, border, new Vector3(rug.center.x, y, rug.max.z - inset - w * .5f), new Vector3(rug.size.x - inset * 2f, .002f, w));
                    Flat(finish, border, new Vector3(rug.min.x + inset + w * .5f, y, rug.center.z), new Vector3(w, .002f, rug.size.z - inset * 2f - w * 2f));
                    Flat(finish, border, new Vector3(rug.max.x - inset - w * .5f, y, rug.center.z), new Vector3(w, .002f, rug.size.z - inset * 2f - w * 2f));
                    bool shortAlongX = rug.size.x < rug.size.z;
                    float span = shortAlongX ? rug.size.x : rug.size.z;
                    foreach (float side in new[] { -1f, 1f })
                        for (float t = -span * .5f + .05f; t < span * .5f - .04f; t += .08f)
                        {
                            var at = shortAlongX
                                ? new Vector3(rug.center.x + t, rug.min.y + .002f, rug.center.z + side * (rug.size.z * .5f + .035f))
                                : new Vector3(rug.center.x + side * (rug.size.x * .5f + .035f), rug.min.y + .002f, rug.center.z + t);
                            Flat(finish, fringe, at, shortAlongX ? new Vector3(.022f, .002f, .07f) : new Vector3(.07f, .002f, .022f));
                        }
                }
                return count;
            }

            int Thresholds()
            {
                int count = 0;
                foreach (var door in site.Layout.Doors)
                {
                    if (door.A != room.Name) continue;
                    bool alongZ = Mathf.Min(Mathf.Abs(door.X - room.MinX), Mathf.Abs(door.X - room.MaxX)) < .35f;
                    var size = alongZ ? new Vector3(.2f, .012f, door.Width) : new Vector3(door.Width, .012f, .2f);
                    finish.Box(top, new Vector3(door.X, room.FloorY + .006f, door.Z), Quaternion.identity, size, tiles[top], null, false);
                    count++;
                }
                return count;
            }

            void Hazard()
            {
                const float Inset = .75f, Width = .07f;
                Strip(finish, null, Flowers + 2, true, room.MinZ + Inset, room.MinX + Inset, room.MaxX - Inset, Width, .003f, .004f);
                Strip(finish, null, Flowers + 2, true, room.MaxZ - Inset, room.MinX + Inset, room.MaxX - Inset, Width, .003f, .004f);
                Strip(finish, null, Flowers + 2, false, room.MinX + Inset, room.MinZ + Inset + Width, room.MaxZ - Inset - Width, Width, .003f, .004f);
                Strip(finish, null, Flowers + 2, false, room.MaxX - Inset, room.MinZ + Inset + Width, room.MaxZ - Inset - Width, Width, .003f, .004f);
                for (int k = 0; k < 2; k++)
                {
                    var at = new Vector3(Mathf.Lerp(room.MinX + 2f, room.MaxX - 2f, R(.2f, .8f)), room.FloorY + .002f, Mathf.Lerp(room.MinZ + 2f, room.MaxZ - 2f, R(.2f, .8f)));
                    Stain(at, R(.35f, .55f), R(.35f, .55f), k);
                }
            }

            void Stain(Vector3 at, float reach, float spread, int index)
            {
                var local = new System.Random(index * 7919 + Mathf.RoundToInt(at.x * 100f) * 31 + Mathf.RoundToInt(at.z * 100f));
                float L(float a, float b) => a + (float)local.NextDouble() * (b - a);
                float angle = L(0f, Mathf.PI * 2f);
                var along = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var side = new Vector3(-along.z, 0f, along.x);
                for (int i = 0; i < 5; i++)
                {
                    float t = (i - 2) * .5f;
                    var centre = at + along * (t * reach * .8f) + side * L(-.07f, .07f) + Vector3.up * (.0004f * i);
                    float radius = spread * L(.24f, .36f) * (1f - .45f * Mathf.Abs(t));
                    finish.Cylinder(pal, Swatch(MidGrey), centre, Quaternion.identity, radius, radius, .002f, 10);
                }
                float core = spread * L(.1f, .14f);
                finish.Cylinder(pal, Swatch(DarkGrey), at + side * L(-.03f, .03f) + Vector3.up * .0024f, Quaternion.identity, core, core, .002f, 9);
                for (int i = 0; i < 3; i++)
                {
                    var drop = at + along * (reach * L(.95f, 1.3f) * (i % 2 == 0 ? 1f : -1f)) + side * L(-.15f, .15f) + Vector3.up * .0028f;
                    float radius = L(.03f, .06f);
                    finish.Cylinder(pal, Swatch(MidGrey), drop, Quaternion.identity, radius, radius, .002f, 8);
                }
            }

            void Stripes()
            {
                var light = Surface(Surfaces.Kind.Lawn, Surfaces.Hex(0x98B489));
                bool alongX = room.MaxX - room.MinX >= room.MaxZ - room.MinZ;
                const float Width = 1.5f;
                float from = alongX ? room.MinZ : room.MinX, to = alongX ? room.MaxZ : room.MaxX;
                int index = 0;
                for (float at = from + .15f; at + Width <= to - .1f; at += Width, index++)
                {
                    if (index % 2 == 1) continue;
                    if (alongX) Strip(finish, light, 0, true, at + Width * .5f, room.MinX + .15f, room.MaxX - .15f, Width, .002f, .004f);
                    else Strip(finish, light, 0, false, at + Width * .5f, room.MinZ + .15f, room.MaxZ - .15f, Width, .002f, .004f);
                }
            }

            void Joints(float step, int swatch)
            {
                float width = step < .5f ? .03f : .025f;
                for (float x = room.MinX + step; x < room.MaxX - .1f; x += step) Strip(finish, null, swatch, false, x, room.MinZ + .12f, room.MaxZ - .12f, width, .002f, .004f);
                for (float z = room.MinZ + step; z < room.MaxZ - .1f; z += step) Strip(finish, null, swatch, true, z, room.MinX + .12f, room.MaxX - .12f, width, .002f, .004f);
            }

            int Stones()
            {
                var layout = site.Layout;
                int count = 0;
                foreach (var door in layout.Doors)
                {
                    string other = door.A == room.Name ? door.B : door.B == room.Name ? door.A : null;
                    var inside = other == null ? null : layout.Room(other);
                    if (inside == null || inside.Outdoor) continue;
                    var inward = Mathf.Abs(door.X - room.MinX) < .35f ? Vector3.right : Mathf.Abs(door.X - room.MaxX) < .35f ? Vector3.left
                        : Mathf.Abs(door.Z - room.MinZ) < .35f ? Vector3.forward : Vector3.back;
                    var across = Vector3.Cross(Vector3.up, inward);
                    for (int k = 0; k < 4; k++)
                    {
                        var at = new Vector3(door.X, room.FloorY + .012f, door.Z) + inward * (.9f + k * .75f) + across * R(-.08f, .08f);
                        if (!room.Contains(at.x, at.z)) break;
                        Flat(finish, Pick(StoneLight, StoneMid, Hearth), at, new Vector3(.55f, .024f, .42f), Quaternion.LookRotation(inward).eulerAngles.y + R(-8f, 8f));
                        count++;
                    }
                }
                return count;
            }

            void Ceiling()
            {
                if (room.Outdoor) return;
                float w = room.MaxX - room.MinX, d = room.MaxZ - room.MinZ;
                var centre = new Vector3((room.MinX + room.MaxX) * .5f, ceiling, (room.MinZ + room.MaxZ) * .5f);
                var axis = w >= d ? Vector3.right : Vector3.forward;
                float longest = Mathf.Max(w, d);
                var lights = longest > 9f || room.Name == "Playroom"
                    ? new[] { centre - axis * longest * .25f, centre + axis * longest * .25f } : new[] { centre };
                float drop = Mathf.Clamp(ceiling - room.FloorY - 2.15f, .45f, .95f), cord = drop - .25f;
                int hung = 0;
                foreach (var at in lights)
                {
                    if (site.Layout.Stairs.Any(s => (s.Lower == room.Name || s.Upper == room.Name) && s.Covers(at.x, at.z))) continue;
                    overhead.Cylinder(pal, Swatch(White), at - Vector3.up * .03f, Quaternion.identity, .07f, .07f, .03f, 10);
                    overhead.Box(pal, at - Vector3.up * (.03f + cord * .5f), Quaternion.identity, new Vector3(.012f, cord, .012f), Vector2.one, Swatch(Black));
                    overhead.Cylinder(pal, Swatch(shade), at - Vector3.up * drop, Quaternion.identity, .22f, .08f, .22f, 14, false);
                    overhead.Cylinder(pal, Swatch(shade), at - Vector3.up * (drop - .22f), Quaternion.identity, .08f, .08f, .005f, 14);
                    overhead.Blob(pal, Swatch(Candle), Vector2.one, at - Vector3.up * (drop - .09f), Vector3.one * .06f);
                    hung++;
                }
                if (hung > 0) notes.Add("Pendant lights x" + hung);
                if (kind != Kind.Attic) return;
                bool beamsAlongX = w < d;
                float span = beamsAlongX ? d : w;
                int beams = 0;
                for (float t = -span * .5f + 1f; t < span * .5f - .5f; t += 2f, beams++)
                {
                    var at = centre + (beamsAlongX ? Vector3.forward : Vector3.right) * t - Vector3.up * .09f;
                    var size = beamsAlongX ? new Vector3(w - .2f, .18f, .14f) : new Vector3(.14f, .18f, d - .2f);
                    overhead.Box(dark, at, Quaternion.identity, size, tiles[dark], null);
                }
                if (beams > 0) notes.Add("Roof beams x" + beams);
            }
        }
    }
}
