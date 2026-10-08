using UnityEngine;
using Wreckabulary.Rules;
using System.Collections.Generic;

namespace Wreckabulary
{
    /// <summary>Centered third-person follow for one local seat and a shared house view for couch play.</summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(-75)]
    public sealed class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        [Header("Third person")]
        [SerializeField, Min(1f)] float distance = 3.15f;
        [SerializeField, Min(0f)] float pivotHeight = 1f;
        [SerializeField, Range(40f, 90f)] float fieldOfView = 55f;
        [SerializeField, Range(-5f, 45f)] float defaultPitch = 10f;
        [SerializeField, Min(0.01f)] float followSmoothTime = 0.075f;
        [SerializeField, Min(0f)] float mouseSensitivity = 0.16f;
        [SerializeField, Min(0f)] float stickDegreesPerSecond = 130f;
        [SerializeField] Vector2 pitchLimits = new(6f, 38f);

        [Header("Obstructions")]
        [SerializeField, Min(0.05f)] float collisionRadius = 0.22f;
        [SerializeField, Min(0.01f)] float collisionPadding = 0.1f;
        [SerializeField, Min(0.1f)] float collisionReturnSpeed = 6f;

        readonly RaycastHit[] obstructionHits = new RaycastHit[32];
        readonly Dictionary<Renderer, Renderer[]> cutawayParts = new();
        readonly Renderer[] hiddenWalls = new Renderer[96];
        int hiddenCount;
        Camera lens;
        PlayerController target;
        Vector3 layoutCentre, focus, focusVelocity;
        float layoutWidth = 20f, layoutDepth = 20f;
        float yaw, pitch, currentDistance, shake, lastOrbitAt;
        int localSeats;
        bool hasLayout, snap = true;

        public Camera ViewCamera => lens;
        public PlayerController FollowedPlayer => target;
        public bool IsThirdPerson => localSeats == 1 && target;
        public bool IsFollowing(PlayerController player) => IsThirdPerson && target == player;
        public Vector3 PlanarForward => Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Instance = null;

        void Awake()
        {
            Instance = this;
            lens = GetComponent<Camera>();
            lens.nearClipPlane = 0.08f;
            focus = transform.position;
            pitch = defaultPitch;
            currentDistance = distance;
        }

        void OnDestroy()
        {
            RestoreWalls();
            if (Instance == this) Instance = null;
        }

        void OnDisable() => RestoreWalls();

        /// <summary>Records house bounds; local seats choose the view rather than forcing an overhead camera.</summary>
        public void FrameLayout(HouseLayout layout)
        {
            if (layout == null || layout.Rooms.Count == 0) return;
            if (!lens) lens = GetComponent<Camera>();
            float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
            float minZ = float.PositiveInfinity, maxZ = float.NegativeInfinity;
            foreach (var room in layout.Rooms)
            {
                minX = Mathf.Min(minX, room.MinX); maxX = Mathf.Max(maxX, room.MaxX);
                minZ = Mathf.Min(minZ, room.MinZ); maxZ = Mathf.Max(maxZ, room.MaxZ);
            }
            layoutWidth = maxX - minX;
            layoutDepth = maxZ - minZ;
            layoutCentre = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            hasLayout = true;
            snap = true;
            SelectTarget();
            ApplyView(0f);
        }

        void Update() => SelectTarget();

        void SelectTarget()
        {
            int count = 0;
            PlayerController local = null, survivor = null;
            foreach (var player in World.Players)
            {
                if (!player) continue;
                if (!player.IsEliminated && !survivor) survivor = player;
                if (player.Binding == null || player.Binding is BotBinding) continue;
                count++;
                local = player;
            }
            var next = count == 1 ? (local.IsEliminated && survivor ? survivor : local) : null;
            if (count == localSeats && next == target) return;
            localSeats = count;
            target = next;
            snap = true;
            if (!target) return;
            var facing = target.Facing;
            yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            pitch = defaultPitch;
            currentDistance = distance;
            focusVelocity = Vector3.zero;
        }

        /// <summary>Transforms screen-relative input once; simulation commands remain in world space.</summary>
        public Vector2 ScreenDirectionToWorld(Vector2 input)
        {
            var rotation = IsThirdPerson ? Quaternion.Euler(0f, yaw, 0f) : Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            var direction = rotation * new Vector3(input.x, 0f, input.y);
            return new Vector2(direction.x, direction.z);
        }

        public static void Shake(float amount)
        {
            if (Instance) Instance.shake = Mathf.Max(Instance.shake, Mathf.Min(amount, 0.4f));
        }

        void LateUpdate()
        {
            if (!lens || Time.timeScale <= 0f) return;
            ApplyView(Time.deltaTime);
        }

        void ApplyView(float dt)
        {
            if (!lens) return;
            RestoreWalls();
            if (IsThirdPerson) FollowPlayer(dt);
            else if (hasLayout) FrameSharedHouse(dt);
            shake = Mathf.MoveTowards(shake, 0f, dt * 1.5f);
            if (shake > 0f)
            {
                // Rotation-only shake cannot push the camera through the collision boundary.
                float phase = Time.unscaledTime * 47f;
                transform.rotation *= Quaternion.Euler(Mathf.Sin(phase) * shake, Mathf.Cos(phase * 1.3f) * shake, 0f);
            }
            snap = false;
        }

