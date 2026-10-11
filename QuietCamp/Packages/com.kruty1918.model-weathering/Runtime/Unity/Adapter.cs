using UnityEngine;
using Kruty1918.ScenePlacement.Unity;
namespace Kruty1918.ModelWeathering.Unity
{
    public static class WeatheringAdapter
    {
        public static WeatheringResult Apply(string id,Mesh readableStaticMesh,WeatheringProfile profile,System.Threading.CancellationToken cancellation=default)=>Weathering.Apply(id,UnityBridge.Read(readableStaticMesh),profile,cancellation);
        public static Mesh Create(WeatheringResult result,string name="Weathered mesh")=>UnityBridge.CreateMesh(result.mesh,name);
    }
}
