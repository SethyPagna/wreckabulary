using System;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Anything built from letters: furniture and summoned objects alike.
    /// When it breaks, it bursts into the tiles that spell its word.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Smashable : MonoBehaviour
    {
        [SerializeField] string word = "TABLE";
        [SerializeField] float health = 30f;
        [Tooltip("Collision impulse below this does no damage (walking into things, settling on the floor).")]
        [SerializeField] float impactThreshold = 6f;
        [SerializeField] float impactDamageScale = 2.5f;
        [Tooltip("Seconds after spawning when impacts do no damage, so dropped boxes survive landing.")]
        [SerializeField] float spawnGrace = 1.5f;

        public string Word => word;
        public float Health => health;
        public bool IsBroken { get; private set; }
        public event Action<Smashable> Broken;

        float graceUntil;

        void OnEnable() => graceUntil = Time.time + spawnGrace;

        public void Init(string newWord, float newHealth = -1f)
        {
            word = newWord.ToUpperInvariant();
            if (newHealth > 0f) health = newHealth;
        }

        public void TakeHit(float damage)
        {
            if (IsBroken) return;
            health -= damage;
            if (health <= 0f) Break();
        }

        void OnCollisionEnter(Collision c)
        {
            if (Time.time < graceUntil) return;
            var other = c.rigidbody;
            if (other)
            {
                if (other.GetComponent<LetterTile>()) return;
                // Players walking into furniture shouldn't wreck it; thrown players should.
                if (other.GetComponent<PlayerController>() && !other.GetComponent<ThrowTracker>()) return;
            }
            float impulse = c.impulse.magnitude;
            if (impulse > impactThreshold) TakeHit((impulse - impactThreshold) * impactDamageScale);
        }

        public void Break()
        {
            if (IsBroken) return;
            IsBroken = true;

            var pool = TilePool.Instance;
            if (pool)
            {
                var centre = TryGetComponent(out Rigidbody rb) ? rb.worldCenterOfMass : transform.position;
                if (TryGetComponent(out LetterBuilt built) && built.Blocks.Count == word.Length)
                    pool.BurstFrom(built.Blocks, word, centre);
                else
                    pool.Burst(word, centre + Vector3.up * 0.3f);
            }
            CameraRig.Shake(0.08f);
            // TODO: letter-burst VFX + wooden clack SFX
            Broken?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
