using System;
using UnityEngine;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Voice
{
    public sealed class VoiceReceiverPlayback : IDisposable
    {
        public const int ClipSeconds = 1;

        readonly AudioClip clip;
        readonly GameObject owned;
        bool disposed;

        public VoiceReceiverPlayback(VoiceReceiver receiver, GameObject host, string name = "Voice playback")
        {
            Receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
            if (host) Source = VoicePlayback.Add(host);
            else
            {
                Source = VoicePlayback.Create(null, name);
                owned = Source.gameObject;
            }
            clip = AudioClip.Create(name, receiver.SampleRate * ClipSeconds, 1, receiver.SampleRate, true, Fill, Seek);
            Source.clip = clip;
            Source.loop = true;
        }

        public VoiceReceiver Receiver { get; }
        public AudioSource Source { get; }
        public AudioClip Clip => clip;
        public bool Playing => !disposed && Source && Source.isPlaying && Source.clip == clip;
        public long Reads { get; private set; }

        public float Volume
        {
            get => Source ? Source.volume : 0f;
            set { if (Source) Source.volume = Mathf.Clamp01(value); }
        }

        public void Play()
        {
            if (disposed || !Source) return;
            if (Source.clip != clip) Source.clip = clip;
            if (!Source.isPlaying) Source.Play();
        }

        public void Stop()
        {
            if (Source) Source.Stop();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (Source)
            {
                Source.Stop();
                Source.clip = null;
                Destroy(owned ? (UnityEngine.Object)owned : Source);
            }
            if (clip) Destroy(clip);
        }

        static void Destroy(UnityEngine.Object target)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(target); else UnityEngine.Object.DestroyImmediate(target);
        }

        void Fill(float[] data)
        {
            if (disposed || data == null) return;
            Receiver.Read(data, 0, data.Length, VoiceClock.Now);
            Reads++;
        }

        static void Seek(int position) { }
    }
}
