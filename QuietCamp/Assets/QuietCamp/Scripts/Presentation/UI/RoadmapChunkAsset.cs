using System;
using UnityEngine;
namespace QuietCamp.Presentation.UI
{
    [Serializable] public sealed class BakedNodeMeshes {public int index;public Mesh detail,completed,silhouette,low,lowCompleted;}
    [Serializable] public sealed class BakedBranchMeshes {public int key;public Mesh silhouette,detail;}
    /// <summary>Native offline output. Catalogs refer to resource names, never all campaign mesh objects.</summary>
    [PreferBinarySerialization]
    public sealed class RoadmapChunkAsset : ScriptableObject
    {
        public string catalogRevision,sourceHash,chunkId;public int index;public Mesh low,high;
        public BakedNodeMeshes[] nodes=Array.Empty<BakedNodeMeshes>();public BakedBranchMeshes[] branches=Array.Empty<BakedBranchMeshes>();
        public long estimatedBytes;
        public Mesh Node(int key,bool complete,bool revealed,bool lowQuality=false)
        {foreach(var node in nodes)if(node.index==key)return revealed?(lowQuality?(complete?(node.lowCompleted??node.completed):(node.low??node.detail)):(complete?node.completed:node.detail)):node.silhouette;return null;}
        public Mesh Branch(int key,bool revealed)
        {foreach(var branch in branches)if(branch.key==key)return revealed?branch.detail:branch.silhouette;return null;}
    }
}
