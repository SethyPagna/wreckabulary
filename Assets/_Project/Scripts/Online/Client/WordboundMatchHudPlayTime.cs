using UnityEngine;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        float runStartedAt;
        bool runTimed;

        static bool CareerWritable => LobbyMenu.RememberChoices &&
            !(Session.Bindings.Count > 0 && Session.Bindings[0] is ScriptedBinding);

        void NoteRunStarted(MatchSnapshot next)
        {
            if (runTimed || runStartedAt > 0f || next.Progress.ElapsedTicks <= 0 || next.Progress.Result != null) return;
            runStartedAt = Time.realtimeSinceStartup;
        }

        int SecondsSinceRunStarted()
        {
            if (runStartedAt <= 0f) return 0;
            int seconds = Mathf.Max(0, Mathf.FloorToInt(Time.realtimeSinceStartup - runStartedAt));
            runStartedAt = 0f;
            return seconds;
        }

        void RecordRunTime()
        {
            if (runTimed || runStartedAt <= 0f) return;
            runTimed = true;
            int seconds = SecondsSinceRunStarted();
            if (seconds <= 0 || !CareerWritable) return;
            var career = MatchTally.LoadCareer();
            career.SecondsPlayed += seconds;
            MatchTally.SaveCareer(career);
        }
    }
}
