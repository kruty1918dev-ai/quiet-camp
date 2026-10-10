using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

namespace QuietCamp.Editor
{
    // Offline controlled-time production rendering, streamed straight to the encoder. No player or raw-frame directory.
    public static class CinematicReferenceMovie
    {
        public static string EncoderPath()
        {
            var path=Environment.GetEnvironmentVariable("QC_FFMPEG");
            if(string.IsNullOrEmpty(path)||!Path.IsPathRooted(path)||!File.Exists(path))
                throw new InvalidOperationException("Prepare the roadmap encoder and set QC_FFMPEG to its absolute path before native capture");
            return path;
        }
        public static object Record(RoadmapWorldAsset asset,string output,bool landscape)
        {
            int width=landscape?1600:720,height=landscape?720:1600;
            string file=landscape?"world-landscape.mp4":"world-portrait.mp4";
            var process=new System.Diagnostics.Process{StartInfo=new System.Diagnostics.ProcessStartInfo(EncoderPath(),
                "-hide_banner -loglevel error -y -f rawvideo -pixel_format rgb24 -video_size "+width+"x"+height+
                " -framerate 24 -i pipe:0 -an -vf vflip -c:v libx264 -threads 1 -preset veryfast -crf 19 -pix_fmt yuv420p -map_metadata -1 -movflags +faststart \""+Path.Combine(output,file)+"\"")
                {UseShellExecute=false,RedirectStandardInput=true,RedirectStandardError=true}};
            var errors=new System.Text.StringBuilder();process.ErrorDataReceived+=(sender,e)=>{if(e.Data!=null)errors.AppendLine(e.Data);};
            var root=new GameObject("Controlled native film");var world=root.AddComponent<RoadmapWorldPresenter>();
            var previous=RenderTexture.active;RenderTexture target=null;Texture2D pixels=null;
            var hashes=new List<string>();bool started=false;
            try
            {
                QualitySettings.SetQualityLevel(1,true);world.ConfigureCapture(asset,0,4,false);
                target=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32){antiAliasing=1};target.Create();
                pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
                world.WorldCamera.targetTexture=target;world.WorldCamera.aspect=(float)width/height;world.Zoom(.85f);
                process.Start();started=true;process.BeginErrorReadLine();
                using(var sha=SHA256.Create())for(int frame=0;frame<=288;frame++)
                {
                    world.SetCaptureFrame(frame/72f,frame/24f);
                    var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
                    RenderPipeline.SubmitRenderRequest(world.WorldCamera,request);RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();var data=pixels.GetRawTextureData<byte>().ToArray();
                    hashes.Add(BitConverter.ToString(sha.ComputeHash(data)).Replace("-","").ToLowerInvariant());
                    process.StandardInput.BaseStream.Write(data,0,data.Length);
                }
                process.StandardInput.Close();process.WaitForExit();
                if(process.ExitCode!=0)throw new InvalidOperationException("Native film encoder: "+errors);
                string fileHash,encoderHash;
                using(var sha=SHA256.Create())
                {
                    using(var stream=File.OpenRead(Path.Combine(output,file)))fileHash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                    using(var stream=File.OpenRead(EncoderPath()))encoderHash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
                }
                return new{file,sha256=fileHash,encoderSha256=encoderHash,sourceHash=asset.sourceHash,width,height,fps=24,frames=289,seconds=289/24f,animationSecondsFrom=0,animationSecondsTo=12,
                    waterClockOffset=12,zoom=.85f,frameRgbSha256=hashes,offlineRetimedRecording=true,mobileFpsMeasured=false};
            }
            finally
            {
                if(world.WorldCamera!=null)world.WorldCamera.targetTexture=null;
                world.Leave();Object.DestroyImmediate(root);RenderTexture.active=previous;
                if(target!=null){target.Release();Object.DestroyImmediate(target);}if(pixels!=null)Object.DestroyImmediate(pixels);
                if(started&&!process.HasExited){process.StandardInput.Close();process.WaitForExit();}process.Dispose();
            }
        }
    }
}
