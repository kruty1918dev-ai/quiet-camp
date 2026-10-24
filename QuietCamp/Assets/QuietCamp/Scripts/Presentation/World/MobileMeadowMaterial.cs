using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Two small shared linear RGBA textures with mipmaps, generated once.
    /// No per-level texture duplication, raymarching, shells or extra ground draws.</summary>
    public static class MobileMeadowMaterial
    {
        static Texture2D _ground,_cards;
        static Texture2D Texture(byte[] bytes,int width,int height,bool repeat,string name)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,true,true){name=name,
                wrapMode=repeat?TextureWrapMode.Repeat:TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
            // Populate only mip 0; Apply generates the remaining levels. Raw-load
            // of a mipmapped texture would require bytes for the entire chain.
            texture.SetPixelData(bytes,0);texture.Apply(true,true);return texture;
        }
        public static void Bind(Material material,CampArtDefinition art)
        {
            if(material==null||!material.HasProperty("_MobileMeadow"))return;
            bool enabled=art?.mobileMeadow==true;
            material.SetFloat("_MobileMeadow",enabled?1:0);
            if(!enabled)return;
            if(_ground==null)_ground=Texture(MobileMeadowPattern.Ground(),MobileMeadowPattern.GroundSize,MobileMeadowPattern.GroundSize,true,"Packed dense meadow detail");
            material.SetTexture("_MeadowDetailMap",_ground);
            if(material.HasProperty("_MeadowTint"))material.SetVector("_MeadowTint",CampMeadowImpostors.SeasonTint(art.atmosphere?.season));
            if(material.HasProperty("_MeadowCardMap"))
            {
                if(_cards==null)
                {
                    _cards=new Texture2D(MobileMeadowPattern.CardWidth,MobileMeadowPattern.CardHeight,TextureFormat.RGBA32,true,true)
                    {name="Coverage-preserving meadow clumps",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear,anisoLevel=2};
                    var mips=MobileMeadowPattern.CardMips();
                    for(int i=0;i<mips.Length;i++)_cards.SetPixelData(mips[i],i);
                    _cards.Apply(false,true);
                }
                material.SetTexture("_MeadowCardMap",_cards);
            }
        }
    }
}
