using System;
using UnityEngine;
using Wreckabulary.Audio;
using Wreckabulary.Rules.Sound;

namespace Wreckabulary
{
    [CreateAssetMenu(menuName = "Wreckabulary/Sound bank")]
    public sealed class GameSoundBank : ScriptableObject
    {
        public const GameCue StingCue = GameCue.Boost;
        const string TickCue = "Tick";

        [SerializeField] AudioClip[] pickup = Array.Empty<AudioClip>();
        [SerializeField] AudioClip[] craft = Array.Empty<AudioClip>();
        [SerializeField] AudioClip[] dodge = Array.Empty<AudioClip>();

        static AudioSource tickSpeaker;
        static AudioClip tickClip;

        public void Configure(AudioClip[] pickupClips, AudioClip[] craftClips, AudioClip[] dodgeClips)
        {
            pickup = pickupClips ?? Array.Empty<AudioClip>();
            craft = craftClips ?? Array.Empty<AudioClip>();
            dodge = dodgeClips ?? Array.Empty<AudioClip>();
        }

        public bool TryNext(GameCue cue, int previous, out AudioClip clip, out int index)
        {
            var variants = cue switch { GameCue.Pickup => pickup, GameCue.Craft => craft, GameCue.Dodge => dodge, _ => null };
            clip = null; index = -1;
            if (variants == null || variants.Length == 0) return false;
            for (int offset = 1; offset <= variants.Length; offset++)
            {
                int candidate = (Mathf.Max(-1, previous) + offset) % variants.Length;
                if (!variants[candidate]) continue;
                clip = variants[candidate]; index = candidate; return true;
            }
            return false;
        }

        public static void Sting() => GameFeedback.Play(StingCue);

        public static bool Tick()
        {
            if (GameFeedback.Muted || AudioListener.volume <= 0f || AudioListener.pause) return false;
            if (!tickSpeaker)
            {
                var root = new GameObject("Card ticks");
                DontDestroyOnLoad(root);
                tickSpeaker = root.AddComponent<AudioSource>();
                tickSpeaker.playOnAwake = false; tickSpeaker.spatialBlend = 0f; tickSpeaker.volume = GameMix.HouseSpeaker;
            }
            if (!tickClip)
            {
                var samples = ToyCueSynth.Render(TickCue);
                tickClip = AudioClip.Create("Card tick", samples.Length, 1, ToyCueSynth.Rate, false);
                tickClip.SetData(samples, 0);
            }
            float gain = GameMix.TickLevel * GameMix.CueTrim;
            tickSpeaker.PlayOneShot(tickClip, gain);
            GameMix.Record(tickClip, tickSpeaker.volume * gain, 1f);
            return true;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ForgetTicks()
        {
            tickSpeaker = null;
            tickClip = null;
        }
    }
}
