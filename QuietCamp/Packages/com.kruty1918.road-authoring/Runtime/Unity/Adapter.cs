using System;
using UnityEngine;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.Unity;
namespace Kruty1918.RoadAuthoring.Unity
{
    public static class RoadAdapter
    {
        public static GameObject Create(RoadResult result,Transform parent,Material[] materials,string name="Authored road")
        {if(!result.Ok||result.mesh==null)throw new InvalidOperationException("Invalid road cannot be instantiated");return UnityBridge.CreateMeshObject(result.mesh,parent,materials,name);}
        public static void Reserve(string id,RoadResult result,PlacementContext context)
        {
            if(!result.Ok)throw new InvalidOperationException("Invalid road corridor");int checkpoint=context.ReservationCount;
            try{foreach(var volume in result.corridor)context.Reserve(id,volume);}catch{context.Rollback(checkpoint);throw;}
        }
    }
}
