using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Marks something as flying: a thrown chair, a thrown player, or a falling box during the collapse.
    /// If it slams into a player (other than the thrower) it counts as a hit.
    /// </summary>
    public class ThrowTracker : MonoBehaviour
    {
        PlayerController thrower;
        float until;
        float knockback;

        public static void Attach(GameObject go, PlayerController thrower, float seconds, float knockback = 8f)
        {
            if (!go.TryGetComponent(out ThrowTracker t)) t = go.AddComponent<ThrowTracker>();
            t.thrower = thrower;
            t.until = Time.time + seconds;
            t.knockback = knockback;
        }

        void Update()
        {
            if (Time.time > until) Destroy(this);
        }

        void OnCollisionEnter(Collision c)
        {
            var rb = c.rigidbody;
            if (!rb || c.relativeVelocity.magnitude < 4f) return;
            if (!rb.TryGetComponent(out PlayerHealth victim)) return;
            if (thrower && victim.gameObject == thrower.gameObject) return;
            if (victim.gameObject == gameObject) return;

            victim.TakeHit(rb.position - transform.position, knockback, -1, thrower);
            Destroy(this);
        }
    }
}
