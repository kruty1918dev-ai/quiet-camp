using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Composition;

static class CompositionCommands
{
    public static readonly string Root=Environment.GetEnvironmentVariable("QC_COMPOSITION_SOURCE")??"QuietCamp/Assets/QuietCamp/Authoring/Roadmap/Composition";
    static readonly JsonSerializer Serializer=JsonSerializer.Create(new JsonSerializerSettings{MissingMemberHandling=MissingMemberHandling.Error});
    public static string HashFile(string file)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
    public static string Hash(string text)=>Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    public static T Read<T>(string file)=>JsonConvert.DeserializeObject<T>(File.ReadAllText(file),new JsonSerializerSettings{MissingMemberHandling=MissingMemberHandling.Error});
    public static string Reference(string key)
    {string name=(string)Read<JObject>(Root+"/manifest.json")[key];if(name==null||Path.GetFileName(name)!=name)throw new Exception("Invalid manifest reference: "+key);return Root+"/"+name;}
    public static Dictionary<string,SceneCompositionDocument> Documents()
    {
        var m=Read<JObject>(Root+"/manifest.json");if((string)m["compilerRevision"]!=SceneComposer.Revision)throw new Exception("Composition compiler revision mismatch");var result=new Dictionary<string,SceneCompositionDocument>();
        foreach(string name in m["regions"].Values<string>())
        {if(Path.GetFileName(name)!=name)throw new Exception("Region path must be a simple filename");var doc=Read<SceneCompositionDocument>(Root+"/"+name);result.Add(doc.id,doc);}return result;
    }
    static Dictionary<string,CompositionResult> Compose(Dictionary<string,SceneCompositionDocument> docs,Dictionary<string,VisualAssetDefinition> assets,Dictionary<string,EnsembleTemplate> templates)
    {
        // Style studies occupy independent miniature scenes, not adjacent world regions.
        if((string)Read<JObject>(Root+"/manifest.json")["id"]=="quiet-camp-diorama-studies")
            return docs.ToDictionary(p=>p.Key,p=>SceneComposer.Compose(p.Value,assets,templates));
        ITerrainSample terrain=null;var world=docs.Values.Select(d=>JsonConvert.DeserializeObject<SceneCompositionDocument>(JsonConvert.SerializeObject(d))).ToArray();
        if((string)Read<JObject>(Root+"/manifest.json")["id"]=="quiet-camp-main")
        {
            var authored=Read<QuietCamp.Domain.RoadmapDefinition>("QuietCamp/Assets/QuietCamp/Authoring/Roadmap/main.json");
            var available=Read<QuietCamp.Domain.LevelSummary[]>("QuietCamp/Assets/QuietCamp/Resources/QuietCamp/level_summaries.json");
            var definition=QuietCamp.Application.RoadmapCompiler.BakeAuthored(authored,
                QuietCamp.Application.RoadmapCompositionAdapter.AuthoredSummaries(authored,available));
            string compatibility=GameInvariant(definition);
            QuietCamp.Application.RoadmapCompositionAdapter.Bind(definition,docs);
            if(GameInvariant(definition)!=compatibility)throw new Exception("Composition adapter changed campaign identity, progression or content; only node/branch positions may change");
            var catalog=new QuietCamp.Application.RoadmapCatalog(definition);terrain=new QuietCamp.Application.RoadmapCompositionAdapter.Terrain(catalog,world);
            foreach(var document in world){int region=Array.FindIndex(definition.regions,r=>r.id==document.id);if(region<0)throw new Exception("Unbound composition region: "+document.id);QuietCamp.Application.RoadmapCompositionAdapter.WorldDocument(document,catalog.RegionStarts[region]/QuietCamp.Application.RoadmapCompositionAdapter.Units);}
        }
        var result=SceneComposer.ComposeWorld(world,assets,templates,terrain);return world.Select((d,i)=>new{d.id,result=result[i]}).ToDictionary(p=>p.id,p=>p.result);
    }
    static string GameInvariant(QuietCamp.Domain.RoadmapDefinition definition)
    {
        var token=JObject.FromObject(definition);
        foreach(var region in token["regions"])
        {
            foreach(var node in region["nodePositions"].Children<JObject>()){node.Remove("x");node.Remove("y");}
            foreach(var branch in region["branches"].Children<JObject>()){branch.Remove("x");branch.Remove("y");}
        }
        return token.ToString(Formatting.None);
    }
    static void Process(string script,IEnumerable<string> arguments,string stdin=null)
    {
        var info=new ProcessStartInfo("python3"){UseShellExecute=false,RedirectStandardInput=stdin!=null};info.ArgumentList.Add(script);foreach(string argument in arguments)info.ArgumentList.Add(argument);
        using(var child=System.Diagnostics.Process.Start(info)){if(stdin!=null){child.StandardInput.Write(stdin);child.StandardInput.Close();}child.WaitForExit();if(child.ExitCode!=0)throw new Exception("Validation/comparison rejected; files unchanged");}
    }
    public static void Run(string[] args)
    {
        if(args.Length==0)throw new ArgumentException("composition inspect|validate|compose|patch|bake|preview|compare");
        string cmd=args[0],id=args.Length>1?args[1]:null;
        if(cmd=="staging-validate")
        {
            var stagingAssets=Read<VisualAssetDefinition[]>(Reference("assets")).Concat(Read<VisualAssetDefinition[]>(Root+"/Staging/assets.json")).ToDictionary(a=>a.id);
            var stagingTemplates=Read<EnsembleTemplate[]>(Reference("templates")).ToDictionary(t=>t.id);
            foreach(string name in new[]{"aircraft","ship","dam"})
            {
                string path=Root+"/Staging/"+name+".json";Process("tools/scene-composition/validate_schema.py",new[]{"--stdin"},File.ReadAllText(path));
                var source=Read<SceneCompositionDocument>(path);var composed=SceneComposer.Compose(source,stagingAssets,stagingTemplates);
                if(!composed.Valid)throw new Exception(JsonConvert.SerializeObject(composed.diagnostics,Formatting.Indented));
                Console.WriteLine("PASS staging "+name+": "+composed.instances.Count+" instances; "+source.surfaces.Length+" terrain recipes; unpublished");
            }
            return;
        }
        if(cmd=="compare")
        {if(args.Length!=3)throw new ArgumentException("compare <before-sidecar.json> <after-sidecar.json>");Process("tools/scene-composition/compare.py",args.Skip(1));return;}
        if(cmd=="preview")
        {Console.WriteLine("Unity Editor: Quiet Camp/Composition/Preview. Frame entity: "+id+". This command never silently launches Editor or scans ADB.");return;}
        if(cmd=="validate"||cmd=="compose"||cmd=="bake")Process("tools/scene-composition/validate_schema.py",new[]{Root});
        var docs=Documents();var assets=Read<VisualAssetDefinition[]>(Reference("assets")).ToDictionary(a=>a.id);var templates=Read<EnsembleTemplate[]>(Reference("templates")).ToDictionary(a=>a.id);
        if(cmd=="patch")
        {
            if(args.Length!=6||args[2]!="--expected-hash"||args[4]!="--patch-file")throw new ArgumentException("patch <id> --expected-hash <hash> --patch-file <json>");
            var found=Find(id);if(found.entity==null)throw new Exception("Generated/unknown ID: inspect it, then patch its source owner/template");
            if(found.hash!=args[3])throw new Exception("stale-patch: source changed; inspect again");
            var patch=Read<JObject>(args[5]);if(patch["id"]!=null||patch["schemaVersion"]!=null)throw new Exception("patch cannot change identity/schema");
            found.entity.Merge(patch,new JsonMergeSettings{MergeArrayHandling=MergeArrayHandling.Replace,MergeNullValueHandling=MergeNullValueHandling.Merge});
            if(found.root is JArray)
            {
                bool template=Path.GetFullPath(found.file)==Path.GetFullPath(Reference("templates"));
                Process("tools/scene-composition/validate_schema.py",new[]{"--catalog",template?"ensemble-templates.schema.json":"visual-assets.schema.json"},found.root.ToString());
                if(template)templates=found.root.ToObject<EnsembleTemplate[]>(Serializer).ToDictionary(t=>t.id);else assets=found.root.ToObject<VisualAssetDefinition[]>(Serializer).ToDictionary(t=>t.id);
            }
            else
            {Process("tools/scene-composition/validate_schema.py",new[]{"--stdin"},found.root.ToString());var doc=found.root.ToObject<SceneCompositionDocument>(Serializer);docs[doc.id]=doc;}
            var diagnostics=Compose(docs,assets,templates).Values.SelectMany(r=>r.diagnostics).Where(d=>d.severity=="error").ToArray();
            if(diagnostics.Length>0)throw new Exception(JsonConvert.SerializeObject(diagnostics,Formatting.Indented));
            // Recheck immediately before publication; never overwrite an edit arriving during validation.
            if(HashFile(found.file)!=args[3])throw new Exception("stale-patch: source changed during validation");
            Atomic(found.file,found.root.ToString(Formatting.Indented)+"\n");Console.WriteLine("Patched "+id+"; sourceHash="+HashFile(found.file));return;
        }
        var results=Compose(docs,assets,templates);
        if(cmd=="inspect")
        {
            var found=Find(id);
            if(found.entity!=null)
            {Console.WriteLine(JsonConvert.SerializeObject(new{source=found.file,sourceHash=found.hash,sourcePointer=JsonPointer(found.entity),entity=found.entity,children=results.Values.SelectMany(r=>r.instances).Where(i=>i.owner==id),candidates=results.Values.SelectMany(r=>r.candidates).Where(i=>i.entityId==id),diagnostics=results.Values.SelectMany(r=>r.diagnostics).Where(d=>d.entityId==id)},Formatting.Indented));return;}
            var instance=results.Values.SelectMany(r=>r.instances).FirstOrDefault(i=>i.id==id);if(instance==null)throw new Exception("Unknown entity: "+id);
            var owner=Find(instance.owner);Console.WriteLine(JsonConvert.SerializeObject(new{generated=true,instance,source=owner.file,sourceHash=owner.hash,patchTarget=instance.owner,asset=assets[instance.asset],instruction="Change owner intention or template, never generated transforms"},Formatting.Indented));return;
        }
        if(cmd!="validate"&&cmd!="compose"&&cmd!="bake")throw new Exception("Unknown command: "+cmd);
        var all=results.Values.SelectMany(r=>r.diagnostics).ToArray();Console.WriteLine(JsonConvert.SerializeObject(new{regions=docs.Count,instances=results.Values.Sum(r=>r.instances.Count),spans=results.Values.Sum(r=>r.spans.Count),diagnostics=all,candidates=results.Values.Sum(r=>r.candidates.Count),rejected=results.Values.Sum(r=>r.candidates.Count(c=>!c.accepted))},Formatting.Indented));
        if(results.Values.Any(r=>!r.Valid))throw new Exception("Composition rejected; previous bake retained");
        if((string)Read<JObject>(Root+"/manifest.json")["id"]=="quiet-camp-diorama-studies")
        {
            if(cmd=="bake")throw new Exception("Style studies are preview-only; no runtime publication");
            foreach(var doc in docs.Values)
            {
                var repeat=SceneComposer.Compose(doc,assets,templates);
                if(JsonConvert.SerializeObject(repeat)!=JsonConvert.SerializeObject(results[doc.id]))throw new Exception("Non-deterministic study: "+doc.id);
                if(repeat.parcels.Count!=doc.ensembles.Length)throw new Exception("Incomplete study ensemble: "+doc.id);
            }
            Console.WriteLine("PASS independent study determinism and complete ensembles; Main regression contracts are a separate check.");
        }
        else CompositionContracts.Run(docs,assets,templates);
        if(cmd=="bake")
        {Atomic(Root+"/bake-preview.json",JsonConvert.SerializeObject(new{revision=SceneComposer.Revision,sourceHash=SourceHash(),results},Formatting.Indented)+"\n");Console.WriteLine("Portable placement bake ready; publish native resources through Quiet Camp/Composition/Bake Main. Runtime catalog unchanged.");}
        else if(cmd=="compose")
        {
            var current=results.Values.SelectMany(r=>r.instances).ToDictionary(i=>i.id);
            var previous=File.Exists(Root+"/bake-preview.json")?Read<JObject>(Root+"/bake-preview.json")["results"].Children<JProperty>().SelectMany(p=>p.Value["instances"].ToObject<ComposedInstance[]>()).ToDictionary(i=>i.id):new Dictionary<string,ComposedInstance>();
            Console.WriteLine(JsonConvert.SerializeObject(new{sourceHash=SourceHash(),dryRun=true,added=current.Keys.Except(previous.Keys).OrderBy(x=>x),removed=previous.Keys.Except(current.Keys).OrderBy(x=>x),changed=current.Keys.Intersect(previous.Keys).Where(k=>JsonConvert.SerializeObject(current[k])!=JsonConvert.SerializeObject(previous[k])).OrderBy(x=>x)},Formatting.Indented));
        }
    }
    static string JsonPointer(JToken token)
    {
        var parts=new List<string>();while(token.Parent!=null){if(token.Parent is JProperty p){parts.Add(p.Name.Replace("~","~0").Replace("/","~1"));token=p.Parent;}else if(token.Parent is JArray array){parts.Add(array.IndexOf(token).ToString());token=array;}else token=token.Parent;}parts.Reverse();return "/"+string.Join("/",parts);
    }
    static (string file,JToken root,JObject entity,string hash) Find(string id)
    {
        (string file,JToken root,JObject entity,string hash) found=default;
        var m=Read<JObject>(Root+"/manifest.json");var files=m["regions"].Values<string>().Select(n=>Root+"/"+n).Concat(new[]{Reference("assets"),Reference("templates")});
        foreach(var file in files)
        {
            var snapshot=File.ReadAllBytes(file);string hash=Convert.ToHexString(SHA256.HashData(snapshot)).ToLowerInvariant();var root=JToken.Parse(System.Text.Encoding.UTF8.GetString(snapshot).TrimStart('\uFEFF'));var objects=root is JObject o?o.DescendantsAndSelf():((JArray)root).Descendants();
            foreach(var entity in objects.OfType<JObject>().Where(j=>(string)j["id"]==id))
            {if(found.entity!=null)throw new Exception("Ambiguous source ID; use an explicit ensemble/template ID");found=(file,root,entity,hash);}
        }
        return found;
    }
    public static string SourceHash()
    {
        var manifest=Read<JObject>(Root+"/manifest.json");
        var files=new[]{Root+"/manifest.json",Reference("assets"),Reference("templates")}.Concat(manifest["regions"].Values<string>().Select(n=>Root+"/"+n)).ToList();
        if((string)manifest["id"]=="quiet-camp-main")files.AddRange(new[]{"QuietCamp/Assets/QuietCamp/Authoring/Roadmap/main.json","QuietCamp/Assets/QuietCamp/Resources/QuietCamp/level_summaries.json","QuietCamp/Assets/QuietCamp/Resources/QuietCamp/atmosphere.json"});
        // Hash the actual executing compiler, not uncompiled source files. Portable
        // and native bake hashes describe different artifacts and are not interchangeable.
        return Hash(SceneComposer.Revision+"\n"+HashFile(typeof(SceneComposer).Assembly.Location)+"\n"+string.Join("\n",files.Select(File.ReadAllText)));
    }
    public static void Atomic(string file,string text)
    {string tmp=file+".tmp-"+Guid.NewGuid().ToString("N");try{File.WriteAllText(tmp,text);File.Move(tmp,file,true);}finally{if(File.Exists(tmp))File.Delete(tmp);}}
}
