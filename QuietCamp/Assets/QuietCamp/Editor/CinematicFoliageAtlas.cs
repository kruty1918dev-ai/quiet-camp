using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace QuietCamp.Editor
{
    /// <summary>Offline leaf masks at their imported resolution; never generated while scrolling.</summary>
    internal sealed class CinematicFoliageAtlas : IDisposable
    {
        const int Width=4096,Height=2048;
        readonly Texture2D pixels=new Texture2D(Width,Height,TextureFormat.RGBA32,false,false);
        readonly Dictionary<string,Rect> slots=new Dictionary<string,Rect>(StringComparer.Ordinal);
        readonly List<object> sources=new List<object>();
        int nextX;
        public CinematicFoliageAtlas(){pixels.SetPixels32(new Color32[Width*Height]);}
        public Rect Add(string path,Texture2D source)
        {
            if(slots.TryGetValue(path,out var existing))return existing;
            if(source.height>Height||nextX+source.width>Width)throw new InvalidOperationException("Foliage atlas needs a new authored layout: "+path);
            var colors=source.GetPixels32();var alpha=new byte[colors.Length];
            for(int i=0;i<colors.Length;i++)
            {
                var gamma=((Color)colors[i]).gamma;float luminance=.2126f*gamma.r+.7152f*gamma.g+.0722f*gamma.b;
                byte light=(byte)Mathf.RoundToInt(255*Mathf.Clamp01(.62f+.48f*luminance));
                alpha[i]=colors[i].a;colors[i]=new Color32(light,light,light,colors[i].a);
            }
            pixels.SetPixels32(nextX,0,source.width,source.height,colors);
            // Half-texel inset keeps card edges in their own texture. The source
            // alpha, including holes between leaves, survives colour adaptation.
            var rect=new Rect((nextX+.5f)/Width,.5f/Height,(source.width-1f)/Width,(source.height-1f)/Height);
            slots.Add(path,rect);nextX+=source.width+8;
            using(var sha=SHA256.Create())sources.Add(new{source=path,width=source.width,height=source.height,
                alphaHash=BitConverter.ToString(sha.ComputeHash(alpha)).Replace("-","").ToLowerInvariant(),
                rect=new{x=rect.x,y=rect.y,width=rect.width,height=rect.height}});
            return rect;
        }
        public Texture2D Publish(string folder)
        {
            if(slots.Count==0)throw new InvalidOperationException("No reviewed leaf textures");
            pixels.Apply(false,false);string path=folder+"/FoliageAtlas.png";
            File.WriteAllBytes(path,pixels.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=true;importer.isReadable=false;
            importer.wrapMode=TextureWrapMode.Clamp;importer.filterMode=FilterMode.Bilinear;importer.maxTextureSize=4096;
            importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.alphaIsTransparency=true;importer.mipmapEnabled=true;
            importer.mipMapsPreserveCoverage=true;importer.alphaTestReferenceValue=.4f;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
            File.WriteAllText(folder+"/FoliageAtlas.provenance.json",JsonConvert.SerializeObject(new{width=Width,height=Height,
                sourceResolutionRetained=true,sourceAlphaRetained=true,palette="Authored per-species green with source luminance",sources},Formatting.Indented)+"\n");
            AssetDatabase.ImportAsset(folder+"/FoliageAtlas.provenance.json");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        public void Dispose(){Object.DestroyImmediate(pixels);}
    }
}
