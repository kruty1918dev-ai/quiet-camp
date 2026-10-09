using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>One palette for foliage, ground and atmospheric light, independent of effect budgets.</summary>
    public readonly struct SeasonPalette
    {
        public readonly Color GrassDark, GrassLight, CanopyDark, CanopyLight, Soil, Fog;
        public readonly Color SunTint,AmbientTint;
        public readonly float FlowerWeight, SnowCoverage;
        readonly string _season;readonly float _progress;
        SeasonPalette(string season,float progress,Color dark, Color light, Color crownDark, Color crownLight, Color soil, Color fog, float flowers, float snow=0,Color? sunTint=null,Color? ambientTint=null)
        {
            _season=season;_progress=progress;GrassDark=dark;GrassLight=light;CanopyDark=crownDark;CanopyLight=crownLight;Soil=soil;Fog=fog;FlowerWeight=flowers;SnowCoverage=snow;
            SunTint=sunTint??(season=="autumn"?new Color(1.06f,.98f,.87f):season=="winter"?new Color(1.02f,1,1.04f):season=="spring"?new Color(1,1.02f,.99f):new Color(1.03f,1,.94f));
            AmbientTint=ambientTint??(season=="winter"?new Color(1,.98f,1.15f):season=="autumn"?new Color(1.05f,.99f,.95f):Color.white);
        }
        public static SeasonPalette For(LevelData level) => SeasonProfile.For(level).Palette;
        public static SeasonPalette For(string season)=>For(season,.5f);
        public static SeasonPalette For(string season,float progress)
        {
            progress=Mathf.Clamp01(progress);
            if(season=="winter")return new SeasonPalette(season,progress,new Color(.68f,.73f,.81f),new Color(.94f,.95f,.98f),new Color(.35f,.38f,.45f),new Color(.68f,.71f,.78f),new Color(.47f,.45f,.43f),new Color(.81f,.86f,.92f),0,1);
            if(season=="autumn")return new SeasonPalette(season,progress,Color.Lerp(new Color(.40f,.43f,.24f),new Color(.37f,.29f,.22f),progress),Color.Lerp(new Color(.64f,.60f,.35f),new Color(.67f,.48f,.29f),progress),new Color(.47f,.32f,.17f),new Color(.88f,.63f,.25f),new Color(.44f,.33f,.23f),new Color(.79f,.73f,.63f),.04f);
            if(season=="spring")return new SeasonPalette(season,progress,new Color(.25f,.45f,.26f),new Color(.53f,.69f,.37f),new Color(.27f,.47f,.29f),new Color(.57f,.71f,.38f),new Color(.42f,.35f,.27f),new Color(.80f,.87f,.78f),1);
            return new SeasonPalette("summer",progress,new Color(.29f,.43f,.23f),new Color(.57f,.66f,.32f),new Color(.23f,.40f,.27f),new Color(.49f,.62f,.32f),new Color(.48f,.38f,.26f),new Color(.82f,.85f,.72f),.86f);
        }
        public SeasonPalette WithGround(Color dark,Color light,Color soil)=>new SeasonPalette(_season,_progress,dark,light,CanopyDark,CanopyLight,soil,Fog,FlowerWeight,SnowCoverage,SunTint,AmbientTint);
        public static SeasonPalette Blend(SeasonPalette a,SeasonPalette b,float t,float snow)
            =>new SeasonPalette("roadmap",t,Color.Lerp(a.GrassDark,b.GrassDark,t),Color.Lerp(a.GrassLight,b.GrassLight,t),Color.Lerp(a.CanopyDark,b.CanopyDark,t),Color.Lerp(a.CanopyLight,b.CanopyLight,t),Color.Lerp(a.Soil,b.Soil,t),Color.Lerp(a.Fog,b.Fog,t),Mathf.Lerp(a.FlowerWeight,b.FlowerWeight,t),snow,Color.Lerp(a.SunTint,b.SunTint,t),Color.Lerp(a.AmbientTint,b.AmbientTint,t));
        public Color Ambient(Color authored)
        {
            var tint=authored*AmbientTint;
            if(_season!="winter")return tint;
            float value=Mathf.Max(authored.r,Mathf.Max(authored.g,authored.b));
            return Color.Lerp(tint,new Color(value*.88f,value*.93f,value,authored.a),.7f);
        }
        public Color Plant(Color authored,float variation,bool conifer=false)
        {
            if(authored.g>authored.r*1.03f)
            {
                if(_season=="winter")return conifer
                    ?Color.Lerp(new Color(.14f,.25f,.20f),new Color(.27f,.39f,.29f),variation)
                    :Color.Lerp(CanopyDark,CanopyLight,variation);
                if(conifer)return Color.Lerp(new Color(.19f,.34f,.26f),new Color(.35f,.48f,.31f),variation);
                if(_season=="autumn")
                {
                    float ripeness=Mathf.Clamp01(.17f+_progress*.88f+(variation-.5f)*.60f);
                    var gold=Color.Lerp(CanopyDark,CanopyLight,variation);
                    return Color.Lerp(new Color(.39f,.47f,.25f),gold,Mathf.SmoothStep(0,1,ripeness));
                }
                return Color.Lerp(CanopyDark,CanopyLight,variation);
            }
            return authored;
        }
    }
}
