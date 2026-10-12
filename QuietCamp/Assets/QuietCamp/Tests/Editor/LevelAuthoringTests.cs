using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class LevelAuthoringTests
    {
        [TearDown] public void StopPaintTool() => PlacementToolIntegration.Adapter?.Stop();
        [TestCase(false)] [TestCase(true)]
        public void BothStartersKeepPublishedContentUntouched(bool generated)
        {
            var original = JsonConvert.SerializeObject(LevelLoader.Load("QC007"));
            var document = LevelAuthoringDocument.Create(generated);
            Assert.IsEmpty(document.Validate(false));
            Assert.IsNull(document.art.generation);
            Assert.AreEqual(original, JsonConvert.SerializeObject(LevelLoader.Load("QC007")));
        }
        [Test] public void ObjectsOwnOccupancyNoiseAndShade()
        {
            var d = LevelAuthoringDocument.Create(false); var cell = new Cell(2, 2);
            d.PlaceObject(cell, "campfire_stones");
            Assert.IsTrue(RuleEvaluator.IsNoisy(d.level, new Cell(4,2)));
            Assert.IsFalse(RuleEvaluator.IsNoisy(d.level, new Cell(5,2)));
            d.RemoveObject(cell); Assert.IsEmpty(d.level.blocked); Assert.IsEmpty(d.level.noise);
            d.PlaceObject(cell, "tree_default"); d.level.canopies = new[] { new ShadeCanopyData { x = 2, z = 2, radiusX = 2, radiusZ = 2 } };
            d.ReconcileObjects(); CollectionAssert.AreEquivalent(ShadeProjection.Cells(d.level), d.level.shade);
            d.RemoveObject(cell); Assert.IsEmpty(d.level.canopies); Assert.IsEmpty(d.level.shade);
            Assert.IsEmpty(d.Validate(false));
        }
        [Test] public void DraftAndExportPreserveFrozenTransforms()
        {
            string path = Path.Combine(Path.GetTempPath(), "qc-authoring-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var d = LevelAuthoringDocument.Create(true);
                d.art.instances[0].rotationEuler = new[] { 2f, 35f, 3f };
                d.art.instances[0].scaleXYZ = new[] { .8f, 1.1f, .9f };
                d.Export(path);
                var opened = LevelAuthoringDocument.Open(Path.Combine(path, d.level.id + ".draft.json"));
                CollectionAssert.AreEqual(d.art.instances[0].scaleXYZ, opened.art.instances[0].scaleXYZ);
                CollectionAssert.AreEqual(d.art.instances[0].rotationEuler, opened.art.instances[0].rotationEuler);
                Assert.IsEmpty(opened.Validate(true));
                Assert.IsNull(opened.art.generation);
                Assert.AreEqual(JsonConvert.SerializeObject(d.art), File.ReadAllText(Path.Combine(path, d.level.id + ".art.json")));
                Assert.AreEqual(64, opened.level.contentHash.Length);
                string firstHash=opened.level.contentHash;opened.Export(path);
                Assert.AreEqual(firstHash,LevelAuthoringDocument.Open(Path.Combine(path,d.level.id+".draft.json")).level.contentHash);
            }
            finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
        }
        [Test] public void UnsolvedDraftCannotBeExported()
        {
            var d = LevelAuthoringDocument.Create(false);
            Assert.IsNotEmpty(d.Validate(true));
            Assert.Throws<InvalidDataException>(() => d.Export(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        }
        [Test] public void ResizePrunesObjectsAndIncompleteFootprints()
        {
            var d = LevelAuthoringDocument.Create(false); d.PlaceObject(new Cell(5,5), "stone_largeA");
            d.PutTent(d.level.guests[0].id, new Cell(4,4), 0); d.Resize(4,4);
            Assert.IsEmpty(d.level.objects); Assert.IsEmpty(d.level.blocked); Assert.IsEmpty(d.level.witness);
            Assert.IsEmpty(d.Validate(false));
            Assert.Throws<ArgumentOutOfRangeException>(() => d.Resize(3,4));
        }
        [Test] public void DocumentUndoRestoresGuestAndBoardEdits()
        {
            var state = ScriptableObject.CreateInstance<LevelAuthoringState>();
            try
            {
                state.Write(LevelAuthoringDocument.Create(false)); Undo.RecordObject(state, "Authoring contract");
                var changed = state.Read(); changed.Resize(4,8); changed.level.guests[0].quiet = true; state.Write(changed); Undo.FlushUndoRecordObjects();
                Undo.PerformUndo(); Assert.AreEqual(6,state.Read().level.width); Assert.IsFalse(state.Read().level.guests[0].quiet);
                Undo.PerformRedo(); Assert.AreEqual(4,state.Read().level.width); Assert.IsTrue(state.Read().level.guests[0].quiet);
            }
            finally { Undo.ClearUndo(state); UnityEngine.Object.DestroyImmediate(state); }
        }
        [Test] public void PreviewSaveDoesNotReadOrWriteThePlayerSlot()
        {
            var save = EditorAuthoringPreview.CreateSave(); Assert.IsTrue(save.MemoryOnly); Assert.IsFalse(save.HasSave);
            Assert.IsTrue(save.Load(out var error)); Assert.IsNull(error);
            save.Progress.lastLevelId = "QC_EDITOR_contract"; Assert.IsTrue(save.Save()); Assert.IsTrue(save.Progress.tutorial.existingPlayer);
        }
        [Test] public void NativeWorkspaceRoundTripKeepsPlantScaleAndTransforms()
        {
            var document = LevelAuthoringDocument.Create(false);
            document.art.instances = new[] { new CampArtDefinition.Instance { modelId = "cover_blades", x = 5, z = 5, scale = .7f, rotationEuler = new[] { 3f, 21f, 2f }, scaleXYZ = new[] { .7f,.9f,.8f } } };
            document.art.models = new[] { LevelAuthoringPalette.Get("cover_blades").model };
            using (var first = new LevelAuthoringWorkspace(document))
            {
                var captured = first.Capture();
                CollectionAssert.AreEqual(document.art.instances[0].scaleXYZ, captured.art.instances[0].scaleXYZ);
                Assert.AreEqual(1,first.SceneryCount);
                using (var second = new LevelAuthoringWorkspace(captured))
                {
                    var reopened=second.Capture();
                    CollectionAssert.AreEqual(captured.art.instances[0].scaleXYZ,reopened.art.instances[0].scaleXYZ);
                    CollectionAssert.AreEqual(captured.art.instances[0].rotationEuler,reopened.art.instances[0].rotationEuler);
                    Assert.AreEqual(captured.art.instances[0].id,reopened.art.instances[0].id);
                }
            }
        }
        [Test] public void ReadyPaletteContainsSunflowerFenceAndPlants()
        {
            var entries = LevelAuthoringPalette.Entries;
            Assert.IsTrue(entries.Any(e => e.id.Contains("sunflower")));
            Assert.IsTrue(entries.Any(e => e.category == "Паркани"));
            Assert.IsTrue(entries.Any(e => e.smallPlant));
            Assert.AreEqual(entries.Length, entries.Select(e=>e.id).Distinct().Count());
            foreach (var e in entries) Assert.NotNull(e.prefab.GetComponent<LevelAuthoringAsset>(),e.id);
        }
        [Test] public void InstalledPwbAdapterActivatesAllTools()
        {
            Assert.NotNull(PlacementToolIntegration.Adapter,"Install the owner's PWB 4.12.0 package before this integration check.");
            using (var workspace = new LevelAuthoringWorkspace(LevelAuthoringDocument.Create(false)))
            {
                foreach (PlacementTool tool in Enum.GetValues(typeof(PlacementTool)))
                    PlacementToolIntegration.Adapter.Activate(tool,LevelAuthoringPalette.Get("cover_blades").prefab,workspace.Scenery,workspace.Surface,1,65,.2f,.15f);
                PlacementToolIntegration.Adapter.Stop();
            }
        }
        [Test] public void TenThousandPlantsRemainEditableAndUseBatches()
        {
            var d = LevelAuthoringDocument.Create(false);
            var watch = System.Diagnostics.Stopwatch.StartNew();
            using (var workspace = new LevelAuthoringWorkspace(d))
            {
                for (int i=0;i<10000;i++) workspace.Spawn(new CampArtDefinition.Instance { modelId="cover_blades",x=5+(i%100)*.035f,z=-2+(i/100)*.035f,scale=1 },false);
                watch.Restart(); workspace.RefreshPlantBatches();
                var batchMs = watch.ElapsedMilliseconds;
                Assert.AreEqual(10000, workspace.SceneryCount);
                Assert.LessOrEqual(workspace.Root.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled),100);
                watch.Restart(); var captured = workspace.Capture(); var captureMs=watch.ElapsedMilliseconds;
                Assert.AreEqual(10000,captured.art.instances.Length); Assert.IsEmpty(captured.Validate(false));
                string metrics=JsonConvert.SerializeObject(new { plants=10000,batchMs,captureMs,editable=true });
                Directory.CreateDirectory("Temp/ai"); File.WriteAllText("Temp/ai/level-authoring-performance.json",metrics);
                TestContext.WriteLine(metrics);
                Assert.Less(batchMs,5000,"A full foliage rebuild should finish in under five seconds on this editor host.");
            }
        }
        [Test] public void PwbPaintEraseAndMovementUseNativeUndo()
        {
            Assert.NotNull(PlacementToolIntegration.Adapter);
            var assembly = PlacementToolIntegration.Adapter.GetType().Assembly;
            var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
            var io = assembly.GetType("PluginMaster.PWBIO",true);
            var strokeType = io.GetNestedType("PaintStrokeItem",System.Reflection.BindingFlags.NonPublic);
            var stroke = (System.Collections.IList)io.GetField("_paintStroke",flags).GetValue(null);
            using (var w = new LevelAuthoringWorkspace(LevelAuthoringDocument.Create(false)))
            {
                var prefab = LevelAuthoringPalette.Get("cover_blades").prefab;
                PlacementToolIntegration.Adapter.Activate(PlacementTool.Paint,prefab,w.Scenery,w.Surface,1,65,.2f,.15f);
                Undo.IncrementCurrentGroup(); int group=Undo.GetCurrentGroup();
                stroke.Clear();
                for(int i=0;i<3;i++) stroke.Add(Activator.CreateInstance(strokeType,new object[] { prefab,AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)),new Vector3(5+i*.2f,0,5),Quaternion.identity,Vector3.one,0,w.Scenery,w.Surface.transform,false,false,-1 }));
                var manager=assembly.GetType("PluginMaster.BrushManager",true);
                var settings=manager.GetProperty("settings",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.FlattenHierarchy).GetValue(null);
                io.GetMethod("Paint",flags).Invoke(null,new[] {settings,"Paint contract",(object)false,false,""});
                Undo.CollapseUndoOperations(group); Assert.AreEqual(3,w.SceneryCount);
                Undo.PerformUndo(); Assert.AreEqual(0,w.SceneryCount); Undo.PerformRedo(); Assert.AreEqual(3,w.SceneryCount);
                var plant=w.Scenery.GetComponentsInChildren<LevelAuthoringAsset>()[0].transform;
                Undo.IncrementCurrentGroup(); Undo.RecordObject(plant,"Move contract"); plant.position+=Vector3.right; Undo.FlushUndoRecordObjects();
                Undo.PerformUndo(); Assert.AreEqual(5,plant.position.x,.001f); Undo.PerformRedo(); Assert.AreEqual(6,plant.position.x,.001f);
                PlacementToolIntegration.Adapter.Activate(PlacementTool.Erase,prefab,w.Scenery,w.Surface,1,65,.2f,.15f);
                var targets=io.GetField("_toErase",flags).GetValue(null); targets.GetType().GetMethod("Add").Invoke(targets,new object[]{plant.gameObject});
                Undo.IncrementCurrentGroup(); io.GetMethod("Erase",flags).Invoke(null,null);
                Assert.AreEqual(2,w.SceneryCount); Undo.PerformUndo(); Assert.AreEqual(3,w.SceneryCount); Undo.PerformRedo(); Assert.AreEqual(2,w.SceneryCount);
                PlacementToolIntegration.Adapter.Stop();
            }
        }
        [Test] public void UnreachableDoorsAndImpossiblePuzzlesStayInvalid()
        {
            var d=LevelAuthoringDocument.Create(false);
            d.PutTent(d.level.guests[0].id,new Cell(0,0),2);
            Assert.IsTrue(RuleEvaluator.Evaluate(d.level,d.level.witness,false).Issues.Any(i=>i.Code=="path"));
            foreach(var g in d.level.guests) g.shade=true; d.level.witness=null;
            var solver=new CampSolver(d.level,Array.Empty<Placement>(),1);
            while(solver.CurrentStatus==CampSolver.Status.Searching) solver.Step(.002);
            Assert.AreEqual(CampSolver.Status.Unsatisfiable,solver.CurrentStatus);
        }
        [Test] public void ShadowAndFriendRequirementsRemainCanonical()
        {
            var d=LevelAuthoringDocument.Create(true); d.level.witness[0].rotation=(d.level.witness[0].rotation+1)%4;
            var report=RuleEvaluator.Evaluate(d.level,d.level.witness);
            Assert.AreEqual(report.IsSolved,d.Validate(true).Count==0);
            foreach(var g in d.level.guests)g.shade=true;d.level.canopies=Array.Empty<ShadeCanopyData>();d.ReconcileObjects();
            Assert.IsTrue(RuleEvaluator.Evaluate(d.level,d.level.witness).Issues.Any(i=>i.Code=="shade"));
        }
        [Test] public void SearchCanBeCancelledWithoutReplacingManualTents()
        {
            var window=LevelAuthoringWindow.OpenWindow();var d=LevelAuthoringDocument.Create(true);
            try
            {
                window.SetDocument(d);string before=JsonConvert.SerializeObject(window.Document.level.witness);
                window.BeginSolve();Assert.IsTrue(window.IsSearching);window.CancelSolver();Assert.IsFalse(window.IsSearching);
                Assert.AreEqual(before,JsonConvert.SerializeObject(window.Document.level.witness));
            }
            finally { window.DiscardChanges();window.Close(); }
        }
        [Test] public void TimeLimitIsDifferentFromNoSolution()
        {
            var solver=new CampSolver(LevelAuthoringDocument.Create(true).level,Array.Empty<Placement>(),.01);
            System.Threading.Thread.Sleep(20);Assert.AreEqual(CampSolver.Status.Timeout,solver.Step(.002));
        }
        [Test] public void RebuildingTheFieldKeepsAllLogicalObjectsAndTents()
        {
            var d=LevelAuthoringDocument.Create(false);
            d.PlaceObject(new Cell(4,4),"campfire_stones");d.PlaceObject(new Cell(3,4),"tree_default");d.PlaceObject(new Cell(5,5),"stone_largeA");
            d.PutTent(d.level.guests[0].id,new Cell(1,1),0);
            using(var workspace=new LevelAuthoringWorkspace(d))
            {
                for(int i=0;i<3;i++)workspace.ApplyDocument(LevelAuthoringDocument.Copy(d));
                Assert.IsEmpty(workspace.Capture().Validate(false));
                Assert.AreEqual(1,workspace.Root.GetComponentsInChildren<Transform>().Count(t=>t.name=="DoorDot"));
            }
        }
        [Test] public void PaintingCannotTargetThePuzzleOrSubmergedGround()
        {
            var d=LevelAuthoringDocument.Create(false);
            using(var w=new LevelAuthoringWorkspace(d))
            {
                var collider=w.Surface.GetComponent<MeshCollider>();
                Assert.IsFalse(collider.Raycast(new Ray(new Vector3(0,50,0),Vector3.down),out _,100));
                Assert.IsTrue(collider.Raycast(new Ray(new Vector3(5,50,5),Vector3.down),out _,100));
                var vertices=collider.sharedMesh.vertices;
                Assert.IsTrue(collider.sharedMesh.triangles.All(i=>vertices[i].y>=-.075f));
            }
            d.art.streamCentre=0;Assert.IsTrue(d.Validate(false).Any(e=>e.Contains("Річка")));
        }
        [Test] public void FriendsUseTheExistingDoorToDoorDistance()
        {
            var d=LevelAuthoringDocument.Create(false);d.level.friends=new[]{new[]{d.level.guests[0].id,d.level.guests[1].id}};
            d.PutTent(d.level.guests[0].id,new Cell(0,0),0);d.PutTent(d.level.guests[1].id,new Cell(4,4),2);
            var report=RuleEvaluator.Evaluate(d.level,d.level.witness,false);
            Assert.IsTrue(report.CanCommit);Assert.IsTrue(report.Issues.Any(i=>i.Code=="friends"));
            Assert.IsFalse(report.Issues.Any(i=>i.Code=="path"));
        }
        [Test] public void GroupsPreserveWorldScaleAndRejectShearedTransforms()
        {
            using(var w=new LevelAuthoringWorkspace(LevelAuthoringDocument.Create(false)))
            {
                var plant=w.Spawn(new CampArtDefinition.Instance{modelId="cover_blades",x=5,z=5,scale=1},false);
                var group=new GameObject("Authoring group");group.transform.SetParent(w.Scenery,false);group.transform.localScale=new Vector3(2,1,1);
                plant.transform.SetParent(group.transform,false);plant.transform.localRotation=Quaternion.Euler(0,90,0);
                var scale=w.Capture().art.instances[0].scaleXYZ;Assert.AreEqual(1,scale[0],.0001);Assert.AreEqual(2,scale[2],.0001);
                plant.transform.localRotation=Quaternion.Euler(0,45,0);Assert.Throws<InvalidOperationException>(()=>w.Capture());
            }
        }
        [Test] public void PwbCanStartWithItsNativePaletteClosed()
        {
            Assert.NotNull(PlacementToolIntegration.Adapter);
            var assembly=PlacementToolIntegration.Adapter.GetType().Assembly;
            var palette=assembly.GetType("PluginMaster.PrefabPalette");
            bool wasOpen=palette.GetProperty("instance").GetValue(null)!=null;
            palette.GetMethod("CloseWindow").Invoke(null,null);
            try
            {
                using(var w=new LevelAuthoringWorkspace(LevelAuthoringDocument.Create(false)))
                {
                    PlacementToolIntegration.Adapter.Activate(PlacementTool.Paint,LevelAuthoringPalette.Get("cover_blades").prefab,w.Scenery,w.Surface,1,65,.2f,.15f);
                    var brush=assembly.GetType("PluginMaster.PaletteManager").GetProperty("selectedBrush").GetValue(null);
                    Assert.NotNull(brush);Assert.AreEqual("cover_blades",brush.GetType().GetProperty("name").GetValue(brush));
                    Assert.NotNull(palette.GetProperty("instance").GetValue(null),"PWB owns and opens its native palette when a tool is activated.");
                    PlacementToolIntegration.Adapter.Stop();
                }
            }
            finally{if(wasOpen)palette.GetMethod("ShowWindow").Invoke(null,null);}
        }
        [Test] public void PreviewCompletionRequiresSolvedLayoutAndMemoryStore()
        {
            var d=LevelAuthoringDocument.Create(true);
            string path=EditorAuthoringPreview.RequestPath;
            byte[] previous=File.Exists(path)?File.ReadAllBytes(path):null;
            bool enabled=EditorAuthoringPreview.Active;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path,JsonConvert.SerializeObject(new EditorAuthoringPreview.Request{level=d.level,art=d.art}));
                EditorAuthoringPreview.Reset();EditorAuthoringPreview.Enable(true);
                var save=EditorAuthoringPreview.CreateSave();
                var progress=new QuietCamp.Application.ProgressionService();
                var memories=new QuietCamp.Application.CampMemoryService(save.Memories);
                var catalog=MonetizationConfiguration.Load().Catalog();
                var completion=new CampCompletionService(save,progress,memories,catalog);
                using(var session=new QuietCamp.Application.CampSession(d.level))
                {
                    Assert.IsFalse(completion.Complete(session,"day"));
                    session.DebugApplyWitness();Assert.IsTrue(session.Check(()=>completion.Complete(session,"day")).IsSolved);Assert.IsTrue(session.IsCompleted);
                    Assert.IsEmpty(save.Progress.completedIds);Assert.IsEmpty(save.Album.entries);
                    var unsafeCompletion=new CampCompletionService(new SaveAdapter(),progress,memories,catalog);
                    Assert.IsFalse(unsafeCompletion.Complete(session,"day"));
                }
            }
            finally
            {
                if(previous==null)File.Delete(path);else File.WriteAllBytes(path,previous);
                EditorAuthoringPreview.Reset();EditorAuthoringPreview.Enable(enabled);
            }
        }
    }
}
