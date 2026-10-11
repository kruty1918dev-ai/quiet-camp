using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Infrastructure;
using QuietCamp.Domain;

static class Campaign100PackContracts
{
    public static void Run(string[] bundled)
    {
        string directory=Path.Combine(Path.GetTempPath(),"qc-campaign100-packs-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new ContentPackStore(directory,id=>bundled.Contains(id),id=>true);
            var level=CampContent.Snapshot(LevelLoader.Load(bundled[0]));level.id="pack:spring-return:1";
            foreach(var beat in level.environmentalStory.beats)beat.levelIds=new[]{level.id};
            level.contentHash=CampContent.CalculateHash(level);
            var pack=new CampContentPack{id="spring-return",levels=new[]{level},titleKey="test",descriptionKey="test"};
            string installedHash=level.contentHash;string json=JsonConvert.SerializeObject(pack);
            void Check(bool value,string message){if(!value)throw new Exception("Pack contract: "+message);}
            Check(store.Install(json).Count==0,"Valid pack installs");
            Check(store.Load(level.id)?.contentHash==level.contentHash&&store.Journeys().Length==1,"Loader and published separate journey");
            Check(store.Install(json).Contains("pack.revision"),"Duplicate installation rejected");
            Check(store.Install("invalid").Contains("pack.json"),"Bad JSON rejected");
            pack.revision=2;pack.levels[0].contentHash=new string('0',64);
            Check(store.Install(JsonConvert.SerializeObject(pack)).Any(e=>e.StartsWith("pack.hash")),"Hash corruption rejected");
            pack.levels[0].contentHash=CampContent.CalculateHash(pack.levels[0]);pack.levels[0].decorSeed++;
            pack.levels[0].contentHash=CampContent.CalculateHash(pack.levels[0]);
            Check(store.Install(JsonConvert.SerializeObject(pack)).Any(e=>e.StartsWith("pack.immutable-id")),"Existing ID cannot silently become another scene");
            var reopened=new ContentPackStore(directory,id=>bundled.Contains(id),id=>true);
            Check(reopened.Load(level.id)?.contentHash==installedHash,"Restart keeps committed version after rejected mutations");
            Check(bundled.Length==100,"Packs never inflate bundled count");
            Console.WriteLine("PASS content packs: atomic valid install, loader, restart, bad JSON/hash, duplicate revision, immutable IDs, separate 100-level base");
        }
        finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
    }
}
