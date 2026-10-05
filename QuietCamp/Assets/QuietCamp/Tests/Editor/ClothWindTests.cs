using NUnit.Framework;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class ClothWindTests
    {
        [TestCase(0)] [TestCase(90)] [TestCase(225)]
        public void ClothFollowsWorldWindUnderRotatedAndScaledTentWhileGroundAndRidgeStayPinned(float yaw)
        {
            var root=new GameObject("cloth wind");
            try
            {
                root.transform.rotation=Quaternion.Euler(0,yaw,0);root.transform.localScale=new Vector3(2,3,1.5f);
                var bounds=new Bounds(new Vector3(0,.5f,0),Vector3.one);
                var p=new Vector3(.2f,.5f,.1f);var w=root.transform.TransformPoint(p);
                var moved=root.transform.TransformPoint(TentCloth.DeformVertex(p,bounds,root.transform,Vector2.right,.35f,1.1f))-w;
                Assert.Greater(Mathf.Abs(moved.x),.001f);Assert.LessOrEqual(Mathf.Abs(moved.z),Mathf.Abs(moved.x)*.12f+.00001f,"Height-dependent direction must remain a small coherent deviation");
                foreach(var pin in new[]{new Vector3(0,0,0),new Vector3(0,1,0)})
                    Assert.Less((TentCloth.DeformVertex(pin,bounds,root.transform,Vector2.right,1,1.1f)-pin).sqrMagnitude,.00000001f);
                Assert.AreEqual(p,TentCloth.DeformVertex(p,bounds,root.transform,Vector2.right,0,1.1f));
            }
            finally{Object.DestroyImmediate(root);}
        }
    }
}
