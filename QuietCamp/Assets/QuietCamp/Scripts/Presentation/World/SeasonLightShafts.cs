using System;
using System.Collections.Generic;
using System.Linq;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    // A few depth-faded light ribbons beneath real tree canopies, aligned to the preset sun.
    public sealed class SeasonLightShafts : IDisposable
    {
        readonly List<GameObject> _nodes=new List<GameObject>();
        readonly List<Mesh> _meshes=new List<Mesh>();
        readonly Material _material;
        readonly SeasonAtmospherePreset _preset;
        public int VisibleCount {get;private set;}
        public Vector3 Direction {get;}
        public SeasonLightShafts(Transform parent,Camera camera,CampArtDefinition art,SeasonAtmospherePreset preset,Vector3[] sources)
        {
            _preset=preset;Direction=Quaternion.Euler(CampArtDefinition.Vector(preset.sunEuler))*Vector3.forward;
            _material=new Material(Resources.Load<Shader>("QuietCamp/SeasonLightShaft"));
            _material.SetColor("_RayColor",CampArtDefinition.Color(preset.sunColor)*preset.sunIntensity);
            _material.SetVector("_ClearHalfSize",new Vector4(art.width*.5f+.55f,art.height*.5f+.55f,0,0));
            int index=0;
            foreach(var source in sources.OrderBy(s=>{var p=camera.WorldToViewportPoint(s);return (p.x-.5f)*(p.x-.5f)+(p.y-.65f)*(p.y-.65f);}).Take(9))
            {
                if(Direction.y>-.1f)break;
                var gap=Vector3.Cross(Vector3.up,Direction).normalized*2.8f;
                if(sources.Any(other=>other!=source&&Vector2.Distance(new Vector2(other.x,other.z),new Vector2(source.x+gap.x,source.z+gap.z))<2.4f))gap=-gap;
                var upper=source+gap+Vector3.up*.15f;
                float ground=art.GroundHeight(upper.x,upper.z);
                float length=Mathf.Clamp((upper.y-ground)/-Direction.y,3,18);
                var lower=upper+Direction*length;
                var side=Vector3.Cross(camera.transform.forward,Direction).normalized;
                if(side.sqrMagnitude<.01f)side=Vector3.Cross(Vector3.up,Direction).normalized;
                var mesh=new Mesh{name="Soft canopy beam"};
                mesh.vertices=new[]{upper-side*.2f,upper+side*.2f,lower+side*1.8f,lower-side*1.8f};
                mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,1,2,0,2,3};mesh.RecalculateBounds();
                var node=new GameObject("Промінь крізь крону "+index++);node.transform.SetParent(parent,false);
                node.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=_material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                _nodes.Add(node);_meshes.Add(mesh);
            }
        }
        public void Advance(float clock,Vector2 wind,int tier,bool reduced)
        {
            int budget=tier==0?3:tier==1?6:9;VisibleCount=0;
            float clear=Mathf.Clamp01(1-_preset.rain*.9f-_preset.snow*.8f);
            float air=Mathf.Clamp(.25f+_preset.lowFog+_preset.fog,.25f,.9f);
            _material.SetFloat("_RayOpacity",_preset.rays*clear*air*.3f);
            _material.SetVector("_RayFlow",new Vector4(wind.x,wind.y,reduced?0:clock,0));
            for(int i=0;i<_nodes.Count;i++){bool active=i<budget&&_preset.rays>0&&clear>.05f;_nodes[i].SetActive(active);if(active)VisibleCount++;}
        }
        public void Dispose()
        {
            foreach(var node in _nodes)Destroy(node);foreach(var mesh in _meshes)Destroy(mesh);Destroy(_material);
        }
        static void Destroy(UnityEngine.Object o){if(UnityEngine.Application.isPlaying)UnityEngine.Object.Destroy(o);else UnityEngine.Object.DestroyImmediate(o);}
    }
}
