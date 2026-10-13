using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    [Serializable] public sealed class SeasonAtmospherePreset
    {
        public string id, label, season, description;
        public int revision=1, seed=1918;
        public float[] sunEuler={45,65,0};
        public int sunColor=0xffefd5, ambientColor=0x9fa78a, skyColor=0x879b93;
        public float sunIntensity=1.1f, shadows=.8f, exposure, contrast=3, saturation=-8, temperature;
        public float bloom=.12f, fog, rays, rain, snow, snowCover, leaves, insects, wind=.2f;
        public string[] audioKeys=Array.Empty<string>();
        public float[] audioGains=Array.Empty<float>();
        public void Validate()
        {
            if(string.IsNullOrEmpty(id)||string.IsNullOrEmpty(label)||!new[]{"spring","summer","autumn","winter"}.Contains(season)||revision<1)
                throw new InvalidDataException("Invalid season preset identity.");
            if(sunEuler==null||sunEuler.Length!=3||audioKeys==null||audioGains==null||audioKeys.Length!=audioGains.Length||audioKeys.Length>6)
                throw new InvalidDataException("Invalid season preset channels.");
            var values=sunEuler.Concat(audioGains).Concat(new[]{sunIntensity,shadows,exposure,contrast,saturation,temperature,bloom,fog,rays,rain,snow,snowCover,leaves,insects,wind});
            if(values.Any(v=>float.IsNaN(v)||float.IsInfinity(v))||sunIntensity<0||sunIntensity>3||Mathf.Abs(exposure)>2||Mathf.Abs(contrast)>30||Mathf.Abs(saturation)>40||Mathf.Abs(temperature)>40)
                throw new InvalidDataException("Invalid season preset values.");
            if(new[]{shadows,fog,rays,rain,snow,snowCover,leaves,insects,wind}.Concat(audioGains).Any(v=>v<0||v>1)||bloom<0||bloom>1)
                throw new InvalidDataException("Invalid season preset intensity.");
            if(season=="winter"&&(insects>0||leaves>0)||season!="winter"&&snowCover>0)
                throw new InvalidDataException("Season effects conflict.");
        }
    }
    public static class SeasonAtmosphereCatalog
    {
        sealed class Catalog { public int version;public SeasonAtmospherePreset[] presets; }
        static SeasonAtmospherePreset[] _presets;
        public static SeasonAtmospherePreset[] All
        {
            get
            {
                if(_presets!=null)return _presets;
                var resource=Resources.Load<TextAsset>("QuietCamp/season_atmosphere");
                if(resource==null)throw new InvalidDataException("Season atmosphere JSON missing.");
                var data=JsonConvert.DeserializeObject<Catalog>(resource.text);
                if(data?.version!=1||data.presets==null||data.presets.Select(p=>p.id).Distinct().Count()!=data.presets.Length)throw new InvalidDataException("Invalid atmosphere catalog.");
                foreach(var preset in data.presets)preset.Validate();
                return _presets=data.presets;
            }
        }
        public static SeasonAtmospherePreset Get(string id)=>All.Single(p=>p.id==id);
        public static SeasonAtmospherePreset Copy(SeasonAtmospherePreset preset)=>JsonConvert.DeserializeObject<SeasonAtmospherePreset>(JsonConvert.SerializeObject(preset));
        public static void Reset()=>_presets=null;
    }
}
