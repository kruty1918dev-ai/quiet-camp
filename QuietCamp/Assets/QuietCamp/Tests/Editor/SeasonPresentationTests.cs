using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.UI;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class SeasonPresentationTests
    {
        static LevelData Winter()=>new LevelData{width=6,height=5,decorSeed=31,order=23,ruleVersion=2,
            entry=new[]{0,0},blocked=Array.Empty<int[]>(),environment=new EnvironmentCompositionData{seasonId="winter"}};
        static bool Green(Color c)=>c.g>c.r*1.03f&&c.g>c.b*1.03f;
        [Test] public void EarlyAndLateAutumnKeepDifferentCanopiesAndIncreasingLitter()
        {
            var early=SeasonProfile.For("autumn",16);var late=SeasonProfile.For("autumn",20);
            Assert.Greater(early.GrassWeight,late.GrassWeight*3);
            Assert.Greater(late.LeafLitter,early.LeafLitter*2);
            Assert.IsTrue(Green(early.Palette.Plant(Color.green,0)));
            for(int i=0;i<=10;i++)Assert.IsFalse(Green(late.Palette.Plant(Color.green,i/10f)));
            int earlyBare=0,lateBare=0;
            for(int i=0;i<100;i++)
            {
                var point=new Vector3(i*.37f,0,i*.17f);
                if(early.BareTree(point,5))earlyBare++;if(late.BareTree(point,5))lateBare++;
            }
            Assert.Greater(lateBare,earlyBare+15,"Leaf shedding must advance across the chapter");
        }
        [Test] public void WinterKeepsGreenOnlyUnderSnowOnEvergreenTrees()
        {
            var winter=SeasonProfile.For("winter",23);Assert.AreEqual(0,winter.GrassWeight);Assert.AreEqual(0,winter.ShrubWeight);Assert.AreEqual(0,winter.PollenWeight);
            for(int i=0;i<=10;i++)
            {
                Assert.IsFalse(Green(winter.Palette.Plant(Color.green,i/10f)));
                Assert.IsTrue(Green(winter.Palette.Plant(Color.green,i/10f,true)));
                Assert.IsTrue(winter.BareTree(new Vector3(i,0,3),71));
                Assert.IsFalse(winter.BareTree(new Vector3(i,0,3),71,true),"Conifers keep their snow-coated needles");
            }
        }
        [Test] public void DeepSnowPreservesPuzzleWalkingNetworkAndWater()
        {
            var level=Winter();var season=SeasonProfile.For(level);
            Assert.AreEqual(0,season.SnowDepth(level,Vector3.zero));
            foreach(var p in new[]{new Vector3(-3,0,-2.5f),new Vector3(3,0,2.5f)})Assert.AreEqual(0,season.SnowDepth(level,p));
            Assert.AreEqual(0,season.SnowDepth(level,CampTrail.Centre(level,new Cell(0,0),7)));
            level.exteriorWalkable=new[]{new[]{6,2},new[]{7,2}};
            Assert.AreEqual(0,season.SnowDepth(level,BoardMath.CellCenterWorld(level,new Cell(7,2))));
            var far=new Vector3(15,0,17);Assert.That(season.SnowDepth(level,far),Is.InRange(.79f,2.16f));
            level.environment.shore=new ShorelineData{side="right",offset=9,width=3,seed=14};
            Assert.AreEqual(0,season.SnowDepth(level,ShorelineGeometry.Point(level.environment.shore,5,.5f)));
        }
        [Test] public void SnowBanksAreContinuousBesideTrailsAndWater()
        {
            var level=Winter();var season=SeasonProfile.For(level);var access=new Cell(0,0);
            var trail=CampTrail.Centre(level,access,8);var side=Vector3.Cross(Vector3.up,CampTrail.Outward(level,access));
            void Scan(Vector3 origin,Vector3 direction)
            {
                float previous=season.SnowDepth(level,origin);
                for(int i=1;i<=350;i++)
                {
                    float next=season.SnowDepth(level,origin+direction*(i*.02f));
                    Assert.Less(Mathf.Abs(next-previous),.045f,"Snow forms a wall beside a traversable surface");previous=next;
                }
            }
            Scan(trail,side);Scan(trail,-side);
            level.environment.shore=new ShorelineData{side="right",offset=9,width=3,seed=14};
            Scan(ShorelineGeometry.Point(level.environment.shore,5,.5f),Vector3.right);
        }
        [Test] public void GroundcoverSilhouettesFitTheCellScaleAndTheFontMatchesItsAtlas()
        {
            var library=CozyVegetationLibrary.Load();Assert.NotNull(library);
            foreach(var plant in library.plants)
            {
                float scale=CozyVegetationLibrary.GroundcoverScale(plant.kind,plant.mesh.bounds,.8f);
                float span=Mathf.Max(plant.mesh.bounds.size.x,plant.mesh.bounds.size.z)*scale;
                Assert.Greater(scale,0);
                Assert.Less(span,plant.kind==CozyVegetationLibrary.PlantKind.Shrub?1f:plant.kind==CozyVegetationLibrary.PlantKind.Leaf?.5f:.4f,plant.id);
            }
            var font=Resources.Load<TMPro.TMP_FontAsset>("Fonts/DejaVuSans SDF");Assert.NotNull(font);
            Assert.AreEqual(font.atlasPadding+1,font.material.GetFloat("_GradientScale"));
        }
        [Test] public void SeasonalPresentationAndRoadmapDoNotChangeCampaignOrRuleEvidence()
        {
            var catalog=AtmosphereCatalog.Load();
            foreach(var summary in CampContent.Summaries)
            {
                var level=LevelLoader.Load(summary.id);string saved=JsonConvert.SerializeObject(level);
                var profile=SeasonProfile.For(level);var mapProfile=SeasonProfile.For(summary);
                Assert.AreEqual(profile.Id,mapProfile.Id,summary.id);Assert.AreEqual(profile.Progress,mapProfile.Progress,summary.id);
                Assert.AreEqual(profile.Palette.GrassLight,mapProfile.Palette.GrassLight,summary.id);
                var scene=RoadmapSceneGenerator.Generate(summary,catalog);
                Assert.AreEqual(profile.Palette.CanopyLight,scene.Palette.CanopyLight,summary.id);
                Assert.IsTrue(RuleEvaluator.Evaluate(level,level.witness).IsSolved,summary.id);
                Assert.AreEqual(saved,JsonConvert.SerializeObject(level),"Rendering must not rehash or edit "+summary.id);
            }
        }
        [Test] public void BareBranchesRetainOriginalTrunkAndHaveBoundedDeterministicGeometry()
        {
            var source=new[]{new Vector3(-.08f,0,0),new Vector3(.08f,0,0),new Vector3(0,.8f,0),new Vector3(-.8f,1.4f,0),new Vector3(.8f,1.4f,0),new Vector3(0,2,0)};
            var normals=Enumerable.Repeat(Vector3.forward,6).ToArray();
            var brown=new Color(.4f,.27f,.19f);var colors=new[]{brown,brown,brown,Color.green,Color.green,Color.green};
            var indices=Enumerable.Range(0,6).ToArray();var original=source.ToArray();
            var tree=SeasonalTreeGeometry.Bare(source,normals,indices,colors);
            CollectionAssert.AreEqual(original,source);Assert.AreEqual(2,tree.Bounds.max.y,.001f);Assert.AreEqual(0,tree.Bounds.min.y,.001f);
            CollectionAssert.AreEqual(source.Take(3),tree.Vertices.Take(3));Assert.IsFalse(tree.Colors.Any(Green));
            Assert.Less(tree.Indices.Length/3,350);Assert.AreEqual(tree.Vertices.Length,tree.Normals.Length);
            CollectionAssert.AreEqual(tree.Vertices,SeasonalTreeGeometry.Bare(source,normals,indices,colors).Vertices);
            foreach(var n in tree.Normals)Assert.That(n.magnitude,Is.InRange(.99f,1.01f));
        }
        [Test] public void RoadmapKeepsTreeSpeciesAndRootsAcrossSeasonsWithoutNativeMeshCopies()
        {
            var summary=new LevelSummary{id="season-test",number=18,width=6,height=5,decorSeed=13,entry=new[]{0,0},lighting="noon",
                environment=new EnvironmentCompositionData{seasonId="summer",biomeId="meadow",treeDensity=.7f}};
            var catalog=AtmosphereCatalog.Load();var library=RoadmapModelLibrary.Load();
            var summer=RoadmapSceneGenerator.Generate(summary,catalog);summary.environment.seasonId="winter";
            var winter=RoadmapSceneGenerator.Generate(summary,catalog);
            var a=summer.Props.Where(p=>p.Asset.StartsWith("tree")).ToArray();var b=winter.Props.Where(p=>p.Asset.StartsWith("tree")).ToArray();
            Assert.AreEqual(a.Length,b.Length);Assert.Greater(a.Length,3);
            for(int i=0;i<a.Length;i++)
            {
                Assert.AreEqual(a[i].Asset,b[i].Asset);Assert.AreEqual(a[i].Position,b[i].Position);Assert.AreEqual(a[i].Height,b[i].Height);
                if(!b[i].Asset.Contains("pine"))Assert.AreSame(library.BareTree(b[i].Asset),b[i].Geometry);
            }
            Assert.IsFalse(winter.Props.Any(p=>p.Asset.StartsWith("flower")||p.Asset.StartsWith("plant")));
        }
    }
}
