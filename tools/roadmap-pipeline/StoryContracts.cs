using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Application;
using QuietCamp.Domain;

static class StoryContracts
{
    public static void Run(HashSet<string> models, LevelSummary[] summaries)
    {
        const string root = "QuietCamp/Assets/QuietCamp/Tests/Fixtures/Roadmap/";
        T Read<T>(string path) => JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        T Copy<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        void Require(bool ok, string message) { if (!ok) throw new Exception("Story contract: " + message); }
        var story = Read<EnvironmentalStoryData>(root + "environmental-story.json");
        var errors = EnvironmentalStoryValidator.Validate(story, id => summaries.Any(s => s.id == id), models.Contains);
        Require(errors.Count == 0, string.Join("; ", errors));
        var map = Read<RoadmapDefinition>(root + "environmental-story-map.json");
        RoadmapCompiler.BakeAuthored(map, summaries.Take(7).ToArray());
        Require(RoadmapValidator.Validate(map, modelExists: models.Contains).Count == 0, "Invalid bound region");
        var node = map.regions[0].nodePositions[0];
        Require(node.world.props.Any(p => p.storyId == "wayside:shelter" && p.storyVisibility == "silhouette"), "Coarse silhouette lost");
        Require(!node.world.props.Any(p => p.storyId == "wayside:board"), "Level-only clue leaked to map");
        Require(!map.regions[0].nodePositions[3].world.props.Any(p => p.storyId != null), "Beat applied to unrelated level");
        var excerpt = RoadmapStoryCompiler.ForLevel(story, "QC002");
        Require(excerpt.beats.Length == 1 && excerpt.beats[0].id == "crossing", "Wrong level excerpt");
        Require(RoadmapStoryCompiler.ForLevel(story, "unknown") == null, "Unknown level gained story");
        Require(EnvironmentalStoryPolicy.Appears("after-care", true) && !EnvironmentalStoryPolicy.Appears("after-care", false), "Care appearance contract broken");
        var snapshot = Copy(new LevelData { id = "QC002", environmentalStory = excerpt });
        Require(snapshot.environmentalStory.beats[0].worldStateAfter == excerpt.beats[0].worldStateAfter, "Snapshot lost narrative");
        Require(JObject.FromObject(new LevelData())["environmentalStory"] == null, "Old snapshot serialization changed");
        Require(JObject.FromObject(new LevelSummary { environmentalStory=story })["environmentalStory"] == null,"Detailed story inflated lightweight runtime summaries");
        int negatives = 0;
        void Bad(string code, Action<EnvironmentalStoryData> mutate)
        {
            var copy = Copy(story); mutate(copy);
            Require(EnvironmentalStoryValidator.Validate(copy, id => summaries.Any(s => s.id == id), models.Contains)
                .Any(e => e.StartsWith("story:" + code + ":")), "Missing diagnostic " + code); negatives++;
        }
        Bad("duplicate-id", d => d.beats[1].props[0].id = d.beats[0].props[0].id);
        Bad("level-binding", d => d.beats[0].levelIds = new[] { "unknown" });
        Bad("visibility-escalation", d => { d.beats[0].roadmapVisibility = "silhouette"; d.beats[0].props[0].roadmapVisibility = "context"; });
        Bad("hidden-view", d => d.beats[0].props[0].roadmapVisibility = "hidden");
        Bad("missing-asset", d => d.beats[0].props[0].level.assetId = "unknown");
        Bad("view-bounds", d => d.beats[0].props[0].level.height = float.NaN);
        Bad("content-tag", d => d.beats[0].props[0].contentTags = new[] { "gore" });
        Bad("content-tag", d => d.beats[0].props[0].contentTags = new[] { "political-slogan" });
        Bad("rigid-prop-wind", d => d.beats[0].props[0].level.sway = true);
        Bad("motif", d => d.beats[0].props[0].motif = "collectible-active-mine");
        Bad("unsupported-evidence", d => d.beats[0].props[0].contentTags = new[] { "technical-marking" });
        Bad("unverified-reference", d => d.beats[0].referenceConfidence = "unverified");
        Bad("missing-inspired-reference", d => d.beats[0].referenceConfidence = "fiction-inspired");
        Bad("landmark", d => d.beats[0].heroLandmark = "unknown");
        Bad("returning-prop", d => d.beats[0].returningProp = "unknown");
        // Synthetic bibliography: these URLs/claims are fixtures, never historical sources for shipped content.
        var documented = Copy(story); var first = documented.beats[0];
        first.historicalReference = "synthetic-reference"; first.referenceConfidence = "verified";
        first.props[1].claimId = "synthetic-claim"; first.props[1].contentTags = new[] { "technical-marking" };
        documented.references = new[] { new EnvironmentalStoryReference { id = "synthetic-reference", basis = "documented",
            reviewer = "automated fixture only", reviewedOn = "2026-10-07", sources = new[] { new EnvironmentalStorySource {
                id = "synthetic-source", title = "Synthetic source", publisher = "Test fixture", url = "https://example.org/test", accessedOn = "2026-10-07" } },
            claims = new[] { new EnvironmentalStoryClaim { id = "synthetic-claim", statement = "Synthetic test claim, not a real event.", sourceIds = new[] { "synthetic-source" } } } } };
        Require(EnvironmentalStoryValidator.Validate(documented).Count == 0, "Valid documented structure rejected");
        documented.references[0].claims[0].sourceIds = new[] { "missing" };
        Require(EnvironmentalStoryValidator.Validate(documented).Any(e => e.StartsWith("story:claim-source:")), "Orphan source accepted"); negatives++;
        documented.references[0].reviewer = null;
        Require(EnvironmentalStoryValidator.Validate(documented).Any(e => e.StartsWith("story:reference-review:")), "Missing review accepted"); negatives++;
        Console.WriteLine("PASS environmental story: region/level binding, silhouette/detail separation, care states, snapshot compatibility, " + negatives + " negative authoring cases");
    }
}
