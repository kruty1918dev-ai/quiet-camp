using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class MobileGeometryBudgetTests
    {
        [TestCase("QC001")] [TestCase("QC007")] [TestCase("gen:qc_camp:60")] [TestCase("gen:qc_camp:91")]
        public void BudgetedArtPreservesSourcesAndFitsItsReservedShare(string id)
        {
            var level=LevelLoader.Load(id);
            var text=Resources.Load<TextAsset>("QuietCamp/ArtRevisions/"+level.contentHash)
                ??Resources.Load<TextAsset>("QuietCamp/ArtCompositions/"+id.Replace(':','_'));
            var original=CampArtDefinition.ReadDocument(text.text,out _);string before=JsonConvert.SerializeObject(original);
            var reduced=CampGeometryBudget.Prepare(original);reduced.Validate(level);
            Assert.AreEqual(before,JsonConvert.SerializeObject(original));
            Assert.LessOrEqual(CampGeometryBudget.CountAuthored(reduced),CampGeometryBudget.AuthoredTriangles);
            Assert.AreSame(reduced,CampGeometryBudget.Prepare(reduced));
            Assert.IsTrue(reduced.instances.Any(i=>i.id==original.primaryLandmarkInstanceId));
            var cover=CampGeometryBudget.SparseCover(reduced,level.width,level.height,level.decorSeed);
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
