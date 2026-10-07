using System;
using System.Collections.Generic;
using QuietCamp.Domain;

namespace QuietCamp.Infrastructure
{
    /// <summary>Static integrity checks over a built WorldMapData. Runs in
    /// EditMode tests and in the authoring pipeline — cheap enough to run on
    /// every build, catches content bugs before the renderer sees them.</summary>
    public static class WorldMapValidator
    {
        /// <summary>Season pairs allowed to sit next to each other without a
        /// transition zone. Same season is always fine; adjacent steps are fine;
        /// a jump of two or more steps is an "invalid transition".</summary>
        static readonly string[] SeasonOrder = { "spring", "summer", "autumn", "winter" };

        public static List<string> Validate(WorldMapData data, Func<string, bool> hasKey = null)
        {
            var issues = new List<string>();
            if (data == null) { issues.Add("worldmap.null"); return issues; }

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in data.Nodes)
                if (node?.LevelId == null || !nodeIds.Add(node.LevelId)) issues.Add("worldmap.node.duplicate");

            var branchIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var branch in data.Branches)
            {
                if (branch?.Id == null || !branchIds.Add(branch.Id)) { issues.Add("worldmap.branch.duplicate"); continue; }
                if (branch.NodeIds == null || branch.NodeIds.Length == 0) issues.Add("worldmap.branch.empty:" + branch.Id);
                if (branch.AttachOrder < 1 || branch.AttachOrder > data.Nodes.Length) issues.Add("worldmap.branch.unreachable:" + branch.Id);
                if (branch.RequiredCompletions > data.Nodes.Length) issues.Add("worldmap.branch.impossible:" + branch.Id);
                if (string.IsNullOrEmpty(branch.TitleKey)) issues.Add("worldmap.branch.title:" + branch.Id);
                if (branch.TeaserDepth > (branch.NodeIds?.Length ?? 0)) issues.Add("worldmap.branch.teaser:" + branch.Id);
            }

            for (var i = 0; i < data.Regions.Length; i++)
            {
                var region = data.Regions[i];
                if (region == null || string.IsNullOrEmpty(region.Id)) { issues.Add("worldmap.region.id"); continue; }
                if (region.From != (i == 0 ? 1 : data.Regions[i - 1].To + 1)) issues.Add("worldmap.region.gap:" + region.Id);
                if (region.To < region.From || region.To > data.Nodes.Length) issues.Add("worldmap.region.range:" + region.Id);
                if (i > 0 && region.TransitionFrom != data.Regions[i - 1].Id) issues.Add("worldmap.region.transition:" + region.Id);
                // Season gaps are inputs to the transition-zone system, not hard
                // failures — reported as warnings so the real world validates.
                if (i > 0 && SeasonJump(data.Regions[i - 1].Season, region.Season)) issues.Add("warn:worldmap.region.season:" + region.Id);
            }
            if (data.Regions.Length > 0 && data.Regions[^1].To != data.Nodes.Length) issues.Add("worldmap.region.tail");

            for (var i = 0; i < data.Chunks.Length; i++)
            {
                var chunk = data.Chunks[i];
                if (chunk == null) { issues.Add("worldmap.chunk.null"); continue; }
                if (chunk.FirstOrder != (i == 0 ? 1 : data.Chunks[i - 1].LastOrder + 1)) issues.Add("worldmap.chunk.gap:" + chunk.Index);
                if (chunk.LastOrder > data.Nodes.Length || chunk.LastOrder < chunk.FirstOrder) issues.Add("worldmap.chunk.range:" + chunk.Index);
                if (chunk.NodeIds == null || chunk.NodeIds.Length != chunk.LastOrder - chunk.FirstOrder + 1) issues.Add("worldmap.chunk.nodes:" + chunk.Index);
            }
            if (data.Chunks.Length > 0 && data.Chunks[^1].LastOrder != data.Nodes.Length) issues.Add("worldmap.chunk.tail");

            // Node order and geometry: main orders contiguous 1..N, Y monotonic.
            for (var i = 0; i < data.Nodes.Length; i++)
            {
                var node = data.Nodes[i];
                if (node.Order != i + 1) issues.Add("worldmap.node.order:" + node.LevelId);
                if (i > 0 && node.Y <= data.Nodes[i - 1].Y) issues.Add("worldmap.node.monotonic:" + node.LevelId);
                if (node.RegionIndex < 0 || node.RegionIndex >= data.Regions.Length) issues.Add("worldmap.node.region:" + node.LevelId);
                if (node.ChunkIndex < 0 || node.ChunkIndex >= data.Chunks.Length) issues.Add("worldmap.node.chunk:" + node.LevelId);
            }

            if (hasKey != null)
                foreach (var branch in data.Branches)
                {
                    if (!string.IsNullOrEmpty(branch.TitleKey) && !hasKey(branch.TitleKey)) issues.Add("worldmap.loc:" + branch.TitleKey);
                    if (!string.IsNullOrEmpty(branch.DescriptionKey) && !hasKey(branch.DescriptionKey)) issues.Add("worldmap.loc:" + branch.DescriptionKey);
                }
            return issues;
        }

        static bool SeasonJump(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            int ia = Array.IndexOf(SeasonOrder, a), ib = Array.IndexOf(SeasonOrder, b);
            return ia >= 0 && ib >= 0 && Math.Abs(ia - ib) > 1;
        }
    }
}
