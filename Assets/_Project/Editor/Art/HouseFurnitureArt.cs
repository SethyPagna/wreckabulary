using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTools
{
    public static class HouseFurnitureArt
    {
        const string CopyFolder = "Assets/_Project/Worlds/Generated/Materials";
        const string WorldFolder = "Assets/_Project/Resources/Worlds";
        static readonly Regex HouseCopy = new Regex(@"^(?<stem>.+_v(?<version>\d+))_furniture_(?<family>.+)_Classic_[0-9a-f]{32}$",
            RegexOptions.CultureInvariant);

        public static void Run()
        {
            Materials();
            Remap();
            SyncHouses();
        }

        public static void Inspect()
        {
            var library = Library();
            var stale = ImportedArtSettings.ReadMaterials()
                .Where(spec => !library.Find(spec.Name) || !Same(library.Find(spec.Name).GetColor("_BaseColor").linear, spec.LinearBaseColor, 0.01f))
                .Select(spec => spec.Name).ToList();
            Report("HOUSE_FURNITURE_INSPECT", ("libraryStale", stale), ("houseCopies", SyncCopies(library, false)),
                ("remaps", RemapModels(library, false)), ("houses", SyncWorlds(library, false)));
        }

        public static void Materials()
        {
            var library = MaterialLibraryBuilder.Build();
            int seeds = TactileMaterialBuildSupport.Generate(library);
            var copies = SyncCopies(library, true);
            AssetDatabase.SaveAssets();
            Report("HOUSE_FURNITURE_MATERIALS", ("library", new List<string> { library.Materials.Count.ToString() }),
                ("seeds", new List<string> { seeds.ToString() }), ("houseCopies", copies));
        }

        public static void Remap()
        {
            var changes = RemapModels(Library(), true);
            AssetDatabase.SaveAssets();
            Report("HOUSE_FURNITURE_REMAP", ("remaps", changes));
        }

        public static void SyncHouses()
        {
            var changes = SyncWorlds(Library(), true);
            AssetDatabase.SaveAssets();
            Report("HOUSE_FURNITURE_HOUSES", ("houses", changes));
        }

        static MaterialLibrary Library()
        {
            var library = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(ImportedArtSettings.LibraryAssetPath);
            if (!library) throw new InvalidOperationException("The MaterialLibrary is missing; run Wreckabulary > Art > Set Up Imported Art.");
            return library;
        }

        static List<string> SyncCopies(MaterialLibrary library, bool apply)
        {
            string version = AuthoredHouse.CurrentContentVersion.ToString();
            var changes = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { CopyFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var match = HouseCopy.Match(Path.GetFileNameWithoutExtension(path));
                if (!match.Success || match.Groups["version"].Value != version) continue;
                var source = library.Find(match.Groups["family"].Value + "_Classic");
                var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!source || !copy || !copy.HasProperty("_BaseColor")) continue;
                var colour = source.GetColor("_BaseColor");
                if (Same(copy.GetColor("_BaseColor"), colour)) continue;
                changes.Add(Path.GetFileName(path));
                if (!apply) continue;
                copy.SetColor("_BaseColor", colour);
                if (copy.HasProperty("_Color")) copy.SetColor("_Color", colour);
                EditorUtility.SetDirty(copy);
            }
            return changes;
        }

        static List<string> RemapModels(MaterialLibrary library, bool apply)
        {
            var changes = new List<string>();
            if (apply) AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { ImportedArtSettings.Root.TrimEnd('/') }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter importer)) continue;
                    var map = importer.GetExternalObjectMap();
                    var added = new List<string>();
                    foreach (string name in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>().Select(m => m.name).Distinct())
                    {
                        var target = library.Find(name);
                        if (!target || !AssetDatabase.Contains(target)) continue;
                        var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), name);
                        if (map.TryGetValue(id, out var existing) && existing == target) continue;
                        added.Add(name);
                        if (apply) importer.AddRemap(id, target);
                    }
                    if (added.Count == 0) continue;
                    changes.Add(path + ": " + string.Join(", ", added));
                    if (apply) importer.SaveAndReimport();
                }
            }
            finally
            {
                if (apply) AssetDatabase.StopAssetEditing();
            }
            return changes;
        }

        static List<string> SyncWorlds(MaterialLibrary library, bool apply)
        {
            var changes = new List<string>();
            var sources = new Dictionary<Mesh, Material[]>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { WorldFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                    var copies = HouseCopies(renderers);
                    var touched = new List<string>();
                    foreach (var renderer in renderers)
                    {
                        var filter = renderer.GetComponent<MeshFilter>();
                        var wanted = filter && filter.sharedMesh ? SourceMaterials(filter.sharedMesh, sources) : null;
                        if (wanted == null) continue;
                        var next = Matched(renderer.sharedMaterials, wanted, copies, library);
                        if (next == null) continue;
                        touched.Add(renderer.name);
                        if (apply) renderer.sharedMaterials = next;
                    }
                    if (touched.Count == 0) continue;
                    changes.Add(path + ": " + touched.Count + " renderers (" + string.Join(", ", touched.Distinct().OrderBy(n => n, StringComparer.Ordinal)) + ")");
                    if (apply) PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
            return changes;
        }

        static Dictionary<string, Material> HouseCopies(IEnumerable<MeshRenderer> renderers)
        {
            string current = AuthoredHouse.CurrentContentVersion.ToString();
            var copies = new Dictionary<string, Material>(StringComparer.Ordinal);
            foreach (var material in renderers.SelectMany(r => r.sharedMaterials).Where(m => m))
            {
                var match = HouseCopy.Match(material.name);
                if (!match.Success) continue;
                string family = match.Groups["family"].Value;
                bool isCurrent = match.Groups["version"].Value == current;
                if (!copies.TryGetValue(family, out var known) || isCurrent && HouseCopy.Match(known.name).Groups["version"].Value != current)
                    copies[family] = material;
            }
            return copies;
        }

        static Material[] SourceMaterials(Mesh mesh, Dictionary<Mesh, Material[]> cache)
        {
            if (cache.TryGetValue(mesh, out var known)) return known;
            Material[] found = null;
            string path = AssetDatabase.GetAssetPath(mesh);
            if (ImportedArtSettings.IsImportedArt(path) && AssetImporter.GetAtPath(path) is ModelImporter)
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var filter = model ? model.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh == mesh) : null;
                var renderer = filter ? filter.GetComponent<MeshRenderer>() : null;
                if (renderer) found = renderer.sharedMaterials;
            }
            cache[mesh] = found;
            return found;
        }

        static Material[] Matched(Material[] current, Material[] wanted, Dictionary<string, Material> copies, MaterialLibrary library)
        {
            var next = current.ToList();
            bool changed = false;
            for (int i = 0; i < wanted.Length; i++)
            {
                string family = Family(wanted[i]);
                string present = i < next.Count ? Family(next[i]) : family + "?";
                if (family == null || present == null || present == family) continue;
                Material target;
                if (copies.TryGetValue(family, out var copy)) target = copy;
                else
                {
                    var shared = library.Find(family + "_Classic");
                    target = shared ? shared : wanted[i];
                }
                while (next.Count <= i) next.Add(null);
                next[i] = target;
                changed = true;
            }
            return changed ? next.ToArray() : null;
        }

        static string Family(Material material)
        {
            if (!material) return null;
            var match = HouseCopy.Match(material.name);
            return match.Success ? match.Groups["family"].Value : MaterialLibrary.FamilyOf(material.name);
        }

        static bool Same(Color a, Color b, float tolerance = 1e-4f) =>
            Mathf.Abs(a.r - b.r) < tolerance && Mathf.Abs(a.g - b.g) < tolerance && Mathf.Abs(a.b - b.b) < tolerance;

        static void Report(string label, params (string key, List<string> values)[] fields)
        {
            string body = string.Join(", ", fields.Select(field => "\"" + field.key + "\": [" +
                string.Join(", ", field.values.Select(value => "\"" + value.Replace("\\", "/").Replace("\"", "'") + "\"")) + "]"));
            Debug.Log(label + " {" + body + "}");
        }
    }
}
