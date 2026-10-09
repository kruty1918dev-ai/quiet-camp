using System;
using System.Collections.Generic;
using QuietCamp.Domain;

namespace QuietCamp.Application
{
    public static class RoadmapValidator
    {
        public static List<string> Validate(RoadmapDefinition data,Func<string,bool> previewExists=null,Func<string,bool> localized=null,Func<string,bool> modelExists=null,Func<string,bool> journeyExists=null)
        {
            var errors=new List<string>();
            if(data==null||data.schemaVersion!=1||data.regions==null||data.regions.Length==0){errors.Add("missing-region");return errors;}
            if(string.IsNullOrEmpty(data.revision)||data.revealDistance<0||data.revealDistance>4)errors.Add("invalid-reveal-policy");
            if(data.presentation!="projected"&&data.presentation!="world3d")errors.Add("invalid-presentation");
            var ids=new HashSet<string>(StringComparer.Ordinal);var nodes=new Dictionary<string,RoadmapNodeData>(StringComparer.Ordinal);
            var regions=new HashSet<string>(StringComparer.Ordinal);var levels=new HashSet<string>(StringComparer.Ordinal);var levelOrders=new Dictionary<string,int>(StringComparer.Ordinal);
            int order=1;string previousSeason=null;
            bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
            bool Id(string id){if(string.IsNullOrWhiteSpace(id)||!ids.Add(id)){errors.Add("duplicate-id:"+id);return false;}return true;}
            void Preview(string id){if(string.IsNullOrWhiteSpace(id)||previewExists!=null&&!previewExists(id))errors.Add("missing-preview:"+id);}
            void Locale(string key){if(string.IsNullOrWhiteSpace(key)||localized!=null&&!localized(key))errors.Add("missing-localization:"+key);}
            for(int r=0;r<data.regions.Length;r++)
            {
                var region=data.regions[r];if(region==null){errors.Add("missing-region");continue;}
                Id(region.id);regions.Add(region.id??"");Locale(region.titleKey);Preview(region.previewId);
                var rural=region.culturalLandscape;
                if(rural!=null)
                {
                    if(data.presentation!="world3d"||rural.profile!="ukrainian-rural"||!Finite(rural.width)||rural.width<64||rural.width>96
                        ||!Finite(rural.fields)||rural.fields<0||rural.fields>1||!Finite(rural.orchards)||rural.orchards<0||rural.orchards>1
                        ||!Finite(rural.settlements)||rural.settlements<0||rural.settlements>1)errors.Add("invalid-cultural-profile:"+region.id);
                    if(r>0&&(data.regions[r-1]?.culturalLandscape==null||data.regions[r-1].culturalLandscape.width!=rural.width))errors.Add("inconsistent-cultural-world:"+region.id);
                    if(modelExists!=null)foreach(var modelId in RoadmapRuralLayout.ModelIds)if(!modelExists(modelId))errors.Add("missing-model:"+modelId);
                }
                else if(r>0&&data.regions[r-1]?.culturalLandscape!=null)errors.Add("inconsistent-cultural-world:"+region.id);
                if(!Finite(region.height)||region.height<420||region.nodePositions==null||region.nodePositions.Length==0||region.nodePositions.Length>(data.presentation=="world3d"?3000:10))errors.Add("region-budget:"+region.id);
                if(!Season(region.season)||region.transition==null||region.transition.toSeason!=region.season||previousSeason!=null&&region.transition.fromSeason!=previousSeason||region.transition!=null&&(!Finite(region.transition.length)||region.transition.length<0||region.transition.length>region.height||!Transition(region.transition.fromSeason,region.transition.toSeason,region.transition.kind)))errors.Add("invalid-season-transition:"+region.id);
                if(!RoadmapEnvironmentSampler.ValidPhase(region.environmentPhase)||region.environmentPhase!=null&&new RoadmapEnvironmentProfile(region.environmentPhase,region.biome).Season!=region.season)errors.Add("invalid-environment-phase:"+region.id);
                previousSeason=region.season;
                if(region.performanceTier!="low"&&region.performanceTier!="balanced"&&region.performanceTier!="high")errors.Add("invalid-performance-tier:"+region.id);
                if(string.IsNullOrWhiteSpace(region.biome)||string.IsNullOrWhiteSpace(region.landmark))errors.Add("region-context:"+region.id);
                if(data.presentation=="world3d")
                {
                    if(string.IsNullOrWhiteSpace(region.lighting)||string.IsNullOrWhiteSpace(region.vegetationProfile)||string.IsNullOrWhiteSpace(region.terrainProfile)||!Finite(region.weatherBias)||region.weatherBias<0||region.weatherBias>1)errors.Add("invalid-world-profile:"+region.id);
                    if(region.revealRules==null||region.revealRules.nearFuture<0||region.revealRules.nearFuture>2||region.revealRules.branchTeaser<0||region.revealRules.branchTeaser>2)errors.Add("invalid-region-reveal:"+region.id);
                    if(region.chunks==null||region.chunks.Length==0)errors.Add("invalid-chunk-coverage:"+region.id);
                    int firstChunk=region.firstLevel;
                    foreach(var chunk in region.chunks??Array.Empty<RoadmapChunkData>())
                    {if(chunk==null){errors.Add("missing-chunk:"+region.id);continue;}Id(chunk.id);if(chunk.firstOrder!=firstChunk||chunk.lastOrder<chunk.firstOrder||chunk.lastOrder>region.lastLevel||chunk.lastOrder-chunk.firstOrder>=10)errors.Add("invalid-chunk-coverage:"+chunk.id);firstChunk=chunk.lastOrder+1;}
                    if(firstChunk!=region.lastLevel+1)errors.Add("invalid-chunk-coverage:"+region.id);
                }
                errors.AddRange(EnvironmentalStoryValidator.Validate(region.environmentalStory,
                    id=>Array.Exists(region.nodePositions??Array.Empty<RoadmapNodeData>(),n=>n!=null&&n.levelId==id),modelExists));
                var history=region.historical;
                if(history!=null&&!string.IsNullOrEmpty(history.contextId))
                {
                    if(history.interpretation!="inspired"||!history.verified||history.sources==null||history.sources.Length==0)errors.Add("unverified-history:"+region.id);
                    foreach(var url in history.sources??Array.Empty<string>())if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https")errors.Add("invalid-history-source:"+region.id);
                }
                if(!string.IsNullOrEmpty(history?.cultureKey))Locale(history.cultureKey);
                if(region.storyProps==null||region.storyProps.Length>12)errors.Add("story-budget:"+region.id);
                foreach(var prop in region.storyProps??Array.Empty<RoadmapStoryPropData>())
                    if(prop==null||string.IsNullOrEmpty(prop.assetId)||!Finite(prop.x)||!Finite(prop.y)||!Finite(prop.height)||prop.height<=0||prop.interpretation!="fictional")errors.Add("invalid-story-prop:"+region.id);
                if(region.branches!=null&&region.branches.Length>4)errors.Add("branch-budget:"+region.id);
                if(modelExists!=null)
                {
                    if(!modelExists(region.landmark))errors.Add("missing-model:"+region.landmark);
                    foreach(var prop in region.storyProps??Array.Empty<RoadmapStoryPropData>())if(prop!=null&&!modelExists(prop.assetId))errors.Add("missing-model:"+prop.assetId);
                }
                if(region.revealRules!=null&&(!Finite(region.revealRules.farLandmarkHeight)||region.revealRules.farLandmarkHeight<1||region.revealRules.farLandmarkHeight>15||!Finite(region.revealRules.farLandmarkX)||region.revealRules.farLandmarkX<0||region.revealRules.farLandmarkX>1))errors.Add("invalid-landmark-reveal:"+region.id);
                if(modelExists!=null&&!string.IsNullOrEmpty(region.revealRules?.farLandmarkAssetId)&&!modelExists(region.revealRules.farLandmarkAssetId))errors.Add("missing-model:"+region.revealRules.farLandmarkAssetId);
                var positions=region.nodePositions??Array.Empty<RoadmapNodeData>();float lastY=-1;
                if(region.firstLevel!=order||region.lastLevel!=order+positions.Length-1)errors.Add("broken-progress-order:"+region.id);
                foreach(var node in positions)
                {
                    if(node==null){errors.Add("missing-node:"+region.id);continue;}
                    if(Id(node.id))nodes[node.id]=node;
                    if(!levels.Add(node.levelId??""))errors.Add("duplicate-level:"+node.levelId);
                    else levelOrders.Add(node.levelId??"",node.order);
                    if(node.order!=order++||!Finite(node.y)||node.y<=lastY)errors.Add("broken-progress-order:"+node.id);lastY=node.y;
                    if(!Finite(node.x)||node.x<.15f||node.x>.85f||!Finite(node.radius)||node.radius<48||node.y<node.radius||node.y+node.radius>region.height)errors.Add("node-bounds:"+node.id);
                    if(string.IsNullOrWhiteSpace(node.levelId))errors.Add("missing-level:"+node.id);
                    Locale(node.titleKey);Preview(node.previewId);
                    if(node.world==null||!Finite(node.world.extent)||node.world.extent<230||node.world.props==null||node.world.props.Length>80)errors.Add("world-budget:"+node.id);
                    if(modelExists!=null)foreach(var prop in node.world?.props??Array.Empty<RoadmapPropData>())if(prop!=null&&!modelExists(prop.assetId))errors.Add("missing-model:"+prop.assetId);
                    foreach(var prop in node.world?.props??Array.Empty<RoadmapPropData>())
                    {
                        if(prop?.storyId!=null&&(prop.storyVisibility!="context"&&prop.storyVisibility!="silhouette"||prop.storyAppearance!="always"&&prop.storyAppearance!="before-care"&&prop.storyAppearance!="after-care"))errors.Add("invalid-story-view:"+node.id);
                        if(prop==null||string.IsNullOrEmpty(prop.assetId)||!Finite(prop.x)||!Finite(prop.z)||!Finite(prop.height)||prop.height<=0||!Finite(prop.yaw))errors.Add("invalid-prop:"+node.id);
                    }
                }
                for(int a=0;a<positions.Length;a++)for(int b=a+1;b<positions.Length;b++)if(positions[a]!=null&&positions[b]!=null&&Overlap(positions[a].x,positions[a].y,positions[a].radius,positions[b].x,positions[b].y,positions[b].radius))errors.Add("node-overlap:"+positions[a].id);
                var branches=region.branches??Array.Empty<RoadmapBranchData>();
                foreach(var branch in branches)
                {
                    if(branch==null){errors.Add("unreachable-branch");continue;}Id(branch.id);Locale(branch.titleKey);Preview(branch.previewId);
                    if(!Finite(branch.x)||!Finite(branch.y)||branch.x<.15f||branch.x>.85f||branch.y<branch.radius||branch.y+branch.radius>region.height||!Finite(branch.radius)||branch.radius<48)errors.Add("branch-bounds:"+branch.id);
                    if(!string.IsNullOrEmpty(branch.journeyId)&&journeyExists!=null&&!journeyExists(branch.journeyId))errors.Add("unreachable-branch:"+branch.id);
                    if(branch.teaserState!="hidden"&&branch.teaserState!="silhouette"&&branch.teaserState!="visible")errors.Add("invalid-teaser:"+branch.id);
                    if(branch.type!="bonus"&&branch.type!="mini-trail"&&branch.type!="story-journey"||branch.teaserDepth<0||branch.teaserDepth>2)errors.Add("invalid-branch-type:"+branch.id);
                    if((branch.nodes?.Length??0)>32)errors.Add("branch-node-budget:"+branch.id);
                    if(!RoadmapBranchPolicy.ValidAccess(branch.accessRule))errors.Add("invalid-branch-access:"+branch.id);
                    if(!string.IsNullOrEmpty(branch.descriptionKey))Locale(branch.descriptionKey);
                    if(branch.season!=null&&!Season(branch.season))errors.Add("invalid-branch-profile:"+branch.id);
                    if(!string.IsNullOrEmpty(branch.heroLandmark)&&modelExists!=null&&!modelExists(branch.heroLandmark))errors.Add("missing-model:"+branch.heroLandmark);
                    if(branch.world==null||(branch.world.props?.Length??0)>40)errors.Add("branch-world-budget:"+branch.id);
                    foreach(var prop in branch.world?.props??Array.Empty<RoadmapPropData>())
                        if(prop==null||!Finite(prop.x)||!Finite(prop.z)||!Finite(prop.height)||prop.height<=0||!Finite(prop.yaw)||string.IsNullOrEmpty(prop.assetId)||modelExists!=null&&!modelExists(prop.assetId))errors.Add("invalid-branch-prop:"+branch.id);
                    var accessMethods=new HashSet<string>();
                    foreach(var option in branch.accessOptions??Array.Empty<RoadmapBranchAccessOption>())
                        if(option==null||!RoadmapBranchPolicy.ValidAccess(option.method)||!accessMethods.Add(option.method)
                            ||option.method=="rewarded"&&string.IsNullOrWhiteSpace(option.placement)
                            ||option.method=="permanent-purchase"&&string.IsNullOrWhiteSpace(option.productId)
                            ||option.method=="subscription"&&string.IsNullOrWhiteSpace(option.entitlementId))errors.Add("invalid-branch-access:"+branch.id);
                    int branchLength=branch.nodes?.Length??0;
                    if(branchLength>0&&(branch.type=="bonus"&&branchLength!=1||branch.type=="mini-trail"&&(branchLength<3||branchLength>5)||branch.type=="story-journey"&&branchLength<8))errors.Add("invalid-branch-length:"+branch.id);
                    foreach(var detail in branch.nodes??Array.Empty<RoadmapBranchNodeData>())
                    {
                        if(detail==null){errors.Add("missing-branch-node:"+branch.id);continue;}
                        Id(detail.id);Preview(detail.previewId);
                        if(string.IsNullOrWhiteSpace(detail.levelId)||!Finite(detail.x)||!Finite(detail.y))errors.Add("invalid-branch-node:"+branch.id);
                        if(detail.world==null||(detail.world.props?.Length??0)>80)errors.Add("world-budget:"+detail.id);
                        foreach(var prop in detail.world?.props??Array.Empty<RoadmapPropData>())
                        {
                            if(prop==null||string.IsNullOrEmpty(prop.assetId)||!Finite(prop.x)||!Finite(prop.z)||!Finite(prop.height)||prop.height<=0||!Finite(prop.yaw))errors.Add("invalid-prop:"+detail.id);
                            else if(modelExists!=null&&!modelExists(prop.assetId))errors.Add("missing-model:"+prop.assetId);
                        }
                    }
                    foreach(var node in positions)if(node!=null&&Overlap(node.x,node.y,node.radius,branch.x,branch.y,branch.radius))errors.Add("node-overlap:"+branch.id);
                }
                for(int a=0;a<branches.Length;a++)for(int b=a+1;b<branches.Length;b++)if(branches[a]!=null&&branches[b]!=null&&Overlap(branches[a].x,branches[a].y,branches[a].radius,branches[b].x,branches[b].y,branches[b].radius))errors.Add("node-overlap:"+branches[a].id);
            }
            foreach(var region in data.regions)
            {
                if(region==null)continue;
                foreach(var node in region.nodePositions??Array.Empty<RoadmapNodeData>())
                    if(node!=null)
                    {
                        var requirements=node.requires??Array.Empty<string>();
                        // Runtime progression is sequential within a journey. Authoring must agree with it.
                        if(node.order==1?requirements.Length!=0:requirements.Length!=1||FindOrder(levelOrders,requirements[0])!=node.order-1)errors.Add("impossible-unlock:"+node.id);
                    }
                foreach(var branch in region.branches??Array.Empty<RoadmapBranchData>())
                {
                    if(branch==null)continue;
                    if(!nodes.TryGetValue(branch.anchorNodeId??"",out var anchor)||Array.FindIndex(region.nodePositions??Array.Empty<RoadmapNodeData>(),n=>n!=null&&n.id==branch.anchorNodeId)<0)errors.Add("unreachable-branch:"+branch.id);
                    if(!string.IsNullOrEmpty(branch.targetRegionId)&&!regions.Contains(branch.targetRegionId))errors.Add("missing-region:"+branch.id);
                    int targets=(string.IsNullOrEmpty(branch.targetRegionId)?0:1)+(string.IsNullOrEmpty(branch.journeyId)?0:1)+(string.IsNullOrEmpty(branch.bonusId)?0:1);
                    if(targets!=1)errors.Add("unreachable-branch:"+branch.id);
                    foreach(var required in branch.requires??Array.Empty<string>())if(!levels.Contains(required)||anchor!=null&&FindOrder(levelOrders,required)>anchor.order)errors.Add("impossible-unlock:"+branch.id);
                }
            }
            return errors;
        }
        static int FindOrder(Dictionary<string,int> levels,string id)=>id!=null&&levels.TryGetValue(id,out var order)?order:int.MaxValue;
        static bool Overlap(float ax,float ay,float ar,float bx,float by,float br)
            =>Math.Abs(ay-by)<ar+br&&Math.Abs(ax-bx)*600<ar+br; // conservative 600-unit authoring viewport
        static bool Season(string s)=>s=="spring"||s=="summer"||s=="autumn"||s=="winter";
        static bool Transition(string a,string b,string kind)
        {
            if(!Season(a)||!Season(b))return false;if(kind=="chapter")return true;if(kind!="blend")return false;
            return a==b||a=="spring"&&b=="summer"||a=="summer"&&b=="autumn"||a=="autumn"&&b=="winter"||a=="winter"&&b=="spring";
        }
        public static List<string> ValidateReveal(RoadmapCatalog catalog,int current,int frontier,IReadOnlyList<RoadmapReveal> states)
        {
            var errors=new List<string>();
            if(states==null||states.Count!=catalog.Nodes.Length){errors.Add("invalid-reveal-state");return errors;}
            if(current<0||current>=states.Count||states[current]!=RoadmapReveal.Revealed)errors.Add("hidden-current-level");
            for(int i=0;i<states.Count;i++)if(catalog.Reveal(i,frontier)==RoadmapReveal.Hidden&&states[i]!=RoadmapReveal.Hidden||i>frontier&&states[i]==RoadmapReveal.Revealed)errors.Add("revealed-future:"+i);
            return errors;
        }
    }
}
