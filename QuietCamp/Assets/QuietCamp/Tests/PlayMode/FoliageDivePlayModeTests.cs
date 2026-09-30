using System.Collections;
using System.Threading.Tasks;
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
        public IEnumerator RecoverDuringCover_NeverEntersCoveredLoading()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
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
            yield return null; yield return null;
            var dive = Dive();
            var services = Services();
            Assert.IsNotNull(dive);
            Assert.IsNotNull(services);

            yield return Wait(dive.CoverAsync());
            Assert.AreEqual(FoliageDiveTransition.State.CoveredLoading, dive.Current);

            // Under the opaque cover gameplay pointers are policy-blocked.
            Assert.IsFalse(services.InputPolicy.CanProcess(
                GameplayInputKind.Placement, new Vector2(500f, 500f)),
                "Placement input must be policy-gated while covered, not only by raycast.");

            // Leaves sit closed over the frame at full progress.
            var leaf = dive.transform.Find("Cover/Leaves/Leaf_0") as RectTransform;
            Assert.IsNotNull(leaf);
            float coveredAlpha = leaf.GetComponent<UnityEngine.UI.Image>().color.a;
            Assert.Greater(coveredAlpha, .9f, "leaf must be opaque when covered");

            dive.BeginReveal();
            var reveal = dive.RevealAsync();

            // Mid-reveal the same progress drives leaves back out.
            bool sawRetract = false;
            while (!reveal.IsCompleted)
            {
                if (leaf.GetComponent<UnityEngine.UI.Image>().color.a < coveredAlpha - .2f)
                    sawRetract = true;
                yield return null;
            }
            yield return Wait(reveal);
            Assert.IsTrue(sawRetract, "Reveal never retracted the leaf overlay.");
            Assert.AreEqual(FoliageDiveTransition.State.Idle, dive.Current);
            Assert.IsTrue(services.InputPolicy.CanProcess(
                GameplayInputKind.Placement, new Vector2(500f, 500f)),
                "Input policy block must be released after the transition.");
        }

        [UnityTest]
        public IEnumerator ReducedMotion_HidesLeavesAndKeepsCamera()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
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
