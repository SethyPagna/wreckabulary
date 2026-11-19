using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules.Voice
{
    public enum VoiceTap { Status, Toggle, Choose }
    public enum VoicePressEnd { None, Tap, Long }

    public static class VoiceButtons
    {
        public const double LongPressSeconds = .5;
        public const int MaxRows = 7;
        public static readonly IReadOnlyList<MicMode> Modes = new[] { MicMode.Off, MicMode.PushToTalk, MicMode.OpenMic };

        public static string ModeName(MicMode mode) => mode switch
        {
            MicMode.PushToTalk => "Push to talk",
            MicMode.OpenMic => "Open",
            _ => "Off",
        };

        public static bool Crossed(MicState state) => state == MicState.Off || state == MicState.Muted;
        public static bool Alarm(MicState state) => state == MicState.Muted;
        public static bool Lit(MicState state) => state == MicState.Live;
        public static bool ShowsKey(MicMode mode) => mode == MicMode.PushToTalk;

        public static VoiceTap MicTap(bool available, MicMode mode) =>
            !available ? VoiceTap.Status : mode == MicMode.Off ? VoiceTap.Choose : VoiceTap.Toggle;

        public static VoiceTap SpeakerTap(bool available) => available ? VoiceTap.Toggle : VoiceTap.Status;

        public static bool Shows(VoiceRoom room, string self) => room != null && room.HeardBy(self).Count > 0;

        public static IReadOnlyList<VoiceMember> Rows(VoiceRoom room, string self)
        {
            if (room == null) return Array.Empty<VoiceMember>();
            var heard = room.HeardBy(self);
            if (heard.Count <= MaxRows) return heard;
            var shown = new VoiceMember[MaxRows];
            for (int i = 0; i < MaxRows; i++) shown[i] = heard[i];
            return shown;
        }

        public static string Label(VoiceRoom room, string playerId)
        {
            if (room == null) return "";
            for (int i = 0; i < room.Members.Count; i++)
                if (room.Members[i].PlayerId == playerId) return "P" + (i + 1);
            return "";
        }

        public static string MicLine(MicState state, string key) => state switch
        {
            MicState.Muted => "Mic muted",
            MicState.Ready => string.IsNullOrEmpty(key) ? "Push to talk" : "Hold " + key + " to talk",
            MicState.Live => "Talking",
            _ => "Mic off",
        };

        public static string MuteLabel(bool muted) => muted ? "Unmute" : "Mute";
    }

    public sealed class VoicePress
    {
        double since = double.NaN;
        bool fired;

        public bool Down => !double.IsNaN(since);

        public bool Begin(double now)
        {
            if (Down || double.IsNaN(now)) return false;
            since = now;
            fired = false;
            return true;
        }

        public bool Long(double now)
        {
            if (!Down || fired || now - since < VoiceButtons.LongPressSeconds) return false;
            fired = true;
            return true;
        }

        public VoicePressEnd End(double now)
        {
            if (!Down) return VoicePressEnd.None;
            var end = fired ? VoicePressEnd.None : Long(now) ? VoicePressEnd.Long : VoicePressEnd.Tap;
            since = double.NaN;
            fired = false;
            return end;
        }

        public void Cancel()
        {
            since = double.NaN;
            fired = false;
        }
    }
}
