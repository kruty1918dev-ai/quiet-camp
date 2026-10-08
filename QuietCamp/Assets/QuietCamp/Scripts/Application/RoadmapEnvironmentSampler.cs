using System;
using QuietCamp.Domain;
namespace QuietCamp.Application
{
    /// <summary>Immutable, portable spatial profiles. No rendering, solver, texture or per-sample allocation.</summary>
    public readonly struct RoadmapEnvironmentProfile
    {
        public readonly string Phase,Season,Biome,AudioBed;
        public readonly float Phenology,Grass,Leaves,Bare,Snow,Moisture,Fog,Particles,Temperature,Trees,Pines,Flowers,Relief,Wind,Birds,Insects,Water,Night;
        public RoadmapEnvironmentProfile(string phase,string biome,string lighting="day",float moistureBias=.15f)
        {
            Night=lighting=="night"?1:lighting=="evening"?.5f:0;Phase=phase;Biome=biome??"forest";
            Season=phase=="winter"||phase=="first-frost"?"winter":phase=="thaw"||phase=="spring"?"spring":phase.Contains("autumn")?"autumn":"summer";
            Phenology=phase=="early-autumn"?.12f:phase=="late-autumn"?1:.55f;
            Snow=phase=="winter"?1:phase=="first-frost"?.18f:phase=="thaw"?.28f:0;
            Leaves=phase=="winter"?.04f:phase=="first-frost"?.18f:phase=="late-autumn"?.28f:phase=="autumn"?.65f:phase=="early-autumn"?.9f:phase=="thaw"?.24f:1;
            Bare=phase=="winter"?1:phase=="first-frost"?.78f:phase=="late-autumn"?.55f:phase=="autumn"?.18f:phase=="early-autumn"?.025f:phase=="thaw"?.8f:0;
            Grass=phase=="winter"?0:phase=="first-frost"?.08f:phase=="late-autumn"?.13f:phase=="autumn"?.36f:phase=="dry-summer"?.63f:phase=="thaw"?.24f:1;
            Moisture=phase=="dry-summer"?.12f:phase=="summer"?.3f:phase=="thaw"?.9f:phase=="spring"?.62f:.68f;
            Moisture=Math.Max(Moisture,moistureBias);
            Fog=phase=="summer"?.15f:phase=="dry-summer"?.10f:phase=="thaw"?.75f:phase=="winter"?.52f:.4f;
            Particles=phase=="winter"?.75f:phase=="late-autumn"?.8f:phase=="autumn"?.65f:phase=="first-frost"?.30f:.2f;
            Temperature=phase=="winter"?-.75f:phase=="first-frost"?-.45f:phase=="thaw"?-.15f:phase=="dry-summer"?.7f:phase=="summer"?.5f:phase=="early-autumn"?.3f:.1f;
            bool forest=Biome.Contains("forest")||Biome.Contains("pine"),wet=Biome.Contains("river")||Biome.Contains("coast")||Biome.Contains("wetland");
            Trees=forest?.88f:Biome.Contains("outskirts")?.4f:wet?.36f:.18f;
            Pines=Biome.Contains("pine")?.9f:forest?.32f:.09f;Flowers=(Season=="spring"?1:Season=="summer"?.7f:.08f)*(Biome.Contains("field")?.2f:1);
            Relief=Biome.Contains("coast")?.10f:Biome.Contains("field")?.05f:wet?.08f:forest?.22f:.12f;
            Wind=forest?.32f:.65f;Birds=phase=="winter"?.16f:Season=="autumn"?.42f:Season=="spring"?1:.85f;Insects=Season=="summer"?1:Season=="spring"?.35f:Season=="autumn"?.12f:0;Water=wet?.55f:0;
            AudioBed=Season=="winter"?"ambience.biome.winter":Season=="autumn"?"ambience.biome.autumn":forest?"ambience.biome.forest":"ambience.biome.meadow";
        }
    }
    public readonly struct RoadmapEnvironmentSample
    {
        public readonly RoadmapEnvironmentProfile From,To;
        public readonly float Blend;
        public RoadmapEnvironmentSample(RoadmapEnvironmentProfile from,RoadmapEnvironmentProfile to,float blend){From=from;To=to;Blend=blend;}
        public float Mix(float a,float b)=>a+(b-a)*Blend;
        public float Grass=>Mix(From.Grass,To.Grass);public float Leaves=>Mix(From.Leaves,To.Leaves);public float Bare=>Mix(From.Bare,To.Bare);
        public float Snow=>Mix(From.Snow,To.Snow);public float Moisture=>Mix(From.Moisture,To.Moisture);public float Fog=>Mix(From.Fog,To.Fog);
        public float Particles=>Mix(From.Particles,To.Particles);public float Temperature=>Mix(From.Temperature,To.Temperature);
        public float Trees=>Mix(From.Trees,To.Trees);public float Pines=>Mix(From.Pines,To.Pines);public float Flowers=>Mix(From.Flowers,To.Flowers);
        public float Relief=>Mix(From.Relief,To.Relief);public float Wind=>Mix(From.Wind,To.Wind);public float Water=>Mix(From.Water,To.Water);
        public float Night=>Mix(From.Night,To.Night);public float Birds=>Mix(From.Birds,To.Birds);public float Insects=>Mix(From.Insects,To.Insects);
    }
    public sealed class RoadmapEnvironmentSampler
    {
        readonly RoadmapEnvironmentProfile[] _profiles;
        readonly float[] _starts,_ends;
        public RoadmapEnvironmentSampler(RoadmapCatalog catalog)
        {
            int count=catalog.Definition.regions.Length;_profiles=new RoadmapEnvironmentProfile[count];_starts=new float[count];_ends=new float[count];
            for(int r=0;r<count;r++)
            {
                var region=catalog.Definition.regions[r];
                _profiles[r]=new RoadmapEnvironmentProfile(region.environmentPhase??region.season,region.biome,region.lighting,region.weatherBias);
                if(r==0)continue;
                // Never overlap adjacent zones, including tiny legacy regions. No hard chapter switch.
                float length=Math.Min(Math.Max(420,region.transition.length),Math.Min(region.height,catalog.Definition.regions[r-1].height)*.9f);
                _starts[r]=catalog.RegionStarts[r]-length*.5f;_ends[r]=catalog.RegionStarts[r]+length*.5f;
            }
        }
        public RoadmapEnvironmentSample Sample(float distance)
        {
            int lo=1,hi=_profiles.Length;
            while(lo<hi){int mid=(lo+hi)/2;if(_ends[mid]<distance)lo=mid+1;else hi=mid;}
            if(lo==_profiles.Length)return new RoadmapEnvironmentSample(_profiles[lo-1],_profiles[lo-1],0);
            if(distance<=_starts[lo])return new RoadmapEnvironmentSample(_profiles[lo-1],_profiles[lo-1],0);
            float t=Math.Max(0,Math.Min(1,(distance-_starts[lo])/(_ends[lo]-_starts[lo])));t=t*t*(3-2*t);
            return new RoadmapEnvironmentSample(_profiles[lo-1],_profiles[lo],t);
        }
        public static bool ValidPhase(string phase)=>phase==null||phase=="spring"||phase=="summer"||phase=="dry-summer"||phase=="early-autumn"||phase=="autumn"||phase=="late-autumn"||phase=="first-frost"||phase=="winter"||phase=="thaw";
    }
}
