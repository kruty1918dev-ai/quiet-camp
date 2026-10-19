using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace QuietCamp.Marketing
{
    /// <summary>A true in-engine final cut, after weather and post effects. No UI or shared renderer edits.</summary>
    public sealed class TeaserBlackout : IDisposable
    {
        readonly Camera _camera;
        readonly BlackPass _pass=new BlackPass();
        public bool Active;
        public TeaserBlackout(Camera camera){_camera=camera;RenderPipelineManager.beginCameraRendering+=Enqueue;}
        void Enqueue(ScriptableRenderContext context,Camera camera)
        {if(Active&&camera==_camera)camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_pass);}
        public void Dispose()=>RenderPipelineManager.beginCameraRendering-=Enqueue;
        sealed class BlackPass : ScriptableRenderPass
        {
            sealed class Data{}
            public BlackPass(){renderPassEvent=(RenderPassEvent)((int)RenderPassEvent.AfterRendering+100);}
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                var resources=frame.Get<UniversalResourceData>();
                using var builder=graph.AddRasterRenderPass<Data>("Teaser final black cut",out var data);
                builder.SetRenderAttachment(resources.activeColorTexture,0,AccessFlags.WriteAll);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((Data d,RasterGraphContext context)=>context.cmd.ClearRenderTarget(RTClearFlags.Color,Color.black,1,0));
            }
        }
    }
}
