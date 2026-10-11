using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Unity;
using Kruty1918.MeshStyle;
using Kruty1918.MeshStyle.Unity;
namespace Kruty1918.MeshStyle.Tests
{
    public sealed class NativeContracts
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        T Own<T>(T value) where T:UnityEngine.Object{owned.Add(value);return value;}
        [TearDown] public void Release(){for(int i=owned.Count-1;i>=0;i--)UnityBridge.Release(owned[i]);owned.Clear();}
        static MeshData Model(){var b=new GeometryBuilder();b.Box(new V3(0,.5f,0),new V3(1,1,1),new Tint(.8f,.7f,.5f));return b.Build();}
        static AssetShape Shape(string id,float halfY,float halfXZ)=>new AssetShape{id=id,contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,halfY,0),new V3(halfXZ,halfY,halfXZ))}};
        [Test] public void HarmonizationUsesForeignMeshWithoutMutatingIt()
        {
            var source=Own(UnityBridge.CreateMesh(Model()));var before=source.vertices;var result=MeshStyleAdapter.Harmonize(source,new StyleProfile{referenceTriangles=12},10000);var output=Own(MeshStyleAdapter.Create(result));Assert.AreNotSame(source,output);CollectionAssert.AreEqual(before,source.vertices);Assert.AreEqual(result.mesh.TriangleCount,output.triangles.Length/3);
        }
        [Test] public void ActualControlCameraDeterminesProjectedArea()
        {
            var model=Own(new GameObject("foreign-model"));var camera=Own(new GameObject("control-camera")).AddComponent<Camera>();camera.transform.position=new Vector3(0,.5f,-6);camera.transform.rotation=Quaternion.identity;var mesh=Own(UnityBridge.CreateMesh(Model()));
            float near=MeshStyleAdapter.ProjectedArea(mesh,model.transform,new[]{camera});camera.transform.position=new Vector3(0,.5f,-12);float far=MeshStyleAdapter.ProjectedArea(mesh,model.transform,new[]{camera});Assert.Greater(near,far);Assert.Greater(far,0);
        }
    }
}
