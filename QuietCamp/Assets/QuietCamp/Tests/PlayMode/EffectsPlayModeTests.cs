using System.Collections;
using System.IO;
using System.Reflection;
using Kruty1918.Haptics;
using NUnit.Framework;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using QuietCamp.Application;
using QuietCamp.Domain;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace QuietCamp.Tests
{
    public class EffectsPlayModeTests
    {
        [UnityTest]
        public IEnumerator CampActionsEmitFeedbackAndCompletionAfterglow()
        {
            int previousQuality = QualitySettings.GetQualityLevel();
            QualitySettings.SetQualityLevel(2);
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            for (int i = 0; i < 120 && SceneManager.GetActiveScene().name != "MainMenu"; i++) yield return null;
            var boot = Object.FindAnyObjectByType<QuietCampBootstrap>();
            Assert.IsNotNull(boot);
            var services = (GameServices)typeof(QuietCampBootstrap).GetField("_services",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(boot);
            services.Settings.reducedMotion = false;
            services.Settings.quality = 3;
            services.PendingLevelId = "QC001";
            try
            {
                yield return SceneManager.LoadSceneAsync("Camp");
                for (int i = 0; i < 120 && (CampSceneHost.Current == null || !CampSceneHost.Current.IsReady); i++) yield return null;
                var host = CampSceneHost.Current;
                Assert.IsTrue(host != null && host.IsReady);
                var feedback = host.GetComponent<CampFeedbackEffects>();
                var post = host.GetComponent<PhasePostFx>();
                host.Session.Restore(System.Array.Empty<Placement>(), null);
                var pose = host.Session.Level.witness[0];
                Assert.IsTrue(host.Session.TryCommit(PlacementCommand.Place(pose.guestId, pose.x, pose.z, pose.rotation), out _));
                Assert.Greater(feedback.ActiveParticles, 0, "A real board commit must trigger particles.");
                yield return new WaitForSecondsRealtime(.15f);
                Capture(Camera.main, host.Atmosphere, "placement");
                host.Session.DebugApplyWitness();
                Assert.IsTrue(host.Session.Check().IsSolved);
                Assert.Greater(feedback.ActiveParticles, 12, "Completion must trigger the golden burst.");
                yield return new WaitForSecondsRealtime(.3f);
                Assert.IsTrue(post.RuntimeProfile.TryGet<ColorAdjustments>(out var color));
                Assert.Less(color.colorFilter.value.b, 1f, "Completion afterglow never reached the post profile.");
                Capture(Camera.main, host.Atmosphere, "completion");
                services.ReducedMotion = true;
                yield return null;
                Assert.AreEqual(0, feedback.ActiveParticles);
                Assert.AreEqual(Color.white, color.colorFilter.value);
            }
            finally
            {
                Object.Destroy(boot.gameObject);
                QualitySettings.SetQualityLevel(previousQuality);
            }
            yield return null;
            yield return SceneManager.LoadSceneAsync("MainMenu");
        }

        static void Capture(Camera camera, CampAtmosphere atmosphere, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var folder = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../../effects-screenshots"));
            Directory.CreateDirectory(folder);
            var previous = camera.targetTexture;
            var active = RenderTexture.active;
            var target = new RenderTexture(768, 1366, 24);
            var texture = new Texture2D(768, 1366, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                atmosphere.RefreshLayout();
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 768, 1366), 0, 0);
                texture.Apply();
                var pixels = texture.GetPixels32();
                int magenta = 0;
                for (int i = 0; i < pixels.Length; i += 17)
                    if (pixels[i].r > 180 && pixels[i].b > 180 && pixels[i].g < 90) magenta++;
                Assert.Less((float)magenta / (pixels.Length / 17), .005f, "An effect rendered with a missing shader.");
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(texture);
                atmosphere.RefreshLayout();
            }
        }

        [UnityTest]
        public IEnumerator ParticlePoolsRespectBudgetsReducedMotionAndReleaseResources()
        {
            bool reduced = false;
            var root = new GameObject("feedback-test");
            var effects = root.AddComponent<CampFeedbackEffects>();
            var randomBefore = JsonUtility.ToJson(Random.state);
            effects.Configure(2, () => reduced, 1918);
            effects.Placement(Vector3.zero);
            effects.Complete(Vector3.zero);
            Assert.Greater(effects.ActiveParticles, 0);
            Assert.LessOrEqual(effects.ActiveParticles, 112);
            Assert.AreEqual(randomBefore, JsonUtility.ToJson(Random.state), "Feedback changed gameplay RNG.");
            var renderer = root.GetComponentInChildren<ParticleSystemRenderer>();
            Assert.IsNotNull(renderer.sharedMaterial);
            Assert.AreEqual("QuietCamp/CozyParticle", renderer.sharedMaterial.shader.name);
            Assert.IsTrue(renderer.sharedMaterial.shader.isSupported);
            Assert.AreEqual(1, renderer.sharedMaterial.GetFloat("_SoftDepth"));
            var material = renderer.sharedMaterial;
            var texture = material.mainTexture;
            reduced = true;
            yield return null;
            Assert.AreEqual(0, effects.ActiveParticles, "Existing particles must clear when reduced motion is enabled.");
            effects.Complete(Vector3.zero);
            effects.Placement(Vector3.zero);
            Assert.AreEqual(0, effects.ActiveParticles);
            Object.Destroy(root);
            yield return null;
            Assert.IsTrue(material == null, "Runtime material leaked.");
            Assert.IsTrue(texture == null, "Runtime texture leaked.");
        }

        [UnityTest]
        public IEnumerator QualityTiersShareGradingAndScaleOnlyBloomSampling()
        {
            var root = new GameObject("post-test");
            var camera = root.AddComponent<Camera>();
            camera.allowHDR = false;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var post = root.AddComponent<PhasePostFx>();
            post.Configure(camera, 0);
            var phase = AtmosphereCatalog.Load().Get("night");
            post.Apply(phase, 0);
            Assert.IsTrue(camera.GetUniversalAdditionalCameraData().renderPostProcessing);
            Assert.IsTrue(camera.allowHDR);
            Assert.IsTrue(post.RuntimeProfile.TryGet<Bloom>(out var lowBloom));
            Assert.AreEqual(1,lowBloom.maxIterations.value);Assert.IsTrue(lowBloom.active);
            float intensity=lowBloom.intensity.value;
            post.Apply(phase, 1);
            Assert.IsTrue(camera.GetUniversalAdditionalCameraData().renderPostProcessing);
            Assert.IsTrue(camera.allowHDR);
            Assert.IsTrue(post.RuntimeProfile.TryGet<Vignette>(out var vignette));
            Assert.Greater(vignette.intensity.value, 0);
            Assert.IsTrue(post.RuntimeProfile.TryGet<Bloom>(out var bloom));
            Assert.IsTrue(bloom.active);
            Assert.AreEqual(2,bloom.maxIterations.value,"Balanced limits the quarter-resolution bloom pyramid.");
            Assert.AreEqual(intensity,bloom.intensity.value,"The same phase must retain its light response at every quality");
            post.Apply(phase, 2);
            Assert.IsTrue(camera.allowHDR);
            Assert.IsTrue(bloom.active);
            Assert.AreEqual(4,bloom.maxIterations.value);
            Assert.AreEqual(intensity,bloom.intensity.value);
            Assert.Greater(bloom.intensity.value, 0);
            var profile = post.RuntimeProfile;
            Object.Destroy(post);
            yield return null;
            Assert.IsTrue(profile == null);
            Assert.IsFalse(camera.allowHDR, "The component must restore camera configuration on disposal.");
            Assert.IsFalse(camera.GetUniversalAdditionalCameraData().renderPostProcessing);
            Object.Destroy(root);
            yield return null;
        }

        sealed class RecordingHaptics : IHapticsBackend
        {
            public bool IsSupported => true;
            public int Plays, Cancels;
            public void Play(HapticCue cue) => Plays++;
            public void Cancel() => Cancels++;
            public void Dispose() { }
        }

        [Test]
        public void SavedHapticsPreferenceAppliesImmediatelyToGameplayFeedback()
        {
            var device = new RecordingHaptics();
            var haptics = new HapticsService(device, () => 1);
            var save = new SaveAdapter();
            save.Settings.haptics = false;
            using (var services = new GameServices(save, null, null, null, null,
                null, null, null, null, null, null, null, null, null, null, null, haptics: haptics))
            {
                services.PlayHaptic(HapticCue.Confirm);
                Assert.AreEqual(0, device.Plays);
                services.HapticsEnabled = true;
                services.PlayHaptic(HapticCue.Confirm);
                Assert.AreEqual(1, device.Plays);
                services.HapticsEnabled = false;
                services.PlayHaptic(HapticCue.Success);
                Assert.AreEqual(1, device.Plays);
                Assert.GreaterOrEqual(device.Cancels, 2);
            }
        }
    }
}
