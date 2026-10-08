using System;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    /// <summary>World-space rural motifs. Sampling never depends on viewport, node count or chunk edges.</summary>
    public static class RoadmapRuralLayout
    {
        public const float StripLength=640,PathWidth=26;
        public const float DistancePerUnit=22.936256f; // projection scale 28 * sin(55 degrees)
        public const int Parts=15;
        public static readonly string[] ModelIds={"ua_power_pylon","ua_rural_pole","ua_wattle_fence","ua_picket_fence",
            "ua_concrete_fence","ua_gate","ua_wheat_patch","ua_sunflower_patch","ua_orchard_tree","ua_bus_shelter","ua_whitewashed_house","ua_well"};
        public readonly struct Placement
        {
            public readonly string Asset;
            public readonly float X,Distance,Height,Yaw;
            public readonly bool Sway;
            public Placement(string asset,float x,float distance,float height,float yaw=0,bool sway=false)
            {Asset=asset;X=x;Distance=distance;Height=height;Yaw=yaw;Sway=sway;}
        }
        public static float Unit(int seed,int strip,int salt)
        {
            unchecked
            {
                uint value=(uint)seed^(uint)strip*0x9e3779b9u^(uint)salt*0x85ebca6bu;
                value^=value>>16;value*=0x7feb352du;value^=value>>15;value*=0x846ca68bu;value^=value>>16;
                return (value&0xffffff)/16777216f;
            }
        }
        public static bool TrySample(RoadmapCulturalLandscapeData profile,int strip,int part,out Placement placement)
        {
            placement=default;
            if(profile==null||profile.profile!="ukrainian-rural"||strip<0||part<0||part>=Parts)return false;
            float centre=(strip+.5f)*StripLength,side=Unit(profile.seed,strip,2)<.5f?-1:1;
            float offset=(Unit(profile.seed,strip,3)-.5f)*100;
            bool field=Unit(profile.seed,strip,4)<profile.fields;
            bool orchard=Unit(profile.seed,strip,5)<profile.orchards;
            bool settlement=Unit(profile.seed,strip,6)<profile.settlements;
            if(part<3)
            {
                if(!field)return false;
                placement=new Placement("ua_wheat_patch",side*(8.1f+part*.8f),centre+offset+(part-1)*95,.78f,part*11,true);
            }
            else if(part==3)
            {
                if(!field||strip%3!=1)return false;
                placement=new Placement("ua_sunflower_patch",side*12.0f,centre+offset+175,1.15f,12,true);
            }
            else if(part==4||part==5)
            {
                if(!orchard)return false;
                placement=new Placement("ua_orchard_tree",-(8.9f+(part-4)*2.9f),centre+offset-160,2.6f,part*41,true);
            }
            else if(part==6)
            {
                if(!settlement)return false;
                placement=new Placement("ua_whitewashed_house",-12.5f,centre+offset,2.6f,100);
            }
            else if(part>=7&&part<=10)
            {
                if(!settlement&&!field)return false;
                string fence=strip%3==0?"ua_wattle_fence":strip%3==1?"ua_picket_fence":"ua_concrete_fence";
                float height=part==10?1.455f:fence=="ua_wattle_fence"?1.075f:fence=="ua_picket_fence"?1.125f:1.335f;
                placement=new Placement(part==10?"ua_gate":fence,-7.1f,centre+offset-90+(part-7)*65,height,90);
            }
            else if(part==11)
            {
                if(!settlement)return false;
                placement=new Placement(strip%2==0?"ua_bus_shelter":"ua_well",-8.8f,centre+offset+180,strip%2==0?1.8f:1.1f,180);
            }
            else if(part==12)
            {
                if(!profile.powerLine||strip%3!=0)return false;
                // One continuous utility corridor; adjacent supports share the exact same lane.
                placement=new Placement("ua_power_pylon",profile.width*.15f,centre,5.8f,0);
            }
            else
            {
                if(!settlement)return false;
                placement=new Placement("ua_rural_pole",-8.8f,centre+offset+245+(part-13)*120,3.0f,0);
            }
            return true;
        }
        /// <summary>Same clearing/road/branch exclusion for offline QA and the native mesh builder.</summary>
        public static bool ClearOfPath(RoadmapCatalog map,float x,float distance,float radius)
        {
            int index=map.NodeAt(distance);float z=-distance/DistancePerUnit;
            float X(int node)=>(map.Nodes[node].x-.5f)*PathWidth;
            float Z(int node)=>-map.Y(node)/DistancePerUnit;
            float Square(float dx,float dz)=>dx*dx+dz*dz;
            for(int n=Math.Max(0,index-2);n<=Math.Min(map.Nodes.Length-1,index+2);n++)
            {
                float ax=X(n),az=Z(n);if(Square(x-ax,z-az)<(5.9f+radius)*(5.9f+radius))return false;
                if(n+1>=map.Nodes.Length)continue;float bz=Z(n+1),t=Math.Clamp((z-az)/(bz-az),0,1);
                float px=ax+(X(n+1)-ax)*t*t*(3-2*t);
                if(z<=az&&z>=bz&&Math.Abs(x-px)<radius+.9f)return false;
            }
            int region=map.RegionAt(distance);
            for(int r=Math.Max(0,region-1);r<=Math.Min(map.Definition.regions.Length-1,region+1);r++)
                foreach(var branch in map.Definition.regions[r].branches)
                {
                    int anchor=map.NodeIndex(branch.anchorNodeId);if(anchor<0)continue;
                    float bx=(branch.x-.5f)*PathWidth,bz=-(map.RegionStarts[r]+branch.y)/DistancePerUnit;
                    if(Square(x-bx,z-bz)<(3+radius)*(3+radius))return false;
                    float ax=X(anchor),az=Z(anchor);float px=ax,pz=az;
                    // Same eighteen smoothstep segments used by the native branch trail.
                    for(int segment=1;segment<=18;segment++)
                    {
                        float u=segment/18f,qx=ax+(bx-ax)*u*u*(3-2*u),qz=az+(bz-az)*u;
                        float dx=qx-px,dz=qz-pz,t=Math.Clamp(((x-px)*dx+(z-pz)*dz)/Math.Max(.001f,dx*dx+dz*dz),0,1);
                        if(Square(x-px-dx*t,z-pz-dz*t)<(radius+.6f)*(radius+.6f))return false;
                        px=qx;pz=qz;
                    }
                }
            return true;
        }
        public static float Radius(string asset)=>asset=="ua_whitewashed_house"?3.0f:asset=="ua_bus_shelter"?1.65f:
            asset=="ua_orchard_tree"?2.05f:asset=="ua_power_pylon"?1.65f:asset=="ua_well"?.75f:
            asset=="ua_rural_pole"?.5f:asset=="ua_wheat_patch"||asset=="ua_sunflower_patch"?1.9f:1.48f;
        public static bool InSeason(string asset,RoadmapEnvironmentSample climate)
        {
            if(asset=="ua_wheat_patch")return climate.Snow<=.55f;
            if(asset=="ua_sunflower_patch")return climate.Snow<=.55f&&climate.Temperature>=.4f;
            return true;
        }
        public static bool Admitted(RoadmapCatalog map,RoadmapEnvironmentSampler climate,Placement placement)=>
            placement.Distance>=0&&placement.Distance<map.Height&&InSeason(placement.Asset,climate.Sample(placement.Distance))
            &&ClearOfPath(map,placement.X,placement.Distance,Radius(placement.Asset))
            &&ClearOfWater(climate,placement.X,placement.Distance,Radius(placement.Asset));
        public static bool ClearOfWater(RoadmapEnvironmentSampler climate,float x,float distance,float radius)
        {
            if(climate.Sample(distance).Water<.3f)return true;
            float z=-distance/DistancePerUnit;
            float bank=PathWidth*.5f*.68f+(float)Math.Sin(z*.065f)*1.4f;
            return Math.Abs(x-bank)>=2+radius;
        }
    }
}
