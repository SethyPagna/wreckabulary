using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public sealed class LobbyNavigationRefinementTests
    {
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey };
        string[] saved;
        float volume;
        int vsync;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            volume = AudioListener.volume; vsync = QualitySettings.vSyncCount;
            yield return TestScenes.Reset();
            foreach (string key in Keys) PlayerPrefs.DeleteKey(key);
            PlayerPrefs.SetString(MatchTally.CareerKey, new Career { Coins = 1234 }.Serialize());
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning,
                2f, "navigation ready");
            Canvas.ForceUpdateCanvases();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
            for (int i = 0; i < Keys.Length; i++)
                if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            PlayerPrefs.Save();
            AudioListener.volume = volume; QualitySettings.vSyncCount = vsync;
        }

        static Button Nav(string name) => LobbyMenu.Instance.transform.Find("Safe area/Top bar")
            .GetComponentsInChildren<Button>().Single(button => button.name == name);

        [UnityTest]
        public IEnumerator CoinBalanceOpensShopThroughSubmitAndClosingReturnsFocusToItsLiveBalance()
        {
            var menu = LobbyMenu.Instance;
            var balance = Nav("Shop");
            Assert.AreEqual("Wallet", balance.transform.parent.name);
            Assert.AreEqual("1,234", balance.GetComponentInChildren<TMP_Text>().text);
            Assert.IsTrue(balance.GetComponentsInChildren<Transform>().Any(child => child.name == "Coin"));
            Assert.IsFalse(menu.transform.Find("Safe area/Top bar").GetComponentsInChildren<Image>()
                .Any(image => image.sprite && image.sprite.name == "Lobby icon cart"), "shop navigation uses the coin balance");
            var rect = (RectTransform)balance.transform;
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = rect.TransformPoint(rect.rect.center) }, hits);
            Assert.IsNotEmpty(hits);
            Assert.IsTrue(hits[0].gameObject.transform.IsChildOf(balance.transform), "the balance has a working pointer target");
            EventSystem.current.SetSelectedGameObject(balance.gameObject);
            ExecuteEvents.Execute(balance.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.AreEqual(LobbyMenu.Shop, menu.Current);
            Assert.IsTrue(balance.Body().Find("Underline").GetComponent<Image>().enabled);
            menu.Career.Coins = 987654;
            menu.SaveCareer();
            Assert.AreEqual("987,654", balance.GetComponentInChildren<TMP_Text>().text);
            menu.Close();
            yield return null;
            Assert.AreEqual(LobbyMenu.Home, menu.Current);
            Assert.AreSame(balance.gameObject, EventSystem.current.currentSelectedGameObject);
            Assert.AreSame(balance, Nav("Shop"), "a changing balance retains the same navigation target");
        }

        [UnityTest]
        public IEnumerator PlayShowsTheChosenModeAndTheGoldTrophyKeepsItsCareerPositionAndReturnFocus()
        {
            var menu = LobbyMenu.Instance;
            var play = Nav("PLAY");
            var title = play.GetComponentsInChildren<TMP_Text>().Single(text => text.name == "Selected mode");
            foreach (string mode in LobbyMenu.Modes.Concat(new[] { LobbyMenu.TutorialMode, LobbyMenu.WorkshopMode }))
            {
                menu.Choose(mode == LobbyMenu.WorkshopMode ? LobbyMenu.Workshop : LobbyMenu.Practice, mode);
                Canvas.ForceUpdateCanvases(); title.ForceMeshUpdate();
                Assert.AreEqual(LobbyMenu.ModeName(mode), title.text);
                Assert.GreaterOrEqual(title.fontSize, 18f);
                Assert.IsFalse(title.isTextOverflowing, "the selected mode stays readable: " + mode);
                var glyphs = title.textInfo.characterInfo.Take(title.textInfo.characterCount).Where(character => character.isVisible).ToArray();
                Assert.AreEqual(title.text.Count(character => !char.IsWhiteSpace(character)), glyphs.Length,
                    mode + " must render every non-space glyph, including the first one; a blank mesh is not a fitting label");
                foreach (var glyph in glyphs)
                {
                    Assert.AreNotEqual('\u2026', glyph.character, mode + " cannot collapse to an ellipsis");
                    Assert.Greater(glyph.topRight.x - glyph.bottomLeft.x, 0f);
                    Assert.Greater(glyph.topRight.y - glyph.bottomLeft.y, 0f);
                    var vertices = title.textInfo.meshInfo[glyph.materialReferenceIndex].vertices;
                    var bounds = (RectTransform)play.transform;
                    for (int vertex = glyph.vertexIndex; vertex < glyph.vertexIndex + 4; vertex++)
                    {
                        var local = bounds.InverseTransformPoint(title.transform.TransformPoint(vertices[vertex]));
                        Assert.IsTrue(bounds.rect.Contains(new Vector2(local.x, local.y)), mode + " mesh remains within its Play control");
                    }
                }
            }
            var trophy = Nav("Leaderboard");
            var career = Nav("CAREER");
            Assert.AreSame(career.transform.parent, trophy.transform.parent);
            Assert.AreEqual(career.transform.GetSiblingIndex() + 1, trophy.transform.GetSiblingIndex());
            var icon = trophy.GetComponentsInChildren<Image>().Single(image => image.sprite && image.sprite.name == "Lobby icon trophy");
            Assert.AreEqual(LobbyKit.Sun, icon.color);
            EventSystem.current.SetSelectedGameObject(trophy.gameObject);
            ExecuteEvents.Execute(trophy.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.AreEqual(LobbyMenu.Trophy, menu.Current);
            Assert.AreEqual(LobbyKit.Sun, icon.color, "selection preserves the coloured trophy");
            menu.Close();
            yield return null;
            Assert.AreSame(trophy.gameObject, EventSystem.current.currentSelectedGameObject);
            play.onClick.Invoke();
            Assert.AreEqual(LobbyMenu.Play, menu.Current);
            menu.Close();
            Assert.AreSame(play.gameObject, EventSystem.current.currentSelectedGameObject);
        }
    }
}
