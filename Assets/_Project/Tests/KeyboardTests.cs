using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    /// <summary>
    /// End to end with a simulated keyboard: real key presses through <see cref="KeyboardBinding"/>,
    /// not the scripted shortcut the other tests use.
    /// </summary>
    public class KeyboardTests : InputTestFixture
    {
        Keyboard kb;

        public override void Setup()
        {
            base.Setup();
            kb = InputSystem.AddDevice<Keyboard>();
        }

        /// <summary>Presses and releases a key, one frame each, the way the game sees a tap.</summary>
        IEnumerator Tap(KeyControl key)
        {
            Press(key, queueEventOnly: true);
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
        }

        IEnumerator SpawnKeyboardPlayer(System.Action<PlayerController> got)
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
            var p = Object.Instantiate(GameAssets.I.playerPrefab);
            p.Setup(0, new KeyboardBinding(KeyboardBinding.Side.Left));
            p.Respawn(Vector3.zero);
            p.FaceTowards(Vector3.forward);
            yield return null;
            got(p);
        }

        [UnityTest]
        public IEnumerator SpellingAWeaponWithTheKeyboard()
        {
            PlayerController p = null;
            yield return SpawnKeyboardPlayer(x => p = x);
            p.Inventory.Set("BAT");

            yield return Tap(kb.kKey);
            Assert.IsTrue(p.Summoner.IsSpelling, "K starts spelling");
            for (int i = 0; i < 3; i++) yield return Tap(kb.spaceKey);
            Assert.AreEqual("BAT", p.Summoner.Spelled, "SPACE adds letters");
            yield return Tap(kb.kKey);

            Assert.AreEqual(0, p.Inventory.Count, "letters spent");
            Assert.AreEqual("BAT", p.Combat.Weapon?.word, "BAT in hand");
        }

        [UnityTest]
        public IEnumerator SpellingAnObjectWithTheKeyboardBuildsIt()
        {
            PlayerController p = null;
            yield return SpawnKeyboardPlayer(x => p = x);
            p.Inventory.Set("ELBAT"); // backwards, so A/D are needed

            yield return Tap(kb.kKey);
            foreach (char want in "TABLE")
            {
                for (int guard = 0; guard < 6 && p.Inventory.Letters[p.Summoner.Cursor] != want; guard++)
                    yield return Tap(kb.dKey);
                Assert.AreEqual(want, p.Inventory.Letters[p.Summoner.Cursor], $"D moves the highlight to {want}");
                yield return Tap(kb.spaceKey);
            }
            Assert.AreEqual("TABLE", p.Summoner.Spelled);
            yield return Tap(kb.kKey);
            yield return null;

            var table = Object.FindObjectsByType<LetterBuilt>().FirstOrDefault(b => b.word == "TABLE");
            Assert.IsNotNull(table, "a TABLE was built");
            Assert.Greater(Vector3.Dot(table.transform.position - p.transform.position, p.Facing), 0.5f, "in front of the player");
            Assert.AreEqual(0, p.Inventory.Count);
        }

        [UnityTest]
        public IEnumerator UndoDropAndWalkWithTheKeyboard()
        {
            PlayerController p = null;
            yield return SpawnKeyboardPlayer(x => p = x);
            p.Inventory.Set("BATZ");

            yield return Tap(kb.kKey);
            yield return Tap(kb.spaceKey);
            yield return Tap(kb.spaceKey);
            Assert.AreEqual("BA", p.Summoner.Spelled);
            yield return Tap(kb.jKey);
            Assert.AreEqual("B", p.Summoner.Spelled, "J undoes");
            yield return Tap(kb.dKey);
            yield return Tap(kb.dKey);
            Assert.AreEqual('Z', p.Inventory.Letters[p.Summoner.Cursor]);
            yield return Tap(kb.sKey);
            Assert.AreEqual("BAT", new string(p.Inventory.Letters.ToArray()), "S drops the highlighted letter");
            yield return Tap(kb.jKey);
            yield return Tap(kb.jKey);
            Assert.IsFalse(p.Summoner.IsSpelling, "J on an empty word stops spelling");

            Press(kb.dKey, queueEventOnly: true);
            yield return new WaitForSeconds(0.4f);
            Release(kb.dKey, queueEventOnly: true);
            Assert.Greater(p.transform.position.x, 0.8f, "D walks right once spelling is over");
        }

        [UnityTearDown]
        public IEnumerator ResetScenes() => TestScenes.Reset();
    }
}
