using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Application;
using QuietCamp.Domain;

/// <summary>Unpublished regional authoring and offline crossing graph reference.
/// Does not implement production layered rules, physics, rendering or independent solver.</summary>
static class WaterMemoryDesignContracts
{
    const string Root = "Design/Roadmap/Regions/water-memory/";
    static void Require(bool ok, string reason)
    { if (!ok) throw new InvalidOperationException("Water memory design: " + reason); }
    static T Read<T>(string name) => JsonConvert.DeserializeObject<T>(File.ReadAllText(Root + name));
    static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));

    static void Regional(EnvironmentalStoryData story, RoadmapDefinition map, JObject assets)
    {
        var models = ((JArray)assets["assets"]).ToDictionary(a => (string)a["id"]);
        var nodes = map.regions.SelectMany(r => r.nodePositions).ToArray();
        Require(EnvironmentalStoryValidator.Validate(story, id => nodes.Any(n => n.levelId == id), models.ContainsKey).Count == 0, "invalid story shape");
        Require(story.beats.All(b => b.referenceConfidence == "fiction-inspired"), "invented object passed off as documented evidence");
        var sources = story.references.SelectMany(r => r.sources).Select(s => s.id).ToHashSet();
        Require(new[] { "unep", "nasa", "unhcr", "mvs", "undp", "succession", "pdna" }.All(s => sources.Contains("water-memory:source-" + s)), "missing researched source binding");
        var baked = new List<RoadmapPropData>();
        foreach (var node in nodes) RoadmapStoryCompiler.Bake(story, node.levelId, baked);
        Require(baked.Count(p => p.assetId == "water_memory_hydro_map") == 1, "missing/duplicated hydro landmark");
        Require(baked.Count(p => p.assetId == "water_memory_boat_map") == 1, "duplicated stranded boat");
        Require(baked.All(p => (string)models[p.assetId]["view"] == "map" && models[p.assetId]["marking"] == null), "detail/text leaked into roadmap");
        Require(story.beats.Single(b => b.storyBeat == "animal-rescue").props.All(p => p.roadmap == null), "animal-rescue details leaked into context");
        Require(story.beats.Single(b => b.storyBeat == "hydro-context").props.All(p => p.appearance == "always"), "completion removed hydro damage");
        var geography = assets["geographyContract"];
        Require(((JArray)geography["upstreamStages"]).Values<string>().SequenceEqual(new[] { "former-shore" })
            && ((JArray)geography["downstreamStages"]).Values<string>().SequenceEqual(new[] { "flood-trace" })
            && ((JArray)geography["hydroStages"]).Values<string>().SequenceEqual(new[] { "dam-area" })
            && (bool)geography["roadBypassesHydroBeforeFloodTrace"] && !(bool)geography["depictsInstantForestRecovery"] && !(bool)geography["reviewed"], "incorrect/uncertified geography contract");
        foreach (var flag in ((JObject)assets["careContract"]).Properties()) Require(!(bool)flag.Value, "unsupported care/hazard behavior: " + flag.Name);
        var landmark = assets["landmarkPresentation"];
        Require((int)landmark["earliestTeaserOrder"] == 2 && (int)landmark["localOwnerOrder"] == 8 && (int)landmark["maxSimultaneousStructures"] == 1
            && (bool)landmark["usesProgression"] && !(bool)landmark["implemented"], "landmark identity/teaser contract");
        Require(((JArray)landmark["playableNearbyLevelIds"]).Values<string>().SequenceEqual(new[] { "QC_WATER008" }), "unfulfilled nearby destination");
        Require(!(bool)assets["published"] && (string)assets["publicationStatus"] == "design-only", "false publication");
        Require(((JObject)assets["releaseGates"]).Properties().All(p => !(bool)p.Value), "unperformed release gate");
        Require(models.Values.All(a => (string)a["status"] == "planned" && !(bool)a["assetReviewPassed"]), "uncertified artwork marked ready");
    }

    readonly record struct CellRef(string Surface, int X, int Z);
    static CellRef At(JToken t) => new((string)t["surfaceId"], (int)t["x"], (int)t["z"]);
    static CellRef[] Cells(string surface, JArray cells) => cells.Select(c => new CellRef(surface, (int)c[0], (int)c[1])).ToArray();

    sealed class CrossingReference
    {
        readonly Dictionary<string, JToken> _surfaces;
        readonly HashSet<CellRef> _walk = new(), _reserved = new();
        readonly List<(string Id, CellRef From, CellRef To)> _portals = new();
        readonly CellRef _origin;
        readonly CellRef[] _exits;
        public CrossingReference(JObject data)
        {
            Require(!(bool)data["runtimeImplemented"] && !(bool)data["productionWitnessCertified"] && (int)data["proposedRuleVersion"] == 3
                && !(bool)data["worldRiverCrossableWithoutConnector"] && !(bool)data["completionChangesTopology"], "invalid design-only topology contract");
            _surfaces = ((JArray)data["surfaces"]).ToDictionary(s => (string)s["id"]);
            Require(_surfaces.Count == 3 && (float)_surfaces["ridge"]["elevation"] > (float)_surfaces["east"]["elevation"], "missing terraces/elevation difference");
            foreach (var pair in _surfaces)
            {
                var cells = Cells(pair.Key, (JArray)pair.Value["cells"]);
                Require(cells.Distinct().Count() == cells.Length && cells.All(c => c.X >= 0 && c.Z >= 0 && c.X < (int)pair.Value["width"] && c.Z < (int)pair.Value["height"]), "invalid surface bounds");
                Require(cells.All(c => c.X + (int)pair.Value["worldOriginXZ"][0] != (int)data["waterGapWorldX"]), "water represented as dry platform");
                var blocked = Cells(pair.Key, (JArray)pair.Value["blocked"]);
                Require(blocked.All(cells.Contains) && Cells(pair.Key, (JArray)pair.Value["shade"]).All(cells.Contains), "invalid solid/shade mask");
                foreach (var c in cells.Except(blocked)) _walk.Add(c);
            }
            var ids = new HashSet<string>();
            foreach (var portal in (JArray)data["connectors"])
            {
                var a = At(portal["fromCell"]); var b = At(portal["toCell"]);
                Require(ids.Add((string)portal["id"]) && a.Surface != b.Surface && _walk.Contains(a) && _walk.Contains(b)
                    && (bool)portal["twoWay"] && (int)portal["cost"] > 0, "invalid connector endpoint/id");
                _portals.Add(((string)portal["id"], a, b)); _reserved.Add(a); _reserved.Add(b);
            }
            _origin = At(data["origin"]); _exits = ((JArray)data["accessPoints"]).Select(e => At(e["cell"])).ToArray();
            Require(_walk.Contains(_origin) && _exits.Length == 3 && _exits.Distinct().Count() == 3 && _exits.All(_walk.Contains), "invalid exits");
            _reserved.Add(_origin); foreach (var e in _exits) _reserved.Add(e);
        }
        static Placement Flat(JToken p) => new() { guestId = (string)p["guestId"], x = (int)p["x"], z = (int)p["z"], rotation = (int)p["rotation"] };
        IEnumerable<CellRef> Neighbors(CellRef a, ISet<string> disabled)
        {
            foreach (var d in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = new CellRef(a.Surface, a.X + d.Item1, a.Z + d.Item2); if (_walk.Contains(n)) yield return n;
            }
            foreach (var p in _portals)
            {
                if (disabled.Contains(p.Id)) continue;
                if (a == p.From) yield return p.To;
                if (a == p.To) yield return p.From;
            }
        }
        public List<string> Check(JArray placements, ISet<string> disabled = null)
        {
            disabled ??= new HashSet<string>(); var occupied = new HashSet<CellRef>(); var errors = new List<string>(); var ids = new HashSet<string>();
            foreach (var p in placements)
            {
                if (!ids.Add((string)p["guestId"])) errors.Add("guest-id");
                var sid = (string)p["surfaceId"];
                if (!_surfaces.TryGetValue(sid, out var surface)) { errors.Add("surface"); continue; }
                var footprint = RuleEvaluator.Footprint(Flat(p)).Select(c => new CellRef(sid, c.X, c.Z)).ToArray();
                if (footprint.Any(c => !_walk.Contains(c))) errors.Add("bounds-or-solid");
                if (footprint.Any(occupied.Contains)) errors.Add("overlap");
                if (footprint.Any(_reserved.Contains)) errors.Add("reserved-landing");
                if ((bool)p["shade"] && footprint.Any(c => !Cells(sid, (JArray)surface["shade"]).Contains(c))) errors.Add("shade");
                occupied.UnionWith(footprint);
            }
            var reached = new HashSet<CellRef>(); var queue = new Queue<CellRef>();
            if (!occupied.Contains(_origin)) { reached.Add(_origin); queue.Enqueue(_origin); }
            while (queue.Count > 0)
                foreach (var next in Neighbors(queue.Dequeue(), disabled)) if (!occupied.Contains(next) && reached.Add(next)) queue.Enqueue(next);
            foreach (var p in placements)
            {
                var door = RuleEvaluator.Door(Flat(p));
                if (!reached.Contains(new CellRef((string)p["surfaceId"], door.X, door.Z))) errors.Add("route:" + (string)p["guestId"]);
            }
            foreach (var e in _exits) if (!reached.Contains(e)) errors.Add("access:" + e.Surface);
            return errors;
        }
    }

    public static void Run()
    {
        var raw = Read<JObject>("region-draft.json"); var map = raw.ToObject<RoadmapDefinition>();
        var story = Read<EnvironmentalStoryData>("story.json"); var assets = Read<JObject>("assets.json");
        var locales = Read<JObject>("localization-draft.json"); var topology = Read<JObject>("crossing-topology-draft.json");
        Require(!(bool)raw["published"] && (string)raw["environmentalStoryFile"] == "story.json", "draft guard");
        Require(map.regions.Length == 1 && map.regions[0].nodePositions.Length == 9 && map.regions[0].chunks.Length == 3, "region structure");
        var nodes = map.regions[0].nodePositions; map.regions[0].environmentalStory = story;
        var models = ((JArray)assets["assets"]).ToDictionary(a => (string)a["id"]);
        var errors = RoadmapValidator.Validate(map, id => nodes.Any(n => n.levelId == id),
            key => new[] { "uk", "en", "de" }.All(l => !string.IsNullOrWhiteSpace((string)locales[l][key])), models.ContainsKey);
        Require(errors.Count == 0, string.Join("; ", errors)); Regional(story, map, assets);
        var briefs = (JArray)assets["puzzleBriefs"];
        Require(briefs.Count == 9 && briefs.All(b => (string)b["solverStatus"] == "not-authored" && (string)b["witnessStatus"] == "not-authored"), "false puzzle certification");
        Require(briefs.Select(b => (string)b["stage"]).SequenceEqual(Enumerable.Repeat("former-shore", 3).Concat(Enumerable.Repeat("flood-trace", 3)).Concat(Enumerable.Repeat("dam-area", 3))), "stage order");
        Console.WriteLine("PASS draft: nine IDs / three stage chunks / uk-en-de labels / seven dated sources / one hydro landmark; level-only rescue clues excluded from roadmap");
        int maxProps = 0, maxTriangles = 0;
        foreach (var node in nodes)
        {
            var excerpt = RoadmapStoryCompiler.ForLevel(story, node.levelId); Require(excerpt != null, "missing excerpt");
            Require(Copy(new LevelData { id = node.levelId, environmentalStory = excerpt }).environmentalStory.beats.Length == excerpt.beats.Length, "snapshot metadata lost");
            var props = excerpt.beats.SelectMany(b => b.props).Where(p => p.level != null).ToArray(); maxProps = Math.Max(maxProps, props.Length);
            foreach (bool cared in new[] { false, true }) maxTriangles = Math.Max(maxTriangles,
                props.Where(p => EnvironmentalStoryPolicy.Appears(p.appearance, cared)).Sum(p => (int)models[p.level.assetId]["maxTriangles"]));
        }
        int board = models.Values.Where(m => (string)m["view"] == "board-geometry").Sum(m => (int)m["maxTriangles"]);
        Require(maxProps <= 12 && maxTriangles <= (int)assets["budgets"]["maxNarrativeTriangles"] && board <= (int)assets["budgets"]["maxBoardGeometryTriangles"], "design triangle/prop budget");
        Console.WriteLine($"PASS excerpts/care/metadata snapshots: max {maxProps} props / {maxTriangles} planned narrative triangles; board geometry cap sum {board} (not measured meshes)");
        var catalog = new RoadmapCatalog(map); var window = new RoadmapWindow();
        foreach (int completed in new[] { 0, 1, 3, 6, 8, 9 })
        {
            var progress = new ProgressionService(); for (int i = 0; i < completed; i++) progress.MarkCompleted(nodes[i].levelId);
            var reveal = new RoadmapRevealState(catalog, progress); var restored = new ProgressionService(); restored.Restore(progress.CompletedIds, progress.LastLevelId, 0);
            Require(new RoadmapRevealState(catalog, restored).LastKnown == reveal.LastKnown, "restart reveal");
            if (completed > 0) { int rev = progress.Revision; progress.MarkCompleted(nodes[0].levelId); Require(progress.Revision == rev && !reveal.Refresh(), "replay reveal changed"); }
        }
        for (int i = 0; i < catalog.Chunks.Length; i++) { window.Move(i, catalog.Chunks.Length); Require(window.Count <= 3, "resident chunk cap"); }
        Console.WriteLine("PASS pure progression/restart/replay/window; early hydro teaser and geography rendering not implemented");

        int negative = 0;
        void Reject(Action action)
        {
            bool rejected = false; try { action(); }
            catch (InvalidOperationException e) when (e.Message.StartsWith("Water memory design: ", StringComparison.Ordinal)) { rejected = true; }
            Require(rejected, "unsafe mutation accepted"); negative++;
        }
        void BadRegion(Action<EnvironmentalStoryData, JObject> mutate)
        { var s = Copy(story); var a = (JObject)assets.DeepClone(); mutate(s, a); Reject(() => Regional(s, map, a)); }
        BadRegion((s,a) => s.beats.Single(b => b.storyBeat == "hydro-context").levelIds = nodes.Select(n => n.levelId).ToArray());
        BadRegion((s,a) => s.beats.Single(b => b.storyBeat == "stranded-boat").levelIds = nodes.Select(n => n.levelId).ToArray());
        BadRegion((s,a) => { var b = s.beats.Single(b => b.storyBeat == "animal-rescue"); b.roadmapVisibility = "context"; b.props[0].roadmapVisibility = "context"; b.props[0].roadmap = Copy(b.props[0].level); });
        BadRegion((s,a) => s.beats[0].referenceConfidence = "verified");
        BadRegion((s,a) => s.references[0].sources = s.references[0].sources.Skip(1).ToArray());
        BadRegion((s,a) => s.beats.Single(b => b.storyBeat == "hydro-context").props[0].appearance = "before-care");
        BadRegion((s,a) => a["geographyContract"]["depictsInstantForestRecovery"] = true);
        BadRegion((s,a) => a["careContract"]["repairsWholeDam"] = true);
        BadRegion((s,a) => a["careContract"]["changesPuzzleTopology"] = true);
        BadRegion((s,a) => a["careContract"]["riverWaterDrinkable"] = true);
        BadRegion((s,a) => a["careContract"]["showsDeadAnimals"] = true);
        BadRegion((s,a) => a["releaseGates"]["layeredDomain"] = true);

        var graph = new CrossingReference(topology); var witness = (JArray)topology["designWitness"];
        Require(graph.Check(witness).Count == 0, "crossing design witness");
        foreach (var id in new[] { "north-bridge", "south-bridge" }) Require(graph.Check(witness, new HashSet<string> { id }).Count == 0, "alternate bridge route failed");
        Require(graph.Check(witness, new HashSet<string> { "north-bridge", "south-bridge" }).Contains("route:east-guest"), "river crossed without bridge");
        Require(graph.Check(witness, new HashSet<string> { "ridge-ramp" }).Contains("route:ridge-guest"), "implicit height teleport");
        void BadPlacement(string error, Action<JArray> mutate)
        { var p = (JArray)witness.DeepClone(); mutate(p); Require(graph.Check(p).Contains(error), "missing placement diagnostic: " + error); negative++; }
        BadPlacement("overlap", p => { p[1]["surfaceId"] = "west"; p[1]["x"] = 0; p[1]["z"] = 0; });
        BadPlacement("bounds-or-solid", p => p[0]["x"] = 3);
        BadPlacement("reserved-landing", p => { p[0]["x"] = 2; p[0]["z"] = 2; });
        BadPlacement("shade", p => p[0]["shade"] = true);
        var badPortal = (JObject)topology.DeepClone(); badPortal["connectors"][0]["toCell"]["surfaceId"] = "water";
        Reject(() => new CrossingReference(badPortal));
        var changedCare = (JObject)topology.DeepClone(); changedCare["completionChangesTopology"] = true;
        Reject(() => new CrossingReference(changedCare));
        Console.WriteLine("PASS crossing graph reference: three dry surfaces / elevation / three access points / alternate temporary bridges; no bridge means no river route, no ramp means no ridge route");
        Console.WriteLine($"PASS {negative} negative/rejection cases: disclosure, sources, fictional evidence, geography/care/publication, overlap, edge, landing, shade and connector endpoints");

        var actual = new HashSet<string>();
        foreach (var name in new[] { "roadmap_models", "roadmap_story_models" })
            foreach (var m in JArray.Parse(File.ReadAllText("QuietCamp/Assets/QuietCamp/Resources/QuietCamp/" + name + ".json"))) actual.Add((string)m["id"]);
        var missing = EnvironmentalStoryValidator.Validate(story, assetExists: actual.Contains);
        Require(missing.Count > 0 && missing.All(e => e.StartsWith("story:missing-asset:", StringComparison.Ordinal)), "unexpected actual asset validation");
        Console.WriteLine($"EXPECTED RELEASE BLOCK: {models.Keys.Count(id => !actual.Contains(id))} planned models absent / {missing.Count} story missing-asset diagnostics; layered runtime, nine puzzles, render/human/device QA pending");
        Console.WriteLine("Design contracts passed. No campaign/resources/saves changed, no Unity launch or player build. No production solver or physics certification.");
    }
}
