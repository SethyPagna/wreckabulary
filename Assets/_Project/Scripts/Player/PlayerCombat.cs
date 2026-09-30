using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>Punching, grabbing, carrying and throwing, using summoned weapons, blocking with a PLATE and reviving teammates.</summary>
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
        /// <summary>When block went down with a shield in hand; the shield is up once its raise time has passed.</summary>
        float raiseStartedAt = -1f;
        PlayerController reviving;
        static readonly Collider[] Overlaps = new Collider[48];
        readonly HashSet<Rigidbody> struck = new();

        /// <summary>Hits closer than this skip the arc check: they're inside the attacker.</summary>
        const float PointBlank = 0.25f;

        /// <summary>Something (or someone) was thrown.</summary>
        public event System.Action<Rigidbody> Thrown;

        public bool IsHolding => held;
        public Rigidbody Held => held;
        public HeldWeapon Weapon => held ? weapon : null;
        /// <summary>A PLATE is up: hits from the front are blocked.</summary>
        public bool IsBlocking => controller.Health.RaisedShield != null;
        public bool IsReviving => reviving;
        /// <summary>The downed teammate being revived, or null.</summary>
        public PlayerController Reviving => reviving;

        void Awake() => controller = GetComponent<PlayerController>();

        void Start()
        {
            var health = controller.Health;
            health.Damaged += (_, _, r) =>
            {
                if (r.Blocked) WearShield(r.BlockedDamage);
                // A staggering hit knocks a carried thing out of the hands; weapons are gripped tighter.
                if (r.HitStun > 0f && held && !weapon) Drop();
            };
            health.KnockedOut += _ =>
            {
                StopReviving();
                Drop();
            };
        }

        void Update()
        {
            if (held == null && (heldPlayer || weapon)) ClearHeld();
            if (heldPlayer && Time.time - heldSince > struggleTime) Drop();

            var c = controller.Commands;
            bool free = controller.CanAct && !controller.IsDodging && !(controller.Summoner && controller.Summoner.IsSpelling);
            UpdateRevive(free && c.grabHeld);
            UpdateBlock(free && c.blockHeld && !reviving);
            if (!free || reviving) return;

            if (c.drop) Drop();
            if (c.grab) GrabOrThrow();
            // No swinging from behind a raised (or rising) shield.
            if (c.attack && raiseStartedAt < 0f) Attack();
        }

        // ---- Blocking ----

        void UpdateBlock(bool wanted)
        {
            var shield = wanted && Weapon ? Weapon.Shield : null;
            if (shield == null)
            {
                if (IsBlocking && Weapon) Weapon.ShowRaised(false);
                raiseStartedAt = -1f;
                controller.Health.RaisedShield = null;
                return;
            }
            if (raiseStartedAt < 0f) raiseStartedAt = Time.time;
            bool up = Time.time - raiseStartedAt >= shield.RaiseSeconds;
            if (up != IsBlocking) Weapon.ShowRaised(up);
            controller.Health.RaisedShield = up ? shield : null;
        }

        /// <summary>Blocked damage wears the PLATE down. Worn through, it falls apart and the block drops with it (see <see cref="ClearHeld"/>).</summary>
        void WearShield(float blocked)
        {
            if (Weapon && Weapon.Shield != null) Weapon.Wear(blocked, this);
        }

        // ---- Reviving ----

        /// <summary>The nearest downed teammate within the rules' revive range, or null.</summary>
        public PlayerController DownedTeammateNearby()
        {
            float range = controller.Health.Rules.ReviveRange;
            float bestSq = range * range;
            PlayerController best = null;
            foreach (var p in World.Players)
            {
                if (p == controller || !p.IsDowned || p.IsHeld || !Teams.AreTeammates(p.Team, controller.Team)) continue;
                float d = World.Flat(p.transform.position - transform.position).sqrMagnitude;
                if (d > bestSq) continue;
                bestSq = d;
                best = p;
            }
            return best;
        }

        /// <summary>Starts reviving the nearest downed teammate. Grab has to stay held until it finishes.</summary>
        public bool TryRevive()
        {
            var target = DownedTeammateNearby();
            if (!target || !target.Health.BeginRevive(controller)) return false;
            reviving = target;
            controller.FaceTowards(target.transform.position - transform.position);
            return true;
        }

        void UpdateRevive(bool keepGoing)
        {
            if (!reviving)
            {
                reviving = null;
                return;
            }
            // A little slack, so the teammate's crawl doesn't break it off at the edge of reach.
            float range = controller.Health.Rules.ReviveRange + 0.3f;
            var to = World.Flat(reviving.transform.position - transform.position);
            if (!keepGoing || !reviving.IsDowned || reviving.IsHeld || to.sqrMagnitude > range * range)
            {
                StopReviving();
                return;
            }
            controller.FaceTowards(to);
            if (reviving.Health.TryFinishRevive(controller)) reviving = null;
        }

        void StopReviving()
        {
            if (reviving) reviving.Health.CancelRevive(controller);
            reviving = null;
        }

        // ---- Attacking ----

        public void Attack()
        {
            if (Time.time < nextAttack) return;
            if (Weapon && Weapon.Shield == null)
            {
                nextAttack = Time.time + Weapon.Cooldown;
                controller.PlayPunch();
                Weapon.Use(this);
                return;
            }
            // Carried things are thrown; with a PLATE in one hand, the other still punches.
            if (held && !Weapon) { Throw(); return; }

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

        /// <summary>Throws what's held. Empty-handed, a downed teammate in reach comes first, then the nearest thing to pick up.</summary>
        void GrabOrThrow()
        {
            if (held) Throw();
            else if (!TryRevive()) TryGrab();
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
            StopReviving();
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
            // Whatever was held, it isn't a raised shield any more.
            raiseStartedAt = -1f;
            if (controller && controller.Health) controller.Health.RaisedShield = null;
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
