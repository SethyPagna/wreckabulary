using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    /// <summary>The wardrobe in the house and its dress-up page.</summary>
    public class WardrobeTests
    {
        Wardrobe wardrobe;
        PlayerJoinManager joins;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.HubScene);
            wardrobe = Object.FindAnyObjectByType<Wardrobe>();
            joins = Object.FindAnyObjectByType<PlayerJoinManager>();
        }

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        static IEnumerator Press(ScriptedBinding input, System.Action<ScriptedBinding> press)
        {
            press(input);
            yield return null;
            yield return null;
        }

        IEnumerator WalkUp(PlayerController p, ScriptedBinding input)
        {
            yield return new WaitForSeconds(1f); // walk in through the door first
            p.Respawn(wardrobe.transform.position + Vector3.back * 0.6f);
            yield return new WaitForFixedUpdate();
            yield return Press(input, i => i.Next.grab = true);
        }

        [UnityTest]
        public IEnumerator GrabAtTheWardrobeOpensTheDressUpPage()
        {
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            yield return WalkUp(p, input);

            Assert.AreEqual(p, wardrobe.User);
            Assert.IsTrue(p.Frozen, "standing still to get dressed");
            Assert.IsTrue(wardrobe.Page.Visible, "the page is open");
            Assert.IsTrue(wardrobe.PreviewCamera.enabled, "the mirror is filming");
            Assert.AreEqual(wardrobe.PreviewCamera.targetTexture, wardrobe.Page.PreviewTexture, "the page shows the mirror");
        }

        [UnityTest]
        public IEnumerator EveryRowChangesTheRoommate()
        {
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            yield return WalkUp(p, input);
            var before = p.Color;

            yield return Press(input, i => i.Next.right = true);           // Colour -> next
            Assert.AreNotEqual(before, p.Color, "colour changed");

            yield return Press(input, i => i.Next.down = true);            // Letter
            char initial = p.Initial;
            yield return Press(input, i => i.Next.right = true);
            Assert.AreEqual((char)(initial + 1), p.Initial, "next sweater letter");
            Assert.AreEqual(p.Initial.ToString(), p.initialLabel.text);

            yield return Press(input, i => i.Next.down = true);            // Hat
            for (int k = 0; k < 4; k++) yield return Press(input, i => i.Next.right = true); // CROWN
            var hat = p.visual.Find(Looks.HatName);
            Assert.IsNotNull(hat, "wearing a hat");
            Assert.AreEqual("CROWN", hat.GetComponent<LetterBuilt>().word, "the hat is spelled out");

            yield return Press(input, i => i.Next.down = true);            // Extra
            yield return Press(input, i => i.Next.right = true);           // SPECS
            Assert.IsNotNull(p.visual.Find(Looks.ExtraName), "wearing specs");

            yield return Press(input, i => i.Next.left = true);            // back to none
            yield return null;
            Assert.IsNull(p.visual.Find(Looks.ExtraName), "specs off again");
            Assert.AreEqual(1, p.visual.Cast<Transform>().Count(t => t.name == Looks.HatName), "only one hat at a time");
        }

        [UnityTest]
        public IEnumerator ShuffleAndDone()
        {
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            yield return WalkUp(p, input);

            wardrobe.Select(Wardrobe.Row.Shuffle);
            var before = p.Look;
            for (int k = 0; k < 5 && p.Look.Equals(before); k++) yield return Press(input, i => i.Next.grab = true);
            Assert.AreNotEqual(before, p.Look, "shuffled");

            wardrobe.Select(Wardrobe.Row.Done);
            yield return Press(input, i => i.Next.grab = true);
            Assert.IsNull(wardrobe.User);
            Assert.IsFalse(p.Frozen);
            Assert.IsFalse(wardrobe.Page.Visible);
            Assert.IsFalse(wardrobe.PreviewCamera.enabled);
            Assert.AreEqual(p.Look, Session.Looks[p.Index], "remembered");
        }

        [UnityTest]
        public IEnumerator YourOutfitFollowsYouIntoAMatch()
        {
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            joins.Join(new ScriptedBinding());
            yield return WalkUp(p, input);

            wardrobe.Select(Wardrobe.Row.Colour);
            yield return Press(input, i => i.Next.left = true);  // Rose (wraps backwards from Coral)
            wardrobe.Select(Wardrobe.Row.Hat);
            yield return Press(input, i => i.Next.right = true); // CAP
            yield return Press(input, i => i.Next.spellDown = true);
            var look = p.Look;
            var colour = p.Color;

            var typewriter = Object.FindAnyObjectByType<Typewriter>();
            int dibs = typewriter.Modes.Select((m, k) => (m, k)).First(x => x.m.scene == Session.DibsScene).k;
            Assert.IsTrue(typewriter.Choose(dibs));
            yield return TestScenes.WaitForActive(Session.DibsScene);

            var there = Object.FindAnyObjectByType<PlayerJoinManager>().Players[0];
            Assert.AreEqual(colour, there.Color, "same colour in the match");
            Assert.AreEqual(look.initial, there.Initial);
            Assert.AreEqual("CAP", there.visual.Find(Looks.HatName)?.GetComponent<LetterBuilt>().word, "and the same hat");
        }
    }
}
