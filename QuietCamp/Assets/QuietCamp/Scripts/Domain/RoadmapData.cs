using System;

namespace QuietCamp.Domain
{
    /// <summary>Portable authoring data. No Unity objects, puzzle generation or save ownership.</summary>
    [Serializable] public sealed class RoadmapDefinition
    {
        public int schemaVersion=1, revealDistance=2;
        public string revision, journeyId="main";
        public string compositionManifest;
        // Native resources are bound independently; synthetic load tests may alias them with logical offsets.
        public BakedChunkBinding[] bakedBindings=Array.Empty<BakedChunkBinding>();
        public bool ShouldSerializebakedBindings()=>bakedBindings.Length>0;
        public bool ShouldSerializecompositionManifest()=>compositionManifest!=null;
        public string presentation="projected"; // Migration switch; new diorama authoring uses world3d.
        public RoadmapRegionData[] regions=Array.Empty<RoadmapRegionData>();
    }
    [Serializable] public sealed class BakedChunkBinding
    {public int resourceIndex,nodeOffset,branchKeyOffset;public float logicalOffset;public string sourceRevision,resourceId;}
    [Serializable] public sealed class RoadmapRegionData
    {
        public string id,titleKey,biome,season,landmark,previewId,performanceTier="balanced";
        public int firstLevel,lastLevel;
        public float height;
        public string environmentPhase;
        public string lighting="warm",vegetationProfile="meadow",terrainProfile="gentle-hills";
        public float weatherBias=.15f;
        public string[] environmentalMotifs=Array.Empty<string>();
        public RoadmapRevealRules revealRules=new RoadmapRevealRules();
        public RoadmapChunkData[] chunks=Array.Empty<RoadmapChunkData>();
        public RoadmapTransitionData transition=new RoadmapTransitionData();
        public RoadmapNodeData[] nodePositions=Array.Empty<RoadmapNodeData>();
        public RoadmapBranchData[] branches=Array.Empty<RoadmapBranchData>();
        public RoadmapStoryPropData[] storyProps=Array.Empty<RoadmapStoryPropData>();
        public RoadmapHistoryData historical=new RoadmapHistoryData();
        public EnvironmentalStoryData environmentalStory;
        public bool ShouldSerializeenvironmentalStory() => environmentalStory != null;
        public RoadmapCulturalLandscapeData culturalLandscape;
        public bool ShouldSerializeculturalLandscape() => culturalLandscape != null;
    }
    /// <summary>Map presentation only. Never changes a puzzle, footprint, unlock or save hash.</summary>
    [Serializable] public sealed class RoadmapCulturalLandscapeData
    {
        public string profile="ukrainian-rural";
        public int seed=1918;
        public float width=84,fields=.65f,orchards=.35f,settlements=.3f;
        public bool powerLine=true;
    }
    [Serializable] public sealed class RoadmapTransitionData
    {
        public string fromSeason,toSeason,kind="blend";
        public float length=220;
    }
    [Serializable] public sealed class RoadmapNodeData
    {
        public string id,levelId,previewId,titleKey;
        public string storyHint;
        public string[] associatedProps=Array.Empty<string>(),branchLinks=Array.Empty<string>();
        public int order;
        public float x,y,radius=62;
        public string[] requires=Array.Empty<string>();
        public RoadmapWorldData world=new RoadmapWorldData();
    }
    [Serializable] public sealed class RoadmapBranchData
    {
        public string id,anchorNodeId,targetRegionId,journeyId,bonusId,titleKey,previewId,teaserState="silhouette";
        public float x,y,radius=135;
        public bool published;
        public string type="bonus",visualIdentity="forest";
        public string visibilityRule="anchor-revealed",accessRule="progression";
        public string descriptionKey,heroLandmark;
        public string biome,season,lighting;
        public RoadmapWorldData world=new RoadmapWorldData();
        public RoadmapBranchAccessOption[] accessOptions=Array.Empty<RoadmapBranchAccessOption>();
        public int teaserDepth=1;
        public RoadmapBranchNodeData[] nodes=Array.Empty<RoadmapBranchNodeData>();
        public string[] requires=Array.Empty<string>();
    }
    [Serializable] public sealed class RoadmapBranchAccessOption
    {
        public string method="progression",productId,entitlementId,placement;
    }
    [Serializable] public sealed class RoadmapRevealRules
    {
        public int nearFuture=2,branchTeaser=1;
        public string atmosphere="soft-haze",farLandmarkAssetId;
        public float farLandmarkHeight=5,farLandmarkX=.5f;
    }
    [Serializable] public sealed class RoadmapChunkData
    {
        public string id;
        public int firstOrder,lastOrder;
    }
    [Serializable] public sealed class RoadmapBranchNodeData
    {
        public string id,levelId,previewId;
        public float x,y;
        public RoadmapWorldData world=new RoadmapWorldData();
    }
    [Serializable] public sealed class RoadmapStoryPropData
    {
        public string assetId,motif,interpretation="fictional";
        public float x,y,height=1,yaw;
    }
    [Serializable] public sealed class RoadmapHistoryData
    {
        public string contextId,interpretation="fictional",cultureKey;
        public bool verified;
        public string[] sources=Array.Empty<string>(),claims=Array.Empty<string>();
    }
    [Serializable] public sealed class RoadmapWorldData
    {
        public float extent=460;
        public RoadmapPropData[] props=Array.Empty<RoadmapPropData>();
    }
    [Serializable] public sealed class RoadmapPropData
    {
        public string assetId;
        public string storyId,storyVisibility,storyAppearance;
        public bool ShouldSerializestoryId() => storyId != null;
        public bool ShouldSerializestoryVisibility() => storyVisibility != null;
        public bool ShouldSerializestoryAppearance() => storyAppearance != null;
        public float x,z,height=1,yaw;
        public bool sway;
    }
    public enum RoadmapNodeState { Completed,Current,Next,Locked,Unknown }
    public enum RoadmapReveal { Hidden, Silhouette, Revealed }
    [Serializable] public sealed class RoadmapAnchor
    {
        public string journeyId="main",nodeId,revision;
        public float offset;
    }
}
