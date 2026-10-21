using System;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation.World;
using UnityEngine;

namespace QuietCamp.Tests
{
    public sealed class SeasonModelChannelTests
    {
        static CampArtDefinition.Model Model(bool optionalChannels)
        {
            var vertices=new float[36];
            for(int triangle=0;triangle<4;triangle++)
                Array.Copy(new float[]{triangle*3,0,0,triangle*3,1,0,triangle*3+1,0,1},0,vertices,triangle*9,9);
            return new CampArtDefinition.Model
            {
                id="channel-contract",seasonalRole="deciduous",seasonalLeafTriangles=new[]{1,3},
                vertices=vertices,normals=Enumerable.Range(0,36).Select(i=>i*.01f).ToArray(),
                colors=Enumerable.Repeat(0x748c49,12).ToArray(),
                uv=optionalChannels?Enumerable.Range(0,24).Select(i=>i*.01f).ToArray():null,
                tangents=optionalChannels?Enumerable.Range(0,48).Select(i=>i*.02f).ToArray():null,
                mossCoverage=optionalChannels?Enumerable.Range(0,12).Select(i=>i*.03f).ToArray():null,
                weatherExposure=optionalChannels?Enumerable.Range(0,12).Select(i=>i*.04f).ToArray():null
            };
        }
        static void Channel<T>(T[] before,T[] after,int[] triangles,int width)
        {
            if(before==null){Assert.IsNull(after);return;}
            CollectionAssert.AreEqual(triangles.SelectMany(t=>before.Skip(t*width).Take(width)).ToArray(),after);
            Assert.AreNotSame(before,after,"Filtered channels must not alias authored data.");
        }
        [TestCase("winter",false)] [TestCase("winter",true)]
        [TestCase("autumn",false)] [TestCase("autumn",true)]
        public void FilteringPreservesEveryVertexChannelAndRemapsLeaves(string season,bool optionalChannels)
        {
            var source=Model(optionalChannels);
            var original=source.vertices.ToArray();
            var preset=SeasonAtmosphereCatalog.Copy(SeasonAtmosphereCatalog.Get(season=="winter"?"winter_snowfall":"autumn_golden"));
            preset.leafLoss=.5f;
            var resolved=SeasonModelAppearance.Resolve(source,preset);
            var kept=season=="winter"?new[]{0,2}:new[]{0,2,3};
            Channel(source.vertices,resolved.vertices,kept,9);
            Channel(source.normals,resolved.normals,kept,9);
            Channel(source.uv,resolved.uv,kept,6);
            Channel(source.tangents,resolved.tangents,kept,12);
            Channel(source.mossCoverage,resolved.mossCoverage,kept,3);
            Channel(source.weatherExposure,resolved.weatherExposure,kept,3);
            Assert.AreEqual(kept.Length*3,resolved.colors.Length);
            CollectionAssert.AreEqual(season=="winter"?Array.Empty<int>():new[]{2},resolved.seasonalLeafTriangles);
            CollectionAssert.AreEqual(original,source.vertices);
            CollectionAssert.AreEqual(new[]{1,3},source.seasonalLeafTriangles);
        }
        [Test] public void EmptyWinterFlowerKeepsPresentAndAbsentChannelSemantics()
        {
            var source=Model(true);source.seasonalRole="flower";source.weatherExposure=null;
            var resolved=SeasonModelAppearance.Resolve(source,SeasonAtmosphereCatalog.Get("winter_snowfall"));
            Assert.IsEmpty(resolved.vertices);Assert.IsEmpty(resolved.normals);Assert.IsEmpty(resolved.colors);
            Assert.IsEmpty(resolved.uv);Assert.IsEmpty(resolved.tangents);Assert.IsEmpty(resolved.mossCoverage);
            Assert.IsEmpty(resolved.seasonalLeafTriangles);Assert.IsNull(resolved.weatherExposure);
            Assert.AreEqual(36,source.vertices.Length);
        }
        [Test] public void WeatherSharesOnlyIdenticalParticleImagesAndRetainsIndependentMaterials()
        {
            var root=new GameObject("Particle resource contract");var cameraObject=new GameObject("Resource QA camera");
            try
            {
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;CameraFitter.Configure(camera);
                var art=new CampArtDefinition{width=6,height=6,streamCentre=100,
                    models=Array.Empty<CampArtDefinition.Model>(),instances=Array.Empty<CampArtDefinition.Instance>()};
                var view=new GameObject("Resource QA weather").AddComponent<SeasonAtmosphereView>();
                view.Configure(SeasonAtmosphereCatalog.Get("winter_snowfall"),art,camera,root.transform);
                var materials=root.GetComponentsInChildren<ParticleSystemRenderer>().Select(r=>r.sharedMaterial).ToArray();
                var textures=materials.Select(m=>m.mainTexture).OfType<Texture2D>().Where(t=>t.width==32&&t.height==32).Distinct().ToArray();
                Assert.AreEqual(2,textures.Length,"Sharp and soft particles each own one immutable image.");
                Assert.Greater(materials.Distinct().Count(),textures.Length,"Individual systems keep independent material parameters.");
                foreach(var texture in textures)
                {
                    var pixels=texture.GetPixels();
                    bool soft=pixels[8*32+16].a<.9f;
                    for(int y=0;y<32;y++)for(int x=0;x<32;x++)
                    {
                        float r=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;
                        float alpha=soft?Mathf.Clamp01(1-r*r):Mathf.Clamp01((1-r)*4);
                        Assert.AreEqual(alpha,pixels[y*32+x].a,1f/255,"Particle image changed.");
                    }
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(cameraObject);}
        }
    }
}
