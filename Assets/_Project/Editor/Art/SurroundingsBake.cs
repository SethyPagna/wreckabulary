using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Surroundings;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class SurroundingsBake
    {
        public const string PrefabFolder = "Assets/_Project/Resources/Surroundings";
        public const string MeshFolder = "Assets/_Project/Art/Surroundings/Baked";
        public const string MaterialFolder = "Assets/_Project/Materials/Backdrop";
        public const string ReceiptFolder = "BuildNotes/surroundings";
        public const string ConfigFolder = "Assets/_Project/Data/Config";
        public const string GroundName = "Ground", LifeName = "Life";
        public const int DiscSegments = 64;
        static readonly string[] CodeInputs =
        {
            "Assets/_Project/Editor/Art/SurroundingsBake.cs", "Assets/_Project/Editor/Art/SurroundingsImport.cs"
        };

        [Serializable] public sealed class FileHash { public string path, sha256; }

        [Serializable]
        public sealed class GroupReceipt
        {
            public string name, ring, slot, mesh, meshHash, shadows;
            public int pieces, renderers, triangles, vertices, colours;
        }

        [Serializable]
        public sealed class TierReceipt
        {
            public string prefab;
            public int renderers, triangles, vertices, movers, rendererLimit, triangleLimit;
            public bool withinBudget;
            public List<GroupReceipt> groups = new();
        }

        [Serializable]
        public sealed class Receipt
        {
            public string mapId, name, light, ambience, layoutHash;
            public int placements, movers, groundZones;
            public bool passed;
            public List<FileHash> inputs = new();
            public List<FileHash> code = new();
            public TierReceipt high = new(), low = new();
            public List<FileHash> outputs = new();
            public List<string> warnings = new(), errors = new();
        }

        sealed class SlotMesh
        {
            public Vector3[] positions, normals;
            public Color32[] colours;
            public Vector2[] uvs;
            public int[] indices;
        }

        sealed class PieceMeshes
        {
            public SurroundingsPiece piece;
            public Matrix4x4 space;
            public readonly Dictionary<string, SlotMesh> slots = new(StringComparer.Ordinal);
            public readonly List<string> order = new();
        }

        sealed class MeshBuffer
        {
            public readonly List<Vector3> positions = new(), normals = new();
            public readonly List<Color32> colours = new();
            public readonly List<Vector2> uvs = new();
            public readonly List<List<int>> submeshes = new();
            public readonly List<string> slots = new();
            public int pieces, triangles;
            public int VertexCount => positions.Count;

            public List<int> Submesh(string slot)
            {
                int index = slots.IndexOf(slot);
                if (index >= 0) return submeshes[index];
                slots.Add(slot);
                submeshes.Add(new List<int>());
                return submeshes[submeshes.Count - 1];
            }

            public void Append(SlotMesh source, string slot, Matrix4x4 transform, uint tint)
            {
                int start = positions.Count;
                var normalMatrix = transform.inverse.transpose;
                for (int i = 0; i < source.positions.Length; i++)
                {
                    positions.Add(transform.MultiplyPoint3x4(source.positions[i]));
                    normals.Add(normalMatrix.MultiplyVector(source.normals[i]).normalized);
                    colours.Add(Tint(source.colours[i], tint));
                    uvs.Add(source.uvs[i]);
                }
                var target = Submesh(slot);
                foreach (int index in source.indices) target.Add(start + index);
                triangles += source.indices.Length / 3;
            }
        }

        sealed class Chunk
        {
            public string name, path, slot;
            public SurroundingsRing ring;
            public MeshBuffer buffer;
            public Mesh asset;
            public string hash;
            public SurroundingsMover mover;
        }

        sealed class Node
        {
            public string name;
            public Vector3 position;
            public Mesh mesh;
            public Material[] materials;
            public ShadowCastingMode shadows;
            public SurroundingsMover mover;
            public readonly List<Node> children = new();
        }

        [MenuItem("Wreckabulary/Art/Bake Surroundings")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Bake surroundings in Edit Mode.");
            if (AssetDatabase.IsValidFolder(SurroundingsImport.KitFolder)) SurroundingsImport.EnsureImported();
            var kitReport = SurroundingsImport.Check();
            if (!kitReport.passed)
            {
                SurroundingsImport.Verify();
                return;
            }
            var maps = PlanFiles();
            if (maps.Count == 0) throw new InvalidOperationException("No map plans at " + SurroundingsImport.DataFolder + "/<mapId>.json.");
            var receipts = new List<Receipt>();
            foreach (string plan in maps) receipts.Add(Bake(plan));
            AssetDatabase.SaveAssets();
            foreach (var receipt in receipts) FinishReceipt(receipt);
            var failed = receipts.Where(r => !r.passed).ToList();
            string summary = "SURROUNDINGS_BAKE: " + (failed.Count == 0 ? "PASS " : "FAIL ") + string.Join(" ", receipts.Select(r =>
                $"{r.mapId}[layout={r.layoutHash} high={r.high.renderers}r/{r.high.triangles}t low={r.low.renderers}r/{r.low.triangles}t outputs={Combined(r.outputs)}]"));
            if (failed.Count > 0) throw new InvalidOperationException(summary + "\n" + string.Join("\n", failed.SelectMany(r => r.errors)));
            Debug.Log(summary);
        }

        public static List<string> PlanFiles()
        {
            string folder = SurroundingsImport.ProjectPath(SurroundingsImport.DataFolder);
            if (!Directory.Exists(folder)) return new List<string>();
            return Directory.GetFiles(folder, "*.json")
                .Select(Path.GetFileName)
                .Where(name => !name.StartsWith("kit_", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .Select(name => SurroundingsImport.DataFolder + "/" + name).ToList();
        }

        public static string PlanPath(string mapId) => SurroundingsImport.DataFolder + "/" + mapId + ".json";
        public static string ReceiptPath(string mapId) => ReceiptFolder + "/" + mapId + ".json";
        public static string PrefabPath(string mapId, bool low) => PrefabFolder + "/" + mapId + (low ? MapSurroundings.LowSuffix : "") + ".prefab";
        public static string MaterialPath(string slot) => MaterialFolder + "/" + slot + ".mat";

        public static List<string> KitFiles() =>
            SurroundingsImport.LoadManifests().Select(m => m.path).ToList();

        public static SurroundingsKit LoadKit()
        {
            SurroundingsKit kit = null;
            foreach (string path in KitFiles())
            {
                string json = File.ReadAllText(SurroundingsImport.ProjectPath(path));
                kit = kit == null ? SurroundingsKit.FromJson(json, path) : kit.Add(json, path);
            }
            return kit ?? throw new InvalidOperationException("No kit manifests at " + SurroundingsImport.DataFolder + "/kit_*.json.");
        }

        public static SurroundingsPlan LoadPlan(string planPath) =>
            SurroundingsPlan.FromJson(File.ReadAllText(SurroundingsImport.ProjectPath(planPath)), planPath);

        public static SurroundingsResult Layout(string planPath, out SurroundingsPlan plan, out SurroundingsKit kit)
        {
            plan = LoadPlan(planPath);
            kit = LoadKit();
            return SurroundingsLayout.Build(plan, kit, GameConfig.Current.HouseFor(plan.MapId));
        }

        public static Receipt LoadReceipt(string mapId)
        {
            string path = SurroundingsImport.ProjectPath(ReceiptPath(mapId));
            return File.Exists(path) ? JsonUtility.FromJson<Receipt>(File.ReadAllText(path)) : null;
        }

        public static List<FileHash> DataInputs(SurroundingsPlan plan, SurroundingsKit kit, string planPath)
        {
            var paths = new List<string> { planPath };
            paths.AddRange(KitFiles());
            if (!string.IsNullOrEmpty(plan.HouseFile)) paths.Add(ConfigFolder + "/" + plan.HouseFile);
            foreach (string fbx in kit.Pieces.Select(p => p.Fbx).Where(f => !string.IsNullOrEmpty(f)).Distinct().OrderBy(f => f, StringComparer.Ordinal))
            {
                paths.Add(fbx);
                paths.Add(fbx + ".meta");
            }
            foreach (string slot in SurroundingsImport.Slots) paths.Add(MaterialPath(slot));
            return paths.Distinct().Select(Hash).ToList();
        }

        static Receipt Bake(string planPath)
        {
            var result = Layout(planPath, out var plan, out var kit);
            var receipt = new Receipt
            {
                mapId = result.MapId, name = result.Name, light = result.LightId, ambience = result.AmbienceId, layoutHash = result.Hash,
                placements = result.Placements.Count, movers = result.Movers.Count, groundZones = result.Ground.Count,
                inputs = DataInputs(plan, kit, planPath), code = CodeFiles().Select(Hash).ToList()
            };
            if (!File.Exists(SurroundingsImport.ProjectPath("Assets/_Project/Resources/Environment/" + result.LightId + ".asset")))
                receipt.warnings.Add($"Light profile Resources/Environment/{result.LightId}.asset is missing; the match keeps its own light.");
            var materials = SurroundingsImport.Slots.ToDictionary(slot => slot, slot => AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(slot)), StringComparer.Ordinal);
            var meshes = LoadPieces(kit, result);
            string meshFolder = MeshFolder + "/" + result.MapId;
            EnsureFolder(meshFolder);
            EnsureFolder(PrefabFolder);

            var high = Chunks(result.Placements, meshes, meshFolder, "");
            var low = Chunks(result.Placements.Where(p => p.InLow), meshes, meshFolder, "Low_");
            foreach (var chunk in low)
            {
                var twin = high.FirstOrDefault(h => h.name == chunk.name && h.hash == chunk.hash);
                if (twin != null) chunk.path = twin.path;
            }
            var ground = GroundChunks(result.Ground, meshFolder);
            var life = result.Movers.Select((mover, n) => MoverChunk(mover, n, meshes, meshFolder)).ToList();
            receipt.warnings.AddRange(Expected(high, result.Budget.High, "High"));
            receipt.warnings.AddRange(Expected(low, result.Budget.Low, "Low"));

            var needed = high.Concat(low).Concat(ground).Concat(life).Select(c => c.buffer.slots).SelectMany(s => s).Distinct().OrderBy(s => s, StringComparer.Ordinal);
            foreach (string slot in needed)
                if (!materials.TryGetValue(slot, out var material) || !material) receipt.errors.Add("Missing backdrop material " + MaterialPath(slot));
                else if (material.shader.name != BackdropShader.ShaderName) receipt.errors.Add($"{MaterialPath(slot)} uses {material.shader.name}, not {BackdropShader.ShaderName}.");

            bool groundInLow = result.Budget.Low.GroundRenderers > 0;
            receipt.high = Tier(high, ground, life, true, SurroundingsBudget.HighRenderers, SurroundingsBudget.HighTriangles, PrefabPath(result.MapId, false));
            receipt.low = Tier(low, groundInLow ? ground : new List<Chunk>(), new List<Chunk>(), false, SurroundingsBudget.LowRenderers, SurroundingsBudget.LowTriangles, PrefabPath(result.MapId, true));
            if (!receipt.high.withinBudget) receipt.errors.Add($"High tier {receipt.high.renderers} renderers / {receipt.high.triangles} triangles is over {SurroundingsBudget.HighRenderers} / {SurroundingsBudget.HighTriangles}.");
            if (!receipt.low.withinBudget) receipt.errors.Add($"Low tier {receipt.low.renderers} renderers / {receipt.low.triangles} triangles is over {SurroundingsBudget.LowRenderers} / {SurroundingsBudget.LowTriangles}.");
            if (receipt.errors.Count > 0) { WriteReceipt(receipt); return receipt; }

            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var chunk in high.Concat(low).Concat(ground).Concat(life))
            {
                if (used.Add(chunk.path)) chunk.asset = Save(chunk);
                else chunk.asset = AssetDatabase.LoadAssetAtPath<Mesh>(chunk.path);
            }
            foreach (string stale in AssetDatabase.FindAssets("t:Mesh", new[] { meshFolder }).Select(AssetDatabase.GUIDToAssetPath).Where(p => !used.Contains(p)).ToList())
                AssetDatabase.DeleteAsset(stale);

            SavePrefab(PrefabPath(result.MapId, false), Tree(high, ground, life, true, materials), result, false);
            SavePrefab(PrefabPath(result.MapId, true), Tree(low, groundInLow ? ground : new List<Chunk>(), new List<Chunk>(), false, materials), result, true);
            receipt.outputs = used.OrderBy(p => p, StringComparer.Ordinal).Concat(new[] { PrefabPath(result.MapId, false), PrefabPath(result.MapId, true) })
                .Select(p => new FileHash { path = p }).ToList();
            receipt.passed = true;
            return receipt;
        }

        static IEnumerable<string> CodeFiles()
        {
            foreach (string path in CodeInputs) yield return path;
            string rules = SurroundingsImport.ProjectPath("Assets/_Project/Scripts/Rules/Surroundings");
            if (!Directory.Exists(rules)) yield break;
            foreach (string file in Directory.GetFiles(rules, "*.cs").Select(Path.GetFileName).OrderBy(f => f, StringComparer.Ordinal))
                yield return "Assets/_Project/Scripts/Rules/Surroundings/" + file;
        }

        static Dictionary<string, PieceMeshes> LoadPieces(SurroundingsKit kit, SurroundingsResult result)
        {
            var pieces = new Dictionary<string, PieceMeshes>(StringComparer.Ordinal);
            foreach (string id in result.Placements.Select(p => p.Piece).Concat(result.Movers.Select(m => m.Piece)).Distinct().OrderBy(i => i, StringComparer.Ordinal))
            {
                var piece = kit.Piece(id) ?? throw new InvalidOperationException("The layout uses a piece the kit lacks: " + id);
                var node = SurroundingsImport.FindObject(piece.Fbx, piece.FbxObject)
                    ?? throw new InvalidOperationException($"No mesh object '{piece.FbxObject}' in {piece.Fbx}; run SurroundingsImport.Verify.");
                var mesh = node.GetComponent<MeshFilter>().sharedMesh;
                var materials = node.GetComponent<MeshRenderer>().sharedMaterials;
                var positions = mesh.vertices;
                var normals = mesh.normals;
                var colours = mesh.colors32;
                var uvs = mesh.uv;
                var entry = new PieceMeshes { piece = piece, space = SurroundingsImport.PieceSpace(node.transform) };
                for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
                {
                    string slot = materials[s].name;
                    var source = mesh.GetIndices(s);
                    var remap = new Dictionary<int, int>();
                    var used = new List<int>();
                    var indices = new int[source.Length];
                    for (int i = 0; i < source.Length; i++)
                    {
                        if (!remap.TryGetValue(source[i], out int local)) { local = used.Count; remap.Add(source[i], local); used.Add(source[i]); }
                        indices[i] = local;
                    }
                    entry.slots[slot] = new SlotMesh
                    {
                        positions = used.Select(v => positions[v]).ToArray(),
                        normals = used.Select(v => normals.Length == positions.Length ? normals[v] : Vector3.up).ToArray(),
                        colours = used.Select(v => colours.Length == positions.Length ? colours[v] : new Color32(255, 255, 255, 0)).ToArray(),
                        uvs = used.Select(v => uvs.Length == positions.Length ? uvs[v] : Vector2.zero).ToArray(),
                        indices = indices
                    };
                    entry.order.Add(slot);
                }
                pieces.Add(id, entry);
            }
            return pieces;
        }

        static Matrix4x4 Placement(SurroundingsPlacement placement, PieceMeshes piece) =>
            Matrix4x4.TRS(new Vector3(placement.X, placement.Y, placement.Z), Quaternion.Euler(0f, placement.Yaw, 0f), Vector3.one * placement.Scale) * piece.space;

        static List<Chunk> Chunks(IEnumerable<SurroundingsPlacement> placements, Dictionary<string, PieceMeshes> pieces, string folder, string prefix)
        {
            var list = placements.ToList();
            var chunks = new List<Chunk>();
            foreach (SurroundingsRing ring in Enum.GetValues(typeof(SurroundingsRing)))
            {
                var inRing = list.Where(p => p.Ring == ring).ToList();
                var slots = inRing.SelectMany(p => pieces[p.Piece].order).Distinct().OrderBy(s => s, StringComparer.Ordinal);
                foreach (string slot in slots)
                {
                    var parts = new List<MeshBuffer> { new() };
                    foreach (var placement in inRing)
                    {
                        var piece = pieces[placement.Piece];
                        if (!piece.slots.TryGetValue(slot, out var source)) continue;
                        var part = parts[parts.Count - 1];
                        if (part.VertexCount > 0 && part.VertexCount + source.positions.Length > SurroundingsLayout.MergeVertexLimit) parts.Add(part = new MeshBuffer());
                        part.Append(source, slot, Placement(placement, piece), placement.TintFor(slot));
                        part.pieces++;
                    }
                    for (int n = 0; n < parts.Count; n++)
                    {
                        string name = slot + (n == 0 ? "" : "_" + (n + 1));
                        chunks.Add(new Chunk
                        {
                            name = ring + "/" + name, ring = ring, slot = slot, buffer = parts[n], hash = ContentHash(parts[n]),
                            path = folder + "/" + prefix + ring + "_" + name + ".asset"
                        });
                    }
                }
            }
            return chunks;
        }

        static List<Chunk> GroundChunks(IReadOnlyList<SurroundingsGround> zones, string folder)
        {
            var chunks = new List<Chunk>();
            foreach (string slot in zones.Select(z => z.Slot).Distinct().OrderBy(s => s, StringComparer.Ordinal))
            {
                var buffer = new MeshBuffer();
                var indices = buffer.Submesh(slot);
                foreach (var zone in zones.Where(z => z.Slot == slot))
                {
                    int start = buffer.positions.Count;
                    var colour = Tint(new Color32(255, 255, 255, 0), zone.Tint);
                    var corners = zone.Shape == SurroundingsGroundShape.Disc ? Disc(zone) : new List<Vector3>
                    {
                        new(zone.MinX, zone.Y, zone.MinZ), new(zone.MinX, zone.Y, zone.MaxZ), new(zone.MaxX, zone.Y, zone.MaxZ), new(zone.MaxX, zone.Y, zone.MinZ)
                    };
                    foreach (var corner in corners)
                    {
                        buffer.positions.Add(corner);
                        buffer.normals.Add(Vector3.up);
                        buffer.colours.Add(colour);
                        buffer.uvs.Add(new Vector2(corner.x, corner.z));
                    }
                    for (int i = 1; i + 1 < corners.Count; i++)
                    {
                        indices.Add(start); indices.Add(start + i); indices.Add(start + i + 1);
                        buffer.triangles++;
                    }
                    buffer.pieces++;
                }
                chunks.Add(new Chunk { name = GroundName + "/" + slot, slot = slot, buffer = buffer, hash = ContentHash(buffer), path = folder + "/" + GroundName + "_" + slot + ".asset" });
            }
            return chunks;
        }

        static List<Vector3> Disc(SurroundingsGround zone)
        {
            var points = new List<Vector3> { new(zone.CentreX, zone.Y, zone.CentreZ) };
            for (int i = DiscSegments; i >= 0; i--)
            {
                float angle = i % DiscSegments * Mathf.PI * 2f / DiscSegments;
                points.Add(new Vector3(zone.CentreX + Mathf.Cos(angle) * zone.Radius, zone.Y, zone.CentreZ + Mathf.Sin(angle) * zone.Radius));
            }
            return points;
        }

        static Chunk MoverChunk(SurroundingsMover mover, int n, Dictionary<string, PieceMeshes> pieces, string folder)
        {
            var piece = pieces[mover.Piece];
            var buffer = new MeshBuffer();
            foreach (string slot in piece.order.OrderBy(s => s, StringComparer.Ordinal))
                buffer.Append(piece.slots[slot], slot, piece.space, mover.TintFor(slot));
            buffer.pieces = 1;
            return new Chunk
            {
                name = LifeName + "/" + n, slot = string.Join("+", buffer.slots), buffer = buffer, mover = mover,
                hash = ContentHash(buffer), path = folder + "/" + LifeName + "_" + n + ".asset"
            };
        }

        static IEnumerable<string> Expected(List<Chunk> chunks, SurroundingsTier tier, string label)
        {
            foreach (var group in tier.Groups)
            {
                int baked = chunks.Where(c => c.ring == group.Ring && c.slot == group.Slot).Sum(c => c.buffer.triangles);
                if (baked != group.Triangles) yield return $"{label} {group.Ring}/{group.Slot}: {baked} triangles baked, the layout expected {group.Triangles}.";
            }
            foreach (var chunk in chunks.Where(c => !tier.Groups.Any(g => g.Ring == c.ring && g.Slot == c.slot)))
                yield return $"{label} {chunk.name} is not in the layout's budget groups.";
        }

        static TierReceipt Tier(List<Chunk> groups, List<Chunk> ground, List<Chunk> life, bool high, int rendererLimit, int triangleLimit, string prefab)
        {
            var tier = new TierReceipt { prefab = prefab, rendererLimit = rendererLimit, triangleLimit = triangleLimit, movers = life.Count };
            foreach (var chunk in groups.Concat(ground).Concat(life))
            {
                tier.groups.Add(new GroupReceipt
                {
                    name = chunk.name, ring = chunk.name.Split('/')[0], slot = chunk.slot, mesh = chunk.path, meshHash = chunk.hash,
                    pieces = chunk.buffer.pieces, renderers = 1, triangles = chunk.buffer.triangles, vertices = chunk.buffer.VertexCount,
                    colours = chunk.buffer.colours.Select(c => (c.r << 16) | (c.g << 8) | c.b).Distinct().Count(),
                    shadows = Shadows(chunk, high).ToString()
                });
            }
            tier.renderers = tier.groups.Count;
            tier.triangles = tier.groups.Sum(g => g.triangles);
            tier.vertices = tier.groups.Sum(g => g.vertices);
            tier.withinBudget = tier.renderers <= rendererLimit && tier.triangles <= triangleLimit;
            return tier;
        }

        static ShadowCastingMode Shadows(Chunk chunk, bool high) =>
            high && chunk.buffer.slots.Count > 0 && chunk.name.StartsWith(nameof(SurroundingsRing.Near) + "/", StringComparison.Ordinal) ? ShadowCastingMode.On : ShadowCastingMode.Off;

        static Node Tree(List<Chunk> groups, List<Chunk> ground, List<Chunk> life, bool high, Dictionary<string, Material> materials)
        {
            var root = new Node { name = MapSurroundings.RootName };
            Node Parent(string name)
            {
                var parent = root.children.FirstOrDefault(c => c.name == name);
                if (parent == null) root.children.Add(parent = new Node { name = name });
                return parent;
            }
            foreach (var chunk in groups.Concat(ground))
            {
                var split = chunk.name.Split('/');
                Parent(split[0]).children.Add(new Node
                {
                    name = split[1], mesh = chunk.asset, shadows = Shadows(chunk, high),
                    materials = chunk.buffer.slots.Select(s => materials[s]).ToArray()
                });
            }
            for (int n = 0; n < life.Count; n++)
            {
                var chunk = life[n];
                var mover = chunk.mover;
                Parent(LifeName).children.Add(new Node
                {
                    name = n.ToString(), mesh = chunk.asset, shadows = ShadowCastingMode.Off, mover = mover,
                    position = mover.Path.Count > 0 ? Point(mover.Path[0]) : Vector3.zero,
                    materials = chunk.buffer.slots.Select(s => materials[s]).ToArray()
                });
            }
            return root;
        }

        static Vector3 Point(SurroundingsPoint point) => new(point.X, point.Y, point.Z);

        static void SavePrefab(string path, Node tree, SurroundingsResult result, bool low)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(tree.name);
            try
            {
                root.name = tree.name;
                Sync(root.transform, tree.children);
                if (!root.TryGetComponent<MapSurroundings>(out var surroundings)) surroundings = root.AddComponent<MapSurroundings>();
                surroundings.Configure(result.MapId, result.LightId, result.AmbienceId, result.Hash, low);
                foreach (var stray in root.GetComponents<Component>().Where(c => c is not Transform && c is not MapSurroundings).ToList()) Object.DestroyImmediate(stray);
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool saved);
                if (!saved) throw new InvalidOperationException("Could not save " + path);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
        }

        static void Sync(Transform parent, List<Node> nodes)
        {
            var kept = new HashSet<Transform>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                Transform child = null;
                foreach (Transform candidate in parent)
                    if (candidate.name == node.name && !kept.Contains(candidate)) { child = candidate; break; }
                if (!child)
                {
                    child = new GameObject(node.name).transform;
                    child.SetParent(parent, false);
                }
                kept.Add(child);
                child.SetSiblingIndex(i);
                child.localPosition = node.position;
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;
                child.gameObject.SetActive(true);
                Configure(child.gameObject, node);
                Sync(child, node.children);
            }
            foreach (var stale in parent.Cast<Transform>().Where(t => !kept.Contains(t)).ToList()) Object.DestroyImmediate(stale.gameObject);
        }

        static void Configure(GameObject go, Node node)
        {
            go.TryGetComponent<BackdropMover>(out var mover);
            if (node.mover == null && mover) Object.DestroyImmediate(mover);
            if (node.mesh)
            {
                if (!go.TryGetComponent<MeshFilter>(out var filter)) filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = node.mesh;
                if (!go.TryGetComponent<MeshRenderer>(out var renderer)) renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = node.materials;
                renderer.shadowCastingMode = node.shadows;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = node.mover != null ? MotionVectorGenerationMode.Object : MotionVectorGenerationMode.Camera;
                renderer.allowOcclusionWhenDynamic = true;
                renderer.staticShadowCaster = false;
            }
            else
            {
                if (go.TryGetComponent<MeshRenderer>(out var renderer)) Object.DestroyImmediate(renderer);
                if (go.TryGetComponent<MeshFilter>(out var filter)) Object.DestroyImmediate(filter);
            }
            if (node.mover != null)
            {
                if (!mover) mover = go.AddComponent<BackdropMover>();
                mover.Path = node.mover.Path.Select(Point).ToArray();
                mover.Speed = node.mover.Speed;
                mover.Loop = node.mover.Loop;
                mover.Phase = node.mover.Phase;
            }
            foreach (var stray in go.GetComponents<Component>().Where(c => c is not Transform && c is not MeshFilter && c is not MeshRenderer && c is not BackdropMover).ToList())
                Object.DestroyImmediate(stray);
        }

        static Mesh Save(Chunk chunk)
        {
            var fresh = Build(chunk);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(chunk.path);
            if (existing)
            {
                EditorUtility.CopySerialized(fresh, existing);
                Object.DestroyImmediate(fresh);
                MarkUnreadable(existing);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
                return existing;
            }
            MarkUnreadable(fresh);
            AssetDatabase.CreateAsset(fresh, chunk.path);
            return fresh;
        }

        static void MarkUnreadable(Mesh mesh)
        {
            var serialized = new SerializedObject(mesh);
            var readable = serialized.FindProperty("m_IsReadable");
            if (readable == null || !readable.boolValue) return;
            readable.boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Mesh Build(Chunk chunk)
        {
            var buffer = chunk.buffer;
            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(chunk.path) };
            mesh.indexFormat = buffer.VertexCount > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(buffer.positions);
            mesh.SetNormals(buffer.normals);
            mesh.SetColors(buffer.colours);
            mesh.SetUVs(0, buffer.uvs);
            mesh.subMeshCount = buffer.submeshes.Count;
            for (int i = 0; i < buffer.submeshes.Count; i++) mesh.SetTriangles(buffer.submeshes[i], i, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        static Color32 Tint(Color32 colour, uint tint)
        {
            if (tint == SurroundingsSlots.NoTint) return colour;
            byte Scale(byte channel, uint by) => (byte)((channel * (int)(by & 0xFF) + 127) / 255);
            return new Color32(Scale(colour.r, tint >> 16), Scale(colour.g, tint >> 8), Scale(colour.b, tint), colour.a);
        }

        static string ContentHash(MeshBuffer buffer)
        {
            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(buffer.VertexCount);
                for (int i = 0; i < buffer.VertexCount; i++)
                {
                    var p = buffer.positions[i]; var n = buffer.normals[i]; var c = buffer.colours[i]; var t = buffer.uvs[i];
                    writer.Write(p.x); writer.Write(p.y); writer.Write(p.z);
                    writer.Write(n.x); writer.Write(n.y); writer.Write(n.z);
                    writer.Write(c.r); writer.Write(c.g); writer.Write(c.b); writer.Write(c.a);
                    writer.Write(t.x); writer.Write(t.y);
                }
                for (int s = 0; s < buffer.submeshes.Count; s++)
                {
                    writer.Write(buffer.slots[s]);
                    writer.Write(buffer.submeshes[s].Count);
                    foreach (int index in buffer.submeshes[s]) writer.Write(index);
                }
            }
            return Sha(stream.ToArray());
        }

        static string Sha(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }

        public static FileHash Hash(string path)
        {
            string full = SurroundingsImport.ProjectPath(path);
            return new FileHash { path = path, sha256 = File.Exists(full) ? Sha(File.ReadAllBytes(full)) : "missing" };
        }

        static string Combined(List<FileHash> files) => Sha(Encoding.UTF8.GetBytes(string.Join("\n", files.Select(f => f.path + " " + f.sha256)))).Substring(0, 16);

        static void FinishReceipt(Receipt receipt)
        {
            if (receipt.passed) receipt.outputs = receipt.outputs.Select(f => Hash(f.path)).ToList();
            WriteReceipt(receipt);
        }

        static void WriteReceipt(Receipt receipt)
        {
            string path = SurroundingsImport.ProjectPath(ReceiptPath(receipt.mapId));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(receipt, true) + "\n");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
