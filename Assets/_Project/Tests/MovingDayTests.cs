using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    /// <summary>Co-op Moving Day: deliveries, spelling furniture, placing it in the right room, stars and time outs.</summary>
    public class MovingDayTests
    {
        MovingDayDirector director;
        PlayerJoinManager joins;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.MovingDayScene);
            director = Object.FindAnyObjectByType<MovingDayDirector>();
            joins = Object.FindAnyObjectByType<PlayerJoinManager>();
        }

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        IEnumerator UntilPlaying() =>
            TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.Playing, 5f, "level start");

        /// <summary>Builds an item straight into a room, as if spelled there, and lets it settle.</summary>
        Smashable PlaceIn(string word, string room, float offset = 0f)
        {
            var at = director.RoomNamed(room).Centre + new Vector3(0f, 0.3f, offset);
            return FurnitureCatalog.Spawn(word, at, 0f, World.Transient);
        }

        [UnityTest]
        public IEnumerator BoxesForTheChecklistArrive()
        {
            yield return UntilPlaying();
            yield return new WaitForSeconds(director.CurrentLevel.items.Length * 1.2f + 0.5f);
            var boxes = Object.FindObjectsByType<Smashable>().Where(s => !s.GetComponent<LetterBuilt>()).Select(s => s.Word).ToList();
            foreach (var item in director.CurrentLevel.items)
                CollectionAssert.Contains(boxes, item.word, $"a box labelled {item.word}");
        }

        [UnityTest]
        public IEnumerator SpellingAChecklistWordBuildsFurniture()
        {
            var p = joins.Join(new ScriptedBinding());
            yield return UntilPlaying();
            Assert.AreEqual(0, p.Inventory.Count, "Moving Day starts empty-handed");
            p.Inventory.Set("BEDX");

            Assert.IsTrue(p.Summoner.Summon("BED"));
            Assert.IsFalse(p.Summoner.Summon("BLADE"), "only checklist words can be spelled");
            yield return null;
            Assert.IsTrue(Object.FindObjectsByType<LetterBuilt>().Any(b => b.word == "BED"));
        }

        [UnityTest]
        public IEnumerator FurnitureOnlyCountsInItsRoom()
        {
            yield return UntilPlaying();
            PlaceIn("BED", "Living Room");
            yield return new WaitForSeconds(1.5f);
            Assert.IsTrue(director.Remaining.Any(i => i.word == "BED"), "a BED in the living room doesn't count");

            PlaceIn("BED", "Bedroom");
            yield return TestScenes.WaitUntil(() => director.Remaining.All(i => i.word != "BED"), 3f, "BED placed in the bedroom");
        }

        [UnityTest]
        public IEnumerator PlacedFurnitureIsLockedInPlace()
        {
            yield return UntilPlaying();
            var lamp = PlaceIn("LAMP", "Bedroom");
            yield return TestScenes.WaitUntil(() => director.Remaining.All(i => i.word != "LAMP"), 3f, "LAMP placed");
            Assert.IsTrue(lamp.GetComponent<Rigidbody>().isKinematic);
            lamp.TakeHit(999f);
            Assert.IsFalse(lamp.IsBroken, "placed furniture can't be wrecked");
        }

        [UnityTest]
        public IEnumerator FinishingQuicklyEarnsThreeStarsAndMovesOn()
        {
            yield return UntilPlaying();
            float z = -3f;
            foreach (var item in director.CurrentLevel.items)
            {
                PlaceIn(item.word, item.room, z);
                z += 1.8f;
            }
            yield return TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.Complete, 5f, "level complete");
            Assert.AreEqual(3, director.Stars);
            Assert.AreEqual(3, Session.MovingDayStars[0]);

            yield return TestScenes.WaitUntil(() => director.LevelIndex == 1, 7f, "next level");
            Assert.AreEqual(director.CurrentLevel.items.Length, director.Remaining.Count);
        }

        [UnityTest]
        public IEnumerator RunningOutOfTimeRestartsTheLevel()
        {
            yield return UntilPlaying();
            PlaceIn("SOFA", "Living Room");
            director.TimeLeft = 0.2f;
            yield return TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.OutOfTime, 2f, "out of time");
            Assert.AreEqual(0, director.Stars);
            yield return TestScenes.WaitUntil(() => director.Current == MovingDayDirector.State.Countdown, 7f, "retry");
            Assert.AreEqual(0, director.LevelIndex);
            Assert.AreEqual(director.CurrentLevel.items.Length, director.Remaining.Count, "checklist resets");
        }

        [UnityTest]
        public IEnumerator TypewriterStartsMovingDaySolo()
        {
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.HubScene);
            Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            var typewriter = Object.FindAnyObjectByType<Typewriter>();
            int index = typewriter.Modes.Select((m, i) => (m, i)).First(x => x.m.scene == Session.MovingDayScene).i;

            Assert.IsTrue(typewriter.Choose(index), "one roommate can play Moving Day");
            yield return TestScenes.WaitForActive(Session.MovingDayScene);
            var p = Object.FindAnyObjectByType<PlayerJoinManager>().Players.Single();
            Assert.IsNotNull(p.Summoner.WordsOverride, "the word wheel shows the checklist");
        }
    }
}
