using System;
using System.Collections.Generic;
using System.Linq;

namespace Kruty1918.ScenePlacement
{
    public sealed class RoutePath
    {
        public readonly V3[] points;readonly float[] distances;public float Length=>distances[distances.Length-1];
        public RoutePath(V3[] points)
        {
            if(points==null||points.Length<2)throw new ArgumentException("Route needs two points");this.points=(V3[])points.Clone();distances=new float[points.Length];
            for(int i=0;i<points.Length;i++){if(!points[i].Finite)throw new ArgumentException("Nonfinite route point");if(i>0){float d=(points[i]-points[i-1]).Length;if(d<1e-5f)throw new ArgumentException("Repeated route point");distances[i]=distances[i-1]+d;}}
        }
        public V3 At(float distance,out V3 tangent)
        {
            distance=Numbers.Clamp(distance,0,Length);int segment=Array.BinarySearch(distances,distance);if(segment<0)segment=~segment-1;segment=Math.Min(points.Length-2,Math.Max(0,segment));tangent=(points[segment+1]-points[segment]).Normalized;
            return V3.Lerp(points[segment],points[segment+1],(distance-distances[segment])/(distances[segment+1]-distances[segment]));
        }
        public float Distance(V3 target)
        {
            float best=float.MaxValue;for(int i=1;i<points.Length;i++){var delta=points[i]-points[i-1];float t=Numbers.Clamp(V3.Dot(target-points[i-1],delta)/delta.LengthSquared,0,1);best=Math.Min(best,(target-points[i-1]-delta*t).Length);}return best;
        }
    }
    public sealed class GeometryBuilder
    {
        readonly List<V3> vertices=new List<V3>(),normals=new List<V3>();readonly List<V2> uv=new List<V2>();readonly List<Tint> colors=new List<Tint>();readonly List<int> indices=new List<int>(),materials=new List<int>();
        readonly SortedDictionary<string,(int components,List<float> values)> channels=new SortedDictionary<string,(int,List<float>)>(StringComparer.Ordinal);
        readonly List<MeshPart> parts=new List<MeshPart>();
        public int VertexCount=>vertices.Count;public int TriangleCount=>indices.Count/3;
        public void Triangle(V3 a,V3 b,V3 c,Tint tint,int material=0,V2? ua=null,V2? ub=null,V2? uc=null)
        {int start=vertices.Count;var n=V3.Cross(b-a,c-a).Normalized;vertices.AddRange(new[]{a,b,c});normals.AddRange(new[]{n,n,n});colors.AddRange(new[]{tint,tint,tint});uv.AddRange(new[]{ua??default,ub??default,uc??default});indices.AddRange(new[]{start,start+1,start+2});materials.Add(material);foreach(var channel in channels.Values)channel.values.AddRange(new float[3*channel.components]);}
        public void Quad(V3 a,V3 b,V3 c,V3 d,Tint tint,int material=0)
        {Triangle(a,b,c,tint,material,new V2(0,0),new V2(1,0),new V2(1,1));Triangle(a,c,d,tint,material,new V2(0,0),new V2(1,1),new V2(0,1));}
        public void Box(V3 center,V3 size,Tint tint,Pose? transform=null,int material=0)
        {
            var h=size*.5f;var p=transform??Pose.At(V3.Zero);V3 P(float x,float y,float z)=>p.Point(center+new V3(x*h.x,y*h.y,z*h.z));
            Quad(P(-1,-1,-1),P(-1,1,-1),P(1,1,-1),P(1,-1,-1),tint,material);
            Quad(P(1,-1,1),P(1,1,1),P(-1,1,1),P(-1,-1,1),tint,material);
            Quad(P(-1,-1,1),P(-1,1,1),P(-1,1,-1),P(-1,-1,-1),tint,material);
            Quad(P(1,-1,-1),P(1,1,-1),P(1,1,1),P(1,-1,1),tint,material);
            Quad(P(-1,1,-1),P(-1,1,1),P(1,1,1),P(1,1,-1),tint,material);
            Quad(P(-1,-1,1),P(-1,-1,-1),P(1,-1,-1),P(1,-1,1),tint,material);
        }
        public void Append(MeshData mesh,Pose pose,string partPrefix=null)
        {
            mesh.Validate();int start=vertices.Count,firstTriangle=TriangleCount;
            partPrefix=partPrefix??("append-"+firstTriangle+"/");
            foreach(var channel in mesh.channels)if(channels.TryGetValue(channel.name,out var existing)&&existing.components!=channel.components)throw new ArgumentException("Incompatible channel: "+channel.name);
            foreach(var part in mesh.parts)if(parts.Any(p=>p.id==partPrefix+part.id))throw new ArgumentException("Duplicate appended part: "+part.id);
            foreach(var channel in mesh.channels)if(!channels.ContainsKey(channel.name))channels.Add(channel.name,(channel.components,new List<float>(new float[start*channel.components])));
            for(int i=0;i<mesh.positions.Length;i++){vertices.Add(pose.Point(mesh.positions[i]));normals.Add(mesh.normals.Length>0?pose.Normal(mesh.normals[i]):V3.Up);colors.Add(mesh.colors.Length>0?mesh.colors[i]:Tint.White);uv.Add(mesh.uv.Length>0?mesh.uv[i]:default);}
            foreach(int index in mesh.indices)indices.Add(start+index);for(int i=0;i<mesh.TriangleCount;i++)materials.Add(mesh.materials.Length>0?mesh.materials[i]:0);
            foreach(var pair in channels){var channel=mesh.channels.FirstOrDefault(c=>c.name==pair.Key);pair.Value.values.AddRange(channel?.values??new float[mesh.positions.Length*pair.Value.components]);}
            foreach(var part in mesh.parts){var copy=part.Copy();copy.id=partPrefix+part.id;copy.triangles=copy.triangles.Select(t=>t+firstTriangle).ToArray();parts.Add(copy);}
        }
        public MeshData Build()=>new MeshData{positions=vertices.ToArray(),normals=normals.ToArray(),uv=uv.ToArray(),colors=colors.ToArray(),indices=indices.ToArray(),materials=materials.ToArray(),channels=channels.Select(c=>new VertexChannel{name=c.Key,components=c.Value.components,values=c.Value.values.ToArray()}).ToArray(),parts=parts.Select(p=>p.Copy()).ToArray()};
    }
}
