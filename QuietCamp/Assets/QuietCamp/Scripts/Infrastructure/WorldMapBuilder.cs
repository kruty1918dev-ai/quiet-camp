using System;
using System.Collections.Generic;
using QuietCamp.Application;
using QuietCamp.Domain;

namespace QuietCamp.Infrastructure
{
    /// <summary>Composes the world map from the existing catalogs — campaign
    /// districts, bonus slots and journeys stay the authoring truth; this
    /// builder derives the Region→Chunk→Node→Branch view the new roadmap
    /// renders. No renderer ever reads catalogs directly.</summary>
    public static class WorldMapBuilder
    {
        public const int ChunkSize = 8;

        public static WorldMapData Build(
            IReadOnlyList<string> mainIds,
            IReadOnlyList<DistrictDefinition> districts,
            IReadOnlyList<BonusCampDefinition> bonusSlots,
            IEnumerable<JourneyDefinition> journeys,
            Func<string, LevelSummary> summaryOf)
        {
            var data = new WorldMapData();
            var nodes = new List<WorldMapNode>(mainIds.Count + 64);
            var branches = new List<WorldMapBranch>();
            var regions = new List<WorldMapRegion>(districts.Count);

            // Region identity comes from the district table; each region samples
            // its first level's environment so biome/season follow real content.
            for (var r = 0; r < districts.Count; r++)
            {
                var d = districts[r];
                var first = r < districts.Count && d.from - 1 < mainIds.Count ? summaryOf(mainIds[d.from - 1]) : null;
                regions.Add(new WorldMapRegion
                {
                    Id = d.id, Act = d.act, From = d.from, To = d.to,
                    Season = first?.environment?.seasonId ?? "",
                    Biome = first?.environmentPreset ?? "meadow",
                    Lighting = first?.lighting ?? "morning",
                    WeatherBias = first?.environment == null ? .3f : Clamp01(first.environment.moisture),
                    VegetationProfile = Vegetation(first),
                    TerrainProfile = first?.environment?.shore != null ? "shore" : "inland",
                    HeroLandmark = first?.environment?.storyMotifs != null && first.environment.storyMotifs.Length > 0 ? first.environment.storyMotifs[0] : null,
                    Motifs = first?.environment?.storyMotifs ?? Array.Empty<string>(),
                    TransitionFrom = r > 0 ? districts[r - 1].id : null,
                });
            }

            int GapsBefore(int order)
            {
                var gaps = 0;
                foreach (var slot in bonusSlots) if (slot.afterLevel <= order - 1) gaps++;
                return gaps;
            }

            var attachBranches = new Dictionary<int, List<string>>();
            void Attach(int order, string branchId)
            {
                if (!attachBranches.TryGetValue(order, out var list)) attachBranches[order] = list = new List<string>();
                list.Add(branchId);
            }

            for (var i = 0; i < mainIds.Count; i++)
            {
                var order = i + 1;
                var level = summaryOf(mainIds[i]);
                nodes.Add(new WorldMapNode
                {
                    LevelId = mainIds[i], Order = order, Main = true,
                    RegionIndex = RegionIndexFor(districts, order),
                    ChunkIndex = i / ChunkSize,
                    X = WorldMapGeometry.NodeX(i),
                    Y = WorldMapGeometry.MainY(i, GapsBefore(order)),
                    PreviewId = level?.environment?.storyMotifs != null && level.environment.storyMotifs.Length > 0 ? level.environment.storyMotifs[0] : null,
                    StoryHint = "journey.main.story." + order,
                    Props = PreviewProps(level),
                });
            }

            WorldMapNode[] BranchNodes(string[] ids, int attachOrder, string branchId)
            {
                var anchor = nodes[Math.Min(attachOrder, mainIds.Count) - 1];
                var side = WorldMapGeometry.BranchSide(branchId, attachOrder);
                var result = new WorldMapNode[ids.Length];
                for (var i = 0; i < ids.Length; i++)
                {
                    WorldMapGeometry.BranchPoint(anchor.X, anchor.Y, i, side, out var x, out var y);
                    var level = summaryOf(ids[i]);
                    result[i] = new WorldMapNode
                    {
                        LevelId = ids[i], Order = i + 1, Main = false, X = x, Y = y,
                        PreviewId = level?.environment?.storyMotifs != null && level.environment.storyMotifs.Length > 0
                            ? level.environment.storyMotifs[0] : null,
                        Props = PreviewProps(level),
                    };
                }
                return result;
            }

            foreach (var slot in bonusSlots ?? Array.Empty<BonusCampDefinition>())
            {
                if (slot == null || slot.afterLevel > mainIds.Count || string.IsNullOrEmpty(slot.levelId)) continue;
                var ids = new[] { slot.levelId };
                branches.Add(new WorldMapBranch
                {
                    Id = slot.id, Type = WorldBranchType.BonusGlade,
                    Access = slot.requiresPremium ? WorldBranchAccess.Purchase
                        : slot.requiredCompletions > 0 ? WorldBranchAccess.Progression : WorldBranchAccess.Free,
                    AttachOrder = slot.afterLevel, TeaserDepth = 1,
                    RequiredCompletions = slot.requiredCompletions,
                    HeroLandmark = slot.theme,
                    TitleKey = slot.titleKey, DescriptionKey = slot.descriptionKey,
                    NodeIds = ids, Nodes = BranchNodes(ids, slot.afterLevel, slot.id),
                });
                Attach(slot.afterLevel, slot.id);
            }

            foreach (var journey in journeys ?? Array.Empty<JourneyDefinition>())
            {
                // "bonus.*" journeys are synthetic mirrors of the bonus slot
                // table — the slots already became BonusGlade branches.
                if (journey == null || journey.id == "main" || journey.id == "qa" || journey.id.StartsWith("bonus.")
                    || journey.levelIds == null || journey.levelIds.Length == 0 || !journey.published) continue;
                var attach = Math.Max(1, Math.Min(journey.requiredCompletions, mainIds.Count));
                var type = journey.levelIds.Length <= 5 ? WorldBranchType.MiniTrail : WorldBranchType.StoryJourney;
                branches.Add(new WorldMapBranch
                {
                    Id = journey.id, Type = type,
                    Access = journey.requiredCompletions > 0 ? WorldBranchAccess.Progression
                        : journey.currencyCost > 0 ? WorldBranchAccess.Embers
                        : !string.IsNullOrEmpty(journey.entitlementId) ? WorldBranchAccess.Purchase
                        : WorldBranchAccess.Free,
                    // MiniTrail teases its second node as a silhouette; a story
                    // journey shows attachment + landmark + the opening pair.
                    AttachOrder = attach,
                    TeaserDepth = type == WorldBranchType.BonusGlade ? 1 : 2,
                    RequiredCompletions = journey.requiredCompletions,
                    HeroLandmark = journey.id,
                    TitleKey = journey.titleKey, DescriptionKey = journey.descriptionKey,
                    NodeIds = journey.levelIds, Nodes = BranchNodes(journey.levelIds, attach, journey.id),
                });
                Attach(attach, journey.id);
            }

            foreach (var node in nodes)
                if (attachBranches.TryGetValue(node.Order, out var links))
                    node.BranchLinks = links.ToArray();

            var chunkCount = (mainIds.Count + ChunkSize - 1) / ChunkSize;
            var chunks = new List<WorldMapChunk>(chunkCount);
            for (var c = 0; c < chunkCount; c++)
            {
                var first = c * ChunkSize;
                var last = Math.Min(first + ChunkSize, mainIds.Count) - 1;
                var chunk = new WorldMapChunk
                {
                    Index = c, FirstOrder = first + 1, LastOrder = last + 1,
                    RegionId = districts[RegionIndexFor(districts, last + 1)].id,
                    NodeIds = new string[last - first + 1],
                };
                for (var i = first; i <= last; i++) chunk.NodeIds[i - first] = mainIds[i];
                var anchors = new List<string>();
                foreach (var branch in branches)
                    if (branch.AttachOrder >= chunk.FirstOrder && branch.AttachOrder <= chunk.LastOrder)
                        anchors.Add(branch.Id);
                chunk.BranchIds = anchors.ToArray();
                chunks.Add(chunk);
            }
            foreach (var region in regions)
                region.BranchIds = branches.FindAll(b => b.AttachOrder >= region.From && b.AttachOrder <= region.To)
                    .ConvertAll(b => b.Id).ToArray();

            data.Regions = regions.ToArray();
            data.Chunks = chunks.ToArray();
            data.Nodes = nodes.ToArray();
            data.Branches = branches.ToArray();
            return data;
        }

        static int RegionIndexFor(IReadOnlyList<DistrictDefinition> districts, int order)
        {
            for (var i = 0; i < districts.Count; i++)
                if (order >= districts[i].from && order <= districts[i].to) return i;
            return districts.Count - 1;
        }

        static string Vegetation(LevelSummary level)
        {
            if (level?.environment == null) return "mixed";
            var density = Clamp01(level.environment.treeDensity);
            return level.environmentPreset == "pines" || density > .66f ? "forest"
                : density > .33f ? "grove" : "meadow";
        }

        static string[] PreviewProps(LevelSummary level)
        {
            if (level?.mapObjects == null || level.mapObjects.Length == 0) return Array.Empty<string>();
            var props = new List<string>(3);
            foreach (var prop in level.mapObjects)
            {
                if (prop?.assetId == null || props.Contains(prop.assetId)) continue;
                props.Add(prop.assetId);
                if (props.Count == 3) break;
            }
            return props.ToArray();
        }

        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
