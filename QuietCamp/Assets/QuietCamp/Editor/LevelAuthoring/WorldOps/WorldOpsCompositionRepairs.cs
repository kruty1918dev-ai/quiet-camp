using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    /// <summary>Shared managed WorldOps operations. External previews invoke this
    /// exact C# authority; publication still requires the ordinary native review.
    /// Never changes a puzzle, source sun, camera, or another object's transform.</summary>
    public static class WorldOpsCompositionRepairs
    {
        public static void OpenWells(CampArtDefinition art)
        {
            WorldWellShafts.Invalidate(art);var holes=WorldWellShafts.Find(art);
            if(holes.Length==0)return;
            art.landscape=WorldWellShafts.Resolve(art,art.landscape);
            var models=art.models.ToDictionary(m=>m.id);
            art.instances=art.instances.Where(p=>p.modelId==null||!models.TryGetValue(p.modelId,out var m)
                ||m.seasonalRole!="groundcover"&&m.seasonalRole!="flower"||!WorldWellShafts.ExcludesGroundPlant(art,p.x,p.y,p.z)).ToArray();
            art.cover=art.cover.Where(p=>!WorldWellShafts.ExcludesGroundPlant(art,p.x,p.y,p.z)).ToArray();
        }
        public static bool IsBridge(string source)=>source=="ua_valley_footbridge"||source=="qc_campaign_landmark_036"||source=="qc_campaign_landmark_038"||source=="qc_campaign_landmark_057"||source=="qc_campaign_landmark_088";
        static (float y,float lowZ,float highZ,float halfWidth) Deck(CampArtDefinition.Model model)
        {
            float top=float.MinValue;
            for(int i=0;i<model.vertices.Length;i+=9)
            {
                float ax=model.vertices[i],az=model.vertices[i+2],bx=model.vertices[i+3],bz=model.vertices[i+5],cx=model.vertices[i+6],cz=model.vertices[i+8];
                float den=(bz-cz)*(ax-cx)+(cx-bx)*(az-cz);if(Mathf.Abs(den)<1e-9f)continue;
                float u=((bz-cz)*-cx+(cx-bx)*-cz)/den,v=((cz-az)*-cx+(ax-cx)*-cz)/den;
                if(u<-.00001f||v<-.00001f||u+v>1.00001f)continue;
                float y=u*model.vertices[i+1]+v*model.vertices[i+4]+(1-u-v)*model.vertices[i+7];if(y>.05f&&y<1)top=Mathf.Max(top,y);
            }
            if(top==float.MinValue)throw new InvalidOperationException("No verified walking deck at bridge centre.");
            var indices=Enumerable.Range(0,model.vertices.Length/3).Where(i=>Mathf.Abs(model.vertices[i*3+1]-top)<.004f).ToArray();
            if(indices.Length<3)throw new InvalidOperationException("Bridge deck bounds could not be derived from its actual faces.");
            return(top,indices.Min(i=>model.vertices[i*3+2]),indices.Max(i=>model.vertices[i*3+2]),indices.Max(i=>Mathf.Abs(model.vertices[i*3])));
        }
        public static void BridgeCrossings(CampArtDefinition art,string[] selectedIds=null)
        {
            var models=art.models.ToDictionary(m=>m.id);var water=new RiverSurfaceField(art.landscape);
            // Four scenes accidentally retain the old crossing at exactly the
            // same centre as their reserved unique bridge. Remove that selected
            // duplicate and its owned overgrowth, never the campaign landmark.
            var primary=art.instances.FirstOrDefault(p=>p.id==art.primaryLandmarkInstanceId&&p.modelId!=null&&models.TryGetValue(p.modelId,out var m)&&IsBridge(m.sourceModelId??m.id));
            if(primary!=null)
            {
                var duplicates=art.instances.Where(p=>p.id!=primary.id&&p.modelId!=null&&models.TryGetValue(p.modelId,out var m)&&(m.sourceModelId??m.id)=="ua_valley_footbridge"&&Mathf.Abs(p.x-primary.x)<.3f&&Mathf.Abs(p.z-primary.z)<.3f&&(selectedIds==null||selectedIds.Contains(p.id))).Select(p=>p.id).ToArray();
                var removed=art.instances.Where(p=>duplicates.Any(id=>p.id==id||p.id.StartsWith("overgrowth/"+id+"/",StringComparison.Ordinal)||p.id.StartsWith(id+"/craft/",StringComparison.Ordinal))).ToArray();
                var removedIds=removed.Select(p=>p.id).ToHashSet();art.instances=art.instances.Where(p=>!removedIds.Contains(p.id)).ToArray();
                var removedModels=removed.Where(p=>p.modelId!=null).Select(p=>p.modelId).ToHashSet();var used=art.instances.Select(p=>p.modelId).ToHashSet();art.models=art.models.Where(m=>!removedModels.Contains(m.id)||used.Contains(m.id)).ToArray();
            }
            foreach(var bridge in art.instances.Where(p=>p.modelId!=null&&models.TryGetValue(p.modelId,out var m)&&IsBridge(m.sourceModelId??m.id)&&(selectedIds==null||selectedIds.Contains(p.id))).ToArray())
            {
                string approachId="worldops/bridge-approach/"+bridge.id;if(art.instances.Any(p=>p.id==approachId))continue;
                if(bridge.rotationEuler!=null&&(Mathf.Abs(bridge.rotationEuler[0])>.001f||Mathf.Abs(bridge.rotationEuler[2])>.001f))throw new InvalidOperationException("A tilted pinned bridge requires an explicit ensemble review.");
                if(!water.TrySample(bridge.x,bridge.z,out var sample))throw new InvalidOperationException("Bridge has no actual river cross-section.");
                var axis=new Vector3(sample.direction.y,0,-sample.direction.x).normalized;
                float yaw=(float)(Math.Atan2(axis.x,axis.z)*180/Math.PI);var model=models[bridge.modelId];
                var deckShape=Deck(model);float scale=bridge.scaleXYZ?[2]??bridge.scale;
                float lowZ=deckShape.lowZ*scale,highZ=deckShape.highZ*scale;
                var centre=new Vector3(sample.centre.x,0,sample.centre.z);var a=centre+axis*lowZ;var b=centre+axis*highZ;
                foreach(var end in new[]{a,b})
                {
                    if(water.TrySample(end.x,end.z,out var bank)&&bank.Wet)throw new InvalidOperationException("Bridge is too short to reach both dry banks.");
                    if(end.x<art.landscape.originX+.8f||end.x>art.landscape.originX+art.landscape.size-.8f||end.z<art.landscape.originZ+.8f||end.z>art.landscape.originZ+art.landscape.size-.8f)throw new InvalidOperationException("Bridge landing would leave the authored terrain.");
                }
                float targetY=Mathf.Max(art.landscape.SampleHeight(a.x,a.z),art.landscape.SampleHeight(b.x,b.z))+.015f;
                float oldYaw=bridge.rotationEuler?[1]??bridge.yaw,delta=yaw-oldYaw;var rotation=Quaternion.Euler(0,delta,0);var origin=new Vector3(bridge.x,bridge.y,bridge.z);var target=new Vector3(centre.x,targetY,centre.z);
                foreach(var pose in art.instances.Where(p=>p.id==bridge.id||p.id.StartsWith("overgrowth/"+bridge.id+"/",StringComparison.Ordinal)||p.id.StartsWith(bridge.id+"/craft/",StringComparison.Ordinal)))
                {
                    var position=target+rotation*(new Vector3(pose.x,pose.y,pose.z)-origin);pose.x=position.x;pose.y=position.y;pose.z=position.z;pose.yaw+=delta;
                    if(pose.rotationEuler!=null)pose.rotationEuler=new[]{pose.rotationEuler[0],pose.rotationEuler[1]+delta,pose.rotationEuler[2]};
                    pose.surfaceOffset=pose.y-art.landscape.SampleHeight(pose.x,pose.z);
                }
                // Derive the actual deck, including its width and ends; no generic
                // 0.28 m assumption for the four different campaign bridges. Stone
                // abutments carry the ends rather than floating rail posts.
                var v=new List<float>();var n=new List<float>();var c=new List<int>();var side=Vector3.Cross(Vector3.up,axis).normalized;
                void Tri(Vector3 p,Vector3 q,Vector3 r,int color)
                {var normal=Vector3.Cross(q-p,r-p).normalized;foreach(var point in new[]{p,q,r}){v.AddRange(new[]{point.x,point.y,point.z});n.AddRange(new[]{normal.x,normal.y,normal.z});c.Add(color);}}
                void Top(Vector3 p,Vector3 q,Vector3 r,Vector3 t,int color)
                {if(Vector3.Cross(q-p,r-p).y<0){Tri(p,r,q,color);Tri(p,t,r,color);}else{Tri(p,q,r,color);Tri(p,r,t,color);}}
                float sy=bridge.scaleXYZ?[1]??bridge.scale;float deck=targetY+deckShape.y*sy;
                foreach(var pair in new[]{(at:a,outward:-axis),(at:b,outward:axis)})
                {
                    var end=pair.at;var outward=pair.outward;float width=deckShape.halfWidth*(bridge.scaleXYZ?[0]??bridge.scale);
                    float rampLength=Mathf.Max(1.15f,Mathf.Abs(deck-art.landscape.SampleHeight(end.x,end.z))/.35f);
                    if(rampLength>2.4f)throw new InvalidOperationException("Bridge needs a designed approach longer than 2.4 m.");
                    var far=end+outward*rampLength;float fy=art.landscape.SampleHeight(far.x,far.z)+.012f;
                    if(water.TrySample(far.x,far.z,out var wet)&&wet.Wet)throw new InvalidOperationException("Bridge approach terminates in water.");
                    if(Mathf.Abs(deck-fy)/rampLength>.6f)throw new InvalidOperationException("Bridge approach would be too steep.");
                    var p0=new Vector3(end.x,deck-.008f,end.z)-side*width;var p1=new Vector3(end.x,deck-.008f,end.z)+side*width;
                    var p2=new Vector3(far.x,fy,far.z)+side*width;var p3=new Vector3(far.x,fy,far.z)-side*width;
                    Top(p0,p1,p2,p3,0x77684D);
                    var low0=new Vector3(p0.x,art.landscape.SampleHeight(p0.x,p0.z)-.035f,p0.z);var low1=new Vector3(p1.x,art.landscape.SampleHeight(p1.x,p1.z)-.035f,p1.z);
                    Tri(p0,p3,low0,0x66583F);Tri(p1,low1,p2,0x66583F);Tri(p0,low0,low1,0x66583F);Tri(p0,low1,p1,0x66583F);
                    // Rough stone bearings, each with a positive ground overlap.
                    var inner=end-outward*.32f;float topY=targetY+(deckShape.y-.11f)*sy;var corners=new[]{end-side*(width+.1f),end+side*(width+.1f),inner+side*(width+.1f),inner-side*(width+.1f)};
                    var top=corners.Select(p=>new Vector3(p.x,topY,p.z)).ToArray();Top(top[0],top[1],top[2],top[3],0x77776B);
                    for(int i=0;i<4;i++){var p=top[i];var q=top[(i+1)%4];var downP=new Vector3(p.x,art.landscape.SampleHeight(p.x,p.z)-.045f,p.z);var downQ=new Vector3(q.x,art.landscape.SampleHeight(q.x,q.z)-.045f,q.z);Tri(p,downP,downQ,0x626356);Tri(p,downQ,q,0x626356);}
                }
                var approach=new CampArtDefinition.Model{id=approachId,sourceModelId="worldops_bridge_approach",seasonalRole="solid",shading="solid",vertices=v.ToArray(),normals=n.ToArray(),colors=c.ToArray(),authoringRecipe="{\"label\":\"Берегові підходи та кам’яні опори пішохідного мосту\",\"generator\":\"WorldOps bridge.crossing v1\"}"};
                art.models=art.models.Concat(new[]{approach}).ToArray();art.instances=art.instances.Concat(new[]{new CampArtDefinition.Instance{id=approachId,modelId=approachId,scale=1,surfaceAttached=false}}).ToArray();
            }
        }
        static float Cross(Vector3 a,Vector3 b,Vector3 c)=>(b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x);
        static List<Vector3> Hull(IEnumerable<Vector3> input)
        {
            var p=input.GroupBy(v=>(v.x,v.z)).Select(g=>g.First()).OrderBy(v=>v.x).ThenBy(v=>v.z).ToArray();var h=new List<Vector3>();
            foreach(var v in p){while(h.Count>=2&&Cross(h[h.Count-2],h[h.Count-1],v)<=0)h.RemoveAt(h.Count-1);h.Add(v);}
            int lower=h.Count;for(int i=p.Length-2;i>=0;i--){while(h.Count>lower&&Cross(h[h.Count-2],h[h.Count-1],p[i])<=0)h.RemoveAt(h.Count-1);h.Add(p[i]);}h.RemoveAt(h.Count-1);return h;
        }
        public static void ShadeSupport(CampArtDefinition art,LevelData level,float minimumHeight=2.6f,float maximumHeight=4)
        {
            if(level.shade==null||level.shade.Length==0)return;
            if(minimumHeight<2.2f||maximumHeight>4||minimumHeight>maximumHeight)throw new ArgumentException("Shade support height must be 2.2–4 m.");
            const string id="worldops/shade-support";
            if(art.instances.Any(p=>p.id==id))return;
            var direction=Quaternion.Euler(CampArtDefinition.Vector(art.atmosphere?.sunEuler??art.sunEuler))*new Vector3(0,0,1);
            if(direction.y>=-.15f)throw new InvalidOperationException("Sun too low for a bounded physical shade support.");
            var target=Hull(level.shade.SelectMany(c=>new[]{new Vector3(c[0]-level.width*.5f+.07f,0,c[1]-level.height*.5f+.07f),new Vector3(c[0]-level.width*.5f+.93f,0,c[1]-level.height*.5f+.07f),new Vector3(c[0]-level.width*.5f+.93f,0,c[1]-level.height*.5f+.93f),new Vector3(c[0]-level.width*.5f+.07f,0,c[1]-level.height*.5f+.93f)}));
            var models=art.models.ToDictionary(m=>m.id);var water=new RiverSurfaceField(art.landscape);
            var protectedBounds=new List<(string id,Vector3 lo,Vector3 hi)>();
            foreach(var pose in art.instances)
            {
                if(pose.modelId==null||!models.TryGetValue(pose.modelId,out var m)||m.seasonalRole=="groundcover"||m.seasonalRole=="flower"||m.vertices.Length==0)continue;
                var transform=Matrix4x4.TRS(new Vector3(pose.x,pose.y,pose.z),Quaternion.Euler(pose.rotationEuler==null?new Vector3(0,pose.yaw,0):CampArtDefinition.Vector(pose.rotationEuler)),pose.scaleXYZ==null?Vector3.one*pose.scale:CampArtDefinition.Vector(pose.scaleXYZ));
                var points=Enumerable.Range(0,m.vertices.Length/3).Select(i=>transform.MultiplyPoint3x4(new Vector3(m.vertices[i*3],m.vertices[i*3+1],m.vertices[i*3+2]))).ToArray();
                if(points.Max(v=>v.y)-points.Min(v=>v.y)<.3f)continue;
                protectedBounds.Add((pose.id,new Vector3(points.Min(v=>v.x),points.Min(v=>v.y),points.Min(v=>v.z)),new Vector3(points.Max(v=>v.x),points.Max(v=>v.y),points.Max(v=>v.z))));
            }
            List<Vector3> roof=null;string failure="No bounded support placement.";
            for(float height=minimumHeight;height<=maximumHeight+.001f;height+=.1f)
            {
                float h=height;var candidate=target.Select(p=>{float y=h+.10f*(p.z-target[0].z);return p-direction*(y/-direction.y);}).ToList();
                float minX=candidate.Min(p=>p.x),maxX=candidate.Max(p=>p.x),minZ=candidate.Min(p=>p.z),maxZ=candidate.Max(p=>p.z);
                if(!(maxX < -level.width*.5f-.35f || minX>level.width*.5f+.35f || maxZ < -level.height*.5f-.35f || minZ>level.height*.5f+.35f))
                {failure="Source sun needs a roof over the puzzle or supports taller than 4 m; use an explicitly selected existing building/tree.";continue;}
                string rejected=null;
                foreach(var p in candidate)
                {
                    float ground=art.landscape.SampleHeight(p.x,p.z);
                    if(p.x<art.landscape.originX+.2f||p.x>art.landscape.originX+art.landscape.size-.2f||p.z<art.landscape.originZ+.2f||p.z>art.landscape.originZ+art.landscape.size-.2f){rejected="Support leaves the authored terrain.";break;}
                    if(water.TrySample(p.x,p.z,out var wet)&&wet.Wet&&ground<wet.centre.y+.03f){rejected="A support foot lies in the actual frozen river; choose another caster.";break;}
                    if(Mathf.Abs(ground-art.landscape.SampleHeight(p.x+.15f,p.z))>.16f||Mathf.Abs(ground-art.landscape.SampleHeight(p.x,p.z+.15f))>.16f){rejected="Unstable slope at a support foot.";break;}
                    foreach(var b in protectedBounds)
                    {
                        if(p.x>b.lo.x-.20f&&p.x<b.hi.x+.20f&&p.z>b.lo.z-.20f&&p.z<b.hi.z+.20f&&b.hi.y>ground+.3f){rejected="Support would intersect protected object "+b.id;break;}
                        if(maxX>b.lo.x&&minX<b.hi.x&&maxZ>b.lo.z&&minZ<b.hi.z&&b.hi.y>candidate.Min(v=>v.y)-.12f&&b.lo.y<candidate.Max(v=>v.y)+.12f){rejected="Roof envelope intersects protected object "+b.id;break;}
                    }
                    if(rejected!=null)break;
                }
                if(rejected==null){roof=candidate;break;}failure=rejected;
            }
            if(roof==null)throw new InvalidOperationException(failure);
            var vertices=new List<float>();var normals=new List<float>();var colors=new List<int>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c,int color)
            {var n=Vector3.Cross(b-a,c-a).normalized;foreach(var p in new[]{a,b,c}){vertices.AddRange(new[]{p.x,p.y,p.z});normals.AddRange(new[]{n.x,n.y,n.z});colors.Add(color);}}
            void Box(Vector3 a,Vector3 b,float radius,int color)
            {
                var along=(b-a).normalized;var right=Vector3.Cross(along,Mathf.Abs(along.y)>.9f?Vector3.right:Vector3.up).normalized*radius;var up=Vector3.Cross(along,right).normalized*radius;
                var v=new[]{a-right-up,a+right-up,a+right+up,a-right+up,b-right-up,b+right-up,b+right+up,b-right+up};
                foreach(var f in new[]{new[]{0,3,2,1},new[]{4,5,6,7},new[]{0,1,5,4},new[]{1,2,6,5},new[]{2,3,7,6},new[]{3,0,4,7}}){Triangle(v[f[0]],v[f[1]],v[f[2]],color);Triangle(v[f[0]],v[f[2]],v[f[3]],color);}
            }
            // A restrained timber-and-canvas camp shelter; a real double-sided roof
            // and grounded frame, not a shadow-only plane or a UI decal.
            for(int i=1;i<roof.Count-1;i++){Triangle(roof[0],roof[i+1],roof[i],0xA2997C);Triangle(roof[0],roof[i],roof[i+1],0x8F876D);}
            for(int i=0;i<roof.Count;i++)
            {
                var p=roof[i];var next=roof[(i+1)%roof.Count];var foot=new Vector3(p.x,art.landscape.SampleHeight(p.x,p.z)-.035f,p.z);
                Box(foot,p,.055f,0x635038);Box(p,next,.043f,0x6D593D);
                // Knee braces explain the load path instead of unsupported corners.
                Box(p-Vector3.up*.45f,p+(next-p).normalized*.42f,.028f,0x635038);
            }
            var model=new CampArtDefinition.Model{id=id,sourceModelId="worldops_camp_shade_awning",seasonalRole="solid",shading="solid",vertices=vertices.ToArray(),normals=normals.ToArray(),colors=colors.ToArray(),
                authoringRecipe="{\"label\":\"Табірний тіньовий навіс із дерев’яними опорами\",\"generator\":\"WorldOps shade.support v1\"}"};
            art.models=art.models.Concat(new[]{model}).ToArray();art.instances=art.instances.Concat(new[]{new CampArtDefinition.Instance{id=id,modelId=id,scale=1}}).ToArray();
        }
    }
}
