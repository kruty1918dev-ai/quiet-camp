using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Presentation.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace QuietCamp.Editor
{
    /// <summary>Native URP composition checks using the production presenter. No player, save or puzzle edits.</summary>
    public static class CinematicRoadmapCapture
    {
        public static void Render()
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)throw new InvalidOperationException("Native graphics required");
            // Immediate batch captures must wait for real shader variants, never the asynchronous placeholder shaders.
            ShaderUtil.allowAsyncCompilation=false;
            // Refresh script import mappings before querying cached native Resources after compilation.
            foreach(string name in new[]{"RoadmapWorldAsset","RoadmapWorldChunk"})
            {
                string scriptPath="Assets/QuietCamp/Scripts/Presentation/World/"+name+".cs";
                var script=AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                if(script==null||script.GetClass()==null)
                {
                    AssetDatabase.ImportAsset(scriptPath,ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
                    script=AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
                }
                if(script==null||script.GetClass()==null)throw new InvalidOperationException("Native world script mapping missing: "+scriptPath);
            }
            CinematicRoadmapBaker.Bake();
            var asset=AssetDatabase.LoadAssetAtPath<RoadmapWorldAsset>("Assets/QuietCamp/Resources/QuietCamp/CinematicRoadmap/World.asset");
            string repo=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../.."));
            string output=Path.Combine(repo,"Design/Roadmap/CinematicPilot/2026-10-10");Directory.CreateDirectory(output);
            int previousQuality=QualitySettings.GetQualityLevel();
            try
            {
            var results=new List<object>();
            foreach(bool low in new[]{true,false})for(int i=0;i<9;i++)
                results.Add(Capture(asset,i*.5f,4,low,720,1600,Path.Combine(output,(low?"low-composition-":"composition-")+i.ToString("00")+".png")));
            foreach(int frontier in new[]{0,1,3,4})
                results.Add(Capture(asset,frontier,frontier,false,720,1600,Path.Combine(output,"progress-"+frontier+".png")));
            foreach(bool low in new[]{true,false})
                results.Add(Capture(asset,3,4,low,1600,720,Path.Combine(output,(low?"low":"balanced")+"-landscape.png")));
            for(int i=0;i<5;i++)
                results.Add(Capture(asset,i,4,false,1600,720,Path.Combine(output,"landscape-zoom-out-"+i+".png"),.85f));
            // Bird's-eye framing must stay inside the terrain at the widest portrait pinch as well.
            foreach(bool low in new[]{true,false})for(int i=0;i<5;i++)
                results.Add(Capture(asset,i,4,low,720,1600,Path.Combine(output,(low?"low-":"")+"portrait-zoom-out-"+i+".png"),.85f));
            // Endpoint inspection margins must remain continuous at maximum zoom-out.
            foreach(bool landscape in new[]{false,true})foreach(float route in new[]{-.25f,4.2f})
                results.Add(Capture(asset,route,4,false,landscape?1600:720,landscape?720:1600,
                    Path.Combine(output,"edge-"+(route<0?"start":"end")+"-"+(landscape?"landscape":"portrait")+".png"),.85f));
            var errors=new List<string>();
            foreach(var shader in new[]{asset.ground.shader,asset.foliage.shader,asset.water.shader,asset.marker.shader}.Distinct())
                foreach(var message in ShaderUtil.GetShaderMessages(shader))
                    if(message.severity.ToString()=="Error")errors.Add(shader.name+": "+message.message);
            File.WriteAllText(Path.Combine(output,"native-capture-receipt.json"),JsonConvert.SerializeObject(new{
                capturedUtc=DateTime.UtcNow.ToString("o"),sourceHash=asset.sourceHash,unity=UnityEngine.Application.unityVersion,
                graphics=SystemInfo.graphicsDeviceType.ToString(),device=SystemInfo.graphicsDeviceName,
                capture="Production URP SingleCameraRequest in isolated Editor with synchronous shader compilation; Game View integration checks are separate",
                mobileFpsMeasured=false,shaderErrors=errors,frames=results},Formatting.Indented)+"\n");
            if(errors.Count>0)throw new InvalidOperationException(string.Join("\n",errors));
            Debug.Log("[CinematicRoadmap] Captured nine compositions, progress and quality fixtures");
            }
            finally{QualitySettings.SetQualityLevel(previousQuality,true);}
        }
        static object Capture(RoadmapWorldAsset asset,float route,int frontier,bool low,int width,int height,string path,float zoom=1)
        {
            var root=new GameObject("Cinematic native capture");var world=root.AddComponent<RoadmapWorldPresenter>();
            RenderTexture target=null;Texture2D pixels=null;var previous=RenderTexture.active;
            try
            {
                if(QualitySettings.GetQualityLevel()!=(low?0:1))QualitySettings.SetQualityLevel(low?0:1,true);
                world.ConfigureCapture(asset,route,frontier,low);
                if(!world.Ready)throw new InvalidOperationException(world.Fault??"Chunks not ready");
                target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=1};target.Create();
                world.WorldCamera.targetTexture=target;world.WorldCamera.aspect=(float)width/height;world.Zoom(zoom);world.SetCamera(route);
                var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                RenderPipeline.SubmitRenderRequest(world.WorldCamera,request);
                RenderPipeline.SubmitRenderRequest(world.WorldCamera,request);RenderTexture.active=target;
                pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
                var planes=GeometryUtility.CalculateFrustumPlanes(world.WorldCamera);
                var visible=world.WorldRoot.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&GeometryUtility.TestPlanesAABB(planes,r.bounds)).ToArray();
                long submittedTriangles=visible.Sum(r=>(long)r.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0)/3);
                if(world.LoadedChunks>3||submittedTriangles>(low?80000:150000)||visible.Select(r=>r.sharedMaterial).Distinct().Count()>(low?8:12))
                    throw new InvalidOperationException("Native composition exceeds the pilot geometry/residency/material budget: "+path+" / "+submittedTriangles);
                return new{file=Path.GetFileName(path),route,frontier,quality=low?"Low":"Balanced",width,height,
                    renderProfile=QualitySettings.names[QualitySettings.GetQualityLevel()],
                    zoom,fieldOfView=world.WorldCamera.fieldOfView,shadowDistance=((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).shadowDistance,loadedChunks=world.LoadedChunks,visibleRenderers=visible.Length,submittedMeshTriangles=submittedTriangles,
                    sharedMaterials=visible.Select(r=>r.sharedMaterial).Distinct().Count(),editorDrawCalls=UnityStats.drawCalls,
                    editorTriangles=UnityStats.triangles,cameraPosition=new[]{world.WorldCamera.transform.position.x,world.WorldCamera.transform.position.y,world.WorldCamera.transform.position.z}};
            }
            finally
            {
                if(world.WorldCamera!=null)world.WorldCamera.targetTexture=null;
                world.Leave();Object.DestroyImmediate(root);RenderTexture.active=previous;
                if(target!=null){target.Release();Object.DestroyImmediate(target);}if(pixels!=null)Object.DestroyImmediate(pixels);
            }
        }
    }
}
