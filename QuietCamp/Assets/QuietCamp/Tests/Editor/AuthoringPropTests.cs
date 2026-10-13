using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Infrastructure;
using UnityEditor;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class AuthoringPropTests
    {
        [Test] public void DecorativePropsRoundTripAndExportWithoutChangingPuzzle()
        {
            string folder = Path.Combine(Path.GetTempPath(), "qc-props-" + Guid.NewGuid().ToString("N"));
            try
            {
                var d = LevelAuthoringDocument.Create(true, "QC007");
                var puzzle = JsonConvert.SerializeObject(d.level);
                var entries = LevelAuthoringPalette.Entries.Where(e => e.prop != null).ToArray();
                d.art.models = d.art.models.Concat(entries.Select(e => e.model)).GroupBy(m => m.id).Select(g => g.First()).ToArray();
                d.art.instances = entries.Select((e,i) => new CampArtDefinition.Instance { id="prop/"+i,modelId=e.id,
                    x=25+i*.1f,z=18,y=.2f,yaw=35,scale=.8f,surfaceAttached=false,rotationEuler=new[]{2f,35f,3f},scaleXYZ=new[]{.8f,.8f,.8f} }).ToArray();
                d.art.cover = Array.Empty<CampArtDefinition.CoverPoint>();
                Assert.IsEmpty(d.Validate(true));
                Assert.AreEqual(puzzle, JsonConvert.SerializeObject(d.level));
                d.Export(folder);
                var restored = LevelAuthoringDocument.Open(Path.Combine(folder,d.level.id+".draft.json"));
                Assert.AreEqual(JsonConvert.SerializeObject(d.art),JsonConvert.SerializeObject(restored.art));
                var exported = JsonConvert.DeserializeObject<CampArtDefinition>(File.ReadAllText(Path.Combine(folder,d.level.id+".art.json")));
                exported.Validate(restored.level);
                Assert.AreEqual(entries.Length,exported.instances.Length);
                StringAssert.Contains("Poly by Google",File.ReadAllText(Path.Combine(folder,"ATTRIBUTION.md")));
            }
            finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
        }
        [Test] public void FullLandmarkFootprintIsCheckedAfterRotationAndScale()
        {
            var prop=AuthoringPropCatalog.Find("qc_windmill_ruin");
            var safe=Matrix4x4.TRS(new Vector3(12,0,0),Quaternion.Euler(0,45,0),Vector3.one);
            Assert.IsFalse(AuthoringPropCatalog.OverlapsBoard(prop,safe,6,8));
            var overlapping=Matrix4x4.TRS(new Vector3(3.5f,0,0),Quaternion.Euler(0,45,0),Vector3.one*2);
            Assert.IsTrue(AuthoringPropCatalog.OverlapsBoard(prop,overlapping,6,8));
            var d=LevelAuthoringDocument.Create(true,"QC007");
            d.art.models=d.art.models.Concat(new[]{LevelAuthoringPalette.Get(prop.id).model}).ToArray();
            d.art.instances=new[]{new CampArtDefinition.Instance{modelId=prop.id,x=3.5f,scale=2,yaw=45}};
            Assert.IsTrue(d.Validate(true).Any(e=>e.Contains("перетинає поле")));
            Assert.Throws<InvalidDataException>(()=>d.Export(Path.Combine(Path.GetTempPath(),"qc-props-rejected")));
            Assert.IsFalse(Directory.Exists(Path.Combine(Path.GetTempPath(),"qc-props-rejected")));
        }
        [Test] public void NewPropPlacementUndoRedoAndWaterHeightSurviveWorkspaceRestore()
        {
            var document=LevelAuthoringDocument.Create(false);
            using(var workspace=new LevelAuthoringWorkspace(document))
            {
                Undo.IncrementCurrentGroup();
                workspace.Spawn(new CampArtDefinition.Instance { id="boat/one",modelId="qc_rowboat",x=9,z=9,y=-.13f,scale=1,
                    surfaceAttached=false,rotationEuler=new[]{0f,32f,0f}},true);
                Undo.FlushUndoRecordObjects(); Assert.AreEqual(1,workspace.SceneryCount);
                Undo.PerformUndo(); Assert.AreEqual(0,workspace.SceneryCount);
                Undo.PerformRedo(); Assert.AreEqual(1,workspace.SceneryCount);
                var captured=workspace.Capture(false);
                Assert.AreEqual(-.13f,captured.art.instances[0].y); Assert.AreEqual(false,captured.art.instances[0].surfaceAttached);
                using(var restored=new LevelAuthoringWorkspace(captured))
                    Assert.AreEqual(JsonConvert.SerializeObject(captured.art),JsonConvert.SerializeObject(restored.Capture(false).art));
            }
        }
        [TestCase("aircraft")] [TestCase("windmill")] [TestCase("watermill")]
        public void DemonstrationsKeepPublishedLevelAndAreSolvable(string kind)
        {
            var published=JsonConvert.SerializeObject(LevelLoader.Load("QC007"));
            var d=PropDemonstrations.Create(kind); Assert.IsEmpty(d.Validate(true));
            Assert.AreEqual(published,JsonConvert.SerializeObject(LevelLoader.Load("QC007")));
        }
        [Test] public void DraftOwnsFrozenGeometryEvenWhenPaletteHasANewerModel()
        {
            var d=LevelAuthoringDocument.Create(false);
            var frozen=LevelAuthoringDocument.Copy(LevelAuthoringPalette.Get("qc_mug").model);
            frozen.colors=Enumerable.Repeat(0x795D44,frozen.colors.Length).ToArray();
            for(int i=0;i<frozen.vertices.Length;i+=3)frozen.vertices[i]*=1.3f;
            d.art.models=new[]{frozen};d.art.instances=new[]{new CampArtDefinition.Instance{modelId=frozen.id,x=9,z=8,scale=1,surfaceAttached=false}};
            using(var workspace=new LevelAuthoringWorkspace(d))
            {
                var mesh=workspace.Scenery.GetChild(0).GetComponent<MeshFilter>().sharedMesh;
                Assert.AreEqual(frozen.vertices[0],mesh.vertices[0].x);
                Assert.AreEqual(JsonConvert.SerializeObject(frozen),JsonConvert.SerializeObject(workspace.Capture(false).art.models[0]));
                Assert.AreNotEqual(JsonConvert.SerializeObject(frozen),JsonConvert.SerializeObject(LevelAuthoringPalette.Get(frozen.id).model));
            }
        }
    }
}
