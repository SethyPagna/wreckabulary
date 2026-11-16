using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;

namespace Wreckabulary.EditorTools
{
    public static class BackdropLook
    {
        public const string ShaderPath = "Assets/_Project/Shaders/Backdrop.shader";
        public const string MaterialFolder = "Assets/_Project/Materials/Backdrop";
        public const string SkyPath = MaterialFolder + "/MapleMorningSky.mat";
        public const string ProfilePath = "Assets/_Project/Resources/Environment/MapleMorning.asset";
        public const string BaselineProfilePath = "Assets/_Project/Resources/Environment/SunlitHouse.asset";
        public const string AmbienceFolder = "Assets/_Project/Resources/Ambience";
        public const string ReceiptPath = "BuildNotes/surroundings/look/backdrop_look.json";
        public const float SaturationScale = .7f, GlowStrength = .55f, SkyThickness = .95f, SkyExposure = 1.5f;
        public static readonly Color SkyTint = new(.60f, .58f, .58f), Horizon = new(.78f, .89f, .99f);

        public readonly struct Slot
        {
            public readonly string Id, Family;
            public readonly float Value, Smoothness;
            public readonly bool Emissive;

            public Slot(string id, string family, float value, float smoothness, bool emissive = false)
            {
                Id = id; Family = family; Value = value; Smoothness = smoothness; Emissive = emissive;
            }
        }

        public static readonly Slot[] Slots =
        {
            new("bk_plaster", "cream", .91f, .10f),
            new("bk_brick", "wood", .71f, .10f),
            new("bk_stone", "ivory", .81f, .12f),
            new("bk_roof", "red", .66f, .18f),
            new("bk_wood", "wood_light", .72f, .14f),
            new("bk_paint", "white", 1f, .36f),
            new("bk_metal", "dark", .34f, .38f),
            new("bk_glass_dark", "glass", .48f, .70f),
            new("bk_glass_lit", "glow", 1f, .50f, true),
            new("bk_leaf", "leaf", .63f, .08f),
            new("bk_bark", "wood_dark", .48f, .08f),
            new("bk_grass", "green", .67f, .06f),
            new("bk_road", "metal", .45f, .12f),
            new("bk_line", "ceramic", .93f, .12f),
            new("bk_card", "white", 1f, 0f),
        };

        [Serializable] sealed class MaterialRow { public string slot, family, path, hex, emissionHex; public float smoothness, classicSaturation, saturation, value; }
        [Serializable] sealed class ClipRow { public string path, loadType, compression; public bool mono; public float seconds; public int frequency, channels; }
        [Serializable] sealed class Receipt
        {
            public string utc, shader, profile, sky;
            public float sunElevation, sunYaw, sunIntensity, fogStart, fogEnd, exposure, contrast, saturation, bloom, skyThickness, skyExposure;
            public string sunHex, horizonHex, skyTintHex, capHex;
            public List<MaterialRow> materials = new();
            public List<ClipRow> clips = new();
        }

        [MenuItem("Wreckabulary/Art/Backdrop/Build look")]
        public static void All()
        {
            var receipt = new Receipt { utc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), shader = ShaderPath };
            BuildMaterials(receipt);
            BuildLight(receipt);
            ImportAudio(receipt);
            AssetDatabase.SaveAssets();
            Write(receipt);
            Debug.Log($"[Wreckabulary] Backdrop look: {receipt.materials.Count} materials, profile {ProfilePath}, {receipt.clips.Count} ambience clips, receipt {ReceiptPath}");
        }

