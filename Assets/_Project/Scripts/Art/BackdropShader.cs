using UnityEngine;

namespace Wreckabulary.Art
{
    public static class BackdropShader
    {
        public const string ShaderName = "Wreckabulary/Backdrop";
        public const string MotionKey = "wv.theme.effects";
        public static readonly Color CapColor = new(.929f, .875f, .769f);

        static readonly int PlayMin = Shader.PropertyToID("_BackdropPlayMin");
        static readonly int PlayMax = Shader.PropertyToID("_BackdropPlayMax");
        static readonly int Cut = Shader.PropertyToID("_BackdropCut");
        static readonly int Wind = Shader.PropertyToID("_BackdropWind");
        static readonly int Cap = Shader.PropertyToID("_BackdropCapColor");

        public static bool MotionAllowed => PlayerPrefs.GetInt(MotionKey, 1) != 0;

        public static void SetPlayVolume(Bounds world)
        {
            Shader.SetGlobalVector(PlayMin, world.min);
            Shader.SetGlobalVector(PlayMax, world.max);
            Shader.SetGlobalColor(Cap, CapColor);
            Shader.SetGlobalFloat(Cut, 1f);
        }

        public static void ClearPlayVolume()
        {
            Shader.SetGlobalFloat(Cut, 0f);
            Shader.SetGlobalVector(PlayMin, Vector4.zero);
            Shader.SetGlobalVector(PlayMax, Vector4.zero);
        }

        internal static void SetWind(float amount) => Shader.SetGlobalFloat(Wind, amount);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetGlobals()
        {
            ClearPlayVolume();
            SetWind(0f);
            Shader.SetGlobalColor(Cap, CapColor);
        }
    }
}
