using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    /// <summary>Checks the core loop: smash → scavenge → spell → summon, plus hits, knockouts and rounds.</summary>
    public class GameplayTests
    {
        GameObject ground;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Test Ground";
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SummonedThing.ClearAll();
            World.ClearTransient();
            yield return TestScenes.Reset();
        }

        static PlayerController SpawnPlayer(int index, Vector3 at, out ScriptedBinding input)
        {
            input = new ScriptedBinding();
            var p = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            p.Setup(index, input);
            p.Respawn(at);
            return p;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator SmashedFurnitureBurstsIntoItsLetters()
        {
            var sofa = LetterBuilt.Spawn("SOFA", Vector3.one * 0.5f, 0, Color.red, null);
            sofa.gameObject.AddComponent<Rigidbody>();
            sofa.gameObject.AddComponent<Smashable>().Init("SOFA", 10f);
            yield return null;

            sofa.GetComponent<Smashable>().TakeHit(20f);
            yield return null;

            Assert.IsTrue(sofa == null, "sofa should be destroyed");
            var letters = new string(TilePool.Instance.Active.Select(t => t.Letter).OrderBy(c => c).ToArray());
            Assert.AreEqual("AFOS", letters);
        }

        [UnityTest]
        public IEnumerator BoxesLandingOnBoxesDontBreak()
        {
            var bottom = DeliverySpawner.CreateBox("SOFA", Vector3.zero);
            yield return new WaitForSeconds(2f); // past its spawn grace
            var top = DeliverySpawner.CreateBox("LAMP", Vector3.up * 4f);
            top.GetComponent<Rigidbody>().linearVelocity = Vector3.down * 10f;
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(bottom && top, "neither box broke");
        }

        [UnityTest]
        public IEnumerator ThrownThingsSmashWhatTheyHit()
        {
            var bottom = DeliverySpawner.CreateBox("SOFA", Vector3.zero);
            yield return new WaitForSeconds(2f);
            var thrown = DeliverySpawner.CreateBox("LAMP", Vector3.up * 3f);
            ThrowTracker.Attach(thrown.gameObject, null, 2f);
            thrown.GetComponent<Rigidbody>().linearVelocity = Vector3.down * 14f;
            yield return new WaitForSeconds(1f);
            Assert.IsFalse(bottom && thrown, "the throw smashed something");
        }

        [UnityTest]
        public IEnumerator PlayerCollectsNearbyTiles()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            TilePool.Instance.Get('B').Launch(new Vector3(0.3f, 0.3f, 0.3f), Vector3.zero);
            yield return new WaitForSeconds(0.5f);

            CollectionAssert.AreEqual(new[] { 'B' }, p.Inventory.Letters.ToArray());
            Assert.AreEqual(0, TilePool.Instance.Active.Count);
        }

        [UnityTest]
        public IEnumerator DroppedTilesCantBeRegrabbedInstantly()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            yield return Frames(2);
            p.Inventory.Set("AB");
            p.Inventory.DropRandom(2, p.transform.position, Vector3.forward);
            Assert.AreEqual(0, p.Inventory.Count);
            yield return Frames(10);
            Assert.AreEqual(0, p.Inventory.Count, "the dropper shouldn't instantly re-collect their own tiles");
        }

        [UnityTest]
        public IEnumerator SummoningSpendsLettersAndEquipsWeapon()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            yield return Frames(2);
            p.Inventory.Set("BLADEX");

            Assert.IsTrue(p.Summoner.Summon("BLADE"));
            CollectionAssert.AreEqual(new[] { 'X' }, p.Inventory.Letters.ToArray());
            Assert.IsNotNull(p.Combat.Weapon);
            Assert.AreEqual("BLADE", p.Combat.Weapon.word);
            Assert.That(p.Combat.Weapon.transform.lossyScale.x, Is.EqualTo(1f).Within(0.15f), "held weapons keep their size (give or take squash and stretch)");
            Assert.IsFalse(p.Summoner.Summon("SWORD"), "can't summon without the letters");
        }

        [UnityTest]
        public IEnumerator WordWheelFromInputSummonsSelectedWord()
        {
            var p = SpawnPlayer(0, Vector3.zero, out var input);
            yield return Frames(2);
            p.Inventory.Set("BLADEW");

            input.Next.spellHeld = true;
            input.Next.spellDown = true;
            yield return null;
            yield return null;
            Assert.IsTrue(p.Summoner.IsSpelling);
            Assert.AreEqual("BLADE", p.Summoner.SelectedWord?.word);

            input.Next.spellHeld = false;
            input.Next.spellUp = true;
            yield return null;
            yield return null;

            Assert.IsFalse(p.Summoner.IsSpelling);
            CollectionAssert.AreEqual(new[] { 'W' }, p.Inventory.Letters.ToArray());
            Assert.AreEqual("BLADE", p.Combat.Weapon?.word);
        }

        [UnityTest]
        public IEnumerator HitKnocksTwoLettersLoose()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            yield return Frames(2);
            p.Inventory.Set("ABCD");

            Assert.IsTrue(p.Health.TakeHit(Vector3.forward));
            Assert.AreEqual(2, p.Inventory.Count);
            Assert.AreEqual(2, TilePool.Instance.Active.Count);
            Assert.IsFalse(p.Health.TakeHit(Vector3.forward), "brief invulnerability after a hit");
        }

        [UnityTest]
        public IEnumerator HitWithNoLettersIsKnockout()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            yield return Frames(2);
            p.Inventory.Set("");
            bool knockedOut = false;
            p.Health.KnockedOut += _ => knockedOut = true;

            p.Health.TakeHit(Vector3.forward);
            Assert.IsTrue(knockedOut);
            Assert.IsTrue(p.IsKnockedOut);
        }

        [UnityTest]
        public IEnumerator PunchFromInputHitsOpponentInFront()
        {
            var attacker = SpawnPlayer(0, Vector3.zero, out var input);
            var victim = SpawnPlayer(1, new Vector3(0f, 0f, 1.1f), out _);
            yield return Frames(3);
            attacker.FaceTowards(Vector3.forward);
            victim.Inventory.Set("ABC");

            input.Next.attack = true;
            yield return null;
            yield return null;

            Assert.AreEqual(1, victim.Inventory.Count);
        }

        [UnityTest]
        public IEnumerator ArmorAbsorbsOneHit()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            yield return Frames(2);
            p.Inventory.Set("ARMORS");
            Assert.IsTrue(p.Summoner.Summon("ARMOR"));
            Assert.AreEqual(1, p.Health.ArmorCharges);

            p.Health.TakeHit(Vector3.forward);
            Assert.AreEqual(1, p.Inventory.Count, "armor soaked the hit, the S stays");
            Assert.AreEqual(0, p.Health.ArmorCharges);
        }

        [UnityTest]
        public IEnumerator EveryListedWordCanBeSummoned()
        {
            var p = SpawnPlayer(0, Vector3.zero, out _);
            var foe = SpawnPlayer(1, new Vector3(3f, 0f, 0f), out _);
            yield return Frames(2);

            foreach (var entry in GameAssets.I.words.Words.Where(w => w.word.Length <= p.Inventory.Capacity))
            {
                p.Health.ResetForRound();
                foe.Health.ResetForRound();
                foe.Inventory.Set("AAAAAA");
                p.Inventory.Set(entry.word);
                Assert.IsTrue(p.Summoner.Summon(entry), $"summon {entry.word}");
                yield return Frames(3);
                p.Combat.ResetForRound();
                SummonedThing.ClearAll();
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator RoundEndsWhenOneRoommateIsLeft()
        {
            yield return SceneManager.LoadSceneAsync("LivingRoom");
            yield return null;
            var rounds = RoundManager.Instance;
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var a = joins.Join(new ScriptedBinding());
            var b = joins.Join(new ScriptedBinding());
            rounds.CountdownTime = 0.1f;

            rounds.StartMatch();
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(Phase.Playing, rounds.Phase);
            Assert.AreEqual(3, a.Inventory.Count, "starter letters");

            b.Inventory.Set("");
            b.Health.TakeHit(Vector3.forward);
            Assert.AreEqual(Phase.RoundOver, rounds.Phase);
            Assert.AreEqual(1, rounds.WinsOf(a));
            Assert.AreEqual(0, rounds.WinsOf(b));

            yield return new WaitForSecondsRealtime(3.3f);
            Assert.AreEqual(2, rounds.Round);
            Assert.That(rounds.Phase, Is.EqualTo(Phase.Countdown).Or.EqualTo(Phase.Playing));
            Assert.IsFalse(b.IsKnockedOut, "knocked-out players get back up for the next round");
        }
    }
}
