using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Wreckabulary.Rules;
using Wreckabulary.Rules.Tests;
using Wreckabulary.Rules.Wordbound;

namespace Wreckabulary.Tests
{
    public sealed class CourseSchema3Tests
    {
        const string PublicCataloguePath = "Assets/_Project/Resources/Wordbound/public-catalogue.json";
        const string KitchenItemsPath = "Assets/_Project/Data/Wordbound/Authoring/FlagshipKitchenV1/Items.json";
        static readonly string[] Schema3Keys =
            { "\"storyAnchors\":", "\"layouts\":", "\"solutions\":", "\"scope\":", "\"optionId\":", "\"layoutId\":", "\"kind\":\"Seeded\"", "\"kind\":\"Choice\"" };

        static CoursePoint P(int x, int z) => new CoursePoint(x, 0, z);
        static List<string> S(params string[] values) => new List<string>(values);
        static CoursePlayRequirements Play(string mode, RaceFormat format = RaceFormat.Solo) =>
            MatchStateValidation.Requirements(ModeDefinition.For(mode, format));
        static CourseResolution Resolve(CourseDocument d, string seed, string mode = "TeamRace", RaceFormat format = RaceFormat.Solo) =>
            CourseResolver.Resolve(d, CourseTemplates.ProofCatalogue(), new CourseResolveRequest(seed, Play(mode, format)));
        static string Definition(ModeDefinition mode, IEnumerable<RaceLap> route) =>
            new MatchProgress(mode, Enumerable.Range(0, mode.MaximumPlayers).Select(i => new WordboundSeat("p" + i,
                mode.TeamCount == 1 ? 0 : i % mode.TeamCount, mode.TeamCount == 1 ? i : i / mode.TeamCount)), route).Capture().DefinitionHash;

