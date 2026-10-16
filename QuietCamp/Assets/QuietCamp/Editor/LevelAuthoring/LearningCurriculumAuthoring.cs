using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Editor.LevelAuthoring.WorldOps;
using UnityEditor;

namespace QuietCamp.Editor.LevelAuthoring
{
    /// <summary>Logical curriculum publication only. Scenery remains byte-identical and is edited through WorldOps.</summary>
    public static class LearningCurriculumAuthoring
    {
        static string Puzzle(LevelData l)=>JsonConvert.SerializeObject(new{l.ruleVersion,l.width,l.height,l.entry,l.blocked,l.shade,l.noise,
            l.exteriorWalkable,l.accessPoints,l.guests,l.friends,l.witness,l.objects,l.canopies});
        static string Metadata(string relative)=>"fileFormatVersion: 2\nguid: "+WorldOpsStorage.Hash("QuietCamp.Curriculum/"+relative.Replace('\\','/')).Substring(0,32)+"\nTextScriptImporter:\n  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n";
        static string CompactJson(string text)
        {
            // Keep numeric literals and strings verbatim; only remove JSON whitespace outside strings.
            var result=new StringBuilder(text.Length);bool quoted=false,escaped=false;
            foreach(char c in text)
            {
                if(quoted)
                {
                    result.Append(c);
                    if(escaped)escaped=false;else if(c=='\\')escaped=true;else if(c=='"')quoted=false;
                }
                else
                {
                    if(c=='"')quoted=true;
                    if(c!=' '&&c!='\t'&&c!='\r'&&c!='\n')result.Append(c);
                }
            }
            return result.ToString();
        }
        static void CompactOwnedOutputs(string editor,string[] roots,Dictionary<string,string> writes,Dictionary<string,string> sources)
        {
            string directory=Path.Combine(editor,"UserSettings/LearningCurriculum");if(!Directory.Exists(directory))return;
            foreach(var journalPath in Directory.GetFiles(directory,"journal.json",SearchOption.AllDirectories))
            {
                var journal=WorldOpsStorage.Read<WorldJournal>(journalPath);
                if(journal.integrity!=WorldOpsStorage.Digest(new{journal.version,journal.id,journal.writes,journal.preconditions}))throw new InvalidDataException("Damaged curriculum journal.");
                if(journal.state!="committed")continue;
                foreach(var entry in journal.writes)
                {
                    string path=Path.GetFullPath(entry.path);
                    if(writes.ContainsKey(path)||!roots.Any(r=>path.StartsWith(r+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                        ||WorldOpsStorage.FileHash(path)!=entry.afterHash)continue;
                    string text=File.ReadAllText(path),compact;
                    if(path.EndsWith(".draft.json",StringComparison.Ordinal))
                    {
                        var json=JToken.Parse(text);compact=CompactJson(text);
                        if(!JToken.DeepEquals(json,JToken.Parse(compact)))throw new InvalidDataException("Formatting changed draft values.");
                    }
                    else if(path.EndsWith(".meta",StringComparison.Ordinal))compact=string.Join("\n",text.Replace("\r\n","\n").Split('\n').Select(l=>l.TrimEnd()));
                    else continue;
                    if(compact==text)continue;
                    sources[path]=entry.afterHash;writes[path]=compact;
                }
            }
        }
        public static object Apply(string repositoryProject,bool compactOwnedOutputs=false)
        {
            const string content="Assets/QuietCamp/Resources/QuietCamp";
            const string drafts="Assets/QuietCamp/Authoring/CampaignLevels";
            string editor=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,".."));
            var roots=new[]{Path.GetFullPath(repositoryProject),editor}.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var curriculum=LearningCurriculum.Read(File.ReadAllText(Path.Combine(roots[0],content,"learning_curriculum.json")));
            var writes=new Dictionary<string,string>();var sources=new Dictionary<string,string>();var changed=new List<string>();
            // Validate all current witnesses once, before preparing any writes.
            foreach(var root in roots)
            {
                sources[Path.Combine(root,content,"learning_curriculum.json")]=WorldOpsStorage.FileHash(Path.Combine(root,content,"learning_curriculum.json"));
                foreach(var card in LevelAuthoringCampaign.ReadCards(Path.Combine(root,content)))
                {
                    string path=Path.Combine(root,content,card.id.StartsWith("gen:")?"GeneratedLevels":"Levels",card.id.Replace(':','_')+".json");
                    sources[path]=WorldOpsStorage.FileHash(path);
                    var level=JsonConvert.DeserializeObject<LevelData>(File.ReadAllText(path));
                    if(LevelContentValidator.Validate(level).Count>0||!RuleEvaluator.Evaluate(level,level.witness).IsSolved)
                        throw new InvalidDataException("Invalid current campaign witness: "+card.id);
                }
            }
            foreach(var lesson in curriculum.lessons)
            {
                if(LevelAuthoringWindow.WorldOpsConflict(lesson.levelId))throw new IOException("Unsaved open draft: "+lesson.levelId);
                foreach(var root in roots)
                {
                    string published=Path.Combine(root,content);
                    // Read published content, never silently publish an unrelated manual draft.
                    var doc=LevelAuthoringCampaign.Open(lesson.levelId,published,Path.Combine(editor,"Temp/ai/no-curriculum-drafts"),false);
                    string before=JsonConvert.SerializeObject(doc.level);var original=LevelAuthoringDocument.Copy(doc.level);
                    curriculum.Pace(doc.level);
                    if(before==JsonConvert.SerializeObject(doc.level))continue;
                    if(!RuleEvaluator.Evaluate(doc.level,doc.level.witness).IsSolved)throw new InvalidDataException("Paced witness failed: "+lesson.levelId);
                    var prepared=LevelAuthoringCampaign.PrepareApply(doc,published,false,false);
                    foreach(var pair in prepared)writes.Add(pair.Key,pair.Value);
                    string levelPath=Path.Combine(published,lesson.levelId.StartsWith("gen:")?"GeneratedLevels":"Levels",lesson.levelId.Replace(':','_')+".json");
                    string artPath=Path.Combine(published,"ArtCompositions",lesson.levelId.Replace(':','_')+".json");
                    sources[artPath]=WorldOpsStorage.FileHash(artPath);
                    string draftPath=Path.Combine(root,drafts,lesson.levelId.Replace(':','_')+".draft.json");
                    sources[draftPath]=WorldOpsStorage.FileHash(draftPath);
                    if(File.Exists(draftPath))
                    {
                        var draft=LevelAuthoringDocument.Open(draftPath);
                        if(draft.sourceContentHash!=sources[levelPath]||Puzzle(draft.level)!=Puzzle(original))throw new IOException("Manual puzzle edits in draft: "+lesson.levelId);
                        curriculum.Pace(draft.level,lesson.levelId);
                        draft.level.contentHash=CampContent.CalculateHash(draft.level);
                        draft.sourceContentHash=WorldOpsStorage.Hash(prepared[levelPath]);
                        writes[draftPath]=JsonConvert.SerializeObject(draft);
                    }
                    if(!changed.Contains(lesson.levelId))changed.Add(lesson.levelId);
                }
            }
            foreach(var root in roots)
            {
                string path=Path.Combine(root,content,"level_summaries.json");
                sources[path]=WorldOpsStorage.FileHash(path);
                var summaries=JsonConvert.DeserializeObject<LevelSummary[]>(File.ReadAllText(path));
                foreach(var summary in summaries)
                {
                    string levelPath=Path.Combine(root,content,summary.id.StartsWith("gen:")?"GeneratedLevels":"Levels",summary.id.Replace(':','_')+".json");
                    var level=JsonConvert.DeserializeObject<LevelData>(writes.TryGetValue(levelPath,out var json)?json:File.ReadAllText(levelPath));
                    summary.contentHash=level.contentHash;summary.shade=level.guests.Any(g=>g.shade);summary.quiet=level.guests.Any(g=>g.quiet);
                    summary.friends=level.friends.Length>0;summary.accessPoints=level.accessPoints;
                    summary.ruleVersion=level.ruleVersion;summary.number=level.order;summary.width=level.width;summary.height=level.height;
                    summary.environmentPreset=level.environmentPreset;summary.lighting=level.lighting;summary.decorSeed=level.decorSeed;
                    summary.entry=level.entry;summary.mapObjects=level.objects;summary.canopies=level.canopies;summary.exteriorWalkable=level.exteriorWalkable;
                    summary.noise=level.noise;summary.fire=level.noise.Length>0;summary.environment=level.environment;
                }
                string summaryJson=JsonConvert.SerializeObject(summaries,Formatting.Indented);
                if(!JToken.DeepEquals(JToken.Parse(File.ReadAllText(path)),JToken.Parse(summaryJson)))writes[path]=summaryJson;
                // Narrative events stay intact; objective text must describe the newly paced rules.
                path=Path.Combine(root,content,"opening_campaign_story.json");sources[path]=WorldOpsStorage.FileHash(path);
                var story=JObject.Parse(File.ReadAllText(path));
                foreach(var entry in story["entries"].OfType<JObject>().Where(e=>changed.Contains((string)e["id"])))
                {
                    string id=(string)entry["id"];
                    string levelPath=Path.Combine(root,content,id.StartsWith("gen:")?"GeneratedLevels":"Levels",id.Replace(':','_')+".json");
                    var level=JsonConvert.DeserializeObject<LevelData>(writes.TryGetValue(levelPath,out var json)?json:File.ReadAllText(levelPath));
                    var themes=new List<string>{"доступ до кожних дверей"};
                    if(level.guests.Any(g=>g.shade))themes.Add("тінь");if(level.guests.Any(g=>g.quiet))themes.Add("тиша");
                    if(level.friends.Length>0)themes.Add("друзі поруч");if(level.accessPoints.Length>0)themes.Add("вільні виходи");
                    entry["mechanic"]=string.Join(", ",themes);
                    entry["objective"]="Прийміть гостей: "+string.Join(", ",themes)+".";
                }
                if(changed.Count>0)writes[path]=story.ToString(Formatting.Indented);
            }
            foreach(var path in writes.Keys.ToArray())
                if(path.EndsWith(".json",StringComparison.Ordinal)&&!File.Exists(path+".meta"))
                {
                    string root=roots.First(r=>Path.GetFullPath(path).StartsWith(r+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase));
                    writes[path+".meta"]=Metadata(Path.GetRelativePath(root,path));
                }
            // Repair only metadata made by this publisher's early filename-only allocator.
            // Content and art archives deliberately have the same basename, so GUIDs need the folder too.
            foreach(var folder in new[]{"ContentRevisions","ArtRevisions"})
            foreach(var canonicalMeta in Directory.GetFiles(Path.Combine(roots[0],content,folder),"*.json.meta"))
            {
                string file=canonicalMeta.Substring(0,canonicalMeta.Length-5);
                string obsolete=WorldOpsStorage.Hash("QuietCamp.Curriculum/"+Path.GetFileName(file)).Substring(0,32);
                if(!File.ReadAllText(canonicalMeta).Contains("guid: "+obsolete))continue;
                string relative=Path.GetRelativePath(roots[0],file);
                foreach(var root in roots)
                {
                    string peer=Path.Combine(root,relative);
                    if(WorldOpsStorage.FileHash(peer)!=WorldOpsStorage.FileHash(file))throw new IOException("Archive differs between projects: "+relative);
                    sources[peer]=WorldOpsStorage.FileHash(peer);sources[peer+".meta"]=WorldOpsStorage.FileHash(peer+".meta");
                    writes[peer+".meta"]=Metadata(relative);
                }
            }
            if(compactOwnedOutputs)CompactOwnedOutputs(editor,roots,writes,sources);
            string journal=Path.Combine(editor,"UserSettings/LearningCurriculum",DateTime.UtcNow.ToString("yyyyMMddTHHmmssfff"));
            WorldOpsTransaction.Prepare(journal,writes,sources,roots);WorldOpsTransaction.Commit(journal);
            LevelAuthoringCampaign.InvalidateCards();AssetDatabase.Refresh();
            foreach(var id in changed)
            {
                string path=Path.Combine(editor,drafts,id.Replace(':','_')+".draft.json");
                if(File.Exists(path))LevelAuthoringWindow.WorldOpsApplied(id,path);
            }
            return new{status="curriculum-applied",levels=changed,files=writes.Count,journal};
        }
    }
}
