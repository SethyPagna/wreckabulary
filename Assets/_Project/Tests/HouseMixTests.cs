using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wreckabulary.Audio;
using Wreckabulary.Rules.Sound;
using Wreckabulary.Voice;
using Object = UnityEngine.Object;

namespace Wreckabulary.Tests
{
    public sealed class HouseMixTests
    {
        const float Seconds = 20f, AmbienceSeconds = 8f;
        const double TargetLufs = -23, TargetWindow = 2;
        bool hadMute, wasMuted, paused;
        float volume;
        string pack;

        [Serializable]
        sealed class Evidence
        {
            public string utc, engine, path, speakerMode;
            public int rate, channels, players, requested, admitted, maxOverlapAdmitted, maxOverlapRequested;
            public float seconds;
            public double peakDbfs, integratedLufs, momentaryMaxLufs, shortTermMaxLufs, shortTermMinLufs, loudnessRangeLu;
            public int clippedSamples;
            public double shareOver8Admitted, shareOver8Requested, shareOver4Admitted, shareOver4Requested;
            public double busPeakIn, busPeakOut, busDeepestReductionDb, busLimitedShare;
            public double ambienceIntegratedLufs = -120, ambienceUnderMixDb, impactShotLufs, feedbackShotLufs, swingShotLufs;
            public int impactShots, feedbackShots, swingShots;
            public float duckDuringCapture = 1f;
            public List<string> loudestNames = new();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            hadMute = PlayerPrefs.HasKey("wv.muted");
            wasMuted = GameFeedback.Muted;
            volume = AudioListener.volume;
            paused = AudioListener.pause;
            pack = PlayerPrefs.HasKey(GameSoundPacks.PreferenceKey) ? PlayerPrefs.GetString(GameSoundPacks.PreferenceKey) : null;
            yield return TestScenes.Reset();
            GameFeedback.Muted = false;
            AudioListener.volume = 1f;
            AudioListener.pause = false;
            GameSoundPacks.Select("default");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MasterBus.Recorder = null;
            GameMix.Logging = false;
            Match.ModeOverride = null;
            yield return TestScenes.Reset();
            GameFeedback.Muted = wasMuted;
            if (!hadMute) PlayerPrefs.DeleteKey("wv.muted");
            if (pack == null) PlayerPrefs.DeleteKey(GameSoundPacks.PreferenceKey); else PlayerPrefs.SetString(GameSoundPacks.PreferenceKey, pack);
            AudioListener.volume = volume;
            AudioListener.pause = paused;
            PlayerPrefs.Save();
        }

        static int Channels() => AudioSettings.speakerMode switch
        {
            AudioSpeakerMode.Mono => 1,
            AudioSpeakerMode.Quad => 4,
            AudioSpeakerMode.Surround => 5,
            AudioSpeakerMode.Mode5point1 => 6,
            AudioSpeakerMode.Mode7point1 => 8,
            _ => 2,
        };

        static IEnumerator WaitForBackend(MasterBus bus, Action<bool> live)
        {
            long blocks = bus.Blocks;
            double until = Time.realtimeSinceStartupAsDouble + 1.5;
            while (bus.Blocks < blocks + 4 && Time.realtimeSinceStartupAsDouble < until) yield return null;
            live(bus.Blocks >= blocks + 4);
        }

        const double PanLaw = .7071067811865476;

        static readonly Dictionary<AudioClip, double> clipLoudness = new();

        static double ClipLufs(AudioClip clip)
        {
            if (clipLoudness.TryGetValue(clip, out double known)) return known;
            var data = new float[clip.samples * clip.channels];
            double value = double.NaN;
            if (clip.GetData(data, 0))
            {
                var mono = new float[clip.samples];
                for (int i = 0; i < clip.samples; i++) mono[i] = data[i * clip.channels];
                value = Loudness.MomentaryMax(mono, clip.frequency);
            }
            return clipLoudness[clip] = value;
        }

