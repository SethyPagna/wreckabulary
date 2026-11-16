using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTools
{
    public static class MaterialLibraryBuilder
    {
        public static MaterialLibrary Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The URP Lit shader wasn't found. Is URP the active render pipeline?");
            EnsureFolder(ImportedArtSettings.LibraryFolder);
            EnsureFolder(System.IO.Path.GetDirectoryName(ImportedArtSettings.LibraryAssetPath).Replace('\\', '/'));

            var specs = ImportedArtSettings.ReadMaterials();
            var seasonal = ReadSeasonal();
            ConfigurePatterns(specs.Concat(seasonal.Select(entry => entry.spec)));
            var built = new List<Material>();
            foreach (var spec in specs) built.Add(BuildMaterial(spec, shader));
            foreach (var (spec, folder) in seasonal) BuildMaterial(spec, shader, folder);

            var expected = new HashSet<string>(specs.Select(s => s.Name));
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { ImportedArtSettings.LibraryFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.StartsWith(PatternFolder + "/", StringComparison.Ordinal)) continue;
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!expected.Contains(name))
                    Debug.LogWarning($"{path} is no longer in materials.json. Delete it once nothing uses it.");
            }

            var library = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(ImportedArtSettings.LibraryAssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<MaterialLibrary>();
                AssetDatabase.CreateAsset(library, ImportedArtSettings.LibraryAssetPath);
            }
            library.Set(built.OrderBy(m => m.name, StringComparer.Ordinal));
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            return library;
        }

        public const string PatternFolder = ImportedArtSettings.LibraryFolder + "/Patterns";

        public static List<(ImportedArtSettings.MaterialSpec spec, string folder)> ReadSeasonal()
        {
            var root = Json.Parse(File.ReadAllText(ImportedArtSettings.MaterialsPath), Path.GetFileName(ImportedArtSettings.MaterialsPath));
            var specs = new List<(ImportedArtSettings.MaterialSpec, string)>();
            if (!root.Has("seasonal")) return specs;
            foreach (var m in root["seasonal"].Items)
            {
                var c = m["baseColor"].Floats(4);
                var e = m["emissive"].Floats(3);
                specs.Add((new ImportedArtSettings.MaterialSpec
                {
                    Name = m["name"].String(),
                    LinearBaseColor = new Color(c[0], c[1], c[2], c[3]),
                    Metallic = m["metallic"].Float(),
                    Roughness = m["roughness"].Float(),
                    LinearEmissive = new Color(e[0], e[1], e[2], 1f),
                    AlphaMode = m["alphaMode"].String("OPAQUE"),
                    AlphaCutoff = m["alphaCutoff"].Float(0.5f),
                    DoubleSided = m["doubleSided"].Bool(false),
                    BaseMap = m["baseMap"].String(null),
                    NormalMap = m["normalMap"].String(null),
                    EmissionMap = m["emissionMap"].String(null),
                    Family = m["family"].String(null),
                    Skin = m["skin"].String(null),
                }, m["folder"].String()));
            }
            return specs;
        }

        public static void ConfigurePatterns(IEnumerable<ImportedArtSettings.MaterialSpec> specs)
        {
            var paths = specs.SelectMany(s => new[] { s.BaseMap, s.EmissionMap })
                .Where(path => path != null && path.StartsWith(PatternFolder + "/", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                if (!File.Exists(path)) throw new InvalidOperationException(path + " is missing; run Tools/AssetPipeline/build_finish_patterns.py.");
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                {
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                    importer = (TextureImporter)AssetImporter.GetAtPath(path);
                }
                if (IsPattern(importer)) continue;
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.mipmapEnabled = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.maxTextureSize = 512;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }
        }

        public static bool IsPattern(TextureImporter importer) => importer && importer.textureType == TextureImporterType.Default &&
            importer.sRGBTexture && importer.mipmapEnabled && importer.wrapMode == TextureWrapMode.Repeat && importer.filterMode == FilterMode.Trilinear &&
            importer.anisoLevel == 4 && importer.maxTextureSize == 512 && importer.textureCompression == TextureImporterCompression.CompressedHQ &&
            importer.alphaSource == TextureImporterAlphaSource.None;

        static Material BuildMaterial(ImportedArtSettings.MaterialSpec s, Shader lit, string folder = ImportedArtSettings.LibraryFolder)
        {
            EnsureFolder(folder);
            string path = $"{folder}/{s.Name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(lit) { name = s.Name };
            else if (mat.shader != lit) mat.shader = lit;

            ApplySpecification(mat, s);

            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        public static void ApplySpecification(Material mat, ImportedArtSettings.MaterialSpec s)
        {
            bool neutralTint = s.Name is "boots_color" or "glasses_frame" or "satchel_straps";
            var baseMap = Load(neutralTint ? ImportedArtSettings.Root + "Textures/ivory_vinyl_BaseColor.png" : s.BaseMap);
            mat.SetColor("_BaseColor", neutralTint ? Color.white : s.LinearBaseColor.gamma);
            mat.SetFloat("_Metallic", s.Metallic);
            mat.SetFloat("_Smoothness", Mathf.Clamp01(1f - s.Roughness));
            mat.SetTexture("_BaseMap", baseMap);
            if (neutralTint)
            {
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseMap);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", Color.white);
            }
            mat.SetTexture("_BumpMap", Load(s.NormalMap));
            mat.SetFloat("_BumpScale", 1f);

            bool emits = s.LinearEmissive.maxColorComponent > 0f || s.EmissionMap != null;
            mat.SetColor("_EmissionColor", emits ? s.LinearEmissive.gamma : Color.black);
            mat.SetTexture("_EmissionMap", Load(s.EmissionMap));
            mat.globalIlluminationFlags = emits ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

            bool transparent = s.AlphaMode == "BLEND";
            bool cutout = s.AlphaMode == "MASK";
            mat.SetFloat("_Surface", transparent ? 1f : 0f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_AlphaClip", cutout ? 1f : 0f);
            mat.SetFloat("_Cutoff", s.AlphaCutoff);
            mat.SetFloat("_Cull", s.DoubleSided ? (float)CullMode.Off : (float)CullMode.Back);

            BaseShaderGUI.SetMaterialKeywords(mat, LitGUI.SetMaterialKeywords);
        }

        static Texture2D Load(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                var main = AssetDatabase.LoadMainAssetAtPath(path);
                throw new InvalidOperationException(
                    $"materials.json names {path}, which didn't load as a texture (guid '{AssetDatabase.AssetPathToGUID(path)}', " +
                    $"main type {AssetDatabase.GetMainAssetTypeAtPath(path)}, main asset {(main == null ? "none" : main.GetType().Name)}, " +
                    $"on disk {System.IO.File.Exists(path)}). Rerun the Blender pipeline if it's missing.");
            }
            return tex;
        }

        internal static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
