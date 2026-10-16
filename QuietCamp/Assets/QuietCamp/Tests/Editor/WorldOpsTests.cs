using System;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using QuietCamp.Editor.LevelAuthoring.WorldOps;
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
        [Test] public void ManualAndPinnedObjectsNeedExplicitSelection()
        {
            var d=Document();Assert.IsEmpty(WorldOpsOperations.Select(d,new WorldSelector()));
            Assert.AreEqual(1,WorldOpsOperations.Select(d,new WorldSelector{ids=new[]{"test/barrel"}}).Length);
            WorldOpsOperations.Record(d,"test/barrel",true).pinned=true;
            Assert.IsEmpty(WorldOpsOperations.Select(d,new WorldSelector{ids=new[]{"test/barrel"}}));
        }
        [Test] public void GroundingUsesRotatedNonuniformGeometry()
        {
            var d=Document();var p=d.art.instances[0];p.y=4;p.rotationEuler=new[]{14f,41f,8f};p.scaleXYZ=new[]{1.2f,.8f,1.5f};
            Assert.IsTrue(WorldOpsGeometry.Validate(d,new WorldCriteria()).Any(f=>f.code=="support.floating"));
            WorldOpsGeometry.Ground(d,p,d.art.models[0],new WorldObjectRecord());
            Assert.That(WorldOpsGeometry.Clearance(d,p,d.art.models[0]),Is.EqualTo(0).Within(.0001));
        }
        [Test] public void PathUpsertIsIdempotentAndCannotClearThePuzzle()
        {
            var d=Document();var op=new WorldOperation{id="path-a",kind="path.upsert",parameters=JObject.Parse("{\"pathId\":\"approach\",\"points\":[[8,8],[9,9]],\"width\":0.6}")};
            WorldOpsOperations.Apply(d,op,3);string hash=WorldOpsStorage.Digest(d);WorldOpsOperations.Apply(d,op,3);
            Assert.AreEqual(hash,WorldOpsStorage.Digest(d));Assert.AreEqual(1,d.worldOps.paths.Count);
            op.parameters["points"]=JArray.Parse("[[0,0],[9,9]]");Assert.Throws<InvalidOperationException>(()=>WorldOpsOperations.Apply(d,op,3));
        }
        [Test] public void TransactionResumesAfterInterruptionAndProtectsLaterEdits()
        {
            string root=Path.Combine(Path.GetTempPath(),"qc-worldops-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                string a=Path.Combine(root,"a.json"),b=Path.Combine(root,"b.json"),journal=Path.Combine(root,"transaction");File.WriteAllText(a,"old-a");File.WriteAllText(b,"old-b");
                var sources=new System.Collections.Generic.Dictionary<string,string>{{a,WorldOpsStorage.FileHash(a)},{b,WorldOpsStorage.FileHash(b)}};
                WorldOpsTransaction.Prepare(journal,new System.Collections.Generic.Dictionary<string,string>{{a,"new-a"},{b,"new-b"}},sources,new[]{root});
                Assert.Throws<IOException>(()=>WorldOpsTransaction.Commit(journal,i=>{if(i==1)throw new IOException("power loss");}));
                WorldOpsTransaction.Commit(journal);Assert.AreEqual("new-b",File.ReadAllText(b));
                File.WriteAllText(a,"later user edit");Assert.Throws<IOException>(()=>WorldOpsTransaction.Rollback(journal));Assert.AreEqual("new-b",File.ReadAllText(b));
                File.WriteAllText(a,"new-a");WorldOpsTransaction.Rollback(journal);Assert.AreEqual("old-a",File.ReadAllText(a));Assert.AreEqual("old-b",File.ReadAllText(b));
            }
            finally{Directory.Delete(root,true);}
        }
        [Test] public void ChangedSourceOrCorruptPreparedWritePreventsEveryWrite()
        {
            string root=Path.Combine(Path.GetTempPath(),"qc-worldops-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                string a=Path.Combine(root,"a"),source=Path.Combine(root,"source"),journal=Path.Combine(root,"transaction");File.WriteAllText(a,"old");File.WriteAllText(source,"old source");
                var j=WorldOpsTransaction.Prepare(journal,new System.Collections.Generic.Dictionary<string,string>{{a,"new"}},new System.Collections.Generic.Dictionary<string,string>{{source,WorldOpsStorage.FileHash(source)}},new[]{root});
                File.WriteAllText(source,"changed");Assert.Throws<IOException>(()=>WorldOpsTransaction.Commit(journal));Assert.AreEqual("old",File.ReadAllText(a));
                File.WriteAllText(source,"old source");File.WriteAllText(j.writes[0].prepared,"broken");Assert.Throws<IOException>(()=>WorldOpsTransaction.Commit(journal));Assert.AreEqual("old",File.ReadAllText(a));
            }
            finally{Directory.Delete(root,true);}
        }
        [Test] public void ProjectedPrimaryGeometryCreatesClippingFinding()
        {
            var d=Document();d.art.primaryLandmarkInstanceId=d.art.instances[0].id;
            var go=new GameObject("WorldOps camera fixture");var camera=go.AddComponent<Camera>();
            try
            {
                camera.orthographic=true;camera.orthographicSize=2;camera.transform.position=new Vector3(0,10,-10);camera.transform.LookAt(Vector3.zero);
                Assert.IsTrue(WorldOpsVisibility.Project(camera,d.art,new Rect(0,0,1,1),new WorldCriteria()).Any(f=>f.code=="landmark.clipped"));
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test] public void ArbitraryCommandsAndUnsafePathsAreRejected()
        {
            Assert.Throws<ArgumentException>(()=>WorldOpsOperations.Validate(new WorldRecipe{id="bad",operations=new[]{new WorldOperation{id="x",kind="csharp.execute"}}}));
            Assert.Throws<InvalidDataException>(()=>WorldOpsStorage.SafeChild(Path.GetTempPath(),"../outside"));
        }
    }
}