        void FollowPlayer(float dt)
        {
            lens.orthographic = false;
            lens.fieldOfView = fieldOfView;
            lens.nearClipPlane = 0.08f;
            var command = target.Commands;
            if (!target.Frozen && !target.IsKnockedOut && !command.spellHeld)
            {
                float scale = command.orbitIsRate ? stickDegreesPerSecond * dt : mouseSensitivity;
                yaw += command.orbit.x * scale;
                pitch = Mathf.Clamp(pitch - command.orbit.y * scale, pitchLimits.x, pitchLimits.y);
                if (command.orbit.sqrMagnitude > 0.001f) lastOrbitAt = Time.time;
                else if (!command.aimAtPointer && command.look.sqrMagnitude < .01f && command.move.sqrMagnitude < .01f && Time.time - lastOrbitAt > .75f)
                {
                    float facing = Mathf.Atan2(target.Facing.x, target.Facing.z) * Mathf.Rad2Deg;
                    yaw = Mathf.LerpAngle(yaw, facing, 1f - Mathf.Exp(-5f * dt));
                }
            }
            var desiredFocus = target.transform.position + Vector3.up * pivotHeight;
            if (snap || (desiredFocus - focus).sqrMagnitude > 100f)
            {
                focus = desiredFocus;
                focusVelocity = Vector3.zero;
            }
            else focus = Vector3.SmoothDamp(focus, desiredFocus, ref focusVelocity, followSmoothTime, Mathf.Infinity, dt);

            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            var backwards = rotation * Vector3.back;
            float clearDistance = UnobstructedDistance(focus, backwards, distance);
            if (clearDistance < 1.2f)
            {
                // A full-height lobby wall must not turn the close chase view into an accidental first-person view.
                float bestYaw = yaw, bestDistance = clearDistance;
                for (int step = 1; step <= 4; step++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float candidateYaw = yaw + step * 35f * side;
                        float room = UnobstructedDistance(focus, Quaternion.Euler(pitch, candidateYaw, 0f) * Vector3.back, distance);
                        if (room > bestDistance + .25f) { bestYaw = candidateYaw; bestDistance = room; }
                    }
                yaw = bestYaw;
                rotation = Quaternion.Euler(pitch, yaw, 0f);
                backwards = rotation * Vector3.back;
                clearDistance = bestDistance;
            }
            currentDistance = snap || clearDistance < currentDistance
                ? clearDistance
                : Mathf.MoveTowards(currentDistance, clearDistance, collisionReturnSpeed * dt);
            transform.SetPositionAndRotation(focus + backwards * currentDistance, rotation);
            HideOccludingCutaways();
        }

        float UnobstructedDistance(Vector3 origin, Vector3 direction, float desiredDistance)
        {
            float available = desiredDistance;
            int count = Physics.SphereCastNonAlloc(origin, collisionRadius, direction, obstructionHits,
                desiredDistance, World.GroundMask, QueryTriggerInteraction.Ignore);
            var ray = new Ray(origin, direction);
            for (int i = 0; i < count; i++)
            {
                var hit = obstructionHits[i];
                if (!hit.collider || hit.collider.transform.IsChildOf(target.transform)) continue;
                float hitDistance = hit.distance;
                if (hit.collider.TryGetComponent<Renderer>(out var renderer))
                {
                    if (IsCutaway(hit.collider, renderer)) continue;
                    // Tall wall colliders preserve doorway routes above the visible cutaway geometry.
                    var visible = renderer.bounds;
                    visible.Expand(collisionRadius * 2f);
                    if (!renderer.enabled || !visible.IntersectRay(ray, out float visualDistance)) continue;
                    hitDistance = Mathf.Max(hitDistance, visualDistance);
                }
                available = Mathf.Min(available, Mathf.Max(0.15f, hitDistance - collisionPadding));
            }
            return available;
        }

        static bool IsCutaway(Collider collider, Renderer renderer) =>
            renderer.bounds.size.y <= 1.35f && collider.bounds.size.y > renderer.bounds.size.y * 2f;

        void HideOccludingCutaways()
        {
            var origin = target.transform.position + Vector3.up * .4f;
            var delta = transform.position - origin;
            int count = Physics.SphereCastNonAlloc(origin, .3f, delta.normalized, obstructionHits,
                delta.magnitude, World.GroundMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = obstructionHits[i].collider;
                if (!collider || !collider.TryGetComponent<Renderer>(out var wall) || !IsCutaway(collider, wall)) continue;
                if (!cutawayParts.TryGetValue(wall, out var parts))
                {
                    var group = new List<Renderer> { wall };
                    var area = wall.bounds;
                    area.Expand(.18f);
                    var parent = wall.transform.parent;
                    if (parent)
                        foreach (Transform child in parent)
                            if (child.TryGetComponent<Renderer>(out var trim) && trim != wall && trim.bounds.size.y < .15f && area.Intersects(trim.bounds))
                                group.Add(trim);
                    parts = group.ToArray();
                    cutawayParts.Add(wall, parts);
                }
                foreach (var part in parts)
                {
                    if (!part || part.forceRenderingOff || hiddenCount >= hiddenWalls.Length) continue;
                    part.forceRenderingOff = true;
                    hiddenWalls[hiddenCount++] = part;
                }
            }
        }

        void RestoreWalls()
        {
            for (int i = 0; i < hiddenCount; i++)
                if (hiddenWalls[i]) hiddenWalls[i].forceRenderingOff = false;
            hiddenCount = 0;
        }

        void FrameSharedHouse(float dt)
        {
            lens.orthographic = true;
            float size = Mathf.Max(layoutWidth / Mathf.Max(0.5f, lens.aspect), layoutDepth * 0.85f) * 0.55f + 1.5f;
            lens.orthographicSize = snap ? size : Mathf.Lerp(lens.orthographicSize, size, 1f - Mathf.Exp(-5f * dt));
            var position = layoutCentre + new Vector3(0f, 30f, -22f);
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(layoutCentre - position));
        }
    }
}