        static (double lufs, int count) RoleLevel(IReadOnlyList<MixShot> shots, CueRole role)
        {
            double power = 0;
            int count = 0;
            foreach (var shot in shots)
            {
                if (!shot.Clip || !shot.Clip.name.StartsWith("House ", StringComparison.Ordinal)) continue;
                string cue = shot.Clip.name.Substring(6);
                if (!ToyCueSynth.Has(cue) || HouseCueMix.Role(cue) != role || shot.Gain <= 0) continue;
                double lufs = ClipLufs(shot.Clip) + 20 * Math.Log10(shot.Gain);
                if (double.IsNaN(lufs)) continue;
                power += Math.Pow(10, lufs / 10);
                count++;
            }
            return (count > 0 ? 10 * Math.Log10(power / count) : -120, count);
        }

        static IEnumerator AmbienceOnly(MasterBus bus, PlayerJoinManager joins, int rate, Action<MixReport> done)
        {
            foreach (var player in joins.Players)
                if (player && player.TryGetComponent<BotController>(out var bot)) bot.enabled = false;
            var muted = new List<AudioSource>();
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (source.GetComponentInParent<Wreckabulary.Art.BackdropAmbience>() || source.mute) continue;
                source.mute = true;
                muted.Add(source);
            }
            yield return new WaitForSecondsRealtime(.8f);
            var recorder = new MixRecorder(rate * Channels() * (int)(AmbienceSeconds + 1));
            MasterBus.Recorder = recorder;
            yield return new WaitForSecondsRealtime(AmbienceSeconds);
            MasterBus.Recorder = null;
            foreach (var source in muted) if (source) source.mute = false;
            done(Loudness.Measure(recorder.ToArray(), Math.Max(1, recorder.Channels), rate));
        }

        static MixReport OfflineMix(IReadOnlyList<MixShot> shots, double start, int rate, float seconds, MasterDynamics bus)
        {
            int frames = (int)(seconds * rate);
            var mix = new float[frames * 2];
            var cache = new Dictionary<AudioClip, float[]>();
            foreach (var shot in shots)
            {
                if (!shot.Clip) continue;
                if (!cache.TryGetValue(shot.Clip, out var data))
                {
                    data = new float[shot.Clip.samples * shot.Clip.channels];
                    if (!shot.Clip.GetData(data, 0)) continue;
                    cache[shot.Clip] = data;
                }
                int channels = shot.Clip.channels;
                double step = shot.Pitch * shot.Clip.frequency / rate;
                int at = (int)((shot.Start - start) * rate);
                for (int i = 0; ; i++)
                {
                    double source = i * step;
                    int index = (int)source;
                    if (index + 1 >= shot.Clip.samples || at + i >= frames) break;
                    if (at + i < 0) continue;
                    double t = source - index;
                    float v = (float)((data[index * channels] * (1 - t) + data[(index + 1) * channels] * t) * shot.Gain * PanLaw);
                    mix[(at + i) * 2] += v;
                    mix[(at + i) * 2 + 1] += v;
                }
            }
            var block = new float[2048];
            for (int f = 0; f < mix.Length; f += block.Length)
            {
                int n = Math.Min(block.Length, mix.Length - f);
                Array.Copy(mix, f, block, 0, n);
                if (n < block.Length) Array.Clear(block, n, block.Length - n);
                bus.Process(block, 2);
                Array.Copy(block, 0, mix, f, n);
            }
            return Loudness.Measure(mix, 2, rate);
        }

