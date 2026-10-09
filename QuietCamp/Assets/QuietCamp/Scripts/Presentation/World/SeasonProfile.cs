using QuietCamp.Domain;
using UnityEngine;

namespace QuietCamp.Presentation.World
{
    /// <summary>Visual phenology shared by scenes and summaries. Never edits saved level data.</summary>
    public readonly struct SeasonProfile
    {
        public readonly string Id;
        public readonly float Progress,GrassWeight,ShrubWeight,LeafLitter,PollenWeight;
        public bool Winter=>Id=="winter";
        public bool Autumn=>Id=="autumn";
        public SeasonPalette Palette=>SeasonPalette.For(Id,Progress);
        SeasonProfile(string id,float progress)
        {
            Id=string.IsNullOrEmpty(id)?"summer":id;Progress=Mathf.Clamp01(progress);
            bool winter=Id=="winter",autumn=Id=="autumn";
            GrassWeight=winter?0:autumn?Mathf.Lerp(.42f,.10f,Progress):Id=="spring"?.90f:1;
            ShrubWeight=winter?0:autumn?Mathf.Lerp(.40f,.12f,Progress):1;
            LeafLitter=autumn?Mathf.Lerp(.40f,1,Progress):0;
            PollenWeight=winter?0:autumn?.06f:Id=="spring"?1:.65f;
        }
        public static SeasonProfile For(LevelData level)
            =>For(EnvironmentCompositionData.For(level).seasonId,level?.order??0);
        public static SeasonProfile For(LevelSummary level)
            =>For(level?.environment?.seasonId,level?.number??0);
        public static SeasonProfile For(string id,int order=0)
            =>new SeasonProfile(id,order>0?((order-1)%5)/4f:.5f);
        public bool BareTree(Vector3 root,int seed,bool conifer=false)
        {
            if(conifer)return false;
            return Winter||(Autumn&&Variation(root,seed)<Mathf.Lerp(.025f,.42f,Progress));
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
            if(!Winter||level==null||level.entry==null)return 0;
            float outside=Mathf.Max(Mathf.Abs(point.x)-level.width*.5f,Mathf.Abs(point.z)-level.height*.5f);
            if(outside<.6f)return 0;
            return SnowDepth(level,point,CampTrail.CorridorDistance(level,point));
        }
        /// <summary>Identical snow field with a caller-cached distance to the static walking network.</summary>
        public float SnowDepth(LevelData level,Vector3 point,float pathDistance)
        {
            if(!Winter||level==null)return 0;
            float outside=Mathf.Max(Mathf.Abs(point.x)-level.width*.5f,Mathf.Abs(point.z)-level.height*.5f);
            if(outside<.6f||pathDistance<=.85f)return 0;
            var shore=EnvironmentCompositionData.For(level).shore;
            float bankDistance=ShorelineGeometry.DistanceToBank(shore,point);
            if(bankDistance<=1.25f)return 0;
            // Broad dunes and smaller wind-deposited ridges retain continuous slopes.
            float phase=(level.decorSeed&511)*.31f;
            float roll=Mathf.PerlinNoise(point.x*.13f+phase,point.z*.13f+19)*.7f
                +Mathf.PerlinNoise(point.x*.055f+phase,point.z*.055f-11)*.3f;
            roll=Mathf.SmoothStep(0,1,Mathf.Clamp01((roll-.24f)/.52f));
            float clearing=Mathf.SmoothStep(0,1,Mathf.Clamp01((outside-.6f)/4.5f));
            float path=Mathf.SmoothStep(0,1,Mathf.Clamp01((pathDistance-.85f)/2.8f));
            float bank=Mathf.SmoothStep(0,1,Mathf.Clamp01((bankDistance-1.25f)/2.4f));
            return clearing*path*bank*(1-CampGroundDetails.HeatAt(level,point))*(.8f+roll*1.35f);
        }
    }
}
