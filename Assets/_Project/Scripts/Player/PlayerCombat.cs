using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Punching, grabbing, carrying and throwing, plus using summoned weapons.</summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        // The punch's numbers are the mode's "unarmed" block in rules.json.

        [Header("Grab and throw")]
        [SerializeField] float grabReach = 0.8f;
        [SerializeField] float grabRadius = 0.9f;
        [SerializeField] float maxCarryMass = 6f;
        [SerializeField] float throwSpeed = 13f;
        [Tooltip("A carried player wriggles free after this long.")]
        [SerializeField] float struggleTime = 1.6f;
        [Tooltip("Damage a thrown player takes on release.")]
        [SerializeField] float thrownPlayerDamage = 6f;

        PlayerController controller;
        float nextAttack;
        Rigidbody held;
        PlayerController heldPlayer;
        HeldWeapon weapon;
        Transform heldHomeParent;
        float heldSince;
        static readonly Collider[] Overlaps = new Collider[48];
        readonly HashSet<Rigidbody> struck = new();

        /// <summary>Hits closer than this skip the arc check: they're inside the attacker.</summary>
        const float PointBlank = 0.25f;

        /// <summary>Something (or someone) was thrown.</summary>
        public event System.Action<Rigidbody> Thrown;

        public bool IsHolding => held;
        public Rigidbody Held => held;
        public HeldWeapon Weapon => held ? weapon : null;

        void Awake() => controller = GetComponent<PlayerController>();

        void Start()
        {
            // A staggering hit knocks a carried thing out of the hands; weapons are gripped tighter.
            controller.Health.Damaged += (_, _, r) => { if (r.HitStun > 0f && held && !weapon) Drop(); };
            controller.Health.KnockedOut += _ => Drop();
        }

        void Update()
        {
            if (held == null && (heldPlayer || weapon)) ClearHeld();
            if (heldPlayer && Time.time - heldSince > struggleTime) Drop();

            if (!controller.CanAct || (controller.Summoner && controller.Summoner.IsSpelling)) return;
            var c = controller.Commands;
            if (c.grab) GrabOrThrow();
            if (c.attack) Attack();
        }

        // ---- Attacking ----

        public void Attack()
        {
            if (Time.time < nextAttack) return;
            if (Weapon)
            {
                nextAttack = Time.time + Weapon.Cooldown;
                controller.PlayPunch();
                Weapon.Use(this);
                return;
            }
            if (held) { Throw(); return; }

            var fist = controller.Health.Rules.Unarmed;
            nextAttack = Time.time + fist.Cycle;
            controller.PlayPunch();
            Strike(fist, null);
        }

        /// <summary>
        /// Hits everything within reach of the chest and inside the swing's arc: players take the
        /// stats' damage, furniture takes its break power. Returns how many players were hit.
        /// </summary>
        public int Strike(MeleeStats stats, string itemId)
        {
            var chest = transform.position + Vector3.up * 0.8f;
            var facing = controller.Facing;
            int n = Physics.OverlapSphereNonAlloc(chest, stats.Reach, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            struck.Clear();
            int playersHit = 0;

            for (int i = 0; i < n; i++)
            {
                var col = Overlaps[i];
                var rb = col.attachedRigidbody;
                if (!rb || rb == controller.Body || rb == held || struck.Contains(rb)) continue;

                // Aim at the nearest part of the thing, so a sofa counts when its arm is in front.
                var to = World.Flat(ClosestPoint(col, chest) - chest);
                if (to.sqrMagnitude > PointBlank * PointBlank && !Geometry.InFrontArc(facing.x, facing.z, to.x, to.z, stats.ArcDegrees))
                    continue;
                struck.Add(rb);

                var dir = World.Flat(rb.position - transform.position);
                var hit = Hits.Melee(controller, dir.sqrMagnitude > 0.01f ? dir : facing, stats, itemId);
                if (rb.TryGetComponent(out PlayerHealth victim))
                {
                    if (victim.ApplyDamage(hit)) playersHit++;
                    continue;
                }
                if (rb.TryGetComponent(out Smashable smash)) smash.ApplyDamage(hit);
                if (rb && !rb.isKinematic)
                    rb.AddForce((facing + Vector3.up * 0.4f) * stats.Knockback * Hits.KnockbackSpeed * 0.5f, ForceMode.VelocityChange);
            }
            return playersHit;
        }

        static Vector3 ClosestPoint(Collider col, Vector3 point)
        {
            // ClosestPoint only works on primitives and convex meshes.
            if (col is MeshCollider { convex: false }) return col.bounds.ClosestPoint(point);
            return col.ClosestPoint(point);
        }

        // ---- Grabbing ----

        void GrabOrThrow()
        {
            if (held) Throw();
            else TryGrab();
        }

        public bool TryGrab()
        {
            var centre = transform.position + Vector3.up * 0.6f + controller.Facing * grabReach;
            int n = Physics.OverlapSphereNonAlloc(centre, grabRadius, Overlaps, ~0, QueryTriggerInteraction.Ignore);
            Rigidbody best = null;
            float bestSq = float.MaxValue;

            for (int i = 0; i < n; i++)
            {
                var rb = Overlaps[i].attachedRigidbody;
                if (!rb || rb == controller.Body || rb.isKinematic || rb.GetComponent<LetterTile>()) continue;
                bool isPlayer = rb.GetComponent<PlayerController>();
                if (!isPlayer && rb.mass > maxCarryMass) continue;
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
            held = rb;
            heldSince = Time.time;
            heldHomeParent = rb.transform.parent;
            weapon = rb.GetComponent<HeldWeapon>();

            if (rb.TryGetComponent(out PlayerController other))
            {
                heldPlayer = other;
                other.SetHeld(true, controller.overheadPoint);
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

            var point = rb.mass > 2.5f ? controller.overheadPoint : controller.holdPoint;
            rb.transform.rotation = Quaternion.LookRotation(controller.Facing);
            var offset = rb.transform.position - BoundsCentre(rb);
            rb.transform.position = point.position + offset;
            rb.transform.SetParent(point, true);
        }

        /// <summary>Throws what's held. Thrown players take a knock as they go.</summary>
        public void Throw()
        {
            if (!held) return;
            nextAttack = Time.time + controller.Health.Rules.Unarmed.Cycle;
            controller.PlayPunch();
            float speed = throwSpeed * Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(held.mass / maxCarryMass));
            var velocity = controller.Facing * speed + Vector3.up * 4f + World.Flat(controller.Body.linearVelocity) * 0.5f;

            if (heldPlayer)
            {
                var victim = heldPlayer;
                Release(velocity);
                victim.Health.ApplyDamage(Hits.Of(controller, controller.Facing, HitSource.Thrown, thrownPlayerDamage, 0f, hitStun: 0.35f));
                victim.Body.linearVelocity = velocity;
                ThrowTracker.Attach(victim.gameObject, controller, 1.2f);
                Thrown?.Invoke(victim.Body);
                return;
            }
            var rb = held;
            Release(velocity);
            ThrowTracker.Attach(rb.gameObject, controller, 1.5f);
            Thrown?.Invoke(rb);
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
                p.transform.position = transform.position + controller.Facing * 0.9f + Vector3.up * 1.2f;
                p.Body.linearVelocity = velocity;
                return;
            }
            rb.transform.SetParent(heldHomeParent ? heldHomeParent : null, true);
            if (rb.GetComponent<HeldWeapon>())
                rb.transform.position = transform.position + controller.Facing * 0.8f + Vector3.up * 1f;
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

        static Vector3 BoundsCentre(Rigidbody rb)
        {
            var renderers = rb.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return rb.worldCenterOfMass;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b.center;
        }
    }
}
