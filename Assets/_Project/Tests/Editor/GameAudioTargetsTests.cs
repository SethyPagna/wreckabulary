using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Audio;
using Wreckabulary.Rules.Sound;

namespace Wreckabulary.EditorTests
{
    public sealed class GameAudioTargetsTests
    {
        sealed class Target
        {
            public double Momentary, Tolerance, Peak, Harsh;
            public bool Loop;
        }

        static readonly Dictionary<string, Target> Targets = new()
        {
            ["winter"] = new Target { Momentary = -13, Tolerance = 1.5, Peak = -3, Harsh = -20 },
            ["ambience-call"] = new Target { Momentary = -19.5, Tolerance = 2, Peak = -6, Harsh = -22 },
            ["ambience-bed"] = new Target { Momentary = -20.5, Tolerance = 3, Peak = -6, Harsh = -34, Loop = true },
        };

        static string Project => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        static string Root => Path.Combine(Project, "Assets/_Project");

        static IEnumerable<(string path, string category)> Clips()
        {
            foreach (string file in Directory.GetFiles(Path.Combine(Root, "Audio/Winter"), "*.wav")) yield return (file, "winter");
            foreach (string file in Directory.GetFiles(Path.Combine(Root, "Resources/Ambience"), "*.wav", SearchOption.AllDirectories))
                yield return (file, Path.GetFileName(file).StartsWith("loop", StringComparison.OrdinalIgnoreCase) ? "ambience-bed" : "ambience-call");
        }

