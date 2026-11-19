using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Wreckabulary.Art;

namespace Wreckabulary
{
    /// <summary>
    /// The Home logo: WRECKABULARY spelled with the real 3D letter tiles from the game, lit and rendered by a
    /// small camera of its own into a transparent texture shown in the UI. The tiles tumble into place when
    /// the badge appears, bob gently, and now and then one hops and spins. Click it to wreck and rebuild it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LobbyTileLogo : MonoBehaviour
    {
        public const string Word = "WRECKABULARY";
        public const int Layer = 30;
        static readonly Vector3 Away = new Vector3(0f, -80f, -400f);
        const float Size = .26f, Gap = .03f, DropTime = .5f, Stagger = .06f;

        sealed class Tile
        {
            public Transform root;
            public Vector3 home;
            public float delay, hop = -1f, seed;
        }

        readonly List<Tile> tiles = new();
        Transform rig;
        Camera eye;
        Light lamp;
        RenderTexture target;
        RawImage view;
        float clock, nextHop = 4f;
        int hopper;
        public RawImage View => view;
        public int Tiles => tiles.Count;
        public RenderTexture Texture => target;

        public static LobbyTileLogo Create(RectTransform parent)
        {
            var rect = LobbyKit.Rect(parent, "Tile logo");
            var image = rect.gameObject.AddComponent<RawImage>();
            image.raycastTarget = true;
            var logo = rect.gameObject.AddComponent<LobbyTileLogo>();
            logo.view = image;
            var button = rect.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(logo.Tumble);
            logo.Build();
            return logo;
        }

        void Build()
        {
            rig = new GameObject("Tile logo rig").transform;
            rig.position = Away;
            float width = Word.Length * Size + (Word.Length - 1) * Gap;
            for (int i = 0; i < Word.Length; i++)
            {
                var tile = Spawn(Word[i]);
                if (!tile) continue;
                tile.localPosition = new Vector3(-width * .5f + Size * .5f + i * (Size + Gap), 0f, 0f);
                tiles.Add(new Tile { root = tile, home = tile.localPosition, delay = i * Stagger, seed = i * 1.37f });
            }
            foreach (var t in rig.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;

            target = new RenderTexture(1024, 160, 16, RenderTextureFormat.ARGB32) { name = "Tile logo", antiAliasing = 4 };
            view.texture = target;
            eye = new GameObject("Tile logo camera").AddComponent<Camera>();
            eye.transform.SetParent(rig, false);
            eye.transform.localPosition = new Vector3(0f, .05f, -6f);
            eye.transform.localRotation = Quaternion.identity;
            eye.fieldOfView = 5.6f;
            eye.nearClipPlane = 4f; eye.farClipPlane = 8f;
            eye.clearFlags = CameraClearFlags.SolidColor;
            eye.backgroundColor = Color.clear;
            eye.cullingMask = 1 << Layer;
            eye.targetTexture = target;
            eye.allowHDR = false;
            eye.depth = -50;
            var data = eye.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.antialiasing = AntialiasingMode.None;

            lamp = new GameObject("Tile logo light").AddComponent<Light>();
            lamp.transform.SetParent(rig, false);
            lamp.transform.localPosition = new Vector3(-1.2f, 1.6f, -2.4f);
            lamp.transform.LookAt(rig.position);
            lamp.type = LightType.Spot;
            lamp.spotAngle = 70f;
            lamp.range = 6f;
            lamp.intensity = 9f;
            lamp.color = LobbyKit.Hex(0xFFF1DC);
            lamp.shadows = LightShadows.None;
            Restart();
        }

        Transform Spawn(char letter)
        {
            string key = "Letters/Tile_" + letter;
            var library = ModelLibrary.Load();
            if (!library || !library.Find(key)) return null;
            var root = new GameObject("Logo tile " + letter).transform;
            root.SetParent(rig, false);
            var holder = new GameObject("Tile").transform;
            holder.SetParent(root, false);
            var model = ModelVisual.Spawn(key, holder);
            if (!model) { Destroy(root.gameObject); return null; }
            var bounds = ModelVisual.BoundsIn(holder, model);
            var size = bounds.size;
            // Turned half way round so the letter face looks at the camera and reads upright.
            holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
            float scale = Size / Mathf.Max(.001f, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
            holder.localScale = Vector3.one * scale;
            holder.localPosition = -(holder.localRotation * bounds.center) * scale;
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            return root;
        }

        /// <summary>Drops every tile in again from above, one after another.</summary>
        public void Restart() { clock = 0f; foreach (var t in tiles) t.hop = -1f; }

        public void Tumble() => Restart();

        void OnEnable()
        {
            if (rig) rig.gameObject.SetActive(true);
            Restart();
        }

        void OnDisable()
        {
            if (rig) rig.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!rig) return;
            float dt = Time.unscaledDeltaTime;
            clock += dt;
            if (clock > nextHop && tiles.Count > 0)
            {
                hopper = (hopper * 7 + 5) % tiles.Count;
                tiles[hopper].hop = 0f;
                nextHop = clock + 3.2f + (hopper % 3) * .8f;
            }
            foreach (var t in tiles)
            {
                if (!t.root) continue;
                float k = Mathf.Clamp01((clock - t.delay) / DropTime);
                float fall = 1f - Bounce(k);
                float bob = Mathf.Sin(clock * 1.6f + t.seed) * .008f;
                var position = t.home + Vector3.up * (fall * 1.4f + bob);
                var rotation = Quaternion.Euler(fall * 40f, 0f, Mathf.Sin(t.seed * 3.1f) * 4f + fall * 60f * Mathf.Sign(Mathf.Sin(t.seed * 7f)));
                if (t.hop >= 0f)
                {
                    t.hop += dt;
                    float h = Mathf.Clamp01(t.hop / .6f);
                    position += Vector3.up * Mathf.Sin(h * Mathf.PI) * .16f;
                    rotation *= Quaternion.Euler(0f, h * 360f, 0f);
                    if (h >= 1f) t.hop = -1f;
                }
                t.root.localPosition = position;
                t.root.localRotation = rotation;
                t.root.gameObject.SetActive(k > 0f);
            }
        }

        static float Bounce(float t)
        {
            if (t < .62f) { float u = t / .62f; return u * u; }
            float v = (t - .81f) / .19f;
            return 1f - .1f * (1f - v * v);
        }

        void OnDestroy()
        {
            if (rig) Destroy(rig.gameObject);
            if (eye) eye.targetTexture = null;
            if (target) { target.Release(); Destroy(target); }
        }
    }
}
