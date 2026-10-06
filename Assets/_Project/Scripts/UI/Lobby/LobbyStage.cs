using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// The 3D half of the lobby: the selected map built far from the hub room, your avatar
    /// standing in its most open room (no platform), and a camera that frames you in the
    /// middle (home) or on the left (loadout). Dragging turns the avatar.
    /// </summary>
    public sealed class LobbyStage : MonoBehaviour
    {
        /// <summary>Where the backdrop is built, well clear of the hub room at the origin.</summary>
        public static readonly Vector3 Origin = new Vector3(0f, 0f, -400f);
        const string AvatarKey = "Avatar/Avatar";
        const float Distance = 4.2f, EyeHeight = .95f, LookHeight = .5f, FieldOfView = 30f;

        public enum Focus { Centre, Left }

        Transform set, avatarRoot;
        GameObject avatar;
        SkinnedMeshRenderer[] meshes;
        PlayableGraph graph;
        Camera cam;
        CameraRig rig;
        bool rigWasEnabled, cameraTaken;
        Vector3 savedPosition; Quaternion savedRotation; bool savedOrtho; float savedFov, savedNear;
        CameraClearFlags savedClear; Color savedBackground;
        Vector3 spot, toCamera;
        float yaw, targetYaw, side, targetSide;

        public string Map { get; private set; }
        public Transform Avatar => avatarRoot;
        public float Yaw => Mathf.Repeat(targetYaw, 360f);
        /// <summary>The avatar's spot in world space, on the floor.</summary>
        public Vector3 Spot => spot;
        public Camera Camera => cam;

        public void Show(string mapId, Outfit outfit)
        {
            TakeCamera();
            SetMap(mapId);
            Dress(outfit);
        }

        public void SetMap(string mapId)
        {
            if (Map == mapId && set) return;
            var layout = GameConfig.Current.HouseFor(mapId);
            if (set) { set.gameObject.SetActive(false); TactileMaterials.Release(set.gameObject); Destroy(set.gameObject); }
            // RoomBuilder places blocks in world space, so build at the origin and then move the whole set.
            set = new GameObject("Lobby backdrop · " + layout.Name).transform;
            set.SetParent(transform, false);
            var geometry = RoomBuilder.CreateGeometry(layout, mapId, set);
            var room = OpenRoom(layout);
            // Floors above yours would hide you from the camera.
            int storey = layout.StoreyOf(room);
            foreach (Transform group in geometry)
                for (int above = storey + 1; above < layout.StoreyFloors().Count; above++)
                    if (group.name == RoomBuilder.StoreyName(above)) group.gameObject.SetActive(false);
            // The arena rug in the middle of your room lies between the camera and you and reads as a stage.
            foreach (var child in geometry.GetComponentsInChildren<Transform>(true))
            {
                var p = set.InverseTransformPoint(child.position);
                if (child.name == "Environment/Arena_Rug" && Mathf.Abs(p.y - room.FloorY) < 1f && p.x > room.MinX && p.x < room.MaxX && p.z > room.MinZ && p.z < room.MaxZ)
                { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            }
            float depth = room.MaxZ - room.MinZ;
            // Stand towards the back wall and look south across the room, so the far wall is the backdrop.
            float distance = Mathf.Min(Distance, depth * .62f);
            var local = new Vector3((room.MinX + room.MaxX) * .5f, room.FloorY, Mathf.Min(room.MaxZ - 1.6f, room.MinZ + .45f + distance + depth * .12f));
            toCamera = new Vector3(0f, 0f, -distance);
            Furnish(layout, storey, local, local + toCamera);
            set.position = Origin;
            spot = Origin + local;
            Map = mapId;
            if (avatarRoot) avatarRoot.position = spot;
            Place(true);
        }

        /// <summary>The map's lobby room if it names one, else the ground-floor room with the most space to stand back in; gardens count.</summary>
        static RoomBox OpenRoom(HouseLayout layout) =>
            (layout.LobbyRoom != null ? layout.Room(layout.LobbyRoom) : null)
            ?? layout.Rooms.Where(r => layout.StoreyOf(r) == 0)
                .OrderByDescending(r => Mathf.Min(r.MaxX - r.MinX, r.MaxZ - r.MinZ)).ThenBy(r => r.Name, System.StringComparer.Ordinal).First();

        void Furnish(HouseLayout layout, int storey, Vector3 stand, Vector3 eye)
        {
            var library = ModelLibrary.Load();
            if (!library) return;
            var decor = new GameObject("Furniture").transform;
            decor.SetParent(set, false);
            foreach (var f in layout.Furniture)
            {
                if (layout.StoreyOf(layout.Room(f.Room)) > storey) continue;
                var at = new Vector3(f.X, layout.Room(f.Room).FloorY + f.Y, f.Z);
                // Keep the view from the camera to you clear; a rug spreads wide enough to look like a stage.
                float clear = f.Word == "RUG" ? 2.2f : 1.1f;
                if (DistanceToSegment(new Vector2(at.x, at.z), new Vector2(eye.x, eye.z), new Vector2(stand.x, stand.z)) < clear) continue;
                string key = f.Word == "RUG" ? "Environment/Round_Rug" : "Items/" + f.Word.ToUpperInvariant();
                if (!library.Find(key)) continue;
                var root = new GameObject(f.Word).transform;
                root.SetParent(decor, false);
                root.SetPositionAndRotation(at, Quaternion.Euler(0f, f.Yaw, 0f));
                ModelVisual.Spawn(key, root);
            }
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (p - (a + ab * t)).magnitude;
        }

        public void Dress(Outfit outfit)
        {
            if (!avatar && !SpawnAvatar()) return;
            PlayerAppearance.Dress(meshes, outfit);
        }

        bool SpawnAvatar()
        {
            var library = ModelLibrary.Load();
            if (!library || !library.Find(AvatarKey)) return false;
            avatarRoot = new GameObject("Lobby avatar").transform;
            avatarRoot.SetParent(transform, false);
            avatarRoot.position = spot;
            avatar = ModelVisual.Spawn(AvatarKey, avatarRoot);
            if (!avatar) return false;
            meshes = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var animator = avatar.GetComponentInChildren<Animator>(true);
            var idle = library.FindClip(AvatarKey, "Idle");
            if (animator && idle)
            {
                animator.applyRootMotion = false;
                graph = PlayableGraph.Create("Lobby avatar");
                graph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
                var output = AnimationPlayableOutput.Create(graph, "Lobby avatar", animator);
                output.SetSourcePlayable(AnimationClipPlayable.Create(graph, idle));
                graph.Play();
            }
            Place(true);
            return true;
        }

        /// <summary>Turns the avatar by a drag, in degrees. Kept unwrapped, so a fast flick of more
        /// than half a turn in one frame still spins the way it was dragged.</summary>
        public void Turn(float degrees) => targetYaw += degrees;

        public void Frame(Focus focus) => targetSide = focus == Focus.Left ? 1f : 0f;

        /// <summary>Takes the main camera for the lobby view; <see cref="Release"/> gives it back.</summary>
        public void TakeCamera()
        {
            if (cameraTaken) return;
            cam = Camera.main;
            if (!cam) return;
            rig = cam.GetComponent<CameraRig>();
            rigWasEnabled = rig && rig.enabled;
            if (rig) rig.enabled = false;
            var t = cam.transform;
            savedPosition = t.position; savedRotation = t.rotation; savedOrtho = cam.orthographic;
            savedFov = cam.fieldOfView; savedNear = cam.nearClipPlane; savedClear = cam.clearFlags; savedBackground = cam.backgroundColor;
            cam.orthographic = false;
            cam.fieldOfView = FieldOfView;
            cam.nearClipPlane = .1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            // A warm evening wash above the walls instead of the hub's teal.
            cam.backgroundColor = new Color(.93f, .8f, .64f);
            GraphicsOptions.ApplyTo(cam);
            cameraTaken = true;
        }

        /// <summary>Gives the camera back to the hub (for exploring or leaving the lobby).</summary>
        public void Release()
        {
            if (!cameraTaken) return;
            if (cam)
            {
                cam.transform.SetPositionAndRotation(savedPosition, savedRotation);
                cam.orthographic = savedOrtho; cam.fieldOfView = savedFov; cam.nearClipPlane = savedNear;
                cam.clearFlags = savedClear; cam.backgroundColor = savedBackground;
            }
            if (rig) rig.enabled = rigWasEnabled;
            cameraTaken = false;
        }

        void LateUpdate() => Place(false);

        void Place(bool snap)
        {
            float k = snap ? 1f : 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime);
            yaw = Mathf.Lerp(yaw, targetYaw, k);
            side = Mathf.Lerp(side, targetSide, snap ? 1f : 1f - Mathf.Exp(-7f * Time.unscaledDeltaTime));
            // Yaw 0 faces the camera.
            if (avatarRoot) avatarRoot.rotation = Quaternion.LookRotation(toCamera.sqrMagnitude > 0 ? toCamera.normalized : Vector3.back) * Quaternion.Euler(0f, yaw, 0f);
            if (!cameraTaken || !cam) return;
            // Slide the camera sideways so you stand in the left third while the loadout fills the right.
            var right = Vector3.Cross(Vector3.up, -toCamera.normalized);
            var shift = right * side * .95f;
            var eye = spot + toCamera + Vector3.up * EyeHeight + shift;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(spot + Vector3.up * LookHeight + shift - eye));
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
            Release();
            TactileMaterials.Release(gameObject);
        }
    }
}