        public static void Materials()
        {
            var receipt = new Receipt { utc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), shader = ShaderPath };
            BuildMaterials(receipt);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Wreckabulary] Backdrop materials: {receipt.materials.Count} in {MaterialFolder}");
        }

        public static void Light()
        {
            var receipt = new Receipt();
            BuildLight(receipt);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Wreckabulary] Backdrop light: {ProfilePath} sun {receipt.sunElevation} deg, fog {receipt.fogStart}-{receipt.fogEnd} m");
        }

        public static void Audio()
        {
            var receipt = new Receipt();
            ImportAudio(receipt);
            Debug.Log($"[Wreckabulary] Backdrop ambience: {receipt.clips.Count} clips under {AmbienceFolder}");
        }

        public static Shader LoadShader()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            if (!shader) throw new FileNotFoundException("The backdrop shader is missing", ShaderPath);
            if (shader.name != BackdropShader.ShaderName) throw new InvalidOperationException($"{ShaderPath} is named {shader.name}, expected {BackdropShader.ShaderName}");
            if (ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException($"{BackdropShader.ShaderName} has compile errors: " + string.Join("; ", ShaderUtil.GetShaderMessages(shader).Select(m => m.message)));
            return shader;
        }

        public static Color SlotColor(Slot slot, Color classicLinear, out float classicSaturation, out float saturation)
        {
            Color.RGBToHSV(classicLinear.gamma, out float hue, out classicSaturation, out _);
            saturation = classicSaturation * SaturationScale;
            var colour = Color.HSVToRGB(hue, saturation, slot.Value);
            colour.a = 1f;
            return colour;
        }

        static void BuildMaterials(Receipt receipt)
        {
            var shader = LoadShader();
            MaterialLibraryBuilder.EnsureFolder(MaterialFolder);
            var classic = ImportedArtSettings.ReadMaterials().Where(spec => spec.Skin == "Classic" && spec.Family != null)
                .GroupBy(spec => spec.Family).ToDictionary(group => group.Key, group => group.First());
            foreach (var slot in Slots)
            {
                if (!classic.TryGetValue(slot.Family, out var spec))
                    throw new InvalidOperationException($"materials.json has no Classic {slot.Family} for {slot.Id}");
                var colour = SlotColor(slot, spec.LinearBaseColor, out float classicSaturation, out float saturation);
                var emission = slot.Emissive ? (colour.linear * GlowStrength).gamma : Color.black;
                emission.a = 1f;
                string path = $"{MaterialFolder}/{slot.Id}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool created = !material;
                if (created) material = new Material(shader);
                else if (material.shader != shader) material.shader = shader;
                material.name = slot.Id;
                material.SetColor("_BaseColor", colour);
                material.SetColor("_EmissionColor", emission);
                material.SetFloat("_Smoothness", slot.Smoothness);
                material.globalIlluminationFlags = slot.Emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                material.enableInstancing = false;
                if (created) AssetDatabase.CreateAsset(material, path);
                else EditorUtility.SetDirty(material);
                receipt.materials.Add(new MaterialRow
                {
                    slot = slot.Id, family = slot.Family, path = path, hex = "#" + ColorUtility.ToHtmlStringRGB(colour),
                    emissionHex = "#" + ColorUtility.ToHtmlStringRGB(emission), smoothness = slot.Smoothness,
                    classicSaturation = classicSaturation, saturation = saturation, value = slot.Value
                });
            }
        }

        static void BuildLight(Receipt receipt)
        {
            var baseline = AssetDatabase.LoadAssetAtPath<EnvironmentLightingProfile>(BaselineProfilePath);
            if (!baseline) throw new FileNotFoundException("The SunlitHouse baseline profile is missing", BaselineProfilePath);
            var sky = BuildSky(baseline.Skybox);
            var profile = AssetDatabase.LoadAssetAtPath<EnvironmentLightingProfile>(ProfilePath);
            bool created = !profile;
            if (created) profile = ScriptableObject.CreateInstance<EnvironmentLightingProfile>();
            profile.name = Path.GetFileNameWithoutExtension(ProfilePath);
            profile.Skybox = sky;
            profile.SunColor = new Color(1f, .93f, .81f);
            profile.SunIntensity = baseline.SunIntensity;
            profile.SunEuler = new Vector3(46f, -38f, 0f);
            profile.FillColor = new Color(.68f, .82f, 1f);
            profile.FillIntensity = baseline.FillIntensity;
            profile.FillEuler = baseline.FillEuler;
            profile.AmbientSky = new Color(.66f, .77f, .90f);
            profile.AmbientEquator = new Color(.54f, .56f, .53f);
            profile.AmbientGround = new Color(.34f, .33f, .27f);
            profile.HorizonColor = Horizon;
            profile.FogStart = 60f;
            profile.FogEnd = 280f;
            profile.Exposure = baseline.Exposure;
            profile.Contrast = baseline.Contrast;
            profile.Saturation = baseline.Saturation;
            profile.Bloom = baseline.Bloom;
            profile.HighLightBudget = baseline.HighLightBudget;
            profile.SelectionInterval = baseline.SelectionInterval;
            if (created) AssetDatabase.CreateAsset(profile, ProfilePath);
            else EditorUtility.SetDirty(profile);
            receipt.profile = ProfilePath;
            receipt.sky = SkyPath;
            receipt.sunElevation = profile.SunEuler.x;
            receipt.sunYaw = profile.SunEuler.y;
            receipt.sunIntensity = profile.SunIntensity;
            receipt.sunHex = "#" + ColorUtility.ToHtmlStringRGB(profile.SunColor);
            receipt.horizonHex = "#" + ColorUtility.ToHtmlStringRGB(profile.HorizonColor);
            receipt.fogStart = profile.FogStart;
            receipt.fogEnd = profile.FogEnd;
            receipt.exposure = profile.Exposure;
            receipt.contrast = profile.Contrast;
            receipt.saturation = profile.Saturation;
            receipt.bloom = profile.Bloom;
            receipt.skyTintHex = "#" + ColorUtility.ToHtmlStringRGB(SkyTint);
            receipt.skyThickness = SkyThickness;
            receipt.skyExposure = SkyExposure;
            receipt.capHex = "#" + ColorUtility.ToHtmlStringRGB(BackdropShader.CapColor);
        }

        static Material BuildSky(Material daylight)
        {
            var shader = Shader.Find("Skybox/Procedural");
            if (!shader) throw new InvalidOperationException("Skybox/Procedural is missing");
            MaterialLibraryBuilder.EnsureFolder(MaterialFolder);
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            bool created = !sky;
            if (created) sky = daylight && daylight.shader == shader ? new Material(daylight) : new Material(shader);
            else if (sky.shader != shader) sky.shader = shader;
            sky.name = Path.GetFileNameWithoutExtension(SkyPath);
            sky.SetColor("_SkyTint", SkyTint);
            sky.SetColor("_GroundColor", new Color(.63f, .67f, .57f));
            sky.SetFloat("_AtmosphereThickness", SkyThickness);
            sky.SetFloat("_Exposure", SkyExposure);
            sky.SetFloat("_SunSize", .05f);
            sky.SetFloat("_SunSizeConvergence", 5f);
            sky.SetFloat("_SunDisk", 2f);
            sky.DisableKeyword("_SUNDISK_NONE");
            sky.DisableKeyword("_SUNDISK_SIMPLE");
            sky.EnableKeyword("_SUNDISK_HIGH_QUALITY");
            if (created) AssetDatabase.CreateAsset(sky, SkyPath);
            else EditorUtility.SetDirty(sky);
            return sky;
        }

        static void ImportAudio(Receipt receipt)
        {
            if (!AssetDatabase.IsValidFolder(AmbienceFolder)) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (!AssetDatabase.IsValidFolder(AmbienceFolder)) throw new DirectoryNotFoundException($"{AmbienceFolder} is missing; run Tools/AssetPipeline/synth_ambience.py first");
            var paths = Directory.GetFiles(AmbienceFolder, "*.wav", SearchOption.AllDirectories).Select(path => path.Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal).ToArray();
            if (paths.Length == 0) throw new FileNotFoundException($"No ambience clips under {AmbienceFolder}; run Tools/AssetPipeline/synth_ambience.py first");
            foreach (string path in paths) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            foreach (string path in paths)
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (!importer || !clip) throw new InvalidOperationException($"{path} did not import as an audio clip");
                var settings = importer.defaultSampleSettings;
                if (!importer.forceToMono || settings.compressionFormat != AudioCompressionFormat.Vorbis || settings.loadType != BackdropAmbienceImport.LoadType(path))
                    throw new InvalidOperationException($"{path} kept the wrong import settings ({settings.loadType}, {settings.compressionFormat}, mono {importer.forceToMono})");
                receipt.clips.Add(new ClipRow
                {
                    path = path, loadType = settings.loadType.ToString(), compression = settings.compressionFormat.ToString(), mono = importer.forceToMono,
                    seconds = clip.length, frequency = clip.frequency, channels = clip.channels
                });
            }
        }

        static void Write(Receipt receipt)
        {
            string full = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReceiptPath));
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, JsonUtility.ToJson(receipt, true));
        }
    }

    public sealed class BackdropAmbienceImport : AssetPostprocessor
    {
        public static bool Applies(string path) => path.StartsWith(BackdropLook.AmbienceFolder + "/", StringComparison.Ordinal);

        public static AudioClipLoadType LoadType(string path) =>
            BackdropAmbience.IsLoop(Path.GetFileNameWithoutExtension(path)) ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;

        void OnPreprocessAudio()
        {
            if (!Applies(assetPath)) return;
            var importer = (AudioImporter)assetImporter;
            var loadType = LoadType(assetPath);
            bool bed = loadType == AudioClipLoadType.Streaming;
            importer.forceToMono = true;
            importer.loadInBackground = true;
            var settings = importer.defaultSampleSettings;
            settings.loadType = loadType;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = bed ? .45f : .6f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
        }
    }
}
