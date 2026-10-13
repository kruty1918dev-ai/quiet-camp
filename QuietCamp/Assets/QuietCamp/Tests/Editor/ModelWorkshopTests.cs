using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;

namespace QuietCamp.Tests
{
    public sealed class ModelWorkshopTests
    {
        [Test] public void ModelReplacementUndoKeepsOtherInstancesAndSource()
        {
            var d=LevelAuthoringDocument.Create(false);
            var source=LevelAuthoringPalette.Get("qc_barrel").model;
            var original=JsonConvert.SerializeObject(source);
            d.art.models=new[]{source};d.art.cover=Array.Empty<CampArtDefinition.CoverPoint>();
            d.art.instances=new[]{new CampArtDefinition.Instance{modelId=source.id,x=7,z=6,scale=1},new CampArtDefinition.Instance{modelId=source.id,x=9,z=6,scale=1}};
            var variant=ModelAuthoringWorkshop.Create(source,new ModelAuthoringRecipe{operation="scale",size=1.25f,label="Undo test",category="Декор"},false);
            LevelAuthoringPalette.EnsureDocumentModels(new[]{variant.model});
            using(var workspace=new LevelAuthoringWorkspace(d))
            {
                var markers=workspace.Scenery.GetComponentsInChildren<LevelAuthoringAsset>();Assert.AreEqual(2,markers.Length);
                var first=markers.OrderBy(m=>m.transform.position.x).First();var second=markers.OrderBy(m=>m.transform.position.x).Last();
                var originalMesh=first.GetComponent<MeshFilter>().sharedMesh;
                Undo.IncrementCurrentGroup();workspace.ReplaceModel(first,variant);Undo.FlushUndoRecordObjects();
                Assert.AreEqual(variant.model.id,first.stableId);Assert.AreEqual(source.id,second.stableId);
                Undo.PerformUndo();Assert.AreEqual(source.id,first.stableId);Assert.AreSame(originalMesh,first.GetComponent<MeshFilter>().sharedMesh);
                Undo.PerformRedo();Assert.AreEqual(variant.model.id,first.stableId);Assert.AreEqual(source.id,second.stableId);
                Undo.ClearUndo(first);Undo.ClearUndo(first.GetComponent<MeshFilter>());
            }
            Assert.AreEqual(original,JsonConvert.SerializeObject(source));
        }
        [UnityTest] public IEnumerator BothDetailJobsFinishAndCancellationDiscardsWork()
        {
            var source=LevelAuthoringPalette.Get("qc_barrel").model;var before=JsonConvert.SerializeObject(source);
            foreach(var backend in new[]{"fast","protected"})
            {
                using(var job=new ModelAuthoringWorkshop.DetailJob(source,new ModelAuthoringRecipe{operation="simplify",density=.65f,backend=backend,label="Detail test",category="Декор"}))
                {
                    double deadline=UnityEditor.EditorApplication.timeSinceStartup+30;
                    while(!job.IsDone && UnityEditor.EditorApplication.timeSinceStartup<deadline)yield return null;
                    Assert.IsTrue(job.IsDone,backend);var v=job.Finish();Assert.Greater(v.model.colors.Length,0);
                    Assert.LessOrEqual(v.model.colors.Length,source.colors.Length);Assert.IsTrue(v.model.vertices.All(n=>!float.IsNaN(n)&&!float.IsInfinity(n)));
                }
            }
            using(var cancelled=new ModelAuthoringWorkshop.DetailJob(source,new ModelAuthoringRecipe{operation="simplify",density=.5f,backend="fast"}))
            {cancelled.Dispose();Assert.Throws<System.OperationCanceledException>(()=>cancelled.Finish());}
            Assert.AreEqual(before,JsonConvert.SerializeObject(source));
        }
        [Test] public void MeshRoundTripPreservesUvsTangentsAndProBuilderContextExists()
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);var input=UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            try
            {
                input.colors=Enumerable.Repeat(Color.gray,input.vertexCount).ToArray();input.RecalculateTangents();
                var model=ModelAuthoringWorkshop.ReadMesh(input,"uv-cube");model.id="qc_test_uv_cube";LevelAuthoringPalette.EnsureDocumentModels(new[]{model});
                var output=LevelAuthoringPalette.Get(model.id).prefab.GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(input.triangles.Length*2,model.uv.Length);Assert.AreEqual(input.triangles.Length*4,model.tangents.Length);
                CollectionAssert.AreEqual(input.triangles.Select(i=>input.uv[i]),output.uv);CollectionAssert.AreEqual(input.triangles.Select(i=>input.tangents[i]),output.tangents);
                Assert.NotNull(UnityEditor.TypeCache.GetTypesDerivedFrom<UnityEditor.EditorTools.EditorToolContext>().FirstOrDefault(t=>t.FullName=="UnityEditor.ProBuilder.PositionToolContext"));
            }
            finally{UnityEngine.Object.DestroyImmediate(input);UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void ModelVariantsPreserveSourceAndAgePredictably()
        {
            var original=LevelAuthoringPalette.Get("qc_barrel").model;var before=JsonConvert.SerializeObject(original);
            var scaled=ModelAuthoringWorkshop.Create(original,new ModelAuthoringRecipe{operation="scale",size=2,label="Test scale",category="Декор"},false);
            Assert.That(AuthoringPropCatalog.ModelBounds(scaled.model).size.y,Is.EqualTo(AuthoringPropCatalog.ModelBounds(original).size.y*2).Within(.001));
            var r=new ModelAuthoringRecipe{operation="age",backend="moss",amount=.8f,seed=42,label="Test moss",category="Декор"};
            var a=ModelAuthoringWorkshop.Create(original,r,false);var b=ModelAuthoringWorkshop.Create(original,r,false);
            Assert.AreEqual(a.model.id,b.model.id);CollectionAssert.AreEqual(original.vertices,a.model.vertices);CollectionAssert.AreEqual(original.normals,a.model.normals);
            Assert.IsFalse(original.colors.SequenceEqual(a.model.colors));Assert.AreEqual(before,JsonConvert.SerializeObject(original));
        }
        [Test] public void ConvexCutsProduceTwoColouredPiecesAndRejectHollowModels()
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);var mesh=UnityEngine.Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            try
            {
                mesh.colors=Enumerable.Repeat(QualitySettings.activeColorSpace==ColorSpace.Linear?Color.red.linear:Color.red,mesh.vertexCount).ToArray();
                var source=ModelAuthoringWorkshop.ReadMesh(mesh,"test-cube");source.id="test-cube";
                Assert.IsTrue(ModelAuthoringWorkshop.IsConvex(source));var pieces=ModelAuthoringWorkshop.Slice(source,Vector3.right,0,"Test cut",false);
                Assert.AreEqual(2,pieces.Length);foreach(var p in pieces){Assert.Greater(p.model.colors.Length,0);Assert.IsTrue(p.model.colors.All(c=>((c>>16)&255)>240));Assert.LessOrEqual(AuthoringPropCatalog.ModelBounds(p.model).size.x,.501f);}
                Assert.IsFalse(ModelAuthoringWorkshop.IsConvex(LevelAuthoringPalette.Get("qc_watermill_ruin").model));
            }
            finally{UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void FiveStudiesAreDifferentValidAndExportFrozenGeometry()
        {
            var profiles=WorldStudyFactory.Profiles;Assert.AreEqual(5,profiles.Length);var signatures=profiles.Select(p=>p.biome).Distinct().Count();Assert.AreEqual(5,signatures);
            string folder=Path.Combine(Path.GetTempPath(),"qc-five-"+Guid.NewGuid().ToString("N"));
            try
            {
                foreach(var profile in profiles)
                {
                    var d=WorldStudyFactory.Create(profile.id);Assert.IsEmpty(d.Validate(true),profile.id);d.Export(folder);
                    var copy=LevelAuthoringDocument.Open(Path.Combine(folder,d.level.id+".draft.json"));Assert.AreEqual(JsonConvert.SerializeObject(d.art),JsonConvert.SerializeObject(copy.art));
                }
            }
            finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
        }
        [Test] public void MetricAircraftAndVillageModelsUseRealSourceDimensions()
        {
            var fore=AuthoringPropCatalog.ModelBounds(WorldStudyFactory.MetricModel("qc_aircraft_fore"));var tail=AuthoringPropCatalog.ModelBounds(WorldStudyFactory.MetricModel("qc_aircraft_tail"));
            Assert.That(fore.size.z+tail.size.z,Is.EqualTo(38).Within(.02));Assert.Greater(fore.size.y,5);
            Assert.That(AuthoringPropCatalog.ModelBounds(WorldStudyFactory.MetricModel("ua_abandoned_house")).size.y,Is.EqualTo(4.05).Within(.01));
        }
    }
}
