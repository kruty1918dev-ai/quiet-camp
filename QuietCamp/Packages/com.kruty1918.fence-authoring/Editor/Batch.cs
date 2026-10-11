using System;
using System.Linq;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.EditorTools;
namespace Kruty1918.FenceAuthoring.EditorTools
{
    public static class Batch
    {
        [Serializable] public sealed class Recipe{public FenceSpec spec;public FenceKit kit;public SurfaceRecipe surface=new SurfaceRecipe();public Reservation[] reserved=Array.Empty<Reservation>();}
        public static void Run(){var r=BatchFiles.Request<Recipe>();var context=BatchFiles.Context(r.surface,r.reserved);var result=FenceGenerator.Generate(r.spec,r.kit,context);BatchFiles.SaveInstances(r.spec.id,result.instances.ToArray(),result.diagnostics);}
    }
}
