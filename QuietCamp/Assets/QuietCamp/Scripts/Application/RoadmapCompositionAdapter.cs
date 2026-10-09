using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Composition;
namespace QuietCamp.Application
{
    /// <summary>Only this game adapter knows normalized nodes, logical scrolling and seasons.</summary>
    public static class RoadmapCompositionAdapter
    {
        public const float Units=22.936256f,Width=26;
        public static void RequireCampaignCoverage(RoadmapDefinition map,IReadOnlyList<string> campaignIds)
        {
            var authored=new List<string>();
            foreach(var region in map.regions)foreach(var node in region.nodePositions)authored.Add(node.levelId);
            bool matches=authored.Count==campaignIds.Count;
            for(int i=0;matches&&i<authored.Count;i++)matches=authored[i]==campaignIds[i];
            if(!matches)throw new InvalidOperationException("Prepared roadmap covers "+authored.Count+
                " nodes; the active campaign requires "+campaignIds.Count+
                " in its exact ID order. Complete authoring before publishing Main.");
        }
        public static void Bind(RoadmapDefinition map,IReadOnlyDictionary<string,SceneCompositionDocument> documents)
        {
            foreach(var region in map.regions)
            {
                if(!documents.TryGetValue(region.id,out var doc))throw new ArgumentException("Missing composition region: "+region.id);
                foreach(var node in region.nodePositions)
                {
                    var anchor=Array.Find(doc.nodes,n=>n.id==node.id);if(anchor==null)throw new ArgumentException("Missing node binding: "+node.id);
                    float delta=anchor.z*Units-node.y;node.x=.5f+anchor.x/Width;node.y=anchor.z*Units;
                    foreach(var branch in region.branches)if(branch.anchorNodeId==node.id){branch.y+=delta;branch.x=Math.Clamp(node.x+(branch.x>.5f?.24f:-.24f),.12f,.88f);}
                }
            }
        }
        public static float TerrainHeight(RoadmapCatalog map,RoadmapEnvironmentSampler climate,float x,float z)
        {
            int index=map.NodeAt(z*Units),next=Math.Min(index+1,map.Nodes.Length-1);
            float Distance(int node){float dx=x-(map.Nodes[node].x-.5f)*Width,dz=z-map.Y(node)/Units;return(float)Math.Sqrt(dx*dx+dz*dz);}
            float t=Math.Clamp((Math.Min(Distance(index),Distance(next))-5)/4,0,1),clear=t*t*(3-2*t);
            var environment=climate.Sample(z*Units);
            return(environment.Relief*(float)Math.Sin(x*.32f)*(float)Math.Cos(-z*.16f)+environment.Snow*.25f*(1+(float)Math.Sin(x*.2f-z*.13f)))*clear;
        }
        public sealed class Terrain : ITerrainSample
        {
            readonly RoadmapCatalog _map;readonly RoadmapEnvironmentSampler _climate;readonly SceneCompositionDocument[] _docs;
            public Terrain(RoadmapCatalog map,SceneCompositionDocument[] docs=null){_map=map;_climate=new RoadmapEnvironmentSampler(map);_docs=docs;}
            public bool Supported(float x,float z,float radius)
            {
                if(_docs==null)return RoadmapRuralLayout.ClearOfWater(_climate,x,z*Units,radius);
                foreach(var doc in _docs)foreach(var surface in doc.surfaces)
                    if(SurfaceRecipes.Wet(surface)&&(SurfaceRecipes.Contains(surface,x,z)||SurfaceRecipes.EdgeDistance(surface,x,z)<radius))return false;
                return true;
            }
            public float Height(float x,float z)=>TerrainHeight(_map,_climate,x,z);
        }
        public static SceneCompositionDocument WorldDocument(SceneCompositionDocument local,float start)
        {
            // A caller-owned copy is required: source authoring must never be modified by compilation.
            foreach(var n in local.nodes)n.z+=start;
            foreach(var z in local.zones)z.z+=start;
            foreach(var e in local.ensembles)if(e.placement.fixedPosition)e.placement.z+=start;
            foreach(var r in local.routes)foreach(var p in r.points)p.z+=start;
            foreach(var l in local.landmarks)l.z+=start;
            foreach(var surface in local.surfaces)if(!surface.relativeToOwner)foreach(var point in surface.points)point.z+=start;
            return local;
        }
    }
}
