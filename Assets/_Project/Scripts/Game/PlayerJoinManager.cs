using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Wreckabulary
{
    /// <summary>Drop-in join: press a button on any keyboard half or gamepad to spawn a roommate.</summary>
    public class PlayerJoinManager : MonoBehaviour
    {
        [SerializeField] PlayerController playerPrefab;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] Transform playersRoot;
        [SerializeField] int maxPlayers = 4;

        readonly List<PlayerController> players = new();
        readonly KeyboardBinding keyboardLeft = new(KeyboardBinding.Side.Left);
        readonly KeyboardBinding keyboardRight = new(KeyboardBinding.Side.Right);

        public IReadOnlyList<PlayerController> Players => players;
        public bool AllowJoining { get; set; } = true;
        public event Action<PlayerController> Joined;

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
            if (players.Any(p => p.Binding == binding)) return;
            if (binding.JoinPressed()) Join(binding);
        }

        public PlayerController Join(InputBinding binding)
        {
            if (players.Count >= maxPlayers) return null;
            int index = players.Count;
            var prefab = playerPrefab ? playerPrefab : GameAssets.I.playerPrefab;
            var p = Instantiate(prefab, SpawnPoint(index), Quaternion.identity, playersRoot);
            p.Setup(index, binding);
            p.Respawn(SpawnPoint(index));
            players.Add(p);
            Popup.Show($"{p.Name} joined!", p.OverheadPosition, p.Color, 4f);
            Joined?.Invoke(p);
            return p;
        }

        public Vector3 SpawnPoint(int index) =>
            spawnPoints != null && spawnPoints.Length > 0 ? spawnPoints[index % spawnPoints.Length].position : new Vector3(index * 2f - 3f, 0f, -2f);

        public bool AnyStartPressed() => players.Any(p => p.Binding != null && p.Binding.StartPressed());
    }
}
