using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>A physical letter tile that can be picked up, carried and dropped.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class LetterTile : MonoBehaviour
    {
        [SerializeField] char letter = 'A';
        [SerializeField] TextMeshPro[] labels;
        [SerializeField] MeshRenderer body;

        public char Letter => letter;
        public LetterRarity Rarity => LetterScores.RarityOf(letter);
        public Rigidbody Body { get; private set; }

        float readyAt;
        LetterInventory droppedBy;
        float ownerLockUntil;

        void Awake() => Body = GetComponent<Rigidbody>();

        public void SetLetter(char c)
        {
            letter = char.ToUpperInvariant(c);
            foreach (var label in labels) label.text = letter.ToString();
            if (body) body.sharedMaterial = GameAssets.I.TileMaterial(Rarity);
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
            droppedBy = from;
            ownerLockUntil = Time.time + 1.2f;
        }
    }
}
