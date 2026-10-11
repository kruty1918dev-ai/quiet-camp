using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kruty1918.ScenePlacement;

namespace Kruty1918.PowerLines
{
    public enum CableState { Intact, Severed, HangingFrom, HangingTo, Grounded }
    [Serializable] public sealed class SupportKit
    {public AssetShape standing,fallen;public V3[] sockets=Array.Empty<V3>();public string networkKind="distribution";}
    [Serializable] public sealed class CableDamage {public int span;public CableState state;public float dropLength=1;}
    public sealed class PowerSpec
    {
        public string id,networkKind="distribution";public RoutePath route;public int seed;public float offset=1,minimumSpan=3,maximumSpan=8,scale=1,sag=.35f,radius=.012f;
        public int samples=12,sides=4;public int[] fallenSupports=Array.Empty<int>();public CableDamage[] damage=Array.Empty<CableDamage>();public Tint cableTint=new Tint(.18f,.19f,.17f);
    }
    public sealed class CableSpan
    {public string id,from,to;public int socket;public CableState state;public V3[] points;public MeshData mesh;public OrientedBox[] volumes;}
    public sealed class PowerResult
    {
        public readonly List<PlacedInstance> supports=new List<PlacedInstance>();public readonly List<CableSpan> spans=new List<CableSpan>();
        public readonly List<Diagnostic> diagnostics=new List<Diagnostic>();public bool Ok=>!diagnostics.Any(d=>d.error);
    }
    public static class PowerGenerator
    {
        public static PowerResult Generate(PowerSpec spec,SupportKit kit,PlacementContext context,CancellationToken cancellation=default)
        {
            var result=new PowerResult();int checkpoint=context.ReservationCount;
            if(spec==null||string.IsNullOrWhiteSpace(spec.id)||spec.route==null||kit?.standing==null||kit.sockets==null||kit.sockets.Length==0||kit.sockets.Any(s=>!s.Finite)||kit.networkKind!=spec.networkKind||!Numbers.Finite(spec.minimumSpan)||spec.minimumSpan<=0||!Numbers.Finite(spec.maximumSpan)||spec.maximumSpan<spec.minimumSpan||!Numbers.Finite(spec.sag)||spec.sag<0||!Numbers.Finite(spec.radius)||spec.radius<=0||!Numbers.Finite(spec.offset)||spec.samples<2||spec.samples>4096||spec.sides<3||spec.sides>32||!Numbers.Finite(spec.scale)||spec.scale<=0)
            {result.diagnostics.Add(new Diagnostic("invalid-power-network",spec?.id,"Compatible support sockets, span limits and bounded cable sampling required"));return result;}
            int count=(int)Math.Ceiling(spec.route.Length/spec.maximumSpan);if(count<1||spec.route.Length/count<spec.minimumSpan)
            {result.diagnostics.Add(new Diagnostic("power-span-length",spec.id,"Route length cannot satisfy span limits"));return result;}
            if(count>4096){result.diagnostics.Add(new Diagnostic("power-support-limit",spec.id,"Split the network into bounded requests"));return result;}
            if(spec.fallenSupports.Any(i=>i<0||i>count)||spec.fallenSupports.Length>0&&kit.fallen==null||spec.damage.Any(d=>d.span<0||d.span>=count||d.dropLength<=0)||spec.damage.Select(d=>d.span).Distinct().Count()!=spec.damage.Length)
            {result.diagnostics.Add(new Diagnostic("invalid-power-damage",spec.id,"Fallen asset, valid distinct span indices and positive drop lengths required"));return result;}
            try
            {
                for(int i=0;i<=count;i++)
                {
                    cancellation.ThrowIfCancellationRequested();var p=spec.route.At(spec.route.Length*i/count,out var tangent);p+=V3.Cross(V3.Up,tangent).Normalized*spec.offset;
                    bool fallen=spec.fallenSupports.Contains(i);var shape=fallen?kit.fallen:kit.standing;
                    var placed=PlacementSolver.Place(new PlacementRequest{id=spec.id+"/support-"+i,owner=spec.id,target=p,shape=shape,scale=spec.scale,yaw=(float)(Math.Atan2(tangent.x,tangent.z)/Numbers.Radians),maxSupportGap=.12f},context,cancellation);
                    if(!placed.Ok){result.diagnostics.AddRange(placed.diagnostics);context.Rollback(checkpoint);result.supports.Clear();return result;}result.supports.Add(placed.instances[0]);
                }
                for(int span=0;span<count;span++)
                {
                    var damage=spec.damage.FirstOrDefault(d=>d.span==span);var state=damage?.state??CableState.Intact;
                    bool fromFallen=spec.fallenSupports.Contains(span),toFallen=spec.fallenSupports.Contains(span+1);
                    if((fromFallen||toFallen)&&state==CableState.Intact||fromFallen&&state==CableState.HangingFrom||toFallen&&state==CableState.HangingTo)
                    {result.diagnostics.Add(new Diagnostic("fallen-cable-anchor",spec.id,"Cable is attached to a fallen standing socket"));context.Rollback(checkpoint);result.supports.Clear();result.spans.Clear();return result;}
                    for(int socket=0;socket<kit.sockets.Length;socket++)
                    {
                        cancellation.ThrowIfCancellationRequested();var a=result.supports[span].pose.Point(kit.sockets[socket]);var b=result.supports[span+1].pose.Point(kit.sockets[socket]);
                        if(state==CableState.Severed)
                        {
                            if(!fromFallen)AddHanging(a,b,true);if(!toFallen)AddHanging(a,b,false);continue;
                        }
                        if(state==CableState.HangingFrom||state==CableState.HangingTo){AddHanging(a,b,state==CableState.HangingFrom);continue;}
                        var points=new V3[spec.samples+1];
                        for(int i=0;i<=spec.samples;i++)
                        {
                            float t=(float)i/spec.samples;var p=V3.Lerp(a,b,t);
                            if(state==CableState.Grounded){if(!context.surface.TrySample(p,-V3.Up,out var ground)){result.diagnostics.Add(new Diagnostic("ground-cable-surface",spec.id,"Grounded cable unsupported"));break;}p.y=ground.point.y+spec.radius;}
                            else
                            {
                                // Endpoint-preserving sag; no runtime physics simulation.
                                float sag=(float)((Math.Cosh(1)-Math.Cosh(2*(t-.5f)))/(Math.Cosh(1)-1));p.y-=spec.sag*sag;
                                if(context.surface.TrySample(p,-V3.Up,out var ground)&&p.y-spec.radius<ground.point.y){result.diagnostics.Add(new Diagnostic("cable-ground-intersection",spec.id,"Intact cable crosses terrain; reduce sag or move supports"));break;}
                            }
                            points[i]=p;
                        }
                        if(!result.Ok)break;
                        AddCable(new CableSpan{id=spec.id+"/span-"+span+"/"+socket,from=result.supports[span].id,to=result.supports[span+1].id,socket=socket,state=state,points=points,mesh=Tube(points,spec.radius,spec.sides,spec.cableTint)});
                        void AddHanging(V3 from,V3 to,bool fromSide)
                        {
                            var anchor=fromSide?from:to;var direction=(fromSide?to-from:from-to);direction.y=0;direction=direction.Normalized;
                            float drop=damage?.dropLength??1;var target=anchor+direction*drop*.32f;target.y=anchor.y-drop;
                            if(context.surface.TrySample(target,-V3.Up,out var ground))target.y=Math.Max(ground.point.y+spec.radius,target.y);
                            var curve=new V3[spec.samples+1];for(int i=0;i<=spec.samples;i++){float t=(float)i/spec.samples;curve[i]=V3.Lerp(anchor,target,t)+direction*(float)Math.Sin(t*Math.PI)*drop*.10f;if(context.surface.TrySample(curve[i],-V3.Up,out var support))curve[i].y=Math.Max(curve[i].y,support.point.y+spec.radius);}
                            AddCable(new CableSpan{id=spec.id+"/span-"+span+"/"+socket+"/"+(fromSide?"a":"b"),from=fromSide?result.supports[span].id:null,to=fromSide?null:result.supports[span+1].id,socket=socket,state=fromSide?CableState.HangingFrom:CableState.HangingTo,points=curve,mesh=Tube(curve,spec.radius,spec.sides,spec.cableTint)});
                        }
                    }
                    if(!result.Ok)break;
                }
                if(!result.Ok){context.Rollback(checkpoint);result.supports.Clear();result.spans.Clear();}return result;
                void AddCable(CableSpan cable)
                {
                    var boxes=new List<OrientedBox>();var allowed=new[]{cable.from,cable.to}.Where(id=>id!=null).Concat(result.spans.Where(s=>s.from==cable.from||s.to==cable.to||s.to==cable.from||s.from==cable.to).Select(s=>s.id)).ToArray();
                    for(int i=1;i<cable.points.Length;i++)
                    {
                        var delta=cable.points[i]-cable.points[i-1];if(delta.LengthSquared<1e-12f)continue;var forward=delta.Normalized;var right=V3.Cross(Math.Abs(forward.y)>.9f?new V3(1,0,0):V3.Up,forward).Normalized;
                        var pose=new Pose{position=(cable.points[i]+cable.points[i-1])*.5f,forward=forward,right=right,up=V3.Cross(forward,right),scale=new V3(1,1,1)};
                        var box=new OrientedBox(new Box(V3.Zero,new V3(spec.radius,spec.radius,delta.Length*.5f)),pose);
                        var collision=context.Collision(box,allowed);if(collision!=null){result.diagnostics.Add(new Diagnostic("cable-volume-overlap",cable.id,"Cable intersects "+collision));return;}boxes.Add(box);
                    }
                    cable.volumes=boxes.ToArray();foreach(var box in boxes)context.Reserve(cable.id,box);result.spans.Add(cable);
                }
            }
            catch{context.Rollback(checkpoint);throw;}
        }
        public static MeshData Tube(V3[] points,float radius,int sides,Tint tint)
        {
            if(points.Length<2||radius<=0||sides<3)throw new ArgumentException("Cable tube dimensions");var builder=new GeometryBuilder();
            for(int i=1;i<points.Length;i++)
            {
                var axis=(points[i]-points[i-1]).Normalized;var side=V3.Cross(axis,Math.Abs(axis.y)>.9f?new V3(1,0,0):V3.Up).Normalized;var up=V3.Cross(side,axis).Normalized;
                V3 Ring(int end,int k){float angle=(float)(Math.PI*2*k/sides);return points[end]+(side*(float)Math.Cos(angle)+up*(float)Math.Sin(angle))*radius;}
                for(int k=0;k<sides;k++)builder.Quad(Ring(i-1,k),Ring(i,k),Ring(i,k+1),Ring(i-1,k+1),tint);
            }
            return builder.Build();
        }
    }
}
