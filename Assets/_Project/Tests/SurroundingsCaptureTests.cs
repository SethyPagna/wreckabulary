using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class SurroundingsCaptureTests
    {
        const int Width = 1920, Height = 1080, SheetColumns = 4, SheetRows = 3, SheetWidth = 640, SheetHeight = 360;
        const float Inset = 1.2f, ShotPitch = .06f, Settle = .6f;
        static readonly string[] PreferenceKeys = GraphicsOptions.Keys.Append(BackdropShader.MotionKey).ToArray();
        readonly Dictionary<string, string> savedStrings = new();
        readonly Dictionary<string, int> savedInts = new();
        string folder;
        RenderTexture full;
        Texture2D image;
        Camera lens;

        sealed class StillBinding : InputBinding
        {
            public override string Id => "maple-close-capture";
            public override bool CanLook => true;
            public override void Read(ref PlayerCommands commands) => commands = default;
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            savedStrings.Clear();
            savedInts.Clear();
            foreach (string key in GraphicsOptions.Keys) if (PlayerPrefs.HasKey(key)) savedStrings[key] = PlayerPrefs.GetString(key);
            if (PlayerPrefs.HasKey(BackdropShader.MotionKey)) savedInts[BackdropShader.MotionKey] = PlayerPrefs.GetInt(BackdropShader.MotionKey);
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (string key in PreferenceKeys)
            {
                if (savedStrings.TryGetValue(key, out string text)) PlayerPrefs.SetString(key, text);
                else if (savedInts.TryGetValue(key, out int number)) PlayerPrefs.SetInt(key, number);
                else PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
            GraphicsOptions.Load();
            GameHud.PauseOnFocusLossOverride = null;
            if (lens) lens.targetTexture = null;
            if (RenderTexture.active == full) RenderTexture.active = null;
            if (full) { full.Release(); Object.Destroy(full); }
            if (image) Object.Destroy(image);
            full = null; image = null; lens = null;
            yield return TestScenes.Reset();
        }

        [UnityTest]
        public IEnumerator MapleCloseAsPlayersSeeIt()
        {
            string root = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Owner-review captures run only when WRECK_CAPTURE_DIR is set.");
            folder = Path.Combine(root, "maple-close");
            Directory.CreateDirectory(folder);
            full = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            full.Create();
            image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            GameHud.PauseOnFocusLossOverride = false;
            PlayerPrefs.SetInt(BackdropShader.MotionKey, 1);
            GraphicsOptions.UsePreset(GraphicsOptions.DefaultPreset);

            yield return Play(false);
            var rig = CameraRig.Instance;
            yield return Shot("couch");
            rig.BeginLook();
            foreach (int yaw in new[] { 0, 90, 180, 270 })
            {
                rig.Orbit(FreeLook.Wrap(yaw) - rig.OrbitYaw, FreeLook.MinElevation - FreeLook.BaseElevation - rig.OrbitPitch);
                yield return Wait(Settle);
                Assert.AreEqual(FreeLook.Wrap(yaw), rig.OrbitYaw, .01f);
                yield return Shot($"freelook-elev30-yaw{yaw:000}");
            }
            rig.EndLook();

            var layout = Object.FindAnyObjectByType<RoomBuilder>().Layout;
            var player = Spawn();
            foreach (var (edge, at, outward) in Edges(layout))
            {
                yield return Stand(player, at, outward);
                yield return Shot($"third-{edge}-outward");
                yield return Stand(player, at, outward + Mathf.PI * .5f);
                yield return Shot($"third-{edge}-along");
            }

            var (_, east, eastward) = Edges(layout).First(e => e.edge == "east");
            yield return Stand(player, east, eastward);
            yield return MotionSheet("motion-east-street-sheet");

            PlayerPrefs.SetInt(BackdropShader.MotionKey, 0);
            yield return Wait(Settle);
            yield return Shot("reduced-motion-third-east-outward");
            Object.Destroy(player.gameObject);
            yield return Wait(1.2f);
            yield return Shot("reduced-motion-couch");
            PlayerPrefs.SetInt(BackdropShader.MotionKey, 1);

            GraphicsOptions.UsePreset("Low");
            yield return Play(true);
            yield return Shot("low-couch");
            player = Spawn();
            yield return Stand(player, east, eastward);
            yield return Shot("low-third-east-outward");
            Debug.Log("[Wreckabulary] Maple Close captures: " + folder);
        }

        IEnumerator Play(bool low)
        {
            if (lens) lens.targetTexture = null;
            yield return TestScenes.Reset();
            Session.LoadMode("Dibs", "pinwheel");
            yield return TestScenes.WaitForActive(Session.DibsScene);
            Time.timeScale = 1f;
            var backdrop = MapSurroundings.Current;
            Assert.IsNotNull(backdrop, "Maple Close attaches in a Pinwheel match");
            Assert.AreEqual(low, backdrop.LowTier, low ? "the Low preset loads the Low tier" : "the default preset loads the High tier");
            Assert.AreEqual("MapleMorning", EnvironmentLighting.Active.Profile.name);
            Assert.IsNotNull(CameraRig.Instance);
            lens = CameraRig.Instance.ViewCamera;
            lens.targetTexture = full;
            yield return Wait(1.5f);
        }

        static IEnumerable<(string edge, Vector3 at, float outward)> Edges(HouseLayout layout)
        {
            float minX = layout.Rooms.Min(r => r.MinX), maxX = layout.Rooms.Max(r => r.MaxX);
            float minZ = layout.Rooms.Min(r => r.MinZ), maxZ = layout.Rooms.Max(r => r.MaxZ);
            float cx = (minX + maxX) * .5f, cz = (minZ + maxZ) * .5f;
            Vector3 Ground(float x, float z) => new(x, layout.Room(layout.RoomAt(x, z))?.FloorY ?? 0f, z);
            yield return ("east", Ground(maxX - Inset, cz), Mathf.PI * .5f);
            yield return ("west", Ground(minX + Inset, cz), -Mathf.PI * .5f);
            yield return ("north", Ground(cx, maxZ - Inset), 0f);
            yield return ("south", Ground(cx, minZ + Inset), Mathf.PI);
        }

        static PlayerController Spawn()
        {
            var player = Object.Instantiate(GameAssets.I.playerPrefab, Vector3.zero, Quaternion.identity);
            player.Setup(3, new StillBinding());
            player.Inventory.Collects = false;
            player.Body.useGravity = false;
            player.Body.constraints = RigidbodyConstraints.FreezeAll;
            return player;
        }

        static IEnumerator Stand(PlayerController player, Vector3 at, float yaw)
        {
            player.Respawn(at + Vector3.up * .02f);
            player.Body.constraints = RigidbodyConstraints.FreezeAll;
            yield return null;
            CameraRig.Instance.Follow(player);
            yield return null;
            player.LookYaw = Mathf.Repeat(yaw + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
            player.LookPitch = ShotPitch;
            yield return Wait(Settle);
            Assert.AreSame(player, CameraRig.Instance.Target, "third person follows the capture player");
        }

        static IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

        IEnumerator Shot(string name)
        {
            yield return null;
            yield return null;
            var previous = RenderTexture.active;
            RenderTexture.active = full;
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            image.Apply(false);
            RenderTexture.active = previous == full ? null : previous;
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
        }

        IEnumerator MotionSheet(string name)
        {
            var camera = lens;
            var target = new RenderTexture(SheetWidth, SheetHeight, 24, RenderTextureFormat.ARGB32);
            target.Create();
            var sheet = new Texture2D(SheetWidth * SheetColumns, SheetHeight * SheetRows, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                camera.targetTexture = target;
                yield return null;
                yield return null;
                for (int frame = 0; frame < SheetColumns * SheetRows; frame++)
                {
                    RenderTexture.active = target;
                    int column = frame % SheetColumns, row = SheetRows - 1 - frame / SheetColumns;
                    sheet.ReadPixels(new Rect(0, 0, SheetWidth, SheetHeight), column * SheetWidth, row * SheetHeight, false);
                    yield return Wait(.5f);
                }
                sheet.Apply(false);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), sheet.EncodeToPNG());
            }
            finally
            {
                if (camera) camera.targetTexture = full;
                RenderTexture.active = previous == target ? null : previous;
                Object.Destroy(sheet);
                target.Release();
                Object.Destroy(target);
            }
        }
    }
}
