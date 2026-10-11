using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kruty1918.ScenePlacement.Unity
{
    public static class UnityBridge
    {
        public static V3 Core(Vector3 p)=>new V3(p.x,p.y,p.z);
        public static Vector3 Native(V3 p)=>new Vector3(p.x,p.y,p.z);
        public static MeshData Read(Mesh mesh)
        {
            if(mesh==null||!mesh.isReadable)throw new ArgumentException("Runtime reading requires a readable static mesh; Editor uses StaticMeshSnapshot.Read without importer changes");
            var materials=new List<int>();var indices=new List<int>();
            for(int slot=0;slot<mesh.subMeshCount;slot++){if(mesh.GetTopology(slot)!=MeshTopology.Triangles)throw new ArgumentException("Triangle topology required");var triangles=mesh.GetTriangles(slot);indices.AddRange(triangles);materials.AddRange(Enumerable.Repeat(slot,triangles.Length/3));}
            var channels=new List<VertexChannel>();
            for(int channel=0;channel<8;channel++){var values=new List<Vector4>();mesh.GetUVs(channel,values);if(values.Count==0)continue;channels.Add(new VertexChannel{name="uv"+channel,components=4,values=values.SelectMany(v=>new[]{v.x,v.y,v.z,v.w}).ToArray()});}
            var result=new MeshData{positions=mesh.vertices.Select(Core).ToArray(),normals=mesh.normals.Select(Core).ToArray(),uv=mesh.uv.Select(v=>new V2(v.x,v.y)).ToArray(),colors=mesh.colors.Select(c=>new Tint(c.r,c.g,c.b,c.a)).ToArray(),indices=indices.ToArray(),materials=materials.ToArray(),channels=channels.ToArray()};result.Validate();return result;
        }
        public static Mesh CreateMesh(MeshData source,string name="Authored mesh",Action<Mesh,VertexChannel> customWriter=null)
        {
            source.Validate();foreach(var channel in source.channels)if((!channel.name.StartsWith("uv",StringComparison.Ordinal)||!int.TryParse(channel.name.Substring(2),out int index)||index<0||index>7)&&customWriter==null)throw new ArgumentException("Custom channel needs an explicit Unity binding: "+channel.name);
            var mesh=new Mesh{name=name,indexFormat=source.positions.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};
            mesh.vertices=source.positions.Select(Native).ToArray();if(source.normals.Length>0)mesh.normals=source.normals.Select(Native).ToArray();
            if(source.uv.Length>0)mesh.uv=source.uv.Select(v=>new Vector2(v.x,v.y)).ToArray();if(source.colors.Length>0)mesh.colors=source.colors.Select(c=>new Color(c.r,c.g,c.b,c.a)).ToArray();
            int count=source.materials.Length>0?source.materials.Max()+1:1;mesh.subMeshCount=count;
            for(int slot=0;slot<count;slot++)mesh.SetTriangles(Enumerable.Range(0,source.TriangleCount).Where(t=>(source.materials.Length>0?source.materials[t]:0)==slot).SelectMany(t=>source.indices.Skip(t*3).Take(3)).ToArray(),slot,false);
            foreach(var channel in source.channels)
            {
                if(!channel.name.StartsWith("uv",StringComparison.Ordinal)||!int.TryParse(channel.name.Substring(2),out int index)||index<0||index>7){customWriter(mesh,channel);continue;}
                var values=new List<Vector4>(source.positions.Length);for(int i=0;i<source.positions.Length;i++){int start=i*channel.components;values.Add(new Vector4(channel.values[start],channel.components>1?channel.values[start+1]:0,channel.components>2?channel.values[start+2]:0,channel.components>3?channel.values[start+3]:0));}mesh.SetUVs(index,values);
            }
            if(source.normals.Length==0)mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static GameObject CreateInstances(IEnumerable<PlacedInstance> instances,Transform parent,Func<string,GameObject> prefabs)
        {
            var root=new GameObject("Authored instances");root.transform.SetParent(parent,false);
            try{foreach(var instance in instances){var prefab=prefabs(instance.asset);if(prefab==null)throw new ArgumentException("Unresolved prefab: "+instance.asset);var go=UnityEngine.Object.Instantiate(prefab,root.transform,false);go.name=instance.id;go.transform.localPosition=Native(instance.pose.position);go.transform.localRotation=Quaternion.LookRotation(Native(instance.pose.forward),Native(instance.pose.up));go.transform.localScale=Native(instance.pose.scale);}return root;}
            catch{Release(root);throw;}
        }
        public static GameObject CreateMeshObject(MeshData source,Transform parent,Material[] materials,string name)
        {
            var mesh=CreateMesh(source,name);var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterials=materials;
            go.AddComponent<OwnedGeneratedMesh>().Initialize(mesh);return go;
        }
        public static void Release(UnityEngine.Object value)
        {if(value==null)return;if(UnityEngine.Application.isPlaying)UnityEngine.Object.Destroy(value);else UnityEngine.Object.DestroyImmediate(value);}
    }
    public sealed class OwnedGeneratedMesh : MonoBehaviour
    {
        Mesh owned;public void Initialize(Mesh mesh){if(owned!=null&&owned!=mesh)UnityBridge.Release(owned);owned=mesh;}
        void OnDestroy(){UnityBridge.Release(owned);owned=null;}
    }
    public sealed class ColliderSurface : ISurfaceQuery
    {
        readonly Collider collider;readonly float probeDistance;readonly string tag;
        public ColliderSurface(Collider collider,float probeDistance=1000,string tag="ground")
        {this.collider=collider??throw new ArgumentNullException(nameof(collider));if(probeDistance<=0)throw new ArgumentException("Positive probe distance");this.probeDistance=probeDistance;this.tag=tag;}
        public bool TrySample(V3 target,V3 direction,out SurfaceSample sample)
        {
            sample=default;var d=UnityBridge.Native(direction).normalized;var ray=new Ray(UnityBridge.Native(target)-d*probeDistance,d);
            if(!collider.Raycast(ray,out var hit,probeDistance*2))return false;sample=new SurfaceSample{point=UnityBridge.Core(hit.point),normal=UnityBridge.Core(hit.normal),tag=tag,light=1};return true;
        }
    }
    public sealed class TerrainSurface : ISurfaceQuery
    {
        readonly Terrain terrain;readonly string tag;
        public TerrainSurface(Terrain terrain,string tag="ground"){this.terrain=terrain??throw new ArgumentNullException(nameof(terrain));this.tag=tag;}
        public bool TrySample(V3 target,V3 direction,out SurfaceSample sample)
        {
            sample=default;if(Math.Abs(direction.y)<.999f)return false;var origin=terrain.transform.position;var size=terrain.terrainData.size;float u=(target.x-origin.x)/size.x,v=(target.z-origin.z)/size.z;if(u<0||u>1||v<0||v>1)return false;
            var p=new Vector3(target.x,terrain.terrainData.GetInterpolatedHeight(u,v)+origin.y,target.z);sample=new SurfaceSample{point=UnityBridge.Core(p),normal=UnityBridge.Core(terrain.terrainData.GetInterpolatedNormal(u,v)),tag=tag,light=1};return true;
        }
    }
}
