using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;
using Wreckabulary.Rules.Surroundings;

namespace Wreckabulary.EditorTests
{
    public sealed class SurroundingsBakeTests
    {
        const string Map = "pinwheel";

        static GameObject Prefab(bool low)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SurroundingsBake.PrefabPath(Map, low));
            Assert.IsNotNull(prefab, "Run Wreckabulary.EditorTools.SurroundingsBake.Run: " + SurroundingsBake.PrefabPath(Map, low));
            return prefab;
        }

        static int Triangles(GameObject prefab) =>
            prefab.GetComponentsInChildren<MeshFilter>(true).Sum(f => Enumerable.Range(0, f.sharedMesh.subMeshCount).Sum(s => (int)f.sharedMesh.GetIndexCount(s) / 3));

        [Test]
        public void ThePinwheelBackdropPrefabsExist()
        {
            foreach (bool low in new[] { false, true })
            {
                var prefab = Prefab(low);
                Assert.AreEqual(Map + (low ? MapSurroundings.LowSuffix : ""), prefab.name, "a saved prefab's root takes the asset name; Attach renames the instance Backdrop");
                var surroundings = prefab.GetComponent<MapSurroundings>();
                Assert.IsNotNull(surroundings);
                Assert.AreEqual(Map, surroundings.MapId);
                Assert.AreEqual(low, surroundings.LowTier);
                Assert.AreEqual("MapleMorning", surroundings.LightId);
                Assert.AreEqual("maple_close", surroundings.AmbienceId);
                Assert.IsNotNull(prefab.transform.Find(nameof(SurroundingsRing.Near)), "a near ring");
                Assert.IsNotNull(Resources.Load<GameObject>(MapSurroundings.PrefabPath(Map, low)), "loadable from Resources");
            }
            Assert.IsNotNull(Prefab(false).transform.Find(nameof(SurroundingsRing.Mid)));
            Assert.IsNotNull(Prefab(false).transform.Find(nameof(SurroundingsRing.Far)));
            Assert.IsNull(Prefab(true).transform.Find(nameof(SurroundingsRing.Mid)), "the Low tier keeps the near ring and cards only");
        }

        [Test]
        public void TheBackdropIsOnlyToLookAt()
        {
            foreach (bool low in new[] { false, true })
            {
                var prefab = Prefab(low);
                Assert.IsEmpty(prefab.GetComponentsInChildren<Collider>(true), "no colliders");
                Assert.IsEmpty(prefab.GetComponentsInChildren<Rigidbody>(true), "no rigidbodies");
                Assert.IsEmpty(prefab.GetComponentsInChildren<Light>(true), "no lights");
                Assert.IsEmpty(prefab.GetComponentsInChildren<Smashable>(true), "nothing to smash");
                Assert.IsEmpty(prefab.GetComponentsInChildren<AudioSource>(true), "ambience is attached at runtime");
                Assert.IsEmpty(prefab.GetComponentsInChildren<Camera>(true));
            }
            Assert.IsEmpty(Prefab(true).GetComponentsInChildren<BackdropMover>(true), "the Low tier holds still");
        }

        [Test]
        public void EveryBackdropMeshIsASavedAsset()
        {
            string folder = SurroundingsBake.MeshFolder + "/" + Map + "/";
            foreach (bool low in new[] { false, true })
                foreach (var filter in Prefab(low).GetComponentsInChildren<MeshFilter>(true))
                {
                    Assert.IsTrue(filter.sharedMesh && EditorUtility.IsPersistent(filter.sharedMesh), filter.name);
                    StringAssert.StartsWith(folder, AssetDatabase.GetAssetPath(filter.sharedMesh), filter.name);
                }
        }

        [Test]
        public void TheBackdropStaysWithinItsBudget()
        {
            var high = Prefab(false);
            var low = Prefab(true);
            Assert.LessOrEqual(high.GetComponentsInChildren<Renderer>(true).Length, SurroundingsBudget.HighRenderers, "High renderers");
            Assert.LessOrEqual(Triangles(high), SurroundingsBudget.HighTriangles, "High triangles");
            Assert.LessOrEqual(low.GetComponentsInChildren<Renderer>(true).Length, SurroundingsBudget.LowRenderers, "Low renderers");
            Assert.LessOrEqual(Triangles(low), SurroundingsBudget.LowTriangles, "Low triangles");
            Assert.LessOrEqual(high.GetComponentsInChildren<BackdropMover>(true).Length, SurroundingsBudget.MaxMovers);
            foreach (var filter in high.GetComponentsInChildren<MeshFilter>(true))
                Assert.LessOrEqual(filter.sharedMesh.vertexCount, SurroundingsLayout.MergeVertexLimit, filter.name);
        }

        [Test]
        public void EveryMaterialUsesTheBackdropShaderAndOnlyTheNearRingCasts()
        {
            foreach (bool low in new[] { false, true })
                foreach (var renderer in Prefab(low).GetComponentsInChildren<Renderer>(true))
                {
                    Assert.IsNotEmpty(renderer.sharedMaterials, renderer.name);
                    foreach (var material in renderer.sharedMaterials)
                    {
                        Assert.IsTrue(material && EditorUtility.IsPersistent(material), renderer.name);
                        Assert.AreEqual(BackdropShader.ShaderName, material.shader.name, renderer.name);
                        StringAssert.StartsWith(SurroundingsBake.MaterialFolder + "/", AssetDatabase.GetAssetPath(material));
                    }
                    Assert.IsTrue(renderer.receiveShadows, renderer.name);
                    bool near = renderer.transform.parent && renderer.transform.parent.name == nameof(SurroundingsRing.Near);
                    var expected = near && !low ? ShadowCastingMode.On : ShadowCastingMode.Off;
                    Assert.AreEqual(expected, renderer.shadowCastingMode, renderer.transform.parent.name + "/" + renderer.name);
                }
        }

        [Test]
        public void ThePrefabMatchesAFreshLayout()
        {
            string plan = SurroundingsBake.PlanPath(Map);
            var result = SurroundingsBake.Layout(plan, out var parsed, out var kit);
            Assert.IsTrue(result.Valid, string.Join("\n", result.Problems));
            var receipt = SurroundingsBake.LoadReceipt(Map);
            Assert.IsNotNull(receipt, "the bake writes " + SurroundingsBake.ReceiptPath(Map));
            Assert.IsTrue(receipt.passed, string.Join("\n", receipt.errors));
            Assert.AreEqual(result.Hash, receipt.layoutHash, "the receipt is from this layout");
            foreach (bool low in new[] { false, true })
                Assert.AreEqual(result.Hash, Prefab(low).GetComponent<MapSurroundings>().LayoutHash, "re-bake: the prefab is from an older layout");
            var inputs = SurroundingsBake.DataInputs(parsed, kit, plan);
            CollectionAssert.AreEqual(inputs.Select(f => f.path + " " + f.sha256), receipt.inputs.Select(f => f.path + " " + f.sha256), "re-bake: an input changed since the bake");
            foreach (var output in receipt.outputs)
                Assert.AreEqual(output.sha256, SurroundingsBake.Hash(output.path).sha256, output.path + " changed since the bake");
            Assert.AreEqual(receipt.high.renderers, Prefab(false).GetComponentsInChildren<Renderer>(true).Length);
            Assert.AreEqual(receipt.low.renderers, Prefab(true).GetComponentsInChildren<Renderer>(true).Length);
            Assert.AreEqual(receipt.high.triangles, Triangles(Prefab(false)));
            Assert.AreEqual(receipt.low.triangles, Triangles(Prefab(true)));
        }

        [Test]
        public void TheGroundCarriesItsZoneTints()
        {
            var result = SurroundingsBake.Layout(SurroundingsBake.PlanPath(Map), out _, out _);
            var receipt = SurroundingsBake.LoadReceipt(Map);
            Assert.IsNotNull(receipt);
            foreach (var slot in result.Ground.Select(z => z.Slot).Distinct())
            {
                var group = receipt.high.groups.Single(g => g.name == SurroundingsBake.GroundName + "/" + slot);
                int tints = result.Ground.Where(z => z.Slot == slot).Select(z => z.Tint).Distinct().Count();
                Assert.AreEqual(tints, group.colours, slot + ": one vertex colour per zone tint");
                Assert.AreEqual(result.Ground.Count(z => z.Slot == slot), group.pieces, slot + " zones");
            }
            Assert.Greater(result.Ground.Select(z => z.Tint).Distinct().Count(), 1, "Maple Close's ground has more than one tone");
        }

        [Test]
        public void ThePlayVolumeMatchesTheLayout()
        {
            var result = SurroundingsBake.Layout(SurroundingsBake.PlanPath(Map), out _, out _);
            var box = MapSurroundings.PlayBox(GameConfig.Current.HouseFor(Map));
            var volume = result.PlayVolume;
            Assert.AreEqual(volume.MinX, box.min.x, 1e-3f); Assert.AreEqual(volume.MaxX, box.max.x, 1e-3f);
            Assert.AreEqual(volume.MinY, box.min.y, 1e-3f); Assert.AreEqual(volume.MaxY, box.max.y, 1e-3f);
            Assert.AreEqual(volume.MinZ, box.min.z, 1e-3f); Assert.AreEqual(volume.MaxZ, box.max.z, 1e-3f);
        }

        [Test]
        public void TheKitImportCheckPasses()
        {
            var report = SurroundingsImport.Check();
            var problems = report.errors.Concat(report.results.SelectMany(r => r.errors.Select(e => r.kit + "/" + r.id + ": " + e)));
            Assert.IsTrue(report.passed, string.Join("\n", problems));
            Assert.Greater(report.pieces, 0);
        }
    }
}
