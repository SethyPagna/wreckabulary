using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// A house wall's two looks: low under an overhead camera, so you see into every room, and full height
    /// round a third-person camera, as in the browser edition (2.7 m outside, 2.4 m inside, never into the
    /// floor above). Either way its collider stands the full storey, so doorways stay the only way through.
    /// </summary>
    public sealed class TallWall : MonoBehaviour
    {
        public const float Low = 1.1f, Exterior = 2.7f, Interior = 2.4f;

        /// <summary>The wall's floor and how high its collider reaches above it.</summary>
        public float FloorY, Height;
        /// <summary>Outside the house on one side (or a garden): the taller of the two.</summary>
        public bool Outside;

        public float VisualHeight(bool tall) => tall ? Mathf.Min(Outside ? Exterior : Interior, Height) : Low;

        public void Apply(bool tall)
        {
            float h = VisualHeight(tall);
            var t = transform;
            var scale = t.localScale;
            scale.y = h;
            t.localScale = scale;
            var at = t.position;
            at.y = FloorY + h * .5f;
            t.position = at;
            // The collider is scaled with the wall, so it's sized back up to the full storey from the floor.
            var box = GetComponent<BoxCollider>();
            box.size = new Vector3(1f, Height / h, 1f);
            box.center = new Vector3(0f, (Height - h) * .5f / h, 0f);
        }
    }
}
