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
        var groundPattern=MobileMeadowPattern.Ground();
        Require(groundPattern.SequenceEqual(MobileMeadowPattern.Ground()),"Ground detail is not deterministic");
        Require(groundPattern.Length==256*256*4,"Unexpected meadow texture allocation");
        double groundCoverage=Enumerable.Range(0,256*256).Count(i=>groundPattern[i*4]>55)/(double)(256*256);
        Require(groundCoverage>.90,"Dense ground lost its blade coverage");
        File.WriteAllBytes(Path.Combine(output,"meadow-ground.rgba"),groundPattern);
        Console.WriteLine($"PASS ground detail: {groundCoverage:P1} coverage; grass cards removed, opaque individual leaves.");
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
            string before=Digest(source);var optimized=CampGeometryBudget.DenseMeadow(CampGeometryBudget.Prepare(source));
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
            foreach(var margin in source.landscape.meshes.Where(m=>m.name=="Surrounding ground margins"))
            {
                var reduced=CampGeometryBudget.SimplifyMargins(margin,source.landscape.resolution,4);
                Require(reduced.triangles.Length<margin.triangles.Length,"Verified terrain margins were not simplified");
                var cut=JsonConvert.DeserializeObject<LandscapeMesh>(JsonConvert.SerializeObject(margin));cut.triangles=cut.triangles.Skip(3).ToArray();
                Require(ReferenceEquals(cut,CampGeometryBudget.SimplifyMargins(cut,source.landscape.resolution,4)),"Cut margin topology was filled");
                var bounds=CampMeadowTufts.GroundBounds(optimized);
                Require(bounds.minX<=optimized.landscape.originX&&bounds.maxX>=optimized.landscape.originX+optimized.landscape.size,"Margin coverage clipped the authored terrain");
            }
            Require(before==Digest(source),"Source mutated by presentation optimization: "+id);
            Require(ReferenceEquals(optimized,CampGeometryBudget.Prepare(optimized)),"Budget pass must be idempotent");
            Require(ReferenceEquals(optimized,CampGeometryBudget.DenseMeadow(optimized)),"Dense meadow stage must be idempotent");
            var model=source.models.ToDictionary(m=>m.id);
            var kept=optimized.instances.Select(i=>i.id).ToHashSet();
            Require(source.instances.Where(i=>i.modelId==null||!CampGeometryBudget.IsGrass(model[i.modelId])||i.id==source.primaryLandmarkInstanceId||i.id.StartsWith("overgrowth/")||!string.IsNullOrEmpty(i.motion)).All(i=>kept.Contains(i.id)),"Protected landmark, tree, flower or supported overgrowth removed");
            Require(optimized.landscape.heights.SequenceEqual(source.landscape.heights),"Height field/physical support changed");
            foreach(var water in source.landscape.meshes.Where(m=>m.water||m.decoration))Require(optimized.landscape.meshes.Contains(water),"Water/decorative topology changed");
            long authored=CampGeometryBudget.CountAuthored(optimized);
            var sparse=CampMeadowTufts.Build(optimized,level.width,level.height,level.decorSeed);
            var surface=new CampTerrainSurface(optimized.landscape);
            double maximumSurfaceAdjustment=0;
            foreach(var item in optimized.instances.Where(i=>i.surfaceAttached==true))
            {
                var p=surface.Position(optimized,item);
                Require(float.IsFinite(p.y),"Nonfinite displayed support in "+id+" / "+item.id);
                Require(Math.Abs(p.y-item.y-(surface.Sample(item.x,item.z)-optimized.landscape.SampleHeight(item.x,item.z)))<.0001,"Attached offset was lost");
                maximumSurfaceAdjustment=Math.Max(maximumSurfaceAdjustment,Math.Abs(p.y-item.y));
            }
            var contact=new CampGroundContactField(surface.Bounds,128,surface.Sample);
            var contactModels=optimized.models.ToDictionary(m=>m.id);
            foreach(var item in optimized.instances)
            {
                if(item.modelId==null||!contactModels.TryGetValue(item.modelId,out var contactModel)||contactModel.shading=="foliage"&&contactModel.seasonalRole!="deciduous"&&contactModel.seasonalRole!="conifer"||contactModel.seasonalRole=="groundcover"||!string.IsNullOrEmpty(item.motion))continue;
                var rotation=item.rotationEuler!=null?UnityEngine.Quaternion.Euler(CampArtDefinition.Vector(item.rotationEuler)):UnityEngine.Quaternion.Euler(0,item.yaw,0);
                var scale=item.scaleXYZ!=null?CampArtDefinition.Vector(item.scaleXYZ):UnityEngine.Vector3.one*item.scale;
                var matrix=UnityEngine.Matrix4x4.TRS(surface.Position(optimized,item),rotation,scale);
                UnityEngine.Vector3 Point(int i)=>matrix.MultiplyPoint3x4(new UnityEngine.Vector3(contactModel.vertices[i],contactModel.vertices[i+1],contactModel.vertices[i+2]));
                for(int i=0;i+8<contactModel.vertices.Length;i+=9)contact.Triangle(Point(i),Point(i+3),Point(i+6));
            }
            int contactTexels=contact.Pixels().Count(c=>c.r>0);
            Require(contactTexels>0,"No physical scenery contact was found in "+id);
            Require(before==Digest(source),"Ground presentation mutated canonical source "+id);
            Require(sparse.triangles.Length/3<=CampGeometryBudget.ProceduralGrassTriangles,"Unbounded grass generation");
            if(sparse.triangles.Length>0)sparse.Validate();
            Require(Digest(sparse)==Digest(CampMeadowTufts.Build(optimized,level.width,level.height,level.decorSeed)),"Meadow tufts are not deterministic");
            Require(sparse.vertices.Length==sparse.triangles.Length*3,"Grass must contain individual triangular leaves, not card quads");
            for(int i=0;i<sparse.vertices.Length;i+=9)
            {
                float dx=sparse.vertices[i]-sparse.vertices[i+3],dz=sparse.vertices[i+2]-sparse.vertices[i+5];
                Require(dx*dx+dz*dz<.011,"Grass leaf became an oversized strip");
                Require(sparse.vertices[i+7]-Math.Max(sparse.vertices[i+1],sparse.vertices[i+4])>.025,"Grass leaf is flat or buried in its supporting terrain");
            }
            if(optimized.atmosphere?.season=="winter")Require(sparse.triangles.Length==0,"Winter generated a second ground carpet");
            var gameplay=PrefabGeometry.Build(level,optimized,Path.Combine(output,"prefab-sources"));
            long gameplayTriangles=gameplay.Sum(g=>(long)((JArray)g["model"]["vertices"]).Count/9);
            long subtotal=authored+sparse.triangles.Length/3+gameplayTriangles;
            Require(subtotal<CampGeometryBudget.SceneTriangles,"Known geometry already exceeds 50k in "+id);
            if(subtotal>worst){worst=subtotal;worstOrder=n+1;}
            rows.Add(new{order=n+1,id,season=optimized.atmosphere?.season,sourceTriangles=CampGeometryBudget.CountAuthored(source),authoredTriangles=authored,
                authoredInstancesBefore=source.instances.Length,authoredInstancesAfter=optimized.instances.Length,proceduralGrassTriangles=sparse.triangles.Length/3,
                solvedGameplayTriangles=gameplayTriangles,knownGeometrySubtotal=subtotal,remainingForRuntimeAndUI=CampGeometryBudget.SceneTriangles-subtotal,
                sourceUnchanged=true,groundContactTexelsAt128=contactTexels,maximumSurfaceAdjustment,nativeSceneVerified=false});
            Console.WriteLine($"PASS {n+1:000} {id}: {CampGeometryBudget.CountAuthored(source)} -> {authored} authored; known subtotal {subtotal}; remaining {CampGeometryBudget.SceneTriangles-subtotal}");
        }
        var report=new{scope="Real shared C# budget policy, all 100 active frozen revisions, actual solved gameplay OBJ geometry. Subtotal excludes runtime river rebuild, UI, belongings, effects and other loaded worlds. Not a native scene census or device FPS measurement.",
            sceneLimit=CampGeometryBudget.SceneTriangles,levels=rows,worstKnownGeometrySubtotal=worst,worstOrder,
            meadowAppearance="opaque packed ground detail + compact individual geometric grass leaves",groundCoverage,
            textureBytesIncludingMips=groundPattern.Length*4/3,nativeVisualAcceptance=false,playerBuild=false};
        File.WriteAllText(Path.Combine(output,"mobile-budget-audit.json"),JsonConvert.SerializeObject(report,Formatting.Indented)+"\n");
        Console.WriteLine("PASS 100/100 source-preservation, budget, ownership, determinism and solved-gameplay geometry checks. Native scene/FPS still unverified.");
    }
}
