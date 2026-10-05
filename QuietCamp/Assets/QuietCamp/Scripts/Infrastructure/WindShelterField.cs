using System;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>Immutable canopy density sampled identically by CPU rain contacts and GPU wind.
    /// Values are sampled at texel centres; pixels outside the finite forest are exposed meadow.</summary>
    public sealed class WindShelterField
    {
        readonly float[] _values;
        public int Resolution {get;}
        public Vector2 Origin {get;}
        public Vector2 Size {get;}
        public float CanopyHeight {get;}
        public WindShelterField(float[] values,int resolution,Vector2 origin,Vector2 size,float canopyHeight)
        {
            if(resolution<2||values==null||values.Length!=resolution*resolution||size.x<=0||size.y<=0)throw new ArgumentException("Invalid wind shelter grid");
            _values=(float[])values.Clone();for(int i=0;i<_values.Length;i++)_values[i]=Mathf.Round(Mathf.Clamp01(_values[i])*255)/255f;
            Resolution=resolution;Origin=origin;Size=size;CanopyHeight=Mathf.Max(.1f,canopyHeight);
        }
        public float Sample(Vector3 world)
        {
            float u=(world.x-Origin.x)/Size.x,v=(world.z-Origin.y)/Size.y;
            if(u<0||v<0||u>1||v>1)return 0;
            float x=u*(Resolution-1),y=v*(Resolution-1);int ix=Mathf.Min(Resolution-2,Mathf.FloorToInt(x)),iy=Mathf.Min(Resolution-2,Mathf.FloorToInt(y));
            return Mathf.Lerp(Mathf.Lerp(_values[iy*Resolution+ix],_values[iy*Resolution+ix+1],x-ix),Mathf.Lerp(_values[(iy+1)*Resolution+ix],_values[(iy+1)*Resolution+ix+1],x-ix),y-iy);
        }
        public Texture2D CreateTexture()
        {
            var texture=new Texture2D(Resolution,Resolution,TextureFormat.RGBA32,false,true){name="Canopy wind shelter",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            var pixels=new Color32[_values.Length];for(int i=0;i<pixels.Length;i++)pixels[i]=new Color32((byte)Mathf.RoundToInt(_values[i]*255),0,0,255);
            texture.SetPixels32(pixels);texture.Apply(false,true);return texture;
        }
        public Vector4 ShaderTransform => new Vector4(Origin.x,Origin.y,Size.x,Size.y);
    }
    /// <summary>Spatial breeze, not a fluid simulation. Coherent moving gusts and protected understory.</summary>
    public static class WindFieldMath
    {
        public readonly struct Sample
        {
            public readonly Vector2 DirectionXZ;public readonly float Strength;
            public Sample(Vector2 direction,float strength){DirectionXZ=direction;Strength=strength;}
        }
        // The owner publishes the same immutable field beside its shader globals.
        public static WindShelterField CurrentShelter {get;set;}
        public static Sample Evaluate(Vector2 direction,float strength,float seconds,Vector3 world,float height,WindShelterField shelter=null)
        {
            float canopy=shelter?.Sample(world)??0,top=shelter?.CanopyHeight??4;
            float exposure=1-canopy*.72f*Mathf.Exp(-Mathf.Max(0,height)/top);
            float phase=(Vector2.Dot(new Vector2(world.x,world.z),direction)-seconds*1.5f)*(.62831853f);
            float coherent=.86f+.14f*Mathf.Sin(phase)+.06f*Mathf.Sin(phase*.47f+world.x*.09f);
            float turn=.11f*Mathf.Sin(phase*.31f+world.z*.13f)*Mathf.Clamp01(height/top);
            var rotated=new Vector2(direction.x*Mathf.Cos(turn)-direction.y*Mathf.Sin(turn),direction.x*Mathf.Sin(turn)+direction.y*Mathf.Cos(turn));
            return new Sample(rotated,Mathf.Clamp01(strength*exposure*coherent));
        }
    }
}
