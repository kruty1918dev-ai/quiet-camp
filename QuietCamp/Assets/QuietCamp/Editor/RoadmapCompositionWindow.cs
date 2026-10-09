
using QuietCamp.Presentation.UI.Prepared;
using RoadmapGladeGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGladeGraphic;
using RoadmapGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapGraphic;
using RoadmapLayout = QuietCamp.Presentation.UI.Prepared.RoadmapLayout;
using RoadmapSceneGenerator = QuietCamp.Presentation.UI.Prepared.RoadmapSceneGenerator;
using RoadmapWeatherGraphic = QuietCamp.Presentation.UI.Prepared.RoadmapWeatherGraphic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Application;
using QuietCamp.Composition;
using QuietCamp.Presentation.UI;
using UnityEditor;
using UnityEngine;
namespace QuietCamp.Editor
{
    /// <summary>Actual baked meshes/shader, with ID-addressable composition overlays. No scene mutation.</summary>
    public sealed class RoadmapCompositionWindow:EditorWindow
    {
        PreviewRenderUtility _preview;Material _material;RoadmapCatalog _catalog;SceneCompositionDocument[] _docs;CompositionResult[] _results;
        enum Gallery{Main,Aircraft,Ship,Dam}
        readonly List<RoadmapChunkAsset> _neighbours=new List<RoadmapChunkAsset>(3);
        Gallery _gallery;Dictionary<string,VisualAssetDefinition> _assets;RoadmapChunkAsset _chunk;int _index;string _id="";bool _top,_ids=true,_footprints,_connections=true,_zones,_ownership,_entrances;Vector3 _focus;
        [MenuItem("Quiet Camp/Composition/Preview")]
        static void Open()=>GetWindow<RoadmapCompositionWindow>("Composition");
        void OnEnable()
        {
            _preview=new PreviewRenderUtility();_preview.camera.orthographic=true;_preview.camera.nearClipPlane=.1f;_preview.camera.farClipPlane=200;
            var shader=Resources.Load<Shader>("QuietCamp/RoadmapWorld");if(shader!=null)_material=new Material(shader){hideFlags=HideFlags.HideAndDontSave};
        }
        void Load()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Use native Game View during Play Mode; composition preview is for Edit Mode");
            if(_index<0)throw new ArgumentOutOfRangeException("Chunk");
            if(_gallery!=Gallery.Main)
            {
                string name=_gallery.ToString().ToLowerInvariant();if(!File.Exists(RoadmapStagingCompositionBaker.NativeFolder(name)+"/bake.json"))throw new InvalidOperationException("Staging revision is not completely baked");_catalog=RoadmapStagingCompositionBaker.Catalog(name,out _docs,out _results,out _assets);
                _neighbours.Clear();for(int c=Math.Max(0,_index-1);c<=Math.Min(_catalog.Chunks.Length-1,_index+1);c++){var asset=AssetDatabase.LoadAssetAtPath<RoadmapChunkAsset>(RoadmapStagingCompositionBaker.NativeFolder(name)+"/chunk-"+c+".asset");if(asset!=null)_neighbours.Add(asset);}
                _chunk=_neighbours.FirstOrDefault(c=>c.index==_index);
                if(_chunk==null)throw new InvalidOperationException("Bake Staging Landmarks before preview");
                _focus=new Vector3(0,0,-(_catalog.Chunks[_index].Top+_catalog.Chunks[_index].Bottom)*.5f/RoadmapCompositionAdapter.Units);return;
            }
            var documents=RoadmapCompositionBaker.Documents();_catalog=new RoadmapCatalog(RoadmapCompositionBaker.Definition(documents));RoadmapCompositionBaker.WorldPlans(_catalog,documents,out _docs,out _results);
            _assets=RoadmapCompositionBaker.Read<VisualAssetDefinition[]>(RoadmapCompositionBaker.Reference("assets")).ToDictionary(a=>a.id);
            var published=RoadmapCompositionBaker.Read<Domain.RoadmapDefinition>(RoadmapContentExporter.Folder+"roadmap_regions.json");
            if(published.revision!=_catalog.Definition.revision||string.IsNullOrEmpty(published.compositionManifest))throw new InvalidOperationException("Source differs from native bake. Run Bake Main before preview.");
            _neighbours.Clear();for(int c=Math.Max(0,_index-1);c<=Math.Min(_catalog.Chunks.Length-1,_index+1);c++){var asset=Resources.Load<RoadmapChunkAsset>(published.bakedBindings.Length>0?published.bakedBindings[c].resourceId:published.compositionManifest+"/chunk-"+c);if(asset==null)throw new InvalidOperationException("Missing native neighbour: "+c);_neighbours.Add(asset);}
            _chunk=_neighbours.First(c=>c.index==_index);
            _focus=new Vector3(0,0,-(_catalog.Chunks[_index].Top+_catalog.Chunks[_index].Bottom)*.5f/RoadmapCompositionAdapter.Units);
        }
        void OnGUI()
        {
            using(new EditorGUILayout.HorizontalScope())
            {
                _gallery=(Gallery)EditorGUILayout.EnumPopup(_gallery,GUILayout.Width(100));_index=EditorGUILayout.IntField("Chunk",_index);_id=EditorGUILayout.TextField(_id);
                if(GUILayout.Button("Load / Frame"))try{Load();Frame();}catch(Exception e){Debug.LogException(e);}
                if(GUILayout.Button("Capture")&&_chunk!=null)Capture();
            }
            using(new EditorGUILayout.HorizontalScope())
            {_top=GUILayout.Toggle(_top,"Top");_ids=GUILayout.Toggle(_ids,"IDs");_footprints=GUILayout.Toggle(_footprints,"Footprints");_connections=GUILayout.Toggle(_connections,"Connections");_zones=GUILayout.Toggle(_zones,"Parcels / zones");_ownership=GUILayout.Toggle(_ownership,"Chunks / reveal owner");_entrances=GUILayout.Toggle(_entrances,"Entrances");}
            var rect=GUILayoutUtility.GetRect(50,10000,50,10000);
            if(EditorApplication.isPlayingOrWillChangePlaymode){_chunk=null;_neighbours.Clear();GUI.Label(rect,"Use Game View in Play Mode. Reload the offline preview after stopping.");return;}
            if(_chunk==null||_material==null){GUI.Label(rect,"Bake Main, then Load. Native mesh preview; authoring changes are made by ID through CLI.");return;}
            _preview.camera.orthographicSize=_top?28:23;_preview.camera.aspect=Mathf.Max(.1f,rect.width/Mathf.Max(1,rect.height));_preview.camera.transform.rotation=Quaternion.Euler(_top?90:55,0,0);_preview.camera.transform.position=_focus-_preview.camera.transform.forward*55;
            var visual=new RoadmapVisualProfile(new RoadmapEnvironmentSampler(_catalog).Sample((_catalog.Chunks[_index].Top+_catalog.Chunks[_index].Bottom)*.5f));
            var sun=-(Quaternion.Euler(Mathf.Lerp(50,22,visual.Environment.Night),65,0)*Vector3.forward);
            _material.SetVector("_WorldSun",sun);_material.SetColor("_WorldSunColor",visual.Sun);_material.SetColor("_WorldAmbient",visual.Ambient);_material.SetVector("_RevealFront",new Vector4(1000000,1001000,RoadmapCompositionAdapter.Units,0));_material.SetColor("_RevealFog",visual.Palette.Fog.linear);
            _material.SetFloat("_WorldMotion",1);_material.SetFloat("_WorldTime",(float)EditorApplication.timeSinceStartup);
            if(Event.current.type==EventType.Repaint)
            {
                _preview.BeginPreview(rect,GUIStyle.none);DrawWorld();
                _preview.Render(true);GUI.DrawTexture(rect,_preview.EndPreview(),ScaleMode.StretchToFill,false);
                Handles.BeginGUI();
                foreach(var p in Visible())
                {
                    var centre=Screen(p.x,p.z,p.height*.5f,rect);if(_ids&&rect.Contains(centre))GUI.Label(new Rect(centre.x,centre.y,260,20),p.id);
                    if(_ownership&&rect.Contains(centre))GUI.Label(new Rect(centre.x,centre.y+18,300,20),"owner: "+p.owner+" | reveal: "+(p.revealOwner??"terrain"));
                    if(_entrances&&(p.role=="gate"||p.role=="entrance")){Handles.color=Color.green;Handles.DrawWireDisc(Screen(p.x,p.z,0,rect),Vector3.forward,6);}
                    if(_footprints&&_assets.TryGetValue(p.asset,out var asset))
                    {float radius=asset.radius*p.height/asset.height;var points=new Vector3[5];for(int i=0;i<5;i++)points[i]=Screen(p.x+Mathf.Cos(i*Mathf.PI*.5f)*radius,p.z+Mathf.Sin(i*Mathf.PI*.5f)*radius,0,rect);Handles.color=Color.yellow;Handles.DrawAAPolyLine(points);}
                }
                if(_zones)foreach(var d in _docs)foreach(var zone in d.zones)
                {Handles.color=new Color(.65f,.85f,.3f,.7f);Rectangle(zone.x-zone.width*.5f,zone.z-zone.depth*.5f,zone.x+zone.width*.5f,zone.z+zone.depth*.5f,rect);}
                if(_ownership)foreach(var chunk in _neighbours){var range=_catalog.Chunks[chunk.index];Handles.color=Color.cyan;Rectangle(-42,range.Top/RoadmapCompositionAdapter.Units,42,range.Bottom/RoadmapCompositionAdapter.Units,rect);}
                if(_connections)foreach(var result in _results)foreach(var span in result.spans){Handles.color=span.height>0?Color.cyan:new Color(1,.8f,.4f);Handles.DrawAAPolyLine(Screen(span.ax,span.az,0,rect),Screen(span.bx,span.bz,0,rect));}
                Handles.EndGUI();
            }
            Repaint();
        }
        void DrawWorld()
        {
            foreach(var chunk in _neighbours){_preview.DrawMesh(chunk.high!=null?chunk.high:chunk.low,Matrix4x4.identity,_material,0);foreach(var n in chunk.nodes)_preview.DrawMesh(n.detail,Matrix4x4.identity,_material,0);foreach(var b in chunk.branches)_preview.DrawMesh(b.silhouette,Matrix4x4.identity,_material,0);}
        }
        void Rectangle(float left,float top,float right,float bottom,Rect rect)=>Handles.DrawAAPolyLine(Screen(left,top,0,rect),Screen(right,top,0,rect),Screen(right,bottom,0,rect),Screen(left,bottom,0,rect),Screen(left,top,0,rect));
        IEnumerable<ComposedInstance> Visible()=>_results.SelectMany(r=>r.instances).Where(p=>_neighbours.Any(c=>p.revealOwner!=null?_catalog.ChunkForNode(_catalog.NodeIndex(p.revealOwner))==c.index:p.z>=_catalog.Chunks[c.index].Top/RoadmapCompositionAdapter.Units&&p.z<_catalog.Chunks[c.index].Bottom/RoadmapCompositionAdapter.Units));
        Vector3 Screen(float x,float z,float y,Rect rect)
        {var p=_preview.camera.WorldToViewportPoint(new Vector3(x,y,-z));return new Vector3(rect.x+p.x*rect.width,rect.y+(1-p.y)*rect.height,0);}
        void Frame()
        {
            var p=_results.SelectMany(r=>r.instances).FirstOrDefault(p=>p.id==_id||p.owner==_id);var node=_docs.SelectMany(d=>d.nodes).FirstOrDefault(n=>n.id==_id);var zone=_docs.SelectMany(d=>d.zones).FirstOrDefault(z=>z.id==_id);var route=_docs.SelectMany(d=>d.routes).FirstOrDefault(r=>r.id==_id);
            float x,z;int chunk;
            if(p!=null){x=p.x;z=p.z;chunk=p.revealOwner!=null?_catalog.ChunkForNode(_catalog.NodeIndex(p.revealOwner)):_catalog.ChunkAt(z*RoadmapCompositionAdapter.Units);}
            else if(node!=null){x=node.x;z=node.z;chunk=_catalog.ChunkForNode(_catalog.NodeIndex(node.id));}
            else if(zone!=null){x=zone.x;z=zone.z;chunk=_catalog.ChunkAt(z*RoadmapCompositionAdapter.Units);}
            else if(route!=null){x=route.points.Average(t=>t.x);z=route.points.Average(t=>t.z);chunk=_catalog.ChunkAt(z*RoadmapCompositionAdapter.Units);}
            else return;
            if(chunk!=_index){_index=chunk;Load();}_focus=new Vector3(x,0,-z);
        }
        void Capture()
        {
            string path=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../TestResults/roadmap-composition-2026-10-08"));Directory.CreateDirectory(path);
            // Static preview uses the same native meshes; gameplay QA captures actual Game View separately.
            _preview.camera.aspect=1;_preview.BeginStaticPreview(new Rect(0,0,1080,1080));DrawWorld();_preview.Render(true);
            var texture=_preview.EndStaticPreview();File.WriteAllBytes(path+"/preview-"+_gallery.ToString().ToLowerInvariant()+"-"+_index+".png",texture.EncodeToPNG());DestroyImmediate(texture);
            var displayed=_neighbours.SelectMany(c=>new[]{c.high??c.low}.Concat(c.nodes.Select(n=>n.detail)).Concat(c.branches.Select(b=>b.silhouette))).Where(m=>m!=null).ToArray();
            File.WriteAllText(path+"/preview-"+_gallery.ToString().ToLowerInvariant()+"-"+_index+".json",JsonConvert.SerializeObject(new{sourceHash=_chunk.sourceHash,camera=new{top=_top,focus=new[]{_focus.x,_focus.y,_focus.z}},metrics=new{estimatedBytes=_neighbours.Sum(c=>c.estimatedBytes),chunks=_neighbours.Count,displayedMeshes=displayed.Length,vertices=displayed.Sum(m=>m.vertexCount)},chunkOwnership=_neighbours.Select(c=>new{c.chunkId,c.index,c.sourceHash}),entities=Visible().Select(p=>new{p.id,p.owner,p.asset,p.x,p.z,p.height,projectedBounds=ProjectedBounds(p)})},Formatting.Indented));
        }
        float[] ProjectedBounds(ComposedInstance p)
        {
            if(!_assets.TryGetValue(p.asset,out var asset))return null;
            var rotate=Quaternion.Euler(0,-p.yaw,0);var origin=new Vector3(p.x,0,-p.z);float scale=p.height/asset.height;
            float left=float.MaxValue,bottom=float.MaxValue,right=float.MinValue,top=float.MinValue;
            for(int i=0;i<8;i++)
            {
                var local=new Vector3((i%2==0?-.5f:.5f)*asset.width*scale,(i/2%2==0?0:1)*p.height,(i/4==0?-.5f:.5f)*asset.depth*scale);
                var v=_preview.camera.WorldToViewportPoint(origin+rotate*local);left=Mathf.Min(left,v.x);bottom=Mathf.Min(bottom,v.y);right=Mathf.Max(right,v.x);top=Mathf.Max(top,v.y);
            }
            return new[]{left,bottom,right,top};
        }
        void OnDisable(){_preview?.Cleanup();_preview=null;if(_material!=null)DestroyImmediate(_material);}
    }
}
