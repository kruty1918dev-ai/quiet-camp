using System;
using System.Linq;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.EditorTools;
namespace Kruty1918.RockPlacement.EditorTools
{
    public static class Batch
    {
        [Serializable] public sealed class Recipe{public RockSpec spec;public SurfaceRecipe surface=new SurfaceRecipe();public Reservation[] reserved=Array.Empty<Reservation>();}
        public static void Run(){var r=BatchFiles.Request<Recipe>();var result=RockGenerator.Generate(r.spec,BatchFiles.Context(r.surface,r.reserved));BatchFiles.SaveInstances(r.spec.id,result.instances.ToArray(),result.diagnostics);}
    }
}
