using System;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace QuietCamp.Presentation.World
{
    /// <summary>Camera-owned edge softness. The projected gameplay board stays sharp.</summary>
    public sealed class CampArtEdgeBlur : IDisposable
    {
        readonly Camera _camera;
        readonly Material _material;
        readonly BlurPass _pass;
        public CampArtEdgeBlur(Camera camera, float pixels)
        {
            _camera = camera;
            var shader = Resources.Load<Shader>("QuietCamp/CampArtEdgeBlur");
            if (shader == null) throw new InvalidOperationException("Camp edge blur shader missing.");
            _material = new Material(shader); _material.SetFloat("_BlurPixels", pixels);
            _pass = new BlurPass(_material);
            RenderPipelineManager.beginCameraRendering += Enqueue;
        }
        public void ProtectBoard(LevelData level)
        {
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
        { if (camera == _camera && _material != null) camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_pass); }
        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering -= Enqueue;
            if (_material != null) { if (UnityEngine.Application.isPlaying) UnityEngine.Object.Destroy(_material); else UnityEngine.Object.DestroyImmediate(_material); }
        }
        sealed class BlurPass : ScriptableRenderPass
        {
            readonly Material _material;
            public BlurPass(Material material)
            { _material = material; renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing; requiresIntermediateTexture = true; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var data = frameData.Get<UniversalResourceData>(); if (data.isActiveTargetBackBuffer) return;
                var source = data.activeColorTexture; var description = graph.GetTextureDesc(source);
                description.name = "Camp art edge softness"; description.clearBuffer = false;
                var destination = graph.CreateTexture(description);
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0), passName: "Camp art edge softness");
                data.cameraColor = destination;
            }
        }
    }
}
