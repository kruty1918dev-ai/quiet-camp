using System;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Camera-owned edge and vegetation softness. Solid objects and UI stay sharp.</summary>
    public sealed class CampArtEdgeBlur : IDisposable
    {
        readonly Camera _camera;
        readonly Material _material;
        readonly BlurPass _pass;
        readonly Func<bool> _visible;
        LevelData _level;
        public CampArtEdgeBlur(Camera camera, float pixels, Func<bool> visible = null,float vegetationPixels=2.2f)
        {
            _camera = camera;
            _visible = visible;
            var shader = Resources.Load<Shader>("QuietCamp/CampArtEdgeBlur");
            if (shader == null) throw new InvalidOperationException("Camp edge blur shader missing.");
            _material = new Material(shader); _material.SetFloat("_BlurPixels", pixels);
            _material.SetFloat("_VegetationBlurPixels",vegetationPixels);
            _pass = new BlurPass(_material);
            RenderPipelineManager.beginCameraRendering += Enqueue;
        }
        public void ProtectBoard(LevelData level)
        {
            _level=level;
            var low = Vector2.one; var high = Vector2.zero;
            foreach (float x in new[] { -level.width * .5f, level.width * .5f })
            foreach (float z in new[] { -level.height * .5f, level.height * .5f })
            {
                var p = _camera.WorldToViewportPoint(new Vector3(x, 1.6f, z));
                low = Vector2.Min(low, p); high = Vector2.Max(high, p);
                p = _camera.WorldToViewportPoint(new Vector3(x, 0, z)); low = Vector2.Min(low, p); high = Vector2.Max(high, p);
            }
            _material.SetVector("_SharpRect", new Vector4(low.x - .015f, low.y - .015f, high.x + .015f, high.y + .015f));
        }
        void Enqueue(ScriptableRenderContext context, Camera camera)
        { if (camera == _camera && _material != null && (_visible?.Invoke() ?? true)){if(_level!=null)ProtectBoard(_level);camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_pass);} }
        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= Enqueue;
            if (_material != null) { if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(_material); else UnityEngine.Object.DestroyImmediate(_material); }
        }
        sealed class BlurPass : ScriptableRenderPass
        {
            static readonly int VegetationMask=Shader.PropertyToID("_CampVegetationMask");
            static readonly int SoftDepth=Shader.PropertyToID("_CampSoftDepth");
            sealed class MaskData { public RendererListHandle renderers; }
            sealed class CompositeData { public TextureHandle source,mask,depth;public Material material; }
            readonly Material _material;
            public BlurPass(Material material)
            { _material = material; renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing; requiresIntermediateTexture = true;ConfigureInput(ScriptableRenderPassInput.Depth); }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var data = frameData.Get<UniversalResourceData>(); if (data.isActiveTargetBackBuffer) return;
                var source = data.activeColorTexture; var description = graph.GetTextureDesc(source);
                var maskDescription=graph.GetTextureDesc(data.activeDepthTexture);
                maskDescription.name="Visible moving vegetation";maskDescription.depthBufferBits=DepthBits.None;
                maskDescription.colorFormat=GraphicsFormat.R8_UNorm;maskDescription.clearBuffer=true;maskDescription.clearColor=Color.clear;
                maskDescription.bindTextureMS=false;
                var mask=graph.CreateTexture(maskDescription);
                var rendering=frameData.Get<UniversalRenderingData>();var camera=frameData.Get<UniversalCameraData>();var lights=frameData.Get<UniversalLightData>();
                var drawing=RenderingUtils.CreateDrawingSettings(new ShaderTagId("QuietCampVegetationMask"),rendering,camera,lights,camera.defaultOpaqueSortFlags);
                var filtering=new FilteringSettings(RenderQueueRange.opaque,camera.camera.cullingMask);
                var renderers=graph.CreateRendererList(new RendererListParams(rendering.cullResults,drawing,filtering));
                using(var builder=graph.AddRasterRenderPass<MaskData>("Visible moving vegetation",out var pass))
                {
                    pass.renderers=renderers;builder.UseRendererList(renderers);
                    builder.SetRenderAttachment(mask,0);builder.SetRenderAttachmentDepth(data.activeDepthTexture,AccessFlags.Read);
                    builder.SetRenderFunc((MaskData p,RasterGraphContext context)=>context.cmd.DrawRendererList(p.renderers));
                }
                description.name = "Camp art edge softness"; description.clearBuffer = false;
                var destination = graph.CreateTexture(description);
                using(var builder=graph.AddRasterRenderPass<CompositeData>("Camp vegetation and edge softness",out var pass))
                {
                    pass.source=source;pass.mask=mask;pass.depth=data.cameraDepthTexture;pass.material=_material;
                    builder.UseTexture(source);builder.UseTexture(mask);builder.UseTexture(pass.depth);builder.SetRenderAttachment(destination,0);builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((CompositeData p,RasterGraphContext context)=>
                    {context.cmd.SetGlobalTexture(VegetationMask,p.mask);context.cmd.SetGlobalTexture(SoftDepth,p.depth);Blitter.BlitTexture(context.cmd,p.source,new Vector4(1,1,0,0),p.material,0);});
                }
                data.cameraColor = destination;
            }
        }
    }
}
