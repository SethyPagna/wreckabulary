using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Art
{
    [DisallowMultipleComponent]
    public sealed class BackdropAmbience : MonoBehaviour
    {
        public const string Folder = "Ambience";
        public const string LoopPrefix = "loop";
        public const float BedLevel = .2f, CallLevel = .26f, MenuDuck = .45f;
        const float FadeSeconds = 1.6f, PollSeconds = .25f, FirstCall = 5f, ShortestGap = 7f, LongestGap = 18f;
        const int CallVoices = 3;

        readonly AudioSource[] voices = new AudioSource[CallVoices];
        readonly float[] voiceScales = { 1f, 1f, 1f };
        AudioSource bed;
        AudioClip[] calls = Array.Empty<AudioClip>();
        System.Random random;
        float level, target, nextPoll, nextCall;
        int lastCall = -1, nextVoice;
        bool configured, started;

        public string Theme { get; private set; }
        public float Level => level;

        public static BackdropAmbience Attach(Transform root, string theme)
        {
            if (!root || string.IsNullOrEmpty(theme)) return null;
            var clips = Resources.LoadAll<AudioClip>(Folder + "/" + theme);
            if (clips == null || clips.Length == 0) return null;
            Array.Sort(clips, (a, b) => string.CompareOrdinal(a.name, b.name));
            var holder = new GameObject("Ambience");
            holder.transform.SetParent(root, false);
            var ambience = holder.AddComponent<BackdropAmbience>();
            ambience.Configure(theme, clips);
            return ambience;
        }

        public static bool IsLoop(string clipName) =>
            !string.IsNullOrEmpty(clipName) && clipName.StartsWith(LoopPrefix, StringComparison.OrdinalIgnoreCase);

        void Configure(string theme, AudioClip[] clips)
        {
            Theme = theme;
            var oneShots = new List<AudioClip>();
            AudioClip loop = null;
            foreach (var clip in clips)
            {
                if (!clip) continue;
                if (!IsLoop(clip.name)) oneShots.Add(clip);
                else if (!loop) loop = clip;
            }
            calls = oneShots.ToArray();
            foreach (var call in calls) if (call.loadState == AudioDataLoadState.Unloaded) call.LoadAudioData();
            random = new System.Random(StableSeed(theme));
            if (loop)
            {
                bed = Source();
                bed.clip = loop;
                bed.loop = true;
            }
            if (calls.Length > 0)
                for (int i = 0; i < voices.Length; i++) voices[i] = Source();
            float now = Time.unscaledTime;
            target = TargetLevel();
            level = 0f;
            nextPoll = now + PollSeconds;
            nextCall = now + FirstCall + (float)random.NextDouble() * 4f;
            configured = true;
            Apply();
        }

        AudioSource Source()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.priority = 220;
            source.bypassReverbZones = true;
            source.volume = 0f;
            return source;
        }

        static int StableSeed(string text)
        {
            unchecked
            {
                int hash = (int)2166136261;
                foreach (char c in text) hash = (hash ^ c) * 16777619;
                return hash & int.MaxValue;
            }
        }

        static float TargetLevel()
        {
            if (GameFeedback.Muted || AudioListener.volume <= 0f) return 0f;
            return Quieted() ? MenuDuck : 1f;
        }

        static bool Quieted()
        {
            if (ModeGuides.Open) return true;
            var hud = GameHud.Active;
            if (hud && (hud.Paused || hud.ResultShown || hud.LeaveAsked)) return true;
            var lobby = LobbyMenu.Instance;
            return lobby && lobby.isActiveAndEnabled;
        }

        void OnEnable()
        {
            if (!configured) return;
            target = TargetLevel();
            level = 0f;
            Apply();
        }

        void OnDisable()
        {
            if (bed && bed.isPlaying) bed.Pause();
            foreach (var voice in voices) if (voice) voice.Stop();
        }

        void Update()
        {
            if (!configured) return;
            float now = Time.unscaledTime;
            if (now >= nextPoll)
            {
                target = TargetLevel();
                nextPoll = now + PollSeconds;
            }
            level = target <= 0f ? 0f : Mathf.MoveTowards(level, target, Time.unscaledDeltaTime / FadeSeconds);
            Apply();
            if (level <= 0f || calls.Length == 0 || now < nextCall) return;
            PlayCall();
            nextCall = now + ShortestGap + (float)random.NextDouble() * (LongestGap - ShortestGap);
        }

        void Apply()
        {
            bool silent = level <= 0f;
            if (bed)
            {
                bed.volume = level * BedLevel;
                if (silent) { if (bed.isPlaying) bed.Pause(); }
                else if (!started) StartBed();
                else if (!bed.isPlaying) bed.UnPause();
            }
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = voices[i];
                if (!voice) continue;
                voice.volume = level * CallLevel * voiceScales[i];
                if (silent && voice.isPlaying) voice.Stop();
            }
        }

        void StartBed()
        {
            started = true;
            bed.Play();
        }

        void PlayCall()
        {
            int pick = random.Next(calls.Length);
            if (calls.Length > 1 && pick == lastCall) pick = (pick + 1 + random.Next(calls.Length - 1)) % calls.Length;
            lastCall = pick;
            if (calls[pick].loadState != AudioDataLoadState.Loaded)
            {
                if (calls[pick].loadState == AudioDataLoadState.Unloaded) calls[pick].LoadAudioData();
                return;
            }
            int index = nextVoice;
            nextVoice = (nextVoice + 1) % voices.Length;
            var voice = voices[index];
            if (!voice) return;
            voiceScales[index] = .6f + (float)random.NextDouble() * .4f;
            voice.Stop();
            voice.clip = calls[pick];
            voice.pitch = .95f + (float)random.NextDouble() * .1f;
            voice.volume = level * CallLevel * voiceScales[index];
            voice.Play();
        }
    }
}
