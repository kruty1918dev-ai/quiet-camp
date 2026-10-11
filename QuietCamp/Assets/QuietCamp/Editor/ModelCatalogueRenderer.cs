using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Presentation.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace QuietCamp.Editor
{
    /// <summary>Native, source-linked model photographs; no scene/puzzle publication.</summary>
    public static class ModelCatalogueRenderer
    {
        public static readonly string[] Roots={"Assets/PolygonCity","Assets/PolygonNatureBiomes/PNB_Meadow_Forest",
            "Assets/PolygonNatureBiomes/PNB_Swamp_Marshland","Assets/PolygonBattleRoyale","Assets/PolygonParticles",
            "Assets/PolygonNatureBiomes/PNB_Core","Assets/QuietCamp/Authoring/Roadmap/Models",
            "Assets/PolygonNatureBiomes/PNB_Tropical_Jungle","Assets/QuietCamp/ThirdParty","Assets/ThirdParty"};
        public sealed class Entry
        {
            public string id,displayId,path,name,sourceHash,package,materialBinding;
            public string framing;
            public float[] bounds;
            public int meshes;public long triangles;
            public string[] shaders,images;
            public string status="rendered-unreviewed";
            public string error;
        }
        sealed class MaterialRef { public string path,name; }
        static Dictionary<string,MaterialRef[]> MaterialMap()
        {
            var result=new Dictionary<string,MaterialRef[]>(StringComparer.Ordinal);
            foreach(var path in AssetDatabase.FindAssets("t:Prefab",Roots).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p))
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)continue;
                foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    var filter=renderer.GetComponent<MeshFilter>();
                    var mesh=filter!=null?filter.sharedMesh:(renderer as SkinnedMeshRenderer)?.sharedMesh;
                    if(mesh==null)continue;string key=MeshKey(mesh);
                    if(result.ContainsKey(key))continue;
                    result[key]=renderer.sharedMaterials.Select(m=>m==null?null:new MaterialRef
                        {path=AssetDatabase.GetAssetPath(m),name=m.name}).ToArray();
                }
            }
            return result;
        }
        static string MeshKey(Mesh mesh)=>AssetDatabase.GetAssetPath(mesh)+"#"+mesh.name;
        static Material Resolve(MaterialRef reference)
        {
            if(reference==null||string.IsNullOrEmpty(reference.path))return null;
            if(reference.path.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))
                return AssetDatabase.LoadAssetAtPath<Material>(reference.path);
            return AssetDatabase.LoadAllAssetsAtPath(reference.path).OfType<Material>().FirstOrDefault(m=>m.name==reference.name);
        }
        public static void Render()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("A real graphics device is required");
            string repository=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../.."));
            string output=Path.Combine(repository,"Design/Roadmap/ModelCatalogue/LocalPreviews~");
            bool delta=Environment.GetEnvironmentVariable("QC_MODEL_CAMPAIGN100")=="1";
            JObject[] coverage=null;
            if(delta)
            {
                output=Path.Combine(repository,"Design/Campaign100/2026-10-11/ModelPreviews~");
                coverage=JObject.Parse(File.ReadAllText(Path.Combine(repository,"Design/Campaign100/2026-10-11/existing-content-audit.json")))["modelSources"].OfType<JObject>().Where(r=>(bool)r["needsCapture"]).ToArray();
            }
            string selected=Environment.GetEnvironmentVariable("QC_MODEL_IDS");
            if(delta&&!string.IsNullOrEmpty(selected))
            {var ids=new HashSet<string>(selected.Split(','));coverage=coverage.Where(r=>ids.Contains((string)r["number"])).ToArray();}
            Directory.CreateDirectory(output);
            int start=IntVariable("QC_MODEL_REVIEW_START",0),count=IntVariable("QC_MODEL_REVIEW_COUNT",int.MaxValue);
            var paths=Roots.SelectMany(root=>AssetDatabase.FindAssets("t:Model",new[]{root}).Select(AssetDatabase.GUIDToAssetPath))
                .Where(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)||p.EndsWith(".obj",StringComparison.OrdinalIgnoreCase))
                .Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            if(delta)paths=coverage.Select(r=>(string)r["path"]).ToArray();
            else if(paths.Length!=1210)throw new InvalidOperationException("Model coverage changed: expected 1210, got "+paths.Length);
            var materials=MaterialMap();EditorUtility.UnloadUnusedAssetsImmediate();
            var library=RoadmapModelLibrary.Load();library.PrepareCulture();library.PrepareStaging();
            var entries=new List<Entry>();
            var preview=new PreviewRenderUtility();
            preview.camera.orthographic=true;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=10000;
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.88f,.89f,.86f);
            preview.camera.allowHDR=false;preview.camera.allowMSAA=false;
            preview.lights[0].intensity=1.25f;preview.lights[0].color=new Color(1,.92f,.80f);
            preview.lights[0].transform.rotation=Quaternion.Euler(48,-35,0);
            preview.lights[1].intensity=.45f;preview.lights[1].transform.rotation=Quaternion.Euler(25,145,0);
            var neutral=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/QuietCamp/Editor/TentThumbnail.shader")){hideFlags=HideFlags.HideAndDontSave};
            neutral.SetColor("_BaseColor",new Color(.60f,.65f,.59f));
            var colored=new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/QuietCamp/Editor/DioramaPalette.shader")){hideFlags=HideFlags.HideAndDontSave};
            try
            {
                for(int index=start;index<paths.Length&&index-start<count;index++)
                {
                    string path=paths[index];var entry=new Entry{id="m_"+AssetDatabase.AssetPathToGUID(path),
                        displayId=delta?(string)coverage[index]["number"]:"M"+(index+1).ToString("D4"),path=path,name=Path.GetFileNameWithoutExtension(path),
                        package=Roots.FirstOrDefault(root=>path.StartsWith(root+"/",StringComparison.Ordinal))??(path.StartsWith("Packages/")?path.Split('/')[1]:"Assets"),sourceHash=delta?(string)coverage[index]["sourceSha256"]:Hash(path)};
                    string photographPath=path;
                    if(delta&&coverage[index]["photographySource"]!=null)
                    {
                        string physical=Path.Combine(repository,"QuietCamp",(string)coverage[index]["photographySource"]);
                        if(Hash(physical)!=entry.sourceHash)throw new InvalidOperationException("Package source changed: "+path);
                        photographPath="Assets/QuietCamp/Editor/CampaignModelPhotography/"+entry.displayId+Path.GetExtension(path);
                        Directory.CreateDirectory(Path.GetDirectoryName(photographPath));File.Copy(physical,photographPath,true);
                        AssetDatabase.ImportAsset(photographPath,ImportAssetOptions.ForceSynchronousImport|ImportAssetOptions.ForceUpdate);
                    }
                    GameObject instance=null;Mesh ownedMesh=null;
                    try
                    {
                        var model=path.StartsWith("Assets/QuietCamp/Authoring/",StringComparison.Ordinal)?library.Get(entry.name):null;
                        bool glb=path.EndsWith(".glb",StringComparison.OrdinalIgnoreCase);
                        if(glb)
                        {
                            var derived=File.ReadAllText(Path.Combine(repository,"Design/Campaign100/2026-10-11/SourcePhotos~",entry.name+".json"));
                            model=JsonConvert.DeserializeObject<RoadmapModelLibrary.Model>(derived);model.Expand();
                        }
                        if(model!=null)
                        {
                            ownedMesh=NativeMesh(model);instance=new GameObject(entry.name);
                            var original=AssetDatabase.LoadAssetAtPath<GameObject>(photographPath);
                            var sourceBounds=glb?new[]{ownedMesh.bounds}:original.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh.bounds).ToArray();
                            var importedBounds=sourceBounds[0];foreach(var b in sourceBounds.Skip(1))importedBounds.Encapsulate(b);
                            instance.transform.localScale=Vector3.one*(importedBounds.size.y/Mathf.Max(.0001f,ownedMesh.bounds.size.y));
                            instance.AddComponent<MeshFilter>().sharedMesh=ownedMesh;
                            instance.AddComponent<MeshRenderer>().sharedMaterial=colored;entry.materialBinding=glb?"source-glb-derived-vertex-colour":"project-palette-photography";
                        }
                        else
                        {
                            var source=AssetDatabase.LoadAssetAtPath<GameObject>(photographPath);
                            if(source==null)throw new InvalidOperationException("Model root could not load");
                            instance=Object.Instantiate(source);entry.materialBinding="model-embedded";
                            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
                            {
                                var filter=renderer.GetComponent<MeshFilter>();
                                var mesh=filter!=null?filter.sharedMesh:(renderer as SkinnedMeshRenderer)?.sharedMesh;
                                if(mesh==null)continue;
                                if(materials.TryGetValue(MeshKey(mesh),out var references))
                                {renderer.sharedMaterials=references.Select(Resolve).Select(m=>m??neutral).ToArray();entry.materialBinding="prefab-linked";}
                                else
                                {var slots=renderer.sharedMaterials;if(slots.Length==0)slots=new[]{neutral};renderer.sharedMaterials=slots.Select(m=>m??neutral).ToArray();}
                            }
                        }
                        foreach(var t in instance.GetComponentsInChildren<Transform>(true)){t.gameObject.layer=31;t.gameObject.SetActive(true);}
                        foreach(var lod in instance.GetComponentsInChildren<LODGroup>(true))lod.enabled=false;
                        foreach(var r in instance.GetComponentsInChildren<Renderer>(true))r.enabled=true;
                        if(path.StartsWith("Packages/")||entry.displayId=="M1215"||entry.displayId=="M1257")
                        {foreach(var r in instance.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(_=>neutral).ToArray();entry.materialBinding="two-sided-neutral-source-geometry-photography";}
                        preview.AddSingleGO(instance);
                        var renderers=instance.GetComponentsInChildren<Renderer>(true);
                        if(renderers.Length==0)throw new InvalidOperationException("Model has no renderers");
                        var bounds=renderers[0].bounds;foreach(var renderer in renderers.Skip(1))bounds.Encapsulate(renderer.bounds);
                        entry.bounds=new[]{bounds.size.x,bounds.size.y,bounds.size.z};
                        var meshes=instance.GetComponentsInChildren<MeshFilter>(true).Select(m=>m.sharedMesh)
                            .Concat(instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(m=>m.sharedMesh)).Where(m=>m!=null).ToArray();
                        entry.meshes=meshes.Length;
                        foreach(var mesh in meshes)for(int s=0;s<mesh.subMeshCount;s++)
                            if(mesh.GetTopology(s)==MeshTopology.Triangles)entry.triangles+=(long)mesh.GetIndexCount(s)/3;
                        entry.shaders=renderers.SelectMany(r=>r.sharedMaterials).Where(m=>m!=null)
                            .Select(m=>m.shader==null?"<missing>":m.shader.name).Distinct().ToArray();
                        if(entry.shaders.Length==0)throw new InvalidOperationException("Photography material was lost; do not accept an uncoloured source");
                        var frame=bounds;
                        if(entry.displayId=="M1325"||entry.displayId=="M1326"||entry.displayId=="M1327")
                        {
                            bool found=false;
                            foreach(var filter in instance.GetComponentsInChildren<MeshFilter>(true))
                            {
                                var mesh=filter.sharedMesh;var vertices=mesh.vertices;
                                for(int sub=0;sub<mesh.subMeshCount;sub++)
                                {
                                    if(mesh.GetTopology(sub)!=MeshTopology.Triangles)continue;
                                    var indices=mesh.GetTriangles(sub);
                                    for(int i=0;i<indices.Length;i+=3)
                                    {
                                        var a=filter.transform.TransformPoint(vertices[indices[i]]);var b=filter.transform.TransformPoint(vertices[indices[i+1]]);var c=filter.transform.TransformPoint(vertices[indices[i+2]]);
                                        if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-16f)continue;
                                        foreach(var point in new[]{a,b,c}){if(!found){frame=new Bounds(point,Vector3.zero);found=true;}else frame.Encapsulate(point);}
                                    }
                                }
                            }
                            entry.framing="Non-degenerate source triangle bounds; oversized particle/shader bounding triangles excluded from camera fit only. Source geometry unchanged.";
                        }
                        preview.camera.orthographicSize=Mathf.Max(.0001f,frame.extents.magnitude*1.06f);
                        entry.images=new string[2];
                        for(int view=0;view<2;view++)
                        {
                            var direction=Quaternion.Euler(28,view==0?-35:145,0)*Vector3.back;
                            preview.camera.transform.position=frame.center+direction*Mathf.Max(5,frame.extents.magnitude*4);
                            preview.camera.transform.LookAt(frame.center);
                            preview.BeginStaticPreview(new Rect(0,0,320,320));preview.Render(true);
                            var image=preview.EndStaticPreview();entry.images[view]=entry.displayId+(view==0?"-front.png":"-back.png");
                            File.WriteAllBytes(Path.Combine(output,entry.images[view]),image.EncodeToPNG());Object.DestroyImmediate(image);
                        }
                    }
                    catch(Exception e){entry.status="render-failed";entry.error=e.ToString();Debug.LogException(e);}
                    finally{if(instance!=null)Object.DestroyImmediate(instance);if(ownedMesh!=null)Object.DestroyImmediate(ownedMesh);}
                    entries.Add(entry);
                    File.WriteAllText(Path.Combine(output,entry.displayId+".json"),JsonConvert.SerializeObject(entry,Formatting.Indented)+"\n");
                    if((index-start+1)%20==0){EditorUtility.UnloadUnusedAssetsImmediate();GC.Collect();Debug.Log("[ModelCatalogue] "+(index+1)+"/"+paths.Length);}
                }
                File.WriteAllText(Path.Combine(output,"batch-"+start+".json"),JsonConvert.SerializeObject(new
                    {unityVersion=UnityEngine.Application.unityVersion,graphics=SystemInfo.graphicsDeviceType.ToString(),device=SystemInfo.graphicsDeviceName,
                        sourceModelCount=paths.Length,start,rendered=entries.Count(e=>e.error==null),failed=entries.Count(e=>e.error!=null),entries,
                        visualReviewComplete=false,playerBuild=false},Formatting.Indented)+"\n");
                if(entries.Any(e=>e.error!=null))throw new InvalidOperationException("Some model photographs failed; inspect per-model receipts");
            }
            finally{preview.Cleanup();Object.DestroyImmediate(neutral);Object.DestroyImmediate(colored);}
        }
        internal static Mesh NativeMesh(RoadmapModelLibrary.Model model)
        {
            int count=model.Positions.Length;var mesh=new Mesh{indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.vertices=model.Positions.Select(p=>p*model.SourceHeight).ToArray();mesh.normals=model.Normals;
            mesh.colors=Enumerable.Range(0,count).Select(i=>model.Colors[i/3]).ToArray();
            if(model.UVs!=null)mesh.uv=model.UVs;mesh.triangles=Enumerable.Range(0,count).ToArray();mesh.RecalculateBounds();return mesh;
        }
        static int IntVariable(string name,int fallback)=>int.TryParse(Environment.GetEnvironmentVariable(name),out int value)?Math.Max(0,value):fallback;
        static string Hash(string path){using(var stream=File.OpenRead(path))using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
    }
}
