using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Unity;
using Kruty1918.RockPlacement;
using Kruty1918.RockPlacement.Unity;
namespace Kruty1918.RockPlacement.Tests
{
    public sealed class NativeContracts
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        T Own<T>(T value) where T:UnityEngine.Object{owned.Add(value);return value;}
        [TearDown] public void Release(){for(int i=owned.Count-1;i>=0;i--)UnityBridge.Release(owned[i]);owned.Clear();}
        static MeshData Model(){var b=new GeometryBuilder();b.Box(new V3(0,.5f,0),new V3(1,1,1),new Tint(.8f,.7f,.5f));return b.Build();}
        static AssetShape Shape(string id,float halfY,float halfXZ)=>new AssetShape{id=id,contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,halfY,0),new V3(halfXZ,halfY,halfXZ))}};
        [Test] public void GeologyPlacesForeignPrefabsOnSampledSlope()
        {
            var spec=new RockSpec{id="foreign-cliff",rocks=new[]{Shape("stone",.2f,.2f)},groups=new[]{new RockGroup{id="outcrop",count=4,radius=2}}};var result=RockGenerator.Generate(spec,new PlacementContext(new PlaneSurface(V3.Zero,new V3(-.1f,1,0))));Assert.IsTrue(result.Ok,string.Join(";",result.diagnostics));var prefab=Own(new GameObject("foreign-rock"));var root=Own(RockAdapter.Create(result,null,_=>prefab));Assert.AreEqual(result.instances.Count,root.transform.childCount);Assert.Less(result.instances[0].pose.up.x,0);
        }
    }
}
