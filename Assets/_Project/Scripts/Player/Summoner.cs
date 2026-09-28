using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Spelling. Press spell to start: your letters appear over your head. Move left/right to pick one,
    /// add it to the word, undo if you slip, and press spell again to cast. If it isn't a word it fizzles
    /// and you keep your letters. You stand still while spelling, so pick your moment.
    /// </summary>
    [RequireComponent(typeof(LetterInventory))]
    [DefaultExecutionOrder(-30)] // before combat, so a spelling key press isn't also a punch or a grab
    public class Summoner : MonoBehaviour
    {
        [SerializeField] WordDatabase database;
        [SerializeField] int maxHints = 3;

        LetterInventory inventory;
        PlayerController controller;
        readonly List<int> picked = new();
        readonly List<char> pickedLetters = new();

        public bool IsSpelling { get; private set; }
        /// <summary>True on the frame spelling ended, so that key press isn't reused.</summary>
        public bool JustClosed => closedFrame == Time.frameCount;
        int closedFrame = -1;
        /// <summary>Index of the highlighted letter in the inventory, or -1 if every letter is used.</summary>
        public int Cursor { get; private set; }
        /// <summary>Inventory indices added to the word so far, in order.</summary>
        public IReadOnlyList<int> Picked => picked;
        public string Spelled => new(pickedLetters.ToArray());
        /// <summary>The word being spelled, if it's a real one.</summary>
        public WordEntry Match => Find(Spelled);
        /// <summary>Words you could still finish from here with the letters you hold, best first.</summary>
        public List<WordEntry> Hints { get; private set; } = new();

        public event Action<string> Summoned;
        public event Action<string> Fizzled;

        /// <summary>Set by a mode to replace the word list, e.g. Moving Day's checklist.</summary>
        public IReadOnlyList<WordEntry> WordsOverride { get; set; }

        IReadOnlyList<WordEntry> Words => WordsOverride ?? (database ? database : GameAssets.I.words).Words;

        void Awake()
        {
            inventory = GetComponent<LetterInventory>();
            controller = GetComponent<PlayerController>();
            inventory.Changed += OnLettersChanged;
        }

        void Update()
        {
            if (!controller.CanAct)
            {
                Close();
                return;
            }
            var c = controller.Commands;
            if (!IsSpelling)
            {
                if (c.spellDown) Open();
                return;
            }

            if (c.left) Move(-1);
            if (c.right) Move(1);
            if (c.confirm) Add();
            if (c.back) Undo();
            if (c.down) DropHighlighted();
            if (c.spellDown) Cast();
        }

        public void Open()
        {
            IsSpelling = true;
            picked.Clear();
            pickedLetters.Clear();
            Cursor = -1;
            MoveToFree(0, 1);
            Refresh();
            controller.MoveScale = 0f;
            Sfx.Play(Sound.SpellOpen, transform.position, 0.6f);
        }

        public void Close()
        {
            if (!IsSpelling) return;
            IsSpelling = false;
            closedFrame = Time.frameCount;
            picked.Clear();
            pickedLetters.Clear();
            controller.MoveScale = 1f;
        }

        /// <summary>Moves the highlight to the next letter not already in the word.</summary>
        public void Move(int dir)
        {
            int n = inventory.Count;
            if (n == 0) return;
            int start = Cursor < 0 ? 0 : Cursor + dir;
            MoveToFree(((start % n) + n) % n, dir);
        }

        void MoveToFree(int from, int dir)
        {
            int n = inventory.Count;
            for (int k = 0; k < n; k++)
            {
                int i = (((from + k * dir) % n) + n) % n;
                if (!picked.Contains(i)) { Cursor = i; return; }
            }
            Cursor = -1;
        }

        /// <summary>Adds the highlighted letter to the word.</summary>
        public void Add()
        {
            if (Cursor < 0 || picked.Contains(Cursor)) return;
            picked.Add(Cursor);
            pickedLetters.Add(inventory.Letters[Cursor]);
            Sfx.Play(Sound.SpellAdd, transform.position, 0.8f, 1f + pickedLetters.Count * 0.05f);
            MoveToFree(Cursor, 1);
            Refresh();
        }

        /// <summary>Drops the highlighted letter on the floor to make room. The word being spelled is kept.</summary>
        public bool DropHighlighted()
        {
            int i = Cursor;
            if (i < 0 || i >= inventory.Count) return false;
            // Letters after the dropped one shift down a slot.
            for (int k = 0; k < picked.Count; k++)
                if (picked[k] > i) picked[k]--;
            inventory.DropAt(i, transform.position, controller.Facing);
            Sfx.Play(Sound.Drop, transform.position);
            Cursor = -1;
            if (inventory.Count > 0) MoveToFree(Mathf.Min(i, inventory.Count - 1), 1);
            Refresh();
            return true;
        }

        /// <summary>Takes back the last letter, or stops spelling if the word is empty.</summary>
        public void Undo()
        {
            if (picked.Count == 0) { Close(); return; }
            Sfx.Play(Sound.SpellUndo, transform.position, 0.6f);
            Cursor = picked[^1];
            picked.RemoveAt(picked.Count - 1);
            pickedLetters.RemoveAt(pickedLetters.Count - 1);
            Refresh();
        }

        /// <summary>Summons the spelled word if it's real; otherwise it fizzles and nothing is spent.</summary>
        public bool Cast()
        {
            string word = Spelled;
            var entry = Match;
            Close();
            if (word.Length == 0) return false;
            if (entry != null) return Summon(entry);

            Popup.Show($"{word}? not a word", controller.OverheadPosition + Vector3.up * 0.6f, new Color(1f, 1f, 1f, 0.8f), 3.5f);
            Fizzled?.Invoke(word);
            Sfx.Play(Sound.Fizzle, transform.position);
            return false;
        }

        /// <summary>Spells a whole word in one go (tests, bots). Uses the same letters and rules.</summary>
        public bool Summon(string word)
        {
            var entry = Find(word);
            return entry != null && Summon(entry);
        }

        public bool Summon(WordEntry entry)
        {
            if (entry == null || !inventory.TrySpend(entry.word)) return false;
            Sfx.Play(Sound.Cast, transform.position);
            SummonEffects.Apply(controller, entry);
            Summoned?.Invoke(entry.word);
            return true;
        }

        WordEntry Find(string word)
        {
            if (string.IsNullOrEmpty(word)) return null;
            word = word.ToUpperInvariant();
            foreach (var entry in Words)
                if (entry.word == word) return entry;
            return null;
        }

        void Refresh()
        {
            string prefix = Spelled;
            Hints = WordSolver.Spellable(Words, inventory.Letters)
                .Where(w => w.word.StartsWith(prefix) && w.word != prefix)
                .Take(maxHints)
                .ToList();
        }

        /// <summary>Letters picked up or knocked loose mid-spell: keep the word if its letters are still there.</summary>
        void OnLettersChanged()
        {
            if (!IsSpelling) return;
            for (int k = 0; k < picked.Count; k++)
            {
                int i = picked[k];
                if (i >= inventory.Count || inventory.Letters[i] != pickedLetters[k])
                {
                    picked.Clear();
                    pickedLetters.Clear();
                    break;
                }
            }
            if (Cursor >= inventory.Count || Cursor < 0 || picked.Contains(Cursor)) MoveToFree(0, 1);
            Refresh();
        }
    }
}
