// Deliberately minimal managed doubles for a CONTROL-FLOW audit.
// This file does not simulate Unity rendering, Yoga, stencil, shader, GPU timing,
// actual scene-generation cost, snow/mesh generation or native memory ownership.
using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Domain;
using QuietCamp.Application;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class RequireComponent : Attribute { public RequireComponent(Type type) { } }
    public class Object { public string name; public static void Destroy(Object value) { } }
    public class Component : Object
    {
        public GameObject gameObject;
        public RectTransform transform => gameObject.transform;
        public T GetComponent<T>() where T : class => gameObject.GetComponent<T>();
        public T GetComponentInParent<T>() where T : class
        { for (var at = transform; at != null; at = at.parent) if (at.gameObject.GetComponent<T>() is T found) return found; return null; }
    }
    public class MonoBehaviour : Component { public bool isActiveAndEnabled => gameObject.activeSelf; }
    public class GameObject : Object
    {
        readonly Dictionary<Type, object> _components = new();
        public RectTransform transform; public bool activeSelf = true;
        public static int Created;
        public GameObject(string name = "") { Created++; this.name = name; transform = new RectTransform { gameObject = this }; _components[typeof(RectTransform)] = transform; }
        public T AddComponent<T>() where T : Component, new() { var c = new T { gameObject = this }; _components[typeof(T)] = c; return c; }
        public T GetComponent<T>() where T : class => _components.Values.OfType<T>().FirstOrDefault();
        public void SetActive(bool active) => activeSelf = active;
    }
    public class RectTransform : Component
    {
        public Rect rect = new Rect(0, -14300, 850, 14300);
        public RectTransform parent; public Vector3 offset;
        public void SetParent(RectTransform value, bool world = false) => parent = value;
        public void SetAsLastSibling() { }
        public Vector3 InverseTransformPoint(Vector3 point) => point - offset;
        public void GetWorldCorners(Vector3[] corners)
        { corners[0] = new Vector3(rect.xMin, rect.yMin); corners[2] = new Vector3(rect.xMax, rect.yMax); }
    }
    public struct Vector2
    {
        public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new(0, 0); public static Vector2 one => new(1, 1);
        public static Vector2 up => new(0, 1); public static Vector2 down => new(0, -1); public static Vector2 left => new(-1, 0); public static Vector2 right => new(1, 0);
        public float magnitude => MathF.Sqrt(x*x+y*y); public Vector2 normalized => magnitude > 0 ? this/magnitude : zero;
        public static Vector2 operator +(Vector2 a, Vector2 b) => new(a.x+b.x,a.y+b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new(a.x-b.x,a.y-b.y);
        public static Vector2 operator *(Vector2 a, float b) => new(a.x*b,a.y*b);
        public static Vector2 operator /(Vector2 a, float b) => new(a.x/b,a.y/b);
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) => a+(b-a)*Mathf.Clamp01(t);
    }
    public struct Vector3
    {
        public float x, y, z; public Vector3(float x,float y,float z=0) { this.x=x;this.y=y;this.z=z; }
        public static Vector3 forward => new(0,0,1); public static Vector3 up => new(0,1);
        public static Vector3 operator +(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator -(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator *(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
        public static Vector3 operator /(Vector3 a,float b)=>new(a.x/b,a.y/b,a.z/b);
    }
    public struct Rect
    {
        public float x,y,width,height; public Rect(float x,float y,float width,float height) { this.x=x;this.y=y;this.width=width;this.height=height; }
        public float xMin=>x; public float yMin=>y; public float xMax=>x+width; public float yMax=>y+height; public Vector2 center=>new(x+width/2,y+height/2);
        public static Rect MinMaxRect(float a,float b,float c,float d)=>new(a,b,c-a,d-b);
        public static bool operator ==(Rect a,Rect b)=>a.x==b.x&&a.y==b.y&&a.width==b.width&&a.height==b.height;
        public static bool operator !=(Rect a,Rect b)=>!(a==b);
        public override bool Equals(object o)=>o is Rect r&&this==r;
        public override int GetHashCode()=>HashCode.Combine(x,y,width,height);
    }
    public struct Color
    {
        public float r,g,b,a; public Color(float r,float g,float b,float a=1) { this.r=r;this.g=g;this.b=b;this.a=a; }
        public static Color white=>new(1,1,1);
        public static Color operator *(Color c,float t)=>new(c.r*t,c.g*t,c.b*t,c.a*t);
        public static Color Lerp(Color c,Color d,float t) { t=Mathf.Clamp01(t);return new(c.r+(d.r-c.r)*t,c.g+(d.g-c.g)*t,c.b+(d.b-c.b)*t,c.a+(d.a-c.a)*t); }
    }
    public static class Mathf
    {
        public const float PI=MathF.PI; public static int Floors;
        public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b); public static int Clamp(int v,int a,int b)=>Math.Clamp(v,a,b);
        public static float Clamp01(float v)=>Clamp(v,0,1); public static float Sin(float v)=>MathF.Sin(v); public static float Cos(float v)=>MathF.Cos(v);
        public static float Abs(float v)=>MathF.Abs(v); public static int FloorToInt(float v) { Floors++;return (int)MathF.Floor(v); }
        public static int CeilToInt(float v)=>(int)MathF.Ceiling(v); public static float Floor(float v)=>MathF.Floor(v); public static float Ceil(float v)=>MathF.Ceiling(v);
        public static float Min(float a,float b)=>MathF.Min(a,b); public static int Min(int a,int b)=>Math.Min(a,b);
        public static float Max(float a,float b)=>MathF.Max(a,b); public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t);
        public static float InverseLerp(float a,float b,float v)=>a==b?0:Clamp01((v-a)/(b-a));
        public static float SmoothStep(float a,float b,float t) { t=Clamp01(t);return a+(b-a)*t*t*(3-2*t); }
        public static float Repeat(float v,float n)=>v-MathF.Floor(v/n)*n;
    }
    public static class Time { public static float unscaledTime,unscaledDeltaTime=1f/60; }
    public class Shader : Object { }
    public class Material : Object { public Material(Shader shader) { } public void SetFloat(string key,float value) { } }
    public static class Resources { public static T Load<T>(string key) where T : new()=>new T(); }
    public class CanvasRenderer : Component { public Mesh GetMesh()=>new(); }
    public class Mesh { public int vertexCount; }
    [Flags] public enum AdditionalCanvasShaderChannels { TexCoord1=1,TexCoord2=2 }
    public class Canvas { public AdditionalCanvasShaderChannels additionalShaderChannels; }
}
namespace UnityEngine.UI
{
    public class MaskableGraphic : UnityEngine.MonoBehaviour
    {
        public bool raycastTarget; public UnityEngine.Material material; public UnityEngine.Material materialForRendering=>material;
        public UnityEngine.RectTransform rectTransform=>transform;
        public UnityEngine.Canvas canvas=new(); public UnityEngine.CanvasRenderer canvasRenderer=new();
        public int Dirties; public void SetVerticesDirty()=>Dirties++;
        protected virtual void OnPopulateMesh(VertexHelper vh) { } protected virtual void OnDestroy() { }
    }
    public sealed class VertexHelper { public int currentVertCount; public void Clear()=>currentVertCount=0; }
    public class ScrollRect : UnityEngine.MonoBehaviour
    {
        public UnityEngine.RectTransform viewport,content;
        public float verticalNormalizedPosition;
        public Event onValueChanged=new();
        public class Event
        {
            event Action<UnityEngine.Vector2> Changed;
            public void AddListener(Action<UnityEngine.Vector2> value)=>Changed+=value;
            public void RemoveListener(Action<UnityEngine.Vector2> value)=>Changed-=value;
            public void Invoke(UnityEngine.Vector2 value)=>Changed?.Invoke(value);
        }
    }
}
namespace QuietCamp.Presentation
{
    public sealed class GameServices
    { public int EffectiveQuality=1;public bool ReducedMotion;public float LevelMapScroll=-1;public ProgressionService Progression=new(); }
}
namespace QuietCamp.Infrastructure
{
    public static class CampContent { public static LevelSummary[] Summaries=Array.Empty<LevelSummary>(); }
    public static class BonusCampCatalog { public static BonusCampDefinition[] Slots=Array.Empty<BonusCampDefinition>(); }
    public static class LevelLoader { public static string[] Ids;public static IReadOnlyList<string> MvpLevelIds()=>Ids; }
    public sealed class AtmosphereCatalog
    {
        public static AtmosphereCatalog Load()=>new();
        public class Profile { public bool Mist;public UnityEngine.Color Ambient=UnityEngine.Color.white,Sun=UnityEngine.Color.white; }
    }
    public static class CampWeatherTimeline
    { public readonly struct State { public readonly float Cloud,Rain; public State(float cloud,float rain) {Cloud=cloud;Rain=rain;} } }
}
namespace QuietCamp.Presentation.UI
{
    public static class QcUi
    { public static UnityEngine.RectTransform Stretch(UnityEngine.RectTransform parent,string name) { var r=new UnityEngine.GameObject(name).transform;r.SetParent(parent);r.rect=parent.rect;return r; } }
    public sealed class RoadmapModelLibrary { public static RoadmapModelLibrary Load()=>new(); }
    public static class RoadmapSceneGenerator
    {
        public static int Generated,Advanced,ThrowOnGenerate;
        public sealed class Prop { public string Asset;public UnityEngine.Vector3 Position;public float Height,Yaw;public bool Sway,Tent,Fire; }
        public sealed class Scene
        {
            public LevelSummary Level;public float VerticalExtent;public bool Night,Winter;
            public List<Prop> Props=new();public QuietCamp.Infrastructure.CampWeatherTimeline.State Weather;
            public QuietCamp.Infrastructure.AtmosphereCatalog.Profile Light=new();public UnityEngine.Vector3 Sun=UnityEngine.Vector3.up;
            public UnityEngine.Color Ground=new(.4f,.5f,.3f);public float Snowfall=>.5f;
            public PaletteData Palette=new();public class PaletteData { public UnityEngine.Color Fog=UnityEngine.Color.white; }
            public void Advance(float dt)=>Advanced++;public void SetMoment(float seconds) { }
            public UnityEngine.Color Tint(UnityEngine.Color c,UnityEngine.Vector3 normal)=>c;
        }
        public static Scene Generate(LevelSummary summary,QuietCamp.Infrastructure.AtmosphereCatalog catalog) { Generated++;if(Generated==ThrowOnGenerate)throw new InvalidOperationException("Injected scene failure");return new Scene { Level=summary }; }
    }
    public sealed class RoadmapPainter
    {
        public int TruncatedModels;public RoadmapPainter(RoadmapModelLibrary library) { }
        public void Shadow(UnityEngine.UI.VertexHelper v,RoadmapSceneGenerator.Scene s,RoadmapSceneGenerator.Prop p,UnityEngine.Vector2 c,float scale) { }
        public void Model(UnityEngine.UI.VertexHelper v,RoadmapSceneGenerator.Scene s,RoadmapSceneGenerator.Prop p,UnityEngine.Vector2 c,float scale) { }
        public static void Glade(UnityEngine.UI.VertexHelper v,RoadmapSceneGenerator.Scene s,UnityEngine.Vector2 c,float scale) { }
        public static UnityEngine.Vector2 Project(UnityEngine.Vector3 p,float scale)=>new((p.z-p.x)*.7071f*scale,((p.x+p.z)*.37f+p.y*.86f)*scale);
        public static UnityEngine.Color Clear(UnityEngine.Color c) { c.a=0;return c; }
        public static void Quad(UnityEngine.UI.VertexHelper v,UnityEngine.Vector2 a,UnityEngine.Vector2 b,UnityEngine.Vector2 c,UnityEngine.Vector2 d,UnityEngine.Color e,UnityEngine.Color f,UnityEngine.Color g,UnityEngine.Color h)=>v.currentVertCount+=4;
        public static void Ribbon(UnityEngine.UI.VertexHelper v,UnityEngine.Vector2 a,UnityEngine.Vector2 b,float w,UnityEngine.Color c)=>v.currentVertCount+=4;
        public static void Triangle(UnityEngine.UI.VertexHelper v,UnityEngine.Vector2 a,UnityEngine.Vector2 b,UnityEngine.Vector2 c,UnityEngine.Color d)=>v.currentVertCount+=3;
        public static void Ellipse(UnityEngine.UI.VertexHelper v,UnityEngine.Vector2 a,UnityEngine.Vector2 b,UnityEngine.Color c,UnityEngine.Color d,int sides=12)=>v.currentVertCount+=sides+2;
    }
}
