using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Wreckabulary.UI;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class WardrobeThumbnailCapture
    {
        const string AvatarKey = "Avatar/Avatar", IdleClip = "Idle";
        const string ResourcesRoot = "Assets/_Project/Resources/", Extension = ".png";
        const string HeadMesh = "SK_Head", ChestBone = "chest", HandBone = "hand_R";
        const string HairSlot = "Hair", EyesSlot = "Eyes", TopSlot = "Top", BottomsSlot = "Bottoms", GlovesSlot = "Gloves",
            FootwearSlot = "Footwear", HeadwearSlot = "Headwear", FaceSlot = "Face", BackSlot = "Back", BadgeSlot = "Badge";
        const string OcclusionKeyword = "_SCREEN_SPACE_OCCLUSION";
        const int Size = 256, Supersample = 2, Seat = 0, MinPixels = 1000, Percent = 100;
        const float FrontYaw = 160f, BackYaw = 340f, Margin = 1.1f, FaceZoom = .72f, HandMargin = 1.25f;
        const float ChestSize = .26f, ChestForward = .1f, ChestDrop = .06f;
        const float CameraDistance = 5f, NearPlane = .01f, FarPlane = 30f;
        const float KeyIntensity = .8f, FillIntensity = .4f, Ambient = .45f;
        static readonly Color Background = new Color(.97f, .94f, .85f, 0f);
        static readonly Quaternion KeyTurn = Quaternion.Euler(25f, -25f, 0f), FillTurn = Quaternion.Euler(10f, 35f, 0f);
        static readonly string[] UncoveredSlots = { HeadwearSlot, FaceSlot };

        enum Region { Body, Head, Face, Torso, Chest, Back, Legs, Hand, Feet }

        sealed class Shot
        {
            public WardrobePiece Piece;
            public byte[] Png;
            public int Transparent, Opaque;
        }

        [MenuItem("Wreckabulary/Art/Capture wardrobe thumbnails")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || !GraphicsSettings.currentRenderPipeline)
                throw new InvalidOperationException("Capture in Edit Mode with the active render pipeline and a graphics device.");
            var library = ModelLibrary.Load();
            var idle = library ? library.FindClip(AvatarKey, IdleClip) : null;
            if (!library || !library.Find(AvatarKey) || !idle) throw new InvalidOperationException("The avatar and its Idle clip are required.");
            if (!WardrobeRig.Art) throw new InvalidOperationException("Resources/" + WardrobeRig.ArtPath + " is missing; run Tools/AssetPipeline/build_signature_pieces.py first.");
            var shots = CaptureAll(GameConfig.Current.Wardrobe, idle);
            if (!shots.Any(shot => shot.Transparent >= MinPixels))
                throw new InvalidOperationException("No thumbnail kept a transparent background; nothing was written.");
            int written = 0;
            foreach (var shot in shots)
                if (Write(shot)) written++;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var shot in shots) ImportAsSprite(AssetPath(shot.Piece));
            WardrobeArt.Forget();
            Debug.Log("[Wreckabulary] WARDROBE_THUMBNAILS: count=" + shots.Count + "; written=" + written + "; unchanged=" + (shots.Count - written)
                + "; size=" + Size + "; folder=" + ResourcesRoot + WardrobeArt.Folder + "; pending owner review");
        }

        static List<Shot> CaptureAll(WardrobeCatalogue wardrobe, AnimationClip idle)
        {
            var shots = new List<Shot>();
            var active = RenderTexture.active;
            bool asyncShaders = EditorSettings.asyncShaderCompilation, occlusion = Shader.IsKeywordEnabled(OcclusionKeyword);
            try
            {
                if (asyncShaders) EditorSettings.asyncShaderCompilation = false;
                foreach (var piece in wardrobe.Pieces) shots.Add(Capture(wardrobe, piece, idle));
            }
            finally
            {
                RenderTexture.active = active;
                if (asyncShaders) EditorSettings.asyncShaderCompilation = true;
                if (occlusion) Shader.EnableKeyword(OcclusionKeyword); else Shader.DisableKeyword(OcclusionKeyword);
            }
            return shots;
        }

        static Shot Capture(WardrobeCatalogue wardrobe, WardrobePiece piece, AnimationClip idle)
        {
            var preview = new PreviewRenderUtility();
            GameObject holder = null;
            Texture2D image = null;
            bool begun = false;
            try
            {
                holder = new GameObject("Wardrobe thumbnail " + piece.Id);
                preview.AddSingleGO(holder);
                var model = ModelVisual.Spawn(AvatarKey, holder.transform);
                if (!model) throw new InvalidOperationException("Avatar spawn failed.");
                idle.SampleAnimation(model, 0f);
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                var worn = PlayerAppearance.Dress(skins, OutfitFor(wardrobe, piece), wardrobe, Seat);
                if (worn.PieceIn(piece.Slot) != piece.Id) throw new InvalidOperationException(piece.Id + " was not worn for its thumbnail.");
                var region = RegionFor(piece.Slot);
                holder.transform.rotation = Quaternion.Euler(0f, region == Region.Back ? BackYaw : FrontYaw, 0f);
                Aim(preview, Focus(region, piece, worn, wardrobe, model, holder.transform, skins));
                preview.BeginStaticPreview(new Rect(0, 0, Size * Supersample, Size * Supersample));
                begun = true;
                preview.Render(true);
                image = ReadRgba(preview.camera.targetTexture);
                return Describe(piece, image);
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

        static Outfit OutfitFor(WardrobeCatalogue wardrobe, WardrobePiece piece)
        {
            var outfit = wardrobe.Default;
            foreach (string slot in UncoveredSlots)
                if (slot != piece.Slot) outfit = wardrobe.Wear(outfit, slot, null) ?? outfit;
            return wardrobe.Wear(outfit, piece.Slot, piece.Id)
                ?? throw new InvalidOperationException(piece.Id + " does not fit the default outfit.");
        }

        static Region RegionFor(string slot) => slot switch
        {
            HairSlot or HeadwearSlot or FaceSlot => Region.Head,
            EyesSlot => Region.Face,
            TopSlot => Region.Torso,
            BadgeSlot => Region.Chest,
            BackSlot => Region.Back,
            BottomsSlot => Region.Legs,
            GlovesSlot => Region.Hand,
            FootwearSlot => Region.Feet,
            _ => Region.Body,
        };

        static Bounds Focus(Region region, WardrobePiece piece, Outfit worn, WardrobeCatalogue wardrobe, GameObject model, Transform holder,
            SkinnedMeshRenderer[] skins)
        {
            var rig = model.GetComponentInChildren<WardrobeRig>();
            var body = Encapsulate(model.GetComponentsInChildren<Renderer>().Where(Visible))
                ?? throw new InvalidOperationException("Nothing visible to capture.");
            Bounds Worn(string slot) => PieceBounds(wardrobe.Piece(worn.PieceIn(slot) ?? ""), skins, rig) ?? body;
            var own = PieceBounds(piece, skins, rig);
            var head = Encapsulate(skins.Where(skin => skin.name == HeadMesh && Visible(skin))) ?? body;
            switch (region)
            {
                case Region.Head: return Join(head, own);
                case Region.Face: return new Bounds(head.center, head.size * FaceZoom);
                case Region.Torso:
                case Region.Back: return Join(Worn(TopSlot), own);
                case Region.Chest: return Join(Chest(model, holder, body), own);
                case Region.Legs: return Join(Worn(BottomsSlot), own);
                case Region.Feet: return Join(Worn(FootwearSlot), own);
                case Region.Hand: return Hand(model, Worn(GlovesSlot));
                default: return body;
            }
        }

        static Bounds? PieceBounds(WardrobePiece piece, SkinnedMeshRenderer[] skins, WardrobeRig rig)
        {
            if (piece == null) return null;
            var renderers = new List<Renderer>();
            var node = rig && !string.IsNullOrEmpty(piece.Rigid) ? rig.NodeFor(piece.Rigid) : null;
            if (node) renderers.AddRange(node.GetComponentsInChildren<Renderer>());
            foreach (var skin in skins)
                if (skin.name == piece.Mesh) renderers.Add(skin);
            return Encapsulate(renderers.Where(Visible));
        }

        static bool Visible(Renderer renderer) => renderer && renderer.enabled && renderer.gameObject.activeInHierarchy;

        static Bounds? Encapsulate(IEnumerable<Renderer> renderers)
        {
            Bounds? all = null;
            foreach (var renderer in renderers)
            {
                var joined = all ?? renderer.bounds;
                joined.Encapsulate(renderer.bounds);
                all = joined;
            }
            return all;
        }

        static Bounds Join(Bounds anchor, Bounds? piece)
        {
            if (piece.HasValue) anchor.Encapsulate(piece.Value);
            return anchor;
        }

        static Bounds Chest(GameObject model, Transform holder, Bounds body)
        {
            var chest = ModelVisual.FindNamed(model, ChestBone);
            var centre = chest ? chest.position + holder.forward * ChestForward - Vector3.up * ChestDrop : body.center;
            return new Bounds(centre, Vector3.one * ChestSize);
        }

        static Bounds Hand(GameObject model, Bounds gloves)
        {
            var hand = ModelVisual.FindNamed(model, HandBone);
            if (!hand) return gloves;
            var centre = new Vector3(hand.position.x, gloves.center.y, hand.position.z);
            return new Bounds(centre, Vector3.one * (gloves.size.y * HandMargin));
        }

        static void Aim(PreviewRenderUtility preview, Bounds focus)
        {
            float half = Mathf.Max(focus.extents.x, focus.extents.y) * Margin;
            if (!(half > 0f) || float.IsInfinity(half)) throw new InvalidOperationException("The thumbnail has nothing to frame.");
            var camera = preview.camera;
            camera.orthographic = true;
            camera.orthographicSize = half;
            camera.transform.SetPositionAndRotation(focus.center + Vector3.back * CameraDistance, Quaternion.identity);
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
                encoded = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
                    { hideFlags = HideFlags.HideAndDontSave, antiAliasing = 1 };
                if (!encoded.Create() || !GraphicsFormatUtility.HasAlphaChannel(encoded.graphicsFormat))
                    throw new InvalidOperationException("Could not create an sRGB output target with alpha.");
                GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Graphics.Blit(source, encoded);
                RenderTexture.active = encoded;
                copy = new Texture2D(Size, Size, TextureFormat.RGBA32, false, false) { hideFlags = HideFlags.HideAndDontSave };
                copy.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false);
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

        static Shot Describe(WardrobePiece piece, Texture2D image)
        {
            var shot = new Shot { Piece = piece };
            foreach (var pixel in image.GetPixels32())
            {
                if (pixel.a == byte.MinValue) shot.Transparent++;
                else if (pixel.a == byte.MaxValue) shot.Opaque++;
            }
            if (shot.Opaque < MinPixels) throw new InvalidOperationException("The thumbnail for " + piece.Id + " is empty.");
            shot.Png = image.EncodeToPNG();
            return shot;
        }

        static string AssetPath(WardrobePiece piece) => ResourcesRoot + WardrobeArt.PathFor(piece.Slot, piece.Id) + Extension;

        static bool Write(Shot shot)
        {
            string asset = AssetPath(shot.Piece);
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "..", asset));
            bool same = File.Exists(file) && File.ReadAllBytes(file).SequenceEqual(shot.Png);
            if (!same)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(file));
                File.WriteAllBytes(file, shot.Png);
            }
            Debug.Log("[Wreckabulary] WARDROBE_THUMBNAIL: " + asset + "; bytes=" + shot.Png.Length
                + "; transparent=" + shot.Transparent * Percent / (Size * Size) + "%; " + (same ? "unchanged" : "written"));
            return !same;
        }

        static void ImportAsSprite(string asset)
        {
            var importer = AssetImporter.GetAtPath(asset) as TextureImporter;
            if (!importer) throw new IOException("No texture importer for " + asset);
            string before = EditorJsonUtility.ToJson(importer);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = Size;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            if (EditorJsonUtility.ToJson(importer) != before) importer.SaveAndReimport();
        }
    }
}
