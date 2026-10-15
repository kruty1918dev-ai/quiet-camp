using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace QuietCamp.Infrastructure
{
    [Serializable] public sealed class RiverEnvironmentProfile
    {
        public int version;
        public float widthVariation, bankIrregularity, bankWidth, shallowDepth, channelDepth, bedDetail;
        public float flowSpeed, rippleRate, mistRate, leafRate, fishRate, bankPlantHeight=.42f;
        public int maxBankPlants, maxParticles;
        public float reflectionStrength=.85f, reflectionFresnel=1.4f, reflectionScale=.4f;
        public static RiverEnvironmentProfile Load()
        {
            var source=Resources.Load<TextAsset>("QuietCamp/river_environment");
            if(source==null)throw new FileNotFoundException("Missing common river environment profile.");
            var p=JsonConvert.DeserializeObject<RiverEnvironmentProfile>(source.text);p.Validate();return p;
        }
        public void Validate()
        {
            if(version<1||version>1000||widthVariation<0||widthVariation>.3f||bankIrregularity<0||bankIrregularity>.5f
                ||bankWidth<.3f||bankWidth>4||shallowDepth<.03f||shallowDepth>.3f||channelDepth<.25f||channelDepth>2
                ||bedDetail<0||bedDetail>.15f||flowSpeed<0||flowSpeed>2||rippleRate<0||rippleRate>100
                ||mistRate<0||mistRate>10||leafRate<0||leafRate>10||fishRate<0||fishRate>1
                ||bankPlantHeight<.15f||bankPlantHeight>1.5f||maxBankPlants<0||maxBankPlants>400||maxParticles<0||maxParticles>256||maxParticles>0&&maxParticles<20
                ||reflectionStrength<0||reflectionStrength>1||reflectionFresnel<.5f||reflectionFresnel>8||reflectionScale<.25f||reflectionScale>1)
                throw new InvalidDataException("Invalid common river environment profile.");
            foreach(var value in new[]{widthVariation,bankIrregularity,bankWidth,shallowDepth,channelDepth,bedDetail,flowSpeed,rippleRate,mistRate,leafRate,fishRate,bankPlantHeight,reflectionStrength,reflectionFresnel,reflectionScale})
                if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidDataException("Non-finite river setting.");
        }
    }
}
