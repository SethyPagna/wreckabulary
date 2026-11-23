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
        public const int RoomySeats = 4, MostSeats = 8;
        public const float Top = 10f, Clearance = 12f, Margin = 8f, Edge = 24f, TeamGap = 36f;
        public const float RoomyPortrait = 68f, SnugPortrait = 56f, RoomyCard = RoomyPortrait, SnugCard = SnugPortrait;
        public const float RoomyGap = 8f, SnugGap = 6f, Frame = 4f, Inset = 3f;
        public const float Outline = 3f, SideGap = 2f * Outline, Hairline = 1f, StaminaTrim = 4f;
        public const float HealthGap = 0f, HealthHeight = 22f, StaminaGap = 2f, StaminaHeight = 8f, ScoreGap = 2f, NameHeight = 24f, ScoreHeight = 22f;
        public const float NumberSize = 18f, NumberMin = 16f, NumberPad = 4f;
        public const float SoloWidth = 240f, SoloHeight = 36f, SoloNumberSize = 30f, SoloPad = 8f, SoloStaminaGap = 4f, SoloStaminaHeight = 6f;
        public const float NameSize = 18f, ScoreSize = 16f, StatusSize = 16f, StatusMin = 15f, TextMin = 15f;
        public const float PlatePad = 10f;
        public static float Height => CardHeight(RoomySeats);
        public static float Shift => Height + PlatePad + Clearance;

        public static bool Visible(int seats) => seats > 1;

        public static bool Snug(int seats) => seats > RoomySeats;

        public static float CardWidth(int seats) => Snug(seats) ? SnugCard : RoomyCard;

        public static float Portrait(int seats) => Snug(seats) ? SnugPortrait : RoomyPortrait;

        public static float CardGap(int seats) => Snug(seats) ? SnugGap : RoomyGap;

        public static float HealthWidth(int seats) => CardWidth(seats) - Hairline;

        public static float StaminaWidth(int seats) => HealthWidth(seats) - StaminaTrim;

        public static float BoxHeight(int seats) => BoxHeight(seats, true);

        public static float BoxHeight(int seats, bool scored) => CardHeight(seats, scored) + 2f * Outline;

        public static float HealthY(int seats) => Portrait(seats) + HealthGap;

        public static float StaminaY(int seats) => HealthY(seats) + HealthHeight + StaminaGap;

        static float StaminaRow => StaminaShown ? StaminaGap + StaminaHeight : 0f;

        public static float SoloTall => SoloHeight + (StaminaShown ? SoloStaminaGap + SoloStaminaHeight : 0f);

        public static float ScoreY(int seats) => HealthY(seats) + HealthHeight + StaminaRow + ScoreGap;

        public static float CardHeight(int seats) => CardHeight(seats, true);

        public static float CardHeight(int seats, bool scored) => scored ? ScoreY(seats) + ScoreHeight : HealthY(seats) + HealthHeight + StaminaRow;

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

        static void Order(IReadOnlyList<int> teams, int[] order, int n)
        {
            for (int i = 0; i < n; i++) order[i] = i;
            if (!Grouped(teams)) return;
            for (int i = 1; i < n; i++)
            {
                int seat = order[i], j = i - 1;
                while (j >= 0 && teams[order[j]] > teams[seat]) { order[j + 1] = order[j]; j--; }
                order[j + 1] = seat;
            }
        }

        public static bool Joined(IReadOnlyList<int> teams, int[] order, int slot, int split)
        {
            if (teams == null || order == null || slot <= 0 || slot >= order.Length || (split > 0 && slot == split)) return false;
            int before = order[slot - 1], seat = order[slot];
            return before >= 0 && seat >= 0 && before < teams.Count && seat < teams.Count && ScoreLine.SameSide(teams, before, seat) && ScoreLine.Sides(teams) > 1;
        }

        public static bool OneCrew(IReadOnlyList<int> teams, int count)
        {
            if (teams == null || count < 2 || count > teams.Count) return false;
            for (int i = 1; i < count; i++)
                if (!ScoreLine.SameSide(teams, 0, i)) return false;
            return true;
        }

        public static float Strip(IReadOnlyList<int> teams, int[] order, float[] centres, float centreWidth, out int split) =>
            Strip(teams, order, centres, centreWidth, out split, out _);

        public static float Strip(IReadOnlyList<int> teams, int[] order, float[] centres, float centreWidth, out int split, out float centreX)
        {
            split = 0;
            centreX = 0f;
            float centre = Math.Max(0f, centreWidth);
            if (teams == null || order == null || centres == null) return centre;
            int n = Math.Min(teams.Count, Math.Min(order.Length, centres.Length));
            if (n == 0) return centre;
            Order(teams, order, n);
            bool centred = centre > 0f && n > 1;
            float card = CardWidth(n), x = 0f;
            if (centred && OneCrew(teams, n))
            {
                split = n;
                float width = n * card + (n - 1) * SideGap + 2f * Outline + centre, start = -width * .5f + Outline;
                for (int i = 0; i < n; i++) centres[i] = start + i * (card + SideGap) + card * .5f;
                centreX = width * .5f - centre * .5f;
                return width;
            }
            if (centred) split = ScoreLine.Split(teams, order, n);
            for (int i = 0; i < n; i++)
            {
                if (i > 0 && !Joined(teams, order, i, split)) x += SideGap;
                centres[i] = x + card * .5f;
                x += card;
            }
            if (!centred)
            {
                for (int i = 0; i < n; i++) centres[i] -= x * .5f;
                return x + 2f * Outline;
            }
            float half = card * .5f, reach = centre * .5f + Outline;
            float left = centres[split - 1] + half, right = centres[split] - half;
            float leftSpan = left - (centres[0] - half), rightSpan = centres[n - 1] + half - right;
            for (int i = 0; i < n; i++) centres[i] += i < split ? -reach - left : reach - right;
            return 2f * (reach + Math.Max(leftSpan, rightSpan) + Outline);
        }

        public static float Arrange(IReadOnlyList<int> teams, int[] order, float[] centres)
        {
            if (teams == null || order == null || centres == null) return 0f;
            int n = Math.Min(teams.Count, Math.Min(order.Length, centres.Length));
            bool grouped = Grouped(teams);
            Order(teams, order, n);
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
