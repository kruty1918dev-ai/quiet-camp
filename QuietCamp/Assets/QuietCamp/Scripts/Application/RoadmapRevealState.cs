using System;
using System.Collections.Generic;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    /// <summary>Progress-derived visibility. Scroll positions and replay selection never grant reveal.</summary>
    public sealed class RoadmapRevealState
    {
        readonly RoadmapCatalog _catalog;
        readonly ProgressionService _progress;
        readonly HashSet<RoadmapBranchData> _visibleBranches=new HashSet<RoadmapBranchData>();
        int _revision=-1;
        public int Frontier {get;private set;}
        public int LastKnown {get;private set;}
        public int Revision=>_revision;
        public RoadmapRevealState(RoadmapCatalog catalog,ProgressionService progress)
        {_catalog=catalog;_progress=progress;Refresh();}
        public bool Refresh()
        {
            if(_revision==_progress.Revision)return false;
            _revision=_progress.Revision;Frontier=_catalog.Frontier(_progress);
            int distance=Math.Min(2,Math.Max(0,_catalog.Definition.revealDistance));
            var rules=_catalog.Definition.regions[_catalog.RegionForNode(Frontier)].revealRules;
            if(rules!=null)distance=Math.Min(distance,Math.Max(0,rules.nearFuture));
            LastKnown=Math.Min(_catalog.Nodes.Length-1,Frontier+distance);
            _visibleBranches.Clear();RoadmapBranchData nearest=null;int nearestDistance=int.MaxValue;
            foreach(var region in _catalog.Definition.regions)foreach(var branch in region.branches)
            {
                int anchor=_catalog.NodeIndex(branch.anchorNodeId);
                if(anchor<0||anchor>Frontier||branch.teaserState=="hidden")continue;
                if(BranchCompleted(branch)){_visibleBranches.Add(branch);continue;}
                int delta=Frontier-anchor;
                // Stable author order resolves ties. At most one uncompleted branch teaser.
                if(delta<nearestDistance){nearestDistance=delta;nearest=branch;}
            }
            if(nearest!=null&&(rules?.branchTeaser??1)>0)_visibleBranches.Add(nearest);
            return true;
        }
        public RoadmapReveal Main(int index)=>index<=Frontier?RoadmapReveal.Revealed:index<=LastKnown?RoadmapReveal.Silhouette:RoadmapReveal.Hidden;
        public bool BranchVisible(RoadmapBranchData branch)=>_visibleBranches.Contains(branch);
        public int BranchFrontier(RoadmapBranchData branch)
        {
            int frontier=0;var nodes=branch.nodes??Array.Empty<RoadmapBranchNodeData>();
            for(int i=0;i<nodes.Length;i++)if(_progress.IsCompleted(nodes[i].levelId))frontier=Math.Max(frontier,Math.Min(i+1,nodes.Length-1));
            return frontier;
        }
        public bool BranchCompleted(RoadmapBranchData branch)
        {
            var nodes=branch.nodes;
            if(nodes==null||nodes.Length==0)return !string.IsNullOrEmpty(branch.bonusId)&&_progress.IsCompleted(branch.bonusId);
            foreach(var node in nodes)if(!_progress.IsCompleted(node.levelId))return false;
            return true;
        }
        /// <summary>Main-map teaser stays short even after unlocking. The journey's own catalog shows its completed world.</summary>
        public RoadmapReveal BranchNode(RoadmapBranchData branch,int index)
        {
            if(!BranchVisible(branch)||index<0||index>=(branch.nodes?.Length??0))return RoadmapReveal.Hidden;
            if(branch.type=="bonus")return index==0?(_catalog.BranchAvailable(branch,_progress)?RoadmapReveal.Revealed:RoadmapReveal.Silhouette):RoadmapReveal.Hidden;
            if(branch.teaserDepth==0||index>=RoadmapBranchPolicy.TeaserCount(branch))return RoadmapReveal.Hidden;
            return index==0&&(_catalog.BranchAvailable(branch,_progress)||_progress.IsCompleted(branch.nodes[0].levelId))?RoadmapReveal.Revealed:RoadmapReveal.Silhouette;
        }
        /// <summary>Leaves atmosphere beyond the final known node but never lets the camera reach unknown campaign nodes.</summary>
        public float MaxScrollDistance(float viewportHeight)
        {
            float end=_catalog.Y(LastKnown)+180;
            // A revealed side glade may sit beyond the final main node. Include its
            // entrance, never the undiscovered length of the branch itself.
            for(int r=0;r<_catalog.Definition.regions.Length;r++)
                foreach(var branch in _catalog.Definition.regions[r].branches)
                    if(BranchVisible(branch))end=Math.Max(end,_catalog.RegionStarts[r]+branch.y+branch.radius+90);
            return Math.Max(0,Math.Min(_catalog.Height-viewportHeight,end-viewportHeight*.68f));
        }
    }
}
