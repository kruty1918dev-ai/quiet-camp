using Kruty1918.ScenePlacement;
namespace Kruty1918.MeshStyle.Samples
{
    public static class Example
    {
        public static MeshData Model(){var b=new GeometryBuilder();b.Box(new V3(0,.5f,0),new V3(1,1,1),Tint.White);return b.Build();}
        public static StyleResult Run()=>MeshHarmonizer.Harmonize(Model(),new StyleProfile{referenceTriangles=12,referenceArea=10000},10000);
    }
}
