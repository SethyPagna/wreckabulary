using System;
using UnityEngine;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    /// <summary>A disposable peaceful roommate. The original Hub player and binding are never changed.</summary>
    public sealed class HomeTourDirector : MonoBehaviour
    {
        public PlayerController Player { get; private set; }
        /// <summary>A mouse or a controller turns the view: the tour follows the roommate from behind, as in matches.</summary>
        public bool ThirdPerson { get; private set; }
        Camera lens;
        readonly ShoulderView view = new();
        public void Begin(HouseLayout house, InputBinding binding, Camera camera)
        {
            lens = camera;
            if (!GameAssets.I.playerPrefab) throw new InvalidOperationException("The roommate prefab is unavailable.");
            Player = Instantiate(GameAssets.I.playerPrefab, transform);
            Player.Setup(0, CreateBinding(binding));
            if (Player.Combat) Player.Combat.enabled = false;
            if (Player.Summoner) Player.Summoner.enabled = false;
            if (Player.Inventory) Player.Inventory.enabled = false;
            if (Player.Health) Player.Health.enabled = false;
            foreach (var hud in Player.GetComponentsInChildren<PlayerHud>(true)) hud.gameObject.SetActive(false);
            var spawn = house.Spawns[0];
            Player.Respawn(new Vector3(spawn.X, house.Room(spawn.Room).FloorY + .08f, spawn.Z));
            Player.Frozen = false;
            ThirdPerson = Player.Binding.CanLook;
            if (ThirdPerson) { Player.ShooterView = true; Player.ResetLook(); view.Snap(); }
            if (lens) lens.rect = new Rect(0f,0f,1f,1f);
        }
        void OnDisable() { if (ThirdPerson) CursorPolicy.Apply(false); }
        void LateUpdate()
        {
            if (!Player || !lens) return;
            if (ThirdPerson)
            {
                view.Place(lens, Player.transform.position, Player.LookYaw, Player.LookPitch, Time.unscaledDeltaTime);
                // The tour owns the cursor while it runs; Esc or START ends it and gives the pointer back.
                CursorPolicy.Apply(CursorPolicy.WantsLock(Player.Binding.ReadsMouse, false, Application.isFocused));
                return;
            }
            var centre = Player.transform.position; centre.y = 0f;
            lens.orthographic = true;
            // Keep the roommate readable on phones, including a narrow portrait viewport.
            lens.orthographicSize = Mathf.Max(6.5f, 3.5f / Mathf.Max(.2f,lens.aspect));
            lens.transform.position = centre + new Vector3(0f,30f,-22f); lens.transform.LookAt(centre);
        }
        /// <summary>Keep peaceful-tour input filtering independent of a spawned player or camera.</summary>
        public static InputBinding CreateBinding(InputBinding source) => new TourBinding(source);
        sealed class TourBinding : InputBinding
        {
            readonly InputBinding source;
            public TourBinding(InputBinding source) { this.source = source ?? DesktopBinding.Shared; }
            public override string Id => "home-tour:" + source.Id;
            public override bool CanLook => source.CanLook;
            public override bool ReadsMouse => source.ReadsMouse;
            public override void Read(ref PlayerCommands c)
            {
                var original = default(PlayerCommands); source.Read(ref original);
                // Merge the source's touch overlay before filtering. The distinct binding ID prevents
                // PlayerController from merging the unfiltered touch commands a second time.
                if (source is not TouchBinding && TouchBinding.Shared.IsOverlayFor(source.Id))
                    TouchBinding.Shared.Merge(ref original);
                // Movement/aim/jump only. Even a craft, attack or grab input cannot start a gameplay action.
                c.move = original.move; c.look = original.look; c.lookDelta = original.lookDelta; c.jump = original.jump;
                c.pointer = original.pointer; c.aimAtPointer = original.aimAtPointer;
            }
            public override bool JoinPressed() => false;
            public override bool StartPressed() => false;
        }
    }
}
