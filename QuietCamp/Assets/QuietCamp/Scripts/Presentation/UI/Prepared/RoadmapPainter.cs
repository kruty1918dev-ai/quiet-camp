using System.Collections.Generic;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.UI;

namespace QuietCamp.Presentation.UI.Prepared
{
    /// <summary>Projects real low-poly mesh faces and their ground shadows into UI.
    /// No screen-sized textures or shadow maps. All lighting is local to a glade.</summary>
    internal sealed class RoadmapPainter
    {
        // Eye direction is the cross product of the two projection rows below.
        // Culling must use this angle; a steeper eye would expose back faces.
        public static readonly Vector3 View=Vector3.Cross(new Vector3(-.7071f,0,.7071f),new Vector3(.37f,.86f,.37f)).normalized;
        readonly RoadmapModelLibrary _library;
        readonly List<Vector2> _points=new List<Vector2>();
        readonly List<Vector2> _hull=new List<Vector2>();
        public int TruncatedModels { get; set; }
        public RoadmapPainter(RoadmapModelLibrary library) { _library=library; }
        public static Vector2 Project(Vector3 v,float scale)=>new Vector2((v.z-v.x)*.7071f,(v.x+v.z)*.37f+v.y*.86f)*scale;
        public static Color Clear(Color c) { c.a=0;return c; }
        public void Shadow(VertexHelper vh,RoadmapSceneGenerator.Scene scene,RoadmapSceneGenerator.Prop prop,Vector2 centre,float scale,int first=0,int last=int.MaxValue)
        {
            var model=prop.Geometry??_library.Get(prop.Asset);if(model==null||prop.Height<.45f)return;
            _points.Clear();var rotation=Quaternion.Euler(0,prop.Yaw,0);
            var sun=scene.Sun;float y=scene.ShadowSunHeight;
            if(prop.Geometry!=null&&prop.Geometry.id.EndsWith(":bare"))
            {
                // Bare trees cast branch shadows, never a solid convex crown silhouette.
                float snow=scene.Winter?scene.SnowDepth(prop.Position):0;
                var shade=new Color(.11f,.14f,.20f,(scene.Night?.08f:.23f)*(1-.65f*scene.Weather.Cloud));
                for(int i=first;i<Mathf.Min(last,model.Positions.Length);i+=3)
                {
                    if(Vector3.Dot(rotation*model.Normals[i],sun)<=0)continue;
                    var a=rotation*Scaled(prop.LeafVertex(model.Positions[i],model.Colors[i/3]),prop)+prop.Position;
                    var b=rotation*Scaled(prop.LeafVertex(model.Positions[i+1],model.Colors[i/3]),prop)+prop.Position;
                    var c=rotation*Scaled(prop.LeafVertex(model.Positions[i+2],model.Colors[i/3]),prop)+prop.Position;
                    if(a.y<snow&&b.y<snow&&c.y<snow)continue;
                    Vector2 Ground(Vector3 p)
                    {float height=Mathf.Max(0,p.y-snow);p.x-=height*sun.x/y;p.z-=height*sun.z/y;p.y=snow+.003f;return centre+Project(p,scale);}
                    if(vh.currentVertCount>55000){TruncatedModels++;return;}
                    int branchStart=vh.currentVertCount;vh.AddVert(Ground(a),shade,Vector2.zero);vh.AddVert(Ground(b),shade,Vector2.zero);vh.AddVert(Ground(c),shade,Vector2.zero);vh.AddTriangle(branchStart,branchStart+1,branchStart+2);
                }
                return;
            }
            if(prop.ShadowHullCount==0)
            {
                foreach(var source in model.Positions)
                {
                    var p=rotation*Scaled(source,prop)+prop.Position;
                    p.x-=p.y*sun.x/y;p.z-=p.y*sun.z/y;p.y=0;
                    _points.Add(Project(p,1));
                }
                _points.Sort((a,b)=>a.x==b.x?a.y.CompareTo(b.y):a.x.CompareTo(b.x));
                _hull.Clear();
                foreach(var p in _points)
                { while(_hull.Count>=2&&Cross(_hull[_hull.Count-1]-_hull[_hull.Count-2],p-_hull[_hull.Count-1])<=0)_hull.RemoveAt(_hull.Count-1);_hull.Add(p); }
                int lower=_hull.Count;
                for(int i=_points.Count-2;i>=0;i--)
                { var p=_points[i];while(_hull.Count>lower&&Cross(_hull[_hull.Count-1]-_hull[_hull.Count-2],p-_hull[_hull.Count-1])<=0)_hull.RemoveAt(_hull.Count-1);_hull.Add(p); }
                if(_hull.Count<4)return;_hull.RemoveAt(_hull.Count-1);
                if(prop.ShadowHull==null||prop.ShadowHull.Length<_hull.Count)prop.ShadowHull=new Vector2[Mathf.NextPowerOfTwo(_hull.Count)];
                _hull.CopyTo(prop.ShadowHull);prop.ShadowHullCount=_hull.Count;
            }
            _hull.Clear();for(int i=0;i<prop.ShadowHullCount;i++)_hull.Add(centre+prop.ShadowHull[i]*scale);
            Vector2 middle=Vector2.zero;foreach(var p in _hull)middle+=p;middle/=_hull.Count;
            var tint=new Color(.095f,.18f,.145f,(scene.Night?.13f:.30f)*(1-.65f*scene.Weather.Cloud));
            int start=vh.currentVertCount;vh.AddVert(middle,tint,Vector2.zero);
            foreach(var p in _hull)vh.AddVert(p,tint,Vector2.zero);
            for(int i=0;i<_hull.Count;i++)
            {
                int next=(i+1)%_hull.Count;vh.AddTriangle(start,start+1+i,start+1+next);
                var a=_hull[i];var b=_hull[next];
                Quad(vh,a,b,b+(b-middle).normalized*4,a+(a-middle).normalized*4,tint,tint,Clear(tint),Clear(tint));
            }
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        public void Model(VertexHelper vh,RoadmapSceneGenerator.Scene scene,RoadmapSceneGenerator.Prop prop,Vector2 centre,float scale,int first=0,int last=int.MaxValue)
        {
            var model=prop.Geometry??_library.Get(prop.Asset);if(model==null)return;
            var rotation=Quaternion.Euler(0,prop.Yaw,0);
            float snow=scene.Winter&&prop.Sway?scene.SnowDepth(prop.Position):0;
            for(int i=first;i<Mathf.Min(last,model.Positions.Length);i+=3)
            {
                var n=model.Normals[i];
                var normal=rotation*new Vector3(n.x/prop.Stretch.x,n.y,n.z/prop.Stretch.y).normalized;
                if(Vector3.Dot(normal,View)<-.01f)continue;
                if(vh.currentVertCount>55000) { TruncatedModels++;return; }
                var a=rotation*Scaled(prop.LeafVertex(model.Positions[i],model.Colors[i/3]),prop)+prop.Position;
                var b=rotation*Scaled(prop.LeafVertex(model.Positions[i+1],model.Colors[i/3]),prop)+prop.Position;
                var c=rotation*Scaled(prop.LeafVertex(model.Positions[i+2],model.Colors[i/3]),prop)+prop.Position;
                if(scene.Winter&&prop.Sway)
                {
                    if(a.y<snow&&b.y<snow&&c.y<snow)continue;
                    // Snow-buried roots terminate at the local surface instead of hanging over it.
                    a.y=Mathf.Max(a.y,snow);b.y=Mathf.Max(b.y,snow);c.y=Mathf.Max(c.y,snow);
                }
                var color=scene.Tint(scene.SurfaceColor(model.Colors[i/3],prop,normal),normal,.88f+.12f*Mathf.Clamp01((a.y+b.y+c.y)/prop.Height));
                int start=vh.currentVertCount;
                void Vertex(Vector3 p,Vector3 source)
                {
                    var v=UIVertex.simpleVert;v.position=centre+Project(p,scale);v.color=color;
                    var root=centre+Project(prop.Position,scale);
                    v.uv1=new Vector4(root.x,root.y,0,0);
                    v.uv2=new Vector4(prop.Sway?Mathf.SmoothStep(0,1,source.y):0,0,0,0);
                    vh.AddVert(v);
                }
                Vertex(a,model.Positions[i]);Vertex(b,model.Positions[i+1]);Vertex(c,model.Positions[i+2]);
                vh.AddTriangle(start,start+1,start+2);
            }
        }
        static Vector3 Scaled(Vector3 p,RoadmapSceneGenerator.Prop prop)
            =>new Vector3(p.x*prop.Stretch.x,p.y,p.z*prop.Stretch.y)*prop.Height;
        public static void Glade(VertexHelper vh,RoadmapSceneGenerator.Scene scene,Vector2 centre,float scale)
        {
            var level=scene.Level;float w=level.width*.5f,h=level.height*.5f;
            var moss=scene.Ground;
            Ellipse(vh,centre,new Vector2((w+h)*scale*.84f,(w+h)*scale*.51f),moss,Clear(moss),24);
            Shore(vh,scene,centre,scale);
            Vector2 P(float x,float z)=>centre+Project(new Vector3(x,0,z),scale);
            // A single meadow, subtle bevel and sparse lines; no checkerboard.
            Quad(vh,P(-w,-h)+Vector2.down*4,P(-w,h)+Vector2.down*4,P(w,h)+Vector2.down*4,P(w,-h)+Vector2.down*4,
                moss*.73f,moss*.73f,moss*.73f,moss*.73f);
            Quad(vh,P(-w,-h),P(-w,h),P(w,h),P(w,-h),moss*1.04f,moss,moss*1.04f,moss);
            var rim=scene.Tint(scene.Palette.GrassLight,Vector3.up);rim.a=.55f;
            Ribbon(vh,P(-w,-h),P(-w,h),2,rim);Ribbon(vh,P(-w,h),P(w,h),2,rim);
            Ribbon(vh,P(w,h),P(w,-h),2,rim);Ribbon(vh,P(w,-h),P(-w,-h),2,rim);
            var line=moss*.65f;line.a=.15f;
            for(int x=1;x<level.width;x++)Ribbon(vh,P(x-w,-h),P(x-w,h),.65f,line);
            for(int z=1;z<level.height;z++)Ribbon(vh,P(-w,z-h),P(w,z-h),.65f,line);
            foreach(var at in RoadmapSceneGenerator.Access(level))
            {
                var outward=RoadmapSceneGenerator.Outward(level,at);
                var a=P(at.x,at.y);var b=P(at.x+outward.x*3.2f,at.y+outward.y*3.2f);
                var soil=scene.Tint(scene.Palette.Soil,Vector3.up);soil.a=.55f;
                soil.a=.30f;
                for(int i=0;i<10;i++)
                {
                    float t=i/10f,u=(i+1)/10f;var direction=(b-a).normalized;var side=new Vector2(-direction.y,direction.x);
                    Vector2 Point(float f)=>Vector2.Lerp(a,b,f)+side*Mathf.Sin(f*Mathf.PI)*3;
                    var soft=soil;soft.a*=.40f;
                    Ribbon(vh,Point(t),Point(u),scale*.65f,soft);
                    Ribbon(vh,Point(t),Point(u),scale*.33f,soil);
                }
                Ellipse(vh,b,new Vector2(14,8),soil,Clear(soil));
            }
            if(!scene.Winter&&scene.Weather.Rain>.15f)
            {
                var wet=Color.Lerp(scene.Light.Ambient,new Color(.48f,.63f,.64f),.5f);wet.a=scene.Weather.Rain*.25f;
                Ellipse(vh,centre+P(w+.7f,h+.5f)-centre,new Vector2(scale*1.2f,scale*.40f),wet,Clear(wet));
            }
            SeasonalGround(vh,scene,centre,scale);
        }
        static void SeasonalGround(VertexHelper vh,RoadmapSceneGenerator.Scene scene,Vector2 centre,float scale)
        {
            float w=scene.Level.width*.5f,h=scene.Level.height*.5f;
            if(scene.Winter)
            {
                // Faceted drift tops share the exact depth field used by the 3D floor.
                const int steps=16;float span=Mathf.Max(w,h)+4.8f;
                var samples=new Vector3[(steps+1)*(steps+1)];
                for(int z=0;z<=steps;z++)for(int x=0;x<=steps;x++)
                {var p=new Vector3(Mathf.Lerp(-span,span,x/(float)steps),0,Mathf.Lerp(-span,span,z/(float)steps));p.y=scene.SnowDepth(p);samples[z*(steps+1)+x]=p;}
                Vector3 Snow(int x,int z)=>samples[z*(steps+1)+x];
                void Face(Vector3 a,Vector3 b,Vector3 c)
                {
                    var middle=(a+b+c)/3;
                    if(Mathf.Abs(middle.x)<w+.3f&&Mathf.Abs(middle.z)<h+.3f)return;
                    if(ShorelineGeometry.Contains(scene.Level.environment?.shore,middle,.4f))return;
                    var normal=Vector3.Cross(b-a,c-a).normalized;var color=scene.Tint(scene.Palette.GrassLight,normal);
                    float radius=Mathf.Max(Mathf.Abs(middle.x),Mathf.Abs(middle.z));color.a=Mathf.Clamp01((span-radius)/1.6f)*scene.SnowAmount;
                    int start=vh.currentVertCount;vh.AddVert(centre+Project(a,scale),color,Vector2.zero);vh.AddVert(centre+Project(b,scale),color,Vector2.zero);vh.AddVert(centre+Project(c,scale),color,Vector2.zero);vh.AddTriangle(start,start+1,start+2);
                }
                for(int z=0;z<steps;z++)for(int x=0;x<steps;x++)
                {var a=Snow(x,z);var b=Snow(x+1,z);var c=Snow(x,z+1);var d=Snow(x+1,z+1);Face(a,c,b);Face(b,c,d);}
            }
            if(scene.LeafLitter>.001f)
            {
                var rng=new System.Random(unchecked(scene.Level.decorSeed*377+59));int count=Mathf.RoundToInt(scene.LeafLitter*85);
                for(int i=0;i<count;i++)
                {
                    var p=new Vector3((float)(rng.NextDouble()*2-1)*(w+3),(float)0,(float)(rng.NextDouble()*2-1)*(h+3));
                    if(ShorelineGeometry.Contains(scene.Level.environment?.shore,p,.2f))continue;
                    var at=centre+Project(p,scale);float angle=(float)rng.NextDouble()*Mathf.PI*2;
                    var axis=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*scale*.08f;var side=new Vector2(-axis.y,axis.x)*.55f;
                    var color=scene.Tint(Color.Lerp(new Color(.48f,.29f,.16f),new Color(.85f,.57f,.23f),(float)rng.NextDouble()),Vector3.up);color.a=.85f;
                    Quad(vh,at-axis,at+side,at+axis,at-side,color,color*.9f,color,color*.9f);
                }
            }
        }
        static void Shore(VertexHelper vh,RoadmapSceneGenerator.Scene scene,Vector2 centre,float scale)
        {
            var shore=scene.Level.environment?.shore;if(shore==null)return;
            float span=Mathf.Max(scene.Level.width,scene.Level.height)*.5f+3.8f;
            var sky=Color.Lerp(scene.Light.Ambient,new Color(.57f,.68f,.66f),.35f);
            var water=scene.Tint(new Color(.26f,.47f,.44f),Vector3.up);
            var shallow=Color.Lerp(water,sky,.20f);var deep=Color.Lerp(water,sky,.42f);
            var soil=scene.Tint(scene.Palette.Soil,Vector3.up);
            Vector2 P(float distance,float fraction)=>centre+Project(ShorelineGeometry.Point(shore,distance,fraction,.07f),scale);
            for(int i=0;i<16;i++)
            {
                float a=Mathf.Lerp(-span,span,i/16f),b=Mathf.Lerp(-span,span,(i+1)/16f);
                float Fade(float at)=>Mathf.SmoothStep(0,1,Mathf.Clamp01((span-Mathf.Abs(at))/2));
                Color At(Color color,float distance){color.a*=Fade(distance);return color;}
                Quad(vh,P(a,0),P(b,0),P(b,.5f),P(a,.5f),At(shallow,a),At(shallow,b),At(deep,b),At(deep,a));
                Quad(vh,P(a,.5f),P(b,.5f),P(b,1),P(a,1),At(deep,a),At(deep,b),At(shallow,b),At(shallow,a));
                for(int edge=0;edge<2;edge++)
                {
                    var pa=P(a,edge);var pb=P(b,edge);var color=soil;
                    color.a=Mathf.Min(1,(1-Mathf.Pow(Mathf.Abs((a+b)*.5f/span),4))*.9f);
                    Ribbon(vh,pa,pb,scale*.25f,color);
                    var foam=sky;foam.a=color.a*.25f;Ribbon(vh,pa,pb,1.2f,foam);
                }
                if(i%3==0)
                {
                    var reflection=sky;reflection.a=.20f*(1-scene.Weather.Cloud*.5f);
                    Ribbon(vh,P(a,.26f),P(a+.18f,.72f),1.3f,reflection);
                }
            }
        }
        public static void Ellipse(VertexHelper vh,Vector2 centre,Vector2 radius,Color inner,Color outer,int sides=12)
        {
            int start=vh.currentVertCount;vh.AddVert(centre,inner,Vector2.zero);
            for(int i=0;i<=sides;i++)
            { float angle=i*Mathf.PI*2/sides;vh.AddVert(centre+Vector2.Scale(new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)),radius),outer,Vector2.zero);if(i>0)vh.AddTriangle(start,start+i,start+i+1); }
        }
        public static void Triangle(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Color color)
        { int s=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);vh.AddVert(c,color,Vector2.zero);vh.AddTriangle(s,s+1,s+2); }
        public static void Ribbon(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
        { var d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;Quad(vh,a+n,b+n,b-n,a-n,color,color,color,color); }
        public static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color ca,Color cb,Color cc,Color cd)
        {
            int s=vh.currentVertCount;vh.AddVert(a,ca,Vector2.zero);vh.AddVert(b,cb,Vector2.zero);vh.AddVert(c,cc,Vector2.zero);vh.AddVert(d,cd,Vector2.zero);
            vh.AddTriangle(s,s+1,s+2);vh.AddTriangle(s,s+2,s+3);
        }
    }
}
