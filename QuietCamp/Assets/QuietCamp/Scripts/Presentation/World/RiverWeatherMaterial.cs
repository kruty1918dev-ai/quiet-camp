using UnityEngine;
using QuietCamp.Infrastructure;

namespace QuietCamp.Presentation.World
{
    public static class RiverWeatherMaterial
    {
        public static void Configure(Material water)
        {
            // These are per-view copies of Stylized Water 3; the supplied asset
            // remains unchanged. Depth and opaque textures are enabled by the view.
            // The authored material owns its palette, opacity and depth range.
            // Weather changes motion and ripples, not the identity of the river.
            Set(water,"_VertexColorTransparency",1);Set(water,"_FogSource",0);Set(water,"_IntersectionSource",0);
            Set(water,"_ColorAbsorption",.12f);Set(water,"_FoamBaseAmount",.025f);Set(water,"_FoamStrength",.32f);Set(water,"_SlopeFoam",.2f);
            Set(water,"_RefractionChromaticAberration",0);Set(water,"_RefractionStrength",.018f);
            Set(water,"_IntersectionLength",.35f);Set(water,"_IntersectionFalloff",.4f);
            Set(water,"_ReflectionStrength",.12f);Set(water,"_SunReflectionStrength",.28f);
            Set(water,"_IntersectionRippleStrength",.18f);Set(water,"_WaveHeight",.024f);
            Set(water,"_NormalStrength",.25f);Set(water,"_WaveSpeed",.55f);
        }
        static void Set(Material m,string property,float value){if(m.HasProperty(property))m.SetFloat(property,value);}
        public static void NaturalChannel(Material water,RiverEnvironmentProfile profile)
        {
            Configure(water);
            // SW3 uses extinction coefficients (inverse metres), not depth in
            // metres. Passing channelDepth here made the whole river transparent.
            Set(water,"_DepthVertical",4/profile.channelDepth);Set(water,"_DepthHorizontal",2/profile.channelDepth);
            Set(water,"_FoamBaseAmount",0);Set(water,"_FoamStrength",.2f);Set(water,"_VertexColorFoam",1);
            Set(water,"_VertexColorWaveFlattening",1);Set(water,"_IntersectionSource",1);
            Set(water,"_SlopeFoam",.1f);Set(water,"_NormalStrength",.16f);Set(water,"_NormalSpeed",.32f);
            Set(water,"_ReflectionStrength",profile.reflectionStrength);Set(water,"_ReflectionFresnel",profile.reflectionFresnel);
            Set(water,"_ReflectionDistortion",.12f);Set(water,"_SunReflectionStrength",.4f);Set(water,"_Speed",profile.flowSpeed);
            Set(water,"_SunReflectionSize",.6f);Set(water,"_SunReflectionDistortion",.25f);
            Set(water,"_ColorAbsorption",.35f);
            Set(water,"_NormalStrength",.28f);Set(water,"_NormalSubSpeed",.55f);Set(water,"_NormalSpeed",.7f);Set(water,"_WorldSpaceUV",0);
            Set(water,"_CausticsBrightness",.1f);Set(water,"_CausticsTiling",.45f);Set(water,"_CausticsSpeed",.08f);
            Set(water,"_IntersectionLength",.16f);Set(water,"_IntersectionFalloff",.6f);
            if(water.HasProperty("_ShallowColor")){var c=water.GetColor("_ShallowColor");c.a=.6f;water.SetColor("_ShallowColor",c);}
            if(water.HasProperty("_BaseColor")){var c=water.GetColor("_BaseColor");c.a=.95f;water.SetColor("_BaseColor",c);}
        }
    }
}
