using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Wreckabulary
{
    /// <summary>
    /// Who is playing, carried between scenes: roommates who walked into the house
    /// come along to whichever mode is picked at the typewriter.
    /// </summary>
    public static class Session
    {
        public const string HubScene = "Hub";
        public const string DibsScene = "LivingRoom";
        public const string TutorialScene = "Tutorial";

        public static readonly List<InputBinding> Bindings = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear() => Bindings.Clear();

        public static void Remember(InputBinding binding)
        {
            if (!Bindings.Exists(b => b.Id == binding.Id)) Bindings.Add(binding);
        }

        public static bool CanLoad(string scene) => Application.CanStreamedLevelBeLoaded(scene);

        public static void Load(string scene)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(scene);
        }

        /// <summary>Back to the house, if the house is in the build.</summary>
        public static bool GoHome()
        {
            if (!CanLoad(HubScene) || SceneManager.GetActiveScene().name == HubScene) return false;
            Load(HubScene);
            return true;
        }
    }
}
