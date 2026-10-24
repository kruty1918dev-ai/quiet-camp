using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Editor.LevelAuthoring.WorldOps;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class MobileGeometryBudgetTests
    {
        [Test] public void GrassUsesCompactRaisedLeavesInsteadOfRibbonQuads()
        {
            var level=LevelLoader.Load("QC007");var art=CampArtDefinition.Load(level);
            var mesh=CampMeadowTufts.Build(art,level.width,level.height,level.decorSeed);
            Assert.Greater(mesh.triangles.Length/3,2800);
            for(int i=0;i<mesh.vertices.Length;i+=9)
            {
                float width=Vector2.Distance(new Vector2(mesh.vertices[i],mesh.vertices[i+2]),new Vector2(mesh.vertices[i+3],mesh.vertices[i+5]));
                Assert.That(width,Is.InRange(.04f,.101f));
                Assert.Greater(mesh.vertices[i+7]-Mathf.Max(mesh.vertices[i+1],mesh.vertices[i+4]),.04f);
            }
            Assert.IsNull(Resources.Load<Shader>("QuietCamp/MobileMeadowCards"));
        }
        [Test] public void RuntimeAuditMatchesThePlayerPolicyAndCannotPublishOrMutate()
        {
            var recipe=new WorldRecipe{id="runtime-test",previewOnly=true,autoApply=false,runtimePresentation=true,
                operations=new[]{new WorldOperation{id="current",kind="audit"}},criteria=new WorldCriteria{maximumSceneTriangles=50000}};
            var text=Resources.Load<TextAsset>("QuietCamp/ArtCompositions/QC007");
            var original=CampArtDefinition.ReadDocument(text.text,out _);string before=JsonConvert.SerializeObject(original);
            var doc=LevelAuthoringDocument.Create(false);doc.art=original;
            WorldOpsOperations.PrepareRuntimePresentation(doc,recipe);
            Assert.AreEqual(before,JsonConvert.SerializeObject(original));
            Assert.AreEqual(JsonConvert.SerializeObject(CampGeometryBudget.DenseMeadow(CampGeometryBudget.Prepare(original))),JsonConvert.SerializeObject(doc.art));
            recipe.autoApply=true;Assert.Throws<ArgumentException>(()=>WorldOpsOperations.Validate(recipe));
            recipe.autoApply=false;recipe.previewOnly=false;Assert.Throws<ArgumentException>(()=>WorldOpsOperations.Validate(recipe));
            recipe.previewOnly=true;recipe.operations[0].kind="vegetation.refine";Assert.Throws<ArgumentException>(()=>WorldOpsOperations.Validate(recipe));
        }
        [Test] public void DenseDetailTexturesHaveMipsAndAreSharedBetweenMaterials()
        {
            var shader=Resources.Load<Shader>("QuietCamp/CampArtLit");Assert.NotNull(shader);
            var first=new Material(shader);var second=new Material(shader);
            try
            {
                var art=new CampArtDefinition{mobileGeometryBudget=true,mobileMeadow=true};
                MobileMeadowMaterial.Bind(first,art);MobileMeadowMaterial.Bind(second,art);
                var ground=(Texture2D)first.GetTexture("_MeadowDetailMap");
                Assert.AreEqual(256,ground.width);Assert.AreEqual(256,ground.height);Assert.Greater(ground.mipmapCount,1);
                Assert.AreEqual(FilterMode.Trilinear,ground.filterMode);Assert.AreEqual(TextureWrapMode.Repeat,ground.wrapMode);
                Assert.IsFalse(ground.isReadable);
                Assert.AreSame(ground,second.GetTexture("_MeadowDetailMap"));
                MobileMeadowMaterial.Bind(second,null);Assert.AreEqual(0,second.GetFloat("_MobileMeadow"));
            }
            finally{UnityEngine.Object.DestroyImmediate(first);UnityEngine.Object.DestroyImmediate(second);}
        }
        [TestCase("QC001")] [TestCase("QC007")] [TestCase("gen:qc_camp:60")] [TestCase("gen:qc_camp:91")]
        public void BudgetedArtPreservesSourcesAndFitsItsReservedShare(string id)
        {
            var level=LevelLoader.Load(id);
            var text=Resources.Load<TextAsset>("QuietCamp/ArtRevisions/"+level.contentHash)
                ??Resources.Load<TextAsset>("QuietCamp/ArtCompositions/"+id.Replace(':','_'));
            var original=CampArtDefinition.ReadDocument(text.text,out _);string before=JsonConvert.SerializeObject(original);
            var reduced=CampGeometryBudget.DenseMeadow(CampGeometryBudget.Prepare(original));reduced.Validate(level);
            Assert.AreEqual(before,JsonConvert.SerializeObject(original));
            Assert.LessOrEqual(CampGeometryBudget.CountAuthored(reduced),CampGeometryBudget.AuthoredTriangles);
            Assert.AreSame(reduced,CampGeometryBudget.Prepare(reduced));
            Assert.IsTrue(reduced.instances.Any(i=>i.id==original.primaryLandmarkInstanceId));
            var cover=CampMeadowTufts.Build(reduced,level.width,level.height,level.decorSeed);
            Assert.LessOrEqual(cover.triangles.Length/3,CampGeometryBudget.ProceduralGrassTriangles);
            if(reduced.atmosphere?.season=="winter")Assert.IsEmpty(cover.triangles);
            Assert.AreEqual(0,reduced.blurPixels);Assert.AreEqual(0,reduced.fieldCover.softnessPixels);
        }
        [Test] public void ExplicitlyProtectedGrassCannotBeRemovedToManufactureAPassingBudget()
        {
            var text=Resources.Load<TextAsset>("QuietCamp/ArtCompositions/QC001");
            var art=CampArtDefinition.ReadDocument(text.text,out _);
            Assert.Throws<System.IO.InvalidDataException>(()=>CampGeometryBudget.Prepare(art,Array.Empty<string>()));
        }
    }
}
