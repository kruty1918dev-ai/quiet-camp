using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    public class CozyParticlePlayModeTests
    {
        [UnityTest]
        public IEnumerator ActualParticleUVsSelectTheIntendedAtlasSilhouettes()
        {
            var root = new GameObject("atlas-regression");
            var camera = root.AddComponent<Camera>(); camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -5);
            var system = new GameObject("particle").AddComponent<ParticleSystem>();
            system.transform.SetParent(root.transform, false); system.transform.localPosition = Vector3.forward * 5;
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main; main.playOnAwake = false; main.startSpeed = 0; main.startLifetime = 10;
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var atlas = CozyParticleAtlas.Create();
            var readable = Read(atlas);
            var mesh = new Mesh();
            try
            {
                for (int tile = 0; tile < 4; tile++)
                {
                    CozyParticleAtlas.Tile(system, tile);
                    system.Clear(); system.Emit(new ParticleSystem.EmitParams { randomSeed = 1918 }, 1);
                    system.Simulate(.01f, false, false);
                    system.GetComponent<ParticleSystemRenderer>().BakeMesh(mesh, camera, true);
                    Assert.Greater(mesh.vertexCount, 0);
                    var uv = mesh.uv.Aggregate(Vector2.zero, (sum, v) => sum + v) / mesh.vertexCount;
                    var expected = new Vector2(tile % 2 == 0 ? .25f : .75f, tile < 2 ? .75f : .25f);
                    Assert.Less(Vector2.Distance(expected, uv), .01f, "WholeSheet frame order changed.");
                    float centre = readable.GetPixelBilinear(uv.x, uv.y).a;
                    if (tile == 3)
                    {
                        Assert.Less(centre, .01f, "The rain ring must have an empty centre.");
                        Assert.Greater(readable.GetPixelBilinear(uv.x + .15f, uv.y).a, .4f);
                    }
                    else Assert.Greater(centre, .4f, "Dust, glow and grass must have an opaque core.");
                    Assert.Less(readable.GetPixelBilinear(uv.x + .245f, uv.y + .245f).a, .01f, "Atlas gutters must remain transparent.");
                }
                Save(readable, "particle-atlas");
            }
            finally { Object.Destroy(root); Object.Destroy(mesh); Object.Destroy(readable); Object.Destroy(atlas); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator SurfaceFadeRendersWithBothOrthographicAndPerspectiveDepth()
        {
            var root = new GameObject("soft-particle-render"); root.transform.position = new Vector3(3000, 0, 0);
            var cameraGo = new GameObject("camera"); cameraGo.transform.SetParent(root.transform, false);
            cameraGo.transform.localPosition = new Vector3(0, 0, -5);
            var camera = cameraGo.AddComponent<Camera>(); camera.enabled = false;
            camera.cullingMask = 1 << 30; camera.nearClipPlane = .1f; camera.farClipPlane = 20;
            camera.orthographicSize = 1; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var surface = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surface.transform.SetParent(root.transform, false); surface.transform.localPosition = Vector3.forward * .05f;
            surface.layer = 30; surface.transform.localScale = Vector3.one * 5;
            var backdrop = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            backdrop.SetColor("_BaseColor", new Color(.1f, .1f, .1f)); backdrop.SetFloat("_Cull", 0);
            surface.GetComponent<Renderer>().sharedMaterial = backdrop;
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.transform.SetParent(root.transform, false); quad.layer = 30;
            var mesh = Object.Instantiate(quad.GetComponent<MeshFilter>().sharedMesh);
            mesh.colors = Enumerable.Repeat(new Color(1, 1, 1, .6f), mesh.vertexCount).ToArray();
            quad.GetComponent<MeshFilter>().sharedMesh = mesh;
            var material = CozyParticleMaterial.Create(Texture2D.whiteTexture, 0);
            material.SetFloat("_SoftDistance", .25f);
            Assert.AreEqual("QuietCamp/CozyParticle", material.shader.name);
            Assert.IsTrue(material.shader.isSupported);
            quad.GetComponent<Renderer>().sharedMaterial = material;
            var target = new RenderTexture(128, 128, 24); camera.targetTexture = target;
            try
            {
                foreach (bool ortho in new[] { true, false })
                {
                    camera.orthographic = ortho;
                    quad.SetActive(false); yield return null; camera.Render();
                    var reference = Read(target); float ground = reference.GetPixel(64, 64).r; Object.Destroy(reference);
                    quad.SetActive(true); CozyParticleMaterial.ApplyTier(material, 0); camera.Render();
                    var hard = Read(target); float hardValue = hard.GetPixel(64, 64).r;
                    CozyParticleMaterial.ApplyTier(material, 1); camera.Render();
                    var soft = Read(target); float softValue = soft.GetPixel(64, 64).r;
                    Save(hard, "surface-hard-" + (ortho ? "ortho" : "perspective"));
                    Save(soft, "surface-soft-" + (ortho ? "ortho" : "perspective"));
                    Object.Destroy(hard); Object.Destroy(soft);
                    Assert.Greater(hardValue - ground, .15f, "The Low material must draw the particle.");
                    Assert.Greater(softValue - ground, .01f, "Surface fade incorrectly removed the whole particle.");
                    Assert.Less(softValue - ground, (hardValue - ground) * .6f, "Depth fade did not soften the intersection.");
                }
            }
            finally
            {
                camera.targetTexture = null; target.Release(); Object.Destroy(target);
                Object.Destroy(root); Object.Destroy(mesh); Object.Destroy(material); Object.Destroy(backdrop);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator RainActuallyFallsAndClearsWhenMotionIsReduced()
        {
            var root = new GameObject("rain-motion-regression");
            var weather = root.AddComponent<CampWeather>();
            weather.Configure(null, new QuietCamp.Domain.LevelData { width = 5, height = 5, decorSeed = 0 }, false);
            weather.Advance(78, 2, false, Vector2.right);
            for (int i = 0; i < 20; i++) weather.Advance(.05f, 2, false, Vector2.right);
            var rain = root.transform.Find("GentleRain").GetComponent<ParticleSystem>();
            var particles = new ParticleSystem.Particle[96];
            Assert.IsTrue(rain.isPlaying, "Manual rain emission must also start simulation.");
            int initialCount=rain.GetParticles(particles);Assert.Greater(initialCount, 0);
            var before = particles.Take(initialCount).First(p=>p.remainingLifetime>.3f);
            rain.Simulate(.2f, false, false); int count = rain.GetParticles(particles);
            var after = particles.Take(count).Single(p => p.randomSeed == before.randomSeed);
            Assert.Less(after.position.y, before.position.y - .5f, "Rain remained suspended.");
            Assert.Greater(after.position.x, before.position.x, "Rain must follow the wind.");
            weather.Advance(0, 0, true, Vector2.zero);
            Assert.IsTrue(root.GetComponentsInChildren<ParticleSystem>().All(p => p.particleCount == 0));
            Object.Destroy(root); yield return null;
        }

        [UnityTest]
        public IEnumerator FeedbackFollowsChangingWindAndReleasesBothMaterials()
        {
            var root = new GameObject("feedback-wind"); int tier = 2; var wind = Vector2.right;
            var feedback = root.AddComponent<CampFeedbackEffects>(); feedback.Configure(tier, () => false, 1918);
            feedback.FollowQuality(() => tier); feedback.FollowWind(() => wind);
            feedback.Placement(Vector3.zero); feedback.Complete(Vector3.zero);
            yield return null;
            var pools = root.GetComponentsInChildren<ParticleSystem>();
            Assert.IsTrue(pools.All(p => p.velocityOverLifetime.x.constant > 0 && p.velocityOverLifetime.z.constant == 0));
            wind = Vector2.down; tier = 0; yield return null;
            Assert.IsTrue(pools.All(p => p.velocityOverLifetime.x.constant == 0 && p.velocityOverLifetime.z.constant < 0));
            var materials = root.GetComponentsInChildren<ParticleSystemRenderer>().Select(p => p.sharedMaterial).Distinct().ToArray();
            Assert.AreEqual(2, materials.Length);
            Assert.IsTrue(materials.All(m => m.GetFloat("_SoftDepth") == 0), "Low must disable depth sampling.");
            var atlas = materials[0].mainTexture;
            Object.Destroy(root); yield return null;
            Assert.IsTrue(materials.All(m => m == null), "A normal or glowing particle material leaked.");
            Assert.IsTrue(atlas == null);
        }

        static Texture2D Read(Texture source)
        {
            var previous = RenderTexture.active;
            var target = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, target); RenderTexture.active = target;
            var result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0); result.Apply();
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target); return result;
        }
        static void Save(Texture2D image, string name)
        {
            var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../Screenshots/Particles"));
            Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
        }
    }
}
