using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    /// <summary>Small flat-shaded meshes with explicit structure; no runtime procedural dependencies.</summary>
    public sealed class CrashMeshAuthoring
    {
        readonly List<float> _vertices=new List<float>(), _normals=new List<float>();
        readonly List<int> _colors=new List<int>();
        public void Triangle(Vector3 a,Vector3 b,Vector3 c,int color,Vector3? expected=null)
        {
            var normal=Vector3.Cross(b-a,c-a).normalized;
            if(normal.sqrMagnitude<.5f)return;
            if(expected.HasValue&&Vector3.Dot(normal,expected.Value)<0){var swap=b;b=c;c=swap;normal=-normal;}
            foreach(var p in new[]{a,b,c}){_vertices.AddRange(new[]{p.x,p.y,p.z});_normals.AddRange(new[]{normal.x,normal.y,normal.z});_colors.Add(color);}
        }
        public void Trunk(Vector3 a,Vector3 b,float radius)
        {
            var axis=(b-a).normalized;var u=Vector3.Cross(axis,Vector3.up).normalized;if(u.sqrMagnitude<.5f)u=Vector3.right;var v=Vector3.Cross(axis,u).normalized;
            for(int k=0;k<8;k++)
            {
                float t=k*Mathf.PI/4,next=(k+1)*Mathf.PI/4;var p=(Mathf.Cos(t)*u+Mathf.Sin(t)*v)*radius;var q=(Mathf.Cos(next)*u+Mathf.Sin(next)*v)*radius;
                int bark=k%3==0?0x635c43:0x72664c;Triangle(a+p,b+p,b+q,bark);Triangle(a+p,b+q,a+q,bark);
                Triangle(a,a+q,a+p,0x95856a,-axis);Triangle(b,b+p,b+q,0x8c7f65,axis);
            }
        }
        public void Box(Vector3 centre,Vector3 size,int color,Quaternion? rotation=null)
        {
            var q=rotation??Quaternion.identity;var half=size*.5f;
            void Face(Vector3 normal,Vector3 u,Vector3 v)
            {
                var n=Vector3.Scale(normal,half);u=Vector3.Scale(u,half);v=Vector3.Scale(v,half);
                Vector3 P(Vector3 p)=>centre+q*p;
                Triangle(P(n-u-v),P(n+u-v),P(n+u+v),color,q*normal);
                Triangle(P(n-u-v),P(n+u+v),P(n-u+v),color,q*normal);
            }
            Face(Vector3.up,Vector3.forward,Vector3.right);Face(Vector3.down,Vector3.right,Vector3.forward);
            Face(Vector3.right,Vector3.up,Vector3.forward);Face(Vector3.left,Vector3.forward,Vector3.up);
            Face(Vector3.forward,Vector3.right,Vector3.up);Face(Vector3.back,Vector3.up,Vector3.right);
        }
        public CampArtDefinition.Model Model(string source,string label,string category="Пропси",bool wind=false)
        {
            var m=new CampArtDefinition.Model{sourceModelId=source,shading=wind?"foliage":"solid",vertices=_vertices.ToArray(),normals=_normals.ToArray(),colors=_colors.ToArray(),seasonalRole="solid"};
            return ModelAuthoringWorkshop.Register(m,new ModelAuthoringRecipe{sourceId=source,operation="external",backend=wind?"low-poly-crash-story/v1-wind":"low-poly-crash-story/v1",label=label,category=category},false).model;
        }
        public static CampArtDefinition.Model Damage(CampArtDefinition.Model source,bool fore,float strength,bool detachedWing,out CampArtDefinition.Model wing)
        {
            var body=new CrashMeshAuthoring();var fragment=new CrashMeshAuthoring();
            var bounds=AuthoringPropCatalog.ModelBounds(source);float cut=fore?bounds.min.z:bounds.max.z;
            Vector3 Point(int i)=>new Vector3(source.vertices[i*3],source.vertices[i*3+1],source.vertices[i*3+2]);
            Vector3 Warp(Vector3 p)
            {
                float edge=Mathf.Clamp01(1-Mathf.Abs(p.z-cut)/2);
                float angle=Mathf.Atan2(p.y-2.6f,p.x);
                if(Mathf.Abs(p.x)<3.2f)
                {
                    p.z+=strength*edge*(.62f*Mathf.Sin(angle*5)+.24f*Mathf.Cos(angle*9));
                    p.x+=strength*edge*.28f*Mathf.Sin(angle*3);
                    p.y-=strength*edge*.45f*(.5f+.5f*Mathf.Cos(angle*4));
                    p.y-=strength*.85f*Mathf.Exp(-(p.z-1.8f)*(p.z-1.8f)/10)*Mathf.Clamp01((p.y-2)/3);
                    p.x*=1-strength*.12f*Mathf.Exp(-(p.z-1.8f)*(p.z-1.8f)/10);
                }
                else p.y-=strength*.11f*(Mathf.Abs(p.x)-3.2f)*(.5f+.5f*Mathf.Cos(p.z*.65f));
                return p;
            }
            for(int i=0;i<source.colors.Length;i+=3)
            {
                var p=new[]{Point(i),Point(i+1),Point(i+2)};var centre=(p[0]+p[1]+p[2])/3;
                var oldNormal=new Vector3(source.normals[i*3],source.normals[i*3+1],source.normals[i*3+2]);
                bool breakPanel=strength>.1f&&Mathf.Abs(centre.z-cut)<1.1f&&centre.y>3.4f&&centre.x>.15f&&centre.x<2.5f;
                if(breakPanel)continue;
                // Clip a real side wing, instead of adding an unrelated decorative wing.
                bool separate=detachedWing&&centre.x>4.1f;
                var target=separate?fragment:body;
                target.Triangle(Warp(p[0]),Warp(p[1]),Warp(p[2]),source.colors[i],oldNormal);
                if(!separate&&Mathf.Abs(centre.z-cut)<.22f&&Mathf.Abs(centre.x)<2.8f)
                {
                    // Folded lips give the exposed broken skin visible thickness.
                    Vector3 Inset(Vector3 v)=>v+new Vector3(-v.x*.025f,(2.6f-v.y)*.025f,fore?.1f:-.1f);
                    var a=Warp(p[0]);var b=Warp(p[1]);body.Triangle(a,b,Inset(b),0x62675a);body.Triangle(a,Inset(b),Inset(a),0x62675a);
                }
            }
            wing=fragment._colors.Count>0?DominantComponent(fragment.Model("qc_crash_detached_wing","Відірване пошкоджене крило","Великі об’єкти")):null;
            return body.Model(source.sourceModelId??source.id,fore?"Зім’ятий ніс літака":"Пошкоджена хвостова секція","Великі об’єкти");
        }
        static CampArtDefinition.Model DominantComponent(CampArtDefinition.Model source)
        {
            int count=source.colors.Length/3;var parent=Enumerable.Range(0,count).ToArray();var points=new Dictionary<Vector3Int,int>();
            int Root(int k){while(parent[k]!=k){parent[k]=parent[parent[k]];k=parent[k];}return k;}
            for(int t=0;t<count;t++)for(int j=0;j<3;j++)
            {
                int k=(t*3+j)*3;var key=new Vector3Int(Mathf.RoundToInt(source.vertices[k]*1000),Mathf.RoundToInt(source.vertices[k+1]*1000),Mathf.RoundToInt(source.vertices[k+2]*1000));
                if(points.TryGetValue(key,out var first))parent[Root(t)]=Root(first);else points[key]=t;
            }
            var largest=Enumerable.Range(0,count).GroupBy(Root).OrderByDescending(g=>g.Sum(t=>{int k=t*9;var a=new Vector3(source.vertices[k],source.vertices[k+1],source.vertices[k+2]);var b=new Vector3(source.vertices[k+3],source.vertices[k+4],source.vertices[k+5]);var c=new Vector3(source.vertices[k+6],source.vertices[k+7],source.vertices[k+8]);return Vector3.Cross(b-a,c-a).magnitude;})).First().ToArray();
            source.vertices=largest.SelectMany(t=>source.vertices.Skip(t*9).Take(9)).ToArray();source.normals=largest.SelectMany(t=>source.normals.Skip(t*9).Take(9)).ToArray();source.colors=largest.SelectMany(t=>source.colors.Skip(t*3).Take(3)).ToArray();return source;
        }
        public static CampArtDefinition.Model Prop(string kind)
        {
            var m=new CrashMeshAuthoring();
            if(kind=="suitcase")
            {
                m.Box(new Vector3(0,.22f,0),new Vector3(.7f,.43f,.3f),0x79624c);
                foreach(float x in new[]{-.29f,.29f})m.Box(new Vector3(x,.22f,.16f),new Vector3(.045f,.44f,.035f),0x484f40);
                m.Box(new Vector3(0,.47f,0),new Vector3(.23f,.04f,.07f),0x4c4b3d);
                foreach(float x in new[]{-.1f,.1f})m.Box(new Vector3(x,.445f,0),new Vector3(.035f,.06f,.05f),0x4c4b3d);
            }
            else if(kind=="seat")
            {
                m.Box(new Vector3(0,.45f,0),new Vector3(.58f,.17f,.56f),0x576e6a);
                m.Box(new Vector3(0,.78f,.23f),new Vector3(.58f,.62f,.14f),0x526862,Quaternion.Euler(-9,0,0));
                foreach(float x in new[]{-.23f,.23f})m.Box(new Vector3(x,.24f,0),new Vector3(.04f,.45f,.42f),0x555747);
                foreach(float x in new[]{-.34f,.34f})m.Box(new Vector3(x,.66f,0),new Vector3(.055f,.06f,.5f),0x626251);
            }
            else if(kind=="boots")
                foreach(float x in new[]{-.14f,.14f}){m.Box(new Vector3(x,.11f,0),new Vector3(.19f,.19f,.34f),0x534736);m.Box(new Vector3(x,.25f,.09f),new Vector3(.17f,.26f,.16f),0x5d5140);}
            else if(kind=="coat")
            {
                m.Box(new Vector3(0,.03f,0),new Vector3(.55f,.055f,.85f),0x747764);
                foreach(float x in new[]{-.31f,.31f})m.Box(new Vector3(x,.03f,-.2f),new Vector3(.22f,.045f,.5f),0x737c64,Quaternion.Euler(0,x>0?25:-25,0));
            }
            else if(kind=="broken_trunk")
            {m.Trunk(new Vector3(-2.2f,.34f,0),new Vector3(2.2f,.27f,.2f),.28f);m.Trunk(new Vector3(.2f,.34f,0),new Vector3(.9f,.75f,.8f),.09f);}
            else if(kind=="rib")
            {
                for(int k=0;k<10;k++){float angle=k*Mathf.PI/5;float next=(k+1)*Mathf.PI/5;var a=new Vector3(Mathf.Cos(angle)*1.55f,1.65f+Mathf.Sin(angle)*1.55f,0);var b=new Vector3(Mathf.Cos(next)*1.55f,1.65f+Mathf.Sin(next)*1.55f,0);var centre=(a+b)*.5f;m.Box(centre,new Vector3(.1f,Vector3.Distance(a,b)+.07f,.13f),0x5b5946,Quaternion.FromToRotation(Vector3.up,b-a));}
            }
            else if(kind=="awning")
            {
                foreach(float x in new[]{-1.55f,1.55f})foreach(float z in new[]{-.68f,.68f}){float h=1.95f-z*Mathf.Tan(7*Mathf.Deg2Rad);m.Box(new Vector3(x,h*.5f,z),new Vector3(.1f,h,.11f),0x6f6146);}
                m.Box(new Vector3(0,1.84f,0),new Vector3(3.3f,.11f,.09f),0x6f6146);
                m.Box(new Vector3(0,2,0),new Vector3(3.4f,.09f,1.75f),0x878b78,Quaternion.Euler(7,0,0));
            }
            else if(kind=="drying")
            {
                foreach(float x in new[]{-1.1f,1.1f})m.Box(new Vector3(x,.85f,0),new Vector3(.07f,1.7f,.08f),0x6c6348,Quaternion.Euler(0,0,x>0?-5:3));
                m.Box(new Vector3(0,1.6f,0),new Vector3(2.3f,.025f,.025f),0x595947);
                m.Box(new Vector3(-.35f,1.3f,0),new Vector3(.6f,.6f,.045f),0x758783,Quaternion.Euler(0,0,5));
                m.Box(new Vector3(.4f,1.35f,0),new Vector3(.45f,.5f,.045f),0x9c947c,Quaternion.Euler(0,0,-4));
            }
            else throw new ArgumentException("Unknown crash prop: "+kind);
            string label=kind=="suitcase"?"Потерта валіза":kind=="seat"?"Врятоване літакове крісло":kind=="boots"?"Стара пара черевиків":kind=="coat"?"Залишене пальто":kind=="broken_trunk"?"Старий зламаний стовбур":kind=="rib"?"Окиснений алюмінієвий шпангоут":kind=="awning"?"Навіс зі знятої обшивки":"Мотузка для сушіння речей";
            var result=m.Model("qc_crash_"+kind,label,kind=="awning"?"Декор":"Пропси",kind=="drying");
            return result;
        }
    }
}
