using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class CoopCrewTests
    {
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        static IEnumerator LoadCrew(string mode, string map = "pinwheel")
        {
            Session.SelectMap(map);
            Match.ModeOverride = mode;
            Session.Remember(new ScriptedBinding());
            Session.Remember(new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
        }

        static PlayerJoinManager Joins => Object.FindAnyObjectByType<PlayerJoinManager>();
        static HouseLayout Layout => Object.FindAnyObjectByType<RoomBuilder>().Layout;

        static Vector3 FarthestRoomFrom(Vector3 spot)
        {
            var room = Layout.Rooms.OrderByDescending(r => new Vector2((r.MinX + r.MaxX) * .5f - spot.x, (r.MinZ + r.MaxZ) * .5f - spot.z).sqrMagnitude).First();
            return new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY, (room.MinZ + room.MaxZ) * .5f);
        }

        static (object locked, Doorway door, string word) FirstBarredDoor(EscapeDirector director)
        {
            var doors = (IList)typeof(EscapeDirector).GetField("doors", Hidden).GetValue(director);
            foreach (object locked in doors)
            {
                var type = locked.GetType();
                if ((bool)type.GetField("Open").GetValue(locked)) continue;
                return (locked, (Doorway)type.GetField("Door").GetValue(locked), (string)type.GetField("Word").GetValue(locked));
            }
            Assert.Fail("Escape bars at least one door");
            return default;
        }

        static Vector3 BesideTheDoor(Doorway door)
        {
            var room = Layout.Room(door.A);
            var inward = new Vector3((room.MinX + room.MaxX) * .5f - door.X, 0f, (room.MinZ + room.MaxZ) * .5f - door.Z).normalized;
            return new Vector3(door.X, room.FloorY, door.Z) + inward * 1f;
        }

        static IEnumerator Playing(EscapeDirector director) =>
            TestScenes.WaitUntil(() => director.Current == EscapeDirector.State.Playing, 6f, "the escape starts");

        [UnityTest]
        public IEnumerator AMissingLetterDropsBesideWhoeverStandsAtThatDoor()
        {
            yield return LoadCrew("Escape");
            var director = Object.FindAnyObjectByType<EscapeDirector>();
            yield return Playing(director);
            PlayerController p1 = Joins.Players[0], p2 = Joins.Players[1];
            var (locked, door, _) = FirstBarredDoor(director);
            p2.Respawn(BesideTheDoor(door));
            p1.Respawn(FarthestRoomFrom(p2.transform.position));
            yield return null;
            int before = Object.FindObjectsByType<Smashable>().Count(s => s.Word == "Q");
            typeof(EscapeDirector).GetMethod("DropPropNear", Hidden).Invoke(director, new[] { 'Q', locked });
            var drops = Object.FindObjectsByType<Smashable>().Where(s => s.Word == "Q").ToArray();
            Assert.AreEqual(before + 1, drops.Length);
            var drop = drops.OrderBy(s => (s.transform.position - p2.transform.position).sqrMagnitude).First();
            Assert.Less(Vector3.Distance(drop.transform.position, p2.transform.position), 3f, "P2 is at that door, so the letter lands by P2");
        }

        [UnityTest]
        public IEnumerator TheBarredDoorPanelHasALineForEachPlayerAtADoor()
        {
            yield return LoadCrew("Escape");
            var director = Object.FindAnyObjectByType<EscapeDirector>();
            yield return Playing(director);
            var (_, door, word) = FirstBarredDoor(director);
            Joins.Players[1].Respawn(BesideTheDoor(door));
            Joins.Players[0].Respawn(FarthestRoomFrom(Joins.Players[1].transform.position));
            yield return new WaitForSeconds(.5f);
            StringAssert.Contains("P2 · SPELL <b>" + string.Join(" ", word.ToCharArray()) + "</b>", director.NoticeLines);
        }

        [UnityTest]
        public IEnumerator AnIdleCrewmateNoLongerKeepsTheOthersFromLeaving()
        {
            yield return LoadCrew("Escape");
            var director = Object.FindAnyObjectByType<EscapeDirector>();
            yield return Playing(director);
            typeof(EscapeDirector).GetField("idle", Hidden).SetValue(director, new IdleWatch(.5));
            typeof(EscapeDirector).GetField("exitOpen", Hidden).SetValue(director, true);
            PlayerController p1 = Joins.Players[0], p2 = Joins.Players[1];
            ((ScriptedBinding)p1.Binding).Next.blockHeld = true;
            p1.Respawn(director.ExtractionPoint);
            p2.Respawn(FarthestRoomFrom(director.ExtractionPoint));
            yield return new WaitForSeconds(.25f);
            Assert.AreEqual(EscapeDirector.State.Playing, director.Current, "P2 is still part of the crew");
            yield return TestScenes.WaitUntil(() => director.Current == EscapeDirector.State.Complete, 3f, "the crew leaves without the idle P2");
            StringAssert.Contains("P2 is idle · the crew can leave without them", director.Checklist);
        }

        [UnityTest]
        public IEnumerator TheVanLeavesWithoutAnIdleCrewmate()
        {
            yield return LoadCrew("MovingOut");
            var director = Object.FindAnyObjectByType<MovingOutDirector>();
            yield return TestScenes.WaitUntil(() => director.Current == MovingOutDirector.State.Playing, 6f, "moving out starts");
            typeof(MovingOutDirector).GetField("idle", Hidden).SetValue(director, new IdleWatch(.5));
            foreach (var keepsake in Object.FindObjectsByType<Keepsake>())
                keepsake.GetComponent<Rigidbody>().position = director.ExtractionPoint + Vector3.up * .3f;
            PlayerController p1 = Joins.Players[0], p2 = Joins.Players[1];
            ((ScriptedBinding)p1.Binding).Next.blockHeld = true;
            p1.Respawn(director.ExtractionPoint);
            p2.Respawn(FarthestRoomFrom(director.ExtractionPoint));
            yield return new WaitForSeconds(.25f);
            Assert.AreEqual(MovingOutDirector.State.Playing, director.Current, "P2 is still part of the crew");
            yield return TestScenes.WaitUntil(() => director.Current == MovingOutDirector.State.Complete, 3f, "the van leaves without the idle P2");
            StringAssert.Contains("P2 is idle · the crew can leave without them", director.Checklist);
        }
    }
}
