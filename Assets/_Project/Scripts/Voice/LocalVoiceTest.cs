using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wreckabulary.Rules.Voice;

namespace Wreckabulary.Voice
{
    public sealed class LocalVoiceTest : MonoBehaviour
    {
        public const int ClipSeconds = 1, MeterSamples = 512;
        public const float AskSeconds = 15f;
        public const string Idle = "Speak and watch the meter. Use headphones.", NoDevice = "No microphone found.",
            Asking = "Allow microphone access to test it.", Denied = "Microphone access is off. Allow it in your system settings.",
            Failed = "Couldn't open the microphone.", Listening = "Listening. Only you can hear this.";

        IMicrophoneSource source;
        AudioClip clip;
        string device;
        VoiceLoop loop;
        VoiceReceiverPlayback output;
        Behaviour owner;
        bool owned, asking, hearYourself, simulateBad;
        float askedAt;
        int readAt;
        float[] captured = Array.Empty<float>();
        readonly float[] samples = new float[MeterSamples];

        public event Action Changed;

        public IMicrophoneSource Source
        {
            get => source ?? UnityMicrophone.Shared;
            set { Stop(); source = value; }
        }

        public Behaviour Owner
        {
            get => owner;
            set { owner = value; owned = true; }
        }

        public bool Running => clip;
        public bool Waiting => asking;
        public float Level { get; private set; }
        public string Status { get; private set; } = Idle;
        public AudioSource Playback => output?.Source;
        public AudioClip Clip => clip;
        public string ActiveDevice => device ?? "";
        public bool Looping => output != null && output.Playing;
        public VoiceLoop Loop => loop;
        public VoiceLinkStats LinkStats => loop?.Receiver.Stats ?? default;

        public bool HearYourself
        {
            get => hearYourself;
            set
            {
                if (hearYourself == value) return;
                hearYourself = value;
                if (!value) StopLoop();
                Notify();
            }
        }

        public bool SimulateBadConnection
        {
            get => simulateBad;
            set
            {
                if (simulateBad == value) return;
                simulateBad = value;
                if (loop != null) loop.Profile = Profile;
                Notify();
            }
        }

        VoiceNetworkProfile Profile => simulateBad ? VoiceNetworkProfile.Bad : VoiceNetworkProfile.Clean;

        public bool Toggle() => Running || asking ? StopAndReport() : Begin();

        public bool Begin()
        {
            Stop();
            var devices = Source.Devices;
            if (devices == null || devices.Count == 0) return Fail(NoDevice);
            if (!Source.Allowed)
            {
                asking = true;
                askedAt = Time.unscaledTime;
                Status = Asking;
                Source.Ask();
                Notify();
                return false;
            }
            return Open(devices);
        }

        public void Stop()
        {
            bool was = clip || asking;
            asking = false;
            StopLoop();
            if (clip)
            {
                try { Source.End(device); } catch (Exception) { }
                if (Application.isPlaying) Destroy(clip); else DestroyImmediate(clip);
            }
            clip = null;
            device = null;
            Level = 0f;
            if (Status == Listening || Status == Asking) Status = Idle;
            if (was) Notify();
        }

        bool StopAndReport()
        {
            Stop();
            return false;
        }

        bool Open(IReadOnlyList<string> devices)
        {
            string wanted = VoiceChat.Device;
            device = wanted.Length > 0 && devices.Contains(wanted) ? wanted : null;
            AudioClip opened = null;
            try { opened = Source.Start(device, ClipSeconds); }
            catch (Exception) { opened = null; }
            if (!opened)
            {
                device = null;
                return Fail(Failed);
            }
            clip = opened;
            Status = Listening;
            Notify();
            return true;
        }

        bool Fail(string why)
        {
            Status = why;
            Notify();
            return false;
        }

        void Update()
        {
            if (asking)
            {
                if (Source.Allowed)
                {
                    asking = false;
                    Open(Source.Devices);
                }
                else if (Time.unscaledTime - askedAt > AskSeconds)
                {
                    asking = false;
                    Fail(Denied);
                }
                return;
            }
            if (!clip) return;
            int position;
            try { position = Source.Position(device); }
            catch (Exception) { Stop(); Fail(Failed); return; }
            int length = clip.samples;
            if (length <= 0) return;
            position = ((position % length) + length) % length;
            int count = Mathf.Min(MeterSamples, length);
            if (clip.GetData(samples, (position - count + length) % length))
                Level = VoiceMeter.Smooth(Level, VoiceMeter.Level(samples, count), Time.unscaledDeltaTime);
            Loopback(position, length);
        }

        void Loopback(int position, int length)
        {
            if (!hearYourself) return;
            if (loop == null || loop.SampleRate != clip.frequency)
            {
                StopLoop();
                loop = new VoiceLoop(clip.frequency, Profile);
                captured = new float[loop.FrameSamples];
                readAt = position;
                output = new VoiceReceiverPlayback(loop.Receiver, gameObject, "Hear yourself");
            }
            loop.Mode = VoiceChat.Mic == MicMode.OpenMic ? MicMode.OpenMic : MicMode.PushToTalk;
            loop.State = MicState.Live;
            int frame = loop.FrameSamples;
            double now = VoiceClock.Now;
            if (frame < length)
                while (((position - readAt) % length + length) % length >= frame)
                {
                    if (!clip.GetData(captured, readAt)) break;
                    loop.Capture(captured, 0, frame, now);
                    readAt = (readAt + frame) % length;
                }
            loop.Pump(now);
            output.Volume = VoiceRules.Gain(VoiceChat.Volume);
            output.Play();
        }

        void StopLoop()
        {
            output?.Dispose();
            output = null;
            loop = null;
        }

        void LateUpdate()
        {
            if ((clip || asking) && owned && (!owner || !owner.isActiveAndEnabled)) Stop();
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && !asking) Stop();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && !asking) Stop();
        }

        void OnDisable() => Stop();

        void OnDestroy() => Stop();

        void Notify() => Changed?.Invoke();
    }
}
