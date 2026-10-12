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
                    CollectionAssert.AreEqual(captured.art.instances[0].scaleXYZ,second.Capture().art.instances[0].scaleXYZ);
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
    }
}
