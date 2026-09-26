using TMPro;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>A physical letter tile that can be picked up, carried and dropped.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class LetterTile : MonoBehaviour
    {
        [SerializeField] char letter = 'A';
        [SerializeField] TextMeshPro label;
        [SerializeField] Renderer body;
        [SerializeField] Material commonMat, rareMat, legendaryMat;

        public char Letter => letter;
        public LetterRarity Rarity => LetterScores.RarityOf(letter);
        public Rigidbody Body { get; private set; }

        void Awake() => Body = GetComponent<Rigidbody>();

        public void SetLetter(char c)
        {
            letter = char.ToUpperInvariant(c);
            if (label) label.text = letter.ToString();
            if (body)
                body.sharedMaterial = Rarity switch
                {
                    LetterRarity.Legendary => legendaryMat,
                    LetterRarity.Rare => rareMat,
                    _ => commonMat
                };
        }

        /// <summary>Called when a player collects this tile.</summary>
        public void Collect() => TilePool.Instance.Release(this);

        /// <summary>Launches the tile out into the world.</summary>
        public void Launch(Vector3 position, Vector3 impulse)
        {
            transform.SetPositionAndRotation(position, Random.rotation);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.AddForce(impulse, ForceMode.Impulse);
            Body.AddTorque(Random.insideUnitSphere * 2f, ForceMode.Impulse);
        }
    }
}
