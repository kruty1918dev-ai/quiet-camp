using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Kruty1918.ScenePlacement;
using Kruty1918.MeshStyle;
using Kruty1918.ModelWeathering;
using Kruty1918.FenceAuthoring;
using Kruty1918.RoadAuthoring;
using Kruty1918.PowerLines;
using Kruty1918.RockPlacement;
using Kruty1918.VegetationPlacement;

// Quiet Camp adapter, deliberately outside the reusable packages. Sources own
// every artistic decision; Core packages own construction and admissibility.
static class FirstTenEnvironmentBake
{
    static string root, report, content;
    static JObject configuration;
    static bool layoutOnly;
    static readonly List<object> layoutReceipts=new List<object>();
    static readonly Dictionary<string,MeshData> models=new Dictionary<string,MeshData>();
    static readonly Dictionary<string,object> styleReceipts=new Dictionary<string,object>();
    static string Sha(byte[] bytes)=>BitConverter.ToString(SHA256.Create().ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
    static V3 Vector(JToken t)=>new V3((float)t[0],(float)t[1],(float)t[2]);
    static V3[] Points(JToken t)=>t.Select(Vector).ToArray();
    static Tint Mix(Tint a,Tint b,float t)=>new Tint(a.r+(b.r-a.r)*t,a.g+(b.g-a.g)*t,a.b+(b.b-a.b)*t);
    static float Noise(float x,float z,int seed)
    {
        int ix=(int)Math.Floor(x),iz=(int)Math.Floor(z);float u=Numbers.Smooth(x-ix),v=Numbers.Smooth(z-iz);
        float A(int dx,int dz)=>Numbers.Unit((ix+dx)+"/"+(iz+dz),seed);
        return (A(0,0)*(1-u)+A(1,0)*u)*(1-v)+(A(0,1)*(1-u)+A(1,1)*u)*v;
    }
    sealed class Landscape : ISurfaceQuery,IEnvironmentalFields
    {
        readonly JObject p;readonly float halfW,halfH;readonly int seed;
        public Landscape(JObject p){this.p=p;halfW=(float)p["width"]*.5f;halfH=(float)p["height"]*.5f;seed=(int)p["seed"];}
        float BaseHeight(float x,float z)
        {
            float border=Math.Max(Math.Abs(x)-halfW-.15f,Math.Abs(z)-halfH-.15f);
            float mask=Numbers.Smooth(Numbers.Clamp(border/(float)p["terrain"]["transitionWidth"],0,1));
            float h=(Noise(x*.24f,z*.24f,seed)-.5f)*(float)p["terrain"]["baseUndulation"];
            foreach(var f in p["terrain"]["features"])
            {
                float dx=x-(float)f["centerXZ"][0],dz=z-(float)f["centerXZ"][1];float r=(float)f["radius"];
                h+=(float)f["height"]*(float)Math.Exp(-(dx*dx+dz*dz)/(r*r*.65f));
            }
            foreach(var e in p["extras"].Where(e=>(string)e["kind"]=="spring"))
            {
                var c=Vector(e["center"]);float d=(float)Math.Sqrt((x-c.x)*(x-c.x)+(z-c.z)*(z-c.z));
                h-=(float)e["waterDepth"]*Numbers.Smooth(Numbers.Clamp(1-d/(float)e["radius"],0,1));
            }
            return -.012f+h*mask;
        }
        public float Height(float x,float z)
        {
            float height=BaseHeight(x,z);
            foreach(var pad in p["terrain"]["supportPads"]??new JArray())
            {
                float cx=(float)pad["centerXZ"][0],cz=(float)pad["centerXZ"][1];
                float outside=Math.Max(Math.Abs(x-cx)-(float)pad["halfSizeXZ"][0],Math.Abs(z-cz)-(float)pad["halfSizeXZ"][1]);
                float blend=1-Numbers.Smooth(outside/(float)pad["blendWidth"]);
                height+= (BaseHeight(cx,cz)-height)*blend;
            }
            return height;
        }
        public bool TrySample(V3 target,V3 direction,out SurfaceSample sample)
        {
            float y=Height(target.x,target.z),d=.035f;
            sample=new SurfaceSample{point=new V3(target.x,y,target.z),normal=new V3((Height(target.x-d,target.z)-Height(target.x+d,target.z))/(2*d),1,(Height(target.x,target.z-d)-Height(target.x,target.z+d))/(2*d)).Normalized,tag="ground",moisture=.4f,light=.7f};
            foreach(var e in p["extras"].Where(e=>(string)e["kind"]=="spring"))
            {var c=Vector(e["center"]);if(new V3(target.x-c.x,0,target.z-c.z).Length<(float)e["radius"]*.62f)sample.tag="water";}
            return Math.Abs(direction.y)>.99f;
        }
        public float Sample(string field,V3 q,float fallback)
        {
            if(field=="moisture")return Numbers.Clamp(.35f-Height(q.x,q.z)*.2f,0,1);
            if(field=="light")return .75f;
            if(field=="density")
            {
                float edge=Math.Max(Math.Abs(q.x)-halfW,Math.Abs(q.z)-halfH);
                if(edge<.3f)return 0;
                float patch=Noise(q.x*.52f,q.z*.52f,seed+59);
                return Numbers.Clamp((patch-.2f)*1.8f,.05f,1);
            }
            return fallback;
        }
    }
    sealed class SightlineRule : IPlacementRule
    {
        readonly float w,h,foreground,background;public SightlineRule(JObject p){w=(float)p["width"]*.5f;h=(float)p["height"]*.5f;foreground=(float)p["sightline"]["foregroundTreeClearance"];background=(float)p["sightline"]["treeClearanceFromBoard"];}
        public Diagnostic Check(PlacementRequest request,PlacedInstance candidate,PlacementContext context)
        {
            if(!request.shape.id.StartsWith("tree-")&&!request.shape.id.StartsWith("poplar-")&&!request.shape.id.StartsWith("orchard-"))return null;
            float d=Math.Max(Math.Abs(candidate.pose.position.x)-w,Math.Abs(candidate.pose.position.z)-h);
            float near=candidate.pose.position.x+candidate.pose.position.z<0?foreground:background;
            return d<near?new Diagnostic("camera-sightline",request.id,"Keep the board and narrative foreground readable"):null;
        }
    }
    sealed class ShadeRule : IPlacementRule
    {
        readonly JObject p,level;
        public ShadeRule(JObject p,JObject level){this.p=p;this.level=level;}
        public Diagnostic Check(PlacementRequest request,PlacedInstance candidate,PlacementContext context)
        {
            string species=request.shape.id;
            if(!species.StartsWith("tree-")&&!species.StartsWith("orchard-")&&!species.StartsWith("poplar-"))return null;
            float elevation=(float)p["sunElevation"]*Numbers.Radians,ratio=(float)(Math.Cos(elevation)/Math.Sin(elevation));
            var ray=new V3((float)Math.Sin(65*Numbers.Radians)*ratio,0,(float)Math.Cos(65*Numbers.Radians)*ratio);
            foreach(var volume in candidate.volumes)
            {
                var projected=new List<V3>();
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                {var v=volume.center+volume.x*(volume.half.x*x)+volume.y*(volume.half.y*y)+volume.z*(volume.half.z*z);projected.Add(new V3(v.x+ray.x*Math.Max(0,v.y),0,v.z+ray.z*Math.Max(0,v.y)));}
                var shadow=Box.FromPoints(projected);float w=(float)p["width"]*.5f,h=(float)p["height"]*.5f;
                // Conservative swept projection: incidental scenic canopies
                // may not imply a new logical shade area on playable cells.
                for(int z=0;z<(int)p["height"];z++)for(int x=0;x<(int)p["width"];x++)
                {
                    bool shaded=(level["shade"]??new JArray()).Any(c=>(int)c[0]==x&&(int)c[1]==z);
                    if(!shaded&&Math.Abs((x+.5f-w)-shadow.center.x)<shadow.half.x+.42f&&Math.Abs((z+.5f-h)-shadow.center.z)<shadow.half.z+.42f)return new Diagnostic("protected-gameplay-shade",request.id,"Scenic shadow intersects an unshaded puzzle cell");
                }
            }
            return null;
        }
    }
    sealed class Batch
    {
        public readonly GeometryBuilder mesh=new GeometryBuilder();public readonly List<float> roots=new List<float>();
        public readonly List<object> objects=new List<object>();
        public void Append(string id,string source,MeshData shape,Pose pose,float windHeight=-1)
        {
            mesh.Append(shape,pose,id+"/");
            for(int i=0;i<shape.positions.Length;i++)roots.AddRange(new[]{pose.position.x,pose.position.z,pose.position.y,windHeight});
            var b=new OrientedBox(shape.Bounds,pose).Bounds;
            objects.Add(new{id,source,position=new[]{pose.position.x,pose.position.y,pose.position.z},bounds=new[]{b.center.x,b.center.y,b.center.z,b.half.x,b.half.y,b.half.z},triangles=shape.TriangleCount});
        }
    }
    static void LoadModels()
    {
        foreach(string file in new[]{"roadmap_models","roadmap_story_models","campaign100_models"})
        foreach(var m in JArray.Parse(File.ReadAllText(Path.Combine(content,file+".json"))))
        {
            float[] data=m["data"].Values<float>().ToArray();int[] colors=m["colors"].Values<int>().ToArray();int count=data.Length/6;
            var mesh=new MeshData{positions=new V3[count],normals=new V3[count],colors=new Tint[count],uv=new V2[count],indices=Enumerable.Range(0,count).ToArray()};
            for(int i=0;i<count;i++)
            {mesh.positions[i]=new V3(data[i*6],data[i*6+1],data[i*6+2]);mesh.normals[i]=new V3(data[i*6+3],data[i*6+4],data[i*6+5]);int c=colors[i/3];mesh.colors[i]=new Tint(((c>>16)&255)/255f,((c>>8)&255)/255f,(c&255)/255f);}
            // Some source exports contain zero-area faces. They contribute
            // neither surface nor shadow and otherwise yield zero normals.
            // Compact an independent copy; never rewrite the imported model.
            var live=Enumerable.Range(0,mesh.TriangleCount).Where(t=>V3.Cross(mesh.positions[t*3+1]-mesh.positions[t*3],mesh.positions[t*3+2]-mesh.positions[t*3]).Length>1e-7f).SelectMany(t=>new[]{t*3,t*3+1,t*3+2}).ToArray();
            mesh=new MeshData{positions=live.Select(i=>mesh.positions[i]).ToArray(),normals=live.Select(i=>mesh.normals[i]).ToArray(),colors=live.Select(i=>mesh.colors[i]).ToArray(),uv=live.Select(i=>mesh.uv[i]).ToArray(),indices=Enumerable.Range(0,live.Length).ToArray()};
            mesh.Validate();models[(string)m["id"]]=mesh;
        }
    }
    static MeshData Styled(string source,float height,bool aged,int seed,float weathering=.52f)
    {
        var input=models[source].Copy();
        if(aged)
        {
            var bottom=input.Bounds.center.y-input.Bounds.half.y;
            var supports=Enumerable.Range(0,input.TriangleCount).Where(t=>Enumerable.Range(0,3).Any(k=>input.positions[input.indices[t*3+k]].y<bottom+(float)configuration["style"]["preserveFeatureMinimumHeight"])).ToArray();
            input.parts=new[]{new MeshPart{id="support",role="support",preserve=true,triangles=supports},new MeshPart{id="body",role="surface",triangles=Enumerable.Range(0,input.TriangleCount).Except(supports).ToArray()}};
        }
        var style=configuration["style"];float referenceHeight=(float)style["referenceHeight"],area=(float)style["referenceProjectedArea"];
        var result=MeshHarmonizer.Harmonize(input,new StyleProfile{referenceTriangles=(int)style["referenceTriangles"],minimumTriangles=24,maximumTriangles=320,referenceArea=area,worldScale=height,maximumWorldError=(float)style["maximumWorldError"],maximumCandidateTests=160000,maximumCollapses=512,maximumColorError=.05f,normals=NormalMode.Flat},Math.Max(80,area*height*height/(referenceHeight*referenceHeight)));
        styleReceipts[source+"@"+height]=new{source,scale=height,result.inputTriangles,result.targetTriangles,outputTriangles=result.mesh.TriangleCount,result.maximumDeviation,diagnostics=result.diagnostics};
        if(!aged)return result.mesh;
        return Weathering.Apply(source,result.mesh,new WeatheringProfile{seed=seed,intensity=weathering,damageProbability=0,maximumRemovedParts=0,fallback=new MaterialWeathering{warp=.012f,sag=.006f,chip=.003f,fade=.19f,soil=.25f}}).mesh;
    }
    static AssetShape Shape(string id,MeshData mesh,bool contacts=true)
    {
        var shape=AssetShape.FromMesh(id,mesh);
        if(contacts)
        {
            // Bottom extrema are the actual feet/roots, not empty bbox corners.
            float y=mesh.Bounds.center.y-mesh.Bounds.half.y;
            var feet=mesh.positions.Where(v=>v.y<=y+.009f).ToArray();
            if(feet.Length>0)shape.contacts=new[]{feet.OrderBy(v=>v.x+v.z).First(),feet.OrderByDescending(v=>v.x+v.z).First(),feet.OrderBy(v=>v.x-v.z).First(),feet.OrderByDescending(v=>v.x-v.z).First()}.Distinct().ToArray();
        }
        return shape;
    }
    static AssetShape SupportShape(string id,MeshData mesh)
    {
        var shape=Shape(id,mesh);float bottom=mesh.Bounds.center.y-mesh.Bounds.half.y;
        var trunk=new List<V3>();var upper=new List<V3>();
        for(int t=0;t<mesh.TriangleCount;t++)
        {
            var points=Enumerable.Range(0,3).Select(k=>mesh.positions[mesh.indices[t*3+k]]).ToArray();
            (points.Any(v=>v.y<bottom+.05f)?trunk:upper).AddRange(points);
        }
        // A crossarm above the player is not a ground-level obstruction.
        // These volumes still bound every piece of the final weathered mesh.
        shape.volumes=new[]{Box.FromPoints(trunk.ToArray()),Box.FromPoints(upper.ToArray())};return shape;
    }
    static MeshData Terrain(JObject p,Landscape surface)
    {
        float step=(float)p["terrain"]["sampleStep"];float min=(float)p["terrain"]["extent"][0],max=(float)p["terrain"]["extent"][1];int n=(int)Math.Round((max-min)/step)+1;
        var mesh=new MeshData{positions=new V3[n*n],normals=new V3[n*n],colors=new Tint[n*n],uv=new V2[n*n],indices=new int[(n-1)*(n-1)*6]};int seed=(int)p["seed"];
        for(int z=0;z<n;z++)for(int x=0;x<n;x++)
        {
            var v=new V3(min+x*step,0,min+z*step);surface.TrySample(v,-V3.Up,out var sample);int i=z*n+x;mesh.positions[i]=sample.point;mesh.normals[i]=sample.normal;
            float patch=Noise(v.x*.35f,v.z*.35f,seed);mesh.colors[i]=Mix(new Tint(.34f,.43f,.25f),new Tint(.48f,.56f,.33f),patch);
        }
        int k=0;for(int z=0;z<n-1;z++)for(int x=0;x<n-1;x++){int a=z*n+x,b=a+1,c=a+n,d=c+1;foreach(int index in new[]{a,c,b,b,c,d})mesh.indices[k++]=index;}
        mesh.Validate();return mesh;
    }
    static void Require(bool ok,IEnumerable<Diagnostic> diagnostic,string id)
    {if(!ok)throw new InvalidOperationException(id+": "+string.Join("; ",diagnostic.Where(d=>d.error).Select(d=>d.ToString())));}
    static PlacedInstance Put(string id,string source,MeshData mesh,V3 target,float scale,float yaw,PlacementContext context,Batch batch,float wind=-1,string[] allowed=null,float supportGap=.035f,float settlement=.018f)
    {
        var result=PlacementSolver.Place(new PlacementRequest{id=id,owner=id.Split('/')[0]+"/place",shape=Shape(source,mesh),target=target,scale=scale,yaw=yaw,settlement=settlement,maxSupportGap=supportGap,alignToSurface=true,allowedContacts=allowed??Array.Empty<string>()},context);
        Require(result.Ok,result.diagnostics,id);var instance=result.instances.Single();batch.Append(id,source,mesh,instance.pose,wind);return instance;
    }
    static MeshData BoxMesh(V3 center,V3 size,Tint tint){var b=new GeometryBuilder();b.Box(center,size,tint);return b.Build();}
    static void Structures(JObject p,Landscape surface,PlacementContext context,Batch props,List<object> links)
    {
        int seed=(int)p["seed"];
        foreach(var extra in p["extras"].OfType<JObject>())
        {
            string kind=(string)extra["kind"],id=(string)extra["id"];
            if(kind=="spring")
            {
                V3 center=Vector(extra["center"]);float radius=(float)extra["radius"];
                // Low masonry ring has an opening towards the approach; no roof.
                var b=new GeometryBuilder();float rimY=0;var stone=BoxMesh(new V3(0,.095f,0),new V3(.29f,.19f,.15f),new Tint(.42f,.44f,.36f));
                for(int i=0;i<10;i++)
                {
                    float a=(i+.5f)*6.2831853f/12-6.2831853f/6;var q=center+new V3((float)Math.Cos(a)*radius,0,(float)Math.Sin(a)*radius);
                    var placement=PlacementSolver.Place(new PlacementRequest{id=id+"/block-"+i,owner=id,shape=Shape("spring-stone",stone),target=q,yaw=90-a/Numbers.Radians,alignToSurface=true,settlement=.025f,maxSupportGap=.035f},context);
                    Require(placement.Ok,placement.diagnostics,id);b.Append(stone,placement.instances.Single().pose);rimY+=placement.instances.Single().pose.position.y;
                }
                surface.TrySample(center,-V3.Up,out var ground);var pose=Pose.At(new V3(center.x,rimY/10,center.z));props.Append(id,"authored-spring-masonry",b.Build(),Pose.At(V3.Zero));
                context.Reserve(id,new OrientedBox(new Box(new V3(0,.1f,0),new V3(radius+.12f,.3f,radius+.12f)),pose));
                // Two recessed planes give water a dark edge and a light centre.
                var water=new GeometryBuilder();for(int i=0;i<12;i++){float a=i*6.2831853f/12,beta=(i+1)*6.2831853f/12;water.Triangle(V3.Zero,new V3((float)Math.Cos(beta)*radius*.73f,0,(float)Math.Sin(beta)*radius*.73f),new V3((float)Math.Cos(a)*radius*.73f,0,(float)Math.Sin(a)*radius*.73f),new Tint(.27f,.45f,.45f));}
                props.Append(id+"/water","authored-spring-water",water.Build(),Pose.At(new V3(center.x,ground.point.y+(float)extra["waterDepth"]*.8f,center.z)));
            }
            if(kind=="ruin")
            {
                var c=Vector(extra["center"]);float w=(float)extra["width"],d=(float)extra["depth"],h=(float)extra["wallHeight"],door=(float)extra["doorWidth"];var b=new GeometryBuilder();var tint=new Tint(.51f,.49f,.39f);
                b.Box(new V3(0,.04f,0),new V3(w,.08f,d),new Tint(.41f,.42f,.33f));
                b.Box(new V3(0,h*.5f,d*.5f),new V3(w,h,.14f),tint);b.Box(new V3(-w*.5f,h*.4f,0),new V3(.14f,h*.8f,d),tint);b.Box(new V3(w*.5f,h*.55f,0),new V3(.14f,h*1.1f,d),tint);
                float half=(w-door)*.5f;for(int s=-1;s<=1;s+=2)b.Box(new V3(s*(door*.5f+half*.5f),h*.35f,-d*.5f),new V3(half,h*.7f,.14f),tint);
                Put(id,"authored-house-remains",b.Build(),c,1,0,context,props);
            }
            if(kind=="fence")
            {
                var construction=configuration["construction"];float h=(float)extra["height"],bay=(float)construction["fenceBayLength"],postWidth=(float)construction["postWidth"],rail=(float)construction["railWidth"];
                var palette=construction["palette"];var tint=new Tint((float)palette[0],(float)palette[1],(float)palette[2]);int slats=(int)construction["slatCount"];
                var post=BoxMesh(new V3(0,h*.5f,0),new V3(postWidth,h,postWidth),tint);var b=new GeometryBuilder();
                b.Box(new V3(0,-h*.14f,0),new V3(bay,rail,.04f),tint);b.Box(new V3(0,h*.20f,0),new V3(bay,rail,.04f),tint);
                for(int i=0;i<slats;i++)b.Box(new V3((i-(slats-1)*.5f)*bay*.85f/slats,0,0),new V3((float)construction["slatWidth"],h*.67f,.032f),tint);
                var section=b.Build();var gate=section.Copy();
                MeshData AgeKit(string name,MeshData model,bool protectAll)
                {
                    var protectedFaces=Enumerable.Range(0,model.TriangleCount).Where(t=>protectAll||Enumerable.Range(0,3).Any(k=>Math.Abs(model.positions[model.indices[t*3+k]].x)>bay*.47f)).ToArray();
                    model.parts=new[]{new MeshPart{id="joints",role="support",preserve=true,triangles=protectedFaces},new MeshPart{id="boards",role="board",triangles=Enumerable.Range(0,model.TriangleCount).Except(protectedFaces).ToArray()}};
                    return Weathering.Apply(name,model,new WeatheringProfile{seed=seed,intensity=.55f,damageProbability=0,maximumRemovedParts=0}).mesh;
                }
                post=AgeKit(id+"/post",post,true);section=AgeKit(id+"/section",section,false);gate=AgeKit(id+"/gate",gate,false);
                var kit=new FenceKit{post=Shape("yard-post",post),section=Shape("yard-section",section),gate=Shape("yard-gate",gate),railSocket=new V3(0,h*.47f,0),sectionLength=bay,minStretch=.3f,maxStretch=1.5f};
                var openings=extra["gateFrom"]==null?Array.Empty<FenceOpening>():new[]{new FenceOpening{id=id+"/gate",from=(float)extra["gateFrom"],to=(float)extra["gateTo"],gate=true}};
                var fence=FenceGenerator.Generate(new FenceSpec{id=id,seed=seed,contour=Points(extra["contour"]),closed=(bool)extra["closed"],jitter=.027f,lean=4,maxSupportGap=.1f,openings=openings},kit,context);
                Require(fence.Ok,fence.diagnostics,id);
                foreach(var instance in fence.instances){var model=instance.asset=="yard-post"?post:instance.asset=="yard-gate"?gate:section;props.Append(instance.id,instance.asset,model,instance.pose);}
                links.Add(new{id,kind="fence",closed=(bool)extra["closed"],connections=fence.connections.Select(c=>new{c.id,c.a,c.b,c.gate,from=new[]{c.from.x,c.from.y,c.from.z},to=new[]{c.to.x,c.to.y,c.to.z}})});
            }
        }
    }
    static void ShadeTree(JObject p,JObject level,Landscape surface,PlacementContext context,Batch trees)
    {
        foreach(var crown in level["canopies"]??new JArray())
        {
            // Real, rooted crown projects into the authored ellipse. The
            // logical shade remains unchanged, as does the tutorial witness.
            float elevation=(float)p["sunElevation"];
            float sy=(float)Math.Sin(elevation*Numbers.Radians),cos=(float)Math.Cos(elevation*Numbers.Radians),dx=(float)Math.Sin(65*Numbers.Radians)*cos,dz=(float)Math.Cos(65*Numbers.Radians)*cos;
            float height=4.6f,centreHeight=height*.72f;
            var center=new V3((float)crown["x"]-(float)p["width"]*.5f,0,(float)crown["z"]-(float)p["height"]*.5f);
            var position=center-new V3(dx,0,dz)*(centreHeight/sy);surface.TrySample(position,-V3.Up,out var ground);
            var mesh=models["tree_default"].Copy();float sx=(float)crown["radiusX"]*2/(mesh.Bounds.half.x*2),sz=(float)crown["radiusZ"]*2/(mesh.Bounds.half.z*2);
            var pose=Pose.At(new V3(position.x,ground.point.y-.018f,position.z),0,new V3(sx,height,sz));
            trees.Append((string)p["id"]+"/shade-tree","tree_default",mesh,pose,height);
            // Register the trunk and crown separately: lower vegetation can
            // live beneath the crown while leaving the root free.
            context.Reserve((string)p["id"]+"/shade-trunk",new OrientedBox(new Box(new V3(0,.2f,0),new V3(.065f,.2f,.065f)),pose));
            context.Reserve((string)p["id"]+"/shade-crown",new OrientedBox(new Box(new V3(0,.7f,0),new V3(.23f,.3f,.21f)),pose));
        }
    }
    static void Power(JObject p,Dictionary<string,RoadResult> roads,PlacementContext context,Batch props,List<object> links)
    {
        foreach(var e in p["extras"].Where(q=>(string)q["kind"]=="power"))
        {
            string id=(string)e["id"];float height=(float)e["supportHeight"];var source=Styled("ua_village_wood_pole",height,true,(int)p["seed"]);
            var spec=new PowerSpec{id=id,route=roads[(string)e["routeId"]].route,offset=(float)e["offset"],minimumSpan=(float)e["minimumSpan"],maximumSpan=(float)e["maximumSpan"],scale=height,sag=(float)e["sag"],radius=.008f,samples=18,seed=(int)p["seed"],damage=new[]{new CableDamage{span=(int)e["severedSpan"],state=CableState.Severed,dropLength=1.8f}}};
            var kit=new SupportKit{standing=SupportShape("ua_village_wood_pole",source),fallen=Shape("ua_village_broken_pole",models["ua_village_broken_pole"]),sockets=new[]{new V3(-.07f,.9f,0),new V3(.07f,.9f,0)}};
            var result=PowerGenerator.Generate(spec,kit,context);Require(result.Ok,result.diagnostics,id);
            foreach(var instance in result.supports)props.Append(instance.id,instance.asset,source,instance.pose);
            foreach(var span in result.spans)props.Append(span.id,"power-conductor",span.mesh,Pose.At(V3.Zero));
            links.Add(new{id,kind="power",supports=result.supports.Select(s=>s.id),spans=result.spans.Select(s=>new{s.id,s.from,s.to,s.socket,state=s.state.ToString(),points=s.points.Select(q=>new[]{q.x,q.y,q.z})})});
        }
    }
    static void Plants(JObject p,Landscape surface,PlacementContext context,Batch trees,Batch ground,List<object> receipts)
    {
        int seed=(int)p["seed"];var tree=models["tree_default"].Copy();var orchard=models["ua_valley_orchard"].Copy();var poplar=models["ua_valley_poplar"].Copy();
        AssetShape TreeShape(string id,MeshData m)
        {var b=m.Bounds;return new AssetShape{id=id,contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,.19f,0),new V3(.065f,.19f,.065f)),new Box(new V3(b.center.x,.70f,b.center.z),new V3(b.half.x,.30f,b.half.z)).Expanded(.075f)}};}
        var species=new[]{new Species{id="tree-broadleaf",shape=TreeShape("tree-broadleaf",tree),tier=0,minimumScale=3.2f,maximumScale=4.6f,spacing=.8f,weight=1},new Species{id="orchard-old",shape=TreeShape("orchard-old",orchard),tier=0,minimumScale=2.7f,maximumScale=3.8f,spacing=.78f,weight=p["planting"]["orchard"]!=null?1:.15f},new Species{id="poplar-young",shape=TreeShape("poplar-young",poplar),tier=0,minimumScale=4.1f,maximumScale=5.4f,spacing=.58f,weight=p["planting"]["poplars"]!=null?1:.1f}};
        var settings=p["planting"];Box Zone(JToken q,float y)=>new Box(new V3(((float)q[0]+(float)q[1])*.5f,0,((float)q[2]+(float)q[3])*.5f),new V3(((float)q[1]-(float)q[0])*.5f,y,((float)q[3]-(float)q[2])*.5f));
        var spec=new VegetationSpec{id=(string)p["id"]+"/trees",seed=seed,zone=Zone(settings["treeZone"],10),cellSize=(float)settings["treeCellSize"],density=(float)settings["treeDensity"],patchScale=.18f,patchContrast=.72f,species=species};
        context.rules.Add(new SightlineRule(p));var result=VegetationGenerator.Generate(spec,context,surface);Require(result.Ok,result.diagnostics,(string)p["id"]);
        foreach(var instance in result.instances)trees.Append(instance.id,instance.asset,instance.asset=="tree-broadleaf"?tree:instance.asset=="orchard-old"?orchard:poplar,instance.pose,instance.pose.scale.y);
        receipts.Add(new{kind="trees",count=result.instances.Count,result.evaluated,diagnostics=result.diagnostics});
        var grass=Styled("grass_leafsLarge",.22f,false,seed);var weeds=models["ua_field_weeds"].Copy();var bush=models["plant_bushSmall"].Copy();var flower=models[(string)settings["daisyVariant"]["source"]].Copy();
        // Preserve stems; white petal heads with a visible gold central disc.
        var daisy=settings["daisyVariant"];var petal=daisy["petalColor"];var centre=daisy["centreColor"];
        for(int i=0;i<flower.colors.Length;i++)if(flower.colors[i].r>.72f)flower.colors[i]=new Tint((float)petal[0],(float)petal[1],(float)petal[2]);
        var head=new GeometryBuilder();head.Append(flower,Pose.At(V3.Zero));head.Box(new V3(0,.99f,0),new V3(.10f,.025f,.10f),new Tint((float)centre[0],(float)centre[1],(float)centre[2]));flower=head.Build();
        Species S(string id,MeshData m,float lo,float hi,float weight,int tier,float spacing){var shape=Shape(id,m,false);shape.motionMargin=.035f;return new Species{id=id,shape=shape,tier=tier,minimumScale=lo,maximumScale=hi,weight=weight,spacing=spacing};}
        var green=new VegetationSpec{id=(string)p["id"]+"/understory",seed=seed+1,zone=Zone(settings["groundZone"],4),cellSize=(float)settings["groundCellSize"],density=(float)settings["groundDensity"],patchScale=(float)settings["patchScale"],patchContrast=(float)settings["patchContrast"],species=new[]{S("grass-blades",grass,.12f,.24f,.8f,2,.25f),S("field-weeds",weeds,.14f,.29f,6,2,.25f),S("daisy",flower,(float)daisy["minimumHeight"],(float)daisy["maximumHeight"],.55f,2,.30f),S("shrub",bush,.35f,.7f,.28f,1,.65f)}};
        result=VegetationGenerator.Generate(green,context,surface);Require(result.Ok,result.diagnostics,(string)p["id"]);
        foreach(var i in result.instances)ground.Append(i.id,i.asset,i.asset=="grass-blades"?grass:i.asset=="field-weeds"?weeds:i.asset=="daisy"?flower:bush,i.pose,i.pose.scale.y);
        receipts.Add(new{kind="understory",count=result.instances.Count,result.evaluated,diagnostics=result.diagnostics});
    }
    static object WriteMesh(string folder,string name,Batch batch)
    {
        var mesh=batch.mesh.Build();mesh.Validate();string file=name+".bytes";string path=Path.Combine(folder,file);
        using(var writer=new BinaryWriter(File.Create(path)))
        {
            writer.Write(Encoding.ASCII.GetBytes("QCENV001"));writer.Write(mesh.positions.Length);writer.Write(mesh.indices.Length);
            for(int i=0;i<mesh.positions.Length;i++)
            {
                var v=mesh.positions[i];var n=mesh.normals[i];var c=mesh.colors[i];writer.Write(v.x);writer.Write(v.y);writer.Write(v.z);writer.Write(n.x);writer.Write(n.y);writer.Write(n.z);
                writer.Write((byte)Math.Round(Numbers.Clamp(c.r,0,1)*255));writer.Write((byte)Math.Round(Numbers.Clamp(c.g,0,1)*255));writer.Write((byte)Math.Round(Numbers.Clamp(c.b,0,1)*255));writer.Write((byte)255);
                for(int k=0;k<4;k++)writer.Write(batch.roots[i*4+k]);
            }
            foreach(int i in mesh.indices)writer.Write(i);
        }
        return new{file=Path.GetFileNameWithoutExtension(file),sha256=Sha(File.ReadAllBytes(path)),vertices=mesh.positions.Length,triangles=mesh.TriangleCount,kind=name,objects=batch.objects};
    }
    static void Bake(JObject p,JObject level,string staging,string revision)
    {
        string id=(string)p["id"];int seed=(int)p["seed"];var surface=new Landscape(p);var context=new PlacementContext(surface);
        float w=(float)p["width"]*.5f,h=(float)p["height"]*.5f;
        context.Reserve(id+"/playable",new OrientedBox(new Box(new V3(0,1,0),new V3(w+.12f,1,h+.12f)),Pose.At(V3.Zero)));
        var land=new Batch();land.Append(id+"/terrain","authored-terrain",Terrain(p,surface),Pose.At(V3.Zero));
        var props=new Batch();var paths=new Batch();var trees=new Batch();var green=new Batch();var links=new List<object>();var diagnostics=new List<object>();
        var roads=new Dictionary<string,RoadResult>();
        foreach(var r in p["routes"])
        {
            bool asphalt=(string)r["surface"]=="worn-asphalt";var points=Points(r["points"]);float width=(float)r["width"];
            var result=RoadGenerator.Generate(new RoadSpec{id=(string)r["id"],points=points,seed=seed,step=(float)r["sampleStep"],heightOffset=.02f,edgeRoughness=(float)r["edgeRoughness"],bands=new[]{new SurfaceBand{distance=0,width=width,tint=asphalt?new Tint(.32f,.34f,.31f):new Tint(.49f,.44f,.33f),surface=(string)r["surface"]},new SurfaceBand{distance=new RoutePath(points).Length,width=width*.89f,tint=asphalt?new Tint(.39f,.39f,.31f):new Tint(.43f,.42f,.30f),surface=(string)r["surface"]}},potholes=asphalt?new[]{points[1],points[2]}:Array.Empty<V3>(),potholeRadius=.24f},surface);
            if(!result.Ok)foreach(var a in result.terrainAdjustments)Console.WriteLine("Road adjustment "+a.point+" requested "+a.requestedHeight);
            Require(result.Ok,result.diagnostics,(string)r["id"]);roads.Add((string)r["id"],result);paths.Append((string)r["id"],(string)r["surface"],result.mesh,Pose.At(V3.Zero));
            links.Add(new{id=(string)r["id"],kind="road",points=result.route.points.Select(q=>new[]{q.x,q.y,q.z}),widths=result.widths,terrainAdjustments=result.terrainAdjustments.Select(a=>new{a.requestedHeight,a.reason,point=new[]{a.point.x,a.point.y,a.point.z}})});
        }
        Structures(p,surface,context,props,links);
        foreach(var prop in p["props"])
        {
            string source=(string)prop["sourceModel"];float scale=(float)prop["height"];bool plant=source.StartsWith("tree_")||source.Contains("orchard")||source.Contains("poplar");
            var mesh=Styled(source,scale,!plant,seed,(float?)prop["weatheringIntensity"]??.52f);Put((string)prop["id"],source,mesh,Vector(prop["target"]),scale,(float)prop["yaw"],context,plant?trees:props,plant?scale:-1,supportGap:(float?)prop["supportGap"]??.035f,settlement:(float?)prop["settlement"]??.018f);
        }
        Power(p,roads,context,props,links);
        ShadeTree(p,level,surface,context,trees);
        foreach(var pair in roads)foreach(var corridor in pair.Value.corridor)context.Reserve(pair.Key,corridor);
        // The board's obstacles are emitted by gameplay; reserve their full
        // upright volume here so the surrounding plants cannot penetrate them.
        foreach(var obj in level["objects"]??new JArray())
        {float x=(float)obj["x"]+.5f-w,z=(float)obj["z"]+.5f-h;context.Reserve(id+"/obstacle/"+obj["x"]+"/"+obj["z"],new OrientedBox(new Box(new V3(x,1.3f,z),new V3(.55f,1.3f,.55f)),Pose.At(V3.Zero)));}
        var rockModel=models["stone_largeA"];
        var groups=p["rockGroups"].Select(g=>new RockGroup{id=(string)g["id"],center=Vector(g["center"]),axis=Vector(g["axis"]),radius=(float)g["radius"],count=(int)g["count"],minimumScale=.12f,maximumScale=.35f,buriedFraction=.2f}).ToArray();
        var rocks=RockGenerator.Generate(new RockSpec{id=id+"/outcrop",seed=seed,rocks=new[]{Shape("stone_largeA",rockModel)},groups=groups},context);
        Require(rocks.Ok,rocks.diagnostics,id);foreach(var rock in rocks.instances)props.Append(rock.id,rock.asset,rockModel,rock.pose);
        if(layoutOnly)
        {
            layoutReceipts.Add(new{id,routes=roads.Count,structuralPieces=props.objects.Count,shadeTrees=trees.objects.Count,reservedVolumes=context.ReservationCount,links});
            Console.WriteLine("PASS authored layout "+id+": terrain/routes/contacts/structures/network/rocks; ecology and native visuals pending.");
            return;
        }
        context.rules.Add(new ShadeRule(p,level));
        Plants(p,surface,context,trees,green,diagnostics);
        string folder=Path.Combine(staging,id);Directory.CreateDirectory(folder);
        var parts=new[]{WriteMesh(folder,"terrain",land),WriteMesh(folder,"structures",props),WriteMesh(folder,"roads",paths),WriteMesh(folder,"trees",trees),WriteMesh(folder,"groundcover",green)};
        var manifest=new{schemaVersion=1,id,sourceRevision=revision,units="board-cell",coordinates="Y-up XZ",seed,parts,links,diagnostics,toolVersions=configuration["toolVersions"],camera=p["camera"],entryConnection=p["entryConnection"],sourceHash=(string)p["sourceLevelHash"],nativeAccepted=false};
        string text=JsonConvert.SerializeObject(manifest,Formatting.Indented)+"\n";File.WriteAllText(Path.Combine(folder,"manifest.json"),text);
        Console.WriteLine(id+" baked: "+trees.objects.Count+" trees, "+green.objects.Count+" ground plants, "+props.objects.Count+" structural pieces; all required placements admitted.");
    }
    static int Main(string[] args)
    {
        if(args.Length<1||args.Length>2)throw new ArgumentException("Usage FirstTenEnvironmentBake <repository-root> [--validate-layouts]");layoutOnly=args.Length==2&&args[1]=="--validate-layouts";root=Path.GetFullPath(args[0]);report=Path.Combine(root,"Design/Campaign100/2026-10-11/FirstTenRefinement");content=Path.Combine(root,"QuietCamp/Assets/QuietCamp/Resources/QuietCamp");
        configuration=JObject.Parse(File.ReadAllText(Path.Combine(report,"Authoring/places.json")));LoadModels();
        var upstream=JObject.Parse(File.ReadAllText(Path.Combine(report,"package-upstreams.json")));
        configuration["toolVersions"]=upstream["packages"];
        var sources=new SortedDictionary<string,string>();
        foreach(string file in new[]{"tools/FirstTenEnvironmentBake.cs","Design/Campaign100/2026-10-11/FirstTenRefinement/Authoring/places.json","Design/Campaign100/2026-10-11/FirstTenRefinement/Baseline/published-first-ten.json"})sources[file]=Sha(File.ReadAllBytes(Path.Combine(root,file)));
        foreach(var file in new[]{"roadmap_models","roadmap_story_models","campaign100_models","atmosphere"})sources["QuietCamp/Assets/QuietCamp/Resources/QuietCamp/"+file+".json"]=Sha(File.ReadAllBytes(Path.Combine(content,file+".json")));
        foreach(var package in upstream["packages"])foreach(var file in Directory.GetFiles(Path.Combine(root,"QuietCamp/Packages",(string)package["package"],"Runtime/Core"),"*.cs"))sources[file.Substring(root.Length+1)]=Sha(File.ReadAllBytes(file));
        string revision=Sha(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(sources)));string staging=Path.Combine(report,"Baked/.pending-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(staging);
        try
        {
            var levels=JArray.Parse(File.ReadAllText(Path.Combine(report,"Baseline/published-first-ten.json")));
            foreach(var p in configuration["places"].OfType<JObject>())Bake(p,(JObject)levels.Single(l=>(string)l["id"]==(string)p["id"]),staging,revision);
            if(layoutOnly)
            {
                File.WriteAllText(Path.Combine(report,"layout-contracts.json"),JsonConvert.SerializeObject(new{checkedUtc=DateTime.UtcNow.ToString("o"),sourceRevision=revision,sources,layouts=layoutReceipts,unityLaunched=false,ecologyVerified=false,nativeAccepted=false,playerBuild=false},Formatting.Indented)+"\n");
                Directory.Delete(staging,true);return 0;
            }
            File.WriteAllText(Path.Combine(staging,"receipt.json"),JsonConvert.SerializeObject(new{sourceRevision=revision,sources,packages=upstream["packages"],style=styleReceipts,count=10,allRequiredPlacements=true,unityLaunched=false,nativeAccepted=false,playerBuild=false},Formatting.Indented)+"\n");
            string destination=Path.Combine(report,"Baked",revision);if(Directory.Exists(destination))Directory.Delete(staging,true);else Directory.Move(staging,destination);
            File.WriteAllText(Path.Combine(report,"Baked/current.json"),JsonConvert.SerializeObject(new{revision,path=revision},Formatting.Indented)+"\n");
            return 0;
        }
        catch{if(Directory.Exists(staging))Directory.Delete(staging,true);throw;}
    }
}
