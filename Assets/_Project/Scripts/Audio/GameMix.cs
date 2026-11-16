using System.Collections.Generic;
using UnityEngine;
using Wreckabulary.Rules.Sound;
using Wreckabulary.Voice;

namespace Wreckabulary.Audio
{
    public readonly struct MixShot
    {
        public readonly AudioClip Clip;
        public readonly double Start;
        public readonly float Gain;
        public readonly float Pitch;

        public MixShot(AudioClip clip, double start, float gain, float pitch)
        {
            Clip = clip;
            Start = start;
            Gain = gain;
            Pitch = pitch;
        }
    }

    public sealed class GameMix : MonoBehaviour
    {
        public const float MenuDuck = .5f, VoiceDuck = .4f;
        public const int HouseVoices = 6, HouseCopies = 3;
        public const double HouseCooldown = .06;
        public const float HouseSpeaker = .85f, CueTrim = .84f, TickLevel = 1f, PanBlend = .5f, PanSpread = 60f;
        const float ScanSeconds = .5f;

        static GameMix instance;
        static readonly CueGate houseGate = new CueGate(ToyCueSynth.Cues.Length, HouseVoices, HouseCopies, HouseCooldown);
        static readonly CueGate tickGate = new CueGate(1, 2, 2, .05, .05);
        static readonly SoundJitter jitter = new SoundJitter(0x5EED1234u);
        static readonly List<MixShot> shots = new List<MixShot>();
        static AnimationCurve flat;
        static Transform ear;
        static bool teammateTalking, logging;
        static double fullLevelUntil;

        public static readonly MixLog Log = new MixLog(16384);
        public static IReadOnlyList<MixShot> Shots => shots;
        public static bool TeammateTalking => teammateTalking;
        public static float DuckTarget { get; private set; } = 1f;

        public static bool Logging
        {
            get => logging;
            set { logging = value; if (value) { Log.Clear(); shots.Clear(); } }
        }

        MasterBus bus;
        float nextScan;

        public static MasterBus Bus => Ensure() ? instance.bus : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            VoiceChat.Changed -= OnVoice;
            instance = null;
            ear = null;
            houseGate.Clear();
            tickGate.Clear();
            Log.Clear();
            shots.Clear();
            logging = teammateTalking = false;
            fullLevelUntil = 0;
            DuckTarget = 1f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            VoiceChat.Changed -= OnVoice;
            VoiceChat.Changed += OnVoice;
            Ensure();
        }

        public static bool Ensure()
        {
            if (instance) return true;
            if (!Application.isPlaying) return false;
            var root = new GameObject("Game mix");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<GameMix>();
            instance.AttachBus();
            return true;
        }

        public static MasterBus Attach() => Ensure() ? instance.AttachBus() : null;

        MasterBus AttachBus()
        {
            var listener = FindAnyObjectByType<AudioListener>();
            ear = listener ? listener.transform : null;
            if (!listener) return bus = null;
            if (!listener.TryGetComponent(out bus)) bus = listener.gameObject.AddComponent<MasterBus>();
            bus.Duck = DuckTarget;
            return bus;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (now >= nextScan)
            {
                nextScan = now + ScanSeconds;
                if (!bus || !bus.isActiveAndEnabled || !ear || !ear.gameObject.activeInHierarchy) AttachBus();
            }
            DuckTarget = Time.unscaledTimeAsDouble < fullLevelUntil ? 1f : Mathf.Min(MenuTarget(), teammateTalking ? VoiceDuck : 1f);
            if (bus) bus.Duck = DuckTarget;
        }

        static float MenuTarget()
        {
            if (ModeGuides.Open) return MenuDuck;
            var hud = GameHud.Active;
            return hud && (hud.Paused || hud.ResultShown || hud.LeaveAsked) ? MenuDuck : 1f;
        }

        static void OnVoice()
        {
            bool talking = false;
            string self = VoiceChat.Self;
            foreach (string id in VoiceChat.Speaking)
                if (id != self) { talking = true; break; }
            teammateTalking = talking;
        }

        public static void HoldFullLevel(float seconds)
        {
            double until = Time.unscaledTimeAsDouble + Mathf.Max(0f, seconds) + .2;
            if (until > fullLevelUntil) fullLevelUntil = until;
            if (instance && instance.bus)
            {
                instance.bus.Duck = DuckTarget = 1f;
                instance.bus.Dynamics.SnapDuck();
            }
        }

        public static bool AdmitHouse(int cue, CuePriority priority, AudioClip clip)
        {
            Ensure();
            double now = Time.unscaledTimeAsDouble;
            bool admitted = houseGate.Admit(cue, priority, now, clip ? clip.length : .2f);
            if (!admitted) Record(clip, 0f, 1f, false);
            return admitted;
        }

        public static bool AdmitTick(AudioClip clip)
        {
            Ensure();
            bool admitted = tickGate.Admit(0, CuePriority.Normal, Time.unscaledTimeAsDouble, clip ? clip.length : .04f);
            if (!admitted) Record(clip, 0f, 1f, false);
            return admitted;
        }

        public static void Record(AudioClip clip, float gain, float pitch, bool admitted = true)
        {
            if (!logging) return;
            double now = Time.unscaledTimeAsDouble;
            float seconds = clip ? clip.length / Mathf.Max(.01f, pitch) : 0f;
            Log.Add(now, seconds, clip ? clip.name : "", admitted, gain);
            if (admitted && clip) shots.Add(new MixShot(clip, now, gain, pitch));
        }

        public static float NextGain() => (float)jitter.Gain();

        public static float NextPitch() => (float)(1 + (jitter.Next01() * 2 - 1) * SoundJitter.PitchSpread);

        public static int NextVariant(int count, int previous) => jitter.Variant(count, previous);

        public static float CouchGain(Vector3 at)
        {
            if (!ear || !ear.gameObject.activeInHierarchy)
            {
                var listener = FindAnyObjectByType<AudioListener>();
                ear = listener ? listener.transform : null;
            }
            if (!ear) return 1f;
            Vector3 e = ear.position, f = ear.forward;
            return (float)CouchAttenuation.Falloff(CouchAttenuation.FocusDistance(e.x, e.y, e.z, f.x, f.y, f.z, at.x, at.y, at.z));
        }

        public static void Spatial(AudioSource source, float blend = PanBlend)
        {
            if (!source) return;
            flat ??= AnimationCurve.Constant(0f, 1f, 1f);
            source.playOnAwake = false;
            source.dopplerLevel = 0f;
            source.spatialBlend = blend;
            source.spread = PanSpread;
            source.rolloffMode = AudioRolloffMode.Custom;
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, flat);
        }
    }
}
