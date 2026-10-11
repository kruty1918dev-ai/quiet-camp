using System.Linq;
using Kruty1918.ScenePlacement;
namespace Kruty1918.ModelWeathering.Samples
{
    public static class Example
    {
        public static MeshData Model(){var b=new GeometryBuilder();b.Box(new V3(0,.5f,0),new V3(1,1,1),Tint.White);var m=b.Build();m.parts=new[]{new MeshPart{id="support",role="support",preserve=true,triangles=Enumerable.Range(0,6).ToArray()},new MeshPart{id="panel",role="board",materialKind="wood",triangles=Enumerable.Range(6,6).ToArray()}};return m;}
        public static WeatheringResult Run()=>Weathering.Apply("abandoned-bench",Model(),new WeatheringProfile{seed=42,intensity=.65f});
    }
}
