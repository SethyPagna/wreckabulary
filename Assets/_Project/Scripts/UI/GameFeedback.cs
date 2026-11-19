using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Audio;
using Wreckabulary.Rules.Sound;

namespace Wreckabulary
{
    public enum GameCue { Pickup, Break, Craft, Hit, Block, Jump, Dodge, Swing, Out, Protect, Boost, Blast, Heal }

    public static class GameFeedback
    {
        static AudioSource speaker;
        static readonly Dictionary<GameCue, AudioClip[]> clips = new();
        static readonly Dictionary<GameCue, int> lastVariant = new();
        static readonly Dictionary<AudioClip, float> levels = new();
        public static event System.Action<GameCue, AudioClip> Played;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt("wv.muted", 0) != 0;
            set { PlayerPrefs.SetInt("wv.muted", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            foreach (var variants in clips.Values) foreach (var clip in variants) if (clip) UnityEngine.Object.Destroy(clip);
            clips.Clear(); lastVariant.Clear(); levels.Clear(); speaker = null; Played = null;
        }

        public static void Play(GameCue cue)
            => PlayInternal(cue, GameSoundPacks.Selected, false);

        public static bool Preview(GameCue cue, string pack)
            => (pack == "default" || pack == "winter") && PlayInternal(cue, pack, true);

        static bool PlayInternal(GameCue cue, string pack, bool preview)
        {
            if (Muted || AudioListener.volume <= 0f || AudioListener.pause) return false;
            if (!speaker)
            {
                var root = new GameObject("House sound cues");
                UnityEngine.Object.DontDestroyOnLoad(root);
                speaker = root.AddComponent<AudioSource>();
                speaker.playOnAwake = false; speaker.spatialBlend = 0f; speaker.volume = GameMix.HouseSpeaker;
            }
            bool bankClip = GameSoundPacks.TryPick(cue, pack, preview, out var clip, out int variant);
            int pitched = -1;
            if (!bankClip)
            {
                var variants = Variants(cue);
                pitched = GameMix.NextVariant(variants.Length, lastVariant.TryGetValue(cue, out int previous) ? previous : -1);
                clip = variants[pitched];
            }
            if (!GameMix.AdmitHouse((int)cue, PriorityOf(cue), clip)) return false;
            float scale = Level(cue, clip) * GameMix.CueTrim * GameMix.NextGain();
            if (preview) GameMix.HoldFullLevel(clip.length);
            speaker.PlayOneShot(clip, scale);
            GameMix.Record(clip, speaker.volume * scale, 1f);
            if (pitched >= 0) lastVariant[cue] = pitched;
            if (bankClip) GameSoundPacks.Played(cue, preview, variant);
            Played?.Invoke(cue, clip);
            return true;
        }

        public static CuePriority PriorityOf(GameCue cue) => cue switch
        {
            GameCue.Craft or GameCue.Out or GameCue.Heal or GameCue.Protect or GameCue.Boost or GameCue.Blast => CuePriority.High,
            GameCue.Jump or GameCue.Dodge or GameCue.Swing => CuePriority.Low,
            _ => CuePriority.Normal
        };

        static AudioClip[] Variants(GameCue cue)
        {
            if (clips.TryGetValue(cue, out var made)) return made;
            made = new AudioClip[SoundJitter.Pitches.Length];
            for (int i = 0; i < made.Length; i++)
            {
                var samples = ToyCueSynth.Render(cue.ToString(), ToyCueSynth.Rate, SoundJitter.Pitches[i]);
                made[i] = AudioClip.Create("House " + cue, samples.Length, 1, ToyCueSynth.Rate, false);
                made[i].SetData(samples, 0);
                levels[made[i]] = HouseCueMix.Scale(cue.ToString(), Loudness.MomentaryMax(samples, ToyCueSynth.Rate));
            }
            return clips[cue] = made;
        }

        public static float Level(GameCue cue, AudioClip clip)
        {
            if (!clip) return 1f;
            if (levels.TryGetValue(clip, out float level)) return level;
            var samples = new float[clip.samples * clip.channels];
            double loudness = double.NaN;
            if (clip.GetData(samples, 0))
            {
                var mono = clip.channels == 1 ? samples : new float[clip.samples];
                if (clip.channels > 1)
                    for (int i = 0; i < clip.samples; i++) mono[i] = samples[i * clip.channels];
                loudness = Loudness.MomentaryMax(mono, clip.frequency);
            }
            return levels[clip] = HouseCueMix.Scale(cue.ToString(), loudness);
        }

        public static void Burst(string model, Vector3 position, float size = 0.4f)
        {
            FeedbackBurst.Play(model, position, size);
        }

        public static void Burst(string model, Vector3 position, float size, Color tint, float seconds = 0.45f) =>
            FeedbackBurst.Play(model, position, size, tint, seconds);

        public static Color SkillColor(string word) => word switch
        {
            "FOAM" => new Color(.43f, .88f, 1f),
            "SOAP" => new Color(.40f, .84f, .91f),
            "BED" => new Color(.74f, .60f, 1f),
            "MAT" => new Color(.48f, .92f, .62f),
            "BOMB" => new Color(1f, .42f, .22f),
            "PLATE" => new Color(1f, .83f, .42f),
            "SHIELD" => new Color(1f, .83f, .42f),
            "APPLE" => new Color(.53f, .96f, .46f),
            "WATER" => new Color(.35f, .83f, 1f),
            "CAKE" => new Color(1f, .60f, .77f),
            "SODA" => new Color(.75f, 1f, .35f),
            "FAN" => new Color(.64f, .97f, 1f),
            "CLOCK" => new Color(.76f, .59f, 1f),
            "PIE" => new Color(1f, .91f, .69f),
            "STOOL" => new Color(.87f, .67f, 1f),
            _ => new Color(1f, .77f, .37f)
        };
    }

}
