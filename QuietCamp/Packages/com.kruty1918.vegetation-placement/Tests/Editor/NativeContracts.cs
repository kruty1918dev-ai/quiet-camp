using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Unity;
using Kruty1918.VegetationPlacement;
using Kruty1918.VegetationPlacement.Unity;
using Pose = Kruty1918.ScenePlacement.Pose;
namespace Kruty1918.VegetationPlacement.Tests
{
    public sealed class NativeContracts
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        T Own<T>(T value) where T:UnityEngine.Object{owned.Add(value);return value;}
        [TearDown] public void Release(){for(int i=owned.Count-1;i>=0;i--)UnityBridge.Release(owned[i]);owned.Clear();}
        static MeshData Model(){var b=new GeometryBuilder();b.Box(new V3(0,.5f,0),new V3(1,1,1),new Tint(.8f,.7f,.5f));return b.Build();}
        static AssetShape Shape(string id,float halfY,float halfXZ)=>new AssetShape{id=id,contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,halfY,0),new V3(halfXZ,halfY,halfXZ))}};
        [Test] public void EcologyPlacesForeignPlantsAndProtectsReservedBuilding()
        {
            var context=new PlacementContext(new PlaneSurface());var building=new OrientedBox(new Box(new V3(0,1,0),new V3(1,1,1)),Pose.At(V3.Zero));context.Reserve("foreign-building",building);
            var spec=new VegetationSpec{id="foreign-forest",zone=new Box(V3.Zero,new V3(4,1,4)),density=1,patchContrast=0,species=new[]{new Species{id="grass",shape=Shape("plant",.1f,.05f),spacing=.15f}}};var result=VegetationGenerator.Generate(spec,context);Assert.IsTrue(result.Ok);Assert.Greater(result.instances.Count,0);Assert.IsTrue(result.instances.All(p=>p.volumes.All(v=>!v.Intersects(building))));var prefab=Own(new GameObject("foreign-plant"));var root=Own(VegetationAdapter.Create(result,null,_=>prefab));Assert.AreEqual(result.instances.Count,root.transform.childCount);
        }
    }
}
