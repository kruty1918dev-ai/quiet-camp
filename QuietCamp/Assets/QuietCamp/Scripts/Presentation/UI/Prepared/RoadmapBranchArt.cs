using QuietCamp.Domain;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using UnityEngine;
namespace QuietCamp.Presentation.UI.Prepared
{
    public static class RoadmapBranchArt
    {
        public static RoadmapSceneGenerator.Scene Create(RoadmapBranchData branch,RoadmapSceneGenerator.Scene reusable=null)
        {
            var summary=new LevelSummary{id=branch.id,width=3,height=3,decorSeed=193,lighting=branch.lighting??(branch.visualIdentity=="fireflies"?"night":"day"),environmentPreset=branch.biome??"meadow",environment=new EnvironmentCompositionData{seasonId=branch.season??"summer",weatherId="calm"}};
            var scene=RoadmapSceneGenerator.FromBaked(summary,branch.world,AtmosphereCatalog.Load(),reusable:reusable);
            var landmark=scene.TakeProp(RoadmapBranchPolicy.Landmark(branch),Vector3.zero,branch.visualIdentity=="fireflies"?.32f:branch.visualIdentity=="lighthouse"?5:branch.visualIdentity=="station"||branch.visualIdentity=="garden"?3:1.2f,0,false);
            scene.Props.Sort((a,b)=>(b.Position.x+b.Position.z).CompareTo(a.Position.x+a.Position.z));scene.MeasureExtent();return scene;
        }
    }
}
