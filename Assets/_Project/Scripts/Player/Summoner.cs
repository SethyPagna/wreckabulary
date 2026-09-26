using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Opens the word wheel and summons the chosen word as an object.</summary>
    [RequireComponent(typeof(LetterInventory))]
    public class Summoner : MonoBehaviour
    {
        [SerializeField] WordDatabase database;
        [SerializeField] Transform hand;

        LetterInventory inventory;
        public List<WordEntry> Options { get; private set; } = new();

        void Awake() => inventory = GetComponent<LetterInventory>();

        /// <summary>Call when the spell button is pressed to refresh the wheel.</summary>
        public List<WordEntry> OpenWheel()
        {
            Options = WordSolver.Spellable(database, inventory.Letters);
            return Options;
        }

        public bool Summon(WordEntry entry)
        {
            if (entry == null || !inventory.TrySpend(entry.word)) return false;
            var go = Instantiate(entry.prefab, hand.position, hand.rotation);
            // Summoned objects are made of letters too, so they can be smashed back into tiles.
            if (!go.TryGetComponent(out Smashable smash)) smash = go.AddComponent<Smashable>();
            smash.Init(entry.word);
            // TODO: summon-slam VFX, attach weapons to hand
            return true;
        }
    }
}
