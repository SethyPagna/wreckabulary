using System;

namespace Wreckabulary.Rules.Voice
{
    public enum MicState { Off, Muted, Ready, Live }

    public static class VoiceRules
    {
        public static bool Transmits(MicMode mode, bool selfMuted, bool pushHeld) => Mic(mode, selfMuted, pushHeld) == MicState.Live;

        public static MicState Mic(MicMode mode, bool selfMuted, bool pushHeld)
        {
            if (mode == MicMode.Off) return MicState.Off;
            if (selfMuted) return MicState.Muted;
            if (mode == MicMode.PushToTalk && !pushHeld) return MicState.Ready;
            return mode is MicMode.PushToTalk or MicMode.OpenMic ? MicState.Live : MicState.Off;
        }

        public static bool Plays(VoiceSettings settings, VoiceRoom room, string self, string speaker) =>
            settings != null && room != null && settings.HearTeammates && !settings.IsMuted(speaker) && room.CanHear(self, speaker);

        public static float Gain(int volume) => Math.Max(0, Math.Min(100, volume)) / 100f;

        public static int Step(int volume, int steps) =>
            Math.Max(0, Math.Min(100, (int)Math.Round((volume + steps * VoiceSettings.VolumeStep) / (double)VoiceSettings.VolumeStep,
                MidpointRounding.AwayFromZero) * VoiceSettings.VolumeStep));
    }
}
