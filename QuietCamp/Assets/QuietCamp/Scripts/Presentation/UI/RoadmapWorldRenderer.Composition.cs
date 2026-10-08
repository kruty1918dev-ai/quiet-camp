#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Composition;
using UnityEngine;
namespace QuietCamp.Presentation.UI
{
    public sealed partial class RoadmapWorldRenderer
    {
        SceneCompositionDocument[] _offlineDocs;
        CompositionResult[] _offlineResults;
        int _offlineBranch=-1;
        public Dictionary<string,VisualAssetDefinition> OfflineAssets;
        bool _offline;
        public void SetOfflineComposition(SceneCompositionDocument[] docs,CompositionResult[] results)
        {_offlineDocs=docs;_offlineResults=results;}
        public Mesh BakePart(int kind,int key,int tier,bool completed=false,int branchReveal=0)
        {
            _offline=true;_offlineBranch=branchReveal;_scale=28;_tier=tier;Clear();
            try
            {
                IEnumerator body;
                if(kind==0)body=Terrain(key);
                else if(kind==2)body=Branch(key);
                else
                {
                    var data=_map.Data;var node=data.Nodes[key];var region=data.Definition.regions[data.RegionForNode(key)];
                    var scene=RoadmapSceneGenerator.FromBaked(_map.Summary(key),node.world,Infrastructure.AtmosphereCatalog.Load(),node==region.nodePositions[0]?region.storyProps:null,null,_map.Environment,data.Y(key),_scale*SinPitch,false,completed);
                    var origin=At(key);origin.y=Ground(origin.x,origin.z);
                    foreach(var prop in scene.Props)Append(prop.Asset.StartsWith("grass")?GroundCover():prop.Geometry??RoadmapModelLibrary.Load().Get(prop.Asset),origin+prop.Position,prop.Asset.StartsWith("grass")?Mathf.Min(.28f,prop.Height):prop.Height,prop.Yaw,scene,prop);
                    AppendSemanticNode(node.id);
                    body=null;
                }
                if(body!=null)Drain(body);
                return IndexedMesh("baked-"+kind+"-"+key+"-"+tier);
            }
            finally{_offline=false;_offlineBranch=-1;Clear();}
        }
        public Mesh BakeSilhouette(int index)
        {
            Clear();var models=RoadmapModelLibrary.Load();var origin=At(index);
            for(int i=0;i<3;i++)Append(models.Get("tree_pineRoundA"),origin+new Vector3((i-1)*2.1f,0,1),2.5f,0);
            for(int i=0;i<_colors.Count;i++)_colors[i]=Color.Lerp(new Color(.5f,.59f,.49f).linear,_colors[i],.1f);
            var mesh=IndexedMesh("silhouette-"+index);Clear();return mesh;
        }
        RoadmapModelLibrary.Model SemanticModel(RoadmapModelLibrary library,string id)
        {
            if(_tier<2&&OfflineAssets.TryGetValue(id,out var definition)&&definition.lod!=null)
            {var low=library.Get(definition.lod);if(low==null)throw new InvalidOperationException("Missing declared LOD: "+definition.lod);return low;}
            return library.Get(id);
        }
        static Color? EnsembleTint(ComposedInstance p)
        {
            if(p.wind||p.role=="landmark"||p.state==null)return null;
            if(p.state=="abandoned"||p.template=="ua.abandoned-homestead")return new Color(.90f,.94f,.88f);
            if(p.state=="partly-reclaimed"||p.state=="reclaimed")return new Color(.97f,.97f,.92f);
            return null;
        }
        void AppendSemanticNode(string nodeId)
        {
            var library=RoadmapModelLibrary.Load();library.PrepareCulture();
            foreach(var result in _offlineResults)
            {
                foreach(var p in result.instances)if(p.revealOwner==nodeId)
                {
                    var at=new Vector3(p.x,Ground(p.x,-p.z),-p.z);var visual=new RoadmapVisualProfile(_map.Environment.Sample(p.z*Application.RoadmapCompositionAdapter.Units));
                    bool bare=p.asset.Contains("tree")&&!p.asset.Contains("pine")&&SceneComposer.Unit(p.id,1918,1)<visual.Environment.Bare;
                    Append(bare?library.BareTree(p.asset):SemanticModel(library,p.asset),at,p.height,-p.yaw,null,new RoadmapSceneGenerator.Prop{Asset=p.asset,Sway=p.wind,HasVisual=true,Visual=visual,Position=at},EnsembleTint(p));
                }
                foreach(var span in result.spans)if(span.height==0&&result.instances.Exists(i=>i.id==span.a&&i.revealOwner==nodeId))
                    Trail(new Vector3(span.ax,Ground(span.ax,-span.az),-span.az),new Vector3(span.bx,Ground(span.bx,-span.bz),-span.bz),.38f,Color.Lerp(_map.VisualAt(span.az*Application.RoadmapCompositionAdapter.Units).Palette.Soil,_map.VisualAt(span.az*Application.RoadmapCompositionAdapter.Units).Palette.GrassLight,.30f));
            }
        }
        IEnumerator SemanticPaths(int chunkIndex)
        {
            var chunk=_map.Data.Chunks[chunkIndex];var doc=_offlineDocs[chunk.Region];
            foreach(var route in doc.routes)if(route.kind=="road"||route.kind=="path")
            for(int edge=1;edge<route.points.Length;edge++)
            {
                var a=route.points[edge-1];var b=route.points[edge];int steps=route.interpolation=="smooth-x"?24:1;
                Vector3 At(float t)=>new Vector3(Mathf.Lerp(a.x,b.x,steps==1?t:Mathf.SmoothStep(0,1,t)),0,-Mathf.Lerp(a.z,b.z,t));
                for(int step=0;step<steps;step++)
                {var start=At(step/(float)steps);var end=At((step+1)/(float)steps);var palette=_map.VisualAt(-start.z*Application.RoadmapCompositionAdapter.Units).Palette;OwnedTrail(start,end,route.width*.82f,Color.Lerp(palette.GrassLight,palette.Soil,.65f),chunk.Top,chunk.Bottom);}
                yield return null;
            }
        }
        bool ClearOfAuthoredRoad(float x,float sourceZ,float radius)
        {
            foreach(var doc in _offlineDocs)foreach(var route in doc.routes)if((route.kind=="road"||route.kind=="path")&&SceneComposer.DistanceTo(route,x,sourceZ,out _)<=radius+route.width)return false;
            return true;
        }
        void OwnedTrail(Vector3 start,Vector3 end,float width,Color color,float top,float bottom)
        {
            float a=-start.z*Application.RoadmapCompositionAdapter.Units,b=-end.z*Application.RoadmapCompositionAdapter.Units;
            if(Mathf.Abs(b-a)<.0001f){if(a>=top&&a<bottom)Trail(start,end,width,color);return;}
            float lower=Mathf.Max(0,Mathf.Min((top-a)/(b-a),(bottom-a)/(b-a))),upper=Mathf.Min(1,Mathf.Max((top-a)/(b-a),(bottom-a)/(b-a)));
            if(upper<=lower)return;var delta=end-start;Trail(start+delta*lower,start+delta*upper,width,color);
        }
        static void Drain(IEnumerator body)
        {while(body.MoveNext())if(body.Current is IEnumerator nested)Drain(nested);}
        Mesh IndexedMesh(string name)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var wind=new List<Vector2>();var uv=new List<Vector2>();var triangles=new List<int>();
            var seen=new Dictionary<(Vector3,Vector3,Color,Vector2,Vector2),int>();
            for(int i=0;i<_vertices.Count;i++)
            {
                var color=_colors[i];var normal=_normals[i];var point=_vertices[i];
                // Restrained baked local fill occlusion, never changes the shadow or reveal mask.
                float ao=Mathf.Lerp(.87f,1,Mathf.Clamp01(point.y*.65f+normal.y*.25f));color.r*=ao;color.g*=ao;color.b*=ao;
                var identity=(point,normal,color,_wind[i],_sourceUv[i]);
                if(!seen.TryGetValue(identity,out int index))
                {index=vertices.Count;seen.Add(identity,index);vertices.Add(point);normals.Add(normal);colors.Add(color);wind.Add(_wind[i]);uv.Add(_sourceUv[i]);}
                triangles.Add(index);
            }
            var mesh=new Mesh{name=name,indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uv);mesh.SetUVs(1,wind);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();var bounds=mesh.bounds;bounds.Expand(.7f);mesh.bounds=bounds;return mesh;
        }
        RoadmapModelLibrary.Model _cover;
        RoadmapModelLibrary.Model GroundCover()
        {
            if(_cover!=null)return _cover;var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();
            for(int blade=0;blade<3;blade++)
            {
                float angle=blade*2.094f;var right=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*.15f;var basePoint=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*.18f;
                var tip=basePoint+new Vector3(Mathf.Sin(angle)*.23f,1-blade*.13f,Mathf.Cos(angle)*.23f);
                for(int side=0;side<2;side++){var a=basePoint-right;var b=basePoint+right;var n=Vector3.Cross(b-a,tip-a).normalized;vertices.Add(side==0?a:b);vertices.Add(side==0?b:a);vertices.Add(tip);normals.Add(side==0?n:-n);normals.Add(side==0?n:-n);normals.Add(side==0?n:-n);colors.Add(new Color(.42f,.57f,.26f));}
            }
            return _cover=new RoadmapModelLibrary.Model{id="composed-short-cover",Positions=vertices.ToArray(),Normals=normals.ToArray(),Colors=colors.ToArray(),SourceHeight=1,Bounds=new Bounds(new Vector3(0,.5f,0),Vector3.one)};
        }
        bool ParcelOccupied(float x,float z,float padding)
        {
            foreach(var result in _offlineResults)foreach(var span in result.spans)if(span.height==0)
            {
                var a=new Vector2(span.ax,span.az);var b=new Vector2(span.bx,span.bz);var delta=b-a;var p=new Vector2(x,-z);float t=Mathf.Clamp01(Vector2.Dot(p-a,delta)/Mathf.Max(.0001f,delta.sqrMagnitude));if((p-a-delta*t).sqrMagnitude<(padding+.55f)*(padding+.55f))return true;
            }
            foreach(var result in _offlineResults)foreach(var p in result.instances)
            {if(p.role=="support")continue;float radius=OfflineAssets.TryGetValue(p.asset,out var asset)?asset.radius*p.height/asset.height:1.1f;if((new Vector2(x-p.x,-z-p.z)).sqrMagnitude<(radius+padding)*(radius+padding))return true;}
            return false;
        }
        LandscapeZone ZoneAt(float x,float z,string kind)
        {
            foreach(var doc in _offlineDocs)foreach(var zone in doc.zones)
                if(zone.kind==kind&&Mathf.Abs(x-zone.x)<zone.width*.5f&&Mathf.Abs(-z-zone.z)<zone.depth*.5f)return zone;
            return null;
        }
        bool ClearPowerCorridor(float x,float sourceZ,float radius)
        {
            foreach(var doc in _offlineDocs)foreach(var route in doc.routes)if((route.kind=="power"||route.kind=="distribution")&&SceneComposer.DistanceTo(route,x,sourceZ,out _)<=route.width+radius)return false;
            return true;
        }
        SurfaceRecipe SurfaceAt(float x,float z)
        {
            SurfaceRecipe found=null;
            foreach(var doc in _offlineDocs)foreach(var surface in doc.surfaces)
                if(SurfaceRecipes.Contains(surface,x,z))found=surface;
            return found;
        }
        bool DrySurface(float x,float sourceZ,float radius)
        {
            foreach(var doc in _offlineDocs)foreach(var surface in doc.surfaces)
                if(SurfaceRecipes.Wet(surface)&&(SurfaceRecipes.Contains(surface,x,sourceZ)||SurfaceRecipes.EdgeDistance(surface,x,sourceZ)<radius))return false;
            return true;
        }
        bool NearBank(float x,float sourceZ)=>DrySurface(x,sourceZ,.8f)&&!DrySurface(x,sourceZ,3.5f);
        bool WetSurface(float x,float sourceZ)=>SurfaceAt(x,sourceZ)?.kind=="channel";
        Color SurfaceColor(SurfaceRecipe surface,float sourceZ)
        {
            var palette=_map.VisualAt(sourceZ*Application.RoadmapCompositionAdapter.Units).Palette;
            switch(surface.kind)
            {
                case "channel":return Color.Lerp(new Color(.25f,.43f,.44f),palette.Fog,.12f);
                case "reservoir-bed":return Color.Lerp(palette.Soil,new Color(.56f,.57f,.43f),.45f);
                case "erosion":return Color.Lerp(palette.Soil,palette.GrassDark,.12f);
                case "pothole":return palette.Soil*.72f;
                case "gravel":case "bus-bay":case "service-yard":return Color.Lerp(palette.Soil,new Color(.49f,.49f,.44f),.5f);
                default:return Color.Lerp(palette.Soil,palette.GrassDark,.28f);
            }
        }
        IEnumerator SemanticSurfaces(int chunkIndex)
        {
            var chunk=_map.Data.Chunks[chunkIndex];float top=chunk.Top/Application.RoadmapCompositionAdapter.Units,bottom=chunk.Bottom/Application.RoadmapCompositionAdapter.Units;
            foreach(var doc in _offlineDocs)foreach(var surface in doc.surfaces)
            {
                var polygon=surface.points.Select(p=>new Vector2(p.x,p.z)).ToList();
                // Clip source-owned polygons to immutable chunk boundaries. No overlapping slabs.
                List<Vector2> Clip(List<Vector2> input,float limit,bool lower)
                {
                    var output=new List<Vector2>();if(input.Count==0)return output;
                    var previous=input[input.Count-1];bool before=lower?previous.y>=limit:previous.y<=limit;
                    foreach(var point in input)
                    {
                        bool inside=lower?point.y>=limit:point.y<=limit;
                        if(inside!=before){float t=(limit-previous.y)/(point.y-previous.y);output.Add(Vector2.Lerp(previous,point,t));}
                        if(inside)output.Add(point);previous=point;before=inside;
                    }return output;
                }
                polygon=Clip(Clip(polygon,top,true),bottom,false);if(polygon.Count<3)continue;
                Vector3 At(Vector2 point)=>new Vector3(point.x,Ground(point.x,-point.y)+.025f,-point.y);
                for(int i=1;i<polygon.Count-1;i++)
                {var a=At(polygon[0]);var b=At(polygon[i]);var c=At(polygon[i+1]);if(Vector3.Cross(b-a,c-a).y<0)Triangle(a,c,b,SurfaceColor(surface,polygon[i].y));else Triangle(a,b,c,SurfaceColor(surface,polygon[i].y));}
                yield return null;
            }
        }
        Color CompositionGround(Vector3 p,Color fallback)
        {
            if(_offlineDocs==null)return fallback;var visual=new RoadmapVisualProfile(_map.Environment.Sample(-p.z*Application.RoadmapCompositionAdapter.Units));
            var surface=SurfaceAt(p.x,-p.z);if(surface!=null)return SurfaceColor(surface,-p.z);
            var field=ZoneAt(p.x,p.z,"field");var forest=ZoneAt(p.x,p.z,"forest");
            float grain=.5f+.5f*Mathf.Sin(p.x*.8f+p.z*.23f)*Mathf.Cos(p.z*.57f);
            var color=Color.Lerp(visual.Palette.GrassLight,visual.Palette.GrassDark,.08f+grain*.16f);
            if(ZoneAt(p.x,p.z,"water")!=null)return Color.Lerp(new Color(.25f,.43f,.44f),visual.Palette.Fog,.12f);
            if(field!=null)color=Color.Lerp(color,visual.Palette.Soil,.10f+visual.Environment.Snow*.12f);
            else if(forest!=null)color=Color.Lerp(color,visual.Palette.Soil,.18f*(1-visual.Environment.Snow));
            if(ParcelOccupied(p.x,p.z,.45f))color=Color.Lerp(color,visual.Palette.Soil,.28f*(1-visual.Environment.Snow));
            return color;
        }
        IEnumerator SemanticTerrain(int chunkIndex)
        {
            var chunk=_map.Data.Chunks[chunkIndex];float top=chunk.Top/Application.RoadmapCompositionAdapter.Units,bottom=chunk.Bottom/Application.RoadmapCompositionAdapter.Units;
            var library=RoadmapModelLibrary.Load();library.PrepareCulture();
            foreach(var result in _offlineResults)
            {
                foreach(var p in result.instances)
                {
                    if(p.revealOwner!=null||p.z<top||p.z>=bottom)continue;
                    var at=new Vector3(p.x,Ground(p.x,-p.z),-p.z);var visual=new RoadmapVisualProfile(_map.Environment.Sample(p.z*Application.RoadmapCompositionAdapter.Units));
                    bool tree=p.asset.Contains("tree"),bare=tree&&!p.asset.Contains("pine")&&Application.RoadmapRuralLayout.Unit(1918,(int)(p.z*10),1)<visual.Environment.Bare;
                    var model=bare?library.BareTree(p.asset):library.Get(p.asset);
                    Append(model,at,p.height,-p.yaw,null,new RoadmapSceneGenerator.Prop{Asset=p.asset,Sway=p.wind,HasVisual=true,Visual=visual,Position=at},EnsembleTint(p));
                    yield return null;
                }
                foreach(var span in result.spans)
                {
                    if(span.height==0||Mathf.Max(span.az,span.bz)<top||Mathf.Min(span.az,span.bz)>=bottom)continue;
                    var a=new Vector3(span.ax,Ground(span.ax,-span.az),-span.az);var b=new Vector3(span.bx,Ground(span.bx,-span.bz),-span.bz);
                    if(span.height==0)Trail(a,b,.38f,Color.Lerp(_map.VisualAt(span.az*Application.RoadmapCompositionAdapter.Units).Palette.Soil,_map.VisualAt(span.az*Application.RoadmapCompositionAdapter.Units).Palette.GrassLight,.30f));
                    else
                    {
                        var pa=result.instances.Find(i=>i.id==span.a);var pb=result.instances.Find(i=>i.id==span.b);
                        Vector3 Socket(ComposedInstance support,AssetSocket socket)
                        {
                            var metadata=OfflineAssets[support.asset];var at=new Vector3(support.x,Ground(support.x,-support.z),-support.z);
                            // Convert the source model socket and its pivot using the same model transform.
                            return at+Quaternion.Euler(0,-support.yaw,0)*new Vector3(socket.x-metadata.pivot.x,socket.y-metadata.pivot.y,socket.z-metadata.pivot.z)/metadata.sourceHeight*support.height;
                        }
                        if(span.state=="intact")
                        {
                            var from=OfflineAssets[pa.asset].conductors;var to=OfflineAssets[pb.asset].conductors;
                            if(from.Length!=to.Length)throw new InvalidOperationException("Conductor socket count mismatch: "+span.id);
                            for(int wire=0;wire<from.Length;wire++)Cable(Socket(pa,from[wire]),Socket(pb,to[wire]),.75f,chunk.Top,chunk.Bottom);
                        }
                        else if(span.state!="removed")
                        {
                            void Hanging(ComposedInstance support,ComposedInstance other)
                            {
                                if(support.role!="support")return;
                                foreach(var socket in OfflineAssets[support.asset].conductors)
                                {
                                    var start=Socket(support,socket);var direction=new Vector3(other.x-support.x,0,-other.z+support.z).normalized;
                                    var end=start+direction*.65f-Vector3.up*span.dropLength;
                                    end.y=Mathf.Max(Ground(end.x,end.z)+.04f,end.y);
                                    Cable(start,end,.12f,chunk.Top,chunk.Bottom);
                                }
                            }
                            if(span.state=="severed"||span.state=="hanging-from")Hanging(pa,pb);
                            if(span.state=="severed"||span.state=="hanging-to")Hanging(pb,pa);
                        }
                    }
                    yield return null;
                }
            }
            yield return SemanticSurfaces(chunkIndex);
            // Global 2D cells preserve identity across seams. Detail changes model/cover spacing,
            // not geography; fields win over forest zones and parcel footprints exclude vegetation.
            const float step=3.5f;
            for(int zi=Mathf.FloorToInt(top/step);zi<Mathf.CeilToInt(bottom/step);zi++)
            for(int xi=-12;xi<=12;xi++)
            {
                int cell=zi*57+xi;float x=xi*step+(Application.RoadmapRuralLayout.Unit(1918,cell,1)-.5f)*1.8f;
                float sourceZ=(zi+Application.RoadmapRuralLayout.Unit(1918,cell,2))*step;if(sourceZ<top||sourceZ>=bottom)continue;
                float z=-sourceZ,distance=sourceZ*Application.RoadmapCompositionAdapter.Units;var visual=new RoadmapVisualProfile(_map.Environment.Sample(distance));
                var field=ZoneAt(x,z,"field");var forest=ZoneAt(x,z,"forest");
                var doc=_offlineDocs[_map.Data.RegionAt(distance)];int seed=doc.seed;
                if(!ClearOfAuthoredRoad(x,sourceZ,1.1f)||(ZoneAt(x,z,"water")!=null||WetSurface(x,sourceZ))||ParcelOccupied(x,z,1)||!Application.RoadmapRuralLayout.ClearOfPath(_map.Data,x,distance,1.1f)||!DrySurface(x,sourceZ,1.1f))continue;
                if(NearBank(x,sourceZ)&&visual.Environment.Snow<.6f)
                {
                    string bankAsset=SceneComposer.Unit("bank:"+cell,seed,4)<.25f?"ua_young_willow":"ua_reed_clump";
                    var at=new Vector3(x,Ground(x,z),z);float height=bankAsset=="ua_young_willow"?3.4f:1.1f;
                    Append(SemanticModel(library,bankAsset),at,height,cell*73,null,new RoadmapSceneGenerator.Prop{Asset=bankAsset,Sway=true,HasVisual=true,Visual=visual,Position=at});
                }
                else if(field!=null&&visual.Environment.Snow<.55f)
                {
                    string asset=field.species;if(asset=="ua_sunflower_patch"&&visual.Environment.Temperature<.4f)asset="ua_wheat_patch";
                    var model=SemanticModel(library,asset);
                    float coverHeight=asset=="ua_wheat_patch"?.78f:asset=="ua_sunflower_patch"?1.08f:OfflineAssets[asset].height;
                    for(int clump=0;clump<(_tier<2?1:2);clump++)
                    {var at=new Vector3(x+(clump-1)*.9f,Ground(x,z),z);Append(model,at,coverHeight,cell%17,null,new RoadmapSceneGenerator.Prop{Asset=asset,Sway=true,HasVisual=true,Visual=visual,Position=at});}
                }
                else if(forest!=null&&ClearPowerCorridor(x,sourceZ,1.3f)&&SceneComposer.Unit(forest?.id+":"+cell,seed,3)<forest.density)
                {
                    bool pine=SceneComposer.Unit(forest?.id+":"+cell,seed,4)<visual.Environment.Pines;string asset=pine?"tree_pineRoundA":"tree_default";
                    bool bare=!pine&&SceneComposer.Unit(forest?.id+":"+cell,seed,5)<visual.Environment.Bare;
                    var at=new Vector3(x,Ground(x,z),z);
                    Append(bare?library.BareTree(asset):library.Get(asset),at,2.7f+SceneComposer.Unit(forest?.id+":"+cell,seed,6)*1.7f,cell*137.5f,null,new RoadmapSceneGenerator.Prop{Asset=asset,Sway=true,HasVisual=true,Visual=visual,Position=at});
                }
            }
            // Short cover also on clearings. Keep real routes/doors visible instead of a bare ring.
            float coverStep=_tier<2?2.5f:1.75f;
            for(int zi=Mathf.FloorToInt(top/coverStep);zi<Mathf.CeilToInt(bottom/coverStep);zi++)
            for(int xi=-23;xi<=23;xi++)
            {
                int cell=zi*101+xi;float x=xi*coverStep,sourceZ=(zi+Application.RoadmapRuralLayout.Unit(333,cell,2))*coverStep;
                if(Mathf.Abs(x)>41||sourceZ<top||sourceZ>=bottom)continue;float z=-sourceZ,distance=sourceZ*Application.RoadmapCompositionAdapter.Units;
                var visual=new RoadmapVisualProfile(_map.Environment.Sample(distance));if(visual.Environment.Snow>.65f||(ZoneAt(x,z,"water")!=null||WetSurface(x,sourceZ))||ParcelOccupied(x,z,.1f)||!DrySurface(x,sourceZ,.15f))continue;
                if(ZoneAt(x,z,"field")!=null)continue;
                bool flower=Application.RoadmapRuralLayout.Unit(333,cell,4)<visual.Environment.Flowers*.22f;var at=new Vector3(x,Ground(x,z),z);string asset=flower?"flower_yellowA":"grass_leafsLarge";
                float height=flower?.32f:.15f+Application.RoadmapRuralLayout.Unit(333,cell,5)*.12f;
                if(!ClearOfAuthoredRoad(x,sourceZ,.15f)||!Application.RoadmapRuralLayout.ClearOfPath(_map.Data,x,distance,.15f))height*=.35f;
                Append(flower?library.Get(asset):GroundCover(),at,height,cell*73,null,new RoadmapSceneGenerator.Prop{Asset=asset,Sway=true,HasVisual=true,Visual=visual,Position=at});
            }
        }
    }
}
#endif
