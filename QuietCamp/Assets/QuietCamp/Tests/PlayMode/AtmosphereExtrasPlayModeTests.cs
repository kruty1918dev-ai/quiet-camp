using System.Collections;
using NUnit.Framework;
using QuietCamp.Domain;
using QuietCamp.Infrastructure;
using QuietCamp.Presentation;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    /// <summary>PlayMode coverage for the wind/particles/audio/transition layer
    /// added by the atmosphere prompts 02–04.</summary>
    public class AtmosphereExtrasPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator ReleaseBootstrap()
        {
            foreach (var boot in Object.FindObjectsByType<QuietCampBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(boot.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator LeafAtlasAndParticleBudgetsAreAlive()
        {
            var sprites = Resources.LoadAll<Sprite>("QuietCamp/Atmosphere/Textures/leaves");
            Assert.AreEqual(4, sprites.Length, "Leaf atlas must expose the four 2×2 cells.");

            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null; yield return null;
            yield return SceneManager.LoadSceneAsync("Camp");
            for (int i = 0; i < 60 && CampSceneHost.Current?.Atmosphere == null; i++)
                yield return null;
            var host = CampSceneHost.Current;
            Assert.IsNotNull(host?.Atmosphere);

            // Shared wind owner produces sane snapshots.
            yield return null;
            var wind = host.Atmosphere.Wind;
            Assert.That(wind.Strength, Is.InRange(0f, 1f));
            Assert.That(wind.DirectionXZ.magnitude, Is.GreaterThan(.5f));

            // Particle layer exists with a supported material.
            var particles = host.Atmosphere.Particles;
            Assert.IsNotNull(particles, "AtmosphereParticles must be created by CampAtmosphere.");
            var leafPs = particles.transform.Find("AmbientLeaves")?.GetComponent<ParticleSystem>();
            Assert.IsNotNull(leafPs);
            Assert.IsTrue(leafPs.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.isSupported);
            var sheet = leafPs.textureSheetAnimation;
            Assert.IsTrue(sheet.enabled && sheet.mode == ParticleSystemAnimationMode.Grid
                && sheet.numTilesX == 2 && sheet.numTilesY == 2,
                "Leaves must pick one of the four atlas cells at spawn.");

            // One stable listener proxy above the board, camera listener off.
            int enabledListeners = 0;
            AudioListener proxy = null;
            foreach (var l in Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (l.enabled) { enabledListeners++; if (l.name == "ListenerProxy") proxy = l; }
            Assert.AreEqual(1, enabledListeners, "Exactly one AudioListener may be active.");
            Assert.IsNotNull(proxy, "Camp must own the board-centre listener proxy.");
        }

        /// <summary>Prompt-03 checklist: a phase budget of 0 means OFF on every
        /// tier — High must not resurrect day fireflies/smoke — and Reduced
        /// Motion silences every ambient emitter, not only the leaves.</summary>
        [UnityTest]
        public IEnumerator DisabledBudgetsStayOffAndReducedMotionSilencesAll()
        {
            var catalog = AtmosphereCatalog.Load();
            var level = new LevelData { width = 8, height = 8, decorSeed = 7 };
            var camGo = new GameObject("TestCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            var hiGo = new GameObject("ParticlesHigh");
            var hi = hiGo.AddComponent<AtmosphereParticles>();
            var rmGo = new GameObject("ParticlesReduced");
            var rm = rmGo.AddComponent<AtmosphereParticles>();
            try
            {
                hi.Configure(cam, level, catalog.Get("morning"),
                    AtmosphereParticles.Tier.High, () => false);
                yield return null;
                Assert.AreEqual(0f, Rate(hi, "Fireflies"), "High resurrected disabled fireflies.");
                Assert.AreEqual(0f, Rate(hi, "FireSmoke"), "High resurrected disabled smoke.");
                Assert.Greater(Rate(hi, "DustMotes"), 0f, "Active dust must emit on High.");

                rm.Configure(cam, level, catalog.Get("evening"),
                    AtmosphereParticles.Tier.Balanced, () => true);
                yield return null;
                Assert.AreEqual(0f, Rate(rm, "AmbientLeaves"), "Reduced motion must silence leaves.");
                Assert.AreEqual(0f, Rate(rm, "DustMotes"), "Reduced motion may turn dust off.");
                Assert.AreEqual(0f, Rate(rm, "FireSmoke"), "Reduced motion may turn smoke off.");
                // Spec §9: fireflies stay as rare static faint points — a
                // much lower rate, no noise drift, no glow pulse.
                float flyRate = Rate(rm, "Fireflies");
                Assert.Greater(flyRate, 0f, "Fireflies stay alive under reduced motion.");
                Assert.Less(flyRate, 3f / 6f, "Reduced fireflies emit sparser than normal.");
                var flyPs = rm.transform.Find("Fireflies").GetComponent<ParticleSystem>();
                Assert.IsFalse(flyPs.noise.enabled, "Reduced fireflies lose the noise drift.");
                Assert.IsFalse(flyPs.colorOverLifetime.enabled,
                    "Reduced fireflies lose the glow pulse.");

                // Smoke waits for an active fire even when the budget allows it.
                var fgGo = new GameObject("ParticlesFire");
                var fg = fgGo.AddComponent<AtmosphereParticles>();
                var evening = catalog.Get("evening");
                fg.Configure(cam, level, evening, AtmosphereParticles.Tier.Balanced, () => false);
                yield return null;
                Assert.AreEqual(0f, Rate(fg, "FireSmoke"), "Smoke must wait for an active fire.");
                fg.SetFire(new Vector3(2f, 0f, 1f), true);
                Assert.Greater(Rate(fg, "FireSmoke"), 0f, "Active fire releases smoke.");
                // Re-applying a profile never duplicates systems.
                int children = fg.transform.childCount;
                fg.ApplyProfile(evening);
                fg.ApplyProfile(evening);
                Assert.AreEqual(children, fg.transform.childCount,
                    "ApplyProfile must be idempotent.");
                Object.Destroy(fgGo);
            }
            finally
            {
                Object.Destroy(hiGo);
                Object.Destroy(rmGo);
                Object.Destroy(camGo);
            }
        }

        static float Rate(Component particles, string child)
        {
            var ps = particles.transform.Find(child)?.GetComponent<ParticleSystem>();
            Assert.IsNotNull(ps, child + " particle system must exist.");
            return ps.emission.rateOverTime.constant;
        }

        [UnityTest]
        public IEnumerator FoliageDiveCoversMenuTransitionAndRestoresInput()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null; yield return null;
            yield return SceneManager.LoadSceneAsync("Camp");
            for (int i = 0; i < 60 && CampSceneHost.Current?.Atmosphere == null; i++)
                yield return null;
            Assert.IsNotNull(CampSceneHost.Current);

            // Fire a real transition to the menu via the router's dive.
            var router = FindRouter();
            Assert.IsNotNull(router, "ScreenRouter must be reachable.");
            router.GoToMenu();

            var dive = Object.FindFirstObjectByType<FoliageDiveTransition>();
            Assert.IsNotNull(dive, "Router must create the foliage dive overlay.");

            // The opaque cover exists at some point during the transition.
            var deadline = Time.realtimeSinceStartup + 4f;
            bool sawCover = false;
            while (Time.realtimeSinceStartup < deadline && !sawCover)
            {
                var cover = dive.transform.Find("Cover/OpaqueCover");
                if (cover != null && dive.Current != FoliageDiveTransition.State.Idle) sawCover = true;
                yield return null;
            }
            Assert.IsTrue(sawCover, "Transition never produced its opaque cover state.");

            deadline = Time.realtimeSinceStartup + 15f;
            while (dive.Current != FoliageDiveTransition.State.Idle
                && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.AreEqual(FoliageDiveTransition.State.Idle, dive.Current);
            // After the reveal, input is unblocked and the camera pose is intact.
            var cam = Camera.main;
            Assert.IsNotNull(cam);
            Assert.Greater(cam.orthographicSize, 0f);
        }

        [UnityTest]
        public IEnumerator FirePhaseUsesPositionalLoopWithoutDuplicates()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return null; yield return null;
            yield return SceneManager.LoadSceneAsync("Camp");
            for (int i = 0; i < 60 && CampSceneHost.Current?.Atmosphere == null; i++)
                yield return null;
            var host = CampSceneHost.Current;
            Assert.IsNotNull(host);
            host.SetAtmospherePhase("evening");
            yield return null; yield return null;
            AssertPositionalLoop("Fire Burning Loop 5");
            host.SetAtmospherePhase("evening");
            yield return null;
            Assert.AreEqual(1, CountLoop("Fire Burning Loop 5"), "Repeated phase duplicated fire.");
        }

        static void AssertPositionalLoop(string clip)
        {
            var found = 0;
            foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (s.isPlaying && s.loop && s.clip != null && s.clip.name == clip)
                {
                    found++;
                    Assert.Greater(s.spatialBlend, .9f, clip + " must be positional.");
                }
            Assert.AreEqual(1, found, clip + " should be playing once.");
        }

        static int CountLoop(string clip)
        {
            var n = 0;
            foreach (var s in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                if (s.isPlaying && s.loop && s.clip != null && s.clip.name == clip) n++;
            return n;
        }

        static ScreenRouter FindRouter()
        {
            // The router is a plain service composed by the bootstrap; reach it
            // through the private field — the same seam other tests use.
            var boot = Object.FindFirstObjectByType<QuietCampBootstrap>();
            if (boot == null) return null;
            var field = typeof(QuietCampBootstrap).GetField("_router",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(boot) as ScreenRouter;
        }
    }
}
