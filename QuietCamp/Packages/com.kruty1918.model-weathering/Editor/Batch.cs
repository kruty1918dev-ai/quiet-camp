using System;
using System.Linq;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.EditorTools;
using UnityEngine;
namespace Kruty1918.ModelWeathering.EditorTools
{
    public static class Batch
    {
        [Serializable] public sealed class Recipe{public string id="weathered",meshAsset;public MeshData mesh;public MeshPart[] parts;public WeatheringProfile profile=new WeatheringProfile();}
        [Serializable] public sealed class Metadata{public float[] ageMask,soilMask;public GrowthRequest[] growth;public string[] operations;}
        public static void Run(){var r=BatchFiles.Request<Recipe>();var mesh=r.mesh??StaticMeshSnapshot.ReadAsset(r.meshAsset);if(r.parts!=null)mesh.parts=r.parts;var result=Weathering.Apply(r.id,mesh,r.profile);BatchFiles.Save(r.id,result.mesh,result.diagnostics,JsonUtility.ToJson(new Metadata{ageMask=result.ageMask,soilMask=result.soilMask,growth=result.growth.ToArray(),operations=result.operations.ToArray()}),new[]{new BatchFiles.Dependency{path="input-geometry:"+(r.meshAsset??r.id),sha256=BatchFiles.Hash(JsonUtility.ToJson(mesh))}});}
    }
}
