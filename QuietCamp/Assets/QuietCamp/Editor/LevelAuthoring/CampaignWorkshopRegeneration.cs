using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using UnityEditor;

namespace QuietCamp.Editor.LevelAuthoring
{
    /// <summary>Generate and validate all drafts before publishing the staged set.</summary>
    public sealed class CampaignWorkshopRegeneration
    {
        const string Content="Assets/QuietCamp/Resources/QuietCamp";
        const string Drafts="Assets/QuietCamp/Authoring/CampaignLevels";
        readonly string _stage;
        readonly AuthoringCampaignCard[] _cards;
        readonly List<object> _proofs=new List<object>();
        readonly Dictionary<string,string> _sourceHashes=new Dictionary<string,string>();
        public int Completed {get;private set;}
        public int Total=>_cards.Length;
        public string Output=>_stage;
        public CampaignWorkshopRegeneration(string output)
        {
            _stage=Path.GetFullPath(output);Directory.CreateDirectory(_stage);_cards=LevelAuthoringCampaign.Cards;
            if(_cards.Length!=100)throw new InvalidOperationException("Expected exactly 100 campaign levels.");
            foreach(var folder in new[]{"Levels","GeneratedLevels","ArtCompositions","ContentRevisions","ArtRevisions"})
                if(Directory.Exists(Path.Combine(Content,folder)))
                    foreach(var file in Directory.GetFiles(Path.Combine(Content,folder),"*.json"))
                    {var path=Path.Combine(_stage,"Content",folder,Path.GetFileName(file));Directory.CreateDirectory(Path.GetDirectoryName(path));File.Copy(file,path);}
            File.Copy(Path.Combine(Content,"campaign.json"),Path.Combine(_stage,"Content/campaign.json"));
            foreach(var folder in new[]{"Levels","GeneratedLevels","ArtCompositions"})
                if(Directory.Exists(Path.Combine(Content,folder)))foreach(var file in Directory.GetFiles(Path.Combine(Content,folder),"*.json"))_sourceHashes[file]=Hash(file);
        }
        static string Hash(string path){using var sha=SHA256.Create();return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        public void Step()
        {
            if(Completed>=Total)return;
            var card=_cards[Completed];var original=LevelAuthoringDocument.Copy(LevelLoader.Load(card.id));
            string name=card.id.Replace(':','_')+".json";
            string source=Path.Combine(Content,card.id.StartsWith("gen:")?"GeneratedLevels":"Levels",name);
            string artSource=Path.Combine(Content,"ArtCompositions",name);
            // This operation replaces the surroundings. Avoid rendering and capturing the
            // discarded old scenery before generating the new editable composition.
            var d=new LevelAuthoringDocument{level=LevelAuthoringDocument.Copy(original),sourceLevelId=card.id,sourceContentHash=Hash(source),sourceArtHash=File.Exists(artSource)?Hash(artSource):null};
            var profile=LevelAuthoringDocument.Copy(WorldStudyFactory.Profiles[Completed%5]);
            profile.seed=191800+card.number*7919;profile.plants=650;profile.cameraSize=Math.Max(profile.cameraSize,Math.Max(card.width,card.height)*1.15f);
            d.art=CampCompositionGenerator.Generate(d.level,"riparian-clearing",profile.seed,out _);d.terrain=null;
            WorldStudyFactory.Compose(d,profile,false);d.level=original;d.art.levelId=original.id;d.title="Рівень "+card.number+" — "+profile.title;
            AuthoringVegetationSnapshot.ExpandCover(d);
            var preset=VegetationAuthoring.Presets[Completed%5];VegetationAuthoring.Apply(d,preset,profile.seed);
            string season=new[]{"spring","summer","autumn","winter"}[Completed/25];var moods=SeasonAtmosphereCatalog.All.Where(p=>p.season==season).ToArray();
            SeasonAtmosphereAuthoring.Apply(d,moods[Completed%5]);
            var errors=d.Validate(true);if(errors.Count>0)throw new InvalidDataException(card.id+": "+string.Join("; ",errors));
            // Publish only presentation changes; all rule fields remain byte-identical.
            var before=JsonConvert.SerializeObject(original);var comparison=LevelAuthoringDocument.Copy(d.level);if(before!=JsonConvert.SerializeObject(comparison))throw new InvalidOperationException("Puzzle changed: "+card.id);
            LevelAuthoringCampaign.Apply(d,Path.Combine(_stage,"Content"));
            var published=JsonConvert.DeserializeObject<QuietCamp.Domain.LevelData>(File.ReadAllText(Path.Combine(_stage,"Content",card.id.StartsWith("gen:")?"GeneratedLevels":"Levels",card.id.Replace(':','_')+".json")));
            d.level=published;d.art.levelId=published.id;d.sourceContentHash=Hash(Path.Combine(_stage,"Content",card.id.StartsWith("gen:")?"GeneratedLevels":"Levels",card.id.Replace(':','_')+".json"));
            d.sourceArtHash=Hash(Path.Combine(_stage,"Content/ArtCompositions",card.id.Replace(':','_')+".json"));
            string draft=Path.Combine(_stage,"Drafts",card.id.Replace(':','_')+".draft.json");Directory.CreateDirectory(Path.GetDirectoryName(draft));
            File.WriteAllText(draft,JsonConvert.SerializeObject(d));
            _proofs.Add(new{id=card.id,number=card.number,objects=d.art.instances.Length,preset=d.art.atmosphere.id,vegetation=d.vegetationPreset,seed=profile.seed,puzzleUnchanged=true});Completed++;
        }
        public void Publish()
        {
            if(Completed!=100)throw new InvalidOperationException("Incomplete regeneration.");
            foreach(var p in _sourceHashes)if(Hash(p.Key)!=p.Value)throw new InvalidOperationException("Source changed while generating: "+p.Key);
            var writes=new Dictionary<string,byte[]>();
            foreach(var file in Directory.GetFiles(Path.Combine(_stage,"Content"),"*.json",SearchOption.AllDirectories))
            {
                var destination=Path.Combine(Content,Path.GetRelativePath(Path.Combine(_stage,"Content"),file));var bytes=File.ReadAllBytes(file);
                if(!File.Exists(destination)||!bytes.SequenceEqual(File.ReadAllBytes(destination)))writes[destination]=bytes;
            }
            foreach(var file in Directory.GetFiles(Path.Combine(_stage,"Drafts"),"*.json"))writes[Path.Combine(Drafts,Path.GetFileName(file))]=File.ReadAllBytes(file);
            var originals=writes.Keys.ToDictionary(p=>p,p=>File.Exists(p)?File.ReadAllBytes(p):null);
            AssetDatabase.DisallowAutoRefresh();AssetDatabase.StartAssetEditing();
            try{foreach(var pair in writes){Directory.CreateDirectory(Path.GetDirectoryName(pair.Key));File.WriteAllBytes(pair.Key+".pending",pair.Value);}foreach(var path in writes.Keys){AuthoringFileTransaction.CommitPrepared(path);}}
            catch{foreach(var pair in originals){if(pair.Value==null){if(File.Exists(pair.Key))File.Delete(pair.Key);}else File.WriteAllBytes(pair.Key,pair.Value);}throw;}
            finally{foreach(var path in writes.Keys)if(File.Exists(path+".pending"))File.Delete(path+".pending");AssetDatabase.StopAssetEditing();AssetDatabase.AllowAutoRefresh();}
            File.WriteAllText(Path.Combine(_stage,"published.json"),JsonConvert.SerializeObject(new{status="campaign-regenerated",total=100,puzzleRulesPreserved=true,files=writes.Keys,levels=_proofs},Formatting.Indented));
            AssetDatabase.Refresh();
        }
    }
}