        internal static CourseDocument VariantCourse()
        {
            var d = new CourseDocument { Id = "variant-proof", Title = "Forked Parcel Circuit", SupportedModes = S("TimeRun", "TeamRace", "BrawlRace") };
            d.Layers.Add(new CourseLayer { Id = "Ground" });
            foreach (var (id, x, z) in new[] { ("Start", 0, 0), ("East", 20000, 0), ("Far", 40000, 0), ("NorthFar", 40000, 20000),
                ("North", 20000, 20000), ("NorthWest", 0, 20000), ("SouthFar", 40000, -20000), ("South", 20000, -20000), ("SouthWest", 0, -20000) })
                d.Modules.Add(new CourseModule { Id = id, DefinitionId = "Room", LayerId = "Ground", Position = P(x, z) });
            foreach (var (id, from, to, x, z) in new[] { ("StartEast", "Start", "East", 10000, 0), ("EastFar", "East", "Far", 30000, 0),
                ("FarNorthFar", "Far", "NorthFar", 40000, 10000), ("NorthFarNorth", "NorthFar", "North", 30000, 20000),
                ("NorthNorthWest", "North", "NorthWest", 10000, 20000), ("NorthWestStart", "NorthWest", "Start", 0, 10000),
                ("FarSouthFar", "Far", "SouthFar", 40000, -10000), ("SouthFarSouth", "SouthFar", "South", 30000, -20000),
                ("SouthSouthWest", "South", "SouthWest", 10000, -20000), ("SouthWestStart", "SouthWest", "Start", 0, -10000),
                ("SouthEast", "South", "East", 20000, -10000), ("EastNorth", "East", "North", 20000, 10000) })
                d.Connections.Add(new CourseConnection { Id = id, FromModuleId = from, ToModuleId = to, From = P(x, z), To = P(x, z) });
            d.Connections.Add(new CourseConnection {
                Id = "Dash", FromModuleId = "NorthFar", ToModuleId = "Start", Kind = "Bridge", Bidirectional = false,
                From = P(30000, 10000), To = P(10000, 10000), GatePuzzleId = "LookoutStool", Scope = CourseConnection.TeamScope
            });
            for (int i = 0; i < 8; i++)
                d.Spawns.Add(new CourseSpawn { Id = "Start" + i, ModuleId = "Start", Position = P(-7000 + i * 2000, -8000) });
            foreach (var (id, module, x, z) in new[] { ("StartSafe", "Start", 0, -6500), ("EastSafe", "East", 20000, -5000),
                ("FarSafe", "Far", 40000, -5000), ("NorthFarSafe", "NorthFar", 40000, 25000), ("SouthFarSafe", "SouthFar", 40000, -25000) })
                d.RecoveryAnchors.Add(new CourseRecovery { Id = id, ModuleId = module, Position = P(x, z), RadiusMm = 500 });
            foreach (var (id, module, word, x, z) in new[] { ("HubPlate", "East", "PLATE", 25000, 5000),
                ("LookoutBed", "NorthFar", "BED", 45000, 15000), ("LookoutStool", "NorthFar", "STOOL", 45000, 25000),
                ("WatchFan", "SouthFar", "FAN", 45000, -15000), ("WatchBed", "SouthFar", "BED", 45000, -25000) })
            {
                d.Supplies.Add(new CourseSupply { Id = id + "Letters", ModuleId = module, Position = P(x, z), Letters = word });
                d.Puzzles.Add(new CoursePuzzle {
                    Id = id, ModuleId = module, PresetId = "PlaceObject", RecipeId = word, RequiredLetters = word, RecoverySupplyIds = S(id + "Letters")
                });
            }
            d.Checkpoints.Add(new CourseCheckpoint { Id = "Gate", ModuleId = "Start", RecoveryAnchorId = "StartSafe" });
            d.Checkpoints.Add(new CourseCheckpoint { Id = "Hub", ModuleId = "East", RecoveryAnchorId = "EastSafe", PuzzleIds = S("HubPlate") });
            d.Checkpoints.Add(new CourseCheckpoint { Id = "Depot", ModuleId = "Far", RecoveryAnchorId = "FarSafe" });
            d.Checkpoints.Add(new CourseCheckpoint {
                Id = "Lookout", ModuleId = "NorthFar", RecoveryAnchorId = "NorthFarSafe",
                Solutions = new List<CourseSolution> {
                    new CourseSolution { Id = "Quick", PuzzleIds = S("LookoutBed") },
                    new CourseSolution { Id = "Grand", PuzzleIds = S("LookoutStool"), ShortcutConnectionId = "Dash" }
                }
            });
            d.Checkpoints.Add(new CourseCheckpoint {
                Id = "Watch", ModuleId = "SouthFar", RecoveryAnchorId = "SouthFarSafe",
                Solutions = new List<CourseSolution> {
                    new CourseSolution { Id = "Breeze", PuzzleIds = S("WatchFan") },
                    new CourseSolution { Id = "Rest", PuzzleIds = S("WatchBed") }
                }
            });
            d.Checkpoints.Add(new CourseCheckpoint { Id = "Home", ModuleId = "Start", RecoveryAnchorId = "StartSafe" });
            CourseAlternative Alternative(string id, string option, string[] connections, params string[] checkpoints) =>
                new CourseAlternative { Id = id, OptionId = option, ConnectionIds = S(connections), CheckpointIds = S(checkpoints) };
            var north = new[] { "FarNorthFar", "NorthFarNorth", "NorthNorthWest", "NorthWestStart" };
            var south = new[] { "FarSouthFar", "SouthFarSouth", "SouthSouthWest", "SouthWestStart" };
            d.RouteSections.Add(new CourseRouteSection {
                Id = "Out", EntryModuleId = "Start", ExitModuleId = "Far", Alternatives = new List<CourseAlternative> {
                    Alternative("OutStraight", "", new[] { "StartEast", "EastFar" }, "Gate", "Hub", "Depot"),
                    Alternative("OutMirror", "", new[] { "StartEast", "EastFar" }, "Gate", "Hub", "Depot")
                }
            });
            d.RouteSections.Add(new CourseRouteSection {
                Id = "Back", EntryModuleId = "Far", ExitModuleId = "Start", Alternatives = new List<CourseAlternative> {
                    Alternative("BackNorth", "", north, "Lookout", "Home"), Alternative("BackSouth", "", south, "Watch", "Home")
                }
            });
            d.RouteSections.Add(new CourseRouteSection {
                Id = "Fork", EntryModuleId = "Far", ExitModuleId = "Start", Kind = CourseRouteSection.ChoiceKind,
                Alternatives = new List<CourseAlternative> {
                    Alternative("ForkNorth", "A", north, "Lookout", "Home"), Alternative("ForkSouth", "B", south, "Watch", "Home")
                }
            });
            d.Laps.Add(new CourseLap { Number = 1, StartModuleId = "Start", FinishModuleId = "Start", NarrativeId = "FindAddress", SectionIds = S("Out", "Back") });
            d.Laps.Add(new CourseLap { Number = 2, StartModuleId = "Start", FinishModuleId = "Start", NarrativeId = "WrongDelivery", SectionIds = S("Out", "Fork") });
            d.Laps.Add(new CourseLap { Number = 3, StartModuleId = "Start", FinishModuleId = "Start", NarrativeId = "RestoreHome", SectionIds = S("Out", "Fork") });
            d.StoryAnchors.Add(new CourseStoryAnchor { Id = "HubDesk", ModuleId = "East", Kind = "Hide", Position = P(25000, -5000) });
            d.StoryAnchors.Add(new CourseStoryAnchor { Id = "StartLamp", ModuleId = "Start", Kind = "Post", Position = P(5000, 5000), RadiusMm = 500 });
            d.StoryAnchors.Add(new CourseStoryAnchor { Id = "HomeDoor", ModuleId = "Start", Kind = "Home", Position = P(0, 0), RadiusMm = 2000 });
            return d;
        }

