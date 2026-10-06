using System;
using System.Collections.Generic;
using System.Linq;

namespace QuietCamp.Domain
{
    /// <summary>Deterministic campaign recipe. Authoring solves and freezes
    /// every candidate; scenery and weather never mutate its puzzle masks.</summary>
    public static class CampCampaignAuthoring
    {
        public const string Version = "cozy-campaign-2";
        public static int SeedForNumber(int number)=>unchecked(20261004+number*7919);
        static readonly (int w,int h,int guests)[] Sizes = {
            (5,5,2),(5,6,2),(6,5,3),(6,6,3),(6,7,3),
            (7,6,3),(6,8,3),(8,6,3),(7,7,4),(7,8,4),
            (4,6,2),(5,7,3),(6,5,3),(6,8,4),(8,6,4),
            (7,7,4),(5,8,3),(8,5,3),(7,8,4),(8,7,5),
            (6,6,3),(6,8,4),(8,6,4),(7,7,4),(8,8,6),
            (5,7,3),(7,5,3),(6,8,4),(8,6,4),(8,8,6)
        };
        // Sets index into level numbers: act-2 (31-42) adds streams and shores,
        // threshold verges for the route feel, and campfires that keep the
        // quiet-sign in play; 61-64 are the bonus side-glades.
        static readonly int[] Water = { 7,10,13,19,24,29,32,40,42 };
        static readonly int[] Fire = { 4,6,9,10,12,15,18,20,21,23,25,27,30,33,36,40,62 };
        static readonly int[] Exterior = { 6,8,9,12,14,17,19,22,24,27,29,30,32,35,38,41 };

