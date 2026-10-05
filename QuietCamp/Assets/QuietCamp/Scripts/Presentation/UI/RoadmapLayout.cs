using QuietCamp.Domain;
using QuietCamp.Infrastructure;

namespace QuietCamp.Presentation.UI
{
    /// <summary>Shared geometry for art, touch targets and initial scroll. Bonus gaps never renumber the campaign.</summary>
    public static class RoadmapLayout
    {
        public const int BonusStep=500;
        public static float MainY(int index)
        {
            int gaps=0;foreach(var slot in BonusCampCatalog.Slots)if(slot.afterLevel<=index)gaps++;
            return index*RoadmapGraphic.Step+RoadmapGraphic.CentreY+gaps*BonusStep;
        }
        public static float BonusY(BonusCampDefinition slot) => MainY(slot.afterLevel-1)+RoadmapGraphic.Step;
        public static float BonusX(BonusCampDefinition slot) => (slot.afterLevel/10)%2==1?.25f:.75f;
        public static float Height(int count)
        {
            int gaps=0;foreach(var slot in BonusCampCatalog.Slots)if(slot.afterLevel<=count)gaps++;
            return count*RoadmapGraphic.Step+200+gaps*BonusStep;
        }
    }
}
