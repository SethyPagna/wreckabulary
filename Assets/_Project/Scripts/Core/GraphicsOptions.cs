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
        /// same exposure, so this lifts mid-tones back to the web's.</summary>
        public static float PostExposure = .45f;
        public const int LookPriority = 5;

        static Volume look;

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
