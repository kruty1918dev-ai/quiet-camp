using System;
using UnityEngine;
using Kruty1918.ScenePlacement.Unity;
namespace Kruty1918.VegetationPlacement.Unity
{
    public static class VegetationAdapter
    {
        public static GameObject Create(VegetationResult result,Transform parent,Func<string,GameObject> prefabs)
        {if(!result.Ok)throw new InvalidOperationException("Invalid vegetation cannot be instantiated");return UnityBridge.CreateInstances(result.instances,parent,prefabs);}
    }
}