        static CourseDocument LayoutArena()
        {
            var d = CourseTemplates.CombatArena();
            for (int i = 0; i < 8; i++)
                d.Spawns.Add(new CourseSpawn { Id = "AltRespawn" + i, ModuleId = "Start", Kind = "Respawn", Position = P(-7000 + i * 2000, 9500) });
            d.Layouts.Add(new CourseLayout { Id = "Castle", Weight = 2, ZoneIds = S("ArenaProps"), SpawnIds = Enumerable.Range(0, 8).Select(i => "Respawn" + i).ToList() });
            d.Layouts.Add(new CourseLayout { Id = "Ballpit", ZoneIds = S("ArenaResources"), SpawnIds = Enumerable.Range(0, 8).Select(i => "AltRespawn" + i).ToList() });
            return d;
        }

        [Test]
        public void LegacyProofDocumentsAndManifestsKeepTheirPinnedHashes()
        {
            var race = WorkshopTemplates.Create(WorkshopTemplate.RaceCircuit);
            var documents = new Dictionary<string, CourseDocument>
            {
                ["eff54dc829bef742222579bf83504da0309bdefa0bdc060666d1cd13853b54bf"] = CourseTemplates.RaceCircuit(),
                ["169565d934ff47396ef8823876df5a114690df439e601d4355815ec313ad0729"] = CourseTemplates.CombatArena(),
                ["b5a2867530c1f4179715a1d85b0556623f6e5c3248ba63dc43e9b1c6c7c90fd2"] = CourseTemplates.FreestyleWorld(),
                ["c8433436e1ce80a3d7f04ad7c0956862339ea41139b8fdb8e49b6c5668f30eb3"] = CourseJson.ImportDocument(race.Snapshot.CourseJsonText).Document
            };
            foreach (var pair in documents) Assert.AreEqual(pair.Key, CourseJson.Sha256(CourseJson.ExportDocument(pair.Value)));
            var cases = new[]
            {
                (CourseTemplates.RaceCircuit(), "TeamRace", RaceFormat.Duo, "0123456789abcdef",
                    "01003a39cd029074b3e973db6d40d3b33202eb21c8b4a1f37bd064fbb9d2bb3f", "8d1b810bfb4404ae71b2a227b66a5f9dc2ab3f217d7922af136d31fcfa9925ef"),
                (CourseJson.ImportDocument(race.Snapshot.CourseJsonText).Document, "BrawlRace", RaceFormat.Squad, "123456789abcdef0",
                    "d8d4817767787ad7dd91d63eb63b7f87426bf3a987f00e2fa927d1f1c9d63afa", "cf330546b71ef1dcd83655d43a293652cb2155bea994f28404eeed17ba7a7ec6"),
                (CourseTemplates.CombatArena(), "FreeForAll", RaceFormat.Solo, "0123456789abcdef",
                    "cd11f814341210d6440b653553ed91a0fe2c3f460b88693ad171ce60e6b6ea24", ""),
                (CourseTemplates.FreestyleWorld(), "Freestyle", RaceFormat.Solo, "ffffffffffffffff",
                    "5c7a07e54e50601a7bb7fb21516b97f1c5c87790596acebbd162738c43e792ec", "")
            };
            foreach (var (document, modeId, format, seed, manifestHash, definitionHash) in cases)
            {
                var result = Resolve(document, seed, modeId, format);
                Assert.IsTrue(result.Success, CourseCoreTests.Errors(result.Issues));
                Assert.AreEqual(manifestHash, result.Manifest.Hash, modeId);
                Assert.AreEqual(CourseResolver.GeneratorVersion, result.Manifest.GeneratorVersion);
                Assert.AreEqual("", result.Manifest.LayoutId);
                CollectionAssert.AreEqual(result.Manifest.Laps, result.Manifest.LapVariants);
                var mode = ModeDefinition.For(modeId, format);
                if (!mode.IsRace) continue;
                Assert.AreEqual(definitionHash, Definition(mode, MatchStateValidation.Route(result.Manifest)));
                Assert.AreEqual(definitionHash, Definition(mode, RaceLap.FromCourse(result.Manifest)));
            }
        }

