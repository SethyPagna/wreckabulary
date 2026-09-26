using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Anything built from letters: furniture and summoned objects alike.
    /// When it breaks, it bursts into the tiles that spell its word.
    /// </summary>
    public class Smashable : MonoBehaviour
    {
        [SerializeField] string word = "TABLE";
        [SerializeField] float health = 30f;
        [SerializeField] float impactDamageScale = 2f;
        [SerializeField] float minImpactSpeed = 3f;

        public string Word => word;

        public void Init(string newWord) => word = newWord.ToUpperInvariant();

        public void TakeHit(float damage)
        {
            health -= damage;
            if (health <= 0f) Break();
        }

        void OnCollisionEnter(Collision c)
        {
            float speed = c.relativeVelocity.magnitude;
            if (speed > minImpactSpeed) TakeHit((speed - minImpactSpeed) * impactDamageScale);
        }

        public void Break()
        {
            TilePool.Instance.Burst(word, transform.position + Vector3.up * 0.5f);
            // TODO: letter-burst VFX + wooden clack SFX
            Destroy(gameObject);
        }
    }
}
