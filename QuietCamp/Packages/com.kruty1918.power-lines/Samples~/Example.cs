using Kruty1918.ScenePlacement;
namespace Kruty1918.PowerLines.Samples
{
    public static class Example
    {
        public static SupportKit Kit()=>new SupportKit{standing=new AssetShape{id="wood-pole",contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,1.5f,0),new V3(.07f,1.5f,.07f))}},fallen=new AssetShape{id="broken-pole",contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,.07f,0),new V3(1.5f,.07f,.07f))}},sockets=new[]{new V3(-.22f,2.8f,0),new V3(.22f,2.8f,0)}};
        public static PowerSpec Spec()=>new PowerSpec{id="village-line",route=new RoutePath(new[]{V3.Zero,new V3(0,0,16)}),sag=.4f};
        public static PowerResult Run()=>PowerGenerator.Generate(Spec(),Kit(),new PlacementContext(new PlaneSurface()));
    }
}
