using System;
using System.Collections.Generic;
using System.Linq;
using Kruty1918.ScenePlacement;
using Kruty1918.MeshStyle;
using Kruty1918.ModelWeathering;
using Kruty1918.FenceAuthoring;
using Kruty1918.RoadAuthoring;
using Kruty1918.PowerLines;
using Kruty1918.RockPlacement;
using Kruty1918.VegetationPlacement;

static class EnvironmentIntegrationProbe
{
    sealed class Fields:IEnvironmentalFields
    {
        readonly float wet;public Fields(float wet){this.wet=wet;}
        public float Sample(string field,V3 p,float fallback)=>field=="moisture"?wet:field=="light"?.65f:field=="density"?.8f:fallback;
    }
    static AssetShape Shape(string id,float halfXZ,float height)=>new AssetShape{id=id,contacts=new[]{V3.Zero},volumes=new[]{new Box(new V3(0,height*.5f,0),new V3(halfXZ,height*.5f,halfXZ))}};
    static void Require(bool ok,string label){if(!ok)throw new Exception(label);}
    static ISurfaceQuery Surface(int type)
    {
        if(type==0)return new PlaneSurface();
        if(type==1){var heights=new float[41*41];for(int z=0;z<41;z++)for(int x=0;x<41;x++)heights[z*41+x]=x*.012f;return new HeightFieldSurface(heights,41,41,.5f,new V3(-10,0,-10));}
        var b=new GeometryBuilder();b.Quad(new V3(-10,-.2f,-10),new V3(-10,-.2f,10),new V3(10,.2f,10),new V3(10,.2f,-10),Tint.White);return new TriangleSurface(b.Build());
    }
    static void Main()
    {
        var names=new[]{"forest","maintained-farm","abandoned-industry"};
        for(int kind=0;kind<names.Length;kind++)
        {
            string id=names[kind];var surface=Surface(kind);var context=new PlacementContext(surface);
            var road=RoadGenerator.Generate(new RoadSpec{id=id+"/road",seed=10+kind,smooth=false,points=new[]{new V3(-7,0,-5),new V3(0,0,-4.5f),new V3(7,0,-5)},bands=new[]{new SurfaceBand{width=.8f},new SurfaceBand{distance=14,width=1.1f}}},surface);
            Require(road.Ok,"road on "+id);foreach(var box in road.corridor)context.Reserve(id+"/road",box);
            var fence=FenceGenerator.Generate(new FenceSpec{id=id+"/fence",seed=1,closed=true,jitter=0,lean=0,contour=new[]{new V3(-3,0,0),new V3(3,0,0),new V3(3,0,6),new V3(-3,0,6)},openings=new[]{new FenceOpening{id="gate",from=1.5f,to=3,gate=true}}},new FenceKit{post=Shape("post-"+kind,.05f,1),section=new AssetShape{id="rail-"+kind,contacts=new[]{V3.Zero},volumes=new[]{new Box(V3.Zero,new V3(.75f,.15f,.025f))}},gate=new AssetShape{id="gate-"+kind,contacts=new[]{V3.Zero},volumes=new[]{new Box(V3.Zero,new V3(.75f,.2f,.03f))}}},context);
            Require(fence.Ok,"connected fence on "+id);
            var entrance=fence.connections.Single(c=>c.gate);var approach=RoadGenerator.Generate(new RoadSpec{id=id+"/approach",smooth=false,points=new[]{new V3(-.75f,0,-4.55f),new V3(-.75f,0,-.3f)},bands=new[]{new SurfaceBand{width=.35f}}},surface);Require(approach.Ok,"gate approach on "+id);
            Require((new V3(entrance.from.x+entrance.to.x,0,entrance.from.z+entrance.to.z)*.5f-new V3(-.75f,0,0)).Length<.01f,"gate ownership");foreach(var box in approach.corridor)context.Reserve(id+"/approach",box);
            var modelBuilder=new GeometryBuilder();modelBuilder.Box(new V3(0,.5f,0),new V3(.5f,1,.5f),new Tint(.65f,.54f,.35f));var model=modelBuilder.Build();
            model.parts=new[]{new MeshPart{id="base",role="support",preserve=true,triangles=new[]{10,11}}};var worn=Weathering.Apply(id+"/prop",model,new WeatheringProfile{seed=11,intensity=kind==1?.15f:.7f});
            var styled=MeshHarmonizer.Harmonize(worn.mesh,new StyleProfile{referenceTriangles=12,maximumWorldError=.02f},10000);
            var prop=PlacementSolver.Place(new PlacementRequest{id=id+"/prop",shape=AssetShape.FromMesh("foreign-model-"+kind,styled.mesh),target=new V3(-5,0,2)},context);Require(prop.Ok,"processed prop placement on "+id);
            var line=PowerGenerator.Generate(new PowerSpec{id=id+"/line",route=road.route,offset=1.3f,minimumSpan=3,maximumSpan=8,sag=.25f},new SupportKit{standing=Shape("pole-"+kind,.05f,3),sockets=new[]{new V3(0,2.8f,0)}},context);Require(line.Ok,string.Join(";",line.diagnostics));
            var rocks=RockGenerator.Generate(new RockSpec{id=id+"/rocks",seed=5,rocks=new[]{Shape("stone-"+kind,.12f,.2f)},groups=new[]{new RockGroup{id="bank",center=new V3(-6,0,4),radius=1,count=4,maximumScale=.8f}}},context);Require(rocks.Ok,"rocks on "+id);
            context.Reserve("playable-clearance",new OrientedBox(new Box(new V3(0,.2f,3),new V3(1.2f,.2f,1.2f)),Pose.At(V3.Zero)));
            var vegetation=VegetationGenerator.Generate(new VegetationSpec{id=id+"/plants",seed=42,zone=new Box(V3.Zero,new V3(8,1,8)),cellSize=.65f,density=.85f,species=new[]{new Species{id="foreign-understory-"+kind,shape=Shape("plant-"+kind,.07f,.25f),spacing=.18f}}},context,new Fields(kind==0?.7f:.4f));
            Require(vegetation.Ok&&vegetation.acceptedTotal>30,"ecological understory on "+id);
            Console.WriteLine(id+": PASS all eight packages; "+vegetation.acceptedTotal+" plants, "+fence.connections.Count+" fence spans, "+line.spans.Count+" cable spans; no game assets");
        }
        Console.WriteLine("3 synthetic Core integrations passed; native visual acceptance pending.");
    }
}
