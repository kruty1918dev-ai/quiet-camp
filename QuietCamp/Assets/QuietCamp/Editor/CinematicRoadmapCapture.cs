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
            CinematicRoadmapBaker.Bake();
            var asset=AssetDatabase.LoadAssetAtPath<RoadmapWorldAsset>("Assets/QuietCamp/Resources/QuietCamp/CinematicRoadmap/World.asset");
            string repo=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../.."));
            string output=Path.Combine(repo,"Design/Roadmap/CinematicPilot/2026-10-10");Directory.CreateDirectory(output);
            var results=new List<object>();
            for(int i=0;i<9;i++)results.Add(Capture(asset,i*.5f,4,false,720,1600,Path.Combine(output,"composition-"+i.ToString("00")+".png")));
            foreach(int frontier in new[]{0,1,3,4})
                results.Add(Capture(asset,frontier,frontier,false,720,1600,Path.Combine(output,"progress-"+frontier+".png")));
            foreach(bool low in new[]{true,false})
                results.Add(Capture(asset,3,4,low,1600,720,Path.Combine(output,(low?"low":"balanced")+"-landscape.png")));
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
        static object Capture(RoadmapWorldAsset asset,float route,int frontier,bool low,int width,int height,string path)
        {
            var root=new GameObject("Cinematic native capture");var world=root.AddComponent<RoadmapWorldPresenter>();
            RenderTexture target=null;Texture2D pixels=null;var previous=RenderTexture.active;
            try
            {
                world.ConfigureCapture(asset,route,frontier,low);
                if(!world.Ready)throw new InvalidOperationException(world.Fault??"Chunks not ready");
                target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=1};target.Create();
                world.WorldCamera.targetTexture=target;world.WorldCamera.aspect=(float)width/height;
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
                    loadedChunks=world.LoadedChunks,visibleRenderers=visible.Length,submittedMeshTriangles=submittedTriangles,
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
