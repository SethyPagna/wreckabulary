using System;
using UnityEngine;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Online
{
    public sealed partial class WordboundMatchHud
    {
        const float ScoreStripHeight = 40f, ScoreStripPad = 16f, ScoreGap = 12f, ScoreMark = 18f, ScoreMarkGap = 5f,
            ScoreNotesGap = 6f, ScoreYouBar = 3f, ScoreYouInset = 6f, ScoreTextRoom = 8f;
        const int ScoreStripCorner = 18, ScoreMinFont = 15, ScoreUnknown = int.MinValue;
        static readonly GUIContent scoreSeparator = new GUIContent(ScoreLine.Separator), scoreClockWidest = new GUIContent("00:00");
        readonly GUIContent scoreClock = new GUIContent(), scoreMine = new GUIContent(), scoreTheirs = new GUIContent();
        GUIStyle scoreStripStyle, scoreClockStyle, scoreMineStyle, scoreTheirsStyle;
        Texture scoreTrophy;
        MatchSnapshot scoreFrame;
        string scoreRoster;
        int[] scoreSeatTeams = Array.Empty<int>();
        Color scoreMineColour = EyeInk, scoreTheirsColour = EyeInk;
        int scoreStyleGeneration = -1, scoreFontMax = ScoreMinFont, scoreRival = -1, scoreStops = ScoreUnknown;
        int scoreMineValue = ScoreUnknown, scoreTheirsValue = ScoreUnknown, scoreTotal = ScoreUnknown, scoreLead = ScoreLine.NoLead;
        long scoreSeconds = long.MinValue;
        bool scoreTwoSides, scoreSidesShown, scoreClockShown;
        float notesTop = EyeMargin;

        void RefreshScore()
        {
            if (ReferenceEquals(scoreFrame, frame)) return;
            scoreFrame = frame;
            scoreClockShown = scoreSidesShown = false;
            if (frame == null || mode == null) return;
            ReadScoreClock();
            ReadScoreSides(frame.Binding);
            if (!scoreTwoSides || !ReadSideCounts(out int mine, out int theirs, out int total)) return;
            if (mine != scoreMineValue || total != scoreTotal) scoreMine.text = ScoreLine.Progress(mine, total);
            if (theirs != scoreTheirsValue || total != scoreTotal) scoreTheirs.text = ScoreLine.Progress(theirs, total);
            scoreMineValue = mine; scoreTheirsValue = theirs; scoreTotal = total;
            scoreLead = ScoreLine.Lead(mine, theirs);
            scoreSidesShown = true;
        }

        void ReadScoreClock()
        {
            if (mode.TimeLimitSeconds <= 0) return;
            long seconds = Math.Max(0, mode.TimeLimitSeconds - frame.Progress.ElapsedTicks / ModeDefinition.TickRate);
            if (seconds != scoreSeconds) { scoreSeconds = seconds; scoreClock.text = CourseGuidance.Clock(seconds); }
            scoreClockShown = true;
        }

        void ReadScoreSides(MatchBinding binding)
        {
            if (binding == null || binding.Hash == scoreRoster) return;
            scoreRoster = binding.Hash;
            var seats = binding.Seats;
            if (scoreSeatTeams.Length != seats.Count) scoreSeatTeams = new int[seats.Count];
            bool seated = false;
            scoreRival = -1;
            for (int i = 0; i < seats.Count; i++)
            {
                scoreSeatTeams[i] = seats[i].Team;
                if (seats[i].Team == team) seated = true; else scoreRival = seats[i].Team;
            }
            scoreTwoSides = seated && scoreRival >= 0 && ScoreLine.TwoSides(scoreSeatTeams);
            if (!scoreTwoSides) return;
            scoreMineColour = TeamColour(team);
            scoreTheirsColour = TeamColour(scoreRival);
        }

        static Color TeamColour(int side)
        {
            var assets = GameAssets.I;
            if (!assets || side < 0) return EyeInk;
            var colour = assets.PlayerColor(side);
            var (r, g, b) = PlayerBar.Vivid(colour.r, colour.g, colour.b);
            return new Color(r, g, b, 1f);
        }

        bool ReadSideCounts(out int mine, out int theirs, out int total)
        {
            mine = theirs = total = 0;
            if (mode.IsRace) return ReadRaceCounts(ref mine, ref theirs, ref total);
            if (mode.Objective != WordboundObjective.Knockouts) return false;
            var players = frame.Progress.Players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Team == team) mine += players[i].Knockouts;
                else if (players[i].Team == scoreRival) theirs += players[i].Knockouts;
            }
            total = mode.KnockoutTarget;
            return true;
        }

        bool ReadRaceCounts(ref int mine, ref int theirs, ref int total)
        {
            total = RaceStops();
            if (total <= 0) return false;
            bool mineKnown = false, theirsKnown = false;
            var teams = frame.Progress.Teams;
            for (int i = 0; i < teams.Count; i++)
            {
                if (teams[i].Team == team) { mine = Math.Min(total, teams[i].OrderedObjectives); mineKnown = true; }
                else if (teams[i].Team == scoreRival) { theirs = Math.Min(total, teams[i].OrderedObjectives); theirsKnown = true; }
            }
            return mineKnown && theirsKnown;
        }

        int RaceStops()
        {
            if (scoreStops != ScoreUnknown) return scoreStops;
            if (course == null) return 0;
            int stops = 0;
            for (int lap = 0; lap < course.Laps.Count && lap < mode.Laps; lap++) stops += course.Laps[lap].Checkpoints.Count + 1;
            return scoreStops = stops;
        }

        void PrepareScoreStyles()
        {
            var cap = KeyCap();
            if (scoreStripStyle != null && scoreStyleGeneration == ui.Generation && scoreStripStyle.normal.background == cap) return;
            scoreStripStyle = new GUIStyle { border = new RectOffset(ScoreStripCorner, ScoreStripCorner, ScoreStripCorner, ScoreStripCorner) };
            scoreStripStyle.normal.background = cap;
            scoreClockStyle = new GUIStyle(ui.Heading) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow,
                padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            if (KitBlack) scoreClockStyle.font = KitBlack;
            scoreClockStyle.normal.textColor = Color.white;
            scoreMineStyle = new GUIStyle(scoreClockStyle) { alignment = TextAnchor.MiddleRight };
            scoreTheirsStyle = new GUIStyle(scoreClockStyle) { alignment = TextAnchor.MiddleLeft };
            scoreFontMax = Mathf.Max(ScoreMinFont, ui.Heading.fontSize);
            scoreStyleGeneration = ui.Generation;
        }

        void SetScoreFont(int size)
        {
            if (scoreClockStyle.fontSize == size) return;
            scoreClockStyle.fontSize = scoreMineStyle.fontSize = scoreTheirsStyle.fontSize = size;
        }

        float MeasureScore(out float centre, out float mine, out float theirs, out float high)
        {
            var clock = scoreClockShown ? scoreClockStyle.CalcSize(scoreClock) : scoreClockStyle.CalcSize(scoreSeparator);
            centre = scoreClockShown ? Mathf.Max(clock.x, scoreClockStyle.CalcSize(scoreClockWidest).x) : clock.x;
            high = Mathf.Max(ScoreStripHeight, Mathf.Ceil(clock.y) + ScoreTextRoom);
            mine = theirs = 0f;
            if (!scoreSidesShown) return centre + ScoreStripPad * 2f;
            mine = scoreMineStyle.CalcSize(scoreMine).x;
            theirs = scoreTheirsStyle.CalcSize(scoreTheirs).x;
            float arm = ScoreStripPad + ScoreMark + ScoreMarkGap + Mathf.Max(mine, theirs) + ScoreGap;
            return centre + arm * 2f;
        }

        float ScoreRightEdge(float width) => VoiceShown && speakerRect.width > 0f ? speakerRect.xMin :
            LookEyeShown && eyeRect.width > 0f ? eyeRect.xMin : width - EyeMargin;

        float ScoreRightBottom() => VoiceShown && micKeyRect.width > 0f ? micKeyRect.yMax :
            LookEyeShown && keyCapRect.width > 0f ? keyCapRect.yMax : EyeMargin;

        float DrawScoreStrip(float width, Rect status)
        {
            if (!GuidanceVisible || mode == null) return EyeMargin;
            RefreshScore();
            if (!scoreClockShown && !scoreSidesShown) return EyeMargin;
            PrepareScoreStyles();
            int font = scoreFontMax;
            SetScoreFont(font);
            float limit = Mathf.Max(1f, width - EyeMargin * 2f);
            float wide = MeasureScore(out float centre, out float mine, out float theirs, out float high);
            while (wide > limit && font > ScoreMinFont)
            {
                SetScoreFont(--font);
                wide = MeasureScore(out centre, out mine, out theirs, out high);
            }
            float left = (width - wide) * .5f, top = EyeMargin;
            bool crowded = (status.width > 0f && left < status.xMax + ScoreGap) || left + wide > ScoreRightEdge(width) - ScoreGap;
            if (crowded) top = Mathf.Max(status.yMax, ScoreRightBottom()) + ScoreNotesGap;
            var area = new Rect(left, top, wide, high);
            PaintScoreStrip(area, centre, mine, theirs);
            return area.yMax + ScoreNotesGap;
        }

        void PaintScoreStrip(Rect area, float centre, float mine, float theirs)
        {
            Color color = GUI.color, background = GUI.backgroundColor;
            try
            {
                GUI.color = Color.white;
                GUI.backgroundColor = KeyCapFill;
                GUI.Box(area, GUIContent.none, scoreStripStyle);
                float middle = area.center.x, reach = centre * .5f + ScoreGap;
                GUI.color = EyeInk;
                GUI.Label(new Rect(middle - centre * .5f, area.y, centre, area.height), scoreClockShown ? scoreClock : scoreSeparator, scoreClockStyle);
                if (!scoreSidesShown) return;
                var mineRect = new Rect(middle - reach - mine, area.y, mine, area.height);
                var theirsRect = new Rect(middle + reach, area.y, theirs, area.height);
                GUI.color = scoreMineColour;
                GUI.Label(mineRect, scoreMine, scoreMineStyle);
                GUI.DrawTexture(new Rect(mineRect.x, area.yMax - ScoreYouInset - ScoreYouBar, mine, ScoreYouBar), Texture2D.whiteTexture);
                GUI.color = scoreTheirsColour;
                GUI.Label(theirsRect, scoreTheirs, scoreTheirsStyle);
                if (scoreLead == ScoreLine.Left) PaintScoreMark(mineRect.x - ScoreMarkGap - ScoreMark, area.center.y);
                else if (scoreLead == ScoreLine.Right) PaintScoreMark(theirsRect.xMax + ScoreMarkGap, area.center.y);
            }
            finally { GUI.color = color; GUI.backgroundColor = background; }
        }

        void PaintScoreMark(float x, float middle)
        {
            if (!scoreTrophy) scoreTrophy = LobbyIcons.Get(LobbyIcons.Trophy).texture;
            var at = new Rect(x, middle - ScoreMark * .5f, ScoreMark, ScoreMark);
            GUI.color = EyeShade;
            GUI.DrawTexture(new Rect(at.x, at.y + EyeShadow, ScoreMark, ScoreMark), scoreTrophy);
            GUI.color = ArrowTint;
            GUI.DrawTexture(at, scoreTrophy);
        }
    }
}
