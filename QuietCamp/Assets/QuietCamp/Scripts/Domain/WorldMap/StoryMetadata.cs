using System;

namespace QuietCamp.Domain
{
    /// <summary>What the roadmap may show of a story element: the map carries
    /// silhouette and context only — evidence and detail live in the level.</summary>
    public enum StoryVisibility { Hidden, Silhouette, Context, Detail }
    /// <summary>How grounded a historical echo is: <c>inspired</c> fiction or
    /// <c>verified</c> against authoritative sources. Anything unverified must
    /// never present itself as evidence.</summary>
    public enum ReferenceConfidence { None, Inspired, Verified }
    /// <summary>How plainly a level may show its evidence — the in-level
    /// counterpart to <see cref="StoryVisibility"/>.</summary>
    public enum DetailVisibility { None, Subtle, Evident }

    /// <summary>Environmental storytelling record — authored per region, node
    /// or branch in world_story.json. The player is never told the story; the
    /// terrain, props and returning details let them assemble it.</summary>
    [Serializable] public sealed class StoryMetadata
    {
        public string id;
        /// <summary>The one dramatic beat this place carries.</summary>
        public string storyBeat;
        /// <summary>The dominant landmark visible from the road.</summary>
        public string heroLandmark;
        /// <summary>A prop motif that recurs across the arc.</summary>
        public string returningProp;
        /// <summary>Whose presence/absence the scene is about.</summary>
        public string characterFocus;
        /// <summary>How the place looked before and after the player's pass.</summary>
        public string worldStateBefore, worldStateAfter;
        /// <summary>Optional real-world echo — factual id, never a slogan.</summary>
        public string historicalReference;
        /// <summary>"none" | "inspired" | "verified".</summary>
        public string referenceConfidence = "none";
        /// <summary>"hidden" | "silhouette" | "context" | "detail" — map view.</summary>
        public string roadmapVisibility = "context";
        /// <summary>"none" | "subtle" | "evident" | "detail" — in-level view.</summary>
        public string levelDetailVisibility = "subtle";

        public StoryVisibility Roadmap => ParseVisibility(roadmapVisibility, StoryVisibility.Context);
        public DetailVisibility LevelDetail
        {
            get
            {
                switch (levelDetailVisibility)
                {
                    case "evident": return DetailVisibility.Evident;
                    case "subtle": return DetailVisibility.Subtle;
                    default: return DetailVisibility.None;
                }
            }
        }

        public ReferenceConfidence Confidence
            => referenceConfidence == "verified" ? ReferenceConfidence.Verified
                : referenceConfidence == "inspired" ? ReferenceConfidence.Inspired
                : ReferenceConfidence.None;

        static StoryVisibility ParseVisibility(string value, StoryVisibility fallback)
        {
            switch (value)
            {
                case "hidden": return StoryVisibility.Hidden;
                case "silhouette": return StoryVisibility.Silhouette;
                case "context": return StoryVisibility.Context;
                case "detail": return StoryVisibility.Detail;
                default: return fallback;
            }
        }
    }
}
