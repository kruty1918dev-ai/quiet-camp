using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public class PerformanceCacheTests
    {
        [Test] public void CampaignOrderIsReadOnlyAndStillMatchesContent()
        {
            var ids = LevelLoader.MvpLevelIds();
            Assert.AreEqual(110, ids.Count);
            Assert.AreEqual("QC001", ids[0]);
            Assert.IsTrue(((System.Collections.Generic.ICollection<string>)ids).IsReadOnly);
            Assert.Throws<System.NotSupportedException>(() => ((System.Collections.Generic.IList<string>)ids)[0] = "changed");
            foreach (var id in ids) Assert.IsTrue(LevelLoader.Exists(id), id);
        }
        static float ReferenceDistance(QuietCamp.Domain.LevelData level, Vector3 position)
        {
            float nearest = float.PositiveInfinity;
            foreach (var cell in level.ruleVersion == 2 ? level.exteriorWalkable ?? System.Array.Empty<int[]>() : System.Array.Empty<int[]>())
            {
                if (cell?.Length != 2) continue;
                var at = BoardMath.CellCenterWorld(level, new QuietCamp.Domain.Cell(cell[0], cell[1]));
                nearest = Mathf.Min(nearest, Mathf.Max(Mathf.Abs(position.x-at.x), Mathf.Abs(position.z-at.z))-.52f);
            }
            foreach (var cell in QuietCamp.Domain.CampAccess.Points(level))
            {
                var previous = CampTrail.Centre(level, cell, 0);
                for (int i=1;i<=12;i++)
                {
                    var next = CampTrail.Centre(level, cell, CampTrail.Length*i/12f);
                    var delta=next-previous;delta.y=0;var offset=position-previous;offset.y=0;
                    float t=Mathf.Clamp01(Vector3.Dot(offset,delta)/Mathf.Max(.001f,delta.sqrMagnitude));
                    nearest=Mathf.Min(nearest,(offset-delta*t).magnitude-.48f);previous=next;
                }
            }
            return nearest;
        }
        [Test] public void CorridorCachePreservesClearanceAndObservesInPlaceEdits()
        {
            var level=LevelLoader.Load("QC003");
            void Compare()
            {
                for (int z=-9;z<=9;z++) for(int x=-9;x<=9;x++)
                {
                    var p=new Vector3(x*.73f,0,z*.81f);
                    Assert.AreEqual(ReferenceDistance(level,p),CampTrail.CorridorDistance(level,p),.00001f);
                }
            }
            Compare();
            level.decorSeed += 137; level.entry[0] = 0; Compare();
            if (level.exteriorWalkable?.Length > 0) { level.exteriorWalkable[0][0] -= 2; Compare(); }
        }
    }
}
