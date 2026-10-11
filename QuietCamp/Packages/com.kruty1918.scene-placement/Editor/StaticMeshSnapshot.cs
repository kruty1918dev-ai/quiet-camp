using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Kruty1918.ScenePlacement.Unity;

namespace Kruty1918.ScenePlacement.EditorTools
{
    public static class StaticMeshSnapshot
    {
        public static MeshData ReadAsset(string path)
        {
            if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("Mesh data or a static model asset path required");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return Read(mesh);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(prefab==null)throw new ArgumentException("Missing static model asset: "+path);
            if(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length>0)throw new ArgumentException("Skinned models are outside static geometry v1");
            var builder=new GeometryBuilder();var materials=new List<Material>();
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null)continue;var data=Read(filter.sharedMesh);var matrix=prefab.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;var normalMatrix=matrix.inverse.transpose;
                for(int i=0;i<data.positions.Length;i++){data.positions[i]=UnityBridge.Core(matrix.MultiplyPoint3x4(UnityBridge.Native(data.positions[i])));if(data.normals.Length>0)data.normals[i]=UnityBridge.Core(normalMatrix.MultiplyVector(UnityBridge.Native(data.normals[i])).normalized);}
                var renderer=filter.GetComponent<MeshRenderer>();var slots=renderer!=null?renderer.sharedMaterials:Array.Empty<Material>();
                for(int t=0;t<data.materials.Length;t++){int slot=data.materials[t];Material value=slot<slots.Length?slots[slot]:null;int index=materials.IndexOf(value);if(index<0){index=materials.Count;materials.Add(value);}data.materials[t]=index;}
                if(matrix.determinant<0)for(int t=0;t<data.indices.Length;t+=3){int swap=data.indices[t+1];data.indices[t+1]=data.indices[t+2];data.indices[t+2]=swap;}
                builder.Append(data,Pose.At(V3.Zero));
            }
            var combined=builder.Build();combined.Validate();return combined;
        }
        public static MeshData Read(Mesh mesh)
        {
            using var snapshot=MeshUtility.AcquireReadOnlyMeshData(mesh);var data=snapshot[0];int count=data.vertexCount;
            using var vertices=new NativeArray<Vector3>(count,Allocator.Temp);data.GetVertices(vertices);
            var result=new MeshData{positions=vertices.ToArray().Select(UnityBridge.Core).ToArray()};
            if(data.HasVertexAttribute(VertexAttribute.Normal)){using var normals=new NativeArray<Vector3>(count,Allocator.Temp);data.GetNormals(normals);result.normals=normals.ToArray().Select(UnityBridge.Core).ToArray();}
            if(data.HasVertexAttribute(VertexAttribute.Color)){using var colors=new NativeArray<Color>(count,Allocator.Temp);data.GetColors(colors);result.colors=colors.ToArray().Select(c=>new Tint(c.r,c.g,c.b,c.a)).ToArray();}
            var channels=new List<VertexChannel>();
            for(int channel=0;channel<8;channel++)if(data.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0+channel)))
            {using var values=new NativeArray<Vector4>(count,Allocator.Temp);data.GetUVs(channel,values);if(channel==0)result.uv=values.ToArray().Select(v=>new V2(v.x,v.y)).ToArray();channels.Add(new VertexChannel{name="uv"+channel,components=4,values=values.ToArray().SelectMany(v=>new[]{v.x,v.y,v.z,v.w}).ToArray()});}
            var indices=new List<int>();var materials=new List<int>();
            for(int slot=0;slot<data.subMeshCount;slot++)
            {var sub=data.GetSubMesh(slot);if(sub.topology!=MeshTopology.Triangles)throw new ArgumentException("Only triangle topology supported");using var buffer=new NativeArray<int>(sub.indexCount,Allocator.Temp);data.GetIndices(buffer,slot,true);indices.AddRange(buffer.ToArray());materials.AddRange(Enumerable.Repeat(slot,sub.indexCount/3));}
            result.indices=indices.ToArray();result.materials=materials.ToArray();result.channels=channels.ToArray();result.Validate();return result;
        }
    }
}
