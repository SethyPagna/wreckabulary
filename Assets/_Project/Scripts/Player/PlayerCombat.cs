using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Punching, grabbing, carrying and throwing, plus using summoned weapons.</summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Punch")]
        [SerializeField] float punchCooldown = 0.35f;
        [SerializeField] float punchReach = 0.85f;
        [SerializeField] float punchRadius = 0.65f;
        [SerializeField] float punchKnockback = 7f;
        [SerializeField] float punchDamage = 12f;

        [Header("Grab and throw")]
        [SerializeField] float grabReach = 0.8f;
        [SerializeField] float grabRadius = 0.9f;
        [Tooltip("Carrying something this heavy (or heavier) halves your speed. Lighter things slow you less.")]
        [SerializeField] float heavyMass = 16f;
        [SerializeField] float throwSpeed = 13f;
        [Tooltip("A carried player wriggles free after this long.")]
        [SerializeField] float struggleTime = 1.6f;

        PlayerController controller;
        float nextAttack;
        Rigidbody held;
        PlayerController heldPlayer;
        HeldWeapon weapon;
        Transform heldHomeParent;
        float heldSince;
        static readonly Collider[] Hits = new Collider[48];

        // Carrying furniture and players (weapons ride in the hand instead).
        const float HandHeight = 0.6f, HeadTop = 1.35f;
        bool overhead;
        Vector3 carrySize;      // the held thing's size along its own axes
        Vector3 pivotToCentre;  // from its pivot to its visual centre, in its own space

        /// <summary>Something (or someone) was thrown.</summary>
        public event System.Action<Rigidbody> Thrown;

        public bool IsHolding => held;
        /// <summary>True when the held thing is lifted over the head (heavy things and other players).</summary>
        public bool IsOverhead => held && !weapon && overhead;
        public Rigidbody Held => held;
        public HeldWeapon Weapon => held ? weapon : null;

        void Awake() => controller = GetComponent<PlayerController>();

        void Start()
        {
            controller.Health.Hit += (_, _) => Drop();
            controller.Health.KnockedOut += _ => Drop();
        }

        void Update()
        {
            if (held == null && (heldPlayer || weapon)) ClearHeld();
            controller.CarryScale = held && !weapon ? Mathf.Lerp(1f, 0.5f, Mathf.Clamp01(held.mass / heavyMass)) : 1f;
            if (heldPlayer && Time.time - heldSince > struggleTime) Drop();

            if (!controller.CanAct || (controller.Summoner && (controller.Summoner.IsSpelling || controller.Summoner.JustClosed))) return;
            var c = controller.Commands;
            if (c.grab) GrabOrPutDown();
            if (c.attack) Attack();
        }

        // ---- Attacking ----

        public void Attack()
        {
            if (Time.time < nextAttack) return;
            if (Weapon)
            {
                nextAttack = Time.time + Weapon.cooldown;
                controller.PlayPunch();
                Sfx.Play(Sound.Punch, transform.position);
                Weapon.Use(this);
                return;
            }
            if (held) { Throw(); return; }

            nextAttack = Time.time + punchCooldown;
            controller.PlayPunch();
            Sfx.Play(Sound.Punch, transform.position, 0.8f);
            Strike(punchReach, punchRadius, punchKnockback, punchDamage, -1);
        }

        /// <summary>Hits everything in a sphere in front of the player. Returns how many players were hit.</summary>
        public int Strike(float reach, float radius, float knockback, float damage, int letters)
        {
            var centre = transform.position + Vector3.up * 0.8f + controller.Facing * reach;
            int n = Physics.OverlapSphereNonAlloc(centre, radius, Hits, ~0, QueryTriggerInteraction.Ignore);
            var seen = new HashSet<Rigidbody>();
            int playersHit = 0;

            for (int i = 0; i < n; i++)
            {
                var rb = Hits[i].attachedRigidbody;
                if (!rb || rb == controller.Body || rb == held || !seen.Add(rb)) continue;

                if (rb.TryGetComponent(out PlayerHealth victim))
                {
                    var dir = World.Flat(rb.position - transform.position);
                    if (victim.TakeHit(dir.sqrMagnitude > 0.01f ? dir : controller.Facing, knockback, letters, controller))
                        playersHit++;
                    continue;
                }
                if (rb.TryGetComponent(out Smashable smash)) smash.TakeHit(damage);
                if (rb && !rb.isKinematic)
                    rb.AddForce((controller.Facing + Vector3.up * 0.4f) * knockback * 0.5f, ForceMode.VelocityChange);
            }
            return playersHit;
        }

        // ---- Grabbing ----

        /// <summary>Grab picks things up and puts them down; attack throws them.</summary>
        void GrabOrPutDown()
        {
            if (held) PutDown();
            else TryGrab();
        }

        /// <summary>
        /// Sets the held thing down gently in front: on the floor, or on top of whatever is there.
        /// It comes closer if a wall is in the way, and keeps facing outwards.
        /// </summary>
        public void PutDown()
        {
            if (!held) return;
            Sfx.Play(Sound.PutDown, transform.position);
            var facing = controller.Facing;
            var feet = controller.Body.position;

            if (heldPlayer)
            {
                var friend = heldPlayer;
                var spot = ClearSpot(feet, facing, 0.9f, 0.45f);
                Release(Vector3.zero);
                friend.transform.position = spot;
                friend.Body.position = spot;
                friend.Body.linearVelocity = Vector3.zero;
                return;
            }
            if (weapon)
            {
                Release(Vector3.zero);
                return;
            }

            var rb = held;
            var rot = CarryRotation;
            float halfDepth = carrySize.z * 0.5f;
            var at = ClearSpot(feet, facing, 0.5f + halfDepth, halfDepth);
            float floor = feet.y;
            if (Physics.Raycast(at + Vector3.up * 3f, Vector3.down, out var hit, 6f, World.GroundMask, QueryTriggerInteraction.Ignore))
                floor = hit.point.y;
            var centre = new Vector3(at.x, floor + carrySize.y * 0.5f + 0.02f, at.z);
            var pos = centre - rot * pivotToCentre;

            Release(Vector3.zero);
            rb.transform.SetPositionAndRotation(pos, rot);
            rb.position = pos;
            rb.rotation = rot;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        /// <summary>A point <paramref name="distance"/> in front, pulled back if a wall or furniture is closer.</summary>
        Vector3 ClearSpot(Vector3 feet, Vector3 facing, float distance, float halfDepth)
        {
            var origin = feet + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, facing, out var hit, distance + halfDepth, World.GroundMask, QueryTriggerInteraction.Ignore)
                && hit.rigidbody == null) // walls only; furniture gets things stacked on top
                distance = Mathf.Max(0.3f, hit.distance - halfDepth - 0.05f);
            return feet + facing * distance;
        }

        public bool TryGrab()
        {
            var centre = transform.position + Vector3.up * 0.6f + controller.Facing * grabReach;
            int n = Physics.OverlapSphereNonAlloc(centre, grabRadius, Hits, ~0, QueryTriggerInteraction.Ignore);
            Rigidbody best = null;
            float bestSq = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                var rb = Hits[i].attachedRigidbody;
                if (!rb || rb == controller.Body || rb.isKinematic || rb.GetComponent<LetterTile>()) continue;
                float d = (rb.worldCenterOfMass - centre).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = rb; }
            }
            if (!best) return false;
            Pick(best);
            return true;
        }

        /// <summary>Equips a freshly summoned weapon, dropping whatever was held.</summary>
        public void Equip(HeldWeapon w)
        {
            Drop();
            Pick(w.GetComponent<Rigidbody>());
        }

        void Pick(Rigidbody rb)
        {
            Sfx.Play(Sound.Grab, transform.position);
            held = rb;
            heldSince = Time.time;
            heldHomeParent = rb.transform.parent;
            weapon = rb.GetComponent<HeldWeapon>();

            if (rb.TryGetComponent(out PlayerController other))
            {
                heldPlayer = other;
                other.SetHeld(true);
                overhead = true;
                // Carried lying across the shoulders: their height runs sideways.
                carrySize = new Vector3(1.24f, 0.84f, 0.84f);
                pivotToCentre = new Vector3(0f, 0.62f, 0f);
                SnapToCarry();
                return;
            }

            rb.isKinematic = true;
            SetCollidersEnabled(rb, false);
            if (weapon)
            {
                rb.transform.SetParent(controller.handR, false);
                rb.transform.localPosition = Vector3.zero;
                rb.transform.localRotation = weapon.HoldRotation;
                return;
            }

            // Measure it along its own axes, then decide how to carry it.
            rb.transform.rotation = Quaternion.identity;
            var b = RenderBounds(rb);
            carrySize = b.size;
            pivotToCentre = b.center - rb.transform.position;
            overhead = rb.mass > 2.5f || carrySize.y > 1.2f || carrySize.x > 1.6f;
            SnapToCarry();
        }

        /// <summary>Front carry: at the hands, letters facing out. Players lie across the shoulders.</summary>
        Quaternion CarryRotation => heldPlayer
            ? Quaternion.LookRotation(controller.Facing) * Quaternion.Euler(0f, 0f, 90f)
            : Quaternion.LookRotation(-controller.Facing);

        Vector3 CarryCentre(Vector3 at) => overhead
            ? at + Vector3.up * (HeadTop + carrySize.y * 0.5f) + controller.Facing * 0.05f
            : at + Vector3.up * Mathf.Max(0.85f, HandHeight + carrySize.y * 0.5f) + controller.Facing * (0.45f + carrySize.z * 0.5f);

        void SnapToCarry()
        {
            var rot = CarryRotation;
            var pos = CarryCentre(controller.Body.position) - rot * pivotToCentre;
            held.transform.SetPositionAndRotation(pos, rot);
            held.position = pos;
            held.rotation = rot;
        }

        void FixedUpdate()
        {
            if (!held || weapon) return;
            // Moved as a kinematic body rather than parented, so it doesn't squash and wobble with the body.
            var rot = CarryRotation;
            held.MovePosition(CarryCentre(controller.Body.position) - rot * pivotToCentre);
            held.MoveRotation(rot);
        }

        /// <summary>
        /// Where the hands go on the thing being carried: gripping its sides, or holding it up from underneath.
        /// Read by the body animation, so hands are placed in one spot each frame.
        /// </summary>
        public bool TryGetGrips(out Vector3 left, out Vector3 right)
        {
            left = right = default;
            if (!held || weapon) return false;
            var t = held.transform;
            var centre = t.position + t.rotation * pivotToCentre;
            var side = Vector3.Cross(Vector3.up, controller.Facing).normalized;
            Vector3 grip;
            float half;
            if (overhead)
            {
                grip = centre - Vector3.up * (carrySize.y * 0.5f);
                half = Mathf.Min(carrySize.x * 0.4f, 0.45f);
            }
            else
            {
                grip = centre;
                half = Mathf.Min(carrySize.x * 0.5f + 0.06f, 0.7f);
            }
            left = grip - side * half;
            right = grip + side * half;
            return true;
        }

        /// <summary>Throws what's held. Thrown players lose letters as if hit.</summary>
        public void Throw()
        {
            if (!held) return;
            nextAttack = Time.time + punchCooldown;
            controller.PlayPunch();
            float speed = throwSpeed * Mathf.Lerp(1f, 0.55f, Mathf.Clamp01(held.mass / heavyMass));
            var velocity = controller.Facing * speed + Vector3.up * 4f + World.Flat(controller.Body.linearVelocity) * 0.5f;

            if (heldPlayer)
            {
                var victim = heldPlayer;
                Release(velocity);
                victim.Health.TakeHit(controller.Facing, 0f, -1, controller);
                victim.Body.linearVelocity = velocity;
                ThrowTracker.Attach(victim.gameObject, controller, 1.2f);
                Thrown?.Invoke(victim.Body);
                Sfx.Play(Sound.Throw, transform.position);
                return;
            }
            var rb = held;
            Release(velocity);
            ThrowTracker.Attach(rb.gameObject, controller, 1.5f);
            Thrown?.Invoke(rb);
            Sfx.Play(Sound.Throw, transform.position);
        }

        /// <summary>Lets go of whatever is held without throwing it.</summary>
        public void Drop()
        {
            if (held) Release(World.Flat(controller.Body.linearVelocity));
        }

        /// <summary>Round reset: summoned weapons vanish, everything else is let go.</summary>
        public void ResetForRound()
        {
            if (held && weapon) Destroy(held.gameObject);
            else Drop();
            ClearHeld();
        }

        /// <summary>Called by a weapon when it runs out of uses.</summary>
        public void ForgetHeld() => ClearHeld();

        void Release(Vector3 velocity)
        {
            var rb = held;
            var p = heldPlayer;
            ClearHeld();

            if (p)
            {
                p.SetHeld(false);
                p.Body.linearVelocity = velocity;
                return;
            }
            if (rb.GetComponent<HeldWeapon>())
            {
                rb.transform.SetParent(heldHomeParent ? heldHomeParent : null, true);
                rb.transform.position = transform.position + controller.Facing * 0.8f + Vector3.up * 1f;
            }
            rb.isKinematic = false;
            SetCollidersEnabled(rb, true);
            rb.linearVelocity = velocity;
            rb.angularVelocity = Random.insideUnitSphere * 3f;
        }

        void ClearHeld()
        {
            held = null;
            heldPlayer = null;
            weapon = null;
        }

        static void SetCollidersEnabled(Rigidbody rb, bool on)
        {
            foreach (var c in rb.GetComponentsInChildren<Collider>(true)) c.enabled = on;
        }

        static Bounds RenderBounds(Rigidbody rb)
        {
            var renderers = rb.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(rb.worldCenterOfMass, Vector3.one * 0.5f);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
