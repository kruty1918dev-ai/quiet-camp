using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor
{
    /// <summary>AssetDatabase import audit only; never bakes a scene or builds a player.</summary>
    public static class TransferImportAudit
    {
        public static void Run()
        {
            var errors=new List<string>();
            var roots=new[]{"Assets/PolygonCity","Assets/PolygonNatureBiomes/PNB_Meadow_Forest",
                "Assets/PolygonNatureBiomes/PNB_Swamp_Marshland","Assets/PolygonBattleRoyale","Assets/PolygonParticles"};
            var packages=new List<object>();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach(var root in roots)
            {
                if(!AssetDatabase.IsValidFolder(root)){errors.Add("Missing package folder: "+root);continue;}
                var paths=AssetDatabase.FindAssets("",new[]{root}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
                var modelPaths=paths.Where(p=>p.EndsWith(".fbx",StringComparison.OrdinalIgnoreCase)).ToArray();
                long triangles=0;int meshCount=0;
                foreach(var path in modelPaths)
                {
                    var meshes=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToArray();
                    if(meshes.Length==0)errors.Add("FBX did not import meshes: "+path);
                    foreach(var mesh in meshes)
                    {
                        meshCount++;
                        for(int i=0;i<mesh.subMeshCount;i++)
                            if(mesh.GetTopology(i)==MeshTopology.Triangles)triangles+=(long)mesh.GetIndexCount(i)/3;
                    }
                }
                var materials=paths.Where(p=>p.EndsWith(".mat",StringComparison.OrdinalIgnoreCase))
                    .Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m=>m!=null).ToArray();
                int legacyMaterials=materials.Count(m=>m.shader==null||m.shader.name.StartsWith("Standard",StringComparison.Ordinal)
                    ||m.shader.name.StartsWith("Legacy Shaders/",StringComparison.Ordinal)
                    ||m.shader.name.StartsWith("Particles/Standard",StringComparison.Ordinal));
                packages.Add(new{root,assets=paths.Length,fbxFiles=modelPaths.Length,meshCount,triangles,
                    prefabFiles=paths.Count(p=>p.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase)),
                    materialCount=materials.Length,legacyMaterials});
            }
            var campaign=LevelLoader.MvpLevelIds();
            if(campaign.Count!=110)errors.Add("Current campaign no longer contains 110 IDs");
            var report=new{passed=errors.Count==0,unityVersion=UnityEngine.Application.unityVersion,
                nativeAssetDatabaseImport=true,packages,errors,mainCampaignNodes=campaign.Count,
                renderedSceneAcceptance=false,shaderAppearanceVerified=false,playerBuild=false};
            string rootPath=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../.."));
            string target=Path.Combine(rootPath,"docs/transfer/2026-10-09/import/native-import.json");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target,JsonConvert.SerializeObject(report,Formatting.Indented)+"\n");
            Debug.Log("[TransferImportAudit] "+JsonConvert.SerializeObject(report));
            if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors));
        }
    }
}
