using Kruty1918.ScenePlacement;
namespace Kruty1918.ScenePlacement.Samples
{
    public static class Example
    {
        public static AssetShape Shape(string id="prop",float height=1,float half=.2f)=>new AssetShape{id=id,contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,height*.5f,0),new V3(half,height*.5f,half))}};
        public static PlacementResult Run()=>PlacementSolver.Ensemble(new[]{new PlacementRequest{id="yard/bench",owner="yard",shape=Shape(),target=new V3(2,0,2)}},new PlacementContext(new PlaneSurface()));
    }
}
