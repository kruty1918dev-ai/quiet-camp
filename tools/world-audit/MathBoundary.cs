// Standalone numeric boundary. No Unity binary, Editor, graphics API or licensing service.
// Engine-only operations throw. This executable only validates DTOs and resolves
// the real project's deterministic seasonal appearance with managed arithmetic.
using System;
using System.Numerics;
namespace UnityEngine {
 public static class Mathf {
  public const float PI=(float)Math.PI;
  public static float InverseLerp(float a,float b,float v)=>a==b?0:Clamp01((v-a)/(b-a));
  public static float PerlinNoise(float x,float y)=>throw new NotSupportedException("Native noise not used by seasonal colours/geometry");
  public const float Deg2Rad=(float)(Math.PI/180);
  public static float Sign(float x)=>x>=0?1:-1;
  public static float Abs(float x)=>Math.Abs(x);
  public static float Min(params float[] v)=>System.Linq.Enumerable.Min(v);
  public static int Min(int a,int b)=>Math.Min(a,b);
  public static float Max(params float[] v)=>System.Linq.Enumerable.Max(v);
  public static int Max(int a,int b)=>Math.Max(a,b);
  public static float Clamp(float x,float a,float b)=>Math.Clamp(x,a,b);
  public static int Clamp(int x,int a,int b)=>Math.Clamp(x,a,b);
  public static float Clamp01(float x)=>Math.Clamp(x,0,1);
  public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
  public static float Sqrt(float x)=>(float)Math.Sqrt(x);
  public static float Sin(float x)=>(float)Math.Sin(x);
  public static float Cos(float x)=>(float)Math.Cos(x);
  public static int FloorToInt(float x)=>(int)Math.Floor(x);
  public static int RoundToInt(float x)=>(int)Math.Round(x,MidpointRounding.ToEven);
  public static float SmoothStep(float a,float b,float t){t=Clamp01(t);return a+(b-a)*t*t*(3-2*t);}
 }
 public struct Vector2 { public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;} public static Vector2 zero=>new();public float sqrMagnitude=>x*x+y*y;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);public Vector2 normalized=>magnitude>0?this/magnitude:new();public static Vector2 Lerp(Vector2 a,Vector2 b,float t)=>a+(b-a)*Mathf.Clamp01(t);public static float Dot(Vector2 a,Vector2 b)=>a.x*b.x+a.y*b.y;public static Vector2 operator +(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y);public static Vector2 operator -(Vector2 a,Vector2 b)=>new(a.x-b.x,a.y-b.y);public static Vector2 operator *(Vector2 a,float s)=>new(a.x*s,a.y*s);public static Vector2 operator /(Vector2 a,float s)=>new(a.x/s,a.y/s); }
 public readonly record struct Vector2Int(int x,int y);

 public struct Vector3 {
  public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
  public static Vector3 zero=>new();public static Vector3 right=>new(1,0,0);public static Vector3 down=>new(0,-1,0);public void Normalize(){this=normalized;}public static Vector3 Lerp(Vector3 a,Vector3 b,float t)=>a+(b-a)*Mathf.Clamp01(t);
  public static Vector3 one=>new(1,1,1);public static Vector3 up=>new(0,1,0);
  public static float Distance(Vector3 a,Vector3 b)=>(a-b).magnitude;public float magnitude=>(float)Math.Sqrt(sqrMagnitude);
  public float sqrMagnitude=>x*x+y*y+z*z;
  public Vector3 normalized=>sqrMagnitude>0?this/(float)Math.Sqrt(sqrMagnitude):new();
  public static Vector3 Scale(Vector3 a,Vector3 b)=>new(a.x*b.x,a.y*b.y,a.z*b.z);
  public static Vector3 Cross(Vector3 a,Vector3 b)=>new(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x);
  public static float Dot(Vector3 a,Vector3 b)=>a.x*b.x+a.y*b.y+a.z*b.z;
  public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
  public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
  public static Vector3 operator -(Vector3 a)=>new(-a.x,-a.y,-a.z);
  public static Vector3 operator *(Vector3 a,float v)=>new(a.x*v,a.y*v,a.z*v);
  public static Vector3 operator /(Vector3 a,float v)=>a*(1/v);
 }
 public readonly record struct Vector3Int(int x,int y,int z);
 public struct Vector4 {public float x,y,z,w; public Vector4(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}}
 public struct Quaternion {
  internal System.Numerics.Quaternion q;
  public static Quaternion Euler(Vector3 e)=>Euler(e.x,e.y,e.z);
  public static Quaternion Euler(float x,float y,float z)=>new(){q=System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitY,y*Mathf.Deg2Rad)*System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitX,x*Mathf.Deg2Rad)*System.Numerics.Quaternion.CreateFromAxisAngle(System.Numerics.Vector3.UnitZ,z*Mathf.Deg2Rad)};
  public static Vector3 operator *(Quaternion r,Vector3 v){var a=System.Numerics.Vector3.Transform(new(v.x,v.y,v.z),r.q);return new(a.X,a.Y,a.Z);}
 }
 public struct Matrix4x4 {public Matrix4x4 inverse=>throw new NotSupportedException();public Matrix4x4 transpose=>throw new NotSupportedException();public Vector3 MultiplyVector(Vector3 v)=>throw new NotSupportedException();public static Matrix4x4 operator *(Matrix4x4 a,Matrix4x4 b)=>throw new NotSupportedException();
 Vector3 p,s;Quaternion q;public static Matrix4x4 TRS(Vector3 p,Quaternion q,Vector3 s)=>new(){p=p,q=q,s=s};public Vector3 MultiplyPoint3x4(Vector3 v)=>p+q*Vector3.Scale(v,s);}
 public struct Color {
  public float r,g,b,a;public Color(float r,float g,float b,float a=1){this.r=r;this.g=g;this.b=b;this.a=a;}
  static float Linear(float x)=>x<=.04045f?x/12.92f:(float)Math.Pow((x+.055f)/1.055f,2.4);
  public static Color white=>new(1,1,1);
  public static Color operator *(Color a,float b)=>new(a.r*b,a.g*b,a.b*b,a.a);
  public static Color operator *(Color a,Color b)=>new(a.r*b.r,a.g*b.g,a.b*b.b,a.a*b.a);
  public Color linear=>new(Linear(r),Linear(g),Linear(b),a);
  public static Color Lerp(Color x,Color y,float t){t=Mathf.Clamp01(t);return new(x.r+(y.r-x.r)*t,x.g+(y.g-x.g)*t,x.b+(y.b-x.b)*t,x.a+(y.a-x.a)*t);}
 }
 public struct Bounds {
  public Vector3 center,size;public Bounds(Vector3 c,Vector3 s){center=c;size=s;}
  public Vector3 extents=>size*.5f;public Vector3 min=>center-extents;public Vector3 max=>center+extents;
  public void Encapsulate(Vector3 p){var a=min;var b=max;var lo=new Vector3(Math.Min(a.x,p.x),Math.Min(a.y,p.y),Math.Min(a.z,p.z));var hi=new Vector3(Math.Max(b.x,p.x),Math.Max(b.y,p.y),Math.Max(b.z,p.z));center=(lo+hi)*.5f;size=hi-lo;}
 }
 public class Transform {public Matrix4x4 worldToLocalMatrix=>throw new NotSupportedException();public Matrix4x4 localToWorldMatrix=>throw new NotSupportedException();}
 public class GameObject {public Transform transform=>throw new NotSupportedException();public T[] GetComponentsInChildren<T>(bool b=false)=>throw new NotSupportedException();}
 public class MeshFilter {public Mesh sharedMesh=>throw new NotSupportedException();public Transform transform=>throw new NotSupportedException();public T GetComponent<T>()=>throw new NotSupportedException();}
 public class Renderer {public Material[] sharedMaterials=>throw new NotSupportedException();}
 public class Material {public bool HasProperty(string s)=>throw new NotSupportedException();public Color GetColor(string s)=>throw new NotSupportedException();}
 public class TextAsset {public string text;}
 public static class Resources {public static T Load<T>(string p)=>throw new NotSupportedException("Engine Resources is not used in this standalone export.");}
 public enum ColorSpace {Linear,Gamma}
 public static class QualitySettings {public static ColorSpace activeColorSpace=>ColorSpace.Linear;}
 public class Mesh {
  public bool isReadable=>throw new NotSupportedException();public int subMeshCount=>throw new NotSupportedException();public int[] GetTriangles(int i)=>throw new NotSupportedException();
  public string name;public Rendering.IndexFormat indexFormat;public Vector3[] vertices,normals;public Vector2[] uv,uv2;public Color[] colors;public int[] triangles;public Vector4[] tangents;
  public void SetUVs(int n,object v)=>throw new NotSupportedException();
  public void RecalculateTangents()=>throw new NotSupportedException();
  public void RecalculateBounds()=>throw new NotSupportedException();
 }
}
namespace UnityEngine.Rendering {public enum IndexFormat {UInt32}}
namespace Kruty1918.ScenePlacement.Composition {
 public class CompositionReview {} public class CompositionAttempt {}
 public static class RiverGeometry {
  public static float Centre(float a,float b,float c)=>throw new NotSupportedException("Unbaked terrain unsupported");
  public static float Width(float a,float b,float c)=>throw new NotSupportedException("Unbaked terrain unsupported");
  public static float Bed(float a,float b,float c,float d)=>throw new NotSupportedException("Unbaked terrain unsupported");
 }
}
namespace QuietCamp.Infrastructure {public static class CampCompositionGenerator {public static CampArtDefinition Generate(QuietCamp.Domain.LevelData l,string p,int s,out object plan){plan=null;throw new NotSupportedException("Unbaked profiles require authoring; no substitute geometry is invented.");}}}

namespace QuietCamp.Presentation.World {
 public static class CampTrail {public static float CorridorDistance(QuietCamp.Domain.LevelData l,UnityEngine.Vector3 v)=>throw new NotSupportedException();}
 public static class CampGroundDetails {public static float HeatAt(QuietCamp.Domain.LevelData l,UnityEngine.Vector3 v)=>throw new NotSupportedException();}
 public static class ShorelineGeometry {public static float DistanceToBank(object s,UnityEngine.Vector3 v)=>throw new NotSupportedException();}
}
