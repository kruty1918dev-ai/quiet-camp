using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Kruty1918.ScenePlacement;

namespace Kruty1918.FenceAuthoring
{
    [Serializable] public sealed class FenceOpening {public string id;public float from,to;public bool gate;}
    [Serializable] public sealed class FenceKit
    {
        public AssetShape post,section,gate;public V3 railSocket=new V3(0,.65f,0);public float sectionLength=1.5f,minStretch=.65f,maxStretch=1.35f;
    }
    [Serializable] public sealed class FenceSpec
    {
        public string id;public V3[] contour=Array.Empty<V3>();public bool closed;public int seed;
        public float offset,jitter=.035f,lean=3,maxSupportGap=.08f;public FenceOpening[] openings=Array.Empty<FenceOpening>();
    }
    public sealed class FenceConnection {public string id,a,b;public V3 from,to;public bool gate;}
    public sealed class FenceResult
    {
        public readonly List<PlacedInstance> instances=new List<PlacedInstance>();public readonly List<FenceConnection> connections=new List<FenceConnection>();
        public readonly List<Diagnostic> diagnostics=new List<Diagnostic>();public bool Ok=>!diagnostics.Any(d=>d.error);
    }
    public static class FenceGenerator
    {
        public static FenceResult Generate(FenceSpec spec,FenceKit kit,PlacementContext context,CancellationToken cancellation=default)
        {
            var result=new FenceResult();int checkpoint=context.ReservationCount;
            if(spec==null||kit?.post==null||kit.section==null||spec.contour==null||spec.contour.Length<(spec.closed?3:2)||spec.contour.Any(p=>!p.Finite)||string.IsNullOrWhiteSpace(spec.id)||!Numbers.Finite(kit.sectionLength)||kit.sectionLength<=0||!Numbers.Finite(kit.minStretch)||kit.minStretch<=0||!Numbers.Finite(kit.maxStretch)||kit.maxStretch<kit.minStretch||!Numbers.Finite(spec.jitter)||spec.jitter<0||!Numbers.Finite(spec.lean)||Math.Abs(spec.lean)>30||!Numbers.Finite(spec.offset)||!kit.railSocket.Finite)
            {result.diagnostics.Add(new Diagnostic("invalid-fence",spec?.id,"Contour and compatible post/section kit required"));return result;}
            try
            {
                var points=spec.contour.ToList();if(spec.closed&&(points[0]-points.Last()).Length>.0001f)points.Add(points[0]);var path=new RoutePath(points.ToArray());
                if(path.Length/kit.sectionLength>4096){result.diagnostics.Add(new Diagnostic("fence-section-limit",spec.id,"Split fence into bounded source contours"));return result;}
                foreach(var opening in spec.openings)
                    if(opening==null||!Numbers.Finite(opening.from)||!Numbers.Finite(opening.to)||opening.from<0||opening.to<=opening.from||opening.to>path.Length||opening.gate&&kit.gate==null)
                    {result.diagnostics.Add(new Diagnostic("invalid-fence-opening",opening?.id,"Finite interval and gate asset required"));return result;}
                var ordered=spec.openings.OrderBy(o=>o.from).ToArray();for(int i=1;i<ordered.Length;i++)if(ordered[i].from<ordered[i-1].to){result.diagnostics.Add(new Diagnostic("overlapping-fence-openings",spec.id,"Opening intervals overlap"));return result;}
                var breakpoints=new SortedSet<float>{0,path.Length};float running=0;
                for(int i=1;i<points.Count;i++){running+=(points[i]-points[i-1]).Length;breakpoints.Add(running);}
                foreach(var opening in ordered){breakpoints.Add(opening.from);breakpoints.Add(opening.to);}
                var boundaries=breakpoints.ToArray();var distances=new SortedSet<float>(boundaries);
                for(int i=1;i<boundaries.Length;i++)
                {
                    float start=boundaries[i-1],end=boundaries[i];if(ordered.Any(o=>start>=o.from-.0001f&&end<=o.to+.0001f))continue;
                    int count=Math.Max(1,(int)Math.Ceiling((end-start)/kit.sectionLength));for(int j=1;j<count;j++)distances.Add(start+(end-start)*j/count);
                }
                var samples=distances.ToArray();var posts=new Dictionary<float,PlacedInstance>();
                for(int i=0;i<samples.Length;i++)
                {
                    cancellation.ThrowIfCancellationRequested();float d=samples[i];if(spec.closed&&i==samples.Length-1){posts.Add(d,posts[samples[0]]);continue;}
                    var p=path.At(d,out var tangent);var side=V3.Cross(V3.Up,tangent).Normalized;p+=side*(spec.offset+(Numbers.Unit(spec.id,spec.seed,i)*2-1)*spec.jitter);
                    int postCheckpoint=context.ReservationCount;
                    var placed=PlacementSolver.Place(new PlacementRequest{id=spec.id+"/post-"+i,owner=spec.id,shape=kit.post,target=p,yaw=(float)(Math.Atan2(tangent.x,tangent.z)/Numbers.Radians),maxSupportGap=spec.maxSupportGap},context,cancellation);
                    if(!placed.Ok){result.diagnostics.AddRange(placed.diagnostics);context.Rollback(checkpoint);result.instances.Clear();return result;}
                    var post=placed.instances[0];
                    if(spec.lean!=0)
                    {
                        float tilt=(Numbers.Unit(post.id,spec.seed,22)*2-1)*spec.lean*Numbers.Radians;var pose=post.pose;var up=(V3.Up+pose.right*(float)Math.Tan(tilt)).Normalized;post.pose=Pose.At(pose.position,(float)(Math.Atan2(pose.forward.x,pose.forward.z)/Numbers.Radians),pose.scale,up);
                        var gaps=new List<float>();foreach(var contact in kit.post.contacts){var anchor=post.pose.Point(contact);if(!context.surface.TrySample(anchor,-V3.Up,out var support)){result.diagnostics.Add(new Diagnostic("fence-lean-unsupported",post.id,"Tilted post contact has no ground"));context.Rollback(checkpoint);result.instances.Clear();return result;}gaps.Add(support.point.y-anchor.y);}
                        float shift=gaps.Max();if(gaps.Any(g=>shift-g>spec.maxSupportGap)){result.diagnostics.Add(new Diagnostic("fence-lean-contact-gap",post.id,"Tilted post loses support"));context.Rollback(checkpoint);result.instances.Clear();return result;}
                        post.pose.position+=V3.Up*shift;
                        context.Rollback(postCheckpoint);post.volumes=kit.post.volumes.Select(v=>new OrientedBox(v.Expanded(kit.post.motionMargin),post.pose)).ToArray();
                        foreach(var v in post.volumes)if(context.Collision(v)!=null){result.diagnostics.Add(new Diagnostic("fence-post-lean-collision",post.id,"Lean intersects unrelated geometry"));context.Rollback(checkpoint);result.instances.Clear();return result;}
                        foreach(var v in post.volumes)context.Reserve(post.id,v);
                    }
                    posts.Add(d,post);result.instances.Add(post);
                }
                for(int i=1;i<samples.Length;i++)
                {
                    cancellation.ThrowIfCancellationRequested();float start=samples[i-1],end=samples[i];var opening=ordered.FirstOrDefault(o=>start>=o.from-.0001f&&end<=o.to+.0001f);if(opening!=null&&!opening.gate)continue;
                    var a=posts[start];var b=posts[end];var from=a.pose.Point(kit.railSocket);var to=b.pose.Point(kit.railSocket);var delta=to-from;
                    var shape=opening?.gate==true?kit.gate:kit.section;float stretch=delta.Length/kit.sectionLength;
                    if(stretch<kit.minStretch||stretch>kit.maxStretch){result.diagnostics.Add(new Diagnostic("fence-section-length",spec.id,"Section stretch outside kit limits"));context.Rollback(checkpoint);result.instances.Clear();return result;}
                    var x=delta.Normalized;var z=V3.Cross(x,V3.Up).Normalized;var y=V3.Cross(z,x).Normalized;var pose=new Pose{position=(from+to)*.5f,right=x,up=y,forward=z,scale=new V3(stretch,1,1)};
                    string id=spec.id+"/section-"+(i-1);var instance=new PlacedInstance{id=id,owner=spec.id,asset=shape.id,pose=pose,volumes=shape.volumes.Select(v=>new OrientedBox(v.Expanded(shape.motionMargin),pose)).ToArray()};
                    var allowed=new[]{a.id,b.id,spec.id+"/section-"+(i-2),spec.id+"/section-"+i,spec.closed&&i==samples.Length-1?spec.id+"/section-0":""};
                    foreach(var volume in instance.volumes)if(context.Collision(volume,allowed)!=null){result.diagnostics.Add(new Diagnostic("fence-section-collision",id,"Section intersects unrelated geometry"));context.Rollback(checkpoint);result.instances.Clear();return result;}
                    foreach(var volume in instance.volumes)context.Reserve(id,volume);result.instances.Add(instance);result.connections.Add(new FenceConnection{id=id,a=a.id,b=b.id,from=from,to=to,gate=opening?.gate==true});
                }
                return result;
            }
            catch{context.Rollback(checkpoint);throw;}
        }
    }
}
