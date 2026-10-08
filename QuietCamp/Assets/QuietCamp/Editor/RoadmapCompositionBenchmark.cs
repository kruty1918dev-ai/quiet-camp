using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEditor;
namespace QuietCamp.Editor
{
    /// <summary>360 logical nodes and 60 regions, reusing measured native assets. No production progress or content.</summary>
    public static class RoadmapCompositionBenchmark
    {
        public sealed class Dataset {public RoadmapDefinition definition;public LevelSummary[] summaries;}
        [MenuItem("Quiet Camp/Composition/Export 360 Benchmark")]
        public static void Export()
        {
            var data=Create();string folder="Assets/QuietCamp/Tests/Fixtures/Roadmap/";
            File.WriteAllText(folder+"composition-360.json",JsonConvert.SerializeObject(data.definition,Formatting.Indented)+"\n");
            File.WriteAllText(folder+"composition-360-summaries.json",JsonConvert.SerializeObject(data.summaries,Formatting.Indented)+"\n");AssetDatabase.Refresh();
        }
        static T Copy<T>(T value)=>JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value));
        public static Dataset Create()
        {
            var source=RoadmapRepository.Main;var original=RoadmapCompositionBaker.Read<LevelSummary[]>(RoadmapContentExporter.Folder+"level_summaries.json");
            if(string.IsNullOrEmpty(source.Definition.compositionManifest))throw new InvalidOperationException("Bake native Main first");
            var regions=new List<RoadmapRegionData>();var summaries=new List<LevelSummary>();var bindings=new List<BakedChunkBinding>();
            for(int cycle=0;cycle<12;cycle++)
            {
                int nodeOffset=cycle*source.Nodes.Length,regionOffset=cycle*source.Definition.regions.Length;
                string Id(string id)=>id==null?null:"bench:"+cycle+":"+id;
                foreach(var originalRegion in source.Definition.regions)
                {
                    var region=Copy(originalRegion);region.id=Id(region.id);region.firstLevel+=nodeOffset;region.lastLevel+=nodeOffset;
                    foreach(var node in region.nodePositions)
                    {
                        var index=source.NodeIndex(node.id);node.id=Id(node.id);node.levelId=Id(node.levelId);node.order+=nodeOffset;node.branchLinks=node.branchLinks.Select(Id).ToArray();
                        node.requires=node.order==1?Array.Empty<string>():new[]{"bench:"+((node.order-2)/source.Nodes.Length)+":"+source.Nodes[(node.order-2)%source.Nodes.Length].levelId};
                        var summary=Copy(original.First(s=>s.id==source.Nodes[index].levelId));summary.id=node.levelId;summaries.Add(summary);
                    }
                    foreach(var chunk in region.chunks){chunk.id=Id(chunk.id);chunk.firstOrder+=nodeOffset;chunk.lastOrder+=nodeOffset;}
                    foreach(var branch in region.branches)
                    {branch.id=Id(branch.id);branch.anchorNodeId=Id(branch.anchorNodeId);branch.requires=branch.requires.Select(Id).ToArray();if(branch.targetRegionId!=null)branch.targetRegionId=Id(branch.targetRegionId);foreach(var node in branch.nodes){node.id=Id(node.id);node.levelId=Id(node.levelId);}}
                    regions.Add(region);
                }
                for(int c=0;c<source.Chunks.Length;c++)bindings.Add(new BakedChunkBinding{resourceIndex=c,nodeOffset=nodeOffset,branchKeyOffset=regionOffset*4,logicalOffset=cycle*source.Height,sourceRevision=source.Definition.bakedBindings.Length>0?source.Definition.bakedBindings[c].sourceRevision:source.Definition.revision,resourceId=source.Definition.bakedBindings.Length>0?source.Definition.bakedBindings[c].resourceId:null});
            }
            var definition=new RoadmapDefinition{revision=source.Definition.revision+"-benchmark-360",presentation="world3d",compositionManifest=source.Definition.compositionManifest,regions=regions.ToArray(),bakedBindings=bindings.ToArray()};
            _=new RoadmapCatalog(definition);return new Dataset{definition=definition,summaries=summaries.ToArray()};
        }
    }
}
