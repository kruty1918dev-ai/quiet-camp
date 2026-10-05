using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using Newtonsoft.Json;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class TentLivingSpaceTests
    {
        static LevelData Level()=>new LevelData{width=6,height=6,ruleVersion=2,entry=new[]{0,0},blocked=Array.Empty<int[]>(),exteriorWalkable=Array.Empty<int[]>(),accessPoints=Array.Empty<AccessPointData>()};
        [TestCase(0)][TestCase(1)][TestCase(2)][TestCase(3)]
        public void ExteriorPackingPreservesDoorRoutesBoundsAndOtherBelongings(int rotation)
        {
            var level=Level();var p=new Placement{guestId="anna",x=2,z=2,rotation=rotation};
            var reservations=TentBelongingsLayout.Reservations(level,new[]{p});
            // Authored models are appreciably narrower than their 2x2 cells.
            var original=reservations.ToArray();var room=new TentLivingSpace(new Bounds(new Vector3(0,.66f,0),new Vector3(1.39f,1.32f,1.70f)));
            var items=TentBelongingsLayout.Find(level,p,room.Footprint(BoardMath.TentCenter(level,p.x,p.z),rotation),reservations);
            Assert.Greater(items.Count(r=>r.width>0),0,"A roomy clearing must not unnecessarily pack everything indoors");
            var field=new Rect(-3+.04f,-3+.04f,6-.08f,6-.08f);
            foreach(var item in items.Where(r=>r.width>0))Assert.IsTrue(TentBelongingsLayout.Safe(level,field,item,original));
            Assert.IsFalse(items[0].width>0&&items[1].width>0&&items[0].Overlaps(items[1]));
            var repeat=TentBelongingsLayout.Find(level,p,room.Footprint(BoardMath.TentCenter(level,p.x,p.z),rotation),new List<Rect>(original));
            CollectionAssert.AreEqual(items,repeat,"The same saved placement must restore the same packing");
        }
        [Test] public void CrowdedOrWetShoreNeverFallsBackToAnUnsafeExteriorPosition()
        {
            var level=Level();var p=new Placement{guestId="anna",x=0,z=0};
            var body=new Rect(-3,-3,2,2);
            var items=TentBelongingsLayout.Find(level,p,body,new List<Rect>{new Rect(-3,-3,6,6)});
            Assert.IsTrue(items.All(r=>r.width==0));
            level.environment=new EnvironmentCompositionData{shore=new ShorelineData{side="left",offset=1,width=2,seed=19}};
            var shore=level.environment.shore;var at=ShorelineGeometry.Point(shore,0,.5f);
            Assert.IsFalse(TentBelongingsLayout.Safe(level,new Rect(-3,-3,6,6),new Rect(at.x-.1f,at.z-.1f,.2f,.2f),Array.Empty<Rect>()));
            Assert.IsFalse(TentBelongingsLayout.Safe(level,new Rect(-3,-3,6,6),new Rect(2.9f,0,.2f,.2f),Array.Empty<Rect>()));
        }
        [Test] public void PackingEveryCampaignWitnessKeepsPuzzleDataAndEvaluatedRoutesUnchanged()
        {
            foreach(var summary in CampContent.Summaries)
            {
                var level=LevelLoader.Load(summary.id);var before=JsonConvert.SerializeObject(level);
                var report=RuleEvaluator.Evaluate(level,level.witness);Assert.IsTrue(report.IsSolved,summary.id);
                var occupied=TentBelongingsLayout.Reservations(level,level.witness);
                var field=new Rect(-level.width*.5f+.04f,-level.height*.5f+.04f,level.width-.08f,level.height-.08f);
                var room=new TentLivingSpace(new Bounds(new Vector3(0,.7f,0),new Vector3(2,1.4f,2)));
                foreach(var placement in level.witness.OrderBy(p=>p.guestId,StringComparer.Ordinal))
                {
                    var prior=occupied.ToArray();var items=TentBelongingsLayout.Find(level,placement,room.Footprint(BoardMath.TentCenter(level,placement.x,placement.z),placement.rotation),occupied);
                    foreach(var item in items.Where(r=>r.width>0))Assert.IsTrue(TentBelongingsLayout.Safe(level,field,item,prior),summary.id);
                }
                Assert.AreEqual(before,JsonConvert.SerializeObject(level));Assert.IsTrue(RuleEvaluator.Evaluate(level,level.witness).IsSolved);
            }
        }
        [TestCase(0)][TestCase(90)][TestCase(225)]
        public void PackedCornerOnlyPushesClothLocallyAndKeepsSeamsGroundAndRidgePinned(float yaw)
        {
            var bounds=new Bounds(new Vector3(0,.5f,0),Vector3.one);
            var matrix=Matrix4x4.TRS(Vector3.one,Quaternion.Euler(0,yaw,0),new Vector3(2,3,2));
            var centre=new Vector4(.30f,.24f,-.28f,.28f);var p=new Vector3(centre.x,centre.y,centre.z);
            var deformed=TentCloth.DeformVertex(p,bounds,matrix,matrix.inverse,Vector2.right,0,5,centre,1);
            Assert.Greater(deformed.x,p.x);Assert.Less(deformed.x-p.x,.08f);Assert.AreEqual(p.y,deformed.y);Assert.AreEqual(p.z,deformed.z);
            foreach(var fixedPoint in new[]{new Vector3(.3f,0,-.28f),new Vector3(.3f,1,-.28f),new Vector3(.5f,.24f,-.28f),new Vector3(-.5f,.24f,-.28f)})
                Assert.Less((TentCloth.DeformVertex(fixedPoint,bounds,matrix,matrix.inverse,Vector2.right,0,5,centre,1)-fixedPoint).magnitude,1e-6f);
            var remote=new Vector3(-.2f,.4f,.3f);
            Assert.AreEqual(remote,TentCloth.DeformVertex(remote,bounds,matrix,matrix.inverse,Vector2.right,0,5,centre,1));
        }
        [Test] public void MoistureDriesSlowlyAndSeasonControlsSnowAndLeafAccumulation()
        {
            var go=new GameObject("inactive surface-state test");go.SetActive(false);var cloth=go.AddComponent<TentCloth>();
            try
            {
                cloth.ConfigureSeason(new LevelData{environment=new EnvironmentCompositionData{seasonId="autumn",treeDensity=.8f}});
                cloth.AdvanceSurface(15,.8f,1,.5f);Assert.Greater(cloth.Wetness,.5f);Assert.AreEqual(0,cloth.SnowCover);Assert.Greater(cloth.LeafCover,0);
                float wet=cloth.Wetness;cloth.AdvanceSurface(1,0,0,0);Assert.Less(cloth.Wetness,wet);Assert.Greater(cloth.Wetness,wet-.02f);
                cloth.ConfigureSeason(new LevelData{environment=new EnvironmentCompositionData{seasonId="winter"}});
                cloth.AdvanceSurface(60,0,.8f,.2f);Assert.Greater(cloth.SnowCover,.4f);Assert.AreEqual(0,cloth.LeafCover);
                float settledSnow=cloth.SnowCover;cloth.AdvanceSurface(60,0,0,.3f);Assert.AreEqual(settledSnow,cloth.SnowCover,"A pause in snowfall must not melt a cold roof");
                cloth.AdvanceSurface(30,.8f,0,.2f);Assert.Less(cloth.SnowCover,settledSnow,"Rain should wash settled snow off");
                cloth.ConfigureSeason(new LevelData{environment=new EnvironmentCompositionData{seasonId="summer"}});
                cloth.AdvanceSurface(120,0,1,.5f);Assert.AreEqual(0,cloth.SnowCover);Assert.AreEqual(0,cloth.LeafCover);Assert.AreEqual(0,cloth.Wetness);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
    }
}
