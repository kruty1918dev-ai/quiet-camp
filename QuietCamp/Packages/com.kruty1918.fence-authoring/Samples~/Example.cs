using Kruty1918.ScenePlacement;
namespace Kruty1918.FenceAuthoring.Samples
{
    public static class Example
    {
        public static FenceKit Kit()=>new FenceKit{post=new AssetShape{id="post",contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,.5f,0),new V3(.06f,.5f,.06f))}},section=new AssetShape{id="rail",contacts=new[]{V3.Zero},volumes=new[]{new Box(V3.Zero,new V3(.75f,.18f,.035f))}},gate=new AssetShape{id="gate",contacts=new[]{V3.Zero},volumes=new[]{new Box(V3.Zero,new V3(.75f,.25f,.04f))}}};
        public static FenceResult Run()=>FenceGenerator.Generate(new FenceSpec{id="garden",contour=new[]{V3.Zero,new V3(6,0,0)},jitter=0,lean=0,openings=new[]{new FenceOpening{id="entrance",from=1.5f,to=3,gate=true}}},Kit(),new PlacementContext(new PlaneSurface()));
    }
}
