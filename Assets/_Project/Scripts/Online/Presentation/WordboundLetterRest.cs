using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Online
{
    public static class WordboundLetterRest
    {
        public const float Lift = .1f, Reach = 4f;

        public static bool TryGround(PhysicsScene scene, Vector3 body, HashSet<Rigidbody> ignored, RaycastHit[] hits, out float ground)
        {
            ground = body.y;
            if (!scene.IsValid() || hits == null || hits.Length == 0) return false;
            int count = scene.Raycast(body + Vector3.up * Lift, Vector3.down, hits, Lift + Reach, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!hit.collider || hit.distance >= nearest) continue;
                var owner = hit.collider.attachedRigidbody;
                if (owner && ignored != null && ignored.Contains(owner)) continue;
                nearest = hit.distance;
                ground = hit.point.y;
            }
            return nearest < float.PositiveInfinity;
        }

        public static Quaternion Flat(Quaternion body)
        {
            var forward = body * Vector3.forward;
            forward.y = 0;
            if (forward.sqrMagnitude < .0001f) { forward = body * Vector3.up; forward.y = 0; }
            return forward.sqrMagnitude < .0001f ? Quaternion.identity : Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
