using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// The word wheel. Hold spell to open it, step through the words you can make,
    /// and release to summon the selected one. Moving is slowed while spelling.
    /// </summary>
    [RequireComponent(typeof(LetterInventory))]
    public class Summoner : MonoBehaviour
    {
        [SerializeField] WordDatabase database;
        [SerializeField] float moveScaleWhileSpelling = 0.35f;
        [SerializeField] int maxHints = 3;

        LetterInventory inventory;
        PlayerController controller;

        public bool IsSpelling { get; private set; }
        /// <summary>Words that can be summoned right now, best first.</summary>
        public List<WordEntry> Ready { get; private set; } = new();
        /// <summary>Near misses shown greyed out, with the letters still needed.</summary>
        public List<(WordEntry entry, string missing)> Hints { get; private set; } = new();
        public int Selected { get; private set; }
        public WordEntry SelectedWord => Selected < Ready.Count ? Ready[Selected] : null;

        public event Action<string> Summoned;

        WordDatabase Database => database ? database : GameAssets.I.words;

        void Awake()
        {
            inventory = GetComponent<LetterInventory>();
            controller = GetComponent<PlayerController>();
            inventory.Changed += () => { if (IsSpelling) Refresh(); };
        }

        void Update()
        {
            if (!controller.CanAct)
            {
                Close();
                return;
            }
            var c = controller.Commands;
            if (c.spellDown) Open();
            if (!IsSpelling) return;

            if (c.up) Step(-1);
            if (c.down) Step(1);
            if (c.grab) { Close(); return; }
            if (c.spellUp || !c.spellHeld)
            {
                var word = SelectedWord;
                Close();
                if (word != null) Summon(word);
            }
        }

        public void Open()
        {
            IsSpelling = true;
            Selected = 0;
            Refresh();
            controller.MoveScale = moveScaleWhileSpelling;
        }

        public void Close()
        {
            if (!IsSpelling) return;
            IsSpelling = false;
            controller.MoveScale = 1f;
        }

        void Refresh()
        {
            var keep = SelectedWord;
            Ready = WordSolver.Spellable(Database.Words, inventory.Letters);
            var hints = WordSolver.Hints(Database.Words, inventory.Letters, inventory.Capacity);
            Hints = hints.GetRange(0, Mathf.Min(maxHints, hints.Count));
            Selected = keep != null && Ready.Contains(keep) ? Ready.IndexOf(keep) : 0;
        }

        void Step(int delta)
        {
            if (Ready.Count == 0) return;
            Selected = (Selected + delta + Ready.Count) % Ready.Count;
        }

        public bool Summon(string word)
        {
            var entry = Database.Find(word);
            return entry != null && Summon(entry);
        }

        public bool Summon(WordEntry entry)
        {
            if (entry == null || !inventory.TrySpend(entry.word)) return false;
            SummonEffects.Apply(controller, entry);
            Summoned?.Invoke(entry.word);
            return true;
        }
    }
}
