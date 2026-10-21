using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Derives physical well openings from actual annular rim geometry.
    /// Does not mutate published art, puzzles or height samples. Shared by runtime
    /// presentation, WorldOps and the license-free external geometry review.</summary>
    public static class WorldWellShafts
    {
        public sealed class Opening
        {
            public string ownerId;
            public Vector3[] rim;
            public float Depth = 2.4f;
            public bool Contains(float x,float z,float inset=0)
            {
                bool inside=false;float nearest=float.MaxValue;
                for(int i=0,j=rim.Length-1;i<rim.Length;j=i++)
                {
                    var a=rim[j];var b=rim[i];float dx=b.x-a.x,dz=b.z-a.z;
                    float t=Mathf.Clamp01(((x-a.x)*dx+(z-a.z)*dz)/Mathf.Max(1e-12f,dx*dx+dz*dz));
                    float ex=x-a.x-dx*t,ez=z-a.z-dz*t;nearest=Mathf.Min(nearest,Mathf.Sqrt(ex*ex+ez*ez));
                    if((a.z>z)!=(b.z>z)&&x<(b.x-a.x)*(z-a.z)/(b.z-a.z)+a.x)inside=!inside;
                }
                return inset>=0?(inside||nearest<1e-6f)&&nearest+1e-6f>=inset:inside||nearest<=-inset;
            }
        }
        static readonly ConditionalWeakTable<CampArtDefinition,Opening[]> Cache=new ConditionalWeakTable<CampArtDefinition,Opening[]>();
        public static void Invalidate(CampArtDefinition art){if(art!=null)Cache.Remove(art);}
        public static Opening[] Find(CampArtDefinition art)=>art==null?Array.Empty<Opening>():Cache.GetValue(art,Discover);
        public static bool Contains(CampArtDefinition art,float x,float z)=>Find(art).Any(h=>h.Contains(x,z));
        public static bool ExcludesGroundPlant(CampArtDefinition art,float x,float y,float z)
            =>Find(art).Any(h=>h.Contains(x,z,-.08f)&&y<h.rim.Min(p=>p.y)-.15f);
        static float Area(IReadOnlyList<Vector3> p)
        {float a=0;for(int i=0;i<p.Count;i++){var b=p[(i+1)%p.Count];a+=p[i].x*b.z-b.x*p[i].z;}return a*.5f;}
        static string Key(Vector3 p)=>Mathf.RoundToInt(p.x*10000)+":"+Mathf.RoundToInt(p.y*10000)+":"+Mathf.RoundToInt(p.z*10000);
        static Opening[] Discover(CampArtDefinition art)
        {
            var result=new List<Opening>();var models=art.models.ToDictionary(m=>m.id);
            foreach(var pose in art.instances)
            {
                if(pose.modelId==null||!models.TryGetValue(pose.modelId,out var model))continue;
                string name=model.sourceModelId??model.id;
                if(name!="ua_well_sweep"&&name!="ua_valley_well"&&name!="qc_campaign_landmark_009"&&name!="qc_campaign_landmark_063"&&name!="qc_campaign_landmark_078")continue;
                // Top faces of an annular rim have an outer and inner closed loop.
                // Detect the largest enclosed loop; never guess from the pole/bucket AABB.
                var groups=new Dictionary<int,List<Vector3[]>>();
                for(int i=0;i<model.vertices.Length;i+=9)
                {
                    var p=new Vector3[3];for(int k=0;k<3;k++)p[k]=new Vector3(model.vertices[i+k*3],model.vertices[i+k*3+1],model.vertices[i+k*3+2]);
                    if(Mathf.Abs(p[0].y-p[1].y)>.002f||Mathf.Abs(p[0].y-p[2].y)>.002f||p[0].y<.25f||p[0].y>1.8f)continue;
                    if(Mathf.Abs(Vector3.Cross(p[1]-p[0],p[2]-p[0]).normalized.y)<.9f)continue;
                    int plane=Mathf.RoundToInt(p[0].y*1000);if(!groups.TryGetValue(plane,out var list))groups[plane]=list=new List<Vector3[]>();list.Add(p);
                }
                Vector3[] best=null;float bestArea=0;
                foreach(var group in groups.Values)
                {
                    var points=new Dictionary<string,Vector3>();var edges=new Dictionary<(string,string),int>();
                    foreach(var t in group)for(int i=0;i<3;i++)
                    {
                        string a=Key(t[i]),b=Key(t[(i+1)%3]);points[a]=t[i];points[b]=t[(i+1)%3];
                        var edge=string.CompareOrdinal(a,b)<0?(a,b):(b,a);edges.TryGetValue(edge,out int count);edges[edge]=count+1;
                    }
                    var graph=new Dictionary<string,List<string>>();
                    foreach(var edge in edges.Where(e=>e.Value==1).Select(e=>e.Key))
                    {
                        if(!graph.TryGetValue(edge.Item1,out var a))graph[edge.Item1]=a=new List<string>();a.Add(edge.Item2);
                        if(!graph.TryGetValue(edge.Item2,out var b))graph[edge.Item2]=b=new List<string>();b.Add(edge.Item1);
                    }
                    var loops=new List<Vector3[]>();var visited=new HashSet<string>();
                    foreach(var start in graph.Keys)
                    {
                        if(visited.Contains(start)||graph[start].Count!=2)continue;
                        var loop=new List<Vector3>();string at=start,previous=null;bool closed=false;
                        for(int k=0;k<graph.Count+1;k++)
                        {
                            if(visited.Contains(at)){closed=at==start;break;}
                            if(graph[at].Count!=2)break;visited.Add(at);loop.Add(points[at]);
                            string next=graph[at][0]==previous?graph[at][1]:graph[at][0];previous=at;at=next;
                        }
                        if(closed&&loop.Count>=3)loops.Add(loop.ToArray());
                    }
                    foreach(var loop in loops)
                    {
                        float area=Mathf.Abs(Area(loop));if(area<.06f||area>4||area<=bestArea)continue;
                        var centroid=loop.Aggregate(Vector3.zero,(a,b)=>a+b)/loop.Length;
                        bool enclosed=loops.Any(other=>other!=loop&&Mathf.Abs(Area(other))>area+.01f&&new Opening{rim=other}.Contains(centroid.x,centroid.z));
                        if(enclosed){best=loop;bestArea=area;}
                    }
                }
                if(best==null&&name.StartsWith("qc_campaign_landmark_",StringComparison.Ordinal))
                    foreach(var group in groups.OrderByDescending(g=>g.Key))
                    {best=VisibleInnerContour(group.Value);if(best!=null)break;}
                if(best==null)continue;
                var matrix=Matrix4x4.TRS(new Vector3(pose.x,pose.y,pose.z),Quaternion.Euler(pose.rotationEuler==null?new Vector3(0,pose.yaw,0):CampArtDefinition.Vector(pose.rotationEuler)),pose.scaleXYZ==null?Vector3.one*pose.scale:CampArtDefinition.Vector(pose.scaleXYZ));
                var rim=best.Select(matrix.MultiplyPoint3x4).ToArray();if(Area(rim)<0)Array.Reverse(rim);
                result.Add(new Opening{ownerId=pose.id,rim=rim});
            }
            return result.ToArray();
        }
        static Vector3[] VisibleInnerContour(List<Vector3[]> faces)
        {
            float Cross2(Vector3 a,Vector3 b)=>a.x*b.z-a.z*b.x;
            bool ContainsOrigin(Vector3[] t)
            {float a=Cross2(t[0],t[1]),b=Cross2(t[1],t[2]),c=Cross2(t[2],t[0]);return a>=-1e-7f&&b>=-1e-7f&&c>=-1e-7f||a<=1e-7f&&b<=1e-7f&&c<=1e-7f;}
            if(faces.Any(ContainsOrigin))return null;
            var angles=faces.SelectMany(t=>t).SelectMany(p=>new[]{Math.Atan2(p.z,p.x)-.00002,Math.Atan2(p.z,p.x),Math.Atan2(p.z,p.x)+.00002})
                .Concat(Enumerable.Range(0,64).Select(i=>i*Math.PI*2/64-Math.PI)).Distinct().OrderBy(a=>a).ToArray();
            var points=new List<Vector3>();int missing=0;
            foreach(double angle in angles)
            {
                var direction=new Vector3((float)Math.Cos(angle),0,(float)Math.Sin(angle));float nearest=float.MaxValue;
                foreach(var t in faces)for(int i=0;i<3;i++)
                {
                    var a=t[i];var edge=t[(i+1)%3]-a;float den=Cross2(direction,edge);if(Mathf.Abs(den)<1e-8f)continue;
                    float distance=Cross2(a,edge)/den,u=Cross2(a,direction)/den;
                    if(distance>.12f&&u>=-1e-5f&&u<=1.00001f)nearest=Mathf.Min(nearest,distance);
                }
                if(nearest>1.9f){missing++;continue;}
                points.Add(new Vector3(direction.x*nearest,faces[0][0].y,direction.z*nearest));
            }
            // Small chipped joints may expose the same shaft. A missing sector
            // larger than 5% is not accepted as an enclosed well rim.
            if(missing>angles.Length*.05f||points.Count<4)return null;
            bool changed=true;
            while(changed&&points.Count>3)
            {
                changed=false;
                for(int i=0;i<points.Count;i++)
                {
                    var a=points[(i+points.Count-1)%points.Count];var b=points[i];var c=points[(i+1)%points.Count];
                    float distance=(b-a).magnitude;
                    float cross=Mathf.Abs((b.x-a.x)*(c.z-a.z)-(b.z-a.z)*(c.x-a.x));
                    if(distance<.00015f||cross<.000025f&&(c-a).magnitude<1.5f){points.RemoveAt(i);changed=true;break;}
                }
            }
            if(points.Count>128||Mathf.Abs(Area(points))<.06f||Mathf.Abs(Area(points))>4)return null;
            return points.ToArray();
        }
        public static FrozenLandscape Resolve(CampArtDefinition art,FrozenLandscape source)
        {
            var holes=Find(art);if(source==null||holes.Length==0)return source;
            var meshes=new List<LandscapeMesh>();
            foreach(var mesh in source.meshes)meshes.Add(mesh.water||mesh.decoration?mesh:Cut(mesh,holes));
            foreach(var hole in holes)if(!meshes.Any(m=>m.name=="Well shaft — "+hole.ownerId))meshes.Add(Shaft(hole,source));
            return new FrozenLandscape{resolution=source.resolution,originX=source.originX,originZ=source.originZ,size=source.size,heights=source.heights,
                riverEnvironmentVersion=source.riverEnvironmentVersion,riverContacts=source.riverContacts,meshes=meshes.ToArray()};
        }
        // Every interpolated vertex retains all original UV/lightmap/flow/tangent
        // channels. Convex subtraction preserves terrain outside the actual opening.
        public static LandscapeMesh Cut(LandscapeMesh source,IReadOnlyList<Opening> holes)
        {
            float meshX0=float.MaxValue,meshX1=float.MinValue,meshZ0=float.MaxValue,meshZ1=float.MinValue;
            for(int i=0;i<source.vertices.Length;i+=3){meshX0=Mathf.Min(meshX0,source.vertices[i]);meshX1=Mathf.Max(meshX1,source.vertices[i]);meshZ0=Mathf.Min(meshZ0,source.vertices[i+2]);meshZ1=Mathf.Max(meshZ1,source.vertices[i+2]);}
            if(!holes.Any(h=>meshX1>=h.rim.Min(p=>p.x)&&meshX0<=h.rim.Max(p=>p.x)&&meshZ1>=h.rim.Min(p=>p.z)&&meshZ0<=h.rim.Max(p=>p.z)))return source;
            var convex=new List<Opening>();
            foreach(var hole in holes)
            {
                bool isConvex=true;for(int i=0;i<hole.rim.Length;i++){var a=hole.rim[i];var b=hole.rim[(i+1)%hole.rim.Length];var c=hole.rim[(i+2)%hole.rim.Length];if((b.x-a.x)*(c.z-b.z)-(b.z-a.z)*(c.x-b.x)<-1e-6f){isConvex=false;break;}}
                if(isConvex){convex.Add(hole);continue;}
                var centre=hole.rim.Aggregate(Vector3.zero,(a,b)=>a+b)/hole.rim.Length;
                if(!hole.Contains(centre.x,centre.z))throw new InvalidOperationException("Well opening is not a verified star-shaped cavity.");
                for(int i=0;i<hole.rim.Length;i++)
                {
                    var triangle=new[]{centre,hole.rim[i],hole.rim[(i+1)%hole.rim.Length]};var origin=triangle.Aggregate(Vector3.zero,(a,b)=>a+b)/3;
                    // Overlap fan seams by 0.2 mm so float quantization cannot
                    // leave a visible terrain spoke at the shared centre.
                    for(int k=0;k<3;k++){var offset=triangle[k]-origin;offset.y=0;triangle[k]+=offset.normalized*.0002f;}
                    convex.Add(new Opening{ownerId=hole.ownerId,rim=triangle});
                }
            }
            holes=convex;
            int count=source.vertices.Length/3;
            var channels=new[]{source.vertices,source.normals,source.uv,source.colors,source.lightmapUv,source.flow,source.tangents};
            var widths=new[]{3,3,2,4,source.lightmapUv?.Length==count*2?2:0,source.flow?.Length==count*3?3:0,source.tangents?.Length==count*4?4:0};
            int total=widths.Sum();float[] Vertex(int i){var v=new float[total];int offset=0;for(int k=0;k<widths.Length;k++){if(widths[k]>0)Array.Copy(channels[k],i*widths[k],v,offset,widths[k]);offset+=widths[k];}return v;}
            var data=channels.Select((c,k)=>widths[k]>0?new List<float>(c):null).ToArray();
            var triangles=new List<int>(source.triangles.Length);
            var bounds=holes.Select(h=>new[]{h.rim.Min(p=>p.x),h.rim.Max(p=>p.x),h.rim.Min(p=>p.z),h.rim.Max(p=>p.z)}).ToArray();
            bool changed=false;
            for(int t=0;t<source.triangles.Length;t+=3)
            {
                int a=source.triangles[t],b=source.triangles[t+1],c=source.triangles[t+2];
                float x0=Mathf.Min(source.vertices[a*3],source.vertices[b*3],source.vertices[c*3]),x1=Mathf.Max(source.vertices[a*3],source.vertices[b*3],source.vertices[c*3]);
                float z0=Mathf.Min(source.vertices[a*3+2],source.vertices[b*3+2],source.vertices[c*3+2]),z1=Mathf.Max(source.vertices[a*3+2],source.vertices[b*3+2],source.vertices[c*3+2]);
                var nearby=Enumerable.Range(0,holes.Count).Where(i=>x1>=bounds[i][0]&&x0<=bounds[i][1]&&z1>=bounds[i][2]&&z0<=bounds[i][3]).ToArray();
                if(nearby.Length==0){triangles.Add(a);triangles.Add(b);triangles.Add(c);continue;}
                var pieces=new List<List<float[]>>{new List<float[]>{Vertex(a),Vertex(b),Vertex(c)}};
                foreach(int index in nearby)
                {
                    var hole=holes[index];var next=new List<List<float[]>>();
                    foreach(var piece in pieces)
                    {
                        var inside=piece;
                        for(int edge=0;edge<hole.rim.Length&&inside.Count>=3;edge++)
                        {
                            var p0=hole.rim[edge];var p1=hole.rim[(edge+1)%hole.rim.Length];
                            float tolerance=1e-6f*Mathf.Sqrt((p1.x-p0.x)*(p1.x-p0.x)+(p1.z-p0.z)*(p1.z-p0.z));
                            float Side(float[] v){float value=(p1.x-p0.x)*(v[2]-p0.z)-(p1.z-p0.z)*(v[0]-p0.x);return Mathf.Abs(value)<=tolerance?0:value;}
                            var keep=new List<float[]>();var outside=new List<float[]>();
                            for(int i=0;i<inside.Count;i++)
                            {
                                var p=inside[i];var q=inside[(i+1)%inside.Count];float dp=Side(p),dq=Side(q);
                                // A point on the cutting edge belongs to both half-planes.
                                if(dp>=0)keep.Add(p);if(dp<=0)outside.Add(p);
                                if((dp>0&&dq<0)||(dp<0&&dq>0))
                                {float f=dp/(dp-dq);var v=new float[total];for(int k=0;k<total;k++)v[k]=p[k]+(q[k]-p[k])*f;keep.Add(v);outside.Add(v);}
                            }
                            if(outside.Count>=3)next.Add(outside);inside=keep;
                        }
                    }
                    pieces=next;
                }
                float originalArea=Mathf.Abs((source.vertices[b*3]-source.vertices[a*3])*(source.vertices[c*3+2]-source.vertices[a*3+2])-(source.vertices[c*3]-source.vertices[a*3])*(source.vertices[b*3+2]-source.vertices[a*3+2]));
                float remainingArea=0;
                foreach(var piece in pieces)for(int i=1;i<piece.Count-1;i++)remainingArea+=Mathf.Abs((piece[i][0]-piece[0][0])*(piece[i+1][2]-piece[0][2])-(piece[i+1][0]-piece[0][0])*(piece[i][2]-piece[0][2]));
                if(Mathf.Abs(originalArea-remainingArea)<=Mathf.Max(1e-6f,originalArea*1e-5f)){triangles.Add(a);triangles.Add(b);triangles.Add(c);continue;}
                changed=true;
                foreach(var piece in pieces)for(int i=1;i<piece.Count-1;i++)
                {
                    var v0=piece[0];var v1=piece[i];var v2=piece[i+1];
                    if(Mathf.Abs((v1[0]-v0[0])*(v2[2]-v0[2])-(v2[0]-v0[0])*(v1[2]-v0[2]))<1e-8f)continue;
                    foreach(var v in new[]{v0,v1,v2})
                    {
                        triangles.Add(data[0].Count/3);int offset=0;
                        for(int k=0;k<widths.Length;k++){for(int n=0;n<widths[k];n++)data[k].Add(v[offset+n]);offset+=widths[k];}
                    }
                }
            }
            if(!changed)return source;
            var result=data.Select(v=>v?.ToArray()).ToArray();
            return new LandscapeMesh{name=source.name,water=source.water,decoration=source.decoration,vertices=result[0],normals=result[1],uv=result[2],colors=result[3],lightmapUv=result[4],flow=result[5],tangents=result[6],triangles=triangles.ToArray()};
        }
        static LandscapeMesh Shaft(Opening hole,FrozenLandscape terrain)
        {
            var v=new List<float>();var n=new List<float>();var uv=new List<float>();var colors=new List<float>();
            void Triangle(Vector3 a,Vector3 b,Vector3 c)
            {var normal=Vector3.Cross(b-a,c-a).normalized;foreach(var p in new[]{a,b,c}){v.AddRange(new[]{p.x,p.y,p.z});n.AddRange(new[]{normal.x,normal.y,normal.z});uv.AddRange(new[]{0f,0f});// Alpha is the CampArtLit weather exposure: underground walls/floor catch no snow.
                colors.AddRange(new[]{.025f,.031f,.028f,0f});}}
            var top=hole.rim.Select(p=>new Vector3(p.x,terrain.SampleHeight(p.x,p.z)+.015f,p.z)).ToArray();
            float bottom=top.Min(p=>p.y)-hole.Depth;var centre=top.Aggregate(Vector3.zero,(a,b)=>a+b)/top.Length;centre.y=bottom;
            for(int i=0;i<top.Length;i++)
            {var a=top[i];var b=top[(i+1)%top.Length];var c=new Vector3(a.x,bottom,a.z);var d=new Vector3(b.x,bottom,b.z);Triangle(a,d,b);Triangle(a,c,d);Triangle(centre,d,c);}
            return new LandscapeMesh{name="Well shaft — "+hole.ownerId,decoration=true,vertices=v.ToArray(),normals=n.ToArray(),uv=uv.ToArray(),colors=colors.ToArray(),triangles=Enumerable.Range(0,v.Count/3).ToArray()};
        }
    }
}
