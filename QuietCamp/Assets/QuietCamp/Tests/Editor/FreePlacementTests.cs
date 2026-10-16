using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class FreePlacementTests
    {
        static IEnumerable<Placement[]> Layouts(LevelData level)
        {
            for(int ax=0;ax<level.width-1;ax++)for(int az=0;az<level.height-1;az++)
            for(int bx=0;bx<level.width-1;bx++)for(int bz=0;bz<level.height-1;bz++)
            for(int ar=0;ar<4;ar++)for(int br=0;br<4;br++)
            {
                var poses=new[]{new Placement{guestId=level.guests[0].id,x=ax,z=az,rotation=ar},
                    new Placement{guestId=level.guests[1].id,x=bx,z=bz,rotation=br}};
                if(RuleEvaluator.Evaluate(level,poses).IsSolved)yield return poses;
            }
        }

        [Test]
        public void FirstGladeHasManyDifferentFootprintsNotJustDifferentRotations()
        {
            var level=LevelLoader.Load("QC001");Assert.AreEqual(2,level.guests.Length);
            var footprints=new HashSet<string>();
            foreach(var layout in Layouts(level))
                footprints.Add(string.Join("/",layout.Select(p=>p.x+","+p.z)));
            Assert.GreaterOrEqual(footprints.Count,10,"The opening must allow players to arrange their own camp.");
        }

        [Test]
        public void NormalSessionAcceptsSolutionsThatDifferFromTheWitness()
        {
            var level=LevelLoader.Load("QC001");
            var alternatives=Layouts(level).Where(poses=>poses.Any(p=>!level.witness.Any(w=>w.guestId==p.guestId&&w.x==p.x&&w.z==p.z&&w.rotation==p.rotation))).Take(4).ToArray();
            Assert.AreEqual(4,alternatives.Length);
            foreach(var poses in alternatives)
            {
                using var session=new CampSession(level);
                foreach(var p in poses)Assert.IsTrue(session.TryCommit(PlacementCommand.Place(p.guestId,p.x,p.z,p.rotation),out _));
                Assert.IsTrue(session.Check().IsSolved);
            }
        }

        [Test]
        public void ReservedEntranceIsTheOnlyInvisibleObstacleOnTheOpeningGlade()
        {
            var level=LevelLoader.Load("QC001");Assert.IsEmpty(level.blocked);
            var entrance=new Cell(level.entry[0],level.entry[1]);Assert.IsTrue(CampAccess.IsReserved(level,entrance));
            var p=new Placement{guestId=level.guests[0].id,x=System.Math.Min(entrance.X,level.width-2),z=System.Math.Min(entrance.Z,level.height-2)};
            var report=RuleEvaluator.Evaluate(level,new[]{p},false);
            Assert.IsFalse(report.CanCommit);
            Assert.IsTrue(report.Issues.Any(i=>i.Code=="overlap"&&i.Cells.Contains(entrance)));
        }

        [Test]
        public void EntranceIsMarkedDuringPlacementAndHiddenDuringOrdinaryPlay()
        {
            var level=LevelLoader.Load("QC001");var root=new GameObject("Entrance guidance test");
            BoardRenderer renderer=null;
            try
            {
                renderer=new BoardRenderer(level,null,root.transform,root.transform,root.transform,root.transform,root.transform);
                var entrance=root.transform.Find("CampEntrances");Assert.NotNull(entrance);Assert.IsFalse(entrance.gameObject.activeSelf);
                using var session=new CampSession(level);
                renderer.ShowPlacementGuidance(session,level.guests[0].id,0);
                Assert.IsTrue(entrance.gameObject.activeSelf);
                Assert.Greater(entrance.GetComponentsInChildren<MeshRenderer>().Length,0);
                Assert.IsTrue(root.transform.Find("ReservedEntrance").gameObject.activeSelf);
                renderer.HidePlacementGuidance();
                Assert.IsFalse(entrance.gameObject.activeSelf);
                Assert.IsFalse(root.transform.Find("ReservedEntrance").gameObject.activeSelf);
            }
            finally { renderer?.ClearAll();Object.DestroyImmediate(root); }
        }

        [Test]
        public void AValidCampFinishesTheLessonWithoutForcedRotationOrUndo()
        {
            var curriculum=LearningCurriculum.Read(Resources.Load<TextAsset>("QuietCamp/learning_curriculum").text);
            var guide=new TutorialDirector(null,false,new ProgressionService(),()=>true,curriculum);
            foreach(var id in new[]{"QC001","QC002"})
            {
                Assert.IsTrue(guide.CanCompleteLevel(id));
                using var session=new CampSession(LevelLoader.Load(id));
                session.DebugApplyWitness();Assert.IsTrue(session.Check().IsSolved);
                Assert.IsTrue(guide.ReportAction("complete",session));
                Assert.AreNotEqual(id,guide.CurrentLevelId);
            }
            Assert.AreEqual("QC003",guide.CurrentLevelId);
        }
    }
}
