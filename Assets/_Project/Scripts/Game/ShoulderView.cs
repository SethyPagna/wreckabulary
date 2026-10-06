using UnityEngine;

namespace Wreckabulary
{
    public sealed class ShoulderView
    {
        public const float FieldOfView = 64f, NearClip = .1f;
        public const float Distance = 2.6f, MinDistance = .35f;
        public const float PivotHeight = 1.3f, Lift = .55f;
        public const float LookAhead = 12f;
        public const float ProbeRadius = .2f, Skin = .05f;
        public const float HeightEase = 12f, OutEase = 6f;
        public const float DefaultPitch = .16f, MinPitch = -.45f, MaxPitch = .95f;
        public const float MouseSensitivity = .0024f;

        readonly RaycastHit[] hits = new RaycastHit[16];
        float eyeY, distance = Distance;
        bool placed;

        public float CurrentDistance => distance;

        public static Vector3 Forward(float yaw, float pitch) =>
            new(Mathf.Sin(yaw) * Mathf.Cos(pitch), -Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));

        public static Vector2 CameraRelative(Vector2 move, float yaw)
        {
            float c = Mathf.Cos(yaw), s = Mathf.Sin(yaw);
            return new Vector2(c * move.x + s * move.y, -s * move.x + c * move.y);
        }

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
            position.y = Mathf.Max(position.y, eyeY + MinDistance);
            var t = camera.transform;
            t.SetPositionAndRotation(position, Quaternion.LookRotation(pivot + forward * LookAhead - position));
            camera.orthographic = false;
            camera.fieldOfView = FieldOfView;
            camera.nearClipPlane = NearClip;
        }
    }
}
