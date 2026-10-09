using System;
using System.Threading.Tasks;
using QuietCamp.Domain;
namespace QuietCamp.Application
{
    /// <summary>Presentation limits and access vocabulary; neither a preview nor a scroll grants access.</summary>
    public static class RoadmapBranchPolicy
    {
        public static int TeaserCount(RoadmapBranchData branch)=>branch.type=="bonus"?Math.Min(1,branch.nodes?.Length??0):Math.Min(Math.Min(2,branch.nodes?.Length??0),branch.teaserDepth+1);
        public static bool ValidAccess(string method)=>method=="progression"||method=="embers"||method=="rewarded"||method=="permanent-purchase"||method=="subscription"||method=="free-story";
        public static string Landmark(RoadmapBranchData branch)=>!string.IsNullOrEmpty(branch.heroLandmark)?branch.heroLandmark:branch.visualIdentity=="fireflies"?"branch_pond":branch.visualIdentity=="lighthouse"?"branch_lighthouse":branch.visualIdentity=="station"?"branch_station":branch.visualIdentity=="garden"?"branch_greenhouse":"story_trail_shelter";
        public static int Completed(RoadmapBranchData branch,ProgressionService progress)
        {int count=0;foreach(var node in branch.nodes??Array.Empty<RoadmapBranchNodeData>())if(progress.IsCompleted(node.levelId))count++;return count;}
    }
    /// <summary>Optional SDK/backend adapter. Must validate its result and persist the grant in the
    /// authoritative journey access service. UI never grants an entitlement on success alone.
    /// Subscription availability must reflect an active entitlement, including expiry/revocation.</summary>
    public interface IRoadmapBranchUnlockProvider
    {
        bool Available(RoadmapBranchData branch,RoadmapBranchAccessOption option);
        Task<bool> Request(RoadmapBranchData branch,RoadmapBranchAccessOption option);
    }
}