        [Test]
        public void ShippedPublicCatalogueManifestsAreReproducedExactly()
        {
            var flagship = WordboundFlagshipContent.Create(TestData.Catalogue());
            var rows = Json.Parse(File.ReadAllText(Path.Combine(TestData.Root, PublicCataloguePath)), PublicCataloguePath)["courses"].Items;
            Assert.Greater(rows.Count, 0);
            foreach (var row in rows)
            {
                string courseId = row["content"]["courseId"].String(), seed = row["seed"].String();
                var mode = ModeDefinition.For(row["mode"].String(), (RaceFormat)Enum.Parse(typeof(RaceFormat), row["format"].String()));
                var play = MatchStateValidation.Requirements(mode);
                var result = courseId == WordboundFlagshipContent.CourseId ? flagship.Resolve(seed, play) :
                    courseId == "arena-proof" ? CourseResolver.Resolve(CourseTemplates.CombatArena(), CourseTemplates.ProofCatalogue(), new CourseResolveRequest(seed, play)) : null;
                Assert.IsNotNull(result, courseId);
                Assert.IsTrue(result.Success, courseId + "/" + mode.Id + "\n" + CourseCoreTests.Errors(result.Issues));
                Assert.AreEqual(row["content"]["courseHash"].String(), result.Manifest.SourceHash, courseId + "/" + mode.Id + "/" + seed);
                Assert.AreEqual(row["content"]["manifestHash"].String(), result.Manifest.Hash, courseId + "/" + mode.Id + "/" + seed);
            }
        }

        [TestCase("flagship"), TestCase("kitchen"), TestCase("parcel")]
        [TestCase("workshop-race"), TestCase("workshop-arena"), TestCase("workshop-freestyle")]
        public void ShippedLegacyCoursesNeverEmitSchema3Fields(string course)
        {
            CourseDocument document; CourseCatalogue catalogue;
            switch (course)
            {
                case "flagship": { var c = WordboundFlagshipContent.Create(TestData.Catalogue()); document = c.CopyDocument(); catalogue = c.Geometry; break; }
                case "kitchen":
                {
                    var c = WordboundKitchenPilotContent.Create(ItemCatalogue.FromJson(File.ReadAllText(Path.Combine(TestData.Root, KitchenItemsPath)), KitchenItemsPath));
                    document = c.CopyDocument(); catalogue = c.Geometry; break;
                }
                case "parcel": { var c = WordboundParcelServiceContent.Create(TestData.Catalogue()); document = c.CopyDocument(); catalogue = c.Geometry; break; }
                default:
                {
                    var preset = WorkshopTemplates.Create(course == "workshop-race" ? WorkshopTemplate.RaceCircuit :
                        course == "workshop-arena" ? WorkshopTemplate.CombatArena : WorkshopTemplate.FreestyleWorld);
                    document = CourseJson.ImportDocument(preset.Snapshot.CourseJsonText).Document; catalogue = preset.Catalogue; break;
                }
            }
            string json = CourseJson.ExportDocument(document);
            Assert.IsTrue(document.Schema == 1 || document.Schema == 2, course);
            foreach (string key in Schema3Keys) Assert.IsFalse(json.Contains(key), course + " " + key);
            Assert.AreEqual(json, CourseJson.ExportDocument(CourseJson.ImportDocument(json).Document));
            var play = Play(document.SupportedModes.OrderBy(m => m, StringComparer.Ordinal).First());
            var result = CourseResolver.Resolve(document, catalogue, new CourseResolveRequest("0123456789abcdef", play));
            Assert.IsTrue(result.Success, CourseCoreTests.Errors(result.Issues));
            string manifest = CourseJson.ExportManifest(result.Manifest);
            foreach (string key in Schema3Keys) Assert.IsFalse(manifest.Contains(key), course + " manifest " + key);
            Assert.AreEqual(CourseResolver.GeneratorVersion, result.Manifest.GeneratorVersion);
            Assert.AreEqual(document.Schema, result.Manifest.Schema);
            Assert.AreEqual(manifest, CourseJson.ExportManifest(CourseJson.ImportManifest(manifest, catalogue).Manifest));
            Assert.IsTrue(result.Manifest.Connections.All(c => c.Scope == CourseConnection.SharedScope && !c.TeamScoped));
            Assert.IsTrue(result.Manifest.LapVariants.All(l => l.OptionId == "" && l.Checkpoints.All(c => c.Solutions.Count == 0)));
            Assert.IsEmpty(result.Manifest.StoryAnchors);
        }

