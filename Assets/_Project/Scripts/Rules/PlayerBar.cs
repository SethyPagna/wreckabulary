using System;
using System.Collections.Generic;

namespace Wreckabulary.Rules
{
    public readonly struct BarBlock
    {
        public readonly float Left, Right, Top, Bottom;

        public BarBlock(float left, float right, float top, float bottom)
        {
            Left = left; Right = right; Top = top; Bottom = bottom;
        }

        public bool Overlaps(float left, float right, float top, float bottom) =>
            Left < right && Right > left && Top < bottom && Bottom > top;
    }

    public static class PlayerBar
    {
        public static readonly bool StaminaShown = true;
        public const int RoomySeats = 4, MostSeats = 8, BarRadius = 4;
        public const float Top = 10f, Clearance = 12f, Margin = 8f, Edge = 24f, TeamGap = 36f;
        public const float RoomyCard = 96f, SnugCard = 78f, RoomyPortrait = 68f, SnugPortrait = 56f;
        public const float RoomyGap = 8f, SnugGap = 6f, Frame = 5f, Inset = 3f;
        public const float HealthGap = 4f, HealthHeight = 22f, StaminaGap = 3f, StaminaHeight = 5f, NameGap = 2f, NameHeight = 22f, ScoreHeight = 20f;
        public const float NumberSize = 18f, NumberMin = 16f, NumberPad = 4f;
        public const float SoloWidth = 240f, SoloHeight = 36f, SoloNumberSize = 30f, SoloPad = 8f, SoloStaminaGap = 4f, SoloStaminaHeight = 6f;
        public const float NameSize = 18f, ScoreSize = 16f, StatusSize = 16f, StatusMin = 15f;
        public const float PlatePad = 10f, PlateRadius = 12f;
        public static float Height => CardHeight(RoomySeats);
        public static float Shift => Height + PlatePad + Clearance;

        public static bool Visible(int seats) => seats > 1;

        public static bool Snug(int seats) => seats > RoomySeats;

        public static float CardWidth(int seats) => Snug(seats) ? SnugCard : RoomyCard;

        public static float Portrait(int seats) => Snug(seats) ? SnugPortrait : RoomyPortrait;

        public static float CardGap(int seats) => Snug(seats) ? SnugGap : RoomyGap;

        public static float HealthY(int seats) => Portrait(seats) + HealthGap;

        public static float StaminaY(int seats) => HealthY(seats) + HealthHeight + StaminaGap;

        static float StaminaRow => StaminaShown ? StaminaGap + StaminaHeight : 0f;

        public static float NameY(int seats) => HealthY(seats) + HealthHeight + StaminaRow + NameGap;

        public static float SoloTall => SoloHeight + (StaminaShown ? SoloStaminaGap + SoloStaminaHeight : 0f);

        public static float ScoreY(int seats) => NameY(seats) + NameHeight;

        public static float CardHeight(int seats) => ScoreY(seats) + ScoreHeight;

        public static float PlateWidth(float width) => width + 2f * PlatePad;

        public static float PlateHeight(int seats) => CardHeight(seats) + 2f * PlatePad;

        public static float ShiftFor(float top, float scale) => ShiftFor(top, scale, Height);

        public static float ShiftFor(float top, float scale, float height) => Math.Max(0f, top - Top) + (height + PlatePad) * scale + Clearance;

        public static bool Grouped(IReadOnlyList<int> teams)
        {
            if (teams == null) return false;
            bool shared = false, mixed = false;
            for (int i = 0; i < teams.Count; i++)
                for (int j = i + 1; j < teams.Count; j++)
                    if (teams[i] == teams[j]) shared = true;
                    else mixed = true;
            return shared && mixed;
        }

        public static float Arrange(IReadOnlyList<int> teams, int[] order, float[] centres)
        {
            if (teams == null || order == null || centres == null) return 0f;
            int n = Math.Min(teams.Count, Math.Min(order.Length, centres.Length));
            bool grouped = Grouped(teams);
            for (int i = 0; i < n; i++) order[i] = i;
            if (grouped)
                for (int i = 1; i < n; i++)
                {
                    int seat = order[i], j = i - 1;
                    while (j >= 0 && teams[order[j]] > teams[seat]) { order[j + 1] = order[j]; j--; }
                    order[j + 1] = seat;
                }
            float card = CardWidth(n), gap = CardGap(n), x = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) x += grouped && teams[order[i]] != teams[order[i - 1]] ? TeamGap : gap;
                centres[i] = x + card * .5f;
                x += card;
            }
            for (int i = 0; i < n; i++) centres[i] -= x * .5f;
            return x;
        }

        public static float Scale(float width, float room)
        {
            if (!(width > 0f) || !(room > 0f)) return 1f;
            float usable = room - 2f * Edge, plate = PlateWidth(width);
            return usable >= plate ? 1f : Math.Max(.1f, usable / plate);
        }

        public static float ClearTop(float halfWidth, float height, IReadOnlyList<BarBlock> blocks, float pad = 0f)
        {
            float top = Top;
            if (blocks == null) return top;
            for (int pass = 0; pass <= blocks.Count; pass++)
            {
                bool moved = false;
                for (int i = 0; i < blocks.Count; i++)
                {
                    var block = blocks[i];
                    if (!block.Overlaps(-halfWidth - pad, halfWidth + pad, top - pad, top + height + pad)) continue;
                    top = block.Bottom + Margin + pad;
                    moved = true;
                }
                if (!moved) break;
            }
            return top;
        }

        public static (float r, float g, float b) Vivid(float r, float g, float b)
        {
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            if (!(max > 0f)) return (r, g, b);
            float saturation = (max - min) / max;
            float value = Math.Max(max, .95f);
            float boosted = saturation < .05f ? saturation : Math.Min(1f, Math.Max(saturation * 1.08f, .6f));
            float Channel(float c)
            {
                float share = max - min > 0f ? (c - min) / (max - min) : 0f;
                return value * (1f - boosted + boosted * share);
            }
            return (Channel(r), Channel(g), Channel(b));
        }

        public static string Status(bool wrecked, bool downed, float bleedOutLeft) =>
            !wrecked && downed ? $"REVIVE\n{Math.Max(0, (int)Math.Ceiling(bleedOutLeft))}s" : "";

        public static string Letter(char initial) => char.ToUpperInvariant(initial).ToString();
    }
}
