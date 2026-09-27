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
        [SerializeField] float maxCarryMass = 6f;
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

        /// <summary>Something (or someone) was thrown.</summary>
        public event System.Action<Rigidbody> Thrown;

        public bool IsHolding => held;
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
                nextAttack = Time.time + Weapon.cooldown;
                controller.PlayPunch();
                Weapon.Use(this);
                return;
            }
            if (held) { Throw(); return; }

            nextAttack = Time.time + punchCooldown;
            controller.PlayPunch();
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

        void GrabOrThrow()
        {
            if (held) Throw();
            else TryGrab();
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

        /// <summary>Throws what's held. Thrown players lose letters as if hit.</summary>
        public void Throw()
        {
            if (!held) return;
            nextAttack = Time.time + punchCooldown;
            controller.PlayPunch();
            float speed = throwSpeed * Mathf.Lerp(1f, 0.65f, Mathf.Clamp01(held.mass / maxCarryMass));
            var velocity = controller.Facing * speed + Vector3.up * 4f + World.Flat(controller.Body.linearVelocity) * 0.5f;

            if (heldPlayer)
            {
                var victim = heldPlayer;
                Release(velocity);
                victim.Health.TakeHit(controller.Facing, 0f, -1, controller);
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
