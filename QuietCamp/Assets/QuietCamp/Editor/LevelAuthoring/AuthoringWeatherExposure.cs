using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;
using Newtonsoft.Json;
namespace QuietCamp.Editor.LevelAuthoring
{
    public static class AuthoringWeatherExposure
    {
        public static void Bake(LevelAuthoringDocument d, string[] targetIds=null)
        {
            const float cell=.4f;var sky=new Dictionary<Vector2Int,float>();var models=d.art.models.ToDictionary(m=>m.id);
            var targets=new HashSet<string>(targetIds??d.art.instances.Where(i=>i.modelId!=null&&models[i.modelId].weatherExposure!=null).Select(i=>i.id));
            if(targets.Count==0)return;
            var items=d.art.instances.Where(i=>i.modelId!=null&&models[i.modelId].shading!="foliage"&&models[i.modelId].seasonalRole!="deciduous"&&models[i.modelId].seasonalRole!="conifer").ToArray();
            Vector3 World(CampArtDefinition.Model m,CampArtDefinition.Instance i,int k)=>new Vector3(i.x,i.y,i.z)+(i.rotationEuler==null?Quaternion.Euler(0,i.yaw,0):Quaternion.Euler(CampArtDefinition.Vector(i.rotationEuler)))*Vector3.Scale(new Vector3(m.vertices[k*3],m.vertices[k*3+1],m.vertices[k*3+2]),i.scaleXYZ==null?Vector3.one*i.scale:CampArtDefinition.Vector(i.scaleXYZ));
            foreach(var i in items)
            {
                var m=models[i.modelId];
                for(int k=0;k<m.colors.Length;k+=3)
                {
                    var a=World(m,i,k);var b=World(m,i,k+1);var c=World(m,i,k+2);
                    float determinant=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);if(Mathf.Abs(determinant)<.0001f)continue;
                    int loX=Mathf.FloorToInt(Mathf.Min(a.x,b.x,c.x)/cell),hiX=Mathf.CeilToInt(Mathf.Max(a.x,b.x,c.x)/cell);
                    int loZ=Mathf.FloorToInt(Mathf.Min(a.z,b.z,c.z)/cell),hiZ=Mathf.CeilToInt(Mathf.Max(a.z,b.z,c.z)/cell);
                    for(int x=loX;x<=hiX;x++)for(int z=loZ;z<=hiZ;z++)
                    {
                        float px=(x+.5f)*cell,pz=(z+.5f)*cell;
                        float u=((b.z-c.z)*(px-c.x)+(c.x-b.x)*(pz-c.z))/determinant,v=((c.z-a.z)*(px-c.x)+(a.x-c.x)*(pz-c.z))/determinant;
                        if(u<-.03f||v<-.03f||u+v>1.03f)continue;
                        float y=u*a.y+v*b.y+(1-u-v)*c.y;var key=new Vector2Int(x,z);if(!sky.TryGetValue(key,out var old)||y>old)sky[key]=y;
                    }
                }
            }
            foreach(var i in d.art.instances.Where(i=>targets.Contains(i.id)))
            {
                var original=models[i.modelId];var m=LevelAuthoringDocument.Copy(models[i.modelId]);m.weatherExposure=new float[m.colors.Length];
                for(int k=0;k<m.colors.Length;k++)
                {
                    var p=World(m,i,k);var key=new Vector2Int(Mathf.FloorToInt(p.x/cell),Mathf.FloorToInt(p.z/cell));
                    m.weatherExposure[k]=sky.TryGetValue(key,out var roof)&&roof>p.y+.2f?.12f:1;
                }
                if(original.weatherExposure!=null&&original.weatherExposure.SequenceEqual(m.weatherExposure))continue;
                var metadata=string.IsNullOrEmpty(m.authoringRecipe)?new ModelAuthoringRecipe{label=m.sourceModelId??m.id,category="Декор"}:JsonConvert.DeserializeObject<ModelAuthoringRecipe>(m.authoringRecipe);
                var variant=ModelAuthoringWorkshop.Register(m,new ModelAuthoringRecipe{operation="external",sourceId=m.id,backend="baked-precipitation-shelter/v1",label=metadata.label,category=metadata.category},false);
                models[variant.model.id]=variant.model;i.modelId=variant.model.id;
            }
            d.art.models=models.Values.Where(m=>d.art.instances.Any(i=>i.modelId==m.id)).ToArray();
        }
    }
}
