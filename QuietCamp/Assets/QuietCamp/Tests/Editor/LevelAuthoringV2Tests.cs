using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public sealed class LevelAuthoringV2Tests
    {
        [UnityTest] public IEnumerator RiverAndTerrainBakeAndRestoreWithoutVendorRuntimeComponents()
        {
            if (RiverTerrainIntegration.Factory == null) Assert.Ignore("Local River Modeler and MicroVerse packages are not installed.");
            var document = LevelAuthoringDocument.Create(false);
            document.terrain = RiverTerrainAuthoringData.FromLegacy(document.art);
            document.terrain.points = new[] { new RiverPoint { x = 9, z = -16 }, new RiverPoint { x = 10, z = 0 }, new RiverPoint { x = 9, z = 16 } };
            document.terrain.stamps = new[] { new LandscapeStamp { x = -8, z = -8, radius = 3, height = 1 } };
            using (var workspace = new LevelAuthoringWorkspace(document))
            {
                double deadline = EditorApplication.timeSinceStartup + 45;
                while (workspace.River.IsUpdating && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.IsFalse(workspace.River.IsUpdating, "MicroVerse did not finish terrain evaluation: " + workspace.River);
                var first = workspace.River.Bake(); first.Validate();
                Assert.That(first.meshes.Count(m => m.water), Is.EqualTo(1));
                var water = first.meshes.Single(m => m.water);
                Assert.AreEqual(water.vertices.Length, water.flow.Length, "Preserve shader flow vectors in UV3.");
                var restoredWater = water.CreateMesh();
                try { Assert.AreEqual(water.vertices.Length / 3, restoredWater.tangents.Length); }
                finally { UnityEngine.Object.DestroyImmediate(restoredWater); }
                Assert.That(first.SampleHeight(-8, -8), Is.GreaterThan(.3f), "Hill stamp must change actual terrain.");
                Assert.That(first.SampleHeight(0, 0), Is.EqualTo(-.045f).Within(.03f), "Puzzle field stays flat.");
                float oldBed = first.SampleHeight(10, 0);
                var attached = workspace.Spawn(new CampArtDefinition.Instance { modelId = "cover_blades", x = -8, z = -8, y = first.SampleHeight(-8, -8) + .1f, scale = 1, surfaceAttached = true, surfaceOffset = .1f }, false);
                var manual = workspace.Spawn(new CampArtDefinition.Instance { modelId = "cover_blades", x = -7, z = -8, y = 2, scale = 1, surfaceAttached = false }, false);
                Assert.That(oldBed, Is.LessThan(-.2f), "River needs a carved bed below its surface.");
                var edit = document.EditingCopy(); edit.terrain.points[1].x = 12; edit.terrain.stamps[0].height = 2;
                workspace.ApplyDocument(edit);
                deadline = EditorApplication.timeSinceStartup + 45;
                while (workspace.River.IsUpdating && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.IsFalse(workspace.River.IsUpdating);
                var snapshot = workspace.Capture(); snapshot.art.landscape.Validate();
                Assert.That(snapshot.art.landscape.SampleHeight(12, 0), Is.LessThan(-.2f));
                Assert.That(attached.transform.localPosition.y, Is.EqualTo(snapshot.art.landscape.SampleHeight(-8, -8) + .1f).Within(.01f));
                Assert.That(manual.transform.localPosition.y, Is.EqualTo(2).Within(.001f));
                Assert.That(snapshot.art.landscape.SampleHeight(10, 0), Is.GreaterThan(oldBed + .1f), "Bank follows edited river curve.");
                var invalid = edit.EditingCopy(); invalid.terrain.points[1].x = 0;
                Assert.Throws<InvalidOperationException>(() => workspace.River.Apply(invalid));
                Assert.Throws<InvalidOperationException>(() => workspace.ApplyDocument(invalid));
                Assert.AreSame(edit, workspace.Document, "Reject a crossing river before changing authoritative draft state.");
                Assert.IsNotEmpty(invalid.Validate(true), "An unbaked invalid curve must also block export.");
                Assert.AreSame(snapshot.art.landscape, workspace.River.Bake());
                string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
                try { snapshot.Save(path); var restored = LevelAuthoringDocument.Open(path); Assert.AreEqual(JsonConvert.SerializeObject(snapshot.art.landscape), JsonConvert.SerializeObject(restored.art.landscape)); }
                finally { if (File.Exists(path)) File.Delete(path); }
            }
        }
        [Test] public void CampaignGalleryOpensAllHundredPublishedPuzzles()
        {
            Assert.AreEqual(100, LevelAuthoringCampaign.Cards.Length);
            foreach (var card in LevelAuthoringCampaign.Cards)
            {
                Assert.That(card.width, Is.InRange(4, 8)); Assert.That(card.height, Is.InRange(4, 8));
                Assert.That(card.guests, Is.InRange(2, 6));
            }
            var document = LevelAuthoringCampaign.Open(LevelAuthoringCampaign.Cards.Last().id);
            Assert.AreEqual(LevelAuthoringCampaign.Cards.Last().id, document.sourceLevelId);
            Assert.IsEmpty(document.Validate(true));
            Assert.Greater(document.art.instances.Length, 0, "Retain the canonical generator's actual surround.");
            using (var workspace = new LevelAuthoringWorkspace(document))
            {
                var captured = workspace.Capture();
                Assert.AreEqual(document.art.instances.Length, captured.art.instances.Length);
                Assert.IsEmpty(captured.Validate(true));
            }
        }
        [Test] public void PaletteAddsAtLeastTwentyFiveDistinctModels()
        {
            var ids = LevelAuthoringPalette.Entries.Select(e => e.id).ToArray();
            Assert.AreEqual(ids.Length, ids.Distinct().Count());
            Assert.GreaterOrEqual(ids.Count(id => id.StartsWith("ua_")), 31);
            CollectionAssert.IsSubsetOf(new[] { "ua_beehive", "ua_barn", "ua_valley_gate", "ua_valley_willow", "ua_valley_well" }, ids);
        }
        [Test] public void LegacyDraftMigratesWithoutChangingItsPlacedObjects()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
            try
            {
                var document = LevelAuthoringDocument.Create(true); document.version = 1;
                string before = JsonConvert.SerializeObject(document.art.instances);
                document.Save(path); var loaded = LevelAuthoringDocument.Open(path);
                Assert.AreEqual(2, loaded.version); Assert.IsNull(loaded.terrain);
                Assert.AreEqual(before, JsonConvert.SerializeObject(loaded.art.instances));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
        [Test] public void UndoRedoRestoresRiverCurveAndHillParameters()
        {
            var state = ScriptableObject.CreateInstance<LevelAuthoringState>();
            try
            {
                var document = LevelAuthoringDocument.Create(false); document.terrain = RiverTerrainAuthoringData.FromLegacy(document.art);
                document.terrain.stamps = new[] { new LandscapeStamp { x = -8, z = -8 } }; state.Write(document);
                float original = document.terrain.points[1].x;
                Undo.RecordObject(state, "Edit river and hill");
                var edit = state.Read(); edit.terrain.points[1].x += 2; edit.terrain.stamps[0].height = 2; state.Write(edit); Undo.FlushUndoRecordObjects();
                Undo.PerformUndo(); Assert.AreEqual(original, state.Read().terrain.points[1].x); Assert.AreEqual(1, state.Read().terrain.stamps[0].height);
                Undo.PerformRedo(); Assert.AreEqual(original + 2, state.Read().terrain.points[1].x); Assert.AreEqual(2, state.Read().terrain.stamps[0].height);
            }
            finally { Undo.ClearUndo(state); UnityEngine.Object.DestroyImmediate(state); }
        }
        [Test] public void SettingsEditCopiesMutableDataWithoutDuplicatingGeometry()
        {
            var document = LevelAuthoringDocument.Create(false); document.terrain = RiverTerrainAuthoringData.FromLegacy(document.art);
            var copy = document.EditingCopy();
            Assert.AreSame(document.art.models, copy.art.models); Assert.AreSame(document.terrain.baseHeights, copy.terrain.baseHeights);
            copy.art.cameraEuler[0] += 10; copy.terrain.points[0].x += 20; copy.level.width = 4;
            Assert.AreNotEqual(document.art.cameraEuler[0], copy.art.cameraEuler[0]);
            Assert.AreNotEqual(document.terrain.points[0].x, copy.terrain.points[0].x); Assert.AreEqual(6, document.level.width);
        }
        [Test] public void MovingTheCameraDoesNotRebuildTheBoardOrPlants()
        {
            using (var workspace = new LevelAuthoringWorkspace(LevelAuthoringDocument.Create(false)))
            {
                int board = workspace.BoardRebuilds, plants = workspace.FullPlantRebuilds;
                var edit = workspace.Document.EditingCopy(); edit.art.cameraEuler[0] += 3; edit.art.sunIntensity += .1f;
                workspace.ApplyDocument(edit);
                Assert.AreEqual(board, workspace.BoardRebuilds); Assert.AreEqual(plants, workspace.FullPlantRebuilds);
            }
        }
        [Test] public void MovingOnePlantRebuildsOnlyItsBatch()
        {
            using (var workspace = new LevelAuthoringWorkspace(LevelAuthoringDocument.Create(false)))
            {
                var prefab = LevelAuthoringPalette.Get("cover_blades").prefab;
                Transform first = null;
                for (int i = 0; i < 10000; i++)
                {
                    var node = (GameObject)PrefabUtility.InstantiatePrefab(prefab, workspace.Scenery);
                    node.transform.localPosition = new Vector3(5 + i % 20 * .1f, 0, i / 20 * .1f);
                    if (first == null) first = node.transform;
                }
                using var recorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "QuietCamp.Authoring.Plants.Partial", 1, Unity.Profiling.ProfilerRecorderOptions.Default | Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                var watch = System.Diagnostics.Stopwatch.StartNew();
                workspace.RefreshPlantBatches(); double fullMs = watch.Elapsed.TotalMilliseconds;
                int full = workspace.FullPlantRebuilds, partial = workspace.PartialPlantRebuilds;
                first.localPosition += Vector3.right;
                watch.Restart();
                workspace.RefreshPlantBatches(new[] { first });
                double partialMs = watch.Elapsed.TotalMilliseconds;
                Assert.AreEqual(full, workspace.FullPlantRebuilds); Assert.AreEqual(partial + 1, workspace.PartialPlantRebuilds);
                Assert.Less(partialMs, fullMs, "A single batch should cost less than all twenty batches.");
                Directory.CreateDirectory("Temp/ai");
                File.WriteAllText("Temp/ai/authoring-v2-plant-performance.json", JsonConvert.SerializeObject(new { plants = 10000, fullMs, partialMs, rebuiltBatches = 1, profilerMarker = recorder.Valid }));
            }
        }
        [Test] public void VisualRevisionDoesNotInvalidateLegacyHashesUntilEdited()
        {
            var level = LevelLoader.Load("QC001"); string original = CampContent.CalculateHash(level);
            Assert.AreEqual(level.contentHash, original);
            level.compositionHash = new string('a', 64);
            Assert.AreNotEqual(original, CampContent.CalculateHash(level));
        }
        [Test] public void ApplyingOneDraftPreservesOtherLevelsAndRejectsStaleSource()
        {
            string root = Path.Combine(Path.GetTempPath(), "qc-authoring-" + Guid.NewGuid()); Directory.CreateDirectory(root);
            const string source = "Assets/QuietCamp/Resources/QuietCamp";
            try
            {
                File.Copy(Path.Combine(source, "campaign.json"), Path.Combine(root, "campaign.json"));
                foreach (string folder in new[] { "Levels", "GeneratedLevels", "ArtCompositions" })
                {
                    Directory.CreateDirectory(Path.Combine(root, folder));
                    foreach (string file in Directory.GetFiles(Path.Combine(source, folder), "*.json")) File.Copy(file, Path.Combine(root, folder, Path.GetFileName(file)));
                }
                var document = LevelAuthoringCampaign.Open("QC001"); string other = File.ReadAllText(Path.Combine(root, "Levels/QC002.json"));
                string previous = document.sourceContentHash;
                LevelAuthoringCampaign.Apply(document, root);
                Assert.AreEqual(other, File.ReadAllText(Path.Combine(root, "Levels/QC002.json")));
                Assert.AreEqual(1, Directory.GetFiles(Path.Combine(root, "ContentRevisions")).Length);
                Assert.AreEqual(1, Directory.GetFiles(Path.Combine(root, "ArtRevisions")).Length);
                Assert.AreEqual(100, LevelAuthoringCampaign.ReadCards(root).Length);
                string artPath = Path.Combine(root, "ArtCompositions/QC001.json");
                File.AppendAllText(artPath, " ");
                Assert.Throws<InvalidOperationException>(() => LevelAuthoringCampaign.Apply(document, root), "Reject changes to source scenery as well as puzzle data.");
                document.sourceContentHash = previous;
                Assert.Throws<InvalidOperationException>(() => LevelAuthoringCampaign.Apply(document, root));
            }
            finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }
    }
}
