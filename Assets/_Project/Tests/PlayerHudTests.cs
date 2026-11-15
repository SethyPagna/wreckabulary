using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    public class PlayerHudTests
    {
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * .5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            yield return TestScenes.Reset();
        }

        static PlayerController Spawn(InputBinding binding, int index, Vector3 at, string name = null)
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, at, Quaternion.identity);
            player.Setup(index, binding);
            if (name != null) player.Name = name;
            player.Respawn(at);
            player.Inventory.Collects = false;
            return player;
        }

        static readonly FieldInfo Letters = typeof(PlayerHud).GetField("lettersText", BindingFlags.NonPublic | BindingFlags.Instance);

        static string Tag(PlayerController p) => ((TextMeshPro)Letters.GetValue(p.GetComponentInChildren<PlayerHud>(true))).text;

        static LineRenderer Ring(PlayerController p) => p.GetComponentInChildren<PlayerHud>(true).SeatRing;

        [UnityTest]
        public IEnumerator OnACouchEveryHumanWearsASeatTagAndARing()
        {
            var one = Spawn(new ScriptedBinding(), 0, Vector3.zero);
            var two = Spawn(new ScriptedBinding(), 1, new Vector3(3f, 0f, 0f));
            var bot = Spawn(new BotBinding(), 2, new Vector3(-3f, 0f, 0f), "AI 3");
            yield return new WaitForSeconds(.3f);
            StringAssert.Contains(">P1<", Tag(one), "The first seat is tagged too.");
            StringAssert.Contains(">P2<", Tag(two));
            StringAssert.Contains(">AI 3<", Tag(bot), "Bots keep their AI tag.");
            foreach (var human in new[] { one, two })
            {
                var ring = Ring(human);
                Assert.IsNotNull(ring, human.Name + " has a ground ring");
                Assert.IsTrue(ring.enabled);
                Assert.AreSame(human.transform, ring.transform.parent, "The ring sits at the player's feet.");
            }
            Assert.IsTrue(Ring(bot) == null || !Ring(bot).enabled, "Bots get no seat ring.");
            two.Health.Eliminate();
            yield return new WaitForSeconds(.1f);
            Assert.IsFalse(Ring(two).enabled, "A wrecked player's ring goes.");
            Assert.AreEqual("", Tag(two));
        }

        [UnityTest]
        public IEnumerator ASoloPlayerKeepsTheRingButNotTheTag()
        {
            var you = Spawn(new ScriptedBinding(), 0, Vector3.zero);
            var bot = Spawn(new BotBinding(), 1, new Vector3(3f, 0f, 0f), "AI 2");
            yield return new WaitForSeconds(.3f);
            Assert.AreEqual("", Tag(you), "Solo: no tag over your own head.");
            Assert.IsTrue(Ring(you) && Ring(you).enabled, "...but the ring stays.");
            StringAssert.Contains(">AI 2<", Tag(bot));
        }
    }
}
