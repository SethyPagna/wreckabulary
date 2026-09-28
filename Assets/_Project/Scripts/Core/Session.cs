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
        public const string MovingDayScene = "MovingDay";
        public const string BedroomScene = "Bedroom";
        public const string KitchenScene = "Kitchen";
        public const string GardenScene = "Garden";
        public const string CreativeScene = "Creative";
        public const string CustomArenaScene = "CustomArena";
        public const string FurnishFirstScene = "FurnishFirst";

        /// <summary>The room being built in Creative, carried into "Play Dibs! here" and back.</summary>
        public static RoomLayout CustomRoom;
        public static int CustomRounds = 3;
        public static int CustomStarterLetters = 3;
        /// <summary>Where "back" goes: the house, or Creative after playing in your own room.</summary>
        public static string ReturnScene = HubScene;

        public static readonly List<InputBinding> Bindings = new();

        /// <summary>Best Moving Day stars per level, for this play session.</summary>
        public static readonly Dictionary<int, int> MovingDayStars = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            Bindings.Clear();
            MovingDayStars.Clear();
            CustomRoom = null;
            CustomRounds = 3;
            CustomStarterLetters = 3;
            ReturnScene = HubScene;
        }

        public static void RecordStars(int level, int stars)
        {
            if (!MovingDayStars.TryGetValue(level, out int best) || stars > best) MovingDayStars[level] = stars;
        }

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

        /// <summary>Back to where this mode was started from: your Creative room, otherwise the house.</summary>
        public static bool GoHome()
        {
            string current = SceneManager.GetActiveScene().name;
            string target = ReturnScene != current && CanLoad(ReturnScene) ? ReturnScene : HubScene;
            if (target == current || !CanLoad(target)) return false;
            if (target == HubScene) ReturnScene = HubScene;
            Load(target);
            return true;
        }
    }
}
