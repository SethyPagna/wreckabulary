using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public sealed class LobbyMapSelectionTests
    {
        string savedCareer;
        bool hadCareer;
        float savedVolume;
        int savedVsync;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadCareer = PlayerPrefs.HasKey(MatchTally.CareerKey);
            savedCareer = PlayerPrefs.GetString(MatchTally.CareerKey);
            savedVolume = AudioListener.volume;
            savedVsync = QualitySettings.vSyncCount;
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            Assert.IsNotNull(LobbyMenu.Instance);
            yield return TestScenes.WaitUntil(() => !GameHud.Active.StateController.IsTransitioning,
                1f, "lobby fade and menu input focus");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            if (hadCareer) PlayerPrefs.SetString(MatchTally.CareerKey, savedCareer);
            else PlayerPrefs.DeleteKey(MatchTally.CareerKey);
            PlayerPrefs.Save();
            AudioListener.volume = savedVolume;
            QualitySettings.vSyncCount = savedVsync;
        }

        [UnityTest]
        public IEnumerator LeavingEscapeHonoursAnExplicitMapSelection()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(mode: "Escape");
            Assert.AreEqual(Session.LobbyRandomMap, menu.Map);
            Assert.AreEqual("pinwheel", Session.MapId);

            menu.Choose(mode: "Dibs", map: "courtyard");

            Assert.AreEqual("courtyard", menu.Map);
            Assert.AreEqual("courtyard", Session.MapId);
            Assert.AreEqual("Dibs", menu.Mode);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LeavingEscapeWithoutAMapRestoresThePreviousHouse()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(map: "courtyard");
            menu.Choose(mode: "Escape");
            Assert.AreEqual(Session.LobbyRandomMap, menu.Map);

            menu.Choose(mode: "Dibs");

            Assert.AreEqual("courtyard", menu.Map);
            Assert.AreEqual("courtyard", Session.MapId);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EscapeStillSelectsARandomHouseEvenWithAnExplicitMap()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(mode: "Escape", map: "courtyard");

            Assert.AreEqual(Session.LobbyRandomMap, menu.Map);
            Assert.AreEqual("pinwheel", Session.MapId);
            Assert.AreEqual("Escape", menu.Mode);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RandomStaysChosenAcrossHouseModesEscapeAndTheHomeBuilder()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs", "courtyard");
            menu.Choose(map: Session.LobbyRandomMap);
            Assert.AreEqual(Session.LobbyRandomMap, menu.Map);
            Assert.AreEqual(Session.LobbyRandomMap, Session.LobbyMap);
            Assert.AreEqual("courtyard", Session.MapId, "Random is resolved at match start, not on the card.");
            foreach (string mode in LobbyMenu.Modes)
            {
                menu.Choose(mode: mode);
                Assert.AreEqual(Session.LobbyRandomMap, menu.Map, mode);
            }
            menu.Choose(mode: "Dibs");
            Assert.AreEqual(Session.LobbyRandomMap, menu.Map, "Leaving Escape keeps the player's Random pick.");
            menu.Choose(LobbyMenu.Workshop);
            Assert.IsTrue(Wreckabulary.Rules.HomeDesigner.Supports(menu.Map), "The Home builder opens a real house.");
            menu.Choose(LobbyMenu.Practice);
            Assert.AreEqual(Session.LobbyRandomMap, menu.Map, "Leaving the Home builder brings Random back.");
            menu.Choose(map: "flat");
            menu.Choose(mode: "Escape");
            menu.Choose(mode: "Duos");
            Assert.AreEqual("flat", menu.Map, "Leaving Escape restores an explicit house.");
            Assert.AreEqual("flat", Session.LobbyMap);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RandomSurvivesAMatchAndTheReturnToTheLobby()
        {
            LobbyMenu.Instance.Choose(LobbyMenu.Practice, "War", Session.LobbyRandomMap);
            Session.SelectMap(Session.ResolveMap("War", LobbyMenu.Instance.Map));
            yield return TestScenes.Load(Session.HubScene);
            yield return null;
            Assert.AreEqual(Session.LobbyRandomMap, LobbyMenu.Instance.Map);
            Assert.AreEqual("War", LobbyMenu.Instance.Mode);
            StringAssert.Contains("Random house", LobbyMenu.Instance.Describe());
        }

        [UnityTest]
        public IEnumerator RandomResolvesToARealHouseBeforeEveryHouseModeLoads()
        {
            var houses = GameConfig.Current.Houses;
            foreach (string mode in LobbyMenu.Modes)
            {
                var seen = Enumerable.Range(0, 40).Select(_ => Session.ResolveMap(mode, Session.LobbyRandomMap)).ToArray();
                Assert.IsTrue(seen.All(houses.ContainsKey), mode + ": " + string.Join(", ", seen));
            }
            Assert.IsTrue(houses.ContainsKey(Session.ResolveMap("Escape", null)), "Escape without a map still picks a house.");
            Assert.AreEqual("flat", Session.ResolveMap("Dibs", "flat"), "An explicit house is kept.");
            Assert.IsNull(Session.ResolveMap("Dibs", null), "No map keeps the current house.");
            Assert.IsNull(Session.ResolveMap(LobbyMenu.TutorialMode, null), "The Tutorial has its own room.");
            yield return null;
        }
    }
}
