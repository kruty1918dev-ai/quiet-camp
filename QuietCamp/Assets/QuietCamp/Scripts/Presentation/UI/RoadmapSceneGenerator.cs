using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Presentation.UI
{
    /// <summary>A deterministic presentation scene from a lightweight level summary.
    /// Neither this generator nor its renderer can access a solver or load a level.</summary>
    public static class RoadmapSceneGenerator
    {
        public sealed class Prop
        {
            public string Asset;
            public Vector3 Position;
            public float Height, Yaw;
            public Vector2 Stretch=Vector2.one;
            public bool Sway, Fire, Tent;
            public RoadmapModelLibrary.Model Geometry;
            internal Vector2[] ShadowHull;
        }
        public sealed class Scene
        {
            public LevelSummary Level;
            internal LevelData SnowTerrain;
            public float SnowDepth(Vector3 p)=>Season.SnowDepth(SnowTerrain,p);
            public AtmosphereCatalog.Profile Light;
            public Vector3 Sun;
            public readonly List<Prop> Props=new List<Prop>();
            public CampWeatherTimeline.State Weather;
            public float WeatherTime;
            public float VerticalExtent {get;private set;}
            public bool Night=>Light.Id=="night";
            public bool Winter=>Level.environment?.seasonId=="winter";
            public float Snowfall=>Winter?Mathf.Lerp(.16f,.72f,Weather.Cloud):0;
            public SeasonProfile Season=>SeasonProfile.For(Level);
            public SeasonPalette Palette=>Season.Palette;
            public Color Ground=>Tint(Color.Lerp(Palette.GrassDark,Palette.GrassLight,.65f),Vector3.up);
            public Color PlantColor(Color source,Prop prop)
                =>prop.Sway?Palette.Plant(source,SeasonProfile.Variation(prop.Position,Level.decorSeed),prop.Asset.Contains("pine")):source;
            public Color SurfaceColor(Color source,Prop prop,Vector3 normal)
                =>Color.Lerp(PlantColor(source,prop),new Color(.88f,.92f,.98f),
                    (prop.Sway||prop.Geometry!=null?Palette.SnowCoverage:0)*Mathf.SmoothStep(0,1,Mathf.Clamp01((normal.y-.35f)/.40f)));
            public void SetMoment(float seconds)
            {
                WeatherTime=seconds>=0?seconds:CampWeatherTimeline.RepresentativeTime(Level.decorSeed);
                Weather=seconds>=0?CampWeatherTimeline.Preview(Level.decorSeed,seconds,Level.environment?.weatherId,Winter)
                    :string.IsNullOrEmpty(Level.environment?.weatherId)?CampWeatherTimeline.Preview(Level.decorSeed,WeatherTime)
                    :CampWeatherTimeline.Initial(Level.environment.weatherId);
                if(Winter)Weather=new CampWeatherTimeline.State(Weather.Cloud,0);
            }
            public Color Tint(Color source,Vector3 normal,float occlusion=1)
            {
                float direct=Mathf.Max(0,Vector3.Dot(normal,Sun))*Light.SunIntensity*(1-.65f*Weather.Cloud);
                float ambient=Night?.36f:.58f;
                var light=Palette.Ambient(Light.Ambient)*ambient+Light.Sun*Palette.SunTint*(direct*.46f);
                light.r+=.13f;light.g+=.13f;light.b+=.13f;light.a=1;
                var result=source*light;result*=occlusion;result.a=source.a;
                return result;
            }
            public void Advance(float dt)
            {
                WeatherTime+=Mathf.Max(0,dt);var target=CampWeatherTimeline.Target(WeatherTime,Level.decorSeed,Level.environment?.weatherId);
                float ease=1-Mathf.Exp(-Mathf.Max(0,dt)/5);
                Weather=new CampWeatherTimeline.State(Mathf.Lerp(Weather.Cloud,target.Cloud,ease),Winter?0:Mathf.Lerp(Weather.Rain,target.Rain,ease));
            }
            internal void MeasureExtent()
            {
                VerticalExtent=0;var library=RoadmapModelLibrary.Load();
                foreach(var prop in Props)
                {
                    var model=prop.Geometry??library.Get(prop.Asset);if(model==null)continue;
                    var bounds=model.Bounds;var rotation=Quaternion.Euler(0,prop.Yaw,0);
                    for(int i=0;i<8;i++)
                    {
                        var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        p=rotation*new Vector3(p.x*prop.Stretch.x,p.y,p.z*prop.Stretch.y)*prop.Height+prop.Position;
                        VerticalExtent=Mathf.Max(VerticalExtent,Mathf.Abs(RoadmapPainter.Project(p,1).y));
                        p.x-=p.y*Sun.x/Mathf.Max(.18f,Sun.y);p.z-=p.y*Sun.z/Mathf.Max(.18f,Sun.y);p.y=0;
                        VerticalExtent=Mathf.Max(VerticalExtent,Mathf.Abs(RoadmapPainter.Project(p,1).y));
                    }
                }
                var shore=Level.environment?.shore;
                if(shore!=null)VerticalExtent=Mathf.Max(VerticalExtent,.37f*(Mathf.Abs(shore.offset)+Mathf.Max(1.2f,shore.width)*.5f+ShorelineGeometry.MaximumBend+Mathf.Max(Level.width,Level.height)*.5f+3.8f)+.07f);
                if(Winter)VerticalExtent=Mathf.Max(VerticalExtent,(Mathf.Max(Level.width,Level.height)*.5f+4.8f)*.74f+2.15f*.86f);
            }
        }
        public static Scene Generate(LevelSummary level,AtmosphereCatalog catalog)
        {
            using var audit = PerformanceAudit.Measure("QC.RoadmapSceneGenerator.Generate");
            var light=catalog.Resolve(level.id,level.lighting);
            var sun=-(Quaternion.Euler(light.Elevation,65,0)*Vector3.forward);
            var scene=new Scene { Level=level,Light=light,Sun=sun,
                WeatherTime=CampWeatherTimeline.RepresentativeTime(level.decorSeed) };
            // A lightweight geometry view; never load content or invoke the solver while scrolling.
            scene.SnowTerrain=new LevelData{width=level.width,height=level.height,ruleVersion=2,decorSeed=level.decorSeed,
                entry=level.entry,accessPoints=level.accessPoints,exteriorWalkable=level.exteriorWalkable,environment=level.environment};
            scene.SetMoment(-1);
            var rng=new System.Random(level.decorSeed);
            float w=level.width*.5f,h=level.height*.5f;
            void Add(string asset,float x,float z,float height,bool sway=false,float yaw=-1)
            {
                var prop=new Prop { Asset=asset,Position=new Vector3(x,0,z),Height=height,
                    Yaw=yaw<0?rng.Next(360):yaw,Sway=sway,Fire=asset.Contains("campfire"),Tent=asset.StartsWith("tent") };
                if(asset.StartsWith("tree")&&scene.Season.BareTree(prop.Position,level.decorSeed,asset.Contains("pine")))prop.Geometry=RoadmapModelLibrary.Load().BareTree(asset);
                scene.Props.Add(prop);
            }
            foreach(var prop in level.mapObjects??Array.Empty<EnvironmentObjectData>())
                Add(prop.assetId,prop.x+.5f-w,prop.z+.5f-h,
                    prop.assetId.StartsWith("tree")?2.15f:prop.assetId.Contains("campfire")?.42f:prop.assetId.Contains("log")?.42f:.55f,
                    prop.assetId.StartsWith("tree"),prop.rotation*90);
            // Match the game's rule-canopy placement: trunks remain outside,
            // their crowns project onto the authored shade area inside the field.
            foreach(var crown in level.canopies??Array.Empty<ShadeCanopyData>())
            {
                float crownHeight=Mathf.Max(2.1f,(crown.x+.4f)*Mathf.Max(.2f,sun.y)/Mathf.Max(.12f,-sun.x));
                var centre=new Vector3(crown.x-w,0,crown.z-h);
                var position=centre+new Vector3(sun.x,0,sun.z)*(crownHeight/Mathf.Max(.2f,sun.y));
                var tree=new Prop { Asset="tree_default",Position=position,Height=crownHeight/.7f,Yaw=0,Sway=true };
                // tree_default's normalized width/depth are baked from its source mesh.
                var source=RoadmapModelLibrary.Load().Get(tree.Asset);float minX=float.MaxValue,maxX=float.MinValue,minZ=minX,maxZ=maxX;
                foreach(var p in source.Positions) { minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.z);maxZ=Mathf.Max(maxZ,p.z); }
                tree.Stretch=new Vector2(crown.radiusX*2/((maxX-minX)*tree.Height),crown.radiusZ*2/((maxZ-minZ)*tree.Height));
                if(scene.Season.BareTree(tree.Position,level.decorSeed))tree.Geometry=RoadmapModelLibrary.Load().BareTree(tree.Asset);
                scene.Props.Add(tree);
            }
            // These are camp vignettes, deliberately outside the playable grid.
            // No witness placements or hidden puzzle solutions are exposed by the map.
            Add("tent_smallOpen",-.9f,-h-1.35f,1.18f,false,0);
            if(level.number>2)Add("tent_detailedOpen",1.5f,-h-1.45f,1.08f,false,90);
            int treeCount=level.environment==null?10:Mathf.RoundToInt(Mathf.Lerp(6,18,Mathf.Clamp01(level.environment.treeDensity)));
            rng=new System.Random(unchecked(level.decorSeed*971+37));
            for(int i=0;i<treeCount;i++)
            {
                float x=(float)(rng.NextDouble()*2-1)*(w+4.4f),z=(float)(rng.NextDouble()*2-1)*(h+4.4f);
                if(Mathf.Abs(x)<w+1.8f&&Mathf.Abs(z)<h+1.8f) { i--;continue; }
                if(OnAccess(level,new Vector2(x,z),1.1f))continue;
                if(ShorelineGeometry.Contains(level.environment?.shore,new Vector3(x,0,z),1.1f))continue;
                int species=rng.Next(4);bool pine=level.environmentPreset=="pines"||species==0;
                float treeHeight=1.8f+(float)rng.NextDouble()*1.4f;int treeYaw=rng.Next(360);
                Add(pine?"tree_pineRoundA":"tree_default",x,z,treeHeight,true,treeYaw);
                float bushHeight=.42f+(float)rng.NextDouble()*.22f;int bushYaw=rng.Next(360);float chance=(float)rng.NextDouble();
                if(!scene.Winter&&chance<scene.Season.ShrubWeight)Add("plant_bushSmall",x+.6f,z-.65f,bushHeight,true,bushYaw);
            }
            for(int i=0;i<22;i++)
            {
                float x=(float)(rng.NextDouble()*2-1)*(w+4.3f),z=(float)(rng.NextDouble()*2-1)*(h+4.3f);
                if(Mathf.Abs(x)<w+.55f&&Mathf.Abs(z)<h+.55f)continue;
                if(OnAccess(level,new Vector2(x,z),.5f))continue;
                if(ShorelineGeometry.Contains(level.environment?.shore,new Vector3(x,0,z),.5f))continue;
                if(scene.Winter||scene.Season.Autumn&&rng.NextDouble()>scene.Season.ShrubWeight)continue;
                if(scene.Season.Autumn&&(i%5==0||i%7==0))continue;
                Add(i%5==0?"flower_yellowA":i%7==0?"flower_purpleA":i%11==0?"grass_leafsLarge":"plant_bushSmall",x,z,.18f+(float)rng.NextDouble()*.26f,true);
                if(i%9==0)Add("stone_largeA",x+.4f,z-.4f,.24f);
            }
            var story=RoadmapModelLibrary.Load().Story(level.environment?.storyMotifs);
            if(story!=null)
            {
                for(int corner=0;corner<4;corner++)
                {
                    var at=new Vector3((corner%2==0?-1:1)*(w+3.3f),0,(corner<2?-1:1)*(h+2.8f));
                    if(OnAccess(level,new Vector2(at.x,at.z),1.4f)||ShorelineGeometry.Contains(level.environment?.shore,at,2))continue;
                    scene.Props.Add(new Prop{Asset=story.id,Geometry=story,Position=at,Height=story.SourceHeight*.7f,Yaw=0});break;
                }
            }
            scene.Props.Sort((a,b)=>(b.Position.x+b.Position.z).CompareTo(a.Position.x+a.Position.z));
            scene.MeasureExtent();
            return scene;
        }
        public static IEnumerable<Vector2> Access(LevelSummary level)
        {
            if(level.entry!=null&&level.entry.Length==2)yield return new Vector2(level.entry[0]+.5f-level.width*.5f,level.entry[1]+.5f-level.height*.5f);
            foreach(var p in level.accessPoints??Array.Empty<AccessPointData>())
                yield return new Vector2(p.x+.5f-level.width*.5f,p.z+.5f-level.height*.5f);
        }
        public static Vector2 Outward(LevelSummary level,Vector2 p)
        {
            float dx=level.width*.5f-Mathf.Abs(p.x),dz=level.height*.5f-Mathf.Abs(p.y);
            return dx<dz?new Vector2(Mathf.Sign(p.x),0):new Vector2(0,Mathf.Sign(p.y));
        }
        static bool OnAccess(LevelSummary level,Vector2 p,float margin)
        {
            foreach(var at in Access(level))
            {
                var direction=Outward(level,at);var delta=p-at;
                if(Vector2.Dot(delta,direction)>-.6f&&Mathf.Abs(delta.x*direction.y-delta.y*direction.x)<margin)return true;
            }
            return false;
        }
    }
}
