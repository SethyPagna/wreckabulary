using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTools
{
    public static class ModeGuideImport
    {
        public const string Folder = "Assets/_Project/Resources/" + ModeGuides.Folder;
        public const string Scenario = "mode_guides";
        public const int Width = 1280, Height = 720;

        public static string RunsRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "qa", "runs"));

        [MenuItem("Wreckabulary/Art/Import mode guide snapshots")]
        public static void Run()
        {
            string run = NewestRun(RunsRoot);
            if (run == null) throw new InvalidOperationException("No " + Scenario + " QA run with guide pictures under " + RunsRoot + ". Run the " + Scenario + " smoke first.");
            From(run);
        }

        public static void From(string runDir)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Import mode guide snapshots in Edit Mode.");
            if (!Directory.Exists(runDir)) throw new DirectoryNotFoundException(runDir);
            EnsureFolder();
            var copied = new List<string>();
            var skipped = new List<string>();
            var missing = ModeGuideBook.Images.Except(Wanted(runDir)).ToList();
            foreach (string id in Wanted(runDir))
            {
                string source = Path.Combine(runDir, id + ".png");
                var (width, height) = PngSize(File.ReadAllBytes(source));
                if (width != Width || height != Height) { skipped.Add(id + " is " + width + "x" + height); continue; }
                string path = Folder + "/" + id + ".png";
                File.Copy(source, Full(path), true);
                copied.Add(path);
            }
            foreach (string path in copied)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter ?? throw new InvalidOperationException("No texture importer for " + path);
                if (MapSnapshotImporter.Configure(importer)) importer.SaveAndReimport();
                if (!AssetDatabase.LoadAssetAtPath<Sprite>(path)) throw new InvalidOperationException("The guide picture did not import as a Sprite: " + path);
            }
            foreach (string reason in skipped) Debug.LogWarning("[Wreckabulary] Mode guide skipped " + reason);
            string summary = "[Wreckabulary] Mode guides " + (copied.Count > 0 ? "DONE" : "FAIL") + ": copied=" + copied.Count + " skipped=" + skipped.Count +
                " missing=" + missing.Count + (missing.Count > 0 ? " (" + string.Join(", ", missing) + ")" : "") + " from=" + runDir + " folder=" + Folder;
            if (copied.Count == 0) throw new InvalidOperationException(summary);
            if (missing.Count > 0 || skipped.Count > 0) Debug.LogWarning(summary); else Debug.Log(summary);
        }

        public static string NewestRun(string runsRoot)
        {
            if (string.IsNullOrEmpty(runsRoot) || !Directory.Exists(runsRoot)) return null;
            return Directory.GetDirectories(runsRoot)
                .Where(dir => Path.GetFileName(dir).EndsWith("-" + Scenario, StringComparison.Ordinal))
                .OrderByDescending(dir => Path.GetFileName(dir), StringComparer.Ordinal)
                .FirstOrDefault(dir => Wanted(dir).Count > 0);
        }

        public static List<string> Wanted(string runDir) =>
            ModeGuideBook.Images.Where(id => File.Exists(Path.Combine(runDir, id + ".png"))).ToList();

        public static (int width, int height) PngSize(byte[] png)
        {
            if (png == null || png.Length < 24 || png[0] != 0x89 || png[1] != (byte)'P' || png[2] != (byte)'N' || png[3] != (byte)'G') return (0, 0);
            return (BigEndian(png, 16), BigEndian(png, 20));
        }

        static int BigEndian(byte[] bytes, int at) => bytes[at] << 24 | bytes[at + 1] << 16 | bytes[at + 2] << 8 | bytes[at + 3];

        static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(Folder)) return;
            int split = Folder.LastIndexOf('/');
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(Folder.Substring(0, split), Folder.Substring(split + 1))))
                throw new IOException("Could not create " + Folder);
        }

        static string Full(string path) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
    }

    public sealed class ModeGuidePictureImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (assetPath.StartsWith(ModeGuideImport.Folder + "/", StringComparison.Ordinal)) MapSnapshotImporter.Configure((TextureImporter)assetImporter);
        }
    }
}
