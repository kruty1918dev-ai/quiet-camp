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
        sealed class Binding { public string asset,catalogueId,source,sourceHash,guid; public int lod; }
        static SourceWorld World;
        static SceneCompositionDocument Document;
        static CompositionResult Composition;
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
                "Assets/QuietCamp/Scripts/Presentation/UI/RoadmapModelLibrary.cs",
                "Assets/QuietCamp/Resources/QuietCamp/RoadmapLit.shader",
                "Assets/QuietCamp/Resources/QuietCamp/roadmap_models.json",
                "Assets/QuietCamp/Resources/QuietCamp/roadmap_culture_models.json",
                "Assets/ThirdParty/Stylized Water 3/Shaders/StylizedWater3_Standard.watershader3",
                "Assets/ThirdParty/Stylized Water 3/Shaders/StylizedWater3_Standard.watershader3.meta",
                "Assets/ThirdParty/Stylized Water 3/Profiles/River Wave Profile.asset",
                "Assets/ThirdParty/Stylized Water 3/Materials/Textures/Normals/LowpolyWaves.png",
                "Assets/ThirdParty/Stylized Water 3/Materials/Textures/IntersectionNoise.png",
                "Assets/ThirdParty/Stylized Water 3/Materials/Textures/Foam/Foam1.png",
                "Assets/QuietCamp/Authoring/Roadmap/Models/EnvironmentKit/models.json"});
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
            Library=RoadmapModelLibrary.Load();Library.PrepareCulture();Library.PrepareStaging();
            Donors=new Dictionary<string,RoadmapModelLibrary.Model>();
            foreach(var b in Read<Binding[]>("bindings.json"))
            {
                if(Hash(File.ReadAllBytes(b.source))!=b.sourceHash||AssetDatabase.AssetPathToGUID(b.source)!=b.guid)
                    throw new InvalidOperationException("Stale reviewed model binding: "+b.catalogueId);
                Donors.Add(b.asset,ReadDonor(b));
            }
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
                    .Concat(new[]{"tree_default","tree_pineRoundA","grass","plant_bushSmall","stone_largeA","log","stump_round"}).Distinct().OrderBy(s=>s).ToArray();
                chunk.estimatedBytes=chunk.balanced.Concat(chunk.low).Where(m=>m!=null).Sum(m=>(long)m.vertexCount*52+m.GetIndexCount(0)*4);
                string file=folder+"/chunk-"+c+".asset";AssetDatabase.CreateAsset(chunk,file);
                foreach(var m in chunk.balanced.Concat(chunk.low).Where(m=>m!=null))AssetDatabase.AddObjectToAsset(m,chunk);
                index.chunks[c]="QuietCamp/CinematicRoadmap/"+Revision.Substring(0,16)+"/chunk-"+c;
                stats.Add(new {index=c,chunk.estimatedBytes,balancedTriangles=chunk.balanced.Where(m=>m!=null).Sum(m=>(long)m.GetIndexCount(0)/3),lowTriangles=chunk.low.Where(m=>m!=null).Sum(m=>(long)m.GetIndexCount(0)/3),chunk.sourceAssets});
            }
            index.horizon=Horizon();AssetDatabase.CreateAsset(index.horizon,folder+"/Horizon.asset");
            index.distantForest=new Mesh[index.chunks.Length];
            for(int c=0;c<index.chunks.Length;c++)
            {
                index.distantForest[c]=FarForest(c);AssetDatabase.CreateAsset(index.distantForest[c],folder+"/Distant-"+c+".asset");
            }
            var river=new Geometry();Water(river,96,World.maxZ);index.river=river.Mesh("Continuous valley river");index.river.RecalculateTangents();
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
            File.WriteAllText(Path.Combine(evidence,"bake-receipt.json"),JsonConvert.SerializeObject(new {sourceHash=Revision,compiler=SceneComposer.Revision,unity=UnityEngine.Application.unityVersion,composition=Composition,stats,levelIds=index.waypoints.Select(p=>p.levelId),playerBuild=false},Formatting.Indented)+"\n");
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
            var parts=Enumerable.Range(0,5).Select(i=>new Geometry()).ToArray();float start=World.chunkStarts[c],end=World.chunkEnds[c];
            TerrainGrid(parts[0],start,end,low?4:2);
            // The composer emits both ensemble roles and accepted landmarks.
            // Appending Document.landmarks again duplicates their geometry and shadows.
            foreach(var item in Composition.instances.Where(i=>OwnerChunk(i.z)==c))Append(parts[Assets[item.asset].wind?2:1],item.asset,item.x,item.z,item.height,item.yaw,Assets[item.asset].wind);
            var random=new System.Random(World.seed+c*811);
            for(float z=Mathf.Max(-22,start)+2;z<Mathf.Min(195,end);z+=4.5f)for(float x=-43;x<44;x+=4.5f)
            {
                float px=x+(float)random.NextDouble()*3,pz=z+(float)random.NextDouble()*3;
                if(random.NextDouble()>(low?.33:.60)||Reserved(px,pz,2.5f)||CanopyObscuresStory(px,pz)||RoadmapLandscape.WaterDistance(px,pz)<1)continue;
                string asset=random.NextDouble()<.36?"tree_pineRoundA":"tree_default";
                Append(parts[2],asset,px,pz,5.8f+(float)random.NextDouble()*5,(float)random.NextDouble()*360,true,.84f+(float)random.NextDouble()*.21f);
            }
            // A second generation of trees closes the transition strips without
            // hiding the story silhouettes. These are the same faceted gameplay meshes.
            random=new System.Random(World.seed+c*421);
            for(float z=Mathf.Max(-22,start)+2;z<Mathf.Min(195,end);z+=5.5f)for(float x=-29;x<30;x+=5.5f)
            {
                float px=x+(float)random.NextDouble()*3,pz=z+(float)random.NextDouble()*3;
                if(random.NextDouble()>(low?.035:.23)||Reserved(px,pz,.8f)||CanopyObscuresStory(px,pz,true)||RoadmapLandscape.WaterDistance(px,pz)<.5f)continue;
                Append(parts[2],random.NextDouble()<.3?"tree_pineRoundA":"tree_default",px,pz,2.7f+(float)random.NextDouble()*1.6f,(float)random.NextDouble()*360,true,.86f);
            }
            random=new System.Random(World.seed+c*733);
            for(float z=Mathf.Max(-22,start)+3;z<Mathf.Min(195,end);z+=7)for(float x=-27;x<28;x+=7)
            {
                float px=x+(float)random.NextDouble()*4,pz=z+(float)random.NextDouble()*4;
                if(random.NextDouble()>(low?.04:.17)||Reserved(px,pz,.5f)||RoadmapLandscape.WaterDistance(px,pz)<.3f)continue;
                double kind=random.NextDouble();string asset=kind<.72?"stone_largeA":kind<.9?"stump_round":"log";
                Append(parts[1],asset,px,pz,asset=="log"?.4f:.25f+(float)random.NextDouble()*.5f,(float)random.NextDouble()*360,false,.87f);
            }
            random=new System.Random(World.seed+c*191);
            for(float z=Mathf.Max(-22,start)+.8f;z<Mathf.Min(195,end);z+=1.8f)for(float x=-28;x<29;x+=1.8f)
            {
                float px=x+(float)random.NextDouble()*1.5f,pz=z+(float)random.NextDouble()*1.5f;
                if(random.NextDouble()>(low?.09:.16)||Reserved(px,pz,.38f)||RoadmapLandscape.WaterDistance(px,pz)<.05f)continue;
                Append(parts[3],"grass",px,pz,.24f+(float)random.NextDouble()*.42f,(float)random.NextDouble()*360,true,.8f+(float)random.NextDouble()*.23f);
            }
            // Dense low silhouette layers use the game's sixteen-triangle shrubs.
            // They join the tree groups and reclaim the road verges without multiplying draw calls.
            random=new System.Random(World.seed+c*991);
            for(float z=Mathf.Max(-22,start)+1;z<Mathf.Min(195,end);z+=low?3:1.8f)for(float x=-34;x<35;x+=low?3:1.8f)
            {
                float px=x+(float)random.NextDouble(),pz=z+(float)random.NextDouble();
                float density=.2f+.65f*Mathf.PerlinNoise(px*.15f,pz*.12f);
                if(random.NextDouble()>density||Reserved(px,pz,.25f)||RoadmapLandscape.WaterDistance(px,pz)<.1f)continue;
                Append(parts[3],"plant_bushSmall",px,pz,.2f+(float)random.NextDouble()*.45f,(float)random.NextDouble()*360,true);
            }
            for(float z=Mathf.Max(101,start)+1;z<Mathf.Min(197,end);z+=low?5:3)
                for(int side=-1;side<=1;side+=2)
                {
                    float x=RoadmapLandscape.RiverX(z)+side*(RoadmapLandscape.RiverWidth(z)+.6f);
                    if(!Reserved(x,z,.3f))Append(parts[3],"pilot.reeds",x,z,1.1f+.4f*Mathf.Sin(z),z*17,true);
                }
            foreach(var wire in World.looseConductors)if(OwnerChunk((wire.az+wire.bz)*.5f)==c)Conductor(parts[1],wire);
            return parts.Select((p,i)=>p.Mesh("Valley "+c+" / "+(low?"low":"balanced")+" / "+i)).ToArray();
        }
        static Mesh FarForest(int chunk)
        {
            var g=new Geometry();var random=new System.Random(World.seed+chunk*811);
            for(float z=Mathf.Max(-22,World.chunkStarts[chunk])+2;z<Mathf.Min(195,World.chunkEnds[chunk]);z+=4.5f)
                for(float x=-43;x<44;x+=4.5f)
                {
                    float px=x+(float)random.NextDouble()*3,pz=z+(float)random.NextDouble()*3;
                    if(random.NextDouble()>.60||Reserved(px,pz,2.5f)||CanopyObscuresStory(px,pz)||RoadmapLandscape.WaterDistance(px,pz)<1)continue;
                    bool pine=random.NextDouble()<.36;float h=5.8f+(float)random.NextDouble()*5;
                    random.NextDouble();random.NextDouble();
                    float y=Ground(px,pz);var tip=new Vector3(px,y+h,pz);var color=pine?new Color(.27f,.39f,.29f):new Color(.39f,.49f,.27f);
                    for(int side=0;side<4;side++)
                    {
                        float a=side*Mathf.PI*.5f,b=(side+1)*Mathf.PI*.5f;
                        var first=new Vector3(px+Mathf.Cos(a)*2,y+h*.43f,pz+Mathf.Sin(a)*2);
                        var second=new Vector3(px+Mathf.Cos(b)*2,y+h*.43f,pz+Mathf.Sin(b)*2);
                        g.Triangle(first,tip,second,color);
                    }
                }
            return g.Mesh("Simplified distant forest "+chunk);
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
            if(Mathf.Abs(x-RoadmapLandscape.RoadX(z))<1.1f+margin)return true;
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
        static void TerrainGrid(Geometry g,float start,float end,float step)
        {
            for(float z=start;z<end;z+=step)for(float x=World.minX;x<World.maxX;x+=step)
            {
                float xx=Mathf.Min(x+step,World.maxX),zz=Mathf.Min(z+step,end);
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
                    return color;
                }
                g.Triangle(At(x,z),At(x,zz),At(xx,z),ColorAt(x,z),second:ColorAt(x,zz),third:ColorAt(xx,z));
                g.Triangle(At(xx,z),At(x,zz),At(xx,zz),ColorAt(xx,z),second:ColorAt(x,zz),third:ColorAt(xx,zz));
            }
        }
        static Mesh Horizon()
        {
            var g=new Geometry();TerrainGrid(g,World.minZ,World.maxZ,10);
            for(int i=0;i<g.vertices.Count;i++)g.vertices[i]+=Vector3.down*.35f;
            for(float z=World.minZ;z<World.maxZ;z+=10)foreach(float x in new[]{-49f,49f})
                Append(g,z%20==0?"tree_pineRoundA":"tree_default",x+4*Mathf.Sin(z),z,7+2*Mathf.Sin(z*.3f),z,false,.83f);
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
            var model=Donors.TryGetValue(asset,out var donor)?donor:Library.Get(asset=="pilot.ruined-house"?"ua_abandoned_house":asset);
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
            // D12's project-owned mosaic, attached to the actual rear and side wall faces.
            float rear=float.MinValue,right=float.MinValue;var bounds=model.Bounds;
            for(int i=0;i<model.Positions.Length;i+=3)
            {
                var color=model.Colors[i/3];if(color.r<.72f||color.g<.68f)continue;
                var center=(model.Positions[i]+model.Positions[i+1]+model.Positions[i+2])/3;
                if(model.Normals[i].z<-.8f)rear=Mathf.Max(rear,center.z);
                if(model.Normals[i].x>.8f)right=Mathf.Max(right,center.x);
            }
            if(rear==float.MinValue||right==float.MinValue)throw new InvalidOperationException("Mosaic wall binding missing");
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
            }
        }
        static RoadmapModelLibrary.Model ReadDonor(Binding binding)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(binding.source);if(source==null)throw new InvalidOperationException(binding.source);
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=filter.sharedMesh;if(mesh==null||filter.name.Contains("LOD")&&!filter.name.Contains("LOD"+binding.lod))continue;
                var matrix=source.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;var ns=mesh.normals;var vs=mesh.vertices;
                for(int sub=0;sub<mesh.subMeshCount;sub++)
                {
                    var color=binding.asset=="pilot.reeds"?new Color(.53f,.57f,.29f):binding.asset=="pilot.ruin-wall"?new Color(.44f,.38f,.29f):new Color(.46f,.48f,.43f);
                    foreach(int i in mesh.GetTriangles(sub)){vertices.Add(matrix.MultiplyPoint3x4(vs[i]));normals.Add(matrix.inverse.transpose.MultiplyVector(ns[i]).normalized);if(vertices.Count%3==1)colors.Add(color);}
                }
            }
            if(vertices.Count==0)throw new InvalidOperationException("Empty donor "+binding.asset);
            var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);
            float h=Mathf.Max(.01f,bounds.size.y);var pivot=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            return new RoadmapModelLibrary.Model{id=binding.asset,Positions=vertices.Select(v=>(v-pivot)/h).ToArray(),Normals=normals.ToArray(),Colors=colors.ToArray(),Bounds=new Bounds((bounds.center-pivot)/h,bounds.size/h),SourceHeight=h};
        }
    }
}
