using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;

// Executes the real C# budget policy; no Unity, graphics or phone emulation.
static class MobileBudgetChecks
{
    static void Require(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    static string Digest(object value)=>Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(value))));
    static void QualityChecks()
    {
        foreach(float dt in new[]{1f/3,1f/2,.25f,.5f,1f,1f/30})
        {
            var policy=new CampQualityPolicy();policy.Reset(2);
            for(float t=0;t<40;t+=dt)policy.ObserveFrame(dt);
            Require(policy.Tier==0,"Sustained overload failed to reach Low at frame seconds "+dt);
        }
        var recovering=new CampQualityPolicy();recovering.Reset(0);
        for(int i=0;i<2400;i++)recovering.ObserveFrame(1f/60);
        Require(recovering.Tier==0,"Quality recovered before sustained headroom");
        for(int i=0;i<1800;i++)recovering.ObserveFrame(1f/60);
        Require(recovering.Tier==1,"Quality failed to recover after sustained headroom");
        var manual=new CampQualityPolicy();manual.Reset(0,0);
        for(int i=0;i<7200;i++)manual.ObserveFrame(1f/120);
        Require(manual.Tier==0,"Manual quality ceiling changed");
        manual.ObserveFrame(float.NaN);manual.ObserveFrame(float.PositiveInfinity);manual.ObserveFrame(0);manual.ObserveFrame(-1);
        Require(manual.Tier==0,"Invalid frame corrupted quality");
        var stall=new CampQualityPolicy();stall.Reset(2);stall.ObserveFrame(10);
        for(int i=0;i<12;i++)stall.ObserveFrame(1f/3);
        Require(stall.Tier==2,"Loading stall bypassed warmup");
        Console.WriteLine("PASS quality: sustained 2/3/4/30 FPS, recovery hysteresis, manual ceiling, invalid samples, loading warmup.");
    }
    public static void Run(string resources,string output)
    {
        QualityChecks();
        foreach(int requested in new[]{0,1,10,40,100,1800,100000})
            Require(CampGeometryBudget.WeatherParticleCapacity(requested)<=requested&&CampGeometryBudget.WeatherParticleCapacity(requested)<=400,"Particle capacity is unbounded");
        var campaign=JObject.Parse(File.ReadAllText(Path.Combine(resources,"QuietCamp/campaign.json")));
        var ids=campaign["mvpLevelIds"].Values<string>().Concat(campaign["generatedLevelIds"].Values<string>()).ToArray();
        Require(ids.Length==100,"Expected the complete active campaign");
        var rows=new List<object>();long worst=0;int worstOrder=0;
        for(int n=0;n<ids.Length;n++)
        {
            string id=ids[n],root=Path.Combine(resources,"QuietCamp");
            var level=JsonConvert.DeserializeObject<LevelData>(File.ReadAllText(Path.Combine(root,id.StartsWith("gen:")?"GeneratedLevels/"+id.Replace(':','_')+".json":"Levels/"+id+".json")));
            string revision=Path.Combine(root,"ArtRevisions",level.contentHash+".json");
            string path=File.Exists(revision)?revision:Path.Combine(root,"ArtCompositions",id.Replace(':','_')+".json");
            var source=CampArtDefinition.ReadDocument(File.ReadAllText(path),out string profile);
            Require(source!=null&&profile==null,"Missing frozen art for "+id);
            string before=Digest(source);var optimized=CampGeometryBudget.Prepare(source);
            if(n==0)
            {
                var falselyMarked=source.EditingCopy();falselyMarked.mobileGeometryBudget=true;
                bool markerFailure=false;try{CampGeometryBudget.Prepare(falselyMarked);}catch(InvalidDataException){markerFailure=true;}
                Require(markerFailure,"A budget marker bypassed the actual geometry count");
                bool protectedFailure=false;
                try{CampGeometryBudget.Prepare(source,Array.Empty<string>());}catch(InvalidDataException){protectedFailure=true;}
                Require(protectedFailure,"A protected 500k grass population must fail, not silently bypass ownership or budget");
                var grid=source.landscape.meshes.First(m=>!m.water&&!m.decoration);
                var cut=JsonConvert.DeserializeObject<LandscapeMesh>(JsonConvert.SerializeObject(grid));cut.triangles=cut.triangles.Skip(3).ToArray();
                Require(ReferenceEquals(cut,CampGeometryBudget.SimplifyGrid(cut,4)),"A cut mesh was incorrectly filled as a rectangular grid");
                var duplicated=JsonConvert.DeserializeObject<LandscapeMesh>(JsonConvert.SerializeObject(grid));
                Array.Copy(duplicated.triangles,0,duplicated.triangles,3,3);
                Require(ReferenceEquals(duplicated,CampGeometryBudget.SimplifyGrid(duplicated,4)),"Invalid topology was simplified");
                var simple=CampGeometryBudget.SimplifyGrid(grid,4);
                Require(simple.triangles.Length<grid.triangles.Length,"Actual terrain was not simplified");
                Require(simple.vertices[0]==grid.vertices[0]&&simple.vertices[2]==grid.vertices[2]&&simple.vertices[^3]==grid.vertices[^3]&&simple.vertices[^1]==grid.vertices[^1],"Terrain extent/seams changed");
                Require(before==Digest(source),"A failed protected optimization mutated its input");
            }
            optimized.Validate(level);
            Require(before==Digest(source),"Source mutated by presentation optimization: "+id);
            Require(ReferenceEquals(optimized,CampGeometryBudget.Prepare(optimized)),"Budget pass must be idempotent");
            var model=source.models.ToDictionary(m=>m.id);
            var kept=optimized.instances.Select(i=>i.id).ToHashSet();
            Require(source.instances.Where(i=>i.modelId==null||!CampGeometryBudget.IsGrass(model[i.modelId])||i.id==source.primaryLandmarkInstanceId||i.id.StartsWith("overgrowth/")||!string.IsNullOrEmpty(i.motion)).All(i=>kept.Contains(i.id)),"Protected landmark, tree, flower or supported overgrowth removed");
            Require(optimized.landscape.heights.SequenceEqual(source.landscape.heights),"Height field/physical support changed");
            foreach(var water in source.landscape.meshes.Where(m=>m.water||m.decoration))Require(optimized.landscape.meshes.Contains(water),"Water/decorative topology changed");
            long authored=CampGeometryBudget.CountAuthored(optimized);
            var sparse=CampGeometryBudget.SparseCover(optimized,level.width,level.height,level.decorSeed);
            Require(sparse.triangles.Length/3<=CampGeometryBudget.ProceduralGrassTriangles,"Unbounded grass generation");
            if(sparse.triangles.Length>0)sparse.Validate();
            Require(Digest(sparse)==Digest(CampGeometryBudget.SparseCover(optimized,level.width,level.height,level.decorSeed)),"Sparse cover is not deterministic");
            if(optimized.atmosphere?.season=="winter")Require(sparse.triangles.Length==0,"Winter generated a second ground carpet");
            var gameplay=PrefabGeometry.Build(level,optimized,Path.Combine(output,"prefab-sources"));
            long gameplayTriangles=gameplay.Sum(g=>(long)((JArray)g["model"]["vertices"]).Count/9);
            long subtotal=authored+sparse.triangles.Length/3+gameplayTriangles;
            Require(subtotal<CampGeometryBudget.SceneTriangles,"Known geometry already exceeds 50k in "+id);
            if(subtotal>worst){worst=subtotal;worstOrder=n+1;}
            rows.Add(new{order=n+1,id,season=optimized.atmosphere?.season,sourceTriangles=CampGeometryBudget.CountAuthored(source),authoredTriangles=authored,
                authoredInstancesBefore=source.instances.Length,authoredInstancesAfter=optimized.instances.Length,proceduralGrassTriangles=sparse.triangles.Length/3,
                solvedGameplayTriangles=gameplayTriangles,knownGeometrySubtotal=subtotal,remainingForRuntimeAndUI=CampGeometryBudget.SceneTriangles-subtotal,
                sourceUnchanged=true,nativeSceneVerified=false});
            Console.WriteLine($"PASS {n+1:000} {id}: {CampGeometryBudget.CountAuthored(source)} -> {authored} authored; known subtotal {subtotal}; remaining {CampGeometryBudget.SceneTriangles-subtotal}");
        }
        var report=new{scope="Real shared C# budget policy, all 100 active frozen revisions, actual solved gameplay OBJ geometry. Subtotal excludes runtime river rebuild, UI, belongings, effects and other loaded worlds. Not a native scene census or device FPS measurement.",
            sceneLimit=CampGeometryBudget.SceneTriangles,levels=rows,worstKnownGeometrySubtotal=worst,worstOrder,nativeVisualAcceptance=false,playerBuild=false};
        File.WriteAllText(Path.Combine(output,"mobile-budget-audit.json"),JsonConvert.SerializeObject(report,Formatting.Indented)+"\n");
        Console.WriteLine("PASS 100/100 source-preservation, budget, ownership, determinism and solved-gameplay geometry checks. Native scene/FPS still unverified.");
    }
}
