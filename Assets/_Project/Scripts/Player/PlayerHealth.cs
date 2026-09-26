using System;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Last Word Standing rule: a hit knocks letters loose.
    /// A hit while holding no letters is a knockout.
    /// </summary>
    [RequireComponent(typeof(LetterInventory))]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] int lettersPerHit = 2;

        LetterInventory inventory;
        public bool IsOut { get; private set; }
        public event Action<PlayerHealth> KnockedOut;

        void Awake() => inventory = GetComponent<LetterInventory>();

        public void TakeHit(Vector3 direction)
        {
            if (IsOut) return;
            if (inventory.IsEmpty)
            {
                IsOut = true;
                // TODO: switch to full ragdoll, slow-mo, "WRECKED!" callout
                KnockedOut?.Invoke(this);
                return;
            }
            inventory.DropRandom(lettersPerHit, transform.position, direction);
        }

        public void ResetForRound() => IsOut = false;
    }
}