        [UnityTest]
        public IEnumerator ABusyFourBotHouseMatchStaysCalmAndNeverClips()
        {
            Session.SelectMap("pinwheel");
            Match.ModeOverride = "Dibs";
            Session.Remember(new ScriptedBinding());
            yield return TestScenes.Load(Session.DibsScene);
            RoundManager.Instance.CountdownTime = .05f;
            yield return TestScenes.WaitUntil(() => RoundManager.Instance.Phase == Phase.Playing, 3f, "battle start");
            var joins = Object.FindAnyObjectByType<PlayerJoinManager>();
            foreach (var player in joins.Players)
            {
                if (player.Binding is BotBinding) continue;
                var binding = new BotBinding();
                player.Binding = binding;
                player.gameObject.AddComponent<BotController>().Configure(player, binding);
            }
            int bots = joins.Players.Count(p => p.Binding is BotBinding && p.GetComponent<BotController>());
            Assert.AreEqual(4, bots, "four bots play");

            var bus = GameMix.Attach();
            Assert.IsNotNull(bus, "the master bus sits on the listener");
            Assert.IsTrue(bus.GetComponent<AudioListener>());
            bool live = false;
            yield return WaitForBackend(bus, ok => live = ok);
            int rate = AudioSettings.outputSampleRate;
            var recorder = new MixRecorder(rate * Channels() * (int)(Seconds + 2));
            bus.Dynamics.ResetStats();
            GameMix.Logging = true;
            MasterBus.Recorder = recorder;
            double start = Time.unscaledTimeAsDouble;
            float lowestDuck = 1f;
            while (Time.unscaledTimeAsDouble - start < Seconds)
            {
                lowestDuck = Mathf.Min(lowestDuck, GameMix.DuckTarget);
                yield return null;
            }
            MasterBus.Recorder = null;
            GameMix.Logging = false;
            double end = Time.unscaledTimeAsDouble;
            live &= recorder.Written > rate * Math.Max(1, recorder.Channels) * 5;

            var admitted = GameMix.Log.Spans(true);
            var requested = GameMix.Log.Spans(false);
            var report = live
                ? Loudness.Measure(recorder.ToArray(), recorder.Channels, rate)
                : OfflineMix(GameMix.Shots, start, rate, Seconds, new MasterDynamics(rate));
            var evidence = new Evidence
            {
                utc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"), engine = Application.unityVersion,
                path = live ? "live OnAudioFilterRead capture after the master bus" : "offline render of the admitted playback log through MasterDynamics",
                speakerMode = AudioSettings.speakerMode.ToString(), rate = rate, channels = live ? recorder.Channels : 2,
                players = bots, seconds = (float)(end - start), requested = requested.Count, admitted = admitted.Count,
                maxOverlapAdmitted = Overlap.Max(admitted), maxOverlapRequested = Overlap.Max(requested),
                peakDbfs = report.PeakDbfs, integratedLufs = report.IntegratedLufs, momentaryMaxLufs = report.MomentaryMaxLufs,
                shortTermMaxLufs = report.ShortTermMaxLufs, shortTermMinLufs = report.ShortTermMinLufs, loudnessRangeLu = report.LoudnessRangeLu,
                clippedSamples = report.ClippedSamples,
                shareOver8Admitted = Overlap.ShareAbove(admitted, 8, start, end), shareOver8Requested = Overlap.ShareAbove(requested, 8, start, end),
                shareOver4Admitted = Overlap.ShareAbove(admitted, 4, start, end), shareOver4Requested = Overlap.ShareAbove(requested, 4, start, end),
                busPeakIn = bus.Dynamics.PeakIn, busPeakOut = bus.Dynamics.PeakOut, busDeepestReductionDb = bus.Dynamics.DeepestReductionDb,
                busLimitedShare = bus.Dynamics.Frames > 0 ? (double)bus.Dynamics.LimitedFrames / bus.Dynamics.Frames : 0,
            };
            evidence.duckDuringCapture = lowestDuck;
            (evidence.impactShotLufs, evidence.impactShots) = RoleLevel(GameMix.Shots, CueRole.Impact);
            (evidence.feedbackShotLufs, evidence.feedbackShots) = RoleLevel(GameMix.Shots, CueRole.Feedback);
            (evidence.swingShotLufs, evidence.swingShots) = RoleLevel(GameMix.Shots, CueRole.Whoosh);
            MixReport ambience = null;
            if (live)
            {
                yield return AmbienceOnly(bus, joins, rate, measured => ambience = measured);
                evidence.ambienceIntegratedLufs = ambience.IntegratedLufs;
                evidence.ambienceUnderMixDb = report.IntegratedLufs - ambience.IntegratedLufs;
            }
            var names = new Dictionary<string, int>();
            for (int i = 0; i < GameMix.Log.Count; i++) if (GameMix.Log[i].Admitted) names[GameMix.Log[i].Name] = names.TryGetValue(GameMix.Log[i].Name, out int n) ? n + 1 : 1;
            evidence.loudestNames = names.OrderByDescending(kv => kv.Value).Take(12).Select(kv => kv.Key + " x" + kv.Value).ToList();
            string file = Path.GetFullPath(Path.Combine(Application.dataPath, "../BuildNotes/H/house-mix-capture.json"));
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, JsonUtility.ToJson(evidence, true));
            Debug.Log("[Wreckabulary] House mix (" + evidence.path + "): " + report + $", admitted {admitted.Count}/{requested.Count}, over 8 at once {evidence.shareOver8Admitted:P2}" +
                $", ambience {evidence.ambienceIntegratedLufs:0.0} LUFS ({evidence.ambienceUnderMixDb:0.0} dB under), impacts {evidence.impactShotLufs:0.0} vs feedback {evidence.feedbackShotLufs:0.0} LUFS-M");

