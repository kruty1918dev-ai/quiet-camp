using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Editor.LevelAuthoring
{
    [Serializable] public sealed class ObjectOvergrowthSettings
    {
        public string material="aluminium",vegetation="climber",coverage="sheltered";
        public int seed=419,maxPlants=48;
        public float density=.65f,scale=.5f,moisture=.65f;
        public void Validate()
        {
            if(!new[]{"aluminium","steel","wood","stone","generic"}.Contains(material)||!new[]{"climber","grass","fern"}.Contains(vegetation)||!new[]{"sheltered","base","upper"}.Contains(coverage)||maxPlants<0||maxPlants>160
                ||!new[]{density,scale,moisture}.All(v=>!float.IsNaN(v)&&!float.IsInfinity(v))||density<0||density>1||moisture<0||moisture>1||scale<.1f||scale>1.5f)throw new ArgumentException("Некоректні параметри обростання.");
        }
        public static ObjectOvergrowthSettings Defaults()=>JsonConvert.DeserializeObject<ObjectOvergrowthSettings>(Resources.Load<TextAsset>("QuietCamp/object_overgrowth").text);
    }

    /// <summary>Bakes the existing weathering system's site-dependent moss mask and editable plant instances.
    /// No Editor dependency or per-object update component is needed in the exported game.</summary>
    public static class ObjectOvergrowthAuthoring
    {
        public static int Apply(LevelAuthoringDocument document,IEnumerable<string> instanceIds,ObjectOvergrowthSettings settings)
        {
            settings.Validate();var targets=new HashSet<string>(instanceIds);var art=document.art;
            var models=art.models.ToDictionary(m=>m.id);var items=art.instances.Where(i=>!targets.Any(id=>(i.id??"").StartsWith("overgrowth/"+id+"/",StringComparison.Ordinal))).ToList();
            var canopy=art.instances.Where(i=>i.modelId!=null&&models.TryGetValue(i.modelId,out var m)&&(m.seasonalRole=="deciduous"||m.seasonalRole=="conifer"))
                .Select(i=>{var b=AuthoringPropCatalog.ModelBounds(models[i.modelId]);return new Bounds(new Vector3(i.x,i.y,i.z)+b.center*i.scale,b.size*i.scale);}).ToArray();
            var solids=art.instances.Where(i=>i.modelId!=null&&models.TryGetValue(i.modelId,out var m)&&m.shading=="solid"&&m.seasonalRole!="deciduous"&&m.seasonalRole!="conifer")
                .ToDictionary(i=>i.id,i=>WorldBounds(models[i.modelId],i));
            var sun=Quaternion.Euler(CampArtDefinition.Vector(art.atmosphere?.sunEuler??new[]{43f,118f,0f}))*Vector3.back;
            var growth=Plant(settings.vegetation);growth=ModelAuthoringWorkshop.Register(growth,new ModelAuthoringRecipe{operation="external",backend="object-overgrowth/v1",label="Обростання — "+settings.vegetation,category="Рослинність"},false).model;models[growth.id]=growth;
            int total=0;
            foreach(var instance in items.Where(i=>targets.Contains(i.id)).ToArray())
            {
                if(instance.modelId==null||!models.TryGetValue(instance.modelId,out var source)||source.shading!="solid"||instance.motion!=null)continue;
                var model=LevelAuthoringDocument.Copy(source);var matrix=Matrix4x4.TRS(new Vector3(instance.x,instance.y,instance.z),Quaternion.Euler(instance.rotationEuler==null?new Vector3(0,instance.yaw,0):CampArtDefinition.Vector(instance.rotationEuler)),instance.scaleXYZ==null?Vector3.one*instance.scale:CampArtDefinition.Vector(instance.scaleXYZ));
                var normalMatrix=matrix.inverse.transpose;var bounds=WorldBounds(source,instance);var others=solids.Where(s=>s.Key!=instance.id).Select(s=>s.Value).Concat(canopy).ToArray();
                float Mask(Vector3 at,Vector3 normal)
                {
                    var ray=new Ray(at+normal*.04f,sun);float light=Mathf.Max(0,Vector3.Dot(normal,sun));
                    if(others.Any(b=>b.IntersectRay(ray,out float distance)&&distance>0&&distance<16))light*=.15f;
                    float contact=Mathf.Exp(-Mathf.Max(0,at.y-art.GroundHeight(at.x,at.z))/.9f);
                    return SurfaceWeathering.MossSuitability(at,normal,contact,light,settings.moisture,settings.material,settings.coverage,settings.seed)*settings.density;
                }
                model.mossCoverage=new float[model.colors.Length];
                for(int v=0;v<model.colors.Length;v++)
                {
                    var at=matrix.MultiplyPoint3x4(new Vector3(model.vertices[v*3],model.vertices[v*3+1],model.vertices[v*3+2]));
                    var normal=normalMatrix.MultiplyVector(new Vector3(model.normals[v*3],model.normals[v*3+1],model.normals[v*3+2])).normalized;
                    var color=CampArtDefinition.Color(model.colors[v]);model.mossCoverage[v]=color.maxColorComponent<.27f?0:Mask(at,normal);
                }
                model=ModelAuthoringWorkshop.Register(model,new ModelAuthoringRecipe{operation="external",backend="site-moss/v1",sourceId=source.sourceModelId??source.id,label="Обростання старого об’єкта",category="Декор",seed=settings.seed,amount=settings.density},false).model;models[model.id]=model;instance.modelId=model.id;
                var random=new System.Random(settings.seed^Stable(instance.id));var placed=new List<Vector3>();int count=0;
                // Area-weighted selection prevents dense details on tiny triangles and bare large panels.
                var cumulative=new float[source.colors.Length/3];float area=0;
                Vector3 Point(int v)=>matrix.MultiplyPoint3x4(new Vector3(source.vertices[v*3],source.vertices[v*3+1],source.vertices[v*3+2]));
                for(int t=0;t<cumulative.Length;t++){int v=t*3;area+=Vector3.Cross(Point(v+1)-Point(v),Point(v+2)-Point(v)).magnitude*.5f;cumulative[t]=area;}
                for(int attempt=0;attempt<settings.maxPlants*80&&count<settings.maxPlants&&area>0;attempt++)
                {
                    float pick=(float)random.NextDouble()*area;int t=Array.BinarySearch(cumulative,pick);if(t<0)t=~t;t=Mathf.Min(t,cumulative.Length-1);int v=t*3;
                    float u=(float)random.NextDouble(),w=(float)random.NextDouble();if(u+w>1){u=1-u;w=1-w;}
                    var a=Point(v);var at=a+(Point(v+1)-a)*u+(Point(v+2)-a)*w;
                    var normal=normalMatrix.MultiplyVector(new Vector3(source.normals[v*3],source.normals[v*3+1],source.normals[v*3+2])).normalized;
                    float mask=Mask(at,normal),height=at.y-art.GroundHeight(at.x,at.z);
                    if(height<-.01f||height>4.5f||mask<.15f||random.NextDouble()>mask||normal.y<-.25f||placed.Any(p=>Vector3.Distance(p,at)<settings.scale*.8f))continue;
                    if(Mathf.Abs(at.x)<art.width*.5f+.4f&&Mathf.Abs(at.z)<art.height*.5f+.4f)continue;
                    if(CampArtDefinition.Color(source.colors[v]).maxColorComponent<.3f)continue;
                    at+=normal*.018f;Quaternion rotation=settings.vegetation=="climber"&&height>.35f?Quaternion.FromToRotation(Vector3.forward,normal):Quaternion.Euler(0,(float)random.NextDouble()*360,0);
                    items.Add(new CampArtDefinition.Instance{id="overgrowth/"+instance.id+"/"+count++,modelId=growth.id,x=at.x,y=at.y,z=at.z,scale=settings.scale*Mathf.Lerp(.75f,1.25f,(float)random.NextDouble()),rotationEuler=new[]{rotation.eulerAngles.x,rotation.eulerAngles.y,rotation.eulerAngles.z},surfaceAttached=false});placed.Add(at);
                }
                total+=count;
            }
            var used=new HashSet<string>(items.Select(i=>i.modelId));art.models=models.Values.Where(m=>used.Contains(m.id)).ToArray();art.instances=items.ToArray();
            if(art.atmosphere!=null)SeasonAtmosphereAuthoring.Apply(document,art.atmosphere);
            return total;
        }
        static int Stable(string value){uint h=2166136261;foreach(char c in value)h=unchecked((h^c)*16777619);return (int)h;}
        static Bounds WorldBounds(CampArtDefinition.Model model,CampArtDefinition.Instance i)
        {
            var b=AuthoringPropCatalog.ModelBounds(model);var matrix=Matrix4x4.TRS(new Vector3(i.x,i.y,i.z),Quaternion.Euler(i.rotationEuler==null?new Vector3(0,i.yaw,0):CampArtDefinition.Vector(i.rotationEuler)),i.scaleXYZ==null?Vector3.one*i.scale:CampArtDefinition.Vector(i.scaleXYZ));var result=new Bounds(matrix.MultiplyPoint3x4(b.min),Vector3.zero);
            for(int k=0;k<8;k++)result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3((k&1)==0?b.min.x:b.max.x,(k&2)==0?b.min.y:b.max.y,(k&4)==0?b.min.z:b.max.z)));return result;
        }
        static CampArtDefinition.Model Plant(string type)
        {
            if(type=="fern")return ForestRegrowthAuthoring.Fern();
            var vertices=new List<float>();var normals=new List<float>();var colors=new List<int>();
            void Tri(Vector3 a,Vector3 b,Vector3 c,int color){var n=Vector3.Cross(b-a,c-a).normalized;foreach(var p in new[]{a,b,c}){vertices.AddRange(new[]{p.x,p.y,p.z});normals.AddRange(new[]{n.x,n.y,n.z});colors.Add(color);}}
            for(int k=0;k<7;k++)
            {
                float h=.1f+k*.095f;var at=new Vector3(Mathf.Sin(k*.8f)*.07f,h,0);float side=k%2==0?-1:1;
                var tip=at+new Vector3(side*(.18f+(k%3)*.03f),.08f,.025f);var width=new Vector3(0,.06f,.035f);
                Tri(at,tip-width,tip+width,k%2==0?0x4A642E:0x385524);Tri(at-Vector3.up*.1f,at+Vector3.right*.015f,at,0x586540);
            }
            return new CampArtDefinition.Model{sourceModelId="qc_overgrowth_"+type,shading="foliage",seasonalRole="groundcover",seasonalGrowth=new[]{.72f,1f,.48f,0f},vertices=vertices.ToArray(),normals=normals.ToArray(),colors=colors.ToArray()};
        }
    }
}
