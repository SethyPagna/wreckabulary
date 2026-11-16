using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public sealed class SurroundingsKitImporter : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!SurroundingsImport.IsKitAsset(assetPath)) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.preserveHierarchy = true;
            importer.importCameras = importer.importLights = importer.importAnimation = false;
            importer.importBlendShapes = importer.importVisibility = importer.importConstraints = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.addCollider = false;
            importer.isReadable = true;
            importer.generateSecondaryUV = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
        }
    }

    public static class SurroundingsImport
    {
        public const string KitFolder = "Assets/_Project/Art/Surroundings/Kit";
        public const string DataFolder = "Assets/_Project/Data/Surroundings";
        public const string ReportPath = "BuildNotes/surroundings/kit_import.json";
        public const float SizeTolerance = .02f, SizeSlack = .006f, PivotSlack = .02f, GroundSlack = .05f;
        public const int MaxSlots = 5;

        public static readonly string[] Slots =
        {
            "bk_plaster", "bk_brick", "bk_stone", "bk_roof", "bk_wood", "bk_paint", "bk_metal", "bk_glass_dark",
            "bk_glass_lit", "bk_leaf", "bk_bark", "bk_grass", "bk_road", "bk_line", "bk_card"
        };
        public static readonly string[] Tintable = { "bk_plaster", "bk_roof", "bk_paint" };
        static readonly string[] FrontSlots = { "bk_paint", "bk_glass_dark", "bk_glass_lit" };
        static readonly string[] FrontFamilies = { "house", "garage", "shed", "building", "landmark" };

        [Serializable]
        public sealed class KitPiece
        {
            public string id, family, far, fbxObject, fbx;
            public float width, depth, height;
            public string[] slots = Array.Empty<string>();
            public int tris;
            public string ObjectName => string.IsNullOrEmpty(fbxObject) ? id : fbxObject;
        }

        [Serializable]
        public sealed class KitManifest
        {
            public string kit, fbx;
            public List<KitPiece> pieces = new();
            [NonSerialized] public string path;
            public string FbxOf(KitPiece piece) => string.IsNullOrEmpty(piece.fbx) ? fbx : piece.fbx;
        }

        [Serializable]
        public sealed class PieceReport
        {
            public string kit, id, family, fbx, fbxObject, mesh;
            public bool found, passed;
            public Vector3 expected, measured, centre;
            public float minY, frontBias;
            public int tris, expectedTris, vertices;
            public string[] slots = Array.Empty<string>();
            public List<string> errors = new(), warnings = new();
        }

        [Serializable]
        public sealed class Report
        {
            public bool passed;
            public int kits, pieces, failed, warned;
            public List<string> manifests = new();
            public List<string> errors = new();
            public List<PieceReport> results = new();
        }

        public static bool IsKitAsset(string assetPath) =>
            assetPath != null && assetPath.Replace('\\', '/').StartsWith(KitFolder + "/", StringComparison.Ordinal);

        public static string ProjectPath(string relative) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));

        public static List<KitManifest> LoadManifests()
        {
            var manifests = new List<KitManifest>();
            string folder = ProjectPath(DataFolder);
            if (!Directory.Exists(folder)) return manifests;
            foreach (string file in Directory.GetFiles(folder, "kit_*.json").OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal))
            {
                var manifest = JsonUtility.FromJson<KitManifest>(File.ReadAllText(file)) ?? new KitManifest();
                manifest.path = DataFolder + "/" + Path.GetFileName(file);
                manifest.pieces ??= new List<KitPiece>();
                manifests.Add(manifest);
            }
            return manifests;
        }

        public static GameObject FindObject(string fbxPath, string objectName) =>
            AssetDatabase.LoadAllAssetsAtPath(fbxPath).OfType<GameObject>()
                .Where(go => go.name == objectName && go.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
                .OrderBy(go => go.transform.parent ? 1 : 0).FirstOrDefault();

        public static Matrix4x4 PieceSpace(Transform node) => Matrix4x4.TRS(Vector3.zero, node.localRotation, node.localScale);

        public static bool Imported(ModelImporter importer) =>
            importer && importer.isReadable && importer.bakeAxisConversion && importer.preserveHierarchy && !importer.addCollider
            && importer.materialImportMode == ModelImporterMaterialImportMode.ImportViaMaterialDescription;

        public static int EnsureImported()
        {
            int reimported = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { KitFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!IsKitAsset(path) || Imported(AssetImporter.GetAtPath(path) as ModelImporter)) continue;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                reimported++;
            }
            return reimported;
        }

        [MenuItem("Wreckabulary/Art/Verify Surroundings Kit")]
        public static void Verify()
        {
            if (AssetDatabase.IsValidFolder(KitFolder)) EnsureImported();
            var report = Check();
            string path = ProjectPath(ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, JsonUtility.ToJson(report, true) + "\n");
            string summary = $"SURROUNDINGS_KIT_IMPORT: {(report.passed ? "PASS" : "FAIL")} kits={report.kits} pieces={report.pieces} failed={report.failed} warned={report.warned} report={ReportPath}";
            foreach (string error in report.errors) Debug.LogError("[Wreckabulary] " + error);
            foreach (var piece in report.results)
            {
                foreach (string error in piece.errors) Debug.LogError($"[Wreckabulary] {piece.kit}/{piece.id}: {error}");
                foreach (string warning in piece.warnings) Debug.LogWarning($"[Wreckabulary] {piece.kit}/{piece.id}: {warning}");
            }
            if (!report.passed) throw new InvalidOperationException(summary);
            Debug.Log(summary);
        }

        public static Report Check()
        {
            var report = new Report();
            var manifests = LoadManifests();
            report.kits = manifests.Count;
            if (manifests.Count == 0) report.errors.Add("No kit manifests at " + DataFolder + "/kit_*.json.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var manifest in manifests)
            {
                report.manifests.Add(manifest.path);
                if (manifest.pieces.Count == 0) report.errors.Add(manifest.path + " lists no pieces.");
                foreach (var piece in manifest.pieces)
                    if (!ids.Add(piece.id ?? "")) report.errors.Add($"Duplicate piece id '{piece.id}' in {manifest.path}.");
            }
            var importers = new Dictionary<string, Object[]>(StringComparer.Ordinal);
            foreach (var manifest in manifests)
                foreach (var piece in manifest.pieces)
                {
                    var result = CheckPiece(manifest, piece, ids, importers);
                    result.passed = result.errors.Count == 0;
                    report.results.Add(result);
                }
            report.pieces = report.results.Count;
            report.failed = report.results.Count(r => !r.passed);
            report.warned = report.results.Count(r => r.warnings.Count > 0);
            report.passed = report.errors.Count == 0 && report.failed == 0;
            return report;
        }

        static PieceReport CheckPiece(KitManifest manifest, KitPiece piece, HashSet<string> ids, Dictionary<string, Object[]> checkedFbx)
        {
            string fbx = manifest.FbxOf(piece);
            var result = new PieceReport
            {
                kit = manifest.kit, id = piece.id, family = piece.family, fbx = fbx, fbxObject = piece.ObjectName,
                expected = new Vector3(piece.width, piece.height, piece.depth), expectedTris = piece.tris,
                slots = piece.slots ?? Array.Empty<string>()
            };
            if (string.IsNullOrEmpty(piece.id)) result.errors.Add("The piece has no id.");
            if (!string.IsNullOrEmpty(piece.far) && !ids.Contains(piece.far)) result.errors.Add($"Its far stand-in '{piece.far}' is not in any kit manifest.");
            if (string.IsNullOrEmpty(fbx) || !File.Exists(ProjectPath(fbx))) { result.errors.Add("Missing FBX " + fbx); return result; }
            if (!checkedFbx.ContainsKey(fbx)) checkedFbx[fbx] = CheckFbx(fbx, result.errors);
            else if (checkedFbx[fbx] == null) result.errors.Add("The FBX is not imported with the surroundings kit settings.");
            var node = FindObject(fbx, piece.ObjectName);
            if (!node) { result.errors.Add($"No mesh object named '{piece.ObjectName}' in {fbx}."); return result; }
            result.found = true;
            var mesh = node.GetComponent<MeshFilter>().sharedMesh;
            var materials = node.TryGetComponent<MeshRenderer>(out var renderer) ? renderer.sharedMaterials : Array.Empty<Material>();
            result.mesh = mesh.name;
            result.vertices = mesh.vertexCount;
            if (node.transform.childCount > 0) result.errors.Add("The piece has child objects; one object per piece, no children.");
            if (Quaternion.Angle(node.transform.localRotation, Quaternion.identity) > .05f)
                result.warnings.Add($"The node is rotated {node.transform.localEulerAngles}; apply rotation in Blender so the mesh itself faces +Z.");
            if ((node.transform.localScale - Vector3.one).sqrMagnitude > 1e-6f)
                result.warnings.Add($"The node is scaled {node.transform.localScale}; apply scale in Blender.");
            if (!mesh.isReadable) { result.errors.Add("The mesh is not readable; the importer did not apply."); return result; }
            var space = PieceSpace(node.transform);
            var vertices = mesh.vertices;
            var bounds = new Bounds(space.MultiplyPoint3x4(vertices.Length > 0 ? vertices[0] : Vector3.zero), Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(space.MultiplyPoint3x4(vertex));
            result.measured = bounds.size;
            result.centre = bounds.center;
            result.minY = bounds.min.y;
            CheckBounds(result, bounds);
            CheckSlots(result, piece, mesh, materials);
            CheckTriangles(result, piece, mesh);
            CheckColours(result, mesh, materials);
            CheckFacing(result, piece, mesh, materials, space);
            return result;
        }

        static Object[] CheckFbx(string fbx, List<string> errors)
        {
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (!Imported(importer))
            {
                errors.Add("The FBX is not imported with the surroundings kit settings (readable, baked axes, no colliders); reimport it under " + KitFolder + ".");
                return null;
            }
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbx);
            foreach (var go in assets.OfType<GameObject>())
            {
                if (go.GetComponent<Collider>()) errors.Add($"{go.name} imported a collider.");
                if (go.GetComponent<Light>()) errors.Add($"{go.name} imported a light.");
                if (go.GetComponent<Camera>()) errors.Add($"{go.name} imported a camera.");
                if (go.GetComponent<Animator>() || go.GetComponent<Animation>()) errors.Add($"{go.name} imported animation.");
                if (go.GetComponent<SkinnedMeshRenderer>()) errors.Add($"{go.name} is skinned; export plain meshes.");
            }
            return assets;
        }

        static bool Near(float measured, float expected) =>
            Mathf.Abs(measured - expected) <= Mathf.Max(SizeTolerance * Mathf.Abs(expected), SizeSlack);

        static void CheckBounds(PieceReport result, Bounds bounds)
        {
            var size = bounds.size;
            var expected = result.expected;
            bool x = Near(size.x, expected.x), y = Near(size.y, expected.y), z = Near(size.z, expected.z);
            if (!(x && y && z))
            {
                string hint = "";
                if (Near(size.x, expected.z) && Near(size.z, expected.x)) hint = " Width and depth are swapped: the piece lies along the wrong horizontal axis.";
                else if (Near(size.y, expected.z) && Near(size.z, expected.y)) hint = " Height and depth are swapped: check the export axes (forward -Z, up Y).";
                else if (Near(size.x, expected.y) && Near(size.y, expected.x)) hint = " Width and height are swapped.";
                result.errors.Add($"Bounds {Fmt(size)} differ from the manifest {Fmt(expected)} (w x h x d) by more than 2%.{hint}");
            }
            float slackX = Mathf.Max(PivotSlack, SizeTolerance * size.x), slackZ = Mathf.Max(PivotSlack, SizeTolerance * size.z);
            if (Mathf.Abs(bounds.center.x) > slackX || Mathf.Abs(bounds.center.z) > slackZ)
                result.errors.Add($"The pivot is not at the footprint centre: bounds centre x={bounds.center.x:0.###}, z={bounds.center.z:0.###}.");
            if (bounds.min.y < -GroundSlack) result.errors.Add($"Geometry goes {-bounds.min.y:0.###} m below the ground (limit {GroundSlack} m).");
            else if (bounds.min.y > GroundSlack) result.errors.Add($"The piece floats {bounds.min.y:0.###} m above its pivot; put its base at y = 0.");
        }

        static void CheckSlots(PieceReport result, KitPiece piece, Mesh mesh, Material[] materials)
        {
            var names = materials.Select(m => m ? m.name : "").ToArray();
            if (mesh.subMeshCount != names.Length) result.errors.Add($"{mesh.subMeshCount} submeshes but {names.Length} materials.");
            foreach (string name in names.Where(n => Array.IndexOf(Slots, n) < 0).Distinct())
                result.errors.Add($"Material '{name}' is not a backdrop slot.");
            if (names.Distinct().Count() != names.Length) result.errors.Add("A slot appears twice; merge faces of one slot into one material.");
            if (names.Length > MaxSlots) result.errors.Add($"{names.Length} slots; at most {MaxSlots}.");
            var expected = (piece.slots ?? Array.Empty<string>()).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            var actual = names.OrderBy(s => s, StringComparer.Ordinal).ToArray();
            if (!expected.SequenceEqual(actual))
                result.errors.Add($"Slots [{string.Join(", ", actual)}] differ from the manifest [{string.Join(", ", expected)}].");
        }

        public static int Triangles(Mesh mesh)
        {
            long count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) == MeshTopology.Triangles) count += mesh.GetIndexCount(i) / 3;
            return (int)count;
        }

        static void CheckTriangles(PieceReport result, KitPiece piece, Mesh mesh)
        {
            result.tris = Triangles(mesh);
            for (int i = 0; i < mesh.subMeshCount; i++)
                if (mesh.GetTopology(i) != MeshTopology.Triangles) result.errors.Add($"Submesh {i} is {mesh.GetTopology(i)}, not triangles.");
            if (result.tris == 0) result.errors.Add("The mesh has no triangles.");
            if (result.tris != piece.tris) result.errors.Add($"{result.tris} triangles after import; the manifest says {piece.tris}.");
        }

        static void CheckColours(PieceReport result, Mesh mesh, Material[] materials)
        {
            var colours = mesh.colors32;
            if (colours.Length != mesh.vertexCount) { result.errors.Add("No vertex colours; export the 'Col' attribute."); return; }
            int swaying = 0, dark = 0;
            for (int i = 0; i < mesh.subMeshCount && i < materials.Length; i++)
            {
                string slot = materials[i] ? materials[i].name : "";
                foreach (int index in mesh.GetIndices(i).Distinct())
                {
                    var c = colours[index];
                    if (slot != "bk_leaf" && c.a > 0) swaying++;
                    if (slot != "bk_card" && Mathf.Min(c.r, Mathf.Min(c.g, c.b)) < 179) dark++;
                }
            }
            if (swaying > 0) result.warnings.Add($"{swaying} vertices outside bk_leaf have wind weight (alpha) above 0.");
            if (dark > 0) result.warnings.Add($"{dark} vertices have a shading multiplier below 0.7 (contract range 0.75-1.0).");
        }

        static void CheckFacing(PieceReport result, KitPiece piece, Mesh mesh, Material[] materials, Matrix4x4 space)
        {
            var vertices = mesh.vertices;
            bool card = piece.family == "card" || (piece.id ?? "").StartsWith("card_", StringComparison.Ordinal);
            float front = 0f, back = 0f, facing = 0f, total = 0f;
            for (int i = 0; i < mesh.subMeshCount && i < materials.Length; i++)
            {
                string slot = materials[i] ? materials[i].name : "";
                bool counts = card || Array.IndexOf(FrontSlots, slot) >= 0;
                if (!counts || mesh.GetTopology(i) != MeshTopology.Triangles) continue;
                var indices = mesh.GetIndices(i);
                for (int t = 0; t + 2 < indices.Length; t += 3)
                {
                    var a = space.MultiplyPoint3x4(vertices[indices[t]]);
                    var cross = Vector3.Cross(space.MultiplyPoint3x4(vertices[indices[t + 1]]) - a, space.MultiplyPoint3x4(vertices[indices[t + 2]]) - a);
                    float area = cross.magnitude * .5f;
                    if (area <= 0f) continue;
                    float z = cross.z / (area * 2f);
                    total += area;
                    if (z > .5f) facing += area;
                    if (z > .7f) front += area;
                    else if (z < -.7f) back += area;
                }
            }
            result.frontBias = front + back > 0f ? (front - back) / (front + back) : 0f;
            if (card)
            {
                if (total <= 0f || facing < total * .95f) result.errors.Add($"Only {(total > 0f ? facing / total : 0f):P0} of the card faces +Z; cards are single-sided and face +Z.");
                return;
            }
            if (Array.IndexOf(FrontFamilies, piece.family) >= 0 && back > front * 1.25f && back > .25f)
                result.warnings.Add($"More door and window area faces -Z ({back:0.0} m²) than +Z ({front:0.0} m²); is the front +Z?");
        }

        static string Fmt(Vector3 v) => $"{v.x:0.###} x {v.y:0.###} x {v.z:0.###}";
    }
}
