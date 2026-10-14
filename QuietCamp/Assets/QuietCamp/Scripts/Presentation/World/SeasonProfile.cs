using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Visual phenology shared by scenes and summaries. Never edits saved level data.</summary>
    public readonly struct SeasonProfile
    {
        public readonly string Id;
        public readonly float Progress,GrassWeight,ShrubWeight,LeafLitter,PollenWeight;
        readonly float _timeline;
        public float SnowCoverage=>_timeline>=0?TimelineSnow(_timeline):Winter?1:0;
        public float BareFraction=>_timeline>=0?TimelineBare(_timeline):Winter?1:Autumn?Mathf.Lerp(.025f,.42f,Progress):0;
        public bool IsContinuous=>_timeline>=0;
        public bool Winter=>_timeline>=0?SnowCoverage>.92f:Id=="winter";
        public bool Autumn=>_timeline>=0?_timeline>.48f&&_timeline<.72f:Id=="autumn";
        public SeasonPalette Palette=>_timeline>=0?TimelinePalette(_timeline):SeasonPalette.For(Id,Progress);
        SeasonProfile(string id,float progress)
        {
            _timeline=-1;Id=string.IsNullOrEmpty(id)?"summer":id;Progress=Mathf.Clamp01(progress);
            bool winter=Id=="winter",autumn=Id=="autumn";
            GrassWeight=winter?0:autumn?Mathf.Lerp(.42f,.10f,Progress):Id=="spring"?.90f:1;
            ShrubWeight=winter?0:autumn?Mathf.Lerp(.40f,.12f,Progress):1;
            LeafLitter=autumn?Mathf.Lerp(.40f,1,Progress):0;
            PollenWeight=winter?0:autumn?.06f:Id=="spring"?1:.65f;
        }
        public static SeasonProfile For(LevelData level)
            =>For(EnvironmentCompositionData.For(level),level?.order??0);
        public static SeasonProfile For(LevelSummary level)
            =>For(level?.environment,level?.number??0);
        public static SeasonProfile For(EnvironmentCompositionData environment,int order=0)
            =>environment?.seasonTimeline>=0?new SeasonProfile(environment.seasonTimeline):For(environment?.seasonId,order);
        SeasonProfile(float timeline)
        {
            _timeline=Mathf.Clamp01(timeline);Progress=_timeline;
            Id=timeline<.20f?"spring":timeline<.48f?"summer":timeline<.70f?"autumn":timeline<.88f?"winter":"spring";
            float snow=TimelineSnow(_timeline),bare=TimelineBare(_timeline);
            GrassWeight=Mathf.Lerp(.95f,.12f,snow);ShrubWeight=Mathf.Lerp(1,.15f,bare);
            LeafLitter=Smooth(.48f,.66f,_timeline)*(1-Smooth(.85f,.97f,_timeline));
            PollenWeight=(1-snow)*Mathf.Lerp(.8f,.12f,LeafLitter);
        }
        static float Smooth(float from,float to,float value)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(from,to,value));
        public static float TimelineSnow(float t)=>Smooth(.66f,.77f,t)*(1-Smooth(.81f,.92f,t));
        public static float TimelineBare(float t)=>Smooth(.51f,.72f,t)*(1-Smooth(.84f,.97f,t));
        public static SeasonPalette TimelinePalette(float t)
        {
            SeasonPalette spring=SeasonPalette.For("spring"),summer=SeasonPalette.For("summer"),autumn=SeasonPalette.For("autumn",.8f),winter=SeasonPalette.For("winter");
            float snow=TimelineSnow(t);
            if(t<.30f)return SeasonPalette.Blend(spring,summer,Smooth(.12f,.30f,t),snow);
            if(t<.59f)return SeasonPalette.Blend(summer,autumn,Smooth(.43f,.59f,t),snow);
            if(t<.78f)return SeasonPalette.Blend(autumn,winter,Smooth(.61f,.78f,t),snow);
            return SeasonPalette.Blend(winter,spring,Smooth(.81f,.98f,t),snow);
        }
        public static SeasonProfile For(string id,int order=0)
            =>new SeasonProfile(id,order>0?((order-1)%5)/4f:.5f);
        public bool BareTree(Vector3 root,int seed,bool conifer=false)
        {
            if(conifer)return false;
            return Variation(root,seed)<BareFraction;
        }
        public static float Variation(Vector3 root,int seed)
        {
            uint x=unchecked((uint)Mathf.RoundToInt(root.x*19)),z=unchecked((uint)Mathf.RoundToInt(root.z*19));
            uint hash=unchecked(x*73856093u^z*19349663u^(uint)seed*83492791u);
            hash^=hash>>16;hash=unchecked(hash*2246822519u);hash^=hash>>13;
            return (hash&65535)/65535f;
        }
        /// <summary>Deep scenery snow, with a compacted clear puzzle and open walking network.</summary>
        public float SnowDepth(LevelData level,Vector3 point)
        {
            if(SnowCoverage<=0||level==null||level.entry==null)return 0;
            float outside=Mathf.Max(Mathf.Abs(point.x)-level.width*.5f,Mathf.Abs(point.z)-level.height*.5f);

            return SnowDepth(level,point,CampTrail.CorridorDistance(level,point));
        }
        /// <summary>Identical snow field with a caller-cached distance to the static walking network.</summary>
        public float SnowDepth(LevelData level,Vector3 point,float pathDistance)
        {
            if(SnowCoverage<=0||level==null)return 0;
            float outside=Mathf.Max(Mathf.Abs(point.x)-level.width*.5f,Mathf.Abs(point.z)-level.height*.5f);

            var shore=EnvironmentCompositionData.For(level).shore;
            float bankDistance=ShorelineGeometry.DistanceToBank(shore,point);
            if(bankDistance<=1.25f)return 0;
            // Broad dunes and smaller wind-deposited ridges retain continuous slopes.
            float phase=(level.decorSeed&511)*.31f;
            float roll=Mathf.PerlinNoise(point.x*.13f+phase,point.z*.13f+19)*.7f
                +Mathf.PerlinNoise(point.x*.055f+phase,point.z*.055f-11)*.3f;
            roll=Mathf.SmoothStep(0,1,Mathf.Clamp01((roll-.24f)/.52f));
            float clearing=Mathf.Lerp(.025f,1,Mathf.SmoothStep(0,1,Mathf.Clamp01(outside/4.5f)));
            float path=1; // Reserved access defines walkability, not previously walked snow.
            float bank=Mathf.SmoothStep(0,1,Mathf.Clamp01((bankDistance-1.25f)/2.4f));
            return SnowCoverage*clearing*path*bank*(1-CampGroundDetails.HeatAt(level,point))*(.8f+roll*1.35f);
        }
    }
}
