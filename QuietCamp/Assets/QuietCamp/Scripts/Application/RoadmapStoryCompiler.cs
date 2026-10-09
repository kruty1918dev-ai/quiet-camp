using System;
using System.Collections.Generic;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    /// <summary>Offline extraction. Runtime map consumes only approved coarse mesh identities.</summary>
    public static class RoadmapStoryCompiler
    {
        public static EnvironmentalStoryData ForLevel(EnvironmentalStoryData region, string levelId)
        {
            if (region == null) return null;
            var beats = new List<EnvironmentalStoryBeat>();
            var references = new List<EnvironmentalStoryReference>();
            foreach (var beat in region.beats)
                if (EnvironmentalStoryPolicy.Applies(beat, levelId)) beats.Add(beat);
            if (beats.Count == 0) return null;
            foreach (var reference in region.references)
                if (beats.Exists(b => b.historicalReference == reference.id)) references.Add(reference);
            // Authoring objects are treated as immutable; snapshots serialize detached copies.
            return new EnvironmentalStoryData { id = region.id, beats = beats.ToArray(), references = references.ToArray() };
        }

        public static void Bake(EnvironmentalStoryData story, string levelId, List<RoadmapPropData> output)
        {
            var errors = EnvironmentalStoryValidator.Validate(story);
            if (errors.Count > 0) throw new ArgumentException(string.Join("; ", errors));
            if (story == null) return;
            foreach (var beat in story.beats)
            {
                if (!EnvironmentalStoryPolicy.Applies(beat, levelId)) continue;
                foreach (var prop in beat.props)
                {
                    string visibility = EnvironmentalStoryPolicy.Visibility(beat, prop, true);
                    if (visibility == "hidden") continue;
                    var view = prop.roadmap;
                    output.Add(new RoadmapPropData { assetId = view.assetId, x = view.x, z = view.z,
                        height = view.height, yaw = view.yaw, sway = view.sway,
                        storyId = prop.id, storyVisibility = visibility, storyAppearance = prop.appearance });
                }
            }
        }
    }
}
