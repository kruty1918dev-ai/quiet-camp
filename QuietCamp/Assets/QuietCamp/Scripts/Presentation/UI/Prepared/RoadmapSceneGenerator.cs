using System;
using System.Collections.Generic;
using QuietCamp.Domain;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Presentation.UI.Prepared
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
            public bool Sway, Fire, Tent, StorySilhouette;
            public RoadmapModelLibrary.Model Geometry;
            public RoadmapVisualProfile Visual;public bool HasVisual;
            public Vector3 LeafVertex(Vector3 local,Color material)
            {
                if(HasVisual&&Sway&&(Asset.StartsWith("tree")||Asset=="ua_orchard_tree")&&!Asset.Contains("pine")&&material.g>material.r*1.03f)
                {float fullness=.5f+.5f*Visual.Environment.Leaves;local.x*=fullness;local.z*=fullness;}
                return local;
            }
            internal Vector2[] ShadowHull;
            internal int ShadowHullCount;
        }
        public sealed class Scene
        {
            public LevelSummary Level;
            public bool StoryCompleted;
            internal LevelData SnowTerrain;
            Vector3[] _trailStarts,_trailEnds;int _trailCount;bool _trailsCached;
            RoadmapEnvironmentSampler _environment;float _distance,_units;bool _projected;
            public RoadmapVisualProfile Visual {get;private set;}
            public RoadmapVisualProfile VisualAt(Vector3 p)=>new RoadmapVisualProfile(_environment.Sample(_distance-(_projected?p.x+p.z:p.z)*_units));
            public float SnowAmount=>_environment!=null?Visual.Environment.Snow:(Level.environment?.seasonId=="winter"?1:0);
            public float LeafLitter=>_environment!=null?(1-Visual.Environment.Leaves)*(1-Visual.Environment.Snow):Season.LeafLitter;
            public float ParticleWeight=>_environment!=null?Visual.Environment.Particles:1;
            public void BindEnvironment(RoadmapEnvironmentSampler environment,float distance,float units,bool projected)
            {_environment=environment;_distance=distance;_units=units;_projected=projected;if(environment!=null){Visual=VisualAt(Vector3.zero);Sun=-(Quaternion.Euler(Mathf.Lerp(50,22,Visual.Environment.Night),65,0)*Vector3.forward);_palette=Visual.Palette;_season=SeasonProfile.For(Level);_phenologyCached=true;SetMoment(-1);}}
            public float SnowDepth(Vector3 p)
            {
                if(SnowTerrain==null||Level==null||!Winter||Mathf.Max(Mathf.Abs(p.x)-Level.width*.5f,Mathf.Abs(p.z)-Level.height*.5f)<.6f)return 0;
                if(!_trailsCached)
                {
                    int capacity=(1+(Level.accessPoints?.Length??0))*12;
                    if(_trailStarts==null||_trailStarts.Length<capacity){_trailStarts=new Vector3[capacity];_trailEnds=new Vector3[capacity];}
                    _trailCount=0;
                    foreach(var cell in CampAccess.Points(SnowTerrain))
                    {
                        var previous=CampTrail.Centre(SnowTerrain,cell,0);
                        for(int i=1;i<=12;i++)
                        {var next=CampTrail.Centre(SnowTerrain,cell,CampTrail.Length*i/12f);_trailStarts[_trailCount]=previous;_trailEnds[_trailCount++]=next;previous=next;}
                    }
                    _trailsCached=true;
                }
                float distance=float.PositiveInfinity;
                foreach(var authored in SnowTerrain.ruleVersion==2?SnowTerrain.exteriorWalkable??Array.Empty<int[]>():Array.Empty<int[]>())
                {
                    if(authored?.Length!=2)continue;
                    var at=BoardMath.CellCenterWorld(SnowTerrain,new Cell(authored[0],authored[1]));
                    distance=Mathf.Min(distance,Mathf.Max(Mathf.Abs(p.x-at.x),Mathf.Abs(p.z-at.z))-.52f);
                }
                for(int i=0;i<_trailCount;i++)
                {
                    var delta=_trailEnds[i]-_trailStarts[i];delta.y=0;var offset=p-_trailStarts[i];offset.y=0;
                    float t=Mathf.Clamp01(Vector3.Dot(offset,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                    distance=Mathf.Min(distance,(offset-delta*t).magnitude-.48f);
                }
                return SeasonProfile.For("winter").SnowDepth(SnowTerrain,p,distance)*SnowAmount;
            }
            public AtmosphereCatalog.Profile Light;
            public Vector3 Sun;
            public float ShadowSunHeight=>Mathf.Max(.38f,Sun.y);
            public readonly List<Prop> Props=new List<Prop>(92);
            Prop[] _slots;int _nextSlot;
            internal static Scene Reusable()
            {
                var scene=new Scene{SnowTerrain=new LevelData(),_slots=new Prop[92]};
                for(int i=0;i<scene._slots.Length;i++)scene._slots[i]=new Prop{ShadowHull=new Vector2[16]};
                return scene;
            }
            internal void Begin(LevelSummary level,AtmosphereCatalog.Profile light)
            {
                Props.Clear();_nextSlot=0;_environment=null;_phenologyCached=false;_trailsCached=false;
                Level=level;Light=light;Sun=-(Quaternion.Euler(light.Elevation,65,0)*Vector3.forward);
                SnowTerrain.width=level.width;SnowTerrain.height=level.height;SnowTerrain.ruleVersion=level.ruleVersion;
                SnowTerrain.decorSeed=level.decorSeed;SnowTerrain.entry=level.entry;SnowTerrain.accessPoints=level.accessPoints;
                SnowTerrain.exteriorWalkable=level.exteriorWalkable;SnowTerrain.environment=level.environment;SnowTerrain.noise=level.noise;
                SetMoment(-1);
            }
            internal Prop TakeProp(string asset,Vector3 position,float height,float yaw,bool sway)
            {
                if(_nextSlot>=_slots.Length)throw new InvalidOperationException("Roadmap prop budget exceeded");
                var prop=_slots[_nextSlot++];prop.Asset=asset;prop.Position=position;prop.Height=height;prop.Yaw=yaw;
                prop.Sway=sway;prop.Tent=asset.StartsWith("tent");prop.Fire=asset.Contains("campfire");
                prop.Geometry=null;prop.StorySilhouette=false;prop.HasVisual=_environment!=null;if(prop.HasVisual)prop.Visual=VisualAt(position);prop.Stretch=Vector2.one;prop.ShadowHullCount=0;Props.Add(prop);return prop;
            }
            public CampWeatherTimeline.State Weather;
            public float WeatherTime;
            public float VerticalExtent {get;private set;}
            public bool Night=>Light.Id=="night";
            public bool Winter=>SnowAmount>.001f;
            public float Snowfall=>SnowAmount*Mathf.Lerp(.16f,.72f,Weather.Cloud);
            SeasonProfile _season;SeasonPalette _palette;bool _phenologyCached;
            public SeasonProfile Season {get {CachePhenology();return _season;}}
            public SeasonPalette Palette {get {CachePhenology();return _palette;}}
            void CachePhenology(){if(_phenologyCached)return;_season=SeasonProfile.For(Level);_palette=_season.Palette;_phenologyCached=true;}
            public Color Ground=>Tint(Color.Lerp(Palette.GrassDark,Palette.GrassLight,.65f),Vector3.up);
            public Color PlantColor(Color source,Prop prop)
                =>prop.Sway?(prop.HasVisual?prop.Visual.Plant(source,SeasonProfile.Variation(prop.Position,Level.decorSeed),prop.Asset.Contains("pine")):Palette.Plant(source,SeasonProfile.Variation(prop.Position,Level.decorSeed),prop.Asset.Contains("pine"))):source;
            public Color SurfaceColor(Color source,Prop prop,Vector3 normal)
                =>prop.StorySilhouette?Color.Lerp(Palette.Fog,Palette.GrassDark,.35f):Color.Lerp(PlantColor(source,prop),new Color(.88f,.92f,.98f),
                    (prop.Sway||prop.Geometry!=null?(prop.HasVisual?prop.Visual.Environment.Snow:Palette.SnowCoverage):0)*Mathf.SmoothStep(0,1,Mathf.Clamp01((normal.y-.35f)/.40f)));
            public void SetMoment(float seconds)
            {
                WeatherTime=seconds>=0?seconds:CampWeatherTimeline.RepresentativeTime(Level.decorSeed);
                Weather=seconds>=0?CampWeatherTimeline.Preview(Level.decorSeed,seconds,Level.environment?.weatherId,Winter)
                    :string.IsNullOrEmpty(Level.environment?.weatherId)?CampWeatherTimeline.Preview(Level.decorSeed,WeatherTime)
                    :CampWeatherTimeline.Initial(Level.environment.weatherId);
                Weather=new CampWeatherTimeline.State(Weather.Cloud,Weather.Rain*(1-SnowAmount));
            }
            public Color Tint(Color source,Vector3 normal,float occlusion=1)
            {
                if(_environment!=null)return Visual.Lit(source,normal)*occlusion;
                float direct=Mathf.Max(0,Vector3.Dot(normal,Sun))*Light.SunIntensity*(1-.65f*Weather.Cloud);
                float ambient=Night?.36f:.58f;
                var light=Palette.Ambient(Light.Ambient)*ambient+(_environment!=null?Visual.Sun:Light.Sun)*Palette.SunTint*(direct*.46f);
                light.r+=.13f;light.g+=.13f;light.b+=.13f;light.a=1;
                var result=source*light;result*=occlusion;result.a=source.a;
                return result;
            }
            public void Advance(float dt)
            {
                WeatherTime+=Mathf.Max(0,dt);var target=CampWeatherTimeline.Target(WeatherTime,Level.decorSeed,Level.environment?.weatherId);
                float ease=1-Mathf.Exp(-Mathf.Max(0,dt)/5);
                Weather=new CampWeatherTimeline.State(Mathf.Lerp(Weather.Cloud,target.Cloud,ease),Mathf.Lerp(Weather.Rain,target.Rain*(1-SnowAmount),ease));
            }
            internal void MeasureExtent(bool geometry=true)
            {
                VerticalExtent=0;if(!geometry){foreach(var prop in Props)VerticalExtent=Mathf.Max(VerticalExtent,Mathf.Abs(prop.Position.z)+prop.Height+3);return;}var library=RoadmapModelLibrary.Load();
                foreach(var prop in Props)
                {
                    var model=prop.Geometry??library.Get(prop.Asset);if(model==null)continue;
                    var bounds=model.Bounds;var rotation=Quaternion.Euler(0,prop.Yaw,0);
                    for(int i=0;i<8;i++)
                    {
                        var p=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                        p=rotation*new Vector3(p.x*prop.Stretch.x,p.y,p.z*prop.Stretch.y)*prop.Height+prop.Position;
                        VerticalExtent=Mathf.Max(VerticalExtent,Mathf.Abs(RoadmapPainter.Project(p,1).y));
                        p.x-=p.y*Sun.x/ShadowSunHeight;p.z-=p.y*Sun.z/ShadowSunHeight;p.y=0;
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
            var light=catalog.Resolve(level.id,level.lighting);
            var sun=-(Quaternion.Euler(light.Elevation,65,0)*Vector3.forward);
            var scene=new Scene { Level=level,Light=light,Sun=sun,
                WeatherTime=CampWeatherTimeline.RepresentativeTime(level.decorSeed) };
            // A lightweight geometry view; never load content or invoke the solver while scrolling.
            scene.SnowTerrain=new LevelData{width=level.width,height=level.height,ruleVersion=2,decorSeed=level.decorSeed,
                entry=level.entry,accessPoints=level.accessPoints,exteriorWalkable=level.exteriorWalkable,environment=level.environment,noise=level.noise};
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
        /// <summary>Activate pre-authored props. No randomness, JSON, story Mesh construction or level generation.</summary>
        public static Scene FromBaked(LevelSummary level,RoadmapWorldData data,AtmosphereCatalog catalog,RoadmapStoryPropData[] story=null,Scene reusable=null,RoadmapEnvironmentSampler environment=null,float distance=0,float units=1,bool projected=false,bool completed=false,bool geometry=true)
        {
            var light=catalog.Resolve(level.id,level.lighting);var library=geometry?RoadmapModelLibrary.Load():null;
            var scene=reusable??Scene.Reusable();scene.Begin(level,light);scene.StoryCompleted=completed;scene.BindEnvironment(environment,distance,units,projected);
            foreach(var value in data.props)
            {
                if(value.storyId!=null&&!EnvironmentalStoryPolicy.Appears(value.storyAppearance,completed))continue;
                if(environment!=null&&value.sway&&!value.assetId.StartsWith("tree"))
                {
                    var at=new Vector3(value.x,0,value.z);var visual=scene.VisualAt(at);
                    float weight=visual.Environment.Grass*(value.assetId.StartsWith("flower")?visual.Environment.Flowers:1);
                    if(SeasonProfile.Variation(at,level.decorSeed)>weight)continue;
                }
                string asset=value.assetId;
                if(asset.StartsWith("tent"))continue;
                if(environment!=null&&(asset=="tree_default"||asset=="tree_pineRoundA"))
                {
                    var at=new Vector3(value.x,0,value.z);
                    asset=SeasonProfile.Variation(at,level.decorSeed^717)<scene.VisualAt(at).Environment.Pines?"tree_pineRoundA":"tree_default";
                }
                var prop=scene.TakeProp(asset,new Vector3(value.x,0,value.z),value.height,value.yaw,value.sway);
                prop.StorySilhouette=value.storyVisibility=="silhouette";
                if(asset.StartsWith("tree")&&(prop.HasVisual?!asset.Contains("pine")&&SeasonProfile.Variation(prop.Position,level.decorSeed)<prop.Visual.Environment.Bare:scene.Season.BareTree(prop.Position,level.decorSeed,asset.Contains("pine"))))prop.Geometry=library?.BareTree(asset);
            }
            foreach(var value in story??Array.Empty<RoadmapStoryPropData>())
                scene.TakeProp(value.assetId,new Vector3(value.x,0,value.y),value.height,value.yaw,false);
            scene.Props.Sort((a,b)=>(b.Position.x+b.Position.z).CompareTo(a.Position.x+a.Position.z));
            scene.MeasureExtent(geometry);return scene;
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
