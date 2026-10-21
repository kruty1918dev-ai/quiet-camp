using System;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    public static class PlantWeatherResponse
    {
        static readonly int Weather=Shader.PropertyToID("_QCWeather");
        public static Vector2[] Weights(CampArtDefinition.Model model)
        {
            var result=new Vector2[model.colors.Length];if(model.seasonalRole=="solid"||(string.IsNullOrEmpty(model.seasonalRole)&&model.shading!="foliage"))return result;
            float min=float.MaxValue,max=float.MinValue;
            for(int i=1;i<model.vertices.Length;i+=3){min=Mathf.Min(min,model.vertices[i]);max=Mathf.Max(max,model.vertices[i]);}
            float height=Mathf.Max(.01f,max-min);float stiffness=model.seasonalRole=="conifer"?.25f:model.seasonalRole=="deciduous"?.65f:1;
            var leaves=new System.Collections.Generic.HashSet<int>(model.seasonalLeafTriangles??Array.Empty<int>());
            for(int i=0;i<result.Length;i++)
            {
                float w=Mathf.Pow(Mathf.Clamp01((model.vertices[i*3+1]-min)/height),2)*stiffness;
                if(model.seasonalRole=="deciduous"&&!leaves.Contains(i/3))w*=.12f;
                result[i]=new Vector2(w,height);
            }
            return result;
        }
        public static void Set(Material material,float clock,float wind,float rain)
        {
            if(material==null||!material.HasProperty(Weather))return;
            material.SetVector(Weather,new Vector4(clock,wind,rain,1));
        }
    }
}
