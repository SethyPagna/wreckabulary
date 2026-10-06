using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// The browser edition's centred third-person camera (Web/src/renderer.js): straight behind the player at
    /// their look yaw and pitch, a little above their head, looking past them. A wall, a ramp or the floor above
    /// pulls it in at once, and it eases back out once the way is clear. Furniture and other loose things never
    /// pull it in. One per camera: the match camera and the home tour each keep their own.
    /// </summary>
    public sealed class ShoulderView
    {
        public const float FieldOfView = 64f, NearClip = .1f;
        /// <summary>Behind the pivot, and never closer than <see cref="MinDistance"/>.</summary>
        public const float Distance = 2.6f, MinDistance = .35f;
        /// <summary>The pivot sits this far above the feet, and the camera this much higher again.</summary>
        public const float PivotHeight = 1.3f, Lift = .55f;
        /// <summary>The camera looks at the point this far ahead of the pivot.</summary>
        public const float LookAhead = 12f;
        public const float ProbeRadius = .2f, Skin = .05f;
        /// <summary>Easing rates, per second: the pivot's height (stairs, jumps) and the way back out after a pull-in.</summary>
        public const float HeightEase = 12f, OutEase = 6f;
        /// <summary>Look pitch in radians, positive looking down.</summary>
        public const float DefaultPitch = .16f, MinPitch = -.45f, MaxPitch = .95f;
        /// <summary>Radians of look per pixel of mouse travel.</summary>
        public const float MouseSensitivity = .0024f;

        readonly RaycastHit[] hits = new RaycastHit[16];
        float eyeY, distance = Distance;
        bool placed;

        /// <summary>How far behind the pivot the camera sits now, after any pull-in.</summary>
        public float CurrentDistance => distance;

        /// <summary>Where the camera looks for this yaw and pitch.</summary>
        public static Vector3 Forward(float yaw, float pitch) =>
            new(Mathf.Sin(yaw) * Mathf.Cos(pitch), -Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));

        /// <summary>A move stick or WASD turned to the camera's yaw, so up walks where the camera looks.</summary>
        public static Vector2 CameraRelative(Vector2 move, float yaw)
        {
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            return new Vector2(c * move.x + s * move.y, -s * move.x + c * move.y);
        }

        /// <summary>The next <see cref="Place"/> starts afresh at full distance (a new player or a respawn).</summary>
        public void Snap() => placed = false;

        public void Place(Camera camera, Vector3 feet, float yaw, float pitch, float dt)
        {
            if (!placed)
            {
                eyeY = feet.y;
                distance = Distance;
                placed = true;
            }
            eyeY = Mathf.Lerp(eyeY, feet.y, 1f - Mathf.Exp(-HeightEase * dt));
            var forward = Forward(yaw, pitch);
            var pivot = new Vector3(feet.x, eyeY + PivotHeight, feet.z);
            var origin = pivot + Vector3.up * Lift;

            float want = Distance;
            int n = Physics.SphereCastNonAlloc(origin, ProbeRadius, -forward, hits, Distance, World.GroundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
                if (!hits[i].rigidbody) want = Mathf.Min(want, hits[i].distance - Skin);
            want = Mathf.Max(MinDistance, want);
            distance = want < distance ? want : Mathf.Lerp(distance, want, 1f - Mathf.Exp(-OutEase * dt));

            var position = origin - forward * distance;
            // Looking up from the stairs mustn't put the camera through the floor you stand on.
            position.y = Mathf.Max(position.y, eyeY + MinDistance);
            var t = camera.transform;
            t.SetPositionAndRotation(position, Quaternion.LookRotation(pivot + forward * LookAhead - position));
            camera.orthographic = false;
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = NearClip;
        }
    }
}
