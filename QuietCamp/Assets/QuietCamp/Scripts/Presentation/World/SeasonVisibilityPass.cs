using System;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace QuietCamp.Presentation.World
{
    // Depth-based atmosphere owned by one camera; UI overlay canvases render afterwards.
    public sealed class SeasonVisibilityPass:IDisposable
    {
        readonly Camera _camera;
        readonly Material _material;
        readonly FogPass _pass;
        readonly SeasonAtmospherePreset _preset;
        public SeasonVisibilityPass(Camera camera,SeasonAtmospherePreset preset)
        {
            _camera=camera;_preset=preset;
            _material=new Material(Resources.Load<Shader>("QuietCamp/SeasonVisibility"));
            _pass=new FogPass(_material);
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
            RenderPipelineManager.beginCameraRendering+=Enqueue;
        }
        public void Update(float clock,Vector2 wind,int tier=1)
        {
            var direction=Vector3.ProjectOnPlane(_camera.transform.forward,Vector3.up).normalized;
            var ray=_camera.ViewportPointToRay(new Vector3(.5f,.5f));
            var centre=ray.origin+ray.direction*(-ray.origin.y/Mathf.Min(-.001f,ray.direction.y));
            _material.SetVector("_FogAxis",new Vector4(direction.x,direction.z,centre.x,centre.z));
            _material.SetVector("_WeatherVisibility",new Vector4(_preset.fog,_preset.snow*_preset.wind,_preset.rain,clock));
            _material.SetVector("_FogRange",new Vector4(_camera.orthographicSize,_preset.fogNear,_preset.fogDistance,_preset.lowFog));
            _material.SetColor("_FogColor",CampArtDefinition.Color(_preset.fogColor==0?_preset.skyColor:_preset.fogColor));
            _material.SetVector("_AirLight",new Vector4(_preset.airLight,tier==0?2:tier==1?4:6,0,0));
            _material.SetColor("_AirSunColor",CampArtDefinition.Color(_preset.sunColor)*_preset.sunIntensity);
            var sun=Quaternion.Euler(CampArtDefinition.Vector(_preset.sunEuler))*Vector3.forward;
            _material.SetVector("_AirSunDirection",new Vector4(sun.x,sun.y,sun.z,0));
            _material.SetVector("_Flow",new Vector4(wind.x,wind.y,_preset.lensRain,0));
        }
        void Enqueue(ScriptableRenderContext context,Camera camera)
        {if(camera==_camera)camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(_pass);}
        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering-=Enqueue;
            if(UnityEngine.Application.isPlaying)UnityEngine.Object.Destroy(_material);else UnityEngine.Object.DestroyImmediate(_material);
        }
        sealed class FogPass:ScriptableRenderPass
        {
            readonly Material _material;
            public FogPass(Material material){_material=material;renderPassEvent=RenderPassEvent.BeforeRenderingPostProcessing;requiresIntermediateTexture=true;ConfigureInput(ScriptableRenderPassInput.Depth);}
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame)
            {
                var data=frame.Get<UniversalResourceData>();if(data.isActiveTargetBackBuffer)return;
                var source=data.activeColorTexture;var desc=graph.GetTextureDesc(source);desc.name="Season depth and height atmosphere";desc.clearBuffer=false;
                var destination=graph.CreateTexture(desc);
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source,destination,_material,0),passName:"Season visibility");
                data.cameraColor=destination;
            }
        }
    }
}
