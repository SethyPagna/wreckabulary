using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Keeps a hidden copy of the furniture as placed in the scene, and swaps in a fresh copy
    /// at the start of every round. Place furniture under the Furniture object in the scene.
    /// </summary>
    public class RoomBuilder : MonoBehaviour
    {
        [SerializeField] Transform furnitureRoot;

        GameObject template;

        void Awake()
        {
            if (!furnitureRoot) return;
            template = Instantiate(furnitureRoot.gameObject, transform);
            template.name = "Furniture (template)";
            template.SetActive(false);
        }

        public void ResetRoom()
        {
            SummonedThing.ClearAll();
            World.ClearTransient();
            if (TilePool.Instance) TilePool.Instance.ReleaseAll();
            if (!template) return;

            var parent = furnitureRoot ? furnitureRoot.parent : null;
            if (furnitureRoot) Destroy(furnitureRoot.gameObject);
            var fresh = Instantiate(template, parent);
            fresh.name = "Furniture";
            fresh.SetActive(true);
            furnitureRoot = fresh.transform;
        }
    }
}
