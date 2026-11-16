using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Online.Editor;

namespace Wreckabulary.Tests
{
    public sealed class WordboundSoloPreviewPoseTests
    {
        static readonly string[] Maps = { WordboundSoloEntry.LostAddressMap, WordboundSoloEntry.ParcelHallMap, WordboundSoloEntry.ParcelServiceMap };

        [Test]
        public void EverySavedPreviewPoseNamesAModuleItsCourseStillHas()
        {
            var saved = AssetDatabase.LoadAssetAtPath<WordboundSoloPreviewSettings>(WordboundSoloPreviewSettings.Path);
            Assert.IsTrue(saved, "The Solo preview settings asset is missing.");
            saved.Validate();
            foreach (string map in Maps)
                CollectionAssert.Contains(WordboundSoloPreviewCapture.InstalledModules(saved, map), saved.Pose(map).Module,
                    map + " previews a module its course no longer has. Exec WordboundSoloPreviewCapture.UseParcelDepotFraming for the Depot.");
        }

        [Test]
        public void TheDefaultDepotPoseNamesARedressedDepotModule()
        {
            var settings = ScriptableObject.CreateInstance<WordboundSoloPreviewSettings>();
            try
            {
                var modules = WordboundSoloPreviewCapture.InstalledModules(settings, WordboundSoloEntry.ParcelServiceMap);
                CollectionAssert.IsSupersetOf(modules, new[] { "Counter", "Dock", "Hall", "Lockers", "Mezzanine", "Garage" });
                CollectionAssert.DoesNotContain(modules, "Service");
                CollectionAssert.Contains(modules, settings.ParcelService.Module);
                Assert.AreEqual(WordboundSoloPreviewSettings.DepotPose().Module, settings.ParcelService.Module);
            }
            finally { Object.DestroyImmediate(settings); }
        }

        [Test]
        public void OnlyAPoseOnAMissingModuleCountsAsStale()
        {
            var settings = ScriptableObject.CreateInstance<WordboundSoloPreviewSettings>();
            try
            {
                settings.ParcelService.Module = "Service";
                Assert.IsTrue(WordboundSoloPreviewCapture.Stale(settings, WordboundSoloEntry.ParcelServiceMap));
                foreach (string module in new[] { "Counter", "Hall", "Garage" })
                {
                    settings.ParcelService.Module = module;
                    Assert.IsFalse(WordboundSoloPreviewCapture.Stale(settings, WordboundSoloEntry.ParcelServiceMap),
                        module + " is an edited pose on a real module and must be kept.");
                }
            }
            finally { Object.DestroyImmediate(settings); }
        }
    }
}
