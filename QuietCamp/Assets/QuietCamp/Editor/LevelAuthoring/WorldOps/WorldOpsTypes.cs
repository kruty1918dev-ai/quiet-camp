using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    [Serializable] public sealed class WorldRecipe
    {
        public int version=1,seed=4157,maxRepairRounds=5;
        public string id,label;
        public string[] levelIds=Array.Empty<string>();
        public int[] orders=Array.Empty<int>();
        public bool autoApply=true;
        public WorldOperation[] operations=Array.Empty<WorldOperation>();
        public WorldCriteria criteria=new WorldCriteria();
    }
    [Serializable] public sealed class WorldCriteria
    {
        public bool requireVisualReview=true,autoRepair=true;
        public float contactTolerance=.03f,minimumCellPixels=24,maximumCentreError=2;
        public string[] profiles={"portrait","landscape"};
        public int quality=2;
    }
    [Serializable] public sealed class WorldSelector
    {
        public string[] ids=Array.Empty<string>(),modelIds=Array.Empty<string>(),roles=Array.Empty<string>();
        public bool primaryOnly,generatedOnly=true;
    }
    [Serializable] public sealed class WorldOperation
    {
        public string id,kind;
        public WorldSelector selector=new WorldSelector();
        public JObject parameters=new JObject();
    }
    [Serializable] public sealed class WorldAuthoringMetadata
    {
        public int version=1;
        public List<WorldObjectRecord> objects=new List<WorldObjectRecord>();
        public List<WorldPath> paths=new List<WorldPath>();
        public Dictionary<string,string> appliedRecipes=new Dictionary<string,string>();
    }
    [Serializable] public sealed class WorldObjectRecord
    {
        public string id,owner="manual",ensembleId,support="grounded",role;
        public bool pinned;
        public float maximumBurial;
    }
    [Serializable] public sealed class WorldPath
    {
        public string id,owner,startObjectId,endObjectId;
        public float[][] points;
        public float width=.8f,maximumSlope=.6f;
        public int color=0x796D50;
    }
    [Serializable] public sealed class ValidationFinding
    {
        public string code,levelId,objectId,message,severity="error",repair;
        public string profile,evidence;
        public float measured,limit;
    }
    [Serializable] public sealed class WorldChangeSet
    {
        public string levelId,beforeHash,afterHash;
        public string[] changedObjects=Array.Empty<string>(),changedModels=Array.Empty<string>();
        public string[] invalidated=Array.Empty<string>();
        public bool changed;
    }
    [Serializable] public sealed class WorldFrame
    {
        public string path,sha256,profile,state,phase;
        public List<ValidationFinding> findings=new List<ValidationFinding>();
    }
    [Serializable] public sealed class ReviewPacket
    {
        public string runId,levelId,inputHash,documentHash;
        public int round;
        public List<WorldFrame> frames=new List<WorldFrame>();
        public List<ValidationFinding> findings=new List<ValidationFinding>();
    }
    [Serializable] public sealed class ReviewVerdict
    {
        public string runId,levelId,inputHash,documentHash,reviewer,notes;
        public int round;
        public bool accepted;
        public string[] inspectedFrameHashes=Array.Empty<string>();
        public WorldOperation[] corrections=Array.Empty<WorldOperation>();
        public string[] residuals=Array.Empty<string>();
    }
    [Serializable] public sealed class WorldLevelRun
    {
        public string levelId,status="queued",inputHash,documentHash,cacheKey,groupId,error,lastFailure;
        public int round,stalled;
        public string stagePath,beforePath;
        public string reviewHash;
        public Dictionary<string,string> sources=new Dictionary<string,string>();
        public WorldChangeSet changes;
        public List<ValidationFinding> findings=new List<ValidationFinding>();
        public ReviewPacket review;
        public bool visualAccepted;
    }
    [Serializable] public sealed class WorldRun
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
        public bool workspaceCaptured;
    }
}
