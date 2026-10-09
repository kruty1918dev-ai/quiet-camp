using QuietCamp.Application;
using QuietCamp.Presentation.World;
using UnityEngine;
namespace QuietCamp.Presentation.UI.Prepared
{
    public readonly struct RoadmapVisualProfile
    {
        public readonly RoadmapEnvironmentSample Environment;
        readonly SeasonPalette _from,_to;
        public readonly SeasonPalette Palette;
        public RoadmapVisualProfile(RoadmapEnvironmentSample sample)
        {
            Environment=sample;_from=Endpoint(sample.From);_to=Endpoint(sample.To);
            Palette=SeasonPalette.Blend(_from,_to,sample.Blend,sample.Snow);
        }
        static SeasonPalette Endpoint(RoadmapEnvironmentProfile p)
        {
            var palette=SeasonPalette.For(p.Season,p.Phenology);
            float soil=p.Biome.Contains("field")?.25f:p.Biome.Contains("outskirts")?.35f:0;
            var light=Color.Lerp(palette.GrassLight,palette.Soil,soil)*(1-p.Moisture*.06f);
            var dark=Color.Lerp(palette.GrassDark,palette.Soil,soil);
            if(p.Biome.Contains("forest")){light=Color.Lerp(light,dark,.14f);}
            if(p.Biome.Contains("coast")){light=Color.Lerp(light,new Color(.75f,.69f,.51f),.7f*(1-p.Snow));dark=Color.Lerp(dark,new Color(.55f,.51f,.37f),.5f*(1-p.Snow));}
            palette=palette.WithGround(dark,light,palette.Soil);
            if(p.Phase=="first-frost")return SeasonPalette.Blend(SeasonPalette.For("autumn",1),palette,p.Snow,p.Snow);
            if(p.Phase=="thaw")return SeasonPalette.Blend(palette,SeasonPalette.For("winter"),p.Snow,p.Snow);
            if(p.Phase=="dry-summer")return SeasonPalette.Blend(palette,SeasonPalette.For("autumn",0),.24f,0);
            return palette;
        }
        public Color Lit(Color source,Vector3 normal)
        {
            var sun=-(Quaternion.Euler(Mathf.Lerp(50,22,Environment.Night),65,0)*Vector3.forward);
            var light=Ambient*.58f+Sun*(Mathf.Max(0,Vector3.Dot(normal,sun))*.46f);
            light.r+=.13f;light.g+=.13f;light.b+=.13f;light.a=1;
            var color=source*light;color.a=source.a;return color;
        }
        public Color Plant(Color source,float variation,bool pine)=>Color.Lerp(_from.Plant(source,variation,pine),_to.Plant(source,variation,pine),Environment.Blend);
        public Color Sun=>Color.Lerp(Color.Lerp(new Color(1,.95f,.83f),new Color(.86f,.91f,1),Mathf.InverseLerp(.7f,-.75f,Environment.Temperature)),new Color(.47f,.55f,.72f),Environment.Night);
        public Color Ambient=>Color.Lerp(new Color(.63f,.67f,.51f),new Color(.66f,.73f,.83f),Mathf.InverseLerp(.7f,-.75f,Environment.Temperature))*(1-Environment.Night*.55f);
    }
}
