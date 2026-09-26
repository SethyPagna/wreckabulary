using System.Collections;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// A summoned weapon held in the hand. Each attack uses it up a little; when it runs out
    /// it falls apart into its letters. Dropped weapons can be grabbed and used by anyone.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class HeldWeapon : MonoBehaviour
    {
        public string word;
        public int uses = 5;
        public float cooldown = 0.35f;

        [Header("Melee")]
        public float reach = 1.2f;
        public float radius = 0.8f;
        public float knockback = 9f;
        public float damage = 25f;
        public int lettersPerHit = 2;

        [Header("Ranged (BOW, CANNON)")]
        public bool ranged;
        public float projectileSpeed = 18f;
        public float blastRadius;

        public Quaternion HoldRotation => Quaternion.Euler(ranged ? 90f : 65f, 0f, 0f);

        public void Use(PlayerCombat user)
        {
            var owner = user.GetComponent<PlayerController>();
            if (ranged)
            {
                var from = transform.position + owner.Facing * 0.4f;
                Projectile.Fire(word, from, owner.Facing * projectileSpeed, owner, knockback, damage, blastRadius, lettersPerHit);
            }
            else
            {
                user.Strike(reach, radius, knockback, damage, lettersPerHit);
                StartCoroutine(Swing());
            }

            if (--uses > 0) return;
            user.ForgetHeld();
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
