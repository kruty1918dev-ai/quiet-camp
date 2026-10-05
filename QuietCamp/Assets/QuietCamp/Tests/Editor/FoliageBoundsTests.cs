using NUnit.Framework;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class FoliageBoundsTests
    {
        [TestCase(0)] [TestCase(90)] [TestCase(225)]
        public void WindEnvelopeSurvivesNarrowScaleAndReregistration(float yaw)
        {
            var root=new GameObject("narrow plant",typeof(MeshFilter),typeof(MeshRenderer));
            var mesh=new Mesh();
            try
            {
                mesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,2,.5f)};
                mesh.RecalculateBounds();root.GetComponent<MeshFilter>().sharedMesh=mesh;
                root.transform.localScale=new Vector3(.2f,3,.3f);root.transform.rotation=Quaternion.Euler(0,yaw,0);
                var renderer=root.GetComponent<MeshRenderer>();var original=mesh.bounds;
                CampFoliageResponse.ExpandBounds(renderer);var envelope=renderer.localBounds;
                CampFoliageResponse.ExpandBounds(renderer);Assert.AreEqual(envelope,renderer.localBounds);
                Assert.AreEqual(original,mesh.bounds,"Wind must not mutate the shared mesh bounds");
                foreach(var vertex in mesh.vertices)foreach(var direction in new[]{Vector3.right,Vector3.forward,(Vector3.right+Vector3.forward).normalized})
                {
                    var displacement=direction*2.1f-Vector3.up*.63f;
                    var moved=vertex+root.transform.InverseTransformVector(displacement);
                    Assert.IsTrue(envelope.Contains(moved),"Culling would clip a wind-bent plant at yaw "+yaw);
                }
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
        }
    }
}
