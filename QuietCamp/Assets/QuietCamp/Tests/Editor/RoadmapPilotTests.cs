using System.Collections.Generic;
using NUnit.Framework;
using QuietCamp.Application;

namespace QuietCamp.Tests
{
    public sealed class RoadmapPilotTests
    {
        [TestCase(0,0)][TestCase(1,1)][TestCase(4,4)][TestCase(5,4)][TestCase(18,4)]
        public void FrontierComesFromCompletedLevelsAndNeverLeavesThePilot(int count,int expected)
        {
            var completed=new HashSet<string>();
            for(int i=1;i<=count;i++)completed.Add("QC"+i.ToString("000"));
            Assert.AreEqual(expected,RoadmapPilotPolicy.Frontier(completed.Contains));
            for(int i=0;i<5;i++)Assert.AreEqual(i<=expected,RoadmapPilotPolicy.CanPlay(RoadmapPilotPolicy.LevelIds[i],completed.Contains,id=>true));
            Assert.IsFalse(RoadmapPilotPolicy.CanPlay("QC006",completed.Contains,id=>true));
            Assert.AreEqual(count,completed.Count,"Presentation must not trim older progress");
        }
        [Test] public void PilotRespectsAccessAndFinishesOnTheFifthPlace()
        {
            Assert.IsFalse(RoadmapPilotPolicy.CanPlay("QC001",id=>false,id=>false));
            Assert.IsFalse(RoadmapPilotPolicy.Finished(id=>id!="QC005"));
            Assert.IsTrue(RoadmapPilotPolicy.Finished(id=>true));
            Assert.AreEqual(-1,RoadmapPilotPolicy.Index("gen:qc_camp:18"));
            CollectionAssert.AreEqual(new[]{"QC001","QC002","QC003","QC004","QC005"},RoadmapPilotPolicy.LevelIds);
        }
    }
}
