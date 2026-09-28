using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Wreckabulary
{
    /// <summary>
    /// Drop-in join: press a button on any keyboard half or gamepad to spawn a roommate.
    /// Roommates who joined in an earlier scene (see <see cref="Session"/>) are brought back automatically.
    /// </summary>
    public class PlayerJoinManager : MonoBehaviour
    {
        [SerializeField] PlayerController playerPrefab;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] Transform playersRoot;
        [SerializeField] int maxPlayers = 4;

        [Header("Arrival")]
        [Tooltip("If set, new roommates walk this way on arrival (through the front door in the house).")]
        [SerializeField] Vector3 walkIn;
        [SerializeField] float walkInTime = 0.5f;
        [SerializeField] int starterLetters = 3;

        [Header("Knockouts")]
        [SerializeField] bool respawnKnockedOut = true;
        [SerializeField] float respawnDelay = 2f;

        readonly List<PlayerController> players = new();
        readonly KeyboardBinding keyboardLeft = new(KeyboardBinding.Side.Left);
        readonly KeyboardBinding keyboardRight = new(KeyboardBinding.Side.Right);

        public IReadOnlyList<PlayerController> Players => players;
        public bool AllowJoining { get; set; } = true;
        /// <summary>When true, knocked-out roommates get back up after a short delay (house, tutorial, lobby).</summary>
        public bool RespawnKnockedOut { get => respawnKnockedOut; set => respawnKnockedOut = value; }
        /// <summary>True if the players were carried over from another scene.</summary>
        public bool RestoredFromSession { get; private set; }
        public int StarterLetters { get => starterLetters; set => starterLetters = Mathf.Clamp(value, 0, 6); }
        public event Action<PlayerController> Joined;

        void Awake()
        {
            // Restore in Awake so every other script's Start already sees the players.
            foreach (var b in Session.Bindings.ToArray())
            {
                if (b is GamepadBinding g && !g.Pad.added) continue;
                Join(b, arriving: false);
                RestoredFromSession = true;
            }
        }

        void Update()
        {
            if (!AllowJoining || players.Count >= maxPlayers) return;

            TryJoin(keyboardLeft);
            TryJoin(keyboardRight);
            foreach (var pad in Gamepad.all)
                if (players.All(p => p.Binding is not GamepadBinding g || g.Pad != pad))
                {
                    var binding = new GamepadBinding(pad);
                    if (binding.JoinPressed()) Join(binding);
                }
        }

        void TryJoin(InputBinding binding)
        {
            // Compare by id: bindings restored from the session are different objects for the same keys.
            if (players.Any(p => p.Binding.Id == binding.Id)) return;
            if (binding.JoinPressed()) Join(binding);
        }

        public PlayerController Join(InputBinding binding) => Join(binding, arriving: true);

        PlayerController Join(InputBinding binding, bool arriving)
        {
            if (players.Count >= maxPlayers) return null;
            int index = players.Count;
            var prefab = playerPrefab ? playerPrefab : GameAssets.I.playerPrefab;
            var p = Instantiate(prefab, SpawnPoint(index), Quaternion.identity, playersRoot);
            p.Setup(index, binding);
            players.Add(p);
            Session.Remember(binding);
            p.Health.KnockedOut += _ => { if (respawnKnockedOut) StartCoroutine(RespawnLater(p)); };
            Place(p);
            if (arriving)
            {
                Popup.Show($"{p.Name} joined!", p.OverheadPosition, p.Color, 4f);
                Sfx.Play(Sound.Join, p.transform.position);
            }
            Joined?.Invoke(p);
            return p;
        }

        /// <summary>Puts a roommate at their spawn point with starter letters, walking in if set up to.</summary>
        public void Place(PlayerController p)
        {
            p.Combat.ResetForRound();
            p.Summoner.Close();
            p.Health.ResetForRound();
            p.Respawn(SpawnPoint(p.Index));
            GiveStarterLetters(p);
            if (walkIn.sqrMagnitude > 0.01f)
            {
                p.FaceTowards(walkIn);
                p.AutoWalk(walkIn.normalized, walkInTime);
            }
        }

        IEnumerator RespawnLater(PlayerController p)
        {
            yield return new WaitForSeconds(respawnDelay);
            if (p && p.IsKnockedOut && respawnKnockedOut) Place(p);
        }

        public Vector3 SpawnPoint(int index) =>
            spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints[index % spawnPoints.Length].position : new Vector3(index * 2f - 3f, 0f, -2f);

        public bool AnyStartPressed() => players.Any(p => p.Binding != null && p.Binding.StartPressed());

        /// <summary>One vowel, then common consonants, so the first hit isn't a knockout.</summary>
        public void GiveStarterLetters(PlayerController p)
        {
            const string vowels = "AAEEIOU";
            const string common = "RSTLNDBMPW";
            var s = new char[starterLetters];
            for (int i = 0; i < s.Length; i++)
                s[i] = i == 0 ? vowels[UnityEngine.Random.Range(0, vowels.Length)] : common[UnityEngine.Random.Range(0, common.Length)];
            p.Inventory.Set(new string(s));
        }
    }
}
