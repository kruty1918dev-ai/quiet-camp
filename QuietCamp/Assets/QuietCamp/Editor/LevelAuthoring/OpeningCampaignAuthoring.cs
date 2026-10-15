using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;

namespace QuietCamp.Editor.LevelAuthoring
{
    [Serializable] public sealed class OpeningCampaignProfile
    {
        public OpeningCampaignStory.Entry story;
        public WorldStudyProfile world;
        public int reviewRevision;
        public string kind;
    }

    // Staged composition only. Publication goes through LevelAuthoringCampaign.Apply.
    public sealed class OpeningCampaignAuthoring
    {
        readonly OpeningCampaignProfile _profile;
        readonly CampArtDefinition.Instance[] _landmarks;
        readonly bool _aircraft;
        readonly bool _reuseOpening;
        public LevelAuthoringDocument Document { get; }
        public int Painted { get; private set; }
        public OpeningCampaignAuthoring(string profilePath)
        {
            _profile=JsonConvert.DeserializeObject<OpeningCampaignProfile>(File.ReadAllText(profilePath));
            if(_profile?.story==null || _profile.world==null)throw new InvalidDataException("Missing opening story/profile.");
            if(PlacementToolIntegration.Controlled==null)throw new InvalidOperationException("Prefab World Builder is required.");
            var d=LevelAuthoringCampaign.Open(_profile.story.id);
            var original=LevelAuthoringDocument.Copy(d.level);
            var primary=CampaignLandmarkCatalog.Read()[original.order-1];
            if(original.order<=10 && d.art.instances.Any(i=>i.id=="opening/"+d.sourceLevelId+"/landmark-0"))
            {
                // Keep the seven reviewed camera/forest/prop corrections in the opening.
                _reuseOpening=true;
                var id="opening/"+d.sourceLevelId+"/landmark-0";
                var pose=LevelAuthoringDocument.Copy(d.art.instances.Single(i=>i.id==id));
                var source=WorldStudyFactory.MetricModel(primary.modelId);
                var model=ModelAuthoringWorkshop.Create(source,new ModelAuthoringRecipe{operation="age",backend="dirt",amount=.38f,seed=_profile.world.seed,label=primary.label,category="Великі об’єкти"},false).model;
                pose.modelId=model.id;pose.scale=1;
                d.art.models=d.art.models.Where(m=>m.id!=model.id).Concat(new[]{model}).ToArray();
                d.art.instances=d.art.instances.Where(i=>i.id!=id&&!i.id.StartsWith("overgrowth/"+id+"/",StringComparison.Ordinal)).ToArray();
                _landmarks=new[]{pose};d.art.primaryLandmarkInstanceId=id;
            }
            else if(_profile.kind?.StartsWith("aircraft",StringComparison.Ordinal)==true)
            {
                _aircraft=true;
                var reference=LevelAuthoringDocument.Open("Assets/QuietCamp/Authoring/WorldStudies/AircraftSeasons/summer.draft.json");
                d.art=LevelAuthoringDocument.Copy(reference.art);
                // Preserve the crash's metre scale and its owned assemblies around wider puzzles.
                float clearance=Mathf.Max(0,(original.width-reference.art.width)*.5f);
                if(clearance>0)foreach(var pose in d.art.instances.Where(i=>Mathf.Abs(i.x)>reference.art.width*.5f))
                {
                    float previous=d.art.GroundHeight(pose.x,pose.z);
                    pose.x+=Mathf.Sign(pose.x)*(clearance+.3f);
                    pose.y+=d.art.GroundHeight(pose.x,pose.z)-previous;
                }
                d.art.width=original.width;d.art.height=original.height;d.art.levelId=original.id;
                string removed=_profile.kind=="aircraft-fore"?"tail":_profile.kind=="aircraft-tail"?"nose":null;
                if(removed!=null)d.art.instances=d.art.instances.Where(i=>!i.id.Contains("/airframe/"+removed)&&!i.id.Contains("/frame/"+removed)).ToArray();
                d.art.cameraTarget=_profile.kind=="aircraft-fore"?new[]{5f,.8f,0f}:_profile.kind=="aircraft-tail"?new[]{-6f,.8f,-1f}:new[]{0f,.8f,0f};
                d.art.orthographicSize=removed==null?18.7f:16;
                _landmarks=d.art.instances.Where(i=>i.id.StartsWith("crash/airframe/",StringComparison.Ordinal)).ToArray();
                d.art.instances=d.art.instances.Except(_landmarks).ToArray();d.terrain=null;
                d.art.primaryLandmarkInstanceId=_profile.kind=="aircraft-fore"?"crash/airframe/nose":_profile.kind=="aircraft-tail"?"crash/airframe/tail":"crash/wing";
            }
            else
            {
            ImportStoryModels(_profile.world.anchors);
            d.art=CampCompositionGenerator.Generate(d.level,"riparian-clearing",_profile.world.seed,out _);
            WorldStudyFactory.Compose(d,_profile.world,false);
            _landmarks=d.art.instances.Take(_profile.world.anchors.Length).ToArray();
            for(int i=0;i<_landmarks.Length;i++)_landmarks[i].id="opening/"+_profile.story.id+"/landmark-"+i;
            d.art.instances=d.art.instances.Skip(_landmarks.Length).ToArray();
            d.art.primaryLandmarkInstanceId=_landmarks[0].id;
            d.terrain=RiverTerrainAuthoringData.FromLegacy(d.art,129);
            d.terrain.bankWidth=1.8f;d.terrain.bankSoftness=.85f;
            d.terrain.stamps=new[]{new LandscapeStamp{id="opening/ridge",x=12,z=12,radius=9,height=.65f},new LandscapeStamp{id="opening/hollow",x=-16,z=9,radius=6,height=-.25f}};
            }
            d.level=original;d.art.levelId=original.id;d.title=_profile.story.title;
            d.art.primaryLandmarkId=primary.modelId;
            var beat=d.level.environmentalStory.beats.Single();
            beat.id="opening-"+d.level.order.ToString("000");
            beat.storyBeat=_profile.story.arrival;beat.intendedInference=_profile.story.finding;
            beat.worldStateAfter=_profile.story.after;
            // The frozen, editable scenery now carries these existing narrative motifs.
            // Legacy environmental-story rendering must not duplicate their geometry.
            foreach(var prop in beat.props){prop.levelDetailVisibility="hidden";prop.level=null;}
            if(!_reuseOpening)d.art.atmosphere=null;
            Document=d;
        }
        public LevelAuthoringDocument Finish(LevelAuthoringWorkspace workspace)
        {
            foreach(var pose in _landmarks.Where(p=>p.surfaceAttached!=false))
                pose.y=(workspace.River!=null?workspace.River.SampleHeight(pose.x,pose.z):Document.art.GroundHeight(pose.x,pose.z))+pose.surfaceOffset;
            Painted=Paint(workspace,_landmarks);
            var d=workspace.Capture(true);
            var forest=ForestRegrowthAuthoring.Defaults();forest.seed=_profile.world.seed;forest.extent=23;forest.targetTrees=48;forest.attempts=12500;forest.height=.42f;forest.flowerShare=.12f;forest.fernShare=.16f;
            forest.targetTrees=_profile.world.trees;
            if(!_aircraft&&!_reuseOpening)ForestRegrowthAuthoring.Apply(d,forest);
            var moss=ObjectOvergrowthSettings.Defaults();moss.material="wood";moss.seed=_profile.world.seed;moss.maxPlants=12;moss.density=.35f;moss.scale=.28f;
            if(!_aircraft)ObjectOvergrowthAuthoring.Apply(d,_landmarks.Where(p=>!p.modelId.Contains("tree")&&!p.modelId.Contains("bucket")&&!p.modelId.Contains("mug")).Select(p=>p.id),moss);
            AuthoringWeatherExposure.Bake(d,_landmarks.Select(p=>p.id).ToArray());
            var mood=SeasonAtmosphereCatalog.Copy(_reuseOpening&&d.art.atmosphere!=null?d.art.atmosphere:SeasonAtmosphereCatalog.Get(_profile.story.weather));
            if(mood.season=="winter")mood.snowCover=SeasonProfile.For(d.level).SnowCoverage;
            SeasonAtmosphereAuthoring.Apply(d,mood);
            d.art.blurPixels=3;
            CampaignLandmarkCatalog.Validate(d.art,d.level.order);
            var errors=d.Validate(true);if(errors.Count>0)throw new InvalidDataException(string.Join("\n",errors));
            return d;
        }
        static void ImportStoryModels(WorldStudyAnchor[] anchors)
        {
            foreach(var anchor in anchors)
            {
                if(LevelAuthoringPalette.Entries.Any(e=>e.id==anchor.model))continue;
                var library=RoadmapModelLibrary.Load();library.PrepareEnvironment();
                var source=library.Get(anchor.model)??throw new InvalidDataException("Missing existing story geometry: "+anchor.model);
                float metres=source.SourceHeight>1.1f?source.SourceHeight:Mathf.Max(.1f,anchor.sourceHeight);
                var model=new CampArtDefinition.Model{id=anchor.model,shading="solid",vertices=source.Positions.SelectMany(v=>new[]{v.x*metres,v.y*metres,v.z*metres}).ToArray(),normals=source.Normals.SelectMany(v=>new[]{v.x,v.y,v.z}).ToArray(),colors=Enumerable.Range(0,source.Positions.Length).Select(i=>{var c=source.Colors[i/3];return Mathf.RoundToInt(c.r*255)<<16|Mathf.RoundToInt(c.g*255)<<8|Mathf.RoundToInt(c.b*255);}).ToArray(),authoringRecipe=JsonConvert.SerializeObject(new ModelAuthoringRecipe{label=anchor.label,category="Будівлі"})};
                LevelAuthoringPalette.EnsureDocumentModels(new[]{model});
            }
        }
        static int Paint(LevelAuthoringWorkspace workspace,CampArtDefinition.Instance[] landmarks)
        {
            int count=0;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            foreach(var batch in landmarks.GroupBy(p=>p.modelId))
            {
                var poses=batch.ToArray();var painted=PlacementToolIntegration.Controlled.PlaceBatch(LevelAuthoringPalette.Get(batch.Key).prefab,workspace.Scenery,workspace.Surface,poses);
                if(painted.Length!=poses.Length)throw new InvalidOperationException("Incomplete PWB placement.");
                for(int i=0;i<painted.Length;i++)
                {
                    var marker=painted[i].GetComponent<LevelAuthoringAsset>();
                    marker.instanceId=poses[i].id;marker.sourcePose=poses[i];marker.surfaceAttached=poses[i].surfaceAttached??true;marker.surfaceOffset=poses[i].surfaceOffset;
                    marker.attachmentInitialized=true;marker.attachedY=poses[i].y;marker.capturedPosition=painted[i].transform.localPosition;marker.capturedRotation=painted[i].transform.localRotation;marker.capturedScale=painted[i].transform.localScale;
                    Undo.ClearUndo(painted[i]);count++;
                }
            }
            Undo.CollapseUndoOperations(group);
            return count;
        }
        public static object AddDetails(string sourcePath,string profilePath,string output)
        {
            var d=LevelAuthoringDocument.Open(sourcePath);
            var p=JsonConvert.DeserializeObject<OpeningCampaignProfile>(File.ReadAllText(profilePath));
            if(p.world.anchors.Length>0){LevelAuthoringPalette.EnsureDocumentModels(d.art.models);ImportStoryModels(p.world.anchors);}
            var models=d.art.models.ToDictionary(m=>m.id);
            var poses=p.world.anchors.Select((anchor,i)=>
            {
                var id=anchor.instanceId??"opening/"+p.story.id+"/detail-"+i;
                var previous=d.art.instances.SingleOrDefault(item=>item.id==id);
                if(anchor.preserveGeometry&&previous==null)throw new InvalidDataException("Cannot preserve geometry of a missing instance: "+id);
                var source=anchor.preserveGeometry?d.art.models.Single(m=>m.id==previous.modelId):WorldStudyFactory.MetricModel(anchor.model);
                var model=ModelAuthoringWorkshop.Create(source,new ModelAuthoringRecipe{operation="age",backend=anchor.age,amount=anchor.ageAmount<0?.22f:anchor.ageAmount,seed=p.world.seed+i,label=anchor.label,category="Декор",weathering=anchor.weathering},false).model;
                models[model.id]=model;
                var at=WorldStudyFactory.OutsideField(d,model,anchor.x,anchor.z,anchor.scale,anchor.yaw);
                var euler=previous?.rotationEuler?.ToArray();if(euler!=null)euler[1]=anchor.yaw;
                var scaleXYZ=previous?.scaleXYZ?.Select(value=>value*anchor.scale/previous.scale).ToArray();
                return new CampArtDefinition.Instance{id=id,modelId=model.id,x=at.x,z=at.y,y=anchor.water?anchor.y:d.art.GroundHeight(at.x,at.y)+anchor.y,yaw=anchor.yaw,scale=anchor.scale,rotationEuler=euler,scaleXYZ=scaleXYZ,surfaceAttached=!anchor.water,surfaceOffset=anchor.y,motion=anchor.motion??previous?.motion,motionPivot=previous?.motionPivot?.ToArray()};
            }).ToArray();
            int painted=0;
            if(poses.Length>0)
            {
                var staging=LevelAuthoringDocument.Copy(d);staging.art.models=models.Values.ToArray();staging.art.instances=Array.Empty<CampArtDefinition.Instance>();staging.terrain=null;
                using(var workspace=new LevelAuthoringWorkspace(staging))
                {
                    painted=Paint(workspace,poses);
                    var captured=workspace.Capture(false);
                    foreach(var model in captured.art.models)models[model.id]=model;
                    var replacement=poses.Select(i=>i.id).ToHashSet();
                    d.art.instances=d.art.instances.Where(i=>!replacement.Contains(i.id)&&!replacement.Any(id=>i.id.StartsWith("overgrowth/"+id+"/",StringComparison.Ordinal))).Concat(captured.art.instances).ToArray();
                }
            }
            d.art.models=models.Values.ToArray();
            if(p.world.refineExistingPlants)RiverTerrainAuthoringData.EnsureGroundMargins(d.art);
            int refined=p.world.refineExistingPlants?ForestRegrowthAuthoring.RefineExisting(d,p.world.seed):0;
            if(poses.Any(i=>i.id==d.art.primaryLandmarkInstanceId))
            {
                var entry=CampaignLandmarkCatalog.Read()[d.level.order-1];d.art.primaryLandmarkId=entry.modelId;
                var material=p.world.anchors.Single(a=>a.instanceId==d.art.primaryLandmarkInstanceId).weathering?.material??(entry.construction.StartsWith("transformer:",StringComparison.Ordinal)?"steel":"wood");
                ObjectOvergrowthAuthoring.Apply(d,new[]{d.art.primaryLandmarkInstanceId},new ObjectOvergrowthSettings{material=material,seed=p.world.seed,maxPlants=12,density=.35f,scale=.28f});
            }
            AuthoringWeatherExposure.Bake(d,d.art.instances.Where(i=>i.id.StartsWith("opening/",StringComparison.Ordinal)).Select(i=>i.id).ToArray());
            if(poses.Length>0)SeasonAtmosphereAuthoring.Apply(d,d.art.atmosphere);
            if(d.art.primaryLandmarkId!=null)CampaignLandmarkCatalog.Validate(d.art,d.level.order);
            var errors=d.Validate(true);if(errors.Count>0)throw new InvalidDataException(string.Join("\n",errors));d.Save(output);
            return new{status="opening-details",painted,refined,backend=painted>0?PlacementToolIntegration.Controlled.Backend:"existing-forest-regrowth",errors};
        }
    }
}
