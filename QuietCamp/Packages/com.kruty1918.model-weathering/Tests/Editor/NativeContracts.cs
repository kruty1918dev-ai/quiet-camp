using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Unity;
using Kruty1918.ModelWeathering;
using Kruty1918.ModelWeathering.Unity;
namespace Kruty1918.ModelWeathering.Tests
{
    public sealed class NativeContracts
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        T Own<T>(T value) where T:UnityEngine.Object{owned.Add(value);return value;}
        [TearDown] public void Release(){for(int i=owned.Count-1;i>=0;i--)UnityBridge.Release(owned[i]);owned.Clear();}
        static MeshData Model(){var b=new GeometryBuilder();b.Box(new V3(0,.5f,0),new V3(1,1,1),new Tint(.8f,.7f,.5f));return b.Build();}
        static AssetShape Shape(string id,float halfY,float halfXZ)=>new AssetShape{id=id,contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,halfY,0),new V3(halfXZ,halfY,halfXZ))}};
        [Test] public void AgingCreatesSeparateNativeGeometry()
        {
            var source=Own(UnityBridge.CreateMesh(Model()));var before=source.vertices;var result=WeatheringAdapter.Apply("foreign-bench",source,new WeatheringProfile{intensity=.8f,seed=7});var output=Own(WeatheringAdapter.Create(result));Assert.AreNotSame(source,output);CollectionAssert.AreEqual(before,source.vertices);Assert.AreEqual(source.vertexCount,output.vertexCount);Assert.Greater(result.growth.Count,0);Assert.IsTrue(result.mesh.colors.Any(c=>c.r<.9f));
        }
    }
}
