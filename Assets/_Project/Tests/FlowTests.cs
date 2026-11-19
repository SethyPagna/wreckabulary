using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class FlowTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => TestScenes.Reset();

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        static int ModeIndex(Typewriter t, string scene) =>
            t.Modes.Select((m, i) => (m, i)).First(x => x.m.scene == scene).i;

        [UnityTest]
        public IEnumerator RoommatesWalkInThroughTheFrontDoor()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.ExploreHouse();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var p = joins.Join(new ScriptedBinding());
            float startX = p.transform.position.x;
            Assert.Less(startX, -8.7f, "arrives outside, on the porch");

            yield return new WaitForSeconds(1f);
            Assert.Greater(p.transform.position.x, -8.4f, "walked in through the doorway");
            Assert.AreEqual(1, Session.Bindings.Count, "remembered for the next scene");
        }

        [UnityTest]
        public IEnumerator FourRoommatesArrivingTogetherAllGetThroughTheDoor()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.ExploreHouse();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            for (int i = 0; i < 4; i++) joins.Join(new ScriptedBinding());
            yield return new WaitForSeconds(2f);
            foreach (var p in joins.Players)
                Assert.Greater(p.transform.position.x, -8.4f, $"{p.Name} made it inside");
        }

        [UnityTest]
        public IEnumerator TypewriterStartsDibsWithTheHouseRoommates()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.ExploreHouse();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            joins.Join(new ScriptedBinding());
            joins.Join(new ScriptedBinding());
            var typewriter = Object.FindAnyObjectByType<Typewriter>();

            Assert.IsTrue(typewriter.Choose(ModeIndex(typewriter, Session.DibsScene)));
            yield return TestScenes.WaitForActive(Session.DibsScene);

            var dibsJoins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(2, dibsJoins.HumanCount, "both roommates came along");
            Assert.AreEqual(4, dibsJoins.Players.Count, "remaining seats have AI opponents");
            Assert.AreEqual(new[] { "P1", "P2" }, dibsJoins.Players.Where(p => p.Binding is not BotBinding).Select(p => p.Name).ToArray());
            Assert.AreEqual(Phase.Countdown, RoundManager.Instance.Phase, "coming from the house skips the lobby");
        }

        [UnityTest]
        public IEnumerator TypewriterStartsSoloDibsWithAnAiOpponent()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.ExploreHouse();
            Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            var typewriter = Object.FindAnyObjectByType<Typewriter>();
            Assert.IsTrue(typewriter.Choose(ModeIndex(typewriter, Session.DibsScene)));
            yield return TestScenes.WaitForActive(Session.DibsScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(1, joins.HumanCount);
            Assert.AreEqual(4, joins.Players.Count);
            Assert.AreEqual(1, Session.Bindings.Count, "AI is not carried home");
        }

        [UnityTest]
        public IEnumerator TypewriterIsUsedWithGrabAndStepsWithUpDown()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.ExploreHouse();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            var typewriter = Object.FindAnyObjectByType<Typewriter>();
            yield return new WaitForSeconds(0.8f);

            p.Respawn(typewriter.transform.position + Vector3.back * 1.2f + Vector3.down * 0.82f);
            yield return new WaitForFixedUpdate();
            input.Next.grab = true;
            yield return null;
            yield return null;
            Assert.AreEqual(p, typewriter.User);
            Assert.IsTrue(p.Frozen, "sitting at the typewriter");

            int before = typewriter.Selected;
            input.Next.down = true;
            yield return null;
            yield return null;
            Assert.AreEqual((before + 1) % typewriter.Modes.Count, typewriter.Selected);

            input.Next.spellDown = true;
            yield return null;
            yield return null;
            Assert.IsNull(typewriter.User);
            Assert.IsFalse(p.Frozen);
        }

        [UnityTest]
        public IEnumerator KnockedOutRoommatesGetBackUpInTheHouse()
        {
            yield return TestScenes.Load(Session.HubScene);
            yield return TestScenes.ExploreHouse();
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var p = joins.Join(new ScriptedBinding());
            yield return null;
            Assert.IsTrue(p.Health.IsInvulnerable, "spawn protection");
            yield return TestScenes.WaitUntil(() => !p.Health.IsInvulnerable, 3f, "spawn protection to wear off");
            p.Health.ApplyDamage(Hits.Of(null, Vector3.forward, HitSource.Melee, 1000f, 1f));
            Assert.IsTrue(p.IsEliminated);
            yield return new WaitForSeconds(2.3f);
            Assert.IsFalse(p.IsKnockedOut);
            Assert.AreEqual(p.Health.Max, p.Health.Current);
        }

        [UnityTest]
        public IEnumerator TutorialCanBeCompleted()
        {
            bool hadDone = PlayerPrefs.HasKey(TutorialDirector.DoneKey);
            int wasDone = PlayerPrefs.GetInt(TutorialDirector.DoneKey, 0);
            PlayerPrefs.DeleteKey(TutorialDirector.DoneKey);
            try
            {
                yield return CompleteTutorial();
                Assert.IsTrue(TutorialDirector.Done, "finishing the last step marks the tutorial done for the lobby");
                yield return EndCardStartsDibs();
            }
            finally
            {
                if (hadDone) PlayerPrefs.SetInt(TutorialDirector.DoneKey, wasDone);
                else PlayerPrefs.DeleteKey(TutorialDirector.DoneKey);
                PlayerPrefs.Save();
            }
        }

        static Smashable Standing(string word) => Object.FindObjectsByType<Smashable>().FirstOrDefault(s => s.Word == word && !s.IsBroken);

        static IEnumerator ReachTheTable(TutorialDirector director, ScriptedBinding input)
        {
            input.Next.move = new Vector2(1f, 0f);
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 1, 3f, "walk step");
            input.Next.move = Vector2.zero;

            input.Next.jump = true;
            yield return new WaitForSeconds(0.8f);
            input.Next.dodge = true;
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 2, 2f, "jump and dodge step");
            yield return TestScenes.WaitUntil(() => Standing(TutorialDirector.FirstFurniture), 2f, "a TABLE to smash");
        }

        IEnumerator CompleteTutorial()
        {
            yield return TestScenes.Load(Session.TutorialScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var director = Object.FindAnyObjectByType<TutorialDirector>();
            var hud = Object.FindAnyObjectByType<GameHud>();
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            hud.Follow(p);
            yield return TestScenes.WaitUntil(() => hud.LocalPlayer == p, 1f, "the HUD follows the tutorial seat");
            Assert.AreEqual(0, p.Inventory.Count, "the tutorial starts empty-handed");
            Assert.AreEqual(11, director.StepCount);

            yield return ReachTheTable(director, input);
            Assert.IsNull(Standing("BAT"), "no special BAT box: real furniture drops the letters of its name");
            Standing(TutorialDirector.FirstFurniture).Break();
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 3, 1f, "smash step");

            p.Inventory.Set("TABLE");
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 4, 1f, "collect step");
            var bookLink = hud.transform.Find("Safe HUD/Letter bag/Bag link").GetComponent<Button>();
            bookLink.onClick.Invoke();
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 5, 1f, "recipe book step");
            bookLink.onClick.Invoke();
            yield return TestScenes.WaitUntil(() => !hud.BagOpen, 1f, "book closed again");

            yield return TestScenes.WaitUntil(() => !p.IsDodging && p.CanAct, 2f, "dodge recovery before spelling BAT");
            Assert.IsTrue(p.Summoner.Summon("BAT"));
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 6, 1f, "spell step");
            CollectionAssert.AreEquivalent("LE".ToCharArray(), p.Inventory.Letters, "spare letters stay in the bag");

            var dummy = director.Dummy;
            Assert.AreEqual("BAT", p.Combat.Weapon?.word);
            Assert.IsTrue(dummy.Health.ApplyDamage(Hits.Melee(p, Vector3.forward, p.Combat.Weapon.Stats, "BAT")));
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 7, 1f, "hit step");

            var chair = Standing("CHAIR");
            yield return new WaitForSeconds(0.3f);
            p.Respawn(chair.transform.position + Vector3.back * 1.2f);
            p.FaceTowards(Vector3.forward);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(p.Combat.TryGrab(), "grab the chair while the BAT waits in the other hand");
            p.Combat.Throw();
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 8, 1f, "throw step");
            Assert.AreEqual("BAT", p.Combat.StoredGear?.word);

            yield return TestScenes.WaitUntil(() => Standing(TutorialDirector.EffectWord), 2f, "a LAMP to smash");
            Standing(TutorialDirector.EffectWord).Break();
            p.Inventory.Set(TutorialDirector.EffectWord);
            yield return TestScenes.WaitUntil(() => p.CanAct, 2f, "ready to spell LAMP");
            Assert.IsTrue(p.Summoner.Summon(TutorialDirector.EffectWord), "LAMP fits the free hand");
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 9, 1f, "effect word step");
            Assert.AreEqual(TutorialDirector.EffectWord, p.Combat.Weapon?.word);

            Assert.IsTrue(p.Combat.SelectSlot(1 - p.Combat.ActiveSlot));
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 10, 1f, "switch hands step");
            Assert.AreEqual("BAT", p.Combat.Weapon?.word, "switching hands brings the BAT back");

            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual(TutorialDirector.DummyHealth, dummy.Health.Max, "a short fight: the dummy has less health than a player");
            Assert.AreEqual(dummy.Health.Max, dummy.Health.Current);
            int punches = Mathf.CeilToInt(dummy.Health.Max / p.Health.Rules.Unarmed.Damage) + 1;
            for (int i = 0; i < punches && dummy.Health.IsAlive; i++)
                dummy.Health.ApplyDamage(Hits.Melee(p, Vector3.forward, p.Health.Rules.Unarmed, null));
            Assert.IsTrue(dummy.IsEliminated);
            Assert.IsFalse(TutorialDirector.Done, "only the last step marks the tutorial done");
            yield return TestScenes.WaitUntil(() => director.Finished, 1f, "knockout step");
        }

        static IEnumerator EndCardStartsDibs()
        {
            var director = Object.FindAnyObjectByType<TutorialDirector>();
            var hud = Object.FindAnyObjectByType<GameHud>();
            yield return TestScenes.WaitUntil(() => director.EndCardShown && hud.ResultShown, 3f, "the end card");
            Assert.AreEqual(Session.TutorialScene, SceneManager.GetActiveScene().name, "the tutorial waits for the player instead of leaving");
            var labels = hud.GetComponentsInChildren<TMPro.TMP_Text>().Select(t => t.text).ToArray();
            CollectionAssert.Contains(labels, TutorialDirector.EndCard().NextLabel);
            CollectionAssert.Contains(labels, "Back to the lobby");
            hud.ConfirmResult();
            yield return TestScenes.WaitForActive(Session.DibsScene);
            Assert.AreEqual(TutorialDirector.PlayNextMode, Match.Mode);
            var dibs = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(1, dibs.HumanCount, "the tutorial seat came along");
            yield return TestScenes.WaitUntil(() => dibs.Players.Count > dibs.HumanCount, 3f, "bots to play against");
        }

        [UnityTest]
        public IEnumerator TutorialBringsBackTheTableWhenItsLettersRunOut()
        {
            yield return TestScenes.Load(Session.TutorialScene);
            var director = Object.FindAnyObjectByType<TutorialDirector>();
            var input = new ScriptedBinding();
            var p = Object.FindAnyObjectByType<PlayerJoinManager>().Join(input);
            yield return ReachTheTable(director, input);
            Standing(TutorialDirector.FirstFurniture).Break();
            yield return TestScenes.WaitUntil(() => director.StepIndex >= 3, 1f, "smash step");
            yield return null;
            TilePool.Instance.ReleaseAll();
            p.Inventory.Set("");
            Assert.IsNull(Standing(TutorialDirector.FirstFurniture));
            yield return TestScenes.WaitUntil(() => Standing(TutorialDirector.FirstFurniture), 2.5f, "a new TABLE once B, A and T are gone");
            Assert.AreEqual(3, director.StepIndex, "the step waits for the new letters");
        }

        [UnityTest]
        public IEnumerator BatSummonWaitsForDodgeCompletionWithoutSpendingLetters()
        {
            yield return TestScenes.Load(Session.TutorialScene);
            var input = new ScriptedBinding();
            var p = Object.FindAnyObjectByType<PlayerJoinManager>().Join(input);
            yield return null;
            p.Inventory.Set("BAT");
            var recipe = GameAssets.I.words.Find("BAT");
            Assert.IsNotNull(recipe);
            Assert.IsTrue(SummonEffects.CanApply(p, recipe), "BAT is admitted before the dodge");

            bool observed = false, dodging = false, canAct = false, canApply = true;
            bool summonedDuringDodge = true, holdingAfterAttempt = true;
            char[] lettersAfterAttempt = null;
            int reservedAfterAttempt = -1, summons = 0;
            void OnSummoned(string word) { if (word == "BAT") summons++; }
            void OnDodged(PlayerController player)
            {
                observed = true;
                dodging = player.IsDodging;
                canAct = player.CanAct;
                canApply = SummonEffects.CanApply(player, recipe);
                summonedDuringDodge = player.Summoner.Summon("BAT");
                lettersAfterAttempt = player.Inventory.Letters.ToArray();
                reservedAfterAttempt = player.Inventory.ReservedCount;
                holdingAfterAttempt = player.Combat.IsHolding;
            }

            p.Dodged += OnDodged;
            p.Summoner.Summoned += OnSummoned;
            try
            {
                input.Next.dodge = true;
                yield return TestScenes.WaitUntil(() => observed, 2f, "dodge start");
                Assert.IsTrue(dodging);
                Assert.IsTrue(canAct, "the dodge guard is the admission blocker");
                Assert.IsFalse(canApply);
                Assert.IsFalse(summonedDuringDodge);
                CollectionAssert.AreEqual("BAT".ToCharArray(), lettersAfterAttempt);
                Assert.AreEqual(0, reservedAfterAttempt);
                Assert.IsFalse(holdingAfterAttempt);
                Assert.AreEqual(0, summons);

                yield return TestScenes.WaitUntil(() => !p.IsDodging, 2f, "natural dodge completion");
                CollectionAssert.AreEqual("BAT".ToCharArray(), p.Inventory.Letters);
                Assert.IsTrue(SummonEffects.CanApply(p, recipe));
                Assert.IsTrue(p.Summoner.Summon("BAT"));
                Assert.AreEqual("BAT", p.Combat.Weapon?.word);
                Assert.AreEqual(0, p.Inventory.Count);
                Assert.AreEqual(1, summons);
            }
            finally
            {
                p.Dodged -= OnDodged;
                p.Summoner.Summoned -= OnSummoned;
            }
        }
    }
}
