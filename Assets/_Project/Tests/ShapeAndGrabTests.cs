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

                input.Next.grab = true; // grab again throws
                yield return null;
                yield return null;
                Assert.IsNull(p.Combat.Held, $"threw the {item.Word}");
                yield return new WaitForSeconds(0.2f);
            }
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