        static float[] ReadWav(string path, out int rate)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4), path);
            int channels = 0, bits = 0, at = 12;
            rate = 0;
            while (at + 8 <= bytes.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(bytes, at, 4);
                int size = BitConverter.ToInt32(bytes, at + 4);
                if (id == "fmt ")
                {
                    Assert.AreEqual(1, BitConverter.ToInt16(bytes, at + 8), path + " is PCM");
                    channels = BitConverter.ToInt16(bytes, at + 10);
                    rate = BitConverter.ToInt32(bytes, at + 12);
                    bits = BitConverter.ToInt16(bytes, at + 22);
                }
                else if (id == "data")
                {
                    Assert.AreEqual(16, bits, path);
                    Assert.AreEqual(1, channels, path + " is mono");
                    var samples = new float[size / 2];
                    for (int i = 0; i < samples.Length; i++) samples[i] = BitConverter.ToInt16(bytes, at + 8 + i * 2) / 32768f;
                    return samples;
                }
                at += 8 + size + (size & 1);
            }
            Assert.Fail(path + " has no data chunk");
            return null;
        }

        static Biquad Section(double hz, int rate, double q, bool high)
        {
            double w = 2 * Math.PI * hz / rate, c = Math.Cos(w), alpha = Math.Sin(w) / (2 * q), a0 = 1 + alpha;
            return high
                ? new Biquad((1 + c) / 2 / a0, -(1 + c) / a0, (1 + c) / 2 / a0, -2 * c / a0, (1 - alpha) / a0)
                : new Biquad((1 - c) / 2 / a0, (1 - c) / a0, (1 - c) / 2 / a0, -2 * c / a0, (1 - alpha) / a0);
        }

        static double HarshBand(float[] x, int rate)
        {
            var chain = new[]
            {
                Section(2000, rate, .5411961, true), Section(2000, rate, 1.3065630, true),
                Section(6000, rate, .5411961, false), Section(6000, rate, 1.3065630, false),
            };
            var band = new float[x.Length];
            for (int i = 0; i < x.Length; i++)
            {
                double y = x[i];
                for (int k = 0; k < chain.Length; k++) y = chain[k].Process(y);
                band[i] = (float)y;
            }
            return Loudness.MomentaryMax(band, rate);
        }

        static string Sha(string path)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        [Serializable] sealed class ImpactManifest { public ImpactFile[] files; }
        [Serializable] sealed class ImpactFile { public string path, sha256; }

        [Test]
        public void BeltPinnedImpactClipsKeepTheirOriginalBytes()
        {
            var manifest = JsonUtility.FromJson<ImpactManifest>(File.ReadAllText(Path.Combine(Project, "ArtSource/Audio/ImpactV1/synth-02/manifest.json")));
            Assert.AreEqual(189, manifest.files.Length);
            foreach (var file in manifest.files)
                Assert.AreEqual(file.sha256, Sha(Path.Combine(Root, "Audio/Impact/Clips", file.path)), file.path + " is pinned by BeltRangeSetup");
        }

        [Test]
        public void EveryMasteredClipMeetsItsCategoryTargets()
        {
            var problems = new List<string>();
            int count = 0;
            foreach (var (path, category) in Clips())
            {
                count++;
                var target = Targets[category];
                var x = ReadWav(path, out int rate);
                string name = Path.GetFileName(path);
                double peakDb = 20 * Math.Log10(Math.Max(x.Max(v => Math.Abs(v)), 1e-12));
                double momentary = Loudness.MomentaryMax(x, rate);
                double harsh = HarshBand(x, rate);
                if (peakDb > target.Peak + .01) problems.Add($"{name}: peak {peakDb:0.00} dBFS over {target.Peak}");
                if (momentary > target.Momentary + target.Tolerance + .3) problems.Add($"{name}: {momentary:0.0} LUFS-M louder than {target.Momentary}");
                if (momentary < target.Momentary - 7) problems.Add($"{name}: {momentary:0.0} LUFS-M nearly silent");
                if (harsh > target.Harsh + 2) problems.Add($"{name}: harsh band {harsh:0.0} LUFS-M over {target.Harsh}");
                if (target.Loop)
                {
                    var steps = new double[x.Length - 1];
                    for (int i = 1; i < x.Length; i++) steps[i - 1] = Math.Abs(x[i] - x[i - 1]);
                    Array.Sort(steps);
                    if (Math.Abs(x[0] - x[x.Length - 1]) > steps[(int)(steps.Length * .999)]) problems.Add($"{name}: loop seam jumps");
                }
                else
                {
                    if (x[0] != 0 || x[x.Length - 1] != 0) problems.Add($"{name}: does not start and end on silence");
                    if (Math.Abs(x.Average(v => (double)v)) > 1e-4) problems.Add($"{name}: DC offset");
                }
            }
            Assert.AreEqual(6 + 6, count, "Winter and ambience clips");
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void RegeneratedSourcesMatchTheShippedClips()
        {
            foreach (string file in Directory.GetFiles(Path.Combine(Root, "Audio/Winter"), "*.wav"))
            {
                string mastered = Path.Combine(Project, WinterSourcePath, Path.GetFileName(file));
                Assert.IsTrue(File.Exists(mastered), mastered);
                Assert.AreEqual(Sha(mastered), Sha(file), Path.GetFileName(file) + ": WinterAudioImporter.Build copies the mastered source");
            }
        }

        const string WinterSourcePath = "ArtSource/Collections/Winter/Audio/Mastered";

        [Test]
        public void AmbienceSitsWellUnderPlayAndCuesSitUnderImpacts()
        {
            double houseGain = 20 * Math.Log10(GameMix.HouseSpeaker * GameMix.CueTrim);
            double impact = HouseCueMix.ImpactLufs + houseGain;
            double feedback = HouseCueMix.FeedbackLufs + houseGain;
            Assert.IsEmpty(Directory.GetFiles(Path.Combine(Root, "Resources/Ambience"), "loop*.wav", SearchOption.AllDirectories), "no continuous noise bed");
            double loudestCall = Directory.GetFiles(Path.Combine(Root, "Resources/Ambience"), "*.wav", SearchOption.AllDirectories)
                .Where(f => !Path.GetFileName(f).StartsWith("loop", StringComparison.OrdinalIgnoreCase))
                .Max(f => Loudness.MomentaryMax(ReadWav(f, out int r), r)) + 20 * Math.Log10(BackdropAmbience.CallLevel);
            double tick = Loudness.MomentaryMax(ToyCueSynth.Render("Tick"), ToyCueSynth.Rate) + 20 * Math.Log10(GameMix.TickLevel * GameMix.CueTrim);
            Debug.Log($"[Wreckabulary] Mix plan: impacts {impact:0.0}, feedback {feedback:0.0}, tick {tick:0.0}, calls {loudestCall:0.0} LUFS-M");
            Assert.Less(feedback, impact, "UI cues are quieter than impacts");
            Assert.Less(tick, feedback);
            Assert.Less(loudestCall, impact - 6, "ambience calls stay under the impacts");
        }

        [Test]
        public void ProceduralCuesBecomeFortyEightKilohertzClipsUnchanged()
        {
            foreach (string cue in ToyCueSynth.Cues)
            {
                var samples = ToyCueSynth.Render(cue);
                var clip = AudioClip.Create("probe " + cue, samples.Length, 1, ToyCueSynth.Rate, false);
                try
                {
                    Assert.IsTrue(clip.SetData(samples, 0));
                    var back = new float[samples.Length];
                    Assert.IsTrue(clip.GetData(back, 0));
                    Assert.AreEqual(48000, clip.frequency);
                    for (int i = 0; i < samples.Length; i += 97) Assert.AreEqual(samples[i], back[i], 1e-6, cue);
                    Assert.Less(ToyCueSynth.HighestPartial(cue) * 1.03, ToyCueSynth.BandLimit * clip.frequency, cue);
                    Assert.LessOrEqual(samples.Max(v => Math.Abs(v)), Math.Pow(10, -3 / 20.0), cue);
                }
                finally { UnityEngine.Object.DestroyImmediate(clip); }
            }
        }
    }
}
