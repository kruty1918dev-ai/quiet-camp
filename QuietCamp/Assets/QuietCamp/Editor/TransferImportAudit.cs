using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace QuietCamp.Editor
{
    /// <summary>AssetDatabase import audit only; never bakes a scene or builds a player.</summary>
    public static class TransferImportAudit
    {
        public static void Run()
        {
            var errors=new List<string>();
            var roots=new[]{"Assets/PolygonCity","Assets/PolygonNatureBiomes/PNB_Meadow_Forest",
                "Assets/PolygonNatureBiomes/PNB_Swamp_Marshland","Assets/PolygonBattleRoyale","Assets/PolygonParticles",
                "Assets/PolygonNatureBiomes/PNB_Core"};
            var packages=new List<object>();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var upgradedMaterials=new List<object>();
            var pathsToUpgrade=AssetDatabase.FindAssets("",roots).Select(AssetDatabase.GUIDToAssetPath)
                .Distinct().Where(p=>!AssetDatabase.IsValidFolder(p)).ToArray();
            var oldMetadata=pathsToUpgrade.Where(NeedsMetadataUpgrade).ToArray();
            if(oldMetadata.Length>0)
                AssetDatabase.ForceReserializeAssets(oldMetadata,ForceReserializeAssetsOptions.ReserializeMetadata);
            var upgraders=MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset));
            foreach(var path in pathsToUpgrade.Where(p=>p.EndsWith(".mat",StringComparison.OrdinalIgnoreCase)))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null||material.shader==null)continue;
                string oldShader=material.shader.name;
                var upgrader=upgraders.FirstOrDefault(u=>u.OldShaderPath==oldShader);
                if(upgrader==null)continue;
                MaterialUpgrader.Upgrade(material,upgrader,MaterialUpgrader.UpgradeFlags.None);
                if(material.shader==null||material.shader.name!=upgrader.NewShaderPath)
                    errors.Add("Material shader upgrade failed: "+path);
                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssetIfDirty(material);
                upgradedMaterials.Add(new{path,oldShader,newShader=material.shader==null?null:material.shader.name});
            }
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
                var materialPaths=paths.Where(p=>p.EndsWith(".mat",StringComparison.OrdinalIgnoreCase)).ToArray();
                var materials=materialPaths.Select(AssetDatabase.LoadAssetAtPath<Material>).Where(m=>m!=null).ToArray();
                if(materials.Length!=materialPaths.Length)errors.Add("Material load failure in "+root);
                int legacyMaterials=materials.Count(m=>m.shader==null||m.shader.name.StartsWith("Standard",StringComparison.Ordinal)
                    ||m.shader.name.StartsWith("Legacy Shaders/",StringComparison.Ordinal)
                    ||m.shader.name.StartsWith("Particles/Standard",StringComparison.Ordinal));
                var shaderCounts=materials.GroupBy(m=>m.shader==null?"<missing>":m.shader.name)
                    .ToDictionary(g=>g.Key,g=>g.Count());
                int missingMeshFilters=0,missingScripts=0,loadedPrefabs=0;
                var prefabPaths=paths.Where(p=>p.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase)).ToArray();
                foreach(var path in prefabPaths)
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if(prefab==null){errors.Add("Prefab load failure: "+path);continue;}
                    loadedPrefabs++;
                    int missingMeshes=prefab.GetComponentsInChildren<MeshFilter>(true).Count(m=>m.sharedMesh==null);
                    int scripts=prefab.GetComponentsInChildren<Transform>(true)
                        .Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                    missingMeshFilters+=missingMeshes;missingScripts+=scripts;
                    if(missingMeshes>0)errors.Add("Prefab has missing meshes: "+path);
                    if(scripts>0)errors.Add("Prefab has missing scripts: "+path);
                }
                packages.Add(new{root,assets=paths.Length,fbxFiles=modelPaths.Length,meshCount,triangles,
                    prefabFiles=prefabPaths.Length,loadedPrefabs,missingMeshFilters,missingScripts,
                    materialCount=materials.Length,legacyMaterials,shaderCounts});
                Debug.Log("[TransferImportAudit] Audited "+root);
            }
            var campaign=LevelLoader.MvpLevelIds();
            if(campaign.Count!=110)errors.Add("Current campaign no longer contains 110 IDs");
            var report=new{passed=errors.Count==0,unityVersion=UnityEngine.Application.unityVersion,
                nativeAssetDatabaseImport=true,metadataUpgraded=oldMetadata.Length,upgradedMaterials,
                packages,errors,mainCampaignNodes=campaign.Count,
                renderedSceneAcceptance=false,shaderAppearanceVerified=false,playerBuild=false};
            string rootPath=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../.."));
            string target=Path.Combine(rootPath,"docs/transfer/2026-10-09/import/native-import.json");
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllText(target,JsonConvert.SerializeObject(report,Formatting.Indented)+"\n");
            Debug.Log("[TransferImportAudit] "+JsonConvert.SerializeObject(report));
            if(errors.Count>0)throw new InvalidOperationException(string.Join("; ",errors));
        }

        static bool NeedsMetadataUpgrade(string path)
        {
            string meta=path+".meta";
            if(!File.Exists(meta))return false;
            var match=Regex.Match(File.ReadAllText(meta),@"(?s)(TextureImporter|ModelImporter):.*?\n  serializedVersion: (\d+)");
            if(!match.Success)return false;
            int version=int.Parse(match.Groups[2].Value);
            return version<(match.Groups[1].Value=="TextureImporter"?10:25);
        }
    }
}
