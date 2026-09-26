using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>Runs rounds: last player standing wins, first to 3 wins the match.</summary>
    public class RoundManager : MonoBehaviour
    {
        [SerializeField] List<PlayerHealth> players = new();
        [SerializeField] int roundsToWin = 3;
        [SerializeField] float collapseAfter = 90f;

        readonly Dictionary<PlayerHealth, int> wins = new();
        float timer;
        bool collapsing;

        public event Action<PlayerHealth> RoundWon;
        public event Action<PlayerHealth> MatchWon;
        public event Action CollapseStarted;

        void Start()
        {
            foreach (var p in players)
            {
                wins[p] = 0;
                p.KnockedOut += OnKnockedOut;
            }
            StartRound();
        }

        void Update()
        {
            timer += Time.deltaTime;
            if (!collapsing && timer >= collapseAfter)
            {
                collapsing = true;
                // TODO: shrink the room or rain letters to force fights
                CollapseStarted?.Invoke();
            }
        }

        void StartRound()
        {
            timer = 0f;
            collapsing = false;
            foreach (var p in players) p.ResetForRound();
            // TODO: reset room, respawn furniture and players
        }

        void OnKnockedOut(PlayerHealth _)
        {
            var alive = players.Where(p => !p.IsOut).ToList();
            if (alive.Count > 1) return;
            var winner = alive.FirstOrDefault();
            if (winner == null) { StartRound(); return; }
            wins[winner]++;
            RoundWon?.Invoke(winner);
            if (wins[winner] >= roundsToWin) MatchWon?.Invoke(winner);
            else StartRound();
        }
    }
}
