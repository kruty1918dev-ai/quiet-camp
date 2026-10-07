using System;

namespace QuietCamp.Domain
{
    /// <summary>How a side route is reached; presentation decides how to ask.</summary>
    public enum WorldBranchAccess { Free, Progression, Embers, Rewarded, Purchase, Entitlement }
    /// <summary>BonusGlade = one level; MiniTrail = 3–5; StoryJourney = 8+.</summary>
    public enum WorldBranchType { BonusGlade, MiniTrail, StoryJourney }
    /// <summary>Derived from progression at query time — never baked into data.</summary>
    public enum WorldNodeState { Hidden, Teaser, Locked, Available, Current, Completed }

    /// <summary>One stop on the world road or on a branch. Position is map-local
    /// px in the same space the legacy RoadmapLayout used.</summary>
    [Serializable] public sealed class WorldMapNode
    {
        public string LevelId;
        public int Order;                 // 1-based order along its own path
        public int RegionIndex = -1;      // main nodes only
        public int ChunkIndex = -1;       // main nodes only
        public float X, Y;
        public bool Main;
        public string PreviewId;          // hero motif / scene identity
        public string StoryHint;          // localization key, may be null
        public string[] Props = Array.Empty<string>();
        public string[] BranchLinks = Array.Empty<string>();
    }

    /// <summary>A route that physically leaves the main road.</summary>
    [Serializable] public sealed class WorldMapBranch
    {
        public string Id, TitleKey, DescriptionKey;
        public WorldBranchType Type;
        public WorldBranchAccess Access;
        public int AttachOrder;           // main-path order it forks from
        public int TeaserDepth = 1;       // nodes visible before the atmosphere takes over
        public int RequiredCompletions;
        public string HeroLandmark;
        public string[] NodeIds = Array.Empty<string>();
    }

    /// <summary>A named run of the world — one visual identity, one stretch of road.</summary>
    [Serializable] public sealed class WorldMapRegion
    {
        public string Id;                 // district id — stable
        public int Act, From, To;         // 1-based inclusive main orders
        public string Biome, Season, Lighting;
        public float WeatherBias;         // 0..1 precipitation tendency
        public string VegetationProfile, TerrainProfile;
        public string HeroLandmark;
        public string[] Motifs = Array.Empty<string>();
        public string TransitionFrom;     // previous region id — blend anchor
        public string[] BranchIds = Array.Empty<string>();
    }

    /// <summary>The activation unit: only current±1 chunks may be live geometry.</summary>
    [Serializable] public sealed class WorldMapChunk
    {
        public int Index;
        public int FirstOrder, LastOrder; // main orders covered, inclusive
        public string RegionId;
        public string[] NodeIds = Array.Empty<string>();
        public string[] BranchIds = Array.Empty<string>();
    }

    [Serializable] public sealed class WorldMapData
    {
        public int Version = 1;
        public WorldMapRegion[] Regions = Array.Empty<WorldMapRegion>();
        public WorldMapChunk[] Chunks = Array.Empty<WorldMapChunk>();
        public WorldMapNode[] Nodes = Array.Empty<WorldMapNode>();
        public WorldMapBranch[] Branches = Array.Empty<WorldMapBranch>();
    }
}
