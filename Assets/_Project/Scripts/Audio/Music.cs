using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary
{
    public enum Track { Cozy, Brawl, Bouncy }

    /// <summary>
    /// Background music, written by a small sequencer on first use: a chord progression with bass,
    /// arpeggios, a simple melody and light drums, looping seamlessly. Crossfades between scenes.
    /// </summary>
    public class Music : MonoBehaviour
    {
        static Music instance;
        static readonly Dictionary<Track, AudioClip> Clips = new();
        const float Volume = 0.32f;

        AudioSource a, b;
        public static Track? Playing { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            instance = null;
            Playing = null;
        }

        static Music Instance
        {
            get
            {
                if (!instance)
                {
                    var go = new GameObject("Music");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Music>();
                    instance.a = instance.MakeSource();
                    instance.b = instance.MakeSource();
                }
                return instance;
            }
        }

        AudioSource MakeSource()
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = true;
            s.playOnAwake = false;
            s.volume = 0f;
            return s;
        }

        /// <summary>Starts a track, crossfading from whatever is playing. Does nothing if it's already on.</summary>
        public static void Play(Track track)
        {
            if (!Application.isPlaying || Playing == track) return;
            Playing = track;
            var m = Instance;
            m.StopAllCoroutines();
            (m.a, m.b) = (m.b, m.a);
            m.a.clip = ClipFor(track);
            m.a.Play();
            m.StartCoroutine(m.Crossfade());
        }

        IEnumerator Crossfade()
        {
            float fromStart = b.volume;
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.8f)
            {
                a.volume = Volume * t;
                b.volume = fromStart * (1f - t);
                yield return null;
            }
            a.volume = Volume;
            b.volume = 0f;
            b.Stop();
        }

        public static AudioClip ClipFor(Track t)
        {
            if (!Clips.TryGetValue(t, out var clip) || !clip) Clips[t] = clip = Compose(t);
            return clip;
        }

        // ---- Composition ----

        struct Style
        {
            public float bpm;
            public int[][] chords;    // MIDI notes per bar, root first
            public Wave bass, arp, lead;
            public bool snare;
            public int seed;
        }

        static Style StyleFor(Track t) => t switch
        {
            // C – Am – F – G, gentle.
            Track.Cozy => new Style
            {
                bpm = 92, bass = Wave.Sine, arp = Wave.Sine, lead = Wave.Triangle, seed = 3,
                chords = new[] { new[] { 60, 64, 67 }, new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 } },
            },
            // Am – F – C – G, driving.
            Track.Brawl => new Style
            {
                bpm = 132, bass = Wave.Square, arp = Wave.Triangle, lead = Wave.Square, snare = true, seed = 7,
                chords = new[] { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 60, 64, 67 }, new[] { 55, 59, 62 } },
            },
            // F – C – Dm – Bb, bouncy.
            _ => new Style
            {
                bpm = 116, bass = Wave.Triangle, arp = Wave.Triangle, lead = Wave.Sine, snare = true, seed = 11,
                chords = new[] { new[] { 53, 57, 60 }, new[] { 60, 64, 67 }, new[] { 50, 53, 57 }, new[] { 58, 62, 65 } },
            },
        };

        static AudioClip Compose(Track track)
        {
            var st = StyleFor(track);
            const int bars = 8;
            float beat = 60f / st.bpm, bar = beat * 4f;
            var buf = Synth.Buffer(bars * bar);
            var rng = new System.Random(st.seed);

            for (int i = 0; i < bars; i++)
            {
                var chord = st.chords[i % st.chords.Length];
                float t0 = i * bar;

                // Bass: root on 1 and 3, fifth on the "and" of 4.
                Synth.Tone(buf, t0, beat * 1.8f, Synth.Midi(chord[0] - 24), Synth.Midi(chord[0] - 24), st.bass, 0.35f, 0.01f, 2f, wrap: true);
                Synth.Tone(buf, t0 + beat * 2f, beat * 1.4f, Synth.Midi(chord[0] - 24), Synth.Midi(chord[0] - 24), st.bass, 0.3f, 0.01f, 2f, wrap: true);
                Synth.Tone(buf, t0 + beat * 3.5f, beat * 0.5f, Synth.Midi(chord[2] - 24), Synth.Midi(chord[2] - 24), st.bass, 0.2f, 0.01f, 3f, wrap: true);

                // Arpeggio in eighths.
                for (int e = 0; e < 8; e++)
                {
                    int note = chord[e % 3] + (e >= 4 ? 12 : 0);
                    Synth.Tone(buf, t0 + e * beat * 0.5f, beat * 0.45f, Synth.Midi(note), Synth.Midi(note), st.arp, 0.09f, 0.005f, 4f, wrap: true);
                }

                // Melody: chord tones on the beat, a passing step now and then; rest on the last bar of each phrase.
                if (i % 4 != 3)
                    for (int q = 0; q < 4; q++)
                    {
                        if (rng.NextDouble() < 0.25) continue;
                        int note = chord[rng.Next(3)] + 12;
                        if (rng.NextDouble() < 0.3) note += rng.Next(2) == 0 ? 2 : -1;
                        float len = beat * (rng.NextDouble() < 0.3 ? 1.8f : 0.9f);
                        Synth.Tone(buf, t0 + q * beat, len, Synth.Midi(note), Synth.Midi(note), st.lead, 0.12f, 0.01f, 2.5f, 5f, 0.004f, true);
                    }

                // Drums.
                for (int q = 0; q < 4; q++)
                {
                    float tq = t0 + q * beat;
                    if (q % 2 == 0) Synth.Tone(buf, tq, 0.14f, 120f, 45f, Wave.Sine, 0.5f, 0.001f, 5f, wrap: true);
                    if (st.snare && q % 2 == 1) Synth.Noise(buf, tq, 0.12f, 0.2f, 0.5f, 0.001f, 6f, 100 + i * 4 + q, true);
                    Synth.Noise(buf, tq + beat * 0.5f, 0.03f, 0.06f, 0.95f, 0.001f, 8f, 200 + i * 4 + q, true);
                }
            }
            return Synth.Clip($"Music_{track}", buf, 0.8f);
        }
    }
}
