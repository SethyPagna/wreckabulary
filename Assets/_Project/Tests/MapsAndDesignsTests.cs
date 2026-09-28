using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    /// <summary>Every object word has a Word World design that builds and stands up, and every Dibs! map works.</summary>
    public class MapsAndDesignsTests
    {
        static readonly string[] Maps = { Session.DibsScene, Session.BedroomScene, Session.KitchenScene, Session.GardenScene };

        [UnitySetUp]
        public IEnumerator SetUp() => TestScenes.Reset();

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        static IEnumerable<string> ObjectWords =>
            GameAssets.I.words.Words.Where(w => w.category == WordCategory.Furniture).Select(w => w.word);

        [Test]
        public void EveryObjectWordHasADesign()
        {
            var missing = ObjectWords.Where(w => !LetterShapes.HasRecipe(w)).ToList();
            CollectionAssert.IsEmpty(missing, "no design for: " + string.Join(", ", missing));
            Assert.GreaterOrEqual(ObjectWords.Count(), 40, "plenty of objects to spell");
        }

        [Test]
        public void EveryDesignUsesEachLetterOnce()
        {
            foreach (var word in LetterShapes.RecipeWords)
            {
                var placements = LetterShapes.For(word, Vector3.one * 0.5f, 0, 0.02f);
                Assert.AreEqual(word.Length, placements.Count, $"{word}: one placement per letter");
                CollectionAssert.AreEquivalent(Enumerable.Range(0, word.Length), placements.Select(p => p.index), $"{word}: each letter placed once");
            }
        }

        [UnityTest]
        public IEnumerator EveryDesignBuildsAtASensibleSize()
        {
            foreach (var word in LetterShapes.RecipeWords)
            {
                var built = LetterBuilt.Spawn(word, Vector3.one * 0.5f, 0, Color.white, null, colliders: false);
                Assert.IsTrue(built.Blocks.All(b => b), $"{word}: every letter built");
                var rs = built.GetComponentsInChildren<Renderer>();
                var bounds = rs[0].bounds;
                foreach (var r in rs) bounds.Encapsulate(r.bounds);
                Assert.That(bounds.size.y, Is.InRange(0.03f, 2.6f), $"{word} height");
                Assert.That(bounds.size.x, Is.InRange(0.1f, 3.2f), $"{word} width");
                Assert.That(bounds.min.y, Is.InRange(-0.05f, 0.25f), $"{word} sits on the ground");
                Object.Destroy(built.gameObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator EveryObjectStandsWhenLeftAlone()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(80f, 1f, 80f);
            var spawned = new List<Smashable>();
            int i = 0;
            foreach (var word in ObjectWords)
            {
                var at = new Vector3((i % 8) * 4f - 14f, 0f, (i / 8) * 4f - 12f);
                spawned.Add(FurnitureCatalog.Spawn(word, at, 0f, null));
                i++;
            }
            yield return new WaitForSeconds(3f);
            var fallen = spawned.Where(s => s && Vector3.Dot(s.transform.up, Vector3.up) < 0.9f).Select(s => s.Word).ToList();
            CollectionAssert.IsEmpty(fallen, "tipped over: " + string.Join(", ", fallen));
        }

        [UnityTest]
        public IEnumerator EveryMapLoadsWithStandingFurnitureAndClearSpawns([ValueSource(nameof(Maps))] string map)
        {
            yield return TestScenes.Load(map);
            Assert.IsNotNull(RoundManager.Instance, $"{map} runs Dibs!");
            var furniture = Object.FindObjectsByType<Smashable>().Where(s => s.GetComponent<LetterBuilt>()).ToList();
            Assert.GreaterOrEqual(furniture.Count, 10, $"{map} is furnished");

            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            for (int i = 0; i < 4; i++)
            {
                var spawn = joins.SpawnPoint(i);
                var blocking = Physics.OverlapSphere(spawn + Vector3.up * 0.7f, 0.6f)
                                      .Where(c => c.attachedRigidbody && c.attachedRigidbody.GetComponent<Smashable>())
                                      .Select(c => c.attachedRigidbody.name).Distinct().ToList();
                CollectionAssert.IsEmpty(blocking, $"{map} spawn {i + 1} blocked by " + string.Join(", ", blocking));
            }

            yield return new WaitForSeconds(3f);
            var fallen = furniture.Where(s => s && Vector3.Dot(s.transform.up, Vector3.up) < 0.9f).Select(s => s.Word).ToList();
            CollectionAssert.IsEmpty(fallen, $"{map}: tipped over " + string.Join(", ", fallen));
        }

        [UnityTest]
        public IEnumerator TypewriterPicksTheDibsMapWithLeftAndRight()
        {
            yield return TestScenes.Load(Session.HubScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            joins.Join(new ScriptedBinding());
            var typewriter = Object.FindAnyObjectByType<Typewriter>();
            yield return new WaitForSeconds(1f);

            typewriter.Open(p);
            int dibs = typewriter.Modes.Select((m, i) => (m, i)).First(x => x.m.scene == Session.DibsScene).i;
            while (typewriter.Selected != dibs)
            {
                input.Next.down = true;
                yield return null;
                yield return null;
            }
            input.Next.right = true; // Living Room -> Bedroom
            yield return null;
            yield return null;
            input.Next.right = true; // -> Kitchen
            yield return null;
            yield return null;
            Assert.AreEqual(2, typewriter.Map);

            input.Next.confirm = true;
            input.Next.grab = true;
            yield return TestScenes.WaitForActive(Session.KitchenScene);
            Assert.AreEqual(2, Object.FindAnyObjectByType<PlayerJoinManager>().Players.Count);
        }

        [UnityTest]
        public IEnumerator MovingDayHasThreeLevelsUsingTheNewDesigns()
        {
            yield return TestScenes.Load(Session.MovingDayScene);
            var director = Object.FindAnyObjectByType<MovingDayDirector>();
            Assert.AreEqual(3, director.LevelCount);
        }
    }
}
