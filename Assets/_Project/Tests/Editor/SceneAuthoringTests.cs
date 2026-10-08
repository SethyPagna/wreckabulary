using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.EditorTools;

namespace Wreckabulary.EditorTests
{
    public sealed class SceneAuthoringTests
    {
        [TestCase("Hub")]
        [TestCase("LivingRoom")]
        [TestCase("MovingDay")]
        [TestCase("Tutorial")]
        public void SavedScenesHaveCurrentPersistentVisualsAndThirdPersonPreview(string name)
        {
            string path = SceneWorkspace.SceneFolder + name + ".unity";
            var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            Assert.That(AssetDatabase.GetLabels(asset), Does.Contain(SceneWorkspace.MigrationLabel));
            var scene = EditorSceneManager.OpenPreviewScene(path);
            try
            {
                var roots = scene.GetRootGameObjects();
                var preview = roots.SelectMany(root => root.GetComponentsInChildren<EditorScenePreview>(true)).Single();
                Assert.AreEqual("EditorOnly", preview.tag);
                Assert.IsNull(preview.GetComponentInChildren<PlayerController>(true));
                Assert.IsNotNull(preview.GetComponentInChildren<SkinnedMeshRenderer>(true));
                var camera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>()).Single();
                Assert.AreEqual(55f, camera.fieldOfView, .01f);
                Assert.AreEqual(.08f, camera.nearClipPlane, .001f);
                Assert.That(camera.transform.eulerAngles.x, Is.InRange(9f, 11f));
                var world = roots.SelectMany(root => root.GetComponentsInChildren<AuthoredHouse>(true)).FirstOrDefault();
                if (name is "LivingRoom" or "MovingDay")
                {
                    Assert.IsNotNull(world);
                    Assert.IsNotNull(PrefabUtility.GetCorrespondingObjectFromSource(world));
                    Assert.IsFalse(roots.Any(root => root.name is "Room" or "Furniture" or "Legacy scene content (preserved)"));
                    Assert.That(world.FurnitureRoot.GetComponentsInChildren<LetterBuilt>().Count(item => item.UsesImportedModel), Is.GreaterThan(5));
                }
                foreach (var presentation in roots.SelectMany(root => root.GetComponentsInChildren<HousePresentation>(true)))
                {
                    Assert.IsTrue(presentation.IsAuthored);
                    foreach (var renderer in presentation.GetComponentsInChildren<Renderer>())
                        foreach (var material in renderer.sharedMaterials)
                            Assert.IsTrue(EditorUtility.IsPersistent(material), renderer.name + " must survive editor reopen.");
                    foreach (var mesh in presentation.GetComponentsInChildren<MeshFilter>())
                        Assert.IsTrue(EditorUtility.IsPersistent(mesh.sharedMesh), mesh.name + " must survive editor reopen.");
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
