using System;
using Photon.Voice;
using UnityEngine;
using Wreckabulary.Voice;
using IVoiceTransport = Wreckabulary.Voice.IVoiceTransport;
using FusionAppSettings = Fusion.Photon.Realtime.PhotonAppSettings;

namespace Wreckabulary.Online
{
    public sealed class PhotonVoiceOptions
    {
        public string AppId;
        public string AppVersion = PhotonVoiceSetup.AppVersion;
        public Func<IAudioDesc> Input;
        public bool CleanMicrophone = true;
    }

    public static class PhotonVoiceSetup
    {
        public const string AppIdVariable = "WORDBOUND_PHOTON_VOICE_APP_ID", AppVersion = "wreckabulary-voice-1";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        static void Install() => VoiceChat.Service = Create;

        static IVoiceTransport Create()
        {
            var options = Options();
            return options == null ? null : new PhotonVoiceTransport(options);
        }

        public static PhotonVoiceOptions Options()
        {
            string appId = Environment.GetEnvironmentVariable(AppIdVariable);
            if (!IsAppId(appId)) appId = SavedAppId();
            return IsAppId(appId) ? new PhotonVoiceOptions { AppId = appId.Trim() } : null;
        }

        public static bool IsAppId(string value) => Guid.TryParse(value?.Trim(), out var id) && id != Guid.Empty;

        static string SavedAppId()
        {
            try { return FusionAppSettings.Global?.AppSettings?.AppIdVoice; }
            catch (Exception) { return null; }
        }
    }
}
