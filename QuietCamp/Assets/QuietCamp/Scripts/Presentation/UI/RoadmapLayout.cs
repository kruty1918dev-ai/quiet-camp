using System;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Compatibility facade. Production layout is owned by the authored region catalog.</summary>
    public static class RoadmapLayout
    {
        public const int BonusStep=500;
        public static float MainY(int index)=>RoadmapRepository.Main.Y(index);
        public static float BonusY(BonusCampDefinition slot)
        {
            var data=RoadmapRepository.Main;
            for(int r=0;r<data.Definition.regions.Length;r++)foreach(var branch in data.Definition.regions[r].branches)
                if(branch.bonusId==slot.id)return data.RegionStarts[r]+branch.y;
            throw new ArgumentException("Missing bonus branch: "+slot.id);
        }
        public static float BonusX(BonusCampDefinition slot)
        {
            foreach(var region in RoadmapRepository.Main.Definition.regions)foreach(var branch in region.branches)if(branch.bonusId==slot.id)return branch.x;
            throw new ArgumentException("Missing bonus branch: "+slot.id);
        }
        public static float Height(int count)
        {var data=RoadmapRepository.Main;if(count!=data.Nodes.Length)throw new ArgumentException("Use a dedicated catalog for this route");return data.Height;}
    }
}
