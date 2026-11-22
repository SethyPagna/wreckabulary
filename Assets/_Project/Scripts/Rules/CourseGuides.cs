namespace Wreckabulary.Rules
{
    public static class CourseGuides
    {
        public const string RowLabel = ModeGuideBook.HowToPlay;

        public static string For(string courseMode) => courseMode switch
        {
            "TimeRun" => ModeGuideBook.TimeTrial,
            "Freestyle" => ModeGuideBook.Freeplay,
            "TeamRace" => ModeGuideBook.TeamRace,
            "BrawlRace" => ModeGuideBook.BrawlRace,
            "FreeForAll" => ModeGuideBook.ArenaFreeForAll,
            _ => null,
        };

        public static bool Has(string courseMode) => ModeGuideBook.Has(For(courseMode));
    }
}
