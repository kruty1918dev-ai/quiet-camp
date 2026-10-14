using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using QuietCamp.Presentation.World;
using QuietCamp.Presentation;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace QuietCamp.Editor.LevelAuthoring
{
    // Captures the running, memory-only preview; it never advances simulation itself.
    public sealed class WeatherMotionCapture
    {
        readonly CampArtView _view;
        readonly Camera _camera;
        readonly Vector3 _position;
        readonly float _size;
        readonly string _output,_result;
        readonly List<object> _frames=new List<object>();
        readonly WeatherObjectMotion[] _moving;
        readonly ParticleSystem.Particle[] _particles=new ParticleSystem.Particle[1800];
        double _next;
        int _index;
        readonly bool _forest;readonly int _quality;
        public WeatherMotionCapture(string output,string result,bool forest=false)
        {
            if(!EditorApplication.isPlaying||!EditorAuthoringPreview.Active||QuietCampBootstrap.ServicesRef?.Save.MemoryOnly!=true)
                throw new InvalidOperationException("Weather motion capture requires an active memory-only preview.");
            _forest=forest;_quality=QuietCampBootstrap.ServicesRef.EffectiveQuality;
            _view=UnityEngine.Object.FindAnyObjectByType<CampArtView>();
            if(_view?.SeasonAtmosphere==null)throw new InvalidOperationException("No seasonal world is running.");
            _camera=_view.GetComponent<Camera>();_position=_camera.transform.position;_size=_camera.orthographicSize;
            _moving=_view.SeasonAtmosphere.transform.parent.GetComponentsInChildren<WeatherObjectMotion>();
            _output=output;_result=result;Directory.CreateDirectory(output);_next=EditorApplication.timeSinceStartup+.5;
            EditorApplication.update+=Tick;
        }
        void Tick()
        {
            if(EditorApplication.timeSinceStartup<_next)return;
            try
            {
                if(!EditorApplication.isPlaying||_camera==null)throw new InvalidOperationException("Preview stopped during capture.");
                if(!_forest&&_index==8)_camera.transform.position=_position+new Vector3(7,0,-5);
                if(!_forest&&_index==16)_camera.orthographicSize=_size*1.35f;
                if(_forest&&_index==16)QuietCampBootstrap.ServicesRef.EffectiveQuality=0;
                if(_forest&&_index==24)QuietCampBootstrap.ServicesRef.EffectiveQuality=2;
                Capture();_index++;_next=EditorApplication.timeSinceStartup+.25;
                if(_index==(_forest?32:24))Finish(null);
            }
            catch(Exception error){Finish(error.ToString());}
        }
        void Capture()
        {
            var previousTarget=_camera.targetTexture;var previousActive=RenderTexture.active;
            float aspect=_camera.aspect;int width=768,height=Mathf.RoundToInt(768/aspect);height=Mathf.Max(2,height/2*2);
            var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.Create();
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                _camera.aspect=aspect;_camera.targetTexture=rt;
                RenderPipeline.SubmitRenderRequest(_camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
                RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
                var bytes=image.EncodeToPNG();string name=$"frame-{_index:00}.png";File.WriteAllBytes(Path.Combine(_output,name),bytes);
                using var hash=SHA256.Create();
                var weather=_view.SeasonAtmosphere;var coverage=weather.Coverage;
                _frames.Add(new{image=name,imageSha256=BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant(),
                    clock=weather.WeatherClock,editorTime=EditorApplication.timeSinceStartup,stage=_forest?(_index<16?"ordinary-camera":_index<24?"low-quality":"high-quality"):(_index<8?"stationary":_index<16?"pan":"zoom"),
                    surfaceSteps=CampSceneHost.Current?.SurfaceDetails?.Surface?.Steps??0,surfacePrints=CampSceneHost.Current?.SurfaceDetails?.PrintCount??0,surfaceResidents=CampSceneHost.Current?.SurfaceDetails?.Residents??0,
                    qualityTier=_view.QualityTier,regrowthTriangleBands=_view.RegrowthTriangleBands,visibleRegrowthTriangles=_view.RegrowthTriangleBands.Take(_view.QualityTier+1).Sum(),visibleShafts=weather.VisibleShafts,windDirection=new[]{weather.WindDirection.x,weather.WindDirection.y},activeFire=weather.ActiveFire,
                    cameraPosition=new[]{_camera.transform.position.x,_camera.transform.position.y,_camera.transform.position.z},orthographicSize=_camera.orthographicSize,
                    coverage=new[]{coverage.xMin,coverage.yMin,coverage.xMax,coverage.yMax},windStrength=weather.WindStrength,
                    width,height,aspect,particles=weather.LiveParticles,visibleLayers=VisibleLayers(weather),motions=_moving.Select(m=>new{name=m.name,position=new[]{m.transform.localPosition.x,m.transform.localPosition.y,m.transform.localPosition.z},rotation=new[]{m.transform.localRotation.x,m.transform.localRotation.y,m.transform.localRotation.z,m.transform.localRotation.w}}).ToArray()});
            }
            finally{_camera.targetTexture=previousTarget;_camera.aspect=aspect;RenderTexture.active=previousActive;UnityEngine.Object.DestroyImmediate(image);rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
        }
        object[] VisibleLayers(SeasonAtmosphereView weather)
        {
            return weather.GetComponentsInChildren<ParticleSystem>().Select(system=>
            {
                int count=system.GetParticles(_particles);var quadrants=new int[4];int visible=0;
                for(int i=0;i<count;i++)
                {
                    var p=_camera.WorldToViewportPoint(_particles[i].position);
                    if(p.z<=0||p.x<0||p.y<0||p.x>1||p.y>1)continue;
                    quadrants[(p.x>.5f?1:0)+(p.y>.5f?2:0)]++;visible++;
                }
                return (object)new{name=system.name,alive=count,visible,quadrants};
            }).ToArray();
        }
        void Finish(string error)
        {
            EditorApplication.update-=Tick;if(_forest&&QuietCampBootstrap.ServicesRef!=null)QuietCampBootstrap.ServicesRef.EffectiveQuality=_quality;if(_camera!=null){_camera.transform.position=_position;_camera.orthographicSize=_size;}
            var text=JsonConvert.SerializeObject(new{status=error==null?"weather-motion-captured":"error",error,
                kind="native-running-world-camera-sequence-no-HUD",playerBuild=false,preset=_view?.SeasonAtmosphere?.Preset.id,frames=_frames},Formatting.Indented);
            File.WriteAllText(Path.Combine(_output,"motion.json"),text);File.WriteAllText(_result,text);
        }
    }
}
