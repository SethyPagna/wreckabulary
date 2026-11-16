using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class ModeGuideTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            new GameObject("Guide events", typeof(EventSystem));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ModeGuides.Close();
            Time.timeScale = 1f;
            yield return TestScenes.Reset();
        }

        static void Click(Transform root, string path)
        {
            var part = root.Find(path);
            Assert.IsNotNull(part, path);
            part.GetComponent<Button>().onClick.Invoke();
        }

        static string Label(Transform root, string path) => root.Find(path).GetComponentInChildren<TMP_Text>(true).text;

        [UnityTest]
        public IEnumerator TheGuidePagesForwardAndBackAndClosesOnTheLastPage()
        {
            int closed = 0;
            var panel = ModeGuides.Show(HouseModes.Dibs, new ScriptedBinding(), () => closed++);
            Assert.IsNotNull(panel);
            Assert.IsTrue(ModeGuides.Open);
            Assert.AreEqual(HouseModes.Dibs, ModeGuides.Mode);
            yield return null;
            var steps = ModeGuideBook.For(HouseModes.Dibs).Steps;
            var root = panel.transform;
            Assert.AreEqual(ModeGuidePanel.RootName, root.name);
            Assert.AreEqual(steps.Count, panel.PageCount);
            Assert.AreEqual(steps[0].Title, panel.TitleText);
            StringAssert.DoesNotContain("{", panel.LineText, "Keys are filled in for the viewer's device.");
            Assert.AreEqual(0, panel.ActiveDot);
            Assert.AreEqual(steps.Count, root.Find("Card/Dots").childCount);
            Assert.IsFalse(root.Find("Card/Guide back").gameObject.activeSelf, "The first page has no Back.");
            Assert.AreEqual("NEXT", Label(root, "Card/Guide next"));
            Assert.AreEqual(root.Find("Card/Guide next").gameObject, EventSystem.current.currentSelectedGameObject, "A pad starts on Next.");

            Click(root, "Card/Guide next");
            Assert.AreEqual(1, panel.Page);
            Assert.AreEqual(steps[1].Title, panel.TitleText);
            Assert.AreEqual(1, panel.ActiveDot);
            Assert.IsTrue(root.Find("Card/Guide back").gameObject.activeSelf);
            Click(root, "Card/Guide back");
            Assert.AreEqual(0, panel.Page);
            panel.Back();
            Assert.AreEqual(0, panel.Page, "Back on the first page stays put.");

            panel.GoTo(steps.Count - 1);
            Assert.AreEqual("GOT IT", Label(root, "Card/Guide next"));
            Click(root, "Card/Guide next");
            Assert.IsFalse(ModeGuides.Open, "GOT IT on the last page closes the guide.");
            Assert.AreEqual(1, closed);
            yield return null;
            Assert.IsNull(GameObject.Find(ModeGuidePanel.RootName));
        }

        [UnityTest]
        public IEnumerator TheCloseButtonAndATapOutsideCloseTheGuide()
        {
            var panel = ModeGuides.Show(HouseModes.War, new ScriptedBinding());
            yield return null;
            Click(panel.transform, "Shade");
            Assert.IsFalse(ModeGuides.Open, "A tap outside the card closes the guide.");
            yield return null;
            panel = ModeGuides.Show(HouseModes.War, new ScriptedBinding());
            yield return null;
            Click(panel.transform, "Card/Guide close");
            Assert.IsFalse(ModeGuides.Open);
            Assert.IsNull(ModeGuides.Show("Hub"), "Only modes with a guide open one.");
            Assert.IsFalse(ModeGuides.Has("Hub"));
            Assert.IsTrue(ModeGuides.Has(ModeGuideBook.TimeTrial));
        }

        [UnityTest]
        public IEnumerator AMissingPictureShowsItemArtInstead()
        {
            var panel = ModeGuides.Show(ModeGuideBook.Freeplay, new ScriptedBinding());
            yield return null;
            var step = ModeGuideBook.For(ModeGuideBook.Freeplay).Steps[0];
            if (ModeGuides.Picture(step.Image))
            {
                Assert.IsTrue(panel.PictureShown);
                Assert.AreEqual("", panel.FallbackArt);
                yield break;
            }
            Assert.IsFalse(panel.PictureShown);
            Assert.AreEqual(step.Art, panel.FallbackArt, "The empty picture shows the step's item instead.");
            Assert.Greater(panel.transform.Find("Card/Picture/Fallback").childCount, 0);
        }

        [UnityTest]
        public IEnumerator KeysFollowTheViewersDevice()
        {
            var panel = ModeGuides.Show(HouseModes.Dibs, TouchBinding.Shared);
            yield return null;
            Assert.AreEqual(ControlDevice.Touch, panel.Device);
            StringAssert.StartsWith("Tap SMASH", panel.LineText);
            ModeGuides.Close();
        }

        [UnityTest]
        public IEnumerator PauseHowToPlayOpensTheGuideForTheCurrentMode()
        {
            Match.ModeOverride = HouseModes.ShoppingList;
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(0, new ScriptedBinding());
            player.Respawn(Vector3.zero);
            var hud = new GameObject("Guide HUD", typeof(Canvas)).AddComponent<GameHud>();
            yield return null;
            hud.transform.Find("Safe HUD/Brand").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(hud.Paused);
            var card = hud.transform.Find("Pause/Pause card");
            Click(card, "Pause how to play");
            Assert.IsTrue(ModeGuides.Open);
            Assert.AreEqual(HouseModes.ShoppingList, ModeGuides.Mode);
            Assert.IsTrue(hud.GuideShown && hud.HelpShown);
            Assert.IsFalse(card.gameObject.activeSelf, "The guide takes the pause card's place.");
            Assert.IsFalse(hud.transform.Find("Pause/Pause help").gameObject.activeSelf);

            hud.TogglePause();
            Assert.IsFalse(ModeGuides.Open, "Esc or Start closes the guide first.");
            Assert.IsTrue(hud.Paused);
            Assert.IsTrue(card.gameObject.activeSelf);

            Click(card, "Pause how to play");
            ModeGuides.Close();
            Assert.IsTrue(hud.Paused, "B closes the guide back to the pause card.");
            Assert.IsTrue(card.gameObject.activeSelf);

            Click(card, "Pause how to play");
            Assert.IsTrue(ModeGuides.Open);
            hud.transform.Find("Pause/Pause help/Help done").GetComponent<Button>().onClick.Invoke();
            Assert.IsFalse(hud.Paused);
            yield return null;
            yield return null;
            Assert.IsFalse(ModeGuides.Open, "Leaving the pause menu closes the guide.");
            Object.Destroy(player.gameObject);
        }
    }
}
