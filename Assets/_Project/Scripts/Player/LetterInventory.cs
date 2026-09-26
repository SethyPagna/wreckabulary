using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>The letters a player carries. They are both ammo and health.</summary>
    public class LetterInventory : MonoBehaviour
    {
        [SerializeField] int capacity = 6;

        readonly List<char> letters = new();
        public IReadOnlyList<char> Letters => letters;
        public bool IsEmpty => letters.Count == 0;
        public bool IsFull => letters.Count >= capacity;

        public event Action Changed;

        public bool TryAdd(char c)
        {
            if (IsFull) return false;
            letters.Add(char.ToUpperInvariant(c));
            Changed?.Invoke();
            return true;
        }

        /// <summary>Removes the letters of a word. Returns false if they are not all held.</summary>
        public bool TrySpend(string word)
        {
            if (!WordSolver.CanSpell(WordSolver.Count(letters), word)) return false;
            foreach (char c in word) letters.Remove(char.ToUpperInvariant(c));
            Changed?.Invoke();
            return true;
        }

        /// <summary>Knocks random letters loose into the world.</summary>
        public void DropRandom(int count, Vector3 from, Vector3 hitDirection)
        {
            for (int i = 0; i < count && letters.Count > 0; i++)
            {
                int idx = UnityEngine.Random.Range(0, letters.Count);
                char c = letters[idx];
                letters.RemoveAt(idx);
                var dir = (hitDirection.normalized + Vector3.up + UnityEngine.Random.insideUnitSphere * 0.5f).normalized;
                TilePool.Instance.Get(c).Launch(from + Vector3.up, dir * 5f);
            }
            Changed?.Invoke();
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out LetterTile tile) && TryAdd(tile.Letter)) tile.Collect();
        }
    }
}
