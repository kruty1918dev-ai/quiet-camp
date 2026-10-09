using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using QuietCamp.Composition;
using QuietCamp.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace QuietCamp.Editor
{
    /// <summary>Independent Editor-only studies of authored intentions. No level, scene or resource publication.</summary>
    public static class DioramaStudyRenderer
    {
        const string Source="Assets/QuietCamp/Authoring/Roadmap/Composition/DioramaStudies";
        sealed class Binding {public string asset,catalogueId,guid,source,sourceHash,prefab,prefabHash;}
        sealed class Water {public float x,z,radiusX,radiusZ,lilyWidth=.7f;}
        sealed class Presentation
        {public string id,titleUk,descriptionUk,season,intendedDistrict,visualReferenceLevelId,referencePurpose;public float radiusX,radiusZ,cameraYaw,cameraPitch;public Water water;public bool mosaicPanel;}
        static readonly List<Mesh> OwnedMeshes=new List<Mesh>();
        static readonly Dictionary<string,Mesh> Meshes=new Dictionary<string,Mesh>();
        static Dictionary<string,Binding> Bindings;
        static RoadmapModelLibrary Library;
        static Material Palette;
        static GameObject Root;
        static Presentation Style;
        static readonly HashSet<string> UsedAssets=new HashSet<string>();
        static T Read<T>(string path)=>JsonConvert.DeserializeObject<T>(File.ReadAllText(path));
        static string Hash(string path)
        {using(var s=File.OpenRead(path))using(var h=SHA256.Create())return BitConverter.ToString(h.ComputeHash(s)).Replace("-","").ToLowerInvariant();}
        static Color C(float r,float g,float b)=>new Color(r,g,b).linear;

        [MenuItem("Quiet Camp/Dioramas/Render 12 Style Studies")]
        public static void Render()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Real graphics required");
            string repo=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../.."));
            string output=Path.Combine(repo,"Design/Roadmap/DioramaStudies/2026-10-10");Directory.CreateDirectory(output);
            var styles=Read<Presentation[]>(Source+"/presentation.json");
            var assets=Read<VisualAssetDefinition[]>(Source+"/assets.json").ToDictionary(a=>a.id);
            var templates=Read<EnsembleTemplate[]>(Source+"/templates.json").ToDictionary(t=>t.id);
            Bindings=Read<Binding[]>(Source+"/bindings.json").ToDictionary(b=>b.asset);
            foreach(var b in Bindings.Values)
                if(Hash(b.source)!=b.sourceHash||Hash(b.prefab)!=b.prefabHash||AssetDatabase.AssetPathToGUID(b.source)!=b.guid)
                    throw new InvalidOperationException("Stale source binding: "+b.catalogueId);
            Library=RoadmapModelLibrary.Load();Library.PrepareCulture();Library.PrepareSeasons();
            Palette=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/QuietCamp/Editor/DioramaPalette.shader")){hideFlags=HideFlags.HideAndDontSave};
            var preview=new PreviewRenderUtility();preview.camera.orthographic=true;
            bool previousAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=150;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.88f,.89f,.86f);
            // The preview scene is already isolated. Culling layer 0 also culled its lights.
            preview.camera.allowHDR=false;preview.camera.allowMSAA=false;
            preview.lights[0].intensity=1.2f;preview.lights[0].color=new Color(1,.93f,.82f);
            preview.lights[0].transform.rotation=Quaternion.Euler(48,-35,0);
            preview.lights[1].intensity=.4f;preview.lights[1].transform.rotation=Quaternion.Euler(25,145,0);
            int count=int.TryParse(Environment.GetEnvironmentVariable("QC_DIORAMA_COUNT"),out var requested)?requested:styles.Length;
            var receipts=new List<object>();
            try
            {
                foreach(var style in styles.Take(count))
                {
                    Style=style;UsedAssets.Clear();Root=new GameObject(style.id+" — "+style.titleUk);
                    var doc=Read<SceneCompositionDocument>(Source+"/"+style.id+".json");
                    var result=SceneComposer.Compose(doc,assets,templates);
                    if(!result.Valid||result.parcels.Count!=doc.ensembles.Length)
                        throw new InvalidOperationException(JsonConvert.SerializeObject(result.diagnostics));
                    Ground(doc.seed);Routes(doc,result);Clearing(doc);
                    foreach(var item in result.instances)Place(item.asset,item.x,item.z,item.height,item.yaw,item.id);
                    Landscape(doc,result,templates);
                    foreach(var t in Root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
                    preview.AddSingleGO(Root);
                    var target=new Vector3(0,.8f,0);var direction=Quaternion.Euler(style.cameraPitch,style.cameraYaw,0)*Vector3.back;
                    preview.camera.transform.position=target+direction*45;preview.camera.transform.LookAt(target);
                    preview.camera.orthographicSize=11.2f;
                    // URP lighting/material state needs real frames before the first photograph.
                    for(int warm=0;warm<3;warm++)
                    {preview.BeginStaticPreview(new Rect(0,0,320,240));preview.Render(true);Object.DestroyImmediate(preview.EndStaticPreview());}
                    preview.BeginStaticPreview(new Rect(0,0,1200,900));preview.Render(true);
                    var photograph=preview.EndStaticPreview();string image=style.id+".png";
                    File.WriteAllBytes(Path.Combine(output,image),photograph.EncodeToPNG());Object.DestroyImmediate(photograph);
                    var renderers=Root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
                    long triangles=0;foreach(var r in renderers){var mesh=r.GetComponent<MeshFilter>().sharedMesh;for(int s=0;s<mesh.subMeshCount;s++)if(mesh.GetTopology(s)==MeshTopology.Triangles)triangles+=(long)mesh.GetIndexCount(s)/3;}
                    var receipt=new{style.id,style.titleUk,image,imageHash=Hash(Path.Combine(output,image)),
                        sourceHash=Hash(Source+"/"+style.id+".json"),presentationHash=Hash(Source+"/presentation.json"),
                        assetsHash=Hash(Source+"/assets.json"),templatesHash=Hash(Source+"/templates.json"),bindingsHash=Hash(Source+"/bindings.json"),
                        rendererHash=Hash("Assets/QuietCamp/Editor/DioramaStudyRenderer.cs"),paletteShaderHash=Hash("Assets/QuietCamp/Editor/DioramaPalette.shader"),
                        compiledEditorHash=Hash(typeof(DioramaStudyRenderer).Assembly.Location),
                        compiler=SceneComposer.Revision,unity=UnityEngine.Application.unityVersion,
                        graphics=SystemInfo.graphicsDeviceType.ToString(),device=SystemInfo.graphicsDeviceName,
                        capturedUtc=DateTime.UtcNow.ToString("o"),result,usedAssets=UsedAssets.OrderBy(s=>s).ToArray(),
                        donorCatalogueIds=UsedAssets.Where(Bindings.ContainsKey).Select(a=>Bindings[a].catalogueId).OrderBy(s=>s).ToArray(),
                        nativePreviewRenderers=renderers.Length,nativePreviewTriangles=triangles,
                        uniqueMaterials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().Count(),
                        warmupFrames=3,synchronousPreviewShaders=true,
                        visualReviewComplete=false,performanceAccepted=false,runtimePublished=false,playerBuild=false};
                    File.WriteAllText(Path.Combine(output,style.id+".json"),JsonConvert.SerializeObject(receipt,Formatting.Indented)+"\n");
                    receipts.Add(receipt);Debug.Log("[DioramaStudy] Captured "+style.id+" "+style.titleUk);
                    Object.DestroyImmediate(Root);Root=null;
                }
                File.WriteAllText(Path.Combine(output,"render-receipt.json"),JsonConvert.SerializeObject(new{captured=receipts.Count,expected=12,receipts},Formatting.Indented)+"\n");
            }
            finally
            {
                if(Root!=null)Object.DestroyImmediate(Root);preview.Cleanup();Object.DestroyImmediate(Palette);ShaderUtil.allowAsyncCompilation=previousAsync;
                foreach(var m in OwnedMeshes)Object.DestroyImmediate(m);OwnedMeshes.Clear();Meshes.Clear();
            }
        }
        static GameObject Place(string asset,float x,float z,float height,float yaw,string identity,float maxWidth=0)
        {
            GameObject go;UsedAssets.Add(asset);
            if(Bindings.TryGetValue(asset,out var binding))
            {
                go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(binding.prefab));
                foreach(var group in go.GetComponentsInChildren<LODGroup>(true))
                {
                    var lods=group.GetLODs();int chosen=Math.Min(asset=="study.reeds"?0:1,lods.Length-1);if(chosen<0)continue;
                    var selected=new HashSet<Renderer>(lods[chosen].renderers);
                    foreach(var r in lods.SelectMany(l=>l.renderers))if(r!=null)r.enabled=selected.Contains(r);
                    group.enabled=false;
                }
            }
            else
            {
                string key=asset+(Style.season=="winter"&&asset=="ua_orchard_tree"?":bare":Style.season=="winter"&&asset=="ua_whitewashed_house"?":winter":"");
                if(!Meshes.TryGetValue(key,out var mesh))
                {
                    var model=key.EndsWith(":bare",StringComparison.Ordinal)?Library.BareTree(asset):Library.Get(asset);
                    if(model==null)throw new InvalidOperationException("Missing study model: "+asset);
                    mesh=ModelCatalogueRenderer.NativeMesh(model);mesh.name=key;
                    var colors=mesh.colors;var vertices=mesh.vertices;var normals=mesh.normals;
                    for(int i=0;i<colors.Length;i++)
                    {
                        var c=colors[i];
                        if(Style.season=="winter"&&asset=="ua_whitewashed_house"&&vertices[i].y>mesh.bounds.min.y+mesh.bounds.size.y*.6f&&normals[i].y>.15f)c=new Color(.86f,.88f,.88f);
                        colors[i]=c.linear;
                    }
                    mesh.colors=colors;OwnedMeshes.Add(mesh);Meshes.Add(key,mesh);
                }
                go=new GameObject(identity);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Palette;
            }
            go.name=identity;var rs=go.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy).ToArray();
            if(rs.Length==0)throw new InvalidOperationException("No selected native renderer: "+asset);
            if(rs.SelectMany(r=>r.sharedMaterials).Any(m=>m==null||m.shader==null||m.shader.name=="Hidden/InternalErrorShader"))
                throw new InvalidOperationException("Invalid study material: "+asset);
            var bounds=rs[0].bounds;foreach(var r in rs.Skip(1))bounds.Encapsulate(r.bounds);
            if(asset=="ua_bus_shelter_mosaic"&&Style.mosaicPanel)Mosaic(go,bounds);
            float scale=height/Mathf.Max(.01f,bounds.size.y);
            if(maxWidth>0)scale=Mathf.Min(scale,maxWidth/Mathf.Max(.01f,Mathf.Max(bounds.size.x,bounds.size.z)));
            var pivot=new GameObject(identity+".ground-pivot");pivot.transform.SetParent(Root.transform,false);
            go.transform.SetParent(pivot.transform,false);go.transform.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            pivot.transform.localScale=Vector3.one*scale;pivot.transform.localRotation=Quaternion.Euler(0,yaw,0);
            pivot.transform.localPosition=new Vector3(x,.025f,z);
            if(asset!="study.grass"&&asset!="study.flowers"&&asset!="study.reeds"&&asset!="study.lilies")
                Disc(identity+".contact",x,z,Mathf.Min(1.5f,bounds.size.x*scale*.43f),Mathf.Min(1.2f,bounds.size.z*scale*.4f),.014f,C(.56f,.59f,.43f));
            return pivot;
        }
        static void Mosaic(GameObject shelter,Bounds b)
        {
            // Project-owned decorative paint study on the otherwise plain source shelter.
            const int columns=13,rows=5;var vertices=new List<Vector3>();var indices=new List<int>();var colors=new List<Color>();
            float width=b.size.x*.70f,height=b.size.y*.30f;
            var source=shelter.GetComponent<MeshFilter>().sharedMesh;var sourceVertices=source.vertices;var sourceNormals=source.normals;var sourceColors=source.colors;
            float rear=float.MinValue,rightWall=float.MinValue;
            for(int i=0;i<sourceVertices.Length;i+=3)
            {
                if(sourceNormals[i].z<-.8f&&sourceColors[i].r>.72f&&sourceColors[i].g>.68f)
                    rear=Mathf.Max(rear,(sourceVertices[i].z+sourceVertices[i+1].z+sourceVertices[i+2].z)/3);
                if(sourceNormals[i].x>.8f&&sourceColors[i].r>.72f&&sourceColors[i].g>.68f)
                    rightWall=Mathf.Max(rightWall,(sourceVertices[i].x+sourceVertices[i+1].x+sourceVertices[i+2].x)/3);
            }
            if(rear==float.MinValue||rightWall==float.MinValue)throw new InvalidOperationException("Shelter wall not found");
            for(int y=0;y<rows;y++)for(int x=0;x<columns;x++)
            {
                float left=b.center.x-width*.5f+x*width/columns,bottom=b.min.y+b.size.y*.35f+y*height/rows;
                float right=left+width/columns*.94f,top=bottom+height/rows*.92f,z=rear-.007f;
                int first=vertices.Count;
                vertices.AddRange(new[]{new Vector3(left,bottom,z),new Vector3(right,bottom,z),new Vector3(right,top,z),new Vector3(left,top,z)});
                indices.AddRange(new[]{first,first+2,first+1,first,first+3,first+2});
                bool stem=x%4==2,leaf=y>1&&(x+y)%4==0;
                var color=stem||leaf?C(.78f,.67f,.36f):y==0?C(.40f,.57f,.54f):C(.35f,.52f,.63f);
                colors.AddRange(Enumerable.Repeat(color,4));
                // Exterior side remains readable from the map camera under the canopy.
                float sideLeft=b.center.z-b.size.z*.32f+x*b.size.z*.64f/columns;
                float sideRight=sideLeft+b.size.z*.64f/columns*.94f;
                first=vertices.Count;
                vertices.AddRange(new[]{new Vector3(rightWall+.007f,bottom,sideLeft),new Vector3(rightWall+.007f,top,sideLeft),new Vector3(rightWall+.007f,top,sideRight),new Vector3(rightWall+.007f,bottom,sideRight)});
                indices.AddRange(new[]{first,first+1,first+2,first,first+2,first+3});colors.AddRange(Enumerable.Repeat(color,4));
            }
            var mesh=new Mesh{name="D12.project-mosaic-paint"};mesh.vertices=vertices.ToArray();mesh.triangles=indices.ToArray();mesh.colors=colors.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();OwnedMeshes.Add(mesh);
            var panel=new GameObject("D12.focal/shelter/project-mosaic-paint");panel.transform.SetParent(shelter.transform,false);
            panel.AddComponent<MeshFilter>().sharedMesh=mesh;panel.AddComponent<MeshRenderer>().sharedMaterial=Palette;
        }
        static void MeshObject(string name,Vector3[] vertices,int[] indices,Color[] colors)
        {
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=indices;mesh.colors=colors;mesh.RecalculateNormals();mesh.RecalculateBounds();OwnedMeshes.Add(mesh);
            var go=new GameObject(name);go.transform.SetParent(Root.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=Palette;
        }
        static void Disc(string name,float x,float z,float rx,float rz,float y,Color color,int sides=40)
        {
            var vertices=new List<Vector3>();var colors=new List<Color>();var indices=new List<int>();
            for(int i=0;i<sides;i++)
            {
                float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;
                vertices.Add(new Vector3(x,y,z));vertices.Add(new Vector3(x+Mathf.Sin(a)*rx,y,z+Mathf.Cos(a)*rz));vertices.Add(new Vector3(x+Mathf.Sin(b)*rx,y,z+Mathf.Cos(b)*rz));
                colors.Add(color);colors.Add(color);colors.Add(color);indices.Add(i*3);indices.Add(i*3+1);indices.Add(i*3+2);
            }
            MeshObject(name,vertices.ToArray(),indices.ToArray(),colors.ToArray());
        }
        static void Ground(int seed)
        {
            Color grass=Style.season=="winter"?C(.86f,.88f,.88f):Style.season=="autumn"?C(.69f,.62f,.40f):C(.64f,.72f,.47f);
            Disc("soil-base",0,0,Style.radiusX,Style.radiusZ,-.12f,C(.49f,.43f,.33f),80);
            Disc("grass-surface",0,0,Style.radiusX,Style.radiusZ,0,grass,80);
            // Small tonal islands keep the broad ground quiet instead of a repeated texture.
            for(int i=0;i<16;i++)
            {
                float x=(SceneComposer.Unit("ground",seed,i*2)-.5f)*22,z=(SceneComposer.Unit("ground",seed,i*2+1)-.5f)*15;
                if(x*x/(Style.radiusX*Style.radiusX)+z*z/(Style.radiusZ*Style.radiusZ)>.64f)continue;
                Disc("ground-tone-"+i,x,z,1.4f,1,.002f,Color.Lerp(grass,C(.78f,.77f,.56f),Style.season=="winter"?.025f:.12f));
            }
            if(Style.water!=null)
            {
                var w=Style.water;Disc("shore",w.x,w.z,w.radiusX+.35f,w.radiusZ+.35f,.004f,C(.72f,.72f,.53f));
                Disc("project-water",w.x,w.z,w.radiusX,w.radiusZ,.006f,C(.42f,.65f,.68f));
                for(int i=0;i<6;i++)Place("study.lilies",w.x-2+i*.7f,w.z-.8f,.08f,i*47,"lilies-"+i,w.lilyWidth);
            }
        }
        static void Strip(string name,float ax,float az,float bx,float bz,float width,Color color,float y=.008f)
        {
            var delta=new Vector2(bx-ax,bz-az).normalized;var side=new Vector2(-delta.y,delta.x)*width*.5f;
            var v=new[]{new Vector3(ax+side.x,y,az+side.y),new Vector3(bx+side.x,y,bz+side.y),new Vector3(bx-side.x,y,bz-side.y),new Vector3(ax-side.x,y,az-side.y)};
            MeshObject(name,v,new[]{0,1,2,0,2,3},Enumerable.Repeat(color,4).ToArray());
        }
        static void Routes(SceneCompositionDocument doc,CompositionResult result)
        {
            var color=Style.season=="winter"?C(.72f,.75f,.74f):C(.78f,.73f,.57f);
            foreach(var r in doc.routes)for(int i=1;i<r.points.Length;i++)Strip(r.id+"."+i,r.points[i-1].x,r.points[i-1].z,r.points[i].x,r.points[i].z,r.width*1.45f,color);
            foreach(var s in result.spans.Where(s=>s.height==0))Strip(s.id,s.ax,s.az,s.bx,s.bz,.75f,color);
        }
        static void Clearing(SceneCompositionDocument doc)
        {
            foreach(var n in doc.nodes)
            {
                var color=Style.season=="winter"?C(.83f,.86f,.85f):Style.season=="autumn"?C(.72f,.66f,.44f):C(.70f,.75f,.52f);
                Disc("clearing",n.x,n.z,3.8f,3.8f,.003f,color);
                var line=Color.Lerp(color,C(.39f,.45f,.35f),.12f);
                // Empty visual lattice only: no puzzle data, checks, rules or level creation.
                for(int i=0;i<=5;i++)
                {
                    float t=-2.5f+i;Strip("visual-grid-x-"+i,n.x+t,n.z-2.5f,n.x+t,n.z+2.5f,.024f,line,.01f);
                    Strip("visual-grid-z-"+i,n.x-2.5f,n.z+t,n.x+2.5f,n.z+t,.024f,line,.01f);
                }
            }
        }
        static void Landscape(SceneCompositionDocument doc,CompositionResult result,Dictionary<string,EnsembleTemplate> templates)
        {
            bool Clear(float x,float z,float margin)
            {
                if(x*x/(Style.radiusX*Style.radiusX)+z*z/(Style.radiusZ*Style.radiusZ)>.83f)return false;
                if(doc.nodes.Any(n=>Vector2.Distance(new Vector2(x,z),new Vector2(n.x,n.z))<n.radius+margin))return false;
                if(doc.routes.Any(r=>SceneComposer.DistanceTo(r,x,z,out _)<r.width+margin))return false;
                if(result.spans.Any(s=>s.height==0&&SegmentDistance(x,z,s.ax,s.az,s.bx,s.bz)<.5f+margin))return false;
                if(result.parcels.Any(p=>{var t=templates[doc.ensembles.First(e=>e.id==p.id).template];return Mathf.Abs(x-p.x)<t.width*p.scale*.5f+margin&&Mathf.Abs(z-p.z)<t.depth*p.scale*.5f+margin;}))return false;
                if(Style.water!=null){var w=Style.water;if((x-w.x)*(x-w.x)/(w.radiusX*w.radiusX)+(z-w.z)*(z-w.z)/(w.radiusZ*w.radiusZ)<1.1f)return false;}
                return true;
            }
            foreach(var zone in doc.zones.Where(z=>z.kind!="parcel"))
            {
                int count=Mathf.RoundToInt(zone.density*(zone.kind=="forest"?28:zone.kind=="field"?15:150));
                for(int i=0;i<count;i++)
                {
                    float x=zone.x+(SceneComposer.Unit(zone.id,doc.seed,i*3)-.5f)*zone.width;
                    float z=zone.z+(SceneComposer.Unit(zone.id,doc.seed,i*3+1)-.5f)*zone.depth;
                    float u=SceneComposer.Unit(zone.id,doc.seed,i*3+2);
                    float height=zone.kind=="forest"?2.5f+u*1.4f:zone.kind=="field"?1.3f:.3f+u*.22f;
                    if(!Clear(x,z,zone.kind=="forest"?.8f:.15f))continue;
                    if(Style.season=="winter"&&zone.kind=="meadow")continue;
                    Place(zone.species,x,z,height,u*360,zone.id+".candidate-"+i);
                    if(zone.kind=="meadow"&&i%5==0&&Style.season!="autumn")Place("study.flowers",x+.3f,z-.3f,.24f,u*90,zone.id+".flowers-"+i);
                }
            }
            for(int i=0;i<14;i++)
            {
                float x=(SceneComposer.Unit("rock",doc.seed,i*2)-.5f)*23,z=(SceneComposer.Unit("rock",doc.seed,i*2+1)-.5f)*16;
                if(Clear(x,z,.3f))Place("study.rock",x,z,.25f+SceneComposer.Unit("height",doc.seed,i)*.3f,i*71,"edge-rock-"+i);
            }
        }
        static float SegmentDistance(float x,float z,float ax,float az,float bx,float bz)
        {var a=new Vector2(ax,az);var d=new Vector2(bx-ax,bz-az);float t=Mathf.Clamp01(Vector2.Dot(new Vector2(x,z)-a,d)/Mathf.Max(.0001f,d.sqrMagnitude));return Vector2.Distance(new Vector2(x,z),a+d*t);}
    }
}
