using System;
using System.Collections.Generic;
using System.Linq;

namespace Wreckabulary.Rules.Wordbound
{
    public static partial class CourseValidator
    {
        public static CourseValidation ValidateResolved(ResolvedCourse manifest, CourseCatalogue catalogue, CoursePlayRequirements play)
        {
            var v = new CourseValidation();
            if (manifest == null) { v.Add("manifest.null", "$", "", "Provide a resolved course."); return v; }
            ValidatePlay(play, v);
            if (!v.Ok) return v;
            if (!Same(CoursePlayData.From(play), manifest.Data.Play))
                v.Add("manifest.play", "$.play", "", "Manifest mode and format differ from the requested match.");
            ValidateManifest(manifest.Data, catalogue, v);
            return v;
        }

        internal static void ValidateManifest(CourseManifestData data, CourseCatalogue catalogue, CourseValidation v)
        {
            ValidateFields(data, "$", v, 0);
            if (!v.Ok) return;
            if (data.Format != "wreckabulary.resolved-course" || data.Schema != data.Document.Schema ||
                data.Schema < 1 || data.Schema > CourseDocument.LatestSchema || data.GeneratorVersion != CourseResolver.GeneratorFor(data.Schema) ||
                data.CanonicalVersion != data.Schema || data.RngVersion != CourseRandom.Version ||
                data.Attempt < 0 || data.Attempt >= CourseLimits.Attempts || !CourseRandom.ValidSeed(data.Seed))
                v.Add("manifest.version", "$", "", "Unsupported format, algorithm version, seed or attempt.");
            if (catalogue == null) { v.Add("catalogue", "$", "", "An approved catalogue is required."); return; }
            if (data.CatalogueHash != catalogue.Hash) v.Add("manifest.catalogue", "$.catalogueHash", "", "Catalogue hash does not match installed content.");
            CoursePlayRequirements play;
            try { play = data.Play.ToRequirements(); }
            catch (FormatException e) { v.Add("manifest.play", "$.play", "", e.Message); return; }
            v.AddRange(Validate(data.Document, catalogue, play).Issues);
            if (!v.Ok) return;
            if (data.SourceHash != CourseJson.Sha256(CourseJson.ExportDocument(data.Document)))
                v.Add("manifest.source", "$.sourceHash", "", "Source document hash does not match.");
            if (data.Hash != CourseResolver.ManifestHash(data))
                v.Add("manifest.hash", "$.hash", "", "Manifest hash does not match its canonical content.");
            if (!v.Ok) return;
            if (!Same(data.Supplies, CourseResolver.ResolveSupplies(data.Document, play)))
                v.Add("manifest.supplies", "$.supplies", "", "Supply state must exactly match source and ownership policy.");
            if (data.LayoutId != CourseResolver.PickLayout(data.Document, data.Seed))
                v.Add("manifest.layout", "$.layoutId", data.LayoutId, "The layout must be the one the seed selects.");
            ValidateSelections(data, play, v);
            ValidatePlacements(data, catalogue, play, v);
            if (v.Ok) ValidateSelectedProgression(data, v);
        }

        static bool Same(object first, object second) => CourseJson.Encode(CourseJson.Value(first)) == CourseJson.Encode(CourseJson.Value(second));

