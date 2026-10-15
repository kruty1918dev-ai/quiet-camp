using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using System.Linq;

namespace QuietCamp.Tests
{
    public sealed class CampArtCompositionTests
    {
        [Test] public void AuthoredViewKeepsThePublishedPuzzleAndWitness()
        {
            var level = LevelLoader.Load("QC007");
            string before = JsonConvert.SerializeObject(level);
            var art = CampArtDefinition.Load(level);
            Assert.NotNull(art);
            Assert.GreaterOrEqual(art.models.Length, 6);
            Assert.IsTrue(RuleEvaluator.Evaluate(level, level.witness).IsSolved);
            Assert.AreEqual(before, JsonConvert.SerializeObject(level));
        }
        [Test] public void DecorationCannotIntroduceABoardObstacle()
        {
            var level = LevelLoader.Load("QC007");
            var art = CampArtDefinition.Load(level);
            art.instances[0].x = 0; art.instances[0].z = 0;
            Assert.Throws<InvalidDataException>(() => art.Validate(level));
        }
        [Test] public void InvalidMeshCannotReachTheRenderer()
        {
            var level = LevelLoader.Load("QC007");
            var art = CampArtDefinition.Load(level);
            art.models[0].vertices[0] = float.NaN;
            Assert.Throws<InvalidDataException>(() => art.Validate(level));
        }
        [Test] public void AllCampaignLevelsHaveEditablePresentation()
            => Assert.NotNull(CampArtDefinition.Load(LevelLoader.Load("QC001")));

        [TestCase(4, 12, 73)]
        [TestCase(12, 4, 74)]
        [TestCase(6, 8, 12007)]
        [TestCase(10, 10, 12009)]
        public void OneProfileProducesSupportedReproducibleCompositions(int width, int depth, int seed)
        {
            var level = new LevelData { id = "generic-host", width = width, height = depth };
            var first = CampCompositionGenerator.Generate(level, "riparian-clearing", seed, out var plan);
            var again = CampCompositionGenerator.Generate(level, "riparian-clearing", seed, out _);
            Assert.IsTrue(plan.Ok);
            Assert.IsTrue(plan.review.passed);
            Assert.AreEqual(8, plan.attempts.Length);
            Assert.AreEqual(plan.attempts.Where(a => a.qualityPassed).Max(a => a.score), plan.review.score);
            Assert.AreEqual(JsonConvert.SerializeObject(first), JsonConvert.SerializeObject(again));
            Assert.GreaterOrEqual(first.instances.Count(i => i.id.StartsWith("woodland-frame/", StringComparison.Ordinal)), 12);
            Assert.AreEqual(1, first.instances.Count(i => i.id == "crossing/bridge"));
            Assert.IsTrue(first.instances.All(i => Math.Abs(i.x) >= width * .5f + .2f || Math.Abs(i.z) >= depth * .5f + .2f));
        }
        [Test] public void ChangingSeedChangesPlacementWithoutChangingThePuzzle()
        {
            var level = LevelLoader.Load("QC007"); var before = JsonConvert.SerializeObject(level);
            var first = CampCompositionGenerator.Generate(level, "riparian-clearing", 12007, out _);
            var second = CampCompositionGenerator.Generate(level, "riparian-clearing", 19, out _);
            Assert.AreNotEqual(JsonConvert.SerializeObject(first.instances), JsonConvert.SerializeObject(second.instances));
            Assert.AreEqual(before, JsonConvert.SerializeObject(level));
        }
        [Test] public void PublishedPresentationContainsFrozenGeometryAndSeason()
        {
            var text = UnityEngine.Resources.Load<UnityEngine.TextAsset>("QuietCamp/ArtCompositions/QC007").text;
            var document = Newtonsoft.Json.Linq.JObject.Parse(text);
            Assert.IsNotEmpty(document["models"].ToArray());
            Assert.IsNotEmpty(document["instances"].ToArray());
            Assert.AreEqual("QC007", (string)document["levelId"]);
            Assert.NotNull(document["atmosphere"]);
        }