        [Test]
        public void Schema3DocumentRoundTripsCanonicallyAndOnlyWithItsOwnFields()
        {
            string json = CourseJson.ExportDocument(VariantCourse());
            Assert.IsTrue(json.Contains("\"schema\":3"));
            foreach (string key in Schema3Keys.Where(k => k != "\"layoutId\":")) Assert.IsTrue(json.Contains(key), key);
            var imported = CourseJson.ImportDocument(json);
            Assert.IsTrue(imported.Success, CourseCoreTests.Errors(imported.Issues));
            Assert.AreEqual(3, imported.Document.Schema);
            Assert.AreEqual(json, CourseJson.ExportDocument(imported.Document));
            Assert.AreEqual("Team", imported.Document.Connections.Single(c => c.Id == "Dash").Scope);
            Assert.AreEqual("Dash", imported.Document.Checkpoints.Single(c => c.Id == "Lookout").Solutions.Single(s => s.Id == "Grand").ShortcutConnectionId);
            Assert.IsFalse(CourseJson.ImportDocument(json.Replace("\"schema\":3", "\"schema\":2")).Success);
            Assert.IsFalse(CourseJson.ImportDocument(json.Replace("\"schema\":3", "\"schema\":4")).Success);
            Assert.IsFalse(CourseJson.ImportDocument(CourseJson.ExportDocument(CourseTemplates.RaceCircuit()).Replace("\"schema\":1", "\"schema\":3")).Success);
            var legacy = CourseTemplates.CombatArena();
            legacy.Layouts.Add(new CourseLayout { Id = "Everything" });
            Assert.IsTrue(CourseJson.ExportDocument(legacy).Contains("\"schema\":3"));
            var reordered = VariantCourse();
            reordered.Checkpoints.Reverse(); reordered.StoryAnchors.Reverse();
            foreach (var checkpoint in reordered.Checkpoints) checkpoint.Solutions.Reverse();
            Assert.AreEqual(json, CourseJson.ExportDocument(reordered));
        }

