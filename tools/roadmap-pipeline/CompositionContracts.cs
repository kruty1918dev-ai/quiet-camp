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
            BadTemplate("role-overlap",t=>{var tree=t.roles.First(r=>r.id.StartsWith("orchard"));tree.x=-.7f;tree.z=-1.5f;});
            BadTemplate("missing-role-asset",t=>t.roles.First(r=>r.id=="house").asset="missing");
            var seasonal=Copy(assets);seasonal["ua_whitewashed_house"].seasons=new[]{"unavailable"};Require(SceneComposer.Compose(doc,seasonal,templates).diagnostics.Any(d=>d.code=="invalid-seasonal-variant"),"Seasonal mismatch admitted");
            if(doc.routes.Any(r=>r.kind=="power"))
            {
                Bad("invalid-span",d=>{var r=d.routes.First(r=>r.kind=="power");r.points[1].x=r.points[0].x;r.points[1].z=r.points[0].z;});
                Bad("missing-service-target",d=>d.routes.First(r=>r.kind=="power").kind="distribution");
                Bad("incompatible-support",d=>d.routes.First(r=>r.kind=="power").supportAsset="tree_default");
                Bad("missing-conductor-sockets",d=>d.routes.First(r=>r.kind=="power").supportAsset="tree_default");
                Require(blocked.diagnostics.Any(d=>d.code=="unsupported-pylon"),"Unsupported span endpoint admitted");
            }
            var ruined=Copy(doc);ruined.ensembles=new[]{new EnsembleIntent{id="test-ruin",template="ua.civilian-foundation",placement=Copy(doc.ensembles[0].placement)}};Require(SceneComposer.Compose(ruined,assets,templates).Valid,"Explicit contextual ruin rejected");
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
        Console.WriteLine("PASS semantic composition: atomic ensembles, gates/roads, supports, deterministic IDs, local edits and negative cases");
    }
}
