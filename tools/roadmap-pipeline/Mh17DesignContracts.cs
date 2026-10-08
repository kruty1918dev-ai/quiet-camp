using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Application;
using QuietCamp.Domain;

/// <summary>Checks a design draft with the real compiler. Planned assets are declarations,
/// never treated as existing models, approved artwork, authored puzzles or historical exhibits.</summary>
static class Mh17DesignContracts
{
    const string Root = "Design/Roadmap/Regions/quiet-field/";
    static void Require(bool ok, string reason) { if (!ok) throw new InvalidOperationException("MH17 design: " + reason); }
    static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));

    static void Disclosure(EnvironmentalStoryData story, RoadmapDefinition map, JObject manifest)
    {
        var models = ((JArray)manifest["assets"]).ToDictionary(a => (string)a["id"]);
        Require(EnvironmentalStoryValidator.Validate(story, id => map.regions.SelectMany(r => r.nodePositions).Any(n => n.levelId == id), models.ContainsKey).Count == 0,
            "invalid story/claim/view structure");
        var assets = new List<RoadmapPropData>();
        foreach (var node in map.regions.SelectMany(r => r.nodePositions)) RoadmapStoryCompiler.Bake(story, node.levelId, assets);
        Require(assets.Count == 1 && assets[0].assetId == "quiet_field_aircraft_map", "compound landmark duplicated or detail leaked into roadmap");
        foreach (var prop in assets) Require((string)models[prop.assetId]["view"] == "map", "level-only model baked into roadmap");
        foreach (var model in models.Values)
        {
            Require((string)model["status"] == "planned" && !(bool)model["assetReviewPassed"], "design falsely claims artwork approval");
            if ((string)model["view"] == "map") Require(model["marking"] == null, "roadmap asset declares technical marking");
        }
        var marking = models["quiet_field_engine_casing_detail"]["marking"];
        Require((string)marking["text"] == "9Д131" && !(bool)marking["uniqueSerialAllowed"] && !(bool)marking["operatorAttributionAllowed"],
            "unsupported marking/serial/attribution");
        Require((string)marking["claimId"] == "quiet-field:engine-type", "marking lacks its source claim");
        foreach (var claim in story.references.SelectMany(r => r.claims))
            Require(!claim.statement.Contains("S-300", StringComparison.OrdinalIgnoreCase), "wrong weapon reference");
        Require(!(bool)manifest["published"] && (string)manifest["publicationStatus"] == "design-only", "draft cannot be presented as a release");
        Require(((JObject)manifest["releaseGates"]).Properties().All(g => g.Value.Type == JTokenType.Boolean && !(bool)g.Value),
            "unperformed release checks claimed as passed");
    }

    public static void Run()
    {
        var rawMap = JObject.Parse(File.ReadAllText(Root + "region-draft.json"));
        Require(!(bool)rawMap["published"] && (string)rawMap["environmentalStoryFile"] == "story.json", "draft publication/source guard");
        var map = rawMap.ToObject<RoadmapDefinition>();
        var story = JsonConvert.DeserializeObject<EnvironmentalStoryData>(File.ReadAllText(Root + "story.json"));
        var manifest = JObject.Parse(File.ReadAllText(Root + "assets.json"));
        var locales = JObject.Parse(File.ReadAllText(Root + "localization-draft.json"));
        Require(map.regions.Length == 1 && map.regions[0].nodePositions.Length == 4, "wrong region/level count");
        map.regions[0].environmentalStory = story;
        var nodes = map.regions[0].nodePositions;
        var declaredAssets = ((JArray)manifest["assets"]).ToDictionary(a => (string)a["id"]);
        var errors = RoadmapValidator.Validate(map,
            previewExists: id => nodes.Any(n => n.levelId == id),
            localized: key => new[] { "uk", "en", "de" }.All(lang => !string.IsNullOrWhiteSpace((string)locales[lang][key])),
            modelExists: declaredAssets.ContainsKey);
        Require(errors.Count == 0, string.Join("; ", errors));
        Disclosure(story, map, manifest);
        Console.WriteLine("PASS draft structure: one region / four node IDs / two chunks / uk-en-de labels / sourced claim bindings");
        Console.WriteLine("PASS real story compiler: exactly one compound aircraft; zero detail-only props baked into roadmap");

        int maxProps = 0, maxTriangles = 0;
        foreach (var node in nodes)
        {
            var excerpt = RoadmapStoryCompiler.ForLevel(story, node.levelId);
            Require(excerpt != null, "missing level excerpt");
            var snapshot = Copy(new LevelData { id = node.levelId, environmentalStory = excerpt });
            Require(snapshot.environmentalStory.beats.Length == excerpt.beats.Length, "snapshot lost story bindings");
            var props = excerpt.beats.SelectMany(b => b.props).Where(p => p.level != null).ToArray();
            maxProps = Math.Max(maxProps, props.Length);
            foreach (bool cared in new[] { false, true })
            {
                int triangles = props.Where(p => EnvironmentalStoryPolicy.Appears(p.appearance, cared))
                    .Sum(p => (int)declaredAssets[p.level.assetId]["maxTriangles"]);
                maxTriangles = Math.Max(maxTriangles, triangles);
                Require(triangles <= (int)manifest["budgets"]["maxStoryTrianglesPerLevel"], "declared triangle budget exceeded");
                foreach (var aircraft in props.Where(p => p.id == "quiet-field:front" || p.id == "quiet-field:rear"))
                    Require(EnvironmentalStoryPolicy.Appears(aircraft.appearance, cared), "care removed aircraft");
                foreach (var clue in props.Where(p => p.claimId != null))
                    Require(EnvironmentalStoryPolicy.Appears(clue.appearance, cared) && pHidden(clue), "care/reveal altered technical clue");
            }
        }
        Require(maxProps <= 12, "declared level prop budget exceeded");
        Console.WriteLine($"PASS excerpts/snapshot/care declarations: max {maxProps} detail props, max {maxTriangles} planned triangles per state (not measured meshes)");

        var catalog = new RoadmapCatalog(map);
        foreach (int completed in new[] { 0, 1, 2, 3, 4 })
        {
            var progress = new ProgressionService();
            for (int i = 0; i < completed; i++) progress.MarkCompleted(nodes[i].levelId);
            var reveal = new RoadmapRevealState(catalog, progress);
            var restored = new ProgressionService(); restored.Restore(progress.CompletedIds, progress.LastLevelId, 0);
            Require(new RoadmapRevealState(catalog, restored).LastKnown == reveal.LastKnown, "restart altered reveal");
            int revision = progress.Revision;
            if (completed > 0) { progress.MarkCompleted(nodes[0].levelId); Require(progress.Revision == revision && !reveal.Refresh(), "replay revealed future"); }
            for (int i = 0; i < nodes.Length; i++)
                if (i > reveal.LastKnown) Require(reveal.Main(i) == RoadmapReveal.Hidden, "future node disclosed");
        }
        var window = new RoadmapWindow();
        for (int i = 0; i < catalog.Chunks.Length; i++) { window.Move(i, catalog.Chunks.Length); Require(window.Count <= 3, "chunk budget exceeded"); }
        Console.WriteLine("PASS progression/restart/replay at 0–4 completed; bounded window planner (not rendered reveal)");

        int negative = 0;
        void Bad(Action<EnvironmentalStoryData, JObject> mutate)
        {
            var s = Copy(story); var m = (JObject)manifest.DeepClone(); mutate(s, m);
            bool rejected = false; try { Disclosure(s, map, m); }
            catch (InvalidOperationException e) when (e.Message.StartsWith("MH17 design: ", StringComparison.Ordinal)) { rejected = true; }
            Require(rejected, "unsafe design mutation accepted"); negative++;
        }
        Bad((s,m) => s.beats.Single(b => b.id == "quiet-field:roadmap-context").levelIds = nodes.Select(n => n.levelId).ToArray());
        Bad((s,m) => {
            var b = s.beats.Single(b => b.id == "quiet-field:technical-clue"); b.roadmapVisibility = "context";
            var p = b.props[0]; p.roadmapVisibility = "context"; p.roadmap = Copy(p.level);
        });
        Bad((s,m) => s.beats.Single(b => b.id == "quiet-field:technical-clue").props[0].claimId = "unknown");
        Bad((s,m) => s.references.Single(r => r.basis == "documented").claims[0].sourceIds = new[] { "unknown" });
        Bad((s,m) => s.references.Single(r => r.basis == "documented").claims[0].statement = "S-300 reference");
        Bad((s,m) => ((JArray)m["assets"]).Single(a => (string)a["id"] == "quiet_field_engine_casing_detail")["marking"]["text"] = "9Д131999999");
        Bad((s,m) => m["published"] = true);
        Bad((s,m) => m["releaseGates"]["puzzles"] = true);
        Console.WriteLine($"PASS {negative} rejection cases: duplicated landmark, evidence leak, orphan claim/source, wrong system, invented serial, false publication/check status");

        var realAssets = new HashSet<string>();
        foreach (var file in new[] { "roadmap_models", "roadmap_story_models" })
            foreach (var model in JArray.Parse(File.ReadAllText("QuietCamp/Assets/QuietCamp/Resources/QuietCamp/" + file + ".json"))) realAssets.Add((string)model["id"]);
        var releaseErrors = EnvironmentalStoryValidator.Validate(story, assetExists: realAssets.Contains);
        Require(releaseErrors.Count > 0 && releaseErrors.All(e => e.StartsWith("story:missing-asset:")), "unexpected actual asset validation result");
        Require(((JArray)manifest["puzzleBriefs"]).All(p => (string)p["solverStatus"] == "not-authored" && (string)p["witnessStatus"] == "not-authored"), "brief pretends to be solver-certified");
        Console.WriteLine($"EXPECTED RELEASE BLOCK: {declaredAssets.Keys.Count(a => !realAssets.Contains(a))} planned models absent; {releaseErrors.Count} actual missing-asset diagnostics. Puzzles/previews/artwork/device QA pending.");
        Console.WriteLine("Design contracts passed. No assets imported, campaign changed, Unity launched or player built.");
    }

    static bool pHidden(EnvironmentalStoryProp prop) => prop.roadmapVisibility == "hidden" && prop.roadmap == null;
}
