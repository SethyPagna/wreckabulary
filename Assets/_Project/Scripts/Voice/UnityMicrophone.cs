using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wreckabulary.Voice
{
    public interface IMicrophoneSource
    {
        IReadOnlyList<string> Devices { get; }
        bool Allowed { get; }
        void Ask();
        AudioClip Start(string device, int seconds);
        int Position(string device);
        void End(string device);
    }

    public sealed class UnityMicrophone : IMicrophoneSource
    {
        public const int PreferredRate = 48000;
        public static readonly UnityMicrophone Shared = new();

        public IReadOnlyList<string> Devices
        {
            get
            {
                try { return Microphone.devices ?? Array.Empty<string>(); }
                catch (Exception) { return Array.Empty<string>(); }
            }
        }

        public bool Allowed
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);
#else
                return Application.HasUserAuthorization(UserAuthorization.Microphone);
#endif
            }
        }

        public void Ask()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
#else
            Application.RequestUserAuthorization(UserAuthorization.Microphone);
#endif
        }

        public AudioClip Start(string device, int seconds)
        {
            return Microphone.Start(device, true, Mathf.Max(1, seconds), Rate(device));
        }

        static int Rate(string device)
        {
            try
            {
                Microphone.GetDeviceCaps(device, out int lowest, out int highest);
                return highest <= 0 ? PreferredRate : Mathf.Clamp(PreferredRate, Mathf.Max(1, lowest), highest);
            }
            catch (Exception) { return PreferredRate; }
        }

        public int Position(string device) => Microphone.GetPosition(device);

        public void End(string device) => Microphone.End(device);
    }
}
