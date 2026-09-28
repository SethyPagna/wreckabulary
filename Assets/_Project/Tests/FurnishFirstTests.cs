using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    /// <summary>Furnish First: race to get the round's checklist into your corner.</summary>
    public class FurnishFirstTests
    {
        FurnishFirstDirector director;
        PlayerJoinManager joins;
        PlayerController p1, p2;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.FurnishFirstScene);
            director = Object.FindAnyObjectByType<FurnishFirstDirector>();
            joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            p1 = joins.Join(new ScriptedBinding());
            p2 = joins.Join(new ScriptedBinding());
            director.CountdownTime = 0.1f;
            director.StartMatch();
            yield return TestScenes.WaitUntil(() => director.Current == FurnishFirstDirector.State.Playing, 3f, "round start");
        }

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        /// <summary>Builds the checklist in a corner, spread out so nothing overlaps.</summary>
        void Furnish(int zone)
        {
            var c = director.Zones[zone].Centre;
            for (int i = 0; i < director.Checklist.Count; i++)
                FurnitureCatalog.Spawn(director.Checklist[i], c + new Vector3(-1.8f + i * 1.8f, 0.05f, 0f), 0f, World.Transient);
        }

        [UnityTest]
        public IEnumerator EachRoundHasAChecklistAndBoxesLandInTheMiddle()
        {
            Assert.AreEqual(3, director.Checklist.Count);
            Assert.AreEqual(3, director.Checklist.Distinct().Count());
            yield return new WaitForSeconds(6f);
            var boxes = Object.FindObjectsByType<Smashable>().Where(s => !s.GetComponent<LetterBuilt>()).ToList();
            Assert.Greater(boxes.Count, 0, "boxes dropped");
            foreach (var b in boxes)
            {
                CollectionAssert.Contains(director.Checklist, b.Word, "boxes carry checklist words");
                Assert.Less(Mathf.Abs(b.transform.position.x), 2.5f, "in the middle");
            }
        }

        [UnityTest]
        public IEnumerator OnlyTheChecklistCanBeSpelled()
        {
            string word = director.Checklist[0];
            p1.Inventory.Set(word);
            Assert.IsTrue(p1.Summoner.Summon(word));
            p1.Inventory.Set("BAT");
            Assert.IsFalse(p1.Summoner.Summon("BAT"), "no weapons in Furnish First");
            yield return null;
        }

        [UnityTest]
        public IEnumerator FurnishingYourCornerWinsTheRound()
        {
            Assert.AreEqual(0, director.Progress(0));
            Furnish(0);
            yield return TestScenes.WaitUntil(() => director.Current == FurnishFirstDirector.State.RoundOver, 4f, "round won");
            Assert.AreEqual(p1, director.LastWinner);
            Assert.AreEqual(1, director.WinsOf(p1));
            Assert.AreEqual(0, director.WinsOf(p2));
        }

        [UnityTest]
        public IEnumerator FurnitureInSomeoneElsesCornerCountsForThem()
        {
            // "Stealing": the items end up in P2's corner, so P2 wins no matter who built them.
            Furnish(1);
            yield return TestScenes.WaitUntil(() => director.Current == FurnishFirstDirector.State.RoundOver, 4f, "round won");
            Assert.AreEqual(p2, director.LastWinner);
        }

        [UnityTest]
        public IEnumerator ThingsStillMovingDontCount()
        {
            var c = director.Zones[0].Centre;
            var item = FurnitureCatalog.Spawn(director.Checklist[0], c + Vector3.up * 0.05f, 0f, World.Transient);
            item.GetComponent<Rigidbody>().linearVelocity = Vector3.right * 6f;
            Assert.IsFalse(director.Has(0, director.Checklist[0]), "sliding through doesn't count");
            yield return new WaitForSeconds(2f);
        }

        [UnityTest]
        public IEnumerator FirstToTwoRoundsWinsTheMatch()
        {
            Furnish(0);
            yield return TestScenes.WaitUntil(() => director.Current == FurnishFirstDirector.State.RoundOver, 4f, "round 1 won");
            yield return TestScenes.WaitUntil(() => director.Current == FurnishFirstDirector.State.Playing && director.Round == 2, 8f, "round 2");
            Assert.AreEqual(0, director.Progress(0), "the room is cleared between rounds");
            Furnish(0);
            yield return TestScenes.WaitUntil(() => director.Current == FurnishFirstDirector.State.MatchOver, 8f, "match won");
            Assert.AreEqual(2, director.WinsOf(p1));
        }

        [UnityTest]
        public IEnumerator TheHouseTypewriterStartsFurnishFirst()
        {
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.HubScene);
            var hubJoins = Object.FindAnyObjectByType<PlayerJoinManager>();
            hubJoins.Join(new ScriptedBinding());
            var typewriter = Object.FindAnyObjectByType<Typewriter>();
            int i = typewriter.Modes.Select((m, k) => (m, k)).First(x => x.m.scene == Session.FurnishFirstScene).k;
            Assert.IsFalse(typewriter.Choose(i), "needs 2 roommates");
            hubJoins.Join(new ScriptedBinding());
            Assert.IsTrue(typewriter.Choose(i));
            yield return TestScenes.WaitForActive(Session.FurnishFirstScene);
            var ff = Object.FindAnyObjectByType<FurnishFirstDirector>();
            Assert.AreEqual(FurnishFirstDirector.State.Countdown, ff.Current, "straight into the race");
        }
    }
}
