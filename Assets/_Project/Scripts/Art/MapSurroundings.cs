using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Surroundings;

namespace Wreckabulary.Art
{
    [DisallowMultipleComponent]
    public sealed class MapSurroundings : MonoBehaviour
    {
        public const string RootName = "Backdrop";
        public const string ResourceFolder = "Surroundings/";
        public const string LowSuffix = "_Low";
        public const string LawnName = "Lawn";
        public const string PlantKey = "Items/PLANT";
        public const string DisableVariable = "WRECKABULARY_NO_BACKDROP";
        public const float PlayMargin = SurroundingsLayout.PlayMargin, PlayFloor = SurroundingsLayout.PlayFloor, PlayHeadroom = SurroundingsLayout.PlayHeadroom;

        static readonly HashSet<Renderer> Registered = new();

        [SerializeField] string mapId;
        [SerializeField] string lightId;
        [SerializeField] string ambienceId;
        [SerializeField] string layoutHash;
        [SerializeField] bool lowTier;

        readonly List<Renderer> owned = new();
        readonly List<Renderer> lawns = new();
        readonly List<GameObject> plants = new();
        readonly List<Renderer> plantRenderers = new();
        EnvironmentLightingProfile previousProfile, appliedProfile;

        public static bool Disabled { get; private set; } = Environment.GetEnvironmentVariable(DisableVariable) == "1";
        public static MapSurroundings Current { get; private set; }
        public Bounds PlayVolume { get; private set; }
        public string MapId => mapId;
        public string LightId => lightId;
        public string AmbienceId => ambienceId;
        public string LayoutHash => layoutHash;
        public bool LowTier => lowTier;
        public IReadOnlyList<Renderer> HiddenLawns => lawns;
        public IReadOnlyList<GameObject> HiddenPlants => plants;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Registered.Clear(); Current = null; }

        public static bool UseLowTier => Application.isMobilePlatform || GraphicsOptions.Preset == "Low";

        public static string PrefabPath(string map, bool low) => ResourceFolder + map + (low ? LowSuffix : "");

        public static bool SceneShowsBackdrop(Scene scene) => scene.name is not (Session.TutorialScene or Session.HubScene);

        public static bool IsBackdrop(Renderer renderer) => renderer && Registered.Contains(renderer);

        public static MapSurroundings Attach(Transform parent, HouseLayout layout, string mapId) => Attach(parent, layout, mapId, null);

        public static MapSurroundings Attach(Transform parent, HouseLayout layout, string mapId, Transform house)
        {
            if (Disabled)
            {
                Debug.Log($"[Wreckabulary] Backdrops switched off by {DisableVariable}=1 (map {mapId}).");
                return null;
            }
            if (!parent || layout == null || layout.Rooms.Count == 0 || string.IsNullOrEmpty(mapId)) return null;
            if (!SceneShowsBackdrop(parent.gameObject.scene)) return null;
            bool low = UseLowTier;
            var prefab = Load(mapId, low) ?? Load(mapId, !low);
            if (!prefab) return null;
            var instance = Instantiate(prefab, parent, false);
            instance.name = RootName;
            var anchor = house ? house : parent;
            instance.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
            var surroundings = instance.GetComponent<MapSurroundings>();
            surroundings.Begin(layout, anchor);
            Debug.Log($"[Wreckabulary] Backdrop {mapId} {(surroundings.lowTier ? "Low" : "High")}: {surroundings.owned.Count} renderers, {surroundings.Triangles} triangles.");
            return surroundings;
        }

        static GameObject Load(string map, bool low)
        {
            var prefab = Resources.Load<GameObject>(PrefabPath(map, low));
            return prefab && prefab.GetComponent<MapSurroundings>() ? prefab : null;
        }

        public void Configure(string map, string light, string ambience, string hash, bool low)
        {
            mapId = map; lightId = light; ambienceId = ambience; layoutHash = hash; lowTier = low;
        }

