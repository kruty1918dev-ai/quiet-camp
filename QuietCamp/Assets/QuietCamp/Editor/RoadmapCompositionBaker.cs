using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Application;
using QuietCamp.Composition;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor
{
    public static class RoadmapCompositionBaker
    {
        public const string Source="Assets/QuietCamp/Authoring/Roadmap/Composition";
        public static string Hash(string text)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        public static T Read<T>(string path)=>JsonConvert.DeserializeObject<T>(File.ReadAllText(path),new JsonSerializerSettings{MissingMemberHandling=MissingMemberHandling.Error});
        public static string Reference(string key)
        {string name=(string)JObject.Parse(File.ReadAllText(Source+"/manifest.json"))[key];if(name==null||Path.GetFileName(name)!=name)throw new InvalidOperationException("Invalid composition reference: "+key);return Source+"/"+name;}
        public static Dictionary<string,SceneCompositionDocument> Documents()
        {
            var manifest=JObject.Parse(File.ReadAllText(Source+"/manifest.json"));if((string)manifest["compilerRevision"]!=SceneComposer.Revision)throw new InvalidOperationException("Composition compiler revision mismatch");var result=new Dictionary<string,SceneCompositionDocument>();
            foreach(string name in manifest["regions"].Values<string>())
            {if(Path.GetFileName(name)!=name)throw new InvalidOperationException("Composition path traversal");var doc=Read<SceneCompositionDocument>(Source+"/"+name);result.Add(doc.id,doc);}return result;
        }
        public static string SourceHash()
        {
            var sources=Directory.GetFiles(Source,"*.json").Where(f=>!f.EndsWith("bake-preview.json")).OrderBy(f=>f,StringComparer.Ordinal).Select(File.ReadAllText).ToList();
            foreach(string path in new[]{"roadmap_models.json","roadmap_culture_models.json","roadmap_story_models.json","level_summaries.json","atmosphere.json"})sources.Add(File.ReadAllText(RoadmapContentExporter.Folder+path));
            sources.Add(File.ReadAllText("Assets/QuietCamp/Authoring/Roadmap/Models/EnvironmentKit/models.json"));
            // Native output depends on adapter, shader and geometry code as well as recipes.
            foreach(string code in new[]{"Scripts/Application/RoadmapCompositionAdapter.cs","Scripts/Application/Composition/SceneComposition.cs","Scripts/Application/Composition/PowerDamage.cs","Scripts/Application/Composition/SurfaceRecipes.cs","Scripts/Application/Composition/ParcelBoundary.cs","Scripts/Application/RoadmapRuralLayout.cs","Scripts/Presentation/UI/RoadmapBranchArt.cs","Scripts/Infrastructure/AtmosphereCatalog.cs","Scripts/Presentation/World/SeasonProfile.cs","Scripts/Presentation/UI/RoadmapWorldRenderer.cs","Scripts/Presentation/UI/RoadmapWorldRenderer.Composition.cs","Scripts/Presentation/UI/RoadmapChunkAsset.cs","Scripts/Presentation/UI/RoadmapModelLibrary.cs","Scripts/Presentation/UI/RoadmapSceneGenerator.cs","Resources/QuietCamp/RoadmapWorld.shader"})sources.Add(File.ReadAllText("Assets/QuietCamp/"+code));
            sources.Add(File.ReadAllText("Assets/QuietCamp/Authoring/Roadmap/main.json"));
            sources.Add(File.ReadAllText("Assets/QuietCamp/Scripts/Application/RoadmapEnvironmentSampler.cs"));
            sources.Add(File.ReadAllText("Assets/QuietCamp/Scripts/Presentation/UI/RoadmapVisualProfile.cs"));
            sources.Add(File.ReadAllText("Assets/QuietCamp/Editor/RoadmapCompositionBaker.cs"));
            sources.Add(File.ReadAllText("Assets/QuietCamp/Scripts/Presentation/World/SeasonalTreeGeometry.cs"));
            sources.Add(File.ReadAllText("Assets/QuietCamp/Scripts/Presentation/World/SeasonPalette.cs"));
            sources.Add(File.ReadAllText("Assets/QuietCamp/Scripts/Application/Composition/PowerDamage.cs"));
            sources.Add(File.ReadAllText("Assets/QuietCamp/Scripts/Application/Composition/SurfaceRecipes.cs"));
            sources.Add(File.ReadAllText("Assets/QuietCamp/Scripts/Application/Composition/ParcelBoundary.cs"));
            sources.Add(SceneComposer.Revision);return Hash(string.Join("\n",sources));
        }
        public static RoadmapDefinition Definition(Dictionary<string,SceneCompositionDocument> docs)
        {
            var map=Read<RoadmapDefinition>("Assets/QuietCamp/Authoring/Roadmap/main.json");
            var summaries=Read<LevelSummary[]>(RoadmapContentExporter.Folder+"level_summaries.json");
            RoadmapCompiler.BakeAuthored(map,summaries);RoadmapCompositionAdapter.Bind(map,docs);
            foreach(var region in map.regions)
            {
                var chunks=new List<RoadmapChunkData>();for(int order=region.firstLevel;order<=region.lastLevel;order+=3)chunks.Add(new RoadmapChunkData{id=region.id+":composition-chunk:"+chunks.Count,firstOrder=order,lastOrder=Math.Min(order+2,region.lastLevel)});
                region.chunks=chunks.ToArray();region.performanceTier="high";
            }map.revision="semantic-world-"+SourceHash().Substring(0,12);map.compositionManifest=null;
            var errors=RoadmapValidator.Validate(map);if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors));return map;
        }
        public static void WorldPlans(RoadmapCatalog catalog,Dictionary<string,SceneCompositionDocument> source,out SceneCompositionDocument[] docs,out CompositionResult[] results)
        {
            var assets=Read<VisualAssetDefinition[]>(Reference("assets")).ToDictionary(a=>a.id);var templates=Read<EnsembleTemplate[]>(Reference("templates")).ToDictionary(t=>t.id);
            docs=new SceneCompositionDocument[catalog.Definition.regions.Length];results=new CompositionResult[docs.Length];
            for(int r=0;r<docs.Length;r++)
            {
                var local=JsonConvert.DeserializeObject<SceneCompositionDocument>(JsonConvert.SerializeObject(source[catalog.Definition.regions[r].id]));
                docs[r]=RoadmapCompositionAdapter.WorldDocument(local,catalog.RegionStarts[r]/RoadmapCompositionAdapter.Units);
            }
            results=SceneComposer.ComposeWorld(docs,assets,templates,new RoadmapCompositionAdapter.Terrain(catalog,docs));
            if(results.Any(r=>!r.Valid))throw new InvalidOperationException(JsonConvert.SerializeObject(results.SelectMany(r=>r.diagnostics),Formatting.Indented));
        }
        [MenuItem("Quiet Camp/Composition/Bake Main")]
        public static void BakeMain()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Bake Main outside Play Mode");
            var source=Documents();var definition=Definition(source);var summaries=Read<LevelSummary[]>(RoadmapContentExporter.Folder+"level_summaries.json");
            var host=new GameObject("Composition offline bake host");var harness=host.AddComponent<RoadmapBenchmarkHarness>();harness.autoRun=false;harness.definitionOverride=definition;harness.summariesOverride=summaries;harness.completedLevels=30;
            try {harness.Build();Bake(harness.Map,source);}
            finally {harness.Services?.Dispose();UnityEngine.Object.DestroyImmediate(host);}
        }
        public static void Bake(RoadmapGraphic map,Dictionary<string,SceneCompositionDocument> source)
        {
            var renderer=map.WorldRenderer;WorldPlans(map.Data,source,out var docs,out var results);renderer.SetOfflineComposition(docs,results);renderer.OfflineAssets=Read<VisualAssetDefinition[]>(Reference("assets")).ToDictionary(a=>a.id);
            string hash=SourceHash(),relative="QuietCamp/Composition/main",target=RoadmapContentExporter.Folder+"roadmap_regions.json",previous=File.ReadAllText(RoadmapContentExporter.Folder+"roadmap_regions.json");
            var bindings=new List<BakedChunkBinding>();var manifests=new List<object>();var created=new List<string>();var transient=new List<RoadmapChunkAsset>();bool published=false;long estimatedOutput=0;
            try
            {
                for(int c=0;c<map.Data.Chunks.Length;c++)
                {
                    string chunkHash=ChunkHash(map,c,docs,results),chunkRelative="QuietCamp/Composition/main/"+chunkHash.Substring(0,12);
                    string chunkFolder="Assets/QuietCamp/Resources/"+chunkRelative;Directory.CreateDirectory(chunkFolder);AssetDatabase.Refresh();
                    string path=chunkFolder+"/chunk-"+c+".asset";var existing=AssetDatabase.LoadAssetAtPath<RoadmapChunkAsset>(path);
                    if(existing!=null&&existing.sourceHash==chunkHash&&Complete(existing,map.Data.Chunks[c])){estimatedOutput+=existing.estimatedBytes;CheckDiskBudget(estimatedOutput);bindings.Add(new BakedChunkBinding{resourceId=chunkRelative+"/chunk-"+c,resourceIndex=c,sourceRevision=existing.catalogRevision});manifests.Add(new{chunk=c,cached=true,manifest=Manifest(existing,chunkRelative+"/chunk-"+c,map.Data.Chunks[c],docs)});continue;}
                    if(existing!=null)throw new InvalidOperationException("Incomplete/stale immutable asset: "+path);
                    var asset=ScriptableObject.CreateInstance<RoadmapChunkAsset>();transient.Add(asset);asset.index=c;asset.chunkId=map.Data.Chunks[c].Id;asset.catalogRevision=map.Data.Definition.revision;asset.sourceHash=chunkHash;
                    asset.low=renderer.BakePart(0,c,0);asset.high=renderer.BakePart(0,c,2);if(Equivalent(asset.low,asset.high)){UnityEngine.Object.DestroyImmediate(asset.low);asset.low=asset.high;}
                    var nodes=new List<BakedNodeMeshes>();var chunk=map.Data.Chunks[c];
                    for(int n=chunk.FirstNode;n<=chunk.LastNode;n++)
                    {
                        nodes.Add(BakeNode(renderer,n));
                        asset.nodes=nodes.ToArray(); // Retain partial ownership for failure cleanup.
                    }
                    asset.nodes=nodes.ToArray();var branches=new List<BakedBranchMeshes>();
                    for(int r=0;r<map.Data.Definition.regions.Length;r++)for(int b=0;b<map.Data.Definition.regions[r].branches.Length;b++)
                    {var branch=map.Data.Definition.regions[r].branches[b];if(map.Data.ChunkForNode(map.Data.NodeIndex(branch.anchorNodeId))==c){var meshesForBranch=new BakedBranchMeshes{key=r*4+b};branches.Add(meshesForBranch);asset.branches=branches.ToArray();meshesForBranch.silhouette=renderer.BakePart(2,r*4+b,2,false,0);meshesForBranch.detail=renderer.BakePart(2,r*4+b,2,false,1);}}
                    asset.branches=branches.ToArray();var meshes=Meshes(asset);asset.estimatedBytes=meshes.Sum(m=>(long)m.vertexCount*56+(long)m.GetIndexCount(0)*4);
                    if(asset.estimatedBytes>15L*1024*1024)throw new InvalidOperationException("Chunk exceeds conservative three-resident cache budget: "+c+" bytes="+asset.estimatedBytes);
                    estimatedOutput+=asset.estimatedBytes;CheckDiskBudget(estimatedOutput);AssetDatabase.CreateAsset(asset,path);created.Add(path);foreach(var mesh in meshes){AssetDatabase.AddObjectToAsset(mesh,asset);mesh.UploadMeshData(true);}EditorUtility.SetDirty(asset);
                    bindings.Add(new BakedChunkBinding{resourceId=chunkRelative+"/chunk-"+c,resourceIndex=c,sourceRevision=asset.catalogRevision});manifests.Add(new{chunk=c,cached=false,manifest=Manifest(asset,chunkRelative+"/chunk-"+c,chunk,docs),meshes=meshes.Count,vertices=meshes.Sum(m=>m.vertexCount)});
                }
                AssetDatabase.SaveAssets();if(SourceHash()!=hash)throw new InvalidOperationException("Source changed during bake; publication cancelled");
                map.Data.Definition.compositionManifest="QuietCamp/Composition/main";map.Data.Definition.bakedBindings=bindings.ToArray();
                foreach(var node in map.Data.Nodes)node.world.props=node.world.props.Where(p=>p.storyId!=null||p.assetId.Contains("campfire")).Concat(node.world.props.Where(p=>p.assetId.StartsWith("tree")).Take(2)).ToArray();
                // Publication pointer changes only after every native resource exists and validates.
                if(File.ReadAllText(target)!=previous)throw new InvalidOperationException("Published catalog changed during bake; publication cancelled");string temp=target+".tmp-"+Guid.NewGuid().ToString("N");
                try {File.WriteAllText(temp,JsonConvert.SerializeObject(map.Data.Definition,Formatting.Indented)+"\n");File.Replace(temp,target,null);AssetDatabase.ImportAsset(target);RoadmapRepository.Reset();published=true;}
                catch {File.WriteAllText(temp,previous);File.Replace(temp,target,null);AssetDatabase.ImportAsset(target);RoadmapRepository.Reset();throw;}
                finally{if(File.Exists(temp))File.Delete(temp);}
                string report=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../TestResults/roadmap-composition-2026-10-08"));Directory.CreateDirectory(report);
                File.WriteAllText(report+"/bake-manifest.json",JsonConvert.SerializeObject(new{hash,map.Data.Definition.revision,relative,chunks=manifests},Formatting.Indented));
                Debug.Log("Composition native bake published: "+relative+" ("+manifests.Count+" chunks)");
            }
            catch {if(!published)foreach(string path in created)AssetDatabase.DeleteAsset(path);throw;}
            finally {foreach(var asset in transient)if(asset!=null&&!EditorUtility.IsPersistent(asset)){foreach(var mesh in Meshes(asset))if(mesh!=null&&!EditorUtility.IsPersistent(mesh))UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(asset);}}
        }
        static BakedChunkManifest Manifest(RoadmapChunkAsset asset,string resource,RoadmapChunkRange chunk,SceneCompositionDocument[] docs)
        {
            var bounds=asset.high.bounds;foreach(var mesh in Meshes(asset))bounds.Encapsulate(mesh.bounds);return new BakedChunkManifest{id=asset.chunkId,sourceHash=asset.sourceHash,resourceId=resource,estimatedBytes=asset.estimatedBytes,lod=new[]{"low","high"},dependencies=docs.Where(d=>d.zones.Any(z=>(z.z+z.depth*.5f)*RoadmapCompositionAdapter.Units>=chunk.Top&&(z.z-z.depth*.5f)*RoadmapCompositionAdapter.Units<chunk.Bottom)).Select(d=>d.id).ToArray(),revealBindings=asset.nodes.Select(n=>"node-index:"+n.index).Concat(asset.branches.Select(b=>"branch-key:"+b.key)).ToArray(),bounds=new BakedBounds{min=new AssetSocket{x=bounds.min.x,y=bounds.min.y,z=bounds.min.z},max=new AssetSocket{x=bounds.max.x,y=bounds.max.y,z=bounds.max.z}}};
        }
        static string ChunkHash(RoadmapGraphic map,int index,SceneCompositionDocument[] docs,CompositionResult[] results)
        {
            var chunk=map.Data.Chunks[index];float top=chunk.Top/RoadmapCompositionAdapter.Units,bottom=chunk.Bottom/RoadmapCompositionAdapter.Units;
            var nodeIds=new HashSet<string>();for(int n=chunk.FirstNode;n<=chunk.LastNode;n++)nodeIds.Add(map.Data.Nodes[n].id);
            var instances=results.SelectMany(r=>r.instances).Where(p=>nodeIds.Contains(p.revealOwner??"")||p.z>=top-30&&p.z<bottom+30).ToArray();
            var spans=results.SelectMany(r=>r.spans).Where(p=>p.height==0?instances.Any(i=>i.id==p.a):Math.Max(p.az,p.bz)>=top&&Math.Min(p.az,p.bz)<bottom).ToArray();
            var assets=new HashSet<string>(instances.Select(p=>p.asset));
            foreach(var id in new[]{"tree_default","tree_pineRoundA","grass_leafsLarge","flower_yellowA","ua_wheat_patch_lod","ua_sunflower_patch_lod","tent_smallOpen"})assets.Add(id);
            var nodes=Enumerable.Range(chunk.FirstNode,chunk.LastNode-chunk.FirstNode+1).Select(n=>map.Data.Nodes[n]).ToArray();foreach(var n in nodes)foreach(var p in n.world.props)assets.Add(p.assetId);
            var nearby=map.Data.Definition.regions.Skip(Math.Max(0,chunk.Region-1)).Take(chunk.Region==0?2:3).ToArray();foreach(var region in nearby){foreach(var p in region.storyProps)assets.Add(p.assetId);if(region.revealRules.farLandmarkAssetId!=null)assets.Add(region.revealRules.farLandmarkAssetId);foreach(var b in region.branches){foreach(var p in b.world.props)assets.Add(p.assetId);foreach(var n in b.nodes)foreach(var p in n.world.props)assets.Add(p.assetId);}}
            // Field cover is generated from zones, not emitted as planner instances.
            foreach(var zone in docs.SelectMany(d=>d.zones).Where(z=>z.kind=="field"&&z.z+z.depth*.5f>=top&&z.z-z.depth*.5f<bottom))if(zone.species!=null)assets.Add(zone.species);
            foreach(var id in assets.ToArray())if(map.WorldRenderer.OfflineAssets.TryGetValue(id,out var metadata)&&metadata.lod!=null)assets.Add(metadata.lod);
            var models=new List<JToken>();foreach(var file in new[]{"roadmap_models.json","roadmap_culture_models.json","roadmap_story_models.json"})foreach(var model in JArray.Parse(File.ReadAllText(RoadmapContentExporter.Folder+file)))if(assets.Contains((string)model["id"]))models.Add(model);
            foreach(var model in JArray.Parse(File.ReadAllText("Assets/QuietCamp/Authoring/Roadmap/Models/EnvironmentKit/models.json")))if(assets.Contains((string)model["id"]))models.Add(model);
            var code=Directory.GetFiles("Assets/QuietCamp/Scripts/Presentation/UI","RoadmapWorldRenderer*.cs").OrderBy(f=>f).Select(File.ReadAllText).ToList();foreach(var file in new[]{"Scripts/Application/RoadmapCompositionAdapter.cs","Scripts/Application/Composition/SceneComposition.cs","Scripts/Application/Composition/PowerDamage.cs","Scripts/Application/Composition/SurfaceRecipes.cs","Scripts/Application/Composition/ParcelBoundary.cs","Scripts/Application/RoadmapRuralLayout.cs","Scripts/Presentation/UI/RoadmapBranchArt.cs","Scripts/Infrastructure/AtmosphereCatalog.cs","Scripts/Presentation/World/SeasonProfile.cs","Scripts/Presentation/UI/RoadmapChunkAsset.cs","Scripts/Presentation/UI/RoadmapModelLibrary.cs","Scripts/Presentation/UI/RoadmapSceneGenerator.cs","Scripts/Presentation/UI/RoadmapVisualProfile.cs","Scripts/Application/RoadmapEnvironmentSampler.cs","Scripts/Presentation/World/SeasonalTreeGeometry.cs","Scripts/Presentation/World/SeasonPalette.cs","Resources/QuietCamp/RoadmapWorld.shader"})code.Add(File.ReadAllText("Assets/QuietCamp/"+file));
            code.Add(File.ReadAllText("Assets/QuietCamp/Editor/RoadmapCompositionBaker.cs"));
            return Hash(JsonConvert.SerializeObject(new{chunk,instances,spans,surfaces=docs.SelectMany((d,i)=>SurfaceRecipes.Resolve(d,results[i])).Where(s=>s.points.Max(p=>p.z)>=top&&s.points.Min(p=>p.z)<bottom),routes=docs.SelectMany(d=>d.routes).Select(r=>new{r.id,r.kind,r.width,r.interpolation,r.nextRoute,segments=r.points.Zip(r.points.Skip(1),(a,b)=>new{a,b}).Where(s=>Math.Max(s.a.z,s.b.z)>=top-2&&Math.Min(s.a.z,s.b.z)<bottom+2).ToArray()}).Where(r=>r.segments.Length>0),zones=docs.SelectMany(d=>d.zones).Where(z=>z.z+z.depth*.5f>=top&&z.z-z.depth*.5f<bottom),nodes,anchors=map.Data.Nodes.Skip(Math.Max(0,chunk.FirstNode-2)).Take(chunk.LastNode-chunk.FirstNode+5).Select(n=>new{n.id,n.x,y=map.Data.Y(map.Data.NodeIndex(n.id))}),assetMetadata=map.WorldRenderer.OfflineAssets.Values.Where(a=>assets.Contains(a.id)),seeds=docs.Where(d=>d.zones.Any(z=>z.z+z.depth*.5f>=top&&z.z-z.depth*.5f<bottom)).Select(d=>new{d.id,d.seed}),environment=nearby.Select(r=>new{r.id,r.biome,r.season,r.environmentPhase,r.lighting,r.weatherBias,r.transition,r.revealRules,r.storyProps,r.branches}),summaries=Enumerable.Range(chunk.FirstNode,chunk.LastNode-chunk.FirstNode+1).Select(map.Summary),models,atmosphere=File.ReadAllText(RoadmapContentExporter.Folder+"atmosphere.json"),code,SceneComposer.Revision}));
        }
        static void CheckDiskBudget(long estimated)
        {
            var drive=new DriveInfo(Path.GetPathRoot(Path.GetFullPath(Source)));
            // Binary serialization and a conservative margin bound this job below 200 MiB on a full host.
            if(estimated>160L*1024*1024&&(double)drive.AvailableFreeSpace/drive.TotalSize<.1)throw new IOException("Host has less than 10% free disk; native output would exceed the 200 MiB write allowance. Existing catalog retained.");
        }
        public static BakedNodeMeshes BakeNode(RoadmapWorldRenderer renderer,int n)
        {
            var created=new List<Mesh>(5);
            Mesh Build(int tier,bool completed=false){var mesh=renderer.BakePart(1,n,tier,completed);created.Add(mesh);return mesh;}
            Mesh Reuse(Mesh mesh,params Mesh[] choices){foreach(var choice in choices)if(Equivalent(mesh,choice)){UnityEngine.Object.DestroyImmediate(mesh);return choice;}return mesh;}
            try
            {
                var detail=Build(2);var completed=Reuse(Build(2,true),detail);
                var low=Reuse(Build(0),detail,completed);var lowCompleted=Reuse(Build(0,true),low,completed,detail);
                var silhouette=renderer.BakeSilhouette(n);created.Add(silhouette);
                return new BakedNodeMeshes{index=n,detail=detail,completed=completed,low=low,lowCompleted=lowCompleted,silhouette=silhouette};
            }
            catch{foreach(var mesh in created)if(mesh!=null&&!EditorUtility.IsPersistent(mesh))UnityEngine.Object.DestroyImmediate(mesh);throw;}
        }
        public static bool Equivalent(Mesh a,Mesh b)=>a.vertexCount==b.vertexCount&&a.GetIndexCount(0)==b.GetIndexCount(0)&&a.vertices.SequenceEqual(b.vertices)&&a.normals.SequenceEqual(b.normals)&&a.colors.SequenceEqual(b.colors)&&a.uv.SequenceEqual(b.uv)&&a.uv2.SequenceEqual(b.uv2)&&a.triangles.SequenceEqual(b.triangles);
        static bool Complete(RoadmapChunkAsset asset,RoadmapChunkRange chunk)
        {
            if(asset.low==null||asset.high==null||asset.estimatedBytes<=0||asset.estimatedBytes>15L*1024*1024)return false;
            for(int i=chunk.FirstNode;i<=chunk.LastNode;i++)if(asset.Node(i,false,true)==null||asset.Node(i,true,true)==null||asset.Node(i,false,false)==null)return false;
            return asset.branches.All(b=>b.detail!=null&&b.silhouette!=null);
        }
        static HashSet<Mesh> Meshes(RoadmapChunkAsset a)
        {
            var result=new HashSet<Mesh>{a.low,a.high};foreach(var n in a.nodes){result.Add(n.detail);result.Add(n.completed);result.Add(n.silhouette);result.Add(n.low);result.Add(n.lowCompleted);}result.Remove(null);foreach(var b in a.branches){result.Add(b.detail);result.Add(b.silhouette);}return result;
        }
    }
}
