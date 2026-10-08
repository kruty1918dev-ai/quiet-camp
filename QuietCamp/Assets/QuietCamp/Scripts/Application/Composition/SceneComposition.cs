using System;
using System.Collections.Generic;
using System.Linq;

namespace QuietCamp.Composition
{
    // Portable, presentation-only authoring. No Unity, progression or Quiet Camp types.
    [Serializable] public sealed class Point { public float x,z; public Point(){} public Point(float x,float z){this.x=x;this.z=z;} }
    [Serializable] public sealed class AssetSocket {public float x,y,z;}
    [Serializable] public sealed class VisualAssetDefinition
    {
        public string id,source,license,sourceHash,lod,atlas,supportKind;public string placementClass="solid";
        public float sourceHeight=1;public AssetSocket pivot=new AssetSocket();
        public AssetSocket[] conductors=Array.Empty<AssetSocket>();
        public float radius=.5f,height=1,width=1,depth=1;
        public bool wind;
        public string[] seasons=Array.Empty<string>();
        public Dictionary<string,Point> sockets=new Dictionary<string,Point>();
    }
    [Serializable] public sealed class EnsembleRole
    {public string id,asset,parent;public float x,z,height=1,yaw;public bool required=true;}
    [Serializable] public sealed class EnsembleTemplate
    {public string id;public float width=10,depth=10;public bool entrance,ruined;public EnsembleRole[] roles=Array.Empty<EnsembleRole>();}
    [Serializable] public sealed class PlacementIntent {public string zone,nearNode;public float x,z,yaw;public bool fixedPosition;}
    [Serializable] public sealed class EnsembleIntent
    {public float scale=1;public string id,template,entranceConnectsTo,state="reclaimed",fenceVariant="wattle";public PlacementIntent placement=new PlacementIntent();}
    [Serializable] public sealed class LandscapeZone
    {public string id,kind="forest",species="tree_default";public float x,z,width=20,depth=20,density=.8f;}
    [Serializable] public sealed class InfrastructureRouteDefinition
    {
        public string id,kind="road",supportAsset,nextRoute;public string interpolation="linear";
        public Point[] points=Array.Empty<Point>();public string[] serviceTargets=Array.Empty<string>();
        public float width=1.1f,minSpan=20,maxSpan=50,supportHeight=5.8f;
    }
    [Serializable] public sealed class SceneAnchor {public string id;public float x,z,radius=5.5f;}
    [Serializable] public sealed class LandmarkIntent
    {public string id,asset,revealOwner,compoundOwner,interpretation="fictional";public float x,z,height=6,yaw;}
    [Serializable] public sealed class CompositionBudgets
    {public int instances=16000,materials=6,renderers=32;public long cacheBytes=67108864;}
    [Serializable] public sealed class SceneCompositionDocument
    {
        public int schemaVersion=1,seed=1918;public string id,season="summer";
        public SceneAnchor[] nodes=Array.Empty<SceneAnchor>();
        public LandscapeZone[] zones=Array.Empty<LandscapeZone>();
        public InfrastructureRouteDefinition[] routes=Array.Empty<InfrastructureRouteDefinition>();
        public EnsembleIntent[] ensembles=Array.Empty<EnsembleIntent>();
        public LandmarkIntent[] landmarks=Array.Empty<LandmarkIntent>();
        public CompositionBudgets budgets=new CompositionBudgets();
    }
    [Serializable] public sealed class CompositionDiagnostic
    {public string code,severity="error",entityId,sourcePointer,message,sourceKind="document";public string[] relatedIds=Array.Empty<string>();public float x,z;}
    [Serializable] public sealed class ComposedInstance
    {public string id,owner,role,asset,revealOwner,state,template;public float x,z,height,yaw;public bool wind;}
    [Serializable] public sealed class RouteSpan
    {public string id,route,a,b;public float ax,az,bx,bz,height;}
    [Serializable] public sealed class BakedBounds {public AssetSocket min=new AssetSocket(),max=new AssetSocket();}
    [Serializable] public sealed class BakedChunkManifest
    {
        public string id,sourceHash,resourceId;public BakedBounds bounds=new BakedBounds();
        public string[] dependencies=Array.Empty<string>(),lod=Array.Empty<string>(),revealBindings=Array.Empty<string>();public long estimatedBytes;
    }
    [Serializable] public sealed class PlacementAttempt
    {public string entityId,reason;public int index;public float x,z;public bool accepted;}
    [Serializable] public sealed class CompositionResult
    {
        public List<ComposedInstance> instances=new List<ComposedInstance>();
        public List<RouteSpan> spans=new List<RouteSpan>();
        public List<PlacementAttempt> candidates=new List<PlacementAttempt>();
        public List<CompositionDiagnostic> diagnostics=new List<CompositionDiagnostic>();
        public bool Valid=>!diagnostics.Exists(d=>d.severity=="error");
    }
    public interface ITerrainSample {bool Supported(float x,float z,float radius);float Height(float x,float z);}
    public sealed class FlatTerrain : ITerrainSample {public bool Supported(float x,float z,float radius)=>true;public float Height(float x,float z)=>0;}
    public static class SceneComposer
    {
        public const string Revision="semantic-composer-0.2.0";
        static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        public static float Unit(string id,int seed,int salt)
        {unchecked {uint h=(uint)seed^((uint)salt*0x85ebca6bu);foreach(char c in id??"")h=(h^c)*16777619;h^=h>>16;h*=0x7feb352du;h^=h>>15;h*=0x846ca68bu;h^=h>>16;return(h&0xffffff)/16777216f;}}
        static float Sq(float a,float b)=>a*a+b*b;
        public static float DistanceTo(InfrastructureRouteDefinition r,float x,float z,out Point nearest)
        {
            nearest=null;float best=float.MaxValue;
            for(int i=1;i<r.points.Length;i++)
            {
                var start=r.points[i-1];var end=r.points[i];int steps=r.interpolation=="smooth-x"?24:1;
                Point At(float t)=>new Point(start.x+(end.x-start.x)*(steps==1?t:t*t*(3-2*t)),start.z+(end.z-start.z)*t);
                for(int j=0;j<steps;j++)
                {var a=At(j/(float)steps);var b=At((j+1)/(float)steps);float dx=b.x-a.x,dz=b.z-a.z,t=Math.Clamp(((x-a.x)*dx+(z-a.z)*dz)/Math.Max(.0001f,Sq(dx,dz)),0,1);var p=new Point(a.x+dx*t,a.z+dz*t);float q=Sq(x-p.x,z-p.z);if(q<best){best=q;nearest=p;}}
            }
            return(float)Math.Sqrt(best);
        }
        sealed class WorldContext
        {
            public SceneAnchor[] nodes;public InfrastructureRouteDefinition[] routes;
            public readonly List<(float x,float z,float w,float d)> parcels=new List<(float,float,float,float)>();
        }
        static bool RoleOverlap(EnsembleRole a,VisualAssetDefinition da,EnsembleRole b,VisualAssetDefinition db)
        {
            if(da.placementClass=="groundcover"||db.placementClass=="groundcover"||da.placementClass=="canopy"&&db.placementClass=="canopy"||da.placementClass=="boundary"&&db.placementClass=="boundary")return false;
            float Scale(EnsembleRole r,VisualAssetDefinition d)=>(r.height>0?r.height:d.height)/d.height;
            float aa=a.yaw*(float)Math.PI/180,bb=b.yaw*(float)Math.PI/180,ac=(float)Math.Cos(aa),asine=(float)Math.Sin(aa),bc=(float)Math.Cos(bb),bs=(float)Math.Sin(bb);
            var axes=new[]{(ac,-asine),(asine,ac),(bc,-bs),(bs,bc)};
            foreach(var axis in axes)
            {
                float ra=(Math.Abs(ac*axis.Item1-asine*axis.Item2)*da.width+Math.Abs(asine*axis.Item1+ac*axis.Item2)*da.depth)*Scale(a,da)*.5f;
                float rb=(Math.Abs(bc*axis.Item1-bs*axis.Item2)*db.width+Math.Abs(bs*axis.Item1+bc*axis.Item2)*db.depth)*Scale(b,db)*.5f;
                if(Math.Abs((a.x-b.x)*axis.Item1+(a.z-b.z)*axis.Item2)>=ra+rb-.025f)return false;
            }return true;
        }
        public static CompositionResult[] ComposeWorld(IReadOnlyList<SceneCompositionDocument> documents,IReadOnlyDictionary<string,VisualAssetDefinition> assets,IReadOnlyDictionary<string,EnsembleTemplate> templates,ITerrainSample terrain=null)
        {
            var output=new CompositionResult[documents.Count];
            for(int i=0;i<documents.Count;i++)
            {output[i]=Compose(documents[i],assets,templates,terrain);if(!output[i].Valid)return output.Select(r=>r??new CompositionResult()).ToArray();}
            var identities=new HashSet<string>(StringComparer.Ordinal);
            for(int i=0;i<documents.Count;i++)
            {
                var d=documents[i];foreach(string id in new[]{d.id}.Concat(d.nodes.Select(n=>n.id)).Concat(d.zones.Select(z=>z.id)).Concat(d.routes.Select(r=>r.id)).Concat(d.ensembles.Select(e=>e.id)).Concat(d.landmarks.Select(l=>l.id)))
                    if(!identities.Add(id))output[i].diagnostics.Add(new CompositionDiagnostic{code="duplicate-world-id",entityId=id,sourcePointer="/",message="Identity appears in another composition document."});
            }
            var allRoutes=documents.SelectMany(d=>d.routes).ToArray();
            for(int i=0;i<documents.Count;i++)foreach(var route in documents[i].routes)if(route.nextRoute!=null)
            {
                var next=Array.Find(allRoutes,r=>r.id==route.nextRoute);var end=route.points[route.points.Length-1];
                string code=next==null?"missing-route-continuation":next.kind!=route.kind?"incompatible-route-network":Sq(end.x-next.points[0].x,end.z-next.points[0].z)>.0001f?"disconnected-route-continuation":null;
                if(code!=null)output[i].diagnostics.Add(new CompositionDiagnostic{code=code,entityId=route.id,sourcePointer="/routes/"+Array.IndexOf(documents[i].routes,route)+"/nextRoute",relatedIds=new[]{route.nextRoute},message="Declared infrastructure continuation must use a compatible route and identical shared endpoint."});
            }
            if(output.Any(r=>!r.Valid))return output;
            var context=new WorldContext{nodes=documents.SelectMany(d=>d.nodes).ToArray(),routes=documents.SelectMany(d=>d.routes).ToArray()};
            foreach(var l in documents.SelectMany(d=>d.landmarks))
            {float r=assets[l.asset].radius*l.height/assets[l.asset].height;context.parcels.Add((l.x,l.z,r*2,r*2));}
            for(int i=0;i<documents.Count;i++)output[i]=Compose(documents[i],assets,templates,terrain,context);
            return output;
        }
        public static CompositionResult Compose(SceneCompositionDocument doc,IReadOnlyDictionary<string,VisualAssetDefinition> assets,IReadOnlyDictionary<string,EnsembleTemplate> templates,ITerrainSample terrain=null)=>Compose(doc,assets,templates,terrain,null);
        static CompositionResult Compose(SceneCompositionDocument doc,IReadOnlyDictionary<string,VisualAssetDefinition> assets,IReadOnlyDictionary<string,EnsembleTemplate> templates,ITerrainSample terrain,WorldContext context)
        {
            var result=new CompositionResult();terrain=terrain??new FlatTerrain();
            if(doc==null||assets==null||templates==null||doc.nodes==null||doc.zones==null||doc.routes==null||doc.ensembles==null||doc.landmarks==null||doc.budgets==null)
            {result.diagnostics.Add(new CompositionDiagnostic{code="schema",entityId=doc?.id,message="Document and collections must be present, not null",sourcePointer="/"});return result;}var ids=new HashSet<string>(StringComparer.Ordinal);
            string EntityPointer(string id)
            {
                string Pointer<T>(T[] entries,Func<T,string> key,string root) where T:class
                {int index=Array.FindIndex(entries,e=>e!=null&&key(e)==id);return index<0?null:root+"/"+index;}
                return Pointer(doc.ensembles,e=>e.id,"/ensembles")??Pointer(doc.routes,e=>e.id,"/routes")??Pointer(doc.zones,e=>e.id,"/zones")??Pointer(doc.nodes,e=>e.id,"/nodes")??Pointer(doc.landmarks,e=>e.id,"/landmarks")??"/";
            }
            void Error(string code,string id,string message,string pointer="")
            {
                string kind="document";if(pointer=="")pointer=EntityPointer(id);
                if(pointer=="/"&&id!=doc.id){int template=templates.Values.ToList().FindIndex(t=>t!=null&&t.id==id),asset=assets.Values.ToList().FindIndex(a=>a!=null&&a.id==id);if(template>=0){kind="templates";pointer="/"+template;}else if(asset>=0){kind="assets";pointer="/"+asset;}}
                result.diagnostics.Add(new CompositionDiagnostic{code=code,entityId=id,message=message,sourcePointer=pointer,sourceKind=kind});
            }
            // Native authoring uses this same core without the CLI's JSON Schema validator.
            if(doc.nodes.Any(n=>n==null)||doc.zones.Any(z=>z==null)||doc.ensembles.Any(e=>e==null)||doc.landmarks.Any(l=>l==null)||doc.routes.Any(r=>r==null||r.points==null||r.serviceTargets==null)||assets.Values.Any(a=>a==null||a.seasons==null)||templates.Values.Any(t=>t==null||t.roles==null||t.roles.Any(r=>r==null||string.IsNullOrWhiteSpace(r.id))))
            {Error("schema",doc.id,"Null collection member or missing role identity.");return result;}
            void Id(string id){if(string.IsNullOrWhiteSpace(id)||!ids.Add(id))Error("duplicate-id",id,"IDs must be present and unique.");}
            Id(doc.id);
            if(!new[]{"spring","summer","autumn","winter"}.Contains(doc.season))Error("invalid-season",doc.id,"Unknown season.","/season");
            if(doc.budgets.instances<=0||doc.budgets.materials<=0||doc.budgets.materials>6||doc.budgets.renderers<=0||doc.budgets.renderers>32||doc.budgets.cacheBytes<=0||doc.budgets.cacheBytes>67108864)Error("invalid-budget",doc.id,"Budget exceeds presentation caps.","/budgets");
            if(doc.schemaVersion!=1){Error("schema",doc.id,"Unsupported composition schema.");return result;}
            foreach(var node in doc.nodes){Id(node.id);if(!Finite(node.x)||!Finite(node.z)||!Finite(node.radius)||node.radius<0)Error("invalid-anchor",node.id,"Non-finite anchor or radius.");}
            foreach(var zone in doc.zones){Id(zone.id);if(!Finite(zone.x)||!Finite(zone.z)||!Finite(zone.width)||!Finite(zone.depth)||!Finite(zone.density)||zone.width<=0||zone.depth<=0||zone.density<0||zone.density>1)Error("invalid-zone",zone.id,"Invalid zone extent/density.");}
            foreach(var route in doc.routes)
            {
                Id(route.id);if(route.points.Length<2||route.points.Any(p=>p==null||!Finite(p.x)||!Finite(p.z))||!Finite(route.width)||route.width<=0||!Finite(route.minSpan)||!Finite(route.maxSpan)||!Finite(route.supportHeight)||route.supportHeight<=0||route.minSpan<=0||route.maxSpan<route.minSpan)Error("invalid-route",route.id,"Route requires finite endpoints and valid spacing.");
                if(route.kind!="road"&&route.kind!="path"&&route.kind!="power"&&route.kind!="distribution")Error("invalid-route-kind",route.id,"Unknown route kind.");
                if(route.interpolation!="linear"&&route.interpolation!="smooth-x"||route.interpolation!="linear"&&(route.kind=="power"||route.kind=="distribution"))Error("invalid-interpolation",route.id,"Power spans must remain linear.");
                if(route.kind=="distribution"&&route.serviceTargets.Length==0)Error("missing-service-target",route.id,"Distribution line must serve an ensemble.");
                if(route.kind=="power"||route.kind=="distribution")
                {
                    if(!assets.TryGetValue(route.supportAsset??"",out var support))Error("missing-support",route.id,"Support asset is missing.");
                    else{if(support.supportKind!=route.kind)Error("incompatible-support",route.id,"Support asset belongs to a different infrastructure network.");if(support.conductors==null||support.conductors.Length==0)Error("missing-conductor-sockets",route.id,"Power support requires declared wire attachment sockets.");}
                }
            }
            foreach(var asset in assets.Values)
                if(!new[]{"solid","canopy","groundcover","boundary"}.Contains(asset.placementClass)||!Finite(asset.radius)||!Finite(asset.height)||!Finite(asset.width)||!Finite(asset.depth)||asset.radius<0||asset.height<=0||asset.width<=0||asset.depth<=0||!Finite(asset.sourceHeight)||asset.sourceHeight<=0||asset.pivot==null||!Finite(asset.pivot.x)||!Finite(asset.pivot.y)||!Finite(asset.pivot.z)||asset.conductors==null||asset.conductors.Any(p=>p==null||!Finite(p.x)||!Finite(p.y)||!Finite(p.z)))Error("invalid-asset",asset.id,"Asset dimensions must be finite and positive.");
            foreach(var template in templates.Values)
            {
                if(template.entrance&&!template.roles.Any(r=>r.id=="gate"||r.id=="entrance"))Error("missing-gate",template.id,"Entrance template requires an entrance role.");
                if(!Finite(template.width)||!Finite(template.depth)||template.width<=0||template.depth<=0)Error("invalid-template",template.id,"Parcel dimensions must be finite and positive.");
                bool Boundary(EnsembleRole role)=>role.asset=="$fence"||assets.TryGetValue(role.asset??"",out var asset)&&asset.placementClass=="boundary";
                var roleIds=new HashSet<string>();foreach(var role in template.roles)
                {
                    if(!roleIds.Add(role.id??""))Error("duplicate-role",template.id,"Duplicate role: "+role.id);
                    if(!Finite(role.x)||!Finite(role.z)||!Finite(role.yaw)||!Finite(role.height)||role.height<0)Error("invalid-role",template.id,"Non-finite role transform.");
                    if(Boundary(role)&&role.parent==null&&(!template.ruined||!template.roles.Any(r=>r.id=="foundation")))Error("fence-without-owner",template.id,"Fence requires an owner role or an explicit ruined foundation context.");
                    var parent=role.parent==null?null:Array.Find(template.roles,p=>p.id==role.parent);
                    if(role.parent!=null&&parent==null)Error("orphan-role",template.id,"Parent is missing: "+role.parent);
                    if(role.required&&parent!=null&&!parent.required)Error("optional-parent",template.id,"Required role depends on optional role.");
                    var visited=new HashSet<string>();var cursor=role;
                    while(cursor!=null){if(!visited.Add(cursor.id)){Error("dependency-cycle",template.id,"Role dependency cycle.");break;}cursor=cursor.parent==null?null:Array.Find(template.roles,p=>p.id==cursor.parent);}
                }
                var gate=Array.Find(template.roles,r=>r.id=="gate");
                if(gate!=null&&(!template.roles.Any(r=>r!=gate&&Boundary(r))||Math.Min(Math.Abs(Math.Abs(gate.z)-template.depth*.5f),Math.Abs(Math.Abs(gate.x)-template.width*.5f))>1))Error("gate-without-boundary",template.id,"Gate requires a fence boundary and a boundary position.");
            }
            foreach(var e in doc.ensembles){Id(e.id);if(!templates.ContainsKey(e.template??""))Error("missing-template",e.id,"Unknown ensemble template.");}
            foreach(var l in doc.landmarks){Id(l.id);if(!assets.ContainsKey(l.asset??""))Error("missing-asset",l.id,"Landmark asset is missing.");if(!doc.nodes.Any(n=>n.id==l.revealOwner))Error("missing-reveal-owner",l.id,"Landmark must bind to progression.");}
            foreach(var r in doc.routes)foreach(var target in r.serviceTargets)if(!doc.ensembles.Any(e=>e.id==target))Error("missing-service-target",r.id,"Unknown ensemble: "+target);
            if(!result.Valid)return result;
            var reserved=context?.parcels??new List<(float x,float z,float w,float d)>();
            foreach(var landmark in doc.landmarks)
            {
                float radius=assets[landmark.asset].radius*landmark.height/assets[landmark.asset].height;
                if(!Finite(landmark.x)||!Finite(landmark.z)||!Finite(landmark.height)||!Finite(landmark.yaw)||landmark.height<=0||!terrain.Supported(landmark.x,landmark.z,radius))Error("unsupported-landmark",landmark.id,"Landmark lacks valid supported footprint.");
                else reserved.Add((landmark.x,landmark.z,radius*2,radius*2));
            }
            if(!result.Valid)return result;
            string Rejection(float x,float z,float radius,bool protectPower=false)
            {
                if(!terrain.Supported(x,z,radius))return "unsupported-surface";float h=terrain.Height(x,z);if(!Finite(h))return "invalid-surface-height";for(int i=0;i<4;i++){float a=i*(float)Math.PI*.5f;if(Math.Abs(terrain.Height(x+(float)Math.Cos(a)*radius,z+(float)Math.Sin(a)*radius)-h)>.75f)return "parcel-slope";}
                foreach(var n in context?.nodes??doc.nodes)if(Sq(x-n.x,z-n.z)<Sq(n.radius+radius,0))return "clearing:"+n.id;
                foreach(var r in context?.routes??doc.routes)if((r.kind=="road"||r.kind=="path"||protectPower&&(r.kind=="power"||r.kind=="distribution"))&&DistanceTo(r,x,z,out _)<=radius+r.width)return "route:"+r.id;
                foreach(var box in reserved)if(Math.Abs(x-box.x)<box.w*.5f+radius&&Math.Abs(z-box.z)<box.d*.5f+radius)return "occupied-parcel";
                return null;
            }
            foreach(var e in doc.ensembles.OrderBy(e=>e.id,StringComparer.Ordinal))
            {
                if(e.placement==null||!Finite(e.scale)||e.scale<=0||!Finite(e.placement.x)||!Finite(e.placement.z)||!Finite(e.placement.yaw)){Error("invalid-placement",e.id,"Placement must be finite.");continue;}
                var template=templates[e.template];var zone=doc.zones.FirstOrDefault(z=>z.id==e.placement.zone);var near=doc.nodes.FirstOrDefault(n=>n.id==e.placement.nearNode);
                if(e.placement.nearNode!=null&&near==null){Error("missing-node",e.id,"Unknown placement anchor.");continue;}
                if(zone==null){Error("missing-zone",e.id,"Placement zone is missing.",EntityPointer(e.id)+"/placement/zone");continue;}
                var road=doc.routes.FirstOrDefault(r=>r.id==e.entranceConnectsTo&&(r.kind=="road"||r.kind=="path"));
                if(template.entrance&&road==null){Error("disconnected-entrance",e.id,"An entrance requires an explicit road connection.");continue;}
                var roles=new List<EnsembleRole>();bool bad=false;
                foreach(var role in template.roles)
                {
                    string asset=role.asset=="$fence"?"ua_"+e.fenceVariant+"_fence":role.asset;
                    if(!assets.ContainsKey(asset??"")){if(role.required){Error("missing-role-asset",e.id,"Required role "+role.id+" has no asset.");bad=true;}continue;}
                    if(role.parent!=null&&!template.roles.Any(p=>p.id==role.parent)){Error("orphan-role",e.id,"Role "+role.id+" has no parent "+role.parent);bad=true;}
                    if(assets[asset].seasons.Length>0&&!assets[asset].seasons.Contains(doc.season)){if(role.required){Error("invalid-seasonal-variant",e.id,"Required asset "+asset+" does not support "+doc.season);bad=true;}continue;}
                    roles.Add(role);
                }
                if(bad)continue;
                while(roles.RemoveAll(role=>role.parent!=null&&!roles.Any(p=>p.id==role.parent))>0){}
                for(int i=0;i<roles.Count;i++)for(int j=i+1;j<roles.Count;j++)
                {
                    string Resolve(EnsembleRole r)=>r.asset=="$fence"?"ua_"+e.fenceVariant+"_fence":r.asset;
                    if(RoleOverlap(roles[i],assets[Resolve(roles[i])],roles[j],assets[Resolve(roles[j])]))
                    {Error("role-overlap",e.id,"Template footprints overlap: "+roles[i].id+" and "+roles[j].id);result.diagnostics.Last().relatedIds=new[]{e.id+"/"+roles[i].id,e.id+"/"+roles[j].id};bad=true;}
                }
                if(bad)continue;
                float parcelWidth=template.width*e.scale,parcelDepth=template.depth*e.scale;
                var entry=roles.FirstOrDefault(r=>r.id=="gate"||r.id=="entrance");
                if(template.entrance&&entry==null){Error("missing-gate",e.id,"Entrance role was not admitted.");continue;}
                bool found=false,unsupportedApproach=false;float px=0,pz=0,yaw=e.placement.yaw;
                for(int attempt=0;attempt<(e.placement.fixedPosition?1:64);attempt++)
                {
                    px=e.placement.fixedPosition?e.placement.x:zone.x+(Unit(e.id,doc.seed,attempt*2)-.5f)*Math.Max(0,zone.width-template.width*e.scale);
                    pz=e.placement.fixedPosition?e.placement.z:zone.z+(Unit(e.id,doc.seed,attempt*2+1)-.5f)*Math.Max(0,zone.depth-template.depth*e.scale);
                    if(near!=null&&Math.Abs(pz-near.z)>27){result.candidates.Add(new PlacementAttempt{entityId=e.id,index=attempt,x=px,z=pz,reason="anchor-distance"});continue;}
                    // Use conservative parcel clearance; no independent child admission.
                    float radius=(float)Math.Sqrt(Sq(parcelWidth*.5f,parcelDepth*.5f));
                    foreach(var role in roles)
                    {var asset=assets[role.asset=="$fence"?"ua_"+e.fenceVariant+"_fence":role.asset];radius=Math.Max(radius,((float)Math.Sqrt(Sq(role.x,role.z))+asset.radius*(role.height>0?role.height:asset.height)/asset.height)*e.scale);}
                    string reason=px-radius<zone.x-zone.width*.5f||px+radius>zone.x+zone.width*.5f||pz-radius<zone.z-zone.depth*.5f||pz+radius>zone.z+zone.depth*.5f?"zone-boundary":Rejection(px,pz,radius,true);
                    if(reason==null&&template.entrance)
                    {
                        DistanceTo(road,px,pz,out var facing);yaw=(float)((Math.Atan2(facing.x-px,facing.z-pz)-Math.Atan2(entry.x,entry.z))*180/Math.PI);
                        float angle=yaw*(float)Math.PI/180,ss=(float)Math.Sin(angle),cc=(float)Math.Cos(angle);
                        float gx=px+(entry.x*cc+entry.z*ss)*e.scale,gz=pz+(-entry.x*ss+entry.z*cc)*e.scale;
                        DistanceTo(road,gx,gz,out var end);
                        for(int sample=0;sample<=32;sample++)
                        {
                            float t=sample/32f,sx=gx+(end.x-gx)*t,sz=gz+(end.z-gz)*t;
                            if(!terrain.Supported(sx,sz,.35f)){reason="unsupported-approach";unsupportedApproach=true;break;}
                            if(reserved.Any(box=>Math.Abs(sx-box.x)<box.w*.5f+.35f&&Math.Abs(sz-box.z)<box.d*.5f+.35f)){reason="approach-crosses-parcel";break;}
                        }
                    }
                    result.candidates.Add(new PlacementAttempt{entityId=e.id,index=attempt,x=px,z=pz,reason=reason,accepted=reason==null});if(reason!=null)continue;
                    found=true;break;
                }
                if(!found){Error(unsupportedApproach?"unsupported-approach":"ensemble-does-not-fit",e.id,unsupportedApproach?"All supported parcel candidates lack a supported entrance approach. Author a crossing or move the zone.":"Entire ensemble rejected; no children emitted. Enlarge/move zone or move anchor.",EntityPointer(e.id));continue;}
                float a=yaw*(float)Math.PI/180,s=(float)Math.Sin(a),c=(float)Math.Cos(a);
                foreach(var role in roles)
                {
                    string asset=role.asset=="$fence"?"ua_"+e.fenceVariant+"_fence":role.asset;
                    var definition=assets[asset];
                    result.instances.Add(new ComposedInstance{id=e.id+"/"+role.id,owner=e.id,role=role.id,asset=asset,state=e.state,template=e.template,x=px+(role.x*c+role.z*s)*e.scale,z=pz+(-role.x*s+role.z*c)*e.scale,height=(role.height>0?role.height:definition.height)*e.scale,yaw=yaw+role.yaw,wind=definition.wind,revealOwner=near?.id});
                }
                reserved.Add((px,pz,parcelWidth,parcelDepth));
                if(template.entrance)
                {
                    var gate=roles.FirstOrDefault(r=>r.id=="gate"||r.id=="entrance");
                    if(gate==null){Error("missing-gate",e.id,"Entrance template has no gate/entrance role.");continue;}
                    float gx=px+(gate.x*c+gate.z*s)*e.scale,gz=pz+(-gate.x*s+gate.z*c)*e.scale;DistanceTo(road,gx,gz,out var end);
                    result.spans.Add(new RouteSpan{id=e.id+"/approach",route=road.id,a=e.id+"/"+gate.id,b=road.id,ax=gx,az=gz,bx=end.x,bz=end.z,height=0});
                }
            }
            foreach(var route in doc.routes.Where(r=>r.kind=="power"||r.kind=="distribution"))
            {
                int support=0;ComposedInstance previous=null;
                for(int edge=1;edge<route.points.Length;edge++)
                {
                    var a=route.points[edge-1];var b=route.points[edge];float length=(float)Math.Sqrt(Sq(b.x-a.x,b.z-a.z));
                    if(length<route.minSpan){Error("invalid-span",route.id,"Control points are closer than minSpan.");continue;}
                    int count=Math.Max(1,(int)Math.Ceiling(length/route.maxSpan));float step=length/count;
                    if(step<route.minSpan){Error("invalid-span",route.id,"Cannot satisfy span limits.");continue;}
                    for(int n=edge==1?0:1;n<=count;n++)
                    {
                        float t=n/(float)count,x=a.x+(b.x-a.x)*t,z=a.z+(b.z-a.z)*t;
                        string rejection=Rejection(x,z,assets[route.supportAsset].radius);
                        if(rejection!=null){Error("unsupported-pylon",route.id,"Support rejected: "+rejection);continue;}
                        var p=new ComposedInstance{id=route.id+"/support-"+support++,owner=route.id,role="support",asset=route.supportAsset,x=x,z=z,height=route.supportHeight,yaw=(float)(Math.Atan2(b.x-a.x,b.z-a.z)*180/Math.PI)};
                        result.instances.Add(p);
                        if(previous!=null)result.spans.Add(new RouteSpan{id=route.id+"/span-"+(support-1),route=route.id,a=previous.id,b=p.id,ax=previous.x,az=previous.z,bx=p.x,bz=p.z,height=route.supportHeight});
                        previous=p;
                    }
                }
            }
            foreach(var landmark in doc.landmarks)result.instances.Add(new ComposedInstance{id=landmark.id,owner=landmark.compoundOwner??landmark.id,role="landmark",asset=landmark.asset,x=landmark.x,z=landmark.z,height=landmark.height,yaw=landmark.yaw,revealOwner=landmark.revealOwner});
            if(result.instances.Count>doc.budgets.instances)Error("instance-budget",doc.id,"Composition exceeds instance budget.");
            foreach(var diagnostic in result.diagnostics)
            {
                string Pointer<T>(T[] entries,Func<T,string> getId,string root)
                {int index=Array.FindIndex(entries,e=>getId(e)==diagnostic.entityId);return index<0?null:root+"/"+index;}
                string pointer=Pointer(doc.ensembles,e=>e.id,"/ensembles")??Pointer(doc.routes,e=>e.id,"/routes")??Pointer(doc.zones,e=>e.id,"/zones")??Pointer(doc.landmarks,e=>e.id,"/landmarks");
                if(pointer!=null&&(string.IsNullOrEmpty(diagnostic.sourcePointer)||diagnostic.sourcePointer=="/"))diagnostic.sourcePointer=pointer;
                diagnostic.relatedIds=diagnostic.relatedIds.Concat(doc.nodes.Select(n=>n.id).Concat(doc.routes.Select(r=>r.id)).Concat(doc.zones.Select(z=>z.id)).Where(id=>diagnostic.message.Contains(id)&&id!=diagnostic.entityId)).Distinct().ToArray();
            }
            return result;
        }
    }
}
