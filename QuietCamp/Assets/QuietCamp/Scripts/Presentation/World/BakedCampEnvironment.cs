using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Loads authored buffers once. No procedural placement, mesh
    /// synthesis, streaming decisions or camera-dependent Update at runtime.</summary>
    public sealed class BakedCampEnvironment : MonoBehaviour
    {
        [Serializable] public sealed class Manifest
        {public int schemaVersion;public string id,sourceRevision;public Part[] parts;public CameraProfile camera;}
        [Serializable] public sealed class CameraProfile {public float pitch=58,yaw=225;}
        [Serializable] public sealed class Part
        {public string file,sha256,kind;public int vertices,triangles;public Piece[] objects;}
        [Serializable] public sealed class Piece {public string id,source;public float[] position,bounds;public int triangles;}
        readonly List<Mesh> _meshes=new List<Mesh>();readonly List<Material> _materials=new List<Material>();
        static readonly ConditionalWeakTable<LevelData,CameraProfile> CameraProfiles=new ConditionalWeakTable<LevelData,CameraProfile>();
        public Manifest Source {get;private set;}
        public int TriangleCount {get;private set;}
        public static bool Uses(LevelData level)=>!string.IsNullOrEmpty(level?.bakedEnvironment);
        public static void Attach(Transform parent,LevelData level)
        {
            if(!Uses(level))return;
            var root=new GameObject("Prebaked environment "+level.id);root.transform.SetParent(parent,false);root.layer=BoardRenderer.DecorLayer;
            try{root.AddComponent<BakedCampEnvironment>().Load(level);}
            catch{Destroy(root);throw;}
        }
        static string Hash(byte[] bytes)
        {using(var hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
        void Load(LevelData level)
        {
            string key=level.bakedEnvironment;
            if(!key.StartsWith("QuietCamp/BakedEnvironments/",StringComparison.Ordinal)||key.Contains(".."))throw new InvalidDataException("Invalid baked environment path");
            var text=Resources.Load<TextAsset>(key+"/manifest");
            if(text==null)throw new FileNotFoundException("Missing baked environment "+key);
            try{Source=JsonConvert.DeserializeObject<Manifest>(text.text);}
            finally{Resources.UnloadAsset(text);}
            if(Source.schemaVersion!=1||Source.id!=level.id||Source.parts==null||Source.parts.Length>16)throw new InvalidDataException("Baked environment manifest mismatch");
            CameraProfiles.Remove(level);CameraProfiles.Add(level,Source.camera??new CameraProfile());
            foreach(var part in Source.parts)
            {
                if(part.file.Contains("/")||part.file.Contains(".."))throw new InvalidDataException("Invalid baked buffer name");
                var asset=Resources.Load<TextAsset>(key+"/"+part.file);
                if(asset==null)throw new FileNotFoundException("Missing baked buffer "+part.file);
                Mesh mesh;
                try
                {
                    var bytes=asset.bytes;
                    if(Hash(bytes)!=part.sha256)throw new InvalidDataException("Baked buffer checksum "+part.file);
                    using(var reader=new BinaryReader(new MemoryStream(bytes,false)))
                    {
                        if(Encoding.ASCII.GetString(reader.ReadBytes(8))!="QCENV001")throw new InvalidDataException("Baked buffer format");
                        int count=reader.ReadInt32(),indices=reader.ReadInt32();
                        if(count!=part.vertices||indices!=part.triangles*3||count<0||count>1000000||indices<0||indices>3000000||bytes.Length!=16L+44L*count+4L*indices)throw new InvalidDataException("Baked buffer dimensions");
                        var vertices=new Vector3[count];var normals=new Vector3[count];var colors=new Color32[count];var roots=new List<Vector4>(count);var triangles=new int[indices];
                        for(int i=0;i<count;i++)
                        {
                            vertices[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());normals[i]=new Vector3(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle());
                            colors[i]=new Color32(reader.ReadByte(),reader.ReadByte(),reader.ReadByte(),reader.ReadByte());roots.Add(new Vector4(reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle(),reader.ReadSingle()));
                        }
                        for(int i=0;i<indices;i++){triangles[i]=reader.ReadInt32();if(triangles[i]<0||triangles[i]>=count)throw new InvalidDataException("Baked index range");}
                        mesh=new Mesh{name="Baked "+level.id+" "+part.kind,indexFormat=count>65535?IndexFormat.UInt32:IndexFormat.UInt16};_meshes.Add(mesh);
                        mesh.vertices=vertices;mesh.normals=normals;mesh.colors32=colors;mesh.SetUVs(1,roots);mesh.triangles=triangles;mesh.RecalculateBounds();
                        var bounds=mesh.bounds;bounds.Expand(part.kind=="trees"?.9f:.1f);mesh.bounds=bounds;mesh.UploadMeshData(false);
                    }
                }
                finally{Resources.UnloadAsset(asset);}
                var go=new GameObject("Baked "+part.kind);go.transform.SetParent(transform,false);go.layer=BoardRenderer.DecorLayer;
                go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var shader=Resources.Load<Shader>("QuietCamp/FoliageLit");if(shader==null)throw new InvalidOperationException("Missing gameplay foliage shader");
                var material=new Material(shader){name="Baked "+part.kind};_materials.Add(material);
                material.SetColor("_BaseColor",Color.white);material.SetFloat("_VertexTint",1);material.SetFloat("_ClusterWind",1);
                bool trees=part.kind=="trees",ground=part.kind=="groundcover";
                material.SetFloat("_TreeWind",trees?1:0);material.SetFloat("_CrownStart",.3f);material.SetFloat("_SwayAmp",trees?.07f:ground?.025f:0);material.SetFloat("_FlutterAmp",ground?.003f:0);material.SetFloat("_SnowCover",SeasonPalette.For(level).SnowCoverage);
                material.SetFloat("_Cull",ground?0:2);
                var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.On;renderer.receiveShadows=true;
                TriangleCount+=part.triangles;
            }
        }
        public static Vector3 CameraEuler(LevelData level)
        {
            if(!Uses(level))return CameraFitter.Euler;
            if(CameraProfiles.TryGetValue(level,out var camera))return new Vector3(camera.pitch,camera.yaw,0);
            return CameraFitter.Euler; // Before the scene-owned buffers have loaded.
        }
        void OnDestroy(){foreach(var mesh in _meshes)if(mesh!=null)Destroy(mesh);foreach(var material in _materials)if(material!=null)Destroy(material);_meshes.Clear();_materials.Clear();}
    }
}
