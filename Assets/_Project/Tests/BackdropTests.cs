using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Wreckabulary.Art;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public class BackdropTests
    {
        const int Size = 128;
        static readonly Bounds Play = new(new Vector3(0f, 1f, 0f), new Vector3(4f, 2f, 4f));
        static readonly Color Cover = Color.red, Floor = Color.green;

        readonly List<Object> owned = new();
        bool fog, asyncShaders;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            yield return TestScenes.Reset();
            fog = RenderSettings.fog;
            RenderSettings.fog = false;
#if UNITY_EDITOR
            asyncShaders = UnityEditor.EditorSettings.asyncShaderCompilation;
            UnityEditor.EditorSettings.asyncShaderCompilation = false;
#endif
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BackdropShader.ClearPlayVolume();
            RenderSettings.fog = fog;
#if UNITY_EDITOR
            UnityEditor.EditorSettings.asyncShaderCompilation = asyncShaders;
#endif
            foreach (var item in owned) if (item) Object.Destroy(item);
            owned.Clear();
            yield return TestScenes.Reset();
        }

        T Own<T>(T item) where T : Object
        {
            owned.Add(item);
            return item;
        }

        Material BackdropMaterial(Color colour)
        {
            var shader = Shader.Find(BackdropShader.ShaderName);
            Assert.IsNotNull(shader, BackdropShader.ShaderName);
            Assert.IsTrue(shader.isSupported, BackdropShader.ShaderName + " is supported");
            var material = Own(new Material(shader));
            material.SetColor("_BaseColor", colour);
            material.SetColor("_EmissionColor", colour);
            return material;
        }

        Material FloorMaterial()
        {
            var material = Own(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
            material.SetColor("_BaseColor", Floor);
            return material;
        }

        Mesh ColouredCube()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var mesh = Own(Object.Instantiate(probe.GetComponent<MeshFilter>().sharedMesh));
            Object.Destroy(probe);
            mesh.colors32 = Enumerable.Repeat(new Color32(255, 255, 255, 0), mesh.vertexCount).ToArray();
            return mesh;
        }

        GameObject Box(string name, Vector3 centre, Vector3 size, Mesh mesh, Material material)
        {
            var box = Own(new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)));
            box.transform.SetPositionAndRotation(centre, Quaternion.identity);
            box.transform.localScale = size;
            box.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = box.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return box;
        }

        Camera View(string name, RenderTexture target)
        {
            var camera = Own(new GameObject(name)).AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 200f;
            camera.targetTexture = target;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            return camera;
        }

        RenderTexture Target()
        {
            var target = Own(new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32));
            target.Create();
            return target;
        }

        Texture2D Read(RenderTexture target, string capture)
        {
            var previous = RenderTexture.active;
            var pixels = Own(new Texture2D(Size, Size, TextureFormat.RGB24, false));
            try
            {
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0, false);
                pixels.Apply();
            }
            finally { RenderTexture.active = previous; }
            string directory = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(directory))
            {
                directory = Path.Combine(directory, "backdrop");
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(Path.Combine(directory, capture + ".png"), pixels.EncodeToPNG());
            }
            return pixels;
        }

        static IEnumerator Frames(int count = 3)
        {
            for (int i = 0; i < count; i++) yield return null;
        }

        static int Pixel(float world, float half) => Mathf.Clamp(Mathf.RoundToInt((world + half) / (2f * half) * Size), 0, Size - 1);

        static bool IsCover(Color c) => c.r > .6f && c.r > c.g + .35f;

        static bool IsFloor(Color c) => c.g > .6f && c.g > c.r + .35f;

        [UnityTest]
        public IEnumerator OrthographicViewNeverCoversThePlayVolume()
        {
            var cube = ColouredCube();
            var backdrop = BackdropMaterial(Cover);
            Box("Play floor", new Vector3(0f, .05f, 0f), new Vector3(4f, .1f, 4f), cube, FloorMaterial());
            Box("Backdrop over play", new Vector3(0f, 6f, 0f), new Vector3(6f, 1f, 3f), cube, backdrop);
            Box("Backdrop beside play", new Vector3(6f, 1f, 0f), new Vector3(2f, 2f, 2f), cube, backdrop);
            var target = Target();
            var camera = View("Backdrop orthographic view", target);
            const float half = 8f;
            camera.orthographic = true;
            camera.orthographicSize = half;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 20f, 0f), Quaternion.LookRotation(Vector3.down, Vector3.forward));
            int centre = Pixel(0f, half), beside = Pixel(6f, half), overhang = Pixel(2.6f, half);

            BackdropShader.ClearPlayVolume();
            yield return Frames();
            var uncut = Read(target, "orthographic-uncut");
            Assert.IsTrue(IsCover(uncut.GetPixel(centre, centre)), "Without the cut the overhead backdrop covers play: " + uncut.GetPixel(centre, centre));

            BackdropShader.SetPlayVolume(Play);
            yield return Frames();
            var cut = Read(target, "orthographic-cut");
            Assert.IsTrue(IsFloor(cut.GetPixel(centre, centre)), "The play floor shows through: " + cut.GetPixel(centre, centre));
            for (int x = Pixel(-1.8f, half); x <= Pixel(1.8f, half); x += 4)
            for (int y = Pixel(-1.8f, half); y <= Pixel(1.8f, half); y += 4)
                Assert.IsFalse(IsCover(cut.GetPixel(x, y)), $"No backdrop pixel over play at {x},{y}: {cut.GetPixel(x, y)}");
            Assert.IsTrue(IsCover(cut.GetPixel(beside, centre)), "A backdrop beside play still renders: " + cut.GetPixel(beside, centre));
            Assert.IsTrue(IsCover(cut.GetPixel(overhang, centre)), "The overhead backdrop outside the volume's outline still renders: " + cut.GetPixel(overhang, centre));
        }

        [UnityTest]
        public IEnumerator PerspectiveCameraInsideThePlayVolumeClipsNothing()
        {
            var cube = ColouredCube();
            Box("Backdrop inside play", new Vector3(0f, 1f, 1f), new Vector3(.8f, .8f, .8f), cube, BackdropMaterial(Cover));
            var target = Target();
            var camera = View("Backdrop perspective view", target);
            camera.orthographic = false;
            camera.fieldOfView = 60f;
            camera.transform.SetPositionAndRotation(new Vector3(0f, 1f, -1.5f), Quaternion.identity);
            BackdropShader.SetPlayVolume(Play);
            Assert.IsTrue(Play.Contains(camera.transform.position));
            yield return Frames();
            var inside = Read(target, "perspective-inside");
            Assert.IsTrue(IsCover(inside.GetPixel(Size / 2, Size / 2)), "A camera inside the volume sees the backdrop whole: " + inside.GetPixel(Size / 2, Size / 2));

            camera.transform.position = new Vector3(0f, 1f, -8f);
            Assert.IsFalse(Play.Contains(camera.transform.position));
            yield return Frames();
            var outside = Read(target, "perspective-outside");
            Assert.IsFalse(IsCover(outside.GetPixel(Size / 2, Size / 2)), "From outside, backdrop inside the volume is cut: " + outside.GetPixel(Size / 2, Size / 2));
        }

        [UnityTest]
        public IEnumerator LifeMovesWhenAllowedAndHoldsStillWithoutAllocating()
        {
            bool hadKey = PlayerPrefs.HasKey(BackdropShader.MotionKey);
            int saved = PlayerPrefs.GetInt(BackdropShader.MotionKey, 1);
            try
            {
                PlayerPrefs.SetInt(BackdropShader.MotionKey, 1);
                var root = Own(new GameObject("Backdrop"));
                var mover = new GameObject("Life 0").AddComponent<BackdropMover>();
                mover.transform.SetParent(root.transform, false);
                mover.Path = new[] { Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 6f) };
                mover.Speed = 4f;
                var life = root.AddComponent<BackdropLife>();
                var method = typeof(BackdropLife).GetMethod("Step", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.IsNotNull(method, "BackdropLife.Step(float deltaTime, float now)");
                var step = (Action<float, float>)Delegate.CreateDelegate(typeof(Action<float, float>), life, method);
                var start = mover.transform.localPosition;
                Assert.AreEqual(Vector3.zero, start);

                yield return Frames(6);
                Assert.Greater(mover.transform.localPosition.x, start.x, "Movers travel on real frames when motion is allowed");

                float now = 10000f;
                for (int i = 0; i < 120; i++) step(1f / 60f, now += .1f);
                Assert.AreEqual(1f, Shader.GetGlobalFloat("_BackdropWind"), 1e-4f, "Wind is on when motion is allowed");
                var before = mover.transform.localPosition;
                long allocatedBefore = Allocated();
                for (int i = 0; i < 600; i++) step(1f / 60f, now += .1f);
                long movingAllocations = Allocated() - allocatedBefore;
                Assert.AreNotEqual(before, mover.transform.localPosition);

                PlayerPrefs.SetInt(BackdropShader.MotionKey, 0);
                step(1f / 60f, now += 1f);
                Assert.AreEqual(0f, Shader.GetGlobalFloat("_BackdropWind"), "Wind stops at once with Reduce theme motion");
                var held = mover.transform.localPosition;
                allocatedBefore = Allocated();
                for (int i = 0; i < 600; i++) step(1f / 60f, now += .1f);
                long heldAllocations = Allocated() - allocatedBefore;
                Assert.AreEqual(held, mover.transform.localPosition, "Movers hold still when motion is not allowed");
                yield return Frames(4);
                Assert.AreEqual(held, mover.transform.localPosition, "Real frames keep them still");

                if (allocatedBefore < 0) Assert.Ignore("This runtime does not count allocations.");
                Assert.AreEqual(0L, movingAllocations, "No allocations while moving");
                Assert.AreEqual(0L, heldAllocations, "No allocations while held");
            }
            finally
            {
                if (hadKey) PlayerPrefs.SetInt(BackdropShader.MotionKey, saved);
                else PlayerPrefs.DeleteKey(BackdropShader.MotionKey);
            }
        }

        [UnityTest]
        public IEnumerator MoverFollowsItsPathAndPingPongsWithoutLoop()
        {
            var mover = Own(new GameObject("Mover")).AddComponent<BackdropMover>();
            mover.Path = new[] { Vector3.zero, new Vector3(0f, 0f, 8f) };
            mover.Speed = 2f;
            mover.Loop = false;
            mover.Phase = .25f;
            var life = Own(new GameObject("Life")).AddComponent<BackdropLife>();
            mover.transform.SetParent(life.transform, false);
            life.enabled = false;
            life.enabled = true;
            Assert.AreEqual(4f, mover.transform.localPosition.z, 1e-4f, "Phase is a fraction of the out-and-back cycle");
            var method = typeof(BackdropLife).GetMethod("Step", BindingFlags.Instance | BindingFlags.NonPublic);
            var step = (Action<float, float>)Delegate.CreateDelegate(typeof(Action<float, float>), life, method);
            bool hadKey = PlayerPrefs.HasKey(BackdropShader.MotionKey);
            int saved = PlayerPrefs.GetInt(BackdropShader.MotionKey, 1);
            try
            {
                PlayerPrefs.SetInt(BackdropShader.MotionKey, 1);
                step(3f, 50000f);
                Assert.AreEqual(6f, mover.transform.localPosition.z, 1e-3f, "Back from the far end");
                Assert.Less(Vector3.Dot(mover.transform.forward, Vector3.forward), -.99f, "Faces the way it travels");
            }
            finally
            {
                if (hadKey) PlayerPrefs.SetInt(BackdropShader.MotionKey, saved);
                else PlayerPrefs.DeleteKey(BackdropShader.MotionKey);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator AmbienceStaysSilentWhenMuted()
        {
            bool wasMuted = GameFeedback.Muted;
            float volume = AudioListener.volume;
            try
            {
                GameFeedback.Muted = true;
                AudioListener.volume = 1f;
                var root = Own(new GameObject("Backdrop"));
                var ambience = BackdropAmbience.Attach(root.transform, "maple_close");
                Assert.IsNotNull(ambience, "maple_close clips load from Resources/Ambience");
                var sources = ambience.GetComponents<AudioSource>();
                Assert.IsTrue(sources.Any(s => s.loop && s.clip && BackdropAmbience.IsLoop(s.clip.name)), "One looping garden bed");
                Assert.Greater(sources.Count(s => !s.loop), 0, "One-shot voices");
                foreach (var source in sources) Assert.AreEqual(0f, source.spatialBlend, "Ambience is 2D");

                float end = Time.unscaledTime + 2.5f;
                while (Time.unscaledTime < end)
                {
                    AssertSilent(ambience, sources);
                    yield return null;
                }

                GameFeedback.Muted = false;
                end = Time.unscaledTime + 4f;
                while (Time.unscaledTime < end && ambience.Level < .4f) yield return null;
                Assert.Greater(ambience.Level, .4f, "Unmuted ambience fades in");
                Assert.Greater(sources.First(s => s.loop).volume, 0f);
                Assert.LessOrEqual(sources.First(s => s.loop).volume, BackdropAmbience.BedLevel + 1e-4f, "The bed stays low");

                GameFeedback.Muted = true;
                end = Time.unscaledTime + .6f;
                while (Time.unscaledTime < end) yield return null;
                AssertSilent(ambience, sources);
            }
            finally
            {
                GameFeedback.Muted = wasMuted;
                AudioListener.volume = volume;
            }
        }

        static void AssertSilent(BackdropAmbience ambience, AudioSource[] sources)
        {
            Assert.AreEqual(0f, ambience.Level, "Muted ambience level");
            foreach (var source in sources)
            {
                Assert.AreEqual(0f, source.volume, "Muted source volume");
                Assert.IsFalse(source.isPlaying && source.clip && !source.loop, "No one-shot plays while muted");
                Assert.IsFalse(source.loop && source.isPlaying, "The bed is paused while muted");
            }
        }

        static long Allocated()
        {
            try { return GC.GetAllocatedBytesForCurrentThread(); }
            catch (NotSupportedException) { return -1; }
        }
    }
}
