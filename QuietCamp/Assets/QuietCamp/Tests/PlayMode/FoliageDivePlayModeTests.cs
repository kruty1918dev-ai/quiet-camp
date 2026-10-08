using System.Collections;
using System.Threading.Tasks;
using Kruty1918.Audio;
using Kruty1918.InputRouting.API;
using NUnit.Framework;
using QuietCamp.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    /// <summary>
    /// Regression coverage for the foliage-dive transition contract
    /// (prompt 04): recovery must not resurrect the covered wait, the reveal
    /// must drive the same normalized progress the leaves read, and the
    /// canonical input policy must gate gameplay pointers for the whole dive.
    /// </summary>
    public class FoliageDivePlayModeTests
    {
        [SetUp]
        public void GuardPerformanceQaStorage()
        {
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("QcPerf.Active", false)
                && !UnityEngine.Application.productName.StartsWith("QuietCampPerfQA"))
                Assert.Ignore("Performance driver must enter Play Mode with its isolated QA identity.");
#endif
        }

        [UnityTearDown]
        public IEnumerator ReleaseBootstrap()
        {
            foreach (var boot in Object.FindObjectsByType<QuietCampBootstrap>(FindObjectsSortMode.None))
                Object.Destroy(boot.gameObject);
            var dive = Object.FindFirstObjectByType<FoliageDiveTransition>();
            if (dive != null) dive.Recover();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Curtain_SubmitsVisibleMeshToCanvas()
        {
            var dive = FoliageDiveTransition.Ensure(null);
            try
            {
                var cover = dive.CoverAsync();
                var leaves = dive.transform.Find("Cover/Leaves");
                // Inspect the component directly: Graphic.canvasRenderer can
                // lazily add it, masking a missing runtime rendering dependency.
                var renderer = leaves.GetComponent<CanvasRenderer>();
                Assert.IsNotNull(renderer, "Leaves need a renderer when the overlay is created.");
                yield return Wait(cover);
                Canvas.ForceUpdateCanvases();

                var rect = leaves.GetComponent<RectTransform>().rect;
                Assert.Greater(rect.width, 0f);
                Assert.Greater(rect.height, 0f);
                Assert.IsFalse(renderer.cull);
                Assert.Greater(renderer.materialCount, 0);
                Assert.IsNotNull(renderer.GetMaterial());
                var mesh = renderer.GetMesh();
                Assert.IsNotNull(mesh);
                Assert.Greater(mesh.vertexCount, 0,
                    "The normal Canvas rebuild must submit leaves, not only generate preview geometry.");
            }
            finally
            {
                dive.Recover();
                Object.Destroy(dive.gameObject);
            }
        }

        [UnityTest]
        public IEnumerator Recover_StopsTransitionSoundWithoutStoppingWorldRustle()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            yield return null; yield return null;
            var dive = Dive();
            var services = Services();
            bool reduced = services.ReducedMotion;
            services.ReducedMotion = false;
            try
            {
                var cover = dive.CoverAsync();
                AudioSource transition = null;
                float deadline = Time.realtimeSinceStartup + 2f;
                var clip = services.Audio.GetSound("sfx.transition.in").Clip;
                while (transition == null && Time.realtimeSinceStartup < deadline)
                {
                    foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                        if (source.clip == clip && source.isPlaying) transition = source;
                    yield return null;
                }
                Assert.IsNotNull(transition, "The entry cue must follow visible leaf motion.");
                Assert.AreEqual(0f, transition.spatialBlend);
                Assert.LessOrEqual(Mathf.Abs(transition.panStereo), .15f);
                var world = services.Audio.Play("sfx.rustle", new AudioPlayOptions(volumeScale: .1f));
                Assert.IsTrue(world.IsPlaying);
                dive.Recover();
                Assert.IsFalse(transition.isPlaying, "Recovery must release its own sound scope.");
                Assert.IsTrue(world.IsPlaying, "Recovery must preserve independent world sounds.");
                world.Stop();
                yield return Wait(cover);
            }
            finally { services.ReducedMotion = reduced; dive.Recover(); }
        }

        [UnityTest]
        public IEnumerator RecoverDuringCover_NeverEntersCoveredLoading()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            yield return null; yield return null;
            var dive = Dive();
            Assert.IsNotNull(dive);

            var cover = dive.CoverAsync();
            // Let the dive begin, then force the error path mid-cover.
            yield return null;
            Assert.AreEqual(FoliageDiveTransition.State.Covering, dive.Current);
            dive.Recover();
            yield return Wait(cover);

            // The covered-wait state must not resurrect after cancellation.
            for (int i = 0; i < 10; i++)
            {
                Assert.AreNotEqual(FoliageDiveTransition.State.CoveredLoading, dive.Current,
                    "CoverAsync resurrected CoveredLoading after Recover.");
                yield return null;
            }
            Assert.AreEqual(FoliageDiveTransition.State.Idle, dive.Current);
        }

        [UnityTest]
        public IEnumerator Reveal_RetractsLeavesAndRestoresInput()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            yield return null; yield return null;
            var dive = Dive();
            var services = Services();
            Assert.IsNotNull(dive);
            Assert.IsNotNull(services);
            bool previousMotion = services.ReducedMotion;
            services.ReducedMotion = false;
            try
            {

            // Probe the global transition lease outside UI hit regions. A
            // fixed in-screen coordinate can land on a menu button as the
            // Game View size changes between visual tests.
            var probe = new Vector2(-100f, -100f);

            yield return Wait(dive.CoverAsync());
            Assert.AreEqual(FoliageDiveTransition.State.CoveredLoading, dive.Current);

            // Under the opaque cover gameplay pointers are policy-blocked.
            Assert.IsFalse(services.InputPolicy.CanProcess(
                GameplayInputKind.Placement, probe),
                "Placement input must be policy-gated while covered, not only by raycast.");

            var canopy = dive.transform.Find("Cover/Leaves").GetComponent<LeafCurtainGraphic>();
            Assert.IsNotNull(canopy);
            Assert.AreEqual(1f, canopy.Travel, .001f, "Canopy must fully cover loading.");
            var cameraPosition = Camera.main.transform.position;

            dive.BeginReveal();
            var reveal = dive.RevealAsync();

            // The curtain continues forward rather than reversing or fading.
            bool sawRetract = false;
            while (!reveal.IsCompleted)
            {
                if (canopy.Travel > 1.2f) sawRetract = true;
                Assert.AreEqual(cameraPosition, Camera.main.transform.position,
                    "A screen-space wipe must not move the scene camera.");
                yield return null;
            }
            yield return Wait(reveal);
            // Pointer raycasts are cached for one frame by the input policy.
            yield return null;
            Assert.IsTrue(sawRetract, "Reveal never retracted the leaf overlay.");
            Assert.AreEqual(FoliageDiveTransition.State.Idle, dive.Current);
            Assert.IsTrue(services.InputPolicy.CanProcess(
                GameplayInputKind.Placement, probe),
                "Input policy block must be released after the transition.");
            }
            finally { services.ReducedMotion = previousMotion; }
        }

        [UnityTest]
        public IEnumerator ReducedMotion_HidesLeavesAndKeepsCamera()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            yield return null; yield return null;
            var services = Services();
            Assert.IsNotNull(services);
            bool prev = services.ReducedMotion;
            services.ReducedMotion = true;
            try
            {
                var dive = Dive();
                yield return Wait(dive.CoverAsync());
                var leaves = dive.transform.Find("Cover/Leaves");
                Assert.IsNotNull(leaves);
                Assert.IsFalse(leaves.gameObject.activeSelf,
                    "Reduced motion must not animate flying leaves.");
                dive.BeginReveal();
                yield return Wait(dive.RevealAsync());
                Assert.AreEqual(FoliageDiveTransition.State.Idle, dive.Current);
            }
            finally { services.ReducedMotion = prev; }
        }

        static IEnumerator Wait(Task task)
        {
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!task.IsCompleted) Assert.Fail("Transition task never completed.");
            if (task.IsFaulted) Assert.Fail(task.Exception?.GetBaseException().Message);
        }

        static FoliageDiveTransition Dive()
        {
            var router = Router();
            return router?.Dive;
        }

        static ScreenRouter Router()
        {
            var boot = Object.FindFirstObjectByType<QuietCampBootstrap>();
            if (boot == null) return null;
            var field = typeof(QuietCampBootstrap).GetField("_router",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(boot) as ScreenRouter;
        }

        static GameServices Services()
        {
            var boot = Object.FindFirstObjectByType<QuietCampBootstrap>();
            if (boot == null) return null;
            var field = typeof(QuietCampBootstrap).GetField("_services",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(boot) as GameServices;
        }
    }
}
