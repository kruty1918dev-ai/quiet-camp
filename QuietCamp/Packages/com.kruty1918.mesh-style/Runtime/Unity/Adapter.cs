using System;
using UnityEngine;
using Kruty1918.ScenePlacement.Unity;
namespace Kruty1918.MeshStyle.Unity
{
    public static class MeshStyleAdapter
    {
        /// <summary>Largest projected bounds area among explicit control cameras, in pixel squared.</summary>
        public static float ProjectedArea(Mesh mesh,Transform model,Camera[] cameras)
        {
            if(mesh==null||model==null||cameras==null||cameras.Length==0)throw new ArgumentException("Model and control cameras required");
            float largest=0;var b=mesh.bounds;
            foreach(var camera in cameras)
            {
                if(camera==null)throw new ArgumentException("Null control camera");
                float x0=float.PositiveInfinity,x1=float.NegativeInfinity,y0=float.PositiveInfinity,y1=float.NegativeInfinity;int front=0;
                for(int k=0;k<8;k++){var p=b.center+Vector3.Scale(b.extents,new Vector3((k&1)==0?-1:1,(k&2)==0?-1:1,(k&4)==0?-1:1));var screen=camera.WorldToViewportPoint(model.TransformPoint(p));if(screen.z<=camera.nearClipPlane)continue;front++;x0=Mathf.Min(x0,screen.x);x1=Mathf.Max(x1,screen.x);y0=Mathf.Min(y0,screen.y);y1=Mathf.Max(y1,screen.y);}
                if(front==0)continue;
                if(front<8){x0=y0=0;x1=y1=1;}
                largest=Mathf.Max(largest,(Mathf.Clamp01(x1)-Mathf.Clamp01(x0))*(Mathf.Clamp01(y1)-Mathf.Clamp01(y0))*camera.pixelWidth*camera.pixelHeight);
            }
            return largest;
        }
        public static StyleResult Harmonize(Mesh mesh,StyleProfile profile,float projectedArea,System.Threading.CancellationToken cancellation=default)=>MeshHarmonizer.Harmonize(UnityBridge.Read(mesh),profile,projectedArea,cancellation);
        public static Mesh Create(StyleResult result,string name="Styled mesh")=>UnityBridge.CreateMesh(result.mesh,name);
    }
}
