using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Kruty1918.ScenePlacement
{
    [Serializable] public struct SurfaceSample {public V3 point,normal;public string tag;public float moisture,light;}
    public interface ISurfaceQuery {bool TrySample(V3 target,V3 direction,out SurfaceSample sample);}
    public sealed class PlaneSurface : ISurfaceQuery
    {
        readonly V3 point,normal;readonly string tag;
        public PlaneSurface(float y=0,string tag="ground"):this(new V3(0,y,0),V3.Up,tag){}
        public PlaneSurface(V3 point,V3 normal,string tag="ground"){this.point=point;this.normal=normal.Normalized;this.tag=tag;}
        public bool TrySample(V3 target,V3 direction,out SurfaceSample sample)
        {float d=V3.Dot(direction,normal);sample=default;if(Math.Abs(d)<1e-6f)return false;sample=new SurfaceSample{point=target+direction*(V3.Dot(point-target,normal)/d),normal=normal,tag=tag,light=1};return true;}
    }
    public sealed class HeightFieldSurface : ISurfaceQuery
    {
        readonly float[] heights;readonly int width,depth;readonly float spacing;readonly V3 origin;
        public HeightFieldSurface(float[] heights,int width,int depth,float spacing,V3 origin)
        {if(width<2||depth<2||heights.Length!=width*depth||spacing<=0)throw new ArgumentException("Heightfield dimensions");this.heights=(float[])heights.Clone();this.width=width;this.depth=depth;this.spacing=spacing;this.origin=origin;foreach(float h in heights)if(!Numbers.Finite(h))throw new ArgumentException("Heightfield finite heights");}
        public bool TrySample(V3 target,V3 direction,out SurfaceSample sample)
        {
            sample=default;if(Math.Abs(direction.y)<.999f)return false;
            float fx=(target.x-origin.x)/spacing,fz=(target.z-origin.z)/spacing;
            if(fx<0||fz<0||fx>width-1||fz>depth-1)return false;
            int x=Math.Min(width-2,(int)Math.Floor(fx)),z=Math.Min(depth-2,(int)Math.Floor(fz));float u=fx-x,v=fz-z;
            float a=heights[z*width+x],b=heights[z*width+x+1],c=heights[(z+1)*width+x],d=heights[(z+1)*width+x+1];
            float y=(a+(b-a)*u)*(1-v)+(c+(d-c)*u)*v;
            sample=new SurfaceSample{point=new V3(target.x,origin.y+y,target.z),normal=new V3(-((b-a)*(1-v)+(d-c)*v)/spacing,1,-((c-a)*(1-u)+(d-b)*u)/spacing).Normalized,tag="ground",light=1};return true;
        }
    }
    [Serializable] public sealed class Diagnostic
    {
        public string code,id,message;public bool error=true;
        public Diagnostic(string code,string id,string message,bool error=true){this.code=code;this.id=id;this.message=message;this.error=error;}
        public override string ToString()=>code+":"+id+":"+message;
    }
    [Serializable] public sealed class AssetShape
    {
        public string id;public Box[] volumes=Array.Empty<Box>();public V3[] contacts=Array.Empty<V3>();public float motionMargin;
        public AssetShape Snapshot()=>new AssetShape{id=id,volumes=volumes?.ToArray(),contacts=contacts?.ToArray(),motionMargin=motionMargin};
        public static AssetShape FromMesh(string id,MeshData mesh)
        {var b=mesh.Bounds;return new AssetShape{id=id,volumes=new[]{b},contacts=new[]{new V3(b.center.x,b.center.y-b.half.y,b.center.z)}};}
    }
    [Serializable] public sealed class PlacementRequest
    {
        public string id,owner;public AssetShape shape;public V3 target;public float yaw,scale=1,settlement,maxSlope=40,maxSupportGap=.08f,searchRadius;
        public int candidates=64,seed;public bool alignToSurface,required=true;public string[] allowedContacts=Array.Empty<string>();
    }
    [Serializable] public sealed class PlacedInstance
    {
        public string id,owner,asset;public Pose pose;public OrientedBox[] volumes;public SurfaceSample surface;
    }
    public interface IPlacementRule {Diagnostic Check(PlacementRequest request,PlacedInstance candidate,PlacementContext context);}
    public sealed class PlacementResult
    {
        public readonly List<PlacedInstance> instances=new List<PlacedInstance>();public readonly List<Diagnostic> diagnostics=new List<Diagnostic>();
        public bool Ok=>!diagnostics.Any(d=>d.error);
    }
    public sealed class PlacementContext
    {
        sealed class Entry{public string id;public OrientedBox box;}
        readonly Dictionary<(int,int),List<Entry>> cells=new Dictionary<(int,int),List<Entry>>();
        readonly List<Entry> entries=new List<Entry>();readonly float cellSize;
        public readonly ISurfaceQuery surface;public readonly List<IPlacementRule> rules=new List<IPlacementRule>();
        public V3 Up{get;set;}=V3.Up;public int ReservationCount=>entries.Count;
        public PlacementContext(ISurfaceQuery surface,float cellSize=4)
        {this.surface=surface??throw new ArgumentNullException(nameof(surface));if(cellSize<=0||!Numbers.Finite(cellSize))throw new ArgumentException("Positive grid size");this.cellSize=cellSize;}
        IEnumerable<(int,int)> Keys(Box b)
        {
            int x0=(int)Math.Floor((b.center.x-b.half.x)/cellSize),x1=(int)Math.Floor((b.center.x+b.half.x)/cellSize),z0=(int)Math.Floor((b.center.z-b.half.z)/cellSize),z1=(int)Math.Floor((b.center.z+b.half.z)/cellSize);
            if((long)(x1-x0+1)*(z1-z0+1)>1000000)throw new ArgumentException("Reservation too large for grid");
            for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++)yield return(x,z);
        }
        public void Reserve(string id,OrientedBox box)
        {
            if(string.IsNullOrWhiteSpace(id)||!box.Bounds.Valid||box.x.LengthSquared<.9f||box.y.LengthSquared<.9f||box.z.LengthSquared<.9f)throw new ArgumentException("Valid reservation ID and oriented box required");
            var keys=Keys(box.Bounds).ToArray();
            var entry=new Entry{id=id,box=box};entries.Add(entry);
            foreach(var key in keys){if(!cells.TryGetValue(key,out var list))cells.Add(key,list=new List<Entry>());list.Add(entry);}
        }
        public string Collision(OrientedBox box,IEnumerable<string> allowed=null)
        {
            var allow=new HashSet<string>(allowed??Array.Empty<string>(),StringComparer.Ordinal);var tested=new HashSet<Entry>();
            foreach(var key in Keys(box.Bounds))if(cells.TryGetValue(key,out var list))foreach(var entry in list)
                if(tested.Add(entry)&&!allow.Contains(entry.id)&&box.Intersects(entry.box))return entry.id;
            return null;
        }
        public void Rollback(int checkpoint)
        {
            if(checkpoint<0||checkpoint>entries.Count)throw new ArgumentOutOfRangeException(nameof(checkpoint));
            for(int i=entries.Count-1;i>=checkpoint;i--){var entry=entries[i];foreach(var key in Keys(entry.box.Bounds))if(cells.TryGetValue(key,out var list)){list.Remove(entry);if(list.Count==0)cells.Remove(key);}entries.RemoveAt(i);}
        }
    }
    public static class PlacementSolver
    {
        public static PlacementResult Place(PlacementRequest request,PlacementContext context,CancellationToken cancellation=default)
        {
            var result=new PlacementResult();
            if(context==null)throw new ArgumentNullException(nameof(context));
            if(request==null||string.IsNullOrWhiteSpace(request.id)||request.shape==null||string.IsNullOrWhiteSpace(request.shape.id)||request.shape.contacts==null||request.shape.volumes==null||request.shape.contacts.Length==0||request.shape.volumes.Length==0||request.shape.contacts.Any(p=>!p.Finite)||request.shape.volumes.Any(b=>!b.Valid)||!Numbers.Finite(request.shape.motionMargin)||request.shape.motionMargin<0||!request.target.Finite||!Numbers.Finite(request.scale)||request.scale<=0||request.candidates<1||request.candidates>4096||!Numbers.Finite(request.settlement)||request.settlement<0||!Numbers.Finite(request.searchRadius)||request.searchRadius<0||!Numbers.Finite(request.yaw)||!Numbers.Finite(request.maxSlope)||request.maxSlope<0||request.maxSlope>90||!Numbers.Finite(request.maxSupportGap)||request.maxSupportGap<0)
            {result.diagnostics.Add(new Diagnostic("invalid-request",request?.id,"Finite dimensions, contact anchors, volumes and bounded search required"));return result;}
            Diagnostic last=null;int attempts=request.searchRadius>0?request.candidates:1;
            for(int attempt=0;attempt<attempts;attempt++)
            {
                cancellation.ThrowIfCancellationRequested();var target=request.target;
                if(attempt>0){float angle=Numbers.Unit(request.id,request.seed,attempt*2)*6.2831853f,r=(float)Math.Sqrt(Numbers.Unit(request.id,request.seed,attempt*2+1))*request.searchRadius;target+=new V3((float)Math.Cos(angle)*r,0,(float)Math.Sin(angle)*r);}
                if(!context.surface.TrySample(target,-context.Up,out var surface)){last=new Diagnostic("missing-surface",request.id,"No supporting surface");continue;}
                float slope=(float)(Math.Acos(Numbers.Clamp(V3.Dot(surface.normal.Normalized,context.Up.Normalized),-1,1))/Numbers.Radians);
                if(slope>request.maxSlope){last=new Diagnostic("surface-slope",request.id,"Slope exceeds profile");continue;}
                var pose=Pose.At(surface.point,request.yaw,new V3(request.scale,request.scale,request.scale),request.alignToSurface?surface.normal:context.Up);
                float shift=float.NegativeInfinity;var projections=new List<float>();bool supported=true;
                foreach(var anchor in request.shape.contacts)
                {
                    var contact=pose.Point(anchor);if(!context.surface.TrySample(contact,-context.Up,out var s)){supported=false;break;}
                    float delta=V3.Dot(s.point-contact,context.Up);projections.Add(delta);shift=Math.Max(shift,delta);
                }
                if(!supported){last=new Diagnostic("unsupported-contact",request.id,"A contact anchor has no surface");continue;}
                if(projections.Any(d=>shift-d>request.maxSupportGap)){last=new Diagnostic("support-gap",request.id,"Contact gap exceeds profile");continue;}
                pose.position+=context.Up*(shift-request.settlement);
                var placed=new PlacedInstance{id=request.id,owner=request.owner,asset=request.shape.id,pose=pose,surface=surface,volumes=request.shape.volumes.Select(b=>new OrientedBox(b.Expanded(request.shape.motionMargin),pose)).ToArray()};
                string collision=null;foreach(var box in placed.volumes){collision=context.Collision(box,request.allowedContacts);if(collision!=null)break;}
                if(collision!=null){last=new Diagnostic("volume-overlap",request.id,"Intersects "+collision);continue;}
                Diagnostic issue=null;var warnings=new List<Diagnostic>();foreach(var rule in context.rules){issue=rule.Check(request,placed,context);if(issue!=null&&issue.error)break;if(issue!=null)warnings.Add(issue);issue=null;}
                if(issue!=null){last=issue;continue;}
                int checkpoint=context.ReservationCount;
                try{foreach(var box in placed.volumes)context.Reserve(placed.id,box);}catch{context.Rollback(checkpoint);throw;}
                result.diagnostics.AddRange(warnings);result.instances.Add(placed);return result;
            }
            result.diagnostics.Add(last??new Diagnostic("no-candidate",request.id,"No acceptable placement"));return result;
        }
        public static PlacementResult Ensemble(IEnumerable<PlacementRequest> requests,PlacementContext context,CancellationToken cancellation=default)
        {
            if(requests==null||context==null)throw new ArgumentNullException();
            int checkpoint=context.ReservationCount;var result=new PlacementResult();var input=requests.ToArray();
            if(input.Any(r=>r==null)){result.diagnostics.Add(new Diagnostic("invalid-request",null,"Null ensemble member"));return result;}
            var ordered=input.OrderBy(r=>r.id,StringComparer.Ordinal).ToArray();
            if(ordered.Select(r=>r.id).Distinct(StringComparer.Ordinal).Count()!=ordered.Length){result.diagnostics.Add(new Diagnostic("duplicate-id",null,"Ensemble IDs must be unique"));return result;}
            try{foreach(var request in ordered){var next=Place(request,context,cancellation);if(!next.Ok&&!request.required)foreach(var d in next.diagnostics)d.error=false;result.diagnostics.AddRange(next.diagnostics);result.instances.AddRange(next.instances);if(!next.Ok&&request.required){context.Rollback(checkpoint);result.instances.Clear();return result;}}return result;}
            catch{context.Rollback(checkpoint);throw;}
        }
    }
}
