using System;
using System.Linq;
using Kruty1918.ScenePlacement;
using Kruty1918.ScenePlacement.EditorTools;
using UnityEngine;
namespace Kruty1918.MeshStyle.EditorTools
{
    public static class Batch
    {
        [Serializable] public sealed class Recipe{public string id="styled",meshAsset;public MeshData mesh;public StyleProfile profile=new StyleProfile();public float projectedArea=10000;}
        public static void Run(){var r=BatchFiles.Request<Recipe>();var mesh=r.mesh??StaticMeshSnapshot.ReadAsset(r.meshAsset);var result=MeshHarmonizer.Harmonize(mesh,r.profile,r.projectedArea);BatchFiles.Save(r.id,result.mesh,result.diagnostics,inputs:new[]{new BatchFiles.Dependency{path="input-geometry:"+(r.meshAsset??r.id),sha256=BatchFiles.Hash(JsonUtility.ToJson(mesh))}});}
    }
}