        [TestCase(false), TestCase(true)] public void NativeWaterMasksLeaveTheChannelClearAndTheShoreVisible(bool frozen)
        {
            var level = LevelLoader.Load("QC007");
            var art = frozen ? CampArtDefinition.Load(level) : CampCompositionGenerator.Generate(level,"riparian-clearing",12007,out _);
            var root = new UnityEngine.GameObject("Water mask contract");
            try
            {
                // Match production: the camera can rotate independently of the
                // world parent. Putting both on root rotated the water plane too.
                var cameraNode=new UnityEngine.GameObject("Water test camera");cameraNode.transform.SetParent(root.transform,false);
                cameraNode.AddComponent<UnityEngine.Camera>();
                var view = cameraNode.AddComponent<QuietCamp.Presentation.World.CampArtView>();
                view.Configure(art, level, root.transform, AssetCatalog.Load());
                var cameraData=cameraNode.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                Assert.IsTrue(cameraData.requiresDepthTexture,"Frozen and procedural rivers both need their own camera depth request.");
                Assert.IsTrue(cameraData.requiresColorTexture,"Refraction must not depend on another camera having rendered first.");
                var water = root.GetComponentsInChildren<UnityEngine.MeshRenderer>().Single(r => r.sharedMaterial.shader.name == "Stylized Water 3/Standard");
                var mesh = water.GetComponent<UnityEngine.MeshFilter>().sharedMesh;
                Assert.AreEqual("Stylized Water 3/Standard", water.sharedMaterial.shader.name);
                Assert.Less(mesh.colors.Min(c => c.r), .001f, "The deep channel must not get bank foam.");
                if (!frozen) Assert.Greater(mesh.colors.Max(c => c.r), .05f, "The procedural bank needs a nonzero foam mask.");
                else
                {
                    var source=view.PresentedLandscape.meshes.Single(m=>m.water);
                    Assert.AreEqual(source.vertices.Length/3,mesh.vertexCount);
                    Assert.AreEqual(source.uv.Length,mesh.uv.Length*2);
                    Assert.AreEqual(source.flow.Length,mesh.vertexCount*3);
                    Assert.IsTrue(source.colors.SequenceEqual(mesh.colors.SelectMany(c=>new[]{c.r,c.g,c.b,c.a})),"Resolved river channels must survive rendering exactly.");
                    Assert.AreEqual(1,water.sharedMaterial.GetFloat("_VertexColorFoam"));
                    Assert.IsNotNull(view.RiverReflections,"A flat authored river needs actual scene reflections.");
                    Assert.IsFalse(water.sharedMaterial.IsKeywordEnabled("_RIVER"),"Native river mode suppresses planar reflections; curved UVs provide the current instead.");
                    Assert.AreEqual(0,water.sharedMaterial.GetFloat("_WorldSpaceUV"));
                    Assert.AreEqual(4,water.gameObject.layer,"Reflection cameras must exclude their own water surface.");
                }
                Assert.Less(mesh.colors.Min(c => c.g), .5f);
                Assert.Greater(mesh.colors.Max(c => c.g), frozen?.5f:.95f);
                Assert.IsTrue(mesh.normals.All(n => n.y > .999f));
                Assert.AreEqual(mesh.vertexCount, mesh.tangents.Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test] public void CommonRiverProfileResolvesEveryCampaignLandscapeWithoutChangingPuzzleOrSourceHeights()
        {
            var campaign=JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(UnityEngine.Resources.Load<UnityEngine.TextAsset>("QuietCamp/campaign").text);
            var profile=RiverEnvironmentProfile.Load();int count=0;
            foreach(string id in campaign["campaignLevelIds"].Values<string>())
            {
                var level=LevelLoader.Load(id);string puzzle=JsonConvert.SerializeObject(level);var art=CampArtDefinition.Load(level);
                var before=(float[])art.landscape.heights.Clone();
                var resolved=RiverLandscapeGeometry.Resolve(art,profile);resolved.Validate();
                if(!art.landscape.meshes.Any(m=>m.water))
                {
                    Assert.AreSame(art.landscape,resolved,id+": a dry forest must not acquire an invented river");count++;continue;
                }
                Assert.AreEqual(profile.version,resolved.riverEnvironmentVersion,id);
                CollectionAssert.AreEqual(before,art.landscape.heights,id+": source geometry mutated");
                Assert.AreEqual(puzzle,JsonConvert.SerializeObject(level),id+": rules changed");
                for(float z=-level.height*.5f;z<=level.height*.5f;z+=.5f)for(float x=-level.width*.5f;x<=level.width*.5f;x+=.5f)
                    Assert.That(resolved.SampleHeight(x,z),Is.EqualTo(art.landscape.SampleHeight(x,z)).Within(.00001f),id+": field changed");
                var frozen=art.EditingCopy();frozen.landscape=resolved;
                Assert.AreSame(resolved,RiverLandscapeGeometry.Resolve(frozen,profile),id+": double deformation");
                foreach(var water in resolved.meshes.Where(m=>m.water))
                {
                    var ys=water.vertices.Where((_,i)=>i%3==1).ToArray();
                    Assert.LessOrEqual(ys.Max()-ys.Min(),.08f,id+": non-planar water requires its native river fallback");
                    Assert.IsTrue(water.flow.All(v=>v>=0&&v<=1),id);
                    Assert.IsTrue(water.normals.Where((_,i)=>i%3==1).All(v=>v>.999f),id);
                    for(int i=0;i<water.vertices.Length/3;i++)
                    {
                        var tangent=new UnityEngine.Vector3(water.tangents[i*4],0,water.tangents[i*4+2]);
                        var uvAlong=new UnityEngine.Vector3(1-water.flow[i*3]*2,0,1-water.flow[i*3+1]*2).normalized;
                        Assert.Greater(UnityEngine.Vector3.Dot(UnityEngine.Vector3.Cross(UnityEngine.Vector3.up,tangent)*water.tangents[i*4+3],uvAlong),.99f,id+": tangent must follow negative-distance UV, not invert the normal map");
                    }
                }
                count++;
            }
            Assert.AreEqual(100,count);
        }
        [Test] public void FlowTurnsWithTheActualChannelAndStopsOutsideItsEndCaps()
        {
            var water=new LandscapeMesh{water=true,vertices=new[]{-1f,0,0,1,0,0,-1,0,4,1,0,4,3,0,8,5,0,8},uv=new[]{0f,0,1,0,0,4,1,4,0,8,1,8}};
            var field=new RiverSurfaceField(new FrozenLandscape{meshes=new[]{water}});
            Assert.IsTrue(field.TrySample(0,2,out var straight));Assert.IsTrue(straight.Wet);
            Assert.That(straight.direction.x,Is.EqualTo(0).Within(.001f));
            Assert.IsTrue(field.TrySample(2,6,out var bend));Assert.IsTrue(bend.Wet);Assert.Greater(bend.direction.x,.5f);
            Assert.Less(field.Velocity(.95f,2,1).magnitude,field.Velocity(0,2,1).magnitude);
            Assert.IsFalse(field.TrySample(0,-2,out _));
            Assert.Throws<InvalidDataException>(()=>new RiverEnvironmentProfile{widthVariation=float.NaN}.Validate());
            var obstacle=new RiverSurfaceField(new FrozenLandscape{meshes=new[]{water},riverContacts=new[]{0f,0,2}});
            var diverted=obstacle.Velocity(0,1.5f,1);
            Assert.Greater(Math.Abs(diverted.x),.1f,"A head-on approach must slide around the rock.");
            Assert.Greater(diverted.y,0,"The obstacle must not reverse the current.");
        }
        [Test] public void BankEcologyIsBoundedRepeatableAndOutsideThePlayableField()
        {
            var art=CampArtDefinition.Load(LevelLoader.Load("QC007"));var profile=RiverEnvironmentProfile.Load();
            var land=RiverLandscapeGeometry.Resolve(art,profile);
            var first=RiverBankEcology.Generate(art,land,profile);
            var again=RiverBankEcology.Generate(art,land,profile);
            Assert.Greater(first.Length,0);Assert.LessOrEqual(first.Length,profile.maxBankPlants);
            Assert.AreEqual(JsonConvert.SerializeObject(first),JsonConvert.SerializeObject(again));
            var field=new RiverSurfaceField(land);
            foreach(var plant in first)
            {
                Assert.IsTrue(Math.Abs(plant.x)>=art.width*.5f+1||Math.Abs(plant.z)>=art.height*.5f+1);
                Assert.IsTrue(field.TrySample(plant.x,plant.z,out var bank));Assert.Greater(bank.signedDistance,0);
                Assert.That(plant.y,Is.EqualTo(land.SampleHeight(plant.x,plant.z)+plant.surfaceOffset).Within(.00001f));
                Assert.IsTrue(art.models.Any(m=>m.id==plant.modelId),"Use an existing model rather than a hidden asset dependency.");
            }
        }
        [Test] public void WaterBodyAbsorbsEnoughLightToRemainVisibleWhileTheLipStaysShallow()
        {
            var source=UnityEngine.Resources.Load<UnityEngine.Material>("QuietCamp/ArtCompositions/QC007Water");
            var water=new UnityEngine.Material(source);
            try
            {
                var profile=RiverEnvironmentProfile.Load();RiverWeatherMaterial.NaturalChannel(water,profile);
                float extinction=water.GetFloat("_DepthHorizontal");
                double deepDensity=1-Math.Exp(-profile.channelDepth*extinction);
                double shallowDensity=1-Math.Exp(-profile.shallowDepth*extinction);
                Assert.Greater(deepDensity,.75,"The body must not disappear into the refracted bed.");
                Assert.Less(shallowDensity,.4,"The shallow lip must keep a visible bed.");
                Assert.Greater(water.GetFloat("_ReflectionStrength"),.5f);
                Assert.Less(water.GetFloat("_ReflectionFresnel"),3,"The high gameplay camera needs readable reflection, not only grazing-angle reflection.");
            }
            finally{UnityEngine.Object.DestroyImmediate(water);}
        }
    }
}
