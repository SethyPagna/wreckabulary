using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Rules.Voice
{
    public enum MicMode { Off, PushToTalk, OpenMic }

    public sealed class VoiceSettings
    {
        public const int Version = 1, DefaultVolume = 80, VolumeStep = 10, MaxMuted = 64, MaxDevice = 256;
        public const string VersionKey = "wv.voice.version", MicKey = "wv.voice.mic", SelfMuteKey = "wv.voice.selfmute",
            HearKey = "wv.voice.hear", VolumeKey = "wv.voice.volume", DeviceKey = "wv.voice.device", MutedKey = "wv.voice.muted";
        public static readonly string[] Keys = { VersionKey, MicKey, SelfMuteKey, HearKey, VolumeKey, DeviceKey, MutedKey };

        readonly SortedSet<string> muted = new(StringComparer.Ordinal);
        int volume = DefaultVolume;
        string device = "";

        public MicMode Mic { get; set; }
        public bool SelfMuted { get; set; }
        public bool HearTeammates { get; set; } = true;
        public int Volume { get => volume; set => volume = Math.Max(0, Math.Min(100, value)); }
        public string Device { get => device; set => device = CleanDevice(value); }
        public IReadOnlyCollection<string> Muted => muted;

        public bool IsMuted(string playerId) => playerId != null && muted.Contains(playerId);

        public bool SetMuted(string playerId, bool mute)
        {
            if (!WordboundIdentity.IsId(playerId)) return false;
            if (!mute) return muted.Remove(playerId);
            if (muted.Count >= MaxMuted && !muted.Contains(playerId)) return false;
            return muted.Add(playerId);
        }

        public VoiceSettings Clone()
        {
            var copy = new VoiceSettings { Mic = Mic, SelfMuted = SelfMuted, HearTeammates = HearTeammates, Volume = Volume, Device = Device };
            foreach (string id in muted) copy.muted.Add(id);
            return copy;
        }

        public static VoiceSettings Read(Func<string, string> read)
        {
            var settings = new VoiceSettings();
            if (read == null || read(VersionKey)?.Trim() != Version.ToString(CultureInfo.InvariantCulture)) return settings;
            settings.Mic = ParseMic(read(MicKey));
            settings.SelfMuted = read(SelfMuteKey)?.Trim() == "1";
            settings.HearTeammates = read(HearKey)?.Trim() != "0";
            settings.Volume = int.TryParse(read(VolumeKey)?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int saved) ? saved : DefaultVolume;
            settings.Device = read(DeviceKey);
            foreach (string id in (read(MutedKey) ?? "").Split(','))
                settings.SetMuted(id.Trim(), true);
            return settings;
        }

        public IReadOnlyList<KeyValuePair<string, string>> Write() => new[]
        {
            Pair(VersionKey, Version.ToString(CultureInfo.InvariantCulture)),
            Pair(MicKey, MicId(Mic)),
            Pair(SelfMuteKey, SelfMuted ? "1" : "0"),
            Pair(HearKey, HearTeammates ? "1" : "0"),
            Pair(VolumeKey, Volume.ToString(CultureInfo.InvariantCulture)),
            Pair(DeviceKey, Device),
            Pair(MutedKey, string.Join(",", muted)),
        };

        public static string MicId(MicMode mode) => mode switch { MicMode.PushToTalk => "push", MicMode.OpenMic => "open", _ => "off" };

        public static MicMode ParseMic(string id) => id?.Trim() switch { "push" => MicMode.PushToTalk, "open" => MicMode.OpenMic, _ => MicMode.Off };

        public static string ShortDevice(string name, int max = 12)
        {
            if (string.IsNullOrWhiteSpace(name)) return "Default";
            string shown = name.Trim();
            const string wrapper = "Microphone (";
            if (shown.StartsWith(wrapper, StringComparison.OrdinalIgnoreCase) && shown.EndsWith(")") && shown.Length > wrapper.Length + 1)
                shown = shown.Substring(wrapper.Length, shown.Length - wrapper.Length - 1).Trim();
            max = Math.Max(2, max);
            return shown.Length <= max ? shown : shown.Substring(0, max - 1).TrimEnd() + "…";
        }

        static string CleanDevice(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > MaxDevice || value.Any(char.IsControl)) return "";
            return value;
        }

        static KeyValuePair<string, string> Pair(string key, string value) => new(key, value);
    }
}
