using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    /// <summary>One-time migration from generated rooms to normal editable Unity world assets.</summary>
    public static class WorldAuthoring
    {
        const string ResourceFolder = "Assets/_Project/Resources/Worlds";
        const string MaterialFolder = "Assets/_Project/Worlds/Generated/Materials";
        const string MeshFolder = "Assets/_Project/Worlds/Generated/Meshes";
        const string LegacyFolder = "Assets/_Project/Editor/Legacy/Scenes";
        const string LegacyDependencyFolder = "Assets/_Project/Editor/Legacy/Dependencies";
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        public static void BakeWorldAssetsIfMissing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Bake worlds outside Play mode.");
            EnsureFolder(ResourceFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(MeshFolder);
            OrganizeLegacyDependencies();
            foreach (string mapId in new[] { "pinwheel", "courtyard" })
            {
                string assetPath = "Assets/_Project/Resources/" + AuthoredHouse.ResourcePath(mapId) + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath)) continue;
                BakeMissingWorld(mapId, assetPath);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Move only archived prototype dependencies, retaining asset GUIDs and all prefab references.</summary>
        public static void OrganizeLegacyDependencies()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Organize legacy assets outside Play mode.");
            foreach (var entry in new[] { (source: MaterialFolder, kind: "Materials", extension: ".mat"),
                         (source: MeshFolder, kind: "Meshes", extension: ".asset") })
            {
                if (!AssetDatabase.IsValidFolder(entry.source)) continue;
                string destinationFolder = LegacyDependencyFolder + "/" + entry.kind;
                EnsureFolder(destinationFolder);
                var paths = AssetDatabase.FindAssets("", new[] { entry.source })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(path => path.StartsWith(entry.source + "/legacy_", StringComparison.Ordinal)
                        && string.Equals(Path.GetDirectoryName(path)?.Replace('\\', '/'), entry.source, StringComparison.Ordinal)
                        && path.EndsWith(entry.extension, StringComparison.Ordinal)).ToArray();
                foreach (string path in paths)
                {
                    string destination = AssetDatabase.GenerateUniqueAssetPath(destinationFolder + "/" + Path.GetFileName(path));
                    string error = AssetDatabase.MoveAsset(path, destination);
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException("Could not move archived dependency " + path + ": " + error);
                }
            }
        }

        static void BakeMissingWorld(string mapId, string assetPath)
        {
            var previousScene = SceneManager.GetActiveScene();
            string previousMode = Match.ModeOverride;
            var bakeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(bakeScene);
            try
            {
                Match.ModeOverride = "Dibs";
                var root = new GameObject(mapId == "pinwheel" ? "Pinwheel House" : "Garden Courtyard");
                var generator = root.AddComponent<RoomBuilder>();
                var world = generator.BuildForAuthoring(GameConfig.Current.HouseFor(mapId), mapId);
                PersistPresentation(world.GeometryRoot.gameObject, mapId);
                PersistAssets(world.FurnitureRoot.gameObject, mapId + "_furniture");
                Object.DestroyImmediate(generator);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, assetPath, out bool success);
                if (!success || !saved) throw new InvalidOperationException("Could not save authored world: " + assetPath);
            }
            finally
            {
                Match.ModeOverride = previousMode;
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(bakeScene, true);
            }
        }

        /// <summary>Upgrade only arena/Moving Day scenes. Existing authored instances and their overrides win.</summary>
        public static void UpgradeSceneWorld()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Upgrade scenes outside Play mode.");
            OrganizeLegacyDependencies();
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var builder = roots.SelectMany(root => root.GetComponentsInChildren<RoomBuilder>(true)).FirstOrDefault();
            if (!builder)
            {
                var movingDay = roots.SelectMany(root => root.GetComponentsInChildren<MovingDayDirector>(true)).FirstOrDefault();
                if (!movingDay) return;
                builder = Undo.AddComponent<RoomBuilder>(movingDay.gameObject);
            }
            var existing = builder.AuthoredWorld;
            if (!existing) existing = roots.SelectMany(root => root.GetComponentsInChildren<AuthoredHouse>(true)).FirstOrDefault();
            if (existing)
            {
                if (builder.AuthoredWorld != existing)
                {
                    Undo.RecordObject(builder, "Connect authored house");
                    builder.SetAuthoredWorld(existing);
                    EditorUtility.SetDirty(builder);
                    EditorSceneManager.MarkSceneDirty(scene);
                }
                ArchiveLegacyRoots(scene, roots);
                return;
            }

            BakeWorldAssetsIfMissing();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ResourceFolder + "/PinwheelHouse.prefab");
            if (!prefab) throw new InvalidOperationException("The authored Pinwheel House prefab is missing.");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Add authored house");
            var world = instance.GetComponent<AuthoredHouse>();
            if (!world) throw new InvalidOperationException("The world prefab must contain AuthoredHouse.");
            Undo.RecordObject(builder, "Connect authored house");
            builder.SetAuthoredWorld(world);
            EditorUtility.SetDirty(builder);

            ArchiveLegacyRoots(scene, roots);
            HousePresentation.ApplyDefaultLighting();
            EditorSceneManager.MarkSceneDirty(scene);
        }

        static void ArchiveLegacyRoots(Scene scene, GameObject[] roots)
        {
            var legacyRoots = roots.Where(root => root && (root.name is "Room" or "Furniture"))
                .Where(root => !root.GetComponentInChildren<AuthoredHouse>(true)).ToArray();
            ArchiveRoots(scene, legacyRoots, "PrototypeContent");
            ArchiveLegacySceneDecor(scene, roots);
        }

        /// <summary>Remove only unchanged prototype signs/room labels, after archiving them outside shipped content.</summary>
        public static void ArchiveLegacySceneDecor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Archive legacy decor outside Play mode.");
            var scene = SceneManager.GetActiveScene();
            ArchiveLegacySceneDecor(scene, scene.GetRootGameObjects());
        }

        static void ArchiveLegacySceneDecor(Scene scene, GameObject[] roots)
        {
            if (scene.name is not ("LivingRoom" or "MovingDay")) return;
            var legacyDecor = roots.Where(root => IsLegacyDecor(root, scene.name)).ToArray();
            ArchiveRoots(scene, legacyDecor, "LegacyDecor");
        }

        static bool IsLegacyDecor(GameObject root, string sceneName)
        {
            if (!root || root.GetComponentInChildren<AuthoredHouse>(true) || !root.TryGetComponent<TMP_Text>(out var label)) return false;
            if (sceneName == "LivingRoom") return root.name == "Sign" && label.text == "<i>Home Sweet Home</i>";
            return sceneName == "MovingDay" &&
                ((root.name == "Sign" && label.text == "<i>Moving Day</i>") ||
                 (root.name == "Floor Label" && (label.text is "LIVING ROOM" or "BEDROOM")));
        }

        static void ArchiveRoots(Scene scene, GameObject[] legacyRoots, string suffix)
        {
            if (legacyRoots.Length == 0) return;
            EnsureFolder(LegacyFolder);
            var archive = new GameObject(scene.name + " " + suffix);
            archive.SetActive(false);
            try
            {
                foreach (var legacy in legacyRoots)
                {
                    var copy = Object.Instantiate(legacy, archive.transform, true);
                    copy.name = legacy.name;
                }
                PersistAssets(archive, "legacy_" + scene.name);
                string path = AssetDatabase.GenerateUniqueAssetPath(LegacyFolder + "/" + SafeName(scene.name) + "_" + suffix + ".prefab");
                var saved = PrefabUtility.SaveAsPrefabAsset(archive, path, out bool success);
                if (!success || !saved) throw new InvalidOperationException("Could not preserve legacy scene content: " + path);
                foreach (var legacy in legacyRoots) Undo.DestroyObjectImmediate(legacy);
                EditorSceneManager.MarkSceneDirty(scene);
            }
            finally
            {
                Object.DestroyImmediate(archive);
            }
        }

        /// <summary>Bake the palette and generated detail once; subsequent calls preserve artist material/mesh edits.</summary>
        public static void PersistPresentation(GameObject root, string assetStem)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Persist presentation outside Play mode.");
            var presentation = root.GetComponent<HousePresentation>();
            if (presentation && presentation.IsAuthored) return;
            if (!presentation)
            {
                HousePresentation.Apply(root, true);
                presentation = root.GetComponent<HousePresentation>();
            }
            PersistAssets(root, assetStem);
            presentation.MarkAuthored();
            EditorUtility.SetDirty(presentation);
            AssetDatabase.SaveAssets();
        }

        static void PersistAssets(GameObject root, string assetStem)
        {
            bool legacy = assetStem.StartsWith("legacy_", StringComparison.Ordinal);
            string materialFolder = legacy ? LegacyDependencyFolder + "/Materials" : MaterialFolder;
            string meshFolder = legacy ? LegacyDependencyFolder + "/Meshes" : MeshFolder;
            EnsureFolder(materialFolder);
            EnsureFolder(meshFolder);
            string stem = SafeName(assetStem);
            var meshes = new Dictionary<Mesh, Mesh>();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                if (!mesh || EditorUtility.IsPersistent(mesh)) continue;
                if (!meshes.TryGetValue(mesh, out var persistent))
                {
                    persistent = Object.Instantiate(mesh);
                    persistent.name = mesh.name;
                    string path = AssetDatabase.GenerateUniqueAssetPath(meshFolder + "/" + stem + "_" + SafeName(mesh.name) + ".asset");
                    AssetDatabase.CreateAsset(persistent, path);
                    meshes.Add(mesh, persistent);
                }
                filter.sharedMesh = persistent;
                EditorUtility.SetDirty(filter);
            }

            var block = new MaterialPropertyBlock();
            var slotBlock = new MaterialPropertyBlock();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                renderer.GetPropertyBlock(block);
                var materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    var source = materials[i];
                    if (!source) continue;
                    renderer.GetPropertyBlock(slotBlock, i);
                    if (EditorUtility.IsPersistent(source) && block.isEmpty && slotBlock.isEmpty) continue;
                    var copy = new Material(source);
                    ApplyProperties(copy, block);
                    ApplyProperties(copy, slotBlock);
                    string fingerprint = MaterialFingerprint(source, copy);
                    string path = materialFolder + "/" + stem + "_" + SafeName(source.name) + "_" + Hash128.Compute(fingerprint) + ".mat";
                    var persistent = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (persistent) Object.DestroyImmediate(copy);
                    else
                    {
                        copy.name = stem + " " + source.name;
                        AssetDatabase.CreateAsset(copy, path);
                        persistent = copy;
                    }
                    materials[i] = persistent;
                    renderer.SetPropertyBlock(null, i);
                    changed = true;
                }
                if (changed) renderer.sharedMaterials = materials;
                renderer.SetPropertyBlock(null);
                EditorUtility.SetDirty(renderer);
                block.Clear();
                slotBlock.Clear();
            }
        }

        static void ApplyProperties(Material material, MaterialPropertyBlock properties)
        {
            if (properties.HasColor(BaseColor) && material.HasProperty(BaseColor)) material.SetColor(BaseColor, properties.GetColor(BaseColor));
            if (properties.HasColor(LegacyColor) && material.HasProperty(LegacyColor)) material.SetColor(LegacyColor, properties.GetColor(LegacyColor));
            if (properties.HasFloat(Smoothness) && material.HasProperty(Smoothness)) material.SetFloat(Smoothness, properties.GetFloat(Smoothness));
        }

        static string MaterialFingerprint(Material source, Material material)
        {
            return AssetDatabase.GetAssetPath(source) + "|" + EditorJsonUtility.ToJson(material);
        }

        static string SafeName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "World";
            return new string(value.Select(c => char.IsLetterOrDigit(c) || c is '_' or '-' ? c : '_').ToArray());
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }
    }
}
