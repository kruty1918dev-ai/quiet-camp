using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Unity;
using Kruty1918.ScenePlacement.EditorTools;
namespace Kruty1918.ScenePlacement.Tests
{
    public sealed class NativeContracts
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        T Own<T>(T value) where T:UnityEngine.Object{owned.Add(value);return value;}
        [TearDown] public void Release(){for(int i=owned.Count-1;i>=0;i--)UnityBridge.Release(owned[i]);owned.Clear();}
        static MeshData Model(){var b=new GeometryBuilder();b.Box(new V3(0,.5f,0),new V3(1,1,1),new Tint(.8f,.7f,.5f));return b.Build();}
        static AssetShape Shape(string id,float halfY,float halfXZ)=>new AssetShape{id=id,contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,halfY,0),new V3(halfXZ,halfY,halfXZ))}};
        [Test] public void StaticMeshRoundTripPreservesUvColorsAndSubmeshes()
        {
            var data=Model();data.materials=Enumerable.Range(0,data.TriangleCount).Select(t=>t%2).ToArray();data.channels=new[]{new VertexChannel{name="uv1",components=4,values=Enumerable.Repeat(.25f,data.positions.Length*4).ToArray()}};
            var mesh=Own(UnityBridge.CreateMesh(data));var read=StaticMeshSnapshot.Read(mesh);
            Assert.AreEqual(data.TriangleCount,read.TriangleCount);Assert.AreEqual(2,mesh.subMeshCount);Assert.AreEqual(data.positions.Length,read.positions.Length);Assert.IsTrue(read.channels.Single(c=>c.name=="uv1").values.All(v=>v==.25f));Assert.AreEqual(data.colors[0].r,read.colors[0].r);
        }
        [Test] public void NativeGeneratedObjectsHaveExplicitOwnership()
        {
            var parent=Own(new GameObject("test-parent"));var root=Own(UnityBridge.CreateMeshObject(Model(),parent.transform,Array.Empty<Material>(),"generated"));Assert.AreEqual(parent.transform,root.transform.parent);Assert.IsNotNull(root.GetComponent<OwnedGeneratedMesh>());var mesh=root.GetComponent<MeshFilter>().sharedMesh;UnityBridge.Release(root);Assert.IsTrue(mesh==null);
        }
        [Test] public void MeshSurfaceSamplesSlopedGeometry()
        {
            var b=new GeometryBuilder();b.Quad(V3.Zero,new V3(0,0,2),new V3(2,.4f,2),new V3(2,.4f,0),Tint.White);var surface=new TriangleSurface(b.Build());Assert.IsTrue(surface.TrySample(new V3(1,0,1),-V3.Up,out var sample));Assert.AreEqual(.2f,sample.point.y,.0001f);
        }
    }
}
