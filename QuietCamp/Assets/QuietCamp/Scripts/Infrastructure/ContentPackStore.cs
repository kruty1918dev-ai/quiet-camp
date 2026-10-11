using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Application;

namespace QuietCamp.Infrastructure
{
    [Serializable] public sealed class CampContentPack
    {
        public int schemaVersion=1,revision=1;
        public string id,titleKey,descriptionKey;
        public LevelData[] levels=Array.Empty<LevelData>();
    }
    /// <summary>Versioned data-only packs. Transport/authentication belong to the caller.
    /// Validated installation is atomic; no network endpoint or player build is implied.</summary>
    public sealed class ContentPackStore
    {
        const int MaxCharacters=4*1024*1024;
        readonly string _path;
        readonly Func<string,bool> _bundled,_assetExists;
        CampContentPack[] _packs=Array.Empty<CampContentPack>();
        public IReadOnlyList<CampContentPack> Packs=>JsonConvert.DeserializeObject<CampContentPack[]>(JsonConvert.SerializeObject(_packs));
        public ContentPackStore(string directory,Func<string,bool> bundled,Func<string,bool> assetExists)
        {
            _path=Path.Combine(directory,"installed-packs.json");_bundled=bundled;_assetExists=assetExists;
            if(!File.Exists(_path))return;
            try
            {
                var json=File.ReadAllText(_path);if(json.Length>MaxCharacters*4)throw new InvalidDataException("Pack store too large");
                var packs=JsonConvert.DeserializeObject<CampContentPack[]>(json);
                var accepted=new List<CampContentPack>();
                foreach(var pack in packs??Array.Empty<CampContentPack>())
                    if(Validate(pack).Count==0&&!accepted.Any(p=>p.id==pack.id))accepted.Add(pack);
                _packs=accepted.ToArray();
            }
            catch { _packs=Array.Empty<CampContentPack>(); } // fail closed without deleting recoverable data
        }
        public IReadOnlyList<string> Validate(CampContentPack pack)
        {
            var errors=new List<string>();
            if(pack==null||pack.schemaVersion!=1||pack.revision<1||!Token(pack.id)||pack.levels==null||pack.levels.Length<1||pack.levels.Length>100)
            {errors.Add("pack.schema");return errors;}
            var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var level in pack.levels)
            {
                if(level==null||string.IsNullOrEmpty(level.id)||!level.id.StartsWith("pack:"+pack.id+":",StringComparison.Ordinal)||!ids.Add(level.id)||_bundled(level.id))
                {errors.Add("pack.level-id");continue;}
                var issues=LevelContentValidator.Validate(level);errors.AddRange(issues);if(issues.Count>0)continue;
                if(level.contentHash!=CampContent.CalculateHash(level))errors.Add("pack.hash:"+level.id);
                if(!RuleEvaluator.Evaluate(level,level.witness??Array.Empty<Placement>()).IsSolved)errors.Add("pack.witness:"+level.id);
                foreach(var guest in level.guests??Array.Empty<GuestData>())if(!_assetExists(guest.assetId))errors.Add("pack.asset:"+guest.assetId);
                foreach(var obj in level.objects??Array.Empty<EnvironmentObjectData>())if(!_assetExists(obj.assetId))errors.Add("pack.asset:"+obj.assetId);
                foreach(var beat in level.environmentalStory?.beats??Array.Empty<EnvironmentalStoryBeat>())
                foreach(var prop in beat.props??Array.Empty<EnvironmentalStoryProp>())
                    if(prop.level!=null&&!_assetExists(prop.level.assetId))errors.Add("pack.asset:"+prop.level.assetId);
            }
            return errors;
        }
        public IReadOnlyList<string> Install(string manifestJson)
        {
            var errors=new List<string>();
            if(manifestJson==null||manifestJson.Length>MaxCharacters)return new[]{"pack.size"};
            CampContentPack pack;
            try{pack=JsonConvert.DeserializeObject<CampContentPack>(manifestJson);}catch{return new[]{"pack.json"};}
            errors.AddRange(Validate(pack));if(errors.Count>0)return errors;
            var previous=_packs.FirstOrDefault(p=>p.id==pack.id);
            if(previous!=null&&pack.revision<=previous.revision)return new[]{"pack.revision"};
            foreach(var level in pack.levels)
            {
                var old=_packs.SelectMany(p=>p.levels).FirstOrDefault(l=>l.id==level.id);
                if(old!=null&&(previous==null||old.contentHash!=level.contentHash))errors.Add("pack.immutable-id:"+level.id);
                var solver=new CampSolver(level,Array.Empty<Placement>(),2);
                var status=solver.Step();while(status==CampSolver.Status.Searching)status=solver.Step(.05);
                if(status!=CampSolver.Status.Solved||!RuleEvaluator.Evaluate(level,solver.Solution).IsSolved)errors.Add("pack.solver:"+level.id);
            }
            // Pack revisions may append content, never erase already installed IDs.
            if(previous!=null&&previous.levels.Any(l=>!pack.levels.Any(n=>n.id==l.id)))errors.Add("pack.removed-id");
            if(errors.Count>0)return errors;
            var next=_packs.Where(p=>p.id!=pack.id).Concat(new[]{pack}).ToArray();
            string temporary=_path+".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                string serialized=JsonConvert.SerializeObject(next);
                if(serialized.Length>MaxCharacters*4)return new[]{"pack.store-size"};
                File.WriteAllText(temporary,serialized);
                if(File.Exists(_path))File.Replace(temporary,_path,null);else File.Move(temporary,_path);
                _packs=next;
            }
            catch {if(File.Exists(temporary))File.Delete(temporary);errors.Add("pack.save");}
            return errors;
        }
        public LevelData Load(string id)
        {
            var found=_packs.SelectMany(p=>p.levels).FirstOrDefault(l=>l.id==id);
            return found==null?null:CampContent.Snapshot(found);
        }
        public JourneyDefinition[] Journeys()=>_packs.Select(p=>new JourneyDefinition{
            id="pack."+p.id,revision=p.revision,titleKey=p.titleKey,descriptionKey=p.descriptionKey,
            published=true,levelIds=p.levels.Select(l=>l.id).ToArray(),storyKeys=Array.Empty<string>()}).ToArray();
        static bool Token(string value)=>!string.IsNullOrEmpty(value)&&value.Length<=48&&value.All(c=>c>='a'&&c<='z'||c>='0'&&c<='9'||c=='-');
    }
    public static class ContentPackRuntime
    {
        static ContentPackStore _shared;
        public static ContentPackStore Store=>_shared??(_shared=new ContentPackStore(
            Path.Combine(UnityEngine.Application.persistentDataPath,"content-packs"),
            id=>LevelLoader.MvpLevelIds().Contains(id),KnownAsset));
        static HashSet<string> _assets;
        static bool KnownAsset(string id)
        {
            if(_assets==null)
            {
                _assets=new HashSet<string>(StringComparer.Ordinal);
                foreach(var name in new[]{"roadmap_models","roadmap_story_models","campaign100_models"})
                {
                    var text=UnityEngine.Resources.Load<UnityEngine.TextAsset>("QuietCamp/"+name);if(text==null)continue;
                    foreach(var model in Newtonsoft.Json.Linq.JArray.Parse(text.text))_assets.Add((string)model["id"]);
                }
            }
            return id!=null&&_assets.Contains(id);
        }
        #if !QC_MONETIZATION_PROBE
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        #endif
        public static void Reset(){_shared=null;_assets=null;}
    }
}
