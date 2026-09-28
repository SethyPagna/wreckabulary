using System;
using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Last Word Standing rule: a hit knocks letters loose.
    /// A hit while holding no letters is a knockout.
    /// </summary>
    [RequireComponent(typeof(LetterInventory))]
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] int lettersPerHit = 2;
        [SerializeField] float invulnerableTime = 0.6f;

        LetterInventory inventory;
        PlayerController controller;
        float invulnerableUntil;

        public bool IsOut { get; private set; }
        public bool IsInvulnerable => Time.time < invulnerableUntil;

        /// <summary>ARMOR: hits absorbed before letters start falling off.</summary>
        public int ArmorCharges { get; set; }
        /// <summary>SHIELD: hits from the front are blocked until this time.</summary>
        public float FrontBlockUntil { get; set; }
        public int LettersPerHit { get => lettersPerHit; set => lettersPerHit = value; }
        /// <summary>Creative: hits still push you around, but never cost letters or knock you out.</summary>
        public bool Harmless { get; set; }

        public event Action<PlayerHealth> KnockedOut;
        /// <summary>Victim, attacker (may be null).</summary>
        public event Action<PlayerHealth, PlayerController> Hit;

        void Awake()
        {
            inventory = GetComponent<LetterInventory>();
            controller = GetComponent<PlayerController>();
        }

        /// <summary>Returns true if the hit landed (not blocked, not invulnerable).</summary>
        public bool TakeHit(Vector3 direction, float knockback = 6f, int letters = -1, PlayerController attacker = null)
        {
            if (IsOut || IsInvulnerable) return false;
            direction = World.Flat(direction);
            direction = direction.sqrMagnitude > 0.001f ? direction.normalized : -controller.Facing;

            if (Time.time < FrontBlockUntil && Vector3.Dot(controller.Facing, -direction) > 0.2f)
            {
                controller.Knock(direction * knockback * 0.3f, 0.1f);
                Popup.Show("BLOCKED", controller.OverheadPosition, Color.white, 2.5f);
                Sfx.Play(Sound.Blocked, transform.position);
                return false;
            }

            invulnerableUntil = Time.time + invulnerableTime;

            if (ArmorCharges > 0)
            {
                ArmorCharges--;
                controller.Knock(direction * knockback * 0.5f, 0.2f);
                Popup.Show("ARMOR!", controller.OverheadPosition, Color.white, 2.5f);
                Sfx.Play(Sound.Blocked, transform.position);
                return false;
            }

            controller.Knock(direction * knockback + Vector3.up * knockback * 0.35f, 0.45f);
            CameraRig.Shake(0.12f);
            if (Harmless)
            {
                Sfx.Play(Sound.Hit, transform.position, 0.5f);
                return true;
            }

            if (inventory.IsEmpty)
            {
                IsOut = true;
                controller.SetKnockedOut(true, direction * knockback);
                Popup.Show("WRECKED!", controller.OverheadPosition, controller.Color, 5f);
                Sfx.Play(Sound.Knockout, transform.position);
                CameraRig.Shake(0.3f);
                Hit?.Invoke(this, attacker);
                KnockedOut?.Invoke(this);
                return true;
            }

            Sfx.Play(Sound.Hit, transform.position);
            inventory.DropRandom(letters < 0 ? lettersPerHit : letters, transform.position, direction);
            Hit?.Invoke(this, attacker);
            return true;
        }

        public void ResetForRound()
        {
            IsOut = false;
            ArmorCharges = 0;
            FrontBlockUntil = 0f;
            invulnerableUntil = 0f;
        }
    }
}
