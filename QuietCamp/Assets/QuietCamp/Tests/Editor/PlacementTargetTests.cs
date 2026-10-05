using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests.Editor
{
    public class PlacementTargetTests
    {
        [TestCase(0,0,.25f,.35f)]
        [TestCase(4,0,1.75f,.30f)]
        [TestCase(0,6,.25f,1.75f)]
        [TestCase(4,6,1.70f,1.80f)]
        public void ObliqueTouchPreservesContinuousGrabAtEveryBoardCorner(int x,int z,float grabX,float grabZ)
        {
            var go=new GameObject("Oblique target camera");
            try
            {
                var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=12;
                camera.transform.position=new Vector3(11,17,13);camera.transform.LookAt(Vector3.zero);
                var level=new LevelData{width=6,height=8};
                var centre=BoardMath.TentCenter(level,x,z);
                var grab=new Vector2(grabX,grabZ);
                var projector=new PlacementTargetProjector(camera,level);projector.Begin(true,grab,centre);
                var grabbed=centre+new Vector3(grabX-1,-centre.y,grabZ-1);
                var aim=(Vector2)camera.WorldToScreenPoint(grabbed);
                var pointer=aim-Vector2.up*projector.LiftPixels;
                Assert.IsTrue(projector.TryProject(pointer,true,null,out var target));
                Assert.AreEqual(new Cell(x,z),target.Anchor);
                Assert.That(Vector3.Distance(target.GroundPoint,grabbed),Is.LessThan(.001f));
                var lift=projector.LiftPixels;
                var destination=grabbed+Vector3.right*.7f;
                var moved=(Vector2)camera.WorldToScreenPoint(destination)-Vector2.up*lift;
                Assert.IsTrue(projector.TryProject(moved,true,null,out var next));
                Assert.AreEqual(new Cell(x+1,z),next.Anchor,"No nearest-valid-cell adjustment at the edge");
                Assert.AreEqual(lift,projector.LiftPixels,"Offset stays fixed for the whole gesture");
                Assert.IsFalse(projector.TryProject(new Vector2(Screen.width*.5f,Screen.height-1),true,null,out _),
                    "A lifted target outside safe area cancels rather than snapping back to the last cell");
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test]
        public void TouchAndMouseUseOneProjectionWithoutMovingCamera()
        {
            var go=new GameObject("Target camera");
            try
            {
                var camera=go.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=6;
                camera.transform.SetPositionAndRotation(Vector3.up*20,Quaternion.Euler(90,0,0));
                var level=new LevelData{width=6,height=8};
                var centre=BoardMath.TentCenter(level,2,3);var projector=new PlacementTargetProjector(camera,level);
                projector.Begin(true,Vector2.one,centre);
                var aim=(Vector2)camera.WorldToScreenPoint(centre);
                var pointer=aim-Vector2.up*projector.LiftPixels;
                Assert.IsTrue(projector.TryProject(pointer,true,null,out var lifted));
                Assert.AreEqual(new Cell(2,3),lifted.Anchor);
                Assert.That(Vector2.Distance(lifted.AimScreen,aim),Is.LessThan(.01f));
                Assert.IsFalse(projector.TryProject(new Vector2(-1,-1),true,null,out _));
                Assert.IsFalse(projector.TryProject(new Vector2(float.NaN,0),true,null,out _));
                projector.Begin(false,Vector2.one,centre);
                Assert.AreEqual(0,projector.LiftPixels);
                Assert.IsTrue(projector.TryProject(aim,true,null,out var mouse));
                Assert.AreEqual(lifted.Anchor,mouse.Anchor);
                Assert.AreEqual(Vector3.up*20,camera.transform.position);
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
