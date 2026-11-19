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
    public sealed class HouseDetailCaptureTests
    {
        const int Width = 1920, Height = 1080;
        const float Settle = .7f, Inset = 1.2f, ShotPitch = .06f, Spread = 1.1f;
        static readonly string[] Maps = { "pinwheel", "courtyard", "flat", "terrace", "walkup" };
        readonly List<string> notes = new();
        string folder;
        RenderTexture full;
        Texture2D image;
        Camera lens;
        int shot;

        sealed class StillBinding : InputBinding
        {
            static int next;
            readonly string id = "house-detail-capture-" + ++next;
            public override string Id => id;
            public override bool CanLook => true;
            public override void Read(ref PlayerCommands commands) => commands = default;
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            notes.Clear();
            shot = 0;
            yield return TestScenes.Reset();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameHud.PauseOnFocusLossOverride = null;
            if (lens) lens.targetTexture = null;
            if (RenderTexture.active == full) RenderTexture.active = null;
            if (full) { full.Release(); Object.Destroy(full); }
            if (image) Object.Destroy(image);
            full = null; image = null; lens = null;
            yield return TestScenes.Reset();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator HouseRoomsAndEdgesAtTenEighty([ValueSource(nameof(Maps))] string map)
        {
            string root = Environment.GetEnvironmentVariable("WRECK_CAPTURE_DIR");
            if (string.IsNullOrEmpty(root)) Assert.Ignore("Owner-review captures run only when WRECK_CAPTURE_DIR is set.");
            folder = Path.Combine(root, "house-detail");
            Directory.CreateDirectory(folder);
            full = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            full.Create();
            image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            GameHud.PauseOnFocusLossOverride = false;
            yield return House(map);
            File.WriteAllLines(Path.Combine(folder, map + ".txt"), notes);
            Debug.Log("[Wreckabulary] House detail captures for " + map + ": " + folder);
        }

        IEnumerator House(string map)
        {
            Session.LoadMode("Dibs", map);
            yield return TestScenes.WaitForActive(Session.DibsScene);
            Time.timeScale = 1f;
            var builder = Object.FindAnyObjectByType<RoomBuilder>();
            Assert.IsNotNull(builder, map);
            Assert.IsNotNull(builder.AuthoredWorld, map + " uses its saved world");
            lens = CameraRig.Instance.ViewCamera;
            lens.targetTexture = full;
            yield return Wait(1.5f);
            var layout = builder.Layout;
            var world = builder.AuthoredWorld;
            var renderers = world.GetComponentsInChildren<Renderer>(true);
            var backdrop = MapSurroundings.Current;
            notes.Add($"{map}: house renderers={renderers.Length} enabled={renderers.Count(r => r.enabled)} colliders={world.GetComponentsInChildren<Collider>(true).Length} " +
                $"details={world.GetComponentsInChildren<RoomDetail>(true).Length} backdrop={(backdrop ? backdrop.MapId + " " + backdrop.Triangles + " tris" : "none")}");
            yield return Shot(map, "overview");

            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            var lead = Join(joins);
            yield return Wait(Settle);
            var largest = layout.Rooms.Where(r => !r.Outdoor && layout.StoreyOf(r) == 0).OrderByDescending(r => r.Area).First();
            float along = largest.MaxX - largest.MinX >= largest.MaxZ - largest.MinZ ? Mathf.PI * .5f : 0f;
            yield return Stand(lead, Centre(largest), along, largest);
            yield return Shot(map, "third-" + largest.Name + "-along");
            yield return Stand(lead, Centre(largest), along + Mathf.PI * .5f, largest);
            yield return Shot(map, "third-" + largest.Name + "-across");
            foreach (var (edge, at, outward) in Edges(layout))
            {
                yield return Stand(lead, at, outward, null);
                yield return Shot(map, "third-" + edge + "-outward");
            }

            var buddy = Join(joins);
            yield return Wait(Settle);
            var floors = layout.StoreyFloors();
            foreach (var room in layout.Rooms.OrderBy(r => layout.StoreyOf(r)).ThenBy(r => r.Name, StringComparer.Ordinal))
            {
                var centre = Centre(room);
                Put(lead, centre + Vector3.left * Spread, room);
                Put(buddy, centre + Vector3.right * Spread, room);
                yield return Wait(Settle);
                Assert.IsFalse(CameraRig.Instance.IsThirdPerson, map + " two seats share the couch camera");
                string storey = floors.Count > 1 ? "s" + layout.StoreyOf(room) + "-" : "";
                yield return Shot(map, "couch-" + storey + room.Name);
            }
        }

        static PlayerController Join(PlayerJoinManager joins)
        {
            var player = joins.Join(new StillBinding());
            player.Inventory.Collects = false;
            player.Frozen = true;
            return player;
        }

        static Vector3 Centre(RoomBox room) => new((room.MinX + room.MaxX) * .5f, room.FloorY + .08f, (room.MinZ + room.MaxZ) * .5f);

        static void Put(PlayerController player, Vector3 at, RoomBox room)
        {
            Physics.SyncTransforms();
            var spot = room == null ? at : ShoulderView.RoomySpot(at, p => p.x > room.MinX + .6f && p.x < room.MaxX - .6f &&
                p.z > room.MinZ + .6f && p.z < room.MaxZ - .6f);
            player.Respawn(spot);
            player.Frozen = true;
            Physics.SyncTransforms();
        }

        static IEnumerator Stand(PlayerController player, Vector3 at, float yaw, RoomBox room)
        {
            Put(player, at, room);
            yield return null;
            player.LookYaw = Mathf.Repeat(yaw + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
            player.LookPitch = ShotPitch;
            yield return Wait(.7f);
            Assert.AreSame(player, CameraRig.Instance.Target, "third person follows the capture player");
        }

        static IEnumerable<(string edge, Vector3 at, float outward)> Edges(HouseLayout layout)
        {
            float minX = layout.Rooms.Min(r => r.MinX), maxX = layout.Rooms.Max(r => r.MaxX);
            float minZ = layout.Rooms.Min(r => r.MinZ), maxZ = layout.Rooms.Max(r => r.MaxZ);
            float cx = (minX + maxX) * .5f, cz = (minZ + maxZ) * .5f;
            Vector3 Ground(float x, float z) => new(x, (layout.Room(layout.RoomAt(x, z))?.FloorY ?? 0f) + .08f, z);
            yield return ("east", Ground(maxX - Inset, cz), Mathf.PI * .5f);
            yield return ("west", Ground(minX + Inset, cz), -Mathf.PI * .5f);
            yield return ("north", Ground(cx, maxZ - Inset), 0f);
            yield return ("south", Ground(cx, minZ + Inset), Mathf.PI);
        }

        static IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

        IEnumerator Shot(string map, string view)
        {
            yield return null;
            var cutaway = lens.GetComponent<CameraCutaway>();
            if (cutaway) cutaway.RefreshVisibility();
            yield return null;
            yield return null;
            var previous = RenderTexture.active;
            RenderTexture.active = full;
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            image.Apply(false);
            RenderTexture.active = previous == full ? null : previous;
            string file = $"{map}-{++shot:000}-{view}.png";
            File.WriteAllBytes(Path.Combine(folder, file), image.EncodeToPNG());
            notes.Add($"  {file} ortho={lens.orthographic} size={(lens.orthographic ? lens.orthographicSize : lens.fieldOfView):0.0}");
        }
    }
}
