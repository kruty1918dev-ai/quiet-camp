using System;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Infrastructure;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class WorldOpsTests
    {
        static LevelAuthoringDocument Document()
        {
            var d=LevelAuthoringDocument.Create(false);
            var m=LevelAuthoringPalette.Get("qc_barrel").model;
            d.art.models=new[]{LevelAuthoringDocument.Copy(m)};
            d.art.instances=new[]{new CampArtDefinition.Instance{id="test/barrel",modelId=m.id,x=8,z=8,scale=1,surfaceAttached=false}};
            d.art.cover=Array.Empty<CampArtDefinition.CoverPoint>();d.art.atmosphere=null;
            return d;
        }
        [Test] public void SameIdPoseChangesReachNativeSceneAndSurviveCapture()
        {
            var d=Document();
            using(var workspace=new LevelAuthoringWorkspace(d))
            {
                int rebuilds=workspace.FullPlantRebuilds;
                var next=LevelAuthoringDocument.Copy(d);var p=next.art.instances[0];
                p.x=9;p.y=.4f;p.z=10;p.yaw=73;p.rotationEuler=new[]{4f,73f,2f};p.scale=1.2f;p.scaleXYZ=new[]{1.2f,1.1f,1.3f};
                workspace.ApplyDocument(next);
                var marker=workspace.Scenery.GetComponentInChildren<LevelAuthoringAsset>();
                Assert.That(marker.transform.localPosition,Is.EqualTo(new Vector3(9,.4f,10)));
                Assert.Less(Quaternion.Angle(marker.transform.localRotation,Quaternion.Euler(4,73,2)),.001);
                var captured=workspace.Capture(false).art.instances.Single();
                Assert.AreEqual(9,captured.x);Assert.AreEqual(10,captured.z);CollectionAssert.AreEqual(p.scaleXYZ,captured.scaleXYZ);
                Assert.AreEqual(rebuilds,workspace.FullPlantRebuilds,"A pose edit must not rebuild every plant.");
            }
        }
        [Test] public void SameIdFrozenGeometryChangesReachMeshesAndCapture()
        {
            var d=Document();
            using(var workspace=new LevelAuthoringWorkspace(d))
            {
                var next=LevelAuthoringDocument.Copy(d);next.art.models[0].vertices[0]+=.17f;
                workspace.ApplyDocument(next);
                var mesh=workspace.Scenery.GetComponentInChildren<MeshFilter>().sharedMesh;
                Assert.That(mesh.vertices[0].x,Is.EqualTo(next.art.models[0].vertices[0]).Within(.0001));
                CollectionAssert.AreEqual(next.art.models[0].vertices,workspace.Capture(false).art.models.Single().vertices);
            }
        }
    }
}
