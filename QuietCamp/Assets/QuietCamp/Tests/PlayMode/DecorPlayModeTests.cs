using System.Collections;
using NUnit.Framework;
using QuietCamp.Presentation.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace QuietCamp.Tests
{
    /// <summary>Camp diorama must spawn the decor ring plus the meadow plate
    /// that grounds it — the forest and playable field share one plane.</summary>
    public class DecorPlayModeTests
    {
        [UnityTest]
        public IEnumerator CampScene_SpawnsMeadowAndDecor()
        {
            yield return SceneManager.LoadSceneAsync("Boot");
            yield return PrivacyBootTestSupport.EnterGame();
            yield return null;
            yield return SceneManager.LoadSceneAsync("Camp");
            yield return null; yield return null;

            var decor = FindRoot("DecorRoot");
            Assert.IsNotNull(decor, "Camp scene needs DecorRoot");
            Assert.Greater(decor.childCount, 20,
                $"Expected a decor ring, got {decor.childCount} children");

            // The decor ring stands on a shared meadow plate so the world
            // reads as one field instead of floating over the backdrop.
            var meadow = decor.Find("Meadow");
            Assert.IsNotNull(meadow, "Meadow apron must ground the decor ring");
            var renderer = meadow.GetComponent<MeshRenderer>();
            Assert.IsNotNull(renderer);
            Assert.IsTrue(renderer.sharedMaterial.shader.isSupported, "Meadow shader is unsupported");
            Assert.IsNull(meadow.GetComponent<Collider>(), "Decor ground must not intercept board input");
            var mesh = meadow.GetComponent<MeshFilter>().sharedMesh;
            Assert.Greater(mesh.bounds.size.x, 40f, "Ground must continue past the camp clearing");
            Assert.AreEqual((int)UnityEngine.Rendering.RenderQueue.Geometry, renderer.sharedMaterial.renderQueue);
            Assert.IsTrue(renderer.receiveShadows, "Trees and the board must cast shadows onto the same ground");
            foreach (var vertex in mesh.vertices)
                Assert.AreEqual(MeadowSurface.GroundY, vertex.y, .0001f, "Ground must stay on the board's XZ plane");
            var forest = decor.Find("ForestSurround");
            Assert.IsNotNull(forest);
            Assert.Greater(forest.childCount, 30);
            Assert.IsEmpty(forest.GetComponentsInChildren<Collider>());
            foreach (Transform tree in forest)
                Assert.AreEqual(0f, tree.localPosition.y, .0001f, "Forest trees must be planted at board height");

            // Full-screen scenery replaces the old cropped skybox.
            var cam = Camera.main;
            Assert.IsNotNull(cam, "Main camera missing");
            Assert.AreEqual(CameraClearFlags.SolidColor, cam.clearFlags);
            Assert.AreEqual(new Rect(0, 0, 1, 1), cam.rect);
            Assert.IsNull(cam.transform.Find("ForestBackdrop"));
            Assert.IsNull(cam.transform.Find("NearFoliage"));
        }

        static Transform FindRoot(string name)
        {
            var scene = SceneManager.GetActiveScene();
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = FindDeep(root.transform, name);
                if (t != null) return t;
            }
            return null;
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (var i = 0; i < t.childCount; i++)
            {
                var c = FindDeep(t.GetChild(i), name);
                if (c != null) return c;
            }
            return null;
        }
    }
}
