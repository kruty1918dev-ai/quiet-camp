using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Rendering.Universal.ShaderGUI;
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
            // The registry also contains 3D-to-2D converters for this pipeline type.
            // Only select built-in-to-3D paths, and never reconvert current URP materials.
            var upgraders=MaterialUpgrader.FetchAllUpgradersForPipeline(typeof(UniversalRenderPipelineAsset))
                .Where(u=>!u.NewShaderPath.StartsWith("Universal Render Pipeline/2D/",StringComparison.Ordinal)
                    &&!u.OldShaderPath.StartsWith("Universal Render Pipeline/",StringComparison.Ordinal)).ToList();
            foreach(var path in pathsToUpgrade.Where(p=>p.EndsWith(".mat",StringComparison.OrdinalIgnoreCase)))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null||material.shader==null)continue;
                string oldShader=material.shader.name;
                if(oldShader=="Universal Render Pipeline/Lit")
                {
                    BaseShaderGUI.SetMaterialKeywords(material,LitGUI.SetMaterialKeywords);
                    EditorUtility.SetDirty(material);AssetDatabase.SaveAssetIfDirty(material);
                    continue;
                }
                if(UpgradeLegacyUnlit(material,oldShader))
                {
                    AssetDatabase.SaveAssetIfDirty(material);
                    upgradedMaterials.Add(new{path,oldShader,newShader=material.shader.name});
                    continue;
                }
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
                if(shaderCounts.Keys.Any(s=>s.StartsWith("Universal Render Pipeline/2D/",StringComparison.Ordinal)))
                    errors.Add("A 3D donor material uses a 2D renderer shader: "+root);
                var shaderWarnings=materialPaths.Select(p=>new{path=p,material=AssetDatabase.LoadAssetAtPath<Material>(p)})
                    .Where(x=>x.material!=null&&(x.material.shader==null
                        ||x.material.shader.name=="Hidden/InternalErrorShader"
                        ||!DeclaresUrp(x.material.shader)))
                    .Select(x=>new{x.path,shader=x.material.shader==null?"<missing>":x.material.shader.name}).ToArray();
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
                    materialCount=materials.Length,legacyMaterials,shaderCounts,shaderWarnings});
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

        static bool DeclaresUrp(Shader shader)
        {
            if(shader==null)return false;
            if(shader.name.StartsWith("Universal Render Pipeline/",StringComparison.Ordinal))return true;
            string source=AssetDatabase.GetAssetPath(shader);
            // GetTag depends on the active subshader, which a Null graphics device cannot select.
            return source.EndsWith(".shader",StringComparison.OrdinalIgnoreCase)&&File.Exists(source)
                &&Regex.IsMatch(File.ReadAllText(source),"\"RenderPipeline\"\\s*=\\s*\"UniversalPipeline\"");
        }

        // Older particle shaders have no built-in URP upgrader. Keep texture, tint,
        // UV transform and blend semantics in the URP particle shader. No scene edits.
        static bool UpgradeLegacyUnlit(Material material,string oldShader)
        {
            bool legacyParticle=oldShader.StartsWith("Legacy Shaders/Particles/",StringComparison.Ordinal);
            bool sprite=oldShader=="Universal Render Pipeline/2D/Mesh2D-Lit-Default";
            bool unlit=oldShader=="Unlit/Texture"||oldShader=="Unlit/Transparent";
            if(!legacyParticle&&!sprite&&!unlit)return false;
            if(legacyParticle&&!oldShader.Contains("Additive")&&!oldShader.Contains("Multiply")
                &&!oldShader.Contains("Alpha Blended"))return false;
            string textureKey=material.HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
            var texture=material.GetTexture(textureKey);
            var scale=material.GetTextureScale(textureKey);var offset=material.GetTextureOffset(textureKey);
            string colorKey=material.HasProperty("_TintColor")?"_TintColor":
                material.HasProperty("_BaseColor")?"_BaseColor":material.HasProperty("_Color")?"_Color":null;
            var color=colorKey==null?Color.white:material.GetColor(colorKey);
            if(legacyParticle)color*=2f;
            var shader=Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Particles/Unlit");
            if(shader==null)throw new InvalidOperationException("Required URP unlit shader is missing");
            material.shader=shader;
            material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",scale);
            material.SetTextureOffset("_BaseMap",offset);material.SetColor("_BaseColor",color);
            bool transparent=!unlit||oldShader=="Unlit/Transparent";
            material.SetFloat("_Surface",transparent?1f:0f);
            material.SetFloat("_Blend",oldShader.Contains("Additive")?2f:oldShader.Contains("Multiply")?3f:0f);
            material.SetFloat("_AlphaClip",0f);
            material.SetFloat("_Cull",unlit?2f:0f);
            if(!unlit)
            {
                material.SetFloat("_SoftParticlesEnabled",oldShader.Contains("Soft")?1f:0f);
                material.SetFloat("_SoftParticlesNearFadeDistance",0f);
                material.SetFloat("_SoftParticlesFarFadeDistance",1f);
                BaseShaderGUI.SetMaterialKeywords(material,null,ParticleGUI.SetMaterialKeywords);
            }
            else BaseShaderGUI.SetMaterialKeywords(material);
            if(oldShader.Contains("Multiply (Double)"))
            {
                material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.DstColor);
                material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.SrcColor);
                material.DisableKeyword("_ALPHAMODULATE_ON");
            }
            if(material.GetTexture("_BaseMap")!=texture)throw new InvalidOperationException("Texture reference changed during unlit upgrade");
            EditorUtility.SetDirty(material);
            return true;
        }
    }
}
