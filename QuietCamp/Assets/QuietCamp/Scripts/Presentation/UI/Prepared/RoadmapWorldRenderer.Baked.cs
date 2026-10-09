using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace QuietCamp.Presentation.UI.Prepared
{
    public sealed partial class RoadmapWorldRenderer
    {
        readonly List<int> _evictions=new List<int>(3);
        readonly Dictionary<int,RoadmapChunkAsset> _baked=new Dictionary<int,RoadmapChunkAsset>(3);
        public bool UsesBaked=>_map!=null&&!string.IsNullOrEmpty(_map.Data.Definition.compositionManifest);
        public int ResidentChunks=>_baked.Count;
        public int ProceduralBuilds {get;private set;}
        public long CacheBytes {get;private set;}
        public long TextureBytes=>_texture!=null?(long)_texture.width*_texture.height*8:0;
        public long PresentationBytes=>CacheBytes+TextureBytes+1024*1024; // fixed-pool/asset metadata reserve; measured engine memory remains separate.
        public int AssetActivations {get;private set;}
        public float WorstActivationMs {get;private set;}
        public string StreamFault {get;private set;}
        int _faultFirst=-1,_faultLast=-1;
        bool CanLoadBaked=>StreamFault==null||_faultFirst!=_map.FirstActiveChunk||_faultLast!=_map.LastActiveChunk;
        void Fault(string message){StreamFault=message;_faultFirst=_map.FirstActiveChunk;_faultLast=_map.LastActiveChunk;Debug.LogError(message);}
        sealed class ChunkLease
        {public string key;public ResourceRequest request;public RoadmapChunkAsset asset;public int waiters,references;public bool released;}
        static readonly Dictionary<string,ChunkLease> SharedLeases=new Dictionary<string,ChunkLease>();
        readonly Dictionary<int,ChunkLease> _chunkLeases=new Dictionary<int,ChunkLease>(3);
        ChunkLease _inFlight;
        public int PendingNativeLoads=>_inFlight==null?0:1;
        public static int SharedNativeLeases=>SharedLeases.Count;
        static ChunkLease BeginLease(string key)
        {
            if(!SharedLeases.TryGetValue(key,out var lease)){lease=new ChunkLease{key=key,request=Resources.LoadAsync<RoadmapChunkAsset>(key)};SharedLeases.Add(key,lease);}lease.waiters++;return lease;
        }
        static RoadmapChunkAsset CompleteLease(ChunkLease lease)
        {
            lease.waiters--;lease.asset=lease.request.asset as RoadmapChunkAsset;
            if(lease.asset==null){ReleaseUnused(lease);return null;}lease.references++;return lease.asset;
        }
        static void ReleaseLease(ChunkLease lease){lease.references--;ReleaseUnused(lease);}
        static void AbandonLease(ChunkLease lease){lease.waiters--;ReleaseUnused(lease);}
        static void ReleaseUnused(ChunkLease lease)
        {
            if(lease.released||lease.waiters>0||lease.references>0)return;lease.released=true;SharedLeases.Remove(lease.key);
            var asset=lease.asset!=null?lease.asset:lease.request.asset as RoadmapChunkAsset;if(asset!=null)Release(asset);
        }
        Domain.BakedChunkBinding Binding(int chunk)=>_map.Data.Definition.bakedBindings.Length==0?null:_map.Data.Definition.bakedBindings[chunk];
        int PartChunk(Slot[] pool,int key)=>pool==_chunks?key:pool==_glades?_map.Data.ChunkForNode(key):_map.Data.ChunkForNode(_map.Data.NodeIndex(_map.Data.Definition.regions[key/4].branches[key%4].anchorNodeId));
        Vector3 BakedPosition(Slot[] pool,int key)
        {if(key<0)return Vector3.zero;var b=Binding(PartChunk(pool,key));return b==null?Vector3.zero:new Vector3(0,0,-b.logicalOffset/Application.RoadmapCompositionAdapter.Units);}
        const long CacheBudget=64L*1024*1024;
        IEnumerator LoadBaked()
        {
            yield return null; // Never complete synchronously before the owning coroutine handle is assigned.
            StreamFault=null;
            // Only async native asset loading/assignment. No model JSON, geometry builder or solver.
            for(int c=_map.FirstActiveChunk;c<=_map.LastActiveChunk;c++)
            {
                if(c<0||_baked.ContainsKey(c))continue;
                var binding=Binding(c);
                var lease=_inFlight=BeginLease(binding?.resourceId??(_map.Data.Definition.compositionManifest+"/chunk-"+(binding?.resourceIndex??c)));
                yield return lease.request;_inFlight=null;
                var asset=CompleteLease(lease);
                if(asset==null||asset.catalogRevision!=(binding?.sourceRevision??_map.Data.Definition.revision))
                {Fault("Missing/stale baked roadmap chunk "+c);if(asset!=null)ReleaseLease(lease);yield return null;continue;}
                if(c<_map.FirstActiveChunk||c>_map.LastActiveChunk){ReleaseLease(lease);continue;}
                TrimBaked();
                bool shared=_baked.ContainsValue(asset);
                if(PresentationBytes+(shared?0:asset.estimatedBytes)>CacheBudget){Fault("Roadmap presentation cache exceeds 64 MiB; rebake within budget.");ReleaseLease(lease);continue;}
                _baked.Add(c,asset);_chunkLeases.Add(c,lease);if(!shared)CacheBytes+=asset.estimatedBytes;
                yield return null;
            }
            foreach(var pool in _pools)foreach(var slot in pool)
            {
                if(!slot.Wanted)continue;
                int chunk=PartChunk(pool,slot.Key);
                if(!_baked.TryGetValue(chunk,out var asset))continue;
                if(slot.Ready)continue;
                var binding=Binding(chunk);var start=System.Diagnostics.Stopwatch.GetTimestamp();Mesh selected;
                if(pool==_chunks)selected=_map.QualityTier==2?asset.high:asset.low;
                else if(pool==_glades)selected=asset.Node(slot.Key-(binding?.nodeOffset??0),_map.Data.State(slot.Key,_map.Progress)==Domain.RoadmapNodeState.Completed,_map.Reveal(slot.Key)==Domain.RoadmapReveal.Revealed,_map.QualityTier<2);
                else
                {
                    var branch=_map.Data.Definition.regions[slot.Key/4].branches[slot.Key%4];
                    bool detailed=branch.nodes.Length==0?_map.Data.BranchAvailable(branch,_map.Progress):_map.RevealState.BranchNode(branch,0)==Domain.RoadmapReveal.Revealed;
                    selected=asset.Branch(slot.Key-(binding?.branchKeyOffset??0),detailed);
                }
                if(selected==null){Fault("Baked roadmap part missing: "+slot.Key);continue;}
                slot.BakedMesh=selected;slot.Ready=true;if(pool==_glades)slot.StoryCompleted=_map.Progress.IsCompleted(_map.Data.Nodes[slot.Key].levelId);slot.Renderer.shadowCastingMode=pool==_glades&&_map.Reveal(slot.Key)!=Domain.RoadmapReveal.Revealed?UnityEngine.Rendering.ShadowCastingMode.Off:UnityEngine.Rendering.ShadowCastingMode.On;
                slot.Filter.sharedMesh=selected;slot.Renderer.transform.position=BakedPosition(pool,slot.Key);AssetActivations++;
                WorstActivationMs=Mathf.Max(WorstActivationMs,(float)((System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000d/System.Diagnostics.Stopwatch.Frequency));
                yield return null;
            }
            TrimBaked();_builder=null;
        }
        void TrimBaked()
        {
            // Release slot references first. Resource assets are never Destroy()ed like dynamic meshes.
            var remove=_evictions;remove.Clear();
            foreach(var p in _baked)if(p.Key<_map.FirstActiveChunk||p.Key>_map.LastActiveChunk)remove.Add(p.Key);
            foreach(int key in remove)
            {
                var asset=_baked[key];foreach(var pool in _pools)foreach(var slot in pool)
                {if(slot.BakedMesh==null)continue;if(Contains(asset,slot.BakedMesh)){slot.Filter.sharedMesh=null;slot.BakedMesh=null;slot.Ready=false;slot.Renderer.gameObject.SetActive(false);}}
                _baked.Remove(key);if(!_baked.ContainsValue(asset))CacheBytes-=asset.estimatedBytes;ReleaseLease(_chunkLeases[key]);_chunkLeases.Remove(key);
            }
        }
        static bool Contains(RoadmapChunkAsset asset,Mesh mesh)
        {
            if(asset.low==mesh||asset.high==mesh)return true;
            foreach(var n in asset.nodes)if(n.detail==mesh||n.completed==mesh||n.silhouette==mesh||n.low==mesh||n.lowCompleted==mesh)return true;
            foreach(var b in asset.branches)if(b.detail==mesh||b.silhouette==mesh)return true;return false;
        }
        static void Release(RoadmapChunkAsset asset)
        {
            // Each asset owns its mesh subassets. Shared palette/materials are not owned here.
            var meshes=new HashSet<Mesh>();if(asset.low!=null)meshes.Add(asset.low);if(asset.high!=null)meshes.Add(asset.high);
            foreach(var n in asset.nodes){if(n.detail!=null)meshes.Add(n.detail);if(n.completed!=null)meshes.Add(n.completed);if(n.silhouette!=null)meshes.Add(n.silhouette);if(n.low!=null)meshes.Add(n.low);if(n.lowCompleted!=null)meshes.Add(n.lowCompleted);}
            foreach(var b in asset.branches){if(b.detail!=null)meshes.Add(b.detail);if(b.silhouette!=null)meshes.Add(b.silhouette);}
            foreach(var mesh in meshes)Resources.UnloadAsset(mesh);Resources.UnloadAsset(asset);
        }
        void CancelBakedRequest()
        {
            var lease=_inFlight;_inFlight=null;if(lease==null)return;
            // A canceled waiter cannot unload a resource that a new entry is still awaiting.
            if(lease.request.isDone)AbandonLease(lease);else lease.request.completed+=operation=>AbandonLease(lease);
        }
        void ReleaseBaked()
        {
            foreach(var pool in _pools??Array.Empty<Slot[]>())foreach(var slot in pool)if(slot.BakedMesh!=null){slot.Filter.sharedMesh=null;slot.BakedMesh=null;slot.Ready=false;}
            foreach(var lease in _chunkLeases.Values)ReleaseLease(lease);_chunkLeases.Clear();_baked.Clear();CacheBytes=0;StreamFault=null;_faultFirst=_faultLast=-1;
        }
    }
}
