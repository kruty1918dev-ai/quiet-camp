using Kruty1918.ScenePlacement;
namespace Kruty1918.RockPlacement.Samples
{
    public static class Example
    {
        public static RockSpec Spec()=>new RockSpec{id="bank",seed=42,rocks=new[]{new AssetShape{id="stone",contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,.2f,0),new V3(.25f,.2f,.2f))}}},groups=new[]{new RockGroup{id="outcrop",center=V3.Zero,count=6,radius=2}}};
        public static RockResult Run()=>RockGenerator.Generate(Spec(),new PlacementContext(new PlaneSurface()));
    }
}
