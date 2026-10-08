using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using QuietCamp.Composition;
static class CompositionContracts
{
    sealed class Surface:ITerrainSample
    {
        public Func<float,float,float,bool> support=(x,z,r)=>true;public Func<float,float,float> height=(x,z)=>0;
        public bool Supported(float x,float z,float radius)=>support(x,z,radius);public float Height(float x,float z)=>height(x,z);
    }
    static T Copy<T>(T x)=>JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(x));
    public static void Run(Dictionary<string,SceneCompositionDocument> docs,Dictionary<string,VisualAssetDefinition> assets,Dictionary<string,EnsembleTemplate> templates)
    {
        void Require(bool ok,string why){if(!ok)throw new Exception(why);}
        foreach(var doc in docs.Values)
        {
            var a=SceneComposer.Compose(doc,assets,templates);var b=SceneComposer.Compose(doc,assets,templates);
            Require(a.Valid,doc.id+": invalid source");Require(JsonConvert.SerializeObject(a)==JsonConvert.SerializeObject(b),"Nondeterministic composition");
            Require(a.instances.Select(i=>i.id).Distinct().Count()==a.instances.Count,"Duplicate ownership");
            foreach(var gate in a.instances.Where(i=>i.role=="gate"))
            {Require(a.instances.Any(i=>i.owner==gate.owner&&i.role.StartsWith("fence")),"Gate without boundary");Require(a.spans.Any(s=>s.a==gate.id&&s.height==0),"Disconnected gate");}
            foreach(var span in a.spans.Where(s=>s.height>0))Require(a.instances.Any(i=>i.id==span.a)&&a.instances.Any(i=>i.id==span.b),"Span without endpoints");
            if(doc.ensembles.Length==0)continue;
            void Bad(string code,Action<SceneCompositionDocument> mutate)
            {var bad=Copy(doc);mutate(bad);var result=SceneComposer.Compose(bad,assets,templates);Require(result.diagnostics.Any(d=>d.code==code),"Missing diagnostic "+code);}
            Bad("schema",d=>d.routes[0].points=null);
            Bad("schema",d=>d.zones[0]=null);
            var missing=Copy(doc);missing.ensembles[0].placement.zone="missing";var missingDiagnostic=SceneComposer.Compose(missing,assets,templates).diagnostics.First(d=>d.code=="missing-zone");
            Require(missingDiagnostic.sourcePointer=="/ensembles/0/placement/zone","Diagnostic lost precise source pointer");
            Bad("duplicate-id",d=>d.ensembles[0].id=d.nodes[0].id);
            Bad("missing-zone",d=>d.ensembles[0].placement.zone="missing");
            Bad("invalid-season",d=>d.season="unknown");
            Bad("invalid-budget",d=>d.budgets.cacheBytes=67108865);
            Bad("instance-budget",d=>d.budgets.instances=1);
            Bad("invalid-placement",d=>d.ensembles[0].scale=0);
            Bad("missing-template",d=>d.ensembles[0].template="missing");
            Bad("disconnected-entrance",d=>d.ensembles[0].entranceConnectsTo="missing");
            Bad("ensemble-does-not-fit",d=>{d.ensembles[0].placement.fixedPosition=true;d.ensembles[0].placement.x=d.nodes[0].x;d.ensembles[0].placement.z=d.nodes[0].z;});
            var blocked=SceneComposer.Compose(doc,assets,templates,new Surface{support=(x,z,r)=>false});Require(!blocked.Valid&&!blocked.instances.Any(),"Unsupported surface admitted children");
            var wetApproach=SceneComposer.Compose(doc,assets,templates,new Surface{support=(x,z,r)=>r>.4f});Require(wetApproach.diagnostics.Any(d=>d.code=="unsupported-approach"),"Water-crossing approach admitted");
            foreach(var e in doc.ensembles)Require(!wetApproach.instances.Any(i=>i.owner==e.id),"Failed approach left yard children");
            var steep=SceneComposer.Compose(doc,assets,templates,new Surface{height=(x,z)=>x*2});Require(steep.diagnostics.Any(d=>d.code=="ensemble-does-not-fit"),"Unsupported slope admitted");
            void BadTemplate(string code,Action<EnsembleTemplate> mutate)
            {var changed=Copy(templates);mutate(changed[doc.ensembles[0].template]);var result=SceneComposer.Compose(doc,assets,changed);Require(result.diagnostics.Any(d=>d.code==code),"Missing template diagnostic "+code);}
            BadTemplate("fence-without-owner",t=>{t.ruined=false;t.roles.First(r=>r.id.StartsWith("fence")).parent=null;});
            BadTemplate("gate-without-boundary",t=>t.roles.First(r=>r.id=="gate").z=0);
            BadTemplate("orphan-role",t=>t.roles.First(r=>r.id=="gate").parent="missing");
            BadTemplate("dependency-cycle",t=>t.roles.First(r=>r.id=="house").parent="gate");
            BadTemplate("role-overlap",t=>{var house=t.roles.First(r=>r.id=="house");var detail=t.roles.First(r=>r.id=="well");detail.x=house.x;detail.z=house.z;});
            BadTemplate("missing-role-asset",t=>t.roles.First(r=>r.id=="house").asset="missing");
            var seasonal=Copy(assets);string houseAsset=templates[doc.ensembles[0].template].roles.First(r=>r.id=="house").asset;seasonal[houseAsset].seasons=new[]{"unavailable"};Require(SceneComposer.Compose(doc,seasonal,templates).diagnostics.Any(d=>d.code=="invalid-seasonal-variant"),"Seasonal mismatch admitted");
            if(doc.routes.Any(r=>r.kind=="power"))
            {
                Bad("invalid-span",d=>{var r=d.routes.First(r=>r.kind=="power");r.points[1].x=r.points[0].x;r.points[1].z=r.points[0].z;});
                Bad("missing-service-target",d=>d.routes.First(r=>r.kind=="power").kind="distribution");
                Bad("incompatible-support",d=>d.routes.First(r=>r.kind=="power").supportAsset="tree_default");
                Bad("missing-conductor-sockets",d=>d.routes.First(r=>r.kind=="power").supportAsset="tree_default");
                Require(blocked.diagnostics.Any(d=>d.code=="unsupported-pylon"),"Unsupported span endpoint admitted");
            }
            var ruined=Copy(doc);ruined.routes=ruined.routes.Where(r=>r.kind!="distribution").ToArray();ruined.surfaces=ruined.surfaces.Where(s=>s.owner==null).ToArray();ruined.ensembles=new[]{new EnsembleIntent{id="test-ruin",template="ua.civilian-foundation",placement=Copy(doc.ensembles[0].placement)}};Require(SceneComposer.Compose(ruined,assets,templates).Valid,"Explicit contextual ruin rejected");
            var failed=Copy(doc);failed.ensembles[0].placement.zone="missing";var partial=SceneComposer.Compose(failed,assets,templates);Require(!partial.instances.Any(i=>i.owner==failed.ensembles[0].id),"Rejected ensemble left children");
            var changed=Copy(doc);changed.ensembles[0].state="remembered";changed.ensembles[0].scale*=.96f;var c=SceneComposer.Compose(changed,assets,templates);Require(c.Valid,"Local geometry edit failed");
            Require(JsonConvert.SerializeObject(a.instances.Where(i=>i.owner!=doc.ensembles[0].id))==JsonConvert.SerializeObject(c.instances.Where(i=>i.owner!=doc.ensembles[0].id)),"Local edit shuffled neighbors");
        }
        var first=new SceneCompositionDocument{id="world-a",zones=new[]{new LandscapeZone{id="zone-a",width=100,depth=100}},ensembles=new[]{new EnsembleIntent{id="place-a",template="ua.civilian-foundation",placement=new PlacementIntent{zone="zone-a",fixedPosition=true}}}};
        var second=Copy(first);second.id="world-b";second.zones[0].id="zone-b";second.ensembles[0].id="place-b";second.ensembles[0].placement.zone="zone-b";
        var overlap=SceneComposer.ComposeWorld(new[]{first,second},assets,templates);
        Require(overlap[0].Valid&&!overlap[1].Valid&&overlap[1].diagnostics.Any(d=>d.code=="ensemble-does-not-fit"),"Cross-region parcels overlap");
        second.ensembles=Array.Empty<EnsembleIntent>();second.nodes=new[]{new SceneAnchor{id="neighbour-clearing",radius=6}};
        Require(!SceneComposer.ComposeWorld(new[]{first,second},assets,templates)[0].Valid,"Adjacent-region clearing ignored");
        second.nodes=Array.Empty<SceneAnchor>();second.id=first.id;
        Require(SceneComposer.ComposeWorld(new[]{first,second},assets,templates).Any(r=>r.diagnostics.Any(d=>d.code=="duplicate-world-id")),"Duplicate global identity admitted");
        var roadA=new SceneCompositionDocument{id="corridor-a",routes=new[]{new InfrastructureRouteDefinition{id="route-a",nextRoute="route-b",points=new[]{new Point(0,0),new Point(0,20)}}}};
        var roadB=new SceneCompositionDocument{id="corridor-b",routes=new[]{new InfrastructureRouteDefinition{id="route-b",points=new[]{new Point(0,20),new Point(10,40)}}}};
        Require(SceneComposer.ComposeWorld(new[]{roadA,roadB},assets,templates).All(r=>r.Valid),"Valid continuous road rejected");
        roadB.routes[0].points[0].x=1;Require(SceneComposer.ComposeWorld(new[]{roadA,roadB},assets,templates).Any(r=>r.diagnostics.Any(d=>d.code=="disconnected-route-continuation")),"Broken regional road seam admitted");
        roadB.routes[0].id="missing";Require(SceneComposer.ComposeWorld(new[]{roadA,roadB},assets,templates).Any(r=>r.diagnostics.Any(d=>d.code=="missing-route-continuation")),"Missing corridor endpoint admitted");
        var damaged=new SceneCompositionDocument{id="damaged-network",routes=new[]{new InfrastructureRouteDefinition
        {id="damaged-line",kind="power",supportAsset="ua_power_pylon",points=new[]{new Point(0,0),new Point(0,80)},
         minSpan=20,maxSpan=40,fallenAsset="ua_power_pylon_fallen",fallenSupports=new[]{1},
         damage=new[]{new SpanDamage{span=0,state="hanging-from"},new SpanDamage{span=1,state="hanging-to"}}}}};
        var damageResult=SceneComposer.Compose(damaged,assets,templates);
        Require(damageResult.Valid&&damageResult.instances.Count(i=>i.role=="fallen-support")==1,"Valid fallen network rejected");
        Require(damageResult.spans.All(s=>s.state!="intact"),"Fallen tower remains a live conductor support");
        var badDamage=Copy(damaged);badDamage.routes[0].damage=Array.Empty<SpanDamage>();
        Require(SceneComposer.Compose(badDamage,assets,templates).diagnostics.Any(d=>d.code=="fallen-live-conductor"),"Live fallen conductor admitted");
        badDamage=Copy(damaged);badDamage.routes[0].damage[0].span=9;
        Require(SceneComposer.Compose(badDamage,assets,templates).diagnostics.Any(d=>d.code=="invalid-power-damage"),"Unknown damage index admitted");
        badDamage=Copy(damaged);badDamage.routes[0].damage[0].state="hanging-to";
        Require(SceneComposer.Compose(badDamage,assets,templates).diagnostics.Any(d=>d.code=="fallen-live-conductor"),"Hanging end attached to fallen tower");
        badDamage=Copy(damaged);badDamage.routes[0].damage[0].dropLength=float.NaN;
        Require(SceneComposer.Compose(badDamage,assets,templates).diagnostics.Any(d=>d.code=="invalid-power-damage"),"NaN drop length admitted");
        Console.WriteLine("PASS conductor damage: standing attachments, fallen exclusion, stale indices and invalid drop rejection");
        var hydrology=new SceneCompositionDocument{id="hydrology",surfaces=new[]{new SurfaceRecipe
        {id="channel",kind="channel",heightOffset=-.2f,points=new[]{new Point(-2,0),new Point(2,0),new Point(2,40),new Point(-2,40)}}},
        routes=new[]{new InfrastructureRouteDefinition{id="dry-detour",kind="path",points=new[]{new Point(-8,0),new Point(-8,40)}}}};
        Require(SceneComposer.Compose(hydrology,assets,templates).Valid,"Dry hydrology bypass rejected");
        Require(SurfaceRecipes.Height(hydrology.surfaces,0,20)<0&&SurfaceRecipes.Height(hydrology.surfaces,8,20)==0,"Surface height leaves footprint");
        var wet=Copy(hydrology);wet.routes[0].points=new[]{new Point(-8,20),new Point(8,20)};
        Require(SceneComposer.Compose(wet,assets,templates).diagnostics.Any(d=>d.code=="wet-route-crossing"),"Route across breach/channel admitted");
        wet=Copy(hydrology);wet.surfaces[0].points=new[]{new Point(-2,0),new Point(2,40),new Point(2,0),new Point(-2,40)};
        Require(SceneComposer.Compose(wet,assets,templates).diagnostics.Any(d=>d.code=="invalid-surface"),"Self-crossing surface admitted");
        wet=Copy(hydrology);wet.surfaces[0].kind="garden";wet.surfaces[0].owner="missing-yard";
        Require(SceneComposer.Compose(wet,assets,templates).diagnostics.Any(d=>d.code=="orphan-surface"),"Unowned rear garden admitted");
        var supported=new SurfaceRecipes.Terrain(new FlatTerrain(),hydrology.surfaces);
        Require(!supported.Supported(0,20,1)&&!supported.Supported(2.5f,20,1)&&supported.Supported(8,20,1),"Channel footprint clearance ignored");
        Console.WriteLine("PASS surface recipes: dry detour, shape/ownership rejection, supported ground and bounded relief");
        var frontage=templates["ua.adjacent-yards"];
        Require(ParcelBoundary.Closed(frontage,frontage.roles,assets,"picket"),"Adjacent yard perimeter is open");
        Require(!ParcelBoundary.Closed(frontage,frontage.roles.Where(r=>!r.id.StartsWith("fence-back")),assets,"picket"),"Missing rear fence accepted");
        Require(!ParcelBoundary.Closed(frontage,frontage.roles.Where(r=>r.id!="gate-east"),assets,"picket"),"Undeclared gate gap accepted");
        foreach(var doc in docs.Values)foreach(var ensemble in doc.ensembles.Where(e=>e.template=="ua.adjacent-yards"))
        {
            var composed=SceneComposer.Compose(doc,assets,templates);
            foreach(var gate in templates[ensemble.template].roles.Where(r=>r.entrance))
                Require(composed.spans.Any(s=>s.height==0&&s.a==ensemble.id+"/"+gate.id),"Secondary yard has no road approach");
        }
        Console.WriteLine("PASS adjacent parcels: all-edge coverage, gate gaps and independent road approaches");
        foreach(var doc in docs.Values.Where(d=>d.surfaces.Any(s=>s.relativeToOwner)))
        {
            var before=SceneComposer.Compose(doc,assets,templates);var moved=Copy(doc);var owner=moved.ensembles.First(e=>moved.surfaces.Any(s=>s.relativeToOwner&&s.owner==e.id));owner.scale*=.96f;
            var after=SceneComposer.Compose(moved,assets,templates);Require(after.Valid,"Owner resize invalid");
            var a=SurfaceRecipes.Resolve(doc,before).First(s=>s.owner==owner.id);var b=SurfaceRecipes.Resolve(moved,after).First(s=>s.owner==owner.id);
            Require(JsonConvert.SerializeObject(a.points)!=JsonConvert.SerializeObject(b.points),"Owned garden detached from resized yard");
            Require(doc.surfaces.Any(s=>s.relativeToOwner),"Resolution mutated source authoring");
        }
        Console.WriteLine("PASS semantic composition: atomic ensembles, gates/roads, supports, deterministic IDs, local edits and negative cases");
    }
}
