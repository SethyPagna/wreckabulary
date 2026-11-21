using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Rules;

namespace Wreckabulary.Tests
{
    public class StrayPieceReturnTests
    {
        const float QuickReturn = .5f, Patience = 15f, NearHome = .6f, Moved = .5f, Gap = .03f;

        RoomBuilder room;
        StrayPieceReturner returner;
        readonly List<Smashable> returned = new();

        [UnitySetUp] public IEnumerator SetUp() => TestScenes.Reset();
        [UnityTearDown] public IEnumerator TearDown() => TestScenes.Reset();

        IEnumerator BuildHouse(params string[] kept)
        {
            Match.ModeOverride = "Dibs";
            Session.SelectMap("pinwheel");
            room = new GameObject("Stray piece test house").AddComponent<RoomBuilder>();
            var keep = new HashSet<Smashable>();
            foreach (string word in kept) keep.Add(room.Originals.First(s => s.Word == word && !keep.Contains(s)));
            foreach (var prop in room.Originals)
                if (!keep.Contains(prop)) prop.gameObject.SetActive(false);
            yield return Watch(room.gameObject.AddComponent<StrayPieceReturner>());
        }

        IEnumerator Watch(StrayPieceReturner watched)
        {
            returner = watched;
            returned.Clear();
            returner.ReturnAfter = QuickReturn;
            returner.PieceReturned += returned.Add;
            yield return TestScenes.WaitUntil(() => returner.Tracked > 0, 5f, "the returner to learn where the furniture lives");
        }

        Smashable Piece(string word) => room.Originals.First(s => s && s.gameObject.activeSelf && s.Word == word);

        static void Put(Smashable piece, Vector3 at)
        {
            var body = piece.GetComponent<Rigidbody>();
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            piece.transform.position = at;
            body.position = at;
            Physics.SyncTransforms();
        }

        static void Stand(Smashable piece, Vector3 floor)
        {
            Physics.SyncTransforms();
            float lowest = piece.transform.position.y;
            foreach (var shape in piece.GetComponentsInChildren<Collider>())
                if (shape.enabled && !shape.isTrigger) lowest = Mathf.Min(lowest, shape.bounds.min.y);
            Put(piece, new Vector3(floor.x, floor.y + Gap + piece.transform.position.y - lowest, floor.z));
        }