        [Test]
        public void VariantCourseRetainsEveryChoiceOptionWithSharedSeededBranches()
        {
            var d = VariantCourse();
            var cat = CourseTemplates.ProofCatalogue();
            Assert.IsTrue(CourseValidator.Validate(d, cat, Play("TeamRace")).Ok, CourseCoreTests.Errors(CourseValidator.Validate(d, cat, Play("TeamRace")).Issues));
            foreach (string seed in new[] { "0000000000000000", "0123456789abcdef", "ffffffffffffffff" })
            {
                var result = Resolve(d, seed);
                Assert.IsTrue(result.Success, CourseCoreTests.Errors(result.Issues));
                var m = result.Manifest;
                Assert.AreEqual(3, m.Schema); Assert.AreEqual(3, m.CanonicalVersion);
                Assert.AreEqual(CourseResolver.VariantGeneratorVersion, m.GeneratorVersion);
                CollectionAssert.AreEqual(new[] { "1/", "2/A", "2/B", "3/A", "3/B" }, m.LapVariants.Select(l => l.Number + "/" + l.OptionId));
                CollectionAssert.AreEqual(new[] { "", "A", "A" }, m.Laps.Select(l => l.OptionId));
                foreach (int lap in new[] { 2, 3 })
                {
                    var a = m.Variant(lap, "A"); var b = m.Variant(lap, "B");
                    Assert.AreSame(a, m.Variant(lap, ""));
                    Assert.IsNull(m.Variant(lap, "C"));
                    Assert.AreEqual(a.AlternativeIds[0], b.AlternativeIds[0]);
                    CollectionAssert.AreEqual(new[] { "Gate", "Hub", "Depot", "Lookout", "Home" }, a.Checkpoints.Select(c => c.Id));
                    CollectionAssert.AreEqual(new[] { "Gate", "Hub", "Depot", "Watch", "Home" }, b.Checkpoints.Select(c => c.Id));
                    Assert.AreEqual("ForkNorth", a.AlternativeIds[1]); Assert.AreEqual("ForkSouth", b.AlternativeIds[1]);
                }
                Assert.AreNotEqual(m.Laps[0].AlternativeIds[0], m.Laps[1].AlternativeIds[0]);
                Assert.AreNotEqual(m.Laps[1].AlternativeIds[0], m.Laps[2].AlternativeIds[0]);
                var lookout = m.Variant(2, "A").Checkpoints.Single(c => c.Id == "Lookout");
                CollectionAssert.AreEqual(new[] { "Grand", "Quick" }, lookout.Solutions.Select(s => s.Id));
                CollectionAssert.AreEqual(new[] { "LookoutStool", "LookoutBed" }, lookout.PuzzleIds);
                Assert.AreEqual("Dash", lookout.Solutions[0].ShortcutConnectionId);
                Assert.IsTrue(m.Connections.Single(c => c.Id == "Dash").TeamScoped);
                CollectionAssert.AreEqual(new[] { "HomeDoor", "HubDesk", "StartLamp" }, m.StoryAnchors.Select(a => a.Id));
                string json = CourseJson.ExportManifest(m);
                var restored = CourseJson.ImportManifest(json, cat);
                Assert.IsTrue(restored.Success, CourseCoreTests.Errors(restored.Issues));
                Assert.AreEqual(json, CourseJson.ExportManifest(restored.Manifest));
                Assert.IsTrue(CourseValidator.ValidateResolved(restored.Manifest, cat, Play("TeamRace")).Ok);
                Assert.AreEqual(m.Hash, Resolve(d, seed).Manifest.Hash);
                var route = RaceLap.FromCourse(m);
                Assert.IsFalse(route[0].HasChoice);
                Assert.IsTrue(route[1].HasChoice && route[2].HasChoice);
                Assert.AreEqual(3, route[1].Divergence);
                Assert.AreEqual(2, route[1].For("A").Checkpoints[3].Solutions.Count);
                Assert.AreNotEqual(Definition(ModeDefinition.For("TeamRace"), route), Definition(ModeDefinition.For("TeamRace"), MatchStateValidation.Route(m)));
            }
            var timed = Resolve(d, "0000000000000001", "TimeRun");
            Assert.IsTrue(timed.Success, CourseCoreTests.Errors(timed.Issues));
            Assert.AreEqual(5, timed.Manifest.LapVariants.Count);
        }

