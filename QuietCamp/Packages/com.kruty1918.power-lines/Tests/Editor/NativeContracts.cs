using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Unity;
using Kruty1918.PowerLines;
using Kruty1918.PowerLines.Unity;
namespace Kruty1918.PowerLines.Tests
{
    public sealed class NativeContracts
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        T Own<T>(T value) where T:UnityEngine.Object{owned.Add(value);return value;}
        [TearDown] public void Release(){for(int i=owned.Count-1;i>=0;i--)UnityBridge.Release(owned[i]);owned.Clear();}
        static MeshData Model(){var b=new GeometryBuilder();b.Box(new V3(0,.5f,0),new V3(1,1,1),new Tint(.8f,.7f,.5f));return b.Build();}
        static AssetShape Shape(string id,float halfY,float halfXZ)=>new AssetShape{id=id,contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,halfY,0),new V3(halfXZ,halfY,halfXZ))}};
        [Test] public void NetworkCreatesForeignSupportsAndStaticCables()
        {
            var kit=new SupportKit{standing=Shape("pole",1.5f,.06f),sockets=new[]{new V3(0,2.8f,0)}};var spec=new PowerSpec{id="foreign-network",route=new RoutePath(new[]{V3.Zero,new V3(0,0,16)})};
            var result=PowerGenerator.Generate(spec,kit,new PlacementContext(new PlaneSurface()));Assert.IsTrue(result.Ok,string.Join(";",result.diagnostics));var prefab=Own(new GameObject("foreign-pole"));var root=Own(PowerAdapter.Create(result,null,_=>prefab,Array.Empty<Material>()));Assert.AreEqual(result.supports.Count+result.spans.Count,root.transform.childCount);Assert.AreEqual(result.spans.Count,root.GetComponentsInChildren<OwnedGeneratedMesh>().Length);
        }
    }
}
