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

        static LineRenderer YouRing(PlayerController p) => p.GetComponentInChildren<PlayerHud>(true).YouRing;

        static TextMeshPro TagText(PlayerController p) => (TextMeshPro)Letters.GetValue(p.GetComponentInChildren<PlayerHud>(true));

        [UnityTest]
        public IEnumerator OnACouchEverySeatWearsATagAndAVividRing()
        {
            var one = Spawn(new ScriptedBinding(), 0, Vector3.zero);
            var two = Spawn(new ScriptedBinding(), 1, new Vector3(3f, 0f, 0f));
            var bot = Spawn(new BotBinding(), 2, new Vector3(-3f, 0f, 0f), "AI 3");
            yield return new WaitForSeconds(.3f);
            StringAssert.Contains(">P1<", Tag(one), "The first seat is tagged too.");
            StringAssert.Contains(">P2<", Tag(two));
            StringAssert.Contains(">AI 3<", Tag(bot), "Bots keep their AI tag.");
            foreach (var seat in new[] { one, two, bot })
            {
                var ring = Ring(seat);
                Assert.IsNotNull(ring, seat.Name + " has a ground ring, bots included");
                Assert.IsTrue(ring.enabled);
                Assert.AreSame(seat.transform, ring.transform.parent, "The ring sits at the player's feet.");
                Assert.AreEqual(PlayerHud.RingWidth, ring.startWidth, 1e-4f, "A thick ring reads from the overhead camera.");
                Assert.AreEqual(.13f, ring.startWidth, 1e-4f);
                Assert.AreEqual(.78f, ring.GetPosition(0).magnitude, 1e-3f, "A wider ring clears the feet.");
                var material = ring.sharedMaterial;
                StringAssert.Contains("Unlit", material.shader.name, "Unlit, so the seat colour stays vivid on any floor.");
                var colour = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
                Color.RGBToHSV(colour, out _, out float saturation, out float value);
                Assert.GreaterOrEqual(value, .94f, seat.Name + " ring is a bright seat colour");
                Assert.GreaterOrEqual(saturation, .55f);
                var tag = TagText(seat);
                Assert.AreEqual(PlayerHud.TagSize, tag.fontSize, 1e-3f, "Bigger name tags.");
                Assert.AreEqual(4.2f, tag.fontSize, 1e-3f);
                Assert.IsTrue((tag.fontStyle & FontStyles.Bold) != 0);
                Assert.IsTrue(tag.fontSharedMaterial.IsKeywordEnabled(ShaderUtilities.Keyword_Outline), "Tags carry a dark outline over floors.");
                Assert.Greater(tag.fontSharedMaterial.GetFloat("_OutlineWidth"), .2f);
            }
            Assert.AreNotEqual(Ring(one).sharedMaterial.GetColor("_BaseColor"), Ring(two).sharedMaterial.GetColor("_BaseColor"), "Each seat has its own colour.");
            two.Health.Eliminate();
            bot.Health.Eliminate();
            yield return new WaitForSeconds(.1f);
            Assert.IsFalse(Ring(two).enabled, "A wrecked player's ring goes.");
            Assert.IsFalse(Ring(bot).enabled);
            Assert.AreEqual("", Tag(two));
        }

        [UnityTest]
        public IEnumerator YourOwnSeatGetsAThinWhiteOuterRing()
        {
            var hud = new GameObject("Ring HUD", typeof(Canvas)).AddComponent<GameHud>();
            var you = Spawn(TouchBinding.Shared, 0, Vector3.zero);
            var bot = Spawn(new BotBinding(), 1, new Vector3(3f, 0f, 0f), "AI 2");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.AreSame(you, hud.LocalPlayer);
            var outer = YouRing(you);
            Assert.IsNotNull(outer, "You get a second ring so you can find yourself.");
            Assert.IsTrue(outer.enabled);
            Assert.AreEqual(PlayerHud.YouWidth, outer.startWidth, 1e-4f);
            Assert.AreEqual(.9f, outer.GetPosition(0).magnitude, 1e-3f, "just outside the seat ring");
            Assert.Greater(outer.GetPosition(0).magnitude - PlayerHud.RingRadius, PlayerHud.RingWidth * .5f, "It never overlaps the seat ring.");
            var white = outer.sharedMaterial.HasProperty("_BaseColor") ? outer.sharedMaterial.GetColor("_BaseColor") : outer.sharedMaterial.color;
            Assert.Greater(Mathf.Min(white.r, white.g, white.b), .95f, "white");
            Assert.IsTrue(YouRing(bot) == null || !YouRing(bot).enabled, "Only you wear it.");
            you.Health.Eliminate();
            yield return new WaitForSeconds(.1f);
            Assert.IsFalse(outer.enabled, "It goes with your seat ring when you're wrecked.");
            Object.Destroy(hud.gameObject);
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
