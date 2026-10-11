using System;
using System.Linq;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.EditorTools;
namespace Kruty1918.PowerLines.EditorTools
{
    public static class Batch
    {
        [Serializable] public sealed class Recipe{public string id="power",networkKind="distribution";public V3[] points;public int seed;public float offset=1,minimumSpan=3,maximumSpan=8,scale=1,sag=.35f,radius=.012f;public int samples=12,sides=4;public int[] fallenSupports=Array.Empty<int>();public CableDamage[] damage=Array.Empty<CableDamage>();public SupportKit kit;public SurfaceRecipe surface=new SurfaceRecipe();public Reservation[] reserved=Array.Empty<Reservation>();}
        public static void Run(){var r=BatchFiles.Request<Recipe>();var spec=new PowerSpec{id=r.id,networkKind=r.networkKind,route=new RoutePath(r.points),seed=r.seed,offset=r.offset,minimumSpan=r.minimumSpan,maximumSpan=r.maximumSpan,scale=r.scale,sag=r.sag,radius=r.radius,samples=r.samples,sides=r.sides,fallenSupports=r.fallenSupports,damage=r.damage};var result=PowerGenerator.Generate(spec,r.kit,BatchFiles.Context(r.surface,r.reserved));BatchFiles.SaveBundle(r.id,result.spans.Select(s=>new BatchFiles.NamedMesh{id=s.id,data=s.mesh}).ToArray(),result.supports.ToArray(),result.diagnostics);}
    }
}
