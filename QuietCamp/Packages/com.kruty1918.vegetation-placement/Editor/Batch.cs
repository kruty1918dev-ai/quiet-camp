using System;
using System.Linq;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.EditorTools;
namespace Kruty1918.VegetationPlacement.EditorTools
{
    public static class Batch
    {
        [Serializable] public sealed class Recipe{public string id="vegetation";public int seed;public Box zone;public Species[] species;public float cellSize=1,density=.8f,patchScale=.12f,patchContrast=.5f,moisture=.5f,light=.7f;public int maximumCandidates=100000;public SurfaceRecipe surface=new SurfaceRecipe();public Reservation[] reserved=Array.Empty<Reservation>();}
        public static void Run(){var r=BatchFiles.Request<Recipe>();var spec=new VegetationSpec{id=r.id,seed=r.seed,zone=r.zone,species=r.species,cellSize=r.cellSize,density=r.density,patchScale=r.patchScale,patchContrast=r.patchContrast,maximumCandidates=r.maximumCandidates};var result=VegetationGenerator.Generate(spec,BatchFiles.Context(r.surface,r.reserved),new ConstantFields{moisture=r.moisture,light=r.light});BatchFiles.SaveInstances(r.id,result.instances.ToArray(),result.diagnostics);}
    }
}
