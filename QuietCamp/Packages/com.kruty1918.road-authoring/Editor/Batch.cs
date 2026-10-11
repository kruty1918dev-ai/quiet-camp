using System;
using System.Linq;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.EditorTools;
namespace Kruty1918.RoadAuthoring.EditorTools
{
    public static class Batch
    {
        [Serializable] public sealed class Recipe{public RoadSpec spec;public SurfaceRecipe surface=new SurfaceRecipe();}
        public static void Run(){var r=BatchFiles.Request<Recipe>();var result=RoadGenerator.Generate(r.spec,r.surface.Create());if(result.mesh==null)throw new InvalidOperationException(string.Join(";",result.diagnostics));BatchFiles.Save(r.spec.id,result.mesh,result.diagnostics);}
    }
}
