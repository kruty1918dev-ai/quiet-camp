using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Application;
using QuietCamp.Composition;
using QuietCamp.Domain;
using QuietCamp.Presentation.UI;
using UnityEditor;
using UnityEngine;
namespace QuietCamp.Editor
{
    /// <summary>Anonymous fictional compound landmarks, outside Resources and outside the campaign.</summary>
    public static class RoadmapStagingCompositionBaker
    {
        public const string Folder="Assets/QuietCamp/Tests/Fixtures/CompositionStaging";
        public static string Hash(string name)=>RoadmapCompositionBaker.Hash(File.ReadAllText(RoadmapCompositionBaker.Source+"/Staging/"+name+".json")+File.ReadAllText("Assets/QuietCamp/Authoring/Roadmap/Models/StagingLandmarks/models.json")+File.ReadAllText(RoadmapCompositionBaker.Source+"/Staging/assets.json")+File.ReadAllText("Assets/QuietCamp/Editor/RoadmapStagingCompositionBaker.cs")+RoadmapCompositionBaker.SourceHash());
        public static string NativeFolder(string name)=>Folder+"/"+name+"/"+Hash(name).Substring(0,12);
        public static RoadmapCatalog Catalog(string name,out SceneCompositionDocument[] docs,out CompositionResult[] results,out Dictionary<string,VisualAssetDefinition> assets)
        {
            if(name!="aircraft"&&name!="ship"&&name!="dam")throw new ArgumentException("Unknown staging composition");
            var source=RoadmapCompositionBaker.Documents();var definition=RoadmapCompositionBaker.Definition(source);
            var region=definition.regions[0];region.branches=Array.Empty<RoadmapBranchData>();definition.regions=new[]{region};definition.revision="staging-"+name;
            docs=new[]{RoadmapCompositionBaker.Read<SceneCompositionDocument>(RoadmapCompositionBaker.Source+"/Staging/"+name+".json")};
            region.id=docs[0].id;region.season=docs[0].season;
            RoadmapCompositionAdapter.Bind(definition,new Dictionary<string,SceneCompositionDocument>{{docs[0].id,docs[0]}});
            // Staging geography has open field/river nodes, not populated camp vignettes.
            foreach(var node in region.nodePositions)node.world.props=node.world.props.Where(p=>p.assetId.StartsWith("grass")||p.assetId.StartsWith("stone")).ToArray();
            var catalog=new RoadmapCatalog(definition);
            assets=RoadmapCompositionBaker.Read<VisualAssetDefinition[]>(RoadmapCompositionBaker.Reference("assets")).Concat(RoadmapCompositionBaker.Read<VisualAssetDefinition[]>(RoadmapCompositionBaker.Source+"/Staging/assets.json")).ToDictionary(a=>a.id);
            var templates=RoadmapCompositionBaker.Read<EnsembleTemplate[]>(RoadmapCompositionBaker.Reference("templates")).ToDictionary(a=>a.id);
            results=new[]{SceneComposer.Compose(docs[0],assets,templates)};if(!results[0].Valid)throw new InvalidOperationException(JsonConvert.SerializeObject(results[0].diagnostics));return catalog;
        }
        [MenuItem("Quiet Camp/Composition/Bake Staging Landmarks")]
        public static void BakeBoth(){Bake("aircraft");Bake("ship");Bake("dam");}
        public static void Bake(string name)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Staging bake outside Play Mode");
            var catalog=Catalog(name,out var docs,out var results,out var assets);var host=new GameObject("Offline staging "+name);var harness=host.AddComponent<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.definitionOverride=catalog.Definition;harness.summariesOverride=RoadmapCompositionBaker.Read<LevelSummary[]>(RoadmapContentExporter.Folder+"level_summaries.json").Take(5).ToArray();harness.completedLevels=5;
            string hash=Hash(name),folder=NativeFolder(name);Directory.CreateDirectory(folder);AssetDatabase.Refresh();var staged=new List<string>();var transient=new List<RoadmapChunkAsset>();
            try
            {
                RoadmapModelLibrary.Load().PrepareStaging();harness.Build();var renderer=harness.Map.WorldRenderer;renderer.SetOfflineComposition(docs,results);renderer.OfflineAssets=assets;
                for(int c=0;c<catalog.Chunks.Length;c++)
                {
                    var cached=AssetDatabase.LoadAssetAtPath<RoadmapChunkAsset>(folder+"/chunk-"+c+".asset");if(cached!=null){if(cached.sourceHash!=hash||cached.low==null||cached.high==null||cached.nodes.Any(n=>n.detail==null||n.completed==null||n.silhouette==null))throw new InvalidOperationException("Incomplete immutable staging chunk: "+c);continue;}
                    var a=ScriptableObject.CreateInstance<RoadmapChunkAsset>();transient.Add(a);a.index=c;a.catalogRevision=catalog.Definition.revision;a.sourceHash=hash;a.chunkId=catalog.Chunks[c].Id;a.low=renderer.BakePart(0,c,0);a.high=renderer.BakePart(0,c,2);
                    var nodes=new List<BakedNodeMeshes>();for(int n=catalog.Chunks[c].FirstNode;n<=catalog.Chunks[c].LastNode;n++){nodes.Add(RoadmapCompositionBaker.BakeNode(renderer,n));a.nodes=nodes.ToArray();}
                    var meshes=new HashSet<Mesh>{a.low,a.high};foreach(var n in a.nodes){meshes.Add(n.detail);meshes.Add(n.completed);meshes.Add(n.silhouette);meshes.Add(n.low);meshes.Add(n.lowCompleted);}a.estimatedBytes=meshes.Sum(m=>(long)m.vertexCount*56+(long)m.GetIndexCount(0)*4);
                    string path=folder+"/chunk-"+c+".pending.asset";if(File.Exists(path))throw new InvalidOperationException("Unconsumed staging bake: "+path);AssetDatabase.CreateAsset(a,path);staged.Add(path);foreach(var m in meshes){AssetDatabase.AddObjectToAsset(m,a);m.UploadMeshData(true);}
                }
                AssetDatabase.SaveAssets();if(Hash(name)!=hash)throw new InvalidOperationException("Staging source changed during bake");
                for(int i=0;i<staged.Count;i++){string path=staged[i],target=path.Replace(".pending.asset",".asset");if(File.Exists(target))throw new InvalidOperationException("Immutable staging resource already exists: "+target);string error=AssetDatabase.MoveAsset(path,target);if(error!="")throw new InvalidOperationException(error);staged[i]=target;}
                // The preview considers a staging revision published only when this commit marker exists.
                File.WriteAllText(folder+"/bake.json",JsonConvert.SerializeObject(new{hash,chunks=catalog.Chunks.Length},Formatting.Indented)+"\n");staged.Clear();
            }
            finally {foreach(var path in staged)AssetDatabase.DeleteAsset(path);foreach(var a in transient)if(a!=null&&!EditorUtility.IsPersistent(a)){var meshes=new HashSet<Mesh>{a.low,a.high};foreach(var n in a.nodes){meshes.Add(n.detail);meshes.Add(n.completed);meshes.Add(n.silhouette);meshes.Add(n.low);meshes.Add(n.lowCompleted);}foreach(var m in meshes)if(m!=null&&!EditorUtility.IsPersistent(m))UnityEngine.Object.DestroyImmediate(m);UnityEngine.Object.DestroyImmediate(a);}harness.Services?.Dispose();UnityEngine.Object.DestroyImmediate(host);}
        }
    }
}
