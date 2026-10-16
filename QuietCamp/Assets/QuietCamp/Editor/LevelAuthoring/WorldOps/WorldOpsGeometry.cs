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
        // A support point is a low vertex over its local ground, not just the world AABB minimum.
        public static float Clearance(LevelAuthoringDocument d,CampArtDefinition.Instance p,CampArtDefinition.Model m)
        {return Vertices(p,m).Min(v=>v.y-d.art.GroundHeight(v.x,v.z));}
        public static void Ground(LevelAuthoringDocument d,CampArtDefinition.Instance p,CampArtDefinition.Model m,WorldObjectRecord record)
        {
            if(record.pinned||record.support!="grounded")return;
            p.y-=Clearance(d,p,m)+Mathf.Min(record.maximumBurial,.03f);
            p.surfaceAttached=true;p.surfaceOffset=p.y-d.art.GroundHeight(p.x,p.z);
        }
        public static List<ValidationFinding> Validate(LevelAuthoringDocument d,WorldCriteria criteria)
        {
            var findings=new List<ValidationFinding>();string id=d.sourceLevelId??d.level.id;
            void Add(string code,string obj,string message,float measured=0,float limit=0,string repair=null)
                =>findings.Add(new ValidationFinding{code=code,levelId=id,objectId=obj,message=message,measured=measured,limit=limit,repair=repair});
            foreach(var issue in d.Validate(true))Add("document.invalid",null,issue);
            var models=d.art.models.ToDictionary(m=>m.id);var water=new RiverSurfaceField(d.art.landscape);
            foreach(var p in d.art.instances)
            {
                if(!models.TryGetValue(p.modelId??"",out var model))continue;
                var role=WorldOpsOperations.Role(d.art,p,model);var record=WorldOpsOperations.Record(d,p.id);
                if(role=="vegetation"||role=="tree")
                {
                    if(water.TrySample(p.x,p.z,out var sample)&&sample.Wet&&record?.role!="aquatic")Add("vegetation.water",p.id,"Plant root intersects the authored water surface.",repair:WorldOpsOperations.Generated(p.id)?"vegetation.redistribute":null);
                    float delta=p.y-d.art.GroundHeight(p.x,p.z)-p.surfaceOffset;
                    if(p.surfaceAttached==true&&Mathf.Abs(delta)>criteria.contactTolerance)Add("vegetation.surface",p.id,"Attached root lost contact with the terrain.",Mathf.Abs(delta),criteria.contactTolerance,"building.ground");
                    continue;
                }
                if(p.id.StartsWith("worldops/path/",StringComparison.Ordinal))continue;
                // Unknown manual support relationships are exceptions, never guessed automatic moves.
                float gap=Clearance(d,p,model);
                if(record?.support=="grounded"||record==null)
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
