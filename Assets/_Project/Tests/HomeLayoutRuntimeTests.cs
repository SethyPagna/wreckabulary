using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Wreckabulary.Tests
{
    public sealed class HomeLayoutRuntimeTests
    {
        static readonly string[] Keys = { MatchTally.CareerKey, LobbyMenu.OutfitKey, LobbyMenu.TurnHintKey, LobbyThemes.PreferenceKey };
        string[] saved;
        float savedDelay;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            saved = Keys.Select(key => PlayerPrefs.HasKey(key) ? PlayerPrefs.GetString(key) : null).ToArray();
            savedDelay = LobbyMenu.StartDelay;
            PlayerPrefs.DeleteKey(LobbyMenu.TurnHintKey);
            yield return TestScenes.Reset();
            Assert.IsNotNull(Resources.Load<LobbyHomeLayout>(LobbyHomeLayout.ResourcePath), "Run the explicit saved Home setup before this integration fixture.");
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.WaitUntil(() => LobbyMenu.Instance && GameHud.Active && !GameHud.Active.StateController.IsTransitioning, 2f, "lobby ready");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (LobbyMenu.Instance && LobbyMenu.Instance.Starting) LobbyMenu.Instance.CancelStart();
            LobbyMenu.StartDelay = savedDelay;
            yield return TestScenes.Reset();
            if (saved != null)
                for (int i = 0; i < Keys.Length; i++)
                    if (saved[i] == null) PlayerPrefs.DeleteKey(Keys[i]); else PlayerPrefs.SetString(Keys[i], saved[i]);
            PlayerPrefs.Save(); LobbyThemes.Restore();
        }

        static LobbyHomePageView View(LobbyMenu menu)
        {
            var views = menu.Page<HomePage>().Root.GetComponentsInChildren<LobbyHomePageView>();
            Assert.AreEqual(1, views.Length, "Refresh retains one active Home view.");
            return views[0];
        }

        [UnityTest]
        public IEnumerator SavedHomeShowsOneBigPlayButtonInItsAuthoredStyleAndKeepsPartyFocus()
        {
            var menu = LobbyMenu.Instance;
            var profile = Resources.Load<LobbyHomeLayout>(LobbyHomeLayout.ResourcePath);
            menu.Choose(LobbyMenu.Practice, "Dibs"); menu.Open(LobbyMenu.Home);
            yield return null;
            string outfit = menu.Outfit.Serialize(), savedOutfit = PlayerPrefs.GetString(LobbyMenu.OutfitKey);
            int coins = menu.Career.Coins;
            var page = View(menu);
            Assert.AreSame(page.Start.Button, menu.Page<HomePage>().First);
            Assert.AreEqual(LobbyHomePageView.PlayButton, page.Start.Button.name);
            Assert.AreEqual(LobbyHomePageView.PlayLabel, page.Start.Label.text);
            AssertOnlyPlay(page);
            Assert.AreEqual(profile.Page.Dock.anchoredPosition, page.Dock.anchoredPosition);
            Assert.AreEqual(profile.Page.Dock.sizeDelta.x, page.Dock.sizeDelta.x);
            Assert.AreEqual(Mathf.Max(profile.Page.Start.GetComponent<LayoutElement>().preferredHeight, LobbyHomePageView.PlayHeight),
                page.Start.GetComponent<LayoutElement>().preferredHeight, "the saved button grows to the big PLAY height");
            Assert.AreSame(LobbyRuntimeTypography.ResolveFont(profile.Page.Start.Label.font), page.Start.Label.font);
            Assert.AreSame(LobbyRuntimeTypography.ResolveMaterial(profile.Page.Start.Label.font, profile.Page.Start.Label.fontSharedMaterial), page.Start.Label.fontSharedMaterial);
            page.Start.Button.onClick.Invoke();
            yield return null;
            Assert.AreEqual(LobbyMenu.Play, menu.Current, "PLAY opens the Play section");
            Assert.IsFalse(menu.Starting, "and never starts a match by itself");
            Assert.AreEqual("Mode " + menu.Mode, EventSystem.current.currentSelectedGameObject.name);
            menu.Open(LobbyMenu.Home);
            var partner = new KeyboardBinding(KeyboardBinding.Side.Right);
            Assert.AreEqual(1, menu.PartySize);
            EventSystem.current.SetSelectedGameObject(View(menu).Start.Button.gameObject);
            menu.Join(partner);
            Assert.AreEqual(2, menu.PartySize);
            Assert.AreEqual(LobbyHomePageView.PlayButton, EventSystem.current.currentSelectedGameObject.name);
            menu.Leave(partner);
            yield return null;
            Assert.AreEqual(1, menu.PartySize);
            Assert.AreEqual(LobbyHomePageView.PlayButton, EventSystem.current.currentSelectedGameObject.name);
            View(menu);
            menu.Choose(LobbyMenu.Matchmaking, "Dibs"); menu.Open(LobbyMenu.Home);
            page = View(menu);
            Assert.IsTrue(page.Start.Button.interactable, "PLAY only opens the Play section, so it stays pressable");
            Assert.AreSame(page.Start.Button, menu.Page<HomePage>().First);
            menu.Go();
            Assert.IsFalse(menu.Starting);
            menu.Choose(LobbyMenu.Practice, LobbyMenu.TutorialMode); menu.Open(LobbyMenu.Home);
            page = View(menu);
            AssertOnlyPlay(page);
            Assert.AreEqual(outfit, menu.Outfit.Serialize()); Assert.AreEqual(savedOutfit, PlayerPrefs.GetString(LobbyMenu.OutfitKey));
            Assert.AreEqual(coins, menu.Career.Coins);
        }

        static void AssertOnlyPlay(LobbyHomePageView page)
        {
            Assert.IsTrue(page.Start.gameObject.activeSelf);
            Assert.IsFalse(page.Mode.gameObject.activeSelf || page.House.gameObject.activeSelf || page.Wordbound.gameObject.activeSelf || page.WordboundNote.gameObject.activeSelf,
                "Home shows only PLAY");
            CollectionAssert.AreEqual(new[] { LobbyHomePageView.PlayButton }, page.Dock.GetComponentsInChildren<Button>().Select(button => button.name).ToArray());
        }

        [UnityTest]
        public IEnumerator SavedHomeKeepsTurnHintAndCancelableCountdownLifecycle()
        {
            var menu = LobbyMenu.Instance;
            menu.Choose(LobbyMenu.Practice, "Dibs"); menu.Open(LobbyMenu.Home);
            yield return null;
            var initial = View(menu);
            Assert.IsTrue(initial.TurnHint.gameObject.activeInHierarchy);
            var turn = menu.GetComponentsInChildren<LobbyDrag>().Single(drag => drag.name == "Turn area");
            ExecuteEvents.Execute(turn.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(turn.gameObject, new PointerEventData(EventSystem.current) { delta = new Vector2(-40, 0) }, ExecuteEvents.dragHandler);
            Assert.AreEqual("seen", PlayerPrefs.GetString(LobbyMenu.TurnHintKey));
            yield return TestScenes.WaitUntil(() => !initial.TurnHint || !initial.TurnHint.gameObject.activeInHierarchy, 1f, "turn hint dismissal");
            menu.Open(LobbyMenu.Home);
            var page = View(menu);
            Assert.IsFalse(page.TurnHint.gameObject.activeSelf);
            LobbyMenu.StartDelay = 3600f;
            try
            {
                menu.Go();
                Assert.IsTrue(menu.Starting);
                Assert.IsFalse(View(menu).Dock.gameObject.activeSelf);
                Assert.IsNull(menu.Page<HomePage>().First);
                Assert.AreEqual("CANCEL", EventSystem.current.currentSelectedGameObject.name);
            }
            finally
            {
                menu.CancelStart(); LobbyMenu.StartDelay = savedDelay;
            }
            yield return null;
            Assert.IsFalse(menu.Starting);
            Assert.IsTrue(View(menu).Dock.gameObject.activeSelf);
            Assert.AreEqual(LobbyHomePageView.PlayButton, EventSystem.current.currentSelectedGameObject.name);
            Assert.AreEqual(Session.HubScene, SceneManager.GetActiveScene().name);
        }
    }
}
