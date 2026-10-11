using System;
using UnityEngine;
using Kruty1918.ScenePlacement.Unity;
namespace Kruty1918.FenceAuthoring.Unity
{
    public static class FenceAdapter
    {
        public static GameObject Create(FenceResult result,Transform parent,Func<string,GameObject> prefabs)
        {if(!result.Ok)throw new InvalidOperationException("Invalid fence cannot be instantiated");return UnityBridge.CreateInstances(result.instances,parent,prefabs);}
    }
}
