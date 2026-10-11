using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Unity;
using Kruty1918.RoadAuthoring;
using Kruty1918.RoadAuthoring.Unity;
namespace Kruty1918.RoadAuthoring.Tests
{
    public sealed class NativeContracts
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        T Own<T>(T value) where T:UnityEngine.Object{owned.Add(value);return value;}
        [TearDown] public void Release(){for(int i=owned.Count-1;i>=0;i--)UnityBridge.Release(owned[i]);owned.Clear();}
        static MeshData Model(){var b=new GeometryBuilder();b.Box(new V3(0,.5f,0),new V3(1,1,1),new Tint(.8f,.7f,.5f));return b.Build();}
        static AssetShape Shape(string id,float halfY,float halfXZ)=>new AssetShape{id=id,contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,halfY,0),new V3(halfXZ,halfY,halfXZ))}};
        [Test] public void RoadUsesMeshSurfaceAndNativeOwnedOutput()
        {
            var b=new GeometryBuilder();b.Quad(new V3(-4,0,-4),new V3(-4,0,8),new V3(4,.4f,8),new V3(4,.4f,-4),Tint.White);var surface=new TriangleSurface(b.Build());var spec=new RoadSpec{id="foreign-path",points=new[]{V3.Zero,new V3(0,0,6)},bands=new[]{new SurfaceBand{width=1}}};var result=RoadGenerator.Generate(spec,surface);Assert.IsTrue(result.Ok);
            var root=Own(RoadAdapter.Create(result,null,Array.Empty<Material>()));Assert.AreEqual(result.mesh.TriangleCount,root.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);var context=new PlacementContext(surface);RoadAdapter.Reserve(spec.id,result,context);Assert.AreEqual(result.corridor.Count,context.ReservationCount);
        }
    }
}
