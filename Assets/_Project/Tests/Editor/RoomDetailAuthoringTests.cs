using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTests
{
    public sealed class RoomDetailAuthoringTests
    {
        const int RenderersPerRoom = 24, AverageRenderersPerRoom = 16;

        [TestCase("pinwheel")]
        [TestCase("courtyard")]
        [TestCase("flat")]
        [TestCase("terrace")]
        [TestCase("walkup")]
        public void SavedRoomDetailIsMergedPersistentAndKeepsPlayPathsClear(string map)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/" + AuthoredHouse.ResourcePath(map) + ".prefab");
            var world = prefab.GetComponent<AuthoredHouse>();
            var layout = GameConfig.Current.HouseFor(map);
            var space = world.GeometryRoot;
            var roots = world.GetComponentsInChildren<RoomDetail>(true).Where(d => !d.Fitted).ToArray();
            CollectionAssert.AreEquivalent(layout.Rooms.Select(r => r.Name), roots.Select(d => d.RoomId), map + " details every room once");
            var furniture = world.FurnitureRoot.GetComponentsInChildren<Smashable>(true).Select(s => ModelVisual.BoundsIn(space, s.gameObject)).ToArray();
            int spans = 0;
            foreach (var root in roots)
            {
                var room = layout.Room(root.RoomId);
                string where = map + " / " + room.Name;
                Assert.AreEqual(RoomDetail.CurrentEdition, root.Edition, where);
                Assert.AreEqual(layout.StoreyOf(room), root.OwnerStorey, where);
                Assert.IsEmpty(root.GetComponentsInChildren<Smashable>(true), where + " adds no loot");
                Assert.IsEmpty(root.GetComponentsInChildren<Rigidbody>(true), where + " adds nothing that moves");
                var renderers = root.GetComponentsInChildren<Renderer>(true);
                Assert.LessOrEqual(renderers.Length, RenderersPerRoom, where + " stays merged into a few meshes");
                foreach (var renderer in renderers)
                {
                    Assert.IsTrue(renderer.sharedMaterial && EditorUtility.IsPersistent(renderer.sharedMaterial), where + " " + renderer.name);
                    if (renderer.name.EndsWith(" fitted small", System.StringComparison.Ordinal))
                        Assert.AreEqual(UnityEngine.Rendering.ShadowCastingMode.Off, renderer.shadowCastingMode, where + " small fitted pieces cast no shadows");
                    var filter = renderer.GetComponent<MeshFilter>();
                    Assert.IsTrue(filter && filter.sharedMesh && EditorUtility.IsPersistent(filter.sharedMesh), where + " " + renderer.name);
                    foreach (string property in renderer.sharedMaterial.GetTexturePropertyNames())
                    {
                        var texture = renderer.sharedMaterial.GetTexture(property);
                        if (texture) Assert.IsTrue(EditorUtility.IsPersistent(texture), where + " " + property);
                    }
                }
                foreach (var cutaway in root.GetComponentsInChildren<CutawaySurface>(true))
                {
                    Assert.AreEqual(root.OwnerStorey, cutaway.OwnerStorey, where);
                    Assert.AreEqual(room.Name, cutaway.RoomId, where);
                    Assert.IsNotEmpty(cutaway.Renderers, where);
                }
                var fitted = root.GetComponentsInChildren<RoomDetail>(true).Where(d => d.Fitted).ToArray();
                Assert.LessOrEqual(fitted.Length, 1, where);
                foreach (var detail in fitted)
                {
                    Assert.AreEqual(detail.Spans.Count, detail.GetComponents<BoxCollider>().Length, where + " one collider per fitted run");
                    float reach = RoomDetailAuthoring.ColliderReach(layout, room);
                    foreach (var span in detail.Spans)
                    {
                        spans++;
                        Assert.LessOrEqual(span.max.y, room.FloorY + reach + .001f, where + " stays below the floor above");
                        Assert.IsTrue(span.min.x >= room.MinX - .01f && span.max.x <= room.MaxX + .01f && span.min.z >= room.MinZ - .01f && span.max.z <= room.MaxZ + .01f,
                            where + " keeps fitted runs inside the room");
                        Assert.LessOrEqual(Mathf.Min(span.size.x, span.size.z), RoomDetailAuthoring.MaxDepth + .001f, where + " hugs a wall");
                        foreach (var b in furniture)
                        {
                            if (b.max.y < room.FloorY - .3f || b.min.y > room.FloorY + 2.6f) continue;
                            Assert.IsFalse(Overlap(span, b, -.02f), where + " fitted run overlaps furniture at " + b.center);
                        }
                        foreach (var spawn in layout.Spawns.Where(s => s.Room == room.Name))
                            Assert.GreaterOrEqual(Gap(span, spawn.X, spawn.Z), 1.2f, where + " spawn clearance");
                        foreach (var door in layout.Doors.Where(d => d.A == room.Name || d.B == room.Name))
                            Assert.GreaterOrEqual(Gap(span, door.X, door.Z), 1f, where + " doorway clearance " + door.A + "/" + door.B);
                        foreach (var stair in layout.Stairs.Where(s => s.Lower == room.Name || s.Upper == room.Name))
                        {
                            var flight = new Bounds();
                            flight.SetMinMax(new Vector3(stair.MinX, room.FloorY, stair.MinZ), new Vector3(stair.MaxX, room.FloorY + 1f, stair.MaxZ));
                            Assert.IsFalse(Overlap(span, flight, .3f), where + " keeps the stairs clear");
                        }
                    }
                }
            }
            Assert.Greater(spans, layout.Rooms.Count, map + " gains fitted runs in most rooms");
            Assert.LessOrEqual(roots.Sum(r => r.GetComponentsInChildren<Renderer>(true).Length), layout.Rooms.Count * AverageRenderersPerRoom, map + " detail renderer budget");
        }

        [Test]
        public void FittedRunsGiveWayOnlyToFurnitureThatOverlapsThem()
        {
            var house = new GameObject("House");
            var furniture = new GameObject("Furniture");
            try
            {
                var fitted = new GameObject("Fitted detail");
                fitted.transform.SetParent(house.transform, false);
                var detail = fitted.AddComponent<RoomDetail>();
                detail.Configure("Kitchen", 0, true);
                detail.SetSpans(new[] { new Bounds(new Vector3(0f, 1.5f, .3f), new Vector3(3f, 3f, .6f)) });
                var loose = new GameObject("Room finish");
                loose.transform.SetParent(house.transform, false);
                loose.AddComponent<RoomDetail>().Configure("Kitchen", 0, false);

                var far = GameObject.CreatePrimitive(PrimitiveType.Cube);
                far.transform.SetParent(furniture.transform, false);
                far.transform.position = new Vector3(0f, .5f, 4f);
                far.AddComponent<Smashable>();
                Assert.AreEqual(0, RoomDetail.GiveWay(house.transform, furniture.transform));
                Assert.IsTrue(fitted.activeSelf);

                var near = GameObject.CreatePrimitive(PrimitiveType.Cube);
                near.transform.SetParent(furniture.transform, false);
                near.transform.position = new Vector3(1f, .5f, .5f);
                near.AddComponent<Smashable>();
                Assert.AreEqual(1, RoomDetail.GiveWay(house.transform, furniture.transform));
                Assert.IsFalse(fitted.activeSelf, "a saved home's furniture wins over a fitted run");
                Assert.IsTrue(loose.activeSelf, "floor and wall finish stay");
            }
            finally
            {
                Object.DestroyImmediate(house);
                Object.DestroyImmediate(furniture);
            }
        }

        static bool Overlap(Bounds a, Bounds b, float grow) =>
            a.min.x - grow < b.max.x && a.max.x + grow > b.min.x && a.min.z - grow < b.max.z && a.max.z + grow > b.min.z;

        static float Gap(Bounds span, float x, float z)
        {
            float dx = Mathf.Max(span.min.x - x, 0f, x - span.max.x);
            float dz = Mathf.Max(span.min.z - z, 0f, z - span.max.z);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
