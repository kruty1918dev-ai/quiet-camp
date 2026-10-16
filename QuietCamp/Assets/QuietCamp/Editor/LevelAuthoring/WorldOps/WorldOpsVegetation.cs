using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    // Refine existing editable plants; never regenerate a level or erase its manual composition.
    public static class WorldOpsVegetation
    {
        static float Limit(JObject p,string key,float value,float min,float max)
        {
            float result=p[key]?.Value<float>()??value;
            if(float.IsNaN(result)||float.IsInfinity(result)||result<min||result>max)throw new ArgumentException("Invalid "+key);
            return result;
        }
        public static void Refine(LevelAuthoringDocument d,WorldOperation operation,int seed)
        {
            var p=operation.parameters;
            float height=Limit(p,"maximumHeight",.42f,.15f,1),span=Limit(p,"maximumSpan",.68f,.25f,1.5f);
            float boardHeight=Limit(p,"boardHeight",.14f,.06f,.2f),boardSpan=Limit(p,"boardSpan",.28f,.12f,.45f);
            float radius=Limit(p,"repairRadius",3,.2f,8),spacing=Limit(p,"spacing",.14f,.05f,.5f);
            var models=d.art.models.ToDictionary(m=>m.id);
            var sites=new WorldOpsGeometry.PlantSites(d);
            var selected=WorldOpsOperations.Select(d,operation.selector).OrderBy(i=>i.id,StringComparer.Ordinal).ToArray();
            var records=(d.worldOps?.objects??new List<WorldObjectRecord>()).ToDictionary(r=>r.id);
            var cells=new Dictionary<Vector2Int,List<CampArtDefinition.Instance>>();
            Vector2Int Cell(float x,float z)=>new Vector2Int(Mathf.FloorToInt(x/spacing),Mathf.FloorToInt(z/spacing));
            void Add(CampArtDefinition.Instance i)
            {var key=Cell(i.x,i.z);if(!cells.TryGetValue(key,out var bucket))cells[key]=bucket=new List<CampArtDefinition.Instance>();bucket.Add(i);}
            foreach(var i in d.art.instances.Where(i=>models.TryGetValue(i.modelId??"",out var m)&&WorldOpsOperations.Role(d.art,i,m)=="vegetation"))Add(i);
            bool Near(float x,float z,string id)
            {
                var key=Cell(x,z);
                for(int dx=-1;dx<=1;dx++)for(int dz=-1;dz<=1;dz++)
                    if(cells.TryGetValue(key+new Vector2Int(dx,dz),out var bucket)&&bucket.Any(i=>i.id!=id&&(new Vector2(i.x-x,i.z-z)).sqrMagnitude<spacing*spacing))return true;
                return false;
            }
            foreach(var i in selected)
            {
                var m=models[i.modelId];records.TryGetValue(i.id,out var record);
                // Moss/ivy and authored support relationships belong to the overgrowth system.
                if(i.id.StartsWith("overgrowth/",StringComparison.Ordinal)||record?.role=="aquatic"||record?.support?.StartsWith("object:",StringComparison.Ordinal)==true)continue;
                var role=WorldOpsOperations.Role(d.art,i,m);
                if(role!="vegetation"&&role!="tree")continue;
                bool inBoard=WorldOpsGeometry.Protected(d,i.x,i.z),flower=m.seasonalRole=="flower";
                var bounds=WorldOpsGeometry.Bounds(i,m);float h=inBoard?boardHeight:flower?1.8f:height,w=inBoard?boardSpan:flower?.85f:span;
                // World bounds include arbitrary rotation and non-uniform scale. Reduce uniformly
                // to preserve the silhouette; an existing small plant is never enlarged.
                float factor=role=="tree"?1:Mathf.Min(1,Mathf.Min(h/Mathf.Max(.001f,bounds.size.y),w/Mathf.Max(.001f,Mathf.Max(bounds.size.x,bounds.size.z))));
                if(factor<.9999f)
                {i.scale*=factor;if(i.scaleXYZ!=null)i.scaleXYZ=i.scaleXYZ.Select(v=>v*factor).ToArray();}
                // Root the actual low vertices, including fern assets with positive local minima.
                i.y-=WorldOpsGeometry.Clearance(d,i,m)+.006f;
                i.surfaceAttached=true;i.surfaceOffset=i.y-d.art.GroundHeight(i.x,i.z);
                bool Bad(float x,float z)=>sites.Wet(x,z)||(!WorldOpsGeometry.Protected(d,x,z)&&WorldOpsGeometry.ProtectedPlant(d,x,z))||sites.InSolid(new Vector3(x,d.art.GroundHeight(x,z)+.12f,z));
                if(!Bad(i.x,i.z))continue;
                var random=new System.Random(seed^WorldOpsOperations.Stable(i.id));
                for(int attempt=0;attempt<96;attempt++)
                {
                    double angle=random.NextDouble()*Math.PI*2;
                    float distance=radius*Mathf.Sqrt((attempt+1)/96f);
                    float x=i.x+(float)Math.Cos(angle)*distance,z=i.z+(float)Math.Sin(angle)*distance;
                    // Relocation cannot invade the puzzle, a path, or the edge of frozen terrain.
                    var land=d.art.landscape;
                    if(WorldOpsGeometry.ProtectedPlant(d,x,z)||land!=null&&(x<land.originX+.4f||z<land.originZ+.4f||x>land.originX+land.size-.4f||z>land.originZ+land.size-.4f)||Bad(x,z)||Near(x,z,i.id))continue;
                    if(cells.TryGetValue(Cell(i.x,i.z),out var old))old.Remove(i);
                    i.x=x;i.z=z;i.y=d.art.GroundHeight(x,z)+i.surfaceOffset;
                    i.y-=WorldOpsGeometry.Clearance(d,i,m)+.006f;i.surfaceOffset=i.y-d.art.GroundHeight(x,z);Add(i);break;
                }
            }
        }
    }
}