        [TestCase("connection.scope"), TestCase("route.kind"), TestCase("choice.options"), TestCase("checkpoint.solutions")]
        [TestCase("solution.overlap"), TestCase("shortcut.reference"), TestCase("anchor.kind"), TestCase("anchor.radius")]
        [TestCase("layout.weight"), TestCase("shortcut.longest"), TestCase("shortcut.scope"), TestCase("shortcut.gate")]
        [TestCase("shortcut.route"), TestCase("shortcut.saving"), TestCase("choice.count"), TestCase("choice.first")]
        [TestCase("choice.lap"), TestCase("choice.checkpoints"), TestCase("choice.length"), TestCase("route.length")]
        [TestCase("anchor.location"), TestCase("layout.reference"), TestCase("layout.capacity")]
        public void NewRuleRejectsWithItsOwnCode(string code)
        {
            bool arena = code.StartsWith("layout.", StringComparison.Ordinal);
            var d = arena ? LayoutArena() : VariantCourse();
            var dash = d.Connections.SingleOrDefault(c => c.Id == "Dash");
            var lookout = d.Checkpoints.SingleOrDefault(c => c.Id == "Lookout");
            var fork = d.RouteSections.SingleOrDefault(s => s.Id == "Fork");
            switch (code)
            {
                case "connection.scope": d.Connections.Single(c => c.Id == "StartEast").Scope = CourseConnection.TeamScope; break;
                case "route.kind": d.RouteSections.Single(s => s.Id == "Back").Kind = "Random"; break;
                case "choice.options": fork.Alternatives[1].OptionId = "C"; break;
                case "checkpoint.solutions": lookout.PuzzleIds.Add("LookoutBed"); break;
                case "solution.overlap": lookout.Solutions[0].PuzzleIds.Add("LookoutStool"); break;
                case "shortcut.reference": lookout.Solutions[0].ShortcutConnectionId = "Teleporter"; break;
                case "anchor.kind": d.StoryAnchors[0].Kind = "Treasure"; break;
                case "anchor.radius": d.StoryAnchors[0].RadiusMm = 50; break;
                case "layout.weight": d.Layouts[0].Weight = 0; break;
                case "shortcut.longest": d.Puzzles.Single(p => p.Id == "LookoutStool").RecipeId = ""; d.Puzzles.Single(p => p.Id == "LookoutStool").RequiredLetters = "BED"; break;
                case "shortcut.scope": dash.Scope = CourseConnection.SharedScope; break;
                case "shortcut.gate": dash.GatePuzzleId = "LookoutBed"; break;
                case "shortcut.route": dash.FromModuleId = "North"; dash.From = P(10000, 20000); break;
                case "shortcut.saving": dash.From = P(50000, 10000); dash.To = P(-10000, 10000); break;
                case "choice.count":
                    d.RouteSections.Add(new CourseRouteSection {
                        Id = "Encore", EntryModuleId = "Start", ExitModuleId = "Start", Kind = CourseRouteSection.ChoiceKind,
                        Alternatives = new List<CourseAlternative> {
                            new CourseAlternative { Id = "EncoreA", OptionId = "A" }, new CourseAlternative { Id = "EncoreB", OptionId = "B" }
                        }
                    });
                    d.Laps[1].SectionIds.Add("Encore"); break;
                case "choice.first":
                    d.RouteSections.Add(new CourseRouteSection {
                        Id = "OutChoice", EntryModuleId = "Start", ExitModuleId = "Far", Kind = CourseRouteSection.ChoiceKind,
                        Alternatives = new List<CourseAlternative> {
                            new CourseAlternative { Id = "OutA", OptionId = "A", ConnectionIds = S("StartEast", "EastFar"), CheckpointIds = S("Gate", "Hub", "Depot") },
                            new CourseAlternative { Id = "OutB", OptionId = "B", ConnectionIds = S("StartEast", "EastFar"), CheckpointIds = S("Gate", "Hub", "Depot") }
                        }
                    });
                    d.Laps[1].SectionIds = S("OutChoice", "Back"); break;
                case "choice.lap": d.Laps[0].SectionIds = S("Out", "Fork"); break;
                case "choice.checkpoints": fork.Alternatives[1].CheckpointIds = S("Home"); break;
                case "choice.length":
                    d.Connections.Single(c => c.Id == "SouthFarSouth").From = d.Connections.Single(c => c.Id == "SouthFarSouth").To = P(30000, -29000);
                    d.Connections.Single(c => c.Id == "SouthSouthWest").From = d.Connections.Single(c => c.Id == "SouthSouthWest").To = P(10000, -29000); break;
                case "route.length":
                    d.RouteSections.Single(s => s.Id == "Back").Alternatives.Add(new CourseAlternative {
                        Id = "BackLong", ConnectionIds = S("FarSouthFar", "SouthFarSouth", "SouthEast", "EastNorth", "NorthNorthWest", "NorthWestStart"),
                        CheckpointIds = S("Watch", "Home")
                    }); break;
                case "anchor.location": d.StoryAnchors[0].Position = P(29500, -5000); break;
                case "layout.reference": d.Layouts[0].ZoneIds.Add("Missing"); break;
                case "layout.capacity": d.Spawns.RemoveAll(s => s.Id == "AltRespawn7"); d.Layouts[1].SpawnIds.Remove("AltRespawn7"); break;
            }
            var play = arena ? Play("FreeForAll") : Play("TeamRace");
            var result = CourseValidator.Validate(d, CourseTemplates.ProofCatalogue(), play);
            Assert.IsTrue(result.Issues.Any(i => i.Code == code), code + "\n" + CourseCoreTests.Errors(result.Issues));
            var pristine = CourseValidator.Validate(arena ? LayoutArena() : VariantCourse(), CourseTemplates.ProofCatalogue(), play);
            Assert.IsTrue(pristine.Ok, CourseCoreTests.Errors(pristine.Issues));
        }

        [TestCase("unrecoverable-solution", "resource.recovery")]
        [TestCase("solution-only-route-gate", "gate.prefix")]
        public void EverySolutionMustBeCompletableOnItsOwn(string mutation, string code)
        {
            var d = VariantCourse();
            if (mutation == "unrecoverable-solution") d.Puzzles.Single(p => p.Id == "LookoutBed").RecoverySupplyIds.Clear();
            else d.Connections.Single(c => c.Id == "NorthFarNorth").GatePuzzleId = "LookoutStool";
            Assert.IsTrue(CourseValidator.Validate(d, CourseTemplates.ProofCatalogue(), Play("TeamRace")).Ok);
            var result = Resolve(d, "0000000000000000");
            Assert.IsFalse(result.Success); Assert.IsNull(result.Manifest);
            Assert.IsTrue(result.Issues.Any(i => i.Code == code), CourseCoreTests.Errors(result.Issues));
            Assert.AreEqual(CourseLimits.Attempts, result.AttemptsUsed);
        }

