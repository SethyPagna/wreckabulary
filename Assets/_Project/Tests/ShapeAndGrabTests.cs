using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Wreckabulary.Tests
{
    /// <summary>3D letters, Word World-style object shapes, and grabbing every piece of furniture.</summary>
    public class ShapeAndGrabTests
    {
        [UnitySetUp]
        public IEnumerator SetUp() => TestScenes.Reset();

        [UnityTearDown]
        public IEnumerator TearDown() => TestScenes.Reset();

        [Test]
        public void EveryLetterHasASolidMesh()
        {
            for (char c = 'A'; c <= 'Z'; c++)
            {
                var mesh = GameAssets.I.LetterMesh(c);
                Assert.IsNotNull(mesh, $"{c} mesh");
                Assert.Greater(mesh.triangles.Length, 30, $"{c} has geometry");
                Assert.That(mesh.bounds.size.y, Is.EqualTo(1f).Within(0.01f), $"{c} is normalised to height 1");
                Assert.That(mesh.bounds.min.y, Is.EqualTo(0f).Within(0.01f), $"{c} sits on the baseline");
            }
            // Letters keep their own proportions: I is narrow, W is wide.
            Assert.Less(GameAssets.I.LetterMesh('I').bounds.size.x, GameAssets.I.LetterMesh('W').bounds.size.x * 0.4f);
        }

        [Test]
        public void LetterMeshesFaceTheCamera()
        {
            // Front faces point at -z (towards the camera); their triangles must wind so they're visible from there.
            var mesh = GameAssets.I.LetterMesh('A');
            var v = mesh.vertices;
            var n = mesh.normals;
            var t = mesh.triangles;
            int front = 0, wrong = 0;
            for (int i = 0; i < t.Length; i += 3)
            {
                var normal = n[t[i]];
                var cross = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (cross.sqrMagnitude < 1e-10f) continue;
                if (normal == Vector3.back) front++;
                if (Vector3.Dot(cross, normal) < 0f) wrong++;
            }
            Assert.Greater(front, 0);
            Assert.AreEqual(0, wrong, "every triangle winds towards its normal");
        }

        [UnityTest]
        public IEnumerator FurnitureIsShapedFromItsOwnLetters()
        {
            var bed = LetterBuilt.Spawn("BED", Vector3.one * 0.5f, 0, Color.blue, null);
            yield return null;
            Assert.AreEqual(3, bed.Blocks.Count);
            CollectionAssert.AreEqual(new[] { "Letter_B", "Letter_E", "Letter_D" }, bed.Blocks.Select(b => b.name).ToArray());

            // The B headboard stands tall, the E mattress lies flat, the D footboard is lower than the headboard.
            var headboard = bed.Blocks[0].GetComponent<Renderer>().bounds;
            var mattress = bed.Blocks[1].GetComponent<Renderer>().bounds;
            var footboard = bed.Blocks[2].GetComponent<Renderer>().bounds;
            Assert.Greater(headboard.size.y, mattress.size.y * 2f);
            Assert.Greater(mattress.size.z, mattress.size.y * 2f, "the mattress lies down");
            Assert.Greater(headboard.max.y, footboard.max.y);
            Assert.Less(headboard.center.x, mattress.center.x);
            Assert.Less(mattress.center.x, footboard.center.x);
        }

        [UnityTest]
        public IEnumerator LooseTilesAreThreeDLetters()
        {
            TilePool.Ensure();
            var tile = TilePool.Instance.Get('Q');
            yield return null;
            Assert.AreEqual(GameAssets.I.LetterMesh('Q'), tile.GetComponentInChildren<MeshFilter>().sharedMesh);
            var w = TilePool.Instance.Get('W');
            var i = TilePool.Instance.Get('I');
            Assert.Greater(w.GetComponent<BoxCollider>().size.x, i.GetComponent<BoxCollider>().size.x * 2f, "colliders fit each letter");
        }

        [UnityTest]
        public IEnumerator EveryPieceOfLivingRoomFurnitureCanBeGrabbedAndThrown()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var input = new ScriptedBinding();
            var p = joins.Join(input);
            yield return new WaitForSeconds(0.3f);

            var furniture = Object.FindObjectsByType<Smashable>().Where(s => s.GetComponent<LetterBuilt>())
                                  .OrderBy(s => s.Word).ToList();
            Assert.Greater(furniture.Count, 8);
            foreach (var item in furniture)
            {
                if (!item) continue; // knocked into something and broken by an earlier throw
                var target = item.GetComponent<Rigidbody>();
                var dir = World.Flat(target.worldCenterOfMass - Vector3.zero).normalized;
                if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
                // Stand just in front of it (on the room side), facing it.
                var from = World.Flat(target.worldCenterOfMass) - dir * 1.1f;
                // Grab takes the nearest thing, so something resting on it (a MUG on the TABLE) may come first.
                for (int attempt = 0; attempt < 3 && p.Combat.Held != target; attempt++)
                {
                    if (p.Combat.Held) p.Combat.Throw();
                    p.Respawn(from);
                    p.FaceTowards(dir);
                    yield return new WaitForFixedUpdate();
                    input.Next.grab = true;
                    yield return null;
                    yield return null;
                }
                Assert.AreEqual(target, p.Combat.Held, $"grabbed the {item.Word}");
                Assert.Less(p.CarryScale, 1.01f);

                input.Next.attack = true; // attack while holding throws
                yield return null;
                yield return null;
                Assert.IsNull(p.Combat.Held, $"threw the {item.Word}");
                yield return new WaitForSeconds(0.4f); // past the attack cooldown
            }
        }

        static PlayerController SpawnOnGround(out ScriptedBinding input)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = Vector3.down * 0.5f;
            ground.transform.localScale = new Vector3(40f, 1f, 40f);
            TilePool.Ensure();
            input = new ScriptedBinding();
            var p = Object.Instantiate(GameAssets.I.playerPrefab);
            p.Setup(0, input);
            p.Respawn(Vector3.zero);
            p.FaceTowards(Vector3.forward);
            return p;
        }

        static Bounds BoundsOf(Component c)
        {
            var rs = c.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        [UnityTest]
        public IEnumerator LightThingsAreCarriedInFrontWithBothHandsOnThem()
        {
            var p = SpawnOnGround(out var input);
            var box = DeliverySpawner.CreateBox("BOX", new Vector3(0f, 0f, 1.1f));
            yield return new WaitForSeconds(0.3f);
            input.Next.grab = true;
            yield return null;
            yield return null;
            Assert.AreEqual(box.GetComponent<Rigidbody>(), p.Combat.Held);
            Assert.IsFalse(p.Combat.IsOverhead);

            // Walk around with it; it should stay put in front, unsquashed, with the hands on it.
            input.Next.move = new Vector2(1f, 0.5f);
            yield return new WaitForSeconds(1f);
            input.Next.move = Vector2.zero;
            yield return new WaitForSeconds(0.2f);

            var b = BoundsOf(box);
            var toBox = World.Flat(b.center - p.transform.position);
            Assert.That(Vector3.Dot(toBox, p.Facing), Is.InRange(0.4f, 1.4f), "in front of the player");
            Assert.That(b.center.y - p.transform.position.y, Is.InRange(0.7f, 1.4f), "at chest height");
            Assert.That(box.transform.lossyScale.x, Is.EqualTo(1f).Within(0.001f), "not squashed with the body");
            Assert.Less(b.SqrDistance(p.handL.position), 0.05f, "left hand on the box");
            Assert.Less(b.SqrDistance(p.handR.position), 0.05f, "right hand on the box");
        }

        [UnityTest]
        public IEnumerator HeavyThingsAreLiftedOverTheHead()
        {
            var p = SpawnOnGround(out var input);
            var sofa = FurnitureCatalog.Spawn("SOFA", new Vector3(0f, 0f, 1.3f), 0f, null);
            yield return new WaitForSeconds(0.3f);
            input.Next.grab = true;
            yield return null;
            yield return null;
            Assert.IsTrue(p.Combat.IsOverhead);
            yield return new WaitForSeconds(0.3f);

            var b = BoundsOf(sofa);
            Assert.Greater(b.min.y - p.transform.position.y, 1.2f, "clear of the head");
            Assert.Less(World.Flat(b.center - p.transform.position).magnitude, 0.4f, "centred over the player");
            Assert.Less(Mathf.Abs(p.handL.position.y - b.min.y), 0.15f, "hands holding it up from underneath");
        }

        [UnityTest]
        public IEnumerator RoommatesAreCarriedOverheadAndThrown()
        {
            var p = SpawnOnGround(out var input);
            var friend = Object.Instantiate(GameAssets.I.playerPrefab);
            friend.Setup(1, new ScriptedBinding());
            friend.Respawn(new Vector3(0f, 0f, 1f));
            friend.Inventory.Set("ABC");
            yield return new WaitForSeconds(0.3f);

            input.Next.grab = true;
            yield return null;
            yield return null;
            Assert.IsTrue(friend.IsHeld);
            input.Next.move = Vector2.right;
            yield return new WaitForSeconds(0.5f);
            var body = BoundsOf(friend.visual);
            Assert.Greater(body.center.y - p.transform.position.y, 1.4f, "up over the head");
            Assert.Less(World.Flat(body.center - p.transform.position).magnitude, 0.5f, "came along, centred over the carrier");

            input.Next.attack = true;
            yield return null;
            yield return null;
            Assert.IsFalse(friend.IsHeld);
            Assert.Less(friend.Inventory.Count, 3, "being thrown knocks letters loose");
        }

        [UnityTest]
        public IEnumerator GrabAgainPutsThingsDownGentlyInFront()
        {
            var p = SpawnOnGround(out var input);
            var box = DeliverySpawner.CreateBox("BOX", new Vector3(0f, 0f, 1.1f));
            yield return new WaitForSeconds(0.3f);
            input.Next.grab = true;
            yield return null;
            yield return null;
            Assert.IsNotNull(p.Combat.Held);

            input.Next.move = Vector2.right;
            yield return new WaitForSeconds(0.5f);
            input.Next.move = Vector2.zero;
            yield return new WaitForSeconds(0.2f);
            input.Next.grab = true;
            yield return null;
            yield return null;
            Assert.IsNull(p.Combat.Held, "put down");
            Assert.IsNull(box.GetComponent<ThrowTracker>(), "putting down isn't a throw");

            yield return new WaitForSeconds(0.6f);
            var rb = box.GetComponent<Rigidbody>();
            var b = BoundsOf(box);
            Assert.Less(rb.linearVelocity.magnitude, 0.3f, "resting, not flying");
            Assert.That(b.min.y, Is.EqualTo(0f).Within(0.08f), "on the floor");
            Assert.Greater(Vector3.Dot(World.Flat(b.center - p.transform.position), p.Facing), 0.4f, "in front of the player");
            Assert.Greater(Vector3.Dot(box.transform.up, Vector3.up), 0.95f, "upright");
            Assert.IsTrue(box, "not broken");
        }

        [UnityTest]
        public IEnumerator PuttingDownAgainstAWallDoesntPushThroughIt()
        {
            var p = SpawnOnGround(out var input);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0f, 1.5f, 1.4f);
            wall.transform.localScale = new Vector3(6f, 3f, 0.3f);
            var sofa = FurnitureCatalog.Spawn("SOFA", new Vector3(3f, 0f, 0f), 0f, null);
            yield return new WaitForSeconds(0.3f);
            p.Respawn(new Vector3(3f, 0f, -1.2f));
            p.FaceTowards(Vector3.forward);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(p.Combat.TryGrab());
            p.Respawn(Vector3.zero);
            p.FaceTowards(Vector3.forward);
            yield return new WaitForSeconds(0.2f);

            p.Combat.PutDown();
            yield return new WaitForSeconds(0.6f);
            Assert.Less(BoundsOf(sofa).max.z, 1.4f - 0.15f + 0.05f, "stays on this side of the wall");
        }

        [UnityTest]
        public IEnumerator DroppingALetterMakesRoomForANewOne()
        {
            var p = SpawnOnGround(out var input);
            yield return null;
            p.Inventory.Set("ABCDEF");
            Assert.IsTrue(p.Inventory.IsFull);

            input.Next.spellDown = true;
            yield return null;
            yield return null;
            input.Next.right = true;
            yield return null;
            yield return null;
            input.Next.right = true;
            yield return null;
            yield return null;
            Assert.AreEqual('C', p.Inventory.Letters[p.Summoner.Cursor]);
            input.Next.down = true; // drop the highlighted letter
            yield return null;
            yield return null;

            Assert.AreEqual("ABDEF", new string(p.Inventory.Letters.ToArray()));
            Assert.IsTrue(TilePool.Instance.Active.Any(t => t.Letter == 'C'), "the C is on the floor");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(5, p.Inventory.Count, "not picked straight back up");

            TilePool.Instance.Get('Z').Launch(p.transform.position + Vector3.up * 0.3f, Vector3.zero);
            yield return new WaitForSeconds(0.5f);
            CollectionAssert.Contains(p.Inventory.Letters.ToArray(), 'Z', "room for a new letter");
        }

        [UnityTest]
        public IEnumerator DroppingALetterKeepsTheWordBeingSpelled()
        {
            var p = SpawnOnGround(out var input);
            yield return null;
            p.Inventory.Set("BXAT");
            p.Summoner.Open();
            p.Summoner.Add();              // B; highlight moves to X
            Assert.IsTrue(p.Summoner.DropHighlighted()); // drop the X
            Assert.AreEqual("B", p.Summoner.Spelled);
            Assert.AreEqual('A', p.Inventory.Letters[p.Summoner.Cursor]);
            p.Summoner.Add();              // A
            p.Summoner.Add();              // T
            Assert.IsTrue(p.Summoner.Cast());
            Assert.AreEqual("BAT", p.Combat.Weapon?.word);
        }

        [UnityTest]
        public IEnumerator FurnitureStaysStandingWhenLeftAlone()
        {
            yield return TestScenes.Load(Session.DibsScene);
            yield return new WaitForSeconds(3f);
            var fallen = Object.FindObjectsByType<Smashable>().Where(s => s.GetComponent<LetterBuilt>())
                               .Where(s => Vector3.Dot(s.transform.up, Vector3.up) < 0.95f)
                               .Select(s => $"{s.Word} (up·y={Vector3.Dot(s.transform.up, Vector3.up):F2}, com={s.GetComponent<Rigidbody>().centerOfMass})")
                               .ToList();
            CollectionAssert.IsEmpty(fallen, "tipped over: " + string.Join(", ", fallen));
        }

        [UnityTest]
        public IEnumerator HeavyThingsSlowYouDown()
        {
            yield return TestScenes.Load(Session.DibsScene);
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var p = joins.Join(new ScriptedBinding());
            var sofa = Object.FindObjectsByType<Smashable>().First(s => s.Word == "SOFA").GetComponent<Rigidbody>();
            p.Respawn(World.Flat(sofa.worldCenterOfMass) + Vector3.back * 1.1f);
            p.FaceTowards(Vector3.forward);
            yield return new WaitForFixedUpdate();
            Assert.IsTrue(p.Combat.TryGrab());
            yield return null;
            Assert.Less(p.CarryScale, 0.6f, "a sofa is heavy");
        }
    }
}
