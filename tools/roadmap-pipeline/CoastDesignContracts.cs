using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Application;
using QuietCamp.Domain;

/// <summary>Design-only regional disclosure and layered-graph reference. Does not implement
/// runtime v3, solver/generator, physics geometry, touch picking or production witnesses.</summary>
static class CoastDesignContracts
{
    const string Root = "Design/Roadmap/Regions/grain-coast/";
    static void Require(bool ok, string reason)
    { if (!ok) throw new InvalidOperationException("Coast design: " + reason); }
    static T Read<T>(string name) => JsonConvert.DeserializeObject<T>(File.ReadAllText(Root + name));
    static T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));

    static void Regional(EnvironmentalStoryData story, RoadmapDefinition map, JObject manifest)
    {
        var models = ((JArray)manifest["assets"]).ToDictionary(a => (string)a["id"]);
        var nodes = map.regions.SelectMany(r => r.nodePositions).ToArray();
        Require(EnvironmentalStoryValidator.Validate(story, id => nodes.Any(n => n.levelId == id), models.ContainsKey).Count == 0, "story references/views invalid");
        var baked = new List<RoadmapPropData>();
        foreach (var node in nodes) RoadmapStoryCompiler.Bake(story, node.levelId, baked);
        Require(baked.Count(p => p.assetId == "grain_coast_ship_map") == 1, "duplicate/missing merchant-ship landmark");
        Require(baked.All(p => (string)models[p.assetId]["view"] == "map"), "detail/proxy/ship geometry leaked into roadmap bake");
        Require(baked.All(p => models[p.assetId]["marking"] == null), "readable sign leaked into roadmap");
        var landmark = manifest["landmarkPresentation"];
        Require((int)landmark["earliestTeaserOrder"] == 2 && (int)landmark["localOwnerOrder"] == 6 && (int)landmark["maxSimultaneousHulls"] == 1,
            "landmark promise/ownership changed");
        Require((bool)landmark["usesProgression"] && !(bool)landmark["implemented"], "unsupported landmark implementation/scroll-only reveal");
        var playable = ((JArray)landmark["playableLevelIds"]).Values<string>().ToArray();
        Require(playable.SequenceEqual(new[] { "QC_SEA006", "QC_SEA007" }) && playable.All(id => nodes.Any(n => n.levelId == id)), "unreachable ship promise");
        var hazard = manifest["hazardContract"];
        foreach (string key in new[] { "activeMinePropsAllowed", "hazardInteractionAllowed", "rewardForCrossing", "waterReopenedOnCompletion", "barrierRemovedOnCompletion", "implemented" })
            Require(!(bool)hazard[key], "hazard/care contract violated: " + key);
        Require((bool)hazard["allAccessPointsLandward"] && (string)hazard["signText"] == "Обережно! Міни" && (string)hazard["signLocale"] == "uk", "warning/access contract invalid");
        var sign = models["grain_coast_warning_detail"];
        Require((string)sign["marking"]["text"] == (string)hazard["signText"] && (string)sign["marking"]["claimId"] == "grain-coast:restricted-zone", "sign lacks supported wording/claim");
        foreach (var prop in story.beats.SelectMany(b => b.props).Where(p => p.id == "grain-coast:warning" || p.id == "grain-coast:hazard-barrier"))
            Require(prop.appearance == "always" && prop.roadmap == null, "care removed hazard warning or disclosed its text");
        Require(!(bool)manifest["published"] && (string)manifest["publicationStatus"] == "design-only", "draft falsely published");
        Require(((JObject)manifest["releaseGates"]).Properties().All(g => !(bool)g.Value), "unperformed release gate claimed as passed");
        Require(models.Values.All(a => (string)a["status"] == "planned" && !(bool)a["assetReviewPassed"]), "planned model masquerades as approved artwork");
    }

    // Prototype addresses are independent of production Cell, which remains X/Z for v1/v2.
    readonly record struct Address(string Surface, int X, int Z);
    static Address AddressOf(JToken token) => new((string)token["surfaceId"], (int)token["x"], (int)token["z"]);
    static Address[] Coordinates(string surface, JArray values)
        => values.Select(c => new Address(surface, (int)c[0], (int)c[1])).ToArray();

    sealed class DeckReference
    {
        readonly JObject _data;
        readonly Dictionary<string, JToken> _surfaces;
        readonly HashSet<Address> _walk = new(), _reserved = new();
        readonly List<(string Id, Address From, Address To)> _connectors = new();
        readonly Address _origin;
        readonly Address[] _access;

        public DeckReference(JObject data)
        {
            _data = data;
            Require(!(bool)data["runtimeImplemented"] && !(bool)data["productionWitnessCertified"] && (int)data["proposedRuleVersion"] == 3,
                "design reference claims production implementation");
            _surfaces = ((JArray)data["surfaces"]).ToDictionary(s => (string)s["id"]);
            Require(_surfaces.Count == 2 && _surfaces["upper"]["elevation"].Value<float>() > _surfaces["lower"]["elevation"].Value<float>(), "invalid elevation model");
            foreach (var pair in _surfaces)
            {
                var cells = Coordinates(pair.Key, (JArray)pair.Value["cells"]);
                Require(cells.Length == cells.Distinct().Count(), "duplicate surface cell");
                Require(cells.All(c => c.X >= 0 && c.Z >= 0 && c.X < (int)pair.Value["width"] && c.Z < (int)pair.Value["height"]), "surface bounds");
                var blocked = Coordinates(pair.Key, (JArray)pair.Value["blocked"]);
                Require(blocked.All(cells.Contains), "blocked cell outside surface");
                Require(Coordinates(pair.Key, (JArray)pair.Value["shade"]).All(cells.Contains), "shade outside surface");
                foreach (var cell in cells.Except(blocked)) _walk.Add(cell);
            }
            var connectorIds = new HashSet<string>();
            foreach (var c in (JArray)data["connectors"])
            {
                var a = AddressOf(c["fromCell"]); var b = AddressOf(c["toCell"]);
                Require(connectorIds.Add((string)c["id"]) && a.Surface != b.Surface && _walk.Contains(a) && _walk.Contains(b)
                    && (int)c["cost"] >= 1 && (bool)c["twoWay"], "invalid explicit connector");
                _connectors.Add(((string)c["id"], a, b)); _reserved.Add(a); _reserved.Add(b);
            }
            _origin = AddressOf(data["origin"]);
            _access = ((JArray)data["accessPoints"]).Select(p => AddressOf(p["cell"])).ToArray();
            Require(_walk.Contains(_origin) && _access.Length >= 2 && _access.All(_walk.Contains), "invalid exits/origin");
            foreach (var point in _access) _reserved.Add(point);
        }

        static Placement Flat(JToken p) => new() { guestId = (string)p["guestId"], x = (int)p["x"], z = (int)p["z"], rotation = (int)p["rotation"] };
        Address[] Footprint(JToken p) => RuleEvaluator.Footprint(Flat(p)).Select(c => new Address((string)p["surfaceId"], c.X, c.Z)).ToArray();
        Address Door(JToken p) { var c = RuleEvaluator.Door(Flat(p)); return new((string)p["surfaceId"], c.X, c.Z); }

        IEnumerable<Address> Neighbors(Address at, ISet<string> disabled)
        {
            foreach (var delta in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var next = new Address(at.Surface, at.X + delta.Item1, at.Z + delta.Item2);
                if (_walk.Contains(next)) yield return next;
            }
            foreach (var c in _connectors)
            {
                if (disabled.Contains(c.Id)) continue;
                if (c.From == at) yield return c.To;
                if (c.To == at) yield return c.From;
            }
        }

        public bool HasImplicitLayerEdge()
            => _walk.Any(a => Neighbors(a, _connectors.Select(c => c.Id).ToHashSet()).Any(b => a.Surface != b.Surface));

        bool Path(Address to, HashSet<Address> occupied, ISet<string> disabled)
        {
            if (!_walk.Contains(to) || occupied.Contains(to) || occupied.Contains(_origin)) return false;
            var visited = new HashSet<Address> { _origin }; var queue = new Queue<Address>(); queue.Enqueue(_origin);
            while (queue.Count > 0)
            {
                var at = queue.Dequeue(); if (at == to) return true;
                foreach (var next in Neighbors(at, disabled)) if (!occupied.Contains(next) && visited.Add(next)) queue.Enqueue(next);
            }
            return false;
        }

        public List<string> Check(JArray placements, ISet<string> disabled = null)
        {
            disabled ??= new HashSet<string>(); var errors = new List<string>(); var occupied = new HashSet<Address>();
            foreach (var p in placements)
            {
                string surfaceId = (string)p["surfaceId"];
                if (!_surfaces.TryGetValue(surfaceId, out var surface)) { errors.Add("surface"); continue; }
                var cells = Footprint(p);
                if (cells.Any(c => !_walk.Contains(c))) errors.Add("bounds-or-solid");
                if (cells.Any(_reserved.Contains)) errors.Add("reserved-landing");
                if (cells.Any(occupied.Contains)) errors.Add("overlap");
                occupied.UnionWith(cells);
                if (surface["headroomUnderUpper"].Type != JTokenType.Null)
                {
                    var scope = Coordinates(surfaceId, (JArray)surface["headroomScope"]);
                    if (cells.Any(scope.Contains) && (float)_data["tentHeight"] + (float)_data["windClearance"] > (float)surface["headroomUnderUpper"])
                        errors.Add("headroom");
                }
                if ((bool)p["shade"] && cells.Any(c => !Coordinates(surfaceId, (JArray)surface["shade"]).Contains(c))) errors.Add("shade");
            }
            foreach (var p in placements) if (!Path(Door(p), occupied, disabled)) errors.Add("route:" + (string)p["guestId"]);
            foreach (var point in _access) if (!Path(point, occupied, disabled)) errors.Add("access");
            return errors;
        }

        public LevelData CollapsedLevel(JArray placements) => new()
        {
            id = "design-only-collapsed-ship", ruleVersion = 2, width = 6, height = 6, entry = new[] { _origin.X, _origin.Z },
            blocked = ((JArray)_surfaces["lower"]["blocked"]).ToObject<int[][]>(), shade = ((JArray)_surfaces["lower"]["shade"]).ToObject<int[][]>(),
            noise = Array.Empty<int[]>(), friends = Array.Empty<string[]>(),
            accessPoints = _access.Select((a,i) => new AccessPointData { id = "exit:" + i, kind = "exit", x = a.X, z = a.Z }).ToArray(),
            guests = placements.Select(p => new GuestData { id = (string)p["guestId"], shade = (bool)p["shade"] }).ToArray()
        };
        public Placement[] CollapsedPlacements(JArray placements) => placements.Select(Flat).ToArray();
    }

    public static void Run()
    {
        var rawMap = Read<JObject>("region-draft.json"); var map = rawMap.ToObject<RoadmapDefinition>();
        var story = Read<EnvironmentalStoryData>("story.json"); var manifest = Read<JObject>("assets.json");
        var locales = Read<JObject>("localization-draft.json"); var deckData = Read<JObject>("ship-topology-draft.json");
        Require(!(bool)rawMap["published"] && (string)rawMap["environmentalStoryFile"] == "story.json", "draft source/publication guard");
        Require(map.regions.Length == 1 && map.regions[0].nodePositions.Length == 8 && map.regions[0].chunks.Length == 4, "region structure");
        map.regions[0].environmentalStory = story; var nodes = map.regions[0].nodePositions;
        var declaredModels = ((JArray)manifest["assets"]).ToDictionary(a => (string)a["id"]);
        var errors = RoadmapValidator.Validate(map, id => nodes.Any(n => n.levelId == id),
            key => new[] { "uk", "en", "de" }.All(l => !string.IsNullOrWhiteSpace((string)locales[l][key])), declaredModels.ContainsKey);
        Require(errors.Count == 0, string.Join("; ", errors)); Regional(story, map, manifest);
        Console.WriteLine("PASS draft: eight level IDs / four chunks / uk-en-de labels / source-bound warning / one baked ship landmark; no level detail on roadmap");
        int maxProps = 0, maxTriangles = 0;
        foreach (var node in nodes)
        {
            var excerpt = RoadmapStoryCompiler.ForLevel(story, node.levelId); Require(excerpt != null, "missing excerpt");
            var snapshot = Copy(new LevelData { id = node.levelId, environmentalStory = excerpt });
            Require(snapshot.environmentalStory.beats.Length == excerpt.beats.Length, "snapshot lost metadata");
            var props = excerpt.beats.SelectMany(b => b.props).Where(p => p.level != null).ToArray(); maxProps = Math.Max(maxProps, props.Length);
            foreach (bool cared in new[] { false, true })
                maxTriangles = Math.Max(maxTriangles, props.Where(p => EnvironmentalStoryPolicy.Appears(p.appearance, cared)).Sum(p => (int)declaredModels[p.level.assetId]["maxTriangles"]));
        }
        Require(maxProps <= 12 && maxTriangles <= (int)manifest["budgets"]["maxNarrativeTriangles"], "narrative design budget");
        int shipTriangles = declaredModels.Values.Where(a => (string)a["view"] == "ship-geometry").Sum(a => (int)a["maxTriangles"]);
        Require(shipTriangles <= (int)manifest["budgets"]["maxShipGeometryTriangles"], "ship design budget");
        Console.WriteLine($"PASS excerpt/snapshot/care declarations: max {maxProps} narrative props / {maxTriangles} planned triangles; ship geometry budget sum {shipTriangles} (not measured meshes)");
        var catalog = new RoadmapCatalog(map); var window = new RoadmapWindow();
        for (int i = 0; i < catalog.Chunks.Length; i++) { window.Move(i, catalog.Chunks.Length); Require(window.Count <= 3, "resident chunks"); }
        foreach (int completed in new[] { 0, 1, 4, 5, 6, 8 })
        {
            var progress = new ProgressionService(); for (int i = 0; i < completed; i++) progress.MarkCompleted(nodes[i].levelId);
            var reveal = new RoadmapRevealState(catalog, progress); var restored = new ProgressionService(); restored.Restore(progress.CompletedIds, progress.LastLevelId, 0);
            Require(new RoadmapRevealState(catalog, restored).LastKnown == reveal.LastKnown, "restart reveal");
            if (completed > 0) { int rev = progress.Revision; progress.MarkCompleted(nodes[0].levelId); Require(progress.Revision == rev && !reveal.Refresh(), "replay reveal"); }
        }
        Console.WriteLine("PASS pure progression/restart/replay/window planner; early ship teaser renderer still unimplemented");

        int negatives = 0;
        void Reject(Action action)
        {
            bool rejected = false; try { action(); }
            catch (InvalidOperationException e) when (e.Message.StartsWith("Coast design: ", StringComparison.Ordinal)) { rejected = true; }
            Require(rejected, "unsafe mutation accepted"); negatives++;
        }
        void BadRegion(Action<EnvironmentalStoryData, JObject> mutate)
        { var s = Copy(story); var m = (JObject)manifest.DeepClone(); mutate(s,m); Reject(() => Regional(s,map,m)); }
        BadRegion((s,m) => s.beats.Single(b => b.storyBeat == "ship-map").levelIds = nodes.Select(n => n.levelId).ToArray());
        BadRegion((s,m) => { var b = s.beats.Single(b => b.storyBeat == "warning"); b.roadmapVisibility = "context"; b.props[0].roadmapVisibility = "context"; b.props[0].roadmap = Copy(b.props[0].level); });
        BadRegion((s,m) => s.beats.Single(b => b.storyBeat == "warning").props[0].appearance = "before-care");
        BadRegion((s,m) => s.beats.Single(b => b.storyBeat == "warning").props[0].claimId = "unknown");
        BadRegion((s,m) => m["hazardContract"]["activeMinePropsAllowed"] = true);
        BadRegion((s,m) => m["hazardContract"]["waterReopenedOnCompletion"] = true);
        BadRegion((s,m) => m["landmarkPresentation"]["playableLevelIds"] = new JArray("missing"));
        BadRegion((s,m) => m["releaseGates"]["layeredDomain"] = true);

        var deck = new DeckReference(deckData); var witness = (JArray)deckData["designWitness"];
        Require(deck.Check(witness).Count == 0, "design graph witness failed"); Require(!deck.HasImplicitLayerEdge(), "implicit height teleport");
        Require(deck.Check(witness, new HashSet<string> { "port-stairs" }).Count == 0, "alternate connector failed");
        Require(deck.Check(witness, new HashSet<string> { "starboard-stairs" }).Count == 0, "first connector failed");
        Require(deck.Check(witness, new HashSet<string> { "port-stairs", "starboard-stairs" }).Contains("route:upper-guest"), "height does not affect route");
        void BadPlacement(string code, Action<JArray> mutate)
        { var p = (JArray)witness.DeepClone(); mutate(p); Require(deck.Check(p).Any(e => e.StartsWith(code)), "missing design diagnostic " + code); negatives++; }
        BadPlacement("overlap", p => p[2]["surfaceId"] = "lower");
        BadPlacement("bounds-or-solid", p => { p[2]["x"] = 5; p[2]["z"] = 5; });
        BadPlacement("reserved-landing", p => { p[2]["x"] = 3; p[2]["z"] = 3; });
        BadPlacement("shade", p => p[2]["shade"] = true);
        var lowRoof = (JObject)deckData.DeepClone(); lowRoof["surfaces"][0]["headroomUnderUpper"] = 1.2;
        Require(new DeckReference(lowRoof).Check(witness).Contains("headroom"), "low roof ignored"); negatives++;
        var badPortal = (JObject)deckData.DeepClone(); badPortal["connectors"][0]["toCell"]["surfaceId"] = "missing";
        Reject(() => new DeckReference(badPortal));
        var collapsed = RuleEvaluator.Evaluate(deck.CollapsedLevel(witness), deck.CollapsedPlacements(witness));
        Require(!collapsed.CanCommit && collapsed.Issues.Any(i => i.Code == "overlap"), "legacy flat evaluator unexpectedly supports layered witness");
        Console.WriteLine("PASS design graph reference: stacked distinct-surface tents, layer-specific shade, headroom, exits, alternate stairs; removing both stairs disconnects upper guest");
        Console.WriteLine("CONFIRMED CURRENT LIMIT: real flat RuleEvaluator rejects the collapsed layered witness as overlap; production v3 is required");
        Console.WriteLine($"PASS {negatives} rejection/negative cases: disclosure, hazard/care, publication, layer collision, edge/landing, shade, clearance and unknown connector surface");

        var actual = new HashSet<string>();
        foreach (string file in new[] { "roadmap_models", "roadmap_story_models" })
            foreach (var model in JArray.Parse(File.ReadAllText("QuietCamp/Assets/QuietCamp/Resources/QuietCamp/" + file + ".json"))) actual.Add((string)model["id"]);
        var missing = EnvironmentalStoryValidator.Validate(story, assetExists: actual.Contains);
        Require(missing.Count > 0 && missing.All(e => e.StartsWith("story:missing-asset:")), "unexpected actual asset diagnostics");
        Require(((JArray)manifest["puzzleBriefs"]).Count == 8 && ((JArray)manifest["puzzleBriefs"]).All(p => (string)p["solverStatus"] == "not-authored" && (string)p["witnessStatus"] == "not-authored"), "design falsely claims authored puzzles");
        Console.WriteLine($"EXPECTED RELEASE BLOCK: {declaredModels.Keys.Count(a => !actual.Contains(a))} planned models absent / {missing.Count} actual story missing-asset diagnostics; v3, puzzles, landmark renderer and device QA pending");
        Console.WriteLine("Design audit passed. No campaign/Unity assets/player saves modified, no Unity launch or player build.");
    }
}
