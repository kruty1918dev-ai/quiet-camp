using System;
using UnityEngine;
using Kruty1918.ScenePlacement.Unity;
namespace Kruty1918.PowerLines.Unity
{
    public static class PowerAdapter
    {
        public static GameObject Create(PowerResult result,Transform parent,Func<string,GameObject> prefabs,Material[] cableMaterials)
        {
            if(!result.Ok)throw new InvalidOperationException("Invalid network cannot be instantiated");
            var root=UnityBridge.CreateInstances(result.supports,parent,prefabs);
            try{foreach(var cable in result.spans)UnityBridge.CreateMeshObject(cable.mesh,root.transform,cableMaterials,cable.id);return root;}
            catch{UnityBridge.Release(root);throw;}
        }
    }
}
