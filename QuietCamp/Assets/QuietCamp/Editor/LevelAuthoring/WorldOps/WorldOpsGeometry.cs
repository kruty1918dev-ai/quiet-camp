using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring.WorldOps
{
    // All distances use transformed source vertices, never renderer-count heuristics.
    public static class WorldOpsGeometry
    {
        public static Matrix4x4 Matrix(CampArtDefinition.Instance p)=>Matrix4x4.TRS(new Vector3(p.x,p.y,p.z),
            Quaternion.Euler(p.rotationEuler==null?new Vector3(0,p.yaw,0):CampArtDefinition.Vector(p.rotationEuler)),
            p.scaleXYZ==null?Vector3.one*p.scale:CampArtDefinition.Vector(p.scaleXYZ));
        public static Vector3[] Vertices(CampArtDefinition.Instance p,CampArtDefinition.Model m)
        {
            var matrix=Matrix(p);var result=new Vector3[m.vertices.Length/3];
            for(int i=0;i<result.Length;i++)result[i]=matrix.MultiplyPoint3x4(new Vector3(m.vertices[i*3],m.vertices[i*3+1],m.vertices[i*3+2]));
            return result;
        }
        public static Bounds Bounds(CampArtDefinition.Instance p,CampArtDefinition.Model m)
        {var vs=Vertices(p,m);var bounds=new Bounds(vs[0],Vector3.zero);foreach(var v in vs)bounds.Encapsulate(v);return bounds;}
        public static bool Protected(LevelAuthoringDocument d,float x,float z)=>Mathf.Abs(x)<d.level.width*.5f+.2f&&Mathf.Abs(z)<d.level.height*.5f+.2f;
        public static bool ProtectedPlant(LevelAuthoringDocument d,float x,float z)
        {
            if(Protected(d,x,z))return true;var point=new Vector2(x,z);
            foreach(var path in d.worldOps?.paths??new List<WorldPath>())
                for(int k=1;k<(path.points?.Length??0);k++)
                {
                    var a=new Vector2(path.points[k-1][0],path.points[k-1][1]);var b=new Vector2(path.points[k][0],path.points[k][1]);var delta=b-a;
                    var nearest=a+delta*Mathf.Clamp01(Vector2.Dot(point-a,delta)/Mathf.Max(.0001f,delta.sqrMagnitude));
                    if(Vector2.Distance(point,nearest)<path.width*.5f+.15f)return true;
                }
            return false;
        }
        // A support point is a low vertex over its local ground, not just the world AABB minimum.
        public static float Clearance(LevelAuthoringDocument d,CampArtDefinition.Instance p,CampArtDefinition.Model m)
        {return Vertices(p,m).Min(v=>v.y-d.art.GroundHeight(v.x,v.z));}
        public static void Ground(LevelAuthoringDocument d,CampArtDefinition.Instance p,CampArtDefinition.Model m,WorldObjectRecord record)
        {
            if(record.pinned)return;
            float clearance=SupportClearance(d,p,m,record);
            if(float.IsInfinity(clearance))throw new InvalidOperationException("The declared physical support does not reach this object.");
            p.y-=clearance+Mathf.Min(record.maximumBurial,.03f);
            p.surfaceAttached=record.support=="grounded";p.surfaceOffset=p.y-d.art.GroundHeight(p.x,p.z);
        }
        static float SupportClearance(LevelAuthoringDocument d,CampArtDefinition.Instance p,CampArtDefinition.Model m,WorldObjectRecord record)
        {
            if(record==null||record.support=="grounded")return Clearance(d,p,m);
            var vertices=Vertices(p,m);float gap=float.PositiveInfinity;
            if(record.support=="water")
            {
                var field=new RiverSurfaceField(d.art.landscape);
                foreach(var v in vertices)if(field.TrySample(v.x,v.z,out var sample)&&sample.Wet)gap=Mathf.Min(gap,v.y-sample.centre.y);
                return gap;
            }
            if(!record.support.StartsWith("object:",StringComparison.Ordinal))throw new InvalidOperationException("Unknown physical support role: "+record.support);
            string id=record.support.Substring(7);var target=d.art.instances.SingleOrDefault(i=>i.id==id&&i.id!=p.id);
            if(target==null)return gap;var targetModel=d.art.models.Single(model=>model.id==target.modelId);var triangles=Vertices(target,targetModel);float top=triangles.Max(v=>v.y)+1;
            foreach(var v in vertices)
            {
                var origin=new Vector3(v.x,top,v.z);float hit=float.PositiveInfinity;
                for(int k=0;k+2<triangles.Length;k+=3)if(TriangleHit(origin,Vector3.down,triangles[k],triangles[k+1],triangles[k+2],out float distance))hit=Mathf.Min(hit,distance);
                if(!float.IsInfinity(hit))gap=Mathf.Min(gap,v.y-(top-hit));
            }
            return gap;
        }
        static bool TriangleHit(Vector3 origin,Vector3 direction,Vector3 a,Vector3 b,Vector3 c,out float distance)
        {
            distance=0;var e1=b-a;var e2=c-a;var cross=Vector3.Cross(direction,e2);float determinant=Vector3.Dot(e1,cross);
            if(Mathf.Abs(determinant)<.000001f)return false;
            float inverse=1/determinant;var t=origin-a;float u=Vector3.Dot(t,cross)*inverse;if(u<0||u>1)return false;
            var q=Vector3.Cross(t,e1);float v=Vector3.Dot(direction,q)*inverse;if(v<0||u+v>1)return false;
            distance=Vector3.Dot(e2,q)*inverse;return distance>.0001f;
        }
        static bool Inside(Vector3 point,Vector3[] triangles)
        {
            var direction=new Vector3(.137f,1,.071f).normalized;
            foreach(float sign in new[]{-1f,1f})
            {
                var hits=new List<float>();
                for(int i=0;i+2<triangles.Length;i+=3)if(TriangleHit(point,direction*sign,triangles[i],triangles[i+1],triangles[i+2],out float hit)&&!hits.Any(h=>Mathf.Abs(h-hit)<.001f))hits.Add(hit);
                if(hits.Count%2!=1)return false;
            }
            return true;
        }
        public static List<ValidationFinding> Validate(LevelAuthoringDocument d,WorldCriteria criteria)
        {
            var findings=new List<ValidationFinding>();string id=d.sourceLevelId??d.level.id;
            void Add(string code,string obj,string message,float measured=0,float limit=0,string repair=null)
                =>findings.Add(new ValidationFinding{code=code,levelId=id,objectId=obj,message=message,measured=measured,limit=limit,repair=repair});
            foreach(var issue in d.Validate(true))Add("document.invalid",null,issue);
            if(d.art.primaryLandmarkId!=null)
                try{CampaignLandmarkCatalog.Validate(d.art,d.level.order);}catch(Exception e){Add("landmark.identity",d.art.primaryLandmarkInstanceId,e.Message);}
            var models=d.art.models.ToDictionary(m=>m.id);var water=new RiverSurfaceField(d.art.landscape);
            var records=(d.worldOps?.objects??new List<WorldObjectRecord>()).ToDictionary(r=>r.id);
            var solids=d.art.instances.Where(p=>p.modelId!=null&&models.TryGetValue(p.modelId,out var m)&&m.shading!="foliage"&&WorldOpsOperations.Role(d.art,p,m)!="tree"&&!p.id.StartsWith("worldops/path/",StringComparison.Ordinal))
                .Select(p=>new{p,bounds=Bounds(p,models[p.modelId]),vertices=Vertices(p,models[p.modelId])}).Where(s=>s.bounds.size.y>.7f).ToArray();
            foreach(var p in d.art.instances)
            {
                if(!models.TryGetValue(p.modelId??"",out var model))continue;
                var role=WorldOpsOperations.Role(d.art,p,model);records.TryGetValue(p.id,out var record);
                if(role=="vegetation"||role=="tree")
                {
                    if(water.TrySample(p.x,p.z,out var sample)&&sample.Wet&&p.y<=sample.centre.y+criteria.contactTolerance&&record?.role!="aquatic")Add("vegetation.water",p.id,"Plant root intersects the authored water surface.",repair:WorldOpsOperations.Generated(p.id)&&!p.id.StartsWith("overgrowth/",StringComparison.Ordinal)?"vegetation.redistribute":null);
                    float delta=p.y-d.art.GroundHeight(p.x,p.z)-p.surfaceOffset;
                    if(p.surfaceAttached==true&&Mathf.Abs(delta)>criteria.contactTolerance)Add("vegetation.surface",p.id,"Attached root lost contact with the terrain.",Mathf.Abs(delta),criteria.contactTolerance,"building.ground");
                    if(p.surfaceAttached==false&&!p.id.StartsWith("overgrowth/",StringComparison.Ordinal)&&(record==null||record.support=="grounded"))
                    {float rootGap=Clearance(d,p,model);if(rootGap>criteria.contactTolerance)Add("vegetation.floating",p.id,"The plant has no declared support above the ground.",rootGap,criteria.contactTolerance);}
                    if(!Protected(d,p.x,p.z)&&ProtectedPlant(d,p.x,p.z))Add("vegetation.path",p.id,"A plant root blocks a protected path.",repair:WorldOpsOperations.Generated(p.id)?"vegetation.redistribute":null);
                    if(!p.id.StartsWith("overgrowth/",StringComparison.Ordinal))
                    {
                        var root=new Vector3(p.x,p.y+.12f,p.z);
                        if(solids.Any(s=>s.bounds.Contains(root)&&Inside(root,s.vertices)))Add("vegetation.solid",p.id,"The plant root lies inside solid prop geometry.",repair:WorldOpsOperations.Generated(p.id)?"vegetation.redistribute":null);
                    }
                    continue;
                }
                if(p.id.StartsWith("worldops/path/",StringComparison.Ordinal))continue;
                // Unknown manual support relationships are exceptions, never guessed automatic moves.
                float gap;
                try{gap=SupportClearance(d,p,model,record);}catch(Exception e){Add("support.role",p.id,e.Message);continue;}
                if(float.IsInfinity(gap)){Add("support.missing",p.id,"The explicit support is missing or does not reach any source vertex.");continue;}
                {
                    if(gap>criteria.contactTolerance)Add("support.floating",p.id,"No source vertex reaches the ground; declare an explicit supported role or ground it.",gap,criteria.contactTolerance,record!=null&&!record.pinned?"building.ground":null);
                    if(record!=null&&gap < -record.maximumBurial-criteria.contactTolerance)Add("support.buried",p.id,"Geometry exceeds its explicitly permitted burial.",-gap,record.maximumBurial,record.pinned?null:"building.ground");
                }
            }
            foreach(var path in d.worldOps?.paths??new List<WorldPath>())
            {
                for(int k=1;k<(path.points?.Length??0);k++)
                {
                    var a=path.points[k-1];var b=path.points[k];float length=Vector2.Distance(new Vector2(a[0],a[1]),new Vector2(b[0],b[1]));
                    if(length<.01f){Add("path.disconnected",path.id,"Consecutive path points coincide.");continue;}
                    int steps=Mathf.CeilToInt(length/.2f);float previous=d.art.GroundHeight(a[0],a[1]);
                    for(int j=1;j<=steps;j++)
                    {
                        var at=Vector2.Lerp(new Vector2(a[0],a[1]),new Vector2(b[0],b[1]),j/(float)steps);float h=d.art.GroundHeight(at.x,at.y);
                        if(Mathf.Abs(h-previous)/(length/steps)>path.maximumSlope)Add("path.slope",path.id,"Path exceeds its permitted slope.");
                        if(water.TrySample(at.x,at.y,out var sample)&&sample.Wet)Add("path.water",path.id,"Path crosses water without an explicit bridge.");previous=h;
                    }
                }
                foreach(var endpoint in new[]{(path.startObjectId,0),(path.endObjectId,(path.points?.Length??1)-1)})
                {
                    if(string.IsNullOrEmpty(endpoint.Item1))continue;
                    var target=d.art.instances.FirstOrDefault(p=>p.id==endpoint.Item1);
                    if(target==null||path.points==null||endpoint.Item2<0||endpoint.Item2>=path.points.Length){Add("path.disconnected",path.id,"The bound entrance object is missing.");continue;}
                    var point=path.points[endpoint.Item2];float distance=Vector2.Distance(new Vector2(target.x,target.z),new Vector2(point[0],point[1]));
                    if(distance>path.width)Add("path.disconnected",path.id,"The path endpoint does not reach its bound entrance.",distance,path.width);
                }
            }
            return findings.GroupBy(f=>f.code+"/"+f.objectId).Select(g=>g.First()).ToList();
        }
    }
}
