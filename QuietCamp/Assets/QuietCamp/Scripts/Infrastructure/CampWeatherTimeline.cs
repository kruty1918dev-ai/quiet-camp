using UnityEngine;

namespace QuietCamp.Infrastructure
{
    /// <summary>The visual weather schedule shared by gameplay and its map previews.
    /// This never changes puzzle rules, saves or the fixed direction of the sun.</summary>
    public static class CampWeatherTimeline
    {
        public readonly struct State
        {
            public readonly float Cloud, Rain;
            public State(float cloud, float rain) { Cloud=cloud; Rain=rain; }
        }
        public static State Target(float elapsed, int seed)
        {
            float phase=(Mathf.Max(0,elapsed)+(seed&7)*2)%110;
            return new State(phase<48?0:phase<70?.65f:phase<92?.9f:0,
                phase>=72&&phase<91?.7f:0);
        }
        public static State Initial(string weatherId) => new State(
            weatherId=="rain"?.8f:weatherId=="mist"?.62f:weatherId=="cloudy"?.55f:0,
            weatherId=="rain"?.65f:0);
        public static State Target(float elapsed, int seed, string weatherId)
        {
            var cycle=Target(elapsed,seed);
            if(string.IsNullOrEmpty(weatherId))return cycle;
            return new State(weatherId=="rain"?.75f+cycle.Cloud*.2f:weatherId=="mist"?.60f+cycle.Cloud*.2f
                :weatherId=="cloudy"?.4f+cycle.Cloud*.4f:cycle.Cloud*.4f,
                weatherId=="rain"?.58f+cycle.Rain*.32f:weatherId=="cloudy"?cycle.Rain*.12f:0);
        }
        // Integrate the existing five-second weather ease at a bounded fixed step.
        // Previews are representative moments of that level's cycle, not a forecast.
        public static State Preview(int seed, float elapsed) => Preview(seed,elapsed,null);
        public static State Preview(int seed,float elapsed,string weatherId,bool winter=false)
        {
            var initial=Initial(weatherId);
            float time=Mathf.Clamp(elapsed,0,220),cloud=initial.Cloud,rain=winter?0:initial.Rain;
            for(float at=0;at<time;)
            {
                float dt=Mathf.Min(.5f,time-at);at+=dt;
                var target=Target(at,seed,weatherId);float ease=1-Mathf.Exp(-dt/5);
                cloud=Mathf.Lerp(cloud,target.Cloud,ease);rain=winter?0:Mathf.Lerp(rain,target.Rain,ease);
            }
            return new State(cloud,rain);
        }
        public static float RepresentativeTime(int seed) => 26+(uint)seed%66;
    }
}
