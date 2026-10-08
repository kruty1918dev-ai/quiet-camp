using System;

namespace QuietCamp.Domain
{
    /// <summary>Author-owned narrative. No UI copy, runtime rewards, or puzzle rules.</summary>
    [Serializable] public sealed class EnvironmentalStoryData
    {
        public int schemaVersion = 1;
        public string id;
        public EnvironmentalStoryBeat[] beats = Array.Empty<EnvironmentalStoryBeat>();
        public EnvironmentalStoryReference[] references = Array.Empty<EnvironmentalStoryReference>();
    }

    [Serializable] public sealed class EnvironmentalStoryBeat
    {
        public string id, storyBeat, heroLandmark, returningProp, characterFocus;
        public string worldStateBefore, worldStateAfter, historicalReference;
        public string referenceConfidence = "fictional";
        public string roadmapVisibility = "context", levelDetailVisibility = "detail";
        // Design notes, deliberately never shown as an explanatory popup.
        public string intendedInference, alternativeInference, culturalContext;
        public string[] levelIds = Array.Empty<string>();
        public EnvironmentalStoryProp[] props = Array.Empty<EnvironmentalStoryProp>();
    }

    [Serializable] public sealed class EnvironmentalStoryProp
    {
        public string id, category, motif, returningProp, claimId;
        public string roadmapVisibility = "inherit", levelDetailVisibility = "inherit";
        public string appearance = "always"; // always, before-care, after-care
        public string[] contentTags = Array.Empty<string>();
        // Separate meshes: a cabin silhouette must never expose its interior evidence.
        public EnvironmentalStoryView roadmap, level;
    }

    [Serializable] public sealed class EnvironmentalStoryView
    {
        public string assetId;
        public float x, z, elevation, height = 1, yaw;
        public bool sway;
    }

    [Serializable] public sealed class EnvironmentalStoryReference
    {
        public string id, basis = "fiction-inspired", reviewer, reviewedOn;
        public EnvironmentalStorySource[] sources = Array.Empty<EnvironmentalStorySource>();
        public EnvironmentalStoryClaim[] claims = Array.Empty<EnvironmentalStoryClaim>();
    }

    [Serializable] public sealed class EnvironmentalStorySource
    {
        public string id, title, publisher, url, accessedOn;
        public string authority = "primary"; // primary or authoritative; requires human review
    }

    [Serializable] public sealed class EnvironmentalStoryClaim
    {
        public string id, statement;
        public string[] sourceIds = Array.Empty<string>();
    }

    /// <summary>Same disclosure/appearance contract for compiler, map and gameplay.</summary>
    public static class EnvironmentalStoryPolicy
    {
        public static int Rank(string value, bool roadmap)
        {
            if (value == "hidden") return 0;
            if (value == (roadmap ? "silhouette" : "context")) return 1;
            if (value == (roadmap ? "context" : "detail")) return 2;
            return -1;
        }

        public static string Visibility(EnvironmentalStoryBeat beat, EnvironmentalStoryProp prop, bool roadmap)
        {
            var parent = roadmap ? beat.roadmapVisibility : beat.levelDetailVisibility;
            var child = roadmap ? prop.roadmapVisibility : prop.levelDetailVisibility;
            return child == "inherit" ? parent : Rank(child, roadmap) <= Rank(parent, roadmap) ? child : parent;
        }

        public static bool Applies(EnvironmentalStoryBeat beat, string levelId)
            => Array.IndexOf(beat.levelIds ?? Array.Empty<string>(), levelId) >= 0;

        public static bool Appears(string appearance, bool cared)
            => appearance == "always" || appearance == (cared ? "after-care" : "before-care");
    }
}
