using System;
using System.Collections.Generic;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    /// <summary>Indexed static catalog and layout. Only region leases own presentation scenes.</summary>
    public sealed class RoadmapCatalog
    {
        public readonly RoadmapDefinition Definition;
        public readonly RoadmapNodeData[] Nodes;
        public readonly float[] RegionStarts;
        public readonly float Height;
        public readonly RoadmapChunkRange[] Chunks;
        readonly int[] _chunkByNode;
        readonly Dictionary<string,int> _nodes=new Dictionary<string,int>(StringComparer.Ordinal);
        readonly Dictionary<string,int> _levels=new Dictionary<string,int>(StringComparer.Ordinal);
        readonly Dictionary<string,int> _regions=new Dictionary<string,int>(StringComparer.Ordinal);
        readonly int[] _regionByNode;
        public RoadmapCatalog(RoadmapDefinition definition)
        {
            var issues=RoadmapValidator.Validate(definition);
            if(issues.Count>0)throw new ArgumentException(string.Join("; ",issues));
            Definition=definition;var all=new List<RoadmapNodeData>();RegionStarts=new float[definition.regions.Length];
            float height=0;var memberships=new List<int>();
            for(int r=0;r<definition.regions.Length;r++)
            {
                var region=definition.regions[r];_regions.Add(region.id,r);RegionStarts[r]=height;
                foreach(var node in region.nodePositions){_nodes.Add(node.id,all.Count);_levels.Add(node.levelId,all.Count);all.Add(node);memberships.Add(r);}
                height+=region.height;
            }
            Nodes=all.ToArray();_regionByNode=memberships.ToArray();Height=height;
            _chunkByNode=new int[Nodes.Length];var chunks=new List<RoadmapChunkRange>();
            for(int r=0;r<definition.regions.Length;r++)
            {
                var region=definition.regions[r];var authored=region.chunks;
                if(authored==null||authored.Length==0)authored=new[]{new RoadmapChunkData{id=region.id+":chunk",firstOrder=region.firstLevel,lastOrder=region.lastLevel}};
                foreach(var chunk in authored)
                {
                    int first=chunk.firstOrder-1,last=chunk.lastOrder-1;
                    float top=first==region.firstLevel-1?RegionStarts[r]:(Y(first-1)+Y(first))*.5f;
                    float bottom=last==region.lastLevel-1?RegionStarts[r]+region.height:(Y(last)+Y(last+1))*.5f;
                    for(int n=first;n<=last;n++)_chunkByNode[n]=chunks.Count;
                    chunks.Add(new RoadmapChunkRange{Id=chunk.id,Region=r,FirstNode=first,LastNode=last,Top=top,Bottom=bottom});
                }
            }
            Chunks=chunks.ToArray();
            var bindings=definition.bakedBindings;
            if(bindings==null)throw new ArgumentException("invalid-baked-bindings");
            if(bindings.Length>0&&(bindings.Length!=Chunks.Length||Array.Exists(bindings,b=>b==null||b.resourceIndex<0||b.nodeOffset<0||b.branchKeyOffset<0||float.IsNaN(b.logicalOffset)||float.IsInfinity(b.logicalOffset)||string.IsNullOrEmpty(b.sourceRevision)||b.resourceId!=null&&(string.IsNullOrWhiteSpace(b.resourceId)||b.resourceId.StartsWith("/")||b.resourceId.Contains("..")||b.resourceId.Contains("\\")||b.resourceId.EndsWith(".asset")))))throw new ArgumentException("invalid-baked-bindings");
        }
        public int NodeIndex(string id)=>id!=null&&_nodes.TryGetValue(id,out var index)?index:-1;
        public int LevelIndex(string id)=>id!=null&&_levels.TryGetValue(id,out var index)?index:-1;
        public int RegionIndex(string id)=>id!=null&&_regions.TryGetValue(id,out var index)?index:-1;
        public int RegionForNode(int index)=>_regionByNode[index];
        public int ChunkForNode(int index)=>_chunkByNode[index];
        public int ChunkAt(float distance)
        {
            int lo=0,hi=Chunks.Length-1;
            while(lo<hi){int mid=(lo+hi+1)/2;if(Chunks[mid].Top<=distance)lo=mid;else hi=mid-1;}return lo;
        }
        public float Y(int index)=>RegionStarts[_regionByNode[index]]+Nodes[index].y;
        public int RegionAt(float distance)
        {
            int lo=0,hi=RegionStarts.Length-1;
            while(lo<hi){int mid=(lo+hi+1)/2;if(RegionStarts[mid]<=distance)lo=mid;else hi=mid-1;}
            return lo;
        }
        public int NearestNode(float distance)
        {
            int lo=NodeAt(distance);
            return lo+1<Nodes.Length&&Math.Abs(Y(lo+1)-distance)<Math.Abs(Y(lo)-distance)?lo+1:lo;
        }
        public int NodeAt(float distance)
        {
            int lo=0,hi=Nodes.Length-1;
            while(lo<hi){int mid=(lo+hi+1)/2;if(Y(mid)<=distance)lo=mid;else hi=mid-1;}
            return lo;
        }
        public int Frontier(ProgressionService progress)
        {
            // Called on entry/progress change, never per-frame. Completed saves may contain gaps.
            int frontier=0;
            for(int i=0;i<Nodes.Length;i++)if(progress.IsCompleted(Nodes[i].levelId))frontier=Math.Max(frontier,Math.Min(i+1,Nodes.Length-1));
            return frontier;
        }
        public RoadmapReveal Reveal(int index,int frontier)
        {
            int distance=Math.Min(2,Math.Max(0,Definition.revealDistance));
            var rules=Definition.regions[RegionForNode(frontier)].revealRules;
            if(rules!=null)distance=Math.Min(distance,Math.Max(0,rules.nearFuture));
            return index<=frontier?RoadmapReveal.Revealed:index<=frontier+distance?RoadmapReveal.Silhouette:RoadmapReveal.Hidden;
        }
        public RoadmapNodeState State(int index,ProgressionService progress)
        {
            if(progress.IsCompleted(Nodes[index].levelId))return RoadmapNodeState.Completed;
            int frontier=Frontier(progress);
            if(index==frontier)return RoadmapNodeState.Current;
            if(index==frontier+1)return RoadmapNodeState.Next;
            return Reveal(index,frontier)==RoadmapReveal.Hidden?RoadmapNodeState.Unknown:RoadmapNodeState.Locked;
        }
        public bool BranchAvailable(RoadmapBranchData branch,ProgressionService progress)
        {
            if(!branch.published)return false;
            foreach(var id in branch.requires)if(!progress.IsCompleted(id))return false;
            return true;
        }
    }
    public struct RoadmapChunkRange
    {
        public string Id;
        public int Region,FirstNode,LastNode;
        public float Top,Bottom;
    }
    /// <summary>No scene creation. Fixed, allocation-free current +/- one window.</summary>
    public sealed class RoadmapWindow
    {
        public int First {get;private set;}=-1;
        public int Last {get;private set;}=-1;
        public int Revision {get;private set;}
        public bool Move(int current,int regionCount,int neighbors=1)
        {
            if(neighbors<0||neighbors>1)throw new ArgumentOutOfRangeException(nameof(neighbors));
            if(regionCount<1)throw new ArgumentOutOfRangeException(nameof(regionCount));
            current=Math.Max(0,Math.Min(regionCount-1,current));int first=Math.Max(0,current-neighbors),last=Math.Min(regionCount-1,current+neighbors);
            if(first==First&&last==Last)return false;First=first;Last=last;Revision++;return true;
        }
        public bool Contains(int region)=>region>=First&&region<=Last;
        public int Count=>First<0?0:Last-First+1;
    }
}
