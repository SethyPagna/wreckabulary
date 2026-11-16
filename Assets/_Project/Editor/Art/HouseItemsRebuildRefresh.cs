using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTools
{
    public static class HouseItemsRebuildRefresh
    {
        [MenuItem("Wreckabulary/Art/Refresh Rebuilt House Items")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Refresh House items outside Play Mode.");
            var rebuilt = Json.Parse(File.ReadAllText(HouseItemsSetup.SourceRoot + "/manifest.json"), "manifest.json")["entries"].Items
                .Where(entry => entry.Has("rebuilt")).Select(entry => entry["word"].String()).ToArray();
            foreach (string word in rebuilt)
            {
                AssetDatabase.ImportAsset(HouseItemsSetup.ArtRoot + "/Models/" + word + ".fbx", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                string icon = HouseItemsSetup.IconPath(word);
                if (File.Exists(icon) && !AssetDatabase.DeleteAsset(icon)) throw new IOException("Could not delete the old icon " + icon);
            }
            HouseItemsSetup.CreateOrVerify();
            Debug.Log("HOUSE_ITEMS_REFRESHED " + string.Join(", ", rebuilt));
        }
    }
}
