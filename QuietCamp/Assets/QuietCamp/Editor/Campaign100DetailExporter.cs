using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Editor
{
    // Copies existing model geometry into the shared triangle/palette representation.
    // No remodel, decimation, asset import setting or vendor material modification.
    public static class Campaign100DetailExporter
    {
        static readonly (string id,string path)[] Sources={
            ("camp_bench","Assets/PolygonCity/Models/SM_Prop_ParkBench_01.fbx"),
            ("camp_lantern","Assets/PolygonNatureBiomes/PNB_Swamp_Marshland/Models/SM_Prop_Swamp_Lantern_01.fbx"),
            ("camp_crate","Assets/PolygonNatureBiomes/PNB_Meadow_Forest/Models/SM_Prop_Camp_Crate_01.fbx"),
            ("camp_bag","Assets/PolygonBattleRoyale/Models/Characters/SM_PickUp_Bag_01.fbx")
        };
        public static void Export()
        {
            string directory=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../Design/Campaign100/2026-10-11/Authoring"));
            var models=new List<object>();var provenance=new List<object>();
            foreach(var source in Sources)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(source.path);
                if(prefab==null)throw new InvalidOperationException("Missing existing source "+source.path);
                var instance=UnityEngine.Object.Instantiate(prefab);
                var textures=new Dictionary<Texture,Texture2D>();
                try
                {
                    var filters=instance.GetComponentsInChildren<MeshFilter>(true);
                    if(filters.Length==0)throw new InvalidOperationException("No source meshes "+source.path);
                    var bounds=new Bounds();bool first=true;
                    foreach(var filter in filters)foreach(var vertex in filter.sharedMesh.vertices)
                    {var point=instance.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex));if(first){bounds=new Bounds(point,Vector3.zero);first=false;}else bounds.Encapsulate(point);}
                    float height=Mathf.Max(.001f,bounds.size.y);var origin=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                    var data=new List<float>();var colors=new List<int>();int count=0;
                    foreach(var filter in filters)
                    {
                        var mesh=filter.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var uv=mesh.uv;
                        var renderer=filter.GetComponent<MeshRenderer>();var materials=renderer?.sharedMaterials;
                        for(int sub=0;sub<mesh.subMeshCount;sub++)
                        {
                            var indices=mesh.GetTriangles(sub);var material=materials!=null&&sub<materials.Length?materials[sub]:null;
                            var tint=material!=null&&material.HasProperty("_BaseColor")?material.GetColor("_BaseColor"):material!=null&&material.HasProperty("_Color")?material.GetColor("_Color"):Color.white;
                            Texture sourceTexture=material?.mainTexture;Texture2D sample=null;
                            if(sourceTexture!=null&&!textures.TryGetValue(sourceTexture,out sample))
                            {
                                var previous=RenderTexture.active;var temporary=RenderTexture.GetTemporary(sourceTexture.width,sourceTexture.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                                try{Graphics.Blit(sourceTexture,temporary);RenderTexture.active=temporary;sample=new Texture2D(temporary.width,temporary.height,TextureFormat.RGB24,false);sample.ReadPixels(new Rect(0,0,temporary.width,temporary.height),0,0);sample.Apply();textures.Add(sourceTexture,sample);}
                                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(temporary);}
                            }
                            for(int i=0;i<indices.Length;i+=3)
                            {
                                Color color=tint;
                                if(sample!=null&&uv.Length==vertices.Length)
                                {var at=(uv[indices[i]]+uv[indices[i+1]]+uv[indices[i+2]])/3;at=Vector2.Scale(at,material.mainTextureScale)+material.mainTextureOffset;color*=sample.GetPixelBilinear(at.x,at.y);}
                                int Pack(float x)=>Mathf.RoundToInt(Mathf.Clamp01(x)*255);
                                colors.Add((Pack(color.r)<<16)|(Pack(color.g)<<8)|Pack(color.b));
                                for(int j=0;j<3;j++)
                                {
                                    int index=indices[i+j];var p=(instance.transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[index]))-origin)/height;
                                    var normal=normals.Length==vertices.Length?instance.transform.InverseTransformDirection(filter.transform.TransformDirection(normals[index])).normalized:Vector3.up;
                                    data.AddRange(new[]{p.x,p.y,p.z,normal.x,normal.y,normal.z});
                                }
                                count++;
                            }
                        }
                    }
                    using var sha=SHA256.Create();var hash=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(source.path))).Replace("-","").ToLowerInvariant();
                    models.Add(new{id=source.id,sourceHeight=height,data=data.ToArray(),colors=colors.ToArray()});
                    provenance.Add(new{id=source.id,path=source.path,sourceSha256=hash,triangles=count,
                        representation="All source mesh triangles and transforms; sampled base colour per triangle, same low-poly lit palette pipeline as gameplay. Full PBR is not copied."});
                    Debug.Log("[Campaign100Details] "+source.id+" "+count+" triangles");
                }
                finally{foreach(var texture in textures.Values)UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(instance);}
            }
            File.WriteAllText(Path.Combine(directory,"detail-models.json"),JsonConvert.SerializeObject(models)+"\n");
            File.WriteAllText(Path.Combine(directory,"detail-models-provenance.json"),JsonConvert.SerializeObject(provenance,Formatting.Indented)+"\n");
            Debug.Log("[Campaign100Details] Existing project details exported.");
        }
    }
}
