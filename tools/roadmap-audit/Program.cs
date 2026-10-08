using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using QuietCamp.Domain;
using QuietCamp.Application;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

internal static class Program
{
    static void Invoke(object owner,string name,params object[] args)
        =>owner.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(owner,args);
    static void Require(bool ok,string message) { if(!ok)throw new InvalidOperationException(message); }
    static void Main()
    {
        var resources=Path.Combine(Environment.CurrentDirectory,"QuietCamp/Assets/QuietCamp/Resources/QuietCamp");
        var source=JsonConvert.DeserializeObject<LevelSummary[]>(File.ReadAllText(Path.Combine(resources,"level_summaries.json")));
        BonusCampCatalog.Slots=JsonConvert.DeserializeObject<BonusCampDefinition[]>(File.ReadAllText(Path.Combine(resources,"bonus_camps.json")));
        var rows=new List<object>();
        foreach(int count in new[]{30,300,1000})
        {
            // Generated test IDs stay in RAM and never become campaign content.
            CampContent.Summaries=Enumerable.Range(0,count).Select(i=>new LevelSummary{id="audit:"+i,number=i+1,decorSeed=source[i%source.Length].decorSeed}).ToArray();
            var root=new GameObject("audit scroll");var scroll=root.AddComponent<ScrollRect>();
            scroll.viewport=new GameObject("viewport").transform;scroll.viewport.rect=new Rect(0,-1000,850,1000);
            var map=QcUi.Stretch(root.transform,"map").gameObject.AddComponent<RoadmapGraphic>();
            map.rectTransform.rect=new Rect(0,-RoadmapLayout.Height(count),850,RoadmapLayout.Height(count));
            RoadmapSceneGenerator.Generated=RoadmapSceneGenerator.Advanced=0;
            var services=new GameServices();map.Configure(services);
            int generated=RoadmapSceneGenerator.Generated;Mathf.Floors=0;Time.unscaledTime=0;
            Invoke(map,"LateUpdate");int centresVisited=Mathf.Floors;int advanced=RoadmapSceneGenerator.Advanced;
            Mathf.Floors=0;Invoke(map,"OnPopulateMesh",new VertexHelper());int pathNodeEvaluations=Mathf.Floors;
            Require(generated==count&&advanced==count&&centresVisited==count,"Current source no longer scans/generates the full campaign; update the audit.");
            Require(pathNodeEvaluations==36*(count-1),"Background path loop count changed.");
            rows.Add(new{count,scenesGeneratedOnOpen=generated,weatherScenesAdvancedPerFrame=advanced,centresVisitedPerFrame=centresVisited,pathNodeEvaluationsPerBackgroundMesh=pathNodeEvaluations,pooledGlades=map.GladePool.Count});
        }
        Console.WriteLine("Current-source CONTROL-FLOW counts (scene generator and native drawing are doubles):");
        Console.WriteLine(JsonConvert.SerializeObject(rows,Formatting.Indented));

        CampContent.Summaries=source;
        var failedRoot=new GameObject("initialization failure test");failedRoot.AddComponent<ScrollRect>();
        var failed=QcUi.Stretch(failedRoot.transform,"partial map").gameObject.AddComponent<RoadmapGraphic>();
        RoadmapSceneGenerator.Generated=0;RoadmapSceneGenerator.ThrowOnGenerate=3;
        bool threw=false;try { failed.Configure(new GameServices()); } catch(InvalidOperationException) { threw=true; }
        Require(threw&&failed.Scenes.Count==2,"Fault injection must interrupt generation after two scenes.");
        RoadmapSceneGenerator.ThrowOnGenerate=0;failed.Configure(new GameServices());
        Require(failed.Scenes.Count==2&&RoadmapSceneGenerator.Generated==3,"Partial initialization reproduction no longer holds.");
        Console.WriteLine("CONFIRMED initialization recovery failure: retry keeps 2/30 scenes because the painter marks initialization complete before generation finishes.");
        var scrollRoot=new GameObject("pool test");var poolScroll=scrollRoot.AddComponent<ScrollRect>();
        poolScroll.viewport=new GameObject("pool viewport").transform;poolScroll.viewport.rect=new Rect(0,-1000,850,1000);
        var pooled=QcUi.Stretch(scrollRoot.transform,"pool map").gameObject.AddComponent<RoadmapGraphic>();
        var poolServices=new GameServices();pooled.Configure(poolServices);Time.unscaledTime=0;Invoke(pooled,"LateUpdate");
        int[] indices=pooled.GladePool.Select(g=>g.SceneIndex).ToArray();
        pooled.rectTransform.offset=new Vector3(0,464);Time.unscaledTime=.1f;Invoke(pooled,"LateUpdate");
        int[] after=pooled.GladePool.Select(g=>g.SceneIndex).ToArray();
        int reassigned=indices.Zip(after).Count(p=>p.First!=p.Second);
        Require(reassigned==3,"The fixture expected the three-slot index shift.");
        Console.WriteLine("CONFIRMED positional pool churn: "+string.Join(',',indices)+" -> "+string.Join(',',after)+"; three slots reassigned for one entering glade.");
        poolServices.ReducedMotion=true;int dirtyBefore=pooled.GladePool.Sum(g=>g.Dirties);Time.unscaledTime=2;Invoke(pooled,"LateUpdate");
        Console.WriteLine("CONFIRMED reduced-motion idle still dirties "+(pooled.GladePool.Sum(g=>g.Dirties)-dirtyBefore)+" glade meshes at the one-second refresh.");

        LevelLoader.Ids=source.Select(s=>s.id).ToArray();
        var bindingRoot=new GameObject("binding test");var bindingScroll=bindingRoot.AddComponent<ScrollRect>();
        bindingScroll.viewport=new GameObject("binding viewport").transform;bindingScroll.viewport.rect=new Rect(0,-1000,850,1000);
        var binding=bindingRoot.AddComponent<MenuMapBinding>();var bindingServices=new GameServices {LevelMapScroll=.8f};
        binding.Configure(bindingServices,()=>true);for(int i=0;i<3;i++)Invoke(binding,"LateUpdate");
        bindingScroll.verticalNormalizedPosition=.6f;bindingScroll.onValueChanged.Invoke(Vector2.zero);
        Require(bindingServices.LevelMapScroll==.6f,"Active binding must initially remember position.");
        Invoke(binding,"OnDisable");binding.Configure(bindingServices,()=>true);for(int i=0;i<5;i++)Invoke(binding,"LateUpdate");
        bindingScroll.verticalNormalizedPosition=.2f;bindingScroll.onValueChanged.Invoke(Vector2.zero);
        Require(bindingServices.LevelMapScroll==.6f,"Reenable reproduction no longer holds; update audit.");
        Console.WriteLine("CONFIRMED binding reenable failure: visible normalized position=.2; remembered=.6 after disable, reconfigure and five frames.");

        var ids=Enumerable.Range(0,300).Select(i=>"progress:"+i).ToArray();var progress=new ProgressionService();
        var catalog=new JourneyCatalog(new[]{new JourneyDefinition{id="main",published=true,levelIds=ids}});
        var access=new JourneyAccessService(catalog,progress,new EntitlementSaveData(),()=>false);
        Require(access.Evaluate(ids[0]).CanStart&&!access.Evaluate(ids[1]).CanStart,"Initial sequential access changed.");
        progress.MarkCompleted(ids[0]);Require(access.Evaluate(ids[1]).CanStart&&!access.Evaluate(ids[2]).CanStart,"Main progression changed.");
        progress.MarkCompleted("bonus:unrelated");Require(!access.Evaluate(ids[2]).CanStart,"Side route unlocked an unrelated main node.");
        var bonus=new BonusCampDefinition{id="bonus:test",afterLevel=10,requiredCompletions=10,levelId="bonus:published"};
        var bonusAccess=new BonusCampAccessService(progress,ids,_=>true);
        foreach(var id in ids.Take(10))progress.MarkCompleted(id);
        Require(bonusAccess.Evaluate(bonus).CanPlay,"Published eligible bonus must be playable.");
        Require(!new BonusCampAccessService(progress,ids,_=>false).Evaluate(bonus).CanPlay,"Unpublished bonus must stay blocked.");
        Console.WriteLine("PASS actual progression and bonus gate contracts on 300 synthetic IDs; no save slots used.");
        Console.WriteLine("No Unity renderer, real scene generation cost, frame time, shader, native lifecycle or device FPS verified.");
    }
}
