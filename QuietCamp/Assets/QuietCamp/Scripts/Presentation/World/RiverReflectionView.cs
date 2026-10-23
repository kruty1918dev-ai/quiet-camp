using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using StylizedWater3;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Owns the installed water asset's planar renderer for one flat river surface.</summary>
    [ExecuteAlways] public sealed class RiverReflectionView : MonoBehaviour
    {
        PlanarReflectionRenderer _renderer;
        Camera _camera;
        float _scale;
        ReflectionProbe _environment;
        int _capture=-1;
        bool _captureRequested;
        public RenderTexture ReflectionTexture => _renderer!=null&&_camera!=null?_renderer.TryGetReflectionTexture(_camera):null;
        public bool HasRenderedReflection => ReflectionTexture!=null;
        public bool HasEnvironmentReflection => _environment!=null&&_capture>=0&&_environment.IsFinishedRendering(_capture);
        public static RiverReflectionView Attach(Transform parent,Camera camera,IReadOnlyList<MeshRenderer> surfaces,RiverEnvironmentProfile profile)
        {
            if(surfaces.Count==0)return null;
            float low=surfaces.Min(s=>s.bounds.min.y),high=surfaces.Max(s=>s.bounds.max.y);
            // A sloping river cannot use one mirror plane. Leave its native river shading intact.
            if(high-low>.08f)return null;
            var node=new GameObject("River scene reflections");node.transform.SetParent(parent,false);
            var view=node.AddComponent<RiverReflectionView>();view._camera=camera;view._scale=profile.reflectionScale;
            node.SetActive(false);
            var environment=new GameObject("River environment reflection");environment.transform.SetParent(node.transform,false);
            var near=surfaces.SelectMany(s=>s.GetComponent<MeshFilter>().sharedMesh.vertices)
                .OrderBy(p=>p.x*p.x+p.z*p.z).First();environment.transform.position=parent.TransformPoint(near+Vector3.up*.25f);
            var probe=environment.AddComponent<ReflectionProbe>();view._environment=probe;
            probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.IndividualFaces;probe.resolution=128;probe.hdr=true;
            probe.clearFlags=ReflectionProbeClearFlags.Skybox;probe.backgroundColor=camera.backgroundColor;
            probe.nearClipPlane=.04f;probe.farClipPlane=64;probe.boxProjection=true;probe.importance=100;probe.blendDistance=0;
            var bounds=surfaces[0].bounds;foreach(var s in surfaces.Skip(1))bounds.Encapsulate(s.bounds);
            bounds.Expand(new Vector3(16,24,16));probe.center=probe.transform.InverseTransformPoint(bounds.center);probe.size=bounds.size;
            int visibleLayers=camera.cullingMask==0?-1:camera.cullingMask;
            probe.cullingMask=visibleLayers&~(1<<WaterObject.WaterLayer)&~(1<<5);probe.shadowDistance=0;
            var reflection=node.AddComponent<PlanarReflectionRenderer>();view._renderer=reflection;
            reflection.renderScale=profile.reflectionScale;reflection.offset=.008f;
            reflection.includeSkybox=false;reflection.enableFog=false;reflection.renderShadows=false;reflection.enableInSceneView=false;
            reflection.cullingMask=probe.cullingMask;
            foreach(var surface in surfaces)
            {
                surface.gameObject.layer=WaterObject.WaterLayer;
                surface.probeAnchor=probe.transform;surface.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
                var material=surface.sharedMaterial;
                // Mesh UVs already follow the bends. This permits real planar reflections
                // without losing directional UV animation or editing the vendor shader.
                material.DisableKeyword("_RIVER");material.SetFloat("_RiverModeOn",0);material.SetFloat("_WorldSpaceUV",0);
                var water=surface.gameObject.AddComponent<WaterObject>();
                water.material=material;water.meshRenderer=surface;water.meshFilter=surface.GetComponent<MeshFilter>();
                reflection.waterObjects.Add(water);
            }
            reflection.RecalculateBounds();node.SetActive(true);reflection.EnableMaterialReflectionSampling();
            return view;
        }
        void LateUpdate()
        {
            // A transition can temporarily hide this camera with mask 0. Never
            // change that mask: the transition owns its restoration. Capture the
            // environment only after visibility and seasonal lighting are ready.
            if(_camera==null||_camera.cullingMask==0)return;
            int layers=_camera.cullingMask&~(1<<WaterObject.WaterLayer)&~(1<<5);
            if(_renderer!=null)_renderer.cullingMask=layers;
            if(_environment!=null)_environment.cullingMask=layers;
            // Capture after level models and seasonal lighting exist, once per view.
            // The cube fills gaps in planar reflections, including off-screen treetops.
            if(_environment!=null&&!_captureRequested&&_camera!=null&&QualitySettings.realtimeReflectionProbes)
            { _captureRequested=true;_environment.backgroundColor=_camera.backgroundColor;_capture=_environment.RenderProbe(); }
        }
        public void SetQuality(int tier)
        {
            if(_renderer!=null)
            {
                // Even a small reflection texture redraws the world. Low uses
                // the existing environment probe instead of a second scene pass.
                _renderer.enabled=tier>0;
                if(tier>0)_renderer.renderScale=tier==1?_scale:Mathf.Min(.6f,_scale*1.3f);
            }
        }
    }
}
