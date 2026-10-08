using System;
using System.Collections.Generic;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    /// <summary>Offline authoring pipeline. Runtime scrolling only consumes its static output.</summary>
    public static class RoadmapCompiler
    {
        /// <summary>Offline world pipeline. The same lightweight summaries feed any campaign length.</summary>
        public static RoadmapDefinition BuildWorld(IReadOnlyList<LevelSummary> levels,IReadOnlyList<BonusCampDefinition> bonuses,string revision="world-1",string journeyId="main")
        {
            var map=Build(levels,bonuses,revision,journeyId);map.presentation="world3d";
            foreach(var region in map.regions)
            {
                var level=levels[region.firstLevel-1];region.lighting=level.lighting??"day";
                region.vegetationProfile=region.biome.Contains("forest")||region.biome.Contains("pine")?"forest-clusters":"meadow-flowers";
                region.terrainProfile="gentle-hills";region.environmentalMotifs=level.environment?.storyMotifs??Array.Empty<string>();
                var chunks=new List<RoadmapChunkData>();
                for(int order=region.firstLevel;order<=region.lastLevel;order+=6)
                    chunks.Add(new RoadmapChunkData{id=region.id+":chunk:"+chunks.Count,firstOrder=order,lastOrder=Math.Min(order+5,region.lastLevel)});
                region.chunks=chunks.ToArray();
            }
            return map;
        }
        public static RoadmapDefinition Build(IReadOnlyList<LevelSummary> levels,IReadOnlyList<BonusCampDefinition> bonuses,string revision="regions-1",string journeyId="main")
        {
            if(levels==null||levels.Count==0)throw new ArgumentException("No summaries");
            var regions=new List<RoadmapRegionData>();string previous=null;int start=0;
            while(start<levels.Count)
            {
                string season=levels[start].environment?.seasonId??"summer";int end=start+1;
                while(end<levels.Count&&end-start<10&&(levels[end].environment?.seasonId??"summer")==season)end++;
                var nodes=new List<RoadmapNodeData>();var branches=new List<RoadmapBranchData>();int extra=0;
                for(int i=start;i<end;i++)
                {
                    var level=levels[i];string id="node:"+level.id;
                    nodes.Add(new RoadmapNodeData{id=id,levelId=level.id,order=i+1,titleKey="menu.levels",previewId=level.id,
                        x=.5f+(float)Math.Sin(i*.85)*.21f,y=210+(i-start)*420+extra,
                        requires=i==0?Array.Empty<string>():new[]{levels[i-1].id},world=BakeWorld(level)});
                    foreach(var bonus in bonuses??Array.Empty<BonusCampDefinition>())if(bonus.afterLevel==i+1)
                    {
                        branches.Add(new RoadmapBranchData{id="branch:"+bonus.id,anchorNodeId=id,bonusId=bonus.id,titleKey=bonus.titleKey,
                            previewId="bonus:"+bonus.id,x=(bonus.afterLevel/10)%2==1?.25f:.75f,y=nodes[nodes.Count-1].y+420,
                            published=!string.IsNullOrEmpty(bonus.levelId),requires=new[]{level.id}});extra+=500;
                    }
                }
                regions.Add(new RoadmapRegionData{id=journeyId+":region:"+regions.Count,titleKey="map.region."+season,
                    biome=levels[start].environment?.biomeId??levels[start].environmentPreset??"forest",season=season,landmark="sign",
                    previewId=levels[start].id,firstLevel=start+1,lastLevel=end,height=(end-start)*420+extra+200,
                    transition=new RoadmapTransitionData{fromSeason=previous??season,toSeason=season,length=Math.Min(1680,(end-start)*420+extra+200),kind=Next(previous,season)?"blend":"chapter"},
                    nodePositions=nodes.ToArray(),branches=branches.ToArray(),storyProps=new[]{new RoadmapStoryPropData{assetId="sign",motif="shelter-marker",x=-6,y=4,height=.7f}}});
                previous=season;start=end;
            }
            return new RoadmapDefinition{revision=revision,journeyId=journeyId,regions=regions.ToArray()};
        }
        /// <summary>Rebuild only derived geometry; author-owned layout, context and branches survive export.</summary>
        public static RoadmapDefinition BakeAuthored(RoadmapDefinition authored,IReadOnlyList<LevelSummary> levels)
        {
            if(authored==null)throw new ArgumentNullException(nameof(authored));
            var summaries=new Dictionary<string,LevelSummary>(StringComparer.Ordinal);
            foreach(var level in levels)summaries.Add(level.id,level);
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var region in authored.regions??Array.Empty<RoadmapRegionData>())
                foreach(var node in region.nodePositions??Array.Empty<RoadmapNodeData>())
                {
                    if(!summaries.TryGetValue(node.levelId,out var level))throw new ArgumentException("Missing authored level: "+node.levelId);
                    if(!seen.Add(node.levelId))throw new ArgumentException("Duplicate authored level: "+node.levelId);
                    if(region.season!=(level.environment?.seasonId??"summer"))throw new ArgumentException("Authored season disagrees with level: "+node.levelId);
                    node.world=BakeWorld(level,region.environmentalStory);
                }
            if(seen.Count!=summaries.Count)throw new ArgumentException("Authoring does not cover every summary");
            var errors=RoadmapValidator.Validate(authored);if(errors.Count>0)throw new ArgumentException(string.Join("; ",errors));
            return authored;
        }
        static bool Next(string a,string b)=>a==null||a==b||a=="spring"&&b=="summer"||a=="summer"&&b=="autumn"||a=="autumn"&&b=="winter"||a=="winter"&&b=="spring";
        public static RoadmapWorldData BakeWorld(LevelSummary level,EnvironmentalStoryData regionStory=null)
        {
            var props=new List<RoadmapPropData>();float w=level.width*.5f,h=level.height*.5f;
            void Add(string asset,float x,float z,float height,bool sway=false,float yaw=0)
                =>props.Add(new RoadmapPropData{assetId=asset,x=x,z=z,height=height,sway=sway,yaw=yaw});
            foreach(var obj in level.mapObjects??Array.Empty<EnvironmentObjectData>())
                Add(obj.assetId,obj.x+.5f-w,obj.z+.5f-h,obj.assetId.StartsWith("tree")?2.2f:obj.assetId.Contains("campfire")?.42f:.55f,obj.assetId.StartsWith("tree"),obj.rotation*90);
            // No puzzle witnesses. These vignettes represent life around, not solutions on, the board.
            var rng=new Random(unchecked(level.decorSeed*971+37));bool winter=level.environment?.seasonId=="winter",autumn=level.environment?.seasonId=="autumn";
            int trees=6+(int)(12*Math.Max(0,Math.Min(1,level.environment?.treeDensity??.5f)));
            for(int attempt=0,placed=0;attempt<200&&placed<trees;attempt++)
            {
                float x=(float)(rng.NextDouble()*2-1)*(w+4.4f),z=(float)(rng.NextDouble()*2-1)*(h+4.4f);
                if(Math.Abs(x)<w+1.8f&&Math.Abs(z)<h+1.8f||Reserved(level,x,z,1.1f))continue;
                Add(level.environmentPreset=="pines"||rng.Next(4)==0?"tree_pineRoundA":"tree_default",x,z,1.8f+(float)rng.NextDouble()*1.4f,true,rng.Next(360));placed++;
                if(!winter&&(!autumn||rng.Next(3)==0))Add("plant_bushSmall",x+.6f,z-.65f,.5f,true,rng.Next(360));
            }
            if(!winter)for(int i=0;i<(autumn?9:22);i++)
            {
                float x=(float)(rng.NextDouble()*2-1)*(w+4.3f),z=(float)(rng.NextDouble()*2-1)*(h+4.3f);
                if(Math.Abs(x)<w+.55f&&Math.Abs(z)<h+.55f||Reserved(level,x,z,.5f))continue;
                Add(autumn?"plant_bushSmall":i%5==0?"flower_yellowA":i%7==0?"flower_purpleA":"grass_leafsLarge",x,z,.18f+(float)rng.NextDouble()*.26f,true,rng.Next(360));
            }
            // Explicit, quiet story motifs. No fabricated artefacts or historical evidence.
            var motifs=new HashSet<string>(StringComparer.Ordinal);
            foreach(var motif in level.environment?.storyMotifs??Array.Empty<string>())
            {
                string asset=motif=="marker"||motif=="repaired-marker"?"sign":motif=="bench"||motif=="pier"?"log_stack":motif=="foundation"||motif=="ruin"||motif=="wall"||motif=="settlement"||motif=="chimney"?"story_foundation":motif=="flowers"?"story_memorial_garden":motif=="ship"?"story_workboat":null;
                if(asset!=null&&motifs.Add(asset)&&!Reserved(level,-w-2.8f-motifs.Count*.7f,h+2.5f,1))Add(asset,-w-2.8f-motifs.Count*.7f,h+2.5f,.6f,false,90);
            }
            RoadmapStoryCompiler.Bake(regionStory??level.environmentalStory,level.id,props);
            props.Sort((a,b)=>(b.x+b.z).CompareTo(a.x+a.z));
            // Conservative pixel extent including tall trees, low sun shadows and animated padding.
            return new RoadmapWorldData{extent=580,props=props.ToArray()};
        }
        static bool Reserved(LevelSummary level,float x,float z,float radius)
        {
            foreach(var cell in level.exteriorWalkable??Array.Empty<int[]>())if(cell!=null&&cell.Length==2&&Math.Abs(x-(cell[0]+.5f-level.width*.5f))<radius&&Math.Abs(z-(cell[1]+.5f-level.height*.5f))<radius)return true;
            var shore=level.environment?.shore;
            if(shore!=null)
            {
                // Same band and seeded bend as ShorelineGeometry, without Unity dependencies.
                float sx=shore.side=="left"?-1:shore.side=="right"?1:0;
                float sz=shore.side=="back"?-1:shore.side=="front"?1:0;
                float along=x*sz-z*sx,axis=x*sx+z*sz;
                double bend=Math.Sin(along*.27+shore.seed*.01)*.48+Math.Sin(along*.63)*.12;
                if(Math.Abs(axis-shore.offset-bend)-Math.Max(1.2,shore.width)*.5<radius)return true;
            }
            return false;
        }
    }
}
