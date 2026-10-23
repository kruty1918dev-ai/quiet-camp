using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class MobileGeometryBudgetTests
    {
        [Test] public void DenseDetailTexturesHaveMipsAndAreSharedBetweenMaterials()
        {
            var shader=Resources.Load<Shader>("QuietCamp/MobileMeadowCards");Assert.NotNull(shader);
            var first=new Material(shader);var second=new Material(shader);
            try
            {
                var art=new CampArtDefinition{mobileGeometryBudget=true,mobileMeadow=true};
                MobileMeadowMaterial.Bind(first,art);MobileMeadowMaterial.Bind(second,art);
                var ground=(Texture2D)first.GetTexture("_MeadowDetailMap");var cards=(Texture2D)first.GetTexture("_MeadowCardMap");
                Assert.AreEqual(256,ground.width);Assert.AreEqual(256,ground.height);Assert.Greater(ground.mipmapCount,1);
                Assert.AreEqual(256,cards.width);Assert.AreEqual(64,cards.height);Assert.Greater(cards.mipmapCount,1);
                Assert.AreEqual(FilterMode.Trilinear,ground.filterMode);Assert.AreEqual(TextureWrapMode.Repeat,ground.wrapMode);
                Assert.IsFalse(ground.isReadable);Assert.IsFalse(cards.isReadable);
                Assert.AreSame(ground,second.GetTexture("_MeadowDetailMap"));Assert.AreSame(cards,second.GetTexture("_MeadowCardMap"));
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
            var cover=CampMeadowImpostors.Build(reduced,level.width,level.height,level.decorSeed);
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