        public static LevelData Create(string id,int number,int seed)
        {
            number=Math.Max(1,number);var spec=Sizes[(number-1)%Sizes.Length];
            for(int attempt=0;attempt<24;attempt++)
            {
                var rng=new Random(unchecked(seed+attempt*104729));
                var level=new LevelData {schemaVersion=1,ruleVersion=2,id=id,order=number,chapter=(number-1)/5+1,
                    seed=seed,decorSeed=unchecked(seed^0x5f5f),generatorVersion=Version,generationAttempt=attempt+1,
                    width=spec.w,height=spec.h,blocked=Array.Empty<int[]>(),shade=Array.Empty<int[]>(),noise=Array.Empty<int[]>(),
                    friends=Array.Empty<string[]>(),lighting=number==63?"night":number%5==0?"evening":number%4==0?"morning":"day",
                    tutorialKey=number<=8?"tutorial."+number:""};
                ConfigureAccess(level,number);
                bool shade=number==3||number>=6&&number%3==1;
                bool quiet=Fire.Contains(number)&&number!=6;
                level.guests=Enumerable.Range(1,spec.guests).Select(i=>new GuestData {id="g"+i,nameKey="guest."+i,
                    assetId=i%3==0?"tent_detailedOpen":"tent_smallOpen",shade=shade&&i==1,quiet=quiet&&i==spec.guests}).ToArray();
                if(number==5||number>=7&&number%4==3)level.friends=new[]{new[]{"g1","g2"}};
                if(shade)
                {
                    bool far=number%2==0;
                    level.canopies=new[]{new ShadeCanopyData{x=1.25f,z=far?spec.h-1.25f:1.25f,radiusX=1.6f,radiusZ=1.6f}};
                    level.shade=ShadeProjection.Cells(level);
                }
                var blocked=new List<Cell>();
                if(Fire.Contains(number))
                {
                    var fire=new Cell(spec.w-1,0);
                    if(CampAccess.IsReserved(level,fire))fire=new Cell(spec.w-2,0);
                    blocked.Add(fire);level.noise=new[]{new[]{fire.X,fire.Z}};
                }
                // The first two fields are open; later obstacles have a visible
                // cell-owning prop and remain outside the protected entry lane.
                int count=number<=2?0:number<=5?1:number<=15?2:3;
                for(int j=0;j<count;j++)
                {
                    for(int tryCell=0;tryCell<32;tryCell++)
                    {
                        var cell=new Cell(rng.Next(spec.w),rng.Next(spec.h));
                        if(blocked.Contains(cell)||CampAccess.IsReserved(level,cell))continue;
                        if(CampAccess.Points(level).Any(p=>Math.Abs(cell.X-p.X)+Math.Abs(cell.Z-p.Z)<=1))continue;
                        if(level.shade.Any(c=>c[0]==cell.X&&c[1]==cell.Z))continue;
                        blocked.Add(cell);break;
                    }
                }
                level.blocked=blocked.Select(c=>new[]{c.X,c.Z}).ToArray();
                var props=new[]{"stone_largeA","log","stump_round","tree_default"};
                level.objects=blocked.Select((cell,i)=>new EnvironmentObjectData{x=cell.X,z=cell.Z,rotation=(number+i)%4,
                    assetId=level.noise.Any(c=>c[0]==cell.X&&c[1]==cell.Z)?"campfire_stones":props[(number+i)%props.Length]}).ToArray();
                level.environment=Composition(level,number);level.environmentPreset=level.environment.biomeId=="pines"||level.environment.biomeId=="forest"?"pines":"meadow";
                var solver=new CampSolver(level,Array.Empty<Placement>(),4);
                var status=solver.Step();while(status==CampSolver.Status.Searching)status=solver.Step(.05);
                if(status!=CampSolver.Status.Solved)continue;
                level.witness=solver.Solution;
                PreferAuthoredTrailDoor(level);
                if(LevelContentValidator.Validate(level).Count==0)return level;
            }
            throw new InvalidOperationException("Campaign generation failed: "+id+" (#"+number+")");
        }
        static void PreferAuthoredTrailDoor(LevelData level)
        {
            if(level.exteriorWalkable.Length==0)return;
            // Freeze a representative solution that actually uses the visible
            // verge where possible; the independent solver remains unconstrained.
            foreach(var guest in level.guests)
                for(int x=0;x<level.width-1;x++)for(int z=0;z<level.height-1;z++)for(int q=0;q<4;q++)
                {
                    var candidate=new Placement{guestId=guest.id,x=x,z=z,rotation=q};var door=RuleEvaluator.Door(candidate);
                    if(RuleEvaluator.Inside(level,door)||!CampWalkability.Contains(level,door))continue;
                    var arranged=level.witness.Select(p=>p.guestId==guest.id?candidate:p).ToArray();
                    if(!RuleEvaluator.Evaluate(level,arranged).IsSolved)continue;
                    level.witness=arranged;return;
                }
        }
        static void ConfigureAccess(LevelData level,int number)
        {
            int side=number<=2?2:(number-3)%4;
            Cell At(int edge)=>edge==0?new Cell(level.width/2,0):edge==1?new Cell(level.width-1,level.height/2)
                :edge==2?new Cell(level.width/2,level.height-1):new Cell(0,level.height/2);
            var primary=At(side);level.entry=new[]{primary.X,primary.Z};
            var extra=new List<AccessPointData>();
            if(number>=5&&number%2==1){var p=At((side+2)%4);extra.Add(new AccessPointData{id="trail-out",kind="exit",x=p.X,z=p.Z});}
            if(number>=10&&number%7==0){var p=At((side+1)%4);extra.Add(new AccessPointData{id="forest-entry",kind="entry",x=p.X,z=p.Z});}
            level.accessPoints=extra.ToArray();
            if(!Exterior.Contains(number))return;
            var cells=new List<int[]>();
            // A narrow authored verge, not a free perimeter around the puzzle.
            // Its free neighbouring board cells provide real connections even
            // when a tent faces out beside the reserved entrance.
            if(side==0||side==2)
                for(int x=Math.Max(0,primary.X-2);x<=Math.Min(level.width-1,primary.X+2);x++)cells.Add(new[]{x,side==0?-1:level.height});
            else
                for(int z=Math.Max(0,primary.Z-2);z<=Math.Min(level.height-1,primary.Z+2);z++)cells.Add(new[]{side==3?-1:level.width,z});
            level.exteriorWalkable=cells.ToArray();
        }
        public static EnvironmentCompositionData Composition(LevelData level,int number)
        {
            int chapter=Math.Min(5,(Math.Max(1,number)-1)/5);
            var season=new[]{"spring","summer","summer","autumn","winter","spring"}[chapter];
            if(number>=61&&number<=64)season=new[]{"spring","autumn","summer","winter"}[number-61];
            bool water=Water.Contains(number);
            string biome=water?"shore":number==64?"pines":chapter==4?"pines":number%3==0?"meadow":"forest";
            string weather=new[]{7,13,17,19}.Contains(number)?"rain":new[]{16,21,24,29}.Contains(number)?"mist":number%4==0?"cloudy":"clear";
            var env=new EnvironmentCompositionData {biomeId=biome,seasonId=season,weatherId=weather,
                moisture=weather=="rain"?.9f:weather=="mist"?.82f:weather=="cloudy"?.48f:.26f,
                treeDensity=biome=="meadow"?.25f:biome=="shore"?.55f:chapter==0?.48f:chapter==4?.8f:.84f,
                clusterSeed=unchecked(level.decorSeed*3571+29),
                meadowSpecies=chapter==0?new[]{"grass","daisy"}:chapter==1?new[]{"grass","poppy","daisy"}:
                    chapter==2?new[]{"grass","wheat","daisy"}:chapter==3?new[]{"grass","wheat"}:chapter==4?new[]{"grass"}:new[]{"grass","daisy","poppy"},
                storyMotifs=chapter==0?new[]{"marker","bench"}:chapter==1?new[]{"bag","lantern"}:
                    chapter==2?new[]{"ruin","bench"}:chapter==3?new[]{"ruin","marker"}:chapter==4?new[]{"lantern","bench"}:new[]{"ruin","flowers","repaired-marker"}};
            if(water)
            {
                var sides=new[]{"left","right","back","front"};
                bool OnSide(Cell p,string side)=>side=="left"?p.X==0:side=="right"?p.X==level.width-1:side=="back"?p.Z==0:p.Z==level.height-1;
                string side=sides.First(s=>!CampAccess.Points(level).Any(p=>OnSide(p,s)));
                float width=number<=10?2.2f:7;
                float half=(side=="left"||side=="right"?level.width:level.height)*.5f;
                env.shore=new ShorelineData {kind=number<=10?"stream":"lake",side=side,width=width,offset=half+width*.5f+3,seed=level.decorSeed};
            }
            return env;
        }
    }
}
