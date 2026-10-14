using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class SurfaceWeatheringTests
    {
        [Test] public void AluminiumRejectsRustAndWearIsDeterministicWithoutChangingSourceGeometry()
        {
            var source=LevelAuthoringPalette.Get("qc_aircraft_fore").model;var before=JsonConvert.SerializeObject(source);
            var recipe=new ModelAuthoringRecipe{operation="age",backend="rust",amount=.9f,seed=11,weathering=new SurfaceWeatheringSettings{material="aluminium",patchSize=2}};
            Assert.Throws<ArgumentException>(()=>ModelAuthoringWorkshop.Create(source,recipe,false));
            recipe.backend="oxidation";var a=ModelAuthoringWorkshop.Create(source,recipe,false);var b=ModelAuthoringWorkshop.Create(source,recipe,false);
            Assert.AreEqual(a.model.id,b.model.id);CollectionAssert.AreEqual(source.vertices,a.model.vertices);CollectionAssert.AreEqual(source.normals,a.model.normals);
            CollectionAssert.AreEqual(source.uv,a.model.uv);CollectionAssert.AreEqual(source.tangents,a.model.tangents);
            Assert.IsFalse(source.colors.SequenceEqual(a.model.colors));recipe.seed=29;var other=ModelAuthoringWorkshop.Create(source,recipe,false);Assert.AreNotEqual(a.model.id,other.model.id);
            Assert.AreEqual(before,JsonConvert.SerializeObject(source));
        }
        static CampArtDefinition.Model Quad()
        {
            return new CampArtDefinition.Model{id="weather-test-floor",shading="solid",seasonalRole="solid",vertices=new float[]{-.25f,0,-.25f,-.25f,0,.25f,.25f,0,.25f,-.25f,0,-.25f,.25f,0,.25f,.25f,0,-.25f},
                normals=Enumerable.Range(0,6).SelectMany(_=>new float[]{0,1,0}).ToArray(),colors=Enumerable.Repeat(0x805040,6).ToArray(),weatherExposure=new float[6]};
        }
        [Test] public void ShelteredGeometryKeepsItsColoursWhileExposedGeometryCollectsSnow()
        {
            var source=Quad();var preset=new SeasonAtmospherePreset{id="winter-test",season="winter",snowCover=1};
            var protectedMesh=SeasonModelAppearance.Resolve(source,preset);CollectionAssert.AreEqual(source.colors,protectedMesh.colors);
            source.weatherExposure=Enumerable.Repeat(1f,6).ToArray();var exposed=SeasonModelAppearance.Resolve(source,preset);
            Assert.That(exposed.colors.All(c=>CampArtDefinition.Color(c).r>.7f));
            var go=new GameObject("Exposure mesh round trip");var mesh=new Mesh();
            try
            {
                mesh.vertices=Enumerable.Range(0,6).Select(i=>new Vector3(source.vertices[i*3],0,source.vertices[i*3+2])).ToArray();mesh.normals=Enumerable.Repeat(Vector3.up,6).ToArray();mesh.triangles=Enumerable.Range(0,6).ToArray();
                mesh.colors=Enumerable.Repeat(new Color(.4f,.3f,.2f,.12f),6).ToArray();var read=ModelAuthoringWorkshop.ReadMesh(mesh,"mask-test");Assert.That(read.weatherExposure.All(v=>Math.Abs(v-.12f)<.001));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(go); }
        }
        [Test] public void MovingRoofRebakesExposureAndRepeatedBakeDoesNotCreateNewVariants()
        {
            var d=LevelAuthoringDocument.Create(false);var target=Quad();var roof=Quad();roof.id="weather-test-roof";roof.weatherExposure=null;
            d.art.models=new[]{target,roof};d.art.cover=Array.Empty<CampArtDefinition.CoverPoint>();
            d.art.instances=new[]{new CampArtDefinition.Instance{id="floor",modelId=target.id,scale=1},new CampArtDefinition.Instance{id="roof",modelId=roof.id,y=2,scale=5}};
            AuthoringWeatherExposure.Bake(d);var first=d.art.instances[0].modelId;Assert.That(d.art.models.Single(m=>m.id==first).weatherExposure.All(v=>v<.2f));
            AuthoringWeatherExposure.Bake(d);Assert.AreEqual(first,d.art.instances[0].modelId);
            d.art.instances[1].x=6;AuthoringWeatherExposure.Bake(d);var exposed=d.art.models.Single(m=>m.id==d.art.instances[0].modelId);Assert.That(exposed.weatherExposure.All(v=>v>.9f));
        }
        [Test] public void RotatedSupportUsesActualVerticesAndFrozenLargeObjectsCannotCoverTheField()
        {
            var d=LevelAuthoringDocument.Create(false);var model=CrashMeshAuthoring.Prop("seat");var rotation=Quaternion.Euler(9,51,17);var origin=new Vector3(8,0,7);
            float height=AircraftCrashAuthoring.GroundingHeight(d.art,model,origin,rotation,1.3f);
            var clearances=Enumerable.Range(0,model.colors.Length).Select(k=>{var p=rotation*new Vector3(model.vertices[k*3],model.vertices[k*3+1],model.vertices[k*3+2])*1.3f+origin+Vector3.up*height;return p.y-d.art.GroundHeight(p.x,p.z);}).ToArray();
            Assert.That(clearances.Min(),Is.EqualTo(0).Within(.001));Assert.That(clearances.All(v=>v>=-.001));
            model.authoringRecipe=JsonConvert.SerializeObject(new ModelAuthoringRecipe{category="Великі об’єкти",label="Frozen footprint"});
            d.art.models=new[]{model};d.art.instances=new[]{new CampArtDefinition.Instance{id="overlap",modelId=model.id,scale=1}};
            Assert.That(AuthoringPropCatalog.Validate(d).Any(e=>e.Contains("перетинає поле")));
        }
    }
}
