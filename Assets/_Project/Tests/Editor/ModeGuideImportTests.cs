using System;
using System.IO;
using NUnit.Framework;
using Wreckabulary.EditorTools;
using Wreckabulary.Rules;

namespace Wreckabulary.EditorTests
{
    public sealed class ModeGuideImportTests
    {
        string root;

        [SetUp]
        public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "wv-mode-guides-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        static byte[] Png(int width, int height)
        {
            var bytes = new byte[33];
            new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G', 13, 10, 26, 10, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(bytes, 0);
            for (int i = 0; i < 4; i++)
            {
                bytes[16 + i] = (byte)(width >> (24 - 8 * i));
                bytes[20 + i] = (byte)(height >> (24 - 8 * i));
            }
            return bytes;
        }

        string Run(string name, params string[] images)
        {
            string dir = Path.Combine(root, name);
            Directory.CreateDirectory(dir);
            foreach (string image in images) File.WriteAllBytes(Path.Combine(dir, image + ".png"), Png(1280, 720));
            return dir;
        }

        [Test]
        public void TheNewestModeGuidesRunWithPicturesWins()
        {
            Run("20261114T100000Z-mode_guides", "Dibs-1");
            string wanted = Run("20261114T110000Z-mode_guides", "Dibs-2", "War-1");
            Run("20261114T120000Z-mode_guides");
            Run("20261114T130000Z-house_match", "Dibs-3");
            Assert.AreEqual(wanted, ModeGuideImport.NewestRun(root));
            Assert.IsNull(ModeGuideImport.NewestRun(Path.Combine(root, "missing")));
        }

        [Test]
        public void OnlyKnownGuidePicturesAreWanted()
        {
            string run = Run("20261114T100000Z-mode_guides", "Dibs-1", "Dibs-9", "house_match_start", "TimeRun-3");
            CollectionAssert.AreEqual(new[] { "Dibs-1", "TimeRun-3" }, ModeGuideImport.Wanted(run));
            foreach (string id in ModeGuideImport.Wanted(run)) CollectionAssert.Contains(ModeGuideBook.Images, id);
        }

        [Test]
        public void PictureSizeIsReadFromThePngHeader()
        {
            Assert.AreEqual((1280, 720), ModeGuideImport.PngSize(Png(1280, 720)));
            Assert.AreEqual((1920, 1080), ModeGuideImport.PngSize(Png(1920, 1080)));
            Assert.AreEqual((0, 0), ModeGuideImport.PngSize(new byte[] { 1, 2, 3 }));
        }
    }
}
