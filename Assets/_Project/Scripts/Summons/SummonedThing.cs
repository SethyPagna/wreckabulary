using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// A summon that lasts a while (SHIELD, SKATES, MAGNET…). When it wears out it falls apart
    /// into the letters it was spelled from, so anyone can grab them and spell again.
    /// </summary>
    public class SummonedThing : MonoBehaviour
    {
        static readonly List<SummonedThing> All = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => All.Clear();

        public string Word;
        public PlayerController Owner;
        public float Expires = float.MaxValue;
        /// <summary>Returns false to end early (e.g. armor used up).</summary>
        public Func<bool> KeepAlive;
        public Action Tick;
        public Action Ended;

        bool ended;

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public static SummonedThing Attach(GameObject go, string word, PlayerController owner, float duration)
        {
            var s = go.AddComponent<SummonedThing>();
            s.Word = word;
            s.Owner = owner;
            s.Expires = Time.time + duration;
            return s;
        }

        void Update()
        {
            if (ended) return;
            bool ownerGone = Owner && Owner.IsKnockedOut;
            if (Time.time >= Expires || ownerGone || (KeepAlive != null && !KeepAlive()))
            {
                FallApart();
                return;
            }
            Tick?.Invoke();
        }

        public void FallApart()
        {
            if (ended) return;
            ended = true;
            Ended?.Invoke();
            if (TryGetComponent(out Smashable smash)) { smash.Break(); return; }

            var pool = TilePool.Instance;
            if (pool)
            {
                var built = GetComponent<LetterBuilt>();
                if (built && built.Blocks.Count == Word.Length) pool.BurstFrom(built.Blocks, Word, transform.position, 3f);
                else pool.Burst(Word, transform.position + Vector3.up * 0.5f, 3f);
            }
            Destroy(gameObject);
        }

        /// <summary>Removes every summon without dropping letters (round reset).</summary>
        public static void ClearAll()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                All[i].ended = true;
                Destroy(All[i].gameObject);
            }
        }
    }
}
