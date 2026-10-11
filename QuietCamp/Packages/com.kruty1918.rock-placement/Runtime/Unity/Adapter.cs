using System;
using UnityEngine;
using Kruty1918.ScenePlacement.Unity;
namespace Kruty1918.RockPlacement.Unity
{
    public static class RockAdapter
    {
        public static GameObject Create(RockResult result,Transform parent,Func<string,GameObject> prefabs)
        {if(!result.Ok)throw new InvalidOperationException("Invalid rock group cannot be instantiated");return UnityBridge.CreateInstances(result.instances,parent,prefabs);}
    }
}
