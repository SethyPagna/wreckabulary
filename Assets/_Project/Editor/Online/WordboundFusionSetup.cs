using System;
using System.IO;
using System.Linq;
using Fusion;
using Fusion.Editor;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Online;

namespace Wreckabulary.EditorTools
{
    public static class WordboundFusionSetup
    {
        public const string SnapshotPrefabPath = "Assets/_Project/Prefabs/Wordbound/Network/WordboundSnapshotChunk.prefab";

        public static void Configure()
        {
            var config = NetworkProjectConfig.Global;
            config.AssembliesToWeave = config.AssembliesToWeave
                .Concat(new[] { "Wreckabulary.Online.Fusion", "Wreckabulary.Online.Physics", "Wreckabulary.Online.Gameplay" }).Distinct().ToArray();
            config.HostMigration.EnableAutoUpdate = false;
            config.Network.ReliableDataTransferModes = NetworkConfiguration.ReliableDataTransfers.ClientToServer;
            string path = NetworkProjectConfigUtilities.SaveGlobalConfig(config);
            Debug.Log("[Wreckabulary] Configured explicit migration checkpoints and host-only reliable admission in " + path);
        }

        public const string VoiceAppIdVariable = "WORDBOUND_PHOTON_VOICE_APP_ID";

        public static void SaveVoiceAppId()
        {
            string id = Environment.GetEnvironmentVariable(VoiceAppIdVariable)?.Trim();
            if (!Guid.TryParse(id, out var parsed) || parsed == Guid.Empty)
                throw new InvalidOperationException("Set " + VoiceAppIdVariable + " to the Photon Voice App ID first.");
            var settings = Fusion.Photon.Realtime.PhotonAppSettings.Global;
            if (settings.AppSettings.AppIdVoice == id)
            {
                Debug.Log("[Wreckabulary] The Voice App ID in Photon settings already matches " + VoiceAppIdVariable + ".");
                return;
            }
            settings.AppSettings.AppIdVoice = id;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[Wreckabulary] Saved the Voice App ID from " + VoiceAppIdVariable + " into Photon settings.");
        }

        public static void CreateSnapshotPrefab()
        {
            if (!NetworkProjectConfig.Global.AssembliesToWeave.Contains("Wreckabulary.Online.Fusion"))
                throw new InvalidOperationException("Run Configure and allow the native compiler to weave the online assembly first.");
            if (!typeof(WordboundFusionSnapshotChunk).GetCustomAttributes(false)
                .Any(a => a.GetType().Name == "NetworkBehaviourWeavedAttribute"))
                throw new InvalidOperationException("The native carrier assembly has not been weaved; run the explicit clean compilation first.");
            EnsureFolder(Path.GetDirectoryName(SnapshotPrefabPath).Replace('\\', '/'));
            if (!File.Exists(SnapshotPrefabPath))
            {
                var root = new GameObject("WordboundSnapshotChunk");
                try
                {
                    root.AddComponent<NetworkObject>();
                    root.AddComponent<WordboundFusionSnapshotChunk>();
                    if (!PrefabUtility.SaveAsPrefabAsset(root, SnapshotPrefabPath))
                        throw new InvalidOperationException("Could not create the native migration carrier prefab.");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            var instance = PrefabUtility.LoadPrefabContents(SnapshotPrefabPath);
            try
            {
                if (!instance.GetComponent<NetworkObject>() || !instance.GetComponent<WordboundFusionSnapshotChunk>() ||
                    instance.GetComponentsInChildren<Renderer>(true).Length != 0 ||
                    instance.GetComponentsInChildren<Collider>(true).Length != 0)
                    throw new InvalidOperationException("The carrier prefab has unexpected content; preserve it for inspection.");
                new NetworkObjectBakerEditTime().Bake(instance);
                if (!PrefabUtility.SaveAsPrefabAsset(instance, SnapshotPrefabPath))
                    throw new InvalidOperationException("Could not save the baked migration carrier.");
            }
            finally { PrefabUtility.UnloadPrefabContents(instance); }
            NetworkProjectConfigUtilities.RebuildPrefabTable();
            AssetDatabase.SaveAssets();
            if (!NetworkProjectConfigUtilities.TryGetPrefabId(SnapshotPrefabPath, out var id) || !id.IsValid)
                throw new InvalidOperationException("Migration carrier is not registered in the native prefab table.");
            Debug.Log("[Wreckabulary] Native migration carrier baked and registered: " + id);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
