using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    [Serializable] public sealed class WorldStudyProfile
    {
        public string id,title,sourceLevel,biome;
        public int seed,trees=36,plants=1500;
        public float cameraPitch=58,cameraYaw=225,cameraSize=10,sunPitch=52,sunYaw=260,sunIntensity=1.2f,extent=36,riverCentre=-10,riverWidth=2.4f,riverBend=.5f,relief=.3f;
        public float[] target={0,0,0};
        public string[] treeModels,bushModels;
        public WorldStudyAnchor[] anchors=Array.Empty<WorldStudyAnchor>();
        public float age=.4f,treeMin=.65f,treeMax=1,innerForest=9,outerForest=24;
        public bool liveTerrain;
        public bool refineExistingPlants;
        public int groundLow=0x76824a,groundHigh=0x9a9e60;
    }
    [Serializable] public sealed class WorldStudyAnchor
    {
        public string model,label,age="moss",instanceId;
        public float sourceHeight;
        public float x,z,y,yaw,scale=1,metric=1;
        public float ageAmount=-1;
        public string motion;
        public SurfaceWeatheringSettings weathering;
        public bool preserveGeometry;
        public bool water;
    }
    public static class WorldStudyFactory
    {
        public const string ProfilesPath="Assets/QuietCamp/Authoring/WorldStudies/profiles.json";
        public static WorldStudyProfile[] Profiles => JsonConvert.DeserializeObject<WorldStudyProfile[]>(File.ReadAllText(ProfilesPath));
        static readonly Dictionary<string,float> MetricHeights=new Dictionary<string,float>();
        public static CampArtDefinition.Model MetricModel(string id)
        {
            var original=LevelAuthoringPalette.Get(id).model;
            float scale=1;
            if(id.StartsWith("qc_aircraft_",StringComparison.Ordinal))scale=38f/(14.23f+15.42f)/.28f;
            else
            {
                if(MetricHeights.Count==0)
                {
                    var manifest=JObject.Parse(File.ReadAllText("Assets/QuietCamp/Authoring/Roadmap/Models/EnvironmentKit/manifest.json"));
                    foreach(var item in manifest["models"])MetricHeights[(string)item["id"]]=(float)item["sourceHeight"];
                }
                // CampModelLibrary already stores some overlapping IDs in metres.
                // Only restore the donor height when this actual mesh is height-normalized.
                if(Math.Abs(AuthoringPropCatalog.ModelBounds(original).size.y-1)<.005f)MetricHeights.TryGetValue(id,out scale);
                if(scale<=0)scale=1;
            }
            if(Math.Abs(scale-1)<.0001f)return original;
            var entry=LevelAuthoringPalette.Get(id);
            var variant=ModelAuthoringWorkshop.Create(original,new ModelAuthoringRecipe{operation="scale",size=scale,label=entry.label+" — метричний",category=entry.category});
            return variant.model;
        }
        public static LevelAuthoringDocument Create(string profileId)
        {
            var p=Profiles.Single(x=>x.id==profileId);var d=LevelAuthoringDocument.Create(true,p.sourceLevel);
            return Compose(d,p);
        }
        public static LevelAuthoringDocument Compose(LevelAuthoringDocument d,WorldStudyProfile p,bool persistVariants=true)
        {
            d.title=p.title;d.level.id="QC_STUDY_"+p.id.ToUpperInvariant();d.art.levelId=d.level.id;d.art.seed=p.seed;d.art.generation="world-study/"+p.id;
            d.art.cameraEuler=new[]{p.cameraPitch,p.cameraYaw,0};d.art.cameraTarget=p.target;d.art.orthographicSize=p.cameraSize;
            d.art.sunEuler=new[]{p.sunPitch,p.sunYaw,0};d.art.sunIntensity=p.sunIntensity;d.art.frameYaw=0;
            d.art.surroundExtent=p.extent;d.art.streamCentre=p.riverCentre;d.art.streamWidth=p.riverWidth;d.art.streamBend=p.riverBend;d.art.streamSlope=.03f;d.art.meadowRelief=p.relief;
            d.art.groundLow=p.groundLow;d.art.groundHigh=p.groundHigh;d.art.blurPixels=.9f;d.art.saturation=-5;d.art.contrast=8;
            var models=new Dictionary<string,CampArtDefinition.Model>();var instances=new List<CampArtDefinition.Instance>();var random=new System.Random(p.seed);
            void Place(CampArtDefinition.Model model,float x,float z,float size,float yaw,float y=0,bool attached=true)
            {
                var position=OutsideField(d,model,x,z,size,yaw);x=position.x;z=position.y;
                models[model.id]=model;
                instances.Add(new CampArtDefinition.Instance{id=p.id+"/"+instances.Count,modelId=model.id,x=x,z=z,y=attached?d.art.GroundHeight(x,z)+y:y,yaw=yaw,scale=size,surfaceAttached=attached,surfaceOffset=y});
            }
            foreach(var anchor in p.anchors)
            {
                var source=MetricModel(anchor.model);
                if(anchor.metric!=1)source=ModelAuthoringWorkshop.Create(source,new ModelAuthoringRecipe{operation="scale",size=anchor.metric,label=anchor.label??anchor.model,category="Декор"},persistVariants).model;
                var model=ModelAuthoringWorkshop.Create(source,new ModelAuthoringRecipe{operation="age",backend=anchor.age,amount=anchor.ageAmount<0?p.age:anchor.ageAmount,seed=p.seed,label=anchor.label??LevelAuthoringPalette.Get(anchor.model).label+" — "+p.title,category=LevelAuthoringPalette.Get(anchor.model).category},persistVariants).model;
                Place(model,anchor.x,anchor.z,anchor.scale,anchor.yaw,anchor.y,!anchor.water);
                instances[instances.Count-1].motion=anchor.motion;
            }
            if(p.biome=="aircraft")
            {
                foreach(var side in new[]{"fore","tail"})
                {
                    var model=AircraftBreakAuthoring.Repair(MetricModel("qc_aircraft_"+side),side=="fore");var b=AuthoringPropCatalog.ModelBounds(model);
                    float x=side=="fore"?d.level.width*.5f+1.7f+b.extents.z:-d.level.width*.5f-1.7f-b.extents.z;
                    Place(model,x,side=="fore"?1.2f:-.6f,1,90);
                }
            }
            var trees=p.treeModels.Select(MetricModel).ToArray();
            if(p.biome=="orchard")trees=trees.Select(m=>ModelAuthoringWorkshop.Create(m,new ModelAuthoringRecipe{operation="age",backend="autumn",amount=.8f,seed=p.seed,label="Осіннє дерево — "+m.id,category="Дерева"},persistVariants).model).ToArray();
            for(int i=0;i<p.trees;i++)
            {
                double angle=random.NextDouble()*Math.PI*2;float radius=p.innerForest+(float)random.NextDouble()*(p.outerForest-p.innerForest);
                float x=(float)Math.Cos(angle)*radius,z=(float)Math.Sin(angle)*radius;
                float size=p.treeMin+(float)random.NextDouble()*(p.treeMax-p.treeMin);
                var model=trees[i%trees.Length];var b=AuthoringPropCatalog.ModelBounds(model);
                if(Math.Abs(x)<d.level.width*.5f+b.extents.x*size+1 || Math.Abs(z)<d.level.height*.5f+b.extents.z*size+1)
                { if(Math.Abs(x)<d.level.width*.5f+b.extents.x*size+1 && Math.Abs(z)<d.level.height*.5f+b.extents.z*size+1)continue; }
                if(d.art.GroundHeight(x,z)<-.18f || instances.Any(v=>Math.Abs(v.x-x)<2.5f&&Math.Abs(v.z-z)<2.5f))continue;
                // Low foreground saplings keep the field readable; adult silhouettes live behind the clearing.
                if(x+z< -8)size*=.45f;
                if(p.biome=="aircraft" && Math.Abs(z)<4 && Math.Abs(x)<28)continue;
                if(p.biome=="aircraft" && instances.Where(a=>(models[a.modelId].sourceModelId??"").StartsWith("qc_aircraft_")).Any(a=>
                {
                    var shape=AuthoringPropCatalog.ModelBounds(models[a.modelId]);
                    return Math.Abs(x-a.x)<shape.extents.z+b.extents.x*size+.3f && Math.Abs(z-a.z)<shape.extents.x+b.extents.z*size+.3f;
                }))continue;
                if(OccludesField(d,p,model,x,z,size))continue;
                Place(model,x,z,size,(float)random.NextDouble()*360);
            }
            var bushes=p.bushModels.Select(MetricModel).ToArray();
            for(int i=0;i<p.plants;i++)
            {
                // Ecological islands: clusters around tree trunks and an irregular woodland edge.
                float x=(float)(random.NextDouble()*2-1)*p.outerForest,z=(float)(random.NextDouble()*2-1)*p.outerForest;
                if(Math.Abs(x)<d.level.width*.5f+.7f && Math.Abs(z)<d.level.height*.5f+.7f)continue;
                float ground=d.art.GroundHeight(x,z);if(ground<-.12f)continue;
                float noise=Mathf.PerlinNoise(x*.18f+p.seed*.01f,z*.18f);if(noise<.4f)continue;
                if(instances.Take(p.anchors.Length+(p.biome=="aircraft"?2:0)).Any(a=>Math.Abs(a.x-x)<1.8f&&Math.Abs(a.z-z)<1.5f))continue;
                var model=bushes[i%bushes.Length];Place(model,x,z,.55f+(float)random.NextDouble()*.65f,(float)random.NextDouble()*360);
            }
            var cover=new List<CampArtDefinition.CoverPoint>();
            for(int i=0;i<p.plants*3;i++)
            {
                float x=(float)(random.NextDouble()*2-1)*Mathf.Min(p.outerForest,18),z=(float)(random.NextDouble()*2-1)*Mathf.Min(p.outerForest,18);
                if(Math.Abs(x)<d.level.width*.5f+.3f&&Math.Abs(z)<d.level.height*.5f+.3f)continue;
                float ground=d.art.GroundHeight(x,z);if(ground<-.12f || Mathf.PerlinNoise(x*.24f+p.seed*.01f,z*.24f)<.4f)continue;
                cover.Add(new CampArtDefinition.CoverPoint{x=x,z=z,y=ground,kind=1,height=.08f+(float)random.NextDouble()*.11f,maximumSpan=.14f,yaw=(float)random.NextDouble()*360,variation=(float)random.NextDouble()});
            }
            d.art.instances=instances.ToArray();d.art.models=models.Values.ToArray();d.art.cover=cover.ToArray();d.art.review=null;d.art.attempts=Array.Empty<Kruty1918.ScenePlacement.Composition.CompositionAttempt>();
            return d;
        }
        public static Vector2 OutsideField(LevelAuthoringDocument d,CampArtDefinition.Model model,float x,float z,float scale,float yaw)
        {
            var shape=AuthoringPropCatalog.ModelBounds(model);
            var rotation=Quaternion.Euler(0,yaw,0);var lo=new Vector2(float.MaxValue,float.MaxValue);var hi=new Vector2(float.MinValue,float.MinValue);
            for(int k=0;k<4;k++)
            {
                var q=rotation*(new Vector3((k&1)==0?shape.min.x:shape.max.x,0,(k&2)==0?shape.min.z:shape.max.z)*scale);
                lo=Vector2.Min(lo,new Vector2(q.x,q.z));hi=Vector2.Max(hi,new Vector2(q.x,q.z));
            }
            float hx=d.level.width*.5f+.65f,hz=d.level.height*.5f+.65f;
            if(x+lo.x<hx&&x+hi.x>-hx&&z+lo.y<hz&&z+hi.y>-hz)
            {
                var shift=new[]{new Vector2(hx-x-lo.x,0),new Vector2(-hx-x-hi.x,0),new Vector2(0,hz-z-lo.y),new Vector2(0,-hz-z-hi.y)}.OrderBy(v=>v.sqrMagnitude).First();
                x+=shift.x;z+=shift.y;
            }
            return new Vector2(x,z);
        }
        static bool OccludesField(LevelAuthoringDocument d,WorldStudyProfile p,CampArtDefinition.Model model,float x,float z,float scale)
        {
            var bounds=AuthoringPropCatalog.ModelBounds(model);var inverse=Quaternion.Inverse(Quaternion.Euler(p.cameraPitch,p.cameraYaw,0));
            float minX=float.MaxValue,maxX=float.MinValue,minY=float.MaxValue,maxY=float.MinValue,near=float.MaxValue;
            for(int k=0;k<8;k++)
            {
                var corner=bounds.center+Vector3.Scale(bounds.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));
                var q=inverse*(corner*scale+new Vector3(x,d.art.GroundHeight(x,z),z));
                minX=Mathf.Min(minX,q.x);maxX=Mathf.Max(maxX,q.x);minY=Mathf.Min(minY,q.y);maxY=Mathf.Max(maxY,q.y);near=Mathf.Min(near,q.z);
            }
            for(int iz=0;iz<d.level.height;iz++)for(int ix=0;ix<d.level.width;ix++)
            {
                var q=inverse*new Vector3(ix-d.level.width*.5f+.5f,.8f,iz-d.level.height*.5f+.5f);
                if(q.x>minX-.25f&&q.x<maxX+.25f&&q.y>minY-.25f&&q.y<maxY+.25f&&near<q.z)return true;
            }
            return false;
        }
        public static void EnableTerrain(LevelAuthoringDocument d,string profileId)
        {
            var p=Profiles.Single(v=>v.id==profileId);if(!p.liveTerrain)return;
            d.terrain=RiverTerrainAuthoringData.FromLegacy(d.art);
            d.terrain.bankWidth=1.7f;d.terrain.bankSoftness=.9f;
            if(p.biome=="river")d.terrain.points=new[]{new RiverPoint{x=-8.7f,z=-40},new RiverPoint{x=-7.6f,z=-10},new RiverPoint{x=-8.2f,z=-4},new RiverPoint{x=-7.4f,z=3},new RiverPoint{x=-6.2f,z=10},new RiverPoint{x=-8.5f,z=40}};
            if(p.biome=="pine")
                d.terrain.points=new[]{new RiverPoint{x=28,z=-40},new RiverPoint{x=30,z=0},new RiverPoint{x=29,z=40}};
            d.terrain.stamps=new[]{new LandscapeStamp{id=p.id+"/hill",x=12,z=11,radius=8,height=p.biome=="pine"?1.25f:.65f},new LandscapeStamp{id=p.id+"/hollow",x=-14,z=-9,radius=6,height=-.25f}};
        }
    }
}
