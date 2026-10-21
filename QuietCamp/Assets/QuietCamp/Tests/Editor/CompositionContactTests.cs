using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Editor.LevelAuthoring.WorldOps;
using UnityEngine;
namespace QuietCamp.Tests.Editor
{
    // These tests use real committed geometry with Unity's numeric types.
    // Portable equivalents run without Unity; these native tests remain pending.
    public sealed class CompositionContactTests
    {
        static CampArtDefinition Art(string key)=>JsonConvert.DeserializeObject<CampArtDefinition>(File.ReadAllText("Assets/QuietCamp/Resources/QuietCamp/ArtCompositions/"+key+".json"));
        [TestCase("QC003",1)] [TestCase("gen_qc_camp_122",2)] [TestCase("QC009",1)] [TestCase("gen_qc_camp_82",1)] [TestCase("gen_qc_camp_99",1)]
        public void RealWellRimsProduceStableCavitiesWithoutSourceMutation(string key,int count)
        {
            var art=Art(key);string source=JsonConvert.SerializeObject(art);
            var openings=WorldWellShafts.Find(art);Assert.AreEqual(count,openings.Length);
            var landscape=WorldWellShafts.Resolve(art,art.landscape);landscape.Validate();
            Assert.AreEqual(source,JsonConvert.SerializeObject(art));
            Assert.AreEqual(JsonConvert.SerializeObject(landscape),JsonConvert.SerializeObject(WorldWellShafts.Resolve(art,landscape)));
            Assert.AreEqual(count,landscape.meshes.Count(m=>m.name.StartsWith("Well shaft — ",StringComparison.Ordinal)));
            foreach(var hole in openings)
            {
                var centre=hole.rim.Aggregate(Vector3.zero,(a,b)=>a+b)/hole.rim.Length;
                Assert.IsTrue(WorldWellShafts.ExcludesGroundPlant(art,centre.x,art.landscape.SampleHeight(centre.x,centre.z),centre.z));
                Assert.IsFalse(WorldWellShafts.ExcludesGroundPlant(art,centre.x,hole.rim.Max(v=>v.y),centre.z),"Keep object-supported overgrowth at the rim.");
            }
        }
        [TestCase("gen_qc_camp_29")] [TestCase("gen_qc_camp_31")] [TestCase("gen_qc_camp_75")] [TestCase("gen_qc_camp_89")] [TestCase("gen_qc_camp_110")]
        public void RealBridgeCrossesCurrentAndReachesBothBanks(string key)
        {
            var art=Art(key);WorldOpsCompositionRepairs.BridgeCrossings(art);string once=JsonConvert.SerializeObject(art);
            WorldOpsCompositionRepairs.BridgeCrossings(art);Assert.AreEqual(once,JsonConvert.SerializeObject(art));
            var models=art.models.ToDictionary(m=>m.id);var water=new RiverSurfaceField(art.landscape);
            var p=art.instances.Single(i=>i.modelId!=null&&models.TryGetValue(i.modelId,out var m)&&WorldOpsCompositionRepairs.IsBridge(m.sourceModelId??m.id));
            Assert.IsTrue(water.TrySample(p.x,p.z,out var current));var axis=Quaternion.Euler(0,p.yaw,0)*Vector3.forward;
            Assert.Less(Mathf.Abs(axis.x*current.direction.x+axis.z*current.direction.y),.02f);
            var v=models[p.modelId].vertices;float[] z=Enumerable.Range(0,v.Length/3).Select(i=>v[i*3+2]).ToArray();
            foreach(float distance in new[]{z.Min(),z.Max()})
            {var at=new Vector3(p.x,p.y,p.z)+axis*distance*(p.scaleXYZ?[2]??p.scale);Assert.IsFalse(water.TrySample(at.x,at.z,out var bank)&&bank.Wet);}
            Assert.IsTrue(art.instances.Any(i=>i.id=="worldops/bridge-approach/"+p.id));
        }
        [TestCase("well.opening")] [TestCase("bridge.crossing")] [TestCase("shade.support")]
        public void RepairsUseTheExistingReviewGate(string kind)
        {
            Assert.Contains(kind,WorldOpsOperations.Kinds);
            var recipe=new WorldRecipe{id="contact-check",capture=false,autoApply=false,operations=new[]{new WorldOperation{id="fix",kind=kind}}};
            Assert.Throws<ArgumentException>(()=>WorldOpsOperations.Validate(recipe));
        }
    }
}
