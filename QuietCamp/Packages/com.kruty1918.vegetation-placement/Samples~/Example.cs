using Kruty1918.ScenePlacement;
namespace Kruty1918.VegetationPlacement.Samples
{
    public static class Example
    {
        public static VegetationSpec Spec()=>new VegetationSpec{id="meadow",seed=42,zone=new Box(V3.Zero,new V3(4,1,4)),density=1,patchContrast=0,species=new[]{new Species{id="grass",spacing=.15f,shape=new AssetShape{id="grass-mesh",contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,.1f,0),new V3(.05f,.1f,.05f))}}}}};
        public static VegetationResult Run()=>VegetationGenerator.Generate(Spec(),new PlacementContext(new PlaneSurface()),new ConstantFields());
    }
}
