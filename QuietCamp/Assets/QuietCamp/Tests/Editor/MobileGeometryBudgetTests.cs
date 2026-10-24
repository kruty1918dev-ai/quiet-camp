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
        [Test] public void MenuAndGameplayProduceTheSamePhysicalTentContactsRegardlessOfConstructionOrder()
        {
            var level=LevelLoader.Load("QC001");var art=CampArtDefinition.Load(level);
            var earlyRoot=new GameObject("Menu contact order");var lateRoot=new GameObject("Gameplay contact order");
            var earlyMaterial=new Material(Resources.Load<Shader>("QuietCamp/CampArtLit"));var lateMaterial=new Material(earlyMaterial);
            try
            {
                var early=earlyRoot.AddComponent<CampGroundDetails>();early.Configure(level,earlyMaterial,art);
                early.Rebuild(level.witness);early.Bind(null);
                var late=lateRoot.AddComponent<CampGroundDetails>();late.Configure(level,lateMaterial,art);late.Bind(null);
                var staticPixels=((Texture2D)lateMaterial.GetTexture("_GroundContact")).GetPixels32();
                late.Rebuild(level.witness);
                var expected=((Texture2D)lateMaterial.GetTexture("_GroundContact")).GetPixels32();
                CollectionAssert.AreEqual(expected,((Texture2D)earlyMaterial.GetTexture("_GroundContact")).GetPixels32());
                Assert.IsTrue(expected.Where((p,i)=>p.r>staticPixels[i].r).Any(),"Committed tents did not contribute their source geometry.");
                late.Rebuild(Array.Empty<QuietCamp.Domain.Placement>());
                CollectionAssert.AreEqual(staticPixels,((Texture2D)lateMaterial.GetTexture("_GroundContact")).GetPixels32(),"Undo retained a removed tent footprint.");
            }
            finally
            {UnityEngine.Object.DestroyImmediate(earlyRoot);UnityEngine.Object.DestroyImmediate(lateRoot);UnityEngine.Object.DestroyImmediate(earlyMaterial);UnityEngine.Object.DestroyImmediate(lateMaterial);}
        }
        [Test] public void DisplayedSurfaceUsesTrianglesInsteadOfTheUnchangedBilinearHeightField()
        {
            var landscape=new FrozenLandscape{originX=0,originZ=0,size=2,resolution=2,heights=new[]{0f,0,0,2},meshes=new[]{new LandscapeMesh{
                vertices=new[]{0f,0,0, 2,0,0, 0,0,2, 2,2,2},triangles=new[]{0,2,1,1,2,3}}}};
            var surface=new CampTerrainSurface(landscape);
            Assert.AreEqual(.5f,landscape.SampleHeight(1,1));Assert.AreEqual(0,surface.Sample(1,1));
            var art=new CampArtDefinition{landscape=landscape};
            var item=new CampArtDefinition.Instance{id="attached",x=1,z=1,y=.7f,surfaceAttached=true};
            Assert.AreEqual(.2f,surface.Position(art,item).y,.0001f);
            item.surfaceAttached=false;Assert.AreEqual(.7f,surface.Position(art,item).y,.0001f);
            Assert.AreEqual(.7f,item.y);Assert.AreEqual(.5f,landscape.SampleHeight(1,1));
        }
        [Test] public void ContactOcclusionUsesTheFootprintAndLeavesElevatedOpeningsClear()
        {
            var field=new CampGroundContactField(new Rect(-2,-2,4,4),64,(x,z)=>0);
            field.Triangle(new Vector3(-1,1,-1),new Vector3(1,1,-1),new Vector3(0,1,1));
            Assert.IsTrue(field.Pixels().All(c=>c.r==0),"An elevated roof painted a ground stain.");
            field.Triangle(new Vector3(-1,.01f,-1),new Vector3(-.7f,.01f,-1),new Vector3(-1,.01f,-.7f));
            var pixels=field.Pixels();Assert.Greater(pixels[16*64+16].r,200);Assert.AreEqual(0,pixels[32*64+32].r,"Open space between supports was filled.");
            field.Clear();Assert.IsTrue(field.Pixels().All(c=>c.r==0),"Removed tent contact survived undo.");
        }
        [Test] public void MenuTentsInheritTheirInactiveBoardBudgetBeforeArtViewExists()
        {
            var root=new GameObject("Inactive menu background");root.SetActive(false);
            try
            {
                var grid=new GameObject("Grid");grid.transform.SetParent(root.transform);var ground=grid.AddComponent<CampGroundDetails>();
                var tents=new GameObject("Tents");tents.transform.SetParent(root.transform);var tent=new GameObject("Tent");tent.transform.SetParent(tents.transform);
                var art=typeof(CampGroundDetails).GetField("_art",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                var check=typeof(TentCloth).GetMethod("UsesGeometryBudget",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
                Assert.IsNull(root.GetComponentInChildren<CampArtView>(true));
                art.SetValue(ground,new CampArtDefinition{mobileGeometryBudget=true});Assert.IsTrue((bool)check.Invoke(null,new object[]{tent.transform}));
                art.SetValue(ground,new CampArtDefinition());Assert.IsFalse((bool)check.Invoke(null,new object[]{tent.transform}));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [TestCase("tent_smallOpen")] [TestCase("tent_detailedOpen")]
        public void BudgetedFabricRetainsSourceAndRigidGeometryAndSharesItsOwnCache(string id)
        {
            var prefab=AssetCatalog.Load().Prefab(id);var filter=prefab.GetComponentInChildren<MeshFilter>();
            var source=filter.sharedMesh;var materials=filter.GetComponent<MeshRenderer>().sharedMaterials;
            ulong mask=0;for(int i=0;i<materials.Length;i++)
            {string name=materials[i].name.ToLowerInvariant();if(name.Contains("red")||name.Contains("yellow")||name.Contains("fabric"))mask|=1UL<<i;}
            var type=typeof(TentCloth).Assembly.GetType("QuietCamp.Presentation.World.FabricGeometry");
            var acquire=type.GetMethod("Acquire");var release=type.GetMethod("Release");var meshProperty=type.GetProperty("Mesh");
            object budget=null,shared=null,rich=null;int cached=TentCloth.CachedGeometryCount;var vertices=source.vertices;var triangles=source.triangles;
            try
            {
                budget=acquire.Invoke(null,new object[]{source,mask,1});shared=acquire.Invoke(null,new object[]{source,mask,1});rich=acquire.Invoke(null,new object[]{source,mask,2});
                var reduced=(Mesh)meshProperty.GetValue(budget);var detailed=(Mesh)meshProperty.GetValue(rich);
                Assert.AreSame(reduced,meshProperty.GetValue(shared));Assert.AreNotSame(reduced,detailed);
                Assert.Less(reduced.triangles.Length,detailed.triangles.Length);
                CollectionAssert.AreEqual(vertices,reduced.vertices.Take(vertices.Length));
                for(int slot=0;slot<source.subMeshCount;slot++)if((mask&(1UL<<slot))==0)
                    CollectionAssert.AreEqual(source.GetTriangles(slot),reduced.GetTriangles(slot),"Rigid poles/pegs changed.");
                CollectionAssert.AreEqual(vertices,source.vertices);CollectionAssert.AreEqual(triangles,source.triangles);
            }
            finally{if(budget!=null)release.Invoke(budget,null);if(shared!=null)release.Invoke(shared,null);if(rich!=null)release.Invoke(rich,null);}
            Assert.AreEqual(cached,TentCloth.CachedGeometryCount);
        }
        [Test] public void MarginResamplingRetainsBothBoundariesAndRejectsCutTopology()
        {
            var text=Resources.Load<TextAsset>("QuietCamp/ArtCompositions/gen_qc_camp_60");
            var art=CampArtDefinition.ReadDocument(text.text,out _);var source=art.landscape.meshes.Single(m=>m.name=="Surrounding ground margins");
            string before=JsonConvert.SerializeObject(source);int n=art.landscape.resolution;
            var reduced=CampGeometryBudget.SimplifyMargins(source,n,4);Assert.AreEqual(256,reduced.triangles.Length/3);
            int width=(n-1)/4+1;
            for(int side=0;side<4;side++)for(int row=0;row<2;row++)for(int col=0;col<width;col++)for(int channel=0;channel<3;channel++)
                Assert.AreEqual(source.vertices[(side*n*5+row*n*4+col*4)*3+channel],reduced.vertices[(side*width*2+row*width+col)*3+channel]);
            Assert.AreEqual(before,JsonConvert.SerializeObject(source));
            var cut=JsonConvert.DeserializeObject<LandscapeMesh>(before);cut.triangles=cut.triangles.Skip(3).ToArray();
            Assert.AreSame(cut,CampGeometryBudget.SimplifyMargins(cut,n,4));
            var bounds=CampMeadowTufts.GroundBounds(art);Assert.AreEqual(-50,bounds.minX);Assert.AreEqual(50,bounds.maxX);
        }
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
