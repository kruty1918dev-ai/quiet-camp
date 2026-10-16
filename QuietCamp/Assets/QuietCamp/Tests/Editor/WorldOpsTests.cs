using System;
using System.Collections;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Linq;
using QuietCamp.Editor.LevelAuthoring.WorldOps;
using NUnit.Framework;
using QuietCamp.Editor.LevelAuthoring;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.TestTools;

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
        [Test] public void ApplyDocumentPreservesNewNativePlacementsAndHonoursExplicitRemoval()
        {
            var d=Document();
            using(var workspace=new LevelAuthoringWorkspace(d))
            {
                var extra=workspace.Spawn(new CampArtDefinition.Instance{id="test/new",modelId=d.art.models[0].id,x=9,z=9,scale=1,surfaceAttached=false},false);
                var next=d.EditingCopy();next.art.exposure+=.01f;workspace.ApplyDocument(next);
                Assert.IsTrue(extra!=null);Assert.IsTrue(workspace.Document.art.instances.Any(p=>p.id=="test/new"));
                var removed=workspace.Document.EditingCopy();removed.art.instances=removed.art.instances.Where(p=>p.id!="test/new").ToArray();
                workspace.ApplyDocument(removed);Assert.IsTrue(extra==null);Assert.AreEqual(1,workspace.SceneryCount);
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
            var mesh=d.art.models.Last();var a=new Vector3(mesh.vertices[0],mesh.vertices[1],mesh.vertices[2]);var b=new Vector3(mesh.vertices[3],mesh.vertices[4],mesh.vertices[5]);var c=new Vector3(mesh.vertices[6],mesh.vertices[7],mesh.vertices[8]);
            Assert.Greater(Vector3.Cross(b-a,c-a).y,0,"Path winding must agree with its upward normal.");
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
                File.WriteAllText(a,"later user edit");Assert.Throws<IOException>(()=>WorldOpsTransaction.Commit(journal));Assert.Throws<IOException>(()=>WorldOpsTransaction.Rollback(journal));Assert.AreEqual("new-b",File.ReadAllText(b));
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
        [UnityTest] public IEnumerator DiagnosticMaskDoesNotOccludeItsOwnCombinedMeshButDetectsARealOccluder()
        {
            var root=new GameObject("WorldOps mask fixture");var cameraObject=new GameObject("WorldOps mask camera");
            var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.orthographic=true;camera.orthographicSize=2;
            var model=GameObject.CreatePrimitive(PrimitiveType.Cube);model.transform.SetParent(root.transform);model.layer=30;
            var primary=model.GetComponent<MeshFilter>().sharedMesh;
            var matrix=Matrix4x4.TRS(new Vector3(12.183f,.054f,8.631f),Quaternion.Euler(13,37,4),new Vector3(1.6f,1.2f,.8f));
            var combined=new Mesh();combined.CombineMeshes(new[]{new CombineInstance{mesh=primary,transform=matrix}},true,true);
            model.GetComponent<MeshFilter>().sharedMesh=combined;
            var target=matrix.MultiplyPoint3x4(Vector3.zero);camera.transform.position=target+new Vector3(0,3,-8);camera.transform.LookAt(target);
            var shader=Shader.Find("Hidden/QuietCamp/WorldOpsMask");Assert.IsNotNull(shader);
            var material=new Material(shader);var rt=new RenderTexture(128,128,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);rt.Create();
            var pixels=new Texture2D(128,128,TextureFormat.RGBA32,false,true);var previous=RenderTexture.active;
            Color32[] Read(bool isolated)
            {
                WorldOpsVisibility.RenderMask(camera,primary,matrix,material,rt,isolated);RenderTexture.active=rt;
                pixels.ReadPixels(new Rect(0,0,128,128),0,0);pixels.Apply();
                return pixels.GetPixels32();
            }
            try
            {
                yield return null; // Let Unity publish newly created renderer transforms.
                var isolated=Read(true);int potential=isolated.Count(p=>p.r>127);Assert.Greater(potential,100);
                Assert.That(WorldOpsVisibility.ComposeVisibility(Read(false),isolated)/(float)potential,Is.GreaterThan(.98f),"A batched copy must not hide the primary itself.");
                var obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube);obstruction.transform.SetParent(root.transform);obstruction.layer=30;
                obstruction.transform.position=target-camera.transform.forward*2;obstruction.transform.rotation=camera.transform.rotation;obstruction.transform.localScale=new Vector3(4,4,.1f);
                yield return null;
                var scene=Read(false);var centre=Array.FindIndex(isolated,p=>p.r>127);
                var description=$"A separate solid in front must still hide it. Depth at first silhouette pixel: scene={scene[centre].g*256+scene[centre].b}, primary={isolated[centre].g*256+isolated[centre].b}.";
                Directory.CreateDirectory("Temp/ai");pixels.SetPixels32(scene);pixels.Apply();File.WriteAllBytes("Temp/ai/worldops-mask-obstruction.png",pixels.EncodeToPNG());
                Assert.That(WorldOpsVisibility.ComposeVisibility(scene,isolated)/(float)potential,Is.LessThan(.1f),description);
            }
            finally
            {
                RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);UnityEngine.Object.DestroyImmediate(combined);
            }
        }
        [Test] public void RepeatedVisualDefectsStopAfterTwoUnimprovedRoundsAndResetOnImprovement()
        {
            var clipping=new ValidationFinding{code="landmark.clipped",objectId="ruin",profile="portrait",measured=80};
            var occlusion=new ValidationFinding{code="landmark.occluded",objectId="ruin",profile="portrait",measured=.5f,limit=.75f};
            var item=new WorldLevelRun{previousReviewFindings=new System.Collections.Generic.List<ValidationFinding>{clipping,occlusion}};
            var current=LevelAuthoringDocument.Copy(item.previousReviewFindings);current[0].measured=40;current[1].measured=.6f;
            Assert.IsFalse(WorldOpsService.RecordReviewProgress(item,current));Assert.IsTrue(item.visualStalls.Values.All(n=>n==0));
            item.previousReviewFindings=LevelAuthoringDocument.Copy(current);
            Assert.IsFalse(WorldOpsService.RecordReviewProgress(item,current));Assert.IsTrue(WorldOpsService.RecordReviewProgress(item,current));
            current[0].measured=20;current[1].measured=.7f;
            Assert.IsFalse(WorldOpsService.RecordReviewProgress(item,current));Assert.IsTrue(item.visualStalls.Values.All(n=>n==0));
            Assert.IsFalse(WorldOpsService.RecordReviewProgress(item,new System.Collections.Generic.List<ValidationFinding>()));Assert.AreEqual(0,item.visualStalls.Count);
        }
        [Test] public void ArbitraryCommandsAndUnsafePathsAreRejected()
        {
            Assert.Throws<ArgumentException>(()=>WorldOpsOperations.Validate(new WorldRecipe{id="bad",operations=new[]{new WorldOperation{id="x",kind="csharp.execute"}}}));
            Assert.Throws<InvalidDataException>(()=>WorldOpsStorage.SafeChild(Path.GetTempPath(),"../outside"));
        }
        [Test] public void DamagedReviewPacketAndChangedFrameAreRejected()
        {
            string root=Path.Combine(Path.GetTempPath(),"qc-worldops-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            try
            {
                string frame=Path.Combine(root,"gameplay.png"),receipt=Path.Combine(root,"capture.json");File.WriteAllText(frame,"frame");File.WriteAllText(receipt,"receipt");
                var item=new WorldLevelRun{review=new ReviewPacket{frames=new System.Collections.Generic.List<WorldFrame>{new WorldFrame{path=frame,sha256=WorldOpsStorage.FileHash(frame),evidence=new System.Collections.Generic.Dictionary<string,string>{{receipt,WorldOpsStorage.FileHash(receipt)}}}}}};
                item.reviewHash=WorldOpsStorage.Digest(item.review);WorldOpsService.VerifyEvidence(item);
                File.WriteAllText(frame,"changed");Assert.Throws<InvalidDataException>(()=>WorldOpsService.VerifyEvidence(item));
                File.WriteAllText(frame,"frame");File.WriteAllText(receipt,"broken");Assert.Throws<IOException>(()=>WorldOpsService.VerifyEvidence(item));
                File.WriteAllText(receipt,"receipt");item.review.inputHash="changed";Assert.Throws<InvalidDataException>(()=>WorldOpsService.VerifyEvidence(item));
            }
            finally{Directory.Delete(root,true);}
        }
        [Test] public void OnePoseEditDoesNotRebuildTenThousandPlants()
        {
            var d=Document();var model=LevelAuthoringPalette.Entries.First(e=>e.smallPlant&&e.model!=null).model;
            d.art.models=new[]{LevelAuthoringDocument.Copy(model)};
            d.art.instances=Enumerable.Range(0,10000).Select(i=>new CampArtDefinition.Instance{id="regrowth/perf/"+i,modelId=model.id,x=8+(i%100)*.1f,z=8+(i/100)*.1f,y=0,scale=.4f}).ToArray();
            using(var workspace=new LevelAuthoringWorkspace(d))
            {
                int rebuilds=workspace.FullPlantRebuilds;var next=d.EditingCopy();next.art.instances[5000].x+=.03f;
                var timer=System.Diagnostics.Stopwatch.StartNew();workspace.ApplyDocument(next);timer.Stop();
                Assert.AreEqual(rebuilds,workspace.FullPlantRebuilds);
                Assert.That(workspace.SceneryCount,Is.EqualTo(10000));
                string path=Path.Combine(WorldOpsService.Home,"plant-edit-performance.json");WorldOpsStorage.Json(path,new{plants=10000,changed=1,fullRebuilds=workspace.FullPlantRebuilds-rebuilds,milliseconds=timer.Elapsed.TotalMilliseconds});
            }
        }
        [Test] public void CampaignIdsAreSafeDirectoryNamesWithoutChangingStableIds()
        {
            var run=new WorldRun{directory=Path.Combine(Path.GetTempPath(),"worldops-fixture")};
            Assert.AreEqual("gen_qc_camp_7",Path.GetFileName(WorldOpsService.LevelFolder(run,"gen:qc_camp:7")));
        }
        [Test] public void PlantInWaterAndDisconnectedEntranceAreDetected()
        {
            var d=Document();var model=LevelAuthoringDocument.Copy(LevelAuthoringPalette.Entries.First(e=>e.smallPlant&&e.model!=null).model);
            model.seasonalRole="groundcover";d.art.models=new[]{model};d.art.instances[0].modelId=model.id;
            d.art.landscape=new FrozenLandscape{originX=-20,originZ=-20,size=40,resolution=2,heights=new float[4],meshes=new[]{new LandscapeMesh{name="River",water=true,
                vertices=new[]{7f,0,7,9,0,7,7,0,9,9,0,9},normals=new[]{0f,1,0,0,1,0,0,1,0,0,1,0},uv=new[]{0f,0,1,0,0,1,1,1},colors=Enumerable.Repeat(1f,16).ToArray(),triangles=new[]{0,2,1,1,2,3}}}};
            Assert.IsTrue(WorldOpsGeometry.Validate(d,new WorldCriteria()).Any(f=>f.code=="vegetation.water"));
            d.worldOps=new WorldAuthoringMetadata();d.worldOps.paths.Add(new WorldPath{id="broken",points=new[]{new[]{12f,12f},new[]{14f,12f}},endObjectId="missing-door"});
            Assert.IsTrue(WorldOpsGeometry.Validate(d,new WorldCriteria()).Any(f=>f.code=="path.disconnected"));
        }
        [Test] public void DeclaredObjectSupportMustActuallyReachTheProp()
        {
            var d=Document();var bottom=d.art.instances[0];WorldOpsGeometry.Ground(d,bottom,d.art.models[0],new WorldObjectRecord());
            var top=LevelAuthoringDocument.Copy(bottom);top.id="top";top.y=5;top.scale=.2f;d.art.instances=new[]{bottom,top};
            var support=WorldOpsOperations.Record(d,top.id,true);support.support="object:"+bottom.id;
            WorldOpsGeometry.Ground(d,top,d.art.models[0],support);
            Assert.IsFalse(WorldOpsGeometry.Validate(d,new WorldCriteria()).Any(f=>f.objectId==top.id&&f.code=="support.floating"));
            support.support="object:absent";Assert.IsTrue(WorldOpsGeometry.Validate(d,new WorldCriteria()).Any(f=>f.objectId==top.id&&f.code=="support.missing"));
        }
        [Test] public void ReadOnlyAuditCannotRepairOrPublishWithoutFrames()
        {
            var recipe=new WorldRecipe{id="unsafe-audit",capture=false,autoApply=true};Assert.Throws<ArgumentException>(()=>WorldOpsOperations.Validate(recipe));
            recipe.autoApply=false;Assert.Throws<ArgumentException>(()=>WorldOpsOperations.Validate(recipe));
            recipe.criteria.autoRepair=false;Assert.DoesNotThrow(()=>WorldOpsOperations.Validate(recipe));
        }
        [Test] public void RecipeStateExcludesItsOwnLedgerButIncludesPinsAndGeometry()
        {
            var d=Document();d.worldOps=new WorldAuthoringMetadata();var hash=WorldOpsService.RecipeState(d);
            d.worldOps.appliedRecipes["recipe"]=hash;Assert.AreEqual(hash,WorldOpsService.RecipeState(d));
            WorldOpsOperations.Record(d,d.art.instances[0].id,true).pinned=true;Assert.AreNotEqual(hash,WorldOpsService.RecipeState(d));
        }
        [Test] public void NativePrimaryProofUsesSeasonalMeshAndItsActualParentTransform()
        {
            var d=Document();d.art.primaryLandmarkInstanceId=d.art.instances[0].id;d.art.atmosphere=SeasonAtmosphereCatalog.Get("winter_clear");
            var root=new GameObject("WorldOps native parent");root.transform.position=new Vector3(1,.2f,2);
            var camera=new GameObject("WorldOps native camera");camera.AddComponent<Camera>();var view=camera.AddComponent<CampArtView>();
            try
            {
                view.Configure(d.art,d.level,root.transform,AssetCatalog.Load());
                Assert.IsTrue(view.TryGetPrimaryPresentedGeometry(out var mesh,out var matrix));
                var expected=SeasonModelAppearance.Resolve(d.art.models[0],d.art.atmosphere);
                Assert.AreEqual(expected.vertices.Length/3,mesh.vertexCount);
                Assert.That(matrix.MultiplyPoint3x4(Vector3.zero),Is.EqualTo(new Vector3(9,.2f,10)));
            }
            finally{view.Release();UnityEngine.Object.DestroyImmediate(camera);UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void GroupMoveCarriesOwnedObjectsAndRejectsPinnedMembers()
        {
            var d=Document();var child=LevelAuthoringDocument.Copy(d.art.instances[0]);child.id="story/child";child.x+=1;d.art.instances=d.art.instances.Concat(new[]{child}).ToArray();
            var record=WorldOpsOperations.Record(d,child.id,true);record.ensembleId="test/barrel";
            var op=new WorldOperation{id="move",kind="building.move",selector=new WorldSelector{ids=new[]{"test/barrel"}},parameters=JObject.Parse("{\"dx\":2,\"yaw\":90}")};
            WorldOpsOperations.Apply(d,op,1);Assert.AreEqual(10,d.art.instances[0].x);Assert.That(child.x,Is.EqualTo(10).Within(.0001));Assert.That(child.z,Is.EqualTo(7).Within(.0001));
            record.pinned=true;string before=WorldOpsStorage.Digest(d);Assert.Throws<InvalidOperationException>(()=>WorldOpsOperations.Apply(d,op,1));Assert.AreEqual(before,WorldOpsStorage.Digest(d));
        }
        [Test] public void PwbCaptureAssignsStableIdsWithoutDuplicatingTheStrokeAndUsesFrozenGeometry()
        {
            Assert.IsNotNull(PlacementToolIntegration.Controlled,"The installed PWB adapter is required.");
            var d=Document();var pose=d.art.instances[0];d.art.instances=Array.Empty<CampArtDefinition.Instance>();d.art.models[0].vertices[0]+=.2f;
            using(var workspace=new LevelAuthoringWorkspace(d))
            {
                var native=PlacementToolIntegration.Controlled.PlaceBatch(LevelAuthoringPalette.Get(pose.modelId).prefab,workspace.Scenery,workspace.Surface,new[]{pose}).Single();
                workspace.RefreshPlantBatches();Assert.That(native.GetComponent<MeshFilter>().sharedMesh.vertices[0].x,Is.EqualTo(d.art.models[0].vertices[0]).Within(.0001));
                var captured=workspace.Capture(false);Assert.IsNotEmpty(captured.art.instances[0].id);
                workspace.ApplyDocument(captured);Assert.AreEqual(1,workspace.SceneryCount,"Saving a PWB stroke must not create a second native object.");
                UnityEditor.Undo.ClearUndo(native);
            }
        }
        [Test] public void VegetationRedistributionPreservesOwnedOvergrowthUnlessExplicitlySelected()
        {
            var d=Document();var plant=LevelAuthoringPalette.Entries.First(e=>e.smallPlant&&e.model!=null).model;d.art.models=new[]{LevelAuthoringDocument.Copy(plant)};
            var p=d.art.instances[0];p.id="overgrowth/wreck/fern";p.modelId=plant.id;var before=WorldOpsStorage.Digest(p);
            WorldOpsOperations.Apply(d,new WorldOperation{id="scatter",kind="vegetation.redistribute",parameters=JObject.Parse("{\"radius\":1}")},2);
            Assert.AreEqual(before,WorldOpsStorage.Digest(p));
        }
    }
}