        static GameObject Slab(string name, Vector3 centre, Vector3 size)
        {
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = name;
            slab.transform.position = centre;
            slab.transform.localScale = size;
            Physics.SyncTransforms();
            return slab;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        float OuterEdge() => room.Layout.Rooms.Max(r => r.MaxX);

        IEnumerator WaitForReturn(Smashable piece, string what)
        {
            yield return TestScenes.WaitUntil(() => returned.Contains(piece), Patience, what);
        }

        [UnityTest]
        public IEnumerator APieceLeftOutsideTheHouseComesHomeWhileOnePushedIndoorsStays()
        {
            yield return BuildHouse("WATER", "MUG");
            Smashable water = Piece("WATER"), mug = Piece("MUG");
            Vector3 waterHome = water.transform.position, mugHome = mug.transform.position;
            var waterFacing = water.transform.rotation;
            var playroom = room.Layout.Room("Playroom");
            var indoors = new Vector3((playroom.MinX + playroom.MaxX) * .5f, playroom.FloorY, (playroom.MinZ + playroom.MaxZ) * .5f - 2f);
            float outside = OuterEdge() + 6f;
            Slab("Ground outside the house", new Vector3(outside, -.5f, 0f), new Vector3(8f, 1f, 8f));

            Stand(mug, indoors);
            Stand(water, new Vector3(outside, 0f, 0f));
            yield return WaitForReturn(water, "the WATER outside the house to come home");

            Assert.Less(Flat(water.transform.position, waterHome), NearHome, "it is back where the house put it");
            Assert.Less(Mathf.Abs(water.transform.position.y - waterHome.y), NearHome);
            Assert.Less(Quaternion.Angle(water.transform.rotation, waterFacing), 30f, "it stands the way it stood");
            Assert.IsFalse(water.IsBroken);
            Assert.AreEqual(1, returner.Returned);
            Assert.IsFalse(returned.Contains(mug), "a piece on another room's floor is in reach and is left alone");
            Assert.Less(Flat(mug.transform.position, indoors), Moved, "the MUG stays where it was pushed");
            Assert.Greater(Flat(mug.transform.position, mugHome), 1f);

            yield return TestScenes.WaitUntil(() => water.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude < .01f, Patience, "the WATER to settle at home");
            int before = returner.Returned;
            Stand(water, new Vector3(outside, 0f, 2f));
            yield return TestScenes.WaitUntil(() => returner.Returned > before, Patience, "a second trip outside to end the same way");
            Assert.Less(Flat(water.transform.position, waterHome), NearHome);
            Assert.IsFalse(returned.Contains(mug));
        }

        [UnityTest]
        public IEnumerator APieceStuckHighOrFallenUnderTheHouseComesHome()
        {
            yield return BuildHouse("WATER", "MUG");
            Smashable water = Piece("WATER"), mug = Piece("MUG");
            Vector3 waterHome = water.transform.position, mugHome = mug.transform.position;
            var playroom = room.Layout.Room("Playroom");
            var shelf = new Vector3((playroom.MinX + playroom.MaxX) * .5f, playroom.FloorY + 2f, (playroom.MinZ + playroom.MaxZ) * .5f - 2f);
            Slab("High shelf", shelf + Vector3.down * .1f, new Vector3(2.5f, .2f, 2.5f));

            Stand(water, shelf);
            Put(mug, new Vector3(mugHome.x, mugHome.y - 12f, mugHome.z));
            yield return WaitForReturn(mug, "the MUG under the house to come home");
            yield return WaitForReturn(water, "the WATER on the high shelf to come home");

            Assert.Less(Flat(water.transform.position, waterHome), NearHome);
            Assert.Less(Mathf.Abs(water.transform.position.y - waterHome.y), NearHome);
            Assert.Less(Flat(mug.transform.position, mugHome), NearHome);
            Assert.Less(Mathf.Abs(mug.transform.position.y - mugHome.y), NearHome);
        }

        [UnityTest]
        public IEnumerator ACarriedOrFixedPieceIsNeverMovedAndOneLetGoIs()
        {
            yield return BuildHouse("WATER", "MUG", "SODA");
            Smashable water = Piece("WATER"), mug = Piece("MUG"), soda = Piece("SODA");
            float outside = OuterEdge() + 6f;
            Slab("Ground outside the house", new Vector3(outside, -.5f, 0f), new Vector3(8f, 1f, 12f));
            var hand = new GameObject("A hand outside the house").transform;
            hand.position = new Vector3(outside, 1f, -3f);
            var waterParent = water.transform.parent;
            var waterBody = water.GetComponent<Rigidbody>();

            waterBody.isKinematic = true;
            water.transform.SetParent(hand, true);
            Put(water, hand.position);
            mug.GetComponent<Rigidbody>().isKinematic = true;
            var fixedAt = new Vector3(outside, .4f, 3f);
            Put(mug, fixedAt);
            Stand(soda, new Vector3(outside, 0f, 0f));
            yield return WaitForReturn(soda, "the loose SODA to come home");
            float waited = Time.time + QuickReturn * 4f;
            yield return TestScenes.WaitUntil(() => Time.time >= waited, Patience, "a few more checks");

            Assert.IsFalse(returned.Contains(water), "a carried piece stays in the hand");
            Assert.IsFalse(returned.Contains(mug), "a fixed piece stays put");
            Assert.Less(Vector3.Distance(water.transform.position, hand.position), .05f);
            Assert.Less(Vector3.Distance(mug.transform.position, fixedAt), .05f);

            water.transform.SetParent(waterParent, true);
            waterBody.isKinematic = false;
            yield return WaitForReturn(water, "the WATER to come home once it is let go");
            Assert.IsFalse(returned.Contains(mug));
        }

        [UnityTest]
        public IEnumerator APieceWhoseHomeIsTakenLandsBesideIt()
        {
            yield return BuildHouse("WATER");
            var water = Piece("WATER");
            var home = water.transform.position;
            var kitchen = room.Layout.Room(room.Layout.RoomAt(home.x, home.y, home.z));
            float outside = OuterEdge() + 6f;
            Slab("Ground outside the house", new Vector3(outside, -.5f, 0f), new Vector3(8f, 1f, 8f));

            Stand(water, new Vector3(outside, 0f, 0f));
            var crate = Slab("A crate on the home spot", home + Vector3.up * .3f, Vector3.one * .5f);
            crate.AddComponent<Rigidbody>().isKinematic = true;
            Physics.SyncTransforms();
            yield return WaitForReturn(water, "the WATER to come back beside its taken home");

            float away = Flat(water.transform.position, home);
            Assert.Greater(away, .4f, "it does not land inside the crate");
            Assert.Less(away, StrayPieces.SpotStep(.5f) * StrayPieces.SpotRings + .5f, "it lands near home");
            Assert.IsTrue(kitchen.Contains(water.transform.position.x, water.transform.position.z), "it lands in its own room");
            yield return TestScenes.WaitUntil(() => water.GetComponent<Rigidbody>().linearVelocity.sqrMagnitude < .01f, Patience, "the WATER to settle");
            int before = returner.Returned;
            float waited = Time.time + QuickReturn * 4f;
            yield return TestScenes.WaitUntil(() => Time.time >= waited, Patience, "a few more checks");
            Assert.AreEqual(before, returner.Returned, "once beside home it is in place and stays");
        }

        [UnityTest]
        public IEnumerator TheHouseSceneStartsItsOwnReturnerAndLeavesVanKeepsakesAlone()
        {
            Session.SelectMap("pinwheel");
            Match.ModeOverride = HouseModes.MovingOut;
            Session.Remember(new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
            var director = Object.FindAnyObjectByType<MovingOutDirector>();
            yield return TestScenes.WaitUntil(() => director && director.Current == MovingOutDirector.State.Playing, 10f, "Pack the Van to start");
            room = Object.FindAnyObjectByType<RoomBuilder>();
            Assert.IsTrue(room.TryGetComponent(out StrayPieceReturner own), "the house scene adds the returner by itself");
            yield return Watch(own);

            var keepsake = Object.FindObjectsByType<Keepsake>().First(k => !k.Packed).GetComponent<Smashable>();
            var loose = room.Originals.First(s => s && !s.IsBroken && !s.Invulnerable && !s.GetComponent<Keepsake>() && s.Word is "WATER" or "MUG");
            var looseHome = loose.transform.position;
            float outside = OuterEdge() + 8f;
            Slab("Ground outside the house", new Vector3(outside, -.5f, 0f), new Vector3(8f, 1f, 12f));

            Stand(keepsake, new Vector3(outside, 0f, 3f));
            Stand(loose, new Vector3(outside, 0f, -3f));
            yield return WaitForReturn(loose, "the loose piece outside the house to come home");
            float waited = Time.time + QuickReturn * 4f;
            yield return TestScenes.WaitUntil(() => Time.time >= waited, Patience, "a few more checks");

            Assert.Less(Flat(loose.transform.position, looseHome), NearHome);
            Assert.IsFalse(returned.Contains(keepsake), "a keepsake is the van's load and is never moved by the returner");
        }
    }
}
