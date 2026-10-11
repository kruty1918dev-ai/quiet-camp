using System;
using System.Collections.Generic;

namespace Kruty1918.ScenePlacement
{
    [Serializable] public struct V2
    {
        public float x,y;
        public V2(float x,float y){this.x=x;this.y=y;}
        public static V2 Lerp(V2 a,V2 b,float t)=>new V2(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t);
    }
    [Serializable] public struct V3
    {
        public float x,y,z;
        public V3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static V3 Zero=>default; public static V3 Up=>new V3(0,1,0);
        public float LengthSquared=>x*x+y*y+z*z;
        public float Length=>(float)Math.Sqrt(LengthSquared);
        public V3 Normalized=>Length>1e-7f?this/Length:Zero;
        public bool Finite=>Numbers.Finite(x)&&Numbers.Finite(y)&&Numbers.Finite(z);
        public static V3 operator +(V3 a,V3 b)=>new V3(a.x+b.x,a.y+b.y,a.z+b.z);
        public static V3 operator -(V3 a,V3 b)=>new V3(a.x-b.x,a.y-b.y,a.z-b.z);
        public static V3 operator -(V3 a)=>a*-1;
        public static V3 operator *(V3 a,float b)=>new V3(a.x*b,a.y*b,a.z*b);
        public static V3 operator /(V3 a,float b)=>a*(1/b);
        public static float Dot(V3 a,V3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
        public static V3 Cross(V3 a,V3 b)=>new V3(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
        public static V3 Lerp(V3 a,V3 b,float t)=>a+(b-a)*t;
        public override string ToString()=>"("+x+","+y+","+z+")";
    }
    [Serializable] public struct Tint
    {
        public float r,g,b,a;
        public Tint(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
        public static Tint White=>new Tint(1,1,1);
        public static Tint Lerp(Tint x,Tint y,float t)=>new Tint(x.r+(y.r-x.r)*t,x.g+(y.g-x.g)*t,x.b+(y.b-x.b)*t,x.a+(y.a-x.a)*t);
    }
    public static class Numbers
    {
        public const float Radians=.017453292519943295f;
        public static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        public static float Clamp(float f,float lo,float hi)=>Math.Max(lo,Math.Min(hi,f));
        public static float Smooth(float f){f=Clamp(f,0,1);return f*f*(3-2*f);}
        public static float Unit(string id,int seed,int salt=0)
        {
            unchecked{uint h=(uint)seed^(uint)salt*0x85ebca6b;foreach(char c in id??"")h=(h^c)*16777619;
                h^=h>>16;h*=0x7feb352d;h^=h>>15;h*=0x846ca68b;h^=h>>16;return(h&0xffffff)/16777216f;}
        }
    }
    [Serializable] public struct Pose
    {
        public V3 position,right,up,forward,scale;
        public static Pose At(V3 position,float yaw=0,V3? scale=null,V3? normal=null)
        {
            var u=(normal??V3.Up).Normalized;float angle=yaw*Numbers.Radians;
            var s=scale??new V3(1,1,1);
            if(!position.Finite||!Numbers.Finite(yaw)||u.LengthSquared<.9f||!s.Finite||s.x<=0||s.y<=0||s.z<=0)throw new ArgumentException("Finite positive pose dimensions and nonzero up required");
            var f=new V3((float)Math.Sin(angle),0,(float)Math.Cos(angle));f=(f-u*V3.Dot(f,u)).Normalized;
            if(f.LengthSquared<.1f)f=V3.Cross(Math.Abs(u.x)>.9f?V3.Up:new V3(1,0,0),u).Normalized;
            var r=V3.Cross(u,f).Normalized;
            return new Pose{position=position,right=r,up=u,forward=f,scale=s};
        }
        public V3 Direction(V3 p)=>right*p.x+up*p.y+forward*p.z;
        public V3 Point(V3 p)=>position+Direction(new V3(p.x*scale.x,p.y*scale.y,p.z*scale.z));
        public V3 Normal(V3 n)=>Direction(new V3(n.x/scale.x,n.y/scale.y,n.z/scale.z)).Normalized;
    }
    [Serializable] public struct Box
    {
        public V3 center,half;
        public Box(V3 center,V3 half){this.center=center;this.half=half;}
        public Box Expanded(float amount)=>new Box(center,half+new V3(amount,amount,amount));
        public bool Valid=>center.Finite&&half.Finite&&half.x>=0&&half.y>=0&&half.z>=0;
        public static Box FromPoints(IReadOnlyList<V3> points)
        {
            if(points==null||points.Count==0)throw new ArgumentException("Nonempty geometry required");
            V3 lo=points[0],hi=lo;foreach(var p in points){lo=new V3(Math.Min(lo.x,p.x),Math.Min(lo.y,p.y),Math.Min(lo.z,p.z));hi=new V3(Math.Max(hi.x,p.x),Math.Max(hi.y,p.y),Math.Max(hi.z,p.z));}
            return new Box((lo+hi)*.5f,(hi-lo)*.5f);
        }
    }
    [Serializable] public struct OrientedBox
    {
        public V3 center,x,y,z,half;
        public OrientedBox(Box local,Pose pose)
        {center=pose.Point(local.center);x=pose.right;y=pose.up;z=pose.forward;half=new V3(local.half.x*Math.Abs(pose.scale.x),local.half.y*Math.Abs(pose.scale.y),local.half.z*Math.Abs(pose.scale.z));}
        public Box Bounds
        {
            get{var h=new V3(Math.Abs(x.x)*half.x+Math.Abs(y.x)*half.y+Math.Abs(z.x)*half.z,Math.Abs(x.y)*half.x+Math.Abs(y.y)*half.y+Math.Abs(z.y)*half.z,Math.Abs(x.z)*half.x+Math.Abs(y.z)*half.y+Math.Abs(z.z)*half.z);return new Box(center,h);}
        }
        public bool Intersects(OrientedBox b,float clearance=0)
        {
            var local=this;var aa=new[]{x,y,z};var ba=new[]{b.x,b.y,b.z};var delta=b.center-center;
            bool Separated(V3 axis)
            {
                if(axis.LengthSquared<1e-10f)return false;axis=axis.Normalized;
                float ar=Math.Abs(V3.Dot(axis,local.x))*local.half.x+Math.Abs(V3.Dot(axis,local.y))*local.half.y+Math.Abs(V3.Dot(axis,local.z))*local.half.z;
                float br=Math.Abs(V3.Dot(axis,b.x))*b.half.x+Math.Abs(V3.Dot(axis,b.y))*b.half.y+Math.Abs(V3.Dot(axis,b.z))*b.half.z;
                return Math.Abs(V3.Dot(delta,axis))>=ar+br+clearance-1e-5f;
            }
            foreach(var axis in aa)if(Separated(axis))return false;
            foreach(var axis in ba)if(Separated(axis))return false;
            foreach(var a in aa)foreach(var other in ba)if(Separated(V3.Cross(a,other)))return false;
            return true;
        }
    }
    [Serializable] public sealed class VertexChannel
    {
        public string name;public int components;public float[] values=Array.Empty<float>();
        public VertexChannel Copy()=>new VertexChannel{name=name,components=components,values=(float[])values.Clone()};
    }
    [Serializable] public sealed class MeshPart
    {
        public string id,role,materialKind;public bool preserve;public int[] triangles=Array.Empty<int>();
        public MeshPart Copy()=>new MeshPart{id=id,role=role,materialKind=materialKind,preserve=preserve,triangles=(int[])triangles.Clone()};
    }
    [Serializable] public sealed class MeshData
    {
        public V3[] positions=Array.Empty<V3>(),normals=Array.Empty<V3>();
        public V2[] uv=Array.Empty<V2>();public Tint[] colors=Array.Empty<Tint>();
        public int[] indices=Array.Empty<int>(),materials=Array.Empty<int>();
        public VertexChannel[] channels=Array.Empty<VertexChannel>();public MeshPart[] parts=Array.Empty<MeshPart>();
        public int TriangleCount=>indices.Length/3;
        public Box Bounds=>Box.FromPoints(positions);
        public void Validate()
        {
            if(positions==null||positions.Length==0||indices==null||indices.Length==0||indices.Length%3!=0)throw new ArgumentException("Nonempty triangle mesh required");
            if(normals==null||uv==null||colors==null||materials==null||channels==null||parts==null)throw new ArgumentException("Mesh attributes must use empty arrays rather than null");
            foreach(var p in positions)if(!p.Finite)throw new ArgumentException("Nonfinite vertex");
            foreach(var i in indices)if(i<0||i>=positions.Length)throw new ArgumentException("Index outside vertices");
            if(normals.Length!=0&&normals.Length!=positions.Length||uv.Length!=0&&uv.Length!=positions.Length||colors.Length!=0&&colors.Length!=positions.Length||materials.Length!=0&&materials.Length!=TriangleCount)throw new ArgumentException("Attribute cardinality");
            foreach(var n in normals)if(!n.Finite)throw new ArgumentException("Nonfinite normal");
            foreach(var p in uv)if(!Numbers.Finite(p.x)||!Numbers.Finite(p.y))throw new ArgumentException("Nonfinite UV");
            foreach(var c in colors)if(!Numbers.Finite(c.r)||!Numbers.Finite(c.g)||!Numbers.Finite(c.b)||!Numbers.Finite(c.a))throw new ArgumentException("Nonfinite color");
            foreach(int m in materials)if(m<0)throw new ArgumentException("Negative material slot");
            var names=new HashSet<string>(StringComparer.Ordinal);
            foreach(var c in channels){if(c==null||string.IsNullOrWhiteSpace(c.name)||!names.Add(c.name)||c.components<1||c.components>4||c.values==null||c.values.Length!=positions.Length*c.components)throw new ArgumentException("Unique channels and matching cardinality required");foreach(float f in c.values)if(!Numbers.Finite(f))throw new ArgumentException("Nonfinite custom channel");}
            names.Clear();foreach(var part in parts){if(part==null||string.IsNullOrWhiteSpace(part.id)||!names.Add(part.id)||part.triangles==null)throw new ArgumentException("Unique part IDs required");foreach(int t in part.triangles)if(t<0||t>=TriangleCount)throw new ArgumentException("Part triangle outside mesh");}
        }
        public MeshData Copy()
        {
            var c=new MeshData{positions=(V3[])positions.Clone(),normals=(V3[])normals.Clone(),uv=(V2[])uv.Clone(),colors=(Tint[])colors.Clone(),indices=(int[])indices.Clone(),materials=(int[])materials.Clone(),channels=new VertexChannel[channels.Length],parts=new MeshPart[parts.Length]};
            for(int i=0;i<channels.Length;i++)c.channels[i]=channels[i].Copy();for(int i=0;i<parts.Length;i++)c.parts[i]=parts[i].Copy();return c;
        }
        public void RecalculateNormals()
        {
            normals=new V3[positions.Length];for(int t=0;t<indices.Length;t+=3){int a=indices[t],b=indices[t+1],c=indices[t+2];var n=V3.Cross(positions[b]-positions[a],positions[c]-positions[a]);normals[a]+=n;normals[b]+=n;normals[c]+=n;}
            for(int i=0;i<normals.Length;i++)normals[i]=normals[i].Normalized;
        }
    }
}