        public static Bounds PlayBox(HouseLayout layout)
        {
            float minX = float.MaxValue, minZ = float.MaxValue, maxX = float.MinValue, maxZ = float.MinValue, top = float.MinValue;
            foreach (var room in layout.Rooms)
            {
                minX = Mathf.Min(minX, room.MinX); maxX = Mathf.Max(maxX, room.MaxX);
                minZ = Mathf.Min(minZ, room.MinZ); maxZ = Mathf.Max(maxZ, room.MaxZ);
                top = Mathf.Max(top, room.FloorY);
            }
            var bounds = new Bounds();
            bounds.SetMinMax(new Vector3(minX - PlayMargin, PlayFloor, minZ - PlayMargin), new Vector3(maxX + PlayMargin, top + PlayHeadroom, maxZ + PlayMargin));
            return bounds;
        }

        static Bounds ToWorld(Transform space, Bounds local)
        {
            var world = new Bounds(space.TransformPoint(local.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                var offset = new Vector3((corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f);
                world.Encapsulate(space.TransformPoint(local.center + Vector3.Scale(local.extents, offset)));
            }
            return world;
        }

        void Begin(HouseLayout layout, Transform house)
        {
            Current = this;
            GetComponentsInChildren(true, owned);
            foreach (var renderer in owned) Registered.Add(renderer);
            PlayVolume = ToWorld(house, PlayBox(layout));
            BackdropShader.SetPlayVolume(PlayVolume);
            if (!GetComponent<BackdropLife>()) gameObject.AddComponent<BackdropLife>();
            if (!string.IsNullOrEmpty(ambienceId)) BackdropAmbience.Attach(transform, ambienceId);
            ApplyLight();
            HideHouseSurroundings(layout, house);
        }

        public int Triangles
        {
            get
            {
                long total = 0;
                foreach (var renderer in owned)
                    if (renderer && renderer.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh)
                        for (int i = 0; i < filter.sharedMesh.subMeshCount; i++) total += filter.sharedMesh.GetIndexCount(i) / 3;
                return (int)total;
            }
        }

        void ApplyLight()
        {
            var environment = EnvironmentLighting.Active;
            if (!environment || string.IsNullOrEmpty(lightId)) return;
            var profile = Resources.Load<EnvironmentLightingProfile>("Environment/" + lightId);
            if (!profile) return;
            previousProfile = environment.Profile;
            appliedProfile = profile;
            environment.Configure(profile, environment.Sun, environment.Fill);
        }

        void HideHouseSurroundings(HouseLayout layout, Transform house)
        {
            var ground = house.Find(RoomBuilder.StoreyName(0));
            if (!ground) return;
            var footprint = PlayBox(layout);
            foreach (var part in ground.GetComponentsInChildren<Transform>(true))
            {
                if (part.name == LawnName && part.TryGetComponent<Renderer>(out var lawn) && lawn.enabled)
                {
                    lawn.enabled = false;
                    lawns.Add(lawn);
                }
                else if (part.name == PlantKey && !InsidePlant(part, ground) && Outside(house.InverseTransformPoint(part.position), footprint))
                {
                    plants.Add(part.gameObject);
                    foreach (var leaf in part.GetComponentsInChildren<Renderer>(true))
                    {
                        if (!leaf.enabled) continue;
                        leaf.enabled = false;
                        plantRenderers.Add(leaf);
                    }
                }
            }
        }

        static bool InsidePlant(Transform part, Transform ground)
        {
            for (var parent = part.parent; parent && parent != ground; parent = parent.parent)
                if (parent.name == PlantKey) return true;
            return false;
        }

        static bool Outside(Vector3 at, Bounds footprint) =>
            at.x < footprint.min.x || at.x > footprint.max.x || at.z < footprint.min.z || at.z > footprint.max.z;

        void OnDestroy()
        {
            foreach (var renderer in owned) Registered.Remove(renderer);
            owned.Clear();
            if (Current == this)
            {
                Current = null;
                BackdropShader.ClearPlayVolume();
            }
            if (gameObject.scene.isLoaded) RestoreHouse();
            lawns.Clear(); plants.Clear(); plantRenderers.Clear();
        }

        void RestoreHouse()
        {
            foreach (var lawn in lawns) if (lawn) lawn.enabled = true;
            foreach (var leaf in plantRenderers) if (leaf) leaf.enabled = true;
            var environment = EnvironmentLighting.Active;
            if (environment && environment.isActiveAndEnabled && appliedProfile && previousProfile && environment.Profile == appliedProfile)
                environment.Configure(previousProfile, environment.Sun, environment.Fill);
        }
    }
}
