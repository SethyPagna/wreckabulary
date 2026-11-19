using UnityEngine;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Voice
{
    public static class VoicePlayback
    {
        public static AudioSource Create(Transform parent, string name)
        {
            var host = new GameObject(name);
            if (parent) host.transform.SetParent(parent, false);
            return Add(host);
        }

        public static AudioSource Add(GameObject host)
        {
            var source = host.AddComponent<AudioSource>();
            Configure(source);
            return source;
        }

        public static void Configure(AudioSource source)
        {
            if (!source) return;
            source.playOnAwake = false;
            source.ignoreListenerVolume = true;
            source.ignoreListenerPause = true;
            source.outputAudioMixerGroup = null;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
            source.bypassEffects = true;
            source.bypassListenerEffects = true;
            source.bypassReverbZones = true;
            source.priority = 0;
            source.volume = VoiceRules.Gain(VoiceChat.Volume);
        }

        public static bool IsSeparate(AudioSource source) =>
            source && source.ignoreListenerVolume && source.ignoreListenerPause && !source.outputAudioMixerGroup &&
            source.spatialBlend == 0f && !source.playOnAwake;
    }
}
