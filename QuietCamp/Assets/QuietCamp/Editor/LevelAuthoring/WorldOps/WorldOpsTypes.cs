using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    public sealed class WorldRecipe
    {
        public int version=1,seed=4157,maxRepairRounds=5;
        public string id,label;
        public string[] levelIds=Array.Empty<string>();
        public int[] orders=Array.Empty<int>();
        public bool autoApply=true,capture=true;
        public WorldOperation[] operations=Array.Empty<WorldOperation>();
        public WorldCriteria criteria=new WorldCriteria();
    }
    public sealed class WorldCriteria
    {
        public bool requireVisualReview=true,autoRepair=true;
        public float contactTolerance=.03f,minimumCellPixels=24,maximumCentreError=2;
        public string[] profiles={"portrait","landscape"};
        public int quality=2;
    }
    public sealed class WorldSelector
    {
        public string[] ids=Array.Empty<string>(),modelIds=Array.Empty<string>(),roles=Array.Empty<string>();
        public bool primaryOnly,generatedOnly=true;
    }
    public sealed class WorldOperation
    {
        public string id,kind;
        public WorldSelector selector=new WorldSelector();
        public JObject parameters=new JObject();
    }
    public sealed class WorldAuthoringMetadata
    {
        public int version=1;
        public List<WorldObjectRecord> objects=new List<WorldObjectRecord>();
        public List<WorldPath> paths=new List<WorldPath>();
        public Dictionary<string,string> appliedRecipes=new Dictionary<string,string>();
    }
    public sealed class WorldObjectRecord
    {
        public string id,owner="manual",ensembleId,support="grounded",role;
        public bool pinned;
        public float maximumBurial;
    }
    public sealed class WorldPath
    {
        public string id,owner,startObjectId,endObjectId;
        public float[][] points;
        public float width=.8f,maximumSlope=.6f;
        public int color=0x796D50;
    }
    public sealed class ValidationFinding
    {
        public string code,levelId,objectId,message,severity="error",repair;
        public string profile,evidence;
        public float measured,limit;
    }
    public sealed class WorldChangeSet
    {
        public string levelId,beforeHash,afterHash;
        public string[] changedObjects=Array.Empty<string>(),changedModels=Array.Empty<string>();
        public string[] invalidated=Array.Empty<string>();
        public bool changed;
    }
    public sealed class WorldFrame
    {
        public string path,sha256,profile,state,phase;
        public List<ValidationFinding> findings=new List<ValidationFinding>();
        public Dictionary<string,string> evidence=new Dictionary<string,string>();
    }
    public sealed class ReviewPacket
    {
        public string runId,levelId,inputHash,documentHash;
        public int round;
        public List<WorldFrame> frames=new List<WorldFrame>();
        public List<ValidationFinding> findings=new List<ValidationFinding>();
    }
    public sealed class ReviewVerdict
    {
        public string runId,levelId,inputHash,documentHash,reviewer,notes;
        public int round;
        public bool accepted;
        public string[] inspectedFrameHashes=Array.Empty<string>();
        public WorldOperation[] corrections=Array.Empty<WorldOperation>();
        public string[] residuals=Array.Empty<string>();
    }
    public sealed class WorldLevelRun
    {
        public string levelId,status="queued",inputHash,documentHash,cacheKey,groupId,error,lastFailure;
        public int round,stalled,attempts;
        public string stagePath,beforePath,beforeFileHash;
        public string reviewHash;
        public string verdictHash;
        public bool cached;
        public Dictionary<string,string> sources=new Dictionary<string,string>();
        public WorldChangeSet changes;
        public List<ValidationFinding> findings=new List<ValidationFinding>();
        public ReviewPacket review;
        public bool visualAccepted;
        public List<ValidationFinding> previousReviewFindings=new List<ValidationFinding>();
        public Dictionary<string,int> visualStalls=new Dictionary<string,int>();
        public List<WorldFrame> beforeFrames=new List<WorldFrame>();
    }
    public sealed class WorldRun
    {
        public int version=1;
        public string id,status="preparing",directory,repositoryProject,editorProject,createdUtc,error;
        public WorldRecipe recipe;
        public string sourceManifestHash;
        public Dictionary<string,string> dependencies=new Dictionary<string,string>();
        public List<WorldLevelRun> levels=new List<WorldLevelRun>();
        public bool cancelled;
        public string nativeLevel,nativeProfile,nativePhase,nativeState,nativeFolder;
        public double nativeDeadline;
        public int nativeOriginalQuality=-1;
        public uint nativeOriginalWidth,nativeOriginalHeight;
        public bool workspaceCaptured;
        public string terrainScenePath;
        public string campaignValidationHash;
        public Dictionary<string,string> campaignSources=new Dictionary<string,string>();
        public bool agentDelivery;
        public string[] preexistingDirtyPaths=Array.Empty<string>();
    }
}
