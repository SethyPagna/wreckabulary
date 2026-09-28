using UnityEngine;

namespace Wreckabulary
{
    /// <summary>An arrow from BOW or a cannonball from CANNON.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        PlayerController owner;
        float knockback, damage, blastRadius;
        int letters;
        bool spent;

        public static Projectile Fire(string word, Vector3 from, Vector3 velocity, PlayerController owner,
                                      float knockback, float damage, float blastRadius, int letters)
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
            p.knockback = knockback;
            p.damage = damage;
            p.blastRadius = blastRadius;
            p.letters = letters;
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
            else if (rb)
            {
                if (rb.TryGetComponent(out PlayerHealth victim)) victim.TakeHit(transform.forward, knockback, letters, owner);
                else if (rb.TryGetComponent(out Smashable smash)) smash.TakeHit(damage);
            }
            Destroy(gameObject);
        }

        void Explode()
        {
            CameraRig.Shake(0.25f);
            Sfx.Play(Sound.Boom, transform.position);
            Popup.Show("BOOM", transform.position + Vector3.up, Color.white, 4f);
            foreach (var col in Physics.OverlapSphere(transform.position, blastRadius, ~0, QueryTriggerInteraction.Ignore))
            {
                var rb = col.attachedRigidbody;
                if (!rb) continue;
                var dir = rb.position - transform.position;
                if (rb.TryGetComponent(out PlayerHealth victim))
                {
                    if (victim.gameObject != owner.gameObject) victim.TakeHit(dir, knockback, letters, owner);
                }
                else
                {
                    if (rb.TryGetComponent(out Smashable smash)) smash.TakeHit(damage);
                    if (rb && !rb.isKinematic) rb.AddExplosionForce(8f, transform.position, blastRadius, 0.5f, ForceMode.VelocityChange);
                }
            }
        }
    }
}
