using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Environmental storytelling data — authored in
    /// <c>world_story.json</c>, keyed by region, level or branch id. The map
    /// consumes silhouette/context fields; levels consume the detail fields.
    /// Load → Validate → Resolve → Freeze → Consume.</summary>
    public sealed class WorldStoryCatalog
    {
        [Serializable] sealed class Doc { public StoryMetadata[] entries = Array.Empty<StoryMetadata>(); }

        readonly Dictionary<string, StoryMetadata> _byId = new Dictionary<string, StoryMetadata>(StringComparer.Ordinal);
        static WorldStoryCatalog _instance;

        public static WorldStoryCatalog Current => _instance ??= Load();

        public static WorldStoryCatalog Load()
        {
            var catalog = new WorldStoryCatalog();
            var asset = Resources.Load<TextAsset>("QuietCamp/world_story");
            if (asset == null) return catalog;
            var doc = JsonConvert.DeserializeObject<Doc>(asset.text);
            foreach (var entry in doc?.entries ?? Array.Empty<StoryMetadata>())
                if (!string.IsNullOrEmpty(entry?.id)) catalog._byId[entry.id] = entry;
            return catalog;
        }

        /// <summary>Look up story metadata for a region, node or branch id.</summary>
        public StoryMetadata For(string id)
            => id != null && _byId.TryGetValue(id, out var entry) ? entry : null;

        /// <summary>Structural + tone invariants — every entry must be
        /// consistent with "roadmap shows context, the level shows detail",
        /// and nothing may present fiction as verified evidence.</summary>
        public static List<string> Validate(WorldMapData map, WorldStoryCatalog catalog)
        {
            var issues = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (map != null)
            {
                foreach (var region in map.Regions) ids.Add(region.Id);
                foreach (var node in map.Nodes) ids.Add(node.LevelId);
                foreach (var branch in map.Branches) ids.Add(branch.Id);
            }
            foreach (var pair in catalog._byId)
            {
                var e = pair.Value;
                // Forward-declared regions are pipeline content — authored
                // before their levels exist; warn so they stay visible.
                if (map != null && !ids.Contains(e.id)) issues.Add("warn:worldstory.unknown:" + e.id);
                if (e.Confidence == ReferenceConfidence.Verified && string.IsNullOrEmpty(e.historicalReference))
                    issues.Add("worldstory.unverified:" + e.id);
                // The roadmap never shows full detail — evidence stays in-level.
                if (e.Roadmap == StoryVisibility.Detail) issues.Add("worldstory.roadmapDetail:" + e.id);
                if (!string.IsNullOrEmpty(e.historicalReference) && e.Confidence == ReferenceConfidence.None)
                    issues.Add("worldstory.unlabeled:" + e.id);
            }
            return issues;
        }
    }
}
