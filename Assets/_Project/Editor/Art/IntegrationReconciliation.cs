using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Wreckabulary.EditorTools
{
    public static class IntegrationReconciliation
    {
        const string DoorGuid = "2e2629d90306729535a9ef5880d712d3";
        const string ImportedDoor = "Assets/_Project/Art/Imported/Environment/Wall_Door.fbx";
        const string ResourcesDoor = "Assets/_Project/Resources/Environment/Wall_Door.fbx";
        const string ArtScripts = "Assets/_Project/Scripts/Art/", LobbyScripts = "Assets/_Project/Scripts/UI/Lobby/";
        static readonly (string from, string to)[] LobbyRedesignScripts =
        {
            (ArtScripts + "CuteFace.cs", ArtScripts + "Cute/CuteFace.cs"),
            (ArtScripts + "CuteHair.cs", ArtScripts + "Cute/CuteHair.cs"),
            (ArtScripts + "CuteShapes.cs", ArtScripts + "Cute/CuteShapes.cs"),
            (ArtScripts + "CuteWear.cs", ArtScripts + "Cute/CuteWear.cs"),
            (LobbyScripts + "LobbyRoom.cs", LobbyScripts + "Room/LobbyRoom.cs"),
            (LobbyScripts + "LobbyWreck.cs", LobbyScripts + "Room/LobbyWreck.cs"),
            (LobbyScripts + "LobbyParcel.cs", LobbyScripts + "Parcel/LobbyParcel.cs"),
            (LobbyScripts + "LobbyParcelGrain.cs", LobbyScripts + "Parcel/LobbyParcelGrain.cs"),
            (LobbyScripts + "LobbyTileLogo.cs", LobbyScripts + "Parcel/LobbyTileLogo.cs"),
            (LobbyScripts + "LobbyTileWord.cs", LobbyScripts + "Parcel/LobbyTileWord.cs"),
        };

        public static void ClearUnusedInferenceSettings()
        {
            if (File.ReadAllText("Packages/manifest.json").Contains("\"com.unity.ai.inference\""))
                throw new InvalidOperationException("Remove the unused inference dependency before clearing its settings.");
            EditorBuildSettings.RemoveConfigObject("com.unity.dt.app-ui");
            var target = NamedBuildTarget.Standalone;
            string current = PlayerSettings.GetScriptingDefineSymbols(target);
            string cleaned = string.Join(";", current.Split(';').Where(symbol =>
                symbol != "SENTIS_ANALYTICS_ENABLED" && symbol != "APP_UI_EDITOR_ONLY"));
            if (current != cleaned) PlayerSettings.SetScriptingDefineSymbols(target, cleaned);
            AssetDatabase.SaveAssets();
            Debug.Log("Removed unused inference/App UI compiler symbols and its package-owned settings reference.");
        }

        public static void OrganizeLobbyRedesign()
        {
            int moved = 0;
            foreach (var (from, to) in LobbyRedesignScripts)
            {
                if (File.Exists(to) && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(to))) continue;
                string guid = AssetDatabase.AssetPathToGUID(from);
                if (!File.Exists(from) || string.IsNullOrEmpty(guid)) throw new InvalidOperationException($"{from} is missing.");
                EnsureFolder(Path.GetDirectoryName(to).Replace('\\', '/'));
                string error = AssetDatabase.MoveAsset(from, to);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                if (AssetDatabase.GUIDToAssetPath(guid) != to) throw new InvalidOperationException($"{to} did not keep its GUID.");
                moved++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"Organized the lobby and character redesign scripts into Art/Cute, UI/Lobby/Room and UI/Lobby/Parcel: {moved} moved.");
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(folder))))
                throw new InvalidOperationException($"Could not create {folder}.");
        }

        [MenuItem("Wreckabulary/Art/Restore Imported Door Location")]
        public static void RestoreDoorAsset()
        {
            string actual = AssetDatabase.GUIDToAssetPath(DoorGuid);
            if (actual == ImportedDoor)
            {
                Debug.Log("Wall_Door already uses its reported imported-art location.");
                return;
            }
            if (actual != ResourcesDoor)
                throw new InvalidOperationException($"Expected Wall_Door at {ResourcesDoor}, but its GUID resolves to '{actual}'.");
            if (File.Exists(ImportedDoor) || File.Exists(ImportedDoor + ".meta") || !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ImportedDoor)))
                throw new InvalidOperationException($"Cannot restore Wall_Door: {ImportedDoor} is already occupied.");
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Imported/Environment"))
                throw new InvalidOperationException("The imported environment folder is missing.");

            string error = AssetDatabase.MoveAsset(ResourcesDoor, ImportedDoor);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            AssetDatabase.SaveAssets();
            if (AssetDatabase.GUIDToAssetPath(DoorGuid) != ImportedDoor)
                throw new InvalidOperationException("Wall_Door did not retain its GUID after the move.");
            Debug.Log("Restored Wall_Door to its reported imported-art location and preserved its GUID.");
        }
    }
}
