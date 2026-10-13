using UnityEngine;

namespace QuietCamp.Presentation.World
{
    public static class RiverWeatherMaterial
    {
        public static void Configure(Material water)
        {
            // These are per-view copies of Stylized Water 3; the supplied asset
            // remains unchanged. Depth and opaque textures are enabled by the view.
            Set(water,"_DepthVertical",.9f);Set(water,"_DepthHorizontal",.7f);
            Set(water,"_VertexColorTransparency",1);Set(water,"_FogSource",0);Set(water,"_IntersectionSource",0);
            Set(water,"_ColorAbsorption",.12f);Set(water,"_FoamBaseAmount",.025f);Set(water,"_FoamStrength",.32f);Set(water,"_SlopeFoam",.2f);
            if(water.HasProperty("_ShallowColor"))water.SetColor("_ShallowColor",new Color(.40f,.53f,.43f,.16f));
            if(water.HasProperty("_BaseColor"))water.SetColor("_BaseColor",new Color(.18f,.35f,.31f,.4f));
            Set(water,"_RefractionChromaticAberration",0);Set(water,"_RefractionStrength",.018f);
            Set(water,"_IntersectionLength",.35f);Set(water,"_IntersectionFalloff",.4f);
            Set(water,"_ReflectionStrength",.12f);Set(water,"_SunReflectionStrength",.28f);
            Set(water,"_IntersectionRippleStrength",.18f);Set(water,"_WaveHeight",.024f);
            Set(water,"_NormalStrength",.25f);Set(water,"_WaveSpeed",.55f);
        }
        static void Set(Material m,string property,float value){if(m.HasProperty(property))m.SetFloat(property,value);}
    }
}
