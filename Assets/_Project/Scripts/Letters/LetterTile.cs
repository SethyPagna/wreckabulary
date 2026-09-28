using UnityEngine;

namespace Wreckabulary
{
    /// <summary>A loose 3D letter that can be picked up, carried and dropped. Pooled: the letter changes on reuse.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class LetterTile : MonoBehaviour
    {
        [SerializeField] char letter = 'A';
        [SerializeField] MeshFilter meshFilter;
        [SerializeField] MeshRenderer body;
        [SerializeField] BoxCollider box;
        [SerializeField] float height = 0.42f;
        [SerializeField] float thickness = 0.2f;

        public char Letter => letter;
        public LetterRarity Rarity => LetterScores.RarityOf(letter);
        public Rigidbody Body { get; private set; }
        public float LaunchedAt { get; private set; }

        float readyAt;
        LetterInventory droppedBy;
        float ownerLockUntil;

        void Awake() => Body = GetComponent<Rigidbody>();

        public void SetLetter(char c)
        {
            letter = char.ToUpperInvariant(c);
            var mesh = GameAssets.I.LetterMesh(letter);
            if (mesh)
            {
                // Uniform scale on the letter face keeps its shape; the mesh sits centred on the tile's origin.
                var b = mesh.bounds;
                var scale = new Vector3(height / b.size.y, height / b.size.y, thickness / b.size.z);
                meshFilter.sharedMesh = mesh;
                meshFilter.transform.localScale = scale;
                meshFilter.transform.localPosition = -Vector3.Scale(scale, b.center);
                box.size = Vector3.Scale(scale, b.size);
                box.center = Vector3.zero;
            }
            if (body) body.sharedMaterial = GameAssets.I.TileMaterial(Rarity);
        }

        void OnCollisionEnter(Collision c)
        {
            float speed = c.relativeVelocity.magnitude;
            if (speed > 2.5f) Sfx.Play(Sound.Clack, transform.position, Mathf.Clamp01(speed / 8f), 0.9f + Random.value * 0.3f);
        }

        /// <summary>Freshly launched tiles can't be grabbed straight away, and never instantly by whoever dropped them.</summary>
        public bool CanBeCollectedBy(LetterInventory who) =>
            Time.time >= readyAt && (who != droppedBy || Time.time >= ownerLockUntil);

        /// <summary>Called when a player collects this tile.</summary>
        public void Collect() => TilePool.Instance.Release(this);

        /// <summary>Launches the tile out into the world.</summary>
        public void Launch(Vector3 position, Vector3 velocity, LetterInventory from = null)
        {
            transform.SetPositionAndRotation(position, Random.rotation);
            Body.position = position;
            Body.linearVelocity = velocity;
            Body.angularVelocity = Random.insideUnitSphere * 8f;
            readyAt = Time.time + 0.3f;
            LaunchedAt = Time.time;
            droppedBy = from;
            ownerLockUntil = Time.time + 1.2f;
        }
    }
}
