
using QuietCamp.Presentation.UI.Prepared;
using RoadmapGladeGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGladeGraphic;
using RoadmapGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGraphic;
using RoadmapLayout = QuietCamp.Presentation.UI.Prepared.RoadmapLayout;
using RoadmapSceneGenerator = QuietCamp.Presentation.UI.Prepared.RoadmapSceneGenerator;
using RoadmapWeatherGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapWeatherGraphic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor
{
    /// <summary>Bakes presentation data, never regenerates or solves a level.
    /// Readable source meshes stay Editor-only; players consume the small baked library.</summary>
    public static class RoadmapContentExporter
    {
        public const string Folder="Assets/QuietCamp/Resources/QuietCamp/";
        [MenuItem("Quiet Camp/Content/Refresh procedural roadmap")]
        public static void Export()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Export roadmap authoring outside Play Mode");
            if(File.Exists(Folder+"roadmap_regions.json")&&!string.IsNullOrEmpty(JsonConvert.DeserializeObject<RoadmapDefinition>(File.ReadAllText(Folder+"roadmap_regions.json")).compositionManifest))throw new InvalidOperationException("Native composition is published: use Quiet Camp/Composition/Bake Main. Legacy export cannot overwrite the catalog.");
            var outputs=new Dictionary<string,string>();var maps=new List<RoadmapDefinition>();
            var summaries=new List<LevelSummary>();int index=0;
            foreach(var id in LevelLoader.MvpLevelIds())
            {
                if(GeneratedCampSource.IsGeneratedId(id)&&Resources.Load<TextAsset>("QuietCamp/GeneratedLevels/"+id.Replace(':','_'))==null)
                    throw new InvalidOperationException("Freeze the level before exporting its map: "+id);
                var level=LevelLoader.Load(id);
                summaries.Add(new LevelSummary { id=id,number=++index,width=level.width,height=level.height,
                    decorSeed=level.decorSeed,lighting=level.lighting,environmentPreset=level.environmentPreset,contentHash=level.contentHash,ruleVersion=level.ruleVersion,noise=level.noise,
                    entry=level.entry,accessPoints=level.accessPoints,mapObjects=level.objects,canopies=level.canopies,
                    exteriorWalkable=level.exteriorWalkable,environment=level.environment,environmentalStory=level.environmentalStory,
                    shade=level.guests.Any(g=>g.shade),quiet=level.guests.Any(g=>g.quiet),
                    friends=level.friends.Length>0,fire=level.objects.Any(p=>p.assetId.Contains("campfire")) });
            }
            outputs[Folder+"level_summaries.json"]=JsonConvert.SerializeObject(summaries,Formatting.Indented);
            var source="Assets/QuietCamp/Authoring/Roadmap/main.json";
            var regions=File.Exists(source)?QuietCamp.Application.RoadmapCompiler.BakeAuthored(JsonConvert.DeserializeObject<RoadmapDefinition>(File.ReadAllText(source)),summaries)
                :QuietCamp.Application.RoadmapCompiler.BuildWorld(summaries,BonusCampCatalog.Slots);
            var regionIssues=QuietCamp.Application.RoadmapValidator.Validate(regions);
            QuietCamp.Application.RoadmapCompositionAdapter.RequireCampaignCoverage(regions,LevelLoader.MvpLevelIds());
            if(regionIssues.Count>0)throw new InvalidOperationException(string.Join("; ",regionIssues));
            outputs[Folder+"roadmap_regions.json"]=JsonConvert.SerializeObject(regions,Formatting.Indented);maps.Add(regions);
            string sliceSource="Assets/QuietCamp/Authoring/Roadmap/foundation-slice.json";
            if(File.Exists(sliceSource))
            {
                var slice=JsonConvert.DeserializeObject<RoadmapDefinition>(File.ReadAllText(sliceSource));
                var ids=new HashSet<string>(slice.regions.SelectMany(r=>r.nodePositions).Select(n=>n.levelId));
                QuietCamp.Application.RoadmapCompiler.BakeAuthored(slice,summaries.Where(s=>ids.Contains(s.id)).ToArray());
                outputs[Folder+"Roadmaps/foundation_slice.json"]=JsonConvert.SerializeObject(slice,Formatting.Indented);maps.Add(slice);
            }
            var journeySummaries=new List<LevelSummary>();
            foreach(var journey in MonetizationConfiguration.Load().journeys)
            {
                if(journey.levelIds.Length==0)continue;
                var local=new List<LevelSummary>();int number=0;
                foreach(var id in journey.levelIds)
                {
                    var level=LevelLoader.Load(id);local.Add(new LevelSummary{id=id,number=++number,width=level.width,height=level.height,
                        decorSeed=level.decorSeed,lighting=level.lighting,environmentPreset=level.environmentPreset,contentHash=level.contentHash,ruleVersion=level.ruleVersion,noise=level.noise,
                        entry=level.entry,accessPoints=level.accessPoints,mapObjects=level.objects,canopies=level.canopies,exteriorWalkable=level.exteriorWalkable,environment=level.environment,environmentalStory=level.environmentalStory,
                        shade=level.guests.Any(g=>g.shade),quiet=level.guests.Any(g=>g.quiet),friends=level.friends.Length>0,fire=level.objects.Any(o=>o.assetId.Contains("campfire"))});
                }
                string authored="Assets/QuietCamp/Authoring/Roadmap/"+journey.id+".json";
                var map=File.Exists(authored)?QuietCamp.Application.RoadmapCompiler.BakeAuthored(JsonConvert.DeserializeObject<RoadmapDefinition>(File.ReadAllText(authored)),local)
                    :QuietCamp.Application.RoadmapCompiler.Build(local,Array.Empty<BonusCampDefinition>(),"regions-1",journey.id);
                var problems=QuietCamp.Application.RoadmapValidator.Validate(map);if(problems.Count>0)throw new InvalidOperationException(string.Join("; ",problems));
                outputs[Folder+"Roadmaps/"+journey.id+".json"]=JsonConvert.SerializeObject(map,Formatting.Indented);maps.Add(map);journeySummaries.AddRange(local);
            }
            outputs[Folder+"journey_summaries.json"]=JsonConvert.SerializeObject(journeySummaries,Formatting.Indented);
            var models=new List<RoadmapModelLibrary.Model>();
            foreach(var entry in AssetCatalog.Load().Entries)
                if(entry.prefab!=null)models.Add(Bake(entry.assetId,entry.prefab));
            foreach(var id in new[]{"plant_bushSmall","grass_leafsLarge","flower_yellowB","flower_purpleA"})
            {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneyNature/Models/"+id+".obj");
                if(model!=null)models.Add(Bake(id,model));
            }
            outputs[Folder+"roadmap_models.json"]=JsonConvert.SerializeObject(models);
            var modelIds=new HashSet<string>(models.Select(m=>m.id));
            string extraPath=Folder+"roadmap_story_models.json";
            if(File.Exists(extraPath))foreach(var model in JsonConvert.DeserializeObject<RoadmapModelLibrary.Model[]>(File.ReadAllText(extraPath)))modelIds.Add(model.id);
            string culturePath=Folder+"roadmap_culture_models.json";
            if(File.Exists(culturePath))foreach(var model in JsonConvert.DeserializeObject<RoadmapModelLibrary.Model[]>(File.ReadAllText(culturePath)))modelIds.Add(model.id);
            var levelIds=new HashSet<string>(summaries.Concat(journeySummaries).Select(x=>x.id));
            var bonuses=new HashSet<string>(BonusCampCatalog.Slots.Select(b=>"bonus:"+b.id));
            var journeys=new HashSet<string>(MonetizationConfiguration.Load().journeys.Select(j=>j.id));
            var locales=new List<Dictionary<string,string>>();
            foreach(var language in new[]{"uk","en","de"})locales.Add(JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(File.ReadAllText("Assets/QuietCamp/Resources/QuietCampLocales/"+language+".json"))["entries"].ToObject<Dictionary<string,string>>());
            foreach(var map in maps)
            {
                var errors=QuietCamp.Application.RoadmapValidator.Validate(map,id=>levelIds.Contains(id)||bonuses.Contains(id),key=>locales.All(l=>l.ContainsKey(key)),modelIds.Contains,journeys.Contains);
                if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors));
            }
            // No resource is replaced before all routes, models and locales validate.
            Commit(outputs);RoadmapRepository.Reset();
            AssetDatabase.Refresh();RoadmapModelLibrary.Reset();
            Debug.Log("[RoadmapExport] "+summaries.Count+" frozen levels, "+models.Count+" source models; no solver or scene capture.");
        }
        static void Commit(Dictionary<string,string> outputs)
        {
            var backups=new Dictionary<string,string>();var changed=new List<string>();
            try
            {
                foreach(var item in outputs)
                {
                    string old=File.Exists(item.Key)?File.ReadAllText(item.Key):null;if(old==item.Value)continue;
                    backups.Add(item.Key,old);Directory.CreateDirectory(Path.GetDirectoryName(item.Key));
                    File.WriteAllText(item.Key+".roadmap-stage",item.Value);
                }
                foreach(var item in backups){if(File.Exists(item.Key))File.Replace(item.Key+".roadmap-stage",item.Key,null);else File.Move(item.Key+".roadmap-stage",item.Key);changed.Add(item.Key);}
            }
            catch
            {
                foreach(var path in changed){if(backups[path]==null)File.Delete(path);else File.WriteAllText(path,backups[path]);}
                throw;
            }
            finally{foreach(var path in backups.Keys)if(File.Exists(path+".roadmap-stage"))File.Delete(path+".roadmap-stage");}
        }
        static RoadmapModelLibrary.Model Bake(string id,GameObject prefab)
        {
            var positions=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<int>();
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;var renderer=filter.GetComponent<MeshRenderer>();
                if(mesh==null||renderer==null)continue;
                var matrix=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                var normalMatrix=matrix.inverse.transpose;
                var vertices=mesh.vertices;var ns=mesh.normals;var materials=renderer.sharedMaterials;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var material=sub<materials.Length?materials[sub]:null;
                    var color=material!=null&&material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):
                        material!=null&&material.HasProperty("_Color")?material.color:new Color(.45f,.57f,.30f);
                    // Understory recolours imported leaves in gameplay; apply that same
                    // palette here, rather than the source OBJ's turquoise leaf material.
                    if((id.StartsWith("plant_")||id.StartsWith("grass_")||id=="flower_yellowB"||id=="flower_purpleA")&&color.g>color.r)
                        color=new Color(.43f,.61f,.29f);
                    Color32 c=color;int packed=(c.r<<16)|(c.g<<8)|c.b;
                    var tris=mesh.GetTriangles(sub);
                    for(int t=0;t<tris.Length;t++)
                    {
                        int vertex=tris[t];positions.Add(matrix.MultiplyPoint3x4(vertices[vertex]));
                        normals.Add(normalMatrix.MultiplyVector(ns[vertex]).normalized);
                        if(t%3==0)colors.Add(packed);
                    }
                }
            }
            if(positions.Count==0)throw new InvalidOperationException("Empty roadmap model: "+id);
            var bounds=new Bounds(positions[0],Vector3.zero);foreach(var p in positions)bounds.Encapsulate(p);
            float height=Mathf.Max(.001f,bounds.size.y);var origin=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            var data=new float[positions.Count*6];
            for(int i=0;i<positions.Count;i++)
            {
                var p=(positions[i]-origin)/height;var n=normals[i];int j=i*6;
                data[j]=Round(p.x);data[j+1]=Round(p.y);data[j+2]=Round(p.z);
                data[j+3]=Round(n.x);data[j+4]=Round(n.y);data[j+5]=Round(n.z);
            }
            return new RoadmapModelLibrary.Model { id=id,data=data,colors=colors.ToArray() };
        }
        static float Round(float f)=>(float)Math.Round(f,4);
    }
    sealed class RoadmapContentWatcher : AssetPostprocessor
    {
        static bool _queued;
        static bool Source(string path)=>path.StartsWith(RoadmapContentExporter.Folder+"Levels/")
            ||path.StartsWith(RoadmapContentExporter.Folder+"GeneratedLevels/")
            ||path==RoadmapContentExporter.Folder+"AssetCatalog.asset"
            ||path==RoadmapContentExporter.Folder+"campaign.json"
            ||path.StartsWith("Assets/QuietCamp/Authoring/Roadmap/")
            ||path==RoadmapContentExporter.Folder+"monetization.json"
            ||path==RoadmapContentExporter.Folder+"bonus_camps.json"
            ||path==RoadmapContentExporter.Folder+"roadmap_story_models.json"
            ||path==RoadmapContentExporter.Folder+"roadmap_culture_models.json"
            ||path.StartsWith("Assets/QuietCamp/Resources/QuietCampLocales/")
            ||path.StartsWith("Assets/QuietCamp/Prefabs/Models/")
            ||path.StartsWith("Assets/QuietCamp/Materials/")
            ||path.StartsWith("Assets/ThirdParty/KenneyNature/Models/");
        static void OnPostprocessAllAssets(string[] imported,string[] deleted,string[] moved,string[] from)
        {
            if(_queued||!imported.Concat(deleted).Concat(moved).Concat(from).Any(Source))return;
            _queued=true;EditorApplication.delayCall+=()=>
            {
                _queued=false;
                if(EditorApplication.isPlayingOrWillChangePlaymode||AssetCatalog.Load()==null)return;
                const string authored="Assets/QuietCamp/Authoring/Roadmap/main.json";
                if(File.Exists(authored))
                {
                    var candidate=JsonConvert.DeserializeObject<RoadmapDefinition>(File.ReadAllText(authored));
                    try { QuietCamp.Application.RoadmapCompositionAdapter.RequireCampaignCoverage(candidate,LevelLoader.MvpLevelIds()); }
                    catch(InvalidOperationException e)
                    { Debug.Log("[RoadmapExport] Current campaign retained: "+e.Message);return; }
                }
                try { RoadmapContentExporter.Export(); }
                catch(Exception e) { Debug.LogError("[RoadmapExport] "+e.Message); }
            };
        }
    }
}
