using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Domain;
using QuietCamp.Application;

static class StoryAuthoring
{
    public static void Fixture()
    {
        const string root = "QuietCamp/Assets/QuietCamp/Tests/Fixtures/Roadmap/";
        var map = JsonConvert.DeserializeObject<RoadmapDefinition>(File.ReadAllText(root + "environmental-story-map.json"));
        var summaries = JsonConvert.DeserializeObject<LevelSummary[]>(File.ReadAllText(root + "foundation-slice-summaries.json"));
        foreach (var summary in summaries)
            foreach (var region in map.regions)
                if (Array.Exists(region.nodePositions, n => n.levelId == summary.id))
                    summary.environmentalStory = RoadmapStoryCompiler.ForLevel(region.environmentalStory, summary.id);
        RoadmapCompiler.BakeAuthored(map, summaries);
        File.WriteAllText(root + "environmental-story-baked.json", JsonConvert.SerializeObject(map, Formatting.Indented) + "\n");
        File.WriteAllText(root + "environmental-story-summaries.json", JsonConvert.SerializeObject(summaries, Formatting.Indented) + "\n");
        Console.WriteLine("PASS environmental story fixture baked; campaign and level files unchanged");
    }

    public static void Brief(string input, string output)
    {
        if (Path.GetExtension(output) != ".md") throw new ArgumentException("Story brief output must be a Markdown file");
        var story = JsonConvert.DeserializeObject<EnvironmentalStoryData>(File.ReadAllText(input));
        var models = new HashSet<string>();
        foreach (var file in new[] { "roadmap_models", "roadmap_story_models" })
            foreach (var model in JArray.Parse(File.ReadAllText("QuietCamp/Assets/QuietCamp/Resources/QuietCamp/" + file + ".json"))) models.Add((string)model["id"]);
        var errors = EnvironmentalStoryValidator.Validate(story, assetExists: models.Contains);
        if (errors.Count > 0) throw new ArgumentException(string.Join("; ", errors));
        string Text(string value) => (value ?? "—").Replace("|", "\\|").Replace("\r", "").Replace("\n", " ");
        var text = new StringBuilder("# Environmental story authoring brief\n\n");
        text.Append("Story: ").Append(Text(story.id)).Append(". Generated from validated data; not a historical fact-check or editorial approval.\n\n");
        foreach (var beat in story.beats)
        {
            text.Append("## ").Append(Text(beat.id)).Append("\n\n")
                .Append("Levels: ").Append(string.Join(", ", beat.levelIds)).Append("\n\n")
                .Append("Beat: ").Append(Text(beat.storyBeat)).Append("\n\n")
                .Append("Before: ").Append(Text(beat.worldStateBefore)).Append("\n\n")
                .Append("After: ").Append(Text(beat.worldStateAfter)).Append("\n\n")
                .Append("Possible inference: ").Append(Text(beat.intendedInference)).Append("\n\n")
                .Append("Alternative: ").Append(Text(beat.alternativeInference)).Append("\n\n")
                .Append("Culture: ").Append(Text(beat.culturalContext)).Append("\n\n")
                .Append("Focus: ").Append(Text(beat.characterFocus)).Append("; landmark prop: ").Append(Text(beat.heroLandmark))
                .Append("; returning prop: ").Append(Text(beat.returningProp)).Append(".\n\n")
                .Append("Reference: ").Append(Text(beat.historicalReference)).Append(" / ").Append(Text(beat.referenceConfidence)).Append(".\n\n")
                .Append("| Prop / category / motif | Roadmap | Level | Appearance / claim |\n|---|---|---|---|\n");
            foreach (var prop in beat.props)
            {
                string View(bool map)
                {
                    var view = map ? prop.roadmap : prop.level;
                    return Text(EnvironmentalStoryPolicy.Visibility(beat, prop, map)) + (view == null ? "" : " / " + Text(view.assetId) + $" / ({view.x}, {view.elevation}, {view.z}) / h={view.height}");
                }
                text.Append("| ").Append(Text(prop.id + " / " + prop.category + " / " + prop.motif))
                    .Append(" | ").Append(View(true)).Append(" | ").Append(View(false)).Append(" | ")
                    .Append(Text(prop.appearance)).Append(" / ").Append(Text(prop.claimId)).Append(" |\n");
            }
            text.Append('\n');
        }
        foreach (var reference in story.references)
        {
            text.Append("## Reference: ").Append(Text(reference.id)).Append("\n\n")
                .Append("Basis: ").Append(Text(reference.basis)).Append("; review: ").Append(Text(reference.reviewer))
                .Append(" / ").Append(Text(reference.reviewedOn)).Append(".\n\n");
            foreach (var source in reference.sources)
                text.Append("- ").Append(Text(source.id + ": " + source.publisher + " — " + source.title + " — " + source.url + " / " + source.accessedOn)).Append('\n');
            text.Append('\n');
            foreach (var claim in reference.claims)
                text.Append("- ").Append(Text(claim.id + ": " + claim.statement + " [" + string.Join(", ", claim.sourceIds) + "]")).Append('\n');
            text.Append('\n');
        }
        var folder = Path.GetDirectoryName(output); if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(output, text.ToString()); Console.WriteLine("PASS story brief: " + output);
    }
}
