using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class ScreenShakeTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            ScreenShake.Reset();
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ScreenShake.Reset();
            Time.timeScale = 1f;
            yield return TestScenes.Reset();
        }

        [Test]
        public void TheSettingDefaultsToFullAndIsRemembered()
        {
            Assert.AreEqual(ShakeLevel.Full, ScreenShake.Level);
            Assert.AreEqual(1f, ScreenShake.Scale);
            ScreenShake.Level = ShakeLevel.Low;
            Assert.AreEqual((int)ShakeLevel.Low, PlayerPrefs.GetInt(ScreenShake.Key));
            Assert.AreEqual(.5f, ScreenShake.Scale);
            ScreenShake.Level = ShakeLevel.Off;
            Assert.AreEqual(0f, ScreenShake.Scale);
        }

        [UnityTest]
        public IEnumerator TheCameraShakesOnlyAsMuchAsTheSettingAllows()
        {
            var rig = new GameObject("Shake camera").AddComponent<Camera>().gameObject.AddComponent<CameraRig>();
            yield return null;
            var field = typeof(CameraRig).GetField("shake", BindingFlags.NonPublic | BindingFlags.Instance);
            ScreenShake.Level = ShakeLevel.Off;
            CameraRig.Shake(1f);
            Assert.AreEqual(0f, (float)field.GetValue(rig), 1e-4f, "Off keeps the camera still.");
            ScreenShake.Level = ShakeLevel.Low;
            CameraRig.Shake(1f);
            Assert.AreEqual(.5f, (float)field.GetValue(rig), 1e-4f, "Low halves every shake.");
            Object.Destroy(rig.gameObject);
        }

        [UnityTest]
        public IEnumerator PauseSettingsStepTheScreenShake()
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, TouchBinding.Shared);
            var hud = new GameObject("Shake HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.StateController.FadeDuration = 0f;
            hud.TogglePause();
            Assert.IsTrue(hud.Paused);
            hud.transform.Find("Pause/Pause card/Pause settings").GetComponent<Button>().onClick.Invoke();
            Button Find(string name) => hud.GetComponentsInChildren<Button>(true).First(b => b.name == name);
            Find("Pause tab gameplay").onClick.Invoke();
            var value = hud.GetComponentsInChildren<TMP_Text>(true).First(t => t.name == "Value" && t.transform.parent.name == "Screen shake");
            Assert.AreEqual("FULL", value.text);
            Find("Screen shake less").onClick.Invoke();
            Assert.AreEqual("LOW", value.text);
            Assert.AreEqual(ShakeLevel.Low, ScreenShake.Level);
            Find("Screen shake less").onClick.Invoke();
            Assert.AreEqual("OFF", value.text);
            Find("Screen shake less").onClick.Invoke();
            Assert.AreEqual("OFF", value.text, "Off is the floor.");
            Object.Destroy(hud.gameObject);
            Object.Destroy(player.gameObject);
        }
    }
}
