using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    /// <summary>Stable entry points to the current scenes, character and presentation sources.</summary>
    public sealed class WorkspaceWindow : EditorWindow
    {
        public const string HubGuid = "4829ca58046a07b4db4cd309dd5093a8";
        public const string EmptyGuid = "58d0348edda8ffa4583aba09a2f26a9d";
        Vector2 scroll;

        [MenuItem("Wreckabulary/Open Current Workspace", priority = -100)]
        public static void ShowWindow()
        {
            var window = GetWindow<WorkspaceWindow>("Wreckabulary");
            window.minSize = new Vector2(360f, 520f);
            window.Show();
        }

        public static string HubPath => AssetDatabase.GUIDToAssetPath(HubGuid);

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            GUILayout.Space(14);
            GUILayout.Label("CURRENT WORKSPACE", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Edit the saved scenes and assets used by the game.", EditorStyles.wordWrappedLabel);
            GUILayout.Space(12);

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                SceneButton("Edit Hub / Main Menu", HubGuid);
                SceneButton("Edit Arena / Dibs", "82b92a76ebd641c49b5e9ed8aa025a1a");
                SceneButton("Edit Moving Day", "ac5fed3922febf84ca59faadd8da4e64");
                SceneButton("Edit Tutorial", "db2c4e6815ecee14ba4faa77ac3f3945");
                GUILayout.Space(8);
                bool openedScene = EditorGUILayout.ToggleLeft("Play the opened scene", WorkspaceBootstrap.PlayOpenedScene);
                if (openedScene != WorkspaceBootstrap.PlayOpenedScene) WorkspaceBootstrap.PlayOpenedScene = openedScene;
                EditorGUILayout.HelpBox(openedScene
                    ? "Play starts in your currently opened scene."
                    : "Play starts at the Hub. Your open authoring scene is restored after Play.", MessageType.Info);
            }

            Section("CHARACTER & ART");
            AssetButton("Edit Player Prefab", "Assets/_Project/Prefabs/Player.prefab", true);
            AssetButton("Character Models", "Assets/_Project/Art/Imported/Avatar");
            AssetButton("Model Library", "Assets/_Project/Resources/ModelLibrary.asset");
            AssetButton("Generated UI Art", "Assets/_Project/Resources/UI/Generated");

            Section("UI & MAP SOURCES");
            AssetButton("Inventory Layout / UXML", "Assets/_Project/Resources/UI/Inventory/Inventory.uxml", true);
            AssetButton("Inventory Styles / USS", "Assets/_Project/Resources/UI/Inventory/Inventory.uss", true);
            AssetButton("Gameplay HUD Layout", "Assets/_Project/Scripts/Game/GameHud.cs", true);
            AssetButton("Main Menu Layout", "Assets/_Project/Scripts/UI/Lobby/LobbyMenu.cs", true);
            AssetButton("Edit Pinwheel House Prefab", "Assets/_Project/Resources/Worlds/PinwheelHouse.prefab", true);
            AssetButton("Edit Garden Courtyard Prefab", "Assets/_Project/Resources/Worlds/GardenCourtyard.prefab", true);
            AssetButton("Arena Maps & Game Rules", "Assets/_Project/Data/Config");
            EditorGUILayout.LabelField("HUD and menu layouts are authored in C#. The inventory uses UI Builder assets.", EditorStyles.wordWrappedMiniLabel);

            Section("WORKSPACE TOOLS");
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("Upgrade Missing Authoring Assets", GUILayout.Height(30)))
                    SceneWorkspace.UpgradeAll();
            }
            EditorGUILayout.LabelField("Adds missing authoring assets while preserving upgraded scene edits. Legacy prototype generation is under Wreckabulary / Legacy.", EditorStyles.wordWrappedMiniLabel);
            GUILayout.Space(12);
            EditorGUILayout.EndScrollView();
        }

        static void Section(string title)
        {
            GUILayout.Space(16);
            GUILayout.Label(title, EditorStyles.boldLabel);
        }

        static void SceneButton(string label, string guid)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(path)))
                if (GUILayout.Button(label, GUILayout.Height(32))) OpenScene(path);
        }

        public static void OpenScene(string path)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || string.IsNullOrEmpty(path)) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            SceneView.RepaintAll();
        }

        static void AssetButton(string label, string path, bool open = false)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(path);
            using (new EditorGUI.DisabledScope(!asset))
            {
                if (!GUILayout.Button(label, GUILayout.Height(25))) return;
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                if (open) AssetDatabase.OpenAsset(asset);
            }
        }
    }
}
