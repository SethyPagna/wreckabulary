using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Wreckabulary.Art;

namespace Wreckabulary
{
    [DisallowMultipleComponent]
    public sealed class SeatPortrait : MonoBehaviour
    {
        public const int Size = 128, Layer = 31;
        const float Distance = 3f, Yaw = 20f, Pitch = 8f, HeadDrop = .3f, HeadSpan = .95f;

        static Camera lens;
        static UniversalRenderPipeline.SingleCameraRequest request;
        static readonly List<Renderer> parts = new();
        static readonly List<int> layers = new();

        PlayerController player;
        PlayerAppearance appearance;
        RenderTexture texture;
        GameObject framed;
        Transform head, chest;
        Renderer face;
        int renderedVersion = int.MinValue;

        public static readonly Color Background = new(.086f, .188f, .184f, 0f);
        public static bool CanRender => SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
        public Texture Texture => renderedVersion != int.MinValue ? texture : null;
        public int Renders { get; private set; }

        public static SeatPortrait For(PlayerController player)
        {
            if (!player) return null;
            if (!player.TryGetComponent(out SeatPortrait portrait)) portrait = player.gameObject.AddComponent<SeatPortrait>();
            return portrait;
        }

        public bool Wants
        {
            get
            {
                if (!CanRender || !Ready(out var model)) return false;
                return model != framed || appearance.OutfitVersion != renderedVersion;
            }
        }

        public Texture Refresh(bool allowRender)
        {
            if (!allowRender || !Wants) return Texture;
            Ready(out var model);
            if (model != framed) Find(model);
            if (Draw(model)) renderedVersion = appearance.OutfitVersion;
            return Texture;
        }

        bool Ready(out GameObject model)
        {
            model = null;
            if (!player && !TryGetComponent(out player)) return false;
            if (!appearance && !TryGetComponent(out appearance)) return false;
            model = appearance.AvatarModel;
            return model && model.activeInHierarchy && !player.IsKnockedOut && !player.IsHeld;
        }

        void Find(GameObject model)
        {
            framed = model;
            renderedVersion = int.MinValue;
            var animator = model.GetComponentInChildren<Animator>(true);
            bool human = animator && animator.isHuman;
            head = human ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
            chest = human ? animator.GetBoneTransform(HumanBodyBones.UpperChest) : null;
            if (human && !chest) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (!head) head = ModelVisual.FindNamed(model, "head");
            if (!chest) chest = ModelVisual.FindNamed(model, "chest");
            face = null;
            foreach (var mesh in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (mesh.name == "SK_Head") { face = mesh; break; }
        }

        bool Frame(GameObject model, out Vector3 centre, out float half)
        {
            centre = default; half = 0f;
            parts.Clear();
            model.GetComponentsInChildren(false, parts);
            bool any = false;
            Bounds all = default;
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var part = parts[i];
                if (!part.enabled || part is ParticleSystemRenderer || part is TrailRenderer || part is LineRenderer) { parts.RemoveAt(i); continue; }
                if (any) all.Encapsulate(part.bounds); else { all = part.bounds; any = true; }
            }
            if (!any) return false;
            var box = face && face.enabled && face.gameObject.activeInHierarchy ? face.bounds : default;
            if (box.size.y > .05f && box.size.y < all.size.y * .6f)
            {
                float tall = box.size.y;
                centre = box.center - Vector3.up * tall * HeadDrop;
                half = tall * HeadSpan;
            }
            else if (head)
            {
                float neck = chest ? Mathf.Clamp(head.position.y - chest.position.y, .06f, .5f) : .15f;
                centre = head.position + Vector3.up * neck * .4f;
                half = neck * 1.6f;
            }
            else
            {
                float tall = all.size.y;
                centre = new Vector3(all.center.x, all.max.y - tall * .2f, all.center.z);
                half = tall * .22f;
            }
            return half > 0f;
        }

        bool Draw(GameObject model)
        {
            if (!Frame(model, out var centre, out float half)) return false;
            if (!texture)
            {
                texture = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { name = "Seat portrait", antiAliasing = 1, useMipMap = false };
                texture.Create();
            }
            var camera = Lens();
            var forward = model.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = player.Facing;
            forward = forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward;
            var toward = Quaternion.AngleAxis(Yaw, Vector3.up) * forward;
            var from = (toward * Mathf.Cos(Pitch * Mathf.Deg2Rad) + Vector3.up * Mathf.Sin(Pitch * Mathf.Deg2Rad)) * Distance;
            camera.transform.SetPositionAndRotation(centre + from, Quaternion.LookRotation(-from, Vector3.up));
            camera.orthographicSize = half;
            camera.backgroundColor = Background;
            layers.Clear();
            for (int i = 0; i < parts.Count; i++) { layers.Add(parts[i].gameObject.layer); parts[i].gameObject.layer = Layer; }
            camera.enabled = true;
            try
            {
                request.destination = texture;
                if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
                else
                {
                    camera.targetTexture = texture;
                    camera.Render();
                    camera.targetTexture = null;
                }
            }
            finally
            {
                camera.enabled = false;
                for (int i = 0; i < parts.Count; i++) if (parts[i]) parts[i].gameObject.layer = layers[i];
                request.destination = null;
                parts.Clear();
            }
            Renders++;
            return true;
        }

        static Camera Lens()
        {
            if (lens) return lens;
            var holder = new GameObject("Seat portrait camera") { hideFlags = HideFlags.HideInHierarchy };
            if (Application.isPlaying) DontDestroyOnLoad(holder);
            lens = holder.AddComponent<Camera>();
            lens.enabled = false;
            lens.orthographic = true;
            lens.aspect = 1f;
            lens.nearClipPlane = .05f;
            lens.farClipPlane = Distance * 2f;
            lens.clearFlags = CameraClearFlags.SolidColor;
            lens.backgroundColor = Background;
            lens.cullingMask = 1 << Layer;
            lens.allowHDR = false;
            lens.allowMSAA = false;
            lens.useOcclusionCulling = false;
            var data = lens.GetUniversalAdditionalCameraData();
            data.renderShadows = false;
            data.renderPostProcessing = false;
            data.antialiasing = AntialiasingMode.None;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            request = new UniversalRenderPipeline.SingleCameraRequest();
            return lens;
        }

        void OnDestroy()
        {
            if (!texture) return;
            texture.Release();
            Destroy(texture);
            texture = null;
        }
    }
}
