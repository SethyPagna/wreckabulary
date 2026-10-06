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

        static Volume look;
        static Light fill;

        /// <summary>The global volume every scene shares.</summary>
        public static Volume Look => look;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
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
            // Edges come from MSAA; dithering hides banding in the dark gradients behind the house.
            data.antialiasing = AntialiasingMode.None;
            data.dithering = true;
            EnsureLook();
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
