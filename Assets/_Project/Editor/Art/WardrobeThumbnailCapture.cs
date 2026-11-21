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
using Wreckabulary.UI;
using Object = UnityEngine.Object;

namespace Wreckabulary.EditorTools
{
    public static class WardrobeThumbnailCapture
    {
        const string AvatarKey = "Avatar/Avatar", IdleClip = "Idle";
        const string ResourcesRoot = "Assets/_Project/Resources/", Extension = ".png";
        const string HeadMesh = "SK_Head", HandBone = "hand_R";
        const string HairSlot = "Hair", EyesSlot = "Eyes", TopSlot = "Top", BottomsSlot = "Bottoms", GlovesSlot = "Gloves",
            FootwearSlot = "Footwear", HeadwearSlot = "Headwear", FaceSlot = "Face", BackSlot = "Back", BadgeSlot = "Badge";
        const string OcclusionKeyword = "_SCREEN_SPACE_OCCLUSION", SettleMethod = "LateUpdate";
        const int Size = 256, Supersample = 2, Seat = 0, MinPixels = 1000, Percent = 100, Renders = 2;
        const float FrontYaw = 160f, BackYaw = 340f, Margin = 1.1f, FaceZoom = .72f, PieceMargin = 1.15f;
        const float CameraDistance = 5f, NearPlane = .01f, FarPlane = 30f;
        const float KeyIntensity = .8f, FillIntensity = .4f, Ambient = .45f;
        static readonly Color Background = new Color(.97f, .94f, .85f, 0f);
        static readonly Quaternion KeyTurn = Quaternion.Euler(25f, -25f, 0f), FillTurn = Quaternion.Euler(10f, 35f, 0f);
        static readonly string[] UncoveredSlots = { HeadwearSlot, FaceSlot };
        static readonly Region[] PieceOnly = { Region.Torso, Region.Chest, Region.Back, Region.Legs, Region.Hand, Region.Feet };

        enum Region { Body, Head, Face, Torso, Chest, Back, Legs, Hand, Feet }

        sealed class Shot
        {
            public WardrobePiece Piece;
            public Region Region;
            public byte[] Png;
            public int Transparent, Opaque;
            public bool Alone;
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
            RequireDistinct(shots);
            int written = 0;
            foreach (var shot in shots)
                if (Write(shot)) written++;
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var shot in shots) ImportAsSprite(AssetPath(shot.Piece));
            WardrobeArt.Forget();
            Debug.Log("[Wreckabulary] WARDROBE_THUMBNAILS: count=" + shots.Count + "; written=" + written + "; unchanged=" + (shots.Count - written)
                + "; size=" + Size + "; piece_only=" + shots.Count(shot => shot.Alone)
                + "; filled_frames=" + string.Join(",", shots.Where(shot => shot.Transparent < MinPixels).Select(shot => shot.Piece.Id))
                + "; folder=" + ResourcesRoot + WardrobeArt.Folder + "; pending owner review");
        }

        static void RequireDistinct(List<Shot> shots)
        {
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            var same = new List<string>();
            foreach (var shot in shots)
            {
                string key = Hash128.Compute(shot.Png).ToString();
                if (seen.TryGetValue(key, out string first)) same.Add(shot.Piece.Id + "=" + first);
                else seen[key] = shot.Piece.Id;
            }
            if (same.Count > 0)
                throw new InvalidOperationException("Thumbnails came out identical (" + string.Join(", ", same) + "); nothing was written.");
        }

