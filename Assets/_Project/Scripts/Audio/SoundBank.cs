using UnityEngine;

namespace Wreckabulary
{
    /// <summary>
    /// Recipes for every placeholder sound: toybox wood, typewriter keys and cheerful chimes.
    /// Swap any of them for a recorded clip later by changing <see cref="Sfx.ClipFor"/>.
    /// </summary>
    public static class SoundBank
    {
        const float C5 = 523.25f, E5 = 659.25f, G5 = 783.99f, C6 = 1046.5f, G4 = 392f;

        public static AudioClip Build(Sound s)
        {
            float[] b;
            switch (s)
            {
                case Sound.Clack: // wooden letter landing
                    b = Synth.Buffer(0.09f);
                    Synth.Tone(b, 0f, 0.08f, 620f, 420f, Wave.Sine, 0.7f, 0.001f, 9f);
                    Synth.Tone(b, 0f, 0.05f, 1400f, 1100f, Wave.Triangle, 0.2f, 0.001f, 10f);
                    Synth.Noise(b, 0f, 0.02f, 0.3f, 0.5f, 0.001f, 8f);
                    break;

                case Sound.Pickup: // bright two-note blip
                    b = Synth.Buffer(0.14f);
                    Synth.Tone(b, 0f, 0.06f, 880f, 990f, Wave.Triangle, 0.5f, 0.003f, 3f);
                    Synth.Tone(b, 0.05f, 0.08f, 1320f, 1320f, Wave.Triangle, 0.45f, 0.003f, 5f);
                    break;

                case Sound.Smash: // crunch, thump, and letters rattling apart
                    b = Synth.Buffer(0.45f);
                    Synth.Noise(b, 0f, 0.3f, 0.8f, 0.25f, 0.002f, 5f, 7);
                    Synth.Tone(b, 0f, 0.25f, 150f, 55f, Wave.Sine, 0.9f, 0.002f, 4f);
                    for (int i = 0; i < 4; i++)
                        Synth.Tone(b, 0.06f + i * 0.07f, 0.07f, 700f - i * 60f, 450f - i * 40f, Wave.Sine, 0.4f, 0.001f, 9f);
                    break;

                case Sound.Punch: // whoosh
                    b = Synth.Buffer(0.14f);
                    Synth.Noise(b, 0f, 0.13f, 0.5f, 0.18f, 0.03f, 4f, 3);
                    Synth.Tone(b, 0f, 0.1f, 180f, 360f, Wave.Sine, 0.15f, 0.02f, 3f);
                    break;

                case Sound.Hit: // bonk
                    b = Synth.Buffer(0.22f);
                    Synth.Tone(b, 0f, 0.18f, 260f, 90f, Wave.Sine, 0.9f, 0.001f, 5f);
                    Synth.Tone(b, 0f, 0.08f, 520f, 200f, Wave.Square, 0.25f, 0.001f, 6f);
                    Synth.Noise(b, 0f, 0.05f, 0.4f, 0.4f, 0.001f, 8f, 5);
                    break;

                case Sound.Blocked: // clank
                    b = Synth.Buffer(0.2f);
                    Synth.Tone(b, 0f, 0.18f, 1250f, 1200f, Wave.Square, 0.35f, 0.001f, 7f);
                    Synth.Tone(b, 0f, 0.18f, 1870f, 1820f, Wave.Sine, 0.3f, 0.001f, 7f);
                    break;

                case Sound.Knockout: // boing
                    b = Synth.Buffer(0.6f);
                    Synth.Tone(b, 0f, 0.55f, 520f, 110f, Wave.Sine, 0.8f, 0.005f, 2f, 11f, 0.08f);
                    break;

                case Sound.Grab:
                    b = Synth.Buffer(0.1f);
                    Synth.Tone(b, 0f, 0.09f, 300f, 460f, Wave.Triangle, 0.5f, 0.004f, 4f);
                    break;

                case Sound.PutDown:
                    b = Synth.Buffer(0.14f);
                    Synth.Tone(b, 0f, 0.12f, 230f, 150f, Wave.Sine, 0.7f, 0.002f, 6f);
                    Synth.Noise(b, 0f, 0.04f, 0.2f, 0.3f, 0.001f, 8f, 9);
                    break;

                case Sound.Throw: // rising whoosh
                    b = Synth.Buffer(0.25f);
                    Synth.Noise(b, 0f, 0.24f, 0.5f, 0.2f, 0.06f, 3f, 4);
                    Synth.Tone(b, 0f, 0.2f, 280f, 760f, Wave.Triangle, 0.25f, 0.01f, 3f);
                    break;

                case Sound.SpellOpen: // carriage return
                    b = Synth.Buffer(0.18f);
                    Synth.Tone(b, 0f, 0.16f, 900f, 1500f, Wave.Triangle, 0.3f, 0.005f, 3f);
                    Synth.Noise(b, 0.12f, 0.04f, 0.3f, 0.7f, 0.001f, 8f, 11);
                    break;

                case Sound.SpellAdd: // typewriter key
                    b = Synth.Buffer(0.05f);
                    Synth.Noise(b, 0f, 0.02f, 0.6f, 0.85f, 0.0005f, 9f, 13);
                    Synth.Tone(b, 0f, 0.03f, 2100f, 1800f, Wave.Square, 0.2f, 0.0005f, 9f);
                    Synth.Tone(b, 0f, 0.04f, 300f, 220f, Wave.Sine, 0.4f, 0.0005f, 8f);
                    break;

                case Sound.SpellUndo:
                    b = Synth.Buffer(0.08f);
                    Synth.Tone(b, 0f, 0.07f, 900f, 520f, Wave.Square, 0.25f, 0.002f, 5f);
                    break;

                case Sound.Drop:
                    b = Synth.Buffer(0.14f);
                    Synth.Tone(b, 0f, 0.13f, 660f, 300f, Wave.Triangle, 0.45f, 0.003f, 4f);
                    break;

                case Sound.Cast: // sparkly arpeggio
                    b = Synth.Buffer(0.6f);
                    float[] up = { C5, E5, G5, C6 };
                    for (int i = 0; i < up.Length; i++)
                    {
                        Synth.Tone(b, i * 0.06f, 0.3f, up[i], up[i], Wave.Triangle, 0.45f, 0.004f, 4f);
                        Synth.Tone(b, i * 0.06f, 0.3f, up[i] * 2f, up[i] * 2f, Wave.Sine, 0.15f, 0.004f, 5f);
                    }
                    Synth.Noise(b, 0.2f, 0.3f, 0.12f, 0.9f, 0.01f, 5f, 17);
                    break;

                case Sound.Fizzle: // sad slide
                    b = Synth.Buffer(0.45f);
                    Synth.Tone(b, 0f, 0.4f, 420f, 130f, Wave.Saw, 0.4f, 0.01f, 2f);
                    Synth.Noise(b, 0f, 0.4f, 0.15f, 0.6f, 0.01f, 3f, 19);
                    break;

                case Sound.Join: // hello!
                    b = Synth.Buffer(0.4f);
                    float[] hi = { C5, E5, G5 };
                    for (int i = 0; i < hi.Length; i++) Synth.Tone(b, i * 0.07f, 0.2f, hi[i], hi[i], Wave.Triangle, 0.5f, 0.003f, 4f);
                    break;

                case Sound.Door: // creak and click
                    b = Synth.Buffer(0.45f);
                    Synth.Tone(b, 0f, 0.35f, 170f, 240f, Wave.Saw, 0.2f, 0.05f, 1f, 23f, 0.05f);
                    Synth.Tone(b, 0.36f, 0.06f, 700f, 500f, Wave.Sine, 0.5f, 0.001f, 8f);
                    break;

                case Sound.Countdown:
                    b = Synth.Buffer(0.16f);
                    Synth.Tone(b, 0f, 0.15f, 660f, 660f, Wave.Square, 0.35f, 0.003f, 3f);
                    break;

                case Sound.Go:
                    b = Synth.Buffer(0.4f);
                    Synth.Tone(b, 0f, 0.38f, 990f, 990f, Wave.Square, 0.35f, 0.003f, 2f);
                    Synth.Tone(b, 0f, 0.38f, 1485f, 1485f, Wave.Triangle, 0.25f, 0.003f, 2f);
                    break;

                case Sound.RoundWin: // little fanfare
                    b = Synth.Buffer(1.2f);
                    float[] fan = { G4, C5, E5, G5 };
                    for (int i = 0; i < fan.Length; i++) Synth.Tone(b, i * 0.1f, 0.18f, fan[i], fan[i], Wave.Square, 0.3f, 0.004f, 2f);
                    foreach (var f in new[] { C5, E5, G5, C6 }) Synth.Tone(b, 0.42f, 0.7f, f, f, Wave.Triangle, 0.25f, 0.01f, 2.5f);
                    break;

                case Sound.Collapse: // rumble
                    b = Synth.Buffer(1.3f);
                    Synth.Noise(b, 0f, 1.25f, 0.9f, 0.03f, 0.1f, 2f, 29);
                    Synth.Tone(b, 0f, 1.2f, 70f, 45f, Wave.Sine, 0.6f, 0.1f, 2f);
                    break;

                case Sound.Bees:
                    b = Synth.Buffer(0.7f);
                    Synth.Tone(b, 0f, 0.65f, 230f, 250f, Wave.Saw, 0.3f, 0.05f, 1f, 31f, 0.06f);
                    Synth.Tone(b, 0f, 0.65f, 345f, 360f, Wave.Saw, 0.15f, 0.05f, 1f, 27f, 0.05f);
                    break;

                case Sound.Zap:
                    b = Synth.Buffer(0.3f);
                    Synth.Tone(b, 0f, 0.25f, 1600f, 180f, Wave.Saw, 0.45f, 0.001f, 3f);
                    Synth.Noise(b, 0f, 0.2f, 0.35f, 0.8f, 0.001f, 5f, 37);
                    break;

                case Sound.Boom:
                    b = Synth.Buffer(0.8f);
                    Synth.Noise(b, 0f, 0.7f, 1f, 0.06f, 0.002f, 4f, 41);
                    Synth.Tone(b, 0f, 0.6f, 110f, 38f, Wave.Sine, 1f, 0.002f, 3f);
                    break;

                case Sound.Quack:
                    b = Synth.Buffer(0.2f);
                    Synth.Tone(b, 0f, 0.18f, 620f, 380f, Wave.Saw, 0.4f, 0.01f, 3f, 40f, 0.04f);
                    Synth.Tone(b, 0f, 0.18f, 930f, 570f, Wave.Square, 0.15f, 0.01f, 3f);
                    break;

                case Sound.Placed: // bell
                    b = Synth.Buffer(0.7f);
                    Synth.Tone(b, 0f, 0.65f, C6, C6, Wave.Sine, 0.5f, 0.002f, 4f);
                    Synth.Tone(b, 0f, 0.5f, C6 * 1.5f, C6 * 1.5f, Wave.Sine, 0.25f, 0.002f, 5f);
                    Synth.Tone(b, 0.08f, 0.55f, G5 * 2f, G5 * 2f, Wave.Sine, 0.2f, 0.002f, 5f);
                    break;

                case Sound.Stars:
                    b = Synth.Buffer(1.3f);
                    for (int i = 0; i < 3; i++)
                    {
                        float f = new[] { E5, G5, C6 }[i];
                        Synth.Tone(b, i * 0.2f, 0.5f, f, f, Wave.Sine, 0.45f, 0.002f, 3f);
                        Synth.Tone(b, i * 0.2f, 0.5f, f * 2f, f * 2f, Wave.Sine, 0.2f, 0.002f, 4f);
                    }
                    foreach (var f in new[] { C5, E5, G5 }) Synth.Tone(b, 0.62f, 0.65f, f, f, Wave.Triangle, 0.25f, 0.01f, 2f);
                    break;

                default:
                    b = Synth.Buffer(0.05f);
                    break;
            }
            return Synth.Clip(s.ToString(), b);
        }
    }
}
