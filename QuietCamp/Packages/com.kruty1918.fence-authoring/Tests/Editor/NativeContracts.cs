using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Unity;
using Kruty1918.FenceAuthoring;
using Kruty1918.FenceAuthoring.Unity;
namespace Kruty1918.FenceAuthoring.Tests
{
    public sealed class NativeContracts
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        T Own<T>(T value) where T:UnityEngine.Object{owned.Add(value);return value;}
        [TearDown] public void Release(){for(int i=owned.Count-1;i>=0;i--)UnityBridge.Release(owned[i]);owned.Clear();}
        static MeshData Model(){var b=new GeometryBuilder();b.Box(new V3(0,.5f,0),new V3(1,1,1),new Tint(.8f,.7f,.5f));return b.Build();}
        static AssetShape Shape(string id,float halfY,float halfXZ)=>new AssetShape{id=id,contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,halfY,0),new V3(halfXZ,halfY,halfXZ))}};
        [Test] public void NativeFenceMatchesCoreSocketsAndTerrain()
        {
            var post=Shape("post",.5f,.06f);var section=new AssetShape{id="rail",contacts=new[]{V3.Zero},volumes=new[]{new Box(V3.Zero,new V3(.75f,.18f,.035f))}};
            var result=FenceGenerator.Generate(new FenceSpec{id="foreign-farm",jitter=0,lean=0,contour=new[]{V3.Zero,new V3(6,0,0)}},new FenceKit{post=post,section=section},new PlacementContext(new PlaneSurface()));
            Assert.IsTrue(result.Ok,string.Join(";",result.diagnostics));var prefab=Own(new GameObject("foreign-part"));var root=Own(FenceAdapter.Create(result,null,_=>prefab));Assert.AreEqual(result.instances.Count,root.transform.childCount);
            foreach(var instance in result.instances){var go=root.GetComponentsInChildren<Transform>().Single(t=>t.parent==root.transform&&t.name==instance.id);Assert.AreEqual(UnityBridge.Native(instance.pose.position),go.localPosition);}
        }
    }
}