        static List<Shot> CaptureAll(WardrobeCatalogue wardrobe, AnimationClip idle)
        {
            var shots = new List<Shot>();
            var active = RenderTexture.active;
            bool asyncShaders = EditorSettings.asyncShaderCompilation, occlusion = Shader.IsKeywordEnabled(OcclusionKeyword);
            bool asyncVariants = ShaderUtil.allowAsyncCompilation;
            try
            {
                if (asyncShaders) EditorSettings.asyncShaderCompilation = false;
                ShaderUtil.allowAsyncCompilation = false;
                if (wardrobe.Pieces.Count > 0) Capture(wardrobe, wardrobe.Pieces[0], idle);
                foreach (var piece in wardrobe.Pieces) shots.Add(Capture(wardrobe, piece, idle));
            }
            finally
            {
                RenderTexture.active = active;
                ShaderUtil.allowAsyncCompilation = asyncVariants;
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
                Settle(model);
                var own = PieceRenderers(piece, skins, model.GetComponentInChildren<WardrobeRig>());
                bool alone = own.Count > 0 && Array.IndexOf(PieceOnly, region) >= 0;
                if (alone) ShowOnly(model, own);
                Aim(preview, alone ? Alone(region, own, model) : Focus(region, own, model, skins), alone ? PieceMargin : Margin);
                preview.BeginStaticPreview(new Rect(0, 0, Size * Supersample, Size * Supersample));
                begun = true;
                for (int pass = 0; pass < Renders; pass++) preview.Render(true);
                image = ReadRgba(preview.camera.targetTexture);
                var shot = Describe(piece, image);
                shot.Region = region;
                shot.Alone = alone;
                return shot;
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

        static void Settle(GameObject model)
        {
            var face = model.GetComponentInChildren<CuteFace>(true);
            if (!face) throw new InvalidOperationException("The avatar has no code-built face to settle.");
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

        static List<Renderer> PieceRenderers(WardrobePiece piece, SkinnedMeshRenderer[] skins, WardrobeRig rig)
        {
            var renderers = new List<Renderer>();
            var node = rig && !string.IsNullOrEmpty(piece.Rigid) ? rig.NodeFor(piece.Rigid) : null;
            if (node) renderers.AddRange(node.GetComponentsInChildren<Renderer>());
            foreach (var skin in skins)
                if (skin.name == piece.Mesh) renderers.Add(skin);
            renderers.RemoveAll(renderer => !Visible(renderer));
            return renderers;
        }

        static void ShowOnly(GameObject model, List<Renderer> kept)
        {
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                renderer.forceRenderingOff = !kept.Contains(renderer);
        }

        static Bounds Focus(Region region, List<Renderer> own, GameObject model, SkinnedMeshRenderer[] skins)
        {
            var body = Encapsulate(model.GetComponentsInChildren<Renderer>().Where(Visible))
                ?? throw new InvalidOperationException("Nothing visible to capture.");
            var head = Encapsulate(skins.Where(skin => skin.name == HeadMesh && Visible(skin))) ?? body;
            switch (region)
            {
                case Region.Head: return Join(head, Encapsulate(own));
                case Region.Face: return new Bounds(head.center, head.size * FaceZoom);
                default: return body;
            }
        }

        static Bounds Alone(Region region, List<Renderer> own, GameObject model)
        {
            var whole = Tight(own, null) ?? throw new InvalidOperationException("The piece has nothing to frame.");
            if (region != Region.Hand) return whole;
            var hand = ModelVisual.FindNamed(model, HandBone);
            if (!hand) return whole;
            bool right = hand.position.x >= whole.center.x;
            return Tight(own, point => point.x >= whole.center.x == right) ?? whole;
        }

        static Bounds? Tight(List<Renderer> renderers, Func<Vector3, bool> keep)
        {
            Bounds? all = null;
            var baked = new Mesh { hideFlags = HideFlags.HideAndDontSave };
            var points = new List<Vector3>();
            try
            {
                foreach (var renderer in renderers)
                {
                    if (!(renderer is SkinnedMeshRenderer skin) || !skin.sharedMesh)
                    {
                        if (keep == null || keep(renderer.bounds.center)) all = Grow(all, renderer.bounds.min, renderer.bounds.max);
                        continue;
                    }
                    skin.BakeMesh(baked, true);
                    baked.GetVertices(points);
                    var toWorld = Matrix4x4.TRS(skin.transform.position, skin.transform.rotation, Vector3.one);
                    Bounds? posed = null, kept = null;
                    foreach (var local in points)
                    {
                        var point = toWorld.MultiplyPoint3x4(local);
                        posed = Grow(posed, point, point);
                        if (keep == null || keep(point)) kept = Grow(kept, point, point);
                    }
                    bool trusted = posed.HasValue && posed.Value.Intersects(skin.bounds);
                    if (trusted && kept.HasValue) all = Grow(all, kept.Value.min, kept.Value.max);
                    else if (!trusted && (keep == null || keep(skin.bounds.center))) all = Grow(all, skin.bounds.min, skin.bounds.max);
                }
            }
            finally { Object.DestroyImmediate(baked); }
            return all;
        }

        static Bounds Grow(Bounds? all, Vector3 min, Vector3 max)
        {
            var grown = all ?? new Bounds((min + max) * .5f, max - min);
            grown.Encapsulate(min);
            grown.Encapsulate(max);
            return grown;
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

        static void Aim(PreviewRenderUtility preview, Bounds focus, float margin)
        {
            float half = Mathf.Max(focus.extents.x, focus.extents.y) * margin;
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
            Debug.Log("[Wreckabulary] WARDROBE_THUMBNAIL: " + asset + "; bytes=" + shot.Png.Length + "; frame=" + shot.Region
                + (shot.Alone ? " piece only" : " on the avatar")
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
