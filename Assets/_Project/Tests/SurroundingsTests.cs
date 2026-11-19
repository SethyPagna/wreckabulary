using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Wreckabulary.Art;
using Wreckabulary.Rules;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class SurroundingsTests
    {
        static readonly string[] OtherHouses = { "courtyard", "flat", "terrace", "walkup" };
        static readonly PropertyInfo DisabledSwitch = typeof(MapSurroundings).GetProperty(nameof(MapSurroundings.Disabled));
        bool startupDisabled;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            startupDisabled = MapSurroundings.Disabled;
            SetDisabled(false);
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            SetDisabled(startupDisabled);
            yield return TestScenes.Reset();
        }

        static void SetDisabled(bool value) => DisabledSwitch.GetSetMethod(true).Invoke(null, new object[] { value });

        static IEnumerator Play(string map)
        {
            Session.LoadMode("Dibs", map);
            yield return TestScenes.WaitForActive(Session.DibsScene);
            yield return null;
        }

        static RoomBuilder Build(string map)
        {
            Match.ModeOverride = "Dibs";
            Session.SelectMap(map);
            var room = new GameObject("Surroundings test house").AddComponent<RoomBuilder>();
            foreach (var prop in room.Originals) prop.gameObject.SetActive(false);
            return room;
        }

        static Transform House(RoomBuilder builder) => builder.AuthoredWorld ? builder.AuthoredWorld.GeometryRoot : null;

        static Transform Ground(RoomBuilder builder) => House(builder).Find(RoomBuilder.StoreyName(0));

        static Renderer Lawn(RoomBuilder builder) =>
            Ground(builder).GetComponentsInChildren<Renderer>(true).Single(r => r.name == MapSurroundings.LawnName);

        static bool Shown(GameObject plant) => plant.activeInHierarchy && plant.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled);

        static List<GameObject> PlantRoots(RoomBuilder builder) =>
            Ground(builder).GetComponentsInChildren<Transform>(true)
                .Where(t => t.name == MapSurroundings.PlantKey && t.parent.name != MapSurroundings.PlantKey)
                .Select(t => t.gameObject).ToList();

        static List<GameObject> RingPlants(RoomBuilder builder)
        {
            var house = House(builder);
            var layout = builder.Layout;
            float minX = layout.Rooms.Min(r => r.MinX), maxX = layout.Rooms.Max(r => r.MaxX);
            float minZ = layout.Rooms.Min(r => r.MinZ), maxZ = layout.Rooms.Max(r => r.MaxZ);
            return PlantRoots(builder).Where(plant =>
            {
                var at = house.InverseTransformPoint(plant.transform.position);
                return at.x < minX || at.x > maxX || at.z < minZ || at.z > maxZ;
            }).ToList();
        }

        [UnityTest]
        public IEnumerator PinwheelLoadsWithItsBackdrop()
        {
            yield return Play("pinwheel");
            var builder = Object.FindAnyObjectByType<RoomBuilder>();
            var backdrop = MapSurroundings.Current;
            Assert.IsNotNull(backdrop, "Pinwheel's Maple Close backdrop");
            Assert.AreEqual(MapSurroundings.RootName, backdrop.name);
            Assert.AreSame(builder.transform, backdrop.transform.parent);
            Assert.AreEqual("pinwheel", backdrop.MapId);
            Assert.AreEqual(1, Object.FindObjectsByType<MapSurroundings>(FindObjectsSortMode.None).Length, "one backdrop per match");
            Assert.IsFalse(Lawn(builder).enabled, "the backdrop's ground replaces the house lawn");
            var ring = RingPlants(builder);
            Assert.Greater(ring.Count, 0);
            Assert.IsTrue(ring.All(plant => !Shown(plant)), "the potted ring gives way to the street");
            Assert.IsTrue(ring.All(plant => plant.activeSelf), "only the ring's renderers switch off; its objects stay as they were");
            CollectionAssert.AreEquivalent(ring, backdrop.HiddenPlants);
            var garden = PlantRoots(builder).Except(ring).ToList();
            Assert.IsTrue(garden.All(Shown), "plants inside the house stay");

            var expected = MapSurroundings.PlayBox(builder.Layout);
            AssertVector(expected.min, backdrop.PlayVolume.min, "play volume min");
            AssertVector(expected.max, backdrop.PlayVolume.max, "play volume max");
            AssertVector(expected.min, (Vector3)Shader.GetGlobalVector("_BackdropPlayMin"), "shader play min");
            AssertVector(expected.max, (Vector3)Shader.GetGlobalVector("_BackdropPlayMax"), "shader play max");

            Assert.IsNotNull(backdrop.GetComponent<BackdropLife>());
            var environment = EnvironmentLighting.Active;
            Assert.IsNotNull(environment, "the match has an environment");
            Assert.AreEqual(backdrop.LightId, environment.Profile.name, "Maple Close's light");
            Assert.AreEqual("MapleMorning", backdrop.LightId);
            Assert.AreEqual("maple_close", backdrop.AmbienceId);

            Assert.IsEmpty(backdrop.GetComponentsInChildren<Collider>(true));
            Assert.IsEmpty(backdrop.GetComponentsInChildren<Rigidbody>(true));
            Assert.IsEmpty(backdrop.GetComponentsInChildren<Light>(true));
            Assert.IsEmpty(backdrop.GetComponentsInChildren<Smashable>(true));
            var renderers = backdrop.GetComponentsInChildren<Renderer>(true);
            Assert.Greater(renderers.Length, 0);
            Assert.IsTrue(renderers.All(MapSurroundings.IsBackdrop));
            Assert.IsFalse(House(builder).GetComponentsInChildren<Renderer>(true).Any(MapSurroundings.IsBackdrop));
        }

        [UnityTest]
        public IEnumerator DestroyingTheBackdropRestoresTheHouseAndClearsTheVolume()
        {
            var builder = Build("pinwheel");
            yield return null;
            var backdrop = MapSurroundings.Current;
            Assert.IsNotNull(backdrop);
            var renderers = backdrop.GetComponentsInChildren<Renderer>(true);
            Object.Destroy(backdrop.gameObject);
            yield return null;
            Assert.IsNull(MapSurroundings.Current);
            Assert.IsFalse(renderers.Any(MapSurroundings.IsBackdrop), "unregistered on destroy");
            Assert.IsTrue(Lawn(builder).enabled);
            Assert.IsTrue(RingPlants(builder).All(Shown));
        }

        [UnityTest]
        public IEnumerator TheOtherHousesHaveTheirOwnNeighbourhoods()
        {
            foreach (string map in OtherHouses)
            {
                yield return TestScenes.Reset();
                Assert.IsNotNull(Resources.Load<GameObject>(MapSurroundings.PrefabPath(map, false)), map + " has a High backdrop");
                Assert.IsNotNull(Resources.Load<GameObject>(MapSurroundings.PrefabPath(map, true)), map + " has a Low backdrop");
                yield return Play(map);
                var builder = Object.FindAnyObjectByType<RoomBuilder>();
                var backdrop = MapSurroundings.Current;
                Assert.IsNotNull(backdrop, map);
                Assert.AreEqual(map, backdrop.MapId);
                Assert.IsFalse(Lawn(builder).enabled, map + " lawn gives way to its neighbourhood");
                var ring = RingPlants(builder);
                Assert.Greater(ring.Count, 0, map);
                Assert.IsTrue(ring.All(plant => !Shown(plant)), map + " potted ring gives way to the street");
                CollectionAssert.AreEquivalent(ring, backdrop.HiddenPlants, map);
            }
        }

        [UnityTest]
        public IEnumerator TutorialAndHubGetNoBackdrop()
        {
            foreach (string scene in new[] { Session.TutorialScene, Session.HubScene })
            {
                yield return TestScenes.Reset();
                Session.SelectMap("pinwheel");
                yield return TestScenes.Load(scene);
                Assert.IsNull(Object.FindAnyObjectByType<MapSurroundings>(), scene);
                Assert.IsFalse(MapSurroundings.SceneShowsBackdrop(UnityEngine.SceneManagement.SceneManager.GetActiveScene()), scene);
                var stray = new GameObject("Stray room builder").AddComponent<RoomBuilder>();
                yield return null;
                Assert.IsNull(Object.FindAnyObjectByType<MapSurroundings>(), scene + " never shows Maple Close, even with a room builder");
                Assert.IsTrue(Lawn(stray).enabled, scene);
            }
        }

        [UnityTest]
        public IEnumerator TheNoBackdropSwitchLeavesTheHouseAsItWas()
        {
            Assert.AreEqual(Environment.GetEnvironmentVariable(MapSurroundings.DisableVariable) == "1", startupDisabled, "read once from the environment");
            SetDisabled(true);
            yield return Play("pinwheel");
            var builder = Object.FindAnyObjectByType<RoomBuilder>();
            Assert.IsNull(MapSurroundings.Current);
            Assert.IsNull(Object.FindAnyObjectByType<MapSurroundings>());
            Assert.IsNull(MapSurroundings.Attach(builder.transform, builder.Layout, "pinwheel", House(builder)));
            Assert.IsTrue(Lawn(builder).enabled);
            Assert.IsTrue(RingPlants(builder).All(Shown));
            Assert.AreNotEqual("MapleMorning", EnvironmentLighting.Active.Profile.name);
        }

        static (Vector3 position, Quaternion rotation, float size) Framing(Camera camera) =>
            (camera.transform.position, camera.transform.rotation, camera.orthographicSize);

        static Camera CouchCamera(RenderTexture target = null)
        {
            var camera = new GameObject("Couch camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            if (target) camera.targetTexture = target;
            camera.gameObject.AddComponent<CameraRig>();
            return camera;
        }

        [UnityTest]
        public IEnumerator PartyCameraFramingIsIdenticalWithAndWithoutTheBackdrop()
        {
            var framings = new List<(Vector3 position, Quaternion rotation, float size)>();
            foreach (bool disabled in new[] { false, true })
            {
                yield return TestScenes.Reset();
                SetDisabled(disabled);
                var camera = CouchCamera();
                Build("pinwheel");
                Assert.AreEqual(!disabled, (bool)MapSurroundings.Current, disabled ? "switched off" : "switched on");
                for (int frame = 0; frame < 5; frame++) yield return null;
                framings.Add(Framing(camera));
            }
            AssertVector(framings[1].position, framings[0].position, "camera position");
            Assert.Less(Quaternion.Angle(framings[1].rotation, framings[0].rotation), .001f, "camera rotation");
            Assert.AreEqual(framings[1].size, framings[0].size, 1e-5f, "orthographic size");
        }

        [UnityTest]
        public IEnumerator StoreyCutawayIgnoresBackdropRenderers()
        {
            var builder = Build("terrace");
            yield return null;
            var backdrop = MapSurroundings.Current;
            Assert.IsNotNull(backdrop, "the terrace's own neighbourhood");
            Assert.AreEqual("terrace", backdrop.MapId);
            Assert.Greater(builder.Layout.StoreyFloors().Count, 1, "the terrace has upper storeys to cut");
            var cutaway = builder.GetComponent<StoreyCutaway>();
            var lifted = backdrop.GetComponentsInChildren<MeshRenderer>(true).First(r => !r.GetComponent<BackdropMover>());
            lifted.transform.position += Vector3.up * 40f;
            var control = GameObject.CreatePrimitive(PrimitiveType.Cube);
            control.GetComponent<Collider>().enabled = false;
            control.transform.position = lifted.bounds.center;
            yield return new WaitForSecondsRealtime(.3f);
            cutaway.Refresh();
            Assert.IsFalse(cutaway.Draws(control.GetComponent<Renderer>()), "a loose renderer above the top storey is cut");
            Assert.IsTrue(cutaway.Draws(lifted), "the backdrop is never part of the cutaway");
            foreach (var renderer in backdrop.GetComponentsInChildren<Renderer>(true)) Assert.IsTrue(cutaway.Draws(renderer), renderer.name);
        }

        [UnityTest]
        public IEnumerator BackdropComponentsDoNotAllocatePerFrame()
        {
            Build("pinwheel");
            yield return null;
            var backdrop = MapSurroundings.Current;
            Assert.IsNotNull(backdrop);
            var calls = new List<Action>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (var behaviour in backdrop.GetComponentsInChildren<MonoBehaviour>(true))
                foreach (string name in new[] { "Update", "LateUpdate", "FixedUpdate" })
                {
                    var method = behaviour.GetType().GetMethod(name, flags, null, Type.EmptyTypes, null);
                    if (method != null) calls.Add((Action)Delegate.CreateDelegate(typeof(Action), behaviour, method));
                }
            Assert.Greater(calls.Count, 0, "backdrop life runs every frame");
            var frame = calls.ToArray();
            void Tick() { for (int i = 0; i < frame.Length; i++) frame[i](); }
            for (int warm = 0; warm < 3; warm++) { Tick(); yield return null; }
            Assert.That(Tick, Is.Not.AllocatingGCMemory());
        }

        [UnityTest]
        public IEnumerator TheBackdropNeverCoversPlayInTheCouchView()
        {
            const int width = 1920, height = 1080, erosion = 8, tolerance = 8;
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            var readback = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            Camera camera = null;
            try
            {
                camera = CouchCamera(target);
                GraphicsOptions.ApplyTo(camera);
                camera.GetUniversalAdditionalCameraData().dithering = false;
                var sun = new GameObject("Couch test sun").AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
                var builder = Build("pinwheel");
                var backdrop = MapSurroundings.Current;
                Assert.IsNotNull(backdrop);
                foreach (var renderer in backdrop.GetComponentsInChildren<Renderer>(true)) renderer.shadowCastingMode = ShadowCastingMode.Off;
                var house = House(builder);
                foreach (var plinth in house.GetComponentsInChildren<Renderer>(true).Where(r => r.name == "Plinth")) plinth.enabled = false;
                var furniture = builder.AuthoredWorld.FurnitureRoot;
                for (int frame = 0; frame < 5; frame++) yield return null;

                Color32[] Capture()
                {
                    RenderTexture.active = target;
                    readback.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                    readback.Apply(false);
                    return readback.GetPixels32();
                }

                yield return null;
                yield return null;
                var on = Capture();
                Save(readback, "backdrop-on");
                backdrop.gameObject.SetActive(false);
                yield return null;
                yield return null;
                var off = Capture();
                Save(readback, "backdrop-off");
                house.gameObject.SetActive(false);
                if (furniture) furniture.gameObject.SetActive(false);
                yield return null;
                yield return null;
                var empty = Capture();
                Save(readback, "house-hidden");
                house.gameObject.SetActive(true);
                if (furniture) furniture.gameObject.SetActive(true);
                backdrop.gameObject.SetActive(true);

                var play = new bool[on.Length];
                for (int i = 0; i < play.Length; i++) play[i] = Difference(off[i], empty[i]) > 12;
                play = Erode(play, width, height, erosion);
                int area = play.Count(p => p), equal = 0;
                for (int i = 0; i < play.Length; i++) if (play[i] && Difference(on[i], off[i]) <= tolerance) equal++;
                Assert.Greater(area, on.Length / 8, "the couch view shows the house");
                float share = (float)equal / area;
                Debug.Log($"[Wreckabulary] Never-cover-play: {equal}/{area} playable pixels equal ({share:P2}).");
                Assert.GreaterOrEqual(share, .995f, "no backdrop pixel covers the playable area");
            }
            finally
            {
                if (camera) camera.targetTexture = null;
                RenderTexture.active = previous == target ? null : previous;
                Object.Destroy(readback);
                target.Release();
                Object.Destroy(target);
            }
        }

        static int Difference(Color32 a, Color32 b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Max(Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b)));

        static bool[] Erode(bool[] mask, int width, int height, int radius)
        {
            var rows = new bool[mask.Length];
            for (int y = 0; y < height; y++)
            {
                int run = 0;
                for (int x = 0; x < width; x++)
                {
                    run = mask[y * width + x] ? run + 1 : 0;
                    if (run > 2 * radius) rows[y * width + x - radius] = true;
                }
            }
            var result = new bool[mask.Length];
            for (int x = 0; x < width; x++)
            {
                int run = 0;
                for (int y = 0; y < height; y++)
                {
                    run = rows[y * width + x] ? run + 1 : 0;
                    if (run > 2 * radius) result[(y - radius) * width + x] = true;
                }
            }
            return result;
        }

        static void Save(Texture2D image, string name)
        {
            string directory = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) return;
            directory = Path.Combine(directory, "surroundings-never-cover-play");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, name + ".png"), image.EncodeToPNG());
        }

        static void AssertVector(Vector3 expected, Vector3 actual, string what)
        {
            Assert.AreEqual(expected.x, actual.x, 1e-3f, what + " x");
            Assert.AreEqual(expected.y, actual.y, 1e-3f, what + " y");
            Assert.AreEqual(expected.z, actual.z, 1e-3f, what + " z");
        }
    }
}
