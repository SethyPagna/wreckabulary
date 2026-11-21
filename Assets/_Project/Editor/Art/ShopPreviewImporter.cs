using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class ShopPreviewImporter
    {
        public const string SheetPath = "BuildNotes/w20d/portraits-sheet.png", BackupFolder = "BuildNotes/w20d/portraits-before";
        const string Folder = "Assets/_Project/Resources/UI/Shop/Looks", Extension = ".png";
        const string AvatarKey = "Avatar/Avatar", IdleClip = "Idle", OcclusionKeyword = "_SCREEN_SPACE_OCCLUSION", SettleMethod = "LateUpdate";
        const int Width = 256, Height = 320, Renders = 2, MinPixels = 1000, ImportLimit = 512, Percent = 100;
        const float Yaw = 160f, Margin = 1.12f, CameraDistance = 5f, NearPlane = .01f, FarPlane = 30f;
        const float KeyIntensity = .8f, FillIntensity = .4f, Ambient = .45f;
        const int SheetGap = 8, LabelScale = 2, LabelRoom = 16;
        static readonly Color Background = new Color(.97f, .94f, .85f, 0f);
        static readonly Quaternion KeyTurn = Quaternion.Euler(25f, -25f, 0f), FillTurn = Quaternion.Euler(10f, 35f, 0f);

        sealed class Portrait
        {
            public string Id, Asset, Outfit;
            public byte[] Png, Previous;
            public Color32[] Pixels;
            public int Transparent, Opaque;
            public bool Written;
        }

        [MenuItem("Wreckabulary/Art/Rebuild shop portraits")]
        public static void Rebuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !GraphicsSettings.currentRenderPipeline)
                throw new InvalidOperationException("Rebuild in Edit Mode with the active render pipeline and a graphics device.");
            var library = ModelLibrary.Load();
            var idle = library ? library.FindClip(AvatarKey, IdleClip) : null;
            if (!library || !library.Find(AvatarKey) || !idle) throw new InvalidOperationException("The avatar and its Idle clip are required.");
            if (!WardrobeRig.Art) throw new InvalidOperationException("Resources/" + WardrobeRig.ArtPath + " is missing; run Tools/AssetPipeline/build_signature_pieces.py first.");
            var wardrobe = GameConfig.Current.Wardrobe;
            var portraits = CosmeticBundles.All.Select(look => new Portrait { Id = look.Id, Asset = Folder + "/" + look.Id + Extension }).ToList();
            foreach (var portrait in portraits)
                if (File.Exists(Full(portrait.Asset))) portrait.Previous = File.ReadAllBytes(Full(portrait.Asset));
            CaptureAll(portraits, wardrobe, idle);
            RequireDistinct(portraits);
            Backup(portraits);
            foreach (var portrait in portraits) Write(portrait);
            foreach (var portrait in portraits) Import(portrait.Asset);
            string sheet = WriteSheet(portraits);
            Debug.Log("[Wreckabulary] SHOP_PORTRAITS: count=" + portraits.Count + "; written=" + portraits.Count(portrait => portrait.Written)
                + "; character=code-built Cute avatar; size=" + Width + "x" + Height + "; folder=" + Folder + "; before=" + BackupFolder
                + "; sheet=" + sheet + "; pending owner review");
        }

        public static void Settle(GameObject model)
        {
            var face = model.GetComponentInChildren<CuteFace>(true);
            if (!face) throw new InvalidOperationException("The avatar has no code-built face, so this would bake the old character.");
            RunLateUpdate(face);
            foreach (var hair in model.GetComponentsInChildren<CuteHair>(false)) RunLateUpdate(hair);
        }

        static void RunLateUpdate(MonoBehaviour behaviour)
        {
            var method = behaviour.GetType().GetMethod(SettleMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            if (method == null) throw new InvalidOperationException(behaviour.GetType().Name + " no longer settles in " + SettleMethod + ".");
            method.Invoke(behaviour, null);
        }

        static void CaptureAll(List<Portrait> portraits, WardrobeCatalogue wardrobe, AnimationClip idle)
        {
            var active = RenderTexture.active;
            bool asyncShaders = EditorSettings.asyncShaderCompilation, occlusion = Shader.IsKeywordEnabled(OcclusionKeyword);
            bool asyncVariants = ShaderUtil.allowAsyncCompilation;
            try
            {
                if (asyncShaders) EditorSettings.asyncShaderCompilation = false;
                ShaderUtil.allowAsyncCompilation = false;
                if (portraits.Count > 0) Capture(new Portrait { Id = portraits[0].Id }, wardrobe, idle);
                foreach (var portrait in portraits) Capture(portrait, wardrobe, idle);
            }
            finally
            {
                RenderTexture.active = active;
                ShaderUtil.allowAsyncCompilation = asyncVariants;
                if (asyncShaders) EditorSettings.asyncShaderCompilation = true;
                if (occlusion) Shader.EnableKeyword(OcclusionKeyword); else Shader.DisableKeyword(OcclusionKeyword);
            }
        }

        static void Capture(Portrait portrait, WardrobeCatalogue wardrobe, AnimationClip idle)
        {
            var preview = new PreviewRenderUtility();
            GameObject holder = null;
            Texture2D image = null;
            bool begun = false;
            try
            {
                holder = new GameObject("Shop portrait " + portrait.Id);
                preview.AddSingleGO(holder);
                var model = ModelVisual.Spawn(AvatarKey, holder.transform);
                if (!model) throw new InvalidOperationException("Avatar spawn failed.");
                idle.SampleAnimation(model, 0f);
                var look = CosmeticBundles.Find(portrait.Id)?.Apply(wardrobe.Default, wardrobe)
                    ?? throw new InvalidOperationException("The look " + portrait.Id + " does not fit the wardrobe.");
                var worn = PlayerAppearance.Dress(model.GetComponentsInChildren<SkinnedMeshRenderer>(true), look);
                portrait.Outfit = worn.Serialize();
                holder.transform.rotation = Quaternion.Euler(0f, Yaw, 0f);
                Settle(model);
                Aim(preview, Body(model, portrait.Id));
                Shader.DisableKeyword(OcclusionKeyword);
                preview.BeginStaticPreview(new Rect(0, 0, Width, Height));
                begun = true;
                for (int pass = 0; pass < Renders; pass++) preview.Render(true);
                image = ReadRgba(preview.camera.targetTexture);
                Describe(portrait, image);
            }
            finally
            {
                if (begun)
                {
                    var pending = preview.EndStaticPreview();
                    if (pending) Object.DestroyImmediate(pending);
                }
                if (image) Object.DestroyImmediate(image);
                if (holder) Object.DestroyImmediate(holder);
                preview.Cleanup();
            }
        }

        static Bounds Body(GameObject model, string id)
        {
            Bounds? all = null;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer.forceRenderingOff || !renderer.gameObject.activeInHierarchy) continue;
                if (renderer.sharedMaterials.Any(material => material && material.shader && !material.shader.isSupported))
                    throw new InvalidOperationException("A material on " + renderer.name + " cannot be drawn for the " + id + " portrait.");
                var joined = all ?? renderer.bounds;
                joined.Encapsulate(renderer.bounds);
                all = joined;
            }
            var body = all ?? throw new InvalidOperationException("Nothing visible to capture for " + id + ".");
            if (!(body.size.sqrMagnitude > 0f) || float.IsInfinity(body.size.sqrMagnitude)) throw new InvalidOperationException("The " + id + " portrait has nothing to frame.");
            return body;
        }

        static void Aim(PreviewRenderUtility preview, Bounds body)
        {
            var camera = preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(body.extents.y, body.extents.x * Height / Width) * Margin;
            camera.transform.SetPositionAndRotation(body.center + Vector3.back * CameraDistance, Quaternion.identity);
            camera.nearClipPlane = NearPlane;
            camera.farClipPlane = FarPlane;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.allowHDR = false;
            preview.lights[0].color = preview.lights[1].color = Color.white;
            preview.lights[0].intensity = KeyIntensity;
            preview.lights[0].transform.rotation = KeyTurn;
            preview.lights[1].intensity = FillIntensity;
            preview.lights[1].transform.rotation = FillTurn;
            preview.ambientColor = new Color(Ambient, Ambient, Ambient);
        }

        static Texture2D ReadRgba(RenderTexture source)
        {
            if (!source || !GraphicsFormatUtility.HasAlphaChannel(source.graphicsFormat))
                throw new InvalidOperationException("The preview target has no alpha channel.");
            RenderTexture encoded = null;
            Texture2D copy = null;
            var active = RenderTexture.active;
            bool srgbWrite = GL.sRGBWrite;
            try
            {
                encoded = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { hideFlags = HideFlags.HideAndDontSave, antiAliasing = 1 };
                if (!encoded.Create() || !GraphicsFormatUtility.HasAlphaChannel(encoded.graphicsFormat))
                    throw new InvalidOperationException("Could not create an sRGB output target with alpha.");
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, encoded);
                RenderTexture.active = encoded;
                copy = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
                copy.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                copy.Apply(false, false);
                var output = copy;
                copy = null;
                return output;
            }
            finally
            {
                RenderTexture.active = active;
                GL.sRGBWrite = srgbWrite;
                if (copy) Object.DestroyImmediate(copy);
                if (encoded)
                {
                    encoded.Release();
                    Object.DestroyImmediate(encoded);
                }
            }
        }

        static void Describe(Portrait portrait, Texture2D image)
        {
            portrait.Pixels = image.GetPixels32();
            portrait.Transparent = portrait.Opaque = 0;
            foreach (var pixel in portrait.Pixels)
            {
                if (pixel.a == byte.MinValue) portrait.Transparent++;
                else if (pixel.a == byte.MaxValue) portrait.Opaque++;
            }
            if (portrait.Opaque < MinPixels) throw new InvalidOperationException("The portrait for " + portrait.Id + " is empty; nothing was written.");
            if (portrait.Transparent < MinPixels) throw new InvalidOperationException("The portrait for " + portrait.Id + " lost its transparent background; nothing was written.");
            portrait.Png = image.EncodeToPNG();
        }

        static void RequireDistinct(List<Portrait> portraits)
        {
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            var same = new List<string>();
            foreach (var portrait in portraits)
            {
                string key = Hash128.Compute(portrait.Png).ToString();
                if (seen.TryGetValue(key, out string first)) same.Add(portrait.Id + "=" + first);
                else seen[key] = portrait.Id;
            }
            if (same.Count > 0) throw new InvalidOperationException("Portraits came out identical (" + string.Join(", ", same) + "); nothing was written.");
        }

        static void Backup(List<Portrait> portraits)
        {
            string folder = Full(BackupFolder);
            Directory.CreateDirectory(folder);
            foreach (var portrait in portraits)
            {
                string file = Path.Combine(folder, portrait.Id + Extension);
                if (portrait.Previous != null && !File.Exists(file)) File.WriteAllBytes(file, portrait.Previous);
            }
        }

        static void Write(Portrait portrait)
        {
            portrait.Written = portrait.Previous == null || !portrait.Previous.SequenceEqual(portrait.Png);
            if (portrait.Written) File.WriteAllBytes(Full(portrait.Asset), portrait.Png);
            Debug.Log("[Wreckabulary] SHOP_PORTRAIT: " + portrait.Asset + "; bytes=" + portrait.Png.Length + "; transparent="
                + portrait.Transparent * Percent / (Width * Height) + "%; opaque=" + portrait.Opaque * Percent / (Width * Height) + "%; outfit=" + portrait.Outfit
                + "; " + (portrait.Written ? "written" : "unchanged"));
        }

        static void Import(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new IOException("No texture importer for " + path);
            string before = EditorJsonUtility.ToJson(importer);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.sRGBTexture = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = ImportLimit;
            importer.npotScale = TextureImporterNPOTScale.None;
            if (EditorJsonUtility.ToJson(importer) != before) importer.SaveAndReimport();
            var image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!image || image.width != Width || image.height != Height || !GraphicsFormatUtility.HasAlphaChannel(image.graphicsFormat))
                throw new InvalidOperationException("The portrait did not import at " + Width + "x" + Height + " with alpha: " + path);
        }

        static string WriteSheet(List<Portrait> portraits)
        {
            try
            {
                int width = SheetGap + portraits.Count * (Width + SheetGap), band = LabelRoom + Height + SheetGap;
                Color32 paper = new Color(Background.r, Background.g, Background.b, 1f), ink = LobbyParcel.Ink;
                var sheet = new CaptureSheet(width, SheetGap + 2 * band, paper);
                for (int i = 0; i < portraits.Count; i++)
                {
                    int left = SheetGap + i * (Width + SheetGap);
                    sheet.Label(portraits[i].Id + " new", left, SheetGap + 2, LabelScale, ink);
                    sheet.Paste(portraits[i].Pixels, Width, Height, left, SheetGap + LabelRoom);
                    sheet.Label(portraits[i].Id + " before", left, SheetGap + band + 2, LabelScale, ink);
                    if (portraits[i].Previous == null) continue;
                    var old = CaptureSheet.Decode(portraits[i].Previous, out int oldWidth, out int oldHeight);
                    sheet.Paste(CaptureSheet.Resize(old, oldWidth, oldHeight, Width, Height), Width, Height, left, SheetGap + band + LabelRoom);
                }
                sheet.Save(Full(SheetPath));
                return SheetPath;
            }
            catch (Exception error)
            {
                Debug.LogWarning("[Wreckabulary] Shop portraits: the contact sheet was not written. " + error);
                return "not written";
            }
        }

        static string Full(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    }
}