        static void ValidateSelections(CourseManifestData data, CoursePlayRequirements play, CourseValidation v)
        {
            bool circuit = play.Capability == CourseCapability.Circuit;
            int count = circuit ? data.Document.Laps.Sum(l => LapOptions(data.Document, l).Count) : 0;
            if (data.Laps.Count != count || data.Laps.Select(l => (l.Number, l.OptionId)).Distinct().Count() != count)
            { v.Add("manifest.laps", "$.laps", "", "Unexpected or duplicate lap plans."); return; }
            var prior = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var variants in data.Laps.OrderBy(l => l.Number).ThenBy(l => l.OptionId, StringComparer.Ordinal).GroupBy(l => l.Number))
            {
                var picks = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var selected in variants)
                {
                    var source = data.Document.Laps.SingleOrDefault(l => l.Number == selected.Number);
                    if (source == null || selected.AlternativeIds.Count != source.SectionIds.Count ||
                        !LapOptions(data.Document, source).Contains(selected.OptionId))
                    { v.Add("manifest.lap", "$.laps", "", "Lap selections must match authored sections."); continue; }
                    var alternatives = new List<CourseAlternative>();
                    for (int i = 0; i < source.SectionIds.Count; i++)
                    {
                        var section = data.Document.RouteSections.Single(s => s.Id == source.SectionIds[i]);
                        var choice = section.Alternatives.SingleOrDefault(a => a.Id == selected.AlternativeIds[i]);
                        if (choice == null || section.Kind == CourseRouteSection.ChoiceKind && choice.OptionId != selected.OptionId)
                        { v.Add("manifest.alternative", "$.laps", section.Id, "Alternative is outside this section."); break; }
                        if (section.Kind != CourseRouteSection.ChoiceKind)
                        {
                            if (section.Alternatives.Count > 1 && prior.TryGetValue(section.Id, out string previous) && previous == choice.Id)
                                v.Add("manifest.repeat", "$.laps", section.Id, "Adjacent laps must use different available branches.");
                            if (picks.TryGetValue(section.Id, out string shared) && shared != choice.Id)
                                v.Add("manifest.variant", "$.laps", section.Id, "Every option of a lap shares its seeded branches.");
                            picks[section.Id] = choice.Id;
                        }
                        alternatives.Add(choice);
                    }
                    if (alternatives.Count == source.SectionIds.Count && !Same(selected, SelectLap(data.Document, source, alternatives, selected.OptionId)))
                        v.Add("manifest.route", "$.laps", "", "Resolved route and checkpoints differ from the selected alternatives.");
                }
                foreach (var pick in picks) prior[pick.Key] = pick.Value;
            }
        }

        internal static IReadOnlyList<string> LapOptions(CourseDocument d, CourseLap lap)
        {
            var choice = lap.SectionIds.Select(id => d.RouteSections.FirstOrDefault(s => s.Id == id))
                .FirstOrDefault(s => s != null && s.Kind == CourseRouteSection.ChoiceKind);
            return choice == null ? new[] { "" } :
                choice.Alternatives.Select(a => a.OptionId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        }

        static void ValidatePlacements(CourseManifestData data, CourseCatalogue catalogue, CoursePlayRequirements play, CourseValidation v)
        {
            var d = data.Document;
            if (data.Placements.Count > CourseLimits.Entities || data.Placements.Select(p => p.Id).Distinct().Count() != data.Placements.Count)
            { v.Add("manifest.placements", "$.placements", "", "Duplicate IDs or too many placements."); return; }
            var zones = d.Zones.ToDictionary(z => z.Id, StringComparer.Ordinal);
            foreach (var p in data.Placements)
                if (!zones.ContainsKey(p.ZoneId) || !catalogue.Assets.ContainsKey(p.AssetId))
                    v.Add("manifest.reference", "$.placements", p.Id, "Unknown zone or asset.");
            if (!v.Ok) return;
            var active = CourseResolver.LayoutZoneIds(d, data.LayoutId);
            foreach (var zone in d.Zones.Where(z => !active.Contains(z.Id) && data.Placements.Any(p => p.ZoneId == z.Id)))
                v.Add("manifest.count", "$.placements", zone.Id, "Zones outside the selected layout stay empty.");
            var occupied = new List<CoursePlacementData>();
            foreach (var zone in d.Zones.Where(z => active.Contains(z.Id))
                .OrderBy(z => z.Purpose == "Resources" ? 0 : z.Purpose == "Props" ? 1 : 2).ThenBy(z => z.Id, StringComparer.Ordinal))
            {
                bool scoped = CourseResolver.TeamScoped(play, zone.Purpose);
                var items = data.Placements.Where(p => p.ZoneId == zone.Id).ToList();
                var canonical = items.Where(p => p.TeamIndex == (scoped ? 0 : -1)).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
                int copies = scoped ? play.TeamCount : 1;
                if (items.Count != canonical.Count * copies || canonical.Count < zone.MinCount || canonical.Count > zone.MaxCount ||
                    zone.ChancePermille == 0 && canonical.Count != 0)
                    v.Add("manifest.count", "$.placements", zone.Id, "Count or ownership copies violate zone constraints.");
                for (int slot = 0; slot < canonical.Count; slot++)
                {
                    var p = canonical[slot];
                    if (p.Id != CourseResolver.EntityId("p", zone.Id, slot, scoped ? 0 : -1) ||
                        p.ModuleId != zone.ModuleId || p.Purpose != zone.Purpose || !zone.Pool.Any(a => a.AssetId == p.AssetId) ||
                        !zone.Yaws.Contains(p.Yaw) || p.Letters != (zone.Purpose == "Resources" ? catalogue.Assets[p.AssetId].Letters : ""))
                        v.Add("manifest.placement", "$.placements", p.Id, "Placement metadata does not match its zone.");
                    bool fixedValid = p.Position.Equals(zone.FixedPosition) && p.Yaw == zone.FixedYaw;
                    bool gridValid = ((long)p.Position.X - zone.Bounds.Min.X) % zone.GridMm == 0 &&
                        ((long)p.Position.Y - zone.Bounds.Min.Y) % zone.GridMm == 0 &&
                        ((long)p.Position.Z - zone.Bounds.Min.Z) % zone.GridMm == 0;
                    if (p.ModuleId != zone.ModuleId || !(zone.Placement == "Fixed" ? fixedValid : gridValid) ||
                        !CourseResolver.Fits(p, zone, d, catalogue, occupied))
                        v.Add("manifest.bounds", "$.placements", p.Id, "Placement overlaps protected space, another item, or violates its authored range.");
                    occupied.Add(p);
                    for (int team = 1; team < copies; team++)
                    {
                        string id = CourseResolver.EntityId("p", zone.Id, slot, team);
                        var peer = items.SingleOrDefault(x => x.Id == id);
                        if (peer == null || peer.TeamIndex != team || peer.AssetId != p.AssetId || peer.ModuleId != p.ModuleId ||
                            peer.Purpose != p.Purpose || peer.Letters != p.Letters || !peer.Position.Equals(p.Position) || peer.Yaw != p.Yaw)
                            v.Add("manifest.fairness", "$.placements", zone.Id, "Teams need identical resource and puzzle opportunities.");
                    }
                }
            }
        }
    }
}
