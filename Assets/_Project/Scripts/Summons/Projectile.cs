using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>An arrow from BOW or a cannonball from CANNON.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        PlayerController owner;
        string word;
        float damage, knockback, breakPower, blastRadius;
        bool spent;

        /// <param name="damage">Player damage (HP).</param>
        /// <param name="knockback">In shove units, like rules.json.</param>
        /// <param name="breakPower">What it does to furniture.</param>
        public static Projectile Fire(string word, Vector3 from, Vector3 velocity, PlayerController owner,
                                      float damage, float knockback, float breakPower, float blastRadius)
        {
            bool heavy = blastRadius > 0f;
            var size = heavy ? Vector3.one * 0.45f : new Vector3(0.12f, 0.12f, 0.7f);
            var go = new GameObject($"{word} shot");
            go.transform.SetParent(World.Transient, false);
            go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(velocity));
            LetterBlocks.Create(heavy ? "O" : "", size, GameAssets.I.Tinted(heavy ? new Color(0.25f, 0.24f, 0.28f) : new Color(0.55f, 0.36f, 0.2f)),
                                go.transform, true);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = heavy ? 3f : 0.3f;
            rb.useGravity = heavy;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = velocity;

            foreach (var mine in owner.GetComponents<Collider>())
                Physics.IgnoreCollision(go.GetComponentInChildren<Collider>(), mine);

            var p = go.AddComponent<Projectile>();
            p.owner = owner;
            p.word = word;
            p.damage = damage;
            p.knockback = knockback;
            p.breakPower = breakPower;
            p.blastRadius = blastRadius;
            Destroy(go, 3f);
            return p;
        }

        void OnCollisionEnter(Collision c)
        {
            if (spent) return;
            var rb = c.rigidbody;
            if (rb && rb.GetComponent<LetterTile>()) return;
            spent = true;

            if (blastRadius > 0f)
            {
                Explode();
            }
            else if (rb && rb.TryGetComponent(out IDamageable target))
            {
                target.ApplyDamage(Hits.Of(owner, transform.forward, HitSource.Thrown, damage, knockback, 0.2f, breakPower, word));
            }
            Destroy(gameObject);
        }

        void Explode()
        {
            CameraRig.Shake(0.25f);
            Popup.Show("BOOM", transform.position + Vector3.up, Color.white, 4f);
            var seen = new HashSet<Rigidbody>();
            foreach (var col in Physics.OverlapSphere(transform.position, blastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = col.attachedRigidbody;
                if (!rb || !seen.Add(rb)) continue;
                var hit = Hits.Of(owner, rb.position - transform.position, HitSource.Explosion, damage, knockback, 0.3f, breakPower, word);
                if (rb.TryGetComponent(out PlayerHealth victim))
                {
                    if (!owner || victim.gameObject != owner.gameObject) victim.ApplyDamage(hit);
                }
                else
                {
                    if (rb.TryGetComponent(out Smashable smash)) smash.ApplyDamage(hit);
                    if (rb && !rb.isKinematic) rb.AddExplosionForce(8f, transform.position, blastRadius, 0.5f, ForceMode.VelocityChange);
                }
            }
        }
    }
}
