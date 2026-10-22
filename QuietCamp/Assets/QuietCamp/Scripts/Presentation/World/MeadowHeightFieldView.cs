#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using QuietCamp.Infrastructure;
using UnityEngine;
using UnityEngine.Rendering;

namespace QuietCamp.Presentation.World
{
    /// <summary>Owns the opt-in WorldOps experiment's proxy geometry and decoded texture lifetime.</summary>
    [ExecuteAlways] public sealed class MeadowHeightFieldView : MonoBehaviour
    {
        readonly List<UnityEngine.Object> _owned=new List<UnityEngine.Object>();
        public int TextureBytes {get;private set;}

        public void Configure(MeadowHeightFieldPatch[] patches,CampArtDefinition art,Action<Material> bind)
        {
            var shader=Shader.Find("QuietCamp/MeadowHeightField");
            if(shader==null||!shader.isSupported)throw new InvalidOperationException("Staged meadow height-field shader is unavailable.");
            Material flowerMaterial=null;
            foreach(var patch in patches)
            {
                patch.Validate();
                var albedo=Texture(patch.albedoPng,patch.resolution);
                var normal=Texture(patch.normalHeightPng,patch.resolution);
                var material=new Material(shader){name="Staged meadow height field"};_owned.Add(material);
                material.SetTexture("_FieldAlbedo",albedo);material.SetTexture("_FieldNormalHeight",normal);
                material.SetVector("_FieldRect",new Vector4(patch.rect[0],patch.rect[1],patch.rect[2],patch.rect[3]));
                material.SetVector("_FieldHeight",new Vector4(patch.heights[0],patch.heights[1],1f/patch.resolution,0));
                material.SetFloat("_ArtGrass",1);material.SetFloat("_ArtMeadow",1);
                material.SetColor("_ArtAmbient",CampArtDefinition.Color(art.atmosphere?.ambientColor??art.ambientColor)*.55f);
                float x=patch.rect[0],z=patch.rect[1],w=patch.rect[2],d=patch.rect[3],y=patch.heights[1];
                var mesh=new Mesh{name=patch.name+" height field",vertices=new[]{new Vector3(x,y,z),new Vector3(x+w,y,z),new Vector3(x+w,y,z+d),new Vector3(x,y,z+d)},triangles=new[]{0,2,1,0,3,2}};
                _owned.Add(mesh);mesh.RecalculateBounds();var bounds=mesh.bounds;
                bounds.Encapsulate(new Vector3(x,patch.heights[0],z));mesh.bounds=bounds;
                var child=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));child.transform.SetParent(transform,false);child.layer=BoardRenderer.DecorLayer;
                child.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=child.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                // The continuous meadow source does not cast shadows. Receiving remains enabled.
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=true;
                mesh.UploadMeshData(true);bind(material);
                if(patch.flowers!=null)
                {
                    if(flowerMaterial==null)
                    {
                        flowerMaterial=new Material(Resources.Load<Shader>("QuietCamp/CampArtLit"));_owned.Add(flowerMaterial);
                        flowerMaterial.SetFloat("_ArtGrass",1);flowerMaterial.SetFloat("_ArtMeadow",1);
                        flowerMaterial.SetColor("_ArtAmbient",material.GetColor("_ArtAmbient"));bind(flowerMaterial);
                    }
                    var flowers=CreateMesh(patch.flowers);_owned.Add(flowers);
                    var detail=new GameObject(patch.name+" retained flowers",typeof(MeshFilter),typeof(MeshRenderer));detail.transform.SetParent(transform,false);detail.layer=BoardRenderer.DecorLayer;
                    detail.GetComponent<MeshFilter>().sharedMesh=flowers;var detailRenderer=detail.GetComponent<MeshRenderer>();
                    detailRenderer.sharedMaterial=flowerMaterial;detailRenderer.shadowCastingMode=ShadowCastingMode.Off;detailRenderer.receiveShadows=true;
                    flowers.UploadMeshData(true);
                }
            }
        }
        public static Mesh CreateMesh(MeadowRetainedMesh data)
        {
            data.Validate();int count=data.vertices.Length/3;
            var points=new Vector3[count];var normals=new Vector3[count];var colours=new Color[count];var response=new Vector2[count];var indices=new int[count];
            for(int i=0;i<count;i++)
            {
                points[i]=new Vector3(data.vertices[i*3],data.vertices[i*3+1],data.vertices[i*3+2]);
                normals[i]=new Vector3(data.normals[i*3],data.normals[i*3+1],data.normals[i*3+2]);
                colours[i]=new Color(data.colours[i*4],data.colours[i*4+1],data.colours[i*4+2],data.colours[i*4+3]);
                response[i]=new Vector2(data.response[i*2],data.response[i*2+1]);indices[i]=i;
            }
            var mesh=new Mesh{name="Unchanged meadow source channels",indexFormat=IndexFormat.UInt32,vertices=points,normals=normals};
            mesh.colors=colours;mesh.uv2=response;mesh.triangles=indices;mesh.RecalculateBounds();return mesh;
        }
        Texture2D Texture(string png,int resolution)
        {
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,true){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Point};
            _owned.Add(texture);
            if(!texture.LoadImage(Convert.FromBase64String(png),true)||texture.width!=resolution||texture.height!=resolution)
                throw new InvalidOperationException("Invalid meadow surface texture.");
            TextureBytes+=resolution*resolution*4;return texture;
        }
        void OnDestroy()
        {
            foreach(var item in _owned)if(item!=null)
            {if(UnityEngine.Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
            _owned.Clear();
        }
    }
}

#endif
