using System;

namespace Wreckabulary.Rules.Sound
{
    public enum CueRole { Impact, Knockout, Feedback, Whoosh }

    public static class HouseCueMix
    {
        public const double ImpactLufs = -20.5, KnockoutLufs = -21.5, FeedbackLufs = -23.5, WhooshLufs = -26.5;

        public static CueRole Role(string cue) => cue switch
        {
            "Hit" or "Break" or "Block" or "Blast" => CueRole.Impact,
            "Out" => CueRole.Knockout,
            "Swing" => CueRole.Whoosh,
            _ => CueRole.Feedback,
        };

        public static double TargetLufs(string cue) => Role(cue) switch
        {
            CueRole.Impact => ImpactLufs,
            CueRole.Knockout => KnockoutLufs,
            CueRole.Whoosh => WhooshLufs,
            _ => FeedbackLufs,
        };

        public static double LevelDb(string cue, double clipLufs) =>
            double.IsNaN(clipLufs) || clipLufs < -90 ? 0 : Math.Min(0, TargetLufs(cue) - clipLufs);

        public static float Scale(string cue, double clipLufs) => (float)Math.Pow(10, LevelDb(cue, clipLufs) / 20);
    }
}
