using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    public enum WordCategory { Weapon, Defence, Movement, Chaos }

    [Serializable]
    public class WordEntry
    {
        public string word;
        public WordCategory category;
        public GameObject prefab;
        [Tooltip("Hidden words are not shown in the word wheel until discovered.")]
        public bool hidden;
        public int Score => LetterScores.ScoreOf(word);
    }

    [CreateAssetMenu(menuName = "Wreckabulary/Word Database")]
    public class WordDatabase : ScriptableObject
    {
        public List<WordEntry> words = new();
    }
}
