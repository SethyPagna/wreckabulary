using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public class HudPresentationTests
    {
        sealed class StartInput : InputBinding
        {
            public bool pressed;
            public override string Id => "ui-submit-start-test";
            public override void Read(ref PlayerCommands commands) => commands.start = pressed;
            public override bool JoinPressed() => false;
            public override bool StartPressed() => pressed;
        }

        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        [UnityTest]
        public IEnumerator PauseRestoresFrozenPlayersAndTheOriginalSimulationSpeed()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var player = joins.Join(new ScriptedBinding());
            var buddy = joins.Join(new ScriptedBinding());
            yield return null;
            player.Frozen = false; buddy.Frozen = true;
            Time.timeScale = 0.75f;
            var hud = Object.FindAnyObjectByType<GameHud>();
            hud.TogglePause();
            Assert.AreEqual(UIState.PauseMenu, hud.StateController.CurrentState);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(player.Frozen && buddy.Frozen);
            yield return new WaitForSecondsRealtime(0.25f);
            var pause = hud.transform.Find("Pause").GetComponent<CanvasGroup>();
            Assert.AreEqual(1f, pause.alpha);
            Assert.IsTrue(pause.blocksRaycasts);
            hud.TogglePause();
            Assert.AreEqual(0.75f, Time.timeScale);
            Assert.IsFalse(player.Frozen);
            Assert.IsTrue(buddy.Frozen, "Existing typewriter or mode freezes must survive pause/resume.");
        }

        [UnityTest]
        public IEnumerator GearSlotsStackBesideLettersAndHudFadesWithoutDeactivation()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            joins.Join(new StartInput());
            yield return new WaitForSecondsRealtime(0.15f);
            var hud = Object.FindAnyObjectByType<GameHud>();
            var bag = hud.transform.Find("Safe HUD/Letter bag");
            var first = (RectTransform)bag.Find("Gear slot 1");
            var second = (RectTransform)bag.Find("Gear slot 2");
            Assert.AreEqual(first.anchoredPosition.x, second.anchoredPosition.x);
            Assert.Greater(first.anchoredPosition.y, second.anchoredPosition.y);
            Assert.Greater(first.anchoredPosition.x, ((RectTransform)bag.Find("Letter 1")).anchoredPosition.x);
            Assert.AreEqual(1f, bag.GetComponent<CanvasGroup>().alpha);
            hud.StateController.FadeDuration = 0f;
            hud.TogglePause();
            Assert.IsTrue(bag.gameObject.activeInHierarchy);
            Assert.AreEqual(0f, hud.transform.Find("Safe HUD").GetComponent<CanvasGroup>().alpha);
        }

        [UnityTest]
        public IEnumerator LobbyRegistersMainMenuAndExplorationRevealsGameplay()
        {
            yield return TestScenes.Load(Session.HubScene);
            var controller = Object.FindAnyObjectByType<UIStateController>();
            Assert.AreEqual(UIState.MainMenu, controller.CurrentState);
            var lobby = Object.FindAnyObjectByType<LobbyMenu>();
            Assert.IsNotNull(lobby);
            var menu = lobby.GetComponent<CanvasGroup>();
            var player = Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            Assert.IsTrue(player.Frozen, "Menu selections must not also move or attack with the roommate.");
            Assert.AreEqual(1f, Time.timeScale, "The main menu keeps the background house animated.");
            controller.FadeDuration = 0f;
            controller.TransitionToState(UIState.GameplayHUD);
            Assert.IsFalse(player.Frozen);
            Assert.AreEqual(0f, menu.alpha);
            Assert.IsTrue(menu.gameObject.activeInHierarchy);
            Assert.IsFalse(menu.blocksRaycasts);
            Assert.IsTrue(Object.FindAnyObjectByType<PlayerJoinManager>().AllowJoining);
        }

        [UnityTest]
        public IEnumerator PauseStopsCountdownAndResumesItsRemainingTime()
        {
            yield return TestScenes.Load(Session.DibsScene);
            Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            var rounds = RoundManager.Instance;
            rounds.CountdownTime = 0.5f;
            rounds.StartMatch();
            var hud = Object.FindAnyObjectByType<GameHud>();
            hud.TogglePause();
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.AreEqual(Phase.Countdown, rounds.Phase);
            Assert.AreEqual(0f, Time.timeScale);
            hud.TogglePause();
            yield return null;
            Assert.AreEqual(Phase.Countdown, rounds.Phase, "Paused wall-clock time must not skip the countdown.");
            yield return TestScenes.WaitUntil(() => rounds.Phase == Phase.Playing, 2f, "resumed countdown");
        }

        [UnityTest]
        public IEnumerator PausingCraftKeepsReservedLettersAndCompletesAfterResume()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var player = Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            yield return null;
            player.Inventory.Collects = false;
            player.Inventory.Set("BAT");
            Assert.IsTrue(player.Summoner.BeginCraft(GameAssets.I.words.Find("BAT")));
            var hud = Object.FindAnyObjectByType<GameHud>();
            hud.TogglePause();
            float progress = player.Summoner.CraftProgress;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.IsTrue(player.Summoner.IsCrafting);
            Assert.AreEqual(3, player.Inventory.ReservedCount);
            Assert.AreEqual(progress, player.Summoner.CraftProgress, 0.001f);
            hud.TogglePause();
            yield return TestScenes.WaitUntil(() => !player.Summoner.IsCrafting, 3f, "craft after pause");
            Assert.AreEqual("BAT", player.Combat.Weapon.word);
            Assert.AreEqual(0, player.Inventory.ReservedCount);
        }

        [UnityTest]
        public IEnumerator PausingDeploymentKeepsTheChannelAndGearUntilResume()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var player = Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            yield return null;
            player.Inventory.Set("MAT");
            Assert.IsTrue(player.Summoner.Summon("MAT"));
            Assert.IsTrue(player.Combat.DeployHeld());
            var hud = Object.FindAnyObjectByType<GameHud>();
            hud.TogglePause();
            yield return new WaitForSecondsRealtime(0.8f);
            Assert.IsTrue(player.Combat.IsDeploying);
            Assert.AreEqual("MAT", player.Combat.Weapon.word);
            Assert.AreEqual(0, DeployedGear.CountFor(player));
            hud.TogglePause();
            yield return TestScenes.WaitUntil(() => !player.Combat.IsDeploying, 2f, "deployment after pause");
            Assert.AreEqual(1, DeployedGear.CountFor(player));
        }

        [UnityTest]
        public IEnumerator PauseBlocksSeparateStartAndRetryControls()
        {
            yield return TestScenes.Load(Session.DibsScene);
            Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            yield return null;
            var hud = Object.FindAnyObjectByType<GameHud>();
            int starts = 0;
            hud.ShowModeActions(true, "START WITH AI", () => starts++);
            var actions = hud.transform.Find("Safe HUD/Mode actions");
            var group = actions.GetComponent<CanvasGroup>();
            Assert.IsTrue(group.interactable);
            hud.TogglePause();
            Assert.AreEqual(0f, group.alpha);
            Assert.IsFalse(group.interactable || group.blocksRaycasts);
            Assert.IsTrue(group.gameObject.activeInHierarchy);
            hud.ShowModeActions(true, "START WITH AI");
            Assert.IsFalse(group.blocksRaycasts, "A director refresh cannot reactivate gameplay controls under a menu.");
            actions.Find("Play").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(0, starts, "The callback also rejects menu input.");
            var pauseRect = (RectTransform)hud.transform.Find("Pause");
            Canvas.ForceUpdateCanvases();
            Assert.AreEqual(((RectTransform)hud.transform).rect.size, pauseRect.rect.size,
                "The pause backdrop must cover the complete HUD, including its screen edges.");
            Assert.AreEqual(hud.transform, pauseRect.parent, "Pause is a sibling of the fading gameplay view.");
            Assert.Greater(pauseRect.GetSiblingIndex(), hud.transform.Find("Safe HUD").GetSiblingIndex());
            hud.TogglePause();
            Assert.IsFalse(group.interactable, "Input waits for the resumed HUD fade.");
            actions.Find("Play").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.AreEqual(0, starts);
            yield return new WaitForSecondsRealtime(0.25f);
            Assert.IsTrue(group.interactable && group.blocksRaycasts);
        }

        [UnityTest]
        public IEnumerator PendingResultsWaitForPauseToResume()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var hud = Object.FindAnyObjectByType<GameHud>();
            hud.ShowResultSoon(new HudResult { Heading = "Round complete" }, null, .15f);
            hud.TogglePause();
            yield return new WaitForSecondsRealtime(.35f);
            Assert.IsTrue(hud.Paused);
            Assert.IsFalse(hud.ResultShown, "A delayed result must not dismiss a player's pause menu.");
            Assert.AreEqual(0f, Time.timeScale);
            hud.TogglePause();
            yield return TestScenes.WaitUntil(() => hud.ResultShown, 1f, "result after resume");
        }

        [UnityTest]
        public IEnumerator ResumeSubmitCannotAlsoStartTheLobbyMatchDuringTheFade()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var input = new StartInput();
            Object.FindAnyObjectByType<PlayerJoinManager>().Join(input);
            yield return null;
            var hud = Object.FindAnyObjectByType<GameHud>();
            hud.StateController.FadeDuration = 0.5f;
            hud.TogglePause();
            input.pressed = true;
            yield return null;
            Assert.AreEqual(Phase.Lobby, RoundManager.Instance.Phase);

            hud.TogglePause();
            Assert.IsTrue(hud.StateController.IsTransitioning);
            yield return null;
            Assert.AreEqual(Phase.Lobby, RoundManager.Instance.Phase,
                "The same Submit/Start input must not start a match while Resume is fading.");
            input.pressed = false;
            yield return new WaitForSecondsRealtime(0.6f);
            Assert.AreEqual(Phase.Lobby, RoundManager.Instance.Phase);
            input.pressed = true;
            yield return null;
            yield return null;
            Assert.AreEqual(Phase.Countdown, RoundManager.Instance.Phase,
                "A new Start press must work once the gameplay HUD is ready.");
        }
    }
}
