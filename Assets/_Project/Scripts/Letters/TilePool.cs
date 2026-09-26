using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Reuses letter tiles instead of instantiating and destroying them.</summary>
    public class TilePool : MonoBehaviour
    {
        public static TilePool Instance { get; private set; }

        [SerializeField] LetterTile tilePrefab;
        [SerializeField] int prewarm = 64;

        readonly Stack<LetterTile> free = new();

        void Awake()
        {
            Instance = this;
            for (int i = 0; i < prewarm; i++) Release(Instantiate(tilePrefab, transform));
        }

        public LetterTile Get(char letter)
        {
            var t = free.Count > 0 ? free.Pop() : Instantiate(tilePrefab, transform);
            t.gameObject.SetActive(true);
            t.SetLetter(letter);
            return t;
        }

        public void Release(LetterTile t)
        {
            t.gameObject.SetActive(false);
            free.Push(t);
        }

        /// <summary>Bursts the letters of a word out from a point.</summary>
        public void Burst(string letters, Vector3 origin, float force = 4f)
        {
            foreach (char c in letters)
            {
                var dir = (Random.insideUnitSphere + Vector3.up * 1.2f).normalized;
                Get(c).Launch(origin + dir * 0.3f, dir * force);
            }
        }
    }
}
