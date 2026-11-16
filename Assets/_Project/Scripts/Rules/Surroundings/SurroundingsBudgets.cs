using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GameStudioKit.Determinism;

namespace Wreckabulary.Rules.Surroundings
{
    sealed partial class LayoutBuilder
    {
        SurroundingsResult Finish()
        {
            var ground = Ground();
            int groundRenderers = ground.Select(g => g.Slot).Distinct().Count();
            var budget = new SurroundingsBudget
            {
                High = Tier(items, groundRenderers, movers),
                Low = Tier(items.Where(i => i.Placement.InLow), groundRenderers, Array.Empty<SurroundingsMover>()),
            };
            CheckBudget("High", budget.High, SurroundingsBudget.HighRenderers, SurroundingsBudget.HighTriangles);
            CheckBudget("Low", budget.Low, SurroundingsBudget.LowRenderers, SurroundingsBudget.LowTriangles);
            var result = new SurroundingsResult
            {
                MapId = plan.MapId, Name = plan.Name, LightId = plan.LightId, AmbienceId = plan.AmbienceId,
                Placements = items.Select(i => i.Placement).ToList(), Movers = movers.ToList(), Ground = ground,
                PlayVolume = new SurroundingsBox((float)play.MinX, SurroundingsLayout.PlayFloor, (float)play.MinZ, (float)play.MaxX, playTop, (float)play.MaxZ),
                Budget = budget, Problems = problems.ToList(), Dropped = dropped.ToList(),
            };
            result.Canonical = Canonical(result);
            result.Hash = Hash64.ToHex(Hash64.Of(result.Canonical));
            return result;
        }

        SurroundingsTier Tier(IEnumerable<Item> placed, int groundRenderers, IReadOnlyList<SurroundingsMover> moving)
        {
            var groups = new Dictionary<(SurroundingsRing, string), SurroundingsGroup>();
            foreach (var item in placed)
                foreach (string slot in item.Piece.Slots)
                {
                    var key = (item.Placement.Ring, slot);
                    if (!groups.TryGetValue(key, out var g)) groups[key] = g = new SurroundingsGroup { Ring = item.Placement.Ring, Slot = slot };
                    g.Pieces++;
                    g.Triangles += item.Piece.TrisIn(slot);
                    g.Vertices += item.Piece.VertsIn(slot);
                }
            return new SurroundingsTier
            {
                Groups = groups.Values.OrderBy(g => g.Ring).ThenBy(g => g.Slot, StringComparer.Ordinal).ToList(),
                GroundRenderers = groundRenderers, Movers = moving.Count,
                MoverTriangles = moving.Sum(m => kit.Piece(m.Piece)?.Tris ?? 0),
            };
        }

        void CheckBudget(string tier, SurroundingsTier t, int renderers, int triangles)
        {
            if (t.Renderers > renderers) Problem("budget", tier, $"{t.Renderers} renderers, at most {renderers} ({Describe(t)})");
            if (t.Triangles > triangles) Problem("budget", tier, $"{t.Triangles} triangles, at most {triangles} ({Describe(t)})");
        }

        static string Describe(SurroundingsTier t) =>
            string.Join(", ", new[] { SurroundingsRing.Near, SurroundingsRing.Mid, SurroundingsRing.Far }.Select(r => $"{r} {t.RenderersIn(r)} renderers {t.TrianglesIn(r)} tris")) +
            $", ground {t.GroundRenderers}, movers {t.Movers}";

        static string Canonical(SurroundingsResult r)
        {
            var text = new StringBuilder();
            var v = r.PlayVolume;
            text.Append("map ").Append(r.MapId).Append('\n');
            text.Append("name ").Append(r.Name).Append('\n');
            text.Append("light ").Append(r.LightId).Append('\n');
            text.Append("ambience ").Append(r.AmbienceId).Append('\n');
            text.Append("volume ").Append(Join(v.MinX, v.MinY, v.MinZ, v.MaxX, v.MaxY, v.MaxZ)).Append('\n');
            foreach (var g in r.Ground)
                text.Append("ground ").Append(g.Shape).Append(' ').Append(g.Slot).Append(' ')
                    .Append(Join(g.Y, g.CentreX, g.CentreZ, g.Radius, g.MinX, g.MinZ, g.MaxX, g.MaxZ)).Append(' ')
                    .Append(Hex(g.Tint)).Append(' ').Append(g.Source).Append('\n');
            foreach (var p in r.Placements)
                text.Append("place ").Append(p.Ring).Append(' ').Append(p.Piece).Append(' ')
                    .Append(Join(p.X, p.Y, p.Z, p.Yaw, p.Scale)).Append(' ')
                    .Append(Hex(p.PlasterTint)).Append(' ').Append(Hex(p.RoofTint)).Append(' ').Append(Hex(p.PaintTint)).Append(' ')
                    .Append(p.Card ? "card" : "-").Append(' ').Append(p.Source).Append('\n');
            foreach (var m in r.Movers)
            {
                text.Append("mover ").Append(m.Piece).Append(' ').Append(Join(m.Speed, m.Phase)).Append(' ').Append(m.Loop ? "loop" : "once").Append(' ').Append(Hex(m.PaintTint));
                foreach (var p in m.Path) text.Append(' ').Append(Join(p.X, p.Y, p.Z).Replace(' ', ','));
                text.Append('\n');
            }
            return text.ToString();
        }

        static string Join(params float[] values) => string.Join(" ", values.Select(x => (x == 0 ? 0.0 : x).ToString("0.###", CultureInfo.InvariantCulture)));

        static string Hex(uint rgb) => rgb.ToString("x6", CultureInfo.InvariantCulture);
    }
}
