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
        [Test] public void PublishedSceneryCannotOverrideTheOctoberTenthPlayableCamera()
        {
            var level=LevelLoader.Load("QC009");var art=CampArtDefinition.Load(level);
            var root=new UnityEngine.GameObject("Historical camera contract");
            try
            {
                var node=new UnityEngine.GameObject("Camera");node.transform.SetParent(root.transform,false);
                var camera=node.AddComponent<UnityEngine.Camera>();
                var view=node.AddComponent<CampArtView>();view.Configure(art,level,root.transform,AssetCatalog.Load());
                CameraFitter.Configure(camera);CameraFitter.Fit(camera,level,null);
                Assert.NotNull(camera.GetComponent<Kruty1918.GameplayViewport.GameplayViewport>().SafeAreaProvider,"Camera and HUD must share safe-area normalization after an orientation change.");
                var frame=camera.GetComponent<Kruty1918.GameplayViewport.GameplayViewport>().Frame;
                var oldBounds=new UnityEngine.Bounds(UnityEngine.Vector3.zero,new UnityEngine.Vector3(
                    level.width+2*(BoardMath.Overhang+.3f),.8f,level.height+2*(BoardMath.Overhang+.3f)));
                Assert.IsTrue(Kruty1918.GameplayViewport.GameplayCameraMath.TryOrthographic(oldBounds,
                    UnityEngine.Quaternion.Euler(58,225,0),frame.RenderAspect,frame.NormalizedGameplay,20,1,out var expected,.1f));
                Assert.That(UnityEngine.Vector3.Distance(camera.transform.position,expected.Position),Is.LessThan(.0001f));
                Assert.That(camera.orthographicSize,Is.EqualTo(expected.OrthographicSize).Within(.0001f));
                Assert.That(UnityEngine.Quaternion.Angle(camera.transform.rotation,UnityEngine.Quaternion.Euler(58,225,0)),Is.LessThan(.01f));
                CameraFitter.Fit(camera,level,null);
                Assert.That(UnityEngine.Vector3.Distance(camera.transform.position,expected.Position),Is.LessThan(.0001f),"A layout refresh must retain the restored pose.");
                var atmosphere=root.AddComponent<CampAtmosphere>();
                atmosphere.Configure(camera,level,null,AtmosphereCatalog.Load().Get("day"),()=>false,1,root.transform);
                Assert.NotNull(atmosphere.Soundscape,"Published seasonal levels must connect the ordinary camp sound reactions.");
                Assert.IsTrue(view.SeasonAtmosphere.HasReactiveSoundscape);
                Assert.AreEqual(10,UnityEngine.Shader.GetGlobalFloat("_AtmosWaveLen"));
                Assert.AreEqual(1.5f,UnityEngine.Shader.GetGlobalFloat("_AtmosWaveSpeed"));
                Assert.AreEqual(1,UnityEngine.Shader.GetGlobalFloat("_AtmosFlutterScale"));
                view.SeasonAtmosphere.Advance(.05f);
                Assert.AreEqual(view.SeasonAtmosphere.Wind.Strength,atmosphere.Wind.Strength);
                Assert.That(atmosphere.SampleWind(UnityEngine.Vector3.zero,1).Strength,Is.GreaterThan(0));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [TestCase(typeof(CampSceneHost),"Find")]
        [TestCase(typeof(MenuSceneHost),"ResolveScene")]
        public void SceneHostFindsItsOwnWorldWhenAnotherSceneIsActive(Type hostType,string lookup)
            => AssertSceneOwnership(hostType,lookup,false);
        [TestCase(typeof(CampSceneHost),"FindCamera")]
        [TestCase(typeof(MenuSceneHost),"FindCamera")]
        public void SceneHostFindsItsOwnCameraWhenAnotherSceneIsActive(Type hostType,string lookup)
            => AssertSceneOwnership(hostType,lookup,true);
        static void AssertSceneOwnership(Type hostType,string lookup,bool camera)
        {
            var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            const string folder="Assets/QuietCamp/Editor/LevelAuthoring/Workspaces";
            if(!UnityEditor.AssetDatabase.IsValidFolder(folder))UnityEditor.AssetDatabase.CreateFolder("Assets/QuietCamp/Editor/LevelAuthoring","Workspaces");
            string ownerPath=folder+"/scene-owner-"+Guid.NewGuid().ToString("N")+".unity",foreignPath=folder+"/scene-foreign-"+Guid.NewGuid().ToString("N")+".unity";
            UnityEngine.SceneManagement.Scene owner=default,foreign=default;
            UnityEngine.GameObject Add(string name,UnityEngine.SceneManagement.Scene scene)
            {var go=new UnityEngine.GameObject(name);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);return go;}
            try
            {
                Assert.IsTrue(UnityEditor.AssetDatabase.CopyAsset("Assets/QuietCamp/Editor/LevelAuthoring/WorkspaceTemplate.unity",ownerPath));
                Assert.IsTrue(UnityEditor.AssetDatabase.CopyAsset("Assets/QuietCamp/Editor/LevelAuthoring/WorkspaceTemplate.unity",foreignPath));
                owner=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ownerPath,UnityEditor.SceneManagement.OpenSceneMode.Additive);
                foreign=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(foreignPath,UnityEditor.SceneManagement.OpenSceneMode.Additive);
                var host=Add("Host under test",owner).AddComponent(hostType);
                var expected=Add(camera?"MainCamera":"DecorRoot",owner);
                var other=Add(camera?"MainCamera":"DecorRoot",foreign);
                if(camera){expected.AddComponent<UnityEngine.Camera>();other.AddComponent<UnityEngine.Camera>();other.tag="MainCamera";}
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(foreign);
                var method=hostType.GetMethod(lookup,System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var found=method.Invoke(method.IsStatic?null:host,camera?null:new object[]{"DecorRoot"});
                var actual=found is UnityEngine.Component component?component.gameObject:found as UnityEngine.GameObject;
                Assert.AreSame(expected,actual,"An unrelated active authoring scene must not own this host's world or camera.");
            }
            finally
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
                if(owner.IsValid()&&owner.isLoaded)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(owner,true);
                if(foreign.IsValid()&&foreign.isLoaded)UnityEditor.SceneManagement.EditorSceneManager.CloseScene(foreign,true);
                UnityEditor.AssetDatabase.DeleteAsset(ownerPath);UnityEditor.AssetDatabase.DeleteAsset(foreignPath);
            }
        }
        [Test] public void GameplayGridExistsBeforeFirstDragAndSurvivesPreviewCancellation()
        {
            var level=LevelLoader.Load("QC001");var art=CampArtDefinition.Load(level);
            var root=new UnityEngine.GameObject("Idle gameplay board contract");BoardRenderer board=null;
            UnityEngine.Transform Child(string name){var child=new UnityEngine.GameObject(name).transform;child.SetParent(root.transform,false);return child;}
            try
            {
                board=new BoardRenderer(level,AssetCatalog.Load(),Child("Base"),Child("Grid"),Child("Obstacle"),Child("Tents"),Child("Overlay"),art);
                var cells=root.GetComponentsInChildren<UnityEngine.MeshRenderer>().Where(r=>r.name.StartsWith("Cell_",StringComparison.Ordinal)).ToArray();
                Assert.AreEqual(level.width*level.height,cells.Length);
                Assert.IsTrue(cells.All(r=>r.sharedMaterial.GetFloat("_GridVisible")==1),"An empty playable board must be visible before the player drags anything.");
                var move=level.witness[0];var report=RuleEvaluator.Evaluate(level,new[]{move},false);
                board.ShowPlacementPreview(move,report);board.HidePlacementPreview();
                Assert.IsTrue(cells.All(r=>r.sharedMaterial.GetFloat("_GridVisible")==1),"Ending or cancelling a drag must not hide the field.");
                board.SetPlacementGridVisible(false);board.ShowPlacementPreview(move,report);board.HidePlacementPreview();
                Assert.IsTrue(cells.All(r=>r.sharedMaterial.GetFloat("_GridVisible")==0),"Preview cleanup must preserve a host's explicitly hidden grid.");
            }
            finally{board?.ClearAll();UnityEngine.Object.DestroyImmediate(root);}
        }
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
