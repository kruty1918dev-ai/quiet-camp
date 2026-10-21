using NUnit.Framework;
using QuietCamp.Editor.LevelAuthoring.WorldOps;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Tests
{
    public sealed class WorldGeometryCensusTests
    {
        static Mesh Mesh()
        {
            var mesh=new Mesh{vertices=new[]{new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(0,1,0)}};
            mesh.subMeshCount=2;mesh.SetTriangles(new[]{0,1,2},0);mesh.SetTriangles(new[]{0,2,1},1);mesh.RecalculateBounds();return mesh;
        }
        [Test] public void CountsEverySubmeshWithoutRequiringReadableVertexData()
        {
            var mesh=Mesh();
            try{mesh.UploadMeshData(true);Assert.IsFalse(mesh.isReadable);Assert.AreEqual(2,WorldOpsGeometryCensus.Triangles(mesh));}
            finally{Object.DestroyImmediate(mesh);}
        }
        [Test] public void SharedInstancesAndInactiveGeometryRemainInTheSceneBudget()
        {
            var root=new GameObject("Census QA");var mesh=Mesh();
            try
            {
                var camera=new GameObject("Camera").AddComponent<Camera>();camera.transform.SetParent(root.transform);camera.transform.position=new Vector3(0,0,-10);
                MeshRenderer Renderer(string name)
                {
                    var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform);
                    go.GetComponent<MeshFilter>().sharedMesh=mesh;return go.GetComponent<MeshRenderer>();
                }
                var first=Renderer("Visible");first.shadowCastingMode=ShadowCastingMode.On;
                var second=Renderer("Inactive");second.gameObject.SetActive(false);
                var before=mesh.vertices;var result=WorldOpsGeometryCensus.Capture(camera,new Renderer[]{first,second});
                Assert.AreEqual(4,result.sceneMeshTriangles);Assert.AreEqual(2,result.uniqueMeshTriangles);
                Assert.AreEqual(2,result.activeMeshTriangles);Assert.AreEqual(2,result.activeShadowCasterTriangles);
                Assert.AreEqual(2,result.frustumCandidateTriangles);Assert.AreEqual(2,result.indexedMeshInstances);
                Assert.IsFalse(second.gameObject.activeSelf);CollectionAssert.AreEqual(before,mesh.vertices);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
        }
        [Test] public void DisabledSkinnedTopologyIsCountedWithoutBakingOrEnablingIt()
        {
            var root=new GameObject("Disabled skin");var mesh=Mesh();
            try
            {
                var renderer=root.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.enabled=false;
                var result=WorldOpsGeometryCensus.Capture(null,new Renderer[]{renderer});
                Assert.AreEqual(2,result.sceneMeshTriangles);Assert.AreEqual(0,result.activeMeshTriangles);
                Assert.IsFalse(renderer.enabled);Assert.AreSame(mesh,renderer.sharedMesh);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(mesh);}
        }
    }
}
