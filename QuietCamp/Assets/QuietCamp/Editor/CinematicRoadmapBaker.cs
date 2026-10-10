using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using QuietCamp.Composition;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace QuietCamp.Editor
{
    /// <summary>Native publication of the five-place world, independent of the retired glade renderer.</summary>
    public static class CinematicRoadmapBaker
    {
        public const string Source = "Assets/QuietCamp/Authoring/Roadmap/Composition/CinematicPilot";
        const string Output = "Assets/QuietCamp/Resources/QuietCamp/CinematicRoadmap";
        sealed class SourcePoint { public string levelId,titleUk; public float x,z,focusX,focusZ,yaw,pitch,distance; }
        sealed class LooseConductor { public float ax,az,ay,bx,bz,by,slack; }
        sealed class SourceWorld { public string id; public int seed; public float fieldOfView,minX,maxX,minZ,maxZ; public float[] chunkStarts,chunkEnds; public SourcePoint[] waypoints; public LooseConductor[] looseConductors=Array.Empty<LooseConductor>(); }
        sealed class Binding { public string asset,catalogueId,source,sourceHash,guid,prefab; public int lod; public Dictionary<string,string> dependencies; }
        static readonly List<object> DonorReviews=new List<object>();
        static readonly List<object> GroundProps=new List<object>();
        static SourceWorld World;
        static SceneCompositionDocument Document;
        static CompositionResult Composition;
        static SurfaceRecipe[] Surfaces;
        static RoadmapModelLibrary Library;
        static Dictionary<string,RoadmapModelLibrary.Model> Donors;
        static Dictionary<string,VisualAssetDefinition> Assets;
        static string Revision;
        static T Read<T>(string name) => JsonConvert.DeserializeObject<T>(File.ReadAllText(Source+"/"+name));
        static string Hash(byte[] data) { using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant(); }
        public static string SourceHash()
        {
            var files=Directory.GetFiles(Source,"*.json").OrderBy(f=>f).Concat(new[]{
                "Assets/QuietCamp/Editor/CinematicRoadmapBaker.cs",
                "Assets/QuietCamp/Scripts/Presentation/World/RoadmapWorldAsset.cs",
                "Assets/QuietCamp/Scripts/Presentation/World/RoadmapWorldChunk.cs",
                "Assets/QuietCamp/Scripts/Application/Composition/SceneComposition.cs",
                "Assets/QuietCamp/Scripts/Application/Composition/SurfaceRecipes.cs",
                "Assets/QuietCamp/Scripts/Presentation/UI/RoadmapModelLibrary.cs",
                "Assets/QuietCamp/Resources/QuietCamp/RoadmapLit.shader",
                "Assets/QuietCamp/Resources/QuietCamp/RoadmapMotes.shader",
                "Assets/QuietCamp/Resources/QuietCamp/roadmap_models.json",
                "Assets/QuietCamp/Resources/QuietCamp/roadmap_culture_models.json",
                "Assets/ThirdParty/Stylized Water 3/Shaders/StylizedWater3_Standard.watershader3",
                "Assets/ThirdParty/Stylized Water 3/Shaders/StylizedWater3_Standard.watershader3.meta",
                "Assets/ThirdParty/Stylized Water 3/Profiles/River Wave Profile.asset",
                "Assets/ThirdParty/Stylized Water 3/Materials/Textures/Normals/LowpolyWaves.png",
                "Assets/ThirdParty/Stylized Water 3/Materials/Textures/IntersectionNoise.png",
                "Assets/ThirdParty/Stylized Water 3/Materials/Textures/Foam/Foam1.png",
                "Assets/QuietCamp/Authoring/Roadmap/Models/EnvironmentKit/models.json"})
                .Concat(Read<Binding[]>("bindings.json").SelectMany(b=>b.dependencies.Keys).Distinct().OrderBy(f=>f,StringComparer.Ordinal));
            return Hash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n",files.Select(f=>f+":"+Hash(File.ReadAllBytes(f))))));
        }
        sealed class Terrain : ITerrainSample
        {
            public float Height(float x,float z) => Ground(x,z);
            public bool Supported(float x,float z,float radius) => x-radius>World.minX&&x+radius<World.maxX&&z-radius>World.minZ&&z+radius<World.maxZ;
        }
        static float Ground(float x,float z)
        {
            float h=RoadmapLandscape.Height(x,z);
            foreach(var e in Document.ensembles)
            {
                float distance=Vector2.Distance(new Vector2(x,z),new Vector2(e.placement.x,e.placement.z));
                float weight=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(9,16,distance));
                if(z>88)weight*=Mathf.SmoothStep(0,1,Mathf.InverseLerp(0,5,RoadmapLandscape.WaterDistance(x,z)));
                h=Mathf.Lerp(h,RoadmapLandscape.Height(e.placement.x,e.placement.z),weight);
            }
            return h;
        }
        [MenuItem("Quiet Camp/Cinematic Roadmap/Bake Five Places")]
        public static void Bake()
        {
            World=Read<SourceWorld>("world.json");Document=Read<SceneCompositionDocument>("valley.json");
            if(World.waypoints.Length!=5||!World.waypoints.Select(p=>p.levelId).SequenceEqual(QuietCamp.Application.RoadmapPilotPolicy.LevelIds))
                throw new InvalidOperationException("Pilot must reference exactly QC001–QC005");
            Assets=Read<VisualAssetDefinition[]>("assets.json").ToDictionary(a=>a.id);
            var templates=Read<EnsembleTemplate[]>("templates.json").ToDictionary(t=>t.id);
            Composition=SceneComposer.Compose(Document,Assets,templates,new Terrain());
            if(!Composition.Valid)throw new InvalidOperationException(JsonConvert.SerializeObject(Composition.diagnostics));
            Surfaces=SurfaceRecipes.Resolve(Document,Composition);
            Library=RoadmapModelLibrary.Load();Library.PrepareCulture();Library.PrepareStaging();
            Donors=new Dictionary<string,RoadmapModelLibrary.Model>();DonorReviews.Clear();GroundProps.Clear();PlacementRejects.Clear();
            foreach(var b in Read<Binding[]>("bindings.json"))
            {
                if(Hash(File.ReadAllBytes(b.source))!=b.sourceHash||AssetDatabase.AssetPathToGUID(b.source)!=b.guid)
                    throw new InvalidOperationException("Stale reviewed model binding: "+b.catalogueId);
                foreach(var dependency in b.dependencies)
                    if(Hash(File.ReadAllBytes(dependency.Key))!=dependency.Value)
                        throw new InvalidOperationException("Stale reviewed prefab/palette input: "+dependency.Key);
                Donors.Add(b.asset,ReadDonor(b));
            }
            BalancedPlants=PlanVegetation(false);LowPlants=PlanVegetation(true);ContactPlants=new PlantIndex();foreach(var plant in BalancedPlants)ContactPlants.Add(plant);
            Revision=SourceHash();var current=Resources.Load<RoadmapWorldAsset>("QuietCamp/CinematicRoadmap/World");
            if(current!=null&&current.sourceHash==Revision&&current.chunks.All(id=>Resources.Load<RoadmapWorldChunk>(id)!=null))return;
            string folder=Output+"/"+Revision.Substring(0,16);
            // A failed bake may have left an unpublished partial revision. Rebuild only that revision.
            if(AssetDatabase.IsValidFolder(folder))AssetDatabase.DeleteAsset(folder);
            Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            var index=ScriptableObject.CreateInstance<RoadmapWorldAsset>();index.sourceHash=Revision;index.revision=Revision.Substring(0,16);
            index.fieldOfView=World.fieldOfView;index.chunkStarts=World.chunkStarts;index.chunkEnds=World.chunkEnds;
            index.waypoints=World.waypoints.Select(p=>new RoadmapWaypoint {levelId=p.levelId,titleUk=p.titleUk,
                position=new Vector3(p.x,Ground(p.x,p.z)+.12f,p.z),focus=new Vector3(p.focusX,Ground(p.focusX,p.focusZ)+1.5f,p.focusZ),
                yaw=p.yaw,pitch=p.pitch,distance=p.distance}).ToArray();
            index.chunks=new string[World.chunkStarts.Length];
            index.ground=Material(folder,"Ground",0,0);index.structure=Material(folder,"Architecture",0,0);
            index.foliage=Material(folder,"Living vegetation",.045f,.003f);
            index.motes=new Material(Resources.Load<Shader>("QuietCamp/RoadmapMotes")){name="Quiet airborne pollen"};
            AssetDatabase.CreateAsset(index.motes,folder+"/Motes.mat");
            index.water=RiverMaterial();
            AssetDatabase.CreateAsset(index.water,folder+"/Water.mat");
            index.marker=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")){name="Roadside waystone"};
            index.marker.SetColor("_BaseColor",new Color(.83f,.76f,.49f));AssetDatabase.CreateAsset(index.marker,folder+"/Marker.mat");
            var stats=new List<object>();
            for(int c=0;c<index.chunks.Length;c++)
            {
                var chunk=ScriptableObject.CreateInstance<RoadmapWorldChunk>();chunk.index=c;chunk.sourceHash=Revision;
                chunk.balanced=BuildChunk(c,false);chunk.low=BuildChunk(c,true);
                chunk.sourceAssets=Composition.instances.Where(i=>OwnerChunk(i.z)==c).Select(i=>i.asset)
                    .Concat(Document.landmarks.Where(l=>OwnerChunk(l.z)==c).Select(l=>l.asset))
                    .Concat(BalancedPlants.Where(p=>OwnerChunk(p.z)==c).Select(p=>p.asset))
                    .Concat(new[]{"tree_default","tree_pineRoundA","stone_largeA","pilot.road-ribbon","pilot.airborne-pollen"}).Distinct().OrderBy(s=>s).ToArray();
                chunk.estimatedBytes=chunk.balanced.Concat(chunk.low).Where(m=>m!=null).Sum(m=>(long)m.vertexCount*52+m.GetIndexCount(0)*4);
                string file=folder+"/chunk-"+c+".asset";AssetDatabase.CreateAsset(chunk,file);
                foreach(var m in chunk.balanced.Concat(chunk.low).Where(m=>m!=null))AssetDatabase.AddObjectToAsset(m,chunk);
                index.chunks[c]="QuietCamp/CinematicRoadmap/"+Revision.Substring(0,16)+"/chunk-"+c;
                stats.Add(new {index=c,chunk.estimatedBytes,balancedTriangles=chunk.balanced.Where(m=>m!=null).Sum(m=>(long)m.GetIndexCount(0)/3),lowTriangles=chunk.low.Where(m=>m!=null).Sum(m=>(long)m.GetIndexCount(0)/3),chunk.sourceAssets});
            }
            index.horizon=Horizon();AssetDatabase.CreateAsset(index.horizon,folder+"/Horizon.asset");
            index.distantForest=new Mesh[index.chunks.Length];
            index.horizonTerrain=new Mesh[index.chunks.Length];
            for(int c=0;c<index.chunks.Length;c++)
            {
                index.distantForest[c]=FarForest(c);AssetDatabase.CreateAsset(index.distantForest[c],folder+"/Distant-"+c+".asset");
                var terrain=new Geometry();TerrainGrid(terrain,World.chunkStarts[c],World.chunkEnds[c],10,true);
                for(int v=0;v<terrain.vertices.Count;v++)terrain.vertices[v]+=Vector3.down*.35f;
                index.horizonTerrain[c]=terrain.Mesh("Unloaded terrain "+c);
                AssetDatabase.CreateAsset(index.horizonTerrain[c],folder+"/HorizonTerrain-"+c+".asset");
            }
            var river=new Geometry();Water(river,88,World.maxZ);index.river=river.Mesh("Continuous valley river");index.river.RecalculateTangents();
            AssetDatabase.CreateAsset(index.river,folder+"/River.asset");
            index.riverByFrontier=new Mesh[5];index.riverByFrontier[4]=index.river;
            for(int frontier=2;frontier<4;frontier++)
            {
                var mesh=Object.Instantiate(index.river);mesh.name="River reveal "+frontier;
                var colors=mesh.colors;var vertices=mesh.vertices;
                float reveal=(frontier*40+26)*index.worldScale;
                for(int v=0;v<colors.Length;v++)colors[v].g=Mathf.SmoothStep(0,1,Mathf.InverseLerp(reveal-3,reveal+7,vertices[v].z*index.worldScale));
                mesh.colors=colors;index.riverByFrontier[frontier]=mesh;
                AssetDatabase.CreateAsset(mesh,folder+"/River-"+frontier+".asset");
            }
            var stone=new Geometry();Append(stone,"stone_largeA",0,0,.35f,0,false);
            for(int v=0;v<stone.vertices.Count;v++)stone.vertices[v]-=Vector3.up*Ground(0,0);
            index.markerMesh=stone.Mesh("Small roadside waystone");
            AssetDatabase.CreateAsset(index.markerMesh,folder+"/Waystone.asset");
            // Make fresh Resources paths visible before publishing their index.
            // Existing Editor sessions otherwise retain the previous resource registry.
            AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(string path in index.chunks)
            {
                var prepared=Resources.Load<RoadmapWorldChunk>(path);
                if(prepared==null||prepared.sourceHash!=Revision)
                    throw new InvalidOperationException("Native chunk not imported before publication: "+path);
            }
            // Index publication is the final operation; never touch the old main catalog.
            const string pointer=Output+"/World.asset";var old=AssetDatabase.LoadAssetAtPath<RoadmapWorldAsset>(pointer);
            if(old==null)AssetDatabase.CreateAsset(index,pointer);
            else {EditorUtility.CopySerialized(index,old);Object.DestroyImmediate(index);index=old;EditorUtility.SetDirty(old);}
            AssetDatabase.SaveAssets();AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string repo=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../.."));string evidence=Path.Combine(repo,"Design/Roadmap/CinematicPilot/2026-10-10");Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence,"bake-receipt.json"),JsonConvert.SerializeObject(new {sourceHash=Revision,compiler=SceneComposer.Revision,unity=UnityEngine.Application.unityVersion,composition=Composition,stats,donors=DonorReviews,road=new{followsNativeTerrainTriangles=true,horizonOnlyWhenUnloaded=true,surfaces=Surfaces},vegetation=new{balanced=BalancedPlants,low=LowPlants,rejected=PlacementRejects,groundProps=GroundProps,fullAnimatedFootprints=true,globalBeforeChunkSplit=true,buildingsExcludeAllPlants=true},levelIds=index.waypoints.Select(p=>p.levelId),playerBuild=false},Formatting.Indented)+"\n");
            Debug.Log("[CinematicRoadmap] Published five native world chunks "+Revision);
        }
        static Material Material(string folder,string name,float sway,float flutter)
        {
            var shader=Resources.Load<Shader>("QuietCamp/RoadmapLit");if(shader==null||!shader.isSupported)throw new InvalidOperationException("Roadmap production shader unavailable");
            var m=new Material(shader){name=name};m.SetColor("_BaseColor",Color.white);m.SetFloat("_VertexTint",1);m.SetFloat("_ClusterWind",1);
            m.SetFloat("_SwayAmp",sway);m.SetFloat("_FlutterAmp",flutter);m.SetFloat("_Cull",sway>0?0:2);
            AssetDatabase.CreateAsset(m,folder+"/"+name+".mat");return m;
        }
        static int OwnerChunk(float z) {for(int i=0;i<World.chunkEnds.Length;i++)if(z<World.chunkEnds[i])return i;return World.chunkEnds.Length-1;}
        sealed class Geometry
        {
            public readonly List<Vector3> vertices=new List<Vector3>(),normals=new List<Vector3>();
            readonly List<Color> colors=new List<Color>();readonly List<Vector4> roots=new List<Vector4>();readonly List<Vector2> uvs=new List<Vector2>();readonly List<int> indices=new List<int>();
            public void Triangle(Vector3 a,Vector3 b,Vector3 c,Color color,Vector4 root=default,Vector3? normal=null,Color? second=null,Color? third=null,Func<Vector3,Vector2> uv=null)
            {
                var n=normal??Vector3.Cross(b-a,c-a).normalized;int first=vertices.Count;
                vertices.AddRange(new[]{a,b,c});normals.AddRange(new[]{n,n,n});colors.AddRange(new[]{color,second??color,third??color});
                if(root==default)root=new Vector4(0,0,0,-1);roots.AddRange(new[]{root,root,root});
                Vector2 UV(Vector3 p)=>uv!=null?uv(p):new Vector2(p.x,p.z);
                uvs.AddRange(new[]{UV(a),UV(b),UV(c)});indices.AddRange(new[]{first,first+1,first+2});
            }
            public Mesh Mesh(string name)
            {
                if(vertices.Count==0)return null;var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};
                m.SetVertices(vertices);m.SetNormals(normals);m.SetColors(colors);m.SetUVs(0,uvs);m.SetUVs(1,roots);m.SetTriangles(indices,0);m.RecalculateBounds();
                var b=m.bounds;b.Expand(1.2f);m.bounds=b;return m;
            }
        }
        static Mesh[] BuildChunk(int c,bool low)
        {
            ContactPlants=new PlantIndex();foreach(var plant in low?LowPlants:BalancedPlants)ContactPlants.Add(plant);
            var parts=Enumerable.Range(0,6).Select(i=>new Geometry()).ToArray();float start=World.chunkStarts[c],end=World.chunkEnds[c];
            TerrainGrid(parts[0],start,end,low?4:2.5f);Road(parts[1],start,end,low?4:2.5f);
            // The composer emits both ensemble roles and accepted landmarks.
            // Appending Document.landmarks again duplicates their geometry and shadows.
            foreach(var item in Composition.instances.Where(i=>OwnerChunk(i.z)==c))Append(parts[Assets[item.asset].wind?2:1],item.asset,item.x,item.z,item.height,item.yaw,Assets[item.asset].wind);
            foreach(var p in (low?LowPlants:BalancedPlants).Where(p=>OwnerChunk(p.z)==c&&!Composition.instances.Any(i=>i.id==p.id)&&!Assets.ContainsKey(p.asset)))
            {
                if(p.asset=="pilot.grass-tuft")GrassTuft(parts[3],p);
                else Append(parts[p.tree?2:3],p.asset,p.x,p.z,p.height,p.yaw,true,p.tint);
            }
            // Authored vegetation was appended above; only planner-owned scatter is added here.
            foreach(var p in (low?LowPlants:BalancedPlants).Where(p=>OwnerChunk(p.z)==c&&Assets.ContainsKey(p.asset)&&!Composition.instances.Any(i=>i.id==p.id)))
                Append(parts[p.tree?2:3],p.asset,p.x,p.z,p.height,p.yaw,true,p.tint);
            var random=new System.Random(World.seed+c*733);
            for(float z=Mathf.Max(-24,start)+3;z<Mathf.Min(235,end);z+=7)for(float x=-35;x<36;x+=7)
            {
                float px=x+(float)random.NextDouble()*3,pz=z+(float)random.NextDouble()*3;
                double chance=random.NextDouble();float height=.35f+(float)random.NextDouble()*.45f,radius=PlantRadius("stone_largeA",height);
                if(chance>(low?.06:.2)||Reserved(px,pz,radius)||!DryFootprint(px,pz,radius)||HitsArchitecture(px,pz,radius)
                    ||ContactPlants.Near(px,pz,radius).Any(p=>Vector2.Distance(new Vector2(px,pz),new Vector2(p.x,p.z))<radius+p.radius+.15f))continue;
                Append(parts[1],"stone_largeA",px,pz,height,z*17,false,.87f);
                GroundProps.Add(new{quality=low?"low":"balanced",x=px,z=pz,height,radius,asset="stone_largeA"});
            }
            foreach(var wire in World.looseConductors)if(OwnerChunk((wire.az+wire.bz)*.5f)==c)Conductor(parts[1],wire);
            random=new System.Random(World.seed+c*1229);
            for(int i=0;i<(low?18:36);i++)
            {
                float x=-25+(float)random.NextDouble()*50,z=Mathf.Lerp(Mathf.Max(-24,start),Mathf.Min(240,end),(float)random.NextDouble());
                if(!DryFootprint(x,z,.5f)||HitsArchitecture(x,z,.5f))continue;
                var p=new Vector3(x,Ground(x,z)+1.5f+(float)random.NextDouble()*3,z);
                var root=new Vector4(x,z,p.y,(float)random.NextDouble()*6.28f);float size=.085f;
                parts[5].Triangle(p-Vector3.right*size,p+Vector3.up*size,p+Vector3.right*size,new Color(.84f,.81f,.59f),root);
            }
            return parts.Select((p,i)=>p.Mesh("Valley "+c+" / "+(low?"low":"balanced")+" / "+i)).ToArray();
        }
        static Mesh FarForest(int chunk)
        {
            var g=new Geometry();
            foreach(var p in BalancedPlants.Where(p=>p.tree&&OwnerChunk(p.z)==chunk))
            {
                float y=Ground(p.x,p.z);var tip=new Vector3(p.x,y+p.height,p.z);var color=new Color(.36f,.47f,.29f);
                float radius=p.radius-.08f-p.height*.055f;
                for(int side=0;side<6;side++)
                {
                    float a=side*Mathf.PI/3,b=(side+1)*Mathf.PI/3;
                    var first=new Vector3(p.x+Mathf.Cos(a)*radius,y+p.height*.43f,p.z+Mathf.Sin(a)*radius);
                    var second=new Vector3(p.x+Mathf.Cos(b)*radius,y+p.height*.43f,p.z+Mathf.Sin(b)*radius);
                    g.Triangle(first,tip,second,color);
                }
            }
            return g.Mesh("Simplified distant forest "+chunk);
        }
        sealed class Plant
        {
            public string id,asset;public float x,z,height,yaw,tint=1,radius;public bool tree;
        }
        static List<Plant> BalancedPlants,LowPlants;
        static readonly Dictionary<string,int> PlacementRejects=new Dictionary<string,int>();
        static RoadmapModelLibrary.Model Model(string asset)=>Donors.TryGetValue(asset,out var donor)?donor:Library.Get(asset=="pilot.ruined-house"?"ua_whitewashed_house":asset);
        static float PlantRadius(string asset,float height)
        {
            var model=Model(asset);float radius=model.Positions.Max(p=>new Vector2(p.x,p.z).magnitude)*height;
            return radius+height*.055f+.08f; // full animated crown, not only the trunk pivot
        }
        sealed class PlantIndex
        {
            readonly Dictionary<Vector2Int,List<Plant>> cells=new Dictionary<Vector2Int,List<Plant>>();
            public void Add(Plant p)
            {
                for(int x=Mathf.FloorToInt((p.x-p.radius)/6);x<=Mathf.FloorToInt((p.x+p.radius)/6);x++)
                    for(int z=Mathf.FloorToInt((p.z-p.radius)/6);z<=Mathf.FloorToInt((p.z+p.radius)/6);z++)
                    {var key=new Vector2Int(x,z);if(!cells.TryGetValue(key,out var list)){list=new List<Plant>();cells.Add(key,list);}list.Add(p);}
            }
            public IEnumerable<Plant> Near(float x,float z,float radius)
            {
                var unique=new HashSet<Plant>();
                for(int a=Mathf.FloorToInt((x-radius)/6);a<=Mathf.FloorToInt((x+radius)/6);a++)
                    for(int b=Mathf.FloorToInt((z-radius)/6);b<=Mathf.FloorToInt((z+radius)/6);b++)
                        if(cells.TryGetValue(new Vector2Int(a,b),out var list))foreach(var p in list)if(unique.Add(p))yield return p;
            }
        }
        static PlantIndex ContactPlants;
        static bool DryFootprint(float x,float z,float radius)
        {
            bool Dry(float a,float b)=>b<88||RoadmapLandscape.WaterDistance(a,b)>.35f&&Ground(a,b)>RoadmapLandscape.WaterHeight+.22f;
            if(!Dry(x,z))return false;
            for(int i=0;i<16;i++)
            {float angle=i*Mathf.PI/8;if(!Dry(x+Mathf.Cos(angle)*radius,z+Mathf.Sin(angle)*radius))return false;}
            return true;
        }
        static bool HitsArchitecture(float x,float z,float radius)
        {
            foreach(var item in Composition.instances.Where(i=>!Assets[i.asset].wind))
            {
                var model=Model(item.asset);var bounds=model.Bounds;var yaw=Quaternion.Euler(0,-item.yaw,0);
                var point=yaw*new Vector3(x-item.x,0,z-item.z);point-=bounds.center*item.height;
                float dx=Mathf.Max(0,Mathf.Abs(point.x)-bounds.extents.x*item.height),dz=Mathf.Max(0,Mathf.Abs(point.z)-bounds.extents.z*item.height);
                if(dx*dx+dz*dz<(radius+.3f)*(radius+.3f))return true;
            }
            return false;
        }
        static List<Plant> PlanVegetation(bool low)
        {
            var result=new List<Plant>();var index=new PlantIndex();
            bool Admit(Plant p,bool authored=false)
            {
                string reason=!DryFootprint(p.x,p.z,p.radius)?"water":HitsArchitecture(p.x,p.z,p.radius)?"architecture":null;
                if(reason==null&&index.Near(p.x,p.z,p.radius).Any(o=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(o.x,o.z))<p.radius+o.radius+.15f))reason="vegetation";
                if(reason==null&&!authored&&(Mathf.Abs(p.x-RoadmapLandscape.RoadX(p.z))<RoadmapLandscape.RoadHalfWidth(p.z)+p.radius+.35f||Reserved(p.x,p.z,p.radius)))reason="road-or-approach";
                if(reason!=null)
                {
                    if(authored)throw new InvalidOperationException("Authored vegetation footprint overlaps "+reason+": "+p.id);
                    PlacementRejects[reason]=PlacementRejects.TryGetValue(reason,out int count)?count+1:1;return false;
                }
                index.Add(p);result.Add(p);return true;
            }
            foreach(var item in Composition.instances.Where(i=>Assets[i.asset].wind).OrderBy(i=>i.id,StringComparer.Ordinal))
                Admit(new Plant{id=item.id,asset=item.asset,x=item.x,z=item.z,height=item.height,yaw=item.yaw,tree=Assets[item.asset].placementClass=="canopy",radius=PlantRadius(item.asset,item.height)},true);
            // World-wide admission precedes chunk splitting, including all authored canopies.
            // No neighbouring chunk can independently fill an already occupied crown footprint.
            var random=new System.Random(World.seed);
            for(float z=-24;z<240;z+=4.5f)for(float x=-43;x<44;x+=4.5f)
            {
                float px=x+(float)random.NextDouble()*3,pz=z+(float)random.NextDouble()*3;
                double chance=random.NextDouble();string asset=random.NextDouble()<.32?"tree_pineRoundA":"tree_default";
                float h=5.8f+(float)random.NextDouble()*4.2f,yaw=(float)random.NextDouble()*360,tint=.88f+(float)random.NextDouble()*.13f;
                if(chance>(low?.45:.78)||CanopyObscuresStory(px,pz))continue;
                Admit(new Plant{id="forest-"+x+"-"+z,asset=asset,x=px,z=pz,height=h,yaw=yaw,tint=tint,tree=true,radius=PlantRadius(asset,h)});
            }
            random=new System.Random(World.seed+421);
            for(float z=-24;z<235;z+=5.5f)for(float x=-32;x<33;x+=5.5f)
            {
                float px=x+(float)random.NextDouble()*3,pz=z+(float)random.NextDouble()*3;
                double chance=random.NextDouble();string asset=random.NextDouble()<.3?"tree_pineRoundA":"tree_default";
                float h=2.7f+(float)random.NextDouble()*1.4f,yaw=(float)random.NextDouble()*360;
                if(chance>(low?.08:.35)||CanopyObscuresStory(px,pz,true))continue;
                Admit(new Plant{id="young-"+x+"-"+z,asset=asset,x=px,z=pz,height=h,yaw=yaw,tint=.92f,tree=true,radius=PlantRadius(asset,h)});
            }
            random=new System.Random(World.seed+991);
            for(float z=-24;z<240;z+=1.8f)for(float x=-40;x<41;x+=1.8f)
            {
                float px=x+(float)random.NextDouble(),pz=z+(float)random.NextDouble();
                double chance=random.NextDouble();float height=.32f+(float)random.NextDouble()*.46f,yaw=(float)random.NextDouble()*360;
                float density=.22f+.7f*Mathf.PerlinNoise(px*.12f,pz*.15f);
                if(chance>density*(low?.5:1))continue;
                Admit(new Plant{id="shrub-"+x+"-"+z,asset="plant_bushSmall",x=px,z=pz,height=height,yaw=yaw,radius=PlantRadius("plant_bushSmall",height)});
            }
            random=new System.Random(World.seed+191);
            for(float z=-24;z<240;z+=1.3f)for(float x=-40;x<41;x+=1.3f)
            {
                float px=x+(float)random.NextDouble(),pz=z+(float)random.NextDouble();double chance=random.NextDouble();
                float h=.28f+(float)random.NextDouble()*.4f,yaw=(float)random.NextDouble()*360;
                if(chance>(low?.18:.37))continue;
                Admit(new Plant{id="tuft-"+x+"-"+z,asset="pilot.grass-tuft",x=px,z=pz,height=h,yaw=yaw,radius=h*.34f+.1f,tint=.85f+(float)random.NextDouble()*.18f});
            }
            for(float z=101;z<220;z+=low?5:3)for(int side=-1;side<=1;side+=2)
            {
                float x=RoadmapLandscape.RiverX(z)+side*(RoadmapLandscape.RiverWidth(z)+1.5f),h=1.05f+.3f*Mathf.Sin(z);
                Admit(new Plant{id="reeds-"+side+"-"+z,asset="pilot.reeds",x=x,z=z,height=h,yaw=z*17,radius=PlantRadius("pilot.reeds",h)});
            }
            return result;
        }
        static float ContactShade(float x,float z)
        {
            float shade=1;
            foreach(var p in ContactPlants.Near(x,z,3))
            {
                float d=Vector2.Distance(new Vector2(x,z),new Vector2(p.x,p.z));float reach=p.tree?p.radius*1.25f:p.radius*.9f;
                shade*=1-(p.tree?.23f:.09f)*Mathf.Pow(Mathf.Clamp01(1-d/reach),1.3f);
            }
            foreach(var item in Composition.instances.Where(i=>!Assets[i.asset].wind&&i.height>.8f))
            {
                var bounds=Model(item.asset).Bounds;var q=Quaternion.Euler(0,-item.yaw,0)*new Vector3(x-item.x,0,z-item.z)-bounds.center*item.height;
                float dx=Mathf.Max(0,Mathf.Abs(q.x)-bounds.extents.x*item.height),dz=Mathf.Max(0,Mathf.Abs(q.z)-bounds.extents.z*item.height);
                shade*=1-.2f*Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dz*dz)/1.3f);
            }
            return Mathf.Max(.68f,shade);
        }
        static void GrassTuft(Geometry g,Plant p)
        {
            var origin=new Vector3(p.x,Ground(p.x,p.z),p.z);var rotation=Quaternion.Euler(0,p.yaw,0);var root=new Vector4(p.x,p.z,origin.y,p.height);
            var color=new Color(.43f,.58f,.26f)*p.tint;
            for(int blade=0;blade<5;blade++)
            {
                float angle=blade*2.39996f;var outward=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var tangent=Vector3.Cross(outward,Vector3.up);
                Vector3 At(Vector3 v)=>origin+rotation*v*p.height;
                var basePoint=outward*.1f;var bend=outward*.25f+Vector3.up*(.5f+blade*.07f);var tip=outward*.32f+Vector3.up*(.65f+blade*.08f);
                g.Triangle(At(basePoint-tangent*.07f),At(bend+tangent*.035f),At(bend-tangent*.035f),color,root);
                g.Triangle(At(basePoint-tangent*.07f),At(basePoint+tangent*.07f),At(bend+tangent*.035f),color,root);
                g.Triangle(At(bend-tangent*.035f),At(bend+tangent*.035f),At(tip),color*1.08f,root);
            }
        }
        static bool CanopyObscuresStory(float x,float z,bool young=false)
        {
            // Preserve authored trees growing through ruins; only scatter respects these view corridors.
            // A tall foreground crown can hide a landmark despite a clear ground footprint.
            bool Covers(float tx,float tz,float width)
            {
                var anchor=World.waypoints.OrderBy(p=>Mathf.Abs(p.z-tz)).First();
                float yaw=anchor.yaw*Mathf.Deg2Rad;var towardsCamera=new Vector2(-Mathf.Sin(yaw),-Mathf.Cos(yaw));
                var delta=new Vector2(x-tx,z-tz);float front=Vector2.Dot(delta,towardsCamera);
                return front>-2&&front<(young?4:13)&&Mathf.Abs(Vector2.Dot(delta,new Vector2(towardsCamera.y,-towardsCamera.x)))<width+(young?1.2f:2.5f);
            }
            foreach(var node in Document.nodes)if(Covers(node.x,node.z,1))return true;
            foreach(var item in Composition.instances)
                if(item.asset=="ua_bus_shelter_mosaic"||item.asset=="pilot.ruined-house"||item.asset=="pilot.ruin-wall"||item.asset=="ua_well")
                    if(Covers(item.x,item.z,2.4f))return true;
            foreach(var item in Document.landmarks)
                if(!Assets[item.asset].wind&&item.height>1&&Covers(item.x,item.z,2.8f))return true;
            return false;
        }
        static bool Reserved(float x,float z,float margin)
        {
            if(Surfaces.Any(s=>SurfaceRecipes.Contains(s,x,z)||SurfaceRecipes.EdgeDistance(s,x,z)<margin+.35f))return true;
            if(Mathf.Abs(x-RoadmapLandscape.RoadX(z))<RoadmapLandscape.RoadHalfWidth(z)+margin)return true;
            foreach(var n in Document.nodes)if(Vector2.Distance(new Vector2(x,z),new Vector2(n.x,n.z))<n.radius+margin)return true;
            foreach(var instance in Composition.instances)
            {
                var asset=Assets[instance.asset];if(asset.wind)continue;
                float radius=asset.radius*instance.height/asset.height;
                if(Vector2.Distance(new Vector2(x,z),new Vector2(instance.x,instance.z))<radius+margin)return true;
            }
            foreach(var span in Composition.spans.Where(s=>s.height==0))
            {
                var a=new Vector2(span.ax,span.az);var b=new Vector2(span.bx,span.bz);
                float t=Mathf.Clamp01(Vector2.Dot(new Vector2(x,z)-a,b-a)/Mathf.Max(.001f,(b-a).sqrMagnitude));
                if(Vector2.Distance(new Vector2(x,z),Vector2.Lerp(a,b,t))<.45f+margin)return true;
            }
            foreach(var l in Document.landmarks)if(!Assets[l.asset].wind&&Vector2.Distance(new Vector2(x,z),new Vector2(l.x,l.z))<2+margin)return true;
            return false;
        }
        static void TerrainGrid(Geometry g,float start,float end,float step,bool distant=false)
        {
            for(float z=start;z<end;z+=step)for(float x=World.minX;x<World.maxX;x+=step)
            {
                float xx=Mathf.Min(x+step,World.maxX),zz=Mathf.Min(z+step,end);
                // The simplified distant terrain must never seal the live river with a
                // coarse triangle spanning both banks. Detailed chunks own its channel bed.
                if(distant&&zz>88&&new[]{z,(z+zz)*.5f,zz}.Any(az=>x<RoadmapLandscape.RiverX(az)+RoadmapLandscape.RiverWidth(az)+3&&xx>RoadmapLandscape.RiverX(az)-RoadmapLandscape.RiverWidth(az)-3))continue;
                Vector3 At(float a,float b)=>new Vector3(a,Ground(a,b),b);
                Color ColorAt(float a,float b)
                {
                    var color=RoadmapLandscape.GroundColor(a,b);
                    foreach(var span in Composition.spans.Where(s=>s.height==0))
                    {
                        var p=new Vector2(a,b);var from=new Vector2(span.ax,span.az);var to=new Vector2(span.bx,span.bz);
                        float t=Mathf.Clamp01(Vector2.Dot(p-from,to-from)/Mathf.Max(.001f,(to-from).sqrMagnitude));
                        float d=Vector2.Distance(p,Vector2.Lerp(from,to,t));
                        color=Color.Lerp(color,new Color(.59f,.53f,.39f),Mathf.Clamp01(1-d)*.8f);
                    }
                    return color*ContactShade(a,b);
                }
                g.Triangle(At(x,z),At(x,zz),At(xx,z),ColorAt(x,z),second:ColorAt(x,zz),third:ColorAt(xx,z));
                g.Triangle(At(xx,z),At(x,zz),At(xx,zz),ColorAt(xx,z),second:ColorAt(x,zz),third:ColorAt(xx,zz));
            }
        }
        // Clip every paved decal to the exact Low/Balanced terrain triangles.
        // Analytic height samples alone can sit below a coarse rendered triangle.
        static void GroundDecal(Geometry g,Vector2[] polygon,Color color,float start,float end,float step,float lift=.04f)
        {
            float area=0;for(int i=0;i<polygon.Length;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Length];area+=a.x*b.y-b.x*a.y;}
            float sign=Mathf.Sign(area),minX=polygon.Min(p=>p.x),maxX=polygon.Max(p=>p.x),minZ=Mathf.Max(start,polygon.Min(p=>p.y)),maxZ=Mathf.Min(end,polygon.Max(p=>p.y));
            if(maxZ<=minZ)return;
            float firstX=World.minX+Mathf.Floor((minX-World.minX)/step)*step,firstZ=start+Mathf.Floor((minZ-start)/step)*step;
            void Clip(Vector3 a,Vector3 b,Vector3 c)
            {
                var vertices=new List<Vector3>{a,b,c};
                for(int edge=0;edge<polygon.Length&&vertices.Count>0;edge++)
                {
                    var from=polygon[edge];var to=polygon[(edge+1)%polygon.Length];
                    float Distance(Vector3 v)=>sign*((to.x-from.x)*(v.z-from.y)-(to.y-from.y)*(v.x-from.x));
                    var next=new List<Vector3>();var previous=vertices[vertices.Count-1];float previousDistance=Distance(previous);
                    foreach(var current in vertices)
                    {
                        float distance=Distance(current);bool inside=distance>=0,wasInside=previousDistance>=0;
                        if(inside!=wasInside)next.Add(Vector3.LerpUnclamped(previous,current,previousDistance/(previousDistance-distance)));
                        if(inside)next.Add(current);previous=current;previousDistance=distance;
                    }
                    vertices=next;
                }
                for(int i=1;i+1<vertices.Count;i++)
                    if(Vector3.Cross(vertices[i]-vertices[0],vertices[i+1]-vertices[0]).sqrMagnitude>1e-12f)
                        g.Triangle(vertices[0]+Vector3.up*lift,vertices[i]+Vector3.up*lift,vertices[i+1]+Vector3.up*lift,color);
            }
            for(float z=firstZ;z<maxZ;z+=step)for(float x=firstX;x<maxX;x+=step)
            {
                float xx=Mathf.Min(x+step,World.maxX),zz=Mathf.Min(z+step,end);
                Vector3 At(float a,float b)=>new Vector3(a,Ground(a,b),b);
                Clip(At(x,z),At(x,zz),At(xx,z));Clip(At(xx,z),At(x,zz),At(xx,zz));
            }
        }
        static float NativeTerrainHeight(float x,float z,float start,float end,float step)
        {
            float a=World.minX+Mathf.Floor((x-World.minX)/step)*step,b=start+Mathf.Floor((z-start)/step)*step;
            float xx=Mathf.Min(a+step,World.maxX),zz=Mathf.Min(b+step,end),u=(x-a)/(xx-a),v=(z-b)/Mathf.Max(.001f,zz-b);
            return u+v<=1?Ground(a,b)*(1-u-v)+Ground(xx,b)*u+Ground(a,zz)*v
                :Ground(xx,zz)*(u+v-1)+Ground(a,zz)*(1-u)+Ground(xx,b)*(1-v);
        }
        static void Road(Geometry g,float start,float end,float step)
        {
            var busBay=Surfaces.Single(s=>s.kind=="bus-bay");float bayStart=busBay.points.Min(p=>p.z),bayEnd=busBay.points.Max(p=>p.z);
            Vector2 Edge(float fraction,float along)=>new Vector2(RoadmapLandscape.RoadX(along)+RoadmapLandscape.RoadHalfWidth(along)*fraction,along);
            for(float z=Mathf.Max(-50,start);z<Mathf.Min(232,end);z+=2)
            {
                float next=Mathf.Min(z+2,end);bool asphalt=z<38;float reclaimed=Mathf.SmoothStep(0,1,Mathf.InverseLerp(20,38,z));
                var asphaltColor=new Color(.27f,.29f,.30f);var soil=new Color(.55f,.50f,.36f);
                Color color=asphalt?Color.Lerp(asphaltColor,soil,reclaimed):soil;
                int strips=asphalt?2:4;
                for(int strip=0;strip<strips;strip++)
                {
                    float a=-1+2f*strip/strips,b=-1+2f*(strip+1)/strips;
                    var shade=color*(.93f+.07f*Mathf.PerlinNoise(z*.21f,strip*3.3f));
                    if(!asphalt&&(strip==1||strip==2))shade=Color.Lerp(shade,new Color(.42f,.49f,.27f),.4f);
                    GroundDecal(g,new[]{Edge(a,z),Edge(a,next),Edge(b,next),Edge(b,z)},shade,start,end,step);
                }
                if(asphalt)foreach(int side in new[]{-1,1})
                {
                    // The left shoulder opens into the owned bay instead of crossing its mouth.
                    if(side<0&&z>=bayStart&&z<bayEnd)continue;
                    float outer=.75f*(1-reclaimed)+.25f*reclaimed;
                    var a=Edge(side,z);var b=Edge(side,next);
                    GroundDecal(g,new[]{a,b,b+Vector2.right*side*outer,a+Vector2.right*side*outer},new Color(.53f,.52f,.44f),start,end,step,.025f);
                }
                if(asphalt&&z<32)
                {
                    var paint=Color.Lerp(new Color(.72f,.71f,.61f),color,reclaimed*.7f);
                    Vector2 At(float offset,float along)=>new Vector2(RoadmapLandscape.RoadX(along)+offset,along);
                    if(Mathf.RoundToInt(z)%6==0)
                        GroundDecal(g,new[]{At(-.10f,z),At(-.10f,z+1.8f),At(.10f,z+1.8f),At(.10f,z)},paint,start,end,step,.055f);
                    foreach(int side in new[]{-1,1})
                    {
                        if(side<0&&z>=bayStart&&z<bayEnd&&Mathf.RoundToInt(z)%6!=0)continue;
                        var a=Edge(side*.95f,z);var b=Edge(side*.95f,next-.10f);
                        GroundDecal(g,new[]{a,b,b-Vector2.right*side*.14f,a-Vector2.right*side*.14f},paint*.85f,start,end,step,.055f);
                    }
                    if(Mathf.RoundToInt(z)%14==0)
                        GroundDecal(g,new[]{At(-2.5f,z+.6f),At(1.5f,z+.94f),At(1.5f,z+1.03f),At(-2.5f,z+.69f)},color*.8f,start,end,step,.056f);
                }
            }
            foreach(var span in Composition.spans.Where(s=>s.height==0&&s.a!="last-stop/bench"))
            {
                var from=new Vector2(span.ax,span.az);var to=new Vector2(span.bx,span.bz);var side=new Vector2(-(to-from).y,(to-from).x).normalized*.6f;
                GroundDecal(g,new[]{from-side,to-side,to+side,from+side},new Color(.53f,.51f,.40f),start,end,step,.045f);
            }
            foreach(var surface in Surfaces)
            {
                var polygon=surface.points.Select(p=>new Vector2(p.x,p.z)).ToArray();
                bool bay=surface.kind=="bus-bay";var color=bay?new Color(.31f,.32f,.31f):new Color(.59f,.58f,.50f);
                GroundDecal(g,polygon,color,start,end,step,bay?.041f:.048f);
                if(bay)continue;
                // Broken low curb on the edge facing traffic: subtle geometry, one material.
                int closest=Enumerable.Range(0,polygon.Length).OrderBy(i=>Mathf.Abs((polygon[i].x+polygon[(i+1)%polygon.Length].x)*.5f)).First();
                var from=polygon[closest];var to=polygon[(closest+1)%polygon.Length];int segments=Mathf.CeilToInt(Vector2.Distance(from,to)/1.4f);
                for(int i=0;i<segments;i++)
                {
                    if(i%4==2)continue;var a=Vector2.Lerp(from,to,i/(float)segments);var b=Vector2.Lerp(from,to,(i+.88f)/segments);
                    if((a.y+b.y)*.5f<start||(a.y+b.y)*.5f>=end)continue;
                    var side=new Vector2(-(b-a).y,(b-a).x).normalized*.10f;
                    Vector3 At(Vector2 p,float y)=>new Vector3(p.x,NativeTerrainHeight(p.x,p.y,start,end,step)+y,p.y);
                    var a0=At(a-side,.04f);var a1=At(a+side,.04f);var b0=At(b-side,.04f);var b1=At(b+side,.04f);var up=Vector3.up*.14f;var tint=new Color(.63f,.62f,.55f);
                    g.Triangle(a0+up,b0+up,a1+up,tint);g.Triangle(a1+up,b0+up,b1+up,tint);
                    g.Triangle(a0,b0,a0+up,tint*.83f);g.Triangle(a0+up,b0,b0+up,tint*.83f);
                    g.Triangle(b1,a1,b1+up,tint*.83f);g.Triangle(b1+up,a1,a1+up,tint*.83f);
                }
            }
        }
        static Mesh Horizon()
        {
            var g=new Geometry();
            for(float z=World.minZ;z<World.maxZ;z+=10)foreach(float x in new[]{-56f,56f})
                Append(g,z%20==0?"tree_pineRoundA":"tree_default",x+1.5f*Mathf.Sin(z),z,7+2*Mathf.Sin(z*.3f),z,false,.83f);
            return g.Mesh("Distant valley silhouette");
        }
        sealed class WaterDefinition
        {
            public string shaderAsset,waveProfile;
            public Dictionary<string,string> textures;
            public Dictionary<string,float> floats;
            public Dictionary<string,float[]> colors,vectors;
            public string[] keywords;
            public float longitudinalStep,bankOverlap;
            public int crossSegments;
        }
        static Material RiverMaterial()
        {
            var source=Read<WaterDefinition>("water.json");
            var shader=AssetDatabase.LoadAssetAtPath<Shader>(source.shaderAsset);
            if(shader==null||!shader.isSupported)throw new InvalidOperationException("Stylized Water 3 shader unavailable");
            var material=new Material(shader){name="Living valley river"};
            foreach(var pair in source.floats)material.SetFloat(pair.Key,pair.Value);
            foreach(var pair in source.colors)material.SetColor(pair.Key,new Color(pair.Value[0],pair.Value[1],pair.Value[2],pair.Value[3]));
            foreach(var pair in source.vectors)material.SetVector(pair.Key,new Vector4(pair.Value[0],pair.Value[1],pair.Value[2],pair.Value[3]));
            foreach(var pair in source.textures)
            {
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(pair.Value);
                if(texture==null)throw new InvalidOperationException("River texture unavailable: "+pair.Value);
                material.SetTexture(pair.Key,texture);
            }
            var profile=AssetDatabase.LoadAllAssetsAtPath(source.waveProfile).OfType<Texture2D>().FirstOrDefault();
            if(profile==null)throw new InvalidOperationException("River wave profile lookup table unavailable");
            material.SetTexture("_WaveProfile",profile);
            foreach(var keyword in source.keywords)material.EnableKeyword(keyword);
            return material;
        }
        static void Water(Geometry g,float start,float end)
        {
            var source=Read<WaterDefinition>("water.json");
            // Overlap rising banks: scene depth, rather than the mesh border, defines the shoreline.
            // Continuous metric UVs follow the curved channel for downstream normals and foam.
            Vector2 UV(Vector3 p)=>new Vector2((p.x-RoadmapLandscape.RiverX(p.z))*.4f,p.z*.4f);
            for(float z=start;z<end;z+=source.longitudinalStep)
            {
                float zz=Mathf.Min(z+source.longitudinalStep,end);
                for(int side=0;side<source.crossSegments;side++)
                {
                    Vector3 At(float p,float t)=>new Vector3(RoadmapLandscape.RiverX(t)+(RoadmapLandscape.RiverWidth(t)+source.bankOverlap)*(p*2/source.crossSegments-1),RoadmapLandscape.WaterHeight,t);
                    g.Triangle(At(side,z),At(side,zz),At(side+1,z),Color.clear,uv:UV);
                    g.Triangle(At(side+1,z),At(side,zz),At(side+1,zz),Color.clear,uv:UV);
                }
            }
        }
        static void Conductor(Geometry g,LooseConductor wire)
        {
            var a=new Vector3(wire.ax,Ground(wire.ax,wire.az)+wire.ay,wire.az);
            var b=new Vector3(wire.bx,Ground(wire.bx,wire.bz)+wire.by,wire.bz);
            Vector3 Point(float t)
            {
                var p=Vector3.Lerp(a,b,t);p.y=Mathf.Max(Ground(p.x,p.z)+.04f,p.y-wire.slack*4*t*(1-t));return p;
            }
            for(int i=0;i<18;i++)
            {
                var from=Point(i/18f);var to=Point((i+1)/18f);var side=Vector3.Cross(to-from,Vector3.up).normalized*.022f;
                g.Triangle(from-side,to-side,to+side,new Color(.24f,.26f,.24f));g.Triangle(from-side,to+side,from+side,new Color(.24f,.26f,.24f));
                g.Triangle(to+side,to-side,from-side,new Color(.24f,.26f,.24f));g.Triangle(from+side,to+side,from-side,new Color(.24f,.26f,.24f));
            }
        }
        static void Append(Geometry g,string asset,float x,float z,float height,float yaw,bool wind,float tint=1)
        {
            var model=Donors.TryGetValue(asset,out var donor)?donor:Library.Get(asset=="pilot.ruined-house"?"ua_whitewashed_house":asset);
            if(model==null)throw new InvalidOperationException("Missing actual model "+asset);
            var rotation=Quaternion.Euler(0,yaw,0);float y=Ground(x,z);
            if(asset=="ua_plank_bridge"||asset=="pilot.broken-bridge"||asset=="ua_dam_breached")y=Mathf.Max(y,RoadmapLandscape.WaterHeight+.12f);
            var origin=new Vector3(x,y,z);var root=new Vector4(x,z,y,wind?height:-1);
            for(int i=0;i<model.Positions.Length;i+=3)
            {
                var center=(model.Positions[i]+model.Positions[i+1]+model.Positions[i+2])/3;
                // Authored damage variant: a real opening lets the owned tree grow through the roof.
                if(asset=="pilot.ruined-house"&&center.y>.62f&&center.x>-.16f&&Mathf.Abs(center.z)<.6f)continue;
                Vector3 At(int k)=>origin+rotation*model.Positions[k]*height;
                var color=model.Colors[i/3];if(wind&&color.g>color.r)color=Color.Lerp(color,new Color(.42f,.54f,.27f),.36f);
                g.Triangle(At(i),At(i+1),At(i+2),color*tint,root,rotation*model.Normals[i]);
            }
            if(asset=="ua_bus_shelter_mosaic")Mosaic(g,model,origin,height,rotation);
        }
        static void Mosaic(Geometry g,RoadmapModelLibrary.Model model,Vector3 origin,float height,Quaternion yaw)
        {
            // D12's palette wraps both end walls, so the road-facing orientation
            // retains a visible mosaic from the guided southern camera.
            float rear=float.MinValue,right=float.MinValue,leftFace=float.MaxValue;var bounds=model.Bounds;
            for(int i=0;i<model.Positions.Length;i+=3)
            {
                var color=model.Colors[i/3];if(color.r<.72f||color.g<.68f)continue;
                var center=(model.Positions[i]+model.Positions[i+1]+model.Positions[i+2])/3;
                if(model.Normals[i].z<-.8f)rear=Mathf.Max(rear,center.z);
                if(model.Normals[i].x>.8f)right=Mathf.Max(right,center.x);
                if(model.Normals[i].x<-.8f)leftFace=Mathf.Min(leftFace,center.x);
            }
            if(rear==float.MinValue||right==float.MinValue||leftFace==float.MaxValue)throw new InvalidOperationException("Mosaic wall binding missing");
            for(int row=0;row<5;row++)for(int column=0;column<13;column++)
            {
                float bottom=bounds.min.y+bounds.size.y*(.35f+row*.06f),top=bottom+bounds.size.y*.055f;
                float left=bounds.center.x-bounds.size.x*.35f+column*bounds.size.x*.7f/13,end=left+bounds.size.x*.7f/13*.94f;
                var color=column%4==2||row>1&&(column+row)%4==0?new Color(.78f,.67f,.36f):row==0?new Color(.40f,.57f,.54f):new Color(.35f,.52f,.63f);
                Vector3 At(float x,float y,float z)=>origin+yaw*new Vector3(x,y,z)*height;
                g.Triangle(At(left,bottom,rear-.007f),At(end,top,rear-.007f),At(end,bottom,rear-.007f),color);
                g.Triangle(At(left,bottom,rear-.007f),At(left,top,rear-.007f),At(end,top,rear-.007f),color);
                float side=bounds.center.z-bounds.size.z*.32f+column*bounds.size.z*.64f/13,sideEnd=side+bounds.size.z*.64f/13*.94f;
                g.Triangle(At(right+.007f,bottom,side),At(right+.007f,top,side),At(right+.007f,top,sideEnd),color);
                g.Triangle(At(right+.007f,bottom,side),At(right+.007f,top,sideEnd),At(right+.007f,bottom,sideEnd),color);
                g.Triangle(At(leftFace-.007f,bottom,side),At(leftFace-.007f,top,sideEnd),At(leftFace-.007f,top,side),color);
                g.Triangle(At(leftFace-.007f,bottom,side),At(leftFace-.007f,bottom,sideEnd),At(leftFace-.007f,top,sideEnd),color);
            }
        }
        static RoadmapModelLibrary.Model ReadDonor(Binding binding)
        {
            // Prefabs own the reviewed mesh and palette. FBX roots may contain overlapping
            // LODs, collision meshes and unbound materials; none belongs in a baked landmark.
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(binding.prefab);if(source==null)throw new InvalidOperationException(binding.prefab);
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();
            var selected=new HashSet<Renderer>();
            foreach(var group in source.GetComponentsInChildren<LODGroup>(true))
            {
                var lods=group.GetLODs();if(binding.lod>=lods.Length)throw new InvalidOperationException("Unavailable reviewed LOD: "+binding.asset);
                foreach(var renderer in lods[binding.lod].renderers)selected.Add(renderer);
            }
            if(selected.Count==0)foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>(true))selected.Add(renderer);
            var textures=new Dictionary<Texture,Texture2D>();var meshes=new List<string>();var materials=new HashSet<string>();
            try
            {
                foreach(var renderer in selected.OrderBy(r=>r.name,StringComparer.Ordinal))
                {
                    var filter=renderer.GetComponent<MeshFilter>();var mesh=filter?.sharedMesh;if(mesh==null)continue;
                    if(binding.lod>0&&source.GetComponentsInChildren<LODGroup>(true).Length==0)
                    {
                        string lodName=System.Text.RegularExpressions.Regex.Replace(mesh.name,"LOD[0-9]+","LOD"+binding.lod);
                        mesh=AssetDatabase.LoadAllAssetsAtPath(binding.source).OfType<Mesh>().FirstOrDefault(m=>m.name==lodName);
                        if(mesh==null||!mesh.name.Contains("LOD"+binding.lod))throw new InvalidOperationException("Unavailable prefab donor LOD: "+binding.asset);
                    }
                    if(AssetDatabase.GetAssetPath(mesh)!=binding.source)throw new InvalidOperationException("Prefab mesh is not the reviewed source: "+binding.asset);
                    meshes.Add(mesh.name);var matrix=source.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    var ns=mesh.normals;var vs=mesh.vertices;var uv=mesh.uv;
                    for(int sub=0;sub<mesh.subMeshCount;sub++)
                    {
                        var material=renderer.sharedMaterials[sub];if(material==null)throw new InvalidOperationException("Unbound donor palette: "+binding.asset);
                        materials.Add(AssetDatabase.GetAssetPath(material));
                        // Texture-cutout foliage needs a different rendering path. Do not turn
                        // transparent leaf cards into solid triangles in the faceted forest.
                        if(material.IsKeywordEnabled("_ALPHATEST_ON"))throw new InvalidOperationException("Cutout donor requires its own reviewed material: "+binding.asset);
                        var tint=material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material.HasProperty("_Color")?material.GetColor("_Color"):Color.white;
                        // Read the authored slot, including older vendor shaders whose fallback
                        // exposes a default white main texture rather than their saved atlas.
                        var saved=new SerializedObject(material).FindProperty("m_SavedProperties.m_TexEnvs");
                        Texture texture=null;Vector2 textureScale=Vector2.one,textureOffset=Vector2.zero;
                        foreach(string property in new[]{"_Albedo","_BaseMap","_MainTex"})
                        {
                            for(int slot=0;slot<saved.arraySize;slot++)
                            {
                                var entry=saved.GetArrayElementAtIndex(slot);if(entry.FindPropertyRelative("first").stringValue!=property)continue;
                                var value=entry.FindPropertyRelative("second");var candidate=value.FindPropertyRelative("m_Texture").objectReferenceValue as Texture;
                                if(candidate==null)continue;
                                texture=candidate;textureScale=value.FindPropertyRelative("m_Scale").vector2Value;textureOffset=value.FindPropertyRelative("m_Offset").vector2Value;break;
                            }
                            if(texture!=null)break;
                        }
                        Texture2D palette=null;
                        if(texture!=null&&!textures.TryGetValue(texture,out palette))
                        {palette=ReadPalette(texture);textures.Add(texture,palette);}
                        var indices=mesh.GetTriangles(sub);
                        for(int t=0;t<indices.Length;t+=3)
                        {
                            var color=tint;
                            if(palette!=null&&uv.Length==vs.Length)
                            {
                                var coordinate=(uv[indices[t]]+uv[indices[t+1]]+uv[indices[t+2]])/3;
                                coordinate=Vector2.Scale(coordinate,textureScale)+textureOffset;
                                // Project-owned vertex palettes are authored display colors too.
                                // Readback is linear; bring the atlas into that common authoring palette.
                                color*=palette.GetPixelBilinear(coordinate.x,coordinate.y).gamma;
                            }
                            colors.Add(new Color(color.r,color.g,color.b,1));
                            for(int k=0;k<3;k++)
                            {int i=indices[t+k];vertices.Add(matrix.MultiplyPoint3x4(vs[i]));normals.Add(matrix.inverse.transpose.MultiplyVector(ns[i]).normalized);}
                        }
                    }
                }
            }
            finally{foreach(var texture in textures.Values)Object.DestroyImmediate(texture);}
            if(vertices.Count==0)throw new InvalidOperationException("Empty donor "+binding.asset);
            var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);
            float h=Mathf.Max(.01f,bounds.size.y);var pivot=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            DonorReviews.Add(new{binding.asset,binding.catalogueId,binding.source,binding.prefab,binding.lod,meshes,triangles=vertices.Count/3,
                paletteMaterials=materials.OrderBy(p=>p).ToArray(),paletteColors=colors.Select(c=>ColorUtility.ToHtmlStringRGB(c)).Distinct().Count(),
                bounds=new[]{bounds.size.x,bounds.size.y,bounds.size.z}});
            return new RoadmapModelLibrary.Model{id=binding.asset,Positions=vertices.Select(v=>(v-pivot)/h).ToArray(),Normals=normals.ToArray(),Colors=colors.ToArray(),Bounds=new Bounds((bounds.center-pivot)/h,bounds.size/h),SourceHeight=h};
        }
        static Texture2D ReadPalette(Texture texture)
        {
            var previous=RenderTexture.active;var target=RenderTexture.GetTemporary(texture.width,texture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            try
            {
                Graphics.Blit(texture,target);RenderTexture.active=target;
                var pixels=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false,true){wrapMode=texture.wrapMode};
                pixels.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);pixels.Apply();return pixels;
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
    }
}
