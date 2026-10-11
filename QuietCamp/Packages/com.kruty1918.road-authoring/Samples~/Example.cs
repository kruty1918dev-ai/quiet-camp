using Kruty1918.ScenePlacement;
namespace Kruty1918.RoadAuthoring.Samples
{
    public static class Example
    {
        public static RoadSpec Spec()=>new RoadSpec{id="path",seed=42,points=new[]{V3.Zero,new V3(0,0,4),new V3(2,0,8)},bands=new[]{new SurfaceBand{distance=0,width=.8f},new SurfaceBand{distance=8,width=1.1f}}};
        public static RoadResult Run()=>RoadGenerator.Generate(Spec(),new PlaneSurface());
    }
}
