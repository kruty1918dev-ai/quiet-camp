using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;
namespace QuietCamp.Editor.LevelAuthoring
{
    public static class WeatherModelRigAuthoring
    {
        public static void Prepare(LevelAuthoringDocument document)
        {
            var models=document.art.models.ToDictionary(m=>m.id);var instances=document.art.instances.ToList();
            foreach(var item in instances.ToArray())
            {
                if(item.modelId==null||!models.TryGetValue(item.modelId,out var model))continue;
                var source=model.sourceModelId??model.id;
                if(source=="qc_rowboat"&&item.motion==null)item.motion="boat";
                if(source=="qc_crash_drying"&&!instances.Any(i=>i.id==item.id+"/cloth0"))SplitDrying(model,item,models,instances);
                if(source=="qc_watermill_ruin"&&!instances.Any(i=>i.id==item.id+"/wheel"))SplitWheel(model,item,models,instances);
                if(source=="qc_watermill_wheel"&&item.motion==null)
                {var pivot=AuthoringPropCatalog.ModelBounds(model).center;item.motion="waterwheel";item.motionPivot=new[]{pivot.x,pivot.y,pivot.z};}
            }
            document.art.models=models.Values.ToArray();document.art.instances=instances.ToArray();
        }
        static void SplitDrying(CampArtDefinition.Model model,CampArtDefinition.Instance item,Dictionary<string,CampArtDefinition.Model> models,List<CampArtDefinition.Instance> instances)
        {
            // Our five closed boxes: two supports, rope, and two cloth panels.
            // Edited topology stays intact; never guess a cut from unrelated triangles.
            if(model.colors.Length!=180)return;
            var support=Slice(model,Enumerable.Range(0,36).ToArray(),model.id+"/support");support.sourceModelId="qc_crash_drying_support";models[support.id]=support;item.modelId=support.id;
            for(int panel=0;panel<2;panel++)
            {
                var cloth=Slice(model,Enumerable.Range(36+panel*12,12).ToArray(),model.id+"/cloth"+panel);cloth.sourceModelId="qc_crash_drying_cloth";models[cloth.id]=cloth;
                var bounds=AuthoringPropCatalog.ModelBounds(cloth);
                instances.Add(new CampArtDefinition.Instance{id=item.id+"/cloth"+panel,modelId=cloth.id,x=item.x,y=item.y,z=item.z,yaw=item.yaw,scale=item.scale,rotationEuler=item.rotationEuler?.ToArray(),scaleXYZ=item.scaleXYZ?.ToArray(),surfaceAttached=false,motion="cloth",motionPivot=new[]{bounds.center.x,bounds.max.y-.02f,bounds.center.z}});
            }
        }
        static void SplitWheel(CampArtDefinition.Model model,CampArtDefinition.Instance item,Dictionary<string,CampArtDefinition.Model> models,List<CampArtDefinition.Instance> instances)
        {
            var original=LevelAuthoringPalette.Get("qc_watermill_ruin").model;
            var wheel=LevelAuthoringPalette.Get("qc_watermill_wheel").model;
            if(model.vertices.Length!=original.vertices.Length)return;
            float scale=model.vertices[0]/original.vertices[0];var offset=new Vector3(1.58f,-.43f,-.1f);
            int start=-1;
            for(int i=0;i<=original.vertices.Length-wheel.vertices.Length;i+=9)
                if(Mathf.Abs(original.vertices[i]-(wheel.vertices[0]+offset.x))<.0001f&&Mathf.Abs(original.vertices[i+1]-(wheel.vertices[1]+offset.y))<.0001f&&Mathf.Abs(original.vertices[i+2]-(wheel.vertices[2]+offset.z))<.0001f){start=i;break;}
            if(start<0)throw new InvalidOperationException("The watermill wheel topology changed; rebuild its separate part.");
            int first=start/9,count=wheel.colors.Length/3;
            var body=Slice(model,Enumerable.Range(0,model.colors.Length/3).Where(t=>t<first||t>=first+count).ToArray(),model.id+"/body");
            var rotor=Slice(model,Enumerable.Range(first,count).ToArray(),model.id+"/wheel");
            body.sourceModelId="qc_watermill_ruin";rotor.sourceModelId="qc_watermill_wheel";
            models[body.id]=body;models[rotor.id]=rotor;item.modelId=body.id;
            instances.Add(new CampArtDefinition.Instance{id=item.id+"/wheel",modelId=rotor.id,x=item.x,y=item.y,z=item.z,yaw=item.yaw,scale=item.scale,rotationEuler=item.rotationEuler?.ToArray(),scaleXYZ=item.scaleXYZ?.ToArray(),surfaceAttached=false,motion="waterwheel",motionPivot=new[]{1.58f*scale,.61f*scale,-.1f*scale}});
        }
        static CampArtDefinition.Model Slice(CampArtDefinition.Model m,int[] triangles,string id)=>new CampArtDefinition.Model
        {id=id,shading=m.shading,seasonalRole="solid",authoringRecipe=m.authoringRecipe,vertices=triangles.SelectMany(t=>m.vertices.Skip(t*9).Take(9)).ToArray(),normals=triangles.SelectMany(t=>m.normals.Skip(t*9).Take(9)).ToArray(),colors=triangles.SelectMany(t=>m.colors.Skip(t*3).Take(3)).ToArray(),weatherExposure=m.weatherExposure==null?null:triangles.SelectMany(t=>m.weatherExposure.Skip(t*3).Take(3)).ToArray(),uv=m.uv==null?null:triangles.SelectMany(t=>m.uv.Skip(t*6).Take(6)).ToArray(),tangents=m.tangents==null?null:triangles.SelectMany(t=>m.tangents.Skip(t*12).Take(12)).ToArray()};
    }
}
