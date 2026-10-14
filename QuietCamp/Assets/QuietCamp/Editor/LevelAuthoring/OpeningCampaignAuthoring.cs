using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using UnityEditor;

namespace QuietCamp.Editor.LevelAuthoring
{
    [Serializable] public sealed class OpeningCampaignProfile
    {
        public OpeningCampaignStory.Entry story;
        public WorldStudyProfile world;
        public int reviewRevision;
    }

    // Staged composition only. Publication goes through LevelAuthoringCampaign.Apply.
    public sealed class OpeningCampaignAuthoring
    {
        readonly OpeningCampaignProfile _profile;
        readonly CampArtDefinition.Instance[] _landmarks;
        public LevelAuthoringDocument Document { get; }
        public int Painted { get; private set; }
        public OpeningCampaignAuthoring(string profilePath)
        {
            _profile=JsonConvert.DeserializeObject<OpeningCampaignProfile>(File.ReadAllText(profilePath));
            if(_profile?.story==null || _profile.world==null)throw new InvalidDataException("Missing opening story/profile.");
            if(PlacementToolIntegration.Controlled==null)throw new InvalidOperationException("Prefab World Builder is required.");
            var d=LevelAuthoringCampaign.Open(_profile.story.id);
            var original=LevelAuthoringDocument.Copy(d.level);
            d.art=CampCompositionGenerator.Generate(d.level,"riparian-clearing",_profile.world.seed,out _);
            WorldStudyFactory.Compose(d,_profile.world,false);
            d.level=original;d.art.levelId=original.id;d.title=_profile.story.title;
            var beat=d.level.environmentalStory.beats.Single();
            beat.id="opening-"+d.level.order.ToString("000");
            beat.storyBeat=_profile.story.arrival;beat.intendedInference=_profile.story.finding;
            beat.worldStateAfter=_profile.story.after;
            // The frozen, editable scenery now carries these existing narrative motifs.
            // Legacy environmental-story rendering must not duplicate their geometry.
            foreach(var prop in beat.props)prop.levelDetailVisibility="hidden";
            _landmarks=d.art.instances.Take(_profile.world.anchors.Length).ToArray();
            for(int i=0;i<_landmarks.Length;i++)_landmarks[i].id="opening/"+_profile.story.id+"/landmark-"+i;
            d.art.instances=d.art.instances.Skip(_landmarks.Length).ToArray();
            d.art.atmosphere=null;
            d.terrain=RiverTerrainAuthoringData.FromLegacy(d.art,129);
            d.terrain.bankWidth=1.8f;d.terrain.bankSoftness=.85f;
            d.terrain.stamps=new[]{new LandscapeStamp{id="opening/ridge",x=12,z=12,radius=9,height=.65f},new LandscapeStamp{id="opening/hollow",x=-16,z=9,radius=6,height=-.25f}};
            Document=d;
        }
        public LevelAuthoringDocument Finish(LevelAuthoringWorkspace workspace)
        {
            foreach(var pose in _landmarks)pose.y=workspace.River.SampleHeight(pose.x,pose.z)+pose.surfaceOffset;
            Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();
            foreach(var batch in _landmarks.GroupBy(p=>p.modelId))
            {
                var poses=batch.ToArray();var painted=PlacementToolIntegration.Controlled.PlaceBatch(LevelAuthoringPalette.Get(batch.Key).prefab,workspace.Scenery,workspace.Surface,poses);
                if(painted.Length!=poses.Length)throw new InvalidOperationException("Incomplete PWB placement.");
                for(int i=0;i<painted.Length;i++)
                {
                    var marker=painted[i].GetComponent<LevelAuthoringAsset>();
                    marker.instanceId=poses[i].id;marker.sourcePose=poses[i];marker.surfaceAttached=poses[i].surfaceAttached??true;marker.surfaceOffset=poses[i].surfaceOffset;
                    marker.attachmentInitialized=true;marker.attachedY=poses[i].y;marker.capturedPosition=painted[i].transform.localPosition;marker.capturedRotation=painted[i].transform.localRotation;marker.capturedScale=painted[i].transform.localScale;
                    Undo.ClearUndo(painted[i]);Painted++;
                }
            }
            Undo.CollapseUndoOperations(group);
            var d=workspace.Capture(true);
            var forest=ForestRegrowthAuthoring.Defaults();forest.seed=_profile.world.seed;forest.extent=23;forest.targetTrees=48;forest.attempts=12500;forest.height=.42f;forest.flowerShare=.12f;forest.fernShare=.16f;
            ForestRegrowthAuthoring.Apply(d,forest);
            var moss=ObjectOvergrowthSettings.Defaults();moss.material="wood";moss.seed=_profile.world.seed;moss.maxPlants=12;moss.density=.35f;moss.scale=.28f;
            ObjectOvergrowthAuthoring.Apply(d,_landmarks.Where(p=>!p.modelId.Contains("tree")&&!p.modelId.Contains("bucket")&&!p.modelId.Contains("mug")).Select(p=>p.id),moss);
            SeasonAtmosphereAuthoring.Apply(d,SeasonAtmosphereCatalog.Get(_profile.story.weather));
            d.art.blurPixels=3;
            var errors=d.Validate(true);if(errors.Count>0)throw new InvalidDataException(string.Join("\n",errors));
            return d;
        }
    }
}
