using System;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>An editable world whose saved geometry and initial furniture are authoritative in Play mode.</summary>
    public sealed class AuthoredHouse : MonoBehaviour
    {
        [SerializeField] string mapId = "pinwheel";
        [SerializeField] Transform geometryRoot;
        [SerializeField] Transform furnitureRoot;

        Transform furnitureSnapshot;
        public string MapId => mapId;
        public Transform GeometryRoot => geometryRoot;
        public Transform FurnitureRoot => furnitureRoot;

        public static string ResourcePath(string id) => id switch
        {
            "pinwheel" => "Worlds/PinwheelHouse",
            "courtyard" => "Worlds/GardenCourtyard",
            _ => null
        };

        public bool Matches(string id) => string.Equals(mapId, id, StringComparison.Ordinal);

        public void Configure(string id, Transform geometry, Transform furniture)
        {
            mapId = id;
            geometryRoot = geometry;
            furnitureRoot = furniture;
        }

        /// <summary>Capture authored transforms, materials and component overrides before gameplay can change them.</summary>
        public void PrepareForPlay()
        {
            if (furnitureSnapshot) return;
            if (!furnitureRoot)
            {
                furnitureRoot = new GameObject("Original furniture").transform;
                furnitureRoot.SetParent(transform, false);
            }
            var snapshotHolder = new GameObject("Round furniture snapshot");
            snapshotHolder.transform.SetParent(transform, false);
            snapshotHolder.SetActive(false);
            snapshotHolder.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            furnitureSnapshot = Instantiate(furnitureRoot, snapshotHolder.transform, false);
            furnitureSnapshot.name = "Initial authored furniture";
            furnitureSnapshot.gameObject.SetActive(false);
        }

        public Transform ResetFurniture(bool furnish)
        {
            PrepareForPlay();
            if (furnitureRoot)
            {
                furnitureRoot.gameObject.SetActive(false);
                Destroy(furnitureRoot.gameObject);
            }
            if (furnish)
            {
                furnitureRoot = Instantiate(furnitureSnapshot, transform, false);
                furnitureRoot.name = "Original furniture";
                furnitureRoot.gameObject.hideFlags = HideFlags.None;
                furnitureRoot.gameObject.SetActive(true);
            }
            else
            {
                furnitureRoot = new GameObject("Original furniture").transform;
                furnitureRoot.SetParent(transform, false);
            }
            return furnitureRoot;
        }
    }
}
