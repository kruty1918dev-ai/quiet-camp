using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kruty1918.ScenePlacement;

namespace Kruty1918.RoadAuthoring
{
    [Serializable] public sealed class SurfaceBand
    {public float distance,width=1;public Tint tint=new Tint(.32f,.30f,.23f);public string surface="dirt";}
    [Serializable] public sealed class RoadSpec
    {
        public string id;public V3[] points=Array.Empty<V3>();public SurfaceBand[] bands=Array.Empty<SurfaceBand>();
        public int seed;public bool smooth=true;public float step=.3f,edgeRoughness=.08f,heightOffset=.008f,maximumSlope=35;
        public float maximumGradeChange=.45f;public V3[] potholes=Array.Empty<V3>();public float potholeRadius=.22f;
        public int maximumSamples=4096,maximumIntersectionChecks=2000000;
    }
    public sealed class TerrainAdjustment {public V3 point;public float requestedHeight;public string reason;}
    public sealed class RoadResult
    {
        public RoutePath route;public MeshData mesh;public float[] widths,materialWeights;public string[] surfaces;
        public readonly List<OrientedBox> corridor=new List<OrientedBox>();public readonly List<TerrainAdjustment> terrainAdjustments=new List<TerrainAdjustment>();
        public readonly List<Diagnostic> diagnostics=new List<Diagnostic>();public bool Ok=>!diagnostics.Any(d=>d.error);
    }
    public static class RoadGenerator
    {
        static V3 Cubic(V3 before,V3 a,V3 b,V3 after,float t)
        {float t2=t*t,t3=t2*t;var ma=(b-before)*.5f;var mb=(after-a)*.5f;return a*(2*t3-3*t2+1)+ma*(t3-2*t2+t)+b*(-2*t3+3*t2)+mb*(t3-t2);}
        static bool Crosses(V3 a,V3 b,V3 c,V3 d)
        {
            float Cross(V3 x,V3 y)=>x.x*y.z-x.z*y.x;var ab=b-a;var cd=d-c;float den=Cross(ab,cd);if(Math.Abs(den)<1e-7f)return false;
            float u=Cross(c-a,cd)/den,v=Cross(c-a,ab)/den;return u>1e-4f&&u<.9999f&&v>1e-4f&&v<.9999f;
        }
        public static RoadResult Generate(RoadSpec spec,ISurfaceQuery surface,CancellationToken cancellation=default)
        {
            var result=new RoadResult();
            if(spec==null||surface==null||string.IsNullOrWhiteSpace(spec.id)||spec.points==null||spec.points.Length<2||spec.bands==null||spec.bands.Length==0||spec.step<=.01f||!Numbers.Finite(spec.step)||!Numbers.Finite(spec.heightOffset)||!Numbers.Finite(spec.edgeRoughness)||spec.edgeRoughness<0||!Numbers.Finite(spec.potholeRadius)||spec.potholeRadius<0||spec.potholes==null||spec.potholes.Any(p=>!p.Finite)||spec.points.Any(p=>!p.Finite)||spec.bands.Any(b=>b==null||b.width<=0||!Numbers.Finite(b.width)||!Numbers.Finite(b.distance))||spec.maximumSamples<2||spec.maximumSamples>65536||spec.maximumIntersectionChecks<1)
            {result.diagnostics.Add(new Diagnostic("invalid-road",spec?.id,"Finite route, width bands and positive sampling step required"));return result;}
            var points=new List<V3>();
            for(int segment=0;segment<spec.points.Length-1;segment++)
            {
                float length=(spec.points[segment+1]-spec.points[segment]).Length;if(length<1e-4f){result.diagnostics.Add(new Diagnostic("repeated-route-point",spec.id,"Distinct control points required"));return result;}
                int steps=Math.Max(1,(int)Math.Ceiling(length/spec.step));if(points.Count+steps>=spec.maximumSamples){result.diagnostics.Add(new Diagnostic("route-sampling-limit",spec.id,"Increase step or split route"));return result;}
                for(int i=0;i<steps;i++)
                {
                    cancellation.ThrowIfCancellationRequested();float t=(float)i/steps;
                    var p=spec.smooth?Cubic(spec.points[Math.Max(0,segment-1)],spec.points[segment],spec.points[segment+1],spec.points[Math.Min(spec.points.Length-1,segment+2)],t):V3.Lerp(spec.points[segment],spec.points[segment+1],t);
                    if(!surface.TrySample(p,-V3.Up,out var sample)){result.diagnostics.Add(new Diagnostic("road-missing-surface",spec.id,"Route has no surface"));return result;}p.y=sample.point.y+spec.heightOffset;points.Add(p);
                }
            }
            var end=spec.points[spec.points.Length-1];if(!surface.TrySample(end,-V3.Up,out var final)){result.diagnostics.Add(new Diagnostic("road-missing-end",spec.id,"Final endpoint unsupported"));return result;}end.y=final.point.y+spec.heightOffset;points.Add(end);
            int checks=0;for(int i=1;i<points.Count;i++)for(int j=i+2;j<points.Count;j++)
            {cancellation.ThrowIfCancellationRequested();if(++checks>spec.maximumIntersectionChecks){result.diagnostics.Add(new Diagnostic("road-intersection-work-limit",spec.id,"Split route or explicitly raise bounded work limit"));return result;}if(Crosses(points[i-1],points[i],points[j-1],points[j])){result.diagnostics.Add(new Diagnostic("road-self-intersection",spec.id,"Declare an intersection as connected route endpoints"));return result;}}
            result.route=new RoutePath(points.ToArray());var bands=spec.bands.OrderBy(b=>b.distance).ToArray();result.widths=new float[points.Count];result.materialWeights=new float[points.Count];result.surfaces=new string[points.Count];var left=new V3[points.Count];var right=new V3[points.Count];var tints=new Tint[points.Count];float distance=0,previousGrade=0;
            for(int i=0;i<points.Count;i++)
            {
                cancellation.ThrowIfCancellationRequested();if(i>0)distance+=(points[i]-points[i-1]).Length;
                int band=0;while(band+1<bands.Length&&bands[band+1].distance<=distance)band++;var a=bands[band];var b=bands[Math.Min(band+1,bands.Length-1)];float blend=a==b?0:Numbers.Smooth((distance-a.distance)/Math.Max(.001f,b.distance-a.distance));
                result.materialWeights[i]=blend;
                float width=a.width+(b.width-a.width)*blend;result.widths[i]=width;result.surfaces[i]=blend<.5f?a.surface:b.surface;tints[i]=Tint.Lerp(a.tint,b.tint,blend);
                var tangent=(points[Math.Min(points.Count-1,i+1)]-points[Math.Max(0,i-1)]).Normalized;var side=V3.Cross(V3.Up,tangent).Normalized;
                float phase=Numbers.Unit(spec.id,spec.seed)*6.2831853f;float l=width*.5f+spec.edgeRoughness*(float)(Math.Sin(distance*1.3+phase)*.65+Math.Sin(distance*3.2)*.35);float r=width*.5f+spec.edgeRoughness*(float)(Math.Sin(distance*1.1+phase+2)*.6+Math.Sin(distance*2.9)*.4);
                left[i]=points[i]-side*Math.Max(width*.25f,l);right[i]=points[i]+side*Math.Max(width*.25f,r);
                foreach(var edge in new[]{left[i],right[i]})if(!surface.TrySample(edge,-V3.Up,out _)){result.diagnostics.Add(new Diagnostic("road-edge-unsupported",spec.id,"Road width extends beyond supporting surface"));return result;}
                surface.TrySample(left[i],-V3.Up,out var ls);surface.TrySample(right[i],-V3.Up,out var rs);left[i].y=ls.point.y+spec.heightOffset;right[i].y=rs.point.y+spec.heightOffset;
                if(i>0)
                {
                    var delta=points[i]-points[i-1];float horizontal=(float)Math.Sqrt(delta.x*delta.x+delta.z*delta.z);float grade=delta.y/Math.Max(.001f,horizontal);float angle=(float)(Math.Atan(Math.Abs(grade))/Numbers.Radians);
                    if(angle>spec.maximumSlope||i>1&&Math.Abs(grade-previousGrade)>spec.maximumGradeChange)
                    {result.diagnostics.Add(new Diagnostic("road-grade",spec.id,"Route needs authored terrain adjustment"));result.terrainAdjustments.Add(new TerrainAdjustment{point=points[i],requestedHeight=points[i-1].y,reason="grade"});}
                    previousGrade=grade;
                }
            }
            var builder=new GeometryBuilder();
            for(int i=1;i<points.Count;i++)
            {
                cancellation.ThrowIfCancellationRequested();var center=(points[i-1]+points[i])*.5f;
                int slices=spec.potholes.Length==0?1:Math.Min(32,Math.Max(4,(int)Math.Ceiling(Math.Max(result.widths[i],result.widths[i-1])/Math.Max(.04f,spec.potholeRadius*.6f))));
                for(int slice=0;slice<slices;slice++)
                {
                    var a=V3.Lerp(left[i-1],right[i-1],(float)slice/slices);var b=V3.Lerp(left[i],right[i],(float)slice/slices);var c=V3.Lerp(left[i],right[i],(float)(slice+1)/slices);var d=V3.Lerp(left[i-1],right[i-1],(float)(slice+1)/slices);
                    var uvA=new V2((float)slice/slices,i-1);var uvB=new V2((float)slice/slices,i);var uvC=new V2((float)(slice+1)/slices,i);var uvD=new V2((float)(slice+1)/slices,i-1);
                    Add(a,b,c,uvA,uvB,uvC);Add(a,c,d,uvA,uvC,uvD);
                    void Add(V3 p,V3 q,V3 r,V2 up,V2 uq,V2 ur)
                    {var midpoint=(p+q+r)/3;bool hole=spec.potholes.Any(h=>new V3(midpoint.x-h.x,0,midpoint.z-h.z).Length<spec.potholeRadius);if(!hole)builder.Triangle(p,q,r,Tint.Lerp(tints[i-1],tints[i],.5f),0,up,uq,ur);}
                }
                float length=(points[i]-points[i-1]).Length;float yaw=(float)(Math.Atan2(points[i].x-points[i-1].x,points[i].z-points[i-1].z)/Numbers.Radians);
                result.corridor.Add(new OrientedBox(new Box(V3.Zero,new V3(Math.Max(result.widths[i-1],result.widths[i])*.5f+spec.edgeRoughness,.10f,length*.5f)),Pose.At(center,yaw)));
            }
            if(builder.TriangleCount==0){result.diagnostics.Add(new Diagnostic("road-no-surface",spec.id,"Potholes cannot remove the entire road"));return result;}
            result.mesh=builder.Build();result.mesh.Validate();return result;
        }
        public static IReadOnlyList<Diagnostic> ValidateConnections(IEnumerable<(string id,RoutePath path,string next)> routes,float tolerance=.01f)
        {
            var all=routes.ToArray();var result=new List<Diagnostic>();foreach(var route in all.Where(r=>!string.IsNullOrEmpty(r.next)))
            {var matches=all.Where(r=>r.id==route.next).ToArray();if(matches.Length!=1)result.Add(new Diagnostic("missing-road-continuation",route.id,"Continuation must identify one route"));else if((route.path.points.Last()-matches[0].path.points[0]).Length>tolerance)result.Add(new Diagnostic("disconnected-road",route.id,"Continuation endpoints differ"));}return result;
        }
    }
}
