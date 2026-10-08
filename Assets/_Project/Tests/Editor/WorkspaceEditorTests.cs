using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.EditorTools;

namespace Wreckabulary.Tests.Editor
{
    public class WorkspaceEditorTests
    {
        SceneSetup[] previousScenes;
        SceneAsset previousPlayEntry;
        bool safeToRestore;

        [SetUp]
        public void SetUp()
        {
            safeToRestore = false;
            for (int i = 0; !Application.isBatchMode && i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty || (string.IsNullOrEmpty(scene.path) && scene.rootCount > 0))
                    Assert.Ignore("Save open authoring scenes before running workspace scene integration tests.");
            }
            previousScenes = EditorSceneManager.GetSceneManagerSetup();
            previousPlayEntry = EditorSceneManager.playModeStartScene;
            safeToRestore = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (!safeToRestore) return;
            EditorSceneManager.playModeStartScene = previousPlayEntry;
            if (previousScenes.Length > 0 && previousScenes.All(scene => !string.IsNullOrEmpty(scene.path)))
                EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [Test]
        public void PrototypeBootstrapRefusesBeforeTouchingProductionAssets()
        {
            string[] paths =
            {
                WorkspaceWindow.HubPath,
                "Assets/_Project/Prefabs/Player.prefab",
                "Assets/_Project/Resources/GameAssets.asset",
                "ProjectSettings/EditorBuildSettings.asset"
            };
            var before = paths.Select(File.ReadAllBytes).ToArray();
            var scene = SceneManager.GetActiveScene().handle;
            var error = Assert.Throws<InvalidOperationException>(PrototypeBuilder.BuildAll);
            StringAssert.Contains("will not overwrite", error.Message);
            for (int i = 0; i < paths.Length; i++)
                CollectionAssert.AreEqual(before[i], File.ReadAllBytes(paths[i]), paths[i]);
            Assert.AreEqual(scene, SceneManager.GetActiveScene().handle);
        }

        [Test]
        public void StartupCanUseACleanBlankScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.IsTrue(WorkspaceBootstrap.CanOpenHubOnStartup());
        }

        [Test]
        public void StartupPreservesUnsavedAuthoringWork()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var editedObject = new GameObject("Unsaved authoring work");
            EditorSceneManager.MarkSceneDirty(scene);
            Assert.IsFalse(WorkspaceBootstrap.CanOpenHubOnStartup());
            Assert.IsTrue(editedObject);
            Assert.IsTrue(scene.isDirty);
        }

        [Test]
        public void StartupPreservesSavedAuthoringScene()
        {
            EditorSceneManager.OpenScene(WorkspaceWindow.HubPath, OpenSceneMode.Single);
            Assert.IsFalse(WorkspaceBootstrap.CanOpenHubOnStartup());
        }

        [Test]
        public void StartupCanReplacePreviousEmptyTestScene()
        {
            string path = AssetDatabase.GUIDToAssetPath(WorkspaceWindow.EmptyGuid);
            Assert.IsNotEmpty(path);
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Assert.IsTrue(WorkspaceBootstrap.CanOpenHubOnStartup());
        }

        [Test]
        public void StartupPreservesAdditiveWorkspace()
        {
            EditorSceneManager.OpenScene(WorkspaceWindow.HubPath, OpenSceneMode.Single);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            Assert.IsFalse(WorkspaceBootstrap.CanOpenHubOnStartup());
        }

        [Test]
        public void ActiveTestRunDisablesDefaultPlayEntry()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(WorkspaceWindow.HubPath);
            WorkspaceBootstrap.ApplyPlayEntry();
            Assert.IsNull(EditorSceneManager.playModeStartScene);
        }
    }
}