            Assert.Greater(admitted.Count, 30, "the match was busy");
            Assert.AreEqual(0, report.ClippedSamples, "no clipping");
            Assert.LessOrEqual(report.PeakDbfs, MasterDynamics.CeilingDb + .01, "peaks stay at or under -1 dBFS");
            Assert.That(report.IntegratedLufs, Is.InRange(TargetLufs - TargetWindow, TargetLufs + TargetWindow), "a busy match sits near -23 LUFS");
            Assert.LessOrEqual(report.ShortTermMaxLufs, -14, "never loud");
            Assert.LessOrEqual(report.LoudnessRangeLu, 15, "no wild jumps in loudness");
            Assert.LessOrEqual(evidence.shareOver8Admitted, .01, "more than 8 sounds at once almost never");
            Assert.LessOrEqual(evidence.maxOverlapAdmitted, GameMix.HouseVoices + 4);
            if (evidence.impactShots > 0 && evidence.feedbackShots > 0)
                Assert.Less(evidence.feedbackShotLufs, evidence.impactShotLufs, "UI cues play below impacts");
            if (evidence.impactShots > 0 && evidence.swingShots > 0)
                Assert.Less(evidence.swingShotLufs, evidence.impactShotLufs, "swings play below impacts");
            if (live)
            {
                Assert.Greater(ambience.IntegratedLufs, -60, "the ambience is audible");
                Assert.That(evidence.ambienceUnderMixDb, Is.InRange(11.0, 18.0), "the ambience sits about 12-15 dB under play");
            }
        }

        [UnityTest]
        public IEnumerator VoiceBypassesTheMasterBusAndGameSoundGoesThroughIt()
        {
            var bus = GameMix.Attach();
            Assert.IsNotNull(bus);
            bool live = false;
            yield return WaitForBackend(bus, ok => live = ok);
            if (!live) Assert.Ignore("The audio backend is not running here; the bypass needs live output.");
            int rate = AudioSettings.outputSampleRate;
            var tone = AudioClip.Create("probe tone", rate, 1, rate, false);
            var samples = new float[rate];
            for (int i = 0; i < samples.Length; i++) samples[i] = .1f * Mathf.Sin(2 * Mathf.PI * 440 * i / rate);
            tone.SetData(samples, 0);
            var voice = VoicePlayback.Create(null, "probe voice");
            var game = new GameObject("probe game").AddComponent<AudioSource>();
            try
            {
                voice.clip = tone; voice.loop = true; voice.volume = 1f;
                game.clip = tone; game.loop = true; game.spatialBlend = 0f;
                Assert.IsTrue(voice.bypassListenerEffects, "voice is set to skip listener effects");

                var heard = new MixRecorder(rate * Channels());
                voice.Play();
                yield return new WaitForSecondsRealtime(.3f);
                MasterBus.Recorder = heard;
                yield return new WaitForSecondsRealtime(.4f);
                MasterBus.Recorder = null;
                float voicePeak = heard.ToArray().Select(Mathf.Abs).DefaultIfEmpty(0f).Max();
                voice.Stop();

                heard = new MixRecorder(rate * Channels());
                game.Play();
                yield return new WaitForSecondsRealtime(.3f);
                MasterBus.Recorder = heard;
                yield return new WaitForSecondsRealtime(.4f);
                MasterBus.Recorder = null;
                float gamePeak = heard.ToArray().Select(Mathf.Abs).DefaultIfEmpty(0f).Max();
                Debug.Log($"[Wreckabulary] Bus sees voice peak {voicePeak:0.0000}, game peak {gamePeak:0.0000}");
                Assert.Less(voicePeak, .001f, "voice never reaches the game limiter");
                Assert.Greater(gamePeak, .02f, "game sound goes through the limiter");
            }
            finally
            {
                MasterBus.Recorder = null;
                if (voice) Object.Destroy(voice.gameObject);
                if (game) Object.Destroy(game.gameObject);
                Object.Destroy(tone);
            }
        }
    }
}
