using System;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class BiomeSurfaceTests
    {
        static LevelData Level(string season,string biome="meadow",float moisture=.2f)
            =>new LevelData{width=6,height=6,decorSeed=91,noise=new[]{new[]{0,2}},environment=new EnvironmentCompositionData{seasonId=season,biomeId=biome,moisture=moisture}};
        [Test] public void SurfacesFollowSeasonAndWetWeatherWithoutSummerInsectsInWinter()
        {
            Assert.AreEqual("sfx.surface.snow",new CampBiomeProfile(Level("winter"),1).SurfaceKey);
            Assert.AreEqual("sfx.surface.leaves",new CampBiomeProfile(Level("autumn")).SurfaceKey);
            Assert.AreEqual("sfx.surface.mud",new CampBiomeProfile(Level("autumn"),.6f).SurfaceKey);
            Assert.AreEqual("sfx.surface.soil",new CampBiomeProfile(Level("summer","pines")).SurfaceKey);
            Assert.AreEqual("sfx.surface.grass",new CampBiomeProfile(Level("spring")).SurfaceKey);
            Assert.AreEqual("sfx.surface.mud",new CampBiomeProfile(Level("summer",moisture:.8f)).SurfaceKey);
            Assert.AreEqual(0,new CampBiomeProfile(Level("winter")).Crickets);
            Assert.Less(new CampBiomeProfile(Level("winter")).Birds,new CampBiomeProfile(Level("spring")).Birds);
        }
        [Test] public void EveryBiomeBedAndSurfaceHasActualMobileImportedClips()
        {
            var catalog=QuietCampAudioCatalog.Load();
            foreach(var name in new[]{"forest","meadow","autumn","winter","water"})
            {
                Assert.IsTrue(catalog.TryGet("ambience.biome."+name,out var sound),name);
                Assert.NotNull(sound.Clip);Assert.IsTrue(sound.Loop);
                Assert.AreEqual(48000,sound.Clip.frequency);Assert.AreEqual(AudioClipLoadType.Streaming,sound.Clip.loadType);
                Assert.AreEqual(1,sound.MaxSimultaneous);Assert.AreEqual(0,sound.DopplerLevel);
            }
            foreach(var name in new[]{"snow","leaves","mud","soil","grass"})
            {
                Assert.IsTrue(catalog.TryGet("sfx.surface."+name,out var sound),name);
                Assert.AreEqual(3,sound.Variants.Length);Assert.IsFalse(sound.Loop);
                foreach(var clip in sound.Variants){Assert.NotNull(clip);Assert.AreEqual(48000,clip.frequency);Assert.AreEqual(1,clip.channels);}
            }
        }
        [Test] public void FireHeatIsLocalAndSnowDunesRemainBoundedAndContinuous()
        {
            var level=Level("winter");var source=BoardMath.CellCenterWorld(level,new Cell(0,2));
            Assert.AreEqual(1,CampGroundDetails.HeatAt(level,source));
            Assert.AreEqual(0,CampGroundDetails.HeatAt(level,source+Vector3.right*3));
            float previous=CampGroundDetails.HeatAt(level,source);
            for(int i=1;i<100;i++)
            {
                float next=CampGroundDetails.HeatAt(level,source+Vector3.left*i*.02f);
                Assert.Less(Mathf.Abs(next-previous),.04f);previous=next;
            }
            level.entry=new[]{0,0};level.blocked=Array.Empty<int[]>();
            var season=SeasonProfile.For(level);
            for(int i=0;i<100;i++)Assert.That(season.SnowDepth(level,new Vector3(8+i*.2f,0,12)),Is.InRange(.0f,2.16f));
        }
    }
}
