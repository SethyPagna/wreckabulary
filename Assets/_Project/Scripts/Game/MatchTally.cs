using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>
    /// Counts what the local player does in a match (damage dealt, gear crafted) and adds the
    /// finished match to their <see cref="Career"/>. Every match is practice for now: there is
    /// no online play yet.
    /// </summary>
    public sealed class MatchTally : MonoBehaviour
    {
        public const string CareerKey = "wv.career";
        static MatchTally current;
        readonly HashSet<PlayerController> watched = new HashSet<PlayerController>();
        PlayerController local;
        float damage;
        int crafted;

        /// <summary>The last finished match, for the lobby to announce once.</summary>
        public static MatchRecord LastResult { get; set; }
        public static bool LastWasBest { get; private set; }

        public static Career LoadCareer() => Career.Deserialize(PlayerPrefs.GetString(CareerKey, ""));

        public static void SaveCareer(Career career)
        {
            PlayerPrefs.SetString(CareerKey, career.Serialize());
            PlayerPrefs.Save();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
        }

        static void OnScene(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == Session.HubScene || scene.name == Session.TutorialScene) return;
            if (!FindAnyObjectByType<PlayerJoinManager>()) return;
            current = new GameObject("Match tally").AddComponent<MatchTally>();
        }

        void Update()
        {
            foreach (var player in World.Players)
            {
                if (!player || watched.Contains(player)) continue;
                watched.Add(player);
                if (player.Health) player.Health.Damaged += OnDamaged;
                // Scripted players are tests: they never pay into the real career.
                if (!local && player.Binding is not BotBinding && player.Binding is not ScriptedBinding)
                {
                    local = player;
                    if (player.Summoner) player.Summoner.Summoned += OnSummoned;
                }
            }
        }

        void OnDestroy()
        {
            foreach (var player in watched)
                if (player && player.Health) player.Health.Damaged -= OnDamaged;
            if (local && local.Summoner) local.Summoner.Summoned -= OnSummoned;
            if (current == this) current = null;
        }

        void OnDamaged(PlayerHealth victim, HitInfo hit, HitResult result)
        {
            if (local && hit.AttackerId == local.Index && victim != local.Health) damage += result.Damage;
        }

        void OnSummoned(string word) => crafted++;

        /// <summary>Called once by the mode when a match ends; "play again" then counts afresh.</summary>
        public static void Finish(bool won)
        {
            if (!current) return;
            current.Record(won);
        }

        /// <summary>For Dibs and Duos: whether the winner is on the local player's team.</summary>
        public static void FinishFor(PlayerController winner)
        {
            if (!current) return;
            current.Record(winner && current.local && winner.Team == current.local.Team);
        }

        void Record(bool won)
        {
            if (!local) return;
            var career = LoadCareer();
            var record = Career.Reward(Match.Mode, Session.MapId, true, won, 0, crafted, Mathf.RoundToInt(damage),
                DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            LastWasBest = career.Record(record);
            LastResult = record;
            SaveCareer(career);
            // "Play again" in the same scene is a new match.
            damage = 0; crafted = 0;
        }
    }
}
