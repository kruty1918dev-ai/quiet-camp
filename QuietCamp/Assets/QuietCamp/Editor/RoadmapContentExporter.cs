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
            var summaries=new List<LevelSummary>();int index=0;
            foreach(var id in LevelLoader.MvpLevelIds())
            {
                if(GeneratedCampSource.IsGeneratedId(id)&&Resources.Load<TextAsset>("QuietCamp/GeneratedLevels/"+id.Replace(':','_'))==null)
                    throw new InvalidOperationException("Freeze the level before exporting its map: "+id);
                var level=LevelLoader.Load(id);
                summaries.Add(new LevelSummary { id=id,number=++index,width=level.width,height=level.height,
                    decorSeed=level.decorSeed,lighting=level.lighting,environmentPreset=level.environmentPreset,
                    entry=level.entry,accessPoints=level.accessPoints,mapObjects=level.objects,canopies=level.canopies,
                    exteriorWalkable=level.exteriorWalkable,environment=level.environment,
                    shade=level.guests.Any(g=>g.shade),quiet=level.guests.Any(g=>g.quiet),
                    friends=level.friends.Length>0,fire=level.objects.Any(p=>p.assetId.Contains("campfire")) });
            }
            WriteIfDifferent(Folder+"level_summaries.json",JsonConvert.SerializeObject(summaries,Formatting.Indented));
            var models=new List<RoadmapModelLibrary.Model>();
            foreach(var entry in AssetCatalog.Load().Entries)
                if(entry.prefab!=null)models.Add(Bake(entry.assetId,entry.prefab));
            foreach(var id in new[]{"plant_bushSmall","grass_leafsLarge","flower_yellowB","flower_purpleA"})
            {
                var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/KenneyNature/Models/"+id+".obj");
                if(model!=null)models.Add(Bake(id,model));
            }
            WriteIfDifferent(Folder+"roadmap_models.json",JsonConvert.SerializeObject(models));
            AssetDatabase.Refresh();
            Debug.Log("[RoadmapExport] "+summaries.Count+" frozen levels, "+models.Count+" source models; no solver or scene capture.");
        }
        static void WriteIfDifferent(string path,string text)
        { if(!File.Exists(path)||File.ReadAllText(path)!=text)File.WriteAllText(path,text); }
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
                try { RoadmapContentExporter.Export(); }
                catch(Exception e) { Debug.LogError("[RoadmapExport] "+e.Message); }
            };
        }
    }
}
