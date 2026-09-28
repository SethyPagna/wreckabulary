using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    /// <summary>Creative, "Home Sweet Home": endless letters, building, the room menu, and playing Dibs! in your room.</summary>
    public class CreativeTests
    {
        const int TestSlot = 3;
        PlayerJoinManager joins;
        CreativeDesk desk;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.CreativeScene);
            joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            desk = Object.FindAnyObjectByType<CreativeDesk>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            RoomLayout.Delete(TestSlot);
            yield return TestScenes.Reset();
        }

        static int Built(string word) => Object.FindObjectsByType<LetterBuilt>().Count(b => b.word == word && b.GetComponent<Smashable>());

        /// <summary>Spells a word through the controls: move the highlight to each letter, add it, then cast.</summary>
        static IEnumerator SpellWithControls(ScriptedBinding input, PlayerController p, string word)
        {
            input.Next.spellDown = true;
            yield return null;
            yield return null;
            foreach (char want in word)
            {
                int target = want - 'A';
                int diff = target - p.Summoner.Cursor;
                // Big jumps with up/down, the rest with left/right.
                while (Mathf.Abs(diff) >= 5)
                {
                    if (diff > 0) input.Next.down = true; else input.Next.up = true;
                    yield return null;
                    yield return null;
                    diff = target - p.Summoner.Cursor;
                }
                while (diff != 0)
                {
                    if (diff > 0) input.Next.right = true; else input.Next.left = true;
                    yield return null;
                    yield return null;
                    diff = target - p.Summoner.Cursor;
                }
                input.Next.confirm = true;
                yield return null;
                yield return null;
            }
            input.Next.spellDown = true;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpellAnythingFromAToZForFree()
        {
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(p.Summoner.EndlessLetters);
            Assert.AreEqual(0, p.Inventory.Count);

            yield return SpellWithControls(input, p, "TREE"); // double E: letters repeat
            Assert.AreEqual(1, Built("TREE"), "a TREE was built");
            yield return SpellWithControls(input, p, "PIANO");
            Assert.AreEqual(1, Built("PIANO"));
            Assert.AreEqual(0, p.Inventory.Count, "nothing was spent");

            p.Summoner.Open();
            foreach (char c in "BAT") { while (p.Summoner.Source[p.Summoner.Cursor] != c) p.Summoner.Move(1); p.Summoner.Add(); }
            Assert.IsFalse(p.Summoner.Cast(), "only objects in Creative");
            Assert.IsNull(p.Combat.Weapon);
        }

        [UnityTest]
        public IEnumerator NobodyGetsKnockedOutWhileBuilding()
        {
            var p = joins.Join(new ScriptedBinding());
            yield return new WaitForSeconds(0.3f);
            for (int i = 0; i < 3; i++)
            {
                p.Health.TakeHit(Vector3.forward);
                yield return new WaitForSeconds(0.7f);
            }
            Assert.IsFalse(p.IsKnockedOut);
        }

        [UnityTest]
        public IEnumerator SmashedLettersTidyThemselvesAway()
        {
            var sofa = FurnitureCatalog.Spawn("SOFA", Vector3.zero, 0f, World.Transient);
            yield return null;
            sofa.Break();
            Assert.Greater(TilePool.Instance.Active.Count, 0);
            yield return new WaitForSeconds(3.5f);
            Assert.AreEqual(0, TilePool.Instance.Active.Count);
        }

        [UnityTest]
        public IEnumerator SaveLoadAndClearTheRoom()
        {
            joins.Join(new ScriptedBinding());
            FurnitureCatalog.Spawn("BED", new Vector3(-3f, 0f, 3f), 90f, World.Transient);
            FurnitureCatalog.Spawn("LAMP", new Vector3(4f, 0f, -2f), 0f, World.Transient);
            yield return new WaitForSeconds(1f);

            desk.Select(CreativeDesk.Item.Save);
            while (desk.Slot != TestSlot) desk.Adjust(1);
            Assert.IsTrue(desk.Activate(CreativeDesk.Item.Save));
            Assert.IsTrue(RoomLayout.Exists(TestSlot));

            Assert.IsTrue(desk.Activate(CreativeDesk.Item.Clear));
            yield return null;
            Assert.AreEqual(0, Built("BED") + Built("LAMP"), "cleared");

            Assert.IsTrue(desk.Activate(CreativeDesk.Item.Load));
            yield return null;
            Assert.AreEqual(1, Built("BED"));
            Assert.AreEqual(1, Built("LAMP"));
            var bed = Object.FindObjectsByType<LetterBuilt>().First(b => b.word == "BED");
            Assert.Less(Vector3.Distance(World.Flat(bed.transform.position), new Vector3(-3f, 0f, 3f)), 0.3f, "back where it was");
            Assert.That(Mathf.DeltaAngle(bed.transform.eulerAngles.y, 90f), Is.EqualTo(0f).Within(5f), "facing the same way");
        }

        [UnityTest]
        public IEnumerator PlayDibsInYourOwnRoomWithYourRules()
        {
            joins.Join(new ScriptedBinding());
            joins.Join(new ScriptedBinding());
            FurnitureCatalog.Spawn("SOFA", new Vector3(0f, 0f, 3.5f), 0f, World.Transient);
            FurnitureCatalog.Spawn("TREE", new Vector3(-5f, 0f, 1f), 0f, World.Transient);
            Session.CustomRounds = 1;
            Session.CustomStarterLetters = 5;
            yield return new WaitForSeconds(1f);

            Assert.IsTrue(desk.Activate(CreativeDesk.Item.Play));
            yield return TestScenes.WaitForActive(Session.CustomArenaScene);
            yield return new WaitForSeconds(0.2f);

            Assert.AreEqual(1, Built("SOFA"), "your SOFA is in the arena");
            Assert.AreEqual(1, Built("TREE"));
            var rounds = RoundManager.Instance;
            var arenaJoins = Object.FindAnyObjectByType<PlayerJoinManager>();
            Assert.AreEqual(2, arenaJoins.Players.Count);
            Assert.AreEqual(Phase.Countdown, rounds.Phase, "straight into the match");
            Assert.AreEqual(5, arenaJoins.Players[0].Inventory.Count, "your starting letters");
            Assert.IsFalse(arenaJoins.Players[0].Summoner.EndlessLetters, "normal spelling in the match");

            // One round to win: knock out P2 and the match is over.
            rounds.CountdownTime = 0.1f;
            yield return TestScenes.WaitUntil(() => rounds.Phase == Phase.Playing, 5f, "round start");
            Assert.AreEqual(1, Built("SOFA"), "the room is rebuilt for the round");
            var loser = arenaJoins.Players[1];
            loser.Inventory.Set("");
            loser.Health.TakeHit(Vector3.forward);
            yield return TestScenes.WaitUntil(() => rounds.Phase == Phase.MatchOver, 5f, "match over after one round");

            // Afterwards it's back to Creative, with the room still built.
            yield return TestScenes.WaitForActive(Session.CreativeScene, 10f);
            yield return null;
            Assert.AreEqual(1, Built("SOFA"), "your room is still there");
        }

        [UnityTest]
        public IEnumerator TheHouseTypewriterOpensCreativeSolo()
        {
            yield return TestScenes.Reset();
            yield return TestScenes.Load(Session.HubScene);
            Object.FindAnyObjectByType<PlayerJoinManager>().Join(new ScriptedBinding());
            var typewriter = Object.FindAnyObjectByType<Typewriter>();
            int i = typewriter.Modes.Select((m, k) => (m, k)).First(x => x.m.scene == Session.CreativeScene).k;
            Assert.IsTrue(typewriter.Choose(i));
            yield return TestScenes.WaitForActive(Session.CreativeScene);
            var p = Object.FindAnyObjectByType<PlayerJoinManager>().Players.Single();
            Assert.IsTrue(p.Summoner.EndlessLetters);
        }
    }
}
