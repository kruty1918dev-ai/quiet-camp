using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Application;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;

namespace QuietCamp.Tests
{
    public class MenuBackdropTests
    {
        [Test] public void AuthoredMenuBoardUsesOnlyGridOverTheSameNaturalLandscapeAsGameplay()
        {
            var level=LevelLoader.Load("QC009");var art=CampArtDefinition.Load(level);
            var root=new UnityEngine.GameObject("Authored menu contract");BoardRenderer board=null;
            try
            {
                Assert.NotNull(art);board=MenuDiorama.Build(root.transform,AssetCatalog.Load(),level,art);
                var cells=root.GetComponentsInChildren<UnityEngine.MeshRenderer>().Where(r=>r.name.StartsWith("Cell_",StringComparison.Ordinal)).ToArray();
                Assert.AreEqual(level.width*level.height,cells.Length);
                Assert.IsTrue(cells.All(r=>r.sharedMaterial.shader.name=="QuietCamp/PlacementGrid"),"Cells provide grid lines and picking, never a different ground surface.");
                Assert.IsTrue(cells.All(r=>r.sharedMaterial.GetFloat("_GridVisible")==0),"Menu scenery does not show placement guides.");
                Assert.IsFalse(root.GetComponentsInChildren<UnityEngine.Transform>().Any(t=>t.name=="BaseSlab"||t.name=="BaseSide"));
                board.SurfaceDetails.Advance(120,.5f,.5f);Assert.AreEqual(0,board.SurfaceDetails.Residents);Assert.AreEqual(0,board.SurfaceDetails.Surface.Steps);
                Assert.IsTrue(root.GetComponentsInChildren<UnityEngine.Collider>(true).All(c=>!c.enabled),"The menu must not take gameplay input.");
            }
            finally{board?.ClearAll();UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void CampSceneRotationsAreNormalizedBeforeHostsInitialize()
        {
            var scene = System.IO.File.ReadAllText("Assets/QuietCamp/Scenes/Camp.unity");
            var rotations = System.Text.RegularExpressions.Regex.Matches(scene,
                @"m_LocalRotation: \{x: ([^,]+), y: ([^,]+), z: ([^,]+), w: ([^}]+)\}");
            Assert.Greater(rotations.Count, 0);
            foreach (System.Text.RegularExpressions.Match rotation in rotations)
            {
                var values = Enumerable.Range(1, 4).Select(index => double.Parse(rotation.Groups[index].Value,
                    System.Globalization.CultureInfo.InvariantCulture));
                Assert.That(values.Sum(value => value * value), Is.EqualTo(1d).Within(.000001d), rotation.Value);
            }
        }
        [Test] public void EveryGladeCanBeChosenAndThePreviousLaunchIsExcluded()
        {
            var levels = CampContent.Summaries;
            Assert.AreEqual(100, levels.Count);
            foreach (var previous in levels)
            {
                var restored = JsonConvert.DeserializeObject<SettingsSaveData>(JsonConvert.SerializeObject(new SettingsSaveData { lastMenuBackdropId = previous.id }));
                var choices = Enumerable.Range(0, levels.Count - 1).Select(ticket => MenuDiorama.SelectId(levels, restored.lastMenuBackdropId, ticket)).ToArray();
                Assert.IsFalse(choices.Contains(previous.id));
                CollectionAssert.AreEquivalent(levels.Where(l => l.id != previous.id).Select(l => l.id).ToArray(), choices);
            }
            Assert.AreNotEqual("gen:qc_camp:1", MenuDiorama.SelectId(levels, "GEN1", 10));
            Assert.NotNull(MenuDiorama.SelectId(levels, null, int.MinValue));
            Assert.IsNull(JsonConvert.DeserializeObject<SettingsSaveData>("{}").lastMenuBackdropId);
            Assert.AreEqual("only", MenuDiorama.SelectId(new[] { new LevelSummary { id = "only" } }, "only", 7));
            Assert.Throws<InvalidOperationException>(() => MenuDiorama.SelectId(Array.Empty<LevelSummary>(), null, 0));
        }
        [Test] public void DecorativeTentsFitAll30LevelsWithoutUsingSolutionsOrClosingRoutes()
        {
            foreach (var summary in CampContent.Summaries)
            {
                var level = LevelLoader.Load(summary.id);
                level.witness = Array.Empty<Placement>();
                var before = JsonConvert.SerializeObject(level);
                var tents = MenuDiorama.DecorativePlacements(level);
                Assert.That(tents.Length, Is.InRange(1, 2), summary.id);
                var report = RuleEvaluator.Evaluate(level, tents, false);
                Assert.IsTrue(report.CanCommit, summary.id);
                Assert.IsFalse(report.Issues.Any(i => i.Code == "path"), summary.id);
                Assert.AreEqual(before, JsonConvert.SerializeObject(level), summary.id);
            }
        }
    }
}
