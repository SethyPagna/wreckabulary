using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Art
{
    public sealed class RoomDetail : MonoBehaviour
    {
        public const int CurrentEdition = 4;
        public const string RootPrefix = "Room detail - ";
        [SerializeField] string roomId;
        [SerializeField] int ownerStorey;
        [SerializeField] int edition;
        [SerializeField] bool fitted;
        [SerializeField] Bounds[] spans = Array.Empty<Bounds>();
        [SerializeField] string[] pieces = Array.Empty<string>();
        public string RoomId => roomId;
        public int OwnerStorey => ownerStorey;
        public int Edition => edition;
        public bool Fitted => fitted;
        public IReadOnlyList<Bounds> Spans => spans;
        public IReadOnlyList<string> Pieces => pieces;

        public void Configure(string id, int storey, bool fittedPiece)
        {
            roomId = id;
            ownerStorey = storey;
            edition = CurrentEdition;
            fitted = fittedPiece;
        }

        public void SetSpans(Bounds[] value) => spans = value ?? Array.Empty<Bounds>();

        public void SetPieces(string[] value) => pieces = value ?? Array.Empty<string>();

        public static int GiveWay(Transform house, Transform furniture)
        {
            if (!house || !furniture) return 0;
            var taken = new List<Bounds>();
            foreach (var item in furniture.GetComponentsInChildren<Smashable>(true))
                if (TryBounds(item.transform, out var bounds)) taken.Add(bounds);
            if (taken.Count == 0) return 0;
            int cleared = 0;
            foreach (var detail in house.GetComponentsInChildren<RoomDetail>(true))
            {
                if (!detail.fitted || !detail.gameObject.activeSelf || !detail.Blocks(taken)) continue;
                detail.gameObject.SetActive(false);
                cleared++;
            }
            return cleared;
        }

        bool Blocks(List<Bounds> taken)
        {
            if (spans.Length == 0) return TryBounds(transform, out var own) && taken.Exists(b => b.Intersects(own));
            foreach (var span in spans)
            {
                var world = ToWorld(transform, span);
                if (taken.Exists(b => b.Intersects(world))) return true;
            }
            return false;
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

        static bool TryBounds(Transform root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is not (MeshRenderer or SkinnedMeshRenderer)) continue;
                if (any) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; any = true; }
            }
            return any;
        }
    }
}
