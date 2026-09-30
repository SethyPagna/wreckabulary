using System.Collections;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// A summoned weapon held in the hand. Each attack uses it up a little; when it runs out
    /// it falls apart into its letters. Dropped weapons can be grabbed and used by anyone.
    /// A shield (PLATE) isn't swung: it's raised with block, and blocked damage wears it down.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class HeldWeapon : MonoBehaviour
    {
        /// <summary>
        /// Converts a summon's furniture damage to player damage. It puts the old words in line
        /// with the catalogue: SWORD 22 next to BLADE 24, CANNON 45 next to BOMB 45.
        /// </summary>
        public const float HealthPerDamage = 0.9f;

        public string word;
        public int uses = 5;
        public float cooldown = 0.35f;

        [Header("Melee (the summon's own numbers: furniture damage, push in m/s)")]
        public float reach = 1.2f;
        public float radius = 0.8f;
        public float knockback = 9f;
        public float damage = 25f;

        [Header("Ranged (BOW, CANNON)")]
        public bool ranged;
        public float projectileSpeed = 18f;
        public float blastRadius;

        MeleeStats stats;
        bool fromCatalogue;
        ItemDefinition item;
        bool lookedUp;
        float durabilityLeft = -1f;

        /// <summary>This word's items.json entry, or null for summon-only words (SWORD, CANNON, …).</summary>
        ItemDefinition Item
        {
            get
            {
                if (!lookedUp)
                {
                    lookedUp = true;
                    GameConfig.Current.Items.TryGet(word, out item);
                }
                return item;
            }
        }

        /// <summary>The block a raised shield gives (items.json), or null for things that can't block.</summary>
        public ShieldStats Shield => Item?.Shield;

        /// <summary>A shield rides at the side, edge on; weapons point forward and up.</summary>
        public Quaternion HoldRotation => Shield != null ? Quaternion.Euler(0f, 80f, 0f) : Quaternion.Euler(ranged ? 90f : 65f, 0f, 0f);

        /// <summary>Durability left: items.json's for catalogue items, 1 for summon-only words.</summary>
        public float DurabilityLeft
        {
            get
            {
                if (durabilityLeft < 0f) durabilityLeft = Item != null && Item.Durability > 0 ? Item.Durability : 1f;
                return durabilityLeft;
            }
        }

        /// <summary>
        /// What a swing does. Catalogue weapons (BAT, BLADE) use items.json; other summoned
        /// words convert their own numbers.
        /// </summary>
        public MeleeStats Stats
        {
            get
            {
                if (stats != null) return stats;
                if (Item != null && Item.Melee != null)
                {
                    fromCatalogue = true;
                    return stats = Item.Melee;
                }
                return stats = new MeleeStats
                {
                    Damage = PlayerDamage,
                    // The old swing hit a sphere of this radius, this far ahead; reach is measured to the target's surface.
                    Reach = reach + radius - 0.4f,
                    ArcDegrees = 100f,
                    Recovery = cooldown,
                    Knockback = Knockback,
                    BreakPower = BreakPower,
                    HitStun = 0.25f,
                };
            }
        }

        /// <summary>Seconds between attacks: the catalogue's swing timing, or the summon's cooldown.</summary>
        public float Cooldown
        {
            get
            {
                var s = Stats;
                return fromCatalogue ? s.Cycle : cooldown;
            }
        }

        float PlayerDamage => Mathf.Round(damage * HealthPerDamage);
        float Knockback => knockback / Hits.KnockbackSpeed;
        float BreakPower => damage / Smashable.HealthPerBreakPower;

        public void Use(PlayerCombat user)
        {
            var owner = user.GetComponent<PlayerController>();
            if (ranged)
            {
                var from = transform.position + owner.Facing * 0.4f;
                Projectile.Fire(word, from, owner.Facing * projectileSpeed, owner, PlayerDamage, Knockback, BreakPower, blastRadius);
            }
            else
            {
                user.Strike(Stats, word);
                StartCoroutine(Swing());
            }

            if (--uses > 0) return;
            Break(user);
        }

        /// <summary>Blocked damage wears a shield down. Returns true if that wore it through and it fell apart.</summary>
        public bool Wear(float amount, PlayerCombat user)
        {
            durabilityLeft = DurabilityLeft - amount;
            if (durabilityLeft > 0f) return false;
            Popup.Show("CRACK!", transform.position + Vector3.up * 0.6f, Color.white, 3f);
            Break(user);
            return true;
        }

        /// <summary>Falls apart into its letters, out of the holder's hand.</summary>
        public void Break(PlayerCombat user)
        {
            if (user) user.ForgetHeld();
            transform.SetParent(World.Transient, true);
            var pool = TilePool.Instance;
            if (pool)
            {
                var built = GetComponent<LetterBuilt>();
                if (built) pool.BurstFrom(built.Blocks, word, transform.position, 3f);
                else pool.Burst(word, transform.position, 3f);
            }
            Destroy(gameObject);
        }

        /// <summary>A raised shield turns flat-on to the front; lowered, it goes back to the side.</summary>
        public void ShowRaised(bool raised) =>
            transform.localRotation = raised ? Quaternion.identity : HoldRotation;

        IEnumerator Swing()
        {
            var rest = HoldRotation;
            for (float t = 0f; t < 1f; t += Time.deltaTime / 0.18f)
            {
                transform.localRotation = rest * Quaternion.Euler(Mathf.Sin(t * Mathf.PI) * 70f, 0f, 0f);
                yield return null;
            }
            transform.localRotation = rest;
        }
    }
}