        [Test]
        public void LayoutPickIsSeededDeterministicAndResolvesOnlyItsZonesAndSpawns()
        {
            var d = LayoutArena();
            var cat = CourseTemplates.ProofCatalogue();
            var picked = new HashSet<string>(StringComparer.Ordinal);
            for (int seed = 0; seed < 32; seed++)
            {
                string hex = seed.ToString("x16", System.Globalization.CultureInfo.InvariantCulture);
                var result = Resolve(d, hex, "FreeForAll");
                Assert.IsTrue(result.Success, CourseCoreTests.Errors(result.Issues));
                var m = result.Manifest;
                picked.Add(m.LayoutId);
                if (seed % 8 == 0) Assert.AreEqual(m.Hash, Resolve(LayoutArena(), hex, "FreeForAll").Manifest.Hash);
                string zone = m.LayoutId == "Castle" ? "ArenaProps" : "ArenaResources";
                Assert.IsTrue(m.Placements.All(p => p.ZoneId == zone), m.LayoutId);
                Assert.IsTrue(m.Placements.Any());
                string respawn = m.LayoutId == "Castle" ? "Respawn" : "AltRespawn";
                CollectionAssert.AreEquivalent(Enumerable.Range(0, 8).Select(i => "Start" + i).Concat(Enumerable.Range(0, 8).Select(i => respawn + i)),
                    m.Spawns.Select(s => s.Id));
                var restored = CourseJson.ImportManifest(CourseJson.ExportManifest(m), cat);
                Assert.IsTrue(restored.Success, CourseCoreTests.Errors(restored.Issues));
                Assert.AreEqual(m.LayoutId, restored.Manifest.LayoutId);
                Assert.AreEqual(CourseResolver.VariantGeneratorVersion, m.GeneratorVersion);
            }
            CollectionAssert.AreEquivalent(new[] { "Ballpit", "Castle" }, picked);
        }

        [TestCase("layout"), TestCase("variant")]
        public void RehashedManifestsCannotSwapLayoutsOrSplitSeededBranches(string mutation)
        {
            var cat = CourseTemplates.ProofCatalogue();
            var result = mutation == "layout" ? Resolve(LayoutArena(), "0000000000000000", "FreeForAll") : Resolve(VariantCourse(), "0000000000000000");
            Assert.IsTrue(result.Success, CourseCoreTests.Errors(result.Issues));
            string json = CourseJson.ExportManifest(result.Manifest), forged;
            if (mutation == "layout")
            {
                string other = result.Manifest.LayoutId == "Castle" ? "Ballpit" : "Castle";
                forged = json.Replace("\"layoutId\":\"" + result.Manifest.LayoutId + "\"", "\"layoutId\":\"" + other + "\"");
            }
            else
            {
                var lap = result.Manifest.Variant(2, "B");
                string pick = lap.AlternativeIds[0], swap = pick == "OutStraight" ? "OutMirror" : "OutStraight";
                int at = json.IndexOf("\"number\":2,\"optionId\":\"B\"", StringComparison.Ordinal);
                int start = json.LastIndexOf("{\"alternativeIds\":[\"" + pick + "\"", at, StringComparison.Ordinal);
                Assert.Greater(start, 0);
                forged = json.Substring(0, start) + "{\"alternativeIds\":[\"" + swap + "\"" + json.Substring(start + ("{\"alternativeIds\":[\"" + pick + "\"").Length);
            }
            Assert.AreNotEqual(json, forged);
            string without = Regex.Replace(forged, "\"hash\":\"[0-9a-f]{64}\",", "");
            forged = Regex.Replace(forged, "\"hash\":\"[0-9a-f]{64}\"", "\"hash\":\"" + CourseJson.Sha256(without) + "\"");
            var imported = CourseJson.ImportManifest(forged, cat);
            Assert.IsFalse(imported.Success);
            Assert.IsTrue(imported.Issues.Any(i => i.Code == "manifest." + mutation), CourseCoreTests.Errors(imported.Issues));
        }
    }
}
