using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Wreckabulary
{
    /// <summary>
    /// The picture's finish, as the web edition renders it: smoothed edges (MSAA 4x, set on the
    /// pipeline asset) and a filmic ACES tone curve. URP leaves post-processing off on a camera
    /// unless asked, so the project's volume profile (bloom, vignette) never ran and bright floors
    /// clipped flat; every scene's main camera turns it on here, and one global "Look" volume swaps
    /// the tone curve and sets the exposure. UI canvases are overlays, which post-processing never touches.
    /// The player's graphics settings (Settings > Graphics) live here too: a preset, render scale, edge smoothing,
    /// shadows and a frame cap. High is the picture the project ships.
    /// </summary>
    public static class GraphicsOptions
    {
        /// <summary>Stops added before the tone curve: Unity's ACES sits darker than the browser's at the
        /// same exposure, so this lifts mid-tones back to the web's (calibrated so shaded plaster matches the
        /// web's #CDC8B8 under the web's own lights).</summary>
        public static float PostExposure = .85f;
        public const int LookPriority = 5;

        // The web's lights (renderer.js:105-124). three.js divides a light by pi and Unity doesn't, so a web
        // intensity shows here divided by pi: sun 2.85, fill 1.05, hemisphere 1.65; its room environment (0.35)
        // becomes an even grey added to the ambient.
        public static Color SunColour = new(1f, .918f, .820f), FillColour = new(.843f, .922f, .949f);
        public static Color SkyColour = new(1f, .945f, .863f), GroundColour = new(.275f, .369f, .376f);
        public static float Sun = 2.85f / Mathf.PI, Fill = 1.05f / Mathf.PI, Hemisphere = 1.65f / Mathf.PI, Environment = .25f;
        /// <summary>Where the sun and fill shine from, the web's positions turned to Unity's camera, which looks
        /// from -z: the sun high over the camera's left shoulder, the cool fill low from the far right.</summary>
        public static Vector3 SunFrom = new(-8f, 19f, -8f), FillFrom = new(8f, 6f, 9f);

        // The player's settings, saved as wv.gfx.* strings. They never edit the project's pipeline asset (in the
        // editor that change would be saved into the project): a copy takes the player's numbers, and the original
        // goes back when the numbers match it again or play stops.
        public const string ScaleKey = "wv.gfx.scale", SmoothingKey = "wv.gfx.aa", ShadowsKey = "wv.gfx.shadows", CapKey = "wv.gfx.cap";
        public static readonly string[] Keys = { ScaleKey, SmoothingKey, ShadowsKey, CapKey };
        public static readonly string[] Presets = { "Low", "Medium", "High", "Ultra" };
        public const string DefaultPreset = "High", Custom = "Custom";
        public static readonly string[] Smoothings = { "Off", "FXAA", "SMAA", "MSAA2", "MSAA4", "MSAA8" };
        public static readonly string[] ShadowLevels = { "Off", "Low", "Medium", "High", "Ultra" };
        /// <summary>The frame caps on offer; 0 is none.</summary>
        public static readonly int[] FrameCaps = { 30, 60, 120, 144, 0 };
        public const float MinScale = .5f, MaxScale = 2f, ScaleStep = .25f;

        /// <summary>Pixels drawn per screen pixel in the 3D view: under 1 is faster, over 1 sharper.</summary>
        public static float RenderScale { get; private set; } = 1f;
        /// <summary>Edge smoothing: Off, FXAA, SMAA, or MSAA2/4/8.</summary>
        public static string Smoothing { get; private set; } = "MSAA4";
        public static string Shadows { get; private set; } = "High";
        /// <summary>The frame-rate cap, 0 for none. V-sync, when on, wins over it.</summary>
        public static int FrameCap { get; private set; }

        static RenderPipelineAsset shipped;
        static UniversalRenderPipelineAsset copy;
        static bool swapped;
        static Volume look;
        static Light fill;

        /// <summary>The global volume every scene shares.</summary>
        public static Volume Look => look;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
            // In the editor a swapped asset would otherwise stay in the project's quality settings after play.
            Application.quitting -= Restore;
            Application.quitting += Restore;
            Load();
        }

        /// <summary>Play pressed in the editor: the open scene never raises sceneLoaded.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnPlay() => OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);

        static void OnScene(Scene scene, LoadSceneMode mode)
        {
            if (Camera.main) ApplyTo(Camera.main);
            ApplyLights();
        }

        /// <summary>
        /// The scene's sun takes the web's colour, strength and angle; a shadowless cool fill joins it; and the
        /// ambient becomes the web's hemisphere (warm sky, teal ground) plus its even room light. Walls then read
        /// cream in the light and cool grey in shade, as on the web, rather than warm tan.
        /// </summary>
        public static void ApplyLights()
        {
            Light sun = null;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (light.type == LightType.Directional && light != fill && (!sun || light.name == "Sun")) sun = light;
            if (sun)
            {
                sun.color = SunColour; sun.intensity = Sun;
                sun.transform.rotation = Quaternion.LookRotation(-SunFrom.normalized);
            }
            if (!fill)
            {
                fill = new GameObject("Fill light").AddComponent<Light>();
                Object.DontDestroyOnLoad(fill.gameObject);
                fill.type = LightType.Directional; fill.shadows = LightShadows.None;
            }
            fill.color = FillColour; fill.intensity = Fill;
            fill.transform.rotation = Quaternion.LookRotation(-FillFrom.normalized);
            // Mixed in linear light, as the web does, then handed back as the colours Unity expects.
            Color sky = SkyColour.linear * Hemisphere, ground = GroundColour.linear * Hemisphere, even = Color.white * Environment;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Opaque(sky + even).gamma;
            RenderSettings.ambientEquatorColor = Opaque((sky + ground) * .5f + even).gamma;
            RenderSettings.ambientGroundColor = Opaque(ground + even).gamma;
        }

        static Color Opaque(Color c) => new(c.r, c.g, c.b, 1f);

        /// <summary>Changes the exposure of the shared Look volume, made or not.</summary>
        public static void SetExposure(float stops)
        {
            PostExposure = stops;
            if (look && look.sharedProfile.TryGet<ColorAdjustments>(out var adjust)) adjust.postExposure.Override(stops);
        }

        /// <summary>Turns post-processing on for a camera and makes sure the Look volume exists.</summary>
        public static void ApplyTo(Camera camera)
        {
            if (!camera) return;
            camera.allowMSAA = true;
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            // Edges come from the player's smoothing (MSAA by default); dithering hides banding in the dark
            // gradients behind the house.
            Smooth(camera, data);
            data.dithering = true;
            EnsureLook();
        }

        /// <summary>The preset the settings match, or Custom.</summary>
        public static string Preset => Presets.FirstOrDefault(Matches) ?? Custom;

        static bool Matches(string preset)
        {
            var (scale, smoothing, shadows) = PresetOf(preset);
            return Mathf.Approximately(scale, RenderScale) && smoothing == Smoothing && shadows == Shadows;
        }

        /// <summary>Each preset's render scale, smoothing and shadows.</summary>
        public static (float scale, string smoothing, string shadows) PresetOf(string preset) => preset switch
        {
            "Low" => (.75f, "FXAA", "Low"),
            "Medium" => (1f, "SMAA", "Medium"),
            "Ultra" => (1.5f, "MSAA4", "Ultra"),
            _ => (1f, "MSAA4", "High"),
        };

        /// <summary>Each shadow level's map size, reach in metres and cascades. High is the project's own.</summary>
        static (int resolution, float distance, int cascades) ShadowSetup(string level) => level switch
        {
            "Low" => (1024, 30f, 1),
            "Medium" => (2048, 40f, 2),
            "Ultra" => (4096, 60f, 4),
            _ => (2048, 50f, 4),
        };

        static int Samples(string smoothing) => smoothing switch { "MSAA2" => 2, "MSAA4" => 4, "MSAA8" => 8, _ => 1 };

        /// <summary>True once the player has saved graphics settings; until then the project's asset is used as it is.</summary>
        public static bool Customised => Keys.Any(PlayerPrefs.HasKey);

        /// <summary>The project's own pipeline asset for this quality level, which the settings never edit.</summary>
        public static UniversalRenderPipelineAsset Shipped
        {
            get
            {
                var asset = swapped ? shipped : QualitySettings.renderPipeline;
                return (asset ? asset : GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
            }
        }

        public static void UsePreset(string preset)
        {
            (RenderScale, Smoothing, Shadows) = PresetOf(preset);
            Save();
        }

        public static void SetRenderScale(float scale)
        {
            RenderScale = Mathf.Clamp(Mathf.Round(scale / ScaleStep) * ScaleStep, MinScale, MaxScale);
            Save();
        }

        public static void SetSmoothing(string smoothing)
        {
            if (!Smoothings.Contains(smoothing)) return;
            Smoothing = smoothing;
            Save();
        }

        public static void SetShadows(string level)
        {
            if (!ShadowLevels.Contains(level)) return;
            Shadows = level;
            Save();
        }

        public static void SetFrameCap(int cap)
        {
            if (!FrameCaps.Contains(cap)) return;
            FrameCap = cap;
            Save();
        }

        /// <summary>Forgets the saved settings: High, no cap, and the project's own asset.</summary>
        public static void ResetToDefaults()
        {
            foreach (var key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
            Load();
        }

        /// <summary>Reads the saved settings (High when there are none) and applies them.</summary>
        public static void Load()
        {
            var (scale, smoothing, shadows) = PresetOf(DefaultPreset);
            RenderScale = float.TryParse(PlayerPrefs.GetString(ScaleKey, ""), NumberStyles.Float, CultureInfo.InvariantCulture, out float saved)
                ? Mathf.Clamp(saved, MinScale, MaxScale) : scale;
            string aa = PlayerPrefs.GetString(SmoothingKey, "");
            Smoothing = Smoothings.Contains(aa) ? aa : smoothing;
            string level = PlayerPrefs.GetString(ShadowsKey, "");
            Shadows = ShadowLevels.Contains(level) ? level : shadows;
            FrameCap = int.TryParse(PlayerPrefs.GetString(CapKey, ""), NumberStyles.Integer, CultureInfo.InvariantCulture, out int cap) &&
                       FrameCaps.Contains(cap) ? cap : 0;
            Apply();
        }

        static void Save()
        {
            PlayerPrefs.SetString(ScaleKey, RenderScale.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(SmoothingKey, Smoothing);
            PlayerPrefs.SetString(ShadowsKey, Shadows);
            PlayerPrefs.SetString(CapKey, FrameCap.ToString(CultureInfo.InvariantCulture));
            PlayerPrefs.Save();
            Apply();
        }

        /// <summary>Puts the settings into the pipeline, the frame cap and every camera set up by <see cref="ApplyTo"/>.</summary>
        public static void Apply()
        {
            ApplyPipeline();
            Application.targetFrameRate = FrameCap > 0 ? FrameCap : -1;
            foreach (var camera in Camera.allCameras)
                if (camera.TryGetComponent(out UniversalAdditionalCameraData data) && data.renderPostProcessing) Smooth(camera, data);
        }

        static void ApplyPipeline()
        {
            var original = Shipped;
            if (!original) return;
            var (resolution, distance, cascades) = ShadowSetup(Shadows);
            int samples = Samples(Smoothing);
            bool asShipped = Mathf.Approximately(original.renderScale, RenderScale) && original.msaaSampleCount == samples &&
                (Shadows == "Off" || (original.mainLightShadowmapResolution == resolution &&
                                      Mathf.Approximately(original.shadowDistance, distance) && original.shadowCascadeCount == cascades));
            if (!Customised || asShipped) { Restore(); return; }
            if (!copy || copy.name != original.name + " (player)")
            {
                copy = Object.Instantiate(original);
                copy.name = original.name + " (player)";
            }
            copy.renderScale = RenderScale;
            copy.msaaSampleCount = samples;
            if (Shadows != "Off")
            {
                copy.mainLightShadowmapResolution = resolution;
                copy.shadowDistance = distance;
                copy.shadowCascadeCount = cascades;
            }
            if (!swapped) { shipped = QualitySettings.renderPipeline; swapped = true; }
            QualitySettings.renderPipeline = copy;
        }

        /// <summary>Hands the quality level its own pipeline asset back.</summary>
        public static void Restore()
        {
            if (!swapped) return;
            QualitySettings.renderPipeline = shipped;
            swapped = false;
        }

        /// <summary>A camera's share: MSAA from the asset, or FXAA or SMAA after the picture; shadows on or off.</summary>
        static void Smooth(Camera camera, UniversalAdditionalCameraData data)
        {
            camera.allowMSAA = Samples(Smoothing) > 1;
            data.antialiasing = Smoothing == "FXAA" ? AntialiasingMode.FastApproximateAntialiasing :
                Smoothing == "SMAA" ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None;
            data.antialiasingQuality = AntialiasingQuality.High;
            data.renderShadows = Shadows != "Off";
        }

        public static Volume EnsureLook()
        {
            if (look) return look;
            var go = new GameObject("Look");
            Object.DontDestroyOnLoad(go);
            look = go.AddComponent<Volume>();
            look.isGlobal = true;
            look.priority = LookPriority;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Look";
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            profile.Add<ColorAdjustments>(true).postExposure.Override(PostExposure);
            look.sharedProfile = profile;
            return look;
        }
    }
}
