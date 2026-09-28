using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Wreckabulary
{
    public enum Sound
    {
        Clack, Pickup, Smash, Punch, Hit, Blocked, Knockout, Grab, PutDown, Throw,
        SpellOpen, SpellAdd, SpellUndo, Drop, Cast, Fizzle,
        Join, Door, Countdown, Go, RoundWin, Collapse,
        Bees, Zap, Boom, Quack, Placed, Stars,
    }

    /// <summary>
    /// Plays sound effects. Every sound is synthesised on first use (see <see cref="SoundBank"/>), so there are
    /// no audio files to import. Sounds pan left and right by where they happen. M mutes everything.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        const int Voices = 20;
        static Sfx instance;
        static readonly Dictionary<Sound, AudioClip> Clips = new();
        static readonly Dictionary<Sound, float> LastPlayed = new();

        /// <summary>Most recent sounds, newest last (for tests).</summary>
        public static readonly List<Sound> History = new();
        public static bool Muted { get; private set; }

        readonly AudioSource[] voices = new AudioSource[Voices];
        int next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            History.Clear();
            LastPlayed.Clear();
            Muted = false;
        }

        static Sfx Instance
        {
            get
            {
                if (!instance)
                {
                    var go = new GameObject("Sfx");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Sfx>();
                }
                return instance;
            }
        }

        void Awake()
        {
            for (int i = 0; i < Voices; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                voices[i] = s;
            }
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.mKey.wasPressedThisFrame) SetMuted(!Muted);
        }

        public static void SetMuted(bool muted)
        {
            Muted = muted;
            AudioListener.volume = muted ? 0f : 1f;
        }

        public static AudioClip ClipFor(Sound s)
        {
            if (!Clips.TryGetValue(s, out var clip) || !clip) Clips[s] = clip = SoundBank.Build(s);
            return clip;
        }

        /// <summary>Plays a sound, panned by where it happened. Rapid repeats of the same sound are thinned out.</summary>
        public static void Play(Sound s, Vector3? at = null, float volume = 1f, float pitch = 1f)
        {
            if (!Application.isPlaying) return;
            float minGap = s is Sound.Clack or Sound.Pickup or Sound.SpellAdd ? 0.03f : 0.01f;
            if (LastPlayed.TryGetValue(s, out float last) && Time.unscaledTime - last < minGap) return;
            LastPlayed[s] = Time.unscaledTime;

            History.Add(s);
            if (History.Count > 64) History.RemoveAt(0);

            var sfx = Instance;
            var voice = sfx.voices[sfx.next];
            sfx.next = (sfx.next + 1) % Voices;
            voice.clip = ClipFor(s);
            voice.volume = Mathf.Clamp01(volume) * 0.8f;
            voice.pitch = pitch * Random.Range(0.95f, 1.05f);
            voice.panStereo = at.HasValue ? Mathf.Clamp(at.Value.x / 9f, -0.8f, 0.8f) : 0f;
            voice.Play();
        }
    }
}
